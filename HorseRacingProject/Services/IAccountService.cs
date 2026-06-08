using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IAccountService
    {
        Task<List<AccountResponse>> GetAccountByStatusAsync(String Status);
        Task<PagedResponse<AccountResponse>> GetAccountByStatusPagedAsync(String Status, int page, int pageSize);
        Task ApproveAccountAsync(Guid accountId);
        Task SuspendAccountAsync(Guid id);
        Task BanAccountAsync(Guid id);
        Task RestoreAccountAsync(Guid id);
    }
}
