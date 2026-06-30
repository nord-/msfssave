# msfssave Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** En interaktiv .NET-konsollapp som sparar och laddar ett MSFS-flygplans tillstånd (registrering, 3D-position + attityd, payload, bränsle) via SimConnect.

**Architecture:** Tre projekt. `MsfsSave.Core` (klassbibliotek) innehåller datamodell, JSON-persistens, ren logik och `ISimConnector`-gränssnittet — helt enhetstestbart utan simulator. `MsfsSave` (konsoll-exe) innehåller `SimConnector` (Managed SimConnect), `Menu` och `Program`. `MsfsSave.Core.Tests` (xUnit) testar Core utan att MSFS SDK behöver finnas. SimConnect-anslutningen öppnas en gång och återanvänds; positionsåterställning sker atomiskt via `SIMCONNECT_DATA_INITPOSITION` med fart 0.

**Tech Stack:** .NET 10, C#, xUnit, System.Text.Json, Managed SimConnect (`Microsoft.FlightSimulator.SimConnect.dll` från MSFS SDK).

---

## File Structure

**`MsfsSave.Core/` (net10.0, ingen SimConnect-referens — testbar):**
- `Models.cs` — `AircraftState`, `PositionState`, `PayloadStation` (records → JSON)
- `StateStore.cs` — läs/skriv/lista/ta bort JSON-filer per registrering i `%APPDATA%\msfssave`
- `FuelMath.cs` — ren `ClampToCapacity`-hjälpare
- `StateComparison.cs` — ren `TitleMatches`-hjälpare för matchningsvarning
- `ISimConnector.cs` — gränssnitt mot simulatorn + `RestoreReport`
- `AppService.cs` — orkestrerar Save/Load/List/Delete ovanpå `ISimConnector` + `StateStore`

**`MsfsSave/` (net10.0-windows, x64 — refererar Core + SimConnect-DLL):**
- `SimConnector.cs` — konkret `ISimConnector` ovanpå Managed SimConnect
- `Menu.cs` — meny-loop, Console-I/O, anropar `AppService`
- `Program.cs` — wiring (DefaultDirectory, SimConnector, AppService, Menu)
- `MsfsSave.csproj`

**`MsfsSave.Core.Tests/` (net10.0 — refererar Core):**
- `FakeSimConnector.cs` — testdubbel för `ISimConnector`
- `StateStoreTests.cs`, `FuelMathTests.cs`, `StateComparisonTests.cs`, `AppServiceTests.cs`

**Rot:**
- `MsfsSave.sln`

**Manuellt verifierade enheter (kan inte enhetstestas utan sim):** `SimConnector`, `Menu`, `Program`. Verifieras med checklistan i Task 10.

---

## Task 1: Solution och projekt-scaffolding

**Files:**
- Create: `MsfsSave.sln`, `MsfsSave.Core/MsfsSave.Core.csproj`, `MsfsSave/MsfsSave.csproj`, `MsfsSave.Core.Tests/MsfsSave.Core.Tests.csproj`

- [ ] **Step 1: Skapa solution och projekt**

Run:
```bash
cd "C:/Users/ricka/Projects/msfssave"
dotnet new sln -n MsfsSave
dotnet new classlib -n MsfsSave.Core -f net10.0 -o MsfsSave.Core
dotnet new console  -n MsfsSave      -f net10.0 -o MsfsSave
dotnet new xunit    -n MsfsSave.Core.Tests -f net10.0 -o MsfsSave.Core.Tests
rm MsfsSave.Core/Class1.cs MsfsSave.Core.Tests/UnitTest1.cs
dotnet sln add MsfsSave.Core MsfsSave MsfsSave.Core.Tests
dotnet add MsfsSave reference MsfsSave.Core
dotnet add MsfsSave.Core.Tests reference MsfsSave.Core
```

- [ ] **Step 2: Sätt konsollprojektets target och plattform**

Replace `MsfsSave/MsfsSave.csproj` with:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Platforms>x64</Platforms>
    <PlatformTarget>x64</PlatformTarget>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>msfssave</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\MsfsSave.Core\MsfsSave.Core.csproj" />
  </ItemGroup>
  <!-- SimConnect-referens läggs till i Task 8 -->
</Project>
```

- [ ] **Step 3: Aktivera nullable/usings i Core**

Ensure `MsfsSave.Core/MsfsSave.Core.csproj` has `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` inside its `<PropertyGroup>`.

- [ ] **Step 4: Bygg och verifiera**

Run: `dotnet build`
Expected: Build succeeded, 3 projekt.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Scaffolda solution: Core, konsoll och testprojekt"
```

---

## Task 2: Datamodell och JSON round-trip i StateStore

**Files:**
- Create: `MsfsSave.Core/Models.cs`, `MsfsSave.Core/StateStore.cs`, `MsfsSave.Core.Tests/StateStoreTests.cs`

- [ ] **Step 1: Skriv det fallerande testet**

`MsfsSave.Core.Tests/StateStoreTests.cs`:
```csharp
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
}
```

