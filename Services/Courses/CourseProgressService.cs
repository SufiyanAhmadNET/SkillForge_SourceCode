using SkillForge.Data;
using SkillForge.Models;
using SkillForge.Interfaces;
using Microsoft.EntityFrameworkCore;
using SkillForge.Services.Courses.Models;

namespace SkillForge.Services.Courses
{
    public class CourseProgressService : ICourseProgressService
    {
        private readonly SkillForgeDbContext _context;
        public CourseProgressService(SkillForgeDbContext context)
        {
            _context = context;
        }

        public bool MarkLessonAsComplete(int studentId, int lessonId)
        {
            var existing = _context.UserProgress
                .FirstOrDefault(p => p.StudentId == studentId && p.LessonId == lessonId);
            if (existing != null)
            {
                existing.IsCompleted = !existing.IsCompleted;
                _context.UserProgress.Update(existing);
            }
            else
            {
                _context.UserProgress.Add(new UserLessonProgress
                {
                    StudentId = studentId,
                    LessonId = lessonId,
                    IsCompleted = true
                });
            }
            return _context.SaveChanges() > 0;
        }

        public List<int> GetCompletedLessonIds(int studentId, int courseId)
        {
            var lessonIds = _context.CourseModules
                .Where(m => m.CourseId == courseId)
                .SelectMany(m => m.Lessons)
                .Select(l => l.Id)
                .ToList();

            return _context.UserProgress
                .Where(p => p.StudentId == studentId && p.IsCompleted && lessonIds.Contains(p.LessonId))
                .Select(p => p.LessonId)
                .ToList();
        }

        public bool IsCourseFullyCompleted(int studentId, int courseId)
        {
            var totalLessons = _context.CourseModules
                .Where(m => m.CourseId == courseId)
                .SelectMany(m => m.Lessons)
                .Count();

            if (totalLessons == 0) return false;

            var lessonIds = _context.CourseModules
                .Where(m => m.CourseId == courseId)
                .SelectMany(m => m.Lessons)
                .Select(l => l.Id)
                .ToList();

            var completedCount = _context.UserProgress
                .Count(p => p.StudentId == studentId && p.IsCompleted && lessonIds.Contains(p.LessonId));

            return completedCount == totalLessons;
        }

        public Certificate? GetOrGenerateCertificate(int studentId, int courseId)
        {
            var existing = _context.Certificates
                .FirstOrDefault(c => c.StudentId == studentId && c.CourseId == courseId);

            if (existing != null) return existing;

            if (IsCourseFullyCompleted(studentId, courseId))
            {
                var certificate = new Certificate
                {
                    StudentId = studentId,
                    CourseId = courseId,
                    IssuedDate = DateTime.UtcNow,
                    CertificateNumber = $"SF-{DateTime.UtcNow.Year}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}"
                };

                _context.Certificates.Add(certificate);
                _context.SaveChanges();
                return certificate;
            }

            return null;
        }

        public List<CertificateVM> GetStudentCertificates(int studentId)
        {
            var enrollments = _context.Enrollments
                .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Active)
                .Include(e => e.Course)
                    .ThenInclude(c => c.CourseDetails)
                .ToList();

            var certificates = _context.Certificates
                .Where(c => c.StudentId == studentId)
                .ToList();

            var results = new List<CertificateVM>();

            foreach (var enrollment in enrollments)
            {
                var cert = certificates.FirstOrDefault(c => c.CourseId == enrollment.CourseId);
                
                var totalLessons = _context.CourseModules
                    .Where(m => m.CourseId == enrollment.CourseId)
                    .SelectMany(m => m.Lessons)
                    .Count();

                int progressPct = 0;
                if (totalLessons > 0)
                {
                    var lessonIds = _context.CourseModules
                        .Where(m => m.CourseId == enrollment.CourseId)
                        .SelectMany(m => m.Lessons)
                        .Select(l => l.Id)
                        .ToList();

                    var completedCount = _context.UserProgress
                        .Count(p => p.StudentId == studentId && p.IsCompleted && lessonIds.Contains(p.LessonId));

                    progressPct = (int)((float)completedCount / totalLessons * 100);
                }

                results.Add(new CertificateVM
                {
                    CourseId = enrollment.CourseId,
                    CourseTitle = enrollment.Course.Title,
                    CertificateNumber = cert?.CertificateNumber ?? string.Empty,
                    IssuedDate = cert?.IssuedDate,
                    IsEarned = cert != null,
                    ProgressPercentage = progressPct,
                    ThumbnailUrl = enrollment.Course.CourseDetails?.Thumbnail_Url
                });
            }

            return results;
        }
    }
}
