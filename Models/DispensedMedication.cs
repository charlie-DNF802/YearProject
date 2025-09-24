using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ward_Management_System.Models
{
    public class DispensedMedication
    {
        [Key]
        public int DispenseId { get; set; }

        [ForeignKey("PrescribedMedication")]
        public int PrescribedMedicationId { get; set; }
        public virtual PrescribedMedication PrescribedMedication { get; set; }

        public string DispensedById { get; set; }
        public DateTime DispensedDate { get; set; }
        public string? Notes { get; set; }
    }
}
