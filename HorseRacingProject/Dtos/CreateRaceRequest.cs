using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class CreateRaceRequest
    {
        [Required]
        public Guid RacecourseId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "RaceNumber must be greater than 0.")]
        public int RaceNumber { get; set; }

        [Required]
        [MaxLength(100)]
        public string? RaceName { get; set; }

        [Required]
        public DateTimeOffset StartTime { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "TrackLength must be greater than 0.")]
        public float? TrackLength { get; set; }

        [Range(3, int.MaxValue, ErrorMessage = "MaxParticipants must be at least 3.")]
        public int? MaxParticipants { get; set; }

        public IFormFile? Image { get; set; }
    }
}
