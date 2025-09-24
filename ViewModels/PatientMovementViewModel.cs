using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Ward_Management_System.DTOs;
using Ward_Management_System.Models;

namespace Ward_Management_System.ViewModels
{
    public class PatientMovementViewModel
    {
        [Required]
        public int AdmissionId { get; set; }

        [Required(ErrorMessage = "Please select a ward.")]
        public int? WardId { get; set; }

        [Required(ErrorMessage = "Please enter a reason for the move.")]
        [MaxLength(255)]
        public string Notes { get; set; }

        [ValidateNever]
        public string PatientName { get; set; }

        [ValidateNever]
        public string IdNumber { get; set; }

        [ValidateNever]
        public string CurrentLocation { get; set; }

        [ValidateNever]
        public List<MovementRecord> MovementHistory { get; set; }

        [ValidateNever]
        public List<Wards> AvailableWards { get; set; }
    }
}
