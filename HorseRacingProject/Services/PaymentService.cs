using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace HorseRacingAPI.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IUnitofWork _uow;
        private readonly IConfiguration _config;
        private readonly PayOSClient _payOS;
        private readonly IHubContext<RaceHub> _hubContext;

        public PaymentService(IUnitofWork uow, IConfiguration config, IHubContext<RaceHub> hubContext)
        {
            _uow = uow;
            _config = config;
            _hubContext = hubContext;
            _payOS = new PayOSClient(
                _config["PayOS:ClientId"]!,
                _config["PayOS:ApiKey"]!,
                _config["PayOS:ChecksumKey"]!
            );
        }

        public async Task<string> CreateDepositUrlAsync(Guid accountId, DepositRequest request, string ipAddress)
        {
            Account? acctCheck = await _uow.GetRepository<Account>().Entities
                .FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted);
            if (acctCheck == null)
                throw new KeyNotFoundException("Account not found.");

            long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            Payment payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                AccountId = accountId,
                Amount    = request.Amount,
                OrderCode = orderCode,
                CreateAt  = DateTimeOffset.UtcNow,
                Status    = PaymentStatus.Pending
            };
            await _uow.GetRepository<Payment>().AddAsync(payment);
            await _uow.SaveAsync();

            string walletUrl = _config["PayOS:ReturnBaseUrl"] + GetWalletPath(acctCheck.Role);

            CreatePaymentLinkRequest paymentData = new CreatePaymentLinkRequest
            {
                OrderCode   = orderCode,
                Amount      = (int)request.Amount,
                Description = $"Nap tien {accountId.ToString("N")[..8]}",
                ReturnUrl   = walletUrl,
                CancelUrl   = walletUrl
            };

            var result = await _payOS.PaymentRequests.CreateAsync(paymentData);
            return result.CheckoutUrl;
        }

        private static string GetWalletPath(AccountRole role) => role switch
        {
            AccountRole.HorseOwner => "/owner/wallet",
            AccountRole.Jockey     => "/jockey/wallet",
            _                      => "/spectator/wallet"
        };

        public async Task<PaymentResponse> ProcessWebhookAsync(Webhook webhookBody)
        {
            WebhookData data = await _payOS.Webhooks.VerifyAsync(webhookBody);

            Payment? payment = await _uow.GetRepository<Payment>().Entities
                .FirstOrDefaultAsync(p => p.OrderCode == data.OrderCode);
            if (payment == null)
                throw new KeyNotFoundException("Payment not found.");

            if (payment.Status != PaymentStatus.Pending)
                throw new InvalidOperationException("Payment already processed.");

            if (data.Code != "00")
            {
                await _uow.GetRepository<Payment>().Entities
                    .Where(p => p.OrderCode == data.OrderCode && p.Status == PaymentStatus.Pending)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.Status, PaymentStatus.Failed)
                        .SetProperty(p => p.BalanceChanged, 0)
                        .SetProperty(p => p.CurrentBalance, (long?)null));

                return new PaymentResponse
                {
                    PaymentId       = payment.PaymentId,
                    Amount          = payment.Amount,
                    Status          = PaymentStatus.Failed.ToString(),
                    TransactionType = PaymentType.Deposit.ToString(),
                    BalanceChanged  = 0,
                    CurrentBalance  = 0,
                    CreateAt        = payment.CreateAt
                };
            }

            long balanceToAdd = (long)payment.Amount;

            await _uow.BeginTransactionAsync();
            try
            {
                int claimed = await _uow.GetRepository<Payment>().Entities
                    .Where(p => p.OrderCode == data.OrderCode && p.Status == PaymentStatus.Pending)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Completed));
                if (claimed == 0)
                    throw new InvalidOperationException("Payment already processed.");

                Account? acct = await _uow.GetRepository<Account>().Entities
                    .FirstOrDefaultAsync(a => a.Id == payment.AccountId && !a.IsDeleted);

                int profileUpdated;
                long currentBalance;
                if (acct?.Role == AccountRole.Jockey)
                {
                    profileUpdated = await _uow.GetRepository<JockeyProfile>().Entities
                        .Where(j => j.AccountId == payment.AccountId)
                        .ExecuteUpdateAsync(s => s.SetProperty(j => j.Balance, j => (j.Balance ?? 0) + balanceToAdd));
                    if (profileUpdated == 0)
                        throw new InvalidOperationException("Jockey profile not found. Cannot credit balance.");
                    currentBalance = await _uow.GetRepository<JockeyProfile>().Entities
                        .Where(j => j.AccountId == payment.AccountId)
                        .Select(j => j.Balance ?? 0)
                        .FirstOrDefaultAsync();
                }
                else
                {
                    profileUpdated = await _uow.GetRepository<UserProfile>().Entities
                        .Where(u => u.AccountId == payment.AccountId)
                        .ExecuteUpdateAsync(s => s.SetProperty(u => u.Balance, u => (u.Balance ?? 0) + balanceToAdd));
                    if (profileUpdated == 0)
                        throw new InvalidOperationException("User profile not found. Cannot credit balance.");
                    currentBalance = await _uow.GetRepository<UserProfile>().Entities
                        .Where(u => u.AccountId == payment.AccountId)
                        .Select(u => u.Balance ?? 0)
                        .FirstOrDefaultAsync();
                }

                await _uow.GetRepository<Payment>().Entities
                    .Where(p => p.PaymentId == payment.PaymentId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.BalanceChanged, balanceToAdd)
                        .SetProperty(p => p.CurrentBalance, currentBalance));

                await _uow.GetRepository<WalletTransaction>().AddAsync(new WalletTransaction
                {
                    WalletTransactionId = Guid.NewGuid(),
                    AccountId = payment.AccountId,
                    Type = WalletTransactionType.Deposit,
                    Amount = balanceToAdd,
                    BalanceAfter = currentBalance,
                    ReferenceId = payment.PaymentId,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                await _uow.SaveAsync();

                await _uow.CommitTransactionAsync();

                await _hubContext.Clients.All.SendAsync("PaymentsUpdated", new
                {
                    amount    = payment.Amount,
                    createdAt = DateTimeOffset.UtcNow
                });
                await _hubContext.Clients.All.SendAsync("BalanceUpdated", new
                {
                    accountId  = payment.AccountId,
                    amount     = balanceToAdd,
                    newBalance = currentBalance,
                    reason     = "Deposit"
                });

                return new PaymentResponse
                {
                    PaymentId       = payment.PaymentId,
                    Amount          = payment.Amount,
                    Status          = PaymentStatus.Completed.ToString(),
                    TransactionType = PaymentType.Deposit.ToString(),
                    BalanceChanged  = balanceToAdd,
                    CurrentBalance  = currentBalance,
                    CreateAt        = payment.CreateAt
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task CancelPaymentAsync(long orderCode)
        {
            int updated = await _uow.GetRepository<Payment>().Entities
                .Where(p => p.OrderCode == orderCode && p.Status == PaymentStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Cancelled));

            if (updated == 0)
                return;
        }

        public async Task<PagedResponse<PaymentResponse>> GetHistoryPagingAsync(Guid accountId, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            IGenericRepository<Payment> paymentRepo = _uow.GetRepository<Payment>();
            int totalCount = await paymentRepo.Entities.CountAsync(p => p.AccountId == accountId);

            IEnumerable<PaymentResponse> items = await paymentRepo.FindAsync<PaymentResponse>(
                predicate: p => p.AccountId == accountId,
                orderBy: q => q.OrderByDescending(p => p.CreateAt),
                selector: p => new PaymentResponse
                {
                    PaymentId       = p.PaymentId,
                    AccountId       = p.AccountId,
                    AccountEmail    = p.Account.Email,
                    Amount          = p.Amount,
                    Status          = p.Status.ToString(),
                    TransactionType = PaymentType.Deposit.ToString(),
                    BalanceChanged  = p.BalanceChanged ?? 0,
                    CurrentBalance  = p.CurrentBalance ?? 0,
                    CreateAt        = p.CreateAt
                },
                pageIndex: page - 1,
                pageSize: pageSize
            );

            return new PagedResponse<PaymentResponse>
            {
                Items      = items.ToList(),
                Page       = page,
                PageSize   = pageSize,
                TotalCount = totalCount
            };
        }
        public async Task<PagedResponse<PaymentResponse>> GetAllPaymentsPagingAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            IGenericRepository<Payment> paymentRepo = _uow.GetRepository<Payment>();

            int totalCount = await paymentRepo.Entities.CountAsync();

            List<Payment> entities = await paymentRepo.Entities
                .Include(p => p.Account)
                .OrderByDescending(p => p.CreateAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResponse<PaymentResponse>
            {
                Items = entities.Select(p => new PaymentResponse
                {
                    PaymentId       = p.PaymentId,
                    AccountId       = p.AccountId,
                    AccountEmail    = p.Account?.Email,
                    Amount          = p.Amount,
                    Status          = p.Status.ToString(),
                    TransactionType = PaymentType.Deposit.ToString(),
                    BalanceChanged  = p.BalanceChanged ?? 0,
                    CurrentBalance  = p.CurrentBalance ?? 0,
                    CreateAt        = p.CreateAt
                }).ToList(),
                Page       = page,
                PageSize   = pageSize,
                TotalCount = totalCount
            };
        }
    }
}
