using SkillForge.Data;
using SkillForge.Models;
using SkillForge.Interfaces;
using SkillForge.Services.Auth.Models;

namespace SkillForge.Services.Auth
{
    public class OtpService : IOtpService
    {
        private readonly SkillForgeDbContext _context;
        private readonly IEmailService _emailService;

        public OtpService(SkillForgeDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // Generate OTP, save to DB, send via email
        public async Task<AuthResult> SendEmailOtp(string Email, string Role)
        {
            if (string.IsNullOrWhiteSpace(Email))
                return new AuthResult { Success = false, status = AuthMessage.EmptyFields };

            Email = Email.Trim().ToLower();
            var otp = new Random().Next(100000, 999999).ToString();

            if (Role == "Student")
            {
                var student = _context.Students.FirstOrDefault(s => s.Email == Email);
                if (student == null)
                    return new AuthResult { Success = false, status = AuthMessage.NewUser };

                student.EmailOtp = otp;
                student.OtpExpiry = DateTime.UtcNow.AddMinutes(5);
                _context.SaveChanges();

                try
                {
                    await _emailService.SendOtpEmail(Email, otp);
                    return new AuthResult { Success = true, status = AuthMessage.VerifyEmail, Email = Email };
                }
                catch (Exception ex)
                {
                    // Log error but return user-friendly status
                    Console.WriteLine($"OTP Email Failure (Student): {ex.Message}");
                    return new AuthResult { Success = false, status = AuthMessage.EmailNotSent };
                }
            }

            if (Role == "Instructor")
            {
                var instructor = _context.instructors.FirstOrDefault(i => i.Email == Email);
                if (instructor == null)
                    return new AuthResult { Success = false, status = AuthMessage.NewUser };

                instructor.EmailOtp = otp;
                instructor.OtpExpiry = DateTime.UtcNow.AddMinutes(5);
                _context.SaveChanges();

                try
                {
                    await _emailService.SendOtpEmail(Email, otp);
                    return new AuthResult { Success = true, status = AuthMessage.VerifyEmail, Email = Email };
                }
                catch (Exception ex)
                {
                    // Log error but return user-friendly status
                    Console.WriteLine($"OTP Email Failure (Instructor): {ex.Message}");
                    return new AuthResult { Success = false, status = AuthMessage.EmailNotSent };
                }
            }

            return new AuthResult { Success = false, status = AuthMessage.NewUser };
        }

        // Verify OTP for email confirmation
        public AuthResult VerifyEmailOtp(string Email, string Otp)
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Otp))
                return new AuthResult { Success = false, status = AuthMessage.EmailNotVerified };

            Email = Email.Trim().ToLower();
            Otp = Otp.Trim();

            var student = _context.Students.FirstOrDefault(s => s.Email == Email);
            if (student != null)
            {
                if (student.IsEmailVerified)
                    return new AuthResult { Success = false, status = AuthMessage.EmailVerified };
                if (student.EmailOtp == null || student.EmailOtp.Trim() != Otp)
                    return new AuthResult { Success = false, status = AuthMessage.EmailNotVerified };
                if (student.OtpExpiry == null || student.OtpExpiry < DateTime.UtcNow)
                    return new AuthResult { Success = false, status = AuthMessage.EmailNotVerified };

                student.IsEmailVerified = true;
                student.EmailOtp = null;
                student.OtpExpiry = null;
                _context.SaveChanges();
                return new AuthResult { Success = true, status = AuthMessage.EmailVerified };
            }

            var instructor = _context.instructors.FirstOrDefault(i => i.Email == Email);
            if (instructor != null)
            {
                if (instructor.IsEmailVerified)
                    return new AuthResult { Success = false, status = AuthMessage.EmailVerified };
                if (instructor.EmailOtp == null || instructor.EmailOtp.Trim() != Otp)
                    return new AuthResult { Success = false, status = AuthMessage.EmailNotVerified };
                if (instructor.OtpExpiry == null || instructor.OtpExpiry < DateTime.UtcNow)
                    return new AuthResult { Success = false, status = AuthMessage.EmailNotVerified };

                instructor.IsEmailVerified = true;
                instructor.EmailOtp = null;
                instructor.OtpExpiry = null;
                _context.SaveChanges();
                return new AuthResult { Success = true, status = AuthMessage.EmailVerified };
            }

            return new AuthResult { Success = false, status = AuthMessage.EmailNotVerified };
        }

        // Verify OTP for security operations like password reset
        public AuthResult VerifySecurityOtp(string Email, string Otp, bool shouldClear = true)
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Otp))
                return new AuthResult { Success = false, status = AuthMessage.InvalidOtp };

            Email = Email.Trim().ToLower();
            Otp = Otp.Trim();

            var student = _context.Students.FirstOrDefault(s => s.Email == Email);
            if (student != null)
            {
                if (student.EmailOtp == null || student.EmailOtp.Trim() != Otp)
                    return new AuthResult { Success = false, status = AuthMessage.InvalidOtp };
                if (student.OtpExpiry == null || student.OtpExpiry < DateTime.UtcNow)
                    return new AuthResult { Success = false, status = AuthMessage.OtpExpired };

                if (shouldClear)
                {
                    student.EmailOtp = null;
                    student.OtpExpiry = null;
                    _context.SaveChanges();
                }
                return new AuthResult { Success = true, status = AuthMessage.OtpVerified };
            }

            var instructor = _context.instructors.FirstOrDefault(i => i.Email == Email);
            if (instructor != null)
            {
                if (instructor.EmailOtp == null || instructor.EmailOtp.Trim() != Otp)
                    return new AuthResult { Success = false, status = AuthMessage.InvalidOtp };
                if (instructor.OtpExpiry == null || instructor.OtpExpiry < DateTime.UtcNow)
                    return new AuthResult { Success = false, status = AuthMessage.OtpExpired };

                if (shouldClear)
                {
                    instructor.EmailOtp = null;
                    instructor.OtpExpiry = null;
                    _context.SaveChanges();
                }
                return new AuthResult { Success = true, status = AuthMessage.OtpVerified };
            }

            return new AuthResult { Success = false, status = AuthMessage.LoginFailed };
        }
    }
}
