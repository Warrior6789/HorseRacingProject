using HorseRacingAPI.Dtos;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class BetService : IBetService
    {
        private readonly IUnitofWork _uow;
        private static readonly Dictionary<string, float> PayoutRatios = new()
        {
           { "Win",   5.0f },
              { "Place", 2.0f },
              { "Show",  1.5f }
        };
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
                      RaceName = "Race " + b.Registration.Race.RaceNumber,
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
            IQueryable<Bet> query = _uow.GetRepository<Bet>().Entities
                .Include(b => b.Registration).ThenInclude(r => r.Horse)
                .Include(b => b.Registration).ThenInclude(r => r.Race)
                .Where(b => b.SpectatorId == spectatorId);
            int totalCount = await query.CountAsync();
            List<BetResponse> items = await query
                .OrderByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new BetResponse
                {
                    BetId = b.BetId,
                    SpectatorId = b.SpectatorId,
                    RegistrationId = b.RegistrationId,
                    HorseName = b.Registration.Horse.HorseName,
                    RaceName = "Race " + b.Registration.Race.RaceNumber,
                    BetAmount = b.BetAmount,
                    BetType = b.BetType,
                    PayoutRatio = b.PayoutRatio,
                    Status = b.Status,
                    CreatedAt = b.CreatedAt
                })
                .ToListAsync();
            return new PagedResponse<BetResponse> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
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
            if (!PayoutRatios.TryGetValue(req.BetType, out float ratio))
                throw new InvalidOperationException("Invalid bet type. Must be Win, Place, or Show.");
            if (req.BetAmount <= 0)
                throw new InvalidOperationException("Bet amount must be greater than 0.");
            UserProfile? profile = await _uow.GetRepository<UserProfile>().Entities
                 .FirstOrDefaultAsync(p => p.AccountId == spectatorId && !p.IsDeleted);

            if (profile == null)
                throw new KeyNotFoundException("User profile not found.");

            if (profile.Balance < (long)req.BetAmount)
                throw new InvalidOperationException("Insufficient balance.");
            bool alreadyBet = await _uow.GetRepository<Bet>().Entities
                 .AnyAsync(b => b.SpectatorId == spectatorId
                             && b.RegistrationId == req.RegistrationId
                             && b.Status == "Pending");
            if (alreadyBet)
                throw new InvalidOperationException("You have already placed a bet on this horse.");
            profile.Balance -= (long)req.BetAmount;
            profile.UpdatedAt = DateTimeOffset.UtcNow;
            await _uow.GetRepository<UserProfile>().UpdateAsync(profile);
            Bet bet = new Bet
            {
                BetId = Guid.NewGuid(),
                SpectatorId = spectatorId,
                RegistrationId = req.RegistrationId,
                BetAmount = req.BetAmount,
                BetType = req.BetType,
                PayoutRatio = ratio,
                Status = "Pending",
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _uow.GetRepository<Bet>().AddAsync(bet);
            await _uow.SaveAsync();
            return new BetResponse
            {
                BetId = bet.BetId,
                SpectatorId = bet.SpectatorId,
                RegistrationId = bet.RegistrationId,
                HorseName = registration.Horse.HorseName,
                RaceName = "Race " + registration.Race.RaceNumber,
                BetAmount = bet.BetAmount,
                BetType = bet.BetType,
                PayoutRatio = bet.PayoutRatio,
                Status = bet.Status,
                CreatedAt = bet.CreatedAt
            };
        }
    }
}
