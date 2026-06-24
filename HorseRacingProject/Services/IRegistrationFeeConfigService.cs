using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IRegistrationFeeConfigService
    {
        Task<RegistrationFeeConfigResponse> CreateAsync(CreateRegistrationFeeConfigRequest req);
        Task<RegistrationFeeConfigResponse> GetActiveAsync();
        Task<PagedResponse<RegistrationFeeConfigResponse>> GetAllPagedAsync(int page, int pageSize);
        Task SetActiveAsync(Guid id);
    }
}
