using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class RefereeReportService : IRefereeReportService
    {
        private readonly IUnitofWork _uow;
        public RefereeReportService(IUnitofWork uow)
        {
            _uow = uow;
        }

        public async Task<RefereeReportResponse> CreateReportAsync(Guid refereeId, CreateRefereeReportDto dto)
        {
            var race = await _uow.GetRepository<Race>().Entities
                .FirstOrDefaultAsync(r => r.RaceId == dto.RaceId)
                ?? throw new KeyNotFoundException("Race not found.");
            if (race.Status != RaceStatus.Finished)
                throw new InvalidOperationException("Race has not finished yet.");

            if (race.RefereeId != refereeId)
                throw new UnauthorizedAccessException("You are not assigned to this race.");

            _ = await _uow.GetRepository<Registration>().Entities
                .FirstOrDefaultAsync(r => r.RegistrationId == dto.RegistrationId && r.RaceId == dto.RaceId)
                ?? throw new KeyNotFoundException("Registration not found.");

            bool resultExists = await _uow.GetRepository<RaceResult>().Entities
                .AnyAsync(r => r.RegistrationId == dto.RegistrationId);
            if (!resultExists)
                throw new InvalidOperationException("No race result recorded for this horse. Cannot create a report.");

            bool existing = await _uow.GetRepository<RefereeReport>().Entities
                .AnyAsync(r => r.RegistrationId == dto.RegistrationId
                            && r.RaceId == dto.RaceId
                            && (r.Status == RefereeReportStatus.Pending
                             || r.Status == RefereeReportStatus.Approved));
            if (existing)
                throw new InvalidOperationException("A report already exists for this horse in this race.");

            var report = new RefereeReport
            {
                ReportId            = Guid.NewGuid(),
                RaceId              = dto.RaceId,
                RefereeId           = refereeId,
                RegistrationId      = dto.RegistrationId,
                IncidentDescription = dto.IncidentDescription,
                PenaltyApplied      = dto.PenaltyApplied,
                Status              = RefereeReportStatus.Pending,
                CreatedAt           = DateTimeOffset.UtcNow
            };

            await _uow.GetRepository<RefereeReport>().AddAsync(report);
            await _uow.SaveAsync();

            return MapToResponse(report);
        }

        public async Task<RefereeReportResponse> ApproveReportAsync(Guid reportId)
        {
            var report = await _uow.GetRepository<RefereeReport>().Entities
                .FirstOrDefaultAsync(r => r.ReportId == reportId)
                ?? throw new KeyNotFoundException("Report not found.");
            if (report.Status != RefereeReportStatus.Pending)
                throw new InvalidOperationException("Only pending reports can be approved.");

            var disqualifiedResult = await _uow.GetRepository<RaceResult>().Entities
                .Include(r => r.Registration).ThenInclude(r => r.Horse)
                .FirstOrDefaultAsync(r => r.RegistrationId == report.RegistrationId
                                       && r.Registration.RaceId == report.RaceId)
                ?? throw new KeyNotFoundException("Race result not found.");
            int disqualifiedPos = disqualifiedResult.FinishPosition
                ?? throw new InvalidOperationException("Race result has no finish position.");

            var race = await _uow.GetRepository<Race>().Entities
                .Include(r => r.PositionPrizeConfig)
                .Include(r => r.JockeyRewardConfig)
                .FirstOrDefaultAsync(r => r.RaceId == report.RaceId)
                ?? throw new KeyNotFoundException("Race not found.");

            if (race.PositionPrizeConfig == null || race.JockeyRewardConfig == null)
                throw new InvalidOperationException("Race prize configs are not set.");

            decimal racePurse = race.PrizePool;

            double[] positionRatios =
            [
                race.PositionPrizeConfig.Pos1Ratio, race.PositionPrizeConfig.Pos2Ratio,
                race.PositionPrizeConfig.Pos3Ratio, race.PositionPrizeConfig.Pos4Ratio,
                race.PositionPrizeConfig.Pos5Ratio, race.PositionPrizeConfig.Pos6Ratio
            ];

            report.Status = RefereeReportStatus.Approved;
            disqualifiedResult.IsDisqualified = true;
            await _uow.GetRepository<RefereeReport>().UpdateAsync(report);
            await _uow.GetRepository<RaceResult>().UpdateAsync(disqualifiedResult);

            var disqualifiedPrizes = await _uow.GetRepository<Prize>().Entities
                .Include(p => p.Registration).ThenInclude(r => r.Horse)
                .Where(p => p.RegistrationId == report.RegistrationId && p.Amount > 0)
                .ToListAsync();

            foreach (var prize in disqualifiedPrizes)
            {
                if (prize.PrizeType == PrizeType.Owner)
                {
                    var ownerProfile = await _uow.GetRepository<UserProfile>().Entities
                        .FirstOrDefaultAsync(p => p.AccountId == prize.Registration.Horse.OwnerId && !p.IsDeleted);
                    if (ownerProfile != null)
                    {
                        ownerProfile.Balance = Math.Max(0, (ownerProfile.Balance ?? 0) - (long)Math.Round(prize.Amount ?? 0));
                        ownerProfile.UpdatedAt = DateTimeOffset.UtcNow;
                        await _uow.GetRepository<UserProfile>().UpdateAsync(ownerProfile);
                    }
                }
                else if (prize.PrizeType == PrizeType.Jockey)
                {
                    var jockeyProfile = await _uow.GetRepository<UserProfile>().Entities
                        .FirstOrDefaultAsync(p => p.AccountId == prize.Registration.JockeyId && !p.IsDeleted);
                    if (jockeyProfile != null)
                    {
                        jockeyProfile.Balance = Math.Max(0, (jockeyProfile.Balance ?? 0) - (long)Math.Round(prize.Amount ?? 0));
                        jockeyProfile.UpdatedAt = DateTimeOffset.UtcNow;
                        await _uow.GetRepository<UserProfile>().UpdateAsync(jockeyProfile);
                    }
                }

                await _uow.GetRepository<Prize>().AddAsync(new Prize
                {
                    PrizeId        = Guid.NewGuid(),
                    RegistrationId = report.RegistrationId,
                    PrizeType      = prize.PrizeType,
                    Amount         = -(prize.Amount ?? 0),
                    DistributedAt  = DateTimeOffset.UtcNow
                });
            }

            var promotedResults = await _uow.GetRepository<RaceResult>().Entities
                .Include(r => r.Registration).ThenInclude(r => r.Horse)
                .Where(r => r.Registration.RaceId == report.RaceId
                         && r.FinishPosition > disqualifiedPos
                         && r.IsDisqualified != true)
                .OrderBy(r => r.FinishPosition)
                .ToListAsync();

            foreach (var result in promotedResults)
            {
                int oldPos = result.FinishPosition!.Value;
                int newPos = oldPos - 1;
                result.FinishPosition = newPos;
                await _uow.GetRepository<RaceResult>().UpdateAsync(result);

                if (newPos > positionRatios.Length) continue;

                decimal prizeAtNewPos = racePurse * (decimal)positionRatios[newPos - 1];
                decimal jockeyNew = prizeAtNewPos * (decimal)(newPos == 1 ? race.JockeyRewardConfig.WinCut : race.JockeyRewardConfig.PlaceCut);
                decimal ownerNew  = prizeAtNewPos - jockeyNew;

                if (oldPos <= positionRatios.Length)
                {
                    decimal prizeAtOldPos = racePurse * (decimal)positionRatios[oldPos - 1];
                    decimal jockeyOld = prizeAtOldPos * (decimal)(oldPos == 1 ? race.JockeyRewardConfig.WinCut : race.JockeyRewardConfig.PlaceCut);
                    jockeyNew -= jockeyOld;
                    ownerNew  -= (prizeAtOldPos - jockeyOld);
                }

                var ownerProfile = await _uow.GetRepository<UserProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == result.Registration.Horse.OwnerId && !p.IsDeleted);
                if (ownerProfile != null && ownerNew != 0)
                {
                    long ownerDelta = (long)Math.Round(Math.Abs(ownerNew));
                    ownerProfile.Balance = ownerNew > 0
                        ? (ownerProfile.Balance ?? 0) + ownerDelta
                        : Math.Max(0, (ownerProfile.Balance ?? 0) - ownerDelta);
                    ownerProfile.UpdatedAt = DateTimeOffset.UtcNow;
                    await _uow.GetRepository<UserProfile>().UpdateAsync(ownerProfile);
                }

                var jockeyProfile = await _uow.GetRepository<UserProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == result.Registration.JockeyId && !p.IsDeleted);
                if (jockeyProfile != null && jockeyNew != 0)
                {
                    long jockeyDelta = (long)Math.Round(Math.Abs(jockeyNew));
                    jockeyProfile.Balance = jockeyNew > 0
                        ? (jockeyProfile.Balance ?? 0) + jockeyDelta
                        : Math.Max(0, (jockeyProfile.Balance ?? 0) - jockeyDelta);
                    jockeyProfile.UpdatedAt = DateTimeOffset.UtcNow;
                    await _uow.GetRepository<UserProfile>().UpdateAsync(jockeyProfile);
                }

                if (ownerNew != 0)
                    await _uow.GetRepository<Prize>().AddAsync(new Prize
                    {
                        PrizeId        = Guid.NewGuid(),
                        RegistrationId = result.RegistrationId,
                        PrizeType      = PrizeType.Owner,
                        Amount         = ownerNew,
                        DistributedAt  = DateTimeOffset.UtcNow
                    });

                if (jockeyNew != 0)
                    await _uow.GetRepository<Prize>().AddAsync(new Prize
                    {
                        PrizeId        = Guid.NewGuid(),
                        RegistrationId = result.RegistrationId,
                        PrizeType      = PrizeType.Jockey,
                        Amount         = jockeyNew,
                        DistributedAt  = DateTimeOffset.UtcNow
                    });
            }

            await _uow.SaveAsync();

            return MapToResponse(report);
        }

        public async Task<RefereeReportResponse> RejectReportAsync(Guid reportId)
        {
            var report = await _uow.GetRepository<RefereeReport>().Entities
                .FirstOrDefaultAsync(r => r.ReportId == reportId)
                ?? throw new KeyNotFoundException("Report not found.");
            if (report.Status != RefereeReportStatus.Pending)
                throw new InvalidOperationException("Only pending reports can be rejected.");

            report.Status = RefereeReportStatus.Rejected;
            await _uow.GetRepository<RefereeReport>().UpdateAsync(report);
            await _uow.SaveAsync();

            return MapToResponse(report);
        }

        public async Task<PagedResponse<RefereeReportResponse>> GetReportsByRaceAsync(Guid raceId, int page, int pageSize, Guid? refereeId = null)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            IGenericRepository<RefereeReport> reportRepo = _uow.GetRepository<RefereeReport>();
            int total = await reportRepo.Entities
                .CountAsync(r => r.RaceId == raceId && (refereeId == null || r.RefereeId == refereeId));
            List<RefereeReportResponse> items = await reportRepo.Entities
                .Include(r => r.Race)
                .Include(r => r.Registration)
                    .ThenInclude(r => r.Horse)
                .Include(r => r.Referee)
                    .ThenInclude(r => r.UserProfiles)
                .Include(r => r.Registration)
                    .ThenInclude(r => r.RaceResults)
                .Where(r => r.RaceId == raceId && (refereeId == null || r.RefereeId == refereeId))
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new RefereeReportResponse
                {
                    ReportId            = r.ReportId,
                    RaceId              = r.RaceId,
                    RaceNumber          = r.Race.RaceNumber,
                    RefereeId           = r.RefereeId,
                    RefereeName         = r.Referee.UserProfiles.Select(up => up.FullName).FirstOrDefault() ?? r.Referee.Email ?? "",
                    RegistrationId      = r.RegistrationId,
                    HorseName           = r.Registration.Horse.HorseName,
                    OriginalPosition    = r.Registration.RaceResults.FirstOrDefault() != null ? r.Registration.RaceResults.FirstOrDefault()!.FinishPosition : null,
                    IncidentDescription = r.IncidentDescription,
                    PenaltyApplied      = r.PenaltyApplied,
                    Status              = r.Status.ToString(),
                    CreatedAt           = r.CreatedAt,
                })
                .ToListAsync();

            return new PagedResponse<RefereeReportResponse>
            {
                Items      = items,
                Page       = page,
                PageSize   = pageSize,
                TotalCount = total
            };
        }

        public async Task<PagedResponse<RefereeReportResponse>> GetMyReportsPagedAsync(Guid refereeId, int page, int pageSize)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            IGenericRepository<RefereeReport> reportRepo = _uow.GetRepository<RefereeReport>();
            int total = await reportRepo.Entities
                .CountAsync(r => r.RefereeId == refereeId);
            List<RefereeReportResponse> items = await reportRepo.Entities
                .Include(r => r.Race)
                .Include(r => r.Registration)
                    .ThenInclude(r => r.Horse)
                .Include(r => r.Referee)
                    .ThenInclude(r => r.UserProfiles)
                .Include(r => r.Registration)
                    .ThenInclude(r => r.RaceResults)
                .Where(r => r.RefereeId == refereeId)
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new RefereeReportResponse
                {
                    ReportId            = r.ReportId,
                    RaceId              = r.RaceId,
                    RaceNumber          = r.Race.RaceNumber,
                    RefereeId           = r.RefereeId,
                    RefereeName         = r.Referee.UserProfiles.Select(up => up.FullName).FirstOrDefault() ?? r.Referee.Email ?? "",
                    RegistrationId      = r.RegistrationId,
                    HorseName           = r.Registration.Horse.HorseName,
                    OriginalPosition    = r.Registration.RaceResults.FirstOrDefault() != null ? r.Registration.RaceResults.FirstOrDefault()!.FinishPosition : null,
                    IncidentDescription = r.IncidentDescription,
                    PenaltyApplied      = r.PenaltyApplied,
                    Status              = r.Status.ToString(),
                    CreatedAt           = r.CreatedAt,
                })
                .ToListAsync();

            return new PagedResponse<RefereeReportResponse>
            {
                Items      = items,
                Page       = page,
                PageSize   = pageSize,
                TotalCount = total
            };
        }

        private static RefereeReportResponse MapToResponse(RefereeReport report) => new RefereeReportResponse
        {
            ReportId            = report.ReportId,
            RaceId              = report.RaceId,
            RefereeId           = report.RefereeId,
            RegistrationId      = report.RegistrationId,
            IncidentDescription = report.IncidentDescription,
            PenaltyApplied      = report.PenaltyApplied,
            Status              = report.Status.ToString(),
            CreatedAt           = (DateTimeOffset)report.CreatedAt!
        };
    }
}
