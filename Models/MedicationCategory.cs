using System.ComponentModel.DataAnnotations;

namespace Ward_Management_System.Models
{
    public class MedicationCategory
    {
        [Key]
        public int ID { get; set; }
        [Required] 
        public string Name { get; set; }

        // Navigation property: One Category can have many medications
        public virtual ICollection<StockMedications> StockMedications { get; set; }
    }
}
