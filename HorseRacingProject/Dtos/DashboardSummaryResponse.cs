namespace HorseRacingAPI.Dtos
{
    public class DashboardFinancialResponse
    {
        public FinancialSummaryResponse Financial { get; set; } = new();
        public List<DepositPoint> DepositsByPeriod { get; set; } = new();
        public List<TransactionPeriodPoint> TransactionsByPeriod { get; set; } = new();
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

    public class TransactionPeriodPoint
    {
        public DateTimeOffset Timestamp { get; set; }
        public decimal Deposit { get; set; }
        public decimal Withdrawal { get; set; }
        public decimal BetPayout { get; set; }
        public decimal PrizePayout { get; set; }
    }

    public class RaceStatusBreakdownResponse
    {
        public int TotalRaces { get; set; }
        public List<RaceStatusCount> ByStatus { get; set; } = new();
    }

    public class RaceStatusCount
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
