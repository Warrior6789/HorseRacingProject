namespace HorseRacingAPI.Dtos
{
    public class JockeyProfileCreateRequest
    {
        public Guid AccountId { get; set; }

        public int? ExperienceYears { get; set; }

        public decimal? JockeyRating { get; set; }
    }
}
