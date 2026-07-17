using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class JockeyProfileCreateRequest
    {
        [MaxLength(100)]
        public string? FullName { get; set; }

        public DateOnly? DateOfBirth { get; set; }

        [MaxLength(50)]
        public string? Nationality { get; set; }

        [MaxLength(50)]
        public string? LicenseNumber { get; set; }

        public IFormFile? Image { get; set; }
    }
}
