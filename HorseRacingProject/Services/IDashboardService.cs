using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IDashboardService
    {
        Task<DashboardFinancialResponse> GetFinancialAsync(DateTimeOffset? from, DateTimeOffset? to, string bucket);
        Task<RaceStatusBreakdownResponse> GetRaceStatusBreakdownAsync();
        Task<BetTypeBreakdownResponse> GetBetTypeBreakdownAsync();
    }
}
