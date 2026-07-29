using MsfsSave.Core;

namespace MsfsSave.Core.Tests;

public class AppServiceTests
{
    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), "msfssave_app_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Save_captures_from_sim_and_stamps_slot_name_and_time()
    {
        var dir = TempDir();
        try
        {
            var sim = new FakeSimConnector
            {
                NextCapture = new AircraftState
                {
                    AtcId = "SE-REAL",
                    Title = "Cessna 172 Skyhawk",
                    Position = new PositionState { Latitude = 1, Longitude = 2 }
                }
            };
            var store = new StateStore(dir);
            var app = new AppService(sim, store);

            var result = app.Save("Höganäs");

            Assert.False(result.Overwritten);
            Assert.Equal("Höganäs", result.State.SlotName);
            Assert.Equal("SE-REAL", result.State.AtcId);
            Assert.NotEqual(default, result.State.SavedAtUtc);
            Assert.Equal("Höganäs", store.Load("Höganäs")!.SlotName);
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
            store.Save(new AircraftState { SlotName = "SE-ABC", AtcId = "SE-ABC", Title = "Cessna 172 Skyhawk" });
            var sim = new FakeSimConnector
            {
                NextReport = new RestoreReport { AtcIdSet = true, LoadedTitle = "Cessna 172 Skyhawk" }
            };
            var app = new AppService(sim, store);

            var result = app.Load("SE-ABC");

            Assert.NotNull(sim.Restored);
            Assert.Equal("SE-ABC", sim.Restored!.AtcId);
            Assert.False(result.TitleMismatch);
            Assert.True(result.AtcIdSet);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_restores_real_atc_id_not_the_slot_name()
    {
        var dir = TempDir();
        try
        {
            var store = new StateStore(dir);
            store.Save(new AircraftState { SlotName = "Höganäs", AtcId = "SE-MAF", Title = "Cessna 172 Skyhawk" });
            var sim = new FakeSimConnector
            {
                NextReport = new RestoreReport { AtcIdSet = true, LoadedTitle = "Cessna 172 Skyhawk" }
            };
            var app = new AppService(sim, store);

            app.Load("Höganäs");

            Assert.Equal("SE-MAF", sim.Restored!.AtcId);
            Assert.NotEqual("Höganäs", sim.Restored!.AtcId);
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
            store.Save(new AircraftState { SlotName = "SE-ABC", AtcId = "SE-ABC", Title = "Cessna 172 Skyhawk" });
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

    [Fact]
    public void Save_throws_and_leaves_sim_untouched_when_not_ready()
    {
        var dir = TempDir();
        try
        {
            var sim = new FakeSimConnector { NextReadiness = new SimReadiness(true, true, false) };
            var app = new AppService(sim, new StateStore(dir));

            var ex = Assert.Throws<SimNotReadyException>((Action)(() => app.Save("SE-ABC")));

            Assert.Equal("motorerna är igång", ex.Reason);
            Assert.Equal(0, sim.CaptureCalls);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Save_writes_nothing_to_disk_when_not_ready()
    {
        var dir = TempDir();
        try
        {
            var sim = new FakeSimConnector { NextReadiness = new SimReadiness(false, true, true) };
            var store = new StateStore(dir);
            var app = new AppService(sim, store);

            Assert.Throws<SimNotReadyException>((Action)(() => app.Save("SE-ABC")));

            Assert.Null(store.Load("SE-ABC"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_throws_and_leaves_sim_untouched_when_not_ready()
    {
        var dir = TempDir();
        try
        {
            var store = new StateStore(dir);
            store.Save(new AircraftState { SlotName = "SE-ABC", AtcId = "SE-ABC", Title = "Cessna 172 Skyhawk" });
            var sim = new FakeSimConnector { NextReadiness = new SimReadiness(true, false, true) };
            var app = new AppService(sim, store);

            var ex = Assert.Throws<SimNotReadyException>((Action)(() => app.Load("SE-ABC")));

            Assert.Equal("planet rör sig", ex.Reason);
            Assert.Equal(0, sim.RestoreCalls);
            Assert.Null(sim.Restored);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void List_and_Delete_work_when_not_ready()
    {
        var dir = TempDir();
        try
        {
            var store = new StateStore(dir);
            store.Save(new AircraftState { SlotName = "SE-ABC", AtcId = "SE-ABC" });
            var sim = new FakeSimConnector { NextReadiness = new SimReadiness(false, false, false) };
            var app = new AppService(sim, store);

            Assert.Single(app.List());
            Assert.True(app.Delete("SE-ABC"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
