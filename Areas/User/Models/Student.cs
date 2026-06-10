using System.ComponentModel.DataAnnotations;

namespace SkillForge.Areas.User.Models
{
    public class Student
    {
        [Key]
        public int Id { get; set; }
        public required string Email { get; set; } = null!;
        public string? Password { get; set; }
        public string? EmailOtp { get; set; }
        public DateTime? OtpExpiry { get; set; }
        public bool IsEmailVerified { get; set; } 
 
        //for Google Login
        public string? GoogleId { get; set; }
        //Navigation Property
        public StudentProfile? Profile { get; set; }
        public DateTime CreatedAt { get; internal set; }
    }

}
