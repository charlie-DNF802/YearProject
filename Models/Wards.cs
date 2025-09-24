using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ward_Management_System.Models
{
    public class Wards
    {
        [Key]
        public int WardId { get; set; }

        [Required]
        public string WardName { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Total bed capacity must be a positive number.")]
        public int TotalBedCapacty { get; set; } // Max beds the room can hold

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Current bed count must be a positive number.")]
        public int CurrentBedCount { get; set; } // Beds currently in the room

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Occupied beds must be a positive number.")]
        public int OccupiedBeds { get; set; } // Beds currently occupied

        [NotMapped]
        public int AvailableBedCount => CurrentBedCount - OccupiedBeds;

        // Custom validation logic
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (CurrentBedCount > TotalBedCapacty)
            {
                yield return new ValidationResult(
                    "Current bed count cannot exceed the total bed capacity.",
                    new[] { nameof(CurrentBedCount) });
            }

            if (OccupiedBeds > CurrentBedCount)
            {
                yield return new ValidationResult(
                    "Occupied beds cannot exceed the current bed count.",
                    new[] { nameof(OccupiedBeds) });
            }
        }
    }
}
