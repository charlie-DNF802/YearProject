using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Ward_Management_System.ViewModels
{
    public class PrescriptionViewModel
    {
        public int AppointmentId { get; set; }
        public string? PatientName { get; set; }

        public List<PrescriptionItemViewModel> Prescriptions { get; set; }
        public List<SelectListItem> Medications { get; set; } = new List<SelectListItem>();
        [Required]
        public string? FinalNote { get; set; }
    }
}
