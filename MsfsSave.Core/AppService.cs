namespace MsfsSave.Core;

public record SaveResult(AircraftState State, bool Overwritten);
public record LoadResult(AircraftState State, bool TitleMismatch, string LoadedTitle, bool AtcIdSet);

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

    public SaveResult Save(string registration)
    {
        var captured = _sim.Capture();
        var state = captured with { Registration = registration, SavedAtUtc = DateTime.UtcNow };
        var existed = _store.Exists(registration);
        _store.Save(state);
        return new SaveResult(state, existed);
    }

    public LoadResult Load(string registration)
    {
        var state = _store.Load(registration)
            ?? throw new FileNotFoundException($"Inget sparat tillstånd för '{registration}'.");
        var report = _sim.Restore(state);
        var mismatch = !StateComparison.TitleMatches(state.Title, report.LoadedTitle);
        return new LoadResult(state, mismatch, report.LoadedTitle, report.AtcIdSet);
    }

    public IReadOnlyList<AircraftState> List() => _store.List();

    public bool Delete(string registration) => _store.Delete(registration);
}
