using System.Text;
using MsfsSave.Core;

namespace MsfsSave;

/// <summary>Listförst-gränssnitt. Console-I/O och tangentdispatch; all logik ligger i Core.</summary>
public class Menu
{
    private readonly AppService _app;
    private readonly ISimConnector _sim;
    private readonly SavedList _list = new();
    // 5 s: ett misslyckat SimConnect-försök blockerar tråden ~450 ms, så tätare försök skulle
    // märkas som tröghet i navigeringen så länge simulatorn är avstängd.
    private readonly ReconnectPolicy _reconnect = new(TimeSpan.FromSeconds(5));
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
        TryConnect();
        if (_sim.IsConnected) RefreshReadiness();
        Draw(_status);

        while (true)
        {
            // Ingen blockerande ReadKey: loopen måste komma tillbaka regelbundet för att kunna
            // återansluta av sig själv. Allt sker ändå i den här tråden — inget konkurrerar om
            // Console, och skärmen ritas bara om när något faktiskt ändrats.
            if (!Console.KeyAvailable)
            {
                if (Tick()) Draw(_status);
                // Ett anslutningsförsök kan ha tagit några hundra ms — kom en tangent under det
                // ska den behandlas nu, inte efter ytterligare en sömn.
                if (!Console.KeyAvailable) Thread.Sleep(PollInterval);
                continue;
            }

            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Escape) return;

            // Simulatorn rörs en gång per tangent som faktiskt behöver den — aldrig vid
            // pilnavigering eller Delete, som annars skulle betala ~450 ms för ett misslyckat
            // Connect plus upp till 5 s när simulatorn inte svarar.
            if (UsesSim(key))
            {
                TryConnect();
                RefreshReadiness();
            }

            var handled = true;
            switch (key.Key)
            {
                case ConsoleKey.UpArrow: _list.MoveUp(); break;
                case ConsoleKey.DownArrow: _list.MoveDown(); break;
                case ConsoleKey.Enter: DoLoad(); break;
                case ConsoleKey.F2: DoSave(); break;
                case ConsoleKey.F5: DoQuickSave(); break;
                case ConsoleKey.Delete: DoDelete(); break;
                case ConsoleKey.R when key.Modifiers == ConsoleModifiers.None: break;
                default: handled = false; break;
            }

