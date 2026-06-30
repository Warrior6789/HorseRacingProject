using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Helpers;
using HorseRacingAPI.Models;

namespace HorseRacingProject.Tests;

public class HorseLogicTests
{
    [Theory]
    [InlineData("Healthy",     HorseStatus.Healthy)]
    [InlineData("healthy",     HorseStatus.Healthy)]
    [InlineData("INJURY",      HorseStatus.Injury)]
    [InlineData("Resting",     HorseStatus.Resting)]
    [InlineData("Retired",     HorseStatus.Retired)]
    [InlineData("  Resting  ", HorseStatus.Resting)]
    public void NormalizeRequired_ValidStatus_ReturnsEnum(string input, HorseStatus expected)
    {
        var result = HorseStatusPolicy.NormalizeRequired(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("0")]
    [InlineData("Invalid")]
    [InlineData("Dead")]
    public void NormalizeRequired_InvalidStatus_ThrowsInvalidOperationException(string input)
    {
        Assert.Throws<InvalidOperationException>(() => HorseStatusPolicy.NormalizeRequired(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeOrDefault_NullOrWhitespace_ReturnsHealthy(string? input)
    {
        Assert.Equal(HorseStatus.Healthy, HorseStatusPolicy.NormalizeOrDefault(input));
    }

    [Fact]
    public void NormalizeOrDefault_ValidStatus_ReturnsEnum()
    {
        Assert.Equal(HorseStatus.Injury, HorseStatusPolicy.NormalizeOrDefault("Injury"));
    }

    [Fact]
    public void CanRegisterForRace_Healthy_ReturnsTrue()
    {
        Assert.True(HorseStatusPolicy.CanRegisterForRace(HorseStatus.Healthy));
    }

    [Theory]
    [InlineData(HorseStatus.Injury)]
    [InlineData(HorseStatus.Resting)]
    [InlineData(HorseStatus.Retired)]
    public void CanRegisterForRace_NonHealthy_ReturnsFalse(HorseStatus status)
    {
        Assert.False(HorseStatusPolicy.CanRegisterForRace(status));
    }

    [Fact]
    public void ActiveStatuses_ContainsHealthyAndResting()
    {
        Assert.Contains(HorseStatus.Healthy, HorseStatusPolicy.ActiveStatuses);
        Assert.Contains(HorseStatus.Resting, HorseStatusPolicy.ActiveStatuses);
    }

    [Theory]
    [InlineData(HorseStatus.Injury)]
    [InlineData(HorseStatus.Retired)]
    public void ActiveStatuses_DoesNotContainInactiveStatuses(HorseStatus status)
    {
        Assert.DoesNotContain(status, HorseStatusPolicy.ActiveStatuses);
    }

    [Fact]
    public void GetDerivedStatus_ConfirmedInLiveRace_ReturnsRacing()
    {
        var horse = BuildHorse(HorseStatus.Healthy,
            (RegistrationStatus.Confirmed, RaceStatus.Live, false));
        Assert.Equal("Racing", GetDerivedStatus(horse));
    }

    [Theory]
    [InlineData(RaceStatus.Scheduled)]
    [InlineData(RaceStatus.BettingOpen)]
    [InlineData(RaceStatus.BettingClosed)]
    public void GetDerivedStatus_ConfirmedInUpcomingRace_ReturnsRegistered(RaceStatus raceStatus)
    {
        var horse = BuildHorse(HorseStatus.Healthy,
            (RegistrationStatus.Confirmed, raceStatus, false));
        Assert.Equal("Registered", GetDerivedStatus(horse));
    }

    [Fact]
    public void GetDerivedStatus_RejectedRegistrationInLiveRace_ReturnsHorseStatus()
    {
        var horse = BuildHorse(HorseStatus.Healthy,
            (RegistrationStatus.Rejected, RaceStatus.Live, false));
        Assert.Equal("Healthy", GetDerivedStatus(horse));
    }

    [Fact]
    public void GetDerivedStatus_ConfirmedButRaceDeleted_ReturnsHorseStatus()
    {
        var horse = BuildHorse(HorseStatus.Resting,
            (RegistrationStatus.Confirmed, RaceStatus.Live, true));
        Assert.Equal("Resting", GetDerivedStatus(horse));
    }

    [Fact]
    public void GetDerivedStatus_NoRegistrations_ReturnsHorseStatus()
    {
        var horse = new Horse { Status = HorseStatus.Injury, HorseName = "Test" };
        Assert.Equal("Injury", GetDerivedStatus(horse));
    }

    [Fact]
    public void GetDerivedStatus_LiveRaceTakesPriorityOverUpcoming()
    {
        var horse = new Horse { Status = HorseStatus.Healthy, HorseName = "Test" };
        horse.Registrations.Add(new Registration
        {
            Status = RegistrationStatus.Confirmed,
            Race = new Race { Status = RaceStatus.Live, IsDeleted = false }
        });
        horse.Registrations.Add(new Registration
        {
            Status = RegistrationStatus.Confirmed,
            Race = new Race { Status = RaceStatus.BettingOpen, IsDeleted = false }
        });
        Assert.Equal("Racing", GetDerivedStatus(horse));
    }

    [Theory]
    [InlineData(0,   10,  1,   10)]
    [InlineData(-5,  10,  1,   10)]
    [InlineData(1,   0,   1,   10)]
    [InlineData(1,  -3,   1,   10)]
    [InlineData(1,  200,  1,  100)]
    [InlineData(1,  101,  1,  100)]
    [InlineData(3,   25,  3,   25)]
    [InlineData(1,    1,  1,    1)]
    [InlineData(1,  100,  1,  100)]
    public void NormalizePaging_ClampsValues(int page, int pageSize, int expectedPage, int expectedSize)
    {
        var query = new HorseQueryRequest { Page = page, PageSize = pageSize };
        NormalizePaging(query);
        Assert.Equal(expectedPage, query.Page);
        Assert.Equal(expectedSize, query.PageSize);
    }

    private static string? GetDerivedStatus(Horse horse)
    {
        bool hasLive = horse.Registrations.Any(r =>
            r.Status == RegistrationStatus.Confirmed &&
            !r.Race.IsDeleted &&
            r.Race.Status == RaceStatus.Live);
        if (hasLive) return "Racing";

        RaceStatus[] upcoming = [RaceStatus.Scheduled, RaceStatus.BettingOpen, RaceStatus.BettingClosed];
        bool hasUpcoming = horse.Registrations.Any(r =>
            r.Status == RegistrationStatus.Confirmed &&
            !r.Race.IsDeleted &&
            upcoming.Contains(r.Race.Status));
        if (hasUpcoming) return "Registered";

        return horse.Status.ToString();
    }

    private static void NormalizePaging(HorseQueryRequest q)
    {
        if (q.Page < 1) q.Page = 1;
        if (q.PageSize < 1) q.PageSize = 10;
        if (q.PageSize > 100) q.PageSize = 100;
    }

    private static Horse BuildHorse(HorseStatus status,
        params (RegistrationStatus reg, RaceStatus race, bool deleted)[] registrations)
    {
        var horse = new Horse { HorseName = "Test", Status = status };
        foreach (var (reg, race, deleted) in registrations)
            horse.Registrations.Add(new Registration
            {
                Status = reg,
                Race = new Race { Status = race, IsDeleted = deleted }
            });
        return horse;
    }
}
