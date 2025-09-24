using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Ward_Management_System.ViewModels
{
    public class PrescriptionItemViewModel
    {
        [Required]
        public int? MedId { get; set; }

        [Required]
        public string Dosage { get; set; }

        [Required]
        public string Frequency { get; set; }

        [Required]
        public string Duration { get; set; }

        public string? Notes { get; set; }
        [ValidateNever]
        public List<SelectListItem> Medications { get; set; }
    }
}
