using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Ward_Management_System.Data;
using Ward_Management_System.Models;
using Ward_Management_System.ViewModels;

namespace Ward_Management_System.Controllers
{
    public class WardAdminController : Controller
    {
        public WardAdminController(AppDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        //Ward Admin Dashboard
        [Authorize(Roles = "WardAdmin,Admin")]
        public IActionResult Index()
        {
            return View();
        }

        //Check In Page
        [Authorize(Roles = "WardAdmin,Admin")]
        public async Task<IActionResult> CheckIn(int pg = 1, DateTime? selectedDate = null, string? statusFilter = null, string? ageFilter = null, string? searchName = null)
        {
            var today = DateTime.Today;

            var query = _context.Appointments
                .Include(a => a.User)
                .Include(a => a.ConsultationRoom)
                .Where(a => a.Status != "Cancelled" && a.Status != "Admitted" && a.Status != "Discharged");

            // filter by date
            if (selectedDate.HasValue)
            {
                query = query.Where(a => a.PreferredDate.Date == selectedDate.Value.Date);
            }
            else
            {
                // only show today and future appointments
                query = query.Where(a => a.PreferredDate >= today);
            }

            // Status Filter
            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(a => a.Status == statusFilter);
            }

            // Age Group Filter
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

            // Search by patient name
            if (!string.IsNullOrEmpty(searchName))
            {
                query = query.Where(a => a.FullName.Contains(searchName));
            }

            // Sort: earliest dates first, then by time
            query = query.OrderBy(a => a.PreferredDate).ThenBy(a => a.PreferredTime);

            // Pagination
            const int pageSize = 5;
            if (pg < 1) pg = 1;

            int recsCount = await query.CountAsync();
            var pager = new Pager(recsCount, pg, pageSize);
            int recSkip = (pg - 1) * pageSize;

            var data = await query.Skip(recSkip).Take(pageSize).ToListAsync();

            ViewBag.Pager = pager;
            ViewBag.CurrentFilters = new { selectedDate, statusFilter, ageFilter, searchName };

            ViewBag.ConsultationRooms = _context.ConsultationRooms
                .Select(cr => new SelectListItem
                {
                    Value = cr.RoomId.ToString(),
                    Text = cr.IsAvailable ? cr.RoomName : $"{cr.RoomName} (Occupied)",
                    Disabled = !cr.IsAvailable
                })
                .ToList();

            ViewBag.Wards = _context.wards
                .Select(w => new SelectListItem { Value = w.WardId.ToString(), Text = w.WardName })
                .ToList();

            return View(data);
        }

        [Authorize(Roles = "WardAdmin,Admin")]
        [HttpPost]
        public async Task<IActionResult> CheckInPatient(int id, int ConsultationRoomId)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null) return NotFound();

            //check if patient is already checked in 
            var duplicateCheck = await _context.Appointments
                .AnyAsync(a => a.IdNumber == appointment.IdNumber
                            && a.FullName == appointment.FullName
                            && (a.Status == "CheckedIn" || a.Status == "Admitted"));

            if (duplicateCheck)
            {
                TempData["ToastMessage"] = "Patient is already checked in.";
                TempData["ToastType"] = "warning";
                return RedirectToAction("CheckIn");
            }

            var room = await _context.ConsultationRooms.FindAsync(ConsultationRoomId);
            if (room == null || !room.IsAvailable)
            {
                TempData["ToastMessage"] = "Room not available.";
                TempData["ToastType"] = "danger";
                return RedirectToAction("CheckIn");
            }

            // Assign room
            appointment.ConsultationRoomId = ConsultationRoomId;
            appointment.Status = "CheckedIn";

            // Mark room as occupied
            room.IsAvailable = false;

            _context.Update(appointment);
            _context.Update(room);
            await _context.SaveChangesAsync();

            TempData["ToastMessage"] = $"{appointment.FullName} checked in to {room.RoomName}.";
            TempData["ToastType"] = "success";

