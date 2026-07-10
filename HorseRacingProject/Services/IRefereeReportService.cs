using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IRefereeReportService
    {
        Task<RefereeReportResponse> CreateReportAsync(Guid refereeId, CreateRefereeReportDto dto);
        Task<RefereeReportResponse> ApproveReportAsync(Guid reportId);
        Task<RefereeReportResponse> RejectReportAsync(Guid reportId);
        Task<RefereeReportPagedResponse> GetReportsByRaceAsync(Guid? raceId, int page, int pageSize, Guid? refereeId = null);
        Task<PagedResponse<RefereeReportResponse>> GetMyReportsPagedAsync(Guid refereeId, int page, int pageSize);
        Task<RefereeReportResponse> UpdateReportAsync(Guid reportId, Guid refereeId, UpdateRefereeReportDto dto);
    }
}
