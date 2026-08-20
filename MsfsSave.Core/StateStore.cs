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
        return Read(path)
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
                var state = Read(file);
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

    private static AircraftState? Read(string path)
    {
        var json = File.ReadAllText(path);
        var state = JsonSerializer.Deserialize<AircraftState>(json);
        return state == null ? null : Migrate(state, json, Path.GetFileNameWithoutExtension(path));
    }

    /// <summary>
    /// Fyller i namnfälten för sparfiler skrivna innan <see cref="AircraftState.SlotName"/> och
    /// <see cref="AircraftState.AtcId"/> delades upp — de har ett enda "Registration"-fält. Utan
    /// detta får posten tomt SlotName och blir omöjlig att ladda, skriva över eller ta bort,
    /// eftersom filnyckeln utgår från SlotName.
    /// </summary>
    private static AircraftState Migrate(AircraftState state, string json, string fileKey)
    {
        if (!string.IsNullOrWhiteSpace(state.SlotName) && !string.IsNullOrWhiteSpace(state.AtcId))
            return state;

        var legacy = LegacyRegistration(json);
        return state with
        {
            SlotName = FirstNonEmpty(state.SlotName, legacy, fileKey),
            AtcId = FirstNonEmpty(state.AtcId, legacy)
        };
    }

    private static string LegacyRegistration(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("Registration", out var value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? ""
                : "";
        }
        catch (JsonException) { return ""; }
    }

    private static string FirstNonEmpty(params string[] candidates)
        => candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))?.Trim() ?? "";

    private string PathFor(string slotName) => Path.Combine(_dir, Sanitize(slotName) + ".json");

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
