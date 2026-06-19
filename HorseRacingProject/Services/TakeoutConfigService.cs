using HorseRacingAPI.Dtos;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class TakeoutConfigService : ITakeoutConfigService
    {
        private readonly IUnitofWork _uow;

        public TakeoutConfigService(IUnitofWork uow)
        {
            _uow = uow;
        }

        public async Task<TakeoutConfigResponse> CreateAsync(CreateTakeoutConfigRequest req)
        {
            if (req.TakeoutPercentage < 0 || req.TakeoutPercentage >= 1)
                throw new ArgumentException("TakeoutPercentage must be between 0 and 1 (e.g. 0.20 for 20%).");

            TakeoutConfig config = new TakeoutConfig
            {
                TakeoutConfigId = Guid.NewGuid(),
                TakeoutPercentage = req.TakeoutPercentage,
                Status = "Inactive",
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _uow.GetRepository<TakeoutConfig>().AddAsync(config);
            await _uow.SaveAsync();
            return MapToResponse(config);
        }

        public async Task<TakeoutConfigResponse> GetActiveConfigAsync()
        {
            TakeoutConfig? config = await _uow.GetRepository<TakeoutConfig>().Entities
                .FirstOrDefaultAsync(c => c.Status == "Active");
            if (config == null)
                throw new KeyNotFoundException("No active takeout config found.");
            return MapToResponse(config);
        }

        public async Task<PagedResponse<TakeoutConfigResponse>> GetAllPagedAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var repo = _uow.GetRepository<TakeoutConfig>();
            int total = await repo.Entities.CountAsync();
            IEnumerable<TakeoutConfigResponse> items = await repo.FindAsync(
                predicate: null,
                orderBy: q => q.OrderByDescending(c => c.CreatedAt),
                selector: c => new TakeoutConfigResponse
                {
                    Id = c.TakeoutConfigId,
                    TakeoutPercentage = c.TakeoutPercentage,
                    Status = c.Status,
                    CreatedAt = c.CreatedAt
                },
                pageIndex: page - 1,
                pageSize: pageSize);

            return new PagedResponse<TakeoutConfigResponse>
            {
                Items = items.ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            };
        }

        public async Task SetActiveAsync(Guid id)
        {
            var repo = _uow.GetRepository<TakeoutConfig>();
            TakeoutConfig? target = await repo.Entities.FirstOrDefaultAsync(c => c.TakeoutConfigId == id);
            if (target == null)
                throw new KeyNotFoundException("Takeout config not found.");

            List<TakeoutConfig> all = await repo.Entities.ToListAsync();
            foreach (TakeoutConfig c in all)
                c.Status = "Inactive";

            target.Status = "Active";
            await _uow.SaveAsync();
        }

        private static TakeoutConfigResponse MapToResponse(TakeoutConfig c) => new TakeoutConfigResponse
        {
            Id = c.TakeoutConfigId,
            TakeoutPercentage = c.TakeoutPercentage,
            Status = c.Status,
            CreatedAt = c.CreatedAt
        };
    }
}
