namespace HorseRacingAPI.Dtos
{
    public class RegistrationFeeConfigResponse
    {
        public Guid Id { get; set; }
        public decimal FeeAmount { get; set; }
        public string? Status { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
