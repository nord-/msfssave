namespace MsfsSave.Core;

public static class StateComparison
{
    public static bool TitleMatches(string savedTitle, string loadedTitle)
        => string.Equals(savedTitle.Trim(), loadedTitle.Trim(), StringComparison.OrdinalIgnoreCase);
}
