using Microsoft.EntityFrameworkCore;
using SkillForge.Data;
using SkillForge.Models;
using SkillForge.Interfaces;
using SkillForge.Services.Courses.Models;

namespace SkillForge.Services.Courses
{
    public class CourseContentService : ICourseContentService
    {
        private readonly SkillForgeDbContext _context;

        public CourseContentService(SkillForgeDbContext context)
        {
            _context = context;
        }

        public bool AddModule(int courseId, string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName)) return false;
            var module = new CourseModules
            {
                CourseId = courseId,
                ModuleName = moduleName
            };
            _context.CourseModules.Add(module);
            ResetCourseStatus(courseId);
            return _context.SaveChanges() > 0;
        }

        public bool AddLesson(int moduleId, string title, string videoUrl)
        {
            if (string.IsNullOrWhiteSpace(title)) return false;
            var module = _context.CourseModules.Find(moduleId);
            if (module == null) return false;

            var lesson = new CourseLesson
            {
                ModuleId = moduleId,
                Title = title,
                VideoUrl = videoUrl,
                Order = _context.CourseLessons.Count(l => l.ModuleId == moduleId) + 1
            };
            _context.CourseLessons.Add(lesson);
            ResetCourseStatus(module.CourseId);
            return _context.SaveChanges() > 0;
        }

    

        public bool DeleteLesson(int lessonId)
        {
            var lesson = _context.CourseLessons.Include(l => l.Module).FirstOrDefault(l => l.Id == lessonId);
            if (lesson == null) return false;
            int courseId = lesson.Module.CourseId;
            _context.CourseLessons.Remove(lesson);
            ResetCourseStatus(courseId);
            return _context.SaveChanges() > 0;
        }

        private void ResetCourseStatus(int courseId)
        {
            var course = _context.Courses.Find(courseId);
            if (course != null && (course.Status == CourseStatus.Approved || course.Status == CourseStatus.Published))
            {
                course.Status = CourseStatus.Draft;
                course.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
