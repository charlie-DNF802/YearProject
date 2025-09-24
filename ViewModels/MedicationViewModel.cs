namespace Ward_Management_System.ViewModels
{
    public class MedicationViewModel
    {
        public int PrescribedMedicationId { get; set; }
        public int MedId { get; set; }
        public string MedicationName { get; set; }
        public string Dosage { get; set; }
        public string Frequency { get; set; }
        public string Duration { get; set; }
        public string Notes { get; set; }
    }
}
