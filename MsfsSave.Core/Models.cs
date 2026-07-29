namespace MsfsSave.Core;

public record AircraftState
{
    /// <summary>Fritt namn på sparplatsen — filnamn i StateStore, valt av användaren. Inte flygplanets registrering.</summary>
    public string SlotName { get; init; } = "";
    /// <summary>Flygplanets faktiska ATC ID, avläst från simulatorn vid sparning. Skrivs tillbaka vid laddning.</summary>
    public string AtcId { get; init; } = "";
    public string Title { get; init; } = "";
    public DateTime SavedAtUtc { get; init; }
    public PositionState Position { get; init; } = new();
    public Dictionary<string, double> FuelGallons { get; init; } = new();
    public List<PayloadStation> PayloadLbs { get; init; } = new();
}

public record PositionState
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double AltitudeFeet { get; init; }
    public double PitchDeg { get; init; }
    public double BankDeg { get; init; }
    public double HeadingTrueDeg { get; init; }
    public bool OnGround { get; init; }
}

public record PayloadStation
{
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public double Weight { get; init; }
}