- [ ] **Step 2: Kör testet, verifiera att det fallerar**

Run: `dotnet test --filter StateStoreTests`
Expected: FAIL — `AircraftState`/`StateStore` finns inte (kompileringsfel).

- [ ] **Step 3: Skapa modellen**

`MsfsSave.Core/Models.cs`:
```csharp
namespace MsfsSave.Core;

public record AircraftState
{
    public string Registration { get; init; } = "";
    public string Title { get; init; } = "";
    public DateTime SavedAtUtc { get; init; }
    public PositionState Position { get; init; } = new();
    public Dictionary<string, double> FuelGallons { get; init; } = new();
    public List<PayloadStation> PayloadLbs { get; init; } = new();
}

public record PositionState
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double AltitudeFeet { get; init; }
    public double PitchDeg { get; init; }
    public double BankDeg { get; init; }
    public double HeadingTrueDeg { get; init; }
    public bool OnGround { get; init; }
}

public record PayloadStation
{
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public double Weight { get; init; }
}
```

- [ ] **Step 4: Implementera StateStore (Save/Load/Exists)**

`MsfsSave.Core/StateStore.cs`:
```csharp
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
```

- [ ] **Step 5: Kör testerna, verifiera grönt**

Run: `dotnet test --filter StateStoreTests`
Expected: PASS (2 tester).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Lägg till datamodell och JSON-persistens (Save/Load)"
```

---

## Task 3: StateStore List och Delete

**Files:**
- Modify: `MsfsSave.Core/StateStore.cs`
- Modify: `MsfsSave.Core.Tests/StateStoreTests.cs`

- [ ] **Step 1: Skriv de fallerande testerna**

Lägg till i `StateStoreTests`:
```csharp
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
            Assert.Equal("SE-ABC", all[0].Registration);
            Assert.Equal("SE-XYZ", all[1].Registration);
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
```

- [ ] **Step 2: Kör, verifiera fail**

Run: `dotnet test --filter StateStoreTests`
Expected: FAIL — `List`/`Delete` finns inte.

- [ ] **Step 3: Implementera List och Delete**

Lägg till i `StateStore` (efter `Exists`):
```csharp
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
        return list.OrderBy(s => s.Registration, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public bool Delete(string registration)
    {
        var path = PathFor(registration);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }
```

- [ ] **Step 4: Kör, verifiera grönt**

Run: `dotnet test --filter StateStoreTests`
Expected: PASS (4 tester).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Lägg till List och Delete i StateStore"
```

---

## Task 4: FuelMath.ClampToCapacity

**Files:**
- Create: `MsfsSave.Core/FuelMath.cs`, `MsfsSave.Core.Tests/FuelMathTests.cs`

- [ ] **Step 1: Skriv det fallerande testet**

`MsfsSave.Core.Tests/FuelMathTests.cs`:
```csharp
using MsfsSave.Core;

namespace MsfsSave.Core.Tests;

public class FuelMathTests
{
    [Theory]
    [InlineData(20.0, 30.0, 20.0)]   // under kapacitet -> oförändrad
    [InlineData(40.0, 30.0, 30.0)]   // över kapacitet  -> klampas
    [InlineData(-5.0, 30.0, 0.0)]    // negativ         -> 0
    [InlineData(30.0, 30.0, 30.0)]   // exakt kapacitet -> oförändrad
    public void ClampToCapacity_clamps_into_valid_range(double requested, double capacity, double expected)
    {
        Assert.Equal(expected, FuelMath.ClampToCapacity(requested, capacity));
    }
}
```

- [ ] **Step 2: Kör, verifiera fail**

Run: `dotnet test --filter FuelMathTests`
Expected: FAIL — `FuelMath` finns inte.

- [ ] **Step 3: Implementera**

`MsfsSave.Core/FuelMath.cs`:
```csharp
namespace MsfsSave.Core;

public static class FuelMath
{
    public static double ClampToCapacity(double requestedGallons, double capacityGallons)
    {
        if (requestedGallons < 0) return 0;
        if (requestedGallons > capacityGallons) return capacityGallons;
        return requestedGallons;
    }
}
```

- [ ] **Step 4: Kör, verifiera grönt**

Run: `dotnet test --filter FuelMathTests`
Expected: PASS (4 fall).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Lägg till FuelMath.ClampToCapacity"
```

---

## Task 5: StateComparison.TitleMatches

**Files:**
- Create: `MsfsSave.Core/StateComparison.cs`, `MsfsSave.Core.Tests/StateComparisonTests.cs`

- [ ] **Step 1: Skriv det fallerande testet**

`MsfsSave.Core.Tests/StateComparisonTests.cs`:
```csharp
using MsfsSave.Core;

namespace MsfsSave.Core.Tests;

public class StateComparisonTests
{
    [Theory]
    [InlineData("Cessna 172", "Cessna 172", true)]
    [InlineData("Cessna 172", "  cessna 172 ", true)]   // trim + case-insensitiv
    [InlineData("Cessna 172", "Airbus A320", false)]
    [InlineData("", "Airbus A320", false)]
    public void TitleMatches_compares_trimmed_case_insensitive(string saved, string loaded, bool expected)
    {
        Assert.Equal(expected, StateComparison.TitleMatches(saved, loaded));
    }
}
```

- [ ] **Step 2: Kör, verifiera fail**

Run: `dotnet test --filter StateComparisonTests`
Expected: FAIL — `StateComparison` finns inte.

- [ ] **Step 3: Implementera**

`MsfsSave.Core/StateComparison.cs`:
```csharp
namespace MsfsSave.Core;

public static class StateComparison
{
    public static bool TitleMatches(string savedTitle, string loadedTitle)
        => string.Equals(savedTitle.Trim(), loadedTitle.Trim(), StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 4: Kör, verifiera grönt**

Run: `dotnet test --filter StateComparisonTests`
Expected: PASS (4 fall).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Lägg till StateComparison.TitleMatches"
```

---

## Task 6: ISimConnector-gränssnitt och RestoreReport

**Files:**
- Create: `MsfsSave.Core/ISimConnector.cs`

- [ ] **Step 1: Skapa gränssnittet**

`MsfsSave.Core/ISimConnector.cs`:
```csharp
namespace MsfsSave.Core;

/// <summary>Abstraktion mot simulatorn. Isolerar SimConnect så att AppService kan testas.</summary>
public interface ISimConnector : IDisposable
{
    bool IsConnected { get; }

    /// <summary>Senast avlästa flygplanstitel (för menyns header). Tom om okänd.</summary>
    string CurrentTitle { get; }

    /// <summary>Senast avlästa ATC ID/registrering (för header + namnförslag). Tom om okänd.</summary>
    string CurrentAtcId { get; }

    /// <summary>Öppnar anslutningen. Kastar vid fel (t.ex. simulatorn inte igång).</summary>
    void Connect();

    /// <summary>Läser nuvarande fullständiga tillstånd från simulatorn.</summary>
    AircraftState Capture();

    /// <summary>Skriver tillståndet till simulatorn och rapporterar utfall.</summary>
    RestoreReport Restore(AircraftState state);
}

public record RestoreReport
{
    /// <summary>True om ATC ID kunde skrivas till simulatorn.</summary>
    public bool AtcIdSet { get; init; }

    /// <summary>Titeln på det flygplan som faktiskt är laddat i simulatorn vid laddning.</summary>
    public string LoadedTitle { get; init; } = "";
}
```

- [ ] **Step 2: Bygg och verifiera**

Run: `dotnet build MsfsSave.Core`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "Lägg till ISimConnector-gränssnitt och RestoreReport"
```

---

## Task 7: AppService med FakeSimConnector

**Files:**
- Create: `MsfsSave.Core/AppService.cs`, `MsfsSave.Core.Tests/FakeSimConnector.cs`, `MsfsSave.Core.Tests/AppServiceTests.cs`

- [ ] **Step 1: Skriv testdubbeln**

`MsfsSave.Core.Tests/FakeSimConnector.cs`:
```csharp
using MsfsSave.Core;

namespace MsfsSave.Core.Tests;

public class FakeSimConnector : ISimConnector
{
    public bool IsConnected { get; set; } = true;
    public string CurrentTitle { get; set; } = "Cessna 172 Skyhawk";
    public string CurrentAtcId { get; set; } = "SE-ABC";

    public AircraftState NextCapture { get; set; } = new();
    public AircraftState? Restored { get; private set; }
    public RestoreReport NextReport { get; set; } = new() { AtcIdSet = true, LoadedTitle = "Cessna 172 Skyhawk" };

    public void Connect() { IsConnected = true; }
    public AircraftState Capture() => NextCapture;
    public RestoreReport Restore(AircraftState state) { Restored = state; return NextReport; }
    public void Dispose() { }
}
```

- [ ] **Step 2: Skriv de fallerande testerna**

`MsfsSave.Core.Tests/AppServiceTests.cs`:
```csharp
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
            Assert.Throws<FileNotFoundException>(() => app.Load("SAKNAS"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
```

- [ ] **Step 3: Kör, verifiera fail**

Run: `dotnet test --filter AppServiceTests`
Expected: FAIL — `AppService`, `SaveResult`, `LoadResult` finns inte.

- [ ] **Step 4: Implementera AppService**

`MsfsSave.Core/AppService.cs`:
```csharp
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
```

- [ ] **Step 5: Kör, verifiera grönt**

Run: `dotnet test --filter AppServiceTests`
Expected: PASS (5 tester).

- [ ] **Step 6: Kör hela sviten**

Run: `dotnet test`
Expected: PASS — alla Core-tester gröna.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "Lägg till AppService och testdubbel"
```

---

## Task 8: SimConnector (Managed SimConnect) — manuell verifiering

> Denna enhet kan inte enhetstestas utan en körande simulator. Koden nedan är komplett men marshaling och simvar-listor MÅSTE valideras mot MSFS i Task 10. Justera tank-/stationslistorna om ditt plan använder andra tankar.

**Files:**
- Modify: `MsfsSave/MsfsSave.csproj` (lägg till SimConnect-referens)
- Create: `MsfsSave/SimConnector.cs`

- [ ] **Step 1: Lägg till SimConnect-referens i csproj**

Lägg till i `MsfsSave/MsfsSave.csproj` (inuti ny `<ItemGroup>`). Vi pekar på MSFS SDK:ts **officiella** DLL:er via miljövariabeln `MSFS_SDK` (= `C:\MSFS SDK\` på den här maskinen; daterade juni 2025 och garanterat aktuella). En `SimConnectDir`-property samlar pathen så den bara behöver ändras på ett ställe. Den **managed** wrappern (`Microsoft.FlightSimulator.SimConnect.dll`) är byggreferensen; den **nativa** (`SimConnect.dll`) kopieras bredvid exe vid körning:
```xml
  <PropertyGroup>
    <SimConnectDir>$(MSFS_SDK)\SimConnect SDK\lib</SimConnectDir>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Microsoft.FlightSimulator.SimConnect">
      <HintPath>$(SimConnectDir)\managed\Microsoft.FlightSimulator.SimConnect.dll</HintPath>
      <Private>true</Private>
    </Reference>
  </ItemGroup>
  <ItemGroup>
    <!-- Native SimConnect.dll måste ligga bredvid exe vid körning -->
    <None Include="$(SimConnectDir)\SimConnect.dll" Condition="Exists('$(SimConnectDir)\SimConnect.dll')">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
```
Fallback om SDK:t inte skulle vara installerat: sätt `SimConnectDir` till `C:\Spel\UniversalAnnouncer` (där finns båda DLL:erna, men en äldre build från april).

- [ ] **Step 2: Implementera SimConnector**

`MsfsSave/SimConnector.cs`:
```csharp
using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;
using MsfsSave.Core;

namespace MsfsSave;

/// <summary>Konkret ISimConnector ovanpå Managed SimConnect (konsoll-läge via event handle).</summary>
public sealed class SimConnector : ISimConnector
{
    private const int WM_USER_SIMCONNECT = 0x0402;
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);

    private enum DEFINITIONS { Aircraft, Fuel, InitPosition, AtcId }
    private enum REQUESTS { Aircraft, Fuel }

    // Ordningen MÅSTE matcha AddToDataDefinition-anropen nedan.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    private struct AircraftData
    {
        public double latitude, longitude, altitude, pitch, bank, heading, onGround;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string title;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string atcId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct FuelData
    {
        public double centerQty, center2Qty, center3Qty,
                      leftMainQty, leftAuxQty, leftTipQty,
                      rightMainQty, rightAuxQty, rightTipQty,
                      external1Qty, external2Qty;
        public double centerCap, center2Cap, center3Cap,
                      leftMainCap, leftAuxCap, leftTipCap,
                      rightMainCap, rightAuxCap, rightTipCap,
                      external1Cap, external2Cap;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    private struct AtcIdData
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string atcId;
    }

    private static readonly (string Key, string Var)[] FuelTanks =
    {
        ("Center", "FUEL TANK CENTER QUANTITY"),
        ("Center2", "FUEL TANK CENTER2 QUANTITY"),
        ("Center3", "FUEL TANK CENTER3 QUANTITY"),
        ("LeftMain", "FUEL TANK LEFT MAIN QUANTITY"),
        ("LeftAux", "FUEL TANK LEFT AUX QUANTITY"),
        ("LeftTip", "FUEL TANK LEFT TIP QUANTITY"),
        ("RightMain", "FUEL TANK RIGHT MAIN QUANTITY"),
        ("RightAux", "FUEL TANK RIGHT AUX QUANTITY"),
        ("RightTip", "FUEL TANK RIGHT TIP QUANTITY"),
        ("External1", "FUEL TANK EXTERNAL1 QUANTITY"),
        ("External2", "FUEL TANK EXTERNAL2 QUANTITY"),
    };

    private const int MaxPayloadStations = 20;

    private readonly EventWaitHandle _event = new(false, EventResetMode.AutoReset);
    private SimConnect? _sc;
    private AircraftData? _lastAircraft;
    private FuelData? _lastFuel;
    private bool _received;

    public bool IsConnected => _sc != null;
    public string CurrentTitle { get; private set; } = "";
    public string CurrentAtcId { get; private set; } = "";

    public void Connect()
    {
        _sc = new SimConnect("msfssave", IntPtr.Zero, WM_USER_SIMCONNECT, _event, 0);
        _sc.OnRecvQuit += (_, _) => Dispose();
        _sc.OnRecvException += (_, e) => Console.Error.WriteLine($"SimConnect-undantag: {e.dwException}");
        _sc.OnRecvSimobjectData += OnRecvData;

        DefineAircraft();
        DefineFuel();
        DefineInitPosition();
        DefineAtcId();

        // Initial avläsning för header.
        var snapshot = Capture();
        CurrentTitle = snapshot.Title;
        CurrentAtcId = snapshot.Registration;
    }

    private void DefineAircraft()
    {
        _sc!.AddToDataDefinition(DEFINITIONS.Aircraft, "PLANE LATITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        _sc.AddToDataDefinition(DEFINITIONS.Aircraft, "PLANE LONGITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        _sc.AddToDataDefinition(DEFINITIONS.Aircraft, "PLANE ALTITUDE", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        _sc.AddToDataDefinition(DEFINITIONS.Aircraft, "PLANE PITCH DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        _sc.AddToDataDefinition(DEFINITIONS.Aircraft, "PLANE BANK DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        _sc.AddToDataDefinition(DEFINITIONS.Aircraft, "PLANE HEADING DEGREES TRUE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        _sc.AddToDataDefinition(DEFINITIONS.Aircraft, "SIM ON GROUND", "bool", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        _sc.AddToDataDefinition(DEFINITIONS.Aircraft, "TITLE", null, SIMCONNECT_DATATYPE.STRING256, 0, SimConnect.SIMCONNECT_UNUSED);
        _sc.AddToDataDefinition(DEFINITIONS.Aircraft, "ATC ID", null, SIMCONNECT_DATATYPE.STRING64, 0, SimConnect.SIMCONNECT_UNUSED);
        _sc.RegisterDataDefineStruct<AircraftData>(DEFINITIONS.Aircraft);
    }

    private void DefineFuel()
    {
        foreach (var (_, var) in FuelTanks)
            _sc!.AddToDataDefinition(DEFINITIONS.Fuel, var, "gallons", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        foreach (var (_, var) in FuelTanks)
            _sc!.AddToDataDefinition(DEFINITIONS.Fuel, var.Replace("QUANTITY", "CAPACITY"), "gallons", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        _sc!.RegisterDataDefineStruct<FuelData>(DEFINITIONS.Fuel);
    }

    private void DefineInitPosition()
    {
        _sc!.AddToDataDefinition(DEFINITIONS.InitPosition, "Initial Position", null, SIMCONNECT_DATATYPE.INITPOSITION, 0, SimConnect.SIMCONNECT_UNUSED);
        _sc.RegisterDataDefineStruct<SIMCONNECT_DATA_INITPOSITION>(DEFINITIONS.InitPosition);
    }

    private void DefineAtcId()
    {
        _sc!.AddToDataDefinition(DEFINITIONS.AtcId, "ATC ID", null, SIMCONNECT_DATATYPE.STRING64, 0, SimConnect.SIMCONNECT_UNUSED);
        _sc.RegisterDataDefineStruct<AtcIdData>(DEFINITIONS.AtcId);
    }

    private void OnRecvData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        switch ((REQUESTS)data.dwRequestID)
        {
            case REQUESTS.Aircraft: _lastAircraft = (AircraftData)data.dwData[0]; break;
            case REQUESTS.Fuel: _lastFuel = (FuelData)data.dwData[0]; break;
        }
        _received = true;
    }

    private void PumpUntilReceived()
    {
        _received = false;
        var deadline = DateTime.UtcNow + ReadTimeout;
        while (!_received && DateTime.UtcNow < deadline)
        {
            if (_event.WaitOne(TimeSpan.FromMilliseconds(200)))
                _sc!.ReceiveMessage();
        }
        if (!_received) throw new TimeoutException("Inget svar från simulatorn inom tidsgränsen.");
    }

    public AircraftState Capture()
    {
        if (_sc == null) throw new InvalidOperationException("Inte ansluten till simulatorn.");

        _sc.RequestDataOnSimObject(REQUESTS.Aircraft, DEFINITIONS.Aircraft, SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.ONCE, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
        PumpUntilReceived();
        var ac = _lastAircraft!.Value;

        _sc.RequestDataOnSimObject(REQUESTS.Fuel, DEFINITIONS.Fuel, SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.ONCE, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
        PumpUntilReceived();
        var fuel = _lastFuel!.Value;

        var fuelQty = new[]
        {
            fuel.centerQty, fuel.center2Qty, fuel.center3Qty,
            fuel.leftMainQty, fuel.leftAuxQty, fuel.leftTipQty,
            fuel.rightMainQty, fuel.rightAuxQty, fuel.rightTipQty,
            fuel.external1Qty, fuel.external2Qty
        };
        var fuelDict = new Dictionary<string, double>();
        for (var i = 0; i < FuelTanks.Length; i++)
            if (fuelQty[i] > 0) fuelDict[FuelTanks[i].Key] = fuelQty[i];

        CurrentTitle = ac.title ?? "";
        CurrentAtcId = ac.atcId ?? "";

        return new AircraftState
        {
            Registration = ac.atcId ?? "",
            Title = ac.title ?? "",
            SavedAtUtc = DateTime.UtcNow,
            Position = new PositionState
            {
                Latitude = ac.latitude,
                Longitude = ac.longitude,
                AltitudeFeet = ac.altitude,
                PitchDeg = ac.pitch,
                BankDeg = ac.bank,
                HeadingTrueDeg = ac.heading,
                OnGround = ac.onGround > 0.5
            },
            FuelGallons = fuelDict,
            PayloadLbs = CapturePayload()
        };
    }

    private List<PayloadStation> CapturePayload()
    {
        // Payload läses station för station via egna definitioner (antal okänt vid kompilering).
        var result = new List<PayloadStation>();
        for (var i = 1; i <= MaxPayloadStations; i++)
        {
            var weight = ReadSinglePayloadWeight(i);
            if (weight is null) break; // station saknas -> klart
            result.Add(new PayloadStation { Index = i, Name = $"Station {i}", Weight = weight.Value });
        }
        return result;
    }

    private double? ReadSinglePayloadWeight(int index)
    {
        // Definierar och läser PAYLOAD STATION WEIGHT:index ad hoc.
        const DEFINITIONS def = DEFINITIONS.AtcId; // återanvänd ej; se kommentar i Task 10
        // Implementeras tillsammans med verifieringen i Task 10 (kräver sim).
        return null;
    }

    public RestoreReport Restore(AircraftState state)
    {
        if (_sc == null) throw new InvalidOperationException("Inte ansluten till simulatorn.");

        // 1. Position/attityd atomiskt, stillastående på marken.
        var pos = new SIMCONNECT_DATA_INITPOSITION
        {
            Latitude = state.Position.Latitude,
            Longitude = state.Position.Longitude,
            Altitude = state.Position.AltitudeFeet,
            Pitch = state.Position.PitchDeg,
            Bank = state.Position.BankDeg,
            Heading = state.Position.HeadingTrueDeg,
            OnGround = (uint)(state.Position.OnGround ? 1 : 0),
            Airspeed = 0
        };
        _sc.SetDataOnSimObject(DEFINITIONS.InitPosition, SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_DATA_SET_FLAG.DEFAULT, pos);

        // 2. Bränsle per tank, klampat mot kapacitet (kapaciteter från senaste Capture om tillgängliga).
        RestoreFuel(state);

        // 3. Payload per station (implementeras i Task 10 tillsammans med avläsningen).

        // 4. ATC ID best effort.
        var atcSet = TrySetAtcId(state.Registration);

        // Läs aktuell titel för matchningskontroll.
        var loadedTitle = SafeReadTitle();
        return new RestoreReport { AtcIdSet = atcSet, LoadedTitle = loadedTitle };
    }

    private void RestoreFuel(AircraftState state)
    {
        // Hämta kapaciteter via en färsk avläsning.
        _sc!.RequestDataOnSimObject(REQUESTS.Fuel, DEFINITIONS.Fuel, SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.ONCE, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
        PumpUntilReceived();
        var fuel = _lastFuel!.Value;
        var caps = new[]
        {
            fuel.centerCap, fuel.center2Cap, fuel.center3Cap,
            fuel.leftMainCap, fuel.leftAuxCap, fuel.leftTipCap,
            fuel.rightMainCap, fuel.rightAuxCap, fuel.rightTipCap,
            fuel.external1Cap, fuel.external2Cap
        };

        var qty = new double[FuelTanks.Length];
        for (var i = 0; i < FuelTanks.Length; i++)
        {
            var requested = state.FuelGallons.TryGetValue(FuelTanks[i].Key, out var v) ? v : 0;
            qty[i] = FuelMath.ClampToCapacity(requested, caps[i]);
        }

        var toSet = new FuelData
        {
            centerQty = qty[0], center2Qty = qty[1], center3Qty = qty[2],
            leftMainQty = qty[3], leftAuxQty = qty[4], leftTipQty = qty[5],
            rightMainQty = qty[6], rightAuxQty = qty[7], rightTipQty = qty[8],
            external1Qty = qty[9], external2Qty = qty[10],
            // kapacitetsfälten skrivs aldrig (read-only i praktiken); skickas oförändrade.
            centerCap = caps[0], center2Cap = caps[1], center3Cap = caps[2],
            leftMainCap = caps[3], leftAuxCap = caps[4], leftTipCap = caps[5],
            rightMainCap = caps[6], rightAuxCap = caps[7], rightTipCap = caps[8],
            external1Cap = caps[9], external2Cap = caps[10]
        };
        _sc.SetDataOnSimObject(DEFINITIONS.Fuel, SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_DATA_SET_FLAG.DEFAULT, toSet);
    }

    private bool TrySetAtcId(string registration)
    {
        try
        {
            var data = new AtcIdData { atcId = registration ?? "" };
            _sc!.SetDataOnSimObject(DEFINITIONS.AtcId, SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_DATA_SET_FLAG.DEFAULT, data);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string SafeReadTitle()
    {
        try
        {
            _sc!.RequestDataOnSimObject(REQUESTS.Aircraft, DEFINITIONS.Aircraft, SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.ONCE, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
            PumpUntilReceived();
            return _lastAircraft!.Value.title ?? "";
        }
        catch
        {
            return CurrentTitle;
        }
    }

    public void Dispose()
    {
        _sc?.Dispose();
        _sc = null;
        _event.Dispose();
    }
}
```

> **Notera (för Task 10):** `ReadSinglePayloadWeight` och payload-skrivning lämnas medvetet stubbade här eftersom de kräver iterativ definition mot ett verkligt plan (antal stationer okänt vid kompilering). De färdigställs och valideras i Task 10 där simulatorn finns tillgänglig. Allt annat i `SimConnector` är komplett.

- [ ] **Step 3: Bygg (kräver MSFS SDK installerat)**

Run: `dotnet build MsfsSave`
Expected: Build succeeded. Om referensen inte hittas: verifiera att `$(SimConnectDir)\Microsoft.FlightSimulator.SimConnect.dll` finns. Saknas SimConnect-typer (INITPOSITION/STRING256/STRING64): byt `SimConnectDir`-pathen till `$(MSFS_SDK)\SimConnect SDK\lib\managed`.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "Lägg till SimConnector ovanpå Managed SimConnect"
```

---

## Task 9: Menu och Program — manuell verifiering

**Files:**
- Create: `MsfsSave/Menu.cs`, modify `MsfsSave/Program.cs`

- [ ] **Step 1: Implementera Menu**

`MsfsSave/Menu.cs`:
```csharp
using MsfsSave.Core;

namespace MsfsSave;

/// <summary>Interaktiv meny-loop. Console-I/O; affärslogik ligger i AppService.</summary>
public class Menu
{
    private readonly AppService _app;
    private readonly ISimConnector _sim;

    public Menu(AppService app, ISimConnector sim)
    {
        _app = app;
        _sim = sim;
    }

    public void Run()
    {
        while (true)
        {
            DrawHeader();
            Console.WriteLine("  1. Spara position");
            Console.WriteLine("  2. Ladda position");
            Console.WriteLine("  3. Lista sparade");
            Console.WriteLine("  4. Ta bort sparad");
            Console.WriteLine("  ESC  Avsluta");
            Console.Write("> ");

            var key = Console.ReadKey(intercept: true);
            Console.WriteLine();

            try
            {
                switch (key.Key)
                {
                    case ConsoleKey.D1 or ConsoleKey.NumPad1: DoSave(); break;
                    case ConsoleKey.D2 or ConsoleKey.NumPad2: DoLoad(); break;
                    case ConsoleKey.D3 or ConsoleKey.NumPad3: DoList(); break;
                    case ConsoleKey.D4 or ConsoleKey.NumPad4: DoDelete(); break;
                    case ConsoleKey.Escape: return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Fel: {ex.Message}");
            }

            Console.WriteLine("\n  (tryck valfri tangent för att fortsätta)");
            Console.ReadKey(intercept: true);
            Console.Clear();
        }
    }

    private void DrawHeader()
    {
        var status = _sim.IsConnected ? "ansluten" : "ej ansluten";
        var plane = string.IsNullOrWhiteSpace(_sim.CurrentTitle) ? "—" : $"{_sim.CurrentTitle} / {_sim.CurrentAtcId}";
        Console.WriteLine("=== msfssave ===            " + $"{status}: {plane}");
        Console.WriteLine();
    }

    private void DoSave()
    {
        var suggestion = string.IsNullOrWhiteSpace(_sim.CurrentAtcId) ? "" : _sim.CurrentAtcId;
        Console.Write($"  Registrering [{suggestion}]: ");
        var input = Console.ReadLine()?.Trim();
        var reg = string.IsNullOrWhiteSpace(input) ? suggestion : input;
        if (string.IsNullOrWhiteSpace(reg)) { Console.WriteLine("  Avbrutet (ingen registrering)."); return; }

        var result = _app.Save(reg);
        Console.WriteLine(result.Overwritten
            ? $"  Skrev över sparat tillstånd för {reg}."
            : $"  Sparade {reg}.");
    }

    private void DoLoad()
    {
        var all = _app.List();
        if (all.Count == 0) { Console.WriteLine("  Inga sparade flygplan."); return; }
        var chosen = ChooseFrom(all);
        if (chosen is null) return;

        var result = _app.Load(chosen.Registration);
        if (result.TitleMismatch)
            Console.WriteLine($"  VARNING: sparad som \"{result.State.Title}\" men \"{result.LoadedTitle}\" är laddat — position/bränsle kanske inte passar.");
        if (!result.AtcIdSet)
            Console.WriteLine("  Obs: ATC ID kunde inte sättas på detta plan.");
        Console.WriteLine($"  Laddade {chosen.Registration}.");
    }

    private void DoList()
    {
        var all = _app.List();
        if (all.Count == 0) { Console.WriteLine("  Inga sparade flygplan."); return; }
        foreach (var s in all)
            Console.WriteLine($"  {s.Registration,-12} {s.Title,-28} {s.SavedAtUtc:yyyy-MM-dd HH:mm} UTC");
    }

    private void DoDelete()
    {
        var all = _app.List();
        if (all.Count == 0) { Console.WriteLine("  Inga sparade flygplan."); return; }
        var chosen = ChooseFrom(all);
        if (chosen is null) return;

        Console.Write($"  Ta bort {chosen.Registration}? (j/N): ");
        if (Console.ReadLine()?.Trim().Equals("j", StringComparison.OrdinalIgnoreCase) == true)
            Console.WriteLine(_app.Delete(chosen.Registration) ? "  Borttaget." : "  Fanns inte.");
        else
            Console.WriteLine("  Avbrutet.");
    }

    private static AircraftState? ChooseFrom(IReadOnlyList<AircraftState> all)
    {
        for (var i = 0; i < all.Count; i++)
            Console.WriteLine($"  {i + 1}. {all[i].Registration} ({all[i].Title})");
        Console.Write("  Välj nummer (Enter = avbryt): ");
        var input = Console.ReadLine()?.Trim();
        if (int.TryParse(input, out var n) && n >= 1 && n <= all.Count)
            return all[n - 1];
        return null;
    }
}
```

- [ ] **Step 2: Implementera Program**

`MsfsSave/Program.cs`:
```csharp
using MsfsSave;
using MsfsSave.Core;

Console.OutputEncoding = System.Text.Encoding.UTF8;

using var sim = new SimConnector();
try
{
    sim.Connect();
}
catch (Exception ex)
{
    Console.WriteLine($"Kunde inte ansluta till simulatorn: {ex.Message}");
    Console.WriteLine("Starta MSFS och ladda ett flygplan, försök sedan igen.");
    return;
}

var store = new StateStore(StateStore.DefaultDirectory);
var app = new AppService(sim, store);
new Menu(app, sim).Run();
```

- [ ] **Step 3: Bygg**

Run: `dotnet build`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "Lägg till meny-loop och programstart"
```

---

## Task 10: Färdigställ payload + manuell end-to-end-verifiering i MSFS

> Kräver körande MSFS 2020 med ett flygplan laddat. Här valideras all SimConnect-marshaling och payload färdigställs mot ett verkligt plan.

**Files:**
- Modify: `MsfsSave/SimConnector.cs` (payload-avläsning och -skrivning)

- [ ] **Step 1: Färdigställ payload-avläsning**

Ersätt `CapturePayload`/`ReadSinglePayloadWeight`-stubbarna med en implementation som först läser `PAYLOAD STATION COUNT`, sedan definierar en struct med `MaxPayloadStations` doubles för `PAYLOAD STATION WEIGHT:1..N` ("pounds"), läser dem i ett svep, och bara behåller de `count` första. Skriv tillbaka i `Restore` via samma definition med `SetDataOnSimObject`. Validera marshaling mot det laddade planet (loggа värden och jämför med MSFS payload-menyn).

- [ ] **Step 2: Verifiera ANSI-marshaling av strängfält**

Bekräfta att `TITLE` och `ATC ID` läses som läsbara strängar (inte avhuggna/skräp). Justera `SizeConst`/`CharSet` om nödvändigt. Kör appen, välj "1. Spara", och kontrollera headerns plan/ATC ID.

- [ ] **Step 3: End-to-end spara/ladda-test**

Manuell checklista i MSFS 2020:
1. Ladda ett plan (t.ex. Cessna 172) på en flygplats. Notera position, bränsle, payload.
2. Kör `msfssave`, välj **1. Spara**, godkänn ATC ID som namn.
3. Flyg iväg / ändra bränsle och payload i MSFS.
4. Välj **2. Ladda**, välj samma registrering.
5. Bekräfta: planet teleporteras till sparad position, står stilla på marken, rätt heading/pitch/bank, bränsle och payload återställda.
6. Kontrollera JSON-filen i `%APPDATA%\msfssave\` ser rätt ut.

- [ ] **Step 4: Verifiera felfall**

1. Stäng MSFS, kör appen → tydligt "kunde inte ansluta"-meddelande, ingen krasch.
2. Ladda ett ANNAT plan än det sparade → matchningsvarning visas men laddning fortsätter.
3. Ladda en registrering som inte finns → tydligt fel.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Färdigställ payload-hantering och verifiera end-to-end i MSFS"
```

---

## Self-Review Notes

**Spec coverage:** Alla spec-krav täcks — interaktiv meny (Task 9), C#/.NET + Managed SimConnect (Task 1, 8), MSFS 2020-simvars (Task 8), reg.nr som nyckel + best-effort ATC ID (Task 7, 8), full attityd stillastående via INITPOSITION (Task 8), JSON per reg i %APPDATA% (Task 2), felhantering inkl. titel-mismatch (Task 7, 9, 10), enhetstester av Core + manuell sim-checklista (Task 2–7, 10), 2024-not (bygger på 2020-simvars).

**Känd avgränsning som följer specen:** Att byta laddad flygplansmodell ligger utanför scope; reg.nr är nyckel. Payload färdigställs i Task 10 mot verkligt plan eftersom antalet stationer inte är känt vid kompilering.
