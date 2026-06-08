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

            tournament.Status = newStatus.ToString();

            await tournamentRepo.UpdateAsync(tournament);
            await _uow.SaveAsync();

            return true;
        }

        public async Task<TournamentResponse> CreateTournamentAsync(CreateTournamentRequest request)
        {
            IGenericRepository<Tournament> tournamentRepo = _uow.GetRepository<Tournament>();
            Tournament tournament = new Tournament
            {
                TournamentName = request.TournamentName,
                Description = request.Description,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Status = TournamentStatus.Upcoming.ToString(),
            };
            await tournamentRepo.AddAsync(tournament);
            await _uow.SaveAsync();
            return new TournamentResponse
            {
                TournamentId = tournament.Id,
                TournamentName = tournament.TournamentName,
                Description = tournament.Description,
                StartDate = tournament.StartDate,
                EndDate = tournament.EndDate,
                Status = tournament.Status
            };
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
                .Where(t => !t.IsDeleted && t.Status != TournamentStatus.Cancelled.ToString())
                .Select(t => new TournamentResponse
            {
                TournamentId = t.Id,
                TournamentName = t.TournamentName,
                Description = t.Description,
                StartDate = t.StartDate,
                EndDate = t.EndDate,
                Status = t.Status
            }).ToListAsync();
            return tournaments;
        }

        public async Task<PagedResponse<TournamentResponse>> GetAllTournamentsPagingAsync(int page, int pageSize)
        {
            IGenericRepository<Tournament> tournamentRepo = _uow.GetRepository<Tournament>();

            IEnumerable<TournamentResponse> items = await tournamentRepo
                .FindAsync(
                predicate: t => !t.IsDeleted && t.Status != TournamentStatus.Cancelled.ToString(),
                orderBy: null,
                selector: t => new TournamentResponse
                {
                    TournamentId = t.Id,
                    TournamentName = t.TournamentName,
                    Description = t.Description,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    Status = t.Status
                },
                pageIndex: page - 1,
                pageSize: pageSize
                );
                

            int total = await tournamentRepo.Entities.CountAsync(t => !t.IsDeleted && t.Status != TournamentStatus.Cancelled.ToString());
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
            if(tournament == null || tournament.IsDeleted)
            {
                throw new KeyNotFoundException($"Tournament with id {id} not found.");
            }
            TournamentResponse tournamentResponse = new TournamentResponse
            {
                TournamentId = tournament.Id,
                TournamentName = tournament.TournamentName,
                Description = tournament.Description,
                StartDate = tournament.StartDate,
                EndDate = tournament.EndDate,
                Status = tournament.Status
            };
            return tournamentResponse;
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

            await tournamentRepo.UpdateAsync(tournament);
            await _uow.SaveAsync();

            return new TournamentResponse
            {
                TournamentId = tournament.Id,
                TournamentName = tournament.TournamentName,
                Description = tournament.Description,
                StartDate = tournament.StartDate,
                EndDate = tournament.EndDate,
                Status = tournament.Status
            };
        }
    }
}
