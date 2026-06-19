using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services;

public interface IWithdrawalService
{
    Task<WithdrawalResponse> CreateWithdrawalAsync(Guid accountId, WithdrawalRequest request);
    Task<PagedResponse<WithdrawalResponse>> GetMyHistoryAsync(Guid accountId, int page, int pageSize);
    Task<PagedResponse<WithdrawalResponse>> GetPendingAsync(int page, int pageSize);
    Task<WithdrawalResponse> ApproveAsync(Guid withdrawalId, ProcessWithdrawalDto dto);
    Task<WithdrawalResponse> RejectAsync(Guid withdrawalId, ProcessWithdrawalDto dto);
}
