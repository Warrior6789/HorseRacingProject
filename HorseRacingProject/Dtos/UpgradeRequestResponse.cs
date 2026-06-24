namespace HorseRacingAPI.Dtos
{
    public class UpgradeRequestResponse
    {
        public Guid AccountId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string CurrentRole { get; set; } = string.Empty;
        public string RequestedRole { get; set; } = string.Empty;
        public DateTimeOffset? RequestedAt { get; set; }

        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? AvatarUrl { get; set; }
        public string? CertificateImageUrl { get; set; }

        public DateOnly? DateOfBirth { get; set; }
        public string? Nationality { get; set; }
        public string? LicenseNumber { get; set; }
        public float? Weight { get; set; }
        public float? Height { get; set; }
    }
}
