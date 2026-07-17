using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class RequestRoleUpgradeDto
    {
        public string RequestedRole { get; set; } = string.Empty;

        public string? FullName { get; set; }

        [RegularExpression(@"^$|^(0|\+84)[35789]\d{8}$", ErrorMessage = "Phone must be a valid Vietnamese phone number (e.g. 0912345678).")]
        public string? Phone { get; set; }

        public DateOnly? DateOfBirth { get; set; }

        [MaxLength(50)]
        public string? Nationality { get; set; }

        [MaxLength(50)]
        public string? LicenseNumber { get; set; }

        [Range(20, 200, ErrorMessage = "Weight must be between 20 and 200 kg.")]
        public float? Weight { get; set; }

        [Range(100, 250, ErrorMessage = "Height must be between 100 and 250 cm.")]
        public float? Height { get; set; }

        public IFormFile? CertificateImage { get; set; }
    }
}
