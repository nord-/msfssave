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
