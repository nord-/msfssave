using MsfsSave.Core;

namespace MsfsSave.Core.Tests;

public class FakeSimConnector : ISimConnector
{
    public bool IsConnected { get; set; } = true;
    public string CurrentTitle { get; set; } = "Cessna 172 Skyhawk";
    public string CurrentAtcId { get; set; } = "SE-ABC";

    public AircraftState NextCapture { get; set; } = new();
    public AircraftState? Restored { get; private set; }
    public RestoreReport NextReport { get; set; } = new() { AtcIdSet = true, LoadedTitle = "Cessna 172 Skyhawk" };

    /// <summary>Standard är ett plan som står stilla på marken med motorerna av.</summary>
    public SimReadiness NextReadiness { get; set; } = new(true, true, true);

    public int CaptureCalls { get; private set; }
    public int RestoreCalls { get; private set; }

    public void Connect() { IsConnected = true; }
    public SimReadiness ReadReadiness() => NextReadiness;
    public AircraftState Capture() { CaptureCalls++; return NextCapture; }
    public RestoreReport Restore(AircraftState state) { RestoreCalls++; Restored = state; return NextReport; }
    public void Dispose() { }
}
