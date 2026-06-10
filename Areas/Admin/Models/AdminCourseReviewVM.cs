using SkillForge.Services.Courses.Models;

namespace SkillForge.Areas.Admin.Models
{
    public class AdminCourseReviewVM
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string InstructorName { get; set; }
        public string Category { get; set; }
        public CourseStatus Status { get; set; }
        public DateTime SubmittedDate { get; set; }
        public int ModuleCount { get; set; }
        public int LessonCount { get; set; }
        public string? ThumbnailUrl { get; set; }
        public decimal Price { get; set; }
        public int DurationWeeks { get; set; }
    }
}
