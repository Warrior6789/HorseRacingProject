using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class JockeyRewardConfigService : IJockeyRewardConfigService
    {
        private readonly IUnitofWork _uow;

        public JockeyRewardConfigService(IUnitofWork uow)
        {
            _uow = uow;
        }

        public async Task<JockeyRewardConfigResponse> CreateAsync(CreateJockeyRewardConfigRequest req)
        {
            if (req.WinCut < req.PlaceCut)
                throw new InvalidOperationException("WinCut must be >= PlaceCut.");

            var config = new JockeyRewardConfig
            {
                JockeyRewardConfigId = Guid.NewGuid(),
                WinCut               = req.WinCut,
                PlaceCut             = req.PlaceCut,
                Status               = ConfigStatus.Inactive,
                CreatedAt            = DateTimeOffset.UtcNow
            };
            await _uow.GetRepository<JockeyRewardConfig>().AddAsync(config);
            await _uow.SaveAsync();
            return MapToResponse(config);
        }

        public async Task<JockeyRewardConfigResponse> GetActiveAsync()
        {
            var config = await _uow.GetRepository<JockeyRewardConfig>().Entities
                .FirstOrDefaultAsync(c => c.Status == ConfigStatus.Active)
                ?? throw new KeyNotFoundException("No active jockey reward config found.");
            return MapToResponse(config);
        }

        public async Task<PagedResponse<JockeyRewardConfigResponse>> GetAllPagedAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var repo = _uow.GetRepository<JockeyRewardConfig>();
            int total = await repo.Entities.CountAsync();
            var items = await repo.Entities
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => MapToResponse(c))
                .ToListAsync();

            return new PagedResponse<JockeyRewardConfigResponse>
            {
                Items      = items,
                Page       = page,
                PageSize   = pageSize,
                TotalCount = total
            };
        }

        public async Task SetActiveAsync(Guid id)
        {
            var repo = _uow.GetRepository<JockeyRewardConfig>();
            bool exists = await repo.Entities.AnyAsync(c => c.JockeyRewardConfigId == id);
            if (!exists)
                throw new KeyNotFoundException("Jockey reward config not found.");

            await _uow.BeginTransactionAsync();
            try
            {
                await repo.Entities.ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, ConfigStatus.Inactive));
                var target = await repo.Entities.FirstOrDefaultAsync(c => c.JockeyRewardConfigId == id);
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

        private static JockeyRewardConfigResponse MapToResponse(JockeyRewardConfig c) => new JockeyRewardConfigResponse
        {
            Id        = c.JockeyRewardConfigId,
            WinCut    = c.WinCut,
            PlaceCut  = c.PlaceCut,
            Status    = c.Status.ToString(),
            CreatedAt = c.CreatedAt
        };
    }
}
