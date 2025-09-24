using System.ComponentModel.DataAnnotations;

namespace Ward_Management_System.ViewModels
{
    public class VerifyEmailViewModel
    {
         [Required(ErrorMessage = "Email is requires!.")]
        [EmailAddress]
        public string Email { get; set; }
    }
}
