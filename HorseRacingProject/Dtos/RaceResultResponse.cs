namespace HorseRacingAPI.Dtos
{
    public class RaceResultHorseDto
    {
        public Guid Id { get; set; }
        public Guid RegistrationId { get; set; }
        public string HorseName { get; set; } = string.Empty;
        public string? Breed { get; set; }
        public string? Color { get; set; }
        public int? Age { get; set; }
        public string? Status { get; set; }
    }

    public class RaceResultResponse
    {
        public int? Position { get; set; }
        public RaceResultHorseDto Horse { get; set; } = new();
        public string? FinishedAt { get; set; }
    }
}
