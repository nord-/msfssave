using MsfsSave.Core;

namespace MsfsSave.Core.Tests;

public class StateStoreTests
{
    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), "msfssave_test_" + Guid.NewGuid().ToString("N"));

    private static AircraftState Sample(string name) => new()
    {
        SlotName = name,
        AtcId = "SE-ABC",
        Title = "Cessna 172 Skyhawk",
        SavedAtUtc = new DateTime(2026, 6, 30, 18, 30, 0, DateTimeKind.Utc),
        Position = new PositionState
        {
            Latitude = 59.65, Longitude = 17.92, AltitudeFeet = 137.0,
            PitchDeg = 0.4, BankDeg = -0.1, HeadingTrueDeg = 210.0, OnGround = true
        },
        FuelGallons = new Dictionary<string, double> { ["LeftMain"] = 26.5, ["RightMain"] = 26.5 },
        PayloadLbs = new List<PayloadStation> { new() { Index = 1, Name = "Pilot", Weight = 170 } }
    };

    [Fact]
    public void Save_then_Load_roundtrips_all_fields()
    {
        var dir = TempDir();
        try
        {
            var store = new StateStore(dir);
            var original = Sample("SE-ABC");

            store.Save(original);
            var loaded = store.Load("SE-ABC");

            Assert.NotNull(loaded);
            Assert.Equal(original.SlotName, loaded!.SlotName);
            Assert.Equal(original.Title, loaded.Title);
            Assert.Equal(original.SavedAtUtc, loaded.SavedAtUtc);
            Assert.Equal(original.Position.Latitude, loaded.Position.Latitude);
            Assert.Equal(original.Position.HeadingTrueDeg, loaded.Position.HeadingTrueDeg);
            Assert.True(loaded.Position.OnGround);
            Assert.Equal(26.5, loaded.FuelGallons["LeftMain"]);
            Assert.Single(loaded.PayloadLbs);
            Assert.Equal("Pilot", loaded.PayloadLbs[0].Name);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_returns_null_when_missing()
    {
        var dir = TempDir();
        try
        {
            var store = new StateStore(dir);
            Assert.Null(store.Load("OKÄND"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Exists_is_false_before_save_and_true_after()
    {
        var dir = TempDir();
        try
        {
            var store = new StateStore(dir);
            Assert.False(store.Exists("SE-ABC"));
            store.Save(Sample("SE-ABC"));
            Assert.True(store.Exists("SE-ABC"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void List_returns_all_saved_sorted_by_registration()
    {
        var dir = TempDir();
        try
        {
            var store = new StateStore(dir);
            store.Save(Sample("SE-XYZ"));
            store.Save(Sample("SE-ABC"));

            var all = store.List();

            Assert.Equal(2, all.Count);
            Assert.Equal("SE-ABC", all[0].SlotName);
            Assert.Equal("SE-XYZ", all[1].SlotName);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Delete_removes_file_and_reports_result()
    {
        var dir = TempDir();
        try
        {
            var store = new StateStore(dir);
            store.Save(Sample("SE-ABC"));

            Assert.True(store.Delete("SE-ABC"));
            Assert.Null(store.Load("SE-ABC"));
            Assert.False(store.Delete("SE-ABC"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    /// <summary>Sparfiler från före uppdelningen SlotName/AtcId har ett enda "Registration"-fält.</summary>
    private const string LegacyJson = """
        {
          "Registration": "Bornholm",
          "Title": "Black Square A36TC Bonanza Professional N3475M",
          "SavedAtUtc": "2026-07-25T12:57:16.6478047Z",
          "Position": { "Latitude": 55.07, "Longitude": 14.74, "OnGround": true },
          "FuelGallons": { "LeftMain": 20.0 },
          "PayloadLbs": []
        }
        """;

    [Fact]
    public void List_migrerar_gammalt_Registration_till_SlotName_och_AtcId()
    {
        var dir = TempDir();
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Bornholm.json"), LegacyJson);
            var store = new StateStore(dir);

            var only = Assert.Single(store.List());

            Assert.Equal("Bornholm", only.SlotName);
            Assert.Equal("Bornholm", only.AtcId);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_migrerar_gammalt_Registration_till_SlotName_och_AtcId()
    {
        var dir = TempDir();
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Bornholm.json"), LegacyJson);
            var store = new StateStore(dir);

            var loaded = store.Load("Bornholm");

            Assert.NotNull(loaded);
            Assert.Equal("Bornholm", loaded!.SlotName);
            Assert.Equal("Bornholm", loaded.AtcId);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Listad_gammal_post_kan_tas_bort_via_sitt_SlotName()
    {
        var dir = TempDir();
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Bornholm.json"), LegacyJson);
            var store = new StateStore(dir);

            var only = store.List()[0];

            Assert.True(store.Delete(only.SlotName));
            Assert.Empty(store.List());
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void List_faller_tillbaka_pa_filnamnet_nar_bade_SlotName_och_Registration_saknas()
    {
        var dir = TempDir();
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "outback.json"), """{ "Title": "Cessna 170B" }""");
            var store = new StateStore(dir);

            var only = Assert.Single(store.List());

            Assert.Equal("outback", only.SlotName);
            Assert.True(store.Delete(only.SlotName));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
