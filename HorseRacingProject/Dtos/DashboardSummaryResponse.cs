namespace HorseRacingAPI.Dtos
{
    public class DashboardFinancialResponse
    {
        public FinancialSummaryResponse Financial { get; set; } = new();
        public List<DepositPoint> DepositsByPeriod { get; set; } = new();
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

    public class DepositPoint
    {
        public DateTimeOffset Timestamp { get; set; }
        public decimal Amount { get; set; }
    }
}
