namespace HorseRacingAPI.Dtos
{
    public class ActiveRunnersResponse
    {
        public int ActiveRunners { get; set; }
        public int ActiveRegistrations { get; set; }
        public int TotalHorses { get; set; }
    }

    public class WinRateResponse
    {
        public int TotalRaces { get; set; }
        public int TotalWins { get; set; }
        public double WinRate { get; set; }
    }

    public class RecentRewardsResponse
    {
        public decimal TotalRewardAmount { get; set; }
        public int RewardCount { get; set; }
        public List<RecentRewardItemResponse> RecentRewards { get; set; } = new();
    }

    public class RecentRewardItemResponse
    {
        public Guid PrizeId { get; set; }
        public Guid RegistrationId { get; set; }
        public Guid HorseId { get; set; }
        public string HorseName { get; set; } = string.Empty;
        public string? PrizeType { get; set; }
        public decimal? Amount { get; set; }
        public DateTimeOffset? DistributedAt { get; set; }
    }
}
