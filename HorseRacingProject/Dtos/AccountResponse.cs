namespace HorseRacingAPI.Dtos
{
    public class AccountResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset? CreateAt { get; set; }
    }
}
