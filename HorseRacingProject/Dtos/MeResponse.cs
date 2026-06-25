namespace HorseRacingAPI.Dtos
{
    public class MeResponse
    {
        public Guid AccountId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? RequestedRole { get; set; }
    }
}
