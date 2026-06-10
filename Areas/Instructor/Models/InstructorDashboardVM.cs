using SkillForge.Models;
using SkillForge.Services.Instructors.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SkillForge.Areas.Instructor.Models
{
    public class InstructorDashboardVM
    {
        // profile info
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhotoPath { get; set; }
        public string? Mobile { get; set; }
        public string? Location { get; set; }
        public string? AboutYou { get; set; }
        public string? CurrentRole { get; set; }
        public string? Expertise { get; set; }
        public int? YearsExperience { get; set; }
        
        // professional and social presence
        public string? Headline { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? GithubUrl { get; set; }
        public string? LinkedinUrl { get; set; }
        public string? TwitterUrl { get; set; }
        public string? Skills { get; set; }

        public MentorApplicationStatus ApplicationStatus { get; set; } = MentorApplicationStatus.NotApplied;
        public string? ApplicationComment { get; set; }
        public int? MentorApplicationId { get; set; }
        public string? WhyTeach { get; set; }
        public string? Topics { get; set; }
        public string? ResumePath { get; set; }
        public string? PortfolioUrl { get; set; }

        // stats
        public int TotalCourses { get; set; }
        public int PublishedCourses { get; set; }
        public int PendingApprovalCourses { get; set; }
        public int RejectedCourses { get; set; }
        public int DraftCourses { get; set; }
        
        public int TotalStudents { get; set; }
        public decimal TotalEarnings { get; set; }
        public double AvgRating { get; set; } = 4.8; // default until rating system implemented

        // lists for dashboard tables
        public List<CourseStatsVM> ActiveCourses { get; set; } = new();
        public List<RecentEnrollmentVM> RecentEnrollments { get; set; } = new();
    }

    public class CourseStatsVM
    {
        public int CourseId { get; set; }
        public string? Title { get; set; }
        public int StudentCount { get; set; }
        public string? Status { get; set; }
        public double Rating { get; set; } = 4.9; 
        public decimal Earnings { get; set; }
    }

    public class RecentEnrollmentVM
    {
        public string? StudentName { get; set; }
        public string? CourseTitle { get; set; }
        public string? EnrolledDate { get; set; }
        public string? Initial { get; set; }
    }
}
