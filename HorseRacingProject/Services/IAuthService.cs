using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IAuthService
    {
        Task RegisterAsync(RegisterDto registerDto);
        Task<string?> LoginAsync(LoginDto loginDto);
        Task RequestRoleUpgradeAsync(Guid accountId, RequestRoleUpgradeDto dto);
        Task<MeResponse> GetMeAsync(Guid accountId);
    }
}
