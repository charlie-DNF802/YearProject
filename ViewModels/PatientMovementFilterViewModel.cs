using Ward_Management_System.Models;

namespace Ward_Management_System.ViewModels
{
    public class PatientMovementFilterViewModel
    {
        public string PatientName { get; set; } = "";
        public int? WardId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public List<PatientMovementHistoryViewModel> Movements { get; set; } = new();
        public List<Wards> AvailableWards { get; set; } = new();
    }
}
