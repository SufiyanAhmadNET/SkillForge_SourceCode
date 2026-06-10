using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillForge.Areas.User.Models;
using SkillForge.Data;
using SkillForge.Models;
using SkillForge.Interfaces;
using System.Security.Claims;

namespace SkillForge.Areas.User.Controllers
{
    [Area("User")]
    public class HomeController : UserBaseController
    {
        private readonly ICourseQueryService _courseQueryService;
        private readonly ICourseProgressService _courseProgressService;
        private readonly IStudentActivityService _studentActivityService;
        private readonly IEnrollmentService _enrollmentService;
        private readonly IConfiguration _config;
        private readonly IAuthService _authService;
        private readonly IOtpService _otpService;
        private readonly ISearchService _searchService;

        public HomeController(ICourseQueryService courseQueryService,
                              ICourseProgressService courseProgressService,
                              IStudentActivityService studentActivityService,
                              IEnrollmentService enrollmentService,
                              IConfiguration config,
                              IAuthService authService,
                              IOtpService otpService,
                              ISearchService searchService)
        {
            _courseQueryService = courseQueryService;
            _courseProgressService = courseProgressService;
            _studentActivityService = studentActivityService;
            _enrollmentService = enrollmentService;
            _config = config;
            _authService = authService;
            _otpService = otpService;
            _searchService = searchService;
        }

        // Search courses for logged in user
        public IActionResult Search(string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return RedirectToAction("Courses");

            var studentId = GetStudentId();
            var result = _searchService.SearchCourses(q, studentId);
            return View(result);
        }

        [Authorize(Roles = "Student")]
        public IActionResult Dashboard()
        {
            var studentId = GetStudentId();
            if (studentId == 0) return RedirectToAction("StudentLogin", "Auth");
            var vm = _studentActivityService.GetStudentDashboard(studentId);
            return View(vm);
        }

