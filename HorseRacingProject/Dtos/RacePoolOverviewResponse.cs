namespace HorseRacingAPI.Dtos
{
    public class RacePoolBetItemResponse
    {
        public Guid BetId { get; set; }
        public Guid SpectatorId { get; set; }
        public string? SpectatorName { get; set; }
        public Guid RegistrationId { get; set; }
        public Guid HorseId { get; set; }
        public string? HorseName { get; set; }
        public string? BetType { get; set; }
        public decimal BetAmount { get; set; }
        public string? Status { get; set; }
        public float? PayoutRatio { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }
    }

    public class RacePoolTypeSummaryResponse
    {
        public string BetType { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int BetCount { get; set; }
    }

    public class RacePoolOverviewResponse
    {
        public Guid RaceId { get; set; }
        public decimal TotalPoolAmount { get; set; }
        public List<RacePoolTypeSummaryResponse> Pools { get; set; } = new();
        public List<RacePoolBetItemResponse> Bets { get; set; } = new();
    }
}
