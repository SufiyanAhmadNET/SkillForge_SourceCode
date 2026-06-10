using Microsoft.EntityFrameworkCore;
using SkillForge.Data;
using SkillForge.Models;
using SkillForge.Interfaces;
using SkillForge.Services.Courses.Models;

namespace SkillForge.Services.Courses
{
    public class CourseQueryService : ICourseQueryService
    {
        private readonly SkillForgeDbContext _context;
        public CourseQueryService(SkillForgeDbContext context)
        {
            _context = context;
        }
        public CoursePageVM GetPublishedCoursePage(int studentId = 0)
        {
            var published = _context.Courses
                .Where(c => c.Status == CourseStatus.Approved || c.Status == CourseStatus.Published)
                .Include(c => c.CourseDetails)
                .Include(c => c.courseCategory)
                .ToList();
            var wishlistedIds = studentId > 0 
                ? _context.Wishlists.Where(w => w.StudentId == studentId).Select(w => w.CourseId).ToList() 
                : new List<int>();
            var popular = published
                .OrderByDescending(c => c.Id)
                .Take(8)
                .Select(c => new CourseCardVM
                {
                    Title = c.Title,
                    SubTitle = !string.IsNullOrWhiteSpace(c.CourseDetails?.ShortSummary) ? c.CourseDetails.ShortSummary : c.CourseDetails?.Description,
                    ShortSummary = !string.IsNullOrWhiteSpace(c.CourseDetails?.ShortSummary) ? c.CourseDetails.ShortSummary : c.CourseDetails?.Description,
                    CategoryName = c.courseCategory?.Name ?? "Uncategorized",
                    Difficulty = c.CourseDetails?.Difficulty.ToString() ?? "None",
                    Total_Price = c.CourseDetails?.Total_Price ?? 0,
                    Actual_Price = c.CourseDetails?.Actual_Price ?? 0,
                    Discount_Percent = c.CourseDetails != null ? c.CourseDetails.Discount_Percent : 0,
                    Thumbnail_Url = c.CourseDetails?.Thumbnail_Url,
                    courseId = c.Id,
                    IsWishListed = wishlistedIds.Contains(c.Id)
                }).ToList();
            var categories = published
                .GroupBy(c => c.courseCategory?.Name ?? "Uncategorized")
                .Select(g => new CategorySectionVM
                {
                    CategoryName = g.Key,
                    Courses = g.Select(c => new CourseCardVM
                    {
                        Title = c.Title,
                        SubTitle = !string.IsNullOrWhiteSpace(c.CourseDetails?.ShortSummary) ? c.CourseDetails.ShortSummary : c.CourseDetails?.Description,
                        ShortSummary = !string.IsNullOrWhiteSpace(c.CourseDetails?.ShortSummary) ? c.CourseDetails.ShortSummary : c.CourseDetails?.Description,
                        CategoryName = c.courseCategory?.Name ?? "Uncategorized",
                        Difficulty = c.CourseDetails?.Difficulty.ToString() ?? "None",
                        Total_Price = c.CourseDetails?.Total_Price ?? 0,
                        Actual_Price = c.CourseDetails?.Actual_Price ?? 0,
                        Discount_Percent = c.CourseDetails != null ? c.CourseDetails.Discount_Percent : 0,
                        Thumbnail_Url = c.CourseDetails?.Thumbnail_Url,
                        courseId = c.Id,
                        IsWishListed = wishlistedIds.Contains(c.Id)
                    }).ToList()
                }).ToList();
            return new CoursePageVM
            {
                PopularCourses = popular,
                CategorySections = categories
            };
        }
        public CourseDetailsVM? GetCourseDetails(int courseId, int studentId = 0)
        {
            var course = _context.Courses
                .Where(c => c.Id == courseId)
                .Include(c => c.CourseDetails)
                .Include(c => c.courseCategory)
                .Include(c => c.CourseOutcomes)
                .FirstOrDefault();
            if (course == null) return null;
            var modules = _context.CourseModules
                .Where(m => m.CourseId == courseId)
                .Include(m => m.Lessons)
                .OrderBy(m => m.Id)
                .ToList();
            bool isWishlisted = false;
            if (studentId > 0)
            {
                isWishlisted = _context.Wishlists.Any(w => w.StudentId == studentId && w.CourseId == courseId);
            }
            return new CourseDetailsVM
            {
                CourseId = course.Id,
                Title = course.Title,
                Desciption = course.CourseDetails?.Description,
                ShortSummary = course.CourseDetails?.ShortSummary,
                VideoUrl = course.CourseDetails?.Intro_Video_Url,
                ActualPrice = course.CourseDetails?.Actual_Price ?? 0,
                TotalPrice = course.CourseDetails?.Total_Price ?? 0,
                DiscountPercent = (float)(course.CourseDetails?.Discount_Percent ?? 0),
                outcomes = course.CourseOutcomes?.ToList() ?? new List<CourseOutcomes>(),
                SubTitle = course.CourseDetails?.ShortSummary ?? string.Empty,
                IsWishlisted = isWishlisted,
                modules = modules,
                Duration = course.CourseDetails?.Duration_Weeks ?? 0,
                Difficulty = course.CourseDetails?.Difficulty.ToString(),
                ThumbnailUrl = course.CourseDetails?.Thumbnail_Url,
                CategoryName = course.courseCategory?.Name
            };
        }
    }
}
