namespace MsfsSave.Core;

/// <summary>Listan över sparade tillstånd plus markörens position. Ren logik, ingen I/O.</summary>
public class SavedList
{
    private readonly List<AircraftState> _items = new();

    public IReadOnlyList<AircraftState> Items => _items;
    public int Count => _items.Count;
    public int SelectedIndex { get; private set; } = -1;
    public AircraftState? Selected => SelectedIndex >= 0 ? _items[SelectedIndex] : null;

    /// <summary>Byter innehåll och klampar markören till giltigt intervall.</summary>
    public void Replace(IReadOnlyList<AircraftState> items)
    {
        _items.Clear();
        _items.AddRange(items);
        SelectedIndex = _items.Count == 0
            ? -1
            : Math.Clamp(SelectedIndex < 0 ? 0 : SelectedIndex, 0, _items.Count - 1);
    }

    public void MoveUp()
    {
        if (SelectedIndex > 0) SelectedIndex--;
    }

    public void MoveDown()
    {
        if (SelectedIndex >= 0 && SelectedIndex < _items.Count - 1) SelectedIndex++;
    }

    /// <summary>Ställer markören på posten. Lämnar markören orörd vid okänd registrering.</summary>
    public void SelectByRegistration(string registration)
    {
        var index = _items.FindIndex(s =>
            string.Equals(s.Registration, registration, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) SelectedIndex = index;
    }
}
