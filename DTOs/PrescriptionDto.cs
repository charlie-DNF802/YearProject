namespace Ward_Management_System.DTOs
{
    public class PrescriptionDto
    {
        public int PrescriptionId { get; set; }
        public DateTime PrescribedDate { get; set; }
        public string PrescribedBy { get; set; }
        public List<MedicationDto> Medications { get; set; }
    }

}
