namespace HorseRacingAPI.Dtos
{
    public class JockeyProfileUpdateRequest
    {
        public string? FullName { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string? Nationality { get; set; }
        public string? LicenseNumber { get; set; }
    }
}
