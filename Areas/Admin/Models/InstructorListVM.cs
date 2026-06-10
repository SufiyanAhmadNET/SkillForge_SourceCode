namespace SkillForge.Areas.Admin.Models
{
    public class InstructorListVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int TotalCourses { get; set; }
        public int TotalStudents { get; set; }
        public DateTime JoinedDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
