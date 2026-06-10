using SkillForge.Services.Courses.Models;
using SkillForge.Services.Instructors.Models;

namespace SkillForge.Areas.Admin.Models
{
    public class PendingApprovalsVM
    {
        public List<MentorApplicationListVM> Applications { get; set; } = new();
        public List<AdminCourseReviewVM> Courses { get; set; } = new();
        
        // Stats for quick view
        public int PendingApplicationsCount => Applications.Count(a => a.Status == MentorApplicationStatus.Pending);
        public int PendingCoursesCount => Courses.Count(c => c.Status == CourseStatus.PendingReview);
    }
}
