namespace HorseRacingAPI.Dtos
{
    public class UserProfileCreateRequest
    {
        public Guid AccountId { get; set; }

        public string? FullName { get; set; }

        public string? Phone { get; set; }
    }
}
