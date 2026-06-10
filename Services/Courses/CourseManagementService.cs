using Microsoft.EntityFrameworkCore;
using SkillForge.Data;
using SkillForge.Models;
using SkillForge.Interfaces;
using SkillForge.Services.Courses.Models;

namespace SkillForge.Services.Courses
{
    // Course management service implementation
    public class CourseManagementService : ICourseManagementService
    {
        private readonly SkillForgeDbContext _context;
        private readonly IMediaService _mediaService;
        public CourseManagementService(SkillForgeDbContext context, IMediaService mediaService)
        {
            _context = context;
            _mediaService = mediaService;
        }

        // Create a new empty draft course
        public Course CreateEmptyDraft(int instructorId)
        {
            // Reuse existing empty draft if any (created in last 1 hour and has placeholder title)
            var existingDraft = _context.Courses
                .FirstOrDefault(c => c.instructor_id == instructorId && 
                                   c.Status == CourseStatus.Draft && 
                                   c.Title == "Untitled Course" &&
                                   c.CreatedAt > DateTime.UtcNow.AddHours(-1));
            
            if (existingDraft != null) return existingDraft;

            var course = new Course
            {
                Title = "Untitled Course",
                instructor_id = instructorId,
                Status = CourseStatus.Draft,
                category_id = _context.course_Categories.FirstOrDefault()?.Id ?? 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CourseDetails = new CourseDetails
                {
                    Description = "No description provided yet.",
                    Difficulty = Course_Difficulty.Beginner
                }
            };

            _context.Courses.Add(course);
            _context.SaveChanges();
            return course;
        }

