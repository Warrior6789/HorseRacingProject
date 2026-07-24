namespace HorseRacingAPI.Dtos
{
    public class RefereeReportResponse
    {
        public Guid ReportId { get; set; }
        public Guid RaceId { get; set; }
        public int?  RaceNumber { get; set; }
        public Guid RefereeId { get; set; }
        public string? RefereeName { get; set; } = null!;
        public Guid RegistrationId { get; set; }
        public string HorseName { get; set; } = null!;
        public int? OriginalPosition { get; set; }
        public string IncidentDescription { get; set; } = null!;
        public string? PenaltyApplied { get; set; }
        public string PenaltyType { get; set; } = null!;
        public decimal? FineAmount { get; set; }
        public string Status { get; set; } = null!;
        public DateTimeOffset? CreatedAt { get; set; }
    }
}
