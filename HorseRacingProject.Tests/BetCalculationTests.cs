namespace HorseRacingProject.Tests;

public class BetCalculationTests
{
    [Theory]
    [InlineData(1000, 0.20, 100, 200,  400)]
    [InlineData(500,  0.20, 50,  100,  200)]
    [InlineData(1000, 0.00, 100, 1000, 100)]
    [InlineData(1000, 0.10, 100, 500,  180)]
    [InlineData(300,  0.25, 100, 300,  75)]
    public void EstimatedPayout_CalculatesCorrectly(
        decimal poolTotal, decimal takeout, decimal betAmount, decimal horseTotal, decimal expected)
    {
        decimal netPool   = poolTotal * (1 - takeout);
        decimal estimated = Math.Round(betAmount * (netPool / horseTotal), 0);
        Assert.Equal(expected, estimated);
    }

    [Fact]
    public void EstimatedPayout_FractionalRoundsToNearestWhole()
    {
        decimal netPool   = 1000m * (1m - 0.30m);
        decimal estimated = Math.Round(100m * (netPool / 300m), 0);
        Assert.Equal(233m, estimated);
    }

    [Fact]
    public void EstimatedPayout_SoloBettor_GetsEntireNetPool()
    {
        decimal poolTotal = 500m;
        decimal takeout   = 0.20m;
        decimal betAmount = 250m;
        decimal netPool   = poolTotal * (1 - takeout);
        decimal estimated = Math.Round(betAmount * (netPool / betAmount), 0);
        Assert.Equal(netPool, estimated);
    }

    [Fact]
    public void NetPool_TakeoutReducesTotalCorrectly()
    {
        decimal total   = 2000m;
        decimal takeout = 0.25m;
        decimal netPool = total * (1 - takeout);
        Assert.Equal(1500m, netPool);
    }

    [Fact]
    public void NetPool_ZeroTakeout_EqualsTotal()
    {
        decimal total   = 1000m;
        decimal netPool = total * (1 - 0m);
        Assert.Equal(total, netPool);
    }
}
