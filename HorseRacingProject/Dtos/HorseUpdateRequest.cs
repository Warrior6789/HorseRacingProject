using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class HorseUpdateRequest
    {
        [MaxLength(100)]
        public string? HorseName { get; set; }

        [Range(0, 100)]
        public int? Age { get; set; }

        [MaxLength(50)]
        public string? Breed { get; set; }

        [Range(0, 2000)]
        public float? Weight { get; set; }

        [MaxLength(20)]
        public string? Status { get; set; }

        public int? RecordWins { get; set; }

        [MaxLength(20)]
        public string? Color { get; set; }
    }
}
