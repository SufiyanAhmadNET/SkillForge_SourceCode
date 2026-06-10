using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillForge.Areas.Instructor.Models;
using SkillForge.Data;
using SkillForge.Models;
using SkillForge.Interfaces;
using SkillForge.Services.Instructors.Models;
using System.Security.Claims;
using SkillForge.Services.Courses.Models;

namespace SkillForge.Areas.Instructor.Controllers
{
    // Instructor area home controller
    [Area("Instructor")]
    public class HomeController : InstructorBaseController
    {
        private readonly IInstructorService _instructorService;
        private readonly ICourseManagementService _courseManagementService;
        private readonly ICourseContentService _courseContentService;
        private readonly IAuthService _authService;
        private readonly IOtpService _otpService;
        private readonly IMediaService _mediaService;
        private readonly IAnalyticsService _analyticsService;
        private readonly IReportDownloadService _reportDownloadService;

        public HomeController(SkillForgeDbContext context, 
            IInstructorService instructorService,
            ICourseManagementService courseManagementService,
            ICourseContentService courseContentService,
            IAuthService authService,
            IOtpService otpService,
            IMediaService mediaService,
            IAnalyticsService analyticsService,
            IReportDownloadService reportDownloadService) : base(context)
        {
            _instructorService = instructorService;
            _courseManagementService = courseManagementService;
            _courseContentService = courseContentService;
            _authService = authService;
            _otpService = otpService;
            _mediaService = mediaService;
            _analyticsService = analyticsService;
            _reportDownloadService = reportDownloadService;
        }

        // ... existing actions ...

