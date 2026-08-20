# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Språk

Kod, kommentarer, användartexter och commit-meddelanden i det här repot är på **svenska**. Följ det.

## Kommandon

```bash
dotnet build                              # hela solution (kräver MSFS SDK, se nedan)
dotnet build MsfsSave.Core                # Core enbart — fungerar utan SDK
dotnet test                               # alla Core-tester
dotnet test --filter StateStoreTests      # en testklass
dotnet test --filter "FullyQualifiedName~ClampToCapacity"   # ett enskilt test
dotnet run --project MsfsSave             # kräver att MSFS är igång med ett plan laddat
dotnet publish MsfsSave -c Release
```

`MsfsSave.csproj` refererar `Microsoft.FlightSimulator.SimConnect.dll` via miljövariabeln
`MSFS_SDK` (`$(MSFS_SDK)\SimConnect SDK\lib\managed\`). Saknas den variabeln bryts bygget av
konsollprojektet — Core och testerna bygger ändå. Den nativa `SimConnect.dll` kopieras till
output-katalogen och måste ligga bredvid exe:n vid körning.

## Arkitektur

Tre projekt med en avsiktlig gräns mellan testbart och sim-beroende:

- **`MsfsSave.Core`** (net10.0, ingen SimConnect-referens) — `AircraftState`-modellen,
  `StateStore` (JSON per registrering i `%APPDATA%\msfssave\`), rena hjälpare (`FuelMath`,
  `StateComparison`), `AppService` som orkestrerar Save/Load/List/Delete, och
  `ISimConnector`-gränssnittet. Allt här ska kunna enhetstestas utan simulator.
- **`MsfsSave`** (net10.0-windows, x64) — `SimConnector` (enda platsen som rör SimConnect),
  `Menu` (all Console-I/O) och `Program` (wiring).
- **`MsfsSave.Core.Tests`** (xUnit) — testar Core mot `FakeSimConnector`.

**Lägg aldrig SimConnect-beroenden i Core, och aldrig affärslogik i `Menu`.** Ny logik som är
värd att testa hör hemma i Core bakom `ISimConnector`.

`SimConnector`, `Menu` och `Program` kan inte enhetstestas — de verifieras manuellt mot en
körande MSFS enligt checklistan i Task 10 i planen (se `docs/superpowers/plans/`).

## SimConnect-invarianter

Dessa är lätta att bryta och ger tyst korrupt data snarare än kompileringsfel:

- **Fältordningen i varje marshaling-struct måste exakt matcha ordningen på
  `AddToDataDefinition`-anropen** för samma `DEFINITIONS`-värde. Lägger du till en simvar måste
  structen ändras på samma position.
- **Payload läses/skrivs station för station**: antalet stationer är okänt vid kompilering, så
  `DEFINITIONS.PayloadStation` rensas (`ClearDataDefinition`) och registreras om per index.
  Skrivning är best-effort per station — en station som saknas i det laddade planet hoppas över.
- **Konsollen har ingen fönsterpump.** `SimConnector` använder ett `EventWaitHandle` och pumpar
  själv i `PumpUntilReceived`, som är kvalificerad på `REQUESTS`-id så att svar på fel förfrågan
  inte råkar avsluta väntan. Timeout är 5 s per avläsning.
- Återställning av position sker atomiskt via `SIMCONNECT_DATA_INITPOSITION` med `Airspeed = 0`.
- ATC ID kan inte skrivas på alla plan; `TrySetAtcId` är best-effort och rapporteras via
  `RestoreReport.AtcIdSet` utan att avbryta resten av laddningen.

## Bränsleskrivning

`RestoreFuel` (`MsfsSave/SimConnector.cs`) gör tre saker i ordning: läser om `DEFINITIONS.Fuel`
för att få **kapaciteterna från det aktuellt laddade planet** (aldrig från sparfilen), klampar
varje tank med `FuelMath.ClampToCapacity`, och skriver alla 11 tankar atomiskt i ett enda
`SetDataOnSimObject` i gallons.

- **Läsning och skrivning har skilda data-definitioner.** `DEFINITIONS.Fuel` (11 kvantiteter +
  11 kapaciteter) enbart för läsning, `DEFINITIONS.FuelWrite` (bara kvantiteter) för skrivning.
  Kapaciteterna är skrivskyddade — att skicka tillbaka dem orsakade problem (commit `e20b357`).
  Slå inte ihop definitionerna igen.
- **Tankar som saknas i sparfilen skrivs som 0.** `Capture` sparar bara tankar med `qty > 0`, så
  en tank som var tom vid sparningen nollställs aktivt vid laddning — avsiktligt. Tankar planet
  saknar helt har kapacitet 0 och klampas därmed också till 0.
- Till skillnad från payload finns ingen `try/catch` per tank; fel från simulatorn dyker upp
  asynkront på stderr via `OnRecvException`.

## Domänmodell

`AircraftState` har två separata namn-fält som inte får slås ihop:

- **`SlotName`** — fritt textnamn valt av användaren i F2-prompten (t.ex. en plats som
  "Höganäs"). Används enbart som filnyckel i `StateStore` (saniterat) och som etikett i listan.
  Skrivs aldrig till simulatorn.
- **`AtcId`** — flygplanets faktiska registrering, avläst från simulatorn av `Capture()` vid
  sparning. Detta är värdet `TrySetAtcId` skriver tillbaka vid laddning, oavsett vad
  sparplatsen heter.

Sparfiler från före uppdelningen har ett enda `Registration`-fält. `StateStore.Read` migrerar dem
vid inläsning: `Registration` fyller både `SlotName` och `AtcId`, och saknas även det används
filnamnet som `SlotName`. Utan det får posten tomt `SlotName` och blir omöjlig att ladda, skriva
över eller ta bort, eftersom filnyckeln utgår från `SlotName`. Migreringen sker bara i minnet —
filen skrivs om först nästa gång användaren sparar över den.

Verktyget kan **inte** byta laddad flygplansmodell — vid laddning jämförs sparad `Title` mot
faktiskt laddad titel och användaren varnas vid mismatch, men laddningen fortsätter.

## Dokumentation

`docs/superpowers/specs/` innehåller designdokument, `docs/superpowers/plans/` implementations-
planen med kryssrutor. Notera status i sidhuvudet: `...aircraft-state-design.md` är **godkänd**,
`...gui-tanka-lasta-design.md` (Spectre.Console-GUI plus Tanka/Lasta) är ett **designförslag som
inte är godkänt** — bygg inte på det utan att fråga Rickard först.
