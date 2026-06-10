using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SkillForge.Services.Instructors.Models;

namespace SkillForge.Areas.Instructor.Models
{
    public class MentorApplication
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int InstructorId { get; set; }

        public string? ResumePath { get; set; }

        [Required]
        public string WhyTeach { get; set; } = string.Empty;

        [Required]
        public string Topics { get; set; } = string.Empty;

        public string? PortfolioUrl { get; set; }

        public MentorApplicationStatus Status { get; set; } = MentorApplicationStatus.Pending;

        public string? AdminComment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }

        // Navigation property
        [ForeignKey("InstructorId")]
        public Instructor? Instructor { get; set; }
    }
}
