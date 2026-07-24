namespace HorseRacingAPI.Dtos
{
    public class JockeyRewardsResponse
    {
        public Guid JockeyAccountId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public decimal TotalRewardAmount { get; set; }
        public int RewardCount { get; set; }
        public PagedResponse<HorseRewardItemResponse> Rewards { get; set; } = new();
    }
}
