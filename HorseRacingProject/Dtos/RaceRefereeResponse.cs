namespace HorseRacingAPI.Dtos
{
    public class RaceRefereeResponse
    {
        public Guid RaceId { get; set; }
        public Guid RefereeId { get; set; }
        public string? RefereeName { get; set; }
        public string? RefereeEmail { get; set; }
    }
}
