
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ward_Management_System.Models
{
    public class Appointment
    {
        [Key]
        public int AppointmentId { get; set; }

        [Required]
        public string UserId { get; set; } // Foreign Key to AspNetUsers

        [ForeignKey("UserId")]
        public Users User { get; set; } // Navigation property
        [Required]
        public string DoctorId { get; set; }

        [ForeignKey("DoctorId")]
        public Users Doctor { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime PreferredDate { get; set; }

        [Required]
        [DataType(DataType.Time)]
        public TimeSpan PreferredTime { get; set; }

        [Required]
        public string Reason { get; set; }
        public int? ConsultationRoomId { get; set; }
        [ForeignKey("ConsultationRoomId")]
        public ConsultationRoom ConsultationRoom { get; set; }

        public DateTime DateBooked { get; set; } = DateTime.Now;

        public string Status { get; set; } = "Pending";
        public string FullName { get; set; }
        public int Age { get; set; }
        public string Gender { get; set; }
        public string IdNumber { get; set; }
        public ICollection<Treatment> Treatments { get; set; }
    }
}
