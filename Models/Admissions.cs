using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ward_Management_System.Models
{
    public class Admissions
    {
        [Key]
        public int AdmissionId { get; set; }

        [Required]
        public string PatientName { get; set; }

        [Required]
        public DateTime AdmissionDate { get; set; } = DateTime.Now;

        [Required]
        public string Allergies { get; set; }

        [Required]
        public string Condition { get; set; }

        public string UserId { get; set; } // Foreign Key to AspNetUsers

        [ForeignKey("UserId")]
        public Users User { get; set; } // Navigation property

        [ForeignKey("Wards")]
        public int WardId { get; set; }
        public virtual Wards Wards { get; set; }

        [ForeignKey("Appointment")]
        public int AppointmentId { get; set; }  // foreign key to appointments

        public virtual Appointment Appointment { get; set; }

        [Required]
        public string Status { get; set; } = "Admitted";
        public string FolderStatus { get; set; } = "No Folder";
    }
}
