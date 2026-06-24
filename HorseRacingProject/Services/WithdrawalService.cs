using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services;

public class WithdrawalService : IWithdrawalService
{
    private readonly IUnitofWork _uow;
    private readonly IHubContext<RaceHub> _hubContext;

    public WithdrawalService(IUnitofWork uow, IHubContext<RaceHub> hubContext)
    {
        _uow = uow;
        _hubContext = hubContext;
    }

    private async Task<object> GetWithdrawalKpiAsync()
    {
        var today = DateTimeOffset.UtcNow.Date;
        var repo = _uow.GetRepository<Withdrawal>();

        int pendingCount = await repo.Entities.CountAsync(w => w.Status == WithdrawalStatus.Pending);
        long pendingAmount = await repo.Entities
            .Where(w => w.Status == WithdrawalStatus.Pending)
            .SumAsync(w => (long?)w.Amount) ?? 0;
        int processedToday = await repo.Entities
            .CountAsync(w => w.Status != WithdrawalStatus.Pending
                          && w.ProcessedAt.HasValue
                          && w.ProcessedAt.Value.Date == today);

        return new { pendingCount, pendingAmount, processedToday };
    }

    public async Task<WithdrawalResponse> CreateWithdrawalAsync(Guid accountId, WithdrawalRequest request)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Withdrawal amount must be greater than 0.");

        UserProfile? profile = await _uow.GetRepository<UserProfile>().Entities
            .FirstOrDefaultAsync(u => u.AccountId == accountId);
        if (profile == null)
            throw new KeyNotFoundException("User profile not found.");

        if ((profile.Balance ?? 0) < request.Amount)
            throw new InvalidOperationException("Insufficient balance.");

        Withdrawal withdrawal = new Withdrawal
        {
            WithdrawalId      = Guid.NewGuid(),
            AccountId         = accountId,
            Amount            = request.Amount,
            BankAccountNumber = request.BankAccountNumber,
            BankName          = request.BankName,
            AccountHolderName = request.AccountHolderName,
            Status            = WithdrawalStatus.Pending,
            CreateAt          = DateTimeOffset.UtcNow
        };
        await _uow.GetRepository<Withdrawal>().AddAsync(withdrawal);
        await _uow.SaveAsync();

        await _hubContext.Clients.All.SendAsync("WithdrawalsUpdated", await GetWithdrawalKpiAsync());

        return MapToResponse(withdrawal);
    }

    public async Task<PagedResponse<WithdrawalResponse>> GetMyHistoryAsync(Guid accountId, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var repo = _uow.GetRepository<Withdrawal>();
        int total = await repo.Entities.CountAsync(w => w.AccountId == accountId);
        var items = await repo.Entities
            .Where(w => w.AccountId == accountId)
            .OrderByDescending(w => w.CreateAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => MapToResponse(w))
            .ToListAsync();

        return new PagedResponse<WithdrawalResponse>
        {
            Items      = items,
            Page       = page,
            PageSize   = pageSize,
            TotalCount = total
        };
    }

    public async Task<PagedResponse<WithdrawalResponse>> GetPendingAsync(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var repo = _uow.GetRepository<Withdrawal>();
        int total = await repo.Entities.CountAsync(w => w.Status == WithdrawalStatus.Pending);
        var items = await repo.Entities
            .Where(w => w.Status == WithdrawalStatus.Pending)
            .OrderBy(w => w.CreateAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => MapToResponse(w))
            .ToListAsync();

        return new PagedResponse<WithdrawalResponse>
        {
            Items      = items,
            Page       = page,
            PageSize   = pageSize,
            TotalCount = total
        };
    }

    public async Task<WithdrawalResponse> ApproveAsync(Guid withdrawalId, ProcessWithdrawalDto dto)
    {
        Withdrawal? withdrawal = await _uow.GetRepository<Withdrawal>().Entities
            .FirstOrDefaultAsync(w => w.WithdrawalId == withdrawalId);
        if (withdrawal == null)
            throw new KeyNotFoundException("Withdrawal not found.");
        if (withdrawal.Status != WithdrawalStatus.Pending)
            throw new InvalidOperationException("Withdrawal is not in Pending status.");

        await _uow.BeginTransactionAsync();
        try
        {
            int updated = await _uow.GetRepository<UserProfile>().Entities
                .Where(u => u.AccountId == withdrawal.AccountId && (u.Balance ?? 0) >= withdrawal.Amount)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Balance, u => (u.Balance ?? 0) - withdrawal.Amount));

            if (updated == 0)
            {
                withdrawal.Status      = WithdrawalStatus.Rejected;
                withdrawal.AdminNote   = "Auto-rejected: insufficient balance at time of approval.";
                withdrawal.ProcessedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                withdrawal.Status      = WithdrawalStatus.Completed;
                withdrawal.AdminNote   = dto.AdminNote;
                withdrawal.ProcessedAt = DateTimeOffset.UtcNow;
            }

            await _uow.GetRepository<Withdrawal>().UpdateAsync(withdrawal);
            await _uow.SaveAsync();
            await _uow.CommitTransactionAsync();

            await _hubContext.Clients.All.SendAsync("WithdrawalsUpdated", await GetWithdrawalKpiAsync());

            return MapToResponse(withdrawal);
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<WithdrawalResponse> RejectAsync(Guid withdrawalId, ProcessWithdrawalDto dto)
    {
        Withdrawal? withdrawal = await _uow.GetRepository<Withdrawal>().Entities
            .FirstOrDefaultAsync(w => w.WithdrawalId == withdrawalId);
        if (withdrawal == null)
            throw new KeyNotFoundException("Withdrawal not found.");
        if (withdrawal.Status != WithdrawalStatus.Pending)
            throw new InvalidOperationException("Withdrawal is not in Pending status.");

        withdrawal.Status      = WithdrawalStatus.Rejected;
        withdrawal.AdminNote   = dto.AdminNote;
        withdrawal.ProcessedAt = DateTimeOffset.UtcNow;
        await _uow.GetRepository<Withdrawal>().UpdateAsync(withdrawal);
        await _uow.SaveAsync();

        await _hubContext.Clients.All.SendAsync("WithdrawalsUpdated", await GetWithdrawalKpiAsync());

        return MapToResponse(withdrawal);
    }

    private static WithdrawalResponse MapToResponse(Withdrawal w) => new WithdrawalResponse
    {
        WithdrawalId      = w.WithdrawalId,
        AccountId         = w.AccountId,
        Amount            = w.Amount,
        BankAccountNumber = w.BankAccountNumber,
        BankName          = w.BankName,
        AccountHolderName = w.AccountHolderName,
        Status            = w.Status.ToString(),
        AdminNote         = w.AdminNote,
        CreateAt          = w.CreateAt,
        ProcessedAt       = w.ProcessedAt
    };
}
