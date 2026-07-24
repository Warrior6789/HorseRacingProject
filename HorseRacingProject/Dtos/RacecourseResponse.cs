namespace HorseRacingAPI.Dtos
{
    public class RacecourseResponse
    {
        public Guid RacecourseId { get; set; }
        public string? RacecourseName { get; set; }
        public string? Location { get; set; }
        public string? TrackType { get; set; }
        public string? ImageUrl { get; set; }
    }
}
