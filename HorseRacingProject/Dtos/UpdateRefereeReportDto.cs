using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Dtos;

public class UpdateRefereeReportDto
{
    public string? IncidentDescription { get; set; }
    public string? PenaltyApplied { get; set; }
    public PenaltyType? PenaltyType { get; set; }
    public decimal? FineAmount { get; set; }
}
