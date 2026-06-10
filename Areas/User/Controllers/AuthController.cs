using Google.Apis.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using SkillForge.Interfaces;
using SkillForge.Models;
using SkillForge.Services.Auth.Models;

namespace SkillForge.Areas.User.Controllers
{
    
    [Area("User")]
    public class AuthController : UserBaseController
    {
        private readonly IAuthService _authService;
        private readonly IOtpService _otpService;
        private readonly IConfiguration _config;

        public AuthController(IAuthService authService, IOtpService otpService, IConfiguration config)
        {
            _authService = authService;
            _otpService = otpService;
            _config = config;
        }

        // Show student registration page
        public IActionResult StudentRegistration()
        {
            return View(new RegisterVM());
        }

        // Post- student registration 
        [HttpPost]
        public async Task<IActionResult> StudentRegistration(RegisterVM model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                TempData["Alert"] = string.Join(" ", errors);
                TempData["AlertType"] = "danger";
                return View(model);
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var result = await _authService.Register(model.Email, model.Password, model.ConfirmPassword, "Student", baseUrl);

            // Handle  registration results
            if (result.status == AuthMessage.EmptyFields)
            {
                TempData["Alert"] = "Please enter all details";
                TempData["AlertType"] = "danger";
                return RedirectToAction("StudentRegistration");
            }

            if (result.status == AuthMessage.EmailExist)
            {
                TempData["Alert"] = "This Email Already Registered, You can Login";
                TempData["AlertType"] = "warning";
                return View();
            }
            
            if (result.status == AuthMessage.EmailRegisteredAsInstructor)
            {
                TempData["Alert"] = "This email is registered as Instructor. Please login as Instructor.";
                TempData["AlertType"] = "warning";
                return View();
            }
            
            if (result.status == AuthMessage.PassNotMatch)
            {
                TempData["Alert"] = "Password and Confirm Password doesn't Match!,Try Again";
                TempData["AlertType"] = "danger";
                return View();
            }
            
            if (result.status == AuthMessage.VerifyEmail)
            {
                TempData["Alert"] = "Registered! \"OTP sent to your email.\"";
                TempData["AlertType"] = "success";
                TempData["VerifyMessage"] = "We’ve sent a OTP on email.";
                TempData["VerifyEmail"] = result.Email;
                return View();
            }
            
            if (result.status == AuthMessage.EmailNotSent)
            {
                TempData["Alert"] = "Registered! Email sending failed";
                TempData["AlertType"] = "danger";
                TempData["VerifyMessage"] = "Email not sent. Try again.";
                return View();
            }
            
            if (result.status == AuthMessage.EmailVerified)
            {
                TempData["Alert"] = "Email verified! You can now login.";
                TempData["AlertType"] = "success";
                return RedirectToAction("StudentLogin");
            }

            if (result.Success)
            {
                TempData["Alert"] = "Registration successful. Login and Start Shaping Your Career in Right Path";
                TempData["AlertType"] = "success";
                return RedirectToAction("StudentLogin");
            }

            return View();
        }

        // Show student login page
        public IActionResult StudentLogin()
        {
            return View();
        }

        // Handle student login submission
        [HttpPost]
        public async Task<IActionResult> StudentLogin(string Email, string Password)
        {
            ViewBag.Email = Email;
            var result = _authService.Login(Email, Password, "Student");

            // Handle login status
            if (result.status == AuthMessage.EmptyFields)
            {
                TempData["Alert"] = "Please enter all details";
                TempData["AlertType"] = "danger";
                return View();
            }

            if (result.status == AuthMessage.NewUser)
            {
                TempData["Alert"] = "This Email Doesn't Registered, Please Create Account";
                TempData["AlertType"] = "warning";
            }
            if (result.status == AuthMessage.EmailRegisteredAsInstructor)
            {
                TempData["Alert"] = "This email is registered as Instructor. Please login from Instructor panel.";
                TempData["AlertType"] = "warning";
            }
            if (result.status == AuthMessage.VerifyEmail)
            {
                await _otpService.SendEmailOtp(Email, "Student");
                TempData["VerifyEmail"] = Email;
                TempData["Alert"] = "OTP sent. Please verify your email.";
                TempData["AlertType"] = "info";
            }
            if (result.status == AuthMessage.WrongPassword)
            {
                TempData["Alert"] = "Incorrect Password";
                TempData["AlertType"] = "danger";
            }
            if (result.status == AuthMessage.LoginFailed)
            {
                TempData["Alert"] = "Login failed. If you signed up with Google, please use the 'Login with Google' button.";
                TempData["AlertType"] = "danger";
            }
            
            // Sign in on success
            if (result.status == AuthMessage.LoginSuccess)
            {
                await SigninUser(result.Id.ToString(), result.Email, "Student", result.PhotoPath ?? "/images/DefaultProfilePhoto.jfif");
                return RedirectToAction("Courses", "Home", new { area = "User" });
            }

            return View();
        }

