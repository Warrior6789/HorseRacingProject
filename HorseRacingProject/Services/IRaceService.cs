using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IRaceService
    {
        Task<PagedResponse<RaceResponse>> GetRacesAsync(int page, int pageSize, Guid? tournamentId, Guid? racecourseId, string? status);
        Task<RaceResponse> GetRaceByIdAsync(Guid raceId);
        Task<List<RaceResponse>> GetRacesByTournamentAsync(Guid tournamentId);
        Task<RaceResponse> CreateRaceAsync(CreateRaceRequest request);
        Task<RaceResponse> UpdateRaceAsync(Guid raceId, UpdateRaceRequest request);
        Task DeleteRaceAsync(Guid raceId);
        Task<RegistrationResponse> RegisterHorseAsync(Guid raceId, Guid ownerId, RegisterHorseToRaceRequest request);
        Task<PagedResponse<UpcomingRaceResponse>> GetUpcomingRacesAsync(int page, int pageSize, List<string>? statuses);
Task<List<RaceResultResponse>> GetRaceResultsAsync(Guid raceId);
        Task<List<RaceResultHorseDto>> GetRaceHorsesAsync(Guid raceId);
        Task<RaceResponse> AdvanceRaceStatusAsync(Guid raceId);
    }
}
