namespace HorseRacingAPI.Dtos
{
    public class JockeyProfileResponse
    {
        public Guid JockeyProfileId { get; set; }

        public Guid AccountId { get; set; }

        public int? ExperienceYears { get; set; }

        public decimal? JockeyRating { get; set; }

        public DateTimeOffset? CreateAt { get; set; }

        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
