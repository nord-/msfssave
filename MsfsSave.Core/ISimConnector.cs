namespace MsfsSave.Core;

/// <summary>Abstraktion mot simulatorn. Isolerar SimConnect så att AppService kan testas.</summary>
public interface ISimConnector : IDisposable
{
    bool IsConnected { get; }

    /// <summary>Senast avlästa flygplanstitel (för menyns header). Tom om okänd.</summary>
    string CurrentTitle { get; }

    /// <summary>Senast avlästa ATC ID/registrering (för header + namnförslag). Tom om okänd.</summary>
    string CurrentAtcId { get; }

    /// <summary>Öppnar anslutningen. Kastar vid fel (t.ex. simulatorn inte igång).</summary>
    void Connect();

    /// <summary>Läser om planet står stilla på marken med motorerna av.</summary>
    SimReadiness ReadReadiness();

    /// <summary>Läser nuvarande fullständiga tillstånd från simulatorn.</summary>
    AircraftState Capture();

    /// <summary>Skriver tillståndet till simulatorn och rapporterar utfall.</summary>
    RestoreReport Restore(AircraftState state);
}

public record RestoreReport
{
    /// <summary>True om ATC ID kunde skrivas till simulatorn.</summary>
    public bool AtcIdSet { get; init; }

    /// <summary>Titeln på det flygplan som faktiskt är laddat i simulatorn vid laddning.</summary>
    public string LoadedTitle { get; init; } = "";
}
