using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Dtos
{
    public class RegisterDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string RequestedRole { get; set; } = string.Empty;
    }
}
