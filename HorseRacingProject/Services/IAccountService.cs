using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IAccountService
    {
        Task<List<AccountResponse>> GetAccountByStatusAsync(String Status);
        Task ApproveAccountAsync(Guid accountId);
        Task SuspendAccountAsync(Guid id);
        Task BanAccountAsync(Guid id);
        Task RestoreAccountAsync(Guid id);
    }
}
