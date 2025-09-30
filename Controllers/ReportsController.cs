using System.Drawing;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using Ward_Management_System.Data;
using Ward_Management_System.Models;
using Ward_Management_System.ViewModels;

namespace Ward_Management_System.Controllers
{
    public class ReportsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly IWebHostEnvironment _env;
        public ReportsController(AppDbContext context, IWebHostEnvironment env, UserManager<Users> userManager)
        {
            _context = context;
            _env = env;
            _userManager = userManager;
        }

        // Export to MedicationSummaryPDF
        public async Task<IActionResult> ExportMedicationSummaryToPdf(string brand = null, int? categoryId = null, string storage = null, string search = null)
        {
            // Filtered medications
            var medicationsQuery = _context.StockMedications
                .Include(m => m.MedicationCategory)
                .AsQueryable();

            if (!string.IsNullOrEmpty(brand))
                medicationsQuery = medicationsQuery.Where(m => m.Brand == brand);

            if (categoryId.HasValue)
                medicationsQuery = medicationsQuery.Where(m => m.MedicationCategory.ID == categoryId.Value);

            if (!string.IsNullOrEmpty(storage))
                medicationsQuery = medicationsQuery.Where(m => m.Storage == storage);

            if (!string.IsNullOrEmpty(search))
                medicationsQuery = medicationsQuery.Where(m => m.Name.Contains(search));

            var medications = await medicationsQuery.ToListAsync();

            // Summary
            var summary = medications
                .GroupBy(m => m.MedicationCategory.Name)
                .Select(g => new
                {
                    Category = g.Key,
                    Count = g.Count()
                })
                .ToList();

            int total = summary.Sum(s => s.Count);

            using (MemoryStream stream = new MemoryStream())
            {
                Document doc = new Document(PageSize.A4, 40, 40, 40, 40);
                PdfWriter writer = PdfWriter.GetInstance(doc, stream);
                doc.Open();

                // Logo
                string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(logoPath);
                    logo.ScaleAbsolute(80, 80);
                    logo.Alignment = Element.ALIGN_LEFT;
                    doc.Add(logo);
                }

                // Title
                var titleFont = FontFactory.GetFont("Arial", 20, iTextSharp.text.Font.BOLD, new BaseColor(0, 51, 153));
                var subFont = FontFactory.GetFont("Arial", 10, new BaseColor(128, 128, 128));
                doc.Add(new Paragraph("Medication Summary Report", titleFont));
                doc.Add(new Paragraph($"Generated on: {DateTime.Now:dd MMM yyyy HH:mm}", subFont));
                doc.Add(new Paragraph("\n"));

                // Summary Table
                PdfPTable summaryTable = new PdfPTable(3) { WidthPercentage = 100 };
                summaryTable.SetWidths(new float[] { 40f, 20f, 40f });

                BaseColor headerColor = new BaseColor(52, 152, 219); // blue
                iTextSharp.text.Font headerFont = FontFactory.GetFont("Arial", 12, iTextSharp.text.Font.BOLD, new BaseColor(255, 255, 255)
);

                summaryTable.AddCell(new PdfPCell(new Phrase("Category", headerFont)) { BackgroundColor = headerColor });
                summaryTable.AddCell(new PdfPCell(new Phrase("Total", headerFont)) { BackgroundColor = headerColor });
                summaryTable.AddCell(new PdfPCell(new Phrase("Distribution", headerFont)) { BackgroundColor = headerColor });

                foreach (var item in summary)
                {
                    float percentage = total > 0 ? ((float)item.Count / total) * 100 : 0;

                    summaryTable.AddCell(new Phrase(item.Category, FontFactory.GetFont("Arial", 11)));
                    summaryTable.AddCell(new Phrase(item.Count.ToString(), FontFactory.GetFont("Arial", 11)));

                    // Draw progress bar
                    PdfPCell barCell = new PdfPCell { MinimumHeight = 20 };
                    PdfTemplate template = writer.DirectContent.CreateTemplate(200, 20);

                    BaseColor barColor = new BaseColor(46, 204, 113); // green
                    BaseColor bgColor = new BaseColor(220, 220, 220);

                    template.SetColorFill(bgColor);
                    template.Rectangle(0, 0, 200, 15);
                    template.Fill();

                    template.SetColorFill(barColor);
                    template.Rectangle(0, 0, (percentage / 100) * 200, 15);
                    template.Fill();

                    ColumnText.ShowTextAligned(template, Element.ALIGN_CENTER,
                        new Phrase($"{percentage:F1}%", FontFactory.GetFont("Arial", 8, new BaseColor(255, 255, 255))),
                        100, 2, 0);

                    iTextSharp.text.Image barImage = iTextSharp.text.Image.GetInstance(template);
                    barCell.AddElement(barImage);

                    summaryTable.AddCell(barCell);
                }

                doc.Add(summaryTable);
                doc.Add(new Paragraph("\n\n")); // spacing

                // Detailed Medications Table
                var detailFont = FontFactory.GetFont("Arial", 10);
                PdfPTable detailTable = new PdfPTable(7) { WidthPercentage = 100 };
                detailTable.SetWidths(new float[] { 5f, 25f, 15f, 10f, 10f, 15f, 20f });

                detailTable.AddCell(new PdfPCell(new Phrase("#", headerFont)) { BackgroundColor = headerColor });
                detailTable.AddCell(new PdfPCell(new Phrase("Name", headerFont)) { BackgroundColor = headerColor });
                detailTable.AddCell(new PdfPCell(new Phrase("Brand", headerFont)) { BackgroundColor = headerColor });
                detailTable.AddCell(new PdfPCell(new Phrase("Qty", headerFont)) { BackgroundColor = headerColor });
                detailTable.AddCell(new PdfPCell(new Phrase("Batch", headerFont)) { BackgroundColor = headerColor });
                detailTable.AddCell(new PdfPCell(new Phrase("Storage", headerFont)) { BackgroundColor = headerColor });
                detailTable.AddCell(new PdfPCell(new Phrase("Category", headerFont)) { BackgroundColor = headerColor });

                int count = 1;
                foreach (var med in medications)
                {
                    detailTable.AddCell(new PdfPCell(new Phrase(count.ToString(), detailFont)));
                    detailTable.AddCell(new PdfPCell(new Phrase(med.Name, detailFont)));
                    detailTable.AddCell(new PdfPCell(new Phrase(med.Brand, detailFont)));
                    detailTable.AddCell(new PdfPCell(new Phrase(med.QuantityAvailable.ToString(), detailFont)));
                    detailTable.AddCell(new PdfPCell(new Phrase(med.BatchNumber, detailFont)));
                    detailTable.AddCell(new PdfPCell(new Phrase(med.Storage, detailFont)));
                    detailTable.AddCell(new PdfPCell(new Phrase(med.MedicationCategory?.Name, detailFont)));
                    count++;
                }

                doc.Add(detailTable);
                doc.Close();

                return File(stream.ToArray(), "application/pdf", "MedicationSummary.pdf");
            }
        }


        // Export RegisteredUsersPDF
        public IActionResult ExportUsersPdf(DateTime? startDate, DateTime? endDate, string gender, string ageGroup, string search)
        {
            // 1. Get filtered users
            var users = GetFilteredUsers(startDate, endDate, gender, ageGroup, search);

            using var ms = new MemoryStream();
            var doc = new Document(PageSize.A4, 25, 25, 40, 40);
            var writer = PdfWriter.GetInstance(doc, ms);

            doc.Open();

            // === 2. Add logo ===
            var logoPath = Path.Combine(_env.WebRootPath, "images", "logo.png");
            if (System.IO.File.Exists(logoPath))
            {
                iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(logoPath);
                logo.ScaleAbsolute(100f, 50f);
                logo.Alignment = Element.ALIGN_LEFT;
                doc.Add(logo);
            }

            // === 3. Add title ===
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, new BaseColor(0, 0, 139));
            var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.Gray);

            var title = new Paragraph("Registered Users Report", titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 5f
            };
            var subtitle = new Paragraph($"Generated on {DateTime.Now:dd MMM yyyy HH:mm}", subtitleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20f
            };

            doc.Add(title);
            doc.Add(subtitle);

            // === 4. Create user table ===
            var table = new PdfPTable(8) { WidthPercentage = 100, SpacingBefore = 10f, SpacingAfter = 15f };
            table.SetWidths(new float[] { 3f, 10f, 12f, 4f, 12f, 8f, 10f, 5f });

            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.White);
            BaseColor headerBg = new BaseColor(52, 73, 94); // dark gray-blue

            string[] headers = { "#", "Full Name", "Email", "Age", "Address", "Phone", "ID Number", "Gender" };
            foreach (var header in headers)
            {
                var cell = new PdfPCell(new Phrase(header, headerFont))
                {
                    BackgroundColor = headerBg,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Padding = 5
                };
                table.AddCell(cell);
            }

            var rowFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
            int count = 1;
            foreach (var u in users)
            {
                // Alternate row background color
                BaseColor bgColor = count % 2 == 0 ? new BaseColor(245, 245, 245) : BaseColor.White;

                table.AddCell(new PdfPCell(new Phrase(count.ToString(), rowFont)) { BackgroundColor = bgColor });
                table.AddCell(new PdfPCell(new Phrase(u.FullName, rowFont)) { BackgroundColor = bgColor });
                table.AddCell(new PdfPCell(new Phrase(u.Email, rowFont)) { BackgroundColor = bgColor });
                table.AddCell(new PdfPCell(new Phrase(u.Age.ToString(), rowFont)) { BackgroundColor = bgColor });
                table.AddCell(new PdfPCell(new Phrase(u.Address, rowFont)) { BackgroundColor = bgColor });
                table.AddCell(new PdfPCell(new Phrase(u.PhoneNumber, rowFont)) { BackgroundColor = bgColor });
                table.AddCell(new PdfPCell(new Phrase(u.IdNumber, rowFont)) { BackgroundColor = bgColor });
                table.AddCell(new PdfPCell(new Phrase(u.Gender, rowFont)) { BackgroundColor = bgColor });

                count++;
            }

            doc.Add(table);

            // === 5. Add statistics section ===
            var statsTitle = new Paragraph("📊 User Statistics", titleFont)
            {
                SpacingBefore = 10f,
                SpacingAfter = 10f
            };
            doc.Add(statsTitle);

            int totalUsers = users.Count;
            int maleCount = users.Count(u => u.Gender == "Male");
            int femaleCount = users.Count(u => u.Gender == "Female");
            int young = users.Count(u => u.Age < 30);
            int middle = users.Count(u => u.Age >= 30 && u.Age < 50);
            int senior = users.Count(u => u.Age >= 50);

            // Stats table
            var statsTable = new PdfPTable(2) { WidthPercentage = 50, SpacingBefore = 10f };
            statsTable.HorizontalAlignment = Element.ALIGN_LEFT;
            statsTable.SetWidths(new float[] { 2f, 1f });

            var statHeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.White);
            var statRowFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);

            var statHeaders = new[] { "Category", "Count" };
            foreach (var h in statHeaders)
            {
                statsTable.AddCell(new PdfPCell(new Phrase(h, statHeaderFont))
                {
                    BackgroundColor = new BaseColor(39, 174, 96), // green
                    Padding = 5,
                    HorizontalAlignment = Element.ALIGN_CENTER
                });
            }

            void AddStatRow(string label, int value)
            {
                statsTable.AddCell(new PdfPCell(new Phrase(label, statRowFont)) { Padding = 5 });
                statsTable.AddCell(new PdfPCell(new Phrase(value.ToString(), statRowFont)) { Padding = 5, HorizontalAlignment = Element.ALIGN_CENTER });
            }

            AddStatRow("Total Users", totalUsers);
            AddStatRow("Male", maleCount);
            AddStatRow("Female", femaleCount);
            AddStatRow("Age 18-29", young);
            AddStatRow("Age 30-49", middle);
            AddStatRow("Age 50+", senior);

            doc.Add(statsTable);

            // === 6. Footer note ===
            var footer = new Paragraph("\nGenerated by Ward Management System © " + DateTime.Now.Year,
                FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, BaseColor.Gray))
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingBefore = 20f
            };
            doc.Add(footer);

            doc.Close();

            return File(ms.ToArray(), "application/pdf", "RegisteredUsers.pdf");
        }


        private List<StaffListViewModel> GetFilteredUsers(DateTime? startDate, DateTime? endDate, string gender, string ageGroup, string search)
        {
            // Get all active users
            var activeUsers = _userManager.Users
                .Where(u => u.IsActive)
                .ToList();

            var staffList = new List<StaffListViewModel>();

            foreach (var user in activeUsers)
            {
                var roles = _userManager.GetRolesAsync(user).Result; // synchronous call inside method
                if (roles.Count == 1 && roles.Contains("User")) // Only registered users
                {
                    staffList.Add(new StaffListViewModel
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        Email = user.Email,
                        Roles = string.Join(", ", roles),
                        Age = user.Age,
                        Address = user.Address,
                        PhoneNumber = user.PhoneNumber,
                        IdNumber = user.IdNumber,
                        Gender = user.Gender,
                        DateAdded = user.DateAdded
                    });
                }
            }

            // Apply filters
            if (startDate.HasValue && endDate.HasValue)
            {
                staffList = staffList
                    .Where(s => s.DateAdded.Date >= startDate.Value.Date && s.DateAdded.Date <= endDate.Value.Date)
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(gender))
            {
                staffList = staffList
                    .Where(s => s.Gender.Equals(gender, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(ageGroup))
            {
                staffList = staffList.Where(s => ageGroup switch
                {
                    "young" => s.Age < 30,
                    "middle" => s.Age >= 30 && s.Age <= 50,
                    "senior" => s.Age > 50,
                    _ => true
                }).ToList();
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                staffList = staffList
                    .Where(s => s.FullName.ToLower().Contains(search)
                             || s.Email.ToLower().Contains(search)
                             || s.Roles.ToLower().Contains(search)
                             || s.Address.ToLower().Contains(search)
                             || s.PhoneNumber.ToLower().Contains(search)
                             || s.IdNumber.ToLower().Contains(search)
                             || s.Gender.ToLower().Contains(search))
                    .ToList();
            }

            return staffList;
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportStaffPdf(string role = "", string gender = "", string ageGroup = "", string search = "")
        {
            // 1. Get filtered staff
            var activeUsers = await _userManager.Users
                .Where(u => u.IsActive)
                .ToListAsync();

            var staffList = new List<StaffListViewModel>();

            foreach (var user in activeUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);

                if (roles.Any(r => r != "Admin" && r != "User"))
                {
                    staffList.Add(new StaffListViewModel
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        Email = user.Email,
                        Roles = string.Join(", ", roles),
                        Age = user.Age,
                        Address = user.Address,
                        PhoneNumber = user.PhoneNumber,
                        IdNumber = user.IdNumber,
                        Gender = user.Gender
                    });
                }
            }

            // Apply filters
            if (!string.IsNullOrWhiteSpace(role))
                staffList = staffList.Where(s => s.Roles.Split(",").Any(r => r.Trim().Equals(role, StringComparison.OrdinalIgnoreCase))).ToList();

            if (!string.IsNullOrWhiteSpace(gender))
                staffList = staffList.Where(s => s.Gender.Equals(gender, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrWhiteSpace(ageGroup))
                staffList = staffList.Where(s => ageGroup switch
                {
                    "young" => s.Age < 30,
                    "middle" => s.Age >= 30 && s.Age <= 50,
                    "senior" => s.Age > 50,
                    _ => true
                }).ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                staffList = staffList.Where(s =>
                    s.FullName.ToLower().Contains(search) ||
                    s.Email.ToLower().Contains(search) ||
                    s.Roles.ToLower().Contains(search) ||
                    s.Address.ToLower().Contains(search) ||
                    s.PhoneNumber.ToLower().Contains(search) ||
                    s.IdNumber.ToLower().Contains(search) ||
                    s.Gender.ToLower().Contains(search)).ToList();
            }

            // 2. Generate PDF
            using var stream = new MemoryStream();
            var doc = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 25, 25, 40, 40);
            var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(doc, stream);
            doc.Open();

            // Logo
            string logoPath = Path.Combine(_env.WebRootPath, "images", "logo.png");
            if (System.IO.File.Exists(logoPath))
            {
                var logo = iTextSharp.text.Image.GetInstance(logoPath);
                logo.ScaleAbsolute(100f, 50f);
                logo.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                doc.Add(logo);
            }

            // Title
            var darkBlue = new iTextSharp.text.BaseColor(0, 0, 139);
            var titleFont = iTextSharp.text.FontFactory.GetFont(
                iTextSharp.text.FontFactory.HELVETICA_BOLD,
                16,
                darkBlue
            );
            var subtitleFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 10, iTextSharp.text.BaseColor.Gray);
            doc.Add(new iTextSharp.text.Paragraph("Staff Report", titleFont) { Alignment = iTextSharp.text.Element.ALIGN_CENTER });
            doc.Add(new iTextSharp.text.Paragraph($"Generated on: {DateTime.Now:dd MMM yyyy HH:mm}", subtitleFont) { Alignment = iTextSharp.text.Element.ALIGN_CENTER });
            doc.Add(new iTextSharp.text.Paragraph("\n"));

            // Table
            var table = new iTextSharp.text.pdf.PdfPTable(9) { WidthPercentage = 100 };
            table.SetWidths(new float[] { 3f, 12f, 12f, 8f, 3f, 12f, 8f, 8f, 5f });

            var headerFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 10, iTextSharp.text.BaseColor.White);
            var headerBg = new iTextSharp.text.BaseColor(52, 73, 94);

            string[] headers = { "#", "Full Name", "Email", "Roles", "Age", "Address", "Phone", "ID Number", "Gender" };
            foreach (var h in headers)
            {
                var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(h, headerFont))
                {
                    BackgroundColor = headerBg,
                    HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER,
                    Padding = 5
                };
                table.AddCell(cell);
            }

            var rowFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 9);
            int count = 1;
            foreach (var s in staffList)
            {
                table.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(count.ToString(), rowFont)));
                table.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(s.FullName, rowFont)));
                table.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(s.Email, rowFont)));
                table.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(s.Roles, rowFont)));
                table.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(s.Age.ToString(), rowFont)));
                table.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(s.Address, rowFont)));
                table.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(s.PhoneNumber, rowFont)));
                table.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(s.IdNumber, rowFont)));
                table.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(s.Gender, rowFont)));

                count++;
            }

            doc.Add(table);

            // Footer
            doc.Add(new iTextSharp.text.Paragraph($"\nGenerated by Ward Management System © {DateTime.Now.Year}", iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_OBLIQUE, 8, iTextSharp.text.BaseColor.Gray)) { Alignment = iTextSharp.text.Element.ALIGN_CENTER });

            doc.Close();

            return File(stream.ToArray(), "application/pdf", "StaffReport.pdf");
        }



    }
}
