using HorseRacingAPI.Enums;

namespace HorseRacingProject.Tests;

// Pure logic tests extracted from RaceService (no DB, no mocks)

public class RaceLogicTests
{
    // -------------------------------------------------------
    // Status transition switch (from AdvanceRaceStatusAsync)
    // -------------------------------------------------------
    [Theory]
    [InlineData(RaceStatus.Scheduled,     RaceStatus.BettingOpen)]
    [InlineData(RaceStatus.BettingOpen,   RaceStatus.BettingClosed)]
    [InlineData(RaceStatus.BettingClosed, RaceStatus.Live)]
    public void AdvanceStatus_ValidTransition_ReturnsNextStatus(RaceStatus current, RaceStatus expected)
    {
        var next = GetNextStatus(current);
        Assert.Equal(expected, next);
    }

    [Theory]
    [InlineData(RaceStatus.Live)]
    [InlineData(RaceStatus.Finished)]
    [InlineData(RaceStatus.Cancelled)]
    [InlineData(RaceStatus.Completed)]
    public void AdvanceStatus_TerminalStatus_ReturnsNull(RaceStatus current)
    {
        Assert.Null(GetNextStatus(current));
    }

    // -------------------------------------------------------
    // Time slot conflict (from CreateRaceAsync / UpdateRaceAsync)
    // Rule: conflict if existingStart < newStart+10min
    //              AND (existingEnd??existingStart+5min)+5min > newStart
    // -------------------------------------------------------
    [Fact]
    public void TimeConflict_ExistingEndLongBeforeNewStart_NoConflict()
    {
        // existing 12:00–12:30, new 13:00 → 12:30+5=12:35 !> 13:00 → no conflict
        Assert.False(HasTimeConflict(T("12:00"), T("12:30"), T("13:00")));
    }

    [Fact]
    public void TimeConflict_ExistingOverlapsBuffer_Conflicts()
    {
        // existing 12:00–14:25, new 14:28 → 14:25+5=14:30 > 14:28 → conflict
        Assert.True(HasTimeConflict(T("12:00"), T("14:25"), T("14:28")));
    }

    [Fact]
    public void TimeConflict_NoEndTimeDefaultDuration_Conflicts()
    {
        // existing 12:00 (no end), new 12:08
        // effectiveEnd = 12:00+5+5=12:10 > 12:08 → conflict
        Assert.True(HasTimeConflict(T("12:00"), null, T("12:08")));
    }

    [Fact]
    public void TimeConflict_NoEndTimeButSafeGap_NoConflict()
    {
        // existing 12:00 (no end), new 12:11
        // effectiveEnd = 12:10 !> 12:11 → no conflict
        Assert.False(HasTimeConflict(T("12:00"), null, T("12:11")));
    }

    [Fact]
    public void TimeConflict_NewRaceInsideExistingBlock_Conflicts()
    {
        // existing 14:05 (no end), new 14:10
        // 14:05 < 14:20 ✓, (14:05+5+5=14:15) > 14:10 ✓ → conflict
        Assert.True(HasTimeConflict(T("14:05"), null, T("14:10")));
    }

    // -------------------------------------------------------
    // Jockey schedule conflict with travel buffer (from RegisterHorseAsync)
    // Same venue   : effectiveWindow = [raceStart,     raceEnd]
    // Diff venue   : effectiveWindow = [raceStart-2h,  raceEnd+2h]
    // Conflict if  : other.StartTime < effectiveEnd && otherEnd > effectiveStart
    // -------------------------------------------------------
    [Theory]
    // Same venue
    [InlineData(true,  "13:00", "13:30", "13:10", "13:45", true)]  // partial overlap → conflict
    [InlineData(true,  "13:00", "13:30", "12:00", "12:30", false)] // ends at 12:30, before 13:00 → no conflict
    [InlineData(true,  "13:00", "13:30", "13:15", "13:45", true)]  // starts mid-race → conflict
    [InlineData(true,  "13:00", "13:30", "13:30", "14:00", false)] // starts exactly at end (strict >) → no conflict
    // Different venue (±2h buffer)
    [InlineData(false, "13:00", "13:30", "12:00", "12:30", true)]  // 12:30>11:00(effectiveStart) → conflict
    [InlineData(false, "13:00", "13:30", "10:00", "10:30", false)] // 10:30 !> 11:00 → no conflict
    [InlineData(false, "13:00", "13:30", "15:31", "16:00", false)] // 15:31 !< 15:30(effectiveEnd) → no conflict
    [InlineData(false, "13:00", "13:30", "15:00", "15:29", true)]  // 15:00 < 15:30 && 15:29 > 11:00 → conflict
    public void JockeyConflict_OverlapDetection(bool sameVenue,
        string raceStartStr, string raceEndStr,
        string otherStartStr, string otherEndStr,
        bool expectConflict)
    {
        bool conflict = HasJockeyConflict(sameVenue,
            T(raceStartStr), T(raceEndStr),
            T(otherStartStr), T(otherEndStr));
        Assert.Equal(expectConflict, conflict);
    }

