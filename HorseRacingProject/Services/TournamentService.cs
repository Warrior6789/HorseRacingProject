using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class TournamentService : ITournamentService
    {
        private readonly IUnitofWork _uow;
        public TournamentService(IUnitofWork uow)
        {
            _uow = uow;
        }
        public async Task<bool> ChangeTournamentStatusAsync(Guid tournamentId, TournamentStatus newStatus)
        {
            IGenericRepository<Tournament> tournamentRepo = _uow.GetRepository<Tournament>();

            Tournament? tournament = await tournamentRepo.GetByIdAsync(tournamentId);

            if (tournament == null || tournament.IsDeleted)
                throw new KeyNotFoundException($"Tournament with id {tournamentId} not found.");

            tournament.Status = newStatus;

            await tournamentRepo.UpdateAsync(tournament);
            await _uow.SaveAsync();

            return true;
        }

        public async Task<TournamentResponse> CreateTournamentAsync(CreateTournamentRequest request)
        {
            IGenericRepository<Tournament> tournamentRepo = _uow.GetRepository<Tournament>();
            Tournament tournament = new Tournament
            {
                Id = Guid.NewGuid(),
                TournamentName = request.TournamentName,
                Description = request.Description,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Status = TournamentStatus.Upcoming,
                FundsPrize = request.FundsPrize
            };
            await tournamentRepo.AddAsync(tournament);
            await _uow.SaveAsync();
            return MapToResponse(tournament);
        }

        public async Task<bool> DeleteTournamentAsync(Guid tournamentId)
        {
            IGenericRepository<Tournament> tournamentRepo = _uow.GetRepository<Tournament>();

            Tournament? tournament = await tournamentRepo.GetByIdAsync(tournamentId);

            if (tournament == null || tournament.IsDeleted)
                throw new KeyNotFoundException($"Tournament with id {tournamentId} not found.");

            tournament.IsDeleted = true;
            tournament.DeletedAt = DateTimeOffset.UtcNow;

            await tournamentRepo.UpdateAsync(tournament);
            await _uow.SaveAsync();

            return true;
        }

        public async Task<List<TournamentResponse>> GetAllTournamentsAsync()
        {
            IGenericRepository<Tournament> tournamentRepo = _uow.GetRepository<Tournament>();
            List<TournamentResponse> tournaments = await tournamentRepo.Entities
                .Where(t => !t.IsDeleted && t.Status != TournamentStatus.Cancelled)
                .Select(t => new TournamentResponse
                {
                    TournamentId = t.Id,
                    TournamentName = t.TournamentName,
                    Description = t.Description,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    Status = t.Status.ToString()
                }).ToListAsync();
            return tournaments;
        }

        public async Task<PagedResponse<TournamentResponse>> GetAllTournamentsPagingAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;
            IGenericRepository<Tournament> tournamentRepo = _uow.GetRepository<Tournament>();

            IEnumerable<TournamentResponse> items = await tournamentRepo
                .FindAsync(
                predicate: t => !t.IsDeleted && t.Status != TournamentStatus.Cancelled,
                orderBy: null,
                selector: t => new TournamentResponse
                {
                    TournamentId = t.Id,
                    TournamentName = t.TournamentName,
                    Description = t.Description,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    Status = t.Status.ToString(),
                    FundsPrize = t.FundsPrize
                },
                pageIndex: page - 1,
                pageSize: pageSize
                );

            int total = await tournamentRepo.Entities.CountAsync(t => !t.IsDeleted && t.Status != TournamentStatus.Cancelled);
            PagedResponse<TournamentResponse> response = new PagedResponse<TournamentResponse>
            {
                Items = items.ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
            };
            return response;
        }

        public async Task<TournamentResponse> GetTournamentsByIdAsync(Guid id)
        {
            IGenericRepository<Tournament> tournamentRepo = _uow.GetRepository<Tournament>();
            Tournament? tournament = await tournamentRepo.GetByIdAsync(id);
            if (tournament == null || tournament.IsDeleted)
                throw new KeyNotFoundException($"Tournament with id {id} not found.");

            return MapToResponse(tournament);
        }

        public async Task<TournamentResponse> UpdateTournamentAsync(Guid tournamentId, UpdateTournamentRequest request)
        {
            IGenericRepository<Tournament> tournamentRepo = _uow.GetRepository<Tournament>();

            Tournament? tournament = await tournamentRepo.GetByIdAsync(tournamentId);

            if (tournament == null || tournament.IsDeleted)
                throw new KeyNotFoundException($"Tournament with id {tournamentId} not found.");

            if (request.TournamentName != null)
                tournament.TournamentName = request.TournamentName;

            if (request.Description != null)
                tournament.Description = request.Description;

            if (request.StartDate.HasValue)
                tournament.StartDate = request.StartDate;

            if (request.EndDate.HasValue)
                tournament.EndDate = request.EndDate;

            if (request.FundsPrize.HasValue)
                tournament.FundsPrize = request.FundsPrize.Value;

            await tournamentRepo.UpdateAsync(tournament);
            await _uow.SaveAsync();

            return MapToResponse(tournament);
        }

        private static TournamentResponse MapToResponse(Tournament t) => new TournamentResponse
        {
            TournamentId = t.Id,
            TournamentName = t.TournamentName,
            Description = t.Description,
            StartDate = t.StartDate,
            EndDate = t.EndDate,
            Status = t.Status.ToString(),
            FundsPrize = t.FundsPrize
        };
    }
}
