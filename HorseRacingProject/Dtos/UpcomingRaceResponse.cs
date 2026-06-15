namespace HorseRacingAPI.Dtos
{
    public class UpcomingRaceResponse
    {
        public Guid RaceId { get; set; }
        public string? RaceName { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public float? TrackLength { get; set; }
        public int? MaxParticipants { get; set; }
        public string? Status { get; set; }
        public string? TournamentName { get; set; }
        public string? RacecourseName { get; set; }
        public string? TrackType { get; set; }
        public string? Location { get; set; }
    }
}
