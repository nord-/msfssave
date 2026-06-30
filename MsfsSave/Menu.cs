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
            Console.WriteLine($"  VARNING: sparad som \"{result.SavedState.Title}\" men \"{result.LoadedTitle}\" är laddat — position/bränsle kanske inte passar.");
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