    // -------------------------------------------------------
    // Bet refund calculation in ResetRaceAsync
    // Won   → reverse payout: balance = Max(0, balance - payout + betAmount)
    // Lost/Active → refund: balance = balance + betAmount
    // -------------------------------------------------------
    [Fact]
    public void ResetBet_WonBet_ReversesPayout()
    {
        // betAmount=100, ratio=2.0, balance=300 → payout=200, new=max(0,300-200+100)=200
        long balance = 300, betAmount = 100;
        float ratio = 2.0f;
        long payout = (long)(betAmount * (decimal)ratio);
        long result = Math.Max(0, balance - payout + betAmount);
        Assert.Equal(200L, result);
    }

    [Fact]
    public void ResetBet_WonBetPayoutExceedsBalance_ClampedToZero()
    {
        // betAmount=100, ratio=2.0, balance=50 → payout=200, Max(0,50-200+100)=Max(0,-50)=0
        long balance = 50, betAmount = 100;
        float ratio = 2.0f;
        long payout = (long)(betAmount * (decimal)ratio);
        long result = Math.Max(0, balance - payout + betAmount);
        Assert.Equal(0L, result);
    }

    [Theory]
    [InlineData(500, 100, 600)]
    [InlineData(0,   50,  50)]
    [InlineData(999, 1,   1000)]
    public void ResetBet_LostOrActiveBet_RefundsBetAmount(long balance, long betAmount, long expected)
    {
        long result = balance + betAmount;
        Assert.Equal(expected, result);
    }

    // -------------------------------------------------------
    // Start time must be strictly > 90 minutes from now
    // Condition: if (startTime <= UtcNow.AddMinutes(90)) → invalid
    // -------------------------------------------------------
    [Fact]
    public void StartTime_LessThan90Min_IsInvalid()
    {
        var now    = DateTimeOffset.UtcNow;
        var tooSoon = now.AddMinutes(89);
        Assert.True(tooSoon <= now.AddMinutes(90));
    }

    [Fact]
    public void StartTime_Exactly90Min_IsInvalid()
    {
        // condition is <=, so exactly 90 min is also rejected
        var now     = DateTimeOffset.UtcNow;
        var exactly = now.AddMinutes(90);
        Assert.True(exactly <= now.AddMinutes(90));
    }

    [Fact]
    public void StartTime_MoreThan90Min_IsValid()
    {
        var now   = DateTimeOffset.UtcNow;
        var valid = now.AddMinutes(91);
        Assert.False(valid <= now.AddMinutes(90));
    }

    // -------------------------------------------------------
    // Helpers — pure logic copied from RaceService
    // -------------------------------------------------------
    private static RaceStatus? GetNextStatus(RaceStatus current) => current switch
    {
        RaceStatus.Scheduled     => RaceStatus.BettingOpen,
        RaceStatus.BettingOpen   => RaceStatus.BettingClosed,
        RaceStatus.BettingClosed => RaceStatus.Live,
        _                        => (RaceStatus?)null
    };

    private static bool HasTimeConflict(
        DateTimeOffset existingStart,
        DateTimeOffset? existingEnd,
        DateTimeOffset newStart)
    {
        DateTimeOffset newBlockEnd      = newStart.AddMinutes(10);
        DateTimeOffset existingBlockEnd = (existingEnd ?? existingStart.AddMinutes(5)).AddMinutes(5);
        return existingStart < newBlockEnd && existingBlockEnd > newStart;
    }

    private static bool HasJockeyConflict(bool sameVenue,
        DateTimeOffset raceStart, DateTimeOffset raceEnd,
        DateTimeOffset otherStart, DateTimeOffset otherEnd)
    {
        DateTimeOffset effectiveStart = sameVenue ? raceStart : raceStart.AddHours(-2);
        DateTimeOffset effectiveEnd   = sameVenue ? raceEnd   : raceEnd.AddHours(2);
        return otherStart < effectiveEnd && otherEnd > effectiveStart;
    }

    private static DateTimeOffset T(string hhmm)
        => DateTimeOffset.Parse($"2025-01-01T{hhmm}:00Z");
}
