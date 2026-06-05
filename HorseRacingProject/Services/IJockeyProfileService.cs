using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IJockeyProfileService
    {
        Task<JockeyProfileResponse> CreateJockeyProfileAsync(JockeyProfileCreateRequest req);
        Task<JockeyProfileResponse> GetJockeyProfileByAccountIdAsync(Guid accountId);
        Task<List<JockeyProfileResponse>> GetAllJockeyProfilesAsync();
        Task UpdateJockeyProfileAsync(Guid accountId, JockeyProfileUpdateRequest req);
    }
}
