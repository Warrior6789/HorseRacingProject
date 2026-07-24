using System.ComponentModel.DataAnnotations;

namespace HorseRacingAPI.Dtos
{
    public class UserProfileCreateRequest
    {
        public string? FullName { get; set; }

        [RegularExpression(@"^$|^(0|\+84)[35789]\d{8}$", ErrorMessage = "Phone must be a valid Vietnamese phone number (e.g. 0912345678).")]
        public string? Phone { get; set; }

        public IFormFile? Image { get; set; }
    }
}
