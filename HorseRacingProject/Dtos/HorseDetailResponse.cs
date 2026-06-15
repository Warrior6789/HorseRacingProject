namespace HorseRacingAPI.Dtos
{
    public class HorseDetailResponse
    {
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
        public string HorseName { get; set; } = string.Empty;
        public int? Age { get; set; }
        public string? Breed { get; set; }
        public float? Weight { get; set; }
        public string? Status { get; set; }
        public int? RecordWins { get; set; }
        public string? Color { get; set; }
        public string? ImageUrl { get; set; }
        public DateTimeOffset? CreateAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
