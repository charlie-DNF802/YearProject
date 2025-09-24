namespace Ward_Management_System.ViewModels
{
    public class AppointmentBookingDetailsVM
    {
        public string DoctorId { get; set; }
        public DateTime PreferredDate { get; set; }
        public TimeSpan PreferredTime { get; set; }
        public int? ConsultationRoomId { get; set; }

        // Additional user details
        public string BookingFor { get; set; }
        public string OtherPersonName { get; set; }
        public int? OtherPersonAge { get; set; }
        public string OtherPersonGender { get; set; }
        public string OtherPersonIdNumber { get; set; }
        public string Reason { get; set; }
        public string FullName { get; set; }
        public int Age { get; set; }
        public string Gender { get; set; }
        public string IdNumber { get; set; }
        public string UserId { get; set; }
    }
}
