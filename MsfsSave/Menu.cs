using System.Text;
using MsfsSave.Core;

namespace MsfsSave;

/// <summary>Listförst-gränssnitt. Console-I/O och tangentdispatch; all logik ligger i Core.</summary>
public class Menu
{
    private readonly AppService _app;
    private readonly ISimConnector _sim;
    private readonly SavedList _list = new();
    private SimReadiness? _readiness;
    private string _readinessError = "";
    private string _status = "";
    private bool _prompting;

    public Menu(AppService app, ISimConnector sim)
    {
        _app = app;
        _sim = sim;
    }

    public void Run()
    {
        RefreshList();
        RefreshReadiness();
        Draw(_status);

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Escape) return;

            // Tillståndet läses en gång per åtgärdstangent — aldrig vid pilnavigering,
            // som annars skulle kunna blockera 5 s när simulatorn inte svarar.
            if (IsActionKey(key)) RefreshReadiness();

            var handled = true;
            switch (key.Key)
            {
                case ConsoleKey.UpArrow: _list.MoveUp(); break;
                case ConsoleKey.DownArrow: _list.MoveDown(); break;
                case ConsoleKey.Enter: DoLoad(); break;
                case ConsoleKey.F2: DoSave(); break;
                case ConsoleKey.Delete: DoDelete(); break;
                case ConsoleKey.R when key.Modifiers == ConsoleModifiers.None: break;
                default: handled = false; break;
            }

