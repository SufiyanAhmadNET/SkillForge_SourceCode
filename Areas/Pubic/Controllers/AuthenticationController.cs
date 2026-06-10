using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace SkillForge.Areas.Pubic.Controllers
{
    [Area("Pubic")]
    //Controller for Login SIgn in 
    public class AuthenticationController : Controller
    {           


         
        public IActionResult AdminLogin()
        {
            return View();
        }

        [HttpPost]
        public IActionResult AdminLogin(string email, string password)
        {
            ViewBag.Email = email;
            if(string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Please enter all details";
                return View();
            }
            
            if (email == "sufiyan@admin.com" && password == "abcd")
            {
                TempData["Alert"] = "success:Admin Login Successful";
                return RedirectToAction("Dashboard", "Home", new { area = "Admin" });
           
            }

            //if wrong email or Password
            else
            {
                ViewBag.InvalidDetails = "Invalid Email or Password";
                return View();
            }
        }

       
    }
}
