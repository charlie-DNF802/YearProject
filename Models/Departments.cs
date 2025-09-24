using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ward_Management_System.Models
{
    public class Departments
    {
        [Key]
        public int DepartmentID { get; set; }

        [Required]
        [StringLength(50, ErrorMessage = "Name cannot exceed 50 characters.")]
        public string DepartmentName { get; set; }

        //Foreign key to hospitalinfo
        [Required]
        [ForeignKey("HospitalInfo")]
        public int Id { get; set; }
        public virtual HospitalInfo HospitalInfo { get; set; }
    }
}
