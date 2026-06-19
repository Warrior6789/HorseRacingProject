using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IPositionPrizeConfigService
    {
        Task<PositionPrizeConfigResponse> CreateAsync(CreatePositionPrizeConfigRequest req);
        Task<PositionPrizeConfigResponse> GetActiveAsync();
        Task<PagedResponse<PositionPrizeConfigResponse>> GetAllPagedAsync(int page, int pageSize);
        Task SetActiveAsync(Guid id);
    }
}
