namespace Ward_Management_System.Models
{
    public class HospitalInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string ContactNumber { get; set; }
        public string Email { get; set; }
        public string Website { get; set; }

        // Navigation property: One Hospital can have many departments
        public virtual ICollection<Departments> Departments { get; set; }
        public string OperatingHours { get; set; }
    }
}
