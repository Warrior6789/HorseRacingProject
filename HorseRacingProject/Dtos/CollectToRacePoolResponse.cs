namespace HorseRacingAPI.Dtos
{
    public class CollectToRacePoolResponse
    {
        public int ChargedCount { get; set; }
        public int SkippedCount { get; set; }
        public decimal TotalCollected { get; set; }
        public decimal PoolTotalAmount { get; set; }
    }
}
