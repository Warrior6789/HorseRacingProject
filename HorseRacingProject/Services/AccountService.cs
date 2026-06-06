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

        public async Task ApproveAccountAsync(Guid accountId)
        {
            Account? account = await _uow.GetRepository<Account>().Entities.FirstOrDefaultAsync(a => a.Id == accountId && a.IsDeleted == false);
            if (account == null)
            {
                throw new ArgumentException("Account not found");
            }
            if(account.Status != AccountStatus.Pending)
            {
                throw new InvalidOperationException("Only pending accounts can be approved");
            }
            account.Status    = AccountStatus.Active;
            account.UpdatedAt = DateTimeOffset.UtcNow;
            await _uow.SaveAsync();
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
