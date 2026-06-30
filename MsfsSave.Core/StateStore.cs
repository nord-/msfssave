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
        => File.WriteAllText(PathFor(state.Registration), JsonSerializer.Serialize(state, Options));

    public AircraftState? Load(string registration)
    {
        var path = PathFor(registration);
        return File.Exists(path) ? JsonSerializer.Deserialize<AircraftState>(File.ReadAllText(path)) : null;
    }

    public bool Exists(string registration) => File.Exists(PathFor(registration));

    private string PathFor(string registration) => Path.Combine(_dir, Sanitize(registration) + ".json");

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}