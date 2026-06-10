using Google.Apis.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SkillForge.Areas.User.Controllers;
using SkillForge.Areas.User.Models;
using SkillForge.Interfaces;
using SkillForge.Models;
using SkillForge.Services.Auth.Models;
using System.Data;
using System.Diagnostics;

namespace SkillForge.Areas.Instructor.Controllers
{
    [Area("Instructor")]
    public class AuthController : UserBaseController
    {
        private readonly ILogger<AuthController> _logger;
        private readonly IAuthService _authService;
        private readonly IOtpService _otpService;
        private readonly IConfiguration _config;
        public AuthController(IAuthService authService, IOtpService otpService, IConfiguration config, ILogger<AuthController> logger)
        {
            _logger = logger;
            _authService = authService;
            _otpService = otpService;
            _config = config;
        }

        //==================
        //Registration Methods
        //====================
        public IActionResult InstructorRegistration()
        {
            return View(new RegisterVM());
        }

        [HttpPost]
        public async Task<IActionResult> InstructorRegistration(RegisterVM model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                TempData["Alert"] = string.Join(" ", errors);
                TempData["AlertType"] = "danger";
                return View(model);
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            //pass paramters to Auth Service CLass
            var result = await _authService.Register(model.Email, model.Password, model.ConfirmPassword, "Instructor", baseUrl);
            
            // Check Invalid or Valid
            // empty fields
            if (result.status == AuthMessage.EmptyFields)
            {
                TempData["Alert"] = "Please enter all details";
                TempData["AlertType"] = "danger";
                return RedirectToAction("InstructorRegistration");
            }
            // old user
            if (result.status == AuthMessage.EmailExist)
            {
                TempData["Alert"] = "This Email Already Registered, You can Login";
                TempData["AlertType"] = "warning";
                return View();
            }
            if (result.status == AuthMessage.EmailRegisteredAsStudent)
            {
                TempData["Alert"] = "This email is registered as Student. Please login as Student.";
                TempData["AlertType"] = "warning";
                return View();
            }
            // password mismatch
            if (result.status == AuthMessage.PassNotMatch)
            {
                TempData["Alert"] = "Password and Confirm Password doesn't Match!";
                TempData["AlertType"] = "danger";
                return View();
            }
            // email sent
            if (result.status == AuthMessage.VerifyEmail)
            {
                TempData["Alert"] = "Registered! Please check your email to verify.";
                TempData["AlertType"] = "success";
                TempData["VerifyMessage"] = "We’ve sent a verification OTP on Email.";
                TempData["VerifyEmail"] = result.Email;
                return View("InstructorRegistration");
            }
            // email failed
            if (result.status == AuthMessage.EmailNotSent)
            {
                TempData["Alert"] = "Registered! Email sending failed";
                TempData["VerifyMessage"] = "Email not sent. Try again.";
                TempData["VerifyEmail"] = result.Email;
                TempData["AlertType"] = "danger";
                return View("InstructorRegistration");
            }
            // email verified
            if (result.status == AuthMessage.EmailVerified)
            {
                TempData["Alert"] = "Email verified! You can now login.";
                TempData["AlertType"] = "success";
                return RedirectToAction("InstructorLogin");
            }

            if (result.Success)
            {
                TempData["Alert"] = "Registration successful. Login and Start Your Journey as Mentor";
                TempData["AlertType"] = "success";
                return RedirectToAction("InstructorLogin");
            }
            
            return View();
        }

