using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class ConversionRateService : IConversionRateService
    {
        private readonly IUnitofWork _uow;
        public ConversionRateService(IUnitofWork uow)
        {
            _uow = uow;
        }

        public async Task<ConversionRateResponse> CreateAsync(CreateConversionRateRequest req)
        {
            IGenericRepository<ConversionRate> conversionRateRepo = _uow.GetRepository<ConversionRate>();
            ConversionRate newConversionRate = new ConversionRate
            {
                ConversionRateId = Guid.NewGuid(),
                RateValue = req.RateValue,
                Status = ConfigStatus.Inactive
            };
            await conversionRateRepo.AddAsync(newConversionRate);
            await _uow.SaveAsync();
            return MapToResponse(newConversionRate);
        }

        public async Task<ConversionRateResponse> GetActiveRateAsync()
        {
            IGenericRepository<ConversionRate> conversionRateRepo = _uow.GetRepository<ConversionRate>();

            ConversionRate? activeRate = await conversionRateRepo.Entities
                .FirstOrDefaultAsync(c => c.Status == ConfigStatus.Active);
            if (activeRate == null)
                throw new KeyNotFoundException("Active conversion rate not found.");

            return MapToResponse(activeRate);
        }

        public async Task<PagedResponse<ConversionRateResponse>> GetAllAsyncPage(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;
            IGenericRepository<ConversionRate> repo = _uow.GetRepository<ConversionRate>();
            IEnumerable<ConversionRateResponse> conversionRates = await repo.FindAsync(
                predicate: null,
                orderBy: q => q.OrderByDescending(c => c.ConversionRateId),
                selector: c => new ConversionRateResponse
                {
                    Id = c.ConversionRateId,
                    RateValue = c.RateValue,
                    Status = c.Status
                },
                pageIndex: page - 1,
                pageSize: pageSize);

            int total = await repo.Entities.CountAsync();
            return new PagedResponse<ConversionRateResponse>
            {
                Items = conversionRates.ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            };
        }

        public async Task SetActiveAsync(Guid id)
        {
            IGenericRepository<ConversionRate> repo = _uow.GetRepository<ConversionRate>();

            bool exists = await repo.Entities.AnyAsync(c => c.ConversionRateId == id);
            if (!exists)
                throw new KeyNotFoundException("Conversion rate not found.");

            await _uow.BeginTransactionAsync();
            try
            {
                await repo.Entities.ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, ConfigStatus.Inactive));
                ConversionRate? target = repo.Entities.FirstOrDefault(c => c.ConversionRateId == id);
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

        private static ConversionRateResponse MapToResponse(ConversionRate c) => new ConversionRateResponse
        {
            Id = c.ConversionRateId,
            RateValue = c.RateValue,
            Status = c.Status
        };
    }
}
