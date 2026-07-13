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
    public class BetService : IBetService
    {
        private const decimal MinBetAmount = 1000;

        private readonly IUnitofWork _uow;
        private readonly IHubContext<RaceHub> _hubContext;
        private static readonly HashSet<BetType> ValidBetTypes = [BetType.Win, BetType.Place, BetType.Show];
        public BetService(IUnitofWork uow, IHubContext<RaceHub> hubContext)
        {
            _uow = uow;
            _hubContext = hubContext;
        }
        public async Task<PagedResponse<BetResponse>> GetMyBetsPagedAsync(Guid spectatorId, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;
            IGenericRepository<Bet> repo = _uow.GetRepository<Bet>();
            int totalCount = await repo.Entities.CountAsync(b => b.SpectatorId == spectatorId);
            IEnumerable<BetResponse> items = await repo.FindAsync<BetResponse>(
                predicate: b => b.SpectatorId == spectatorId,
                orderBy: q => q.OrderByDescending(b => b.CreatedAt),
                selector: b => new BetResponse
                {
                    BetId = b.BetId,
                    SpectatorId = b.SpectatorId,
                    RegistrationId = b.RegistrationId,
                    RaceId = b.Registration.RaceId,
                    HorseName = b.Registration.Horse.HorseName,
                    RaceNumber = b.Registration.Race.RaceNumber,
                    RaceName = b.Registration.Race.RaceName,
                    BetAmount = b.BetAmount,
                    BetType = b.BetType.ToString(),
                    PayoutRatio = b.PayoutRatio,
                    Status = b.Status.ToString(),
                    CreatedAt = b.CreatedAt
                },
                pageIndex: page - 1,
                pageSize: pageSize,
                include: q => q.Include(b => b.Registration).ThenInclude(r => r.Horse)
                               .Include(b => b.Registration).ThenInclude(r => r.Race));
            List<BetResponse> itemList = items.ToList();
            await EnrichEstimatedPayoutAsync(itemList);
            return new PagedResponse<BetResponse> { Items = itemList, Page = page, PageSize = pageSize, TotalCount = totalCount };
        }

        public async Task<BetResponse> PlaceBetAsync(Guid spectatorId, PlaceBetRequest req)
        {
            Registration? registration = await _uow.GetRepository<Registration>().Entities
                 .Include(r => r.Race)
                 .Include(r => r.Horse)
                 .FirstOrDefaultAsync(r => r.RegistrationId == req.RegistrationId);
            if (registration == null)
                throw new KeyNotFoundException("Registration not found.");
            if (registration.Status != RegistrationStatus.Confirmed)
                throw new InvalidOperationException("Horse is not confirmed in this race.");
            if (registration.Race.Status != RaceStatus.BettingOpen)
                throw new InvalidOperationException("Betting is not open for this race.");
            if (!Enum.TryParse<BetType>(req.BetType, ignoreCase: true, out var betType) || !ValidBetTypes.Contains(betType))
                throw new InvalidOperationException("Invalid bet type. Must be Win, Place, or Show.");

            if (req.BetAmount < MinBetAmount)
                throw new InvalidOperationException($"Bet amount must be at least {MinBetAmount} coins.");
            bool profileExists = await _uow.GetRepository<UserProfile>().Entities
                .AnyAsync(p => p.AccountId == spectatorId && !p.IsDeleted);
            if (!profileExists)
                throw new KeyNotFoundException("User profile not found.");

            bool alreadyBet = await _uow.GetRepository<Bet>().Entities
                 .AnyAsync(b => b.SpectatorId == spectatorId
                             && b.RegistrationId == req.RegistrationId
                             && b.BetType == betType
                             && b.Status == BetStatus.Active);
            if (alreadyBet)
                throw new InvalidOperationException($"You have already placed a {betType} bet on this horse.");

            await _uow.BeginTransactionAsync();
            try
            {
                int deducted = await _uow.GetRepository<UserProfile>().Entities
                    .Where(p => p.AccountId == spectatorId && !p.IsDeleted && p.Balance >= (long)req.BetAmount)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.Balance, p => p.Balance - (long)req.BetAmount)
                        .SetProperty(p => p.UpdatedAt, DateTimeOffset.UtcNow));
                if (deducted == 0)
                    throw new InvalidOperationException("Insufficient balance.");

                int poolUpdated = await _uow.GetRepository<RacePool>().Entities
                    .Where(p => p.RaceId == registration.RaceId && p.BetType == betType)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.TotalAmount, p => p.TotalAmount + req.BetAmount));

                if (poolUpdated == 0)
                    await _uow.GetRepository<RacePool>().AddAsync(new RacePool
                    {
                        RacePoolId = Guid.NewGuid(),
                        RaceId = registration.RaceId,
                        BetType = betType,
                        TotalAmount = req.BetAmount
                    });

                bool duplicateInTx = await _uow.GetRepository<Bet>().Entities
                    .AnyAsync(b => b.SpectatorId == spectatorId
                                && b.RegistrationId == req.RegistrationId
                                && b.Status == BetStatus.Active);
                if (duplicateInTx)
                    throw new InvalidOperationException("You have already placed a bet on this horse.");

                Bet bet = new Bet
                {
                    BetId = Guid.NewGuid(),
                    SpectatorId = spectatorId,
                    RegistrationId = req.RegistrationId,
                    BetAmount = req.BetAmount,
                    BetType = betType,
                    PayoutRatio = null,
                    Status = BetStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                await _uow.GetRepository<Bet>().AddAsync(bet);
                await _uow.SaveAsync();
                await _uow.CommitTransactionAsync();

                List<RacePool> pools = await _uow.GetRepository<RacePool>().Entities
                    .Where(p => p.RaceId == registration.RaceId)
                    .ToListAsync();

                await _hubContext.Clients.Group($"race-{registration.RaceId}")
                    .SendAsync("PoolUpdate", new
                    {
                        raceId = registration.RaceId,
                        pools = pools.Select(p => new
                        {
                            betType = p.BetType.ToString(),
                            totalAmount = p.TotalAmount
                        })
                    });

                return MapToResponse(bet, registration);
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        private static BetResponse MapToResponse(Bet b, Registration registration) => new BetResponse
        {
            BetId = b.BetId,
            SpectatorId = b.SpectatorId,
            RegistrationId = b.RegistrationId,
            RaceId = registration.RaceId,
            HorseName = registration.Horse.HorseName,
            RaceNumber = registration.Race.RaceNumber,
            RaceName = registration.Race.RaceName,
            BetAmount = b.BetAmount,
            BetType = b.BetType?.ToString(),
            PayoutRatio = b.PayoutRatio,
            Status = b.Status.ToString(),
            CreatedAt = b.CreatedAt
        };

        private async Task EnrichEstimatedPayoutAsync(List<BetResponse> bets)
        {
            List<BetResponse> activeBets = bets.Where(b => b.Status == BetStatus.Active.ToString()).ToList();
            if (!activeBets.Any()) return;

            List<Guid> raceIds = activeBets.Select(b => b.RaceId).Distinct().ToList();
            List<Guid> registrationIds = activeBets.Select(b => b.RegistrationId).Distinct().ToList();

            Dictionary<Guid, decimal> takeoutByRace = await _uow.GetRepository<Race>().Entities
                .Include(r => r.TakeoutConfig)
                .Where(r => raceIds.Contains(r.RaceId))
                .ToDictionaryAsync(r => r.RaceId, r => (decimal)(r.TakeoutConfig?.TakeoutPercentage ?? 0.20f));

            List<RacePool> pools = await _uow.GetRepository<RacePool>().Entities
                .Where(p => raceIds.Contains(p.RaceId))
                .ToListAsync();

            var perHorsePools = await _uow.GetRepository<Bet>().Entities
                .Where(b => registrationIds.Contains(b.RegistrationId) && b.Status == BetStatus.Active)
                .GroupBy(b => new { b.RegistrationId, b.BetType })
                .Select(g => new { g.Key.RegistrationId, g.Key.BetType, Total = g.Sum(b => b.BetAmount) })
                .ToListAsync();

            foreach (BetResponse bet in activeBets)
            {
                if (!Enum.TryParse<BetType>(bet.BetType, ignoreCase: true, out BetType betType)) continue;

                RacePool? pool = pools.FirstOrDefault(p => p.RaceId == bet.RaceId && p.BetType == betType);
                var horsePool = perHorsePools.FirstOrDefault(p => p.RegistrationId == bet.RegistrationId && p.BetType == betType);

                if (pool == null || horsePool == null || horsePool.Total <= 0) continue;

                decimal takeout = takeoutByRace.GetValueOrDefault(bet.RaceId, 0.20m);
                decimal netPool = pool.TotalAmount * (1 - takeout);
                bet.EstimatedPayout = Math.Round(bet.BetAmount * (netPool / horsePool.Total), 0);
            }
        }
    }
}
