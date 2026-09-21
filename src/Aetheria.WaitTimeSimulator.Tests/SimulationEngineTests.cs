using Aetheria.WaitTimeSimulator;

namespace Aetheria.WaitTimeSimulator.Tests;

public class SimulationEngineTests
{
    [Theory]
    [InlineData(9, 0, 15)]
    [InlineData(13, 30, 55)]
    [InlineData(14, 45, 60)]
    [InlineData(20, 30, 15)]
    public void GetBaseWaitTime_FollowsTimeOfDayCurve(int hour, int minute, int expected)
    {
        var time = new TimeOnly(hour, minute);

        var baseWaitTime = SimulationEngine.GetBaseWaitTime(time);

        Assert.Equal(expected, baseWaitTime);
    }

    [Fact]
    public void CalculateTargetWaitTime_Rain_BoostsIndoorAndSuppressesOutdoor()
    {
        var snapshot = new SimulationSnapshot(new TimeOnly(14, 0), ParkScenario.Rain, IsRunning: true);

        var indoor = new SimulationEngine.AttractionProfile(Popularity: 1.0, IsIndoor: true);
        var outdoor = new SimulationEngine.AttractionProfile(Popularity: 1.0, IsIndoor: false);

        var indoorTarget = SimulationEngine.CalculateTargetWaitTime(indoor, snapshot);
        var outdoorTarget = SimulationEngine.CalculateTargetWaitTime(outdoor, snapshot);

        Assert.True(indoorTarget > outdoorTarget);
    }

    [Theory]
    [InlineData(ParkScenario.Quiet)]
    [InlineData(ParkScenario.Normal)]
    [InlineData(ParkScenario.Peak)]
    [InlineData(ParkScenario.Rain)]
    [InlineData(ParkScenario.Closing)]
    public void CalculateTargetWaitTime_StaysWithinClampedRangeAndIsAMultipleOfFive(ParkScenario scenario)
    {
        var snapshot = new SimulationSnapshot(new TimeOnly(15, 0), scenario, IsRunning: true);

        var profile = new SimulationEngine.AttractionProfile(Popularity: 1.2, IsIndoor: true);

        var target = SimulationEngine.CalculateTargetWaitTime(profile, snapshot);

        Assert.InRange(target, 5, 120);

        Assert.Equal(0, target % 5);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(300)]
    [InlineData(-30)]
    public void CalculateNextWaitTime_NeverLeavesClampedRange(int target)
    {
        var next = SimulationEngine.CalculateNextWaitTime(current: 60, target: Math.Clamp(target, 5, 120));

        Assert.InRange(next, 5, 120);

        Assert.Equal(0, next % 5);
    }

    [Fact]
    public void RoundToFive_RoundsToNearestMultipleOfFive()
    {
        Assert.Equal(30, SimulationEngine.RoundToFive(32));
        Assert.Equal(35, SimulationEngine.RoundToFive(33));
        Assert.Equal(0, SimulationEngine.RoundToFive(2));
    }
}