        // Add new course with details and syllabus
        public CourseReturn AddCourse(CourseVM courseVM, int instructorId, IFormFile? thumbnailFile, IFormFile? videoFile, string? youtubeUrl, string? videoType, string? submitAction = "draft")
        {
            try
            {
                EnsureDefaultCategories();
                
                bool isSubmitAction = submitAction?.ToLower() == "submit";

                // Input validation (Only strict for Submit)
                if (isSubmitAction)
                {
                    if (courseVM == null)
                        return new CourseReturn { Success = false, message = CourseMessage.EmptyFields, TechnicalMessage = "Invalid course data." };
                    if (string.IsNullOrWhiteSpace(courseVM.Title))
                        return new CourseReturn { Success = false, message = CourseMessage.EmptyFields, TechnicalMessage = "Course title is required." };
                    if (string.IsNullOrWhiteSpace(courseVM.Description))
                        return new CourseReturn { Success = false, message = CourseMessage.EmptyFields, TechnicalMessage = "Course description is required." };
                    if (string.IsNullOrWhiteSpace(courseVM.ShortSummary))
                        return new CourseReturn { Success = false, message = CourseMessage.EmptyFields, TechnicalMessage = "Short summary is required." };
                    if (!courseVM.Duration_Weeks.HasValue || courseVM.Duration_Weeks.Value <= 0)
                        return new CourseReturn { Success = false, message = CourseMessage.EmptyFields, TechnicalMessage = "Duration must be greater than 0 weeks." };
                    if (courseVM.Actual_Price < 0)
                        return new CourseReturn { Success = false, message = CourseMessage.EmptyFields, TechnicalMessage = "Price cannot be negative." };
                    
                    // Clean outcomes
                    var outcomesCheck = courseVM.outcome?
                        .Select(o => o?.Trim())
                        .Where(o => !string.IsNullOrWhiteSpace(o))
                        .ToList() ?? new List<string>();
                    
                    if (!outcomesCheck.Any())
                        return new CourseReturn { Success = false, message = CourseMessage.EmptyFields, TechnicalMessage = "Add at least one learning outcome." };
                }
                
                // Clean outcomes for saving
                var outcomes = courseVM.outcome?
                    .Select(o => o?.Trim())
                    .Where(o => !string.IsNullOrWhiteSpace(o))
                    .Select(o => o!)
                    .ToList() ?? new List<string>();
                
                // Category check
                int.TryParse(courseVM.Category_Id, out var categoryId);
                if (categoryId <= 0) categoryId = _context.course_Categories.FirstOrDefault()?.Id ?? 1;
                
                // Create course entity
                var course = new Course
                {
                    Title = string.IsNullOrWhiteSpace(courseVM.Title) ? "Untitled Course" : courseVM.Title,
                    instructor_id = instructorId,
                    category_id = categoryId,
                    Status = isSubmitAction ? CourseStatus.PendingReview : CourseStatus.Draft,
                    Rejection_Reason = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                // Add course details
                course.CourseDetails = new CourseDetails
                {
                    Description = courseVM.Description ?? "No description provided yet.",
                    ShortSummary = courseVM.ShortSummary,
                    Actual_Price = courseVM.Actual_Price,
                    Discount_Percent = courseVM.Discount_Percent,
                    Total_Price = courseVM.Actual_Price - (courseVM.Actual_Price * courseVM.Discount_Percent / 100),
                    Duration_Weeks = courseVM.Duration_Weeks ?? 0,
                    Difficulty = courseVM.Difficulty ?? Course_Difficulty.Beginner,
                    Thumbnail_Url = thumbnailFile != null ? _mediaService.SaveThumbnail(thumbnailFile) : courseVM.Temp_Thumbnail_Url,
                    Intro_Video_Url = _mediaService.HandleVideo(videoFile, youtubeUrl, videoType),
                };

                // Strict validation for Submission
                if (isSubmitAction)
                {
                    if (string.IsNullOrEmpty(course.CourseDetails.Thumbnail_Url))
                        return new CourseReturn { Success = false, message = CourseMessage.EmptyFields, TechnicalMessage = "A course thumbnail is required to submit for review." };
                    
                    if (string.IsNullOrEmpty(course.CourseDetails.Intro_Video_Url))
                        return new CourseReturn { Success = false, message = CourseMessage.EmptyFields, TechnicalMessage = "Please provide an intro video. You can either paste a YouTube link or upload a video file." };
                }
                
                // Map outcomes
                course.CourseOutcomes = outcomes
                    .Select(o => new CourseOutcomes { Outcome = o }).ToList();
                
                _context.Courses.Add(course);
                _context.SaveChanges();
                
                // Save syllabus modules and lessons
                if (courseVM.Syllabus != null && courseVM.Syllabus.Any())
                {
                    foreach (var modVM in courseVM.Syllabus)
                    {
                        if (string.IsNullOrWhiteSpace(modVM.ModuleName)) continue;
                        var module = new CourseModules
                        {
                            CourseId = course.Id,
                            ModuleName = modVM.ModuleName
                        };
                        _context.CourseModules.Add(module);
                        _context.SaveChanges();
                        
                        if (modVM.Lessons != null && modVM.Lessons.Any())
                        {
                            int lessonOrder = 1;
                            foreach (var lesVM in modVM.Lessons)
                            {
                                if (string.IsNullOrWhiteSpace(lesVM.Title)) continue;
                                var lesson = new CourseLesson
                                {
                                    ModuleId = module.Id,
                                    Title = lesVM.Title,
                                    VideoUrl = lesVM.VideoUrl,
                                    Order = lessonOrder++
                                };
                                _context.CourseLessons.Add(lesson);
                            }
                        }
                    }
                    _context.SaveChanges();
                }
                
                return new CourseReturn
                {
                    Success = true,
                    message = isSubmitAction ? CourseMessage.SentForApproval : CourseMessage.SavedToDraft,
                    courseData = course
                };
            }
            catch (Exception ex)
            {
                return new CourseReturn
                {
                    Success = false,
                    message = CourseMessage.CourseNotAdded,
                    TechnicalMessage = ex.Message
                };
            }
        }

        // Update existing course details and syllabus
        public CourseReturn UpdateCourse(CourseVM courseVM, int instructorId, IFormFile? thumbnailFile, IFormFile? videoFile, string? youtubeUrl, string? videoType, string? submitAction = "draft")
        {
            try
            {
                // Fetch course for update
                var course = _context.Courses
                    .Where(c => c.Id == courseVM.Id && c.instructor_id == instructorId)
                    .Include(c => c.CourseDetails)
                    .Include(c => c.CourseOutcomes)
                    .FirstOrDefault();
                
                if (course == null)
                    return new CourseReturn { Success = false, message = CourseMessage.CourseNotAdded, TechnicalMessage = "Course not found." };
                
                bool isSubmitAction = submitAction?.ToLower() == "submit";

                // Input validation (Only strict for Submit)
                if (isSubmitAction)
                {
                    if (string.IsNullOrWhiteSpace(courseVM.Title))
                        return new CourseReturn { Success = false, message = CourseMessage.EmptyFields, TechnicalMessage = "Course title is required." };
                    if (string.IsNullOrWhiteSpace(courseVM.Description))
                        return new CourseReturn { Success = false, message = CourseMessage.EmptyFields, TechnicalMessage = "Course description is required." };
                }

                // Update basic info
                if (!string.IsNullOrWhiteSpace(courseVM.Title))
                    course.Title = courseVM.Title;
                
                if (int.TryParse(courseVM.Category_Id, out var catId) && catId > 0)
                    course.category_id = catId;

                course.UpdatedAt = DateTime.UtcNow;
                
                if (isSubmitAction)
                    course.Status = CourseStatus.PendingReview;
                else if (course.Status == CourseStatus.Approved || course.Status == CourseStatus.Published)
                    course.Status = CourseStatus.Draft;
                
                if (course.CourseDetails == null) course.CourseDetails = new CourseDetails();
                
                // Update pricing and specs
                course.CourseDetails.Description = courseVM.Description ?? course.CourseDetails.Description;
                course.CourseDetails.ShortSummary = courseVM.ShortSummary ?? course.CourseDetails.ShortSummary;
                course.CourseDetails.Actual_Price = courseVM.Actual_Price;
                course.CourseDetails.Discount_Percent = courseVM.Discount_Percent;
                course.CourseDetails.Total_Price = courseVM.Actual_Price - (courseVM.Actual_Price * courseVM.Discount_Percent / 100);
                course.CourseDetails.Duration_Weeks = courseVM.Duration_Weeks ?? course.CourseDetails.Duration_Weeks;
                course.CourseDetails.Difficulty = courseVM.Difficulty ?? course.CourseDetails.Difficulty;
                
                // Handle media updates
                if (thumbnailFile != null)
                    course.CourseDetails.Thumbnail_Url = _mediaService.SaveThumbnail(thumbnailFile);
                else if (!string.IsNullOrEmpty(courseVM.Temp_Thumbnail_Url))
                    course.CourseDetails.Thumbnail_Url = courseVM.Temp_Thumbnail_Url;

                if (videoFile != null || !string.IsNullOrWhiteSpace(youtubeUrl))
                    course.CourseDetails.Intro_Video_Url = _mediaService.HandleVideo(videoFile, youtubeUrl, videoType);

                // Strict validation for Submission
                if (isSubmitAction)
                {
                    if (string.IsNullOrEmpty(course.CourseDetails.Thumbnail_Url))
                        return new CourseReturn { Success = false, message = CourseMessage.EmptyFields, TechnicalMessage = "A course thumbnail is required to submit for review." };

                    if (string.IsNullOrEmpty(course.CourseDetails.Intro_Video_Url))
                        return new CourseReturn { Success = false, message = CourseMessage.EmptyFields, TechnicalMessage = "Please provide an intro video. You can either paste a YouTube link or upload a video file." };
                }
                
                // Update outcomes
                if (courseVM.outcome != null)
                {
                    _context.CourseOutcomes.RemoveRange(course.CourseOutcomes);
                    course.CourseOutcomes = courseVM.outcome
                        .Where(o => !string.IsNullOrWhiteSpace(o))
                        .Select(o => new CourseOutcomes { Outcome = o.Trim() }).ToList();
                }
                
                // Update syllabus (replace all modules)
                if (courseVM.Syllabus != null && courseVM.Syllabus.Count > 0)
                {
                    var existingModules = _context.CourseModules.Where(m => m.CourseId == course.Id).ToList();
                    _context.CourseModules.RemoveRange(existingModules);
                    
                    foreach (var modVM in courseVM.Syllabus)
                    {
                        if (string.IsNullOrWhiteSpace(modVM.ModuleName)) continue;
                        var module = new CourseModules { CourseId = course.Id, ModuleName = modVM.ModuleName };
                        _context.CourseModules.Add(module);
                        _context.SaveChanges();
                        
                        if (modVM.Lessons != null)
                        {
                            int order = 1;
                            foreach (var les in modVM.Lessons)
                            {
                                if (string.IsNullOrWhiteSpace(les.Title)) continue;
                                _context.CourseLessons.Add(new CourseLesson { ModuleId = module.Id, Title = les.Title, VideoUrl = les.VideoUrl, Order = order++ });
                            }
                        }
                    }
                }
                
                _context.Courses.Update(course);
                _context.SaveChanges();
                
                return new CourseReturn { Success = true, message = isSubmitAction ? CourseMessage.SentForApproval : CourseMessage.SavedToDraft };
            }
            catch (Exception ex)
            {
                return new CourseReturn { Success = false, message = CourseMessage.CourseNotAdded, TechnicalMessage = ex.Message };
            }
        }

        // Get courses created by instructor
        public List<MyCourseVM> MyCourses(int instructorId, string? search = null, string? category = null, string? status = null)
        {
            var query = _context.Courses
                .Where(c => c.instructor_id == instructorId && c.Status != CourseStatus.Deleted);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c => c.Title.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(c => c.courseCategory != null && c.courseCategory.Name == category);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<CourseStatus>(status, true, out var courseStatus))
                {
                    query = query.Where(c => c.Status == courseStatus);
                }
            }

