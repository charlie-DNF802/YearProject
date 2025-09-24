using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ward_Management_System.Models
{
    public class StockMedications
    {
        [Key]
        public int MedId {  get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Brand { get; set; }

        [Required]
        public int QuantityAvailable { get; set; }

        [Required]
        public string BatchNumber { get; set; }

        [Required]
        public string Storage {  get; set; }

        public bool IsActive { get; set; } = true;
        public int Schedule { get; set; }

        //Foreign key to hospitalinfo
        [Required]
        [ForeignKey("MedicationCategory")]
        public int ID { get; set; }
        public virtual MedicationCategory MedicationCategory { get; set; }
    }
}
