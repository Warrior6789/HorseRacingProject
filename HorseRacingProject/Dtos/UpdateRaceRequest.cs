using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class UpdateRaceRequest
    {
        public Guid? RacecourseId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "RaceNumber must be greater than 0.")]
        public int? RaceNumber { get; set; }

        [MaxLength(100)]
        public string? RaceName { get; set; }

        public DateTimeOffset? StartTime { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "TrackLength must be greater than 0.")]
        public float? TrackLength { get; set; }

        [Range(3, int.MaxValue, ErrorMessage = "MaxParticipants must be at least 3.")]
        public int? MaxParticipants { get; set; }
    }
}
