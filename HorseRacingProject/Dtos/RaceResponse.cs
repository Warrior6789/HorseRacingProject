namespace HorseRacingAPI.Dtos
{
    public class RaceResponse
    {
        public Guid RaceId { get; set; }
        public int? RaceNumber { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public float? TrackLength { get; set; }
        public int? MaxParticipants { get; set; }
        public string? Status { get; set; }
        public string? RacecourseName { get; set; }
        public string? Location { get; set; }
        public TournamentResponse? Tournament { get; set; }
    }
}
