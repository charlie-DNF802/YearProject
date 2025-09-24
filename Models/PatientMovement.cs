using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ward_Management_System.Models
{
    public class PatientMovement
    {
        [Key]
        public int MovementID { get; set; }

        [ForeignKey("Admission")]
        public int AdmissionId { get; set; }
        public Admissions Admission { get; set; }

        [Required(ErrorMessage = "Please select a ward.")]
        [ForeignKey("Ward")]
        public int WardId { get; set; }
        public Wards Ward { get; set; }

        public DateTime MovementTime { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Please provide a reason or notes.")]
        [MaxLength(255)]
        public string Notes { get; set; }
    }

}
