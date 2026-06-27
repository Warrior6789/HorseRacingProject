using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class AccountService : IAccountService
    {
        private readonly IUnitofWork _uow;
        private readonly IHubContext<RaceHub> _hubContext;

        public AccountService(IUnitofWork uow, IHubContext<RaceHub> hubContext)
        {
            _uow = uow;
            _hubContext = hubContext;
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

        public async Task<PagedResponse<AccountResponse>> GetAccountByStatusPagedAsync(string status, int page, int pageSize, string? role = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;
            IGenericRepository<Account> accRepo = _uow.GetRepository<Account>();

            if (!Enum.TryParse<AccountStatus>(status, ignoreCase: true, out var accountStatus))
                throw new ArgumentException("Invalid account status");

            AccountRole? parsedRole = role != null && Enum.TryParse<AccountRole>(role, ignoreCase: true, out var r) ? r : (AccountRole?)null;

            int totalCount = await accRepo.Entities
                .Where(a => a.Status == accountStatus && !a.IsDeleted
                    && (parsedRole == null || a.Role == parsedRole))
                .CountAsync();

            IEnumerable<AccountResponse> items = await accRepo.FindAsync<AccountResponse>(
                predicate: a => a.Status == accountStatus && !a.IsDeleted
                    && (parsedRole == null || a.Role == parsedRole),
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

        public async Task<List<UpgradeRequestResponse>> GetRoleUpgradeRequestsAsync()
        {
            List<Account> accounts = await _uow.GetRepository<Account>().Entities
                .Where(a => a.RequestedRole != null && !a.IsDeleted)
                .ToListAsync();

            List<UpgradeRequestResponse> result = new();

            foreach (Account account in accounts)
            {
                UpgradeRequestResponse item = new()
                {
                    AccountId    = account.Id,
                    Email        = account.Email,
                    CurrentRole  = account.Role.ToString(),
                    RequestedRole = account.RequestedRole!.ToString()!,
                    RequestedAt  = account.UpdatedAt
                };

                if (account.RequestedRole == AccountRole.Jockey)
                {
                    JockeyProfile? jp = await _uow.GetRepository<JockeyProfile>().Entities
                        .FirstOrDefaultAsync(p => p.AccountId == account.Id && !p.IsDeleted);

                    if (jp != null)
                    {
                        item.FullName             = jp.FullName;
                        item.CertificateImageUrl  = jp.CertificateImageUrl;
                        item.DateOfBirth          = jp.DateOfBirth;
                        item.Nationality          = jp.Nationality;
                        item.LicenseNumber        = jp.LicenseNumber;
                        item.Weight               = jp.Weight;
                        item.Height               = jp.Height;
                    }
                }
                else
                {
                    UserProfile? up = await _uow.GetRepository<UserProfile>().Entities
                        .FirstOrDefaultAsync(p => p.AccountId == account.Id && !p.IsDeleted);

                    if (up != null)
                    {
                        item.FullName            = up.FullName;
                        item.Phone               = up.Phone;
                        item.AvatarUrl           = up.ImageUrl;
                        item.CertificateImageUrl = up.CertificateImageUrl;
                    }
                }

                result.Add(item);
            }

            return result;
        }

        public async Task<PagedResponse<UpgradeRequestResponse>> GetRoleUpgradeRequestsPagedAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            IGenericRepository<Account> accRepo = _uow.GetRepository<Account>();

            int totalCount = await accRepo.Entities
                .CountAsync(a => a.RequestedRole != null && !a.IsDeleted);

            List<Account> accounts = await accRepo.Entities
                .Where(a => a.RequestedRole != null && !a.IsDeleted)
                .OrderBy(a => a.UpdatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            List<UpgradeRequestResponse> items = new();

            foreach (Account account in accounts)
            {
                UpgradeRequestResponse item = new()
                {
                    AccountId     = account.Id,
                    Email         = account.Email,
                    CurrentRole   = account.Role.ToString(),
                    RequestedRole = account.RequestedRole!.ToString()!,
                    RequestedAt   = account.UpdatedAt
                };

                if (account.RequestedRole == AccountRole.Jockey)
                {
                    JockeyProfile? jp = await _uow.GetRepository<JockeyProfile>().Entities
                        .FirstOrDefaultAsync(p => p.AccountId == account.Id && !p.IsDeleted);
                    if (jp != null)
                    {
                        item.FullName            = jp.FullName;
                        item.CertificateImageUrl = jp.CertificateImageUrl;
                        item.DateOfBirth         = jp.DateOfBirth;
                        item.Nationality         = jp.Nationality;
                        item.LicenseNumber       = jp.LicenseNumber;
                        item.Weight              = jp.Weight;
                        item.Height              = jp.Height;
                    }
                }
                else
                {
                    UserProfile? up = await _uow.GetRepository<UserProfile>().Entities
                        .FirstOrDefaultAsync(p => p.AccountId == account.Id && !p.IsDeleted);
                    if (up != null)
                    {
                        item.FullName            = up.FullName;
                        item.Phone               = up.Phone;
                        item.AvatarUrl           = up.ImageUrl;
                        item.CertificateImageUrl = up.CertificateImageUrl;
                    }
                }

                items.Add(item);
            }

            return new PagedResponse<UpgradeRequestResponse>
            {
                Items      = items,
                Page       = page,
                PageSize   = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<UpgradeRequestResponse> GetUpgradeRequestDetailAsync(Guid accountId)
        {
            Account? account = await _uow.GetRepository<Account>().Entities
                .FirstOrDefaultAsync(a => a.Id == accountId && a.RequestedRole != null && !a.IsDeleted);

            if (account == null)
                throw new KeyNotFoundException("No pending upgrade request found for this account.");

            UpgradeRequestResponse item = new()
            {
                AccountId     = account.Id,
                Email         = account.Email,
                CurrentRole   = account.Role.ToString(),
                RequestedRole = account.RequestedRole!.ToString()!,
                RequestedAt   = account.UpdatedAt
            };

            if (account.RequestedRole == AccountRole.Jockey)
            {
                JockeyProfile? jp = await _uow.GetRepository<JockeyProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == accountId && !p.IsDeleted);

                if (jp != null)
                {
                    item.FullName            = jp.FullName;
                    item.CertificateImageUrl = jp.CertificateImageUrl;
                    item.DateOfBirth         = jp.DateOfBirth;
                    item.Nationality         = jp.Nationality;
                    item.LicenseNumber       = jp.LicenseNumber;
                    item.Weight              = jp.Weight;
                    item.Height              = jp.Height;
                }
            }
            else
            {
                UserProfile? up = await _uow.GetRepository<UserProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == accountId && !p.IsDeleted);

                if (up != null)
                {
                    item.FullName            = up.FullName;
                    item.Phone               = up.Phone;
                    item.AvatarUrl           = up.ImageUrl;
                    item.CertificateImageUrl = up.CertificateImageUrl;
                }
            }

            return item;
        }

        public async Task ApproveRoleUpgradeAsync(Guid accountId)
        {
            Account? account = await _uow.GetRepository<Account>().Entities.FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted);
            if (account == null)
                throw new KeyNotFoundException("Account not found.");
            if (account.RequestedRole == null)
                throw new InvalidOperationException("This account has no pending role upgrade request.");

            if (account.RequestedRole == AccountRole.Jockey)
            {
                UserProfile? userProfile = await _uow.GetRepository<UserProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == accountId && !p.IsDeleted);

                if (userProfile != null)
                {
                    JockeyProfile? jockeyProfile = await _uow.GetRepository<JockeyProfile>().Entities
                        .FirstOrDefaultAsync(p => p.AccountId == accountId && !p.IsDeleted);

                    if (jockeyProfile != null)
                    {
                        if (userProfile.ImageUrl != null)
                            jockeyProfile.ImageUrl = userProfile.ImageUrl;
                        jockeyProfile.Balance = userProfile.Balance ?? 0;
                        jockeyProfile.UpdatedAt = DateTimeOffset.UtcNow;
                        await _uow.GetRepository<JockeyProfile>().UpdateAsync(jockeyProfile);
                    }

                    await _uow.GetRepository<UserProfile>().DeleteAsync(userProfile);
                }
            }

            account.Role = account.RequestedRole.Value;
            account.RequestedRole = null;
            account.UpdatedAt = DateTimeOffset.UtcNow;
            await _uow.SaveAsync();

            int pendingCount = await _uow.GetRepository<Account>().Entities
                .CountAsync(a => a.RequestedRole != null && !a.IsDeleted);
            await _hubContext.Clients.All.SendAsync("UpgradeRequestsUpdated", new { pendingCount });
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

            int pendingCount = await _uow.GetRepository<Account>().Entities
                .CountAsync(a => a.RequestedRole != null && !a.IsDeleted);
            await _hubContext.Clients.All.SendAsync("UpgradeRequestsUpdated", new { pendingCount });
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