        // ==========================================
        // REPORT DOWNLOAD ACTIONS
        // ==========================================

        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> DownloadCourseFinancialReport(int id)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var instructorId)) return RedirectToAction("InstructorLogin", "Auth");

            var data = await _analyticsService.GetCourseFinancialReportAsync(id, instructorId);
            if (data == null) return NotFound();

            var pdf = await _reportDownloadService.GenerateCourseFinancialReportPdfAsync(data);
            string fileName = $"{data.CourseTitle.Replace(" ", "_")}_Financial_Report.pdf";
            return File(pdf, "application/pdf", fileName);
        }

        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> DownloadStudentRoster(int id)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var instructorId)) return RedirectToAction("InstructorLogin", "Auth");

            var data = await _analyticsService.GetCourseStudentListReportAsync(id, instructorId);
            if (data == null) return NotFound();

            var pdf = await _reportDownloadService.GenerateStudentListPdfAsync(data);
            string fileName = $"Student_Roster_{data.CourseTitle.Replace(" ", "_")}.pdf";
            return File(pdf, "application/pdf", fileName);
        }

        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> DownloadMonthlyEarningsReport(int year, int month)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var instructorId)) return RedirectToAction("InstructorLogin", "Auth");

            var data = await _analyticsService.GetMonthlyFinancialReportAsync(instructorId, year, month);
            var pdf = await _reportDownloadService.GenerateMonthlyFinancialReportPdfAsync(data);
            string fileName = $"{data.ReportMonth.Replace(" ", "_")}_Financial_Report.pdf";
            return File(pdf, "application/pdf", fileName);
        }

        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> DownloadAllCoursesFinancialReport()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var instructorId)) return RedirectToAction("InstructorLogin", "Auth");

            var data = await _analyticsService.GetInstructorGlobalCourseReportAsync(instructorId);
            var pdf = await _reportDownloadService.GenerateInstructorGlobalCourseReportPdfAsync(data);
            string fileName = "Lifetime_Course_Performance_Report.pdf";
            return File(pdf, "application/pdf", fileName);
        }

        // Instructor dashboard with stats
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> Dashboard()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var instructorId))
            {
                return RedirectToAction("InstructorLogin", "Auth");
            }
            var vm = await _analyticsService.GetInstructorDashboardStatsAsync(instructorId);
            return View(vm);
        }

        // Show add course page (Draft-First)
        public IActionResult AddCourse()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var instructorId))
            {
                return RedirectToAction("InstructorLogin", "Auth");
            }

            // Create or reuse empty draft
            var draft = _courseManagementService.CreateEmptyDraft(instructorId);
            var vm = _courseManagementService.GetCourseForEdit(draft.Id, instructorId);
            return View(vm);
        }

        // Handle add course submission
        [HttpPost]
        public IActionResult AddCourse(CourseVM courseVM, IFormFile? thumbnail_url, IFormFile? Video_File, string? YouTubeUrl, string? videoType, string? OutcomesRaw, string? submitAction)
        {
            var instructorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(instructorIdClaim, out var InstructorId))
            {
                TempData["Alert"] = "Session expired. Please login again.";
                TempData["AlertType"] = "danger";
                return RedirectToAction("InstructorLogin", "Auth");
            }

            // Repopulate for the view early to prevent data loss on validation errors
            ViewBag.OutcomesRaw = OutcomesRaw;
            ViewBag.YouTubeUrl = YouTubeUrl;
            ViewBag.VideoType = videoType;

            // Parse outcomes from textarea early so they are available if returning View
            if (!string.IsNullOrWhiteSpace(OutcomesRaw))
            {
                courseVM.outcome = OutcomesRaw
                    .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(o => o.Trim())
                    .Where(o => !string.IsNullOrWhiteSpace(o))
                    .ToList();
            }

            bool isDraft = submitAction?.ToLower() == "draft";

            // Clean ModelState for optional media fields before checking IsValid
            if (!isDraft)
            {
                // If either YouTube URL or Video File is provided, we don't want "required" errors for the other
                bool hasVideo = !string.IsNullOrWhiteSpace(YouTubeUrl) || (Video_File != null && Video_File.Length > 0);
                
                if (hasVideo)
                {
                    // Remove implicit "required" errors for these fields if we have a source
                    ModelState.Remove("Video_File");
                    ModelState.Remove("YouTubeUrl");
                }
            }

            if (isDraft)
            {
                if (string.IsNullOrWhiteSpace(courseVM.Title))
                {
                    TempData["Alert"] = "Course Title is required even for drafts.";
                    TempData["AlertType"] = "danger";
                    return View(courseVM);
                }
            }
            else
            {
                // Handle temporary thumbnail preservation during validation errors
                if (thumbnail_url != null && thumbnail_url.Length > 0)
                {
                    try { courseVM.Temp_Thumbnail_Url = _mediaService.SaveThumbnail(thumbnail_url); } catch { /* ignore */ }
                }

                // Check ModelState only for submission
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors)
                        .Select(e => !string.IsNullOrWhiteSpace(e.ErrorMessage) ? e.ErrorMessage : e.Exception?.Message)
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .ToList();

                    // If we still don't have a video after cleaning ModelState, add a custom error
                    if (!errors.Any(e => e.Contains("video", StringComparison.OrdinalIgnoreCase)))
                    {
                        bool hasVideo = !string.IsNullOrWhiteSpace(YouTubeUrl) || (Video_File != null && Video_File.Length > 0);
                        if (!hasVideo)
                        {
                            errors.Add("Please provide an intro video (YouTube link or File).");
                        }
                    }

                    TempData["Alert"] = string.Join(" ", errors);
                    TempData["AlertType"] = "danger";
                    // Return same view with data
                    return View(courseVM);
                }
            }

            // Save course via service (Update the existing draft)
            var course = _courseManagementService.UpdateCourse(courseVM, InstructorId, thumbnail_url, Video_File, YouTubeUrl, videoType, submitAction);
            if (course.message == CourseMessage.EmptyFields)
            {
                TempData["Alert"] = !string.IsNullOrWhiteSpace(course.TechnicalMessage)
                    ? course.TechnicalMessage
                    : "Please enter all required course details in the correct format.";
                TempData["AlertType"] = "danger";
                return View(courseVM);
            }
            if (!course.Success)
            {
                TempData["Alert"] = course.TechnicalMessage ?? "Course could not be added. Please check your input and try again.";
                TempData["AlertType"] = "danger";
                return View(courseVM);
            }

            TempData["Alert"] = isDraft ? "Course saved to draft successfully." : "Course submitted successfully for review.";
            TempData["AlertType"] = "success";
            return RedirectToAction("MyCourses", "Home");
        }

        // AutoSave AJAX Action
        [HttpPost]
        public IActionResult AutoSave(CourseVM courseVM, string OutcomesRaw)
        {
            var instructorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(instructorIdClaim, out var instructorId)) return Unauthorized();

            if (courseVM.Id <= 0) return BadRequest();

            if (!string.IsNullOrWhiteSpace(OutcomesRaw))
            {
                courseVM.outcome = OutcomesRaw
                    .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(o => o.Trim())
                    .Where(o => !string.IsNullOrWhiteSpace(o))
                    .ToList();
            }

            // Save as draft (ignore files for autosave to keep it fast)
            var result = _courseManagementService.UpdateCourse(courseVM, instructorId, null, null, null, null, "draft");
            
            return Json(new { success = result.Success, message = result.Success ? "Draft Saved" : "Save failed" });
        }

        // Handle course submission for review
        [HttpPost]
        public IActionResult SubmitForReview(int id)
        {
            var instructorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(instructorIdClaim, out var instructorId))
            {
                return RedirectToAction("InstructorLogin", "Auth");
            }

            var course = _context.Courses.FirstOrDefault(c => c.Id == id && c.instructor_id == instructorId);
            if (course == null) return NotFound();

            course.Status = CourseStatus.PendingReview;
            course.UpdatedAt = DateTime.UtcNow;
            _context.SaveChanges();

            TempData["Alert"] = "Course submitted for review successfully.";
            TempData["AlertType"] = "success";
            return RedirectToAction("CourseDetails", new { id = id });
        }

        // List instructor's courses
        [Authorize(Roles = "Instructor")]
        public IActionResult MyCourses(string? search = null, string? category = null, string? status = null)
        {
            var instructorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(instructorIdClaim, out var InstructorId))
            {
                return RedirectToAction("InstructorLogin", "Auth");
            }

            var mycourse = _courseManagementService.MyCourses(InstructorId, search, category, status);
            ViewBag.DeletedCourses = _courseManagementService.GetDeletedCourses(InstructorId);
            
            ViewBag.SearchTerm = search;
            ViewBag.CategoryFilter = category;
            ViewBag.StatusFilter = status;

            return View(mycourse);
        }

        // Detailed course overview for instructor
        public async Task<IActionResult> CourseDetails(int id)
        {
            var instructorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(instructorIdClaim, out var instructorId))
            {
                return RedirectToAction("InstructorLogin", "Auth");
            }
            var courseDetails = await _instructorService.GetInstructorCourseDetails(id, instructorId);
            if (courseDetails == null)
            {
                return NotFound();
            }
            return View(courseDetails);
        }

        // Show edit course page
        public IActionResult EditCourse(int id)
        {
            var instructorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(instructorIdClaim, out var instructorId))
            {
                return RedirectToAction("InstructorLogin", "Auth");
            }
            var course = _courseManagementService.GetCourseForEdit(id, instructorId);
            if (course == null)
            {
                return NotFound();
            }
            return View(course);
        }

        // Handle edit course submission
        [HttpPost]
        public IActionResult EditCourse(CourseVM courseVM, IFormFile? thumbnail_url, IFormFile? Video_File, string? YouTubeUrl, string? videoType, string? OutcomesRaw, string? submitAction)
        {
            var instructorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(instructorIdClaim, out var instructorId))
            {
                return RedirectToAction("InstructorLogin", "Auth");
            }

            // Repopulate for the view
            ViewBag.OutcomesRaw = OutcomesRaw;
            ViewBag.YouTubeUrl = YouTubeUrl;
            ViewBag.VideoType = videoType;

            // Parse outcomes
            if (!string.IsNullOrWhiteSpace(OutcomesRaw))
            {
                courseVM.outcome = OutcomesRaw
                    .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(o => o.Trim())
                    .Where(o => !string.IsNullOrWhiteSpace(o))
                    .ToList();
            }

            bool isDraft = submitAction?.ToLower() == "draft";

            // Clean ModelState for optional media fields before checking IsValid
            if (!isDraft)
            {
                // If either YouTube URL or Video File is provided, we don't want "required" errors for the other
                bool hasVideo = !string.IsNullOrWhiteSpace(YouTubeUrl) || (Video_File != null && Video_File.Length > 0);
                
                if (hasVideo)
                {
                    // Remove implicit "required" errors for these fields if we have a source
                    ModelState.Remove("Video_File");
                    ModelState.Remove("YouTubeUrl");
                }
            }

            if (isDraft)
            {
                if (string.IsNullOrWhiteSpace(courseVM.Title))
                {
                    TempData["Alert"] = "Course Title is required even for drafts.";
                    TempData["AlertType"] = "danger";
                    return View(courseVM);
                }
            }
            else
            {
                // Handle temporary thumbnail preservation during validation errors
                if (thumbnail_url != null && thumbnail_url.Length > 0)
                {
                    try { courseVM.Temp_Thumbnail_Url = _mediaService.SaveThumbnail(thumbnail_url); } catch { /* ignore */ }
                }

                // Check ModelState only for submission
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors)
                        .Select(e => !string.IsNullOrWhiteSpace(e.ErrorMessage) ? e.ErrorMessage : e.Exception?.Message)
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .ToList();

                    // If we still don't have a video after cleaning ModelState, add a custom error
                    if (!errors.Any(e => e.Contains("video", StringComparison.OrdinalIgnoreCase)))
                    {
                        bool hasVideo = !string.IsNullOrWhiteSpace(YouTubeUrl) || (Video_File != null && Video_File.Length > 0);
                        if (!hasVideo)
                        {
                            errors.Add("Please provide an intro video (YouTube link or File).");
                        }
                    }

                    TempData["Alert"] = string.Join(" ", errors);
                    TempData["AlertType"] = "danger";
                    return View(courseVM);
                }
            }

            // Update via service
            var result = _courseManagementService.UpdateCourse(courseVM, instructorId, thumbnail_url, Video_File, YouTubeUrl, videoType, submitAction);
            if (result.Success)
            {
                TempData["Alert"] = isDraft ? "Course saved to draft successfully." : "Course submitted successfully for review.";
                TempData["AlertType"] = "success";
                return RedirectToAction("MyCourses");
            }
            TempData["Alert"] = result.TechnicalMessage ?? "Failed to update course.";
            TempData["AlertType"] = "danger";
            return View(courseVM);
        }

        // Remove course and its data
        public IActionResult DeleteCourse(int id)
        {
            var instructorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(instructorIdClaim, out var instructorId))
            {
                return RedirectToAction("InstructorLogin", "Auth");
            }

            bool deleted = _courseManagementService.DeleteCourse(id, instructorId);
            if (deleted)
            {
                TempData["Alert"] = "Course deleted successfully.";
                TempData["AlertType"] = "success";
            }
            else
            {
                TempData["Alert"] = "Failed to delete course. Check permissions.";
                TempData["AlertType"] = "danger";
            }
            return RedirectToAction("MyCourses");
        }

        // Instructor earnings view
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> Earning(int? year, int? month)
        {
            var instructorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(instructorIdClaim, out var instructorId))
            {
                return RedirectToAction("InstructorLogin", "Auth");
            }

            var earningsData = await _analyticsService.GetInstructorEarningsDashboardAsync(instructorId, year, month);
            return View(earningsData);
        }

        // Instructor profile management view
        public async Task<IActionResult> Profile()
        {
            var instructorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(instructorIdClaim, out var instructorId))
            {
                return RedirectToAction("InstructorLogin", "Auth");
            }

            var vm = await _analyticsService.GetInstructorDashboardStatsAsync(instructorId);
            
            if (vm == null) return NotFound();

            // Preserve security tab state
            TempData.Keep("EmailSent");
            TempData.Keep("ForgotEmail");
            TempData.Keep("OtpVerified");
            TempData.Keep("VerifiedOtp");

            return View(vm);
        }

        // Handle profile update
        [HttpPost]
        public IActionResult Profile(InstructorProfile profile, IFormFile PhotoFile)
        {
            var instructorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(instructorIdClaim, out var instructorId))
            {
                return RedirectToAction("InstructorLogin", "Auth");
            }

            profile.InstructorId = instructorId;

            // Handle photo upload via MediaService
            if (PhotoFile != null && PhotoFile.Length > 0)
            {
                try
                {
                    profile.PhotoPath = _mediaService.SaveProfilePhoto(PhotoFile);
                }
                catch (Exception ex)
                {
                    TempData["Alert"] = ex.Message;
                    TempData["AlertType"] = "danger";
                    return RedirectToAction("Profile");
                }
            }
            else
            {
                var existing = _context.instructorProfiles.AsNoTracking().FirstOrDefault(p => p.InstructorId == instructorId);
                profile.PhotoPath = existing?.PhotoPath ?? "/images/DefaultProfilePhoto.jfif";
            }

            // Upsert profile
            var existingProfile = _context.instructorProfiles.FirstOrDefault(p => p.InstructorId == instructorId);
            if (existingProfile == null)
            {
                _context.instructorProfiles.Add(profile);
            }
            else
            {
                existingProfile.FirstName = profile.FirstName;
                existingProfile.LastName = profile.LastName;
                existingProfile.Mobile = profile.Mobile;
                existingProfile.Location = profile.Location;
                existingProfile.AboutYou = profile.AboutYou;
                existingProfile.CurrentRole = profile.CurrentRole;
                existingProfile.Expertise = profile.Expertise;
                existingProfile.YearsExperience = profile.YearsExperience;
                existingProfile.Headline = profile.Headline;
                existingProfile.WebsiteUrl = profile.WebsiteUrl;
                existingProfile.GithubUrl = profile.GithubUrl;
                existingProfile.LinkedinUrl = profile.LinkedinUrl;
                existingProfile.TwitterUrl = profile.TwitterUrl;
                existingProfile.Skills = profile.Skills;
                existingProfile.PhotoPath = profile.PhotoPath;
                _context.instructorProfiles.Update(existingProfile);
            }
            _context.SaveChanges();
            TempData["Alert"] = "Profile updated successfully.";
            TempData["AlertType"] = "success";
            return RedirectToAction("Profile");
        }

        // Handle mentor application submission
        [HttpPost]
        [Authorize(Roles = "Instructor")]
        public IActionResult ApplyMentor(MentorApplication application, IFormFile ResumeFile)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var instructorId)) return RedirectToAction("InstructorLogin", "Auth");

            // Ensure profile is complete before applying
            var profile = _context.instructorProfiles.FirstOrDefault(p => p.InstructorId == instructorId);
            if (profile == null ||
                string.IsNullOrWhiteSpace(profile.FirstName) ||
                string.IsNullOrWhiteSpace(profile.LastName) ||
                string.IsNullOrWhiteSpace(profile.Mobile) ||
                string.IsNullOrWhiteSpace(profile.Location) ||
                string.IsNullOrWhiteSpace(profile.CurrentRole) ||
                string.IsNullOrWhiteSpace(profile.Expertise) ||
                profile.YearsExperience == null ||
                string.IsNullOrWhiteSpace(profile.Skills) ||
                string.IsNullOrWhiteSpace(profile.AboutYou))
            {
                TempData["Alert"] = "Please complete all Personal and Professional details in your profile before applying as a mentor.";
                TempData["AlertType"] = "warning";
                return RedirectToAction("Profile");
            }

            // Basic validation
            if (string.IsNullOrWhiteSpace(application.WhyTeach) || string.IsNullOrWhiteSpace(application.Topics))
            {
                TempData["Alert"] = "Please fill in all required fields.";
                TempData["AlertType"] = "warning";
                return RedirectToAction("Profile", new { tab = "application" });
            }

            // Handle resume upload via MediaService
            if (ResumeFile != null && ResumeFile.Length > 0)
            {
                try
                {
                    application.ResumePath = _mediaService.UploadResume(ResumeFile);
                }
                catch (Exception ex)
                {
                    TempData["Alert"] = ex.Message;
                    TempData["AlertType"] = "danger";
                    return RedirectToAction("Profile", new { tab = "application" });
                }
            }

            application.InstructorId = instructorId;
            application.Status = MentorApplicationStatus.Pending;
            application.CreatedAt = DateTime.UtcNow;

            _context.MentorApplications.Add(application);
            _context.SaveChanges();

            TempData["Alert"] = "Application submitted successfully! Our team will review it soon.";
            TempData["AlertType"] = "success";
            return RedirectToAction("Profile", new { tab = "application" });
        }

        // Update course syllabus modules/lessons
        [HttpPost]
        public IActionResult UpdateSyllabus(int id, List<ModuleVM> Syllabus)
        {
            var instructorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(instructorIdClaim, out var instructorId))
            {
                return RedirectToAction("InstructorLogin", "Auth");
            }

            // Fetch existing course
            var courseVM = _courseManagementService.GetCourseForEdit(id, instructorId);
            if (courseVM == null)
            {
                return NotFound();
            }

            // Update syllabus only
            courseVM.Syllabus = Syllabus;

            // Save via management service
            var result = _courseManagementService.UpdateCourse(courseVM, instructorId, null, null, null, null, "draft");
            if (result.Success)
            {
                TempData["Alert"] = "Syllabus updated successfully.";
                TempData["AlertType"] = "success";
            }
            else
            {
                TempData["Alert"] = result.TechnicalMessage ?? "Failed to update syllabus.";
                TempData["AlertType"] = "danger";
            }

            return RedirectToAction("CourseDetails", new { id = id });
        }

        // Add new module to course
        [HttpPost]
        public IActionResult AddModule(int courseId, string moduleName)
        {
            var success = _courseContentService.AddModule(courseId, moduleName);
            if (success)
            {
                TempData["Alert"] = "Module added successfully.";
                TempData["AlertType"] = "success";
            }
            return RedirectToAction("CourseDetails", new { id = courseId });
        }

        // Add new lesson to module
        [HttpPost]
        public IActionResult AddLesson(int moduleId, int courseId, string title, string videoUrl)
        {
            var success = _courseContentService.AddLesson(moduleId, title, videoUrl);
            if (success)
            {
                TempData["Alert"] = "Lesson added successfully.";
                TempData["AlertType"] = "success";
            }
            return RedirectToAction("CourseDetails", new { id = courseId });
        }

        // Remove module from course
        [HttpPost]
        public IActionResult DeleteModule(int moduleId, int courseId)
        {
            var success = _courseContentService.DeleteModule(moduleId);
            if (success)
            {
                TempData["Alert"] = "Module deleted.";
                TempData["AlertType"] = "success";
            }
            return RedirectToAction("CourseDetails", new { id = courseId });
        }

        // Remove lesson from module
        [HttpPost]
        public IActionResult DeleteLesson(int lessonId, int courseId)
        {
            var success = _courseContentService.DeleteLesson(lessonId);
            if (success)
            {
                TempData["Alert"] = "Lesson deleted.";
                TempData["AlertType"] = "success";
            }
            return RedirectToAction("CourseDetails", new { id = courseId });
        }

        // ==========================================
        // SECURITY / PASSWORD MANAGEMENT
        // ==========================================

        [HttpPost]
        [Authorize(Roles = "Instructor")]
        public IActionResult UpdatePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                TempData["Alert"] = "Passwords do not match.";
                TempData["AlertType"] = "danger";
                return RedirectToAction("Profile", new { tab = "security", view = "change" });
            }

            var email = CurrentUserEmail();
            var result = _authService.ChangePassword(email, currentPassword, newPassword, "Instructor");

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
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> SendProfileOTP(string email)
        {
            var result = await _otpService.SendEmailOtp(email, "Instructor");
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
        [Authorize(Roles = "Instructor")]
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
                TempData["EmailSent"] = true;
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
        [Authorize(Roles = "Instructor")]
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

            var result = _authService.ResetPassword(email, newPassword, otp, "Instructor");
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
