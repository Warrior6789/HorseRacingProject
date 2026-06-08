using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Services
{
    public interface ITournamentService
    {
        Task<List<TournamentResponse>> GetAllTournamentsAsync();
        Task<TournamentResponse> GetTournamentsByIdAsync(Guid id);
        Task<PagedResponse<TournamentResponse>> GetAllTournamentsPagingAsync(int page, int pageSize);
        Task<TournamentResponse> CreateTournamentAsync(CreateTournamentRequest request);
        Task<TournamentResponse> UpdateTournamentAsync(Guid tournamentId, UpdateTournamentRequest request);
        Task<bool> DeleteTournamentAsync(Guid tournamentId);
        Task<bool> ChangeTournamentStatusAsync(Guid tournamentId, TournamentStatus newStatus);
    }
}
