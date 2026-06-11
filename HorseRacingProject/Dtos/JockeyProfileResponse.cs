namespace HorseRacingAPI.Dtos
{
    public class JockeyProfileResponse
    {
        public Guid JockeyProfileId { get; set; }
        public Guid AccountId { get; set; }
        public string? FullName { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string? Nationality { get; set; }
        public string? LicenseNumber { get; set; }
        public int? TotalRaces { get; set; }
        public int? TotalWins { get; set; }
        public string? ImageUrl { get; set; }
        public DateTimeOffset? CreateAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
