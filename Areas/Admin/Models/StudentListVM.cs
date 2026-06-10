namespace SkillForge.Areas.Admin.Models
{
    public class StudentListVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int CourseCount { get; set; }
        public List<string> EnrolledCoursesList { get; set; } = new();
        public DateTime JoinedDate { get; set; }
        public string Status { get; set; } = "Active";
    }
}
