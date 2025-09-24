using System.ComponentModel.DataAnnotations;

namespace Ward_Management_System.ViewModels
{
    public class DoctorTimeSelectionVM
    {
        [Required]
        public string DoctorId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime PreferredDate { get; set; }

        [Required]
        [DataType(DataType.Time)]
        public TimeSpan PreferredTime { get; set; }
    }
}
