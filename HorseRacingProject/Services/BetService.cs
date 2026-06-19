using HorseRacingAPI.Dtos;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class BetService : IBetService
    {
        private readonly IUnitofWork _uow;
        private static readonly HashSet<string> ValidBetTypes = ["Win", "Place", "Show"];
        public BetService(IUnitofWork uow)
        {
            _uow = uow;
        }
        public async Task<List<BetResponse>> GetMyBetsAsync(Guid spectatorId)
        {
            return await _uow.GetRepository<Bet>().Entities
                  .Include(b => b.Registration).ThenInclude(r => r.Horse)
                  .Include(b => b.Registration).ThenInclude(r => r.Race)
                  .Where(b => b.SpectatorId == spectatorId)
                  .OrderByDescending(b => b.CreatedAt)
                  .Select(b => new BetResponse
                  {
                      BetId = b.BetId,
                      SpectatorId = b.SpectatorId,
                      RegistrationId = b.RegistrationId,
                      HorseName = b.Registration.Horse.HorseName,
                      RaceNumber = b.Registration.Race.RaceNumber,
                      BetAmount = b.BetAmount,
                      BetType = b.BetType,
                      PayoutRatio = b.PayoutRatio,
                      Status = b.Status,
                      CreatedAt = b.CreatedAt
                  })
                  .ToListAsync();
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
                    HorseName = b.Registration.Horse.HorseName,
                    RaceNumber = b.Registration.Race.RaceNumber,
                    BetAmount = b.BetAmount,
                    BetType = b.BetType,
                    PayoutRatio = b.PayoutRatio,
                    Status = b.Status,
                    CreatedAt = b.CreatedAt
                },
                pageIndex: page - 1,
                pageSize: pageSize,
                include: q => q.Include(b => b.Registration).ThenInclude(r => r.Horse)
                               .Include(b => b.Registration).ThenInclude(r => r.Race));
            return new PagedResponse<BetResponse> { Items = items.ToList(), Page = page, PageSize = pageSize, TotalCount = totalCount };
        }

        public async Task<BetResponse> PlaceBetAsync(Guid spectatorId, PlaceBetRequest req)
        {
            Registration? registration = await _uow.GetRepository<Registration>().Entities
                 .Include(r => r.Race)
                 .Include(r => r.Horse)
                 .FirstOrDefaultAsync(r => r.RegistrationId == req.RegistrationId);
            if (registration == null)
                throw new KeyNotFoundException("Registration not found.");
            if (registration.Status != "Confirmed")
                throw new InvalidOperationException("Horse is not confirmed in this race.");
            if (registration.Race.Status != "BettingOpen")
                throw new InvalidOperationException("Betting is not open for this race.");
            if (!ValidBetTypes.Contains(req.BetType))
                throw new InvalidOperationException("Invalid bet type. Must be Win, Place, or Show.");

            if (req.BetAmount <= 0)
                throw new InvalidOperationException("Bet amount must be greater than 0.");
            bool profileExists = await _uow.GetRepository<UserProfile>().Entities
                .AnyAsync(p => p.AccountId == spectatorId && !p.IsDeleted);
            if (!profileExists)
                throw new KeyNotFoundException("User profile not found.");

            bool alreadyBet = await _uow.GetRepository<Bet>().Entities
                 .AnyAsync(b => b.SpectatorId == spectatorId
                             && b.RegistrationId == req.RegistrationId
                             && b.Status == "Pending");
            if (alreadyBet)
                throw new InvalidOperationException("You have already placed a bet on this horse.");

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
                    .Where(p => p.RaceId == registration.RaceId && p.BetType == req.BetType)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.TotalAmount, p => p.TotalAmount + req.BetAmount));

                if (poolUpdated == 0)
                    await _uow.GetRepository<RacePool>().AddAsync(new RacePool
                    {
                        RacePoolId = Guid.NewGuid(),
                        RaceId = registration.RaceId,
                        BetType = req.BetType,
                        TotalAmount = req.BetAmount
                    });

                bool duplicateInTx = await _uow.GetRepository<Bet>().Entities
                    .AnyAsync(b => b.SpectatorId == spectatorId
                                && b.RegistrationId == req.RegistrationId
                                && b.Status == "Pending");
                if (duplicateInTx)
                    throw new InvalidOperationException("You have already placed a bet on this horse.");

                Bet bet = new Bet
                {
                    BetId = Guid.NewGuid(),
                    SpectatorId = spectatorId,
                    RegistrationId = req.RegistrationId,
                    BetAmount = req.BetAmount,
                    BetType = req.BetType,
                    PayoutRatio = null,
                    Status = "Pending",
                    CreatedAt = DateTimeOffset.UtcNow
                };
                await _uow.GetRepository<Bet>().AddAsync(bet);
                await _uow.SaveAsync();
                await _uow.CommitTransactionAsync();
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
            HorseName = registration.Horse.HorseName,
            RaceNumber = registration.Race.RaceNumber,
            BetAmount = b.BetAmount,
            BetType = b.BetType,
            PayoutRatio = b.PayoutRatio,
            Status = b.Status,
            CreatedAt = b.CreatedAt
        };
    }
}