        // Handle Google authentication callback
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> GoogleLogin(string credential)
        {
            try
            {
                if (string.IsNullOrEmpty(credential))
                {
                    TempData["Alert"] = "Google login failed: missing credential.";
                    TempData["AlertType"] = "danger";
                    return RedirectToAction("StudentLogin");
                }

                // Validate Google token
                var verify = await GoogleJsonWebSignature.ValidateAsync(credential);
                if (verify == null || string.IsNullOrEmpty(verify.Email))
                {
                    TempData["Alert"] = "Google verification failed.";
                    TempData["AlertType"] = "danger";
                    return RedirectToAction("StudentLogin");
                }

                // Authenticate via service
                var result = _authService.GoogleAuth(
                    verify.Email,
                    verify.GivenName,
                    verify.FamilyName,
                    verify.Subject,
                    verify.Picture,
                    "Student"
                );

                if (result != null && result.status == AuthMessage.EmailRegisteredAsInstructor)
                {
                    TempData["Alert"] = "This email is registered as Instructor. Please login as Instructor.";
                    TempData["AlertType"] = "warning";
                    return RedirectToAction("StudentLogin");

                }
                if (result == null || !result.Success)
                {
                    TempData["Alert"] = "Google login failed. Please try again.";
                    TempData["AlertType"] = "danger";
                    return RedirectToAction("StudentLogin");
                }

                // Sign in user
                await SigninUser(result.Id.ToString(), result.Email ?? string.Empty, "Student", result.PhotoPath ?? "/images/DefaultProfilePhoto.jfif");

                TempData["Alert"] = "Welcome! \"You Logged in with Google.\"";
                TempData["AlertType"] = "success";
                return RedirectToAction("Courses", "Home", new { area = "User" });
            }

            catch (Exception)
            {
                TempData["Alert"] = "Something went wrong during Google login.";
                TempData["AlertType"] = "danger";
                return RedirectToAction("StudentLogin");
            }
        }

