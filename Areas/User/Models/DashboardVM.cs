using System.ComponentModel.DataAnnotations;

namespace SkillForge.Areas.User.Models
{
    public class DashboardVM
    {
        public int Id { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Mobile { get; set; }
        public string? City { get; set; }
        public string? Interests { get; set; }
        public string? PhotoPath { get; set; }

        // stats
        public int EnrolledCount { get; set; }
        public int CompletedCount { get; set; }
        public int CertificateCount { get; set; }
        public int WishlistCount { get; set; }

        // course lists
        public List<SkillForge.Models.CourseCardVM> EnrolledCourses { get; set; } = new();
        public List<SkillForge.Models.CourseCardVM> RecommendedCourses { get; set; } = new();
        public List<SkillForge.Models.CertificateVM> Certificates { get; set; } = new();
  
    }
}
