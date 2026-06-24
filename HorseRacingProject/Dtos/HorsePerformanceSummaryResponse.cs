namespace HorseRacingAPI.Dtos
{
    public class HorsePerformanceSummaryResponse
    {
        public Guid HorseId { get; set; }
        public string HorseName { get; set; } = string.Empty;
        public int TotalRaces { get; set; }
        public int TotalWins { get; set; }
        public decimal TotalEarned { get; set; }
    }
}
