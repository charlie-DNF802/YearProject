using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Ward_Management_System.Data;
using Ward_Management_System.DTOs;
using Ward_Management_System.Models;
using Ward_Management_System.ViewModels;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Ward_Management_System.Controllers
{
    public class NurseController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        public NurseController(AppDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        [Authorize(Roles = "Nurse,Sister,Admin")]

        public async Task<IActionResult> PatientList(int pg = 1)
        {
            var admittedPatients = await (from ad in _context.Admissions
                                          where ad.Status == "Admitted"
                                          join ap in _context.Appointments
                                          on ad.AppointmentId equals ap.AppointmentId
                                          join w in _context.wards
                                          on ad.WardId equals w.WardId
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

        //Adding Patient Vitals
        [HttpPost]
        [Authorize(Roles = "Nurse,Sister,Admin")]

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddVitals(PatientVitals vitals)
        {
            var folderExists = await _context.PatientFolder.AnyAsync(f => f.FolderId == vitals.FolderId);
            if (!folderExists)
            {
                TempData["ToastMessage"] = "Error: Patient record not found.";
                TempData["ToastType"] = "danger";
                Console.WriteLine("Error : Patient folder can not be found");
                return RedirectToAction("PatientList");
            }

            if (ModelState.IsValid)
            {
                _context.PatientVitals.Add(vitals);
                await _context.SaveChangesAsync();

                TempData["ToastMessage"] = "Vitals recorded successfully.";
                TempData["ToastType"] = "success";
            }
            if (!ModelState.IsValid)
            {
                foreach (var entry in ModelState)
                {
                    foreach (var error in entry.Value.Errors)
                    {
                        TempData["ToastMessage"] = $"Failed to save vitals. Please check input: {entry.Key}, Error: {error.ErrorMessage}";
                        TempData["ToastType"] = "danger";

                    }
                }
            }
            //else
            //{
            //    TempData["ToastMessage"] = "Failed to save vitals. Please check input.";
            //    TempData["ToastType"] = "danger";
            //}
            return RedirectToAction("PatientList");
        }

        //Get TreatPatients
        [Authorize(Roles = "Nurse,Sister,Admin")]
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
                TempData["ToastMessage"] = "Failed to save. Please try again.";
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
        [Authorize(Roles = "Nurse,Sister,Admin")]
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

        // GET: ViewInstructions
        [Authorize(Roles = "Nurse,Sister,Admin")]
        public async Task<IActionResult> ViewInstructions()
        {
            var doctorRoleId = await _context.Roles
                .Where(r => r.Name == "Doctor")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            var usersWithDoctorRole = await _context.UserRoles
                .Where(ur => ur.RoleId == doctorRoleId)
                .Select(ur => ur.UserId)
                .ToListAsync();

            var data = await _context.Appointments
                .Where(a => a.Status == "Admitted" || a.Status == "CheckedIn")
                .Select(a => new
                {
                    a.AppointmentId,a.FullName,a.IdNumber,a.Age,a.Gender,Treatment = a.Treatments
                        .Where(t => usersWithDoctorRole.Contains(t.RecordedById)).OrderByDescending(t => t.TreatmentDate)
                        .Select(t => new
                        {
                            t.Procedure,
                            t.Notes,
                            t.TreatmentDate,
                            DoctorName = t.RecordedBy.UserName
                        })
                        .FirstOrDefault()
                })
                .ToListAsync();

            var result = data.Select(a => new PatientsLatestTreatmentVM
            {
                AppointmentId = a.AppointmentId,
                FullName = a.FullName,
                IdNumber = a.IdNumber,
                Age = a.Age,
                Gender = a.Gender,
                Procedure = a.Treatment?.Procedure,
                Notes = a.Treatment?.Notes,
                TreatmentDate = a.Treatment?.TreatmentDate,
                DoctorName = a.Treatment?.DoctorName
            }).ToList();

            return View(result);
        }

        //:Get : PendingPrescriptions
        [Authorize(Roles = "Nurse,Sister,Admin")]
        public async Task<IActionResult> PendingPrescriptions()
        {
            var prescriptions = await _context.Prescriptions
                .Include(p => p.Medications)
                    .ThenInclude(pm => pm.StockMedications)
                .Include(p => p.Appointment)
                .Include(p => p.PrescribedBy)
                .Where(p =>
                    p.Medications.Any(m => !m.IsDispensed) &&
                    p.Appointment.Status != "Completed")
                .ToListAsync();

            return View(prescriptions);
        }

        //Get: Prescriptions
        [Authorize(Roles = "Nurse,Sister,Admin")]
        public async Task<IActionResult> AdministerMedication(int prescriptionId)
        {
            var prescription = await _context.Prescriptions
                .Include(p => p.Medications)
                .ThenInclude(pm => pm.StockMedications)
                .Include(p => p.Appointment)
                .FirstOrDefaultAsync(p => p.PrescriptionId == prescriptionId);

            if (prescription == null)
                return NotFound();

            return View(prescription);
        }

        //Post: AdministerMedication
        [HttpPost]
        [Authorize(Roles = "Nurse,Sister,Admin")]
        public async Task<IActionResult> AdministerMedication(int prescribedMedicationId, string notes)
        {
            var prescribedMed = await _context.PrescribedMedications
                .FirstOrDefaultAsync(pm => pm.Id == prescribedMedicationId);

            if (prescribedMed == null)
                return NotFound();

            var medication = await _context.StockMedications
                .FirstOrDefaultAsync(m => m.MedId == prescribedMed.MedId);

            var userRole = User.IsInRole("Nurse") ? "Nurse" : "Sister";

            // Restriction: Nurses cannot dispense Schedule 5+
            if (userRole == "Nurse" && medication.Schedule >= 5)
            {
                TempData["ToastMessage"] = "You are not authorized to dispense Schedule 5 or higher medication.";
                TempData["ToastType"] = "danger";
                return RedirectToAction("PendingPrescriptions");
            }

            prescribedMed.IsDispensed = true;

            var record = new DispensedMedication
            {
                PrescribedMedicationId = prescribedMedicationId,
                DispensedById = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value,
                DispensedDate = DateTime.Now,
                Notes = notes
            };

            _context.DispensedMedications.Add(record);
            await _context.SaveChangesAsync();

            TempData["ToastMessage"] = "Medication dispensed successfully.";
            TempData["ToastType"] = "success";
            return RedirectToAction("PendingPrescriptions");
        }



    }
}
