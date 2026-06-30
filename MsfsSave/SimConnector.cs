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
    private REQUESTS? _awaiting;

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

        // Initial avläsning för header (icke-kritisk — kan misslyckas om inget plan är laddat än).
        try
        {
            var snapshot = Capture();
            CurrentTitle = snapshot.Title;
            CurrentAtcId = snapshot.Registration;
        }
        catch
        {
            // Header fylls vid första lyckade Capture istället.
        }
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
        var req = (REQUESTS)data.dwRequestID;
        switch (req)
        {
            case REQUESTS.Aircraft: _lastAircraft = (AircraftData)data.dwData[0]; break;
            case REQUESTS.Fuel: _lastFuel = (FuelData)data.dwData[0]; break;
        }
        if (_awaiting == req) _received = true;
    }

    private void PumpUntilReceived(REQUESTS expected)
    {
        _awaiting = expected;
        _received = false;
        var deadline = DateTime.UtcNow + ReadTimeout;
        while (!_received && DateTime.UtcNow < deadline)
        {
            if (_sc == null) break;
            try
            {
                if (_event.WaitOne(TimeSpan.FromMilliseconds(200)))
                    _sc.ReceiveMessage();
            }
            catch (ObjectDisposedException) { break; }
        }
        _awaiting = null;
        if (!_received) throw new TimeoutException("Inget svar från simulatorn inom tidsgränsen.");
    }

    public AircraftState Capture()
    {
        if (_sc == null) throw new InvalidOperationException("Inte ansluten till simulatorn.");

        _lastAircraft = null;
        _sc.RequestDataOnSimObject(REQUESTS.Aircraft, DEFINITIONS.Aircraft, SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.ONCE, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
        PumpUntilReceived(REQUESTS.Aircraft);
        var ac = _lastAircraft ?? throw new InvalidOperationException("Fick inga flygplansdata från simulatorn.");

        _lastFuel = null;
        _sc.RequestDataOnSimObject(REQUESTS.Fuel, DEFINITIONS.Fuel, SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.ONCE, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
        PumpUntilReceived(REQUESTS.Fuel);
        var fuel = _lastFuel ?? throw new InvalidOperationException("Fick ingen bränsledata från simulatorn.");

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
        _ = def; // suppress unused warning
        _ = index;
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
        _lastFuel = null;
        _sc!.RequestDataOnSimObject(REQUESTS.Fuel, DEFINITIONS.Fuel, SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.ONCE, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
        PumpUntilReceived(REQUESTS.Fuel);
        var fuel = _lastFuel ?? throw new InvalidOperationException("Fick ingen bränsledata från simulatorn.");
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
            _lastAircraft = null;
            _sc!.RequestDataOnSimObject(REQUESTS.Aircraft, DEFINITIONS.Aircraft, SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.ONCE, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
            PumpUntilReceived(REQUESTS.Aircraft);
            return _lastAircraft?.title ?? CurrentTitle;
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
