namespace HorseRacingAPI.Dtos
{
    public class BetResponse
    {
        public Guid BetId { get; set; }
        public Guid SpectatorId { get; set; }
        public Guid RegistrationId { get; set; }
        public Guid RaceId { get; set; }
        public string? HorseName { get; set; }
        public int? RaceNumber { get; set; }
        public string? RaceName { get; set; }
        public decimal BetAmount { get; set; }
        public string? BetType { get; set; }
        public float? PayoutRatio { get; set; }
        public decimal? EstimatedPayout { get; set; }
        public string? Status { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }
    }
}
