namespace HorseRacingAPI.Dtos
{
    public class UserProfileCreateRequest
    {
        public string? FullName { get; set; }

        public string? Phone { get; set; }

        public IFormFile? Image { get; set; }
    }
}
