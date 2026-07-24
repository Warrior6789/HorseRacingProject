using System.ComponentModel.DataAnnotations;
using HorseRacingAPI.Enums;

namespace HorseRacingAPI.Dtos
{
    public class RegisterDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        public string Password { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        [RegularExpression(@"^$|^(0|\+84)[35789]\d{8}$", ErrorMessage = "Phone must be a valid Vietnamese phone number (e.g. 0912345678).")]
        public string Phone { get; set; } = string.Empty;

        public IFormFile? Avatar { get; set; }
    }
}
