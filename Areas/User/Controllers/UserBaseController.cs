using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SkillForge.Controllers;
using SkillForge.Data;

namespace SkillForge.Areas.User.Controllers
{
    public class UserBaseController : AuthBaseController
    {
        [FromServices]
        public required SkillForgeDbContext _context { get; set; }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var id = CurrentUserId(); // string- Identity GUID

            if (!string.IsNullOrEmpty(id))
            {
                // query profile using string Id
                 var profile = _context.StudentProfiles.FirstOrDefault(p => p.StudentId.ToString() == id);

                ViewBag.Email = CurrentUserEmail();
                ViewBag.FirstName = profile?.FirstName;
                ViewBag.LastName = profile?.LastName;
                ViewBag.PhotoPath = profile?.PhotoPath ?? CurrentUserPhotoPath();
                
                // Add Cart Count
                ViewBag.CartCount = _context.Carts.Count(c => c.StudentId.ToString() == id);
            }

            base.OnActionExecuting(context);
        }
    }
}