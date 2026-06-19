using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IJockeyRewardConfigService
    {
        Task<JockeyRewardConfigResponse> CreateAsync(CreateJockeyRewardConfigRequest req);
        Task<JockeyRewardConfigResponse> GetActiveAsync();
        Task<PagedResponse<JockeyRewardConfigResponse>> GetAllPagedAsync(int page, int pageSize);
        Task SetActiveAsync(Guid id);
    }
}
