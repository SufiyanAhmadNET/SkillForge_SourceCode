using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SkillForge.Data;
using SkillForge.Controllers;
using SkillForge.Areas.Instructor.Models;
using SkillForge.Services.Instructors.Models;
using System;
using System.Linq;

namespace SkillForge.Areas.Instructor.Controllers
{
    public class InstructorBaseController : AuthBaseController
    {
        protected readonly SkillForgeDbContext _context;

        public InstructorBaseController(SkillForgeDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var id = CurrentUserId();

            if (!string.IsNullOrEmpty(id))
            {
                var instructorId = int.Parse(id);
                var profile = _context.instructorProfiles.FirstOrDefault(p => p.InstructorId == instructorId);
                var application = _context.MentorApplications
                    .Where(a => a.InstructorId == instructorId)
                    .OrderByDescending(a => a.CreatedAt)
                    .FirstOrDefault();

                var status = application?.Status ?? MentorApplicationStatus.NotApplied;

                ViewBag.Email = CurrentUserEmail();
                ViewBag.FirstName = profile?.FirstName;
                ViewBag.LastName = profile?.LastName;
                ViewBag.PhotoPath = profile?.PhotoPath ?? CurrentUserPhotoPath();
                ViewBag.ApplicationStatus = status;

                // Restrict access if not approved
                var actionName = context.RouteData.Values["action"]?.ToString();
                var controllerName = context.RouteData.Values["controller"]?.ToString();

                var restrictedActions = new[] { "AddCourse", "MyCourses", "CourseDetails", "EditCourse", "DeleteCourse", "Earning" };

                if (status != MentorApplicationStatus.Approved && restrictedActions.Contains(actionName) && controllerName == "Home")
                {
                    var controller = context.Controller as Controller;
                    if (controller != null)
                    {
                        controller.TempData["Alert"] = status == MentorApplicationStatus.Pending 
                            ? "Your mentor application is pending admin approval." 
                            : "Please apply as a mentor and get approved to access these features.";
                        controller.TempData["AlertType"] = "info";
                    }
                    context.Result = new RedirectToActionResult("Profile", "Home", new { tab = "application" });
                }
            }

            base.OnActionExecuting(context);
        }
    }
}