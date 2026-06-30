using MsfsSave.Core;

namespace MsfsSave.Core.Tests;

public class StateComparisonTests
{
    [Theory]
    [InlineData("Cessna 172", "Cessna 172", true)]
    [InlineData("Cessna 172", "  cessna 172 ", true)]   // trim + case-insensitiv
    [InlineData("Cessna 172", "Airbus A320", false)]
    [InlineData("", "Airbus A320", false)]
    public void TitleMatches_compares_trimmed_case_insensitive(string saved, string loaded, bool expected)
    {
        Assert.Equal(expected, StateComparison.TitleMatches(saved, loaded));
    }
}
