using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IJockeyProfileService
    {
        Task<JockeyProfileResponse> CreateJockeyProfileAsync(Guid accountId, JockeyProfileCreateRequest req);
        Task<JockeyProfileResponse> GetJockeyProfileByAccountIdAsync(Guid accountId);
        Task<List<JockeyProfileResponse>> GetAllJockeyProfilesAsync();
        Task<PagedResponse<JockeyProfileResponse>> GetAllJockeyProfilesPagedAsync(int page, int pageSize);
        Task UpdateJockeyProfileAsync(Guid accountId, JockeyProfileUpdateRequest req);
        Task<string> UploadImageAsync(Guid accountId, IFormFile file);
        Task<JockeyRewardsResponse> GetJockeyRewardsAsync(Guid accountId, int page, int pageSize);
    }
}
