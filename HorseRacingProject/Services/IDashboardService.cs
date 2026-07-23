using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IDashboardService
    {
        Task<DashboardSummaryResponse> GetSummaryAsync(DateTimeOffset? from, DateTimeOffset? to);
    }
}
