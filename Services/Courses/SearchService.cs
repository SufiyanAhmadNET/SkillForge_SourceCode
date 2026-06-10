using Microsoft.EntityFrameworkCore;
using SkillForge.Areas.Admin.Models;
using SkillForge.Data;
using SkillForge.Interfaces;
using SkillForge.Models;
using SkillForge.Services.Courses.Models;

namespace SkillForge.Services.Courses
{
    // Search service implementation
    public class SearchService : ISearchService
    {
        private readonly SkillForgeDbContext _context;

        public SearchService(SkillForgeDbContext context)
        {
            _context = context;
        }

        public SearchResultVM SearchCourses(string keyword, int studentId = 0)
        {
            var result = new SearchResultVM { Keyword = keyword };

            if (string.IsNullOrWhiteSpace(keyword)) return result;

            var searchTerm = keyword.ToLower().Trim();

            // Fetch wishlisted courses for the student
            var wishlistedIds = studentId > 0
                ? _context.Wishlists.AsNoTracking()
                    .Where(w => w.StudentId == studentId)
                    .Select(w => w.CourseId).ToList()
                : new List<int>();

            // Fetch enrolled course IDs if studentId > 0
            var enrolledIds = studentId > 0
                ? _context.Enrollments.AsNoTracking()
                    .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Active)
                    .Select(e => e.CourseId).ToList()
                : new List<int>();

            // 1. Search Logic: Filter approved/published courses
            var query = _context.Courses.AsNoTracking()
                .Where(c => c.Status == CourseStatus.Approved || c.Status == CourseStatus.Published)
                .Include(c => c.CourseDetails)
                .Include(c => c.courseCategory)
                .AsQueryable();

            // Filter by keyword
            var matchedCourses = query.Where(c =>
                c.Title.ToLower().Contains(searchTerm) ||
                (c.CourseDetails != null && c.CourseDetails.ShortSummary != null && c.CourseDetails.ShortSummary.ToLower().Contains(searchTerm)) ||
                (c.CourseDetails != null && c.CourseDetails.Description != null && c.CourseDetails.Description.ToLower().Contains(searchTerm)) ||
                (c.courseCategory != null && c.courseCategory.Name.ToLower().Contains(searchTerm))
            ).ToList();

            if (!matchedCourses.Any()) return result;

            //  Exact Matches (prioritize enrolled courses if logged in)
            result.ExactMatches = matchedCourses
                .OrderByDescending(c => enrolledIds.Contains(c.Id))
                .Take(8)
                .Select(c => MapToCard(c, wishlistedIds))
                .ToList();

            // Related Courses (same categories as matches, exclude matches)
            var matchedCategoryIds = matchedCourses.Select(c => c.category_id).Distinct().ToList();
            var matchedCourseIds = matchedCourses.Select(c => c.Id).ToList();

            result.RelatedCourses = query
                .Where(c => matchedCategoryIds.Contains(c.category_id) && !matchedCourseIds.Contains(c.Id))
                .Take(6)
                .Select(c => MapToCard(c, wishlistedIds))
                .ToList();

            return result;
        }

        public List<AdminCourseReviewVM> SearchAdminCourses(string? keyword, string? category, string? status)
        {
            var query = _context.Courses.AsNoTracking()
                .Include(c => c.courseCategory)
                .Include(c => c.CourseDetails)
                .AsQueryable();

            // Admin visibility rules: 
            // Hide Drafts and Deleted Drafts (courses missing required submission media)
            query = query.Where(c => c.Status != CourseStatus.Draft && 
                                    (c.Status != CourseStatus.Deleted || 
                                     (c.CourseDetails != null && 
                                      !string.IsNullOrEmpty(c.CourseDetails.Thumbnail_Url) && 
                                      !string.IsNullOrEmpty(c.CourseDetails.Intro_Video_Url))));

            // Filter by keyword (Title or Instructor Name/Email)
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var term = keyword.ToLower().Trim();
                
                //  fetch matching instructor IDs
                var matchingInstructorIds = _context.instructors
                    .Include(i => i.Profile)
                    .Where(i => i.Email.ToLower().Contains(term) || 
                                (i.Profile != null && (i.Profile.FirstName + " " + i.Profile.LastName).ToLower().Contains(term)))
                    .Select(i => i.Id)
                    .ToList();

                query = query.Where(c => c.Title.ToLower().Contains(term) || matchingInstructorIds.Contains(c.instructor_id));
            }

            // Filter by category
            if (!string.IsNullOrWhiteSpace(category) && category != "all")
            {
                query = query.Where(c => c.courseCategory != null && c.courseCategory.Name == category);
            }

            // Filter by status
            if (!string.IsNullOrWhiteSpace(status) && status != "all")
            {
                if (Enum.TryParse<CourseStatus>(status, true, out var courseStatus))
                {
                    query = query.Where(c => c.Status == courseStatus);
                }
            }

            var instructors = _context.instructors.Include(i => i.Profile).AsNoTracking().ToList();
            var modules = _context.CourseModules.Include(m => m.Lessons).AsNoTracking().ToList();