        // Request email verification OTP
        public async Task<IActionResult> SendEmailOtp(string Email)
        {
            var result = await _otpService.SendEmailOtp(Email, "Student");
            if (result.status == AuthMessage.VerifyEmail)
            {
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        // Verify registration OTP
        public IActionResult VerifyEmailOtp(string Email, string Otp)
        {
            var result = _otpService.VerifyEmailOtp(Email, Otp);
            if (result.status == AuthMessage.EmailVerified)
            {
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Invalid or expired OTP" });
        }

        // Show forgot password page
        public IActionResult ForgotPassword()
        {
            // Keep state across redirects or reloads
            TempData.Keep("EmailSent");
            TempData.Keep("ForgotEmail");
            TempData.Keep("OtpVerified");
            TempData.Keep("VerifiedOtp");
            return View();
        }

        // Request password reset OTP
        [HttpPost]
        public async Task<IActionResult> SendForgotOTP(string email, string role)
        {
            if (string.IsNullOrEmpty(email))
            {
                TempData["Alert"] = "Email is required.";
                TempData["AlertType"] = "warning";
                return RedirectToAction("ForgotPassword");
            }
            var result = await _otpService.SendEmailOtp(email, role);
            if (result.Success)
            {
                TempData["Alert"] = "OTP sent. Please check your email.";
                TempData["AlertType"] = "success";
                TempData["EmailSent"] = true;
                TempData["ForgotEmail"] = email;
            }
            else
            {
                string msg = result.status.ToString();
                if (msg == "NewUser") msg = "This email is not registered.";
                TempData["Alert"] = msg;
                TempData["AlertType"] = "danger";
            }
            return RedirectToAction("ForgotPassword");
        }

        // Verify password reset OTP
        [HttpPost]
        public IActionResult VerifyForgotOTP(string email, string otp)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(otp))
            {
                TempData["Alert"] = "Email and OTP are required.";
                TempData["AlertType"] = "warning";
                return RedirectToAction("ForgotPassword");
            }
            var result = _otpService.VerifySecurityOtp(email, otp, false);
            if (result.Success)
            {
                TempData["Alert"] = "OTP verified. You can now set your new password.";
                TempData["AlertType"] = "success";
                TempData["OtpVerified"] = true;
                TempData["VerifiedOtp"] = otp;
                TempData["ForgotEmail"] = email; 
                TempData["EmailSent"] = true;
            }
            else
            {
                string msg = result.status.ToString();
                if (msg == "InvalidOtp") msg = "Invalid OTP. Please try again.";
                else if (msg == "OtpExpired") msg = "OTP has expired. Please request a new one.";
                TempData["Alert"] = msg;
                TempData["AlertType"] = "danger";
                TempData["EmailSent"] = true;
                TempData["ForgotEmail"] = email;
            }
            return RedirectToAction("ForgotPassword");
        }

        // Set new password after OTP verification
        [HttpPost]
        public IActionResult ResetPassword(string email, string newPassword, string confirmPassword, string otp, string role)
        {
            if (string.IsNullOrEmpty(email))
            {
                TempData["Alert"] = "Email is missing. Please restart recovery.";
                TempData["AlertType"] = "danger";
                return RedirectToAction("ForgotPassword");
            }
            
            if (string.IsNullOrEmpty(otp)) 
            {
                TempData["Alert"] = "OTP is missing. Please restart recovery.";
                TempData["AlertType"] = "danger";
                TempData["EmailSent"] = true;
                TempData["ForgotEmail"] = email;
                return RedirectToAction("ForgotPassword");
            }

            if (newPassword != confirmPassword)
            {
                TempData["Alert"] = "Passwords do not match.";
                TempData["AlertType"] = "warning";
                // Preserve state
                TempData["OtpVerified"] = true;
                TempData["VerifiedOtp"] = otp;
                TempData["ForgotEmail"] = email;
                TempData["EmailSent"] = true;
                return RedirectToAction("ForgotPassword");   
            }

            // Reset password via service
            var result = _authService.ResetPassword(email, newPassword, otp, role);
            if (result.Success)
            {
                TempData["Alert"] = "Password reset successfully. Please login.";
                TempData["AlertType"] = "success";
                return RedirectToAction("StudentLogin");
            }
            else
            {
                string msg = result.status.ToString();
                if (msg == "InvalidOtp") msg = "Invalid OTP. Please try again.";
                else if (msg == "OtpExpired") msg = "OTP has expired. Please request a new one.";
                else msg = "Failed to reset password. Please try again.";
                
                TempData["Alert"] = msg;
                TempData["AlertType"] = "danger";
                
                // Keep verified state if possible
                if (result.status != AuthMessage.InvalidOtp && result.status != AuthMessage.OtpExpired)
                {
                    TempData["OtpVerified"] = true;
                    TempData["VerifiedOtp"] = otp;
                    TempData["ForgotEmail"] = email;
                    TempData["EmailSent"] = true;
                }
                else
                {
                    TempData["EmailSent"] = true;
                    TempData["ForgotEmail"] = email;
                }
                  
                return RedirectToAction("ForgotPassword");
            }
        }

        // Clear session and sign out
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            HttpContext.Session.Clear();
            TempData.Clear();
            return RedirectToAction("StudentLogin", "Auth");
        }
    }
}
  