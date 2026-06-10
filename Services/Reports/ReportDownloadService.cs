using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkillForge.Interfaces;
using SkillForge.Models;
using System.Threading.Tasks;

namespace SkillForge.Services.Reports
{
    public class ReportDownloadService : IReportDownloadService
    {
        public async Task<byte[]> GenerateCourseFinancialReportPdfAsync(CourseFinancialReportVM data)
        {
            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        RenderHeader(page, $"{data.CourseTitle} Financial Report", data.Instructor);
                        
                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            // Course Summary Section
                            col.Item().PaddingBottom(10).Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Course Details").Bold().FontSize(12).FontColor(Colors.Purple.Medium);
                                    c.Item().Text($"Category: {data.Category}");
                                    c.Item().Text($"Level: {data.Level}");
                                    c.Item().Text($"Status: {data.Status}");
                                });
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text($"Publish Date: {data.PublishDate}");
                                    c.Item().Text($"Base Price: ₹{data.BasePrice:N0}");
                                    c.Item().Text($"Discount: {data.DiscountPercent}%");
                                    c.Item().Text($"Selling Price: ₹{data.SellingPrice:N0}");
                                });
                            });

                            // Financial Summary Boxes
                            col.Item().PaddingVertical(10).Row(row =>
                            {
                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(10).Column(c => { c.Item().Text("Students").FontSize(9); c.Item().Text(data.TotalStudents.ToString()).Bold().FontSize(14); });
                                row.ConstantItem(10);
                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(10).Column(c => { c.Item().Text("Gross Revenue").FontSize(9); c.Item().Text($"₹{data.GrossRevenue:N0}").Bold().FontSize(14); });
                                row.ConstantItem(10);
                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(10).Column(c => { c.Item().Text("Platform Fee (20%)").FontSize(9); c.Item().Text($"₹{data.PlatformFee:N0}").Bold().FontSize(14).FontColor(Colors.Red.Medium); });
                                row.ConstantItem(10);
                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(10).Column(c => { c.Item().Text("Net Earnings").FontSize(9); c.Item().Text($"₹{data.NetEarnings:N0}").Bold().FontSize(14).FontColor(Colors.Green.Medium); });
                            });

                            col.Item().PaddingTop(10).Text("Transaction History").Bold().FontSize(12).FontColor(Colors.Purple.Medium);

                            // Main Table
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(30);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("#");
                                    header.Cell().Element(CellStyle).Text("Student");
                                    header.Cell().Element(CellStyle).Text("Email");
                                    header.Cell().Element(CellStyle).Text("Date");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Paid");

                                    static IContainer CellStyle(IContainer container) => container.DefaultTextStyle(x => x.Bold().FontSize(10)).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                                });

                                int i = 1;
                                foreach (var t in data.Transactions)
                                {
                                    table.Cell().Element(RowStyle).Text(i++.ToString());
                                    table.Cell().Element(RowStyle).Text(t.StudentName);
                                    table.Cell().Element(RowStyle).Text(t.StudentEmail);
                                    table.Cell().Element(RowStyle).Text(t.EnrollmentDate);
                                    table.Cell().Element(RowStyle).AlignRight().Text($"₹{t.AmountPaid:N0}");

                                    static IContainer RowStyle(IContainer container) => container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten4).DefaultTextStyle(x => x.FontSize(10));
                                }
                            });
                        });

                        RenderFooter(page);
                    });
                });
                return doc.GeneratePdf();
            });
        }

        public async Task<byte[]> GenerateStudentListPdfAsync(CourseStudentListReportVM data)
        {
            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        RenderHeader(page, $"Enrolled Students for {data.CourseTitle} Course", data.Instructor);

                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            col.Item().PaddingBottom(10).Text(t =>
                            {
                                t.Span("Category: ").Bold(); t.Span(data.Category);
                                t.Span("  |  ").Bold();
                                t.Span("Total Active Students: ").Bold(); t.Span(data.TotalStudents.ToString());
                            });

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(30);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(4);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("#");
                                    header.Cell().Element(CellStyle).Text("Student Name");
                                    header.Cell().Element(CellStyle).Text("Email");
                                    header.Cell().Element(CellStyle).Text("Mobile");
                                    header.Cell().Element(CellStyle).Text("Enrolled Date");

                                    static IContainer CellStyle(IContainer container) => container.DefaultTextStyle(x => x.Bold().FontSize(10)).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                                });

                                int i = 1;
                                foreach (var s in data.Students)
                                {
                                    table.Cell().Element(RowStyle).Text(i++.ToString());
                                    table.Cell().Element(RowStyle).Text(s.Name);
                                    table.Cell().Element(RowStyle).Text(s.Email);
                                    table.Cell().Element(RowStyle).Text(s.Mobile ?? "-");
                                    table.Cell().Element(RowStyle).Text(s.EnrollmentDate);

                                    static IContainer RowStyle(IContainer container) => container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten4).DefaultTextStyle(x => x.FontSize(10));
                                }
                            });
                        });
                        RenderFooter(page);
                    });
                });
                return doc.GeneratePdf();
            });
        }

        public async Task<byte[]> GenerateMonthlyFinancialReportPdfAsync(MonthlyFinancialReportVM data)
        {
            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        RenderHeader(page, $"{data.ReportMonth} Financial Report", data.Instructor);

                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            // Monthly Summary Boxes
                            col.Item().PaddingVertical(10).Row(row =>
                            {
                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(10).Column(c => { c.Item().Text("Total Courses").FontSize(9); c.Item().Text(data.TotalCourses.ToString()).Bold().FontSize(14); });
                                row.ConstantItem(10);
                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(10).Column(c => { c.Item().Text("Gross Sales").FontSize(9); c.Item().Text($"₹{data.TotalEarning:N0}").Bold().FontSize(14); });
                                row.ConstantItem(10);
                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(10).Column(c => { c.Item().Text("Platform Fee").FontSize(9); c.Item().Text($"₹{data.PlatformFee:N0}").Bold().FontSize(14).FontColor(Colors.Red.Medium); });
                                row.ConstantItem(10);
                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(10).Column(c => { c.Item().Text("Net Share").FontSize(9); c.Item().Text($"₹{data.NetEarnings:N0}").Bold().FontSize(14).FontColor(Colors.Green.Medium); });
                            });

                            col.Item().PaddingTop(10).Text("Course-wise Monthly Breakdown").Bold().FontSize(12).FontColor(Colors.Purple.Medium);

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("Course Name");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Students");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Gross");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Fee");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Net");

                                    static IContainer CellStyle(IContainer container) => container.DefaultTextStyle(x => x.Bold().FontSize(10)).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                                });

                                foreach (var b in data.CourseBreakdowns)
                                {
                                    table.Cell().Element(RowStyle).Text(b.CourseName);
                                    table.Cell().Element(RowStyle).AlignRight().Text(b.NewStudents.ToString());
                                    table.Cell().Element(RowStyle).AlignRight().Text($"₹{b.GrossRevenue:N0}");
                                    table.Cell().Element(RowStyle).AlignRight().Text($"₹{b.PlatformFee:N0}");
                                    table.Cell().Element(RowStyle).AlignRight().Text($"₹{b.NetEarnings:N0}");

                                    static IContainer RowStyle(IContainer container) => container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten4).DefaultTextStyle(x => x.FontSize(10));
                                }
                            });
                        });
                        RenderFooter(page);
                    });
                });
                return doc.GeneratePdf();
            });
        }

        public async Task<byte[]> GenerateInstructorGlobalCourseReportPdfAsync(InstructorGlobalCourseReportVM data)
        {
            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        RenderHeader(page, "Global Course Performance Report", data.Instructor);

                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            // Global Summary Boxes
                            col.Item().PaddingVertical(10).Row(row =>
                            {
                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(10).Column(c => { c.Item().Text("Total Courses").FontSize(9); c.Item().Text(data.CourseEarnings.Count.ToString()).Bold().FontSize(14); });
                                row.ConstantItem(10);
                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(10).Column(c => { c.Item().Text("Lifetime Gross").FontSize(9); c.Item().Text($"₹{data.TotalGrossRevenue:N0}").Bold().FontSize(14); });
                                row.ConstantItem(10);
                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(10).Column(c => { c.Item().Text("Platform cut").FontSize(9); c.Item().Text($"₹{data.TotalPlatformFee:N0}").Bold().FontSize(14).FontColor(Colors.Red.Medium); });
                                row.ConstantItem(10);
                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(10).Column(c => { c.Item().Text("Lifetime Net").FontSize(9); c.Item().Text($"₹{data.TotalNetEarnings:N0}").Bold().FontSize(14).FontColor(Colors.Green.Medium); });
                            });

                            col.Item().PaddingTop(10).Text("Course-wise Lifetime Performance").Bold().FontSize(12).FontColor(Colors.Purple.Medium);

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("Course Title");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Students");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Avg Price");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Gross");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Net Share");

                                    static IContainer CellStyle(IContainer container) => container.DefaultTextStyle(x => x.Bold().FontSize(10)).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                                });

                                foreach (var b in data.CourseEarnings)
                                {
                                    table.Cell().Element(RowStyle).Text(b.CourseTitle);
                                    table.Cell().Element(RowStyle).AlignRight().Text(b.EnrolledStudents.ToString());
                                    table.Cell().Element(RowStyle).AlignRight().Text($"₹{b.PricePerStudent:N0}");
                                    table.Cell().Element(RowStyle).AlignRight().Text($"₹{b.GrossRevenue:N0}");
                                    table.Cell().Element(RowStyle).AlignRight().Text($"₹{b.InstructorEarnings:N0}");

                                    static IContainer RowStyle(IContainer container) => container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten4).DefaultTextStyle(x => x.FontSize(10));
                                }
                            });
                        });
                        RenderFooter(page);
                    });
                });
                return doc.GeneratePdf();
            });
        }

        public async Task<byte[]> GenerateAdminEnrollmentReportPdfAsync(AdminEnrollmentReportVM data)
        {
            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        RenderAdminHeader(page, data.Title);

                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            col.Item().PaddingBottom(10).Row(row =>
                            {
                                row.RelativeItem().Text(t => { t.Span("Date Range: ").Bold(); t.Span(data.DateRange); });
                                row.RelativeItem().AlignRight().Text(t => { t.Span("Total Enrollments: ").Bold(); t.Span(data.TotalEnrollments.ToString()); });
                            });

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(30);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(AdminCellStyle).Text("#");
                                    header.Cell().Element(AdminCellStyle).Text("Student");
                                    header.Cell().Element(AdminCellStyle).Text("Course");
                                    header.Cell().Element(AdminCellStyle).Text("Instructor");
                                    header.Cell().Element(AdminCellStyle).AlignRight().Text("Date");
                                });

                                int i = 1;
                                foreach (var e in data.Enrollments)
                                {
                                    table.Cell().Element(AdminRowStyle).Text(i++.ToString());
                                    table.Cell().Element(AdminRowStyle).Text(e.StudentName);
                                    table.Cell().Element(AdminRowStyle).Text(e.CourseTitle);
                                    table.Cell().Element(AdminRowStyle).Text(e.InstructorName);
                                    table.Cell().Element(AdminRowStyle).AlignRight().Text(e.EnrollmentDate);
                                }
                            });
                        });
                        RenderAdminFooter(page, data.TotalRecords);
                    });
                });
                return doc.GeneratePdf();
            });
        }

        public async Task<byte[]> GenerateAdminSalesReportPdfAsync(AdminSalesReportVM data)
        {
            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        RenderAdminHeader(page, data.Title);

                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            col.Item().PaddingBottom(10).Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text(t => { t.Span("Date Range: ").Bold(); t.Span(data.DateRange); });
                                    c.Item().Text(t => { t.Span("Total Orders: ").Bold(); t.Span(data.TotalOrders.ToString()); });
                                });
                                row.RelativeItem().AlignRight().Column(c =>
                                {
                                    c.Item().Text(t => { t.Span("Gross Revenue: ").Bold(); t.Span($"₹{data.TotalRevenue:N0}"); });
                                    c.Item().Text(t => { t.Span("Avg Order Value: ").Bold(); t.Span($"₹{data.AvgOrderValue:N0}"); });
                                });
                            });

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(40);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(4);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(AdminCellStyle).Text("Order");
                                    header.Cell().Element(AdminCellStyle).Text("Student");
                                    header.Cell().Element(AdminCellStyle).Text("Course");
                                    header.Cell().Element(AdminCellStyle).AlignRight().Text("Amount");
                                    header.Cell().Element(AdminCellStyle).AlignRight().Text("Date");
                                });

                                foreach (var s in data.Sales)
                                {
                                    table.Cell().Element(AdminRowStyle).Text($"#{s.OrderId}");
                                    table.Cell().Element(AdminRowStyle).Text(s.StudentName);
                                    table.Cell().Element(AdminRowStyle).Text(s.CourseTitle);
                                    table.Cell().Element(AdminRowStyle).AlignRight().Text($"₹{s.Amount:N0}");
                                    table.Cell().Element(AdminRowStyle).AlignRight().Text(s.PurchaseDate);
                                }
                            });
                        });
                        RenderAdminFooter(page, data.TotalRecords);
                    });
                });
                return doc.GeneratePdf();
            });
        }

        public async Task<byte[]> GenerateAdminStudentReportPdfAsync(AdminStudentReportVM data)
        {
            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        RenderAdminHeader(page, data.Title);

                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            col.Item().PaddingBottom(10).Row(row =>
                            {
                                row.RelativeItem().Text(t => { t.Span("Total Students: ").Bold(); t.Span(data.TotalStudents.ToString()); });
                                row.RelativeItem().AlignRight().Text(t => { t.Span("Total Enrollments: ").Bold(); t.Span(data.TotalEnrollments.ToString()); });
                            });

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(30);
                                    columns.RelativeColumn(4);
                                    columns.RelativeColumn(5);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(AdminCellStyle).Text("#");
                                    header.Cell().Element(AdminCellStyle).Text("Student Name");
                                    header.Cell().Element(AdminCellStyle).Text("Email");
                                    header.Cell().Element(AdminCellStyle).Text("Registered Date");
                                    header.Cell().Element(AdminCellStyle).AlignRight().Text("Total Courses");
                                });

                                int i = 1;
                                foreach (var s in data.Students)
                                {
                                    table.Cell().Element(AdminRowStyle).Text(i++.ToString());
                                    table.Cell().Element(AdminRowStyle).Text(s.Name);
                                    table.Cell().Element(AdminRowStyle).Text(s.Email);
                                    table.Cell().Element(AdminRowStyle).Text(s.JoinedDate);
                                    table.Cell().Element(AdminRowStyle).AlignRight().Text(s.TotalCourses.ToString());
                                }
                            });
                        });
                        RenderAdminFooter(page, data.TotalRecords);
                    });
                });
                return doc.GeneratePdf();
            });
        }

        public async Task<byte[]> GenerateAdminInstructorReportPdfAsync(AdminInstructorReportVM data)
        {
            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        RenderAdminHeader(page, data.Title);

                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            col.Item().PaddingBottom(10).Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text(t => { t.Span("Total Approved Instructors: ").Bold(); t.Span(data.TotalInstructors.ToString()); });
                                    c.Item().Text(t => { t.Span("Total Published Courses: ").Bold(); t.Span(data.TotalCourses.ToString()); });
                                });
                                row.RelativeItem().AlignRight().Text(t => { t.Span("Total Students Taught: ").Bold(); t.Span(data.TotalStudentsTaught.ToString()); });
                            });

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(4);
                                    columns.RelativeColumn(1.5f);
                                    columns.RelativeColumn(1.5f);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(AdminCellStyle).Text("Instructor");
                                    header.Cell().Element(AdminCellStyle).Text("Email");
                                    header.Cell().Element(AdminCellStyle).AlignRight().Text("Courses");
                                    header.Cell().Element(AdminCellStyle).AlignRight().Text("Students");
                                    header.Cell().Element(AdminCellStyle).AlignRight().Text("Joined Date");
                                });

                                foreach (var ins in data.Instructors)
                                {
                                    table.Cell().Element(AdminRowStyle).Text(ins.Name);
                                    table.Cell().Element(AdminRowStyle).Text(ins.Email);
                                    table.Cell().Element(AdminRowStyle).AlignRight().Text(ins.Courses.ToString());
                                    table.Cell().Element(AdminRowStyle).AlignRight().Text(ins.Students.ToString());
                                    table.Cell().Element(AdminRowStyle).AlignRight().Text(ins.JoinedDate);
                                }
                            });
                        });
                        RenderAdminFooter(page, data.TotalRecords);
                    });
                });
                return doc.GeneratePdf();
            });
        }

        public async Task<byte[]> GenerateAdminRevenueReportPdfAsync(AdminRevenueReportVM data)
        {
            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        RenderAdminHeader(page, data.Title);

                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            col.Item().PaddingBottom(10).Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text(t => { t.Span("Date Range: ").Bold(); t.Span(data.DateRange); });
                                    c.Item().Text(t => { t.Span("Gross Revenue: ").Bold(); t.Span($"₹{data.GrossRevenue:N0}"); });
                                });
                                row.RelativeItem().AlignRight().Column(c =>
                                {
                                    c.Item().Text(t => { t.Span("Total Orders: ").Bold(); t.Span(data.TotalOrders.ToString()); });
                                    c.Item().Text(t => { t.Span("Avg Rev/Order: ").Bold(); t.Span($"₹{data.AvgRevenuePerOrder:N0}"); });
                                });
                            });

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(3);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(AdminCellStyle).Text("Date");
                                    header.Cell().Element(AdminCellStyle).AlignRight().Text("Orders");
                                    header.Cell().Element(AdminCellStyle).AlignRight().Text("Revenue");
                                });

                                foreach (var r in data.RevenueData)
                                {
                                    table.Cell().Element(AdminRowStyle).Text(r.Date);
                                    table.Cell().Element(AdminRowStyle).AlignRight().Text(r.Orders.ToString());
                                    table.Cell().Element(AdminRowStyle).AlignRight().Text($"₹{r.Revenue:N0}");
                                }
                            });
                        });
                        RenderAdminFooter(page, data.TotalRecords);
                    });
                });
                return doc.GeneratePdf();
            });
        }

        public async Task<byte[]> GenerateAdminPayoutReportPdfAsync(AdminPayoutReportVM data)
        {
            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        RenderAdminHeader(page, data.Title);

                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            col.Item().PaddingBottom(10).Row(row =>
                            {
                                row.RelativeItem().Text(t => { t.Span("Total Instructor Revenue: ").Bold(); t.Span($"₹{data.TotalInstructorRevenue:N0}"); });
                                row.RelativeItem().AlignRight().Text(t => { t.Span("Total Payouts: ").Bold(); t.Span($"₹{data.TotalPayouts:N0}"); });
                            });

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(4);
                                    columns.RelativeColumn(1.5f);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(AdminCellStyle).Text("Instructor");
                                    header.Cell().Element(AdminCellStyle).AlignRight().Text("Courses");
                                    header.Cell().Element(AdminCellStyle).AlignRight().Text("Revenue Generated");
                                    header.Cell().Element(AdminCellStyle).AlignRight().Text("Commission");
                                    header.Cell().Element(AdminCellStyle).AlignRight().Text("Payout Amount");
                                });

                                foreach (var p in data.Payouts)
                                {
                                    table.Cell().Element(AdminRowStyle).Text(p.InstructorName);
                                    table.Cell().Element(AdminRowStyle).AlignRight().Text(p.Courses.ToString());
                                    table.Cell().Element(AdminRowStyle).AlignRight().Text($"₹{p.RevenueGenerated:N0}");
                                    table.Cell().Element(AdminRowStyle).AlignRight().Text($"₹{p.Commission:N0}");
                                    table.Cell().Element(AdminRowStyle).AlignRight().Text($"₹{p.PayoutAmount:N0}");
                                }
                            });
                        });
                        RenderAdminFooter(page, data.TotalRecords);
                    });
                });
                return doc.GeneratePdf();
            });
        }

        public async Task<byte[]> GenerateAdminApplicationsReportPdfAsync(AdminApplicationsReportVM data)
        {
            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        RenderAdminHeader(page, data.Title);

                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            col.Item().PaddingBottom(10).Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text(t => { t.Span("Total Applications: ").Bold(); t.Span(data.TotalApplications.ToString()); });
                                    c.Item().Text(t => { t.Span("Approved: ").Bold(); t.Span(data.Approved.ToString()); });
                                });
                                row.RelativeItem().AlignRight().Column(c =>
                                {
                                    c.Item().Text(t => { t.Span("Pending: ").Bold(); t.Span(data.Pending.ToString()); });
                                    c.Item().Text(t => { t.Span("Rejected: ").Bold(); t.Span(data.Rejected.ToString()); });
                                });
                            });

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(4);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(AdminCellStyle).Text("Applicant");
                                    header.Cell().Element(AdminCellStyle).Text("Email");
                                    header.Cell().Element(AdminCellStyle).Text("Specialization");
                                    header.Cell().Element(AdminCellStyle).AlignRight().Text("Applied Date");
                                    header.Cell().Element(AdminCellStyle).AlignRight().Text("Status");
                                });

                                foreach (var a in data.Applications)
                                {
                                    table.Cell().Element(AdminRowStyle).Text(a.ApplicantName);
                                    table.Cell().Element(AdminRowStyle).Text(a.Email);
                                    table.Cell().Element(AdminRowStyle).Text(a.Specialization);
                                    table.Cell().Element(AdminRowStyle).AlignRight().Text(a.AppliedDate);
                                    table.Cell().Element(AdminRowStyle).AlignRight().Text(a.Status);
                                }
                            });
                        });
                        RenderAdminFooter(page, data.TotalRecords);
                    });
                });
                return doc.GeneratePdf();
            });
        }

        public async Task<byte[]> GenerateAdminCoursesReportPdfAsync(AdminCoursesReportVM data)
        {
            return await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        RenderAdminHeader(page, data.Title);

                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            col.Item().PaddingBottom(10).Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text(t => { t.Span("Total Courses: ").Bold(); t.Span(data.TotalRecords.ToString()); });
                                    c.Item().Text(t => { t.Span("Active Courses: ").Bold(); t.Span(data.ActiveCourses.ToString()); });
                                });
                                row.RelativeItem().AlignRight().Column(c =>
                                {
                                    c.Item().Text(t => { t.Span("Pending Review: ").Bold(); t.Span(data.PendingCourses.ToString()); });
                                });
                            });

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1.5f);
                                    columns.RelativeColumn(1.5f);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(AdminCellStyle).Text("Course");
                                    header.Cell().Element(AdminCellStyle).Text("Instructor");
                                    header.Cell().Element(AdminCellStyle).Text("Category");
                                    header.Cell().Element(AdminCellStyle).AlignRight().Text("Students");
                                    header.Cell().Element(AdminCellStyle).AlignRight().Text("Earnings");
                                    header.Cell().Element(AdminCellStyle).AlignRight().Text("Status");
                                });

                                foreach (var c in data.Courses)
                                {
                                    table.Cell().Element(AdminRowStyle).Text(c.Title);
                                    table.Cell().Element(AdminRowStyle).Text(c.InstructorName);
                                    table.Cell().Element(AdminRowStyle).Text(c.Category);
                                    table.Cell().Element(AdminRowStyle).AlignRight().Text(c.Students.ToString());
                                    table.Cell().Element(AdminRowStyle).AlignRight().Text($"₹{c.Earnings:N0}");
                                    table.Cell().Element(AdminRowStyle).AlignRight().Text(c.Status);
                                }
                            });
                        });
                        RenderAdminFooter(page, data.TotalRecords);
                    });
                });
                return doc.GeneratePdf();
            });
        }

        private void RenderAdminHeader(PageDescriptor page, string title)
        {
            page.Header().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("SkillForge").FontSize(24).Bold().FontColor(Colors.Green.Medium);
                    col.Item().Text("Admin Report Center").FontSize(10).FontColor(Colors.Grey.Medium);
                });
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text(title).FontSize(16).Bold().FontColor(Colors.Green.Medium);
                    col.Item().Text($"Date: {System.DateTime.Now:dd MMM yyyy}").FontSize(9).FontColor(Colors.Grey.Medium);
                });
            });
        }

        private void RenderAdminFooter(PageDescriptor page, int totalRecords)
        {
            page.Footer().PaddingTop(10).Column(col =>
            {
                col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                col.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Generated by SkillForge Admin").FontSize(9).FontColor(Colors.Grey.Medium);
                        c.Item().Text($"Generated: {System.DateTime.Now:dd MMM yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Lighten1);
                    });
                    
                    row.RelativeItem().AlignCenter().Text(x =>
                    {
                        x.Span("Page ").FontSize(9).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Medium);
                        x.Span(" of ").FontSize(9).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(9).FontColor(Colors.Grey.Medium);
                    });

                    row.RelativeItem().AlignRight().Text($"Total Records: {totalRecords}").FontSize(9).FontColor(Colors.Grey.Medium).Bold();
                });
            });
        }

        private static IContainer AdminCellStyle(IContainer container) => 
            container.DefaultTextStyle(x => x.Bold().FontSize(10).FontColor(Colors.White))
                     .PaddingVertical(5).PaddingHorizontal(5)
                     .Background(Colors.Green.Medium);

        private static IContainer AdminRowStyle(IContainer container) => 
            container.PaddingVertical(5).PaddingHorizontal(5)
                     .BorderBottom(1).BorderColor(Colors.Grey.Lighten4)
                     .DefaultTextStyle(x => x.FontSize(10));

        private void RenderHeader(PageDescriptor page, string title, InstructorInfoVM instructor)
        {
            page.Header().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("SkillForge").FontSize(24).Bold().FontColor(Colors.Purple.Medium);
                    col.Item().Text(title).FontSize(14).SemiBold();
                });
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text($"Date: {System.DateTime.Now:dd MMM yyyy}").FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().Text(instructor.Name).SemiBold().FontSize(11);
                    col.Item().Text(instructor.Email).FontSize(9).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrEmpty(instructor.Mobile)) col.Item().Text(instructor.Mobile).FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            });
        }

        private void RenderFooter(PageDescriptor page)
        {
            page.Footer().PaddingTop(10).AlignCenter().Text(x =>
            {
                x.Span("SkillForge Platform - Private & Confidential | Page ").FontSize(9).FontColor(Colors.Grey.Medium);
                x.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Medium);
                x.Span(" of ").FontSize(9).FontColor(Colors.Grey.Medium);
                x.TotalPages().FontSize(9).FontColor(Colors.Grey.Medium);
            });
        }
    }
}
