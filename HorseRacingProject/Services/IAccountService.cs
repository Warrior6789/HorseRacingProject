using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IAccountService
    {
        Task<PagedResponse<AccountResponse>> GetAccountByStatusPagedAsync(string status, int page, int pageSize, string? role = null, string? search = null);
        Task SuspendAccountAsync(Guid id);
        Task BanAccountAsync(Guid id);
        Task RestoreAccountAsync(Guid id);
        Task<PagedResponse<UpgradeRequestResponse>> GetRoleUpgradeRequestsPagedAsync(int page, int pageSize);
        Task<UpgradeRequestResponse> GetUpgradeRequestDetailAsync(Guid accountId);
        Task ApproveRoleUpgradeAsync(Guid accountId);
        Task RejectRoleUpgradeAsync(Guid accountId);
    }
}
