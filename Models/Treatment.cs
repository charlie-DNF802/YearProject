using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Ward_Management_System.Models
{
    public class Treatment
    {
        public int TreatmentId { get; set; }

        [ForeignKey("Appointment")]
        [Required(ErrorMessage = "Please select a patient.")]
        public int AppointmentId { get; set; }
        [Required(ErrorMessage = "Please select a procedure.")]
        public string Procedure { get; set; }
        public string Notes { get; set; }
        public DateTime TreatmentDate { get; set; }
        public string? RecordedById { get; set; }
        [ForeignKey("RecordedById")]
        public Users? RecordedBy { get; set; }
        public bool IsViewed { get; set; } = false;
        // navigation property
        [ValidateNever]
        public virtual Appointment Appointment { get; set; }
    }
}
