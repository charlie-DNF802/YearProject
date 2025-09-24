namespace Ward_Management_System.ViewModels
{
    public class PersonalDetailsViewModel
    {
        // User details
        public string FullName { get; set; }
        public int Age
        {
            get
            {
                if (string.IsNullOrEmpty(IdNumber) || IdNumber.Length < 6)
                    return 0;

                try
                {
                    var yearPart = int.Parse(IdNumber.Substring(0, 2));
                    var monthPart = int.Parse(IdNumber.Substring(2, 2));
                    var dayPart = int.Parse(IdNumber.Substring(4, 2));

                    var currentYear = DateTime.Now.Year % 100;
                    var century = (yearPart <= currentYear) ? 2000 : 1900;

                    var birthDate = new DateTime(century + yearPart, monthPart, dayPart);

                    var age = DateTime.Today.Year - birthDate.Year;
                    if (birthDate.Date > DateTime.Today.AddYears(-age))
                        age--;

                    return age;
                }
                catch
                {
                    return 0;
                }
            }
        }
        public string IdNumber { get; set; }
        public string Address { get; set; }
        public string Gender { get; set; }
        public DateTime DateAdded { get; set; }
        public string? ProfileImagePath { get; set; }


        // Emergency Contact 1
        public string EmergencyName1 { get; set; }
        public string EmergencyPhone1 { get; set; }
        public string EmergencyEmail1 { get; set; }
        public string EmergencyRelationship1 { get; set; }

        // Emergency Contact 2 (optional)
        public string? EmergencyName2 { get; set; }
        public string? EmergencyPhone2 { get; set; }
        public string? EmergencyEmail2 { get; set; }
        public string? EmergencyRelationship2 { get; set; }
    }
}
