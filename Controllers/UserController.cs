using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Ward_Management_System.Data;
using Ward_Management_System.Models;
using Ward_Management_System.ViewModels;

namespace Ward_Management_System.Controllers
{
    public class UserController : Controller
    {
        public UserController(AppDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        [Authorize(Roles = "User")]
        public  IActionResult Index()
        {
            return View();
        }

        //View Personal details 
        [Authorize(Roles = "User")]
        public async Task<IActionResult> ViewPersonalDetails()
        {
            string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var emergencyContact = await _context.EmergencyContacts
                .Where(e => e.UserId == userId)
                .ToListAsync();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null )
            {
                return NotFound();
            }

            var model = new PersonalDetailsViewModel
            {
                FullName = user.FullName,
                //Age = user.Age,
                Address = user.Address,
                IdNumber = user.IdNumber,
                Gender = user.Gender,
                DateAdded = user.DateAdded,
                ProfileImagePath = user.ProfileImagePath,


                EmergencyName1 = emergencyContact.ElementAtOrDefault(0)?.EmergencyName,
                EmergencyPhone1 = emergencyContact.ElementAtOrDefault(0)?.EmergencyPhone,
                EmergencyEmail1 = emergencyContact.ElementAtOrDefault(0)?.EmergencyEmail,
                EmergencyRelationship1 = emergencyContact.ElementAtOrDefault(0)?.EmergencyRelationship,

                EmergencyName2 = emergencyContact.ElementAtOrDefault(1)?.EmergencyName,
                EmergencyPhone2 = emergencyContact.ElementAtOrDefault(1)?.EmergencyPhone,
                EmergencyEmail2 = emergencyContact.ElementAtOrDefault(1)?.EmergencyEmail,
                EmergencyRelationship2 = emergencyContact.ElementAtOrDefault(1)?.EmergencyRelationship
            };

            return View(model);
        }

        //View Appointments
        [Authorize(Roles = "User")]
        public async Task<IActionResult> ViewAppointments(int pg = 1)
        {
            var user = await _userManager.GetUserAsync(User);

            var appointments = await _context.Appointments
                                             .Include(a => a.ConsultationRoom)
                                             .Include(a => a.Doctor)
                                             .Where(a => a.UserId == user.Id
                                                        && (a.Status == "Pending" || a.Status == "Scheduled")
                                                        && a.PreferredDate >= DateTime.Today)
                                             .OrderByDescending(a => a.PreferredDate)
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
            var data = appointments.Skip(recSkip).Take(pager.PageSize).ToList();
            this.ViewBag.Pager = pager;

            return View(data);
        }

        // GET: EmergencyContacts/AddEmergencyContactDetails
        [Authorize(Roles = "User")]
        public IActionResult AddEmergencyContactDetails()
        {
            string userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            ViewBag.UserId = userId;
            return View();
        }

        // POST: EmergencyContacts/AddEmergencyContactDetails
        [Authorize(Roles = "User")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEmergencyContactDetails(EmergencyContact emergencyContact)
        {
            string userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            // Count existing contacts for this user
            int existingCount = await _context.EmergencyContacts
                .Where(ec => ec.UserId == userId)
                .CountAsync();

            if (existingCount >= 2)
            {
                // Too many contacts, show error
                ModelState.AddModelError("", "You can only add up to 2 emergency contacts.");
                TempData["ToastMessage"] = "You have reached the maximum of 2 emergency contacts!";
                TempData["ToastType"] = "danger";
                ViewBag.UserId = userId;
                return View(emergencyContact);
            }

            if (ModelState.IsValid)
            {
                emergencyContact.UserId = userId;
                _context.Add(emergencyContact);
                await _context.SaveChangesAsync();
                TempData["ToastMessage"] = "Emergency contact added successfully!";
                TempData["ToastType"] = "success";
                return RedirectToAction(nameof(AddEmergencyContactDetails));
            }

            ViewBag.UserId = userId;
            return View(emergencyContact);
        }

        //Post: ProfilePicture
        [HttpPost]
        public async Task<IActionResult> UploadProfileImage(IFormFile ProfileImage)
        {
            if (ProfileImage != null && ProfileImage.Length > 0)
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/profile");
                Directory.CreateDirectory(uploadsFolder); // Ensure folder exists

                var fileName = $"{userId}_{Path.GetFileName(ProfileImage.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await ProfileImage.CopyToAsync(stream);
                }

                var relativePath = $"/images/profile/{fileName}";

                // Save relativePath to the user's profile in the database
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.ProfileImagePath = relativePath;
                    await _context.SaveChangesAsync();
                }

                TempData["ToastMessage"] = "Profile picture updated!";
                TempData["ToastType"] = "success";
            }

