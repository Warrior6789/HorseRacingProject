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
        private readonly ICloudinaryService _cloudinaryService;

        public RacecourseService(IUnitofWork uow, ICloudinaryService cloudinaryService)
        {
            _uow = uow;
            _cloudinaryService = cloudinaryService;
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
                    TrackType = r.TrackType,
                    ImageUrl = r.ImageUrl
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
                    TrackType = r.TrackType,
                    ImageUrl = r.ImageUrl
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

        public async Task<RacecourseResponse> CreateRacecourseAsync(CreateRacecourseRequest request)
        {
            IGenericRepository<Racecourse> repo = _uow.GetRepository<Racecourse>();

            string? imageUrl = null;
            if (request.Image != null)
                imageUrl = await _cloudinaryService.UploadImageAsync(request.Image, "racecourses");

            Racecourse racecourse = new Racecourse
            {
                Id = Guid.NewGuid(),
                RacecourseName = request.RacecourseName,
                Location = request.Location,
                TrackType = request.TrackType,
                ImageUrl = imageUrl
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

        public async Task<string> UploadImageAsync(Guid id, IFormFile file)
        {
            IGenericRepository<Racecourse> repo = _uow.GetRepository<Racecourse>();
            Racecourse? racecourse = await repo.GetByIdAsync(id);
            if (racecourse == null || racecourse.IsDeleted)
                throw new KeyNotFoundException($"Racecourse with id {id} not found.");

            string? oldImageUrl = racecourse.ImageUrl;
            string newImageUrl = await _cloudinaryService.UploadImageAsync(file, "racecourses");

            try
            {
                racecourse.ImageUrl = newImageUrl;
                await _uow.SaveAsync();
            }
            catch
            {
                string newPublicId = string.Join("/", new Uri(newImageUrl).AbsolutePath
                    .TrimStart('/').Split('/').TakeLast(2)).Split('.')[0];
                await _cloudinaryService.DeleteImageAsync(newPublicId);
                throw;
            }

            if (oldImageUrl != null)
            {
                string oldPublicId = string.Join("/", new Uri(oldImageUrl).AbsolutePath
                    .TrimStart('/').Split('/').TakeLast(2)).Split('.')[0];
                await _cloudinaryService.DeleteImageAsync(oldPublicId);
            }

            return newImageUrl;
        }

        private static RacecourseResponse MapToResponse(Racecourse r) => new RacecourseResponse
        {
            RacecourseId = r.Id,
            RacecourseName = r.RacecourseName,
            Location = r.Location,
            TrackType = r.TrackType,
            ImageUrl = r.ImageUrl
        };
    }
}
