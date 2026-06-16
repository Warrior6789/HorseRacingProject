using HorseRacingAPI.Dtos;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class RacecourseService : IRacecourseService
    {
        private readonly IUnitofWork _uow;

        public RacecourseService(IUnitofWork uow)
        {
            _uow = uow;
        }

        public async Task<List<RacecourseResponse>> GetAllRacecoursesAsync()
        {
            IGenericRepository<Racecourse> repo = _uow.GetRepository<Racecourse>();
            return await repo.Entities
                .Where(r => !r.IsDeleted)
                .Select(r => new RacecourseResponse
                {
                    RacecourseId = r.Id,
                    RacecourseName = r.RacecourseName,
                    Location = r.Location,
                    TrackType = r.TrackType
                })
                .ToListAsync();
        }

        public async Task<PagedResponse<RacecourseResponse>> GetAllRacecoursesPagingAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;
            IGenericRepository<Racecourse> repo = _uow.GetRepository<Racecourse>();

            IEnumerable<RacecourseResponse> items = await repo.FindAsync(
                predicate: r => !r.IsDeleted,
                orderBy: null,
                selector: r => new RacecourseResponse
                {
                    RacecourseId = r.Id,
                    RacecourseName = r.RacecourseName,
                    Location = r.Location,
                    TrackType = r.TrackType
                },
                pageIndex: page - 1,
                pageSize: pageSize
            );

            int total = await repo.Entities.CountAsync(r => !r.IsDeleted);

            return new PagedResponse<RacecourseResponse>
            {
                Items = items.ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            };
        }

        public async Task<RacecourseResponse> GetRacecourseByIdAsync(Guid id)
        {
            IGenericRepository<Racecourse> repo = _uow.GetRepository<Racecourse>();
            Racecourse? racecourse = await repo.GetByIdAsync(id);

            if (racecourse == null || racecourse.IsDeleted)
                throw new KeyNotFoundException($"Racecourse with id {id} not found.");

            return MapToResponse(racecourse);
        }

        public async Task<RacecourseResponse> CreateRacecourseAsync(CreateRacecourseRequest request)
        {
            IGenericRepository<Racecourse> repo = _uow.GetRepository<Racecourse>();

            Racecourse racecourse = new Racecourse
            {
                RacecourseName = request.RacecourseName,
                Location = request.Location,
                TrackType = request.TrackType
            };

            await repo.AddAsync(racecourse);
            await _uow.SaveAsync();

            return MapToResponse(racecourse);
        }

        public async Task<RacecourseResponse> UpdateRacecourseAsync(Guid id, UpdateRacecourseRequest request)
        {
            IGenericRepository<Racecourse> repo = _uow.GetRepository<Racecourse>();
            Racecourse? racecourse = await repo.GetByIdAsync(id);

            if (racecourse == null || racecourse.IsDeleted)
                throw new KeyNotFoundException($"Racecourse with id {id} not found.");

            if (request.RacecourseName != null)
                racecourse.RacecourseName = request.RacecourseName;

            if (request.Location != null)
                racecourse.Location = request.Location;

            if (request.TrackType != null)
                racecourse.TrackType = request.TrackType;

            await repo.UpdateAsync(racecourse);
            await _uow.SaveAsync();

            return MapToResponse(racecourse);
        }

        public async Task<bool> DeleteRacecourseAsync(Guid id)
        {
            IGenericRepository<Racecourse> repo = _uow.GetRepository<Racecourse>();
            Racecourse? racecourse = await repo.GetByIdAsync(id);

            if (racecourse == null || racecourse.IsDeleted)
                throw new KeyNotFoundException($"Racecourse with id {id} not found.");

            racecourse.IsDeleted = true;
            racecourse.DeletedAt = DateTimeOffset.UtcNow;

            await repo.UpdateAsync(racecourse);
            await _uow.SaveAsync();

            return true;
        }

        private static RacecourseResponse MapToResponse(Racecourse r) => new RacecourseResponse
        {
            RacecourseId = r.Id,
            RacecourseName = r.RacecourseName,
            Location = r.Location,
            TrackType = r.TrackType
        };
    }
}
