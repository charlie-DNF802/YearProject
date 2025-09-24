namespace Ward_Management_System.DTOs
{
    public class MedicationDto
    {
        public int MedicationId { get; set; }
        public bool IsDispensed { get; set; }
        public string Dosage { get; set; }
        public string Frequency { get; set; }
        public string Duration { get; set; }
        public string? Notes { get; set; }
        public string MedicationName { get; set; }

    }
}
