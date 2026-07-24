using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HorseRacingAPI.Services
{
    public class RefereeReportService : IRefereeReportService
    {
        private readonly IUnitofWork _uow;
        private readonly IRaceSettlementService _settlementService;
        private readonly IHubContext<RaceHub> _hubContext;

        public RefereeReportService(IUnitofWork uow, IRaceSettlementService settlementService, IHubContext<RaceHub> hubContext)
        {
            _uow = uow;
            _settlementService = settlementService;
            _hubContext = hubContext;
        }

        public async Task<RefereeReportResponse> CreateReportAsync(Guid refereeId, CreateRefereeReportDto dto)
        {
            var race = await _uow.GetRepository<Race>().Entities
                .FirstOrDefaultAsync(r => r.RaceId == dto.RaceId)
                ?? throw new KeyNotFoundException("Race not found.");
            if (race.Status != RaceStatus.Live)
                throw new InvalidOperationException("Reports can only be submitted while the race is Live.");

            if (race.RefereeId != refereeId)
                throw new UnauthorizedAccessException("You are not assigned to this race.");

            _ = await _uow.GetRepository<Registration>().Entities
                .FirstOrDefaultAsync(r => r.RegistrationId == dto.RegistrationId && r.RaceId == dto.RaceId)
                ?? throw new KeyNotFoundException("Registration not found.");

            bool existing = await _uow.GetRepository<RefereeReport>().Entities
                .AnyAsync(r => r.RegistrationId == dto.RegistrationId
                            && r.RaceId == dto.RaceId
                            && (r.Status == RefereeReportStatus.Pending
                             || r.Status == RefereeReportStatus.Approved));
            if (existing)
                throw new InvalidOperationException("A report already exists for this horse in this race.");

            if (dto.PenaltyType == PenaltyType.Fine)
                throw new InvalidOperationException("Fine penalty is no longer supported. Use Warning or Disqualification.");

            var report = new RefereeReport
            {
                ReportId            = Guid.NewGuid(),
                RaceId              = dto.RaceId,
                RefereeId           = refereeId,
                RegistrationId      = dto.RegistrationId,
                IncidentDescription = dto.IncidentDescription,
                PenaltyApplied      = dto.PenaltyApplied,
                PenaltyType         = dto.PenaltyType,
                FineAmount          = null,
                Status              = RefereeReportStatus.Pending,
                CreatedAt           = DateTimeOffset.UtcNow
            };

            await _uow.GetRepository<RefereeReport>().AddAsync(report);
            await _uow.SaveAsync();
            await _hubContext.Clients.All.SendAsync("ReportUpdated", new
            {
                reportId = report.ReportId,
                raceId   = report.RaceId,
                status   = "Pending"
            });

            return MapToResponse(report);
        }

        public async Task<RefereeReportResponse> ApproveReportAsync(Guid reportId)
        {
            var report = await _uow.GetRepository<RefereeReport>().Entities
                .Include(r => r.Registration).ThenInclude(r => r.Horse)
                .FirstOrDefaultAsync(r => r.ReportId == reportId)
                ?? throw new KeyNotFoundException("Report not found.");
            if (report.Status != RefereeReportStatus.Pending)
                throw new InvalidOperationException("Only pending reports can be approved.");

            report.Status = RefereeReportStatus.Approved;
            await _uow.GetRepository<RefereeReport>().UpdateAsync(report);

            switch (report.PenaltyType)
            {
                case PenaltyType.Warning:
                    break;

                case PenaltyType.Fine:
                    await ApplyFineAsync(report);
                    break;

                case PenaltyType.Disqualification:
                    await ApplyDisqualificationAsync(report);
                    break;
            }

            await _uow.SaveAsync();
            await _settlementService.TrySettleAsync(report.RaceId);
            await _hubContext.Clients.All.SendAsync("ReportUpdated", new
            {
                reportId = report.ReportId,
                raceId   = report.RaceId,
                status   = "Approved"
            });

            return MapToResponse(report);
        }

        private async Task ApplyFineAsync(RefereeReport report)
        {
            if (report.FineAmount == null || report.FineAmount <= 0) return;

            long fine = (long)Math.Round(report.FineAmount.Value);

            var ownerProfile = await _uow.GetRepository<UserProfile>().Entities
                .FirstOrDefaultAsync(p => p.AccountId == report.Registration.Horse.OwnerId && !p.IsDeleted);
            if (ownerProfile != null)
            {
                ownerProfile.Balance = Math.Max(0, (ownerProfile.Balance ?? 0) - fine);
                ownerProfile.UpdatedAt = DateTimeOffset.UtcNow;
                await _uow.GetRepository<UserProfile>().UpdateAsync(ownerProfile);

                await _uow.GetRepository<WalletTransaction>().AddAsync(new WalletTransaction
                {
                    WalletTransactionId = Guid.NewGuid(),
                    AccountId = report.Registration.Horse.OwnerId,
                    Type = WalletTransactionType.Fine,
                    Amount = -fine,
                    BalanceAfter = ownerProfile.Balance ?? 0,
                    ReferenceId = report.ReportId,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            await _uow.GetRepository<Prize>().AddAsync(new Prize
            {
                PrizeId        = Guid.NewGuid(),
                RegistrationId = report.RegistrationId,
                PrizeType      = PrizeType.Owner,
                Amount         = -report.FineAmount.Value,
                DistributedAt  = DateTimeOffset.UtcNow
            });
        }

        private async Task ApplyDisqualificationAsync(RefereeReport report)
        {
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

            disqualifiedResult.IsDisqualified = true;
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
                        long ownerDelta = (long)Math.Round(prize.Amount ?? 0);
                        ownerProfile.Balance = Math.Max(0, (ownerProfile.Balance ?? 0) - ownerDelta);
                        ownerProfile.UpdatedAt = DateTimeOffset.UtcNow;
                        await _uow.GetRepository<UserProfile>().UpdateAsync(ownerProfile);

                        await _uow.GetRepository<WalletTransaction>().AddAsync(new WalletTransaction
                        {
                            WalletTransactionId = Guid.NewGuid(),
                            AccountId = prize.Registration.Horse.OwnerId,
                            Type = WalletTransactionType.PrizeAdjustment,
                            Amount = -ownerDelta,
                            BalanceAfter = ownerProfile.Balance ?? 0,
                            ReferenceId = prize.Registration.RegistrationId,
                            CreatedAt = DateTimeOffset.UtcNow
                        });
                    }
                }
                else if (prize.PrizeType == PrizeType.Jockey)
                {
                    var jockeyProfile = await _uow.GetRepository<JockeyProfile>().Entities
                        .FirstOrDefaultAsync(p => p.AccountId == prize.Registration.JockeyId && !p.IsDeleted);
                    if (jockeyProfile != null)
                    {
                        long jockeyReversalDelta = (long)Math.Round(prize.Amount ?? 0);
                        jockeyProfile.Balance = Math.Max(0, (jockeyProfile.Balance ?? 0) - jockeyReversalDelta);
                        jockeyProfile.UpdatedAt = DateTimeOffset.UtcNow;
                        await _uow.GetRepository<JockeyProfile>().UpdateAsync(jockeyProfile);

                        await _uow.GetRepository<WalletTransaction>().AddAsync(new WalletTransaction
                        {
                            WalletTransactionId = Guid.NewGuid(),
                            AccountId = prize.Registration.JockeyId,
                            Type = WalletTransactionType.PrizeAdjustment,
                            Amount = -jockeyReversalDelta,
                            BalanceAfter = jockeyProfile.Balance ?? 0,
                            ReferenceId = prize.Registration.RegistrationId,
                            CreatedAt = DateTimeOffset.UtcNow
                        });
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
                decimal jockeyNew = newPos switch
                {
                    1 => prizeAtNewPos * (decimal)race.JockeyRewardConfig.WinCut,
                    2 => prizeAtNewPos * (decimal)race.JockeyRewardConfig.PlaceCut,
                    _ => 0m
                };
                decimal ownerNew  = prizeAtNewPos - jockeyNew;

                if (oldPos <= positionRatios.Length)
                {
                    decimal prizeAtOldPos = racePurse * (decimal)positionRatios[oldPos - 1];
                    decimal jockeyOld = oldPos switch
                    {
                        1 => prizeAtOldPos * (decimal)race.JockeyRewardConfig.WinCut,
                        2 => prizeAtOldPos * (decimal)race.JockeyRewardConfig.PlaceCut,
                        _ => 0m
                    };
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

                    await _uow.GetRepository<WalletTransaction>().AddAsync(new WalletTransaction
                    {
                        WalletTransactionId = Guid.NewGuid(),
                        AccountId = result.Registration.Horse.OwnerId,
                        Type = WalletTransactionType.PrizeAdjustment,
                        Amount = ownerNew > 0 ? ownerDelta : -ownerDelta,
                        BalanceAfter = ownerProfile.Balance ?? 0,
                        ReferenceId = result.RegistrationId,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                }

                var jockeyProfile = await _uow.GetRepository<JockeyProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == result.Registration.JockeyId && !p.IsDeleted);
                if (jockeyProfile != null && jockeyNew != 0)
                {
                    long jockeyDelta = (long)Math.Round(Math.Abs(jockeyNew));
                    jockeyProfile.Balance = jockeyNew > 0
                        ? (jockeyProfile.Balance ?? 0) + jockeyDelta
                        : Math.Max(0, (jockeyProfile.Balance ?? 0) - jockeyDelta);
                    jockeyProfile.UpdatedAt = DateTimeOffset.UtcNow;
                    await _uow.GetRepository<JockeyProfile>().UpdateAsync(jockeyProfile);

                    await _uow.GetRepository<WalletTransaction>().AddAsync(new WalletTransaction
                    {
                        WalletTransactionId = Guid.NewGuid(),
                        AccountId = result.Registration.JockeyId,
                        Type = WalletTransactionType.PrizeAdjustment,
                        Amount = jockeyNew > 0 ? jockeyDelta : -jockeyDelta,
                        BalanceAfter = jockeyProfile.Balance ?? 0,
                        ReferenceId = result.RegistrationId,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
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
            await _settlementService.TrySettleAsync(report.RaceId);
            await _hubContext.Clients.All.SendAsync("ReportUpdated", new
            {
                reportId = report.ReportId,
                raceId   = report.RaceId,
                status   = "Rejected"
            });

            return MapToResponse(report);
        }

        public async Task<RefereeReportPagedResponse> GetReportsByRaceAsync(Guid? raceId, int page, int pageSize, Guid? refereeId = null)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            IGenericRepository<RefereeReport> reportRepo = _uow.GetRepository<RefereeReport>();
            IQueryable<RefereeReport> baseQuery = reportRepo.Entities
                .Where(r => (raceId == null || r.RaceId == raceId) && (refereeId == null || r.RefereeId == refereeId));

            int total         = await baseQuery.CountAsync();
            int pendingCount  = await baseQuery.CountAsync(r => r.Status == RefereeReportStatus.Pending);
            int approvedCount = await baseQuery.CountAsync(r => r.Status == RefereeReportStatus.Approved);
            int rejectedCount = await baseQuery.CountAsync(r => r.Status == RefereeReportStatus.Rejected);

            List<RefereeReportResponse> items = await baseQuery
                .Include(r => r.Race)
                .Include(r => r.Registration)
                    .ThenInclude(r => r.Horse)
                .Include(r => r.Referee)
                    .ThenInclude(r => r.UserProfile)
                .Include(r => r.Registration)
                    .ThenInclude(r => r.RaceResult)
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new RefereeReportResponse
                {
                    ReportId            = r.ReportId,
                    RaceId              = r.RaceId,
                    RaceNumber          = r.Race.RaceNumber,
                    RefereeId           = r.RefereeId,
                    RefereeName         = (r.Referee.UserProfile != null ? r.Referee.UserProfile.FullName : null) ?? r.Referee.Email ?? "",
                    RegistrationId      = r.RegistrationId,
                    HorseName           = r.Registration.Horse.HorseName,
                    OriginalPosition    = r.Registration.RaceResult != null ? r.Registration.RaceResult.FinishPosition : null,
                    IncidentDescription = r.IncidentDescription,
                    PenaltyApplied      = r.PenaltyApplied,
                    Status              = r.Status.ToString(),
                    CreatedAt           = r.CreatedAt,
                })
                .ToListAsync();

            return new RefereeReportPagedResponse
            {
                Items         = items,
                Page          = page,
                PageSize      = pageSize,
                TotalCount    = total,
                PendingCount  = pendingCount,
                ApprovedCount = approvedCount,
                RejectedCount = rejectedCount
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
                    .ThenInclude(r => r.UserProfile)
                .Include(r => r.Registration)
                    .ThenInclude(r => r.RaceResult)
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
                    RefereeName         = (r.Referee.UserProfile != null ? r.Referee.UserProfile.FullName : null) ?? r.Referee.Email ?? "",
                    RegistrationId      = r.RegistrationId,
                    HorseName           = r.Registration.Horse.HorseName,
                    OriginalPosition    = r.Registration.RaceResult != null ? r.Registration.RaceResult.FinishPosition : null,
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

        public async Task<RefereeReportResponse> UpdateReportAsync(Guid reportId, Guid refereeId, UpdateRefereeReportDto dto)
        {
            RefereeReport? report = await _uow.GetRepository<RefereeReport>().Entities
                .FirstOrDefaultAsync(r => r.ReportId == reportId)
                ?? throw new KeyNotFoundException("Report not found.");

            if (report.RefereeId != refereeId)
                throw new UnauthorizedAccessException("Access denied.");

            if (report.Status != RefereeReportStatus.Pending)
                throw new InvalidOperationException("Only Pending reports can be edited.");

            if (dto.PenaltyType == PenaltyType.Fine)
                throw new InvalidOperationException("Fine penalty is no longer supported. Use Warning or Disqualification.");

            report.IncidentDescription = dto.IncidentDescription ?? report.IncidentDescription;
            report.PenaltyApplied      = dto.PenaltyApplied ?? report.PenaltyApplied;
            if (dto.PenaltyType.HasValue) report.PenaltyType = dto.PenaltyType.Value;
            report.FineAmount = null;

            await _uow.GetRepository<RefereeReport>().UpdateAsync(report);
            await _uow.SaveAsync();

            return MapToResponse(report);
        }

        private static RefereeReportResponse MapToResponse(RefereeReport report) => new RefereeReportResponse
        {
            ReportId            = report.ReportId,
            RaceId              = report.RaceId,
            RefereeId           = report.RefereeId,
            RegistrationId      = report.RegistrationId,
            IncidentDescription = report.IncidentDescription,
            PenaltyApplied      = report.PenaltyApplied,
            PenaltyType         = report.PenaltyType.ToString(),
            FineAmount          = report.FineAmount,
            Status              = report.Status.ToString(),
            CreatedAt           = (DateTimeOffset)report.CreatedAt!
        };
    }
}
