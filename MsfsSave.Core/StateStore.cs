using System.Text.Json;

namespace MsfsSave.Core;

public class StateStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _dir;

    public StateStore(string directory)
    {
        _dir = directory;
        Directory.CreateDirectory(_dir);
    }

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "msfssave");

    public void Save(AircraftState state)
        => File.WriteAllText(PathFor(state.SlotName), JsonSerializer.Serialize(state, Options));

    public AircraftState? Load(string slotName)
    {
        var path = PathFor(slotName);
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<AircraftState>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"Sparfilen för '{slotName}' är tom eller ogiltig.");
    }

    public bool Exists(string slotName) => File.Exists(PathFor(slotName));

    public IReadOnlyList<AircraftState> List()
    {
        var list = new List<AircraftState>();
        foreach (var file in Directory.EnumerateFiles(_dir, "*.json"))
        {
            try
            {
                var state = JsonSerializer.Deserialize<AircraftState>(File.ReadAllText(file));
                if (state != null) list.Add(state);
            }
            catch (JsonException) { /* hoppa över korrupta filer */ }
        }
        return list.OrderBy(s => s.SlotName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public bool Delete(string slotName)
    {
        var path = PathFor(slotName);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    private string PathFor(string slotName) => Path.Combine(_dir, Sanitize(slotName) + ".json");

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
