using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Ward_Management_System.Data;
using Ward_Management_System.Models;
using Ward_Management_System.ViewModels;
using Ward_Management_System.DTOs;

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

        //Get: patient records/folder
        public async Task<IActionResult> PatientList(int pg = 1)
        {
            var doctorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;


            var admittedPatients = await (from ad in _context.Admissions
                                          join ap in _context.Appointments
                                          on ad.AppointmentId equals ap.AppointmentId
                                          join w in _context.wards
                                          on ad.WardId equals w.WardId
                                          where ad.Status == "Admitted" && ap.DoctorId == doctorId
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
                                              Status = ad.Status,

                                          }).ToListAsync();
            var checkedInPatients = await (from a in _context.Appointments
                                           where a.Status == "CheckedIn"
                                           && !_context.Admissions.Any(ad => ad.AppointmentId == a.AppointmentId) && a.DoctorId == doctorId
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
            //gets the recent folder details
            var folders = _context.PatientFolder
                                        .Include(f => f.Appointment)
                                        .GroupBy(f => new { f.Appointment.FullName, f.Appointment.IdNumber })
                                        .Select(g => g.OrderByDescending(f => f.CreatedDate).First()).ToList();

            ViewBag.ModelFolderList = folders;

            var allPatients = admittedPatients.Union(checkedInPatients.ToList()).ToList();

            // Attach FolderId
            foreach (var patient in allPatients)
            {
                var folder = folders.FirstOrDefault(f =>
                    f.Appointment.FullName == patient.PatientName &&
                    f.Appointment.IdNumber == patient.IdNumber);

                patient.FolderId = folder?.FolderId ?? 0;
            }

            //Paging
            const int pageSize = 5;
            if (pg < 1)
            {
                pg = 1;
            }

            int recsCount = allPatients.Count();
            var pager = new Pager(recsCount, pg, pageSize);
            int recSkip = (pg - 1) * pageSize;
            var data = allPatients.Skip(recSkip).Take(pageSize).ToList();
            ViewBag.Pager = pager; // Pass the pager to the view for pagination

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

        [HttpGet]
        public async Task<IActionResult> SearchPatients(string term)
        {
            var results = await _context.Appointments
                .Where(a => a.FullName.Contains(term) && (a.Status == "Admitted" || a.Status == "CheckedIn"))
                .Select(a => new
                {
                    id = a.AppointmentId,
                    text = a.FullName
                })
                .Take(20) // limit to 20 results
                .ToListAsync();

            return Json(results);
        }

        // GET: /MyPatients/
        public async Task<IActionResult> PatientPrescription()
        {
            var doctorId = _userManager.GetUserId(User);

            var patients = await _context.Appointments
                 .Where(a => a.DoctorId == doctorId &&
                    (a.Status == "Admitted" || a.Status == "CheckedIn"))
                .OrderBy(a => a.FullName)
                .ToListAsync();

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
            var doctorId = _userManager.GetUserId(User);

            var prescriptions = await _context.PrescribedMedications
                .Where(pm =>
                    pm.Prescription.PrescribedById == doctorId &&
                    pm.Prescription.Appointment.IdNumber == patientIdNumber &&
                    pm.IsDispensed == false)
                .Select(pm => new
                {
                    pm.PrescriptionId,
                    pm.Prescription.PrescribedDate,
                    MedicationId = pm.MedId,
                    MedicationName = pm.StockMedications.Name, // Ensure StockMedications has a Name
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
            var doctorId = _userManager.GetUserId(User);

            var prescriptions = await _context.PrescribedMedications
                .Where(pm =>
                    pm.Prescription.PrescribedById == doctorId &&
                    !pm.IsDispensed &&
                    pm.Prescription.Appointment.Status != "Completed")
                .Select(pm => new
                {
                    PatientIdNumber = pm.Prescription.Appointment.IdNumber,
                    PatientName = pm.Prescription.Appointment.FullName,
                    PrescriptionId = pm.PrescriptionId,
                    PrescribedDate = pm.Prescription.PrescribedDate,
                    MedicationId = pm.MedId,
                    MedicationName = pm.StockMedications.Name, // Example field in StockMedications
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

            //Paging
            const int pageSize = 5;
            if (pg < 1)
            {
                pg = 1;
            }

            int recsCount = prescriptions.Count();
            var pager = new Pager(recsCount, pg, pageSize);
            int recSkip = (pg - 1) * pageSize;
            var data = prescriptions.Skip(recSkip).Take(pageSize).ToList();

            ViewBag.Pager = pager;

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
            var doctor = await _userManager.GetUserAsync(User);
            if (doctor == null)
                return Unauthorized();

            var allowedStatuses = new[] { "Pending", "CheckedIn", "Scheduled" };
            var today = DateTime.Today;

            var appointments = await _context.Appointments
                .Where(a => a.DoctorId == doctor.Id && allowedStatuses.Contains(a.Status) && a.PreferredDate >= today)
                .Include(a => a.User)
                .Include(a => a.ConsultationRoom)
                .OrderBy(a => a.PreferredDate)
                .ThenBy(a => a.PreferredTime)
                .ToListAsync();

            //Paging
            const int pageSize = 5;
            if (pg < 1)
            {
                pg = 1;
            }

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






    }
}