            return RedirectToAction("CheckIn");
        }

        //Admit Patient
        [Authorize(Roles = "WardAdmin,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdmitPatient(Admissions admission, int AppointmentId)
        {
            //get appointment in order to check if the patient is already admitted
            var appointment = await _context.Appointments
                .Include(a => a.ConsultationRoom)
                .FirstOrDefaultAsync(a => a.AppointmentId == AppointmentId);

            if (appointment == null)
            {
                TempData["ToastMessage"] = "Admission failed: Appointment not found.";
                TempData["ToastType"] = "danger";
                return RedirectToAction("CheckIn");
            }

            var existingAdmission = await _context.Admissions
                .Include(ad => ad.Appointment)
                .FirstOrDefaultAsync(ad =>
                    ad.Appointment.FullName == appointment.FullName &&
                    ad.Appointment.IdNumber == appointment.IdNumber &&
                    ad.Status == appointment.Status);

            if (existingAdmission != null)
            {
                TempData["ToastMessage"] = "Admission failed: Patient is already admitted.";
                TempData["ToastType"] = "warning";
                return RedirectToAction("CheckIn");
            }

            if (ModelState.IsValid)
            {
                TempData["ToastMessage"] = "Admission failed: Invalid form submission.";
                TempData["ToastType"] = "danger";
                return RedirectToAction("CheckIn");
            }
            appointment.Status = "Admitted";

            var hasExistingFolder = await _context.PatientFolder
                    .Include(f => f.Appointment)
                    .AnyAsync(f => f.Appointment.IdNumber == appointment.IdNumber);

            admission.FolderStatus = hasExistingFolder ? "Has Folder" : "No Folder";

            admission.Status = appointment.Status;
            admission.AppointmentId = appointment.AppointmentId;
            _context.Admissions.Add(admission);

            var ward = await _context.wards.FirstOrDefaultAsync(w => w.WardId == admission.WardId);
            if (ward != null)
            {
                if (ward.CurrentBedCount > 0 && ward.OccupiedBeds < ward.TotalBedCapacty)
                {
                    ward.OccupiedBeds += 1;
                    ward.CurrentBedCount -= 1;
                }
                else
                {
                    TempData["ToastMessage"] = "Admission failed: No available beds in selected ward.";
                    TempData["ToastType"] = "warning";
                    return RedirectToAction("CheckIn");
                }
            }

            if (appointment.ConsultationRoomId != null)
            {
                var room = await _context.ConsultationRooms
                    .FirstOrDefaultAsync(r => r.RoomId == appointment.ConsultationRoomId);

                if (room != null)
                {
                    room.IsAvailable = true; 
                }
            }

            await _context.SaveChangesAsync();

            TempData["ToastMessage"] = "Patient successfully admitted. Consultation room freed up.";
            TempData["ToastType"] = "success";
            return RedirectToAction("CheckIn");
        }

        [Authorize(Roles = "WardAdmin,Admin")]
        public async Task<IActionResult> ViewAdmissions(int pg = 1, string? locationFilter = null, string? statusFilter = null, string? searchTerm = null)
        {
            var admittedPatients = await (from ad in _context.Admissions
                                          where ad.Status != "Discharged"
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

            // Combine patients
            var allPatients = admittedPatients.Union(checkedInPatients).ToList();

            // Apply filters
            if (!string.IsNullOrEmpty(locationFilter))
                allPatients = allPatients.Where(p => p.WardName == locationFilter).ToList();

            if (!string.IsNullOrEmpty(statusFilter))
                allPatients = allPatients.Where(p => p.FolderStatus == statusFilter).ToList();

            if (!string.IsNullOrEmpty(searchTerm))
                allPatients = allPatients.Where(p => p.PatientName.Contains(searchTerm) || p.IdNumber.Contains(searchTerm)).ToList();

            // Paging
            const int pageSize = 5;
            if (pg < 1) pg = 1;

            int recsCount = allPatients.Count();
            var pager = new Pager(recsCount, pg, pageSize);
            int recSkip = (pg - 1) * pageSize;
            var data = allPatients.Skip(recSkip).Take(pageSize).ToList();

            ViewBag.Pager = pager;

            // Keep selected filters in ViewBag
            ViewBag.CurrentFilters = new { locationFilter, statusFilter, searchTerm };

            // Provide list of wards for dropdown
            ViewBag.WardList = _context.wards.ToList();
            ViewBag.ConsultationRooms = _context.ConsultationRooms.ToList();

            // Recent folder details
            var folders = _context.PatientFolder
                                .Include(f => f.Appointment)
                                .ThenInclude(a => a.User)
                                .Include(f => f.Appointment)
                                .ThenInclude(a => a.User.EmergencyContacts)
                                .GroupBy(f => new { f.Appointment.FullName, f.Appointment.IdNumber })
                                .Select(g => g.OrderByDescending(f => f.CreatedDate).First())
                                .ToList();
            ViewBag.ModelFolderList = folders;

            // Patient Treatments - only latest per patient
            var treatments = _context.Treatment
                .Include(t => t.RecordedBy)
                .Include(t => t.Appointment)
                .GroupBy(t => new { t.Appointment.FullName, t.Appointment.IdNumber })
                .Select(g => g.OrderByDescending(t => t.TreatmentDate).FirstOrDefault())
                .ToList();
            ViewBag.Treatments = treatments;

            // Patient Vitals - only latest per folder
            var vitals = _context.PatientVitals
                .GroupBy(v => v.FolderId)
                .Select(g => g.OrderByDescending(v => v.VitalsDate).FirstOrDefault())
                .ToList();
            ViewBag.Vitals = vitals;

            // Discharges - only latest per patient
            var discharges = _context.DischargeInformation
                .Include(d => d.DischargedBy)
                .Include(d => d.Appointment)
                .GroupBy(d => new { d.Appointment.FullName, d.Appointment.IdNumber })
                .Select(g => g.OrderByDescending(d => d.DischargeDate).FirstOrDefault())
                .ToList();
            ViewBag.Discharges = discharges;


            return View(data);
        }


        [Authorize(Roles = "WardAdmin,Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateFolder(int appointmentId)
        {
           
            var appointment = await _context.Appointments
                                            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);


            if (appointment == null)
            {
                TempData["ToastMessage"] = "Appointment not found.";
                TempData["ToastType"] = "error";
                return RedirectToAction("ViewAdmissions", "WardAdmin");
            }

            var admission = await _context.Admissions
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            var hasFolder = await _context.Admissions
              .AnyAsync(a => a.UserId == appointment.UserId &&
                    a.Appointment.IdNumber == appointment.IdNumber &&
                    a.FolderStatus == "Has Folder");

            if (hasFolder)
            {
                TempData["ToastMessage"] = "This patient already has a folder.";
                TempData["ToastType"] = "warning";
                return RedirectToAction("ViewAdmissions", "WardAdmin");
            }

            if (admission == null)
            {
                TempData["ToastMessage"] = "No admission record found for this appointment.";
                TempData["ToastType"] = "error";
                return RedirectToAction("ViewAdmissions", "WardAdmin");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();
            var folder = new PatientFolder
            {
                AppointmentId = appointment.AppointmentId,
                CreatedBy = user.FullName,
                CreatedDate = DateTime.Now
            };
            _context.PatientFolder.Add(folder);
            admission.FolderStatus = "Has Folder";
            _context.Admissions.Update(admission);

            await _context.SaveChangesAsync();
            TempData["ToastMessage"] = "Patient folder created successfully.";
            TempData["ToastType"] = "success";
            return RedirectToAction("ViewAdmissions", "WardAdmin");
        }

        //Open Folder 
        [Authorize(Roles = "WardAdmin,Admin")]
        [HttpGet]
        public async Task<IActionResult> OpenFolder(int appointmentId)
        {
            var folder = await _context.PatientFolder
                               .Include(f => f.Appointment)
                               .FirstOrDefaultAsync(f => f.AppointmentId == appointmentId);

            if (folder == null) return NotFound();

            return View(folder);
        }

        // GET: Discharge List (only patients with Ready To Be Discharged)
        public async Task<IActionResult> DischargeList(int pg = 1)
        {
            var readyPatients = await _context.Appointments
                .Where(a => a.Status == "Ready To Be Discharged")
                .ToListAsync();

            const int pageSize = 5;
            if(pg < 1) { pg = 1; }

            int recsCount = readyPatients.Count();
            var pager = new Pager(recsCount, pg, pageSize);
            int recSkip = (pg - 1) * pageSize;
            var data = readyPatients.Skip(recSkip).Take(pageSize).ToList();

            ViewBag.Pager = pager;

            return View(data);
        }

        // POST: Mark Appointment as Discharged
        [Authorize(Roles = "WardAdmin,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Discharge(int appointmentId)
        {
            var appointment = await _context.Appointments
                .Include(a => a.ConsultationRoom)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appointment == null)
            {
                TempData["ToastMessage"] = "Appointment not found.";
                TempData["ToastType"] = "danger";
                return RedirectToAction("DischargeList");
            }

            // Update status
            appointment.Status = "Discharged";

            // Find admission linked to this appointment
            var admission = await _context.Admissions
                .FirstOrDefaultAsync(ad => ad.AppointmentId == appointmentId);

            if (admission != null)
            {
                admission.Status = "Discharged";
            }

            if (appointment.ConsultationRoomId != null)
            {
                var room = await _context.ConsultationRooms
                    .FirstOrDefaultAsync(r => r.RoomId == appointment.ConsultationRoomId);

                if (room != null)
                {
                    room.IsAvailable = true; 
                }
            }

            await _context.SaveChangesAsync();

            TempData["ToastMessage"] = $"{appointment.FullName} has been discharged successfully.";
            TempData["ToastType"] = "success";

            return RedirectToAction("DischargeList");
        }

        // GET: Ward/MonitorMovement
        [Authorize(Roles = "WardAdmin,Admin")]
        public IActionResult MonitorMovement(PatientMovementFilterViewModel filter,int pg =1)
        {
            var query = _context.PatientMovements
                .Include(m => m.Admission)
                 .ThenInclude(a => a.Appointment)
                .Include(m => m.Ward)
                .AsQueryable();

            // Filter by patient name
            if (!string.IsNullOrEmpty(filter.PatientName))
            {
                query = query.Where(m => m.Admission.PatientName.Contains(filter.PatientName));
            }

            // Filter by ward
            if (filter.WardId.HasValue)
            {
                query = query.Where(m => m.WardId == filter.WardId.Value);
            }

            // Filter by date range
            if (filter.FromDate.HasValue)
            {
                query = query.Where(m => m.MovementTime >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(m => m.MovementTime <= filter.ToDate.Value);
            }

            int recsCount = query.Count();
            const int pageSize = 5;
            if (pg < 1)
            {
                pg = 1;
            }

            var pager = new Pager(recsCount, pg, pageSize);

            var movements = query
                .OrderByDescending(m => m.MovementTime)
                .Skip((pg - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new PatientMovementHistoryViewModel
                {
                    PatientName = m.Admission.PatientName,
                    IdNumber = m.Admission.Appointment.IdNumber,
                    AdmissionId = m.AdmissionId,
                    WardName = m.Ward.WardName,
                    MovementTime = m.MovementTime,
                    Notes = m.Notes
                })
                .ToList();

            filter.Movements = movements;
            filter.AvailableWards = _context.wards.ToList();

            ViewBag.Pager = pager;

            return View(filter);
        }


    }
}
