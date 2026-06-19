using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class GradePurseConfigService : IGradePurseConfigService
    {
        private readonly IUnitofWork _uow;

        public GradePurseConfigService(IUnitofWork uow)
        {
            _uow = uow;
        }

        public async Task<GradePurseConfigResponse> CreateAsync(CreateGradePurseConfigRequest req)
        {
            if (req.G1Ratio < req.G2Ratio || req.G2Ratio < req.G3Ratio || req.G3Ratio < req.ListedRatio || req.ListedRatio < req.OpenRatio)
                throw new InvalidOperationException("Grade ratios must be in descending order: G1 >= G2 >= G3 >= Listed >= Open.");

            var config = new GradePurseConfig
            {
                GradePurseConfigId = Guid.NewGuid(),
                G1Ratio            = req.G1Ratio,
                G2Ratio            = req.G2Ratio,
                G3Ratio            = req.G3Ratio,
                ListedRatio        = req.ListedRatio,
                OpenRatio          = req.OpenRatio,
                Status             = ConfigStatus.Inactive,
                CreatedAt          = DateTimeOffset.UtcNow
            };
            await _uow.GetRepository<GradePurseConfig>().AddAsync(config);
            await _uow.SaveAsync();
            return MapToResponse(config);
        }

        public async Task<GradePurseConfigResponse> GetActiveAsync()
        {
            var config = await _uow.GetRepository<GradePurseConfig>().Entities
                .FirstOrDefaultAsync(c => c.Status == ConfigStatus.Active)
                ?? throw new KeyNotFoundException("No active grade purse config found.");
            return MapToResponse(config);
        }

        public async Task<PagedResponse<GradePurseConfigResponse>> GetAllPagedAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var repo = _uow.GetRepository<GradePurseConfig>();
            int total = await repo.Entities.CountAsync();
            var items = await repo.Entities
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => MapToResponse(c))
                .ToListAsync();

            return new PagedResponse<GradePurseConfigResponse>
            {
                Items      = items,
                Page       = page,
                PageSize   = pageSize,
                TotalCount = total
            };
        }

        public async Task SetActiveAsync(Guid id)
        {
            var repo = _uow.GetRepository<GradePurseConfig>();
            bool exists = await repo.Entities.AnyAsync(c => c.GradePurseConfigId == id);
            if (!exists)
                throw new KeyNotFoundException("Grade purse config not found.");

            await _uow.BeginTransactionAsync();
            try
            {
                await repo.Entities.ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, ConfigStatus.Inactive));
                var target = await repo.Entities.FirstOrDefaultAsync(c => c.GradePurseConfigId == id);
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

        private static GradePurseConfigResponse MapToResponse(GradePurseConfig c) => new GradePurseConfigResponse
        {
            Id          = c.GradePurseConfigId,
            G1Ratio     = c.G1Ratio,
            G2Ratio     = c.G2Ratio,
            G3Ratio     = c.G3Ratio,
            ListedRatio = c.ListedRatio,
            OpenRatio   = c.OpenRatio,
            Status      = c.Status.ToString(),
            CreatedAt   = c.CreatedAt
        };
    }
}
