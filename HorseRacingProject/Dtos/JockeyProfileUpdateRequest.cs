using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class JockeyProfileUpdateRequest
    {
        [MaxLength(100)]
        public string? FullName { get; set; }

        public DateOnly? DateOfBirth { get; set; }

        [MaxLength(50)]
        public string? Nationality { get; set; }

        [MaxLength(50)]
        public string? LicenseNumber { get; set; }

        [Range(20, 200, ErrorMessage = "Weight must be between 20 and 200 kg.")]
        public float? Weight { get; set; }

        [Range(100, 250, ErrorMessage = "Height must be between 100 and 250 cm.")]
        public float? Height { get; set; }
    }
}