            return query.ToList().Select(c => {
                var instructor = instructors.FirstOrDefault(i => i.Id == c.instructor_id);
                var courseModules = modules.Where(m => m.CourseId == c.Id).ToList();
                
                return new AdminCourseReviewVM
                {
                    Id = c.Id,
                    Title = c.Title,
                    InstructorName = instructor?.Profile != null ? (instructor.Profile.FirstName + " " + instructor.Profile.LastName).Trim() : "Unknown",
                    Category = c.courseCategory?.Name ?? "Uncategorized",
                    Status = c.Status,
                    SubmittedDate = c.UpdatedAt,
                    ModuleCount = courseModules.Count,
                    LessonCount = courseModules.Sum(m => m.Lessons.Count),
                    ThumbnailUrl = c.CourseDetails?.Thumbnail_Url,
                    Price = c.CourseDetails?.Total_Price ?? 0,
                    DurationWeeks = c.CourseDetails?.Duration_Weeks ?? 0
                };
            })
            .OrderByDescending(c => c.Status == CourseStatus.PendingReview)
            .ThenByDescending(c => c.SubmittedDate)
            .ToList();
        }

        public List<StudentListVM> SearchStudents(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return new List<StudentListVM>();

            var searchTerm = keyword.ToLower().Trim();

            // Fetch students matching name or email
            var students = _context.Students
                .Include(s => s.Profile)
                .Where(s => s.Email.ToLower().Contains(searchTerm) ||
                            (s.Profile != null && (s.Profile.FirstName + " " + s.Profile.LastName).ToLower().Contains(searchTerm)))
                .ToList();

            // Fetch all enrollments for matching students to avoid N+1
            var studentIds = students.Select(s => s.Id).ToList();
            var enrollments = _context.Enrollments
                .Include(e => e.Course)
                .Where(e => studentIds.Contains(e.StudentId))
                .ToList();

            return students.Select(s => new StudentListVM
            {
                Id = s.Id,
                Name = s.Profile != null ? (s.Profile.FirstName + " " + s.Profile.LastName).Trim() : s.Email.Split('@')[0],
                Email = s.Email,
                CourseCount = enrollments.Count(e => e.StudentId == s.Id),
                EnrolledCoursesList = enrollments.Where(e => e.StudentId == s.Id).Select(e => e.Course.Title).ToList(),
                JoinedDate = s.CreatedAt,
                Status = "Active"
            }).ToList();
        }

        public List<InstructorListVM> SearchInstructors(string keyword, string? status = null)
        {
            var searchTerm = (keyword ?? "").ToLower().Trim();

            var query = _context.instructors
                .Include(i => i.Profile)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(i => i.Email.ToLower().Contains(searchTerm) ||
                                         (i.Profile != null && (i.Profile.FirstName + " " + i.Profile.LastName).ToLower().Contains(searchTerm)));
            }

            var instructors = query.ToList();
            var applications = _context.MentorApplications.ToList();
            var courses = _context.Courses.ToList();
            var enrollments = _context.Enrollments.ToList();

            var result = instructors.Select(i => {
                var app = applications.Where(a => a.InstructorId == i.Id).OrderByDescending(a => a.CreatedAt).FirstOrDefault();
                var activeInstructorCoursesCount = courses.Count(c => c.instructor_id == i.Id && (c.Status == CourseStatus.Approved || c.Status == CourseStatus.Published));
                var instructorCourses = courses.Where(c => c.instructor_id == i.Id).Select(c => c.Id).ToList();
                var instructorStudents = enrollments.Count(e => instructorCourses.Contains(e.CourseId));

                return new InstructorListVM
                {
                    Id = i.Id,
                    Name = i.Profile != null ? (i.Profile.FirstName + " " + i.Profile.LastName).Trim() : i.Email.Split('@')[0],
                    Email = i.Email,
                    TotalCourses = activeInstructorCoursesCount,
                    TotalStudents = instructorStudents,
                    JoinedDate = i.CreatedAt,
                    Status = app?.Status.ToString() ?? "NotApplied"
                };
            });

            if (!string.IsNullOrWhiteSpace(status) && status != "all")
            {
                result = result.Where(i => i.Status == status);
            }

            return result.ToList();
        }

        // Map Course entity to CourseCardVM
        private static CourseCardVM MapToCard(Course c, List<int> wishlistedIds)
        {
            return new CourseCardVM
            {
                courseId = c.Id,
                Title = c.Title,
                SubTitle = c.CourseDetails?.ShortSummary ?? c.CourseDetails?.Description,
                ShortSummary = c.CourseDetails?.ShortSummary ?? c.CourseDetails?.Description,
                CategoryName = c.courseCategory?.Name ?? "Uncategorized",
                Difficulty = c.CourseDetails?.Difficulty.ToString() ?? "None",
                Total_Price = c.CourseDetails?.Total_Price ?? 0,
                Actual_Price = c.CourseDetails?.Actual_Price ?? 0,
                Discount_Percent = c.CourseDetails?.Discount_Percent ?? 0,
                Thumbnail_Url = c.CourseDetails?.Thumbnail_Url,
                IsWishListed = wishlistedIds.Contains(c.Id)
            };
        }
    }
}
