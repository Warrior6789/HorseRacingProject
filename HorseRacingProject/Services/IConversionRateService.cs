using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IConversionRateService
    {
        public Task<ConversionRateResponse> GetActiveRateAsync();
        Task<PagedResponse<ConversionRateResponse>> GetAllAsyncPage(int page, int pageSize);
        Task<ConversionRateResponse> CreateAsync(CreateConversionRateRequest req);
        Task SetActiveAsync(Guid id);
    }
}
