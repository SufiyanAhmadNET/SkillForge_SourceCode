using Microsoft.EntityFrameworkCore;
using Razorpay.Api;
using SkillForge.Data;
using SkillForge.Models;
using System.Security.Cryptography;
using System.Text;
using SkillForge.Interfaces;

namespace SkillForge.Services.Payments
{
    public class EnrollmentService : IEnrollmentService
    {
        private readonly SkillForgeDbContext _context;
        private readonly IConfiguration _config;
        private readonly string _keyId;
        private readonly string _keySecret;

        public string RazorpayKeyId => _keyId;

        public EnrollmentService(SkillForgeDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
            
            // Sanitize keys (trim whitespace and all common quote marks)
            _keyId = (_config["Razorpay:KeyId"] ?? string.Empty).Trim().Trim('\"').Trim('\'');
            _keySecret = (_config["Razorpay:KeySecret"] ?? string.Empty).Trim().Trim('\"').Trim('\'');

            if (string.IsNullOrEmpty(_keyId)) throw new Exception("Razorpay KeyId missing");
            if (string.IsNullOrEmpty(_keySecret)) throw new Exception("Razorpay KeySecret missing");
        }

        public EnrollResult CreateOrder(int studentId, int courseId)
        {
            if (courseId == 0) return CreateCartOrder(studentId);
            try
            {
                var existing = _context.Enrollments
                    .FirstOrDefault(e => e.StudentId == studentId && e.CourseId == courseId);
                if (existing != null && existing.Status == EnrollmentStatus.Active)
                    return new EnrollResult { Success = false, Message = "You are already enrolled in this course." };

                var course = _context.Courses
                    .Include(c => c.CourseDetails)
                    .FirstOrDefault(c => c.Id == courseId);
                if (course == null)
                    return new EnrollResult { Success = false, Message = "Course not found." };

                var amount = course.CourseDetails?.Total_Price ?? 0;

                // Handle Free Course Logic ─ directly activate enrollment
                if (amount == 0)
                {
                    Enrollment freeEnrollment;
                    if (existing != null)
                    {
                        freeEnrollment = existing;
                        freeEnrollment.Status = EnrollmentStatus.Active;
                    }
                    else
                    {
                        freeEnrollment = new Enrollment
                        {
                            StudentId = studentId,
                            CourseId = courseId,
                            Status = EnrollmentStatus.Active,
                            EnrolledAt = DateTime.UtcNow
                        };
                        _context.Enrollments.Add(freeEnrollment);
                    }
                    _context.SaveChanges();

                    return new EnrollResult
                    {
                        Success = true,
                        IsFree = true,
                        EnrollmentId = freeEnrollment.Id,
                        CourseTitle = course.Title
                    };
                }

                var amountInPaise = (int)(amount * 100);

                var client = new RazorpayClient(_keyId, _keySecret);
                var options = new Dictionary<string, object>
                {
                    { "amount", amountInPaise },
                    { "currency", "INR" },
                    { "receipt", $"sf_{studentId}_{courseId}_{DateTime.UtcNow.Ticks}" },
                    { "payment_capture", 1 }
                };
                var order = client.Order.Create(options);
                string razorpayOrderId = order["id"].ToString();

                Enrollment enrollment;
                if (existing != null)
                {
                    enrollment = existing;
                    enrollment.Status = EnrollmentStatus.Pending;
                }
                else
                {
                    enrollment = new Enrollment
                    {
                        StudentId = studentId,
                        CourseId = courseId,
                        Status = EnrollmentStatus.Pending
                    };
                    _context.Enrollments.Add(enrollment);
                }
                _context.SaveChanges();

                var payment = _context.Payments
                    .FirstOrDefault(p => p.EnrollmentId == enrollment.Id);
                if (payment == null)
                {
                    payment = new Models.Payment
                    {
                        EnrollmentId = enrollment.Id,
                        RazorpayOrderId = razorpayOrderId,
                        Amount = amount,
                        Status = PaymentStatus.Pending
                    };
                    _context.Payments.Add(payment);
                }
                else
                {
                    payment.RazorpayOrderId = razorpayOrderId;
                    payment.Status = PaymentStatus.Pending;
                }
                _context.SaveChanges();

                var student = _context.Students.Find(studentId);
                var profile = _context.StudentProfiles.FirstOrDefault(p => p.StudentId == studentId);
                return new EnrollResult
                {
                    Success = true,
                    RazorpayOrderId = razorpayOrderId,
                    Amount = amountInPaise,
                    CourseTitle = course.Title,
                    EnrollmentId = enrollment.Id,
                    StudentEmail = student?.Email,
                    StudentMobile = profile?.Mobile,
                    StudentName = profile != null ? $"{profile.FirstName} {profile.LastName}" : "Student"
                };
            }
            catch (Exception ex)
            {
                return new EnrollResult { Success = false, Message = ex.Message };
            }
        }

