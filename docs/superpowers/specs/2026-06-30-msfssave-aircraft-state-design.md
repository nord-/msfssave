# msfssave — spara och ladda flygplanstillstånd från MSFS

**Datum:** 2026-06-30
**Status:** Godkänd design

## Syfte

Ett verktyg för att spara ett flygplans tillstånd i Microsoft Flight Simulator
— registreringsnummer, 3D-position med attityd, payload och bränsle — och senare
ladda tillbaka exakt samma tillstånd. Typiskt scenario: lämna ett parkerat plan
och återkomma dagen därpå med planet på samma plats, med samma last och bränsle.

## Beslut och avgränsningar

- **Gränssnitt:** Interaktiv konsoll-app med en meny-loop, inte engångskommandon.
- **Stack:** C# / .NET med officiella Managed SimConnect.
- **Målversion:** MSFS 2020 i första hand; 2024-kompatibelt där det går.
  Vi bygger mot 2020-simvars som fungerar i båda versionerna.
- **Registreringsnummer:** Används som nyckel/filnamn för det sparade tillståndet
  OCH skrivs tillbaka till simulatorn som ATC ID (best effort).
- **Position:** Full attityd (lat/lon/alt + heading + pitch + bank) men planet
  återställs stillastående på marken (hastighet = 0).
- **Processmodell:** Engångsprocess som lever så länge appen är öppen.
  SimConnect-anslutningen öppnas en gång vid start och återanvänds.

### Kända begränsningar

- SimConnect kan **inte** byta vilken flygplansmodell/livery som är laddad.
  Användaren måste välja rätt flygplan i MSFS innan laddning. Reg.nr är därför
  primärt en nyckel; modellbyte ligger utanför scope.
- ATC ID är inte garanterat skrivbart på alla flygplan (känd SimConnect-
  begränsning). Misslyckas det rapporteras det, men övrig laddning fortsätter.
- MSFS 2024 har ett nytt bränslesystem för vissa plan; de äldre
  `FUEL TANK …`-simvars stöds brett men kanske inte fullt ut på alla 2024-plan.

## Arkitektur

```
msfssave.exe (interaktiv konsoll)
  ├─ Menu           – ritar menyn, läser tangenttryck, driver loopen
  ├─ AircraftState  – datamodell (POCO) → JSON
  ├─ StateStore     – läser/skriver JSON-filer per registrering
  └─ ISimConnector  – gränssnitt mot simulatorn (öppnas vid start, hålls öppen)
        └─ SimConnector – konkret implementation ovanpå Managed SimConnect
```

`ISimConnector` isolerar alla sim-anrop så att `AircraftState` och `StateStore`
kan enhetstestas utan en körande simulator. SimConnect-anslutningen öppnas en
gång vid start och återanvänds för alla menyval, vilket gör spara/ladda snabbare
och låter menyns header visa live-status.

## Gränssnitt — meny-loop

Appen startar, ansluter till SimConnect och visar en meny i en loop tills ESC:

```
=== msfssave ===            ansluten: Cessna 172 Skyhawk / SE-ABC
  1. Spara position
  2. Ladda position
  3. Lista sparade
  4. Ta bort sparad
  ESC  Avsluta
> _
```

- **Header** visar anslutningsstatus + nuvarande flygplan/ATC ID. Är simulatorn
  inte igång står det "ej ansluten" och appen försöker återansluta.
- **1. Spara** – läser nuvarande tillstånd. Föreslår nuvarande ATC ID som namn;
  användaren bekräftar eller skriver eget. Finns filen redan → fråga om
  överskrivning.
- **2. Ladda** – visar en numrerad lista över sparade flygplan; användaren väljer
  ett, bekräftar, och tillståndet skrivs till simulatorn.
- **3. Lista** – visar reg, flygplanstyp, tidsstämpel.
- **4. Ta bort** – numrerad lista, välj, bekräfta.
- **ESC** – kopplar ner och avslutar.

## Datamodell (JSON)

En JSON-fil per registrering i `%APPDATA%\msfssave\`.

```jsonc
{
  "registration": "SE-ABC",      // ATC ID
  "title": "Cessna 172 Skyhawk", // TITLE-simvar, för matchningsvarning
  "savedAtUtc": "2026-06-30T18:30:00Z",
  "position": {
    "latitude": 59.65, "longitude": 17.92, "altitudeFeet": 137.0,
    "pitchDeg": 0.4, "bankDeg": -0.1, "headingTrueDeg": 210.0,
    "onGround": true
  },
  "fuelGallons": { "LeftMain": 26.5, "RightMain": 26.5, "Center": 0.0 },
  "payloadLbs":  [ { "index": 1, "name": "Pilot", "weight": 170 } ]
}
```

## Dataflöde mot SimConnect

### Spara (läsning)

En data-definition läser i ett svep:

- `PLANE LATITUDE`, `PLANE LONGITUDE`, `PLANE ALTITUDE`
- `PLANE PITCH DEGREES`, `PLANE BANK DEGREES`, `PLANE HEADING DEGREES TRUE`
- `SIM ON GROUND`
- `ATC ID`, `TITLE`
- alla `FUEL TANK … QUANTITY` (gallons)
- `PAYLOAD STATION COUNT` + `PAYLOAD STATION WEIGHT:n` + `PAYLOAD STATION NAME:n`

### Ladda (skrivning)

1. **Position/attityd atomiskt** via `SIMCONNECT_DATA_INITPOSITION` med
   `Airspeed = 0` och `OnGround = 1` → planet teleporteras och står stilla.
   Renare och stabilare än att sätta varje variabel för sig.
2. **Bränsle** – skriver tillbaka varje tankkvantitet, klampat mot tankens
   kapacitet.
3. **Payload** – skriver `PAYLOAD STATION WEIGHT:n` per station.
4. **ATC ID** – skrivs best effort.

## Felhantering

- Simulatorn inte igång / SimConnect svarar inte → tydligt felmeddelande,
  ingen krasch; headern visar "ej ansluten".
- Sparfil saknas vid laddning → tydligt fel + tips att lista sparade.
- `title` matchar inte laddat flygplan → varning (t.ex. "sparad som C172 men
  A320 laddad — position/bränsle kanske inte passar"). Laddar ändå; användaren
  bestämmer.
- ATC ID gick inte att sätta → rapporteras, men resten av laddningen fortsätter.

## Testning

- Enhetstester för `StateStore` (round-trip JSON) och mappning i `AircraftState`
  via en fejkad `ISimConnector` — körs utan simulator.
- SimConnect-lagret verifieras manuellt med en checklista i sim: spara på en
  flygplats, flyg iväg, ladda tillbaka, bekräfta att position, attityd, bränsle
  och payload stämmer.

## Framtida möjligheter (utanför scope nu)

- Bakgrundsdaemon + tunn klient om snabbtangenter i simulatorn önskas senare;
  SimConnect-lagret kan lyftas dit utan att röra resten.
