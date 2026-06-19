namespace HorseRacingAPI.Dtos
{
    public class TakeoutConfigResponse
    {
        public Guid Id { get; set; }
        public float TakeoutPercentage { get; set; }
        public string? Status { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
