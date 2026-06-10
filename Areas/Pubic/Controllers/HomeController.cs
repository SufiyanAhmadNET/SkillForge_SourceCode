using Microsoft.AspNetCore.Mvc;
using SkillForge.Interfaces;
using SkillForge.Models;
using System.Security.Claims;

namespace SkillForge.Areas.Pubic.Controllers
{
    [Area("Pubic")]
    public class HomeController : Controller
    {
        private readonly ICourseQueryService _courseQueryService;
        private readonly ISearchService _searchService;

        public HomeController(ICourseQueryService courseQueryService, ISearchService searchService)
        {
            _courseQueryService = courseQueryService;
            _searchService = searchService;
        }

        public IActionResult Index()
        {
            return View();
        }

        // Search courses
        public IActionResult Search(string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return RedirectToAction("Courses");

            var result = _searchService.SearchCourses(q);
            return View(result);
        }

        public IActionResult Courses()
        {
            // studentId is 0 for guest users
            int studentId = 0;
            if (User.Identity.IsAuthenticated)
            {
                var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int.TryParse(claim, out studentId);
            }

            var vm = _courseQueryService.GetPublishedCoursePage(studentId);
            return View(vm);
        }

        //Course Details
        public IActionResult CoursesDetails(int id)
        {
            if (id <= 0) return RedirectToAction("Courses");

            int studentId = 0;
            if (User.Identity.IsAuthenticated)
            {
                var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int.TryParse(claim, out studentId);
            }

            var vm = _courseQueryService.GetCourseDetails(id, studentId);
            if (vm == null) return NotFound();

            ViewBag.IsPreview = false;
            return View(vm);
        }
    }
}
