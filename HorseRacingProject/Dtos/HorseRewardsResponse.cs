namespace HorseRacingAPI.Dtos
{
    public class HorseRewardsResponse
    {
        public Guid HorseId { get; set; }
        public string HorseName { get; set; } = string.Empty;
        public decimal TotalRewardAmount { get; set; }
        public int RewardCount { get; set; }
        public PagedResponse<HorseRewardItemResponse> Rewards { get; set; } = new();
    }
}
