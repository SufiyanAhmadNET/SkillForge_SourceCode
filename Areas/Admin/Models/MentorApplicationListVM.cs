using SkillForge.Services.Instructors.Models;

namespace SkillForge.Areas.Admin.Models
{
    public class MentorApplicationListVM
    {
        public int Id { get; set; }
        public int InstructorId { get; set; }
        public string InstructorName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Expertise { get; set; } = string.Empty;
        public int Experience { get; set; }
        public DateTime AppliedDate { get; set; }
        public MentorApplicationStatus Status { get; set; }
        public string? ResumePath { get; set; }
    }
}
