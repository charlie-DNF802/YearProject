using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ward_Management_System.Data;
using Ward_Management_System.DTOs;
using Ward_Management_System.Models;
using Ward_Management_System.ViewModels;

namespace Ward_Management_System.Controllers
{
    public class MovementController : Controller
    {
        public MovementController(AppDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        // POST: MovePatient
        [HttpPost]
        public IActionResult MovePatient(PatientMovementViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var patient = _context.Admissions
                    .Include(a => a.Appointment)
                    .Include(a => a.Wards)
                    .FirstOrDefault(a => a.AdmissionId == model.AdmissionId);

                if (patient == null)
                    return NotFound("Admission not found");

                var history = _context.PatientMovements
                    .Where(m => m.AdmissionId == model.AdmissionId)
                    .Include(m => m.Ward)
                    .OrderByDescending(m => m.MovementTime)
                    .Select(m => new MovementRecord
                    {
                        LocationName = m.Ward.WardName,
                        MovementTime = m.MovementTime,
                        Notes = m.Notes ?? string.Empty
                    })
                    .ToList();

                model.PatientName = patient.PatientName;
                model.CurrentLocation = patient.Wards?.WardName ?? "Not Assigned";
                model.MovementHistory = history;
                model.AvailableWards = _context.wards.ToList();

                var errors = ModelState.Values
                                        .SelectMany(v => v.Errors)
                                        .Select(e => e.ErrorMessage)
                                        .ToList();

                // Join errors into one string
                var errorMessage = string.Join(" | ", errors);

                // Toast with detailed errors
                TempData["ToastMessage"] = "Patient move failed: " + errorMessage;
                TempData["ToastType"] = "danger";

                return View("PatientMovement", model);
            }

            var movement = new PatientMovement
            {
                AdmissionId = model.AdmissionId,
                WardId = model.WardId.Value,
                MovementTime = DateTime.Now,
                Notes = model.Notes
            };

            _context.PatientMovements.Add(movement);

            var admission = _context.Admissions.FirstOrDefault(a => a.AdmissionId == model.AdmissionId);
            if (admission != null)
            {
                admission.WardId = model.WardId.Value;
            }

            _context.SaveChanges();
            TempData["ToastMessage"] = "Patient moved successfully!";
            TempData["ToastType"] = "success";

            return RedirectToAction("Movement", new { admissionId = model.AdmissionId });
        }


        //GET: Movement
        public IActionResult Movement(int admissionId)
        {
            var patient = _context.Admissions
                .Include(a => a.Appointment)
                .Include(a => a.Wards)
                .FirstOrDefault(a => a.AdmissionId == admissionId);

            var history = _context.PatientMovements
                .Where(m => m.AdmissionId == admissionId)
                .Include(m => m.Ward)
                .OrderByDescending(m => m.MovementTime)
                .Select(m => new MovementRecord
                {
                    LocationName = m.Ward.WardName,
                    MovementTime = m.MovementTime,
                    Notes = m.Notes
                })
                .ToList();

            if (patient == null)
                return NotFound("Admission not found");

            var model = new PatientMovementViewModel
            {
                AdmissionId = admissionId,
                PatientName = patient.PatientName,
                IdNumber = patient.Appointment.IdNumber,
                CurrentLocation = patient.Wards?.WardName ?? "Not Assigned",
                MovementHistory = history,
                AvailableWards = _context.wards.ToList()
            };

            if (!ModelState.IsValid)
            {
                return View("PatientMovement", model);
            }

            return View("PatientMovement", model);
        }



    }
}
