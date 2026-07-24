using System.ComponentModel.DataAnnotations;
using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Dtos
{
    public class CreateRefereeReportDto
    {
        [Required] public Guid RaceId { get; set; }
        [Required] public Guid RegistrationId { get; set; }
        [Required, MinLength(10)] public string IncidentDescription { get; set; } = null!;
        public string? PenaltyApplied { get; set; }
        [Required] public PenaltyType PenaltyType { get; set; }
        public decimal? FineAmount { get; set; }
    }
}
