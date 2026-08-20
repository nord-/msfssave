using MsfsSave;
using MsfsSave.Core;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// Anslutningen avgör inte om appen får köra. Utan simulator går listan att läsa och poster att
// ta bort; Menu ansluter själv så snart MSFS finns där.
using var sim = new SimConnector();
var store = new StateStore(StateStore.DefaultDirectory);
var app = new AppService(sim, store);
new Menu(app, sim).Run();
