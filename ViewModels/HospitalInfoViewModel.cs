namespace Ward_Management_System.ViewModels
{
    public class HospitalInfoViewModel
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string ContactNumber { get; set; }
        public string Email { get; set; }
        public string Website { get; set; }
        public List<string> Departments { get; set; }
        public string OperatingHours { get; set; }
    }
}
