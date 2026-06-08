using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IDashboardService
    {
        Task<ActiveRunnersResponse> GetActiveRunnersAsync(Guid accountId, bool isAdmin);
        Task<WinRateResponse> GetWinRateAsync(Guid accountId, bool isAdmin);
        Task<RecentRewardsResponse> GetRecentRewardsAsync(Guid accountId, bool isAdmin);
    }
}
