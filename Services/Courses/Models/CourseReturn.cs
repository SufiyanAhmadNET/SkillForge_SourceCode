using SkillForge.Models;

namespace SkillForge.Services.Courses.Models
{
    public class CourseReturn
    {

        public bool Success { get; set; }

        //get messages  CourseMessage enum class
        public CourseMessage message { get; set; }
        //return course data after saved to DB
        public Course? courseData { get; set; }
        public string? TechnicalMessage { get; set; }
    }
}
