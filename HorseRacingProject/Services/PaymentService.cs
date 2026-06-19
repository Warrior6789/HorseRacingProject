using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Helpers;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IUnitofWork _uow;
        private readonly IConfiguration _config;
        public PaymentService(IUnitofWork uow, IConfiguration config)
        {
            _uow = uow;
            _config = config;
        }
        public async Task<string> CreateDepositUrlAsync(Guid Id, DepositRequest request, string ipAddress)
        {
            IGenericRepository<UserProfile> userProfiletRepo = _uow.GetRepository<UserProfile>();
            UserProfile? userProfile = await userProfiletRepo.Entities.FirstOrDefaultAsync(a => a.AccountId == Id);
            if (userProfile == null)
                throw new KeyNotFoundException("User profile not found.");

            IGenericRepository<ConversionRate> conversionRateRepo = _uow.GetRepository<ConversionRate>();
            ConversionRate? converionRate = await conversionRateRepo.Entities
                .FirstOrDefaultAsync(c => c.Status == ConfigStatus.Active);
            if (converionRate == null)
                throw new InvalidOperationException("No active conversion rate found.");

            IGenericRepository<Payment> paymentRepo = _uow.GetRepository<Payment>();
            Payment payment = new Payment()
            {
                PaymentId        = Guid.NewGuid(),
                AccountId        = Id,
                Amount           = request.Amount,
                ConversionRateId = converionRate.ConversionRateId,
                CreateAt         = DateTimeOffset.UtcNow,
                Status           = PaymentStatus.Pending
            };

            await paymentRepo.AddAsync(payment);
            await _uow.SaveAsync();

            string baseUrl = _config["VNPay:BaseUrl"]!;
            string tmnCode = _config["VNPay:TmnCode"]!;
            string hashSecret = _config["VNPay:HashSecret"]!;
            string returnUrl = _config["VNPay:ReturnUrl"]!;

            string url = VnPayHelper.BuildPaymentUrl(
                baseUrl,
                tmnCode,
                hashSecret,
                returnUrl,
                payment.PaymentId.ToString("N"),
                (long)(request.Amount * 100),
                $"Deposit for account {Id}",
                ipAddress,
                DateTimeOffset.UtcNow
                );
            return url;
        }

        public async Task<PagedResponse<PaymentResponse>> GetHistoryPagingAsync(Guid accountId, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            IGenericRepository<Payment> paymentRepo = _uow.GetRepository<Payment>();
            int totalCount = await paymentRepo.Entities
                .CountAsync(p => p.AccountId == accountId);

            IEnumerable<PaymentResponse> items = await paymentRepo.FindAsync<PaymentResponse>(
                predicate: p => p.AccountId == accountId,
                orderBy: q => q.OrderByDescending(p => p.CreateAt),
                selector: p => new PaymentResponse
                {
                    PaymentId = p.PaymentId,
                    Amount = p.Amount,
                    Status = p.Status.ToString(),
                    TransactionType = p.Status == PaymentStatus.WithdrawPending
                        ? PaymentType.Withdraw.ToString()
                        : PaymentType.Deposit.ToString(),
                    BalanceChanged = 0,
                    CurrentBalance = 0,
                    CreateAt = p.CreateAt
                },
                pageIndex: page - 1,
                pageSize: pageSize
            );
            return new PagedResponse<PaymentResponse>
            {
                Items = items.ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<PaymentResponse> ProcessCallbackAsync(IQueryCollection queryParams)
        {
            string hashSecret = _config["VNPay:HashSecret"]!;
            if (!VnPayHelper.VerifySignature(queryParams, hashSecret))
                throw new InvalidOperationException("Invalid payment signature.");

            string txnRef = queryParams["vnp_TxnRef"].ToString();
            string responseCode = queryParams["vnp_ResponseCode"].ToString();

            if (!Guid.TryParse(txnRef, out Guid paymentId))
                throw new InvalidOperationException("Invalid transaction reference.");

            IGenericRepository<Payment> paymentRepo = _uow.GetRepository<Payment>();
            Payment? payment = await paymentRepo.Entities.FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment == null)
                throw new KeyNotFoundException("Payment not found.");

            if (payment.Status != PaymentStatus.Pending)
                throw new InvalidOperationException("Payment already processed.");

            if (responseCode != "00")
            {
                int failed = await _uow.GetRepository<Payment>().Entities
                    .Where(p => p.PaymentId == paymentId && p.Status == PaymentStatus.Pending)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Failed));
                string failedStatus = failed > 0
                    ? PaymentStatus.Failed.ToString()
                    : (await _uow.GetRepository<Payment>().Entities
                        .Where(p => p.PaymentId == paymentId)
                        .Select(p => p.Status)
                        .FirstOrDefaultAsync()).ToString();
                return new PaymentResponse
                {
                    PaymentId = payment.PaymentId,
                    Amount = payment.Amount,
                    Status = failedStatus,
                    TransactionType = PaymentType.Deposit.ToString(),
                    BalanceChanged = 0,
                    CurrentBalance = 0,
                    CreateAt = payment.CreateAt
                };
            }

            ConversionRate? conversionRate = await _uow.GetRepository<ConversionRate>()
                .Entities
                .FirstOrDefaultAsync(c => c.ConversionRateId == payment.ConversionRateId);
            if (conversionRate == null)
                throw new KeyNotFoundException("Conversion rate not found.");

            long balanceToAdd = (long)(payment.Amount * (decimal)conversionRate.RateValue);

            await _uow.BeginTransactionAsync();
            try
            {
                int claimed = await _uow.GetRepository<Payment>().Entities
                    .Where(p => p.PaymentId == paymentId && p.Status == PaymentStatus.Pending)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Completed));
                if (claimed == 0)
                    throw new InvalidOperationException("Payment already processed.");

                int profileUpdated = await _uow.GetRepository<UserProfile>().Entities
                    .Where(u => u.AccountId == payment.AccountId)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.Balance, u => (u.Balance ?? 0) + balanceToAdd));
                if (profileUpdated == 0)
                    throw new InvalidOperationException("User profile not found. Cannot credit balance.");

                long currentBalance = await _uow.GetRepository<UserProfile>().Entities
                    .Where(u => u.AccountId == payment.AccountId)
                    .Select(u => u.Balance ?? 0)
                    .FirstOrDefaultAsync();

                await _uow.CommitTransactionAsync();

                return new PaymentResponse
                {
                    PaymentId = payment.PaymentId,
                    Amount = payment.Amount,
                    Status = PaymentStatus.Completed.ToString(),
                    TransactionType = PaymentType.Deposit.ToString(),
                    BalanceChanged = balanceToAdd,
                    CurrentBalance = currentBalance,
                    CreateAt = payment.CreateAt
                };
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }
    }
}
