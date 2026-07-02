namespace HorseRacingAPI.Dtos
{
    public class TakeoutLedgerResponse
    {
        public Guid TakeoutLedgerId { get; set; }
        public Guid RaceId { get; set; }
        public string? RaceName { get; set; }
        public int? RaceNumber { get; set; }
        public string BetType { get; set; } = string.Empty;
        public decimal TotalPool { get; set; }
        public float TakeoutPercentage { get; set; }
        public decimal TakeoutAmount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
