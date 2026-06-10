namespace SkillForge.Services.Auth.Models
{
    public class AuthResult
    {
        public bool Success { get; set; }
        public string Role { get; set; } = string.Empty;
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? PhotoPath { get; set; }

        //get messages status from AuthMessage enum class
        public AuthMessage status { get; set; }
    }
}
