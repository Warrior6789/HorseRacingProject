using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class BetPayoutConfigService : IBetPayoutConfigService
    {
        private readonly IUnitofWork _uow;

        public BetPayoutConfigService(IUnitofWork uow)
        {
            _uow = uow;
        }

        public async Task<BetPayoutConfigResponse> CreateAsync(CreateBetPayoutConfigRequest req)
        {
            BetPayoutConfig config = new BetPayoutConfig
            {
                BetPayoutConfigId = Guid.NewGuid(),
                WinRatio = req.WinRatio,
                PlaceRatio = req.PlaceRatio,
                ShowRatio = req.ShowRatio,
                Status = BetPayoutConfigStatus.Inactive.ToString(),
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _uow.GetRepository<BetPayoutConfig>().AddAsync(config);
            await _uow.SaveAsync();
            return MapToResponse(config);
        }

        public async Task<BetPayoutConfigResponse> GetActiveConfigAsync()
        {
            BetPayoutConfig? config = await _uow.GetRepository<BetPayoutConfig>().Entities
                .FirstOrDefaultAsync(c => c.Status == BetPayoutConfigStatus.Active.ToString());
            if (config == null)
                throw new KeyNotFoundException("No active bet payout config found.");
            return MapToResponse(config);
        }

        public async Task<PagedResponse<BetPayoutConfigResponse>> GetAllPagedAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            IGenericRepository<BetPayoutConfig> repo = _uow.GetRepository<BetPayoutConfig>();
            int total = await repo.Entities.CountAsync();
            IEnumerable<BetPayoutConfigResponse> items = await repo.FindAsync(
                predicate: null,
                orderBy: q => q.OrderByDescending(c => c.CreatedAt),
                selector: c => new BetPayoutConfigResponse
                {
                    Id = c.BetPayoutConfigId,
                    WinRatio = c.WinRatio,
                    PlaceRatio = c.PlaceRatio,
                    ShowRatio = c.ShowRatio,
                    Status = Enum.Parse<BetPayoutConfigStatus>(c.Status ?? "Inactive"),
                    CreatedAt = c.CreatedAt
                },
                pageIndex: page - 1,
                pageSize: pageSize);

            return new PagedResponse<BetPayoutConfigResponse>
            {
                Items = items.ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            };
        }

        public async Task SetActiveAsync(Guid id)
        {
            IGenericRepository<BetPayoutConfig> repo = _uow.GetRepository<BetPayoutConfig>();
            BetPayoutConfig? target = await repo.Entities.FirstOrDefaultAsync(c => c.BetPayoutConfigId == id);
            if (target == null)
                throw new KeyNotFoundException("Bet payout config not found.");

            List<BetPayoutConfig> all = await repo.Entities.ToListAsync();
            foreach (BetPayoutConfig c in all)
                c.Status = BetPayoutConfigStatus.Inactive.ToString();

            target.Status = BetPayoutConfigStatus.Active.ToString();
            await _uow.SaveAsync();
        }

        private static BetPayoutConfigResponse MapToResponse(BetPayoutConfig c) => new BetPayoutConfigResponse
        {
            Id = c.BetPayoutConfigId,
            WinRatio = c.WinRatio,
            PlaceRatio = c.PlaceRatio,
            ShowRatio = c.ShowRatio,
            Status = Enum.Parse<BetPayoutConfigStatus>(c.Status ?? "Inactive"),
            CreatedAt = c.CreatedAt
        };
    }
}
