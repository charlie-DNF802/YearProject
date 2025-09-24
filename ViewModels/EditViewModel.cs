using System.ComponentModel.DataAnnotations;

namespace Ward_Management_System.ViewModels
{
    public class EditViewModel
    {
        public string Id { get; set; }

        [Required(ErrorMessage = "Full name is required.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        public string Email { get; set; }

        public string Role { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Default Password")]
        public string? DefaultPassword { get; set; }

        [Range(18, 100, ErrorMessage = "Age must be between 18 and 100.")]
        public int Age { get; set; }

        public string Address { get; set; }

        public string PhoneNumber { get; set; }

        public string IdNumber { get; set; }

        public string Gender { get; set; }
    }
}

