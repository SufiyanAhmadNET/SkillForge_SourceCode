using Microsoft.EntityFrameworkCore;
using SkillForge.Areas.Instructor.Models;
using SkillForge.Data;
using SkillForge.Interfaces;
using SkillForge.Models;
using SkillForge.Services.Instructors.Models;
using SkillForge.Services.Courses.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SkillForge.Services.Analytics
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly SkillForgeDbContext _context;

        public AnalyticsService(SkillForgeDbContext context)
        {
            _context = context;
        }

        public async Task<InstructorDashboardVM> GetInstructorDashboardStatsAsync(int instructorId)
        {
            var instructor = await _context.instructors
                .AsNoTracking()
                .Include(i => i.Profile)
                .FirstOrDefaultAsync(i => i.Id == instructorId);

            var application = await _context.MentorApplications
                .AsNoTracking()
                .Where(m => m.InstructorId == instructorId)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();

            var courses = await _context.Courses
                .AsNoTracking()
                .Where(c => c.instructor_id == instructorId)
                .ToListAsync();

            var totalStudents = await GetInstructorStudentCountAsync(instructorId);
            var totalEarnings = await GetInstructorRevenueAsync(instructorId);

            var activeCoursesOverview = await GetInstructorCoursesOverviewAsync(instructorId);

            return new InstructorDashboardVM
            {
                Email = instructor?.Email,
                FirstName = instructor?.Profile?.FirstName ?? "Instructor",
                LastName = instructor?.Profile?.LastName,
                PhotoPath = instructor?.Profile?.PhotoPath ?? "/images/DefaultProfilePhoto.jfif",
                Mobile = instructor?.Profile?.Mobile,
                Location = instructor?.Profile?.Location,
                AboutYou = instructor?.Profile?.AboutYou,
                CurrentRole = instructor?.Profile?.CurrentRole,
                Expertise = instructor?.Profile?.Expertise,
                YearsExperience = instructor?.Profile?.YearsExperience,
                LinkedinUrl = instructor?.Profile?.LinkedinUrl,
                GithubUrl = instructor?.Profile?.GithubUrl,
                TwitterUrl = instructor?.Profile?.TwitterUrl,
                WebsiteUrl = instructor?.Profile?.WebsiteUrl,
                Skills = instructor?.Profile?.Skills,

                ApplicationStatus = application?.Status ?? MentorApplicationStatus.NotApplied,
                ApplicationComment = application?.AdminComment,
                MentorApplicationId = application?.Id,
                WhyTeach = application?.WhyTeach,
                Topics = application?.Topics,
                ResumePath = application?.ResumePath,
                PortfolioUrl = application?.PortfolioUrl,

                TotalCourses = courses.Count(c => c.Status == CourseStatus.Published || c.Status == CourseStatus.Approved),
                PublishedCourses = courses.Count(c => c.Status == CourseStatus.Published || c.Status == CourseStatus.Approved),
                PendingApprovalCourses = courses.Count(c => c.Status == CourseStatus.PendingReview),
                RejectedCourses = courses.Count(c => c.Status == CourseStatus.Rejected),
                DraftCourses = courses.Count(c => c.Status == CourseStatus.Draft),

                TotalStudents = totalStudents,
                TotalEarnings = totalEarnings,
                AvgRating = 4.8, // Default placeholder as per requirement

                ActiveCourses = activeCoursesOverview
            };
        }

        public async Task<List<CourseStatsVM>> GetInstructorCoursesOverviewAsync(int instructorId)
        {
            return await _context.Courses
                .AsNoTracking()
                .Where(c => c.instructor_id == instructorId)
                .Select(c => new CourseStatsVM
                {
                    CourseId = c.Id,
                    Title = c.Title,
                    Status = c.Status.ToString(),
                    StudentCount = _context.Enrollments.Count(e => e.CourseId == c.Id && e.Status == EnrollmentStatus.Active),
                    Rating = 4.9, // Placeholder as requested
                    Earnings = _context.Payments
                        .Where(p => p.Enrollment.CourseId == c.Id && p.Status == PaymentStatus.Success)
                        .Sum(p => (decimal?)p.Amount) ?? 0
                })
                .ToListAsync();
        }

        public async Task<decimal> GetInstructorRevenueAsync(int instructorId)
        {
            return await _context.Payments
                .AsNoTracking()
                .Where(p => _context.Courses.Any(c => c.Id == p.Enrollment.CourseId && c.instructor_id == instructorId) 
                            && p.Status == PaymentStatus.Success)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;
        }

        public async Task<int> GetInstructorStudentCountAsync(int instructorId)
        {
            return await _context.Enrollments
                .AsNoTracking()
                .CountAsync(e => _context.Courses.Any(c => c.Id == e.CourseId && c.instructor_id == instructorId) 
                                 && e.Status == EnrollmentStatus.Active);
        }

        public async Task<decimal> GetCourseRevenueAsync(int courseId)
        {
            return await _context.Payments
                .AsNoTracking()
                .Where(p => p.Enrollment.CourseId == courseId && p.Status == PaymentStatus.Success)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;
        }

        public async Task<InstructorEarningsVM> GetInstructorEarningsDashboardAsync(int instructorId, int? year, int? month)
        {
            var now = DateTime.UtcNow;
            int selYear = year ?? now.Year;
            int selMonth = month ?? now.Month;

            // Fetch all successful payments for this instructor's courses
            var allPayments = await _context.Payments
                .AsNoTracking()
                .Include(p => p.Enrollment.Course)
                .Where(p => p.Enrollment.Course.instructor_id == instructorId && p.Status == PaymentStatus.Success)
                .ToListAsync();

            // Fetch all active enrollments for this instructor's courses
            var allEnrollments = await _context.Enrollments
                .AsNoTracking()
                .Where(e => e.Course.instructor_id == instructorId && e.Status == EnrollmentStatus.Active)
                .ToListAsync();

            // Summary Totals
            var totalEarned = allPayments.Sum(p => p.Amount);
            var thisMonthEarnings = allPayments.Where(p => p.CreatedAt.Year == now.Year && p.CreatedAt.Month == now.Month).Sum(p => p.Amount);
            var prevMonthEarnings = allPayments.Where(p => p.CreatedAt.Year == (now.Month == 1 ? now.Year - 1 : now.Year) && p.CreatedAt.Month == (now.Month == 1 ? 12 : now.Month - 1)).Sum(p => p.Amount);
            
            double growth = 0;
            if (prevMonthEarnings > 0)
                growth = (double)((thisMonthEarnings - prevMonthEarnings) / prevMonthEarnings * 100);

            var payingStudents = allPayments.Select(p => p.Enrollment.StudentId).Distinct().Count();
            var newStudentsWeek = allEnrollments.Count(e => e.EnrolledAt >= now.AddDays(-7));

            // Course Wise Breakdown
            var courseEarnings = allPayments
                .GroupBy(p => new { p.Enrollment.CourseId, p.Enrollment.Course.Title })
                .Select(g => new CourseEarningsItemVM
                {
                    CourseId = g.Key.CourseId,
                    CourseTitle = g.Key.Title,
                    EnrolledStudents = g.Select(p => p.Enrollment.StudentId).Distinct().Count(),
                    GrossRevenue = g.Sum(p => p.Amount),
                    PlatformFee = g.Sum(p => p.Amount) * 0.20m, // 20% fee
                    InstructorEarnings = g.Sum(p => p.Amount) * 0.80m,
                    PricePerStudent = g.Any() ? g.Average(p => p.Amount) : 0
                }).ToList();

            // Monthly Breakdown (Historical)
            var monthlyBreakdown = allPayments
                .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
                .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
                .Select(g => new MonthlyBreakdownItemVM
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthName = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy"),
                    NewStudents = g.Select(p => p.Enrollment.StudentId).Distinct().Count(),
                    GrossRevenue = g.Sum(p => p.Amount),
                    InstructorEarnings = g.Sum(p => p.Amount) * 0.80m,
                    PayoutStatus = (g.Key.Year < now.Year || (g.Key.Year == now.Year && g.Key.Month < now.Month)) ? "Paid" : "Pending"
                }).ToList();

            // Filter available years
            var availableYears = allPayments.Select(p => p.CreatedAt.Year).Distinct().OrderByDescending(y => y).ToList();
            if (!availableYears.Any()) availableYears.Add(now.Year);

            return new InstructorEarningsVM
            {
                TotalEarned = totalEarned * 0.80m, // Instructor's 80% share
                ThisMonthEarnings = thisMonthEarnings * 0.80m,
                PreviousMonthEarnings = prevMonthEarnings * 0.80m,
                GrowthPercentage = Math.Round(growth, 1),
                PendingPayout = thisMonthEarnings * 0.80m, // Conceptually pending for current month
                PayingStudents = payingStudents,
                NewStudentsThisWeek = newStudentsWeek,
                CourseEarnings = courseEarnings,
                MonthlyBreakdown = monthlyBreakdown,
                AvailableYears = availableYears,
                SelectedYear = selYear,
                SelectedMonth = selMonth
            };
        }

        // Admin Analytics Implementation
        public async Task<int> GetTotalStudentsCountAsync()
        {
            return await _context.Students.CountAsync();
        }

        public async Task<int> GetTotalInstructorsCountAsync()
        {
            return await _context.instructors.CountAsync();
        }

        public async Task<decimal> GetTotalPlatformRevenueAsync()
        {
            return await _context.Payments
                .AsNoTracking()
                .Where(p => p.Status == PaymentStatus.Success)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;
        }

        public async Task<decimal> GetPlatformRevenueThisMonthAsync()
        {
            var now = DateTime.UtcNow;
            return await _context.Payments
                .AsNoTracking()
                .Where(p => p.Status == PaymentStatus.Success && p.CreatedAt.Month == now.Month && p.CreatedAt.Year == now.Year)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;
        }

        public async Task<int> GetNewEnrolledCountAsync(int days)
        {
            var cutoff = DateTime.UtcNow.AddDays(-days);
            return await _context.Enrollments
                .AsNoTracking()
                .CountAsync(e => e.EnrolledAt >= cutoff);
        }

        public async Task<int> GetNewPublishedCoursesCountAsync(int days)
        {
            var cutoff = DateTime.UtcNow.AddDays(-days);
            return await _context.Courses
                .AsNoTracking()
                .CountAsync(c => c.CreatedAt >= cutoff && (c.Status == CourseStatus.Approved || c.Status == CourseStatus.Published));
        }

        public async Task<CourseFinancialReportVM?> GetCourseFinancialReportAsync(int courseId, int instructorId)
        {
            var instructor = await _context.instructorProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.InstructorId == instructorId);
            var inst = await _context.instructors.AsNoTracking().FirstOrDefaultAsync(i => i.Id == instructorId);

            var course = await _context.Courses
                .AsNoTracking()
                .Include(c => c.CourseDetails)
                .Include(c => c.courseCategory)
                .FirstOrDefaultAsync(c => c.Id == courseId && c.instructor_id == instructorId);

            if (course == null) return null;

            var payments = await _context.Payments
                .AsNoTracking()
                .Include(p => p.Enrollment.Student.Profile)
                .Where(p => p.Enrollment.CourseId == courseId && p.Status == PaymentStatus.Success)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return new CourseFinancialReportVM
            {
                Instructor = new InstructorInfoVM
                {
                    Name = instructor != null ? $"{instructor.FirstName} {instructor.LastName}" : "Instructor",
                    Email = inst?.Email ?? string.Empty,
                    Mobile = instructor?.Mobile ?? string.Empty
                },
                CourseTitle = course.Title,
                Category = course.courseCategory?.Name ?? "Uncategorized",
                Level = course.CourseDetails?.Difficulty.ToString() ?? "None",
                Status = course.Status.ToString(),
                PublishDate = course.CreatedAt.ToString("dd MMM yyyy"),
                BasePrice = course.CourseDetails?.Actual_Price ?? 0,
                DiscountPercent = course.CourseDetails?.Discount_Percent ?? 0,
                SellingPrice = course.CourseDetails?.Total_Price ?? 0,
                TotalStudents = payments.Count,
                GrossRevenue = payments.Sum(p => p.Amount),
                PlatformFee = payments.Sum(p => p.Amount) * 0.20m,
                NetEarnings = payments.Sum(p => p.Amount) * 0.80m,
                Transactions = payments.Select(p => new CourseTransactionVM
                {
                    StudentName = p.Enrollment.Student.Profile != null ? $"{p.Enrollment.Student.Profile.FirstName} {p.Enrollment.Student.Profile.LastName}" : "Student",
                    StudentEmail = p.Enrollment.Student.Email,
                    EnrollmentDate = p.CreatedAt.ToString("dd MMM yyyy"),
                    AmountPaid = p.Amount
                }).ToList()
            };
        }

        public async Task<CourseStudentListReportVM?> GetCourseStudentListReportAsync(int courseId, int instructorId)
        {
            var instructor = await _context.instructorProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.InstructorId == instructorId);
            var inst = await _context.instructors.AsNoTracking().FirstOrDefaultAsync(i => i.Id == instructorId);

            var course = await _context.Courses
               .AsNoTracking()
               .Include(c => c.courseCategory)
               .FirstOrDefaultAsync(c => c.Id == courseId && c.instructor_id == instructorId);

            if (course == null) return null;

            var enrollments = await _context.Enrollments
                .AsNoTracking()
                .Include(e => e.Student.Profile)
                .Where(e => e.CourseId == courseId && e.Status == EnrollmentStatus.Active)
                .ToListAsync();

            return new CourseStudentListReportVM
            {
                Instructor = new InstructorInfoVM
                {
                    Name = instructor != null ? $"{instructor.FirstName} {instructor.LastName}" : "Instructor",
                    Email = inst?.Email ?? string.Empty,
                    Mobile = instructor?.Mobile ?? string.Empty
                },
                CourseTitle = course.Title,
                Category = course.courseCategory?.Name ?? "Uncategorized",
                TotalStudents = enrollments.Count,
                Students = enrollments.OrderBy(e => e.Student.Profile?.FirstName).Select(e => new StudentRosterItemVM
                {
                    Name = e.Student.Profile != null ? $"{e.Student.Profile.FirstName} {e.Student.Profile.LastName}" : "Student",
                    Email = e.Student.Email,
                    Mobile = e.Student.Profile?.Mobile ?? "-",
                    EnrollmentDate = e.EnrolledAt.ToString("dd MMM yyyy")
                }).ToList()
            };
        }

        public async Task<MonthlyFinancialReportVM> GetMonthlyFinancialReportAsync(int instructorId, int year, int month)
        {
            var instructor = await _context.instructorProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.InstructorId == instructorId);
            var inst = await _context.instructors.AsNoTracking().FirstOrDefaultAsync(i => i.Id == instructorId);

            var payments = await _context.Payments
               .AsNoTracking()
               .Include(p => p.Enrollment.Course)
               .Where(p => p.Enrollment.Course.instructor_id == instructorId &&
                           p.Status == PaymentStatus.Success &&
                           p.CreatedAt.Year == year && p.CreatedAt.Month == month)
               .ToListAsync();

            var courseBreakdowns = payments.GroupBy(p => p.Enrollment.CourseId)
                .Select(g => new MonthlyCourseBreakdownVM
                {
                    CourseName = g.First().Enrollment.Course.Title,
                    NewStudents = g.Select(p => p.Enrollment.StudentId).Distinct().Count(),
                    SellingPrice = g.Any() ? g.Average(p => p.Amount) : 0,
                    GrossRevenue = g.Sum(p => p.Amount),
                    PlatformFee = g.Sum(p => p.Amount) * 0.20m,
                    NetEarnings = g.Sum(p => p.Amount) * 0.80m
                }).ToList();

            return new MonthlyFinancialReportVM
            {
                Instructor = new InstructorInfoVM
                {
                    Name = instructor != null ? $"{instructor.FirstName} {instructor.LastName}" : "Instructor",
                    Email = inst?.Email ?? string.Empty,
                    Mobile = instructor?.Mobile ?? string.Empty
                },
                ReportMonth = new DateTime(year, month, 1).ToString("MMMM yyyy"),
                TotalCourses = courseBreakdowns.Count,
                TotalEarning = payments.Sum(p => p.Amount),
                PlatformFee = payments.Sum(p => p.Amount) * 0.20m,
                NetEarnings = payments.Sum(p => p.Amount) * 0.80m,
                CourseBreakdowns = courseBreakdowns
            };
        }

        public async Task<InstructorGlobalCourseReportVM> GetInstructorGlobalCourseReportAsync(int instructorId)
        {
            var instructor = await _context.instructorProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.InstructorId == instructorId);
            var inst = await _context.instructors.AsNoTracking().FirstOrDefaultAsync(i => i.Id == instructorId);

            var allPayments = await _context.Payments
                .AsNoTracking()
                .Include(p => p.Enrollment.Course)
                .Where(p => p.Enrollment.Course.instructor_id == instructorId && p.Status == PaymentStatus.Success)
                .ToListAsync();

            var courseEarnings = allPayments
                .GroupBy(p => new { p.Enrollment.CourseId, p.Enrollment.Course.Title })
                .Select(g => new CourseEarningsItemVM
                {
                    CourseId = g.Key.CourseId,
                    CourseTitle = g.Key.Title,
                    EnrolledStudents = g.Select(p => p.Enrollment.StudentId).Distinct().Count(),
                    GrossRevenue = g.Sum(p => p.Amount),
                    PlatformFee = g.Sum(p => p.Amount) * 0.20m,
                    InstructorEarnings = g.Sum(p => p.Amount) * 0.80m,
                    PricePerStudent = g.Any() ? g.Average(p => p.Amount) : 0
                }).ToList();

            return new InstructorGlobalCourseReportVM
            {
                Instructor = new InstructorInfoVM
                {
                    Name = instructor != null ? $"{instructor.FirstName} {instructor.LastName}" : "Instructor",
                    Email = inst?.Email ?? string.Empty,
                    Mobile = instructor?.Mobile ?? string.Empty
                },
                CourseEarnings = courseEarnings,
                TotalGrossRevenue = allPayments.Sum(p => p.Amount),
                TotalPlatformFee = allPayments.Sum(p => p.Amount) * 0.20m,
                TotalNetEarnings = allPayments.Sum(p => p.Amount) * 0.80m
            };
        }

        // ==========================================
        // ADMIN REPORT DATA RETRIEVAL
        // ==========================================

        public async Task<AdminEnrollmentReportVM> GetAdminEnrollmentReportAsync(int days)
        {
            var cutoff = days > 0 ? DateTime.UtcNow.AddDays(-days) : DateTime.MinValue;
            
            var enrollmentsData = await (from e in _context.Enrollments
                                         join c in _context.Courses on e.CourseId equals c.Id
                                         join s in _context.Students on e.StudentId equals s.Id
                                         join sp in _context.StudentProfiles on s.Id equals sp.StudentId into spGroup
                                         from sp in spGroup.DefaultIfEmpty()
                                         join i in _context.instructors on c.instructor_id equals i.Id
                                         join ip in _context.instructorProfiles on i.Id equals ip.InstructorId into ipGroup
                                         from ip in ipGroup.DefaultIfEmpty()
                                         where e.EnrolledAt >= cutoff
                                         orderby e.EnrolledAt descending
                                         select new EnrollmentReportItemVM
                                         {
                                             StudentName = sp != null ? $"{sp.FirstName} {sp.LastName}" : "Student",
                                             CourseTitle = c.Title,
                                             InstructorName = ip != null ? $"{ip.FirstName} {ip.LastName}" : "Instructor",
                                             EnrollmentDate = e.EnrolledAt.ToString("dd MMM yyyy")
                                         }).ToListAsync();

            return new AdminEnrollmentReportVM
            {
                Title = "Enrollment Report",
                GeneratedDate = DateTime.Now.ToString("dd MMM yyyy HH:mm"),
                DateRange = days > 0 ? $"Last {days} Days" : "All Time",
                TotalRecords = enrollmentsData.Count,
                TotalEnrollments = enrollmentsData.Count,
                TotalCourses = enrollmentsData.Select(e => e.CourseTitle).Distinct().Count(),
                UniqueStudents = enrollmentsData.Select(e => e.StudentName).Distinct().Count(),
                Enrollments = enrollmentsData
            };
        }

        public async Task<AdminSalesReportVM> GetAdminSalesReportAsync(int days)
        {
            var cutoff = days > 0 ? DateTime.UtcNow.AddDays(-days) : DateTime.MinValue;

            var payments = await _context.Payments
                .AsNoTracking()
                .Include(p => p.Enrollment.Student.Profile)
                .Include(p => p.Enrollment.Course)
                .Where(p => p.Status == PaymentStatus.Success && p.CreatedAt >= cutoff)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var totalRevenue = payments.Sum(p => p.Amount);
            var totalOrders = payments.Count;

            return new AdminSalesReportVM
            {
                Title = "Sales Report",
                GeneratedDate = DateTime.Now.ToString("dd MMM yyyy HH:mm"),
                DateRange = days > 0 ? $"Last {days} Days" : "All Time",
                TotalRecords = totalOrders,
                TotalOrders = totalOrders,
                TotalRevenue = totalRevenue,
                AvgOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0,
                Sales = payments.Select(p => new SalesReportItemVM
                {
                    OrderId = p.Id.ToString(),
                    StudentName = p.Enrollment.Student.Profile != null ? $"{p.Enrollment.Student.Profile.FirstName} {p.Enrollment.Student.Profile.LastName}" : "Student",
                    CourseTitle = p.Enrollment.Course.Title,
                    Amount = p.Amount,
                    PurchaseDate = p.CreatedAt.ToString("dd MMM yyyy")
                }).ToList()
            };
        }

        public async Task<AdminStudentReportVM> GetAdminStudentReportAsync(int days)
        {
            var cutoff = days > 0 ? DateTime.UtcNow.AddDays(-days) : DateTime.MinValue;

            // Fetch students. If CreatedAt is default (0001), we include them in "All Time"
            var students = await _context.Students
                .AsNoTracking()
                .Include(s => s.Profile)
                .Where(s => days == 0 || s.CreatedAt >= cutoff)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var allEnrollments = await _context.Enrollments.AsNoTracking().ToListAsync();

            var studentItems = students.Select(s => new StudentReportItemVM
            {
                Name = s.Profile != null ? $"{s.Profile.FirstName} {s.Profile.LastName}" : s.Email.Split('@')[0],
                Email = s.Email,
                JoinedDate = s.CreatedAt == DateTime.MinValue ? "N/A" : s.CreatedAt.ToString("dd MMM yyyy"),
                TotalCourses = allEnrollments.Count(e => e.StudentId == s.Id),
                Status = "Active"
            }).ToList();

            return new AdminStudentReportVM
            {
                Title = "Student Report",
                GeneratedDate = DateTime.Now.ToString("dd MMM yyyy HH:mm"),
                DateRange = days > 0 ? $"Last {days} Days" : "All Time",
                TotalRecords = studentItems.Count,
                TotalStudents = studentItems.Count,
                ActiveStudents = studentItems.Count,
                TotalEnrollments = allEnrollments.Count,
                Students = studentItems
            };
        }

        public async Task<AdminInstructorReportVM> GetAdminInstructorReportAsync(int days)
        {
            var cutoff = days > 0 ? DateTime.UtcNow.AddDays(-days) : DateTime.MinValue;

            // Join with MentorApplications to get only Approved instructors
            var instructorsData = await (from i in _context.instructors
                                         join m in _context.MentorApplications on i.Id equals m.InstructorId
                                         join ip in _context.instructorProfiles on i.Id equals ip.InstructorId into ipGroup
                                         from ip in ipGroup.DefaultIfEmpty()
                                         where m.Status == MentorApplicationStatus.Approved
                                         && (days == 0 || i.CreatedAt >= cutoff)
                                         orderby i.CreatedAt descending
                                         select new { i, ip }).ToListAsync();

            var allCourses = await _context.Courses.AsNoTracking().ToListAsync();
            var allEnrollments = await _context.Enrollments.AsNoTracking().ToListAsync();

            var instructorItems = instructorsData.Select(x => new InstructorReportItemVM
            {
                Name = x.ip != null ? $"{x.ip.FirstName} {x.ip.LastName}" : "Instructor",
                Email = x.i.Email,
                Courses = allCourses.Count(c => c.instructor_id == x.i.Id && (c.Status == CourseStatus.Approved || c.Status == CourseStatus.Published)),
                Students = allEnrollments.Count(e => allCourses.Any(c => c.Id == e.CourseId && c.instructor_id == x.i.Id)),
                JoinedDate = x.i.CreatedAt.ToString("dd MMM yyyy"),
                Status = "Approved"
            }).ToList();

            return new AdminInstructorReportVM
            {
                Title = "Instructor Report",
                GeneratedDate = DateTime.Now.ToString("dd MMM yyyy HH:mm"),
                DateRange = days > 0 ? $"Last {days} Days" : "All Time",
                TotalRecords = instructorItems.Count,
                TotalInstructors = instructorItems.Count,
                TotalCourses = allCourses.Count(c => c.Status == CourseStatus.Approved || c.Status == CourseStatus.Published),
                TotalStudentsTaught = allEnrollments.Count,
                Instructors = instructorItems
            };
        }

        public async Task<AdminRevenueReportVM> GetAdminRevenueReportAsync(int days)
        {
            var cutoff = days > 0 ? DateTime.UtcNow.AddDays(-days) : DateTime.MinValue;

            var payments = await _context.Payments
                .AsNoTracking()
                .Where(p => p.Status == PaymentStatus.Success && p.CreatedAt >= cutoff)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var dailyData = payments
                .GroupBy(p => p.CreatedAt.Date)
                .Select(g => new RevenueReportItemVM
                {
                    Date = g.Key.ToString("dd MMM yyyy"),
                    Orders = g.Count(),
                    Revenue = g.Sum(p => p.Amount)
                })
                .OrderByDescending(x => x.Date)
                .ToList();

            var totalRevenue = payments.Sum(p => p.Amount);
            var totalOrders = payments.Count;

            return new AdminRevenueReportVM
            {
                Title = "Revenue Report",
                GeneratedDate = DateTime.Now.ToString("dd MMM yyyy HH:mm"),
                DateRange = days > 0 ? $"Last {days} Days" : "All Time",
                TotalRecords = dailyData.Count,
                GrossRevenue = totalRevenue,
                TotalOrders = totalOrders,
                AvgRevenuePerOrder = totalOrders > 0 ? totalRevenue / totalOrders : 0,
                RevenueData = dailyData
            };
        }

        public async Task<AdminPayoutReportVM> GetAdminPayoutReportAsync(int days)
        {
            var cutoff = days > 0 ? DateTime.UtcNow.AddDays(-days) : DateTime.MinValue;

            var payoutsData = await (from p in _context.Payments
                                     join e in _context.Enrollments on p.EnrollmentId equals e.Id
                                     join c in _context.Courses on e.CourseId equals c.Id
                                     join i in _context.instructors on c.instructor_id equals i.Id
                                     join ip in _context.instructorProfiles on i.Id equals ip.InstructorId into ipGroup
                                     from ip in ipGroup.DefaultIfEmpty()
                                     where p.Status == PaymentStatus.Success && p.CreatedAt >= cutoff
                                     group p by new { i.Id, ip.FirstName, ip.LastName } into g
                                     select new PayoutReportItemVM
                                     {
                                         InstructorName = g.Key.FirstName != null ? $"{g.Key.FirstName} {g.Key.LastName}" : "Instructor",
                                         Courses = _context.Courses.Count(c => c.instructor_id == g.Key.Id),
                                         RevenueGenerated = g.Sum(p => p.Amount),
                                         Commission = g.Sum(p => p.Amount) * 0.20m,
                                         PayoutAmount = g.Sum(p => p.Amount) * 0.80m
                                     }).ToListAsync();

            var totalRevenue = payoutsData.Sum(x => x.RevenueGenerated);

            return new AdminPayoutReportVM
            {
                Title = "Instructor Payout Report",
                GeneratedDate = DateTime.Now.ToString("dd MMM yyyy HH:mm"),
                DateRange = days > 0 ? $"Last {days} Days" : "All Time",
                TotalRecords = payoutsData.Count,
                TotalInstructorRevenue = totalRevenue,
                TotalPayouts = totalRevenue * 0.80m,
                Payouts = payoutsData
            };
        }

        public async Task<AdminApplicationsReportVM> GetAdminApplicationsReportAsync(int days)
        {
            var cutoff = days > 0 ? DateTime.UtcNow.AddDays(-days) : DateTime.MinValue;

            var applicationsData = await _context.MentorApplications
                .AsNoTracking()
                .Include(a => a.Instructor.Profile)
                .Where(a => a.CreatedAt >= cutoff)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new ApplicationReportItemVM
                {
                    ApplicantName = a.Instructor != null && a.Instructor.Profile != null 
                        ? $"{a.Instructor.Profile.FirstName} {a.Instructor.Profile.LastName}" 
                        : "Applicant",
                    Email = a.Instructor != null ? a.Instructor.Email : "-",
                    Specialization = a.Topics, // Topics seems to be the expertise in MentorApplication
                    AppliedDate = a.CreatedAt.ToString("dd MMM yyyy"),
                    Status = a.Status.ToString()
                }).ToListAsync();

            return new AdminApplicationsReportVM
            {
                Title = "Instructor Applications Report",
                GeneratedDate = DateTime.Now.ToString("dd MMM yyyy HH:mm"),
                DateRange = days > 0 ? $"Last {days} Days" : "All Time",
                TotalRecords = applicationsData.Count,
                TotalApplications = applicationsData.Count,
                Approved = _context.MentorApplications.Count(a => a.Status == MentorApplicationStatus.Approved && a.CreatedAt >= cutoff),
                Pending = _context.MentorApplications.Count(a => a.Status == MentorApplicationStatus.Pending && a.CreatedAt >= cutoff),
                Rejected = _context.MentorApplications.Count(a => a.Status == MentorApplicationStatus.Rejected && a.CreatedAt >= cutoff),
                Applications = applicationsData
            };
        }

        public async Task<AdminCoursesReportVM> GetAdminCoursesReportAsync(int days)
        {
            var cutoff = days > 0 ? DateTime.UtcNow.AddDays(-days) : DateTime.MinValue;

            var coursesData = await (from c in _context.Courses
                                     join i in _context.instructors on c.instructor_id equals i.Id
                                     join ip in _context.instructorProfiles on i.Id equals ip.InstructorId into ipGroup
                                     from ip in ipGroup.DefaultIfEmpty()
                                     join cat in _context.course_Categories on c.category_id equals cat.Id
                                     where c.CreatedAt >= cutoff && 
                                           c.Status != CourseStatus.Draft && 
                                           (c.Status != CourseStatus.Deleted || 
                                            (_context.CourseDetails.Any(cd => cd.CourseId == c.Id && 
                                                                            cd.Thumbnail_Url != null && cd.Thumbnail_Url != "" &&
                                                                            cd.Intro_Video_Url != null && cd.Intro_Video_Url != "")))
                                     select new AdminCourseReportItemVM
                                     {
                                         Title = c.Title,
                                         InstructorName = ip.FirstName != null ? $"{ip.FirstName} {ip.LastName}" : "Instructor",
                                         Category = cat.Name,
                                         Status = c.Status.ToString(),
                                         Price = _context.CourseDetails.Where(cd => cd.CourseId == c.Id).Select(cd => cd.Total_Price).FirstOrDefault(),
                                         Students = _context.Enrollments.Count(e => e.CourseId == c.Id && e.Status == EnrollmentStatus.Active),
                                         Earnings = _context.Payments.Where(p => p.Enrollment.CourseId == c.Id && p.Status == PaymentStatus.Success).Sum(p => (decimal?)p.Amount) ?? 0,
                                         CreatedDate = c.CreatedAt.ToString("dd MMM yyyy")
                                     }).ToListAsync();

            return new AdminCoursesReportVM
            {
                Title = "Platform Courses Report",
                GeneratedDate = DateTime.Now.ToString("dd MMM yyyy HH:mm"),
                DateRange = days > 0 ? $"Last {days} Days" : "All Time",
                TotalRecords = coursesData.Count,
                ActiveCourses = coursesData.Count(c => c.Status == "Published" || c.Status == "Approved"),
                PendingCourses = coursesData.Count(c => c.Status == "PendingReview"),
                Courses = coursesData
            };
        }
    }
}
