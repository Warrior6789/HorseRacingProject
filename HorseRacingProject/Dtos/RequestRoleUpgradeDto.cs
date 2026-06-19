namespace HorseRacingAPI.Dtos
{
    public class RequestRoleUpgradeDto
    {
        public string RequestedRole { get; set; } = string.Empty;

        // HorseOwner & Referee
        public string? FullName { get; set; }
        public string? Phone { get; set; }

        // Jockey only
        public DateOnly? DateOfBirth { get; set; }
        public string? Nationality { get; set; }
        public string? LicenseNumber { get; set; }
        public float? Weight { get; set; }
        public float? Height { get; set; }

        // Ảnh chứng chỉ (tất cả role)
        public IFormFile? CertificateImage { get; set; }
    }
}
