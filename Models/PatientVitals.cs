using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ward_Management_System.Models
{
    public class PatientVitals
    {
        [Key]
        public int VitalsId { get; set; }

        [ForeignKey("PatientFolder")]
        public int FolderId { get; set; }
        //public virtual PatientFolder PatientFolder { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime VitalsDate { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Notes are required")]
        [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
        public string Notes { get; set; }

        [Required(ErrorMessage = "Blood type is required")]
        [RegularExpression(@"^(A|B|AB|O)[+-]$", ErrorMessage = "Blood type must be A+, A-, B+, B-, AB+, AB-, O+, or O-")]
        public string BloodType { get; set; }

        [Required(ErrorMessage = "Blood pressure is required")]
        [RegularExpression(@"^\d{1,3}\/\d{1,3}$", ErrorMessage = "Blood pressure must be in format XXX/XX (e.g., 120/80)")]
        public required string BloodPressure { get; set; }

        [Required(ErrorMessage = "Heart rate is required")]
        [Range(30, 250, ErrorMessage = "Heart rate must be between 30 and 250 bpm")]
        public int HeartRate { get; set; }

        [Required(ErrorMessage = "Temperature is required")]
        [Range(34.0, 46.0, ErrorMessage = "Temperature must be between 34.0°C and 46.0°C")]
        public double Temperature { get; set; }

        [Required(ErrorMessage = "Respiratory rate is required")]
        [Range(8, 60, ErrorMessage = "Respiratory rate must be between 8 and 60 breaths per minute")]
        public int RespiratoryRate { get; set; }

        [Required(ErrorMessage = "Oxygen saturation is required")]
        [Range(70, 100, ErrorMessage = "Oxygen saturation must be between 70% and 100%")]
        public int OxygenSaturation { get; set; }
    }
}
