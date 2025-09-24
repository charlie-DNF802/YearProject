using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ward_Management_System.Models
{
    public class DischargeInformation
    {
        [Key]
        public int DischargeId { get; set; }

        [ForeignKey("Appointment")]
        public int AppointmentId { get; set; }
        public virtual Appointment Appointment { get; set; }

        [Required]
        public string DischargedById { get; set; }

        public virtual Users DischargedBy { get; set; }

        public string? DischargeSummary { get; set; }

        [Required]
        public DateTime DischargeDate { get; set; }
    }
}
