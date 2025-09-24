using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Ward_Management_System.Models
{
    public class Users : IdentityUser
    {
        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Full name can only contain letters and spaces.")]
        public string FullName { get; set; }

        [Range(0, 120, ErrorMessage = "Age must be between 0 and 120.")]
        public int Age { get; set; }

        [Required(ErrorMessage = "Address is required.")]
        [StringLength(250, ErrorMessage = "Address cannot exceed 250 characters.")]
        public string Address { get; set; }

        [Required(ErrorMessage = "ID Number is required.")]
        [RegularExpression(@"^\d{13}$", ErrorMessage = "ID Number must be exactly 13 digits.")]
        public string IdNumber { get; set; }

        [Required(ErrorMessage = "Gender is required.")]
        [RegularExpression(@"^(Male|Female|Other)$", ErrorMessage = "Gender must be Male, Female, or Other.")]
        public string Gender { get; set; }

        public bool IsActive { get; set; } = true;

        public string? ProfileImagePath { get; set; }

        public DateTime DateAdded { get; set; } = DateTime.Now;

        public ICollection<EmergencyContact> EmergencyContacts { get; set; }
    }
}
 