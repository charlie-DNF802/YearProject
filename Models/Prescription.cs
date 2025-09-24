using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Ward_Management_System.Models
{
    public class Prescription
    {
        [Key]
        public int PrescriptionId { get; set; }
        public int AppointmentId { get; set; }
        [ForeignKey("AppointmentId")]
        public virtual Appointment Appointment { get; set; }
        public string PrescribedById { get; set; }
        [ForeignKey("PrescribedById")]
        public Users PrescribedBy { get; set; }
        public DateTime PrescribedDate { get; set; }

        public virtual ICollection<PrescribedMedication> Medications { get; set; } = new List<PrescribedMedication>();

    }
}
