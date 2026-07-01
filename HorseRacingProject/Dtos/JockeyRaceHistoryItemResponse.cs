namespace HorseRacingAPI.Dtos
{
    public class JockeyRaceHistoryItemResponse
    {
        public Guid RegistrationId { get; set; }
        public Guid RaceId { get; set; }
        public DateTimeOffset? Date { get; set; }
        public string? RaceName { get; set; }
        public int? RaceNumber { get; set; }
        public string? HorseName { get; set; }
        public string? RacecourseName { get; set; }
        public string? TrackType { get; set; }
        public int? Position { get; set; }
        public decimal? Earnings { get; set; }
    }
}
