using MsfsSave.Core;

namespace MsfsSave.Core.Tests;

public class StateStoreTests
{
    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), "msfssave_test_" + Guid.NewGuid().ToString("N"));

    private static AircraftState Sample(string reg) => new()
    {
        Registration = reg,
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
            Assert.Equal(original.Registration, loaded!.Registration);
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
}
