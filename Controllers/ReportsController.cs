using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ward_Management_System.Data;
using Ward_Management_System.Models;

namespace Ward_Management_System.Controllers
{
    public class ReportsController : Controller
    {
        private readonly AppDbContext _context;
        public ReportsController(AppDbContext context)
        {
            _context = context;
        }

        // Export to PDF
        public async Task<IActionResult> ExportMedicationSummaryToPdf()
        {
            // Query summary with total
            var summary = await _context.StockMedications
                .Include(m => m.MedicationCategory)
                .GroupBy(m => m.MedicationCategory.Name)
                .Select(g => new
                {
                    Category = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            int total = summary.Sum(s => s.Count);

            using (MemoryStream stream = new MemoryStream())
            {
                Document doc = new Document(PageSize.A4, 40, 40, 40, 40);
                PdfWriter writer = PdfWriter.GetInstance(doc, stream);
                doc.Open();

                // ✅ Logo
                string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(logoPath);
                    logo.ScaleAbsolute(80, 80);
                    logo.Alignment = Element.ALIGN_LEFT;
                    doc.Add(logo);
                }

                // ✅ Title
                var titleFont = FontFactory.GetFont("Arial", 20, Font.BOLD, new BaseColor(0, 51, 153));
                var subFont = FontFactory.GetFont("Arial", 10, new BaseColor(128, 128, 128));
                doc.Add(new Paragraph("Medication Summary Report", titleFont));
                doc.Add(new Paragraph($"Generated on: {DateTime.Now:dd MMM yyyy HH:mm}", subFont));
                doc.Add(new Paragraph("\n"));

                // ✅ Table Header
                PdfPTable table = new PdfPTable(3) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 40f, 20f, 40f });

                BaseColor headerColor = new BaseColor(52, 152, 219); // blue
                Font headerFont = FontFactory.GetFont("Arial", 12, Font.BOLD, new BaseColor(255, 255, 255));

                PdfPCell cell1 = new PdfPCell(new Phrase("Category", headerFont)) { BackgroundColor = headerColor };
                PdfPCell cell2 = new PdfPCell(new Phrase("Total", headerFont)) { BackgroundColor = headerColor };
                PdfPCell cell3 = new PdfPCell(new Phrase("Distribution", headerFont)) { BackgroundColor = headerColor };

                table.AddCell(cell1);
                table.AddCell(cell2);
                table.AddCell(cell3);

                // ✅ Table Rows with Progress Bar
                foreach (var item in summary)
                {
                    float percentage = total > 0 ? ((float)item.Count / total) * 100 : 0;

                    table.AddCell(new Phrase(item.Category, FontFactory.GetFont("Arial", 11)));
                    table.AddCell(new Phrase(item.Count.ToString(), FontFactory.GetFont("Arial", 11)));

                    // Draw progress bar
                    PdfPCell barCell = new PdfPCell();
                    barCell.MinimumHeight = 20;

                    PdfTemplate template = writer.DirectContent.CreateTemplate(200, 20);
                    BaseColor barColor = new BaseColor(46, 204, 113); // green
                    BaseColor bgColor = new BaseColor(220, 220, 220);

                    // Background bar
                    template.SetColorFill(bgColor);
                    template.Rectangle(0, 0, 200, 15);
                    template.Fill();

                    // Filled bar
                    template.SetColorFill(barColor);
                    template.Rectangle(0, 0, (percentage / 100) * 200, 15);
                    template.Fill();

                    // Overlay percentage text
                    ColumnText.ShowTextAligned(template, Element.ALIGN_CENTER,
                        new Phrase($"{percentage:F1}%", FontFactory.GetFont("Arial", 8, new BaseColor(0, 0, 0))),
                        100, 2, 0);

                    iTextSharp.text.Image barImage = iTextSharp.text.Image.GetInstance(template);
                    barCell.AddElement(barImage);

                    table.AddCell(barCell);
                }

                doc.Add(table);
                doc.Close();

                return File(stream.ToArray(), "application/pdf", "MedicationSummary.pdf");
            }
        }

    }
}
