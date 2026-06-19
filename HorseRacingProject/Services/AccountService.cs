using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class AccountService : IAccountService
    {
        private readonly IUnitofWork _uow;
        public AccountService(IUnitofWork uow)
        {
            _uow = uow;
        }

        public async Task BanAccountAsync(Guid id)
        {
            Account? account = await _uow.GetRepository<Account>().Entities.FirstOrDefaultAsync(a => a.Id == id && a.IsDeleted == false);
            if(account == null)
            {
                throw new ArgumentException("Account not found");
            }
            if ((account.Status == AccountStatus.Banned))
            {
                throw new InvalidOperationException("Account is already banned.");
            }
            account.Status    = AccountStatus.Banned;
            account.UpdatedAt = DateTimeOffset.UtcNow;
            await _uow.SaveAsync();
        }

        public async Task<List<AccountResponse>> GetAccountByStatusAsync(string status)
        {
            IGenericRepository<Account> accRepo = _uow.GetRepository<Account>();
            if (!Enum.TryParse<AccountStatus>(status, ignoreCase: true, out var accountStatus))
            {
                throw new ArgumentException("Invalid account status");
            }

            return await accRepo.Entities
                .Where(a => a.Status == accountStatus && !a.IsDeleted)
                .Select(a => new AccountResponse
                {
                    Id = a.Id,
                    Email = a.Email,
                    Role = a.Role.ToString(),
                    Status = a.Status.ToString(),
                    CreateAt = a.CreateAt
                }).ToListAsync();
        }

        public async Task<PagedResponse<AccountResponse>> GetAccountByStatusPagedAsync(string status, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;
            IGenericRepository<Account> accRepo = _uow.GetRepository<Account>();

            if (!Enum.TryParse<AccountStatus>(status, ignoreCase: true, out var accountStatus))
            {
                throw new ArgumentException("Invalid account status");
            }

            int totalCount = await accRepo.Entities
                .Where(a => a.Status == accountStatus && !a.IsDeleted)
                .CountAsync();

            IEnumerable<AccountResponse> items = await accRepo.FindAsync<AccountResponse>(
                predicate: a => a.Status == accountStatus && !a.IsDeleted,
                orderBy: null,
                selector: a => new AccountResponse
                {
                    Id = a.Id,
                    Email = a.Email,
                    Role = a.Role.ToString(),
                    Status = a.Status.ToString(),
                    CreateAt = a.CreateAt
                },
                pageIndex: page - 1,
                pageSize: pageSize
            );

            return new PagedResponse<AccountResponse>
            {
                Items = items.ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task RestoreAccountAsync(Guid id)
        {
            Account? account = await _uow.GetRepository<Account>().Entities.FirstOrDefaultAsync(a => a.Id == id && a.IsDeleted == false);
            if (account == null)
            {
                throw new ArgumentException("Account not found");
            }
            if (account.Status != AccountStatus.Suspended)
            {
                throw new InvalidOperationException("Only Suspended accounts can be restored.");
            }
            account.Status    = AccountStatus.Active;
            account.UpdatedAt = DateTimeOffset.UtcNow;
            await _uow.SaveAsync();
        }

        public async Task<List<AccountResponse>> GetRoleUpgradeRequestsAsync()
        {
            return await _uow.GetRepository<Account>().Entities
                .Where(a => a.RequestedRole != null && !a.IsDeleted)
                .Select(a => new AccountResponse
                {
                    Id = a.Id,
                    Email = a.Email,
                    Role = a.Role.ToString(),
                    Status = a.Status.ToString(),
                    RequestedRole = a.RequestedRole.ToString(),
                    CreateAt = a.CreateAt
                }).ToListAsync();
        }

        public async Task ApproveRoleUpgradeAsync(Guid accountId)
        {
            Account? account = await _uow.GetRepository<Account>().Entities.FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted);
            if (account == null)
                throw new KeyNotFoundException("Account not found.");
            if (account.RequestedRole == null)
                throw new InvalidOperationException("This account has no pending role upgrade request.");

            account.Role = account.RequestedRole.Value;
            account.RequestedRole = null;
            account.UpdatedAt = DateTimeOffset.UtcNow;
            await _uow.SaveAsync();
        }

        public async Task RejectRoleUpgradeAsync(Guid accountId)
        {
            Account? account = await _uow.GetRepository<Account>().Entities.FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted);
            if (account == null)
                throw new KeyNotFoundException("Account not found.");
            if (account.RequestedRole == null)
                throw new InvalidOperationException("This account has no pending role upgrade request.");

            if (account.RequestedRole == AccountRole.Jockey)
            {
                JockeyProfile? jockeyProfile = await _uow.GetRepository<JockeyProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == accountId && !p.IsDeleted);
                if (jockeyProfile != null)
                    await _uow.GetRepository<JockeyProfile>().DeleteAsync(jockeyProfile);
            }
            else
            {
                UserProfile? userProfile = await _uow.GetRepository<UserProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == accountId && !p.IsDeleted);
                if (userProfile != null)
                    await _uow.GetRepository<UserProfile>().DeleteAsync(userProfile);
            }

            account.RequestedRole = null;
            account.UpdatedAt = DateTimeOffset.UtcNow;
            await _uow.SaveAsync();
        }

        public async Task SuspendAccountAsync(Guid id)
        {
            Account? account = await _uow.GetRepository<Account>().Entities.FirstOrDefaultAsync(a => a.Id == id && a.IsDeleted == false);
            if (account == null)
            {
                throw new ArgumentException("Account not found");
            }
            if ((account.Status != AccountStatus.Active))
            {
                throw new InvalidOperationException("Only Active accounts can be suspended.");
            }
            account.Status    = AccountStatus.Suspended;
            account.UpdatedAt = DateTimeOffset.UtcNow;
            await _uow.SaveAsync();
        }
    }
}
