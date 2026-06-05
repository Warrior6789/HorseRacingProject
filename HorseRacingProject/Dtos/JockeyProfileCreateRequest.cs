namespace HorseRacingAPI.Dtos
{
    public class JockeyProfileCreateRequest
    {
        public Guid AccountId { get; set; }
        public string? FullName { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string? Nationality { get; set; }
        public string? LicenseNumber { get; set; }
    }
}
