using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IRaceService
    {
        Task<PagedResponse<RaceResponse>> GetRacesAsync(int page, int pageSize, Guid? racecourseId, string? status, string? search = null, string? date = null);
        Task<RaceResponse> GetRaceByIdAsync(Guid raceId);
        Task<RaceResponse> CreateRaceAsync(CreateRaceRequest request);
        Task<RaceResponse> UpdateRaceAsync(Guid raceId, UpdateRaceRequest request);
        Task DeleteRaceAsync(Guid raceId);
        Task<RegistrationResponse> RegisterHorseAsync(Guid raceId, Guid ownerId, RegisterHorseToRaceRequest request);
        Task<PagedResponse<UpcomingRaceResponse>> GetUpcomingRacesAsync(int page, int pageSize, List<string>? statuses);
Task<List<RaceResultResponse>> GetRaceResultsAsync(Guid raceId);
        Task<List<RegistrationResponse>> GetRaceRegistrationsAsync(Guid raceId);
        Task<List<int>> GetTakenGateNumbersAsync(Guid raceId);
        Task<RaceResponse> AdvanceRaceStatusAsync(Guid raceId);
        Task<string> UploadImageAsync(Guid raceId, IFormFile file);
        Task<RacePoolOverviewResponse> GetRacePoolOverviewAsync(Guid raceId);
        Task<RacePrizePreviewResponse> GetPrizePreviewAsync(Guid raceId);
        Task<TakeoutLedgerPagedResponse> GetTakeoutLedgerPagedAsync(int page, int pageSize, Guid? raceId = null, string? betType = null);
    }
}
