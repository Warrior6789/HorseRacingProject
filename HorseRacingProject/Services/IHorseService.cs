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
        Task<List<HorseResponse>> GetActiveHorsesAsync(Guid accountId, bool isAdmin);
        Task<List<HorseScheduleResponse>> GetMyScheduleAsync(Guid ownerId);
        Task<List<HorseScheduleResponse>> GetHorseScheduleAsync(Guid horseId, Guid accountId, bool isAdmin);
        Task<HorseRewardsResponse> GetHorseRewardsAsync(Guid horseId, Guid accountId, bool isAdmin, HorseRewardsQueryRequest query);
        Task<string> UploadImageAsync(Guid horseId, Guid accountId, bool isAdmin, IFormFile file);
    }
}
