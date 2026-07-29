namespace MsfsSave.Core;

public record SaveResult(AircraftState State, bool Overwritten);
public record LoadResult(AircraftState SavedState, bool TitleMismatch, string LoadedTitle, bool AtcIdSet);

/// <summary>Orkestrerar spara/ladda mellan simulatorn och lagret.</summary>
public class AppService
{
    private readonly ISimConnector _sim;
    private readonly StateStore _store;

    public AppService(ISimConnector sim, StateStore store)
    {
        _sim = sim;
        _store = store;
    }

    public SaveResult Save(string slotName)
    {
        RequireReady();
        var captured = _sim.Capture();
        var state = captured with { SlotName = slotName, SavedAtUtc = DateTime.UtcNow };
        var existed = _store.Exists(slotName);
        _store.Save(state);
        return new SaveResult(state, existed);
    }

    public LoadResult Load(string slotName)
    {
        RequireReady();
        var state = _store.Load(slotName)
            ?? throw new FileNotFoundException($"Inget sparat tillstånd för '{slotName}'.");
        var report = _sim.Restore(state);
        var mismatch = !StateComparison.TitleMatches(state.Title, report.LoadedTitle);
        return new LoadResult(state, mismatch, report.LoadedTitle, report.AtcIdSet);
    }

    public IReadOnlyList<AircraftState> List() => _store.List();

    public bool Delete(string slotName) => _store.Delete(slotName);

    /// <summary>Vägrar all dataöverföring om planet inte står stilla på marken med motorerna av.</summary>
    private void RequireReady()
    {
        var readiness = _sim.ReadReadiness();
        if (!readiness.CanTransfer) throw new SimNotReadyException(readiness.BlockReason!);
    }
}
