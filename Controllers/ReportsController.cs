using System.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

            // === CHART SECTION ===
            // Path to Base64 image or Chart file (Chart64 image)
            string chartBase64 = HttpContext.Request.Form["chartImage"];

            if (!string.IsNullOrEmpty(chartBase64))
            {
                try
                {
                    // Remove "data:image/png;base64," prefix if it exists
                    if (chartBase64.StartsWith("data:image"))
                        chartBase64 = chartBase64.Substring(chartBase64.IndexOf(",") + 1);

                    byte[] chartBytes = Convert.FromBase64String(chartBase64);
                    var chartImage = iTextSharp.text.Image.GetInstance(chartBytes);
                    chartImage.ScaleToFit(500f, 300f);
                    chartImage.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                    doc.Add(new iTextSharp.text.Paragraph("\nStaff Overview Chart\n", titleFont)
                    {
                        Alignment = iTextSharp.text.Element.ALIGN_CENTER
                    });
                    doc.Add(chartImage);
                    doc.Add(new iTextSharp.text.Paragraph("\n"));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Chart not added: {ex.Message}");
                }
            }

            doc.Add(new iTextSharp.text.Paragraph("Staff Report", titleFont) { Alignment = iTextSharp.text.Element.ALIGN_CENTER });
            doc.Add(new iTextSharp.text.Paragraph("\n"));
            doc.Add(table);

            // Footer
            doc.Add(new iTextSharp.text.Paragraph($"\nGenerated by Ward Management System © {DateTime.Now.Year}", iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_OBLIQUE, 8, iTextSharp.text.BaseColor.Gray)) { Alignment = iTextSharp.text.Element.ALIGN_CENTER });

            doc.Close();

            return File(stream.ToArray(), "application/pdf", "StaffReport.pdf");
        }

        [Authorize(Roles = "WardAdmin,Admin")]
        public async Task<IActionResult> ExportAppointmentsToPDF(DateTime? selectedDate = null, string? statusFilter = null, string? ageFilter = null, string? searchName = null)
        {
            var today = DateTime.Today;

            var query = _context.Appointments
                .Include(a => a.User)
                .Include(a => a.ConsultationRoom)
                .Where(a => a.Status != "Cancelled" && a.Status != "Admitted");

            // apply filters
            if (selectedDate.HasValue)
                query = query.Where(a => a.PreferredDate.Date == selectedDate.Value.Date);
            else
                query = query.Where(a => a.PreferredDate >= today);

            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(a => a.Status == statusFilter);

            if (!string.IsNullOrEmpty(ageFilter))
            {
                query = ageFilter switch
                {
                    "child" => query.Where(a => a.Age < 18),
                    "young" => query.Where(a => a.Age >= 18 && a.Age <= 29),
                    "middle" => query.Where(a => a.Age >= 30 && a.Age <= 49),
                    "senior" => query.Where(a => a.Age >= 50),
                    _ => query
                };
            }

            if (!string.IsNullOrEmpty(searchName))
                query = query.Where(a => a.FullName.Contains(searchName));

            var appointments = await query.OrderBy(a => a.PreferredDate).ThenBy(a => a.PreferredTime).ToListAsync();

            // Generate PDF
            using (var ms = new MemoryStream())
            {
                Document doc = new Document(PageSize.A4, 25, 25, 30, 30);
                PdfWriter.GetInstance(doc, ms);
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
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                var header = new Paragraph("Appointments Report", titleFont) { Alignment = Element.ALIGN_CENTER };
                doc.Add(header);
                doc.Add(new Paragraph($"Generated on: {DateTime.Now:dd/MM/yyyy HH:mm}\n\n"));

                // Table
                PdfPTable table = new PdfPTable(6) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 5, 25, 10, 15, 15, 15 });

                // Header row
                string[] headers = { "#", "Patient", "Age", "Date", "Time", "Status" };
                foreach (var h in headers)
                {
                    PdfPCell cell = new PdfPCell(new Phrase(h, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10)))
                    {
                        BackgroundColor = new BaseColor(211, 211, 211),
                        HorizontalAlignment = Element.ALIGN_CENTER
                    };
                    table.AddCell(cell);
                }

                // Data rows
                int count = 1;
                foreach (var a in appointments)
                {
                    table.AddCell(count.ToString());
                    table.AddCell(a.FullName);
                    table.AddCell(a.Age.ToString());
                    table.AddCell(a.PreferredDate.ToString("dd/MM/yyyy"));
                    table.AddCell(a.PreferredTime.ToString(@"hh\:mm"));
                    table.AddCell(a.Status);
                    count++;
                }

                doc.Add(table);
                // Footer
                doc.Add(new iTextSharp.text.Paragraph($"\nGenerated by Ward Management System © {DateTime.Now.Year}", iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_OBLIQUE, 8, iTextSharp.text.BaseColor.Gray)) { Alignment = iTextSharp.text.Element.ALIGN_CENTER });
                doc.Close();

                return File(ms.ToArray(), "application/pdf", "AppointmentsReport.pdf");
            }
        }

        [Authorize(Roles = "WardAdmin,Admin")]
        public async Task<IActionResult> ExportAdmissionsToPDF(string? locationFilter, string? statusFilter, string? searchTerm)
        {
            // Fetch admissions similar to ViewAdmissions logic
            var admittedPatients = await (from ad in _context.Admissions
                                          where ad.Status != "Discharged"
                                          join ap in _context.Appointments on ad.AppointmentId equals ap.AppointmentId
                                          join w in _context.wards on ad.WardId equals w.WardId
                                          select new AdmissionViewModel
                                          {
                                              AdmissionId = ad.AdmissionId,
                                              AppointmentId = ap.AppointmentId,
                                              PatientName = ap.FullName,
                                              IdNumber = ap.IdNumber,
                                              AdmissionDate = ad.AdmissionDate,
                                              FolderStatus = _context.PatientFolder
                                                                  .Include(pf => pf.Appointment)
                                                                  .Any(pf => pf.Appointment.FullName == ap.FullName && pf.Appointment.IdNumber == ap.IdNumber)
                                                                  ? "Has Folder" : "No Folder",
                                              Condition = ad.Condition,
                                              WardName = w.WardName,
                                              Status = ad.Status
                                          }).ToListAsync();

            var checkedInPatients = await (from a in _context.Appointments
                                           where a.Status == "CheckedIn" && !_context.Admissions.Any(ad => ad.AppointmentId == a.AppointmentId)
                                           join cr in _context.ConsultationRooms on a.ConsultationRoomId equals cr.RoomId
                                           select new AdmissionViewModel
                                           {
                                               AdmissionId = 0,
                                               AppointmentId = a.AppointmentId,
                                               PatientName = a.FullName,
                                               IdNumber = a.IdNumber,
                                               AdmissionDate = null,
                                               Condition = a.Reason,
                                               WardName = cr.RoomName,
                                               FolderStatus = _context.PatientFolder
                                                                .Include(pf => pf.Appointment)
                                                                .Any(pf => pf.Appointment.FullName == a.FullName && pf.Appointment.IdNumber == a.IdNumber)
                                                                ? "Has Folder" : "No Folder",
                                               Status = a.Status
                                           }).ToListAsync();

            var allPatients = admittedPatients.Union(checkedInPatients).ToList();

            // Apply filters
            if (!string.IsNullOrEmpty(locationFilter))
                allPatients = allPatients.Where(p => p.WardName == locationFilter).ToList();

            if (!string.IsNullOrEmpty(statusFilter))
                allPatients = allPatients.Where(p => p.FolderStatus == statusFilter).ToList();

            if (!string.IsNullOrEmpty(searchTerm))
                allPatients = allPatients.Where(p => p.PatientName.Contains(searchTerm) || p.IdNumber.Contains(searchTerm)).ToList();

            // Generate PDF
            using var ms = new MemoryStream();
            Document doc = new Document(PageSize.A4, 25, 25, 30, 30);
            PdfWriter.GetInstance(doc, ms);
            doc.Open();

            // Add logo
            string logoPath = Path.Combine(_env.WebRootPath, "images/logo.png");
            if (System.IO.File.Exists(logoPath))
            {
                iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(logoPath);
                logo.ScaleToFit(100f, 100f);
                logo.Alignment = Element.ALIGN_LEFT;
                doc.Add(logo);
            }

            // Add title
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
            Paragraph title = new Paragraph("Admissions Report", titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20f
            };
            doc.Add(title);

            // Create table
            PdfPTable table = new PdfPTable(6)
            {
                WidthPercentage = 100
            };
            table.SetWidths(new float[] { 1, 3, 2, 2, 2, 3 });

            // Table Header
            string[] headers = { "#", "Patient Name", "ID Number", "Location", "Folder Status", "Condition" };
            foreach (var header in headers)
            {
                var cell = new PdfPCell(new Phrase(header, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)))
                {
                    BackgroundColor = BaseColor.LightGray,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Padding = 5
                };
                table.AddCell(cell);
            }

            // Table rows
            int count = 1;
            foreach (var p in allPatients)
            {
                table.AddCell(new PdfPCell(new Phrase(count.ToString())) { Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase(p.PatientName)) { Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase(p.IdNumber)) { Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase(p.WardName)) { Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase(p.FolderStatus)) { Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase(p.Condition)) { Padding = 5 });
                count++;
            }

            doc.Add(table);
            doc.Add(new iTextSharp.text.Paragraph($"\nGenerated by Ward Management System © {DateTime.Now.Year}", iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_OBLIQUE, 8, iTextSharp.text.BaseColor.Gray)) { Alignment = iTextSharp.text.Element.ALIGN_CENTER });
            doc.Close();

            return File(ms.ToArray(), "application/pdf", "AdmissionsReport.pdf");
        }

        [Authorize(Roles = "WardAdmin,Admin")]
        public IActionResult ExportPatientMovementsToPDF(string? PatientName, int? WardId, DateTime? FromDate, DateTime? ToDate)
        {
            var query = _context.PatientMovements
                .Include(m => m.Admission)
                    .ThenInclude(a => a.Appointment)
                .Include(m => m.Ward)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(PatientName))
                query = query.Where(m => m.Admission.PatientName.Contains(PatientName));

            if (WardId.HasValue)
                query = query.Where(m => m.WardId == WardId.Value);

            if (FromDate.HasValue)
                query = query.Where(m => m.MovementTime >= FromDate.Value);

            if (ToDate.HasValue)
                query = query.Where(m => m.MovementTime <= ToDate.Value);

            var movements = query
                .OrderByDescending(m => m.MovementTime)
                .Select(m => new
                {
                    m.Admission.PatientName,
                    m.Admission.Appointment.IdNumber,
                    WardName = m.Ward.WardName,
                    m.MovementTime,
                    m.Notes
                })
                .ToList();

            // PDF generation
            using var ms = new MemoryStream();
            Document doc = new Document(PageSize.A4, 25, 25, 30, 30);
            PdfWriter.GetInstance(doc, ms);
            doc.Open();

            // Add logo
            string logoPath = Path.Combine(_env.WebRootPath, "images", "logo.png");
            if (System.IO.File.Exists(logoPath))
            {
                iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(logoPath);
                logo.ScaleToFit(100f, 100f);
                logo.Alignment = Element.ALIGN_LEFT;
                doc.Add(logo);
            }

            // Add title
            iTextSharp.text.Font titleFont = FontFactory.GetFont("Helvetica", 16f, iTextSharp.text.Font.BOLD);
            Paragraph title = new Paragraph("Patient Movement History", titleFont);
            title.Alignment = Element.ALIGN_CENTER;
            title.SpacingAfter = 20f;
            doc.Add(title);

            // Create table
            PdfPTable table = new PdfPTable(5);
            table.WidthPercentage = 100;
            table.SetWidths(new float[] { 5f, 20f, 15f, 15f, 25f });

            // Table header
            string[] headers = { "#", "Patient Name", "ID Number", "Ward", "Movement Time / Notes" };
            foreach (var h in headers)
            {
                PdfPCell cell = new PdfPCell(new Phrase(h))
                {
                    BackgroundColor = BaseColor.LightGray,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Padding = 5
                };
                table.AddCell(cell);
            }

            int count = 1;
            foreach (var m in movements)
            {
                table.AddCell(new PdfPCell(new Phrase(count.ToString())) { Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase(m.PatientName)) { Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase(m.IdNumber)) { Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase(m.WardName)) { Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase($"{m.MovementTime:dd MMM yyyy HH:mm}\n{m.Notes}")) { Padding = 5 });
                count++;
            }

            doc.Add(table);
            doc.Add(new iTextSharp.text.Paragraph($"\nGenerated by Ward Management System © {DateTime.Now.Year}", iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_OBLIQUE, 8, iTextSharp.text.BaseColor.Gray)) { Alignment = iTextSharp.text.Element.ALIGN_CENTER });
            doc.Close();

            return File(ms.ToArray(), "application/pdf", "PatientMovementHistory.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> ExportDoctorsPatientsToPDF(string locationFilter, string ageFilter, string searchTerm)
        {
            var user = await _userManager.GetUserAsync(User);
            var doctorId = _userManager.GetUserId(User);
            var doctorRoles = await _userManager.GetRolesAsync(user);
            bool isAdmin = doctorRoles.Contains("Admin");

            // ===== Get Patients (same logic as in PatientList) =====
            var admittedPatientsQuery = from ad in _context.Admissions
                                        join ap in _context.Appointments
                                            on ad.AppointmentId equals ap.AppointmentId
                                        join w in _context.wards
                                            on ad.WardId equals w.WardId
                                        where ad.Status == "Admitted"
                                        select new
                                        {
                                            ap.FullName,
                                            ap.IdNumber,
                                            ap.Age,
                                            Condition = ad.Condition,
                                            WardName = w.WardName,
                                            Status = ad.Status,
                                            FolderStatus = _context.PatientFolder
                                                .Any(pf => pf.Appointment.FullName == ap.FullName &&
                                                           pf.Appointment.IdNumber == ap.IdNumber)
                                                           ? "Has Folder" : "No Folder"
                                        };

            var checkedInPatientsQuery = from a in _context.Appointments
                                         where a.Status == "CheckedIn" &&
                                               !_context.Admissions.Any(ad => ad.AppointmentId == a.AppointmentId)
                                         join cr in _context.ConsultationRooms on a.ConsultationRoomId equals cr.RoomId
                                         select new
                                         {
                                             a.FullName,
                                             a.IdNumber,
                                             a.Age,
                                             Condition = a.Reason,
                                             WardName = cr.RoomName,
                                             Status = a.Status,
                                             FolderStatus = _context.PatientFolder
                                                 .Any(pf => pf.Appointment.FullName == a.FullName &&
                                                            pf.Appointment.IdNumber == a.IdNumber)
                                                            ? "Has Folder" : "No Folder"
                                         };

            if (!isAdmin)
            {
                admittedPatientsQuery = admittedPatientsQuery.Where(p =>
                    _context.Appointments.Any(a => a.FullName == p.FullName && a.IdNumber == p.IdNumber && a.DoctorId == doctorId));

                checkedInPatientsQuery = checkedInPatientsQuery.Where(p =>
                    _context.Appointments.Any(a => a.FullName == p.FullName && a.IdNumber == p.IdNumber && a.DoctorId == doctorId));
            }

            var allPatients = await admittedPatientsQuery.Union(checkedInPatientsQuery).ToListAsync();

            // ===== Apply filters =====
            if (!string.IsNullOrEmpty(locationFilter))
                allPatients = allPatients.Where(p => p.WardName == locationFilter).ToList();

            if (!string.IsNullOrEmpty(ageFilter))
            {
                allPatients = ageFilter switch
                {
                    "child" => allPatients.Where(p => p.Age < 18).ToList(),
                    "young" => allPatients.Where(p => p.Age >= 18 && p.Age <= 29).ToList(),
                    "middle" => allPatients.Where(p => p.Age >= 30 && p.Age <= 49).ToList(),
                    "senior" => allPatients.Where(p => p.Age >= 50).ToList(),
                    _ => allPatients
                };
            }

            if (!string.IsNullOrEmpty(searchTerm))
                allPatients = allPatients.Where(p =>
                    p.FullName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    p.IdNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

            // ===== Create PDF =====
            using (MemoryStream stream = new MemoryStream())
            {
                Document doc = new Document(PageSize.A4, 40, 40, 60, 40);
                PdfWriter writer = PdfWriter.GetInstance(doc, stream);
                doc.Open();

                // 🔹 Add Logo
                string logoPath = Path.Combine(_env.WebRootPath, "images", "logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    var logo = iTextSharp.text.Image.GetInstance(logoPath);
                    logo.ScaleAbsolute(60, 60);
                    logo.Alignment = Element.ALIGN_LEFT;
                    doc.Add(logo);
                }

                // 🔹 Add Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                Paragraph title = new Paragraph("Patient Report", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                title.SpacingAfter = 20f;
                doc.Add(title);

                // 🔹 Add Report Info
                var infoFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                doc.Add(new Paragraph($"Doctor: {user?.FullName}", infoFont));
                doc.Add(new Paragraph($"Generated: {DateTime.Now:dd MMM yyyy HH:mm}", infoFont));

                if (!string.IsNullOrEmpty(locationFilter) || !string.IsNullOrEmpty(ageFilter) || !string.IsNullOrEmpty(searchTerm))
                {
                    doc.Add(new Paragraph("Applied Filters:", infoFont));
                    if (!string.IsNullOrEmpty(locationFilter)) doc.Add(new Paragraph($" - Location: {locationFilter}", infoFont));
                    if (!string.IsNullOrEmpty(ageFilter)) doc.Add(new Paragraph($" - Age Group: {ageFilter}", infoFont));
                    if (!string.IsNullOrEmpty(searchTerm)) doc.Add(new Paragraph($" - Search Term: {searchTerm}", infoFont));
                }

                doc.Add(new Paragraph("\n")); // spacing

                // 🔹 Add Table
                PdfPTable table = new PdfPTable(6) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 2.5f, 2f, 1f, 2f, 2f, 2f });

                // Header row
                string[] headers = { "Patient Name", "ID Number", "Age", "Condition", "Ward/Room", "Folder Status" };
                foreach (var header in headers)
                {
                    PdfPCell cell = new PdfPCell(new Phrase(header, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10)));
                    cell.BackgroundColor = new BaseColor(211, 211, 211);
                    cell.HorizontalAlignment = Element.ALIGN_CENTER;
                    table.AddCell(cell);
                }

                // Data rows
                foreach (var p in allPatients)
                {
                    table.AddCell(new Phrase(p.FullName));
                    table.AddCell(new Phrase(p.IdNumber));
                    table.AddCell(new Phrase(p.Age.ToString()));
                    table.AddCell(new Phrase(p.Condition));
                    table.AddCell(new Phrase(p.WardName));
                    table.AddCell(new Phrase(p.FolderStatus));
                }

                doc.Add(table);

                // 🔹 Summary
                doc.Add(new Paragraph("\nSummary:", titleFont));
                doc.Add(new Paragraph($"Total Patients: {allPatients.Count}", infoFont));

                var groupedByWard = allPatients.GroupBy(p => p.WardName);
                foreach (var group in groupedByWard)
                {
                    doc.Add(new Paragraph($" - {group.Key}: {group.Count()} patients", infoFont));
                }

                int foldersCount = allPatients.Count(p => p.FolderStatus == "Has Folder");
                doc.Add(new Paragraph($"Patients with Folders: {foldersCount}", infoFont));
                doc.Add(new Paragraph($"Patients without Folders: {allPatients.Count - foldersCount}", infoFont));

                doc.Close();

                return File(stream.ToArray(), "application/pdf", "PatientReport.pdf");
            }
        }

        // 1. Export ALL pending prescriptions by logged-in doctor
        public async Task<IActionResult> ExportPendingPrescriptionsPDF()
        {
            var doctorId = _userManager.GetUserId(User);

            var prescriptions = await _context.PrescribedMedications
                .Include(pm => pm.Prescription)
                    .ThenInclude(p => p.Appointment)
                .Include(pm => pm.StockMedications)
                .Where(pm => pm.Prescription.PrescribedById == doctorId && !pm.IsDispensed)
                .ToListAsync();

            return GeneratePrescriptionPdf(prescriptions, "Pending Prescriptions");
        }
        // 2. Export PENDING prescriptions for a SINGLE patient
        public async Task<IActionResult> ExportPatientPrescriptionPDF(string patientIdNumber)
        {
            var doctorId = _userManager.GetUserId(User);

            var prescriptions = await _context.PrescribedMedications
                .Include(pm => pm.Prescription)
                    .ThenInclude(p => p.Appointment)
                .Include(pm => pm.StockMedications)
                .Where(pm => pm.Prescription.PrescribedById == doctorId &&
                             pm.Prescription.Appointment.IdNumber == patientIdNumber &&
                             !_context.DispensedMedications
                                 .Any(dm => dm.PrescribedMedicationId == pm.Id))
                .ToListAsync();

            var patientFullName = prescriptions.FirstOrDefault()?.Prescription.Appointment.FullName ?? patientIdNumber;

            return GeneratePrescriptionPdf(
                prescriptions,
                $"Pending Prescriptions for {patientFullName}",
                isSinglePatient: true
            );
        }



        // 3. Export ALL administered prescriptions (history for inventory)
        public async Task<IActionResult> ExportAdministeredPrescriptionsPDF()
        {
            var doctorId = _userManager.GetUserId(User);

            var prescriptions = await _context.PrescribedMedications
                .Include(pm => pm.Prescription)
                    .ThenInclude(p => p.Appointment)
                .Include(pm => pm.StockMedications)
                .Where(pm => pm.Prescription.PrescribedById == doctorId && pm.IsDispensed)
                .ToListAsync();

            return GeneratePrescriptionPdf(prescriptions, "Administered Prescriptions (History)");
        }
        // Shared PDF generator for prescriptions
        private FileResult GeneratePrescriptionPdf(List<PrescribedMedication> prescriptions, string titleText, bool isSinglePatient = false)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Document doc = new Document(PageSize.A4, 40f, 40f, 60f, 60f);
                PdfWriter.GetInstance(doc, ms);
                doc.Open();

                // Logo
                string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(logoPath);
                    logo.ScaleAbsolute(50f, 50f);
                    logo.Alignment = Element.ALIGN_LEFT;
                    logo.SpacingAfter = 15f;
                    doc.Add(logo);
                }

                // Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.Black);
                Paragraph title = new Paragraph(titleText, titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                title.SpacingAfter = 20f;
                doc.Add(title);

                if (!prescriptions.Any())
                {
                    var prescriptionFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);
                    doc.Add(new Paragraph("No prescriptions found.", prescriptionFont));
                    doc.Close();
                    return File(ms.ToArray(), "application/pdf", $"{titleText.Replace(" ", "_")}.pdf");
                }

                // Table setup
                PdfPTable table;
                if (isSinglePatient)
                {
                    // Single patient: hide patient column, replace ID Number with patient name
                    table = new PdfPTable(4);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 2f, 2f, 2f, 2f });

                    string[] headers = { "Medication", "Dosage", "Frequency", "Status" };
                    foreach (var header in headers)
                    {
                        var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.White);
                        PdfPCell cell = new PdfPCell(new Phrase(header, headerFont))
                        {
                            BackgroundColor = new BaseColor(0, 102, 204),
                            HorizontalAlignment = Element.ALIGN_CENTER
                        };
                        table.AddCell(cell);
                    }

                    foreach (var p in prescriptions)
                    {
                        table.AddCell(p.StockMedications.Name);
                        table.AddCell(p.Dosage);
                        table.AddCell(p.Frequency);
                        table.AddCell(p.IsDispensed ? "Administered" : "Pending");
                    }
                }
                else
                {
                    // Default behavior (all other exports)
                    table = new PdfPTable(6);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 2f, 2f, 2f, 2f, 2f, 2f });

                    string[] headers = { "Patient", "ID Number", "Medication", "Dosage", "Frequency", "Status" };
                    foreach (var header in headers)
                    {
                        var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.White);
                        PdfPCell cell = new PdfPCell(new Phrase(header, headerFont))
                        {
                            BackgroundColor = new BaseColor(0, 102, 204),
                            HorizontalAlignment = Element.ALIGN_CENTER
                        };
                        table.AddCell(cell);
                    }

                    foreach (var p in prescriptions)
                    {
                        table.AddCell(p.Prescription.Appointment.FullName);
                        table.AddCell(p.Prescription.Appointment.IdNumber);
                        table.AddCell(p.StockMedications.Name);
                        table.AddCell(p.Dosage);
                        table.AddCell(p.Frequency);
                        table.AddCell(p.IsDispensed ? "Administered" : "Pending");
                    }
                }

                doc.Add(table);

                // ✅ Add signature line ONLY for single patient (Pending prescriptions)
                if (isSinglePatient)
                {
                    doc.Add(new Paragraph("\n\n\n")); // spacing before the signature area
                    var signatureFont = FontFactory.GetFont(FontFactory.HELVETICA, 12, BaseColor.Black);
                    var dottedLine = new Paragraph("....................................................", signatureFont)
                    {
                        Alignment = Element.ALIGN_LEFT
                    };
                    doc.Add(dottedLine);

                    var label = new Paragraph("Doctor's Signature", FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 10, BaseColor.Gray))
                    {
                        Alignment = Element.ALIGN_LEFT
                    };
                    doc.Add(label);
                }

                // ✅ Add signature line for Pending and Completed (Administered) prescriptions
                bool shouldAddSignature = titleText.Contains("Pending") || titleText.Contains("Administered");

                // Footer
                doc.Add(new iTextSharp.text.Paragraph($"\nGenerated by Ward Management System © 2025",
                    iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_OBLIQUE, 10, iTextSharp.text.Font.ITALIC, iTextSharp.text.BaseColor.Gray))
                { Alignment = iTextSharp.text.Element.ALIGN_CENTER });

                doc.Close();
                return File(ms.ToArray(), "application/pdf", $"{titleText.Replace(" ", "_")}.pdf");
            }
        }


        [HttpPost]
        public async Task<IActionResult> ExportStatsPDF(string chartBase64)
        {
            var doctorId = _userManager.GetUserId(User);

            // 1️ Gather statistics
            var threeDaysAgo = DateTime.Today.AddDays(-30);

            // 1️⃣ Gather statistics for the last 30 days
            var prescriptions = await _context.Prescriptions
                .Include(p => p.Medications)  // load medications
                .Where(p => p.PrescribedById == doctorId && p.PrescribedDate.Date >= threeDaysAgo)
                .ToListAsync();

            // 2️⃣ Total prescriptions
            var totalPrescriptions = prescriptions.Count;

            // 3️⃣ Average items per prescription
            var avgItemsPerPrescription = prescriptions.Any()
                ? prescriptions.Average(p => p.Medications.Count)
                : 0;

            // 2️ Medication counts (last 30 days)
            var thirtyDaysAgo = DateTime.Today.AddDays(-30);
            var medCounts = await _context.PrescribedMedications
                .Include(pm => pm.StockMedications)
                .Include(pm => pm.Prescription)
                .Where(pm => pm.Prescription.PrescribedById == doctorId && pm.Prescription.PrescribedDate >= thirtyDaysAgo)
                .GroupBy(pm => pm.StockMedications.Name) // group only by medication name
                .Select(g => new
                {
                    MedicationName = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(g => g.Count)
                .ToListAsync();

            // 3️ Generate PDF
            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4, 40f, 40f, 60f, 60f);
                PdfWriter.GetInstance(doc, ms);
                doc.Open();

                // Logo
                var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    var logo = iTextSharp.text.Image.GetInstance(logoPath);
                    logo.ScaleToFit(60f, 60f);
                    logo.Alignment = Element.ALIGN_LEFT;
                    logo.SpacingAfter = 15f;

                    doc.Add(logo);
                }

                // Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                doc.Add(new Paragraph("Prescription Dashboard Stats", titleFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 20f });

                // Stats summary
                var statsFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);
                doc.Add(new Paragraph($"Total Prescriptions: {totalPrescriptions}", statsFont));
                doc.Add(new Paragraph($"Average Items per Prescription: {avgItemsPerPrescription:F2}", statsFont));
                doc.Add(new Paragraph(" ", statsFont));

                // Chart
                if (!string.IsNullOrEmpty(chartBase64))
                {
                    var bytes = Convert.FromBase64String(chartBase64.Split(',')[1]);
                    var chartImg = iTextSharp.text.Image.GetInstance(bytes);
                    chartImg.Alignment = Element.ALIGN_CENTER;
                    chartImg.ScaleToFit(500f, 250f);
                    chartImg.SpacingAfter = 20f;
                    doc.Add(chartImg);
                }

                // Medication table
                if (medCounts.Any())
                {
                    var table = new PdfPTable(2) { WidthPercentage = 100 };
                    table.SetWidths(new float[] { 4f, 2f });

                    string[] headers = { "Medication", "Total Prescribed" };
                    foreach (var header in headers)
                    {
                        var cell = new PdfPCell(new Phrase(header, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)))
                        {
                            BackgroundColor = new BaseColor(0, 102, 204),
                            HorizontalAlignment = Element.ALIGN_CENTER
                        };
                        table.AddCell(cell);
                    }

                    foreach (var row in medCounts)
                    {
                        table.AddCell(row.MedicationName);
                        table.AddCell(row.Count.ToString());
                    }

                    doc.Add(table);
                }
                else
                {
                    doc.Add(new Paragraph("No prescriptions in the last 30 days.", statsFont));
                }

                doc.Close();
                return File(ms.ToArray(), "application/pdf", "Prescription_Stats.pdf");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportPatientStatsPDF(string chartBase64)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByIdAsync(userId);
            var userRoles = await _userManager.GetRolesAsync(user);

            bool isAdmin = userRoles.Contains("Admin");
            bool isDoctorRole = userRoles.Contains("Doctor");

            // Get all relevant patients
            var admittedPatientsQuery = from ad in _context.Admissions
                                        join ap in _context.Appointments on ad.AppointmentId equals ap.AppointmentId
                                        join w in _context.wards on ad.WardId equals w.WardId
                                        where ad.Status == "Admitted"
                                        select new
                                        {
                                            ap.AppointmentId,
                                            ap.FullName,
                                            ap.IdNumber,
                                            AdmissionDate = (DateTime?)ad.AdmissionDate,
                                            ad.Status,
                                            Age = ap.Age,
                                            WardName = w.WardName
                                        };

            var checkedInPatientsQuery = from a in _context.Appointments
                                         where a.Status == "CheckedIn" && !_context.Admissions.Any(ad => ad.AppointmentId == a.AppointmentId)
                                         join cr in _context.ConsultationRooms on a.ConsultationRoomId equals cr.RoomId
                                         select new
                                         {
                                             a.AppointmentId,
                                             a.FullName,
                                             a.IdNumber,
                                             AdmissionDate = (DateTime?)null,
                                             a.Status,
                                             Age = a.Age,
                                             WardName = cr.RoomName
                                         };

            if (!isAdmin && isDoctorRole)
            {
                admittedPatientsQuery = admittedPatientsQuery
                    .Where(x => _context.Appointments.Any(a => a.AppointmentId == x.AppointmentId && a.DoctorId == userId));

                checkedInPatientsQuery = checkedInPatientsQuery
                    .Where(x => _context.Appointments.Any(a => a.AppointmentId == x.AppointmentId && a.DoctorId == userId));
            }

            var allPatients = admittedPatientsQuery.Union(checkedInPatientsQuery).ToList();

            int totalPatients = allPatients.Count;
            int admittedCount = allPatients.Count(p => p.Status == "Admitted");
            int checkedInCount = allPatients.Count(p => p.Status == "CheckedIn");
            int young = allPatients.Count(u => u.Age < 30);
            int middle = allPatients.Count(u => u.Age >= 30 && u.Age < 50);
            int senior = allPatients.Count(u => u.Age >= 50);

            // Recent Admissions
            var latest3 = allPatients
                .Where(p => p.Status == "Admitted" || p.Status == "CheckedIn")
                .OrderByDescending(p => p.AdmissionDate ?? DateTime.MinValue)
                .Take(3)
                .ToList();

            // Generate PDF
            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4, 40f, 40f, 60f, 60f);
                PdfWriter.GetInstance(doc, ms);
                doc.Open();

                // Logo
                string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(logoPath);
                    logo.ScaleAbsolute(50f, 50f);
                    logo.Alignment = Element.ALIGN_LEFT;
                    doc.Add(logo);
                }

                // Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.Black);
                var title = new Paragraph("Patient Statistics", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20f
                };
                doc.Add(title);

                // Stats summary
                var statsFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);
                doc.Add(new Paragraph($"Total Patients: {totalPatients}", statsFont));
                doc.Add(new Paragraph($"Admitted: {admittedCount} | Checked-In: {checkedInCount}", statsFont));
                doc.Add(new Paragraph(" ", statsFont));

                // Include Chart if provided
                if (!string.IsNullOrEmpty(chartBase64))
                {
                    var chartBytes = Convert.FromBase64String(chartBase64.Split(',')[1]);
                    var chartImg = iTextSharp.text.Image.GetInstance(chartBytes);
                    chartImg.Alignment = Element.ALIGN_CENTER;
                    chartImg.ScaleToFit(500f, 250f);
                    chartImg.SpacingAfter = 20f;
                    doc.Add(chartImg);
                }

                // Recent Admissions table
                if (latest3.Any())
                {
                    var table = new PdfPTable(3) { WidthPercentage = 100 };
                    table.SetWidths(new float[] { 4f, 2f, 3f });
                    string[] headers = { "Patient Name", "Admission Date", "Location" };
                    foreach (var header in headers)
                    {
                        var cell = new PdfPCell(new Phrase(header, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)))
                        {
                            BackgroundColor = new BaseColor(0, 102, 204),
                            HorizontalAlignment = Element.ALIGN_CENTER
                        };
                        table.AddCell(cell);
                    }

                    foreach (var p in latest3)
                    {
                        table.AddCell(p.FullName);
                        table.AddCell(p.AdmissionDate?.ToString("dd MMM yyyy") ?? "-");
                        table.AddCell(p.WardName);
                    }

                    doc.Add(table);
                }
                else
                {
                    doc.Add(new Paragraph("No recent admissions.", statsFont));
                }

                // Footer
                doc.Add(new Paragraph($"\nGenerated by Ward Management System © 2025", FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 10, BaseColor.Gray))
                { Alignment = Element.ALIGN_CENTER });

                doc.Close();
                return File(ms.ToArray(), "application/pdf", "Patient_Statistics.pdf");
            }
        }

        [HttpPost]
        public IActionResult ExportAppointmentsToPDF(string filteredData)
        {
            var appointments = new List<Dictionary<string, object>>();

            if (!string.IsNullOrEmpty(filteredData))
            {
                appointments = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(filteredData);
            }

            using (var ms = new MemoryStream())
            {
                var doc = new iTextSharp.text.Document(PageSize.A4, 40f, 40f, 60f, 60f);
                PdfWriter.GetInstance(doc, ms);
                doc.Open();

                // ✅ Add logo
                string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    var logo = iTextSharp.text.Image.GetInstance(logoPath);
                    logo.ScaleAbsolute(50f, 50f);
                    logo.Alignment = Element.ALIGN_LEFT;
                    doc.Add(logo);
                }

                // ✅ Title styling
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.Black);
                var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 12, BaseColor.Gray);
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 11);

                var title = new Paragraph("Appointments Report", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 10f
                };
                doc.Add(title);

                var date = new Paragraph($"Generated on {DateTime.Now:dd MMM yyyy HH:mm}", subtitleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20f
                };
                doc.Add(date);

                // ✅ Table content
                if (appointments.Any())
                {
                    var table = new PdfPTable(8) { WidthPercentage = 100 };
                    table.SetWidths(new float[] { 3f, 1.5f, 1.5f, 2f, 2f, 1.5f, 3f, 1.5f });

                    string[] headers = { "Patient Name", "Age", "Gender", "ID Number", "Date", "Time", "Reason", "Status" };
                    foreach (var header in headers)
                    {
                        var cell = new PdfPCell(new Phrase(header, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)))
                        {
                            BackgroundColor = new BaseColor(0, 102, 204),
                            HorizontalAlignment = Element.ALIGN_CENTER,
                            Padding = 5
                        };
                        table.AddCell(cell);
                    }

                    foreach (var appt in appointments)
                    {
                        table.AddCell(new Phrase(appt["FullName"]?.ToString() ?? "-", normalFont));
                        table.AddCell(new Phrase(appt["Age"]?.ToString() ?? "-", normalFont));
                        table.AddCell(new Phrase(appt["Gender"]?.ToString() ?? "-", normalFont));
                        table.AddCell(new Phrase(appt["IdNumber"]?.ToString() ?? "-", normalFont));
                        table.AddCell(new Phrase(appt["Date"]?.ToString() ?? "-", normalFont));
                        table.AddCell(new Phrase(appt["Time"]?.ToString() ?? "-", normalFont));
                        table.AddCell(new Phrase(appt["Reason"]?.ToString() ?? "-", normalFont));
                        table.AddCell(new Phrase(appt["Status"]?.ToString() ?? "-", normalFont));
                    }

                    doc.Add(table);
                }
                else
                {
                    doc.Add(new Paragraph("No appointments found.", normalFont));
                }

                // ✅ Footer
                var footer = new Paragraph("\nGenerated by Ward Management System © 2025", FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 10, BaseColor.Gray))
                {
                    Alignment = Element.ALIGN_CENTER
                };
                doc.Add(footer);

                doc.Close();
                return File(ms.ToArray(), "application/pdf", "Appointments_Report.pdf");
            }
        }




    }
}
