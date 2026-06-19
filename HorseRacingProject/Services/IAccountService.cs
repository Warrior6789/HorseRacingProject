using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IAccountService
    {
        Task<List<AccountResponse>> GetAccountByStatusAsync(String Status);
        Task<PagedResponse<AccountResponse>> GetAccountByStatusPagedAsync(String Status, int page, int pageSize);
        Task SuspendAccountAsync(Guid id);
        Task BanAccountAsync(Guid id);
        Task RestoreAccountAsync(Guid id);
        Task<List<AccountResponse>> GetRoleUpgradeRequestsAsync();
        Task ApproveRoleUpgradeAsync(Guid accountId);
        Task RejectRoleUpgradeAsync(Guid accountId);
    }
}
