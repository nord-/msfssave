using System.Diagnostics;
using Microsoft.FlightSimulator.SimConnect;
using MsfsSave.Core;

namespace MsfsSave;

/// <summary>
/// Sonderar om simulatorn svarar, i två steg: först finns processen, sedan svarar SimConnect.
///
/// Ordningen är inte kosmetisk. En misslyckad SimConnect-konstruktor läcker ~4,5 handles per
/// anrop (mätt) och läckan går inte att städa utifrån — objektet finns aldrig att kassera.
/// Att öppna en anslutning i en evig loop medan simulatorn är avstängd är därför inte möjligt.
/// Processkollen kostar ~5 ms, läcker inget, och håller Open borta ur just det fallet.
///
/// INVARIANT: klassen har inga fält och metoden rör inget utanför sig själv. Både SimConnect-
/// objektet och event-handlen skapas och slängs inuti anropet, så inget objekt lever kvar för
/// att delas med tråden som äger den riktiga anslutningen. Det är hela skälet till att den här
/// får köras i bakgrunden — lägger man till delat tillstånd faller det.
/// </summary>
public sealed class SimProbe : ISimProbe
{
    private const int WM_USER_SIMCONNECT = 0x0402;

    /// <summary>Täcker FlightSimulator.exe (2020) och FlightSimulator2024.exe (2024).</summary>
    private const string ProcessPrefix = "FlightSimulator";

    public bool IsAvailable() => SimulatorRunning() && SimConnectAnswers();

    private static bool SimulatorRunning()
    {
        var all = Process.GetProcesses();
        try
        {
            return all.Any(p => p.ProcessName.StartsWith(ProcessPrefix, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            // Kan vi inte läsa processlistan är det inget skäl att påstå att simen saknas —
            // låt SimConnect avgöra i stället.
            return true;
        }
        finally
        {
            foreach (var p in all) p.Dispose();
        }
    }

    /// <summary>
    /// Processen kan finnas långt innan SimConnect tar emot anslutningar — under uppstart och
    /// menyer svarar den inte. Därför räcker inte processkollen som svar.
    /// </summary>
    private static bool SimConnectAnswers()
    {
        try
        {
            using var handle = new EventWaitHandle(false, EventResetMode.AutoReset);
            using var sc = new SimConnect("msfssave-probe", IntPtr.Zero, WM_USER_SIMCONNECT, handle, 0);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
