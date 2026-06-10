using Microsoft.AspNetCore.Mvc;
using SkillForge.Interfaces;
using SkillForge.Services.Instructors.Models;
using SkillForge.Areas.Admin.Models;
using SkillForge.Services.Courses.Models;
using SkillForge.Areas.Instructor.Models;

namespace SkillForge.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class HomeController : Controller
    {
        private readonly IAdminService _adminService;
        private readonly ISearchService _searchService;
        private readonly IAnalyticsService _analyticsService;
        private readonly IReportDownloadService _reportDownloadService;

        public HomeController(IAdminService adminService, 
                              ISearchService searchService,
                              IAnalyticsService analyticsService,
                              IReportDownloadService reportDownloadService)
        {
            _adminService = adminService;
            _searchService = searchService;
            _analyticsService = analyticsService;
            _reportDownloadService = reportDownloadService;
        }

        // Dashboard
        public IActionResult Dashboard()
        {
            var data = _adminService.GetDashboardData();
            ViewBag.PendingCount = data.RecentApplications.Count(a => a.Status == MentorApplicationStatus.Pending);
            return View(data);
        }

        // Pending Approvals
        public IActionResult Pending_Approvals(int page = 1)
        {
            const int pageSize = 10;
            var applications = _adminService.GetAllMentorApplications();
            var courses = _adminService.GetAllCoursesForReview();

            int totalRecords = applications.Count;
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));

            var pagedApplications = applications
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var vm = new PendingApprovalsVM
            {
                Applications = pagedApplications,
                Courses = courses
            };

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalRecords = totalRecords;
            ViewBag.PageSize = pageSize;
            ViewBag.PendingCount = applications.Count(a => a.Status == MentorApplicationStatus.Pending);
            ViewBag.AllApplications = applications; // For the "Recent" section if needed or status counts

            return View(vm);
        }

        // Approve Application
        [HttpPost]
        public IActionResult ApproveApplication(int id)
        {
            var application = _adminService.GetAllMentorApplications().FirstOrDefault(a => a.Id == id);
            var previousStatus = application?.Status;

            var result = _adminService.UpdateApplicationStatus(id, MentorApplicationStatus.Approved);
            if (result)
            {
                TempData["Alert"] = "success:Instructor application approved successfully.";
                if (previousStatus.HasValue)
                {
                    TempData["UndoData"] = $"Application|{id}|{(int)previousStatus.Value}";
                }
            }
            else
            {
                TempData["Alert"] = "error:Failed to approve application.";
            }
            return RedirectToAction(nameof(Pending_Approvals));
        }

        // Reject Application
        [HttpPost]
        public IActionResult RejectApplication(int id, string? comment)
        {
            var application = _adminService.GetAllMentorApplications().FirstOrDefault(a => a.Id == id);
            var previousStatus = application?.Status;

            var result = _adminService.UpdateApplicationStatus(id, MentorApplicationStatus.Rejected, comment);
            if (result)
            {
                bool isRevoke = previousStatus == MentorApplicationStatus.Approved;
                TempData["Alert"] = isRevoke ? "success:Instructor approval revoked." : "success:Instructor application rejected.";
                
                if (previousStatus.HasValue)
                {
                    TempData["UndoData"] = $"Application|{id}|{(int)previousStatus.Value}";
                }
            }
            else
            {
                TempData["Alert"] = "error:Failed to reject application.";
            }
            return RedirectToAction(nameof(Pending_Approvals));
        }

        // Undo Action
        [HttpPost]
        public IActionResult UndoAction(string undoData)
        {
            if (string.IsNullOrEmpty(undoData)) return BadRequest();

            var parts = undoData.Split('|');
            if (parts.Length != 3) return BadRequest();

            string type = parts[0];
            int id = int.Parse(parts[1]);
            int statusInt = int.Parse(parts[2]);

            bool result = false;
            string message = "";

            if (type == "Application")
            {
                result = _adminService.UpdateApplicationStatus(id, (MentorApplicationStatus)statusInt);
                message = "Action undone successfully.";
            }
            else if (type == "Course")
            {
                result = _adminService.UpdateCourseStatus(id, (CourseStatus)statusInt);
                message = "Action undone successfully.";
            }

            if (result)
            {
                TempData["Alert"] = $"success:{message}";
            }
            else
            {
                TempData["Alert"] = "error:Failed to undo action.";
            }

            return Redirect(Request.Headers["Referer"].ToString() ?? "/Admin/Home/Dashboard");
        }

        // Admin Courses Dashboard
        public IActionResult Courses(string? search, string? category, string? status, int page = 1)
        {
            const int pageSize = 10;
            List<AdminCourseReviewVM> courses;

            if (!string.IsNullOrWhiteSpace(search) || (!string.IsNullOrWhiteSpace(category) && category != "all") || (!string.IsNullOrWhiteSpace(status) && status != "all"))
            {
                courses = _searchService.SearchAdminCourses(search, category, status);
                ViewBag.SearchTerm = search;
                ViewBag.SelectedCategory = category;
                ViewBag.SelectedStatus = status;
            }
            else
            {
                courses = _adminService.GetAllCoursesForReview();
            }

            int totalRecords = courses.Count;
            int activeCount = courses.Count(c => c.Status == CourseStatus.Approved || c.Status == CourseStatus.Published);
            int pendingCount = courses.Count(c => c.Status == CourseStatus.PendingReview);

            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));

            var pagedData = courses
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalRecords = totalRecords;
            ViewBag.ActiveCount = activeCount;
            ViewBag.PendingCourseCount = pendingCount;
            ViewBag.PageSize = pageSize;
            ViewBag.Categories = _adminService.GetAllCoursesForReview().Select(c => c.Category).Distinct().ToList();
            ViewBag.PendingCount = _adminService.GetAllMentorApplications().Count(a => a.Status == MentorApplicationStatus.Pending);
            
            return View(pagedData);
        }

        // Approve Course
        [HttpPost]
        public IActionResult ApproveCourse(int id)
        {
            var course = _adminService.GetAllCoursesForReview().FirstOrDefault(c => c.Id == id);
            var previousStatus = course?.Status;

            var result = _adminService.UpdateCourseStatus(id, CourseStatus.Approved);
            if (result)
            {
                TempData["Alert"] = "success:Course approved and is now live.";
                if (previousStatus.HasValue)
                {
                    TempData["UndoData"] = $"Course|{id}|{(int)previousStatus.Value}";
                }
            }
            else
            {
                TempData["Alert"] = "error:Failed to approve course.";
            }
            return Redirect(Request.Headers["Referer"].ToString() ?? Url.Action(nameof(Courses))!);
        }

        // Reject Course
        [HttpPost]
        public IActionResult RejectCourse(int id, string? reason)
        {
            var course = _adminService.GetAllCoursesForReview().FirstOrDefault(c => c.Id == id);
            var previousStatus = course?.Status;

            var result = _adminService.UpdateCourseStatus(id, CourseStatus.Rejected, reason);
            if (result)
            {
                TempData["Alert"] = "success:Course rejected with feedback.";
                if (previousStatus.HasValue)
                {
                    TempData["UndoData"] = $"Course|{id}|{(int)previousStatus.Value}";
                }
            }
            else
            {
                TempData["Alert"] = "error:Failed to reject course.";
            }
            return Redirect(Request.Headers["Referer"].ToString() ?? Url.Action(nameof(Courses))!);
        }

        // Student List
        public IActionResult Students(string? search, int page = 1)
        {
            const int pageSize = 10;
            List<StudentListVM> students;
            
            if (!string.IsNullOrWhiteSpace(search))
            {
                students = _searchService.SearchStudents(search);
                ViewBag.SearchTerm = search;
            }
            else
            {
                students = _adminService.GetAllStudents();
            }

            int totalStudents = students.Count;
            int totalPages = (int)Math.Ceiling(totalStudents / (double)pageSize);
            page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));

            var pagedStudents = students
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalRecords = totalStudents;
            ViewBag.PageSize = pageSize;
            ViewBag.PendingCount = _adminService.GetAllMentorApplications().Count(a => a.Status == MentorApplicationStatus.Pending);

            return View(pagedStudents);
        }

        // Instructor List
        public IActionResult Instructors(string? search, string? status, int page = 1)
        {
            const int pageSize = 10;
            List<InstructorListVM> instructors;

            if (!string.IsNullOrWhiteSpace(search) || (!string.IsNullOrWhiteSpace(status) && status != "all"))
            {
                instructors = _searchService.SearchInstructors(search ?? "", status);
                ViewBag.SearchTerm = search;
                ViewBag.SelectedStatus = status;
            }
            else
            {
                instructors = _adminService.GetAllInstructors();
            }

            int totalRecords = instructors.Count;
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));

            var pagedData = instructors
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalRecords = totalRecords;
            ViewBag.PageSize = pageSize;
            ViewBag.PendingCount = _adminService.GetAllMentorApplications().Count(a => a.Status == MentorApplicationStatus.Pending);
            
            return View(pagedData);
        }

        // Instructor Profile/ Details
        public async Task<IActionResult> ViewInstructorProfile(int id)
        {
            var vm = await _analyticsService.GetInstructorDashboardStatsAsync(id);
            if (vm == null) return NotFound();

            ViewBag.PendingCount = _adminService.GetAllMentorApplications().Count(a => a.Status == MentorApplicationStatus.Pending);
            return View(vm);
        }

        // Reports
        public IActionResult Reports()
        {
            ViewBag.PendingCount = _adminService.GetAllMentorApplications().Count(a => a.Status == MentorApplicationStatus.Pending);
            return View();
        }

        // ==========================================
        // REPORT DOWNLOAD ACTIONS
        // ==========================================

        public async Task<IActionResult> DownloadEnrollmentReport(int days = 30)
        {
            var data = await _analyticsService.GetAdminEnrollmentReportAsync(days);
            var pdf = await _reportDownloadService.GenerateAdminEnrollmentReportPdfAsync(data);
            return File(pdf, "application/pdf", $"Enrollment_Report_{DateTime.Now:yyyyMMdd}.pdf");
        }

        public async Task<IActionResult> DownloadSalesReport(int days = 30)
        {
            var data = await _analyticsService.GetAdminSalesReportAsync(days);
            var pdf = await _reportDownloadService.GenerateAdminSalesReportPdfAsync(data);
            return File(pdf, "application/pdf", $"Sales_Report_{DateTime.Now:yyyyMMdd}.pdf");
        }

        public async Task<IActionResult> DownloadStudentReport(int days = 30)
        {
            var data = await _analyticsService.GetAdminStudentReportAsync(days);
            var pdf = await _reportDownloadService.GenerateAdminStudentReportPdfAsync(data);
            return File(pdf, "application/pdf", $"Student_Report_{DateTime.Now:yyyyMMdd}.pdf");
        }

        public async Task<IActionResult> DownloadInstructorReport(int days = 30)
        {
            var data = await _analyticsService.GetAdminInstructorReportAsync(days);
            var pdf = await _reportDownloadService.GenerateAdminInstructorReportPdfAsync(data);
            return File(pdf, "application/pdf", $"Instructor_Report_{DateTime.Now:yyyyMMdd}.pdf");
        }

        public async Task<IActionResult> DownloadRevenueReport(int days = 30)
        {
            var data = await _analyticsService.GetAdminRevenueReportAsync(days);
            var pdf = await _reportDownloadService.GenerateAdminRevenueReportPdfAsync(data);
            return File(pdf, "application/pdf", $"Revenue_Report_{DateTime.Now:yyyyMMdd}.pdf");
        }

        public async Task<IActionResult> DownloadPayoutReport(int days = 30)
        {
            var data = await _analyticsService.GetAdminPayoutReportAsync(days);
            var pdf = await _reportDownloadService.GenerateAdminPayoutReportPdfAsync(data);
            return File(pdf, "application/pdf", $"Payout_Report_{DateTime.Now:yyyyMMdd}.pdf");
        }

        public async Task<IActionResult> DownloadApplicationsReport(int days = 30)
        {
            var data = await _analyticsService.GetAdminApplicationsReportAsync(days);
            var pdf = await _reportDownloadService.GenerateAdminApplicationsReportPdfAsync(data);
            return File(pdf, "application/pdf", $"Applications_Report_{DateTime.Now:yyyyMMdd}.pdf");
        }

        public async Task<IActionResult> DownloadCoursesReport(int days = 0)
        {
            var data = await _analyticsService.GetAdminCoursesReportAsync(days);
            var pdf = await _reportDownloadService.GenerateAdminCoursesReportPdfAsync(data);
            return File(pdf, "application/pdf", $"Platform_Courses_Report_{DateTime.Now:yyyyMMdd}.pdf");
        }
    }
}
