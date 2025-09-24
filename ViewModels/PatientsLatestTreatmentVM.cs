namespace Ward_Management_System.ViewModels
{
    public class PatientsLatestTreatmentVM
    {
        public int AppointmentId { get; set; }
        public string FullName { get; set; }
        public string IdNumber { get; set; }
        public int Age { get; set; }
        public string Gender { get; set; }

        public string Procedure { get; set; }
        public string Notes { get; set; }
        public DateTime? TreatmentDate { get; set; }
        public string DoctorName { get; set; }
    }
}