            return RedirectToAction("ViewPersonalDetails");
        }
       
        //Get: ChooseDoctorTime
        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> ChooseDoctorTime()
        {
            ViewBag.Doctors = await _userManager.GetUsersInRoleAsync("Doctor");
            return View(new DoctorTimeSelectionVM());
        }

        //Post: ChooseDoctorTime
        [HttpPost]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> ChooseDoctorTime(DoctorTimeSelectionVM model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Doctors = await _userManager.GetUsersInRoleAsync("Doctor");
                return View(model);
            }

            // check if the selected slot is taken
            bool exists = await _context.Appointments.AnyAsync(a =>
                a.DoctorId == model.DoctorId &&
                a.PreferredDate == model.PreferredDate &&
                a.PreferredTime == model.PreferredTime);

            if (exists)
            {
                ModelState.AddModelError("", "Time slot already booked for selected doctor.");
                TempData["ToastMessage"] = "Time slot already booked for selected doctor.";
                TempData["ToastType"] = "danger";
                ViewBag.Doctors = await _userManager.GetUsersInRoleAsync("Doctor");
                return View(model);
            }
            if (model.PreferredDate.Date == DateTime.Today && model.PreferredTime <= DateTime.Now.TimeOfDay)
            {
                ModelState.AddModelError("", "You cannot book a time that has already passed today.");
                TempData["ToastMessage"] = "You cannot book a time that has already passed today.";
                TempData["ToastType"] = "danger";
                ViewBag.Doctors = await _userManager.GetUsersInRoleAsync("Doctor");
                return View(model);
            }

            return RedirectToAction("ConfirmAppointment", model);
        }

        //Get: ConfirmAppointment
        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> ConfirmAppointment(string doctorId, DateTime preferredDate, TimeSpan preferredTime)
        {
            ViewBag.Doctor = await _userManager.FindByIdAsync(doctorId);

            var vm = new AppointmentBookingDetailsVM
            {
                DoctorId = doctorId,
                PreferredDate = preferredDate,
                PreferredTime = preferredTime
            };
            return View(vm);
        }

        //Post: ConfirmAppointment
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> ConfirmAppointment(AppointmentBookingDetailsVM model)
        {
            // Final safety check
            bool exists = await _context.Appointments.AnyAsync(a =>
                a.DoctorId == model.DoctorId &&
                a.PreferredDate == model.PreferredDate &&
                a.PreferredTime == model.PreferredTime);

            if (exists)
            {
                TempData["ToastMessage"] = "Sorry, someone just booked that slot. Please try again.";
                TempData["ToastType"] = "danger";
                return RedirectToAction("ChooseDoctorTime");
            }

            var user = await _userManager.GetUserAsync(User);

            // Check if this user already has a booking on the same day
            bool alreadyBookedSameDay = await _context.Appointments.AnyAsync(a =>
                a.IdNumber == (model.BookingFor == "Self" ? user.IdNumber : model.OtherPersonIdNumber)
                && a.PreferredDate.Date == model.PreferredDate.Date
                && a.Status != "Cancelled"); // allow booking again if the first one was cancelled

            if (alreadyBookedSameDay)
            {
                TempData["ToastMessage"] = "You already have an appointment booked for this day.";
                TempData["ToastType"] = "danger";
                return RedirectToAction("ViewAppointments");
            }


            var appointment = new Appointment
            {
                UserId = user.Id,
                DoctorId = model.DoctorId,
                PreferredDate = model.PreferredDate,
                PreferredTime = model.PreferredTime,
                Reason = model.Reason,
                ConsultationRoomId = null
            };

            if (model.BookingFor == "Self")
            {
                appointment.FullName = user.FullName;
                appointment.Age = model.Age;
                appointment.Gender = user.Gender;
                appointment.IdNumber = user.IdNumber;
            }
            else
            {
                appointment.FullName = model.OtherPersonName;
                appointment.Age = model.OtherPersonAge ?? 0;
                appointment.Gender = model.OtherPersonGender;
                appointment.IdNumber = model.OtherPersonIdNumber;
            }

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            TempData["ToastMessage"] = "Appointment Successfully Booked!";
            TempData["ToastType"] = "success";

            return RedirectToAction("ViewAppointments");
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
        
        //Post: Cancel Appointment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                TempData["ToastMessage"] = "Appointment not found!";
                TempData["ToastType"] = "danger";
                return RedirectToAction("ViewAppointments");
            }

            appointment.Status = "Cancelled";
            _context.Update(appointment);
            await _context.SaveChangesAsync();

            TempData["ToastMessage"] = "Appointment cancelled successfully.";
            TempData["ToastType"] = "success";
            return RedirectToAction("ViewAppointments");
        }


    }
}