            return query
                .Include(c => c.CourseDetails)
                .Include(c => c.courseCategory)
                .Select(c => new MyCourseVM
                {
                    CourseId = c.Id,
                    Thumbnail_Url = c.CourseDetails != null ? c.CourseDetails.Thumbnail_Url : string.Empty,
                    Status = c.Status,
                    CategoryName = c.courseCategory != null ? c.courseCategory.Name : "Uncategorized",
                    Title = c.Title,
                    Total_Price = c.CourseDetails != null ? c.CourseDetails.Total_Price : 0
                }).ToList();
        }

        // Get soft-deleted courses for instructor
        public List<MyCourseVM> GetDeletedCourses(int instructorId)
        {
            return _context.Courses
                .Where(c => c.instructor_id == instructorId && c.Status == CourseStatus.Deleted)
                .Include(c => c.CourseDetails)
                .Include(c => c.courseCategory)
                .Select(c => new MyCourseVM
                {
                    CourseId = c.Id,
                    Thumbnail_Url = c.CourseDetails != null ? c.CourseDetails.Thumbnail_Url : string.Empty,
                    Status = c.Status,
                    CategoryName = c.courseCategory != null ? c.courseCategory.Name : "Uncategorized",
                    Title = c.Title,
                    Total_Price = c.CourseDetails != null ? c.CourseDetails.Total_Price : 0
                }).ToList();
        }