            if (handled) Draw(_status);
        }
    }

    /// <summary>
    /// Tangenter vars åtgärd kräver simulatorn. Delete står medvetet utanför — borttagning rör
    /// bara sparfilerna och ska svara direkt även när MSFS är avstängt.
    /// </summary>
    private static bool UsesSim(ConsoleKeyInfo key) => key.Key switch
    {
        ConsoleKey.Enter or ConsoleKey.F2 or ConsoleKey.F5 => true,
        ConsoleKey.R when key.Modifiers == ConsoleModifiers.None => true,
        _ => false
    };

    // ── Åtgärder ──────────────────────────────────────────────────────────

    private void DoLoad()
    {
        var chosen = _list.Selected;
        if (chosen is null) return;

        // Samma spärr som DoSave, så statusraden säger samma sak som headern i stället för att
        // AppService.RequireReady kastar ett rått "Inte ansluten till simulatorn".
        if (Blocked("ladda")) return;

        try
        {
            Draw(StatusText.WritingToSim);
            var result = _app.Load(chosen.SlotName);
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
        if (Blocked("spara")) return;

        var suggestion = (_sim.CurrentAtcId ?? "").Trim();
        var input = Prompt($"Namn på sparplats [{suggestion}]: ");
        if (input is null) { _status = ""; return; }

        var slotName = string.IsNullOrWhiteSpace(input) ? suggestion : input.Trim();
        if (string.IsNullOrWhiteSpace(slotName)) { _status = StatusText.NoSlotName; return; }

        try
        {
            Draw(StatusText.ReadingFromSim);
            var result = _app.Save(slotName);
            RefreshList();
            _list.SelectBySlotName(slotName);
            _status = StatusText.Saved(result);
        }
        catch (SimNotReadyException ex) { _status = StatusText.Blocked("spara", ex.Reason); }
        catch (Exception ex) { _status = StatusText.Error(ex.Message); }
        finally { FlushKeys(); }
    }

    private void DoQuickSave()
    {
        var chosen = _list.Selected;
        if (chosen is null) return;

        // Samma spärr som DoSave — RefreshReadiness har redan körts för det här tangenttrycket.
        if (Blocked("spara")) return;

        try
        {
            Draw(StatusText.ReadingFromSim);
            var result = _app.Save(chosen.SlotName);
            RefreshList();
            _list.SelectBySlotName(chosen.SlotName);
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
            var existed = _app.Delete(chosen.SlotName);
            RefreshList();
            _status = StatusText.Deleted(chosen.SlotName, existed);
        }
        catch (Exception ex) { _status = StatusText.Error(ex.Message); }
    }

    // ── Tillstånd ─────────────────────────────────────────────────────────

    private const int PollInterval = 100;

    /// <summary>Sant när överföring är spärrad; sätter då statusraden med samma orsak som headern.</summary>
    private bool Blocked(string verb)
    {
        var reason = StatusText.LockReason(_sim.IsConnected, _readiness, _readinessError);
        if (reason is null) return false;
        _status = StatusText.Blocked(verb, reason);
        return true;
    }

    /// <summary>
    /// Ett varv utan tangenttryck. Sant när skärmen behöver ritas om — bara vid faktisk
    /// förändring, annars skulle Console.Clear flimra tio gånger i sekunden.
    /// </summary>
    private bool Tick()
    {
        var wasConnected = _sim.IsConnected;
        if (_reconnect.ShouldAttempt(wasConnected, DateTime.UtcNow)) TryConnect();

        if (_sim.IsConnected == wasConnected) return false;

        if (_sim.IsConnected) RefreshReadiness();
        else _readiness = null;
        return true;
    }

    /// <summary>Ett anslutningsförsök. Misslyckas tyst — headern visar redan att simen saknas.</summary>
    private void TryConnect()
    {
        if (_sim.IsConnected) return;
        try
        {
            _sim.Connect();
            _readinessError = "";
        }
        catch
        {
            // Nästa försök kommer om några sekunder; ingen anledning att skrika om varje.
        }
    }

    private void RefreshList() => _list.Replace(_app.List());

    private void RefreshReadiness()
    {
        if (!_sim.IsConnected)
        {
            _readiness = null;
            return;
        }

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

        var reason = StatusText.LockReason(_sim.IsConnected, _readiness, _readinessError);
        var locked = reason != null;
        if (locked)
        {
            // Ingen uppmaning att trycka R utan anslutning — den kommer av sig själv.
            var hint = _sim.IsConnected ? " Tryck R för att läsa om." : "";
            WriteRow($" ⚑ Spara och ladda låst: {reason}.{hint}");
        }

        Console.WriteLine();

        if (_list.Count == 0)
        {
            WriteRow(" Inga sparade positioner — F2 sparar den här.");
        }
        else
        {
            WriteRow($"   {"Namn",-NameWidth} {"Reg",-RegWidth} {"Sparad",-StampWidth}   Flygplan");
            for (var i = 0; i < _list.Count; i++)
            {
                var s = _list.Items[i];
                var marker = i == _list.SelectedIndex ? " ›" : "  ";
                var stamp = s.SavedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                var title = TextFormat.Truncate(s.Title, TitleWidth);
                var reg = TextFormat.Truncate(s.AtcId, RegWidth);
                WriteRow($"{marker} {s.SlotName,-NameWidth} {reg,-RegWidth} {stamp,-StampWidth}   {title}");
            }
        }

        Console.WriteLine();
        if (_prompting) WriteKeyRow(new[] { ("Enter", "spara"), ("ESC", "avbryt") });
        else WriteKeyRow(KeyRow(locked));
        Console.WriteLine();
        WriteRow(" " + status);
    }

    private const int NameWidth = 12;
    private const int RegWidth = 9;
    private const int StampWidth = 16;

    /// <summary>Det som blir över till flygplansnamnet när övriga kolumner tagit sitt.</summary>
    private static int TitleWidth => Math.Max(8, Width - (3 + NameWidth + 1 + RegWidth + 1 + StampWidth + 3));

    private (string Key, string Label)[] KeyRow(bool locked)
    {
        var keys = new List<(string, string)>();
        if (_list.Count > 0) keys.Add(("↑↓", "välj"));
        if (_list.Count > 0 && !locked) keys.Add(("Enter", "ladda"));
        if (!locked) keys.Add(("F2", "spara"));
        if (_list.Count > 0 && !locked) keys.Add(("F5", "skriv över"));
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
