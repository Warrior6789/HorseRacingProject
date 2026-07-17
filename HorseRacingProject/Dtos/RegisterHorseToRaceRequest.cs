using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class RegisterHorseToRaceRequest
    {
        [Required]
        public Guid HorseId { get; set; }

        [Required]
        public Guid JockeyId { get; set; }

        [Range(1, 20, ErrorMessage = "GateNumber must be between 1 and 20.")]
        public int? GateNumber { get; set; }
    }
}
