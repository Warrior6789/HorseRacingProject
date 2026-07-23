namespace HorseRacingAPI.Dtos
{
    public class DashboardSummaryResponse
    {
        public Dictionary<string, int> RacesByStatus { get; set; } = new();
        public Dictionary<string, int> AccountsByRole { get; set; } = new();
        public Dictionary<string, int> RegistrationsByStatus { get; set; } = new();
        public FinancialSummaryResponse Financial { get; set; } = new();
        public List<DailyRevenuePoint> RevenueByDay { get; set; } = new();
        public List<TopHorseResponse> TopHorses { get; set; } = new();
    }

    public class FinancialSummaryResponse
    {
        public decimal TotalDeposits { get; set; }
        public decimal TotalWithdrawals { get; set; }
        public decimal TotalBetsPlaced { get; set; }
        public decimal TotalBetPayouts { get; set; }
        public decimal TotalPrizePayouts { get; set; }
        public decimal TotalTakeoutRevenue { get; set; }
    }

    public class DailyRevenuePoint
    {
        public DateOnly Date { get; set; }
        public decimal TakeoutAmount { get; set; }
    }

    public class TopHorseResponse
    {
        public Guid HorseId { get; set; }
        public string HorseName { get; set; } = null!;
        public int RecordWins { get; set; }
    }
}
