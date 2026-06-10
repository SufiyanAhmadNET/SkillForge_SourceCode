using SkillForge.Areas.Instructor.Models;
using SkillForge.Areas.User.Models;
using SkillForge.Data;
using SkillForge.Models;
using SkillForge.Interfaces;
using SkillForge.Services.Auth.Models;

namespace SkillForge.Services.Auth
{
    // Authentication service implementation
    public class AuthService : IAuthService
    {
        private readonly SkillForgeDbContext _context;
        private readonly IOtpService _otpService;
        public AuthService(SkillForgeDbContext context, IOtpService otpService)
        {
            _context = context;
            _otpService = otpService;
        }

        // Register new user
        public async Task<AuthResult> Register(string Email, string Password, string ConfirmPassword, string Role, string baseUrl)
        {
            try
            {
                // Basic validation
                if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(ConfirmPassword))
                    return new AuthResult { Success = false, status = AuthMessage.EmptyFields };
                
                Email = Email.Trim().ToLower();
                
                // Check existing email
                bool EmailExistinStudent = _context.Students.Any(s => s.Email == Email);
                bool EmailExistinInstrutor = _context.instructors.Any(i => i.Email == Email);
                
                if (Password != ConfirmPassword)
                    return new AuthResult { Success = false, status = AuthMessage.PassNotMatch };

                // Student registration
                if (Role == "Student")
                {
                    if (EmailExistinStudent)
                        return new AuthResult { Success = false, status = AuthMessage.EmailExist };
                    if (EmailExistinInstrutor)
                        return new AuthResult { Success = false, status = AuthMessage.EmailRegisteredAsInstructor };
                    
                    var student = new Student
                    {
                        Email = Email,
                        Password = BCrypt.Net.BCrypt.HashPassword(Password),
                        IsEmailVerified = false,
                        EmailOtp = new Random().Next(100000, 999999).ToString(),
                        OtpExpiry = DateTime.UtcNow.AddMinutes(5)
                    };
                
                    _context.Students.Add(student);
                    _context.SaveChanges();
                    
                    // Send verification OTP
                    return await _otpService.SendEmailOtp(Email, "Student");
                }

                // Instructor registration
                if (Role == "Instructor")
                {
                    if (EmailExistinInstrutor)
                        return new AuthResult { Success = false, status = AuthMessage.EmailExist };
                    if (EmailExistinStudent)
                        return new AuthResult { Success = false, status = AuthMessage.EmailRegisteredAsStudent };

                    var instructor = new Instructor
                    {
                        Email = Email,
                        Password = BCrypt.Net.BCrypt.HashPassword(Password),
                        IsEmailVerified = false,
                        EmailOtp = new Random().Next(100000, 999999).ToString(),
                        OtpExpiry = DateTime.UtcNow.AddMinutes(5)
                    };
                    _context.instructors.Add(instructor);
                    _context.SaveChanges();
                    
                    // Send verification OTP
                    return await _otpService.SendEmailOtp(Email, "Instructor");
                }
                return new AuthResult { Success = false, status = AuthMessage.RegisterFailed };
            }
            catch (Exception)
            {
                return new AuthResult { Success = false, status = AuthMessage.RegisterFailed };
            }
        }

        // Handle user login
        public AuthResult Login(string Email, string Password, string Role)
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
                return new AuthResult { Success = false, status = AuthMessage.EmptyFields };

            Email = Email.Trim().ToLower();

            // Student login
            if (Role == "Student")
            {
                var student = _context.Students.FirstOrDefault(s => s.Email == Email);
                if (student == null)
                {
                    bool existsAsInstructor = _context.instructors.Any(i => i.Email == Email);
                    if (existsAsInstructor)
                        return new AuthResult { Success = false, status = AuthMessage.EmailRegisteredAsInstructor };
                    return new AuthResult { Success = false, status = AuthMessage.NewUser };
                }
                
                // Verification and password check
                if (!student.IsEmailVerified)
                    return new AuthResult { Success = false, status = AuthMessage.VerifyEmail };
                if (string.IsNullOrEmpty(student.Password))
                    return new AuthResult { Success = false, status = AuthMessage.LoginFailed };
                if (!BCrypt.Net.BCrypt.Verify(Password, student.Password))
                    return new AuthResult { Success = false, status = AuthMessage.WrongPassword };
                
                return new AuthResult
                {
                    Success = true,
                    status = AuthMessage.LoginSuccess,
                    Role = "Student",
                    Id = student.Id,
                    Email = student.Email
                };
            }