        //Login Methods
        public IActionResult InstructorLogin()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> InstructorLogin(string Email, string Password)
        {
            ViewBag.Email = Email;
            var result = _authService.Login(Email, Password, "Instructor");

            // Handle login status
            if (result.status == AuthMessage.EmptyFields)
            {
                TempData["Alert"] = "Please enter all details";
                TempData["AlertType"] = "danger";
                return View();
            }

            // new user
            if (result.status == AuthMessage.NewUser)
            {
                TempData["Alert"] = "This Email Doesn't Registered, Please Create Account";
                TempData["AlertType"] = "warning";
            }
            if (result.status == AuthMessage.EmailRegisteredAsStudent)
            {
                TempData["Alert"] = "This email is registered as Student. Please login from Student panel.";
                TempData["AlertType"] = "warning";
            }
            // email not verified
            if (result.status == AuthMessage.VerifyEmail)
            {
                TempData["Alert"] = "Email not verified";
                TempData["VerifyMessage"] = "Please verify your email first.";
                TempData["VerifyEmail"] = Email;
                TempData["AlertType"] = "warning";
            }
            // wrong password
            if (result.status == AuthMessage.WrongPassword)
            {
                TempData["Alert"] = "Incorrect Password";
                TempData["AlertType"] = "danger";
            }
            //successful login
            if (result.status == AuthMessage.LoginSuccess)
            {
                await SigninUser(result.Id.ToString(), result.Email, "Instructor", result.PhotoPath ?? "/images/DefaultProfilePhoto.jfif");
                return RedirectToAction("Dashboard", "Home", new { area = "Instructor" });
            }
            
            return View();
        }
            
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> GoogleLogin(string credential)
        {
            try
            {
                // validation 
                if (string.IsNullOrEmpty(credential))
                {
                    TempData["Alert"] = "Google login failed: missing credential.";
                    TempData["AlertType"] = "danger";
                    return RedirectToAction("InstructorLogin");
                }
                //Verify token with Google
                var verify = await GoogleJsonWebSignature.ValidateAsync(credential);
                if (verify == null || string.IsNullOrEmpty(verify.Email))
                {
                    TempData["Alert"] = "Google verification failed.";
                    TempData["AlertType"] = "danger";
                    return RedirectToAction("InstructorLogin");
                }
                // Call Auth Service
                var result = _authService.GoogleAuth(
                    verify.Email,
                    verify.GivenName,
                    verify.FamilyName,
                    verify.Subject,
                    verify.Picture,
                    "Instructor"
                );
                //  fail
                if (result != null && result.status == AuthMessage.EmailRegisteredAsStudent)
                {
                    TempData["Alert"] = "This email is registered as Student. Please login from Student panel.";
                    TempData["AlertType"] = "warning";
                    return RedirectToAction("InstructorLogin");
                }
                if (result == null || !result.Success)
                {
                    TempData["Alert"] = "Google login failed. Please try again.";
                    TempData["AlertType"] = "danger";
                    return RedirectToAction("InstructorLogin");
                }
                // Sign in user
                await SigninUser(
                    result.Id.ToString(),
                    result.Email ?? string.Empty,
                    "Instructor",
                    result.PhotoPath ?? "/images/DefaultProfilePhoto.jfif"
                );
                
                TempData["Alert"] = "Welcome back, Instructor!";
                TempData["AlertType"] = "success";
                return RedirectToAction("Dashboard", "Home", new { area = "Instructor" });
            }
            catch (Exception )
            {
                TempData["Alert"] = "Something went wrong during Google login.";
                TempData["AlertType"] = "danger";
                return RedirectToAction("InstructorLogin");
            }
        }

        //SEnd Verification Method
        [HttpPost]
        public async Task<IActionResult> SendEmailOtp(string Email)
        {
            var result = await _otpService.SendEmailOtp(Email, "Instructor");
            if (result.status == AuthMessage.VerifyEmail)
            {
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        //Verify EMail  
        [HttpPost]
        public IActionResult VerifyEmailOtp(string Email, string Otp)
        {
            var result = _otpService.VerifyEmailOtp(Email, Otp);
            if (result.status == AuthMessage.EmailVerified)
            {
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Invalid or expired OTP" });
        }

        public IActionResult ForgotPassword()
        {
            // Keep state across reloads/redirects
            TempData.Keep("EmailSent");
            TempData.Keep("ForgotEmail");
            TempData.Keep("OtpVerified");
            TempData.Keep("VerifiedOtp");
            return View();
        }

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
                TempData["ForgotEmail"] = email; // Use ForgotEmail to match partial
                TempData["EmailSent"] = true;
            }
            else
            {
                string msg = result.status.ToString();
                if (msg == "InvalidOtp") msg = "Invalid OTP. Please try again.";
                else if (msg == "OtpExpired") msg = "OTP has expired. Please request a new one.";
                TempData["Alert"] = msg;
                TempData["AlertType"] = "danger";
                // Keep preserved email and visibility
                TempData["EmailSent"] = true;
                TempData["ForgotEmail"] = email;
            }
            return RedirectToAction("ForgotPassword");
        }

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

            var result = _authService.ResetPassword(email, newPassword, otp, role);
            if (result.Success)
            {
                TempData["Alert"] = "Password updated successfully. Please login.";
                TempData["AlertType"] = "success";
                return RedirectToAction("InstructorLogin");
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

        //Logout
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            HttpContext.Session.Clear();
            return RedirectToAction("InstructorLogin");
        }
    }
}
