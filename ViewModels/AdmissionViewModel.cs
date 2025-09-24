namespace Ward_Management_System.ViewModels
{
    public class AdmissionViewModel
    {
        public int AdmissionId { get; set; }
        public int AppointmentId { get; set; }
        public int? FolderId { get; set; }
        public string PatientName { get; set; }
        public string IdNumber { get; set; }
        public DateTime? AdmissionDate { get; set; }
        public string FolderStatus { get; set; }
        public string Condition { get; set; }
        public string WardName { get; set; }
        public string Status { get; set; }
    }
}
