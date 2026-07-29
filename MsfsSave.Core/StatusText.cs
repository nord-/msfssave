namespace MsfsSave.Core;

/// <summary>Statusradens texter. Ren formatering så att ordalydelsen kan låsas med tester.</summary>
public static class StatusText
{
    public const string NoSlotName = "✗ Kan inte spara: inget namn angivet.";
    public const string ReadingFromSim = "… läser från simulatorn";
    public const string WritingToSim = "… skriver till simulatorn";

    public static string Loaded(LoadResult result)
    {
        var slotName = result.SavedState.SlotName;
        var warnings = new List<string>();
        if (result.TitleMismatch) warnings.Add($"\"{result.LoadedTitle}\" är laddat");
        if (!result.AtcIdSet) warnings.Add("ATC ID ej satt");

        return warnings.Count == 0
            ? $"✓ Laddade {slotName}."
            : $"⚠ Laddade {slotName} — {string.Join(", ", warnings)}.";
    }

    public static string Saved(SaveResult result) => result.Overwritten
        ? $"✓ Skrev över {result.State.SlotName}."
        : $"✓ Sparade {result.State.SlotName}.";

    public static string Deleted(string slotName, bool existed) => existed
        ? $"✓ Tog bort {slotName}."
        : $"✗ {slotName} fanns inte längre — listan uppdaterad.";

    public static string Blocked(string verb, string reason) => $"✗ Kan inte {verb}: {reason}.";

    public static string Error(string message) => $"✗ Fel: {message}";
}
