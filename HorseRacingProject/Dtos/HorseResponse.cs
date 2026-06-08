namespace HorseRacingAPI.Dtos
{
    public class HorseResponse
    {
        public Guid Id { get; set; }
        public string HorseName { get; set; } = string.Empty;
        public string? Breed { get; set; }
        public string? Color { get; set; }
        public int? Age { get; set; }
        public float? Weight { get; set; }
        public int? RecordWins { get; set; }
        public string? Status { get; set; }
    }
}
