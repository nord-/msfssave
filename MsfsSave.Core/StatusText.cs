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

    /// <summary>
    /// Varför spara och ladda är låst, eller null när ingenting blockerar. En saknad anslutning
    /// nämns före allt annat — den förklarar i sig varför tillståndet är okänt. Frasen bär ingen
    /// egen slutpunkt; anroparen sätter den i sin mening.
    /// </summary>
    public static string? LockReason(bool connected, SimReadiness? readiness, string error)
    {
        if (!connected) return "MSFS är inte igång — försöker ansluta";
        if (readiness is null)
            return error.Length > 0
                ? $"simulatorns tillstånd är okänt ({error})"
                : "simulatorns tillstånd är okänt";
        return readiness.BlockReason;
    }

    public static string Blocked(string verb, string reason) => $"✗ Kan inte {verb}: {reason}.";

    public static string Error(string message) => $"✗ Fel: {message}";
}
