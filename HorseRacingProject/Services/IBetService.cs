using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IBetService
    {
        Task<BetResponse> PlaceBetAsync(Guid spectatorId, PlaceBetRequest req);
        Task<PagedResponse<BetResponse>> GetMyBetsPagedAsync(Guid spectatorId, int page, int pageSize, string? status = null);
    }
}
