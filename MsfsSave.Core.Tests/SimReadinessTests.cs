using MsfsSave.Core;

namespace MsfsSave.Core.Tests;

public class SimReadinessTests
{
    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    public void CanTransfer_requires_all_three(bool onGround, bool stationary, bool enginesOff, bool expected)
    {
        var readiness = new SimReadiness(onGround, stationary, enginesOff);
        Assert.Equal(expected, readiness.CanTransfer);
    }

    [Fact]
    public void BlockReason_is_null_when_ready()
    {
        Assert.Null(new SimReadiness(true, true, true).BlockReason);
    }

    [Theory]
    [InlineData(false, false, false, "planet är i luften")]
    [InlineData(true, false, false, "planet rör sig")]
    [InlineData(true, true, false, "motorerna är igång")]
    public void BlockReason_reports_first_failing_condition(bool onGround, bool stationary, bool enginesOff, string expected)
    {
        var readiness = new SimReadiness(onGround, stationary, enginesOff);
        Assert.Equal(expected, readiness.BlockReason);
    }
}
