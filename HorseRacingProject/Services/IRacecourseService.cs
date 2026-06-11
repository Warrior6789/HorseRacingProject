using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IRacecourseService
    {
        Task<List<RacecourseResponse>> GetAllRacecoursesAsync();
        Task<PagedResponse<RacecourseResponse>> GetAllRacecoursesPagingAsync(int page, int pageSize);
        Task<RacecourseResponse> GetRacecourseByIdAsync(Guid id);
        Task<RacecourseResponse> CreateRacecourseAsync(CreateRacecourseRequest request);
        Task<RacecourseResponse> UpdateRacecourseAsync(Guid id, UpdateRacecourseRequest request);
        Task<bool> DeleteRacecourseAsync(Guid id);
    }
}