            // Instructor login
            if (Role == "Instructor")
            {
                var instructor = _context.instructors.FirstOrDefault(i => i.Email == Email);
                if (instructor == null)
                {
                    bool existsAsStudent = _context.Students.Any(s => s.Email == Email);
                    if (existsAsStudent)
                        return new AuthResult { Success = false, status = AuthMessage.EmailRegisteredAsStudent };
                    return new AuthResult { Success = false, status = AuthMessage.NewUser };
                }

                // Verification and password check
                if (!instructor.IsEmailVerified)
                    return new AuthResult { Success = false, status = AuthMessage.VerifyEmail };
                if (string.IsNullOrEmpty(instructor.Password))
                    return new AuthResult { Success = false, status = AuthMessage.LoginFailed };
                if (!BCrypt.Net.BCrypt.Verify(Password, instructor.Password))
                    return new AuthResult { Success = false, status = AuthMessage.WrongPassword };
                
                return new AuthResult
                {
                    Success = true,
                    status = AuthMessage.LoginSuccess,
                    Role = "Instructor",
                    Id = instructor.Id,
                    Email = instructor.Email
                };
            }

            // Admin login
            if (Role == "Admin")
            {
                if (Email == "sufiyan@admin.com" && Password == "abcd")
                    return new AuthResult { Success = true, status = AuthMessage.LoginSuccess, Role = "Admin" };
                return new AuthResult { Success = false, status = AuthMessage.LoginFailed };
            }
            return new AuthResult { Success = false, status = AuthMessage.LoginFailed };
        }

        // Google authentication login/register
        public AuthResult GoogleAuth(string Email, string FirstName, string LastName, string GoogleId, string Picture, string Role)
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(GoogleId))
                return new AuthResult { Success = false, status = AuthMessage.LoginFailed };

            Email = Email.Trim().ToLower();

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                // Student Google login
                if (Role == "Student")
                {
                    bool existsAsInstructor = _context.instructors.Any(i => i.Email == Email);
                    if (existsAsInstructor)
                        return new AuthResult { Success = false, status = AuthMessage.EmailRegisteredAsInstructor };

                    var student = _context.Students.FirstOrDefault(s => s.GoogleId == GoogleId);
                    if (student == null)
                    {
                        // Match by email if GoogleId not found
                        student = _context.Students.FirstOrDefault(s => s.Email == Email);
                        if (student != null)
                        {
                            student.GoogleId = GoogleId;
                            student.IsEmailVerified = true;
                            _context.SaveChanges();
                            transaction.Commit();
                            return new AuthResult
                            {
                                Success = true,
                                status = AuthMessage.LoginSuccess,
                                Email = student.Email,
                                Role = "Student",
                                Id = student.Id,
                                PhotoPath = Picture ?? "/images/DefaultProfilePhoto.jfif"
                            };
                        }
                    }
                    
                    if (student != null)
                    {
                        transaction.Commit();
                        return new AuthResult
                        {
                            Success = true,
                            status = AuthMessage.LoginSuccess,
                            Email = student.Email,
                            Role = "Student",
                            Id = student.Id,
                            PhotoPath = Picture ?? "/images/DefaultProfilePhoto.jfif"
                        };
                    }

                    // Create new student from Google
                    student = new Student
                    {
                        Email = Email,
                        GoogleId = GoogleId,
                        Password = null,
                        EmailOtp = null,
                        IsEmailVerified = true
                    };
                    _context.Students.Add(student);
                    _context.SaveChanges();

                    var studentProfile = new StudentProfile
                    {
                        StudentId = student.Id,
                        FirstName = FirstName ?? "Guest",
                        LastName = LastName ?? "User",
                        Mobile = null,
                        PhotoPath = Picture ?? "/images/DefaultProfilePhoto.jfif"
                    };
                    _context.StudentProfiles.Add(studentProfile);
                    _context.SaveChanges();
                    transaction.Commit();

                    return new AuthResult
                    {
                        Success = true,
                        status = AuthMessage.LoginSuccess,
                        Email = student.Email,
                        Role = "Student",
                        Id = student.Id,
                        PhotoPath = studentProfile.PhotoPath
                    };
                }

                // Instructor Google login
                if (Role == "Instructor")
                {
                    bool existsAsStudent = _context.Students.Any(s => s.Email == Email);
                    if (existsAsStudent)
                        return new AuthResult { Success = false, status = AuthMessage.EmailRegisteredAsStudent };

                    var instructor = _context.instructors.FirstOrDefault(i => i.GoogleId == GoogleId);
                    if (instructor == null)
                    {
                        // Match by email if GoogleId not found
                        instructor = _context.instructors.FirstOrDefault(i => i.Email == Email);
                        if (instructor != null)
                        {
                            instructor.GoogleId = GoogleId;
                            instructor.IsEmailVerified = true;
                            _context.SaveChanges();
                            transaction.Commit();
                            return new AuthResult
                            {
                                Success = true,
                                status = AuthMessage.LoginSuccess,
                                Email = instructor.Email,
                                Id = instructor.Id,
                                Role = "Instructor",
                            };
                        }
                    }
                    
                    if (instructor != null)
                    {
                        transaction.Commit();
                        return new AuthResult
                        {
                            Success = true,
                            status = AuthMessage.LoginSuccess,
                            Email = instructor.Email,
                            Id = instructor.Id,
                            Role = "Instructor",
                        };
                    }

                    // Create new instructor from Google
                    instructor = new Instructor
                    {
                        Email = Email,
                        GoogleId = GoogleId,
                        Password = null,
                        EmailOtp = null,
                        IsEmailVerified = true
                    };
                    _context.instructors.Add(instructor);
                    _context.SaveChanges();

                    var instructorProfile = new InstructorProfile
                    {
                        InstructorId = instructor.Id,
                        FirstName = FirstName ?? string.Empty,
                        LastName = LastName ?? string.Empty,
                    };
                    _context.instructorProfiles.Add(instructorProfile);
                    _context.SaveChanges();
                    transaction.Commit();

                    return new AuthResult
                    {
                        Success = true,
                        status = AuthMessage.LoginSuccess,
                        Email = instructor.Email,
                        Id = instructor.Id,
                        Role = "Instructor",
                        PhotoPath = instructorProfile.PhotoPath
                    };
                }
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Console.WriteLine($"GoogleAuth exception: {ex.Message}");
            }
            return new AuthResult { Success = false, status = AuthMessage.LoginFailed };
        }
        
        // Change existing password (authenticated flow)
        public AuthResult ChangePassword(string Email, string CurrentPassword, string NewPassword, string Role)
        {
            // Student password change
            if (Role == "Student")
            {
                var student = _context.Students.FirstOrDefault(s => s.Email == Email);
                if (student == null) return new AuthResult { Success = false, status = AuthMessage.LoginFailed };
                
                // Verify current password
                if (!BCrypt.Net.BCrypt.Verify(CurrentPassword, student.Password))
                    return new AuthResult { Success = false, status = AuthMessage.WrongPassword };

                student.Password = BCrypt.Net.BCrypt.HashPassword(NewPassword);
                _context.SaveChanges();
                return new AuthResult { Success = true, status = AuthMessage.PasswordResetSuccess };
            }

            // Instructor password change
            if (Role == "Instructor")
            {
                var instructor = _context.instructors.FirstOrDefault(i => i.Email == Email);
                if (instructor == null) return new AuthResult { Success = false, status = AuthMessage.LoginFailed };
                
                // Verify current password
                if (!BCrypt.Net.BCrypt.Verify(CurrentPassword, instructor.Password))
                    return new AuthResult { Success = false, status = AuthMessage.WrongPassword };

                instructor.Password = BCrypt.Net.BCrypt.HashPassword(NewPassword);
                _context.SaveChanges();
                return new AuthResult { Success = true, status = AuthMessage.PasswordResetSuccess };
            }
            return new AuthResult { Success = false, status = AuthMessage.PasswordResetFailed };
        }

        // Reset forgotten password
        public AuthResult ResetPassword(string Email, string NewPassword, string Otp, string Role)
        {
            // Verify OTP before reset
            var otpVerification = _otpService.VerifySecurityOtp(Email, Otp);
            if (!otpVerification.Success) return otpVerification;

            // Student password reset
            if (Role == "Student")
            {
                var student = _context.Students.FirstOrDefault(s => s.Email == Email);
                if (student == null) return new AuthResult { Success = false, status = AuthMessage.LoginFailed };
                student.Password = BCrypt.Net.BCrypt.HashPassword(NewPassword);
                _context.SaveChanges();
                return new AuthResult { Success = true, status = AuthMessage.PasswordResetSuccess };
            }

            // Instructor password reset
            if (Role == "Instructor")
            {
                var instructor = _context.instructors.FirstOrDefault(i => i.Email == Email);
                if (instructor == null) return new AuthResult { Success = false, status = AuthMessage.LoginFailed };
                instructor.Password = BCrypt.Net.BCrypt.HashPassword(NewPassword);
                _context.SaveChanges();
                return new AuthResult { Success = true, status = AuthMessage.PasswordResetSuccess };
            }
            return new AuthResult { Success = false, status = AuthMessage.PasswordResetFailed };
        }
    }
}
