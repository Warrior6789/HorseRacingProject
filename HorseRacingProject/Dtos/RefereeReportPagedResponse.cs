namespace HorseRacingAPI.Dtos
{
    public class RefereeReportPagedResponse : PagedResponse<RefereeReportResponse>
    {
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
    }
}
