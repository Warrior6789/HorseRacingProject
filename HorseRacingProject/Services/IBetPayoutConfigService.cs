using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IBetPayoutConfigService
    {
        Task<BetPayoutConfigResponse> GetActiveConfigAsync();
        Task<PagedResponse<BetPayoutConfigResponse>> GetAllPagedAsync(int page, int pageSize);
        Task<BetPayoutConfigResponse> CreateAsync(CreateBetPayoutConfigRequest req);
        Task SetActiveAsync(Guid id);
    }
}
