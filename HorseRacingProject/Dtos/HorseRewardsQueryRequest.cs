namespace HorseRacingAPI.Dtos
{
    public class HorseRewardsQueryRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
