using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IRaceService
    {
        Task<PagedResponse<RaceResponse>> GetRacesAsync(int page, int pageSize, Guid? racecourseId, string? status, string? search = null);
        Task<RaceResponse> GetRaceByIdAsync(Guid raceId);
        Task<RaceResponse> CreateRaceAsync(CreateRaceRequest request);
        Task<RaceResponse> UpdateRaceAsync(Guid raceId, UpdateRaceRequest request);
        Task DeleteRaceAsync(Guid raceId);
        Task<RegistrationResponse> RegisterHorseAsync(Guid raceId, Guid ownerId, RegisterHorseToRaceRequest request);
        Task<PagedResponse<UpcomingRaceResponse>> GetUpcomingRacesAsync(int page, int pageSize, List<string>? statuses);
Task<List<RaceResultResponse>> GetRaceResultsAsync(Guid raceId);
        Task<List<RaceResultHorseDto>> GetRaceHorsesAsync(Guid raceId);
        Task<List<RegistrationResponse>> GetRaceRegistrationsAsync(Guid raceId);
        Task<RaceResponse> AdvanceRaceStatusAsync(Guid raceId);
        Task ResetRaceAsync(Guid raceId);
        Task<string> UploadImageAsync(Guid raceId, IFormFile file);
        Task<CollectToRacePoolResponse> CollectFromSpectatorsAsync(Guid raceId, CollectToRacePoolRequest request);
    }
}
