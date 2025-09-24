using System.ComponentModel.DataAnnotations;

namespace Ward_Management_System.Models
{
    public class ConsultationRoom
    {
        [Key]
        public int RoomId { get; set; }

        [Required]
        public string RoomName { get; set; }

        public bool IsAvailable { get; set; } = true;
    }
}
