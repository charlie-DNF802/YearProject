namespace Ward_Management_System.ViewModels
{
    public class UpdatePrescriptionViewModel
    {
        public int PrescriptionId { get; set; }
        public string PatientName { get; set; }
        public List<MedicationViewModel> Medications { get; set; } = new();
    }
}
