using System.ComponentModel.DataAnnotations;

namespace Ward_Management_System.DTOs
{
    public class MovementRecord
    {
        public string LocationName { get; set; }
        public DateTime MovementTime { get; set; }
        [Required(ErrorMessage = "Notes are required")]
        public string Notes { get; set; }
    }
}
