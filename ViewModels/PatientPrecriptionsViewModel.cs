using Ward_Management_System.DTOs;
using Ward_Management_System.Models;

namespace Ward_Management_System.ViewModels
{
    public class PatientPrecriptionsViewModel
    {
        public string PatientIdNumber { get; set; }
        public string PatientName { get; set; }
        public List<PrescriptionDto> Prescriptions { get; set; }
        public int TotalItems { get; set; }
        public string Status { get; set; }
    }
}
