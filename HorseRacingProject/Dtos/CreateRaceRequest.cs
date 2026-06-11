using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class CreateRaceRequest
    {
        [Required]
        public Guid TournamentId { get; set; }

        [Required]
        public Guid RacecourseId { get; set; }

        [Required]
        public int RaceNumber { get; set; }

        [Required]
        public DateTimeOffset StartTime { get; set; }

        public float? TrackLength { get; set; }

        public int? MaxParticipants { get; set; }
    }
}
