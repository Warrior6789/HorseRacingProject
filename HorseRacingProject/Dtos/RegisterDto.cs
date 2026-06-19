using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Dtos
{
    public class RegisterDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public IFormFile? Avatar { get; set; }
    }
}
