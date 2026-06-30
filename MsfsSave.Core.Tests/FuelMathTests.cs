using MsfsSave.Core;

namespace MsfsSave.Core.Tests;

public class FuelMathTests
{
    [Theory]
    [InlineData(20.0, 30.0, 20.0)]   // under kapacitet -> oförändrad
    [InlineData(40.0, 30.0, 30.0)]   // över kapacitet  -> klampas
    [InlineData(-5.0, 30.0, 0.0)]    // negativ         -> 0
    [InlineData(30.0, 30.0, 30.0)]   // exakt kapacitet -> oförändrad
    public void ClampToCapacity_clamps_into_valid_range(double requested, double capacity, double expected)
    {
        Assert.Equal(expected, FuelMath.ClampToCapacity(requested, capacity));
    }
}
