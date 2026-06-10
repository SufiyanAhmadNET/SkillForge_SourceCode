using Microsoft.EntityFrameworkCore;
using SkillForge.Areas.Instructor.Models;
using SkillForge.Data;
using SkillForge.Models;
using SkillForge.Interfaces;
using SkillForge.Services.Instructors.Models;
using SkillForge.Services.Courses.Models;

namespace SkillForge.Services.Instructors
{
    public class InstructorService : IInstructorService
    {
        private readonly SkillForgeDbContext _context;
        private readonly IAnalyticsService _analyticsService;

        public InstructorService(SkillForgeDbContext context, IAnalyticsService analyticsService)
        {
            _context = context;
            _analyticsService = analyticsService;
        }

        public async Task<CourseDetailsVM?> GetInstructorCourseDetails(int courseId, int instructorId)
        {
            var course = _context.Courses
                .Where(c => c.Id == courseId && c.instructor_id == instructorId)
                .Include(c => c.CourseDetails)
                .Include(c => c.CourseOutcomes)
                .Include(c => c.courseCategory)
                .FirstOrDefault();
            if (course == null) return null;

            var courseLessons = _context.CourseModules
                .Where(m => m.CourseId == courseId)
                .SelectMany(m => m.Lessons)
                .ToList();
            var totalLessons = courseLessons.Count;
            var enrollments = _context.Enrollments
                .Where(e => e.CourseId == courseId && e.Status == EnrollmentStatus.Active)
                .Include(e => e.Student)
                    .ThenInclude(s => s.Profile)
                .ToList();
            var studentIds = enrollments.Select(e => e.StudentId).ToList();
            var allProgress = _context.UserProgress
                .Where(p => studentIds.Contains(p.StudentId) && p.IsCompleted)
                .ToList();

            var earnings = await _analyticsService.GetCourseRevenueAsync(courseId);

            return new CourseDetailsVM
            {
                CourseId = course.Id,
                Title = course.Title,
                Desciption = course.CourseDetails?.Description,
                VideoUrl = course.CourseDetails?.Intro_Video_Url,
                ActualPrice = course.CourseDetails?.Actual_Price ?? 0,
                TotalPrice = course.CourseDetails?.Total_Price ?? 0,
                DiscountPercent = (float)(course.CourseDetails?.Discount_Percent ?? 0),
                outcomes = course.CourseOutcomes?.ToList() ?? new List<CourseOutcomes>(),
                SubTitle = course.CourseDetails?.Description?.Split('.').FirstOrDefault() ?? string.Empty,
                Status = course.Status.ToString(),
                RejectionReason = course.Rejection_Reason,
                Duration = course.CourseDetails?.Duration_Weeks ?? 0,
                Difficulty = course.CourseDetails?.Difficulty.ToString(),
                CategoryName = course.courseCategory?.Name,
                ThumbnailUrl = course.CourseDetails?.Thumbnail_Url,
                CourseEarnings = earnings,
                modules = _context.CourseModules.Where(m => m.CourseId == courseId).Include(m => m.Lessons).ToList(),
                EnrolledStudents = enrollments.Select(e => {
                    var studentCompleted = allProgress.Count(p => p.StudentId == e.StudentId && courseLessons.Any(cl => cl.Id == p.LessonId));
                    var progress = totalLessons > 0 ? (int)((float)studentCompleted / totalLessons * 100) : 0;
                    
                    return new StudentEnrollmentVM
                    {
                        StudentId = e.StudentId,
                        Name = e.Student.Profile?.FirstName ?? e.Student.Email.Split('@')[0],
                        Email = e.Student.Email,
                        EnrolledAt = e.EnrolledAt.ToString("MMM dd, yyyy"),
                        PhotoPath = e.Student.Profile?.PhotoPath ?? "/images/DefaultProfilePhoto.jfif",
                        Initial = (e.Student.Profile?.FirstName ?? e.Student.Email).Substring(0, 1).ToUpper(),
                        Progress = progress 
                    };
                }).ToList()
            };
        }
    }
}