        // Fetch course data for editing
        public CourseVM? GetCourseForEdit(int courseId, int instructorId)
        {
            var course = _context.Courses
                .Where(c => c.Id == courseId && c.instructor_id == instructorId && c.Status != CourseStatus.Deleted)
                .Include(c => c.CourseDetails)
                .Include(c => c.CourseOutcomes)
                .FirstOrDefault();
            
            if (course == null) return null;
            
            // Fetch syllabus
            var syllabus = _context.CourseModules
                .Where(m => m.CourseId == courseId)
                .Include(m => m.Lessons)
                .OrderBy(m => m.Id)
                .Select(m => new ModuleVM
                {
                    ModuleName = m.ModuleName,
                    Lessons = m.Lessons.OrderBy(l => l.Order).Select(l => new LessonVM
                    {
                        Title = l.Title,
                        VideoUrl = l.VideoUrl
                    }).ToList()
                }).ToList();

            // Map to VM
            return new CourseVM
            {
                Id = course.Id,
                Title = course.Title,
                Description = course.CourseDetails?.Description ?? string.Empty,
                ShortSummary = course.CourseDetails?.ShortSummary ?? string.Empty,
                Category_Id = course.category_id.ToString(),
                Actual_Price = course.CourseDetails?.Actual_Price ?? 0,
                Discount_Percent = course.CourseDetails?.Discount_Percent ?? 0,
                Total_Price = course.CourseDetails?.Total_Price ?? 0,
                Difficulty = course.CourseDetails?.Difficulty,
                Duration_Weeks = course.CourseDetails?.Duration_Weeks,
                Thumbnail_Url = course.CourseDetails?.Thumbnail_Url,
                Intro_Video_Url = course.CourseDetails?.Intro_Video_Url,
                outcome = course.CourseOutcomes?.Select(o => o.Outcome).ToList(),
                CourseStatus = course.Status,
                Syllabus = syllabus
            };
        }

        // Delete course and related data (Soft delete)
        public bool DeleteCourse(int courseId, int instructorId)
        {
            try
            {
                var course = _context.Courses
                    .Where(c => c.Id == courseId && c.instructor_id == instructorId)
                    .FirstOrDefault();
                
                if (course == null) return false;
                
                course.Status = CourseStatus.Deleted;
                course.UpdatedAt = DateTime.UtcNow;
                
                _context.Courses.Update(course);
                _context.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Initialize default course categories
        private void EnsureDefaultCategories()
        {
            if (_context.course_Categories.Any())
                return;
            
            var defaultCategories = new[]
            {
                "Software Development",
                "Data Science",
                "AI / Machine Learning",
                "DevOps & Cloud",
                "Cybersecurity"
            };
            
            _context.course_Categories.AddRange(defaultCategories.Select(name => new Course_Category { Name = name }));
            _context.SaveChanges();
        }
    }
}
