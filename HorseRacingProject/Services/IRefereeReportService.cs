using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IRefereeReportService
    {
        Task<RefereeReportResponse> CreateReportAsync(Guid refereeId, CreateRefereeReportDto dto);
        Task<RefereeReportResponse> ApproveReportAsync(Guid reportId);
        Task<RefereeReportResponse> RejectReportAsync(Guid reportId);
        Task<PagedResponse<RefereeReportResponse>> GetReportsByRaceAsync(Guid raceId, int page, int pageSize, Guid? refereeId = null);
    }
}
