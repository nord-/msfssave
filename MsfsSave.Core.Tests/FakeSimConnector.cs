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

    public void Connect() { IsConnected = true; }
    public AircraftState Capture() => NextCapture;
    public RestoreReport Restore(AircraftState state) { Restored = state; return NextReport; }
    public void Dispose() { }
}