        [Authorize(Roles = "Student")]
        public IActionResult Profile()
        {
            var studentId = GetStudentId();
            var dvm = _studentActivityService.GetStudentDashboard(studentId);
            var profile = _context.StudentProfiles.FirstOrDefault(p => p.StudentId == studentId);
            if (profile != null)
            {
                dvm.FirstName = profile.FirstName;
                dvm.LastName = profile.LastName;
                dvm.Mobile = profile.Mobile;
                dvm.City = profile.City;
                dvm.Interests = profile.Interests;
                dvm.PhotoPath = profile.PhotoPath ?? "/images/DefaultProfilePhoto.jfif";
            }

            // Preserve security tab state
            TempData.Keep("EmailSent");
            TempData.Keep("ForgotEmail");
            TempData.Keep("OtpVerified");
            TempData.Keep("VerifiedOtp");

            return View(dvm);
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        public IActionResult Profile(StudentProfile profile, IFormFile PhotoFile)
        {
            var id = GetStudentId();
            if (id == 0) return RedirectToAction("StudentLogin", "Auth");
            
            profile.StudentId = id;

            if (PhotoFile != null && PhotoFile.Length > 0)
            {
                var fileName = Guid.NewGuid() + Path.GetExtension(PhotoFile.FileName);
                var path = Path.Combine("wwwroot", "images", "profiles", fileName);
                
                if (!Directory.Exists(Path.GetDirectoryName(path)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                }

                using (var stream = new FileStream(path, FileMode.Create))
                {
                    PhotoFile.CopyTo(stream);
                }
                profile.PhotoPath = "/images/profiles/" + fileName;
            }
            else
            {
                var existing = _context.StudentProfiles.AsNoTracking().FirstOrDefault(s => s.StudentId == id);
                profile.PhotoPath = existing?.PhotoPath ?? "/images/DefaultProfilePhoto.jfif";
            }

            var existingprofile = _context.StudentProfiles.FirstOrDefault(s => s.StudentId == id);
            if (existingprofile == null)
            {
                _context.StudentProfiles.Add(profile);
                _context.SaveChanges();
                TempData["Alert"] = "Profile created successfully!";
                TempData["AlertType"] = "success";
            }
            else
            {
                existingprofile.FirstName = profile.FirstName;
                existingprofile.LastName = profile.LastName;
                existingprofile.Mobile = profile.Mobile;
                existingprofile.City = profile.City;
                existingprofile.Interests = profile.Interests;
                existingprofile.PhotoPath = profile.PhotoPath;
                _context.Update(existingprofile);
                _context.SaveChanges();
                TempData["Alert"] = "Profile updated successfully.";
                TempData["AlertType"] = "success";
            }

            // Preserve security tab state
            TempData.Keep("EmailSent");
            TempData.Keep("ForgotEmail");
            TempData.Keep("OtpVerified");
            TempData.Keep("VerifiedOtp");

            return RedirectToAction("Profile");
        }

        public IActionResult Courses()
        {
            var studentId = GetStudentId();
            var vm = _courseQueryService.GetPublishedCoursePage(studentId);
            return View(vm);
        }

        public IActionResult CourseDetails(int id, bool preview = false)
        {
            if (id <= 0) return RedirectToAction("Courses");
            
            var studentId = GetStudentId();
            bool isAdmin = User.IsInRole("Admin");

            if (!preview && !isAdmin && studentId > 0 && _enrollmentService.IsEnrolled(studentId, id))
            {
                return RedirectToAction("LearningView", new { id = id });
            }

            var vm = _courseQueryService.GetCourseDetails(id, studentId);
            if (vm == null) return NotFound();
            
            ViewBag.IsPreview = preview || isAdmin;
            return View(vm);
        }

        [Authorize(Roles = "Student")]
        public IActionResult LearningView(int id)
        {
            var studentId = GetStudentId();
            if (!_enrollmentService.IsEnrolled(studentId, id))
            {
                return RedirectToAction("CourseDetails", new { id = id });
            }

            var vm = _courseQueryService.GetCourseDetails(id, studentId);
            if (vm == null) return NotFound();
            
            ViewBag.CompletedLessons = _courseProgressService.GetCompletedLessonIds(studentId, id);
            return View(vm);
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        public IActionResult MarkLessonComplete(int lessonId, int courseId)
        {
            var studentId = GetStudentId();
            if (studentId == 0) return Json(new { success = false });
            
            bool success = _courseProgressService.MarkLessonAsComplete(studentId, lessonId);
            var enrolledCourses = _studentActivityService.GetEnrolledCourses(studentId);
            var course = enrolledCourses.FirstOrDefault(c => c.courseId == courseId);
            var progress = course?.ProgressPercentage ?? 0;
            
            return Json(new { success = success, progress = progress });
        }

        [Authorize(Roles = "Student")]
        public IActionResult EnrolledCourses()
        {
            var studentId = GetStudentId();
            var enrolledCourses = _studentActivityService.GetEnrolledCourses(studentId);
            return View(enrolledCourses);
        }

        [Authorize(Roles = "Student")]
        public IActionResult Checkout(int courseId)
        {
            var studentId = GetStudentId();

            if (!_studentActivityService.IsProfileComplete(studentId))
            {
                TempData["Alert"] = "Please complete your profile before enrolling in courses.";
                TempData["AlertType"] = "warning";
                return RedirectToAction("Profile", "Home");
            }

            if (_enrollmentService.IsEnrolled(studentId, courseId))
            {
                TempData["Alert"] = "You are already enrolled in this course!";
                TempData["AlertType"] = "info";
                return RedirectToAction("EnrolledCourses", "Home");
            }

            var result = _enrollmentService.CreateOrder(studentId, courseId);
            if (!result.Success)
            {
                TempData["Alert"] = result.Message;
                TempData["AlertType"] = "danger";
                return RedirectToAction("CourseDetails", "Home", new { id = courseId });
            }

            // Handle Free Course ─ skip Razorpay UI
            if (result.IsFree)
            {
                TempData["Alert"] = "Enrollment successful! Start learning now.";
                TempData["AlertType"] = "success";
                return RedirectToAction("EnrollmentSuccess", new { enrollmentId = result.EnrollmentId });
            }

            ViewBag.RazorpayOrderId = result.RazorpayOrderId;
            ViewBag.Amount = result.Amount;
            ViewBag.AmountDisplay = result.Amount / 100m;
            ViewBag.CourseTitle = result.CourseTitle;
            ViewBag.CourseId = courseId;
            // Use sanitized key from service (Single Source of Truth)
            ViewBag.RazorpayKeyId = _enrollmentService.RazorpayKeyId;
            ViewBag.StudentEmail = result.StudentEmail;
            ViewBag.StudentMobile = result.StudentMobile;
            ViewBag.StudentName = result.StudentName;
            
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        public IActionResult VerifyPayment(string razorpay_order_id, string razorpay_payment_id, string razorpay_signature, int courseId)
        {
            var studentId = GetStudentId();
            if (!_studentActivityService.IsProfileComplete(studentId))
            {
                TempData["Alert"] = "Please complete your profile before proceeding.";
                TempData["AlertType"] = "warning";
                return RedirectToAction("Profile", "Home");
            }

            var result = _enrollmentService.VerifyPayment(razorpay_order_id, razorpay_payment_id, razorpay_signature);
            if (!result.Success)
            {
                TempData["Alert"] = result.Message ?? "Payment failed.";
                TempData["AlertType"] = "danger";
                return RedirectToAction("Checkout", new { courseId = courseId });
            }

            TempData["Alert"] = "Enrollment successful! Start learning now.";
            TempData["AlertType"] = "success";
            return RedirectToAction("EnrollmentSuccess", new { enrollmentId = result.EnrollmentId });
        }

        [Authorize(Roles = "Student")]
        public IActionResult EnrollmentSuccess(int enrollmentId)
        {
            ViewBag.EnrollmentId = enrollmentId;
            return View();
        }

        [Authorize(Roles = "Student")]
        public IActionResult Wishlist()
        {
            var studentId = GetStudentId();
            var wishlist = _studentActivityService.GetWishlist(studentId);
            return View(wishlist);
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        public IActionResult ToggleWishlist(int courseId)
        {
            var studentId = GetStudentId();
            if (studentId == 0) return Json(new { success = false, message = "Please login first" });
            
            bool added = _studentActivityService.ToggleWishlist(studentId, courseId);
            return Json(new { success = true, added = added });
        }

        [Authorize(Roles = "Student")]
        public IActionResult Orders()
        {
            var studentId = GetStudentId();
            var orders = _studentActivityService.GetStudentOrders(studentId);
            return View(orders);
        }

        [Authorize(Roles = "Student")]
        public IActionResult MyCertificates()
        {
            var studentId = GetStudentId();
            var certificates = _courseProgressService.GetStudentCertificates(studentId);
            return View(certificates);
        }

        [Authorize(Roles = "Student")]
        public IActionResult ViewCertificate(int id)
        {
            var studentId = GetStudentId();
            var cert = _courseProgressService.GetOrGenerateCertificate(studentId, id);
            
            if (cert == null) 
            {
                TempData["Alert"] = "You have not completed this course yet.";
                TempData["AlertType"] = "warning";
                return RedirectToAction("MyCertificates");
            }

            var student = _context.Students.Include(s => s.Profile).FirstOrDefault(s => s.Id == studentId);
            var course = _context.Courses.FirstOrDefault(c => c.Id == id);

            var vm = new CertificateVM
            {
                CourseId = id,
                CourseTitle = course?.Title ?? "Unknown Course",
                CertificateNumber = cert.CertificateNumber,
                IssuedDate = cert.IssuedDate,
                IsEarned = true
            };

            ViewBag.StudentName = $"{student?.Profile?.FirstName} {student?.Profile?.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(ViewBag.StudentName)) ViewBag.StudentName = student?.Email;

            return View(vm);
        }

        [Authorize(Roles = "Student")]
        public IActionResult Cart()
        {
            var studentId = GetStudentId();
            var cartItems = _studentActivityService.GetCartItems(studentId);
            return View(cartItems);
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        public IActionResult AddToCart(int courseId)
        {
            var studentId = GetStudentId();

            if (!_studentActivityService.IsProfileComplete(studentId))
            {
                return Json(new { success = false, message = "Please complete your profile before adding courses to cart." });
            }

            bool added = _studentActivityService.AddToCart(studentId, courseId);
            if (added)
            {
                var count = _studentActivityService.GetCartCount(studentId);
                return Json(new { success = true, message = "Added to cart", cartCount = count });
            }
            return Json(new { success = false, message = "You are already enrolled in this course or item already in cart" });
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        public IActionResult RemoveFromCart(int courseId)
        {
            var studentId = GetStudentId();
            _studentActivityService.RemoveFromCart(studentId, courseId);
            var count = _studentActivityService.GetCartCount(studentId);
            return Json(new { success = true, message = "Removed from cart", cartCount = count });
        }

        [HttpGet]
        public IActionResult DebugRazorpay()
        {
            var keyId = _enrollmentService.RazorpayKeyId;
            
            // Check character codes to find invisible junk characters
            var charCodes = keyId.Select(c => (int)c).ToArray();

            return Json(new {
                KeyIdLength = keyId.Length,
                KeyIdStart = keyId.Length >= 9 ? keyId.Substring(0, 9) : keyId,
                KeyIdEnd = keyId.Length >= 4 ? keyId.Substring(keyId.Length - 4) : "",
                CharCodes = charCodes,
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
            });
        }

        private int GetStudentId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }

        // ==========================================
        // SECURITY / PASSWORD MANAGEMENT
        // ==========================================

        [HttpPost]
        [Authorize(Roles = "Student")]
        public IActionResult UpdatePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                TempData["Alert"] = "Passwords do not match.";
                TempData["AlertType"] = "danger";
                return RedirectToAction("Profile", new { tab = "security", view = "change" });
            }

            var email = CurrentUserEmail();
            var result = _authService.ChangePassword(email, currentPassword, newPassword, "Student");

            if (result.Success)
            {
                TempData["Alert"] = "Password updated successfully.";
                TempData["AlertType"] = "success";
            }
            else
            {
                string msg = "Failed to update password.";
                if (result.status == SkillForge.Services.Auth.Models.AuthMessage.WrongPassword) msg = "Incorrect current password.";

                TempData["Alert"] = msg;
                TempData["AlertType"] = "danger";
            }

            return RedirectToAction("Profile", new { tab = "security", view = "change" });
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> SendProfileOTP(string email)
        {
            var result = await _otpService.SendEmailOtp(email, "Student");
            if (result.Success)
            {
                TempData["Alert"] = "OTP sent to your email.";
                TempData["AlertType"] = "success";
                TempData["EmailSent"] = true;
                TempData["ForgotEmail"] = email;
            }
            else
            {
                TempData["Alert"] = "Failed to send OTP.";
                TempData["AlertType"] = "danger";
            }
            return RedirectToAction("Profile", new { tab = "security", view = "forgot" });
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        public IActionResult VerifyForgotOTP(string email, string otp)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(otp))
            {
                TempData["Alert"] = "Email and OTP are required.";
                TempData["AlertType"] = "warning";
                return RedirectToAction("Profile", new { tab = "security", view = "forgot" });
            }

            var result = _otpService.VerifySecurityOtp(email, otp, false);
            if (result.Success)
            {
                TempData["Alert"] = "OTP verified. You can now reset your password.";
                TempData["AlertType"] = "success";
                TempData["OtpVerified"] = true;
                TempData["VerifiedOtp"] = otp;
                TempData["ForgotEmail"] = email;
            }
            else
            {
                TempData["Alert"] = "Invalid or expired OTP.";
                TempData["AlertType"] = "danger";
                TempData["EmailSent"] = true;
                TempData["ForgotEmail"] = email;
            }
            return RedirectToAction("Profile", new { tab = "security", view = "forgot" });
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        public IActionResult ResetProfilePassword(string email, string otp, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                TempData["Alert"] = "Passwords do not match.";
                TempData["AlertType"] = "danger";
                TempData["OtpVerified"] = true;
                TempData["VerifiedOtp"] = otp;
                TempData["ForgotEmail"] = email;
                return RedirectToAction("Profile", new { tab = "security", view = "forgot" });
            }

            var result = _authService.ResetPassword(email, newPassword, otp, "Student");
            if (result.Success)
            {
                TempData["Alert"] = "Password reset successfully.";
                TempData["AlertType"] = "success";
                // Clear state
                TempData.Remove("EmailSent");
                TempData.Remove("ForgotEmail");
                TempData.Remove("OtpVerified");
                TempData.Remove("VerifiedOtp");
            }
            else
            {
                TempData["Alert"] = "Failed to reset password.";
                TempData["AlertType"] = "danger";
                TempData["OtpVerified"] = true;
                TempData["VerifiedOtp"] = otp;
                TempData["ForgotEmail"] = email;
            }
            return RedirectToAction("Profile", new { tab = "security", view = "forgot" });
        }
    }
}
