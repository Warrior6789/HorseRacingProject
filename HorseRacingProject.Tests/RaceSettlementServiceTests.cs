using HorseRacingAPI.Enums;

namespace HorseRacingProject.Tests;

public class RaceSettlementCalculationTests
{
    [Fact]
    public void WinBet_OnlyPosition1Wins()
    {
        var reg1 = Guid.NewGuid();
        var reg2 = Guid.NewGuid();
        var reg3 = Guid.NewGuid();

        var positions = new Dictionary<Guid, int>
        {
            { reg1, 1 },
            { reg2, 2 },
            { reg3, 3 }
        };

        var winners = GetWinners(BetType.Win, positions);

        Assert.Contains(reg1, winners);
        Assert.DoesNotContain(reg2, winners);
        Assert.DoesNotContain(reg3, winners);
    }

    [Fact]
    public void PlaceBet_Position1And2Win()
    {
        var reg1 = Guid.NewGuid();
        var reg2 = Guid.NewGuid();
        var reg3 = Guid.NewGuid();

        var positions = new Dictionary<Guid, int>
        {
            { reg1, 1 },
            { reg2, 2 },
            { reg3, 3 }
        };

        var winners = GetWinners(BetType.Place, positions);

        Assert.Contains(reg1, winners);
        Assert.Contains(reg2, winners);
        Assert.DoesNotContain(reg3, winners);
    }

    [Fact]
    public void ShowBet_Top3Win()
    {
        var reg1 = Guid.NewGuid();
        var reg2 = Guid.NewGuid();
        var reg3 = Guid.NewGuid();
        var reg4 = Guid.NewGuid();

        var positions = new Dictionary<Guid, int>
        {
            { reg1, 1 }, { reg2, 2 }, { reg3, 3 }, { reg4, 4 }
        };

        var winners = GetWinners(BetType.Show, positions);

        Assert.Contains(reg1, winners);
        Assert.Contains(reg2, winners);
        Assert.Contains(reg3, winners);
        Assert.DoesNotContain(reg4, winners);
    }

    [Fact]
    public void PayoutRatio_CalculatedCorrectly()
    {
        decimal totalPool   = 1_000_000;
        decimal takeout     = 0.20m;
        decimal winningPool = 500_000;

        decimal netPool = totalPool * (1 - takeout);
        float ratio = Math.Max((float)(netPool / winningPool), 1.0f);

        Assert.Equal(1.6f, ratio, precision: 4);
    }

    [Fact]
    public void PayoutRatio_MinimumIsOne()
    {
        decimal netPool     = 100_000;
        decimal winningPool = 200_000;

        float ratio = Math.Max((float)(netPool / winningPool), 1.0f);

        Assert.Equal(1.0f, ratio);
    }

    [Fact]
    public void Payout_CorrectAmount()
    {
        decimal betAmount = 500_000;
        float ratio = 1.6f;

        long payout = (long)(betAmount * (decimal)ratio);

        Assert.Equal(800_000L, payout);
    }

    [Fact]
    public void PrizeDistribution_JockeyAndOwnerSplit()
    {
        decimal positionPrize = 1_000_000;
        decimal winCut        = 0.10m;

        decimal jockeyAmount = positionPrize * winCut;
        decimal ownerAmount  = positionPrize - jockeyAmount;

        Assert.Equal(100_000m, jockeyAmount);
        Assert.Equal(900_000m, ownerAmount);
    }

    private static HashSet<Guid> GetWinners(BetType betType, Dictionary<Guid, int> positions)
    {
        return positions
            .Where(kvp => betType switch
            {
                BetType.Win   => kvp.Value == 1,
                BetType.Place => kvp.Value <= 2,
                BetType.Show  => kvp.Value <= 3,
                _             => false
            })
            .Select(kvp => kvp.Key)
            .ToHashSet();
    }
}
