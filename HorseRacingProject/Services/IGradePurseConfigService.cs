using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IGradePurseConfigService
    {
        Task<GradePurseConfigResponse> CreateAsync(CreateGradePurseConfigRequest req);
        Task<GradePurseConfigResponse> GetActiveAsync();
        Task<PagedResponse<GradePurseConfigResponse>> GetAllPagedAsync(int page, int pageSize);
        Task SetActiveAsync(Guid id);
    }
}
