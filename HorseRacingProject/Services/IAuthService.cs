using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IAuthService
    {
        Task RegisterAsync(RegisterDto registerDto);
        Task<string?> LoginAsync(LoginDto loginDto); 
    }
}
