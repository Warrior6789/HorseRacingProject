using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IHorseService
    {
        Task<PagedResponse<HorseDetailResponse>> GetHorsesAsync(Guid accountId, bool isAdmin, HorseQueryRequest query);
        Task<HorseDetailResponse> GetHorseByIdAsync(Guid horseId, Guid accountId, bool isAdmin);
        Task<HorseDetailResponse> CreateHorseAsync(Guid accountId, bool isAdmin, HorseCreateRequest request);
        Task<HorseDetailResponse> UpdateHorseAsync(Guid horseId, Guid accountId, bool isAdmin, HorseUpdateRequest request);
        Task DeleteHorseAsync(Guid horseId, Guid accountId, bool isAdmin);
        Task<PagedResponse<HorseResponse>> GetActiveHorsesPagedAsync(Guid accountId, bool isAdmin, int page, int pageSize);
        Task<string> UploadImageAsync(Guid horseId, Guid accountId, bool isAdmin, IFormFile file);
        Task<HorsePerformanceSummaryResponse> GetPerformanceSummaryAsync(Guid horseId, Guid accountId, bool isAdmin);
        Task<PagedResponse<OwnerRaceHistoryItemResponse>> GetOwnerRaceHistoryAsync(Guid ownerId, int page, int pageSize);
    }
}
