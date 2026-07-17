using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace HorseRacingAPI.Dtos
{
    public class HorseCreateRequest
    {
        public Guid? OwnerId { get; set; }

        [Required]
        [MaxLength(100)]
        public string HorseName { get; set; } = string.Empty;

        [Required]
        [Range(0, 100)]
        public int? Age { get; set; }

        [Required]
        [MaxLength(50)]
        public string? Breed { get; set; }

        [Required]
        [Range(0, 2000)]
        public float? Weight { get; set; }

        public int? RecordWins { get; set; }

        [Required]
        [MaxLength(20)]
        public string? Color { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        public IFormFile? Image { get; set; }
    }
}
