using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class CreateRefereeReportDto
    {
        [Required] public Guid RaceId { get; set; }
        [Required] public Guid RegistrationId { get; set; }
        [Required, MinLength(10)] public string IncidentDescription { get; set; } = null!;
        public string? PenaltyApplied { get; set; } 
    }
}
