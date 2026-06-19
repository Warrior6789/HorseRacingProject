using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface ITakeoutConfigService
    {
        Task<TakeoutConfigResponse> CreateAsync(CreateTakeoutConfigRequest req);
        Task<TakeoutConfigResponse> GetActiveConfigAsync();
        Task<PagedResponse<TakeoutConfigResponse>> GetAllPagedAsync(int page, int pageSize);
        Task SetActiveAsync(Guid id);
    }
}
