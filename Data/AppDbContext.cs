using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ward_Management_System.Models;

namespace Ward_Management_System.Data
{
    public class AppDbContext : IdentityDbContext<Users>
    {
        public AppDbContext(DbContextOptions options) : base(options)
        {
        }
        //public DbSet<Users> User { get; set; }
        public DbSet<Admissions> Admissions { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Departments> departments { get; set; }
        public DbSet<DischargeInformation> DischargeInformation { get; set; }
        public DbSet<DispensedMedication> DispensedMedications { get; set; }
        public DbSet<EmergencyContact> EmergencyContacts { get; set; }
        public DbSet<HospitalInfo> HospitalInfo { get; set; }
        public DbSet<MedicationCategory> MedicationCategory { get; set; }
        public DbSet<PatientFolder> PatientFolder { get; set; }
        public DbSet<PatientVitals> PatientVitals { get; set; }
        public DbSet<PrescribedMedication> PrescribedMedications { get; set; }
        public DbSet<Prescription> Prescriptions { get; set; }
        public DbSet<StockMedications> StockMedications { get; set; }
        public DbSet<Treatment> Treatment { get; set; }
        public DbSet<Wards> wards {  get; set; } 
        public DbSet<PatientMovement> PatientMovements { get; set; }
        public DbSet<ConsultationRoom> ConsultationRooms { get; set; }

    }
}
