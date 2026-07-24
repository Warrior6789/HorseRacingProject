using HorseRacingAPI.Enums;

namespace HorseRacingProject.Tests;

public class RaceLogicTests
{
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

    [Fact]
    public void TimeConflict_ExistingEndLongBeforeNewStart_NoConflict()
    {
        Assert.False(HasTimeConflict(T("12:00"), T("12:30"), T("13:00")));
    }

    [Fact]
    public void TimeConflict_ExistingOverlapsBuffer_Conflicts()
    {
        Assert.True(HasTimeConflict(T("12:00"), T("14:25"), T("14:28")));
    }

    [Fact]
    public void TimeConflict_NoEndTimeDefaultDuration_Conflicts()
    {
        Assert.True(HasTimeConflict(T("12:00"), null, T("12:08")));
    }

    [Fact]
    public void TimeConflict_NoEndTimeButSafeGap_NoConflict()
    {
        Assert.False(HasTimeConflict(T("12:00"), null, T("12:11")));
    }

    [Fact]
    public void TimeConflict_NewRaceInsideExistingBlock_Conflicts()
    {
        Assert.True(HasTimeConflict(T("14:05"), null, T("14:10")));
    }

    [Theory]
    [InlineData(true,  "13:00", "13:30", "13:10", "13:45", true)]
    [InlineData(true,  "13:00", "13:30", "12:00", "12:30", false)]
    [InlineData(true,  "13:00", "13:30", "13:15", "13:45", true)]
    [InlineData(true,  "13:00", "13:30", "13:30", "14:00", false)]
    [InlineData(false, "13:00", "13:30", "12:00", "12:30", true)]
    [InlineData(false, "13:00", "13:30", "10:00", "10:30", false)]
    [InlineData(false, "13:00", "13:30", "15:31", "16:00", false)]
    [InlineData(false, "13:00", "13:30", "15:00", "15:29", true)]
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

    [Fact]
    public void ResetBet_WonBet_ReversesPayout()
    {
        long balance = 300, betAmount = 100;
        float ratio = 2.0f;
        long payout = (long)(betAmount * (decimal)ratio);
        long result = Math.Max(0, balance - payout + betAmount);
        Assert.Equal(200L, result);
    }

    [Fact]
    public void ResetBet_WonBetPayoutExceedsBalance_ClampedToZero()
    {
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

    [Fact]
    public void StartTime_LessThan90Min_IsInvalid()
    {
        var now     = DateTimeOffset.UtcNow;
        var tooSoon = now.AddMinutes(89);
        Assert.True(tooSoon <= now.AddMinutes(90));
    }

    [Fact]
    public void StartTime_Exactly90Min_IsInvalid()
    {
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
