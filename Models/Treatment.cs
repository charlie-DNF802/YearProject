using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Ward_Management_System.Models
{
    public class Treatment
    {
        public int TreatmentId { get; set; }
        public int AppointmentId { get; set; }
        public string Procedure { get; set; }
        public string Notes { get; set; }
        public DateTime TreatmentDate { get; set; }
        public string? RecordedById { get; set; }
        [ForeignKey("RecordedById")]
        public Users? RecordedBy { get; set; }
    }
}
