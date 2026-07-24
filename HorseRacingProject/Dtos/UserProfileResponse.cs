namespace HorseRacingAPI.Dtos
{
    public class UserProfileResponse
    {
        public Guid ProfileId { get; set; }

        public Guid AccountId { get; set; }

        public string? FullName { get; set; }
        public string? Phone { get; set; }

        public long? Balance { get; set; }

        public string? ImageUrl { get; set; }
        public DateTimeOffset? CreateAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
