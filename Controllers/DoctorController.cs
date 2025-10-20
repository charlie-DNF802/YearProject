using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Ward_Management_System.Data;
using Ward_Management_System.DTOs;
using Ward_Management_System.Models;
using Ward_Management_System.ViewModels;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Ward_Management_System.Controllers
{
    [Authorize(Roles = "Doctor,Admin")]
    public class DoctorController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        public DoctorController(AppDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        // GET: patient records/folder
        public async Task<IActionResult> PatientList(string locationFilter, string ageFilter, string searchTerm, int pg = 1)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByIdAsync(userId);
            var userRoles = await _userManager.GetRolesAsync(user);

            bool isAdmin = userRoles.Contains("Admin");
            bool isDoctorRole = userRoles.Contains("Doctor");

            var admittedPatientsQuery = from ad in _context.Admissions
                                        join ap in _context.Appointments
                                            on ad.AppointmentId equals ap.AppointmentId
                                        join w in _context.wards
                                            on ad.WardId equals w.WardId
                                        where ad.Status == "Admitted" || ad.Status == "Ready To Be Discharged"
                                        select new AdmissionViewModel
                                        {
                                            AdmissionId = ad.AdmissionId,
                                            AppointmentId = ap.AppointmentId,
                                            PatientName = ap.FullName,
                                            IdNumber = ap.IdNumber,
                                            AdmissionDate = ad.AdmissionDate,
                                            FolderStatus = _context.PatientFolder
                                                .Any(pf => pf.Appointment.FullName == ap.FullName &&
                                                           pf.Appointment.IdNumber == ap.IdNumber)
                                                           ? "Has Folder" : "No Folder",
                                            Condition = ad.Condition,
                                            WardName = w.WardName,
                                            Status = ad.Status,
                                            Age = ap.Age
                                        };

            var checkedInPatientsQuery = from a in _context.Appointments
                                         where a.Status == "CheckedIn"
                                               && !_context.Admissions.Any(ad => ad.AppointmentId == a.AppointmentId)
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
                                                 .Any(pf => pf.Appointment.FullName == a.FullName &&
                                                            pf.Appointment.IdNumber == a.IdNumber)
                                                            ? "Has Folder" : "No Folder",
                                             Status = a.Status,
                                             Age = a.Age
                                         };

            // Doctor filter (non-admin doctors only see their patients)
            if (!isAdmin && isDoctorRole)
            {
                admittedPatientsQuery = admittedPatientsQuery.Where(p => _context.Appointments
                    .Any(a => a.AppointmentId == p.AppointmentId && a.DoctorId == userId));

                checkedInPatientsQuery = checkedInPatientsQuery.Where(p => _context.Appointments
                    .Any(a => a.AppointmentId == p.AppointmentId && a.DoctorId == userId));
            }

            var allPatients = await admittedPatientsQuery.Union(checkedInPatientsQuery).ToListAsync();

            // Apply Filters
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
                    p.PatientName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    p.IdNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

            // Pass current filters back to the view
            ViewBag.CurrentFilters = new
            {
                locationFilter,
                ageFilter,
                searchTerm
            };
            // Provide list of wards for dropdown
            ViewBag.WardList = _context.wards.ToList();
            ViewBag.ConsultationRooms = _context.ConsultationRooms.ToList();

            var statsSource = allPatients;
            int totalPatients = statsSource.Count;
            int admittedCount = statsSource.Count(p => p.Status == "Admitted");
            int checkedInCount = statsSource.Count(p => p.Status == "CheckedIn");
            int young = statsSource.Count(u => u.Age < 30);
            int middle = statsSource.Count(u => u.Age >= 30 && u.Age < 50);
            int senior = statsSource.Count(u => u.Age >= 50);

            ViewBag.TotalPatients = totalPatients;
            ViewBag.AdmittedCount = admittedCount;
            ViewBag.CheckedInCount = checkedInCount;
            ViewBag.YoungPercent = totalPatients > 0 ? young * 100 / totalPatients : 0;
            ViewBag.MiddlePercent = totalPatients > 0 ? middle * 100 / totalPatients : 0;
            ViewBag.SeniorPercent = totalPatients > 0 ? senior * 100 / totalPatients : 0;

            // Chart data: frequency of admissions
            var startDate = DateTime.Today.AddDays(-29);
            var admissionsForChartQuery = from ad in _context.Admissions
                                          join ap in _context.Appointments on ad.AppointmentId equals ap.AppointmentId
                                          where ad.AdmissionDate >= startDate
                                          select new { ad.AdmissionId, ad.AdmissionDate, ap.DoctorId, ap.FullName };


            if (!isAdmin && isDoctorRole)
            {
                admissionsForChartQuery = admissionsForChartQuery.Where(x => x.DoctorId == userId);
            }

            var admissionsForChartList = admissionsForChartQuery.ToList();

            var days = Enumerable.Range(0, 30)
                .Select(i => DateTime.Today.AddDays(-29 + i))
                .ToList();

            var labels = days
                .Select(d => d.ToString("dd MMM"))
                .ToArray();

            var counts = days
                .Select(d => admissionsForChartList.Count(ad =>
                    ad.AdmissionDate.Date == d.Date))
                .ToArray();

            ViewBag.AdmissionChartLabels = labels;
            ViewBag.AdmissionChartData = counts;

            var latest3 = await (from ad in _context.Admissions
                                 join ap in _context.Appointments on ad.AppointmentId equals ap.AppointmentId
                                 join w in _context.wards on ad.WardId equals w.WardId
                                 where ad.Status == "Admitted"
                                 && (isAdmin || (isDoctorRole && ap.DoctorId == userId))
                                 orderby ad.AdmissionDate descending
                                 select new
                                 {
                                     ap.FullName,
                                     AdmissionDate = ad.AdmissionDate, // keep DateTime for now
                                     w.WardName,
                                     ap.IdNumber
                                 })
                     .Take(3)
                     .ToListAsync();

            ViewBag.LatestAdmissions = latest3
                                        .Select(x => new {
                                            x.FullName,
                                            AdmissionDate = x.AdmissionDate.ToString("dd MMM yyyy"),
                                            x.WardName,
                                            x.IdNumber
                                        })
                                        .ToList();

            // Paging 
            const int pageSize = 5;
            if (pg < 1) pg = 1;

            int recsCount = allPatients.Count();
            var pager = new Pager(recsCount, pg, pageSize);
            int recSkip = (pg - 1) * pageSize;

            var data = allPatients.Skip(recSkip).Take(pageSize).ToList();
            ViewBag.Pager = pager;

            // Expose chart JSON safely
            ViewBag.AdmissionChartLabelsJson = System.Text.Json.JsonSerializer.Serialize(labels);
            ViewBag.AdmissionChartDataJson = System.Text.Json.JsonSerializer.Serialize(counts);
            ViewBag.LatestAdmissionsJson = System.Text.Json.JsonSerializer.Serialize(latest3);

            return View(data);
        }


        //Get: Patient vitals
        public async Task<IActionResult> ViewVitals(int folderId, bool latestOnly = false)
        {
            var query = _context.PatientVitals
                .Where(v => v.FolderId == folderId)
                .OrderByDescending(v => v.VitalsDate);

            var vitals = latestOnly
                ? await query.Take(1).ToListAsync()
                : await query.ToListAsync();

            var patient = await _context.PatientFolder
                .Include(p => p.Appointment)
                .FirstOrDefaultAsync(p => p.FolderId == folderId);

            ViewBag.PatientName = patient?.Appointment?.FullName ?? "Unknown";
            ViewBag.FolderId = folderId;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_VitalsPartial", vitals);
            }

            return View(vitals);
        }

        public async Task<IActionResult> TreatPatients()
        {

            var patients = await _context.Appointments
                                          .Where(a => a.Status == "Admitted" || a.Status == "CheckedIn")
                                          .Select(a => new SelectListItem
                                          {
                                              Value = a.AppointmentId.ToString(),
                                              Text = a.FullName
                                          }).ToListAsync();

            ViewBag.PatientList = patients;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TreatPatients(Treatment treatment)
        {
            treatment.TreatmentDate = DateTime.Now;
            treatment.RecordedById = _userManager.GetUserId(User);

            if (ModelState.IsValid)
            {
                _context.Add(treatment);
                await _context.SaveChangesAsync();
                TempData["ToastMessage"] = "Treatment Successfully recorded.";
                TempData["ToastType"] = "success";
                return RedirectToAction("TreatPatients");
            }
            else
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                      .Select(e => e.ErrorMessage)
                                      .ToList();

                TempData["ToastMessage"] = "Failed to save: " + string.Join("; ", errors);
                TempData["ToastType"] = "danger";
            }
            // Repopulate dropdown if model is invalid
            ViewBag.PatientList = await _context.Appointments
                                                .Where(a => a.Status == "Admitted" || a.Status == "CheckedIn")
                                                 .Select(a => new SelectListItem
                                                 {
                                                     Value = a.AppointmentId.ToString(),
                                                     Text = a.FullName
                                                 }).ToListAsync();

            return View(treatment);
        }

        // GET: /MyPatients/
        public async Task<IActionResult> PatientPrescription()
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);
            var roles = await _userManager.GetRolesAsync(user);

            bool isAdmin = roles.Contains("Admin");
            bool isDoctor = roles.Contains("Doctor");

            IQueryable<Appointment> query = _context.Appointments
                .Where(a => a.Status == "Admitted" || a.Status == "CheckedIn")
                .OrderBy(a => a.FullName);

            // If the user is a Doctor but not Admin, filter by DoctorId
            if (isDoctor && !isAdmin)
            {
                query = query.Where(a => a.DoctorId == userId);
            }

            var patients = await query.ToListAsync();

            return View(patients);
        }


        //Get: Prescription page
        [HttpGet]
        public async Task<IActionResult> Prescribe(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null) return NotFound();

            var meds = await _context.StockMedications
                .Where(m => m.IsActive && m.QuantityAvailable > 0)
                .Select(m => new SelectListItem
                {
                    Value = m.MedId.ToString(),
                    Text = m.Name + " (" + m.Brand + ")"
                }).ToListAsync();

            var viewModel = new PrescriptionViewModel
            {
                AppointmentId = appointment.AppointmentId,
                PatientName = appointment.FullName,
                Medications = meds,
                Prescriptions = new List<PrescriptionItemViewModel>
                {
                    new PrescriptionItemViewModel { Medications = meds }
                }
            };

            return View(viewModel);
        }

        //Post: Prescribe medication
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Prescribe(PrescriptionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var meds = await _context.StockMedications
                    .Where(m => m.IsActive && m.QuantityAvailable > 0)
                    .Select(m => new SelectListItem
                    {
                        Value = m.MedId.ToString(),
                        Text = m.Name + " (" + m.Brand + ")"
                    }).ToListAsync();

                model.Medications = meds;

                foreach (var item in model.Prescriptions)
                {
                    item.Medications = meds;
                }

                TempData["ToastMessage"] = "Unable to save prescription. Please fix errors.";
                TempData["ToastType"] = "danger";
                return View(model);
            }

            var userId = _userManager.GetUserId(User);

            var prescription = new Prescription
            {
                AppointmentId = model.AppointmentId,
                PrescribedById = userId,
                PrescribedDate = DateTime.Now,
                Medications = model.Prescriptions.Select(p => new PrescribedMedication
                {
                    MedId = p.MedId.Value,
                    Dosage = p.Dosage,
                    Frequency = p.Frequency,
                    Duration = p.Duration,
                    Notes = model.FinalNote
                }).ToList()
            };
            _context.Prescriptions.Add(prescription);
            await _context.SaveChangesAsync();

            TempData["ToastMessage"] = "Prescription(s) saved successfully.";
            TempData["ToastType"] = "success";
            return RedirectToAction("PatientPrescription", "Doctor");
        }

        // GET: Doctor/DischargeForm/{id}
        public async Task<IActionResult> DischargeForm(int id)
        {
            var appointment = await _context.Appointments
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appointment == null)
            {
                return NotFound();
            }

            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkReadyForDischarge(int id, string dischargeSummary)
        {
            var appointment = await _context.Appointments.FindAsync(id);

            if (appointment == null)
            {
                return NotFound();
            }

            appointment.Status = "Ready To Be Discharged";

            var admission = await _context.Admissions
                                 .FirstOrDefaultAsync(a => a.AppointmentId == appointment.AppointmentId);
            if (admission != null)
            {

                admission.Status = "Ready To Be Discharged";
            }

            var doctorId = _userManager.GetUserId(User);

            var dischargeInfo = new DischargeInformation
            {
                AppointmentId = appointment.AppointmentId,
                DischargeSummary = dischargeSummary,
                DischargeDate = DateTime.Now,
                DischargedById = doctorId
            };

            _context.DischargeInformation.Add(dischargeInfo);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Patient marked as ready to be discharged.";
            return RedirectToAction("PatientList", "Doctor");
        }

        [HttpGet]
        public async Task<IActionResult> GetPatientPrescriptions(string patientIdNumber)
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);
            var doctorId = _userManager.GetUserId(User);

            IQueryable<PrescribedMedication> query = _context.PrescribedMedications
                .Include(pm => pm.Prescription)
                    .ThenInclude(p => p.Appointment)
                .Include(pm => pm.StockMedications);

            if (roles.Contains("Admin"))
            {
                // 🔹 Admin: show all prescriptions for the patient
                query = query.Where(pm =>
                    pm.Prescription.Appointment.IdNumber == patientIdNumber &&
                    !pm.IsDispensed);
            }
            else
            {
                // 🔹 Doctor: show only their own prescriptions
                query = query.Where(pm =>
                    pm.Prescription.PrescribedById == doctorId &&
                    pm.Prescription.Appointment.IdNumber == patientIdNumber &&
                    !pm.IsDispensed);
            }

            var prescriptions = await query
                .Select(pm => new
                {
                    pm.PrescriptionId,
                    pm.Prescription.PrescribedDate,
                    MedicationId = pm.MedId,
                    MedicationName = pm.StockMedications.Name,
                    pm.Dosage,
                    pm.Frequency,
                    pm.Duration,
                    pm.Notes,
                    pm.IsDispensed
                })
                .GroupBy(x => new { x.PrescriptionId, x.PrescribedDate })
                .Select(g => new PrescriptionDto
                {
                    PrescriptionId = g.Key.PrescriptionId,
                    PrescribedDate = g.Key.PrescribedDate,
                    Medications = g.Select(m => new MedicationDto
                    {
                        MedicationId = m.MedicationId,
                        MedicationName = m.MedicationName,
                        Dosage = m.Dosage,
                        Frequency = m.Frequency,
                        Duration = m.Duration,
                        Notes = m.Notes,
                        IsDispensed = m.IsDispensed
                    }).ToList()
                })
                .OrderByDescending(p => p.PrescribedDate)
                .ToListAsync();

            return Json(prescriptions);
        }
        public async Task<IActionResult> MyPrescriptions(int pg = 1)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            IQueryable<PrescribedMedication> query = _context.PrescribedMedications
                .Include(pm => pm.Prescription)
                    .ThenInclude(p => p.Appointment)
                .Include(pm => pm.StockMedications);

            // Filter based on role
            if (!roles.Contains("Admin"))
            {
                query = query.Where(pm =>
                    pm.Prescription.PrescribedById == userId &&
                    !pm.IsDispensed &&
                    pm.Prescription.Appointment.Status != "Completed");
            }
            else
            {
                query = query.Where(pm =>
                    !pm.IsDispensed &&
                    pm.Prescription.Appointment.Status != "Completed");
            }

            // prescriptions list
            var prescriptions = await query
                .Select(pm => new
                {
                    PatientIdNumber = pm.Prescription.Appointment.IdNumber,
                    PatientName = pm.Prescription.Appointment.FullName,
                    PrescriptionId = pm.PrescriptionId,
                    PrescribedDate = pm.Prescription.PrescribedDate,
                    MedicationId = pm.MedId,
                    MedicationName = pm.StockMedications.Name,
                    Dosage = pm.Dosage,
                    Frequency = pm.Frequency,
                    Duration = pm.Duration,
                    Notes = pm.Notes,
                    IsDispensed = pm.IsDispensed
                })
                .GroupBy(x => x.PatientIdNumber)
                .Select(g => new PatientPrecriptionsViewModel
                {
                    PatientIdNumber = g.Key,
                    PatientName = g.First().PatientName,
                    Prescriptions = g.GroupBy(x => x.PrescriptionId)
                        .Select(pg => new PrescriptionDto
                        {
                            PrescriptionId = pg.Key,
                            PrescribedDate = pg.First().PrescribedDate,
                            Medications = pg.Select(m => new MedicationDto
                            {
                                MedicationId = m.MedicationId,
                                MedicationName = m.MedicationName,
                                Dosage = m.Dosage,
                                Frequency = m.Frequency,
                                Duration = m.Duration,
                                Notes = m.Notes,
                                IsDispensed = m.IsDispensed
                            }).ToList()
                        }).ToList(),
                    TotalItems = g.Count(),
                    Status = g.Any(m => !m.IsDispensed) ? "Pending" : "Completed"
                })
                .ToListAsync();

            // 🔹 Paging setup
            const int pageSize = 5;
            if (pg < 1)
                pg = 1;

            int recsCount = prescriptions.Count();
            var pager = new Pager(recsCount, pg, pageSize);
            int recSkip = (pg - 1) * pageSize;
            var data = prescriptions.Skip(recSkip).Take(pageSize).ToList();

            ViewBag.Pager = pager;

            // Stats
            ViewBag.TotalPrescriptions = prescriptions.Count();
            ViewBag.AvgItemsPerPrescription = prescriptions.Any()
                ? prescriptions.Average(p => p.TotalItems).ToString("0.0") : "0";

            // Chart for the past 30 days
            var today = DateTime.Today;
            var startDate = today.AddDays(-29);
            var last30Days = Enumerable.Range(0, 30)
                .Select(i => startDate.AddDays(i))
                .ToList();

            // Extract all inner prescriptions
            var allInnerPrescriptions = prescriptions
                .SelectMany(p => p.Prescriptions)
                .ToList();
            var medicationCounts = allInnerPrescriptions
                .GroupBy(p => p.PrescribedDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Sum(p => p.Medications.Count) // sum the medications in each prescription
                })
                .ToList();


            // Fill in missing days with 0
            var labels = last30Days.Select(d => d.ToString("MMM dd")).ToList();
            var counts = last30Days.Select(d =>
                 medicationCounts.FirstOrDefault(x => x.Date == d)?.Count ?? 0
            ).ToList();


            ViewBag.PrescriptionChartLabelsJson = JsonSerializer.Serialize(labels);
            ViewBag.PrescriptionChartDataJson = JsonSerializer.Serialize(counts);

            // Recent patients
            ViewBag.RecentPatients = allInnerPrescriptions
                .OrderByDescending(p => p.PrescribedDate)
                .Take(3)
                .Select(p => new
                {
                    p.PrescribedDate,
                    DatePrescribed = p.PrescribedDate.ToString("MMM dd"),
                    PatientName = prescriptions
                        .FirstOrDefault(x => x.Prescriptions.Any(pp => pp.PrescriptionId == p.PrescriptionId))?.PatientName,
                    PatientIdNumber = prescriptions
                        .FirstOrDefault(x => x.Prescriptions.Any(pp => pp.PrescriptionId == p.PrescriptionId))?.PatientIdNumber
                })
                .ToList();

            return View(data);
        }


        // GET: ScheduleFollowUp
        [HttpGet]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> ScheduleFollowUp(int appointmentId)
        {
            var appointment = await _context.Appointments
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appointment == null)
                return NotFound();

            ViewBag.Wards = await _context.wards.ToListAsync();

            // Pre-fill follow-up with patient info
            var vm = new AppointmentBookingDetailsVM
            {
                UserId = appointment.UserId,
                FullName = appointment.FullName,
                Age = appointment.Age,
                Gender = appointment.Gender,
                IdNumber = appointment.IdNumber,
                DoctorId = appointment.DoctorId // same doctor
            };

            return View(vm);
        }

        // POST: ScheduleFollowUp
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> ScheduleFollowUp(AppointmentBookingDetailsVM model)
        {
            // Safety check: no double booking
            bool exists = await _context.Appointments.AnyAsync(a =>
                a.DoctorId == model.DoctorId &&
                a.PreferredDate == model.PreferredDate &&
                a.PreferredTime == model.PreferredTime);

            if (exists)
            {
                TempData["ToastMessage"] = "Time slot already booked.";
                TempData["ToastType"] = "danger";
                return View(model);
            }

            var doctor = await _userManager.GetUserAsync(User);
            if (doctor == null)
                return Unauthorized();

            var followUp = new Appointment
            {
                UserId = model.UserId,
                DoctorId = doctor.Id, // always the logged-in doctor
                PreferredDate = model.PreferredDate,
                PreferredTime = model.PreferredTime,
                Reason = string.IsNullOrWhiteSpace(model.Reason) ? "Follow-up visit" : model.Reason,
                ConsultationRoomId = null,
                FullName = model.FullName,
                Age = model.Age,
                Gender = model.Gender,
                IdNumber = model.IdNumber,
                DateBooked = DateTime.Now,
                Status = "Scheduled"
            };

            _context.Appointments.Add(followUp);
            await _context.SaveChangesAsync();

            TempData["ToastMessage"] = "Follow-up appointment scheduled!";
            TempData["ToastType"] = "success";

            return RedirectToAction("DoctorAppointments");
        }

        //Get: GetTakenSlots
        [HttpGet]
        public async Task<IActionResult> GetTakenSlots(string doctorId, DateTime date)
        {
            var bookedSlots = await _context.Appointments
                                    .Where(a => a.DoctorId == doctorId && a.PreferredDate == date)
                                    .Select(a => a.PreferredTime.ToString(@"hh\:mm"))
                                    .ToListAsync();

            return Json(bookedSlots);
        }


        // GET: DoctorAppointments
        public async Task<IActionResult> DoctorAppointments(int pg = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);
            var allowedStatuses = new[] { "Pending", "CheckedIn", "Scheduled" };
            var today = DateTime.Today;

            IQueryable<Appointment> query = _context.Appointments
                .Include(a => a.User)
                .Include(a => a.ConsultationRoom);

            if (roles.Contains("Admin"))
            {
                // 🔹 Admin sees all doctors’ appointments
                query = query.Where(a => allowedStatuses.Contains(a.Status) && a.PreferredDate >= today);
            }
            else
            {
                // 🔹 Doctor sees only their own appointments
                query = query.Where(a => a.DoctorId == user.Id && allowedStatuses.Contains(a.Status) && a.PreferredDate >= today);
            }

            var appointments = await query
                .OrderBy(a => a.PreferredDate)
                .ThenBy(a => a.PreferredTime)
                .ToListAsync();

            // Paging
            const int pageSize = 5;
            if (pg < 1)
                pg = 1;

            int recsCount = appointments.Count();
            var pager = new Pager(recsCount, pg, pageSize);
            int recSkip = (pg - 1) * pageSize;
            var data = appointments.Skip(recSkip).Take(pageSize).ToList();

            ViewBag.Pager = pager;

            return View(data);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOutPatient(int appointmentId)
        {
            // find appointment
            var appointment = await _context.Appointments
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appointment == null)
            {
                TempData["ToastMessage"] = "Appointment not found.";
                TempData["ToastType"] = "danger";
                return RedirectToAction("ViewAdmissions");
            }

            // free up consultation room if assigned
            if (appointment.ConsultationRoomId.HasValue)
            {
                var room = await _context.ConsultationRooms
                    .FirstOrDefaultAsync(r => r.RoomId == appointment.ConsultationRoomId);

                if (room != null)
                {
                    room.IsAvailable = true;
                    _context.Update(room);
                }

                //// clear the room reference
                //appointment.ConsultationRoomId = null;
            }

            // update appointment status
            appointment.Status = "CheckedOut";

            _context.Update(appointment);
            await _context.SaveChangesAsync();

            TempData["ToastMessage"] = $"{appointment.FullName} has been checked out successfully.";
            TempData["ToastType"] = "success";

            return RedirectToAction("PatientList");
        }

        // GET: Doctor/UpdatePrescription
        public IActionResult UpdatePrescription(int prescriptionId)
        {
            var prescription = _context.Prescriptions
                .Include(p => p.Appointment)
                .Include(p => p.Medications)
                .ThenInclude(pm => pm.StockMedications)
                .FirstOrDefault(p => p.PrescriptionId == prescriptionId);

            if (prescription == null)
                return NotFound();

            // Map to a ViewModel if needed
            var model = new UpdatePrescriptionViewModel
            {
                PrescriptionId = prescription.PrescriptionId,
                PatientName = prescription.Appointment.FullName,
                Medications = prescription.Medications.Where(m => !m.IsDispensed).Select(m => new MedicationViewModel
                {
                    PrescribedMedicationId = m.Id,
                    MedId = m.MedId,
                    MedicationName = m.StockMedications.Name,
                    Dosage = m.Dosage,
                    Frequency = m.Frequency,
                    Duration = m.Duration,
                    Notes = m.Notes
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        public IActionResult UpdatePrescription(UpdatePrescriptionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);

                TempData["ToastMessage"] = "Update failed: " + string.Join(" | ", errors);
                TempData["ToastType"] = "danger";

                return View(model);
            }

            foreach (var med in model.Medications)
            {
                var prescribedMed = _context.PrescribedMedications
                                            .FirstOrDefault(m => m.Id == med.PrescribedMedicationId);

                if (prescribedMed != null)
                {
                    prescribedMed.Dosage = med.Dosage;
                    prescribedMed.Frequency = med.Frequency;
                    prescribedMed.Duration = med.Duration;
                    prescribedMed.Notes = med.Notes;
                }
            }

            _context.SaveChanges();
            TempData["ToastMessage"] = "Prescription updated successfully!";
            TempData["ToastType"] = "success";

            return RedirectToAction("MyPrescriptions");
        }


        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> PatientFolders()
        {
            var doctorId = _userManager.GetUserId(User);

            // Get all patients who have folders (for this doctor)
            var patients = (await _context.Admissions
                 .Include(a => a.Appointment)
                     .ThenInclude(appt => appt.User)
                 .Where(a => a.FolderStatus == "Has Folder" &&
                             (User.IsInRole("Admin") || a.Appointment.DoctorId == doctorId))
                 .ToListAsync())  // bring data to memory
                 .GroupBy(a => a.Appointment.IdNumber)
                 .Select(g => g.OrderByDescending(a => a.AdmissionDate).FirstOrDefault())
                 .Select(a => new PatientFolderVM
                 {
                     AppointmentId = a.AppointmentId,
                     FullName = a.Appointment.FullName,
                     Age = a.Appointment.Age,
                     Gender = a.Appointment.Gender,
                     IdNumber = a.Appointment.IdNumber,
                     ProfileImagePath = a.Appointment.User.ProfileImagePath,
                     Status = a.Appointment.Status,
                     PhoneNumber = a.Appointment.User.PhoneNumber,
                     Address = a.Appointment.User.Address
                 })
                 .ToList();

            // Attach latest info for modal (ViewBags)
            ViewBag.PatientFolders = await _context.PatientFolder
                .Include(f => f.Appointment)
                .ThenInclude(a => a.User)
                .ToListAsync();

            ViewBag.Treatments = await _context.Treatment
                .Include(t => t.Appointment)
                .Include(t => t.RecordedBy)
                .GroupBy(t => t.AppointmentId)
                .Select(g => g.OrderByDescending(t => t.TreatmentDate).FirstOrDefault())
                .ToListAsync();

            ViewBag.Vitals = await _context.PatientVitals
                .GroupBy(v => v.FolderId)
                .Select(g => g.OrderByDescending(v => v.VitalsDate).FirstOrDefault())
                .ToListAsync();

            ViewBag.Discharges = await _context.DischargeInformation
                .Include(d => d.Appointment)
                .Include(d => d.DischargedBy)
                .GroupBy(d => d.AppointmentId)
                .Select(g => g.OrderByDescending(d => d.DischargeDate).FirstOrDefault())
                .ToListAsync();

            ViewBag.EmergencyContacts = await _context.EmergencyContacts
                .Include(e => e.User)
                .ToListAsync();

            return View(patients);
        }

        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> AdministeredPrescriptions(int pg = 1)
        {
            var doctorId = _userManager.GetUserId(User);

            var prescriptions = _context.PrescribedMedications
                .Include(pm => pm.Prescription)
                    .ThenInclude(p => p.Appointment)
                .Include(pm => pm.StockMedications)
                .Where(pm => pm.Prescription.PrescribedById == doctorId && pm.IsDispensed);

            // Pagination
            const int pageSize = 5;
            if (pg < 1) pg = 1;

            int recsCount = await prescriptions.CountAsync();
            var pager = new Pager(recsCount, pg, pageSize);
            int recSkip = (pg - 1) * pageSize;

            var data = await prescriptions.Skip(recSkip).Take(pageSize).ToListAsync();

            ViewBag.Pager = pager;

            return View(data);
        }

    }
}
