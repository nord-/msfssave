# msfssave

Spara och ladda ett flygplans tillstånd i Microsoft Flight Simulator — registrering,
3D-position med attityd, bränsle och payload — och ladda tillbaka exakt samma tillstånd
senare. Tänkt för scenariot: lämna ett parkerat plan och återkom dagen därpå med planet
på samma plats, med samma last och bränsle.

Ett litet interaktivt konsollverktyg ovanpå SimConnect.

## Funktioner

- **Spara position** — läser nuvarande tillstånd från simulatorn och sparar det som JSON.
- **Ladda position** — teleporterar planet tillbaka till sparad position/attityd (stillastående
  på marken) och återställer bränsle och payload.
- **Lista / Ta bort** sparade tillstånd.

Varje sparat flygplan identifieras av sitt registreringsnummer (ATC ID), som även skrivs
tillbaka till simulatorn på de plan som stöder det.

## Krav

- Microsoft Flight Simulator 2020 (bör även fungera mot 2024 — bygger på 2020-simvars).
- .NET 10 SDK.
- MSFS SimConnect SDK installerat. Projektet refererar
  `Microsoft.FlightSimulator.SimConnect.dll` via miljövariabeln `MSFS_SDK`
  (`%MSFS_SDK%\SimConnect SDK\lib\managed\`).

## Bygga och köra

```bash
dotnet build
dotnet test                       # enhetstester för Core-logiken
dotnet run --project MsfsSave     # starta verktyget (kräver att MSFS är igång)
```

Eller publicera en fristående exe:

```bash
dotnet publish MsfsSave -c Release
```

Starta MSFS med ett flygplan laddat **innan** du kör verktyget. Den nativa `SimConnect.dll`
kopieras automatiskt bredvid exe-filen vid bygge/publicering.

## Användning

Verktyget visar en meny i en loop:

```
=== msfssave ===            ansluten: Cessna 172 Skyhawk / SE-ABC
  1. Spara position
  2. Ladda position
  3. Lista sparade
  4. Ta bort sparad
  ESC  Avsluta
```

Sparfiler lagras som en JSON-fil per registrering i `%APPDATA%\msfssave\`.

## Vad som sparas

- **Position & attityd** — latitud, longitud, höjd, pitch, bank, heading (sätts stillastående
  på marken via `SIMCONNECT_DATA_INITPOSITION`).
- **Bränsle** — kvantitet per tank (klampas mot tankens kapacitet vid laddning).
- **Payload** — vikt per station.
- **Registrering (ATC ID)** och flygplanstitel.

## Begränsningar

- Verktyget kan inte byta vilken flygplansmodell/livery som är laddad — du måste välja rätt
  flygplan i MSFS innan du laddar. Registreringen är en nyckel/etikett, inte ett modellbyte.
- ATC ID går inte att skriva på alla plan (SimConnect-begränsning); det görs best-effort och
  påverkar inte resten av laddningen.

## Arkitektur

- `MsfsSave.Core` — datamodell, JSON-persistens, ren logik och `ISimConnector`-gränssnittet.
  Helt enhetstestbart utan simulator.
- `MsfsSave` — konsoll-appen: `SimConnector` (Managed SimConnect), meny-loop och programstart.
- `MsfsSave.Core.Tests` — xUnit-tester för Core.