            if (handled) Draw(_status);
        }
    }

    private static bool IsActionKey(ConsoleKeyInfo key) => key.Key switch
    {
        ConsoleKey.Enter => true,
        ConsoleKey.F2 or ConsoleKey.Delete => true,
        ConsoleKey.R when key.Modifiers == ConsoleModifiers.None => true,
        _ => false
    };

    // ── Åtgärder ──────────────────────────────────────────────────────────

    private void DoLoad()
    {
        var chosen = _list.Selected;
        if (chosen is null) return;

        try
        {
            Draw(StatusText.WritingToSim);
            var result = _app.Load(chosen.Registration);
            _status = StatusText.Loaded(result);
        }
        catch (SimNotReadyException ex) { _status = StatusText.Blocked("ladda", ex.Reason); }
        catch (Exception ex) { _status = StatusText.Error(ex.Message); }
        finally { FlushKeys(); }
    }

    private void DoSave()
    {
        // Tillståndet är redan läst för det här tangenttrycket. Kontrollen här är bara till för
        // att slippa visa prompten i onödan; AppService.RequireReady är den verkliga spärren.
        if (_readiness is null || !_readiness.CanTransfer)
        {
            _status = StatusText.Blocked("spara", _readiness?.BlockReason ?? "simulatorns tillstånd är okänt");
            return;
        }

        var suggestion = (_sim.CurrentAtcId ?? "").Trim();
        var input = Prompt($"Registrering [{suggestion}]: ");
        if (input is null) { _status = ""; return; }

        var registration = string.IsNullOrWhiteSpace(input) ? suggestion : input.Trim();
        if (string.IsNullOrWhiteSpace(registration)) { _status = StatusText.NoRegistration; return; }

        try
        {
            Draw(StatusText.ReadingFromSim);
            var result = _app.Save(registration);
            RefreshList();
            _list.SelectByRegistration(registration);
            _status = StatusText.Saved(result);
        }
        catch (SimNotReadyException ex) { _status = StatusText.Blocked("spara", ex.Reason); }
        catch (Exception ex) { _status = StatusText.Error(ex.Message); }
        finally { FlushKeys(); }
    }

    private void DoDelete()
    {
        var chosen = _list.Selected;
        if (chosen is null) return;

        try
        {
            var existed = _app.Delete(chosen.Registration);
            RefreshList();
            _status = StatusText.Deleted(chosen.Registration, existed);
        }
        catch (Exception ex) { _status = StatusText.Error(ex.Message); }
    }

    // ── Tillstånd ─────────────────────────────────────────────────────────

    private void RefreshList() => _list.Replace(_app.List());

    private void RefreshReadiness()
    {
        try
        {
            _readiness = _sim.ReadReadiness();
            _readinessError = "";
        }
        catch (Exception ex)
        {
            // Får inte slukas: ett tyst fel här låser appen utan att förklara varför.
            _readiness = null;
            _readinessError = ex.Message;
            _status = StatusText.Error(ex.Message);
        }
    }

    private static void FlushKeys()
    {
        while (Console.KeyAvailable) Console.ReadKey(intercept: true);
    }

    // ── Rendering ─────────────────────────────────────────────────────────

    private static int Width => Math.Max(20, Console.WindowWidth - 1);

    private static void WriteRow(string text) => Console.WriteLine(TextFormat.Truncate(text, Width));

    private void Draw(string status)
    {
        Console.Clear();

        var connection = _sim.IsConnected ? "ansluten" : "ej ansluten";
        var plane = string.IsNullOrWhiteSpace(_sim.CurrentTitle)
            ? "—"
            : $"{_sim.CurrentTitle} / {_sim.CurrentAtcId}";
        WriteRow($"=== msfssave ===        {connection}: {plane}");

        var locked = _readiness is null || !_readiness.CanTransfer;
        if (locked)
        {
            var reason = _readiness?.BlockReason
                ?? (_readinessError.Length > 0
                    ? $"simulatorns tillstånd är okänt ({_readinessError})"
                    : "simulatorns tillstånd är okänt");
            WriteRow($" ⚑ Spara och ladda låst: {reason}. Tryck R för att läsa om.");
        }

        Console.WriteLine();

        if (_list.Count == 0)
        {
            WriteRow(" Inga sparade positioner — F2 sparar den här.");
        }
        else
        {
            WriteRow($"   {"Reg",-RegWidth} {"Sparad",-StampWidth}   Flygplan");
            for (var i = 0; i < _list.Count; i++)
            {
                var s = _list.Items[i];
                var marker = i == _list.SelectedIndex ? " ›" : "  ";
                var stamp = s.SavedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                var title = TextFormat.Truncate(s.Title, TitleWidth);
                WriteRow($"{marker} {s.Registration,-RegWidth} {stamp,-StampWidth}   {title}");
            }
        }

        Console.WriteLine();
        if (_prompting) WriteKeyRow(new[] { ("Enter", "spara"), ("ESC", "avbryt") });
        else WriteKeyRow(KeyRow(locked));
        Console.WriteLine();
        WriteRow(" " + status);
    }

    private const int RegWidth = 12;
    private const int StampWidth = 16;

    /// <summary>Det som blir över till flygplansnamnet när övriga kolumner tagit sitt.</summary>
    private static int TitleWidth => Math.Max(8, Width - (3 + RegWidth + 1 + StampWidth + 3));

    private (string Key, string Label)[] KeyRow(bool locked)
    {
        var keys = new List<(string, string)>();
        if (_list.Count > 0) keys.Add(("↑↓", "välj"));
        if (_list.Count > 0 && !locked) keys.Add(("Enter", "ladda"));
        if (!locked) keys.Add(("F2", "spara"));
        if (_list.Count > 0) keys.Add(("Delete", "ta bort"));
        keys.Add(("R", "uppdatera"));
        keys.Add(("ESC", "avsluta"));
        return keys.ToArray();
    }

    /// <summary>Tangenten i cyan, förklaringen i normalfärg. Kapar tyst vid fönstrets kant.</summary>
    private static void WriteKeyRow((string Key, string Label)[] items)
    {
        var remaining = Width - 1;
        Console.Write(" ");

        for (var i = 0; i < items.Length; i++)
        {
            var separator = i == 0 ? "" : "   ";
            var chunk = separator.Length + items[i].Key.Length + 1 + items[i].Label.Length;
            if (chunk > remaining) break;

            Console.Write(separator);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(items[i].Key);
            Console.ResetColor();
            Console.Write(" " + items[i].Label);
            remaining -= chunk;
        }

        Console.WriteLine();
    }

    /// <summary>Läser en rad med egen tangentloop så att ESC kan avbryta. Null = avbrutet.</summary>
    private string? Prompt(string label)
    {
        var buffer = new StringBuilder();
        _prompting = true;
        try
        {
            return ReadLine(label, buffer);
        }
        finally
        {
            _prompting = false;
        }
    }

    private string? ReadLine(string label, StringBuilder buffer)
    {
        while (true)
        {
            Draw(label + buffer + "▏");
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Escape) return null;
            if (key.Key == ConsoleKey.Enter) return buffer.ToString();
            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0) buffer.Length--;
                continue;
            }
            if (!char.IsControl(key.KeyChar)) buffer.Append(key.KeyChar);
        }
    }
}
