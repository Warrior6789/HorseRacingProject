using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class RegisterHorseToRaceRequest
    {
        [Required]
        public Guid HorseId { get; set; }

        [Required]
        public Guid JockeyId { get; set; }

        public int? GateNumber { get; set; }
    }
}
