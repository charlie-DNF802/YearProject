using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ward_Management_System.Models
{
    public class PrescribedMedication
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Prescription")]
        public int PrescriptionId { get; set; }
        public virtual Prescription Prescription { get; set; }

        [ForeignKey("StockMedications")]
        public int MedId { get; set; }
        public virtual StockMedications StockMedications { get; set; }
        public string Dosage { get; set; }
        public string Frequency { get; set; }
        public string Duration { get; set; }
        public string? Notes { get; set; }
        public bool IsDispensed { get; set; } = false;


    }
}
