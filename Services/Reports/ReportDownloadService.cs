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

                           
    }
}
