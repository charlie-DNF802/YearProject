namespace Ward_Management_System.ViewModels
{
    public class PatientMovementHistoryViewModel
    {
        public int AdmissionId { get; set; }
        public string PatientName { get; set; }
        public string IdNumber { get; set; }
        public string WardName { get; set; }
        public DateTime MovementTime { get; set; }
        public string Notes { get; set; }
    }
}
