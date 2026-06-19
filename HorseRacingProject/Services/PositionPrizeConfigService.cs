using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class PositionPrizeConfigService : IPositionPrizeConfigService
    {
        private readonly IUnitofWork _uow;

        public PositionPrizeConfigService(IUnitofWork uow)
        {
            _uow = uow;
        }

        public async Task<PositionPrizeConfigResponse> CreateAsync(CreatePositionPrizeConfigRequest req)
        {
            double sum = (double)req.Pos1Ratio + req.Pos2Ratio + req.Pos3Ratio + req.Pos4Ratio + req.Pos5Ratio + req.Pos6Ratio;
            if (sum > 1.0 + 1e-5)
                throw new InvalidOperationException($"Sum of position ratios ({sum:P1}) must not exceed 100%.");

            var config = new PositionPrizeConfig
            {
                PositionPrizeConfigId = Guid.NewGuid(),
                Pos1Ratio             = req.Pos1Ratio,
                Pos2Ratio             = req.Pos2Ratio,
                Pos3Ratio             = req.Pos3Ratio,
                Pos4Ratio             = req.Pos4Ratio,
                Pos5Ratio             = req.Pos5Ratio,
                Pos6Ratio             = req.Pos6Ratio,
                Status                = ConfigStatus.Inactive,
                CreatedAt             = DateTimeOffset.UtcNow
            };
            await _uow.GetRepository<PositionPrizeConfig>().AddAsync(config);
            await _uow.SaveAsync();
            return MapToResponse(config);
        }

        public async Task<PositionPrizeConfigResponse> GetActiveAsync()
        {
            var config = await _uow.GetRepository<PositionPrizeConfig>().Entities
                .FirstOrDefaultAsync(c => c.Status == ConfigStatus.Active)
                ?? throw new KeyNotFoundException("No active position prize config found.");
            return MapToResponse(config);
        }

        public async Task<PagedResponse<PositionPrizeConfigResponse>> GetAllPagedAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var repo = _uow.GetRepository<PositionPrizeConfig>();
            int total = await repo.Entities.CountAsync();
            var items = await repo.Entities
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => MapToResponse(c))
                .ToListAsync();

            return new PagedResponse<PositionPrizeConfigResponse>
            {
                Items      = items,
                Page       = page,
                PageSize   = pageSize,
                TotalCount = total
            };
        }

        public async Task SetActiveAsync(Guid id)
        {
            var repo = _uow.GetRepository<PositionPrizeConfig>();
            bool exists = await repo.Entities.AnyAsync(c => c.PositionPrizeConfigId == id);
            if (!exists)
                throw new KeyNotFoundException("Position prize config not found.");

            await _uow.BeginTransactionAsync();
            try
            {
                await repo.Entities.ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, ConfigStatus.Inactive));
                var target = await repo.Entities.FirstOrDefaultAsync(c => c.PositionPrizeConfigId == id);
                target!.Status = ConfigStatus.Active;
                await _uow.SaveAsync();
                await _uow.CommitTransactionAsync();
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        private static PositionPrizeConfigResponse MapToResponse(PositionPrizeConfig c) => new PositionPrizeConfigResponse
        {
            Id        = c.PositionPrizeConfigId,
            Pos1Ratio = c.Pos1Ratio,
            Pos2Ratio = c.Pos2Ratio,
            Pos3Ratio = c.Pos3Ratio,
            Pos4Ratio = c.Pos4Ratio,
            Pos5Ratio = c.Pos5Ratio,
            Pos6Ratio = c.Pos6Ratio,
            Status    = c.Status.ToString(),
            CreatedAt = c.CreatedAt
        };
    }
}