        public EnrollResult CreateCartOrder(int studentId)
        {
            try
            {
                var cartItems = _context.Carts
                    .Where(c => c.StudentId == studentId)
                    .Include(c => c.Course)
                        .ThenInclude(co => co.CourseDetails)
                    .ToList();
                if (!cartItems.Any())
                    return new EnrollResult { Success = false, Message = "Cart is empty." };

                var totalAmount = cartItems.Sum(c => c.Course.CourseDetails?.Total_Price ?? 0);
                var amountInPaise = (int)(totalAmount * 100);

                var client = new RazorpayClient(_keyId, _keySecret);
                var options = new Dictionary<string, object>
                {
                    { "amount", amountInPaise },
                    { "currency", "INR" },
                    { "receipt", $"cart_{studentId}_{DateTime.UtcNow.Ticks}" },
                    { "payment_capture", 1 }
                };
                var order = client.Order.Create(options);
                string razorpayOrderId = order["id"].ToString();

                foreach (var item in cartItems)
                {
                    var existing = _context.Enrollments
                        .FirstOrDefault(e => e.StudentId == studentId && e.CourseId == item.CourseId);
                    Enrollment enrollment;
                    if (existing != null)
                    {
                        enrollment = existing;
                        if (enrollment.Status != EnrollmentStatus.Active)
                            enrollment.Status = EnrollmentStatus.Pending;
                    }
                    else
                    {
                        enrollment = new Enrollment
                        {
                            StudentId = studentId,
                            CourseId = item.CourseId,
                            Status = EnrollmentStatus.Pending
                        };
                        _context.Enrollments.Add(enrollment);
                    }
                    _context.SaveChanges();

                    var payment = _context.Payments.FirstOrDefault(p => p.EnrollmentId == enrollment.Id);
                    if (payment == null)
                    {
                        payment = new Models.Payment
                        {
                            EnrollmentId = enrollment.Id,
                            RazorpayOrderId = razorpayOrderId,
                            Amount = item.Course.CourseDetails?.Total_Price ?? 0,
                            Status = PaymentStatus.Pending
                        };
                        _context.Payments.Add(payment);
                    }
                    else
                    {
                        payment.RazorpayOrderId = razorpayOrderId;
                        payment.Status = PaymentStatus.Pending;
                    }
                }
                _context.SaveChanges();

                var student = _context.Students.Find(studentId);
                var profile = _context.StudentProfiles.FirstOrDefault(p => p.StudentId == studentId);
                return new EnrollResult
                {
                    Success = true,
                    RazorpayOrderId = razorpayOrderId,
                    Amount = amountInPaise,
                    CourseTitle = $"{cartItems.Count} Courses",
                    StudentEmail = student?.Email,
                    StudentMobile = profile?.Mobile,
                    StudentName = profile != null ? $"{profile.FirstName} {profile.LastName}" : "Student"
                };
            }
            catch (Exception ex)
            {
                return new EnrollResult { Success = false, Message = ex.Message };
            }
        }

        public EnrollResult VerifyPayment(string razorpayOrderId, string razorpayPaymentId, string razorpaySignature)
        {
            try
            {
                var expectedSignature = GenerateSignature(razorpayOrderId, razorpayPaymentId);
                if (expectedSignature != razorpaySignature)
                {
                    MarkPaymentFailed(razorpayOrderId);
                    return new EnrollResult { Success = false, Message = "Payment verification failed. Possible fraud." };
                }

                var payments = _context.Payments
                    .Include(p => p.Enrollment)
                    .Where(p => p.RazorpayOrderId == razorpayOrderId)
                    .ToList();
                if (!payments.Any())
                    return new EnrollResult { Success = false, Message = "Payment record not found." };

                foreach (var payment in payments)
                {
                    payment.RazorpayPaymentId = razorpayPaymentId;
                    payment.RazorpaySignature = razorpaySignature;
                    payment.Status = PaymentStatus.Success;
                    payment.PaidAt = DateTime.UtcNow;
                    payment.Enrollment.Status = EnrollmentStatus.Active;
                }
                _context.SaveChanges();

                var studentId = payments.First().Enrollment.StudentId;
                var courseIds = payments.Select(p => p.Enrollment.CourseId).ToList();
                var cartItems = _context.Carts.Where(c => c.StudentId == studentId && courseIds.Contains(c.CourseId)).ToList();
                if (cartItems.Any())
                {
                    _context.Carts.RemoveRange(cartItems);
                    _context.SaveChanges();
                }
                return new EnrollResult
                {
                    Success = true,
                    Message = "Payment verified. Enrollment active!",
                    EnrollmentId = payments.First().EnrollmentId
                };
            }
            catch (Exception ex)
            {
                return new EnrollResult { Success = false, Message = ex.Message };
            }
        }

        private string GenerateSignature(string orderId, string paymentId)
        {
            var message = $"{orderId}|{paymentId}";
            var keyBytes = Encoding.UTF8.GetBytes(_keySecret);
            var msgBytes = Encoding.UTF8.GetBytes(message);
            using var hmac = new HMACSHA256(keyBytes);
            var hashBytes = hmac.ComputeHash(msgBytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        private void MarkPaymentFailed(string razorpayOrderId)
        {
            var payments = _context.Payments
                .Include(p => p.Enrollment)
                .Where(p => p.RazorpayOrderId == razorpayOrderId)
                .ToList();
            foreach (var payment in payments)
            {
                payment.Status = PaymentStatus.Failed;
                payment.Enrollment.Status = EnrollmentStatus.Failed;
            }
            _context.SaveChanges();
        }

        public bool IsEnrolled(int studentId, int courseId)
        {
            return _context.Enrollments
                .Any(e => e.StudentId == studentId &&
                          e.CourseId == courseId &&
                          e.Status == EnrollmentStatus.Active);
        }
    }
}
