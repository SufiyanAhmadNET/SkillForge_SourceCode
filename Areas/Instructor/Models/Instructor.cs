using System.ComponentModel.DataAnnotations;
//using SkillForge.Models;
namespace SkillForge.Areas.Instructor.Models
{
    public class Instructor
    {
        [Key]
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;

        public string? Password { get; set; }
        public string? EmailOtp { get; set; }
        public DateTime? OtpExpiry { get; set; }

        public bool IsEmailVerified { get; set; }

        //for Google Login
        public string? GoogleId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        //navigation Property
        public InstructorProfile? Profile { get; set; }
    }
}
