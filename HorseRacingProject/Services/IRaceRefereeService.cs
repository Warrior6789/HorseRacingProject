using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IRaceRefereeService
    {
        Task<RaceRefereeResponse> AssignAsync(Guid raceId, Guid refereeId);
        Task UnassignAsync(Guid raceId);
        Task<RaceRefereeResponse?> GetByRaceAsync(Guid raceId);
    }
}
