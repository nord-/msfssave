namespace MsfsSave.Core;

/// <summary>Statusradens texter. Ren formatering så att ordalydelsen kan låsas med tester.</summary>
public static class StatusText
{
    public const string NoRegistration = "✗ Kan inte spara: ingen registrering angiven.";
    public const string ReadingFromSim = "… läser från simulatorn";
    public const string WritingToSim = "… skriver till simulatorn";

    public static string Loaded(LoadResult result)
    {
        var registration = result.SavedState.Registration;
        var warnings = new List<string>();
        if (result.TitleMismatch) warnings.Add($"\"{result.LoadedTitle}\" är laddat");
        if (!result.AtcIdSet) warnings.Add("ATC ID ej satt");

        return warnings.Count == 0
            ? $"✓ Laddade {registration}."
            : $"⚠ Laddade {registration} — {string.Join(", ", warnings)}.";
    }

    public static string Saved(SaveResult result) => result.Overwritten
        ? $"✓ Skrev över {result.State.Registration}."
        : $"✓ Sparade {result.State.Registration}.";

    public static string Deleted(string registration, bool existed) => existed
        ? $"✓ Tog bort {registration}."
        : $"✗ {registration} fanns inte längre — listan uppdaterad.";

    public static string Blocked(string verb, string reason) => $"✗ Kan inte {verb}: {reason}.";

    public static string Error(string message) => $"✗ Fel: {message}";
}
