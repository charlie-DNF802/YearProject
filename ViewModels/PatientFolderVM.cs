namespace Ward_Management_System.ViewModels
{
    public class PatientFolderVM
    {
        public int AppointmentId { get; set; }

        public string FullName { get; set; }

        public int Age { get; set; }

        public string Gender { get; set; }

        public string IdNumber { get; set; }

        public string ProfileImagePath { get; set; }
        public string Status { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }

        // showS the default image if ProfileImagePath is null or empty
        public string DisplayImage
        {
            get
            {
                return string.IsNullOrEmpty(ProfileImagePath)
                    ? "/images/placeholder.jpg"
                    : ProfileImagePath;
            }
        }
    }
}
