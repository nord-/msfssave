using MsfsSave.Core;

namespace MsfsSave.Core.Tests;

public class AppServiceTests
{
    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), "msfssave_app_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Save_captures_from_sim_and_stamps_registration_and_time()
    {
        var dir = TempDir();
        try
        {
            var sim = new FakeSimConnector
            {
                NextCapture = new AircraftState
                {
                    Title = "Cessna 172 Skyhawk",
                    Position = new PositionState { Latitude = 1, Longitude = 2 }
                }
            };
            var store = new StateStore(dir);
            var app = new AppService(sim, store);

            var result = app.Save("SE-NEW");

            Assert.False(result.Overwritten);
            Assert.Equal("SE-NEW", result.State.Registration);
            Assert.NotEqual(default, result.State.SavedAtUtc);
            Assert.Equal("SE-NEW", store.Load("SE-NEW")!.Registration);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Save_reports_overwrite_when_file_exists()
    {
        var dir = TempDir();
        try
        {
            var sim = new FakeSimConnector();
            var app = new AppService(sim, new StateStore(dir));
            app.Save("SE-ABC");

            var result = app.Save("SE-ABC");

            Assert.True(result.Overwritten);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_restores_to_sim_and_flags_no_mismatch_when_titles_equal()
    {
        var dir = TempDir();
        try
        {
            var store = new StateStore(dir);
            store.Save(new AircraftState { Registration = "SE-ABC", Title = "Cessna 172 Skyhawk" });
            var sim = new FakeSimConnector
            {
                NextReport = new RestoreReport { AtcIdSet = true, LoadedTitle = "Cessna 172 Skyhawk" }
            };
            var app = new AppService(sim, store);

            var result = app.Load("SE-ABC");

            Assert.NotNull(sim.Restored);
            Assert.Equal("SE-ABC", sim.Restored!.Registration);
            Assert.False(result.TitleMismatch);
            Assert.True(result.AtcIdSet);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_flags_mismatch_when_loaded_title_differs()
    {
        var dir = TempDir();
        try
        {
            var store = new StateStore(dir);
            store.Save(new AircraftState { Registration = "SE-ABC", Title = "Cessna 172 Skyhawk" });
            var sim = new FakeSimConnector
            {
                NextReport = new RestoreReport { AtcIdSet = false, LoadedTitle = "Airbus A320neo" }
            };
            var app = new AppService(sim, store);

            var result = app.Load("SE-ABC");

            Assert.True(result.TitleMismatch);
            Assert.Equal("Airbus A320neo", result.LoadedTitle);
            Assert.False(result.AtcIdSet);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_throws_when_state_missing()
    {
        var dir = TempDir();
        try
        {
            var app = new AppService(new FakeSimConnector(), new StateStore(dir));
            Assert.Throws<FileNotFoundException>((Action)(() => app.Load("SAKNAS")));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
