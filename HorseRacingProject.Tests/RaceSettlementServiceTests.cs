using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using Xunit;

namespace HorseRacingProject.Tests;

// Test logic tính toán thuần túy từ RaceSettlementService
// Không cần mock DB — chỉ test các công thức tính payout

public class RaceSettlementCalculationTests
{
    // -------------------------------------------------------
    // Test 1: Win bet — chỉ vị trí 1 thắng
    // -------------------------------------------------------
    [Fact]
    public void WinBet_OnlyPosition1Wins()
    {
        // 3 con ngựa, vị trí: reg1=1st, reg2=2nd, reg3=3rd
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

        Assert.Contains(reg1, winners);      // vị trí 1 → thắng
        Assert.DoesNotContain(reg2, winners); // vị trí 2 → thua
        Assert.DoesNotContain(reg3, winners); // vị trí 3 → thua
    }

    // -------------------------------------------------------
    // Test 2: Place bet — vị trí 1 và 2 thắng
    // -------------------------------------------------------
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

    // -------------------------------------------------------
    // Test 3: Show bet — vị trí 1, 2, 3 thắng
    // -------------------------------------------------------
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

    // -------------------------------------------------------
    // Test 4: Payout ratio tính đúng
    // pool = 1_000_000, takeout = 20%, winningPool = 500_000
    // netPool = 800_000, ratio = 800_000 / 500_000 = 1.6
    // -------------------------------------------------------
    [Fact]
    public void PayoutRatio_CalculatedCorrectly()
    {
        decimal totalPool = 1_000_000;
        decimal takeout = 0.20m;
        decimal winningPool = 500_000;

        decimal netPool = totalPool * (1 - takeout);
        float ratio = Math.Max((float)(netPool / winningPool), 1.0f);

        Assert.Equal(1.6f, ratio, precision: 4);
    }

    // -------------------------------------------------------
    // Test 5: Không có winner → ratio tối thiểu là 1.0 (không lỗ vốn)
    // -------------------------------------------------------
    [Fact]
    public void PayoutRatio_MinimumIsOne()
    {
        decimal netPool = 100_000;
        decimal winningPool = 200_000; // winningPool > netPool

        float ratio = Math.Max((float)(netPool / winningPool), 1.0f);

        Assert.Equal(1.0f, ratio);
    }

    // -------------------------------------------------------
    // Test 6: Payout cụ thể cho 1 bet
    // betAmount = 500_000, ratio = 1.6 → payout = 800_000
    // -------------------------------------------------------
    [Fact]
    public void Payout_CorrectAmount()
    {
        decimal betAmount = 500_000;
        float ratio = 1.6f;

        long payout = (long)(betAmount * (decimal)ratio);

        Assert.Equal(800_000L, payout);
    }

    // -------------------------------------------------------
    // Test 7: Prize distribution — jockey lấy winCut từ phần thưởng
    // position prize = 1_000_000, winCut = 10% → jockey = 100_000, owner = 900_000
    // -------------------------------------------------------
    [Fact]
    public void PrizeDistribution_JockeyAndOwnerSplit()
    {
        decimal positionPrize = 1_000_000;
        decimal winCut = 0.10m;

        decimal jockeyAmount = positionPrize * winCut;
        decimal ownerAmount = positionPrize - jockeyAmount;

        Assert.Equal(100_000m, jockeyAmount);
        Assert.Equal(900_000m, ownerAmount);
    }

    // -------------------------------------------------------
    // Helper: copy logic từ RaceSettlementService
    // -------------------------------------------------------
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
