using System.Collections.Generic;

namespace SkillForge.Areas.Instructor.Models
{
    public class InstructorEarningsVM
    {
        // Summary Cards
        public decimal TotalEarned { get; set; }
        public decimal ThisMonthEarnings { get; set; }
        public decimal PendingPayout { get; set; }
        public int PayingStudents { get; set; }
        public int NewStudentsThisWeek { get; set; }
        public decimal PreviousMonthEarnings { get; set; }
        public double GrowthPercentage { get; set; }

        // Filter Options
        public List<int> AvailableYears { get; set; } = new List<int>();
        public int SelectedYear { get; set; }
        public int SelectedMonth { get; set; }

        // Data Tables
        public List<CourseEarningsItemVM> CourseEarnings { get; set; } = new List<CourseEarningsItemVM>();
        public List<MonthlyBreakdownItemVM> MonthlyBreakdown { get; set; } = new List<MonthlyBreakdownItemVM>();
    }

    public class CourseEarningsItemVM
    {
        public int CourseId { get; set; }
        public string CourseTitle { get; set; }
        public decimal PricePerStudent { get; set; }
        public int EnrolledStudents { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal PlatformFee { get; set; }
        public decimal InstructorEarnings { get; set; }
    }

    public class MonthlyBreakdownItemVM
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; }
        public int NewStudents { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal InstructorEarnings { get; set; }
        public string PayoutStatus { get; set; }
    }
}
