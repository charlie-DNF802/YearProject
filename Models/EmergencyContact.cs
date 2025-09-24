using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Ward_Management_System.Models
{
    public class EmergencyContact
    {
        [Key]
        public int EmergencyID { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Name can only contain letters and spaces.")]
        public string EmergencyName { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be exactly 10 digits.")]
        public string EmergencyPhone { get; set; }

        [Required(ErrorMessage = "Email is required!")]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z]+\.[a-zA-Z]{2,}$",
         ErrorMessage = "Invalid email format. Domain must not contain numbers.")]
        public string EmergencyEmail { get; set; }

        [Required(ErrorMessage = "Relationship is required.")]
        [StringLength(50, ErrorMessage = "Relationship cannot exceed 50 characters.")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Relationship can only contain letters and spaces.")]
        public string EmergencyRelationship { get; set; }

        // Navigation Property
        [ValidateNever]
        public Users User { get; set; }

        // Foreign Key
        [Required]
        public string UserId { get; set; }
    }
}
