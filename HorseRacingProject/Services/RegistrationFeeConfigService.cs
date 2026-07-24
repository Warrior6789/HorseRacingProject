using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class RegistrationFeeConfigService : IRegistrationFeeConfigService
    {
        private readonly IUnitofWork _uow;

        public RegistrationFeeConfigService(IUnitofWork uow)
        {
            _uow = uow;
        }

        public async Task<RegistrationFeeConfigResponse> CreateAsync(CreateRegistrationFeeConfigRequest req)
        {
            RegistrationFeeConfig config = new RegistrationFeeConfig
            {
                RegistrationFeeConfigId = Guid.NewGuid(),
                FeeAmount = req.FeeAmount,
                Status = ConfigStatus.Inactive,
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _uow.GetRepository<RegistrationFeeConfig>().AddAsync(config);
            await _uow.SaveAsync();
            return MapToResponse(config);
        }

        public async Task<RegistrationFeeConfigResponse> GetActiveAsync()
        {
            RegistrationFeeConfig? config = await _uow.GetRepository<RegistrationFeeConfig>().Entities
                .FirstOrDefaultAsync(c => c.Status == ConfigStatus.Active);
            if (config == null)
                throw new KeyNotFoundException("No active registration fee config found.");
            return MapToResponse(config);
        }

        public async Task<PagedResponse<RegistrationFeeConfigResponse>> GetAllPagedAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var repo = _uow.GetRepository<RegistrationFeeConfig>();
            int total = await repo.Entities.CountAsync();
            IEnumerable<RegistrationFeeConfigResponse> items = await repo.FindAsync(
                predicate: null,
                orderBy: q => q.OrderByDescending(c => c.CreatedAt),
                selector: c => new RegistrationFeeConfigResponse
                {
                    Id = c.RegistrationFeeConfigId,
                    FeeAmount = c.FeeAmount,
                    Status = c.Status.ToString(),
                    CreatedAt = c.CreatedAt
                },
                pageIndex: page - 1,
                pageSize: pageSize);

            return new PagedResponse<RegistrationFeeConfigResponse>
            {
                Items = items.ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            };
        }

        public async Task SetActiveAsync(Guid id)
        {
            var repo = _uow.GetRepository<RegistrationFeeConfig>();
            RegistrationFeeConfig? target = await repo.Entities
                .FirstOrDefaultAsync(c => c.RegistrationFeeConfigId == id);
            if (target == null)
                throw new KeyNotFoundException("Registration fee config not found.");

            List<RegistrationFeeConfig> all = await repo.Entities.ToListAsync();
            foreach (RegistrationFeeConfig c in all)
                c.Status = ConfigStatus.Inactive;

            target.Status = ConfigStatus.Active;
            await _uow.SaveAsync();
        }

        private static RegistrationFeeConfigResponse MapToResponse(RegistrationFeeConfig c) => new RegistrationFeeConfigResponse
        {
            Id = c.RegistrationFeeConfigId,
            FeeAmount = c.FeeAmount,
            Status = c.Status.ToString(),
            CreatedAt = c.CreatedAt
        };
    }
}
