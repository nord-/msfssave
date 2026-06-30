# msfssave — konsoll-GUI samt Tanka & Lasta

**Datum:** 2026-06-30
**Status:** Designförslag (komplement till `2026-06-30-msfssave-aircraft-state-design.md`)
**För:** Kjell

## Syfte med detta dokument

Bygger vidare på den godkända spara/ladda-designen. Två saker tillkommer:

1. Ett konkret förslag på **konsoll-GUI** för meny-loopen.
2. Två nya funktioner: **Tanka** (sätt bränslenivå) och **Lasta** (sätt payload —
   PAX och bagage), med en gemensam **vikt & balans-panel**.

Tanka/Lasta ändrar appens karaktär något: hittills *fångar* den ett tillstånd,
nu *editerar* den också tillståndet live i den körande simulatorn.

## Val av GUI-bibliotek: Spectre.Console

Passar loop-modellen direkt. Ger numrerade val, textprompts med default-värde,
ja/nej-bekräftelser, tabeller och färgad markup utan ett helt fönstersystem.

- `Console.WriteLine` blir snabbt rörigt med överskrivningsfrågor och tabeller.
- **Terminal.Gui** (ncurses-aktig TUI) är overkill för detta — rätt val först om
  vi senare vill ha en *alltid* live-uppdaterande header medan appen står stilla
  vid menyn (se "Header-uppdatering" nedan).

**Hybrid:** behåll tangentmodellen (1–6 + ESC) genom att läsa
`Console.ReadKey(intercept: true)` själv i loopen, och använd Spectre enbart för
rendering och underprompter.

## Meny

```
┌─ msfssave ───────────────────────────────────────────────┐
│ ● ANSLUTEN   Cessna 172 Skyhawk   ATC ID: SE-ABC          │
└───────────────────────────────────────────────────────────┘

   1  Spara position        4  Ta bort sparad
   2  Ladda position        5  Tanka
   3  Lista sparade         6  Lasta (PAX & bagage)

   ESC Avsluta

   ›
```

Ej ansluten (grön ● → grå ○, amber text):

```
┌─ msfssave ───────────────────────────────────────────────┐
│ ○ EJ ANSLUTEN   väntar på MSFS…   [R] försök igen         │
└───────────────────────────────────────────────────────────┘
```

## Skärmar — spara/ladda/lista/ta bort

### Spara

Läs tillstånd, visa sammanfattning som panel, föreslå ATC ID som filnamn:

```
── Spara position ──────────────────────────────────────────
  Flygplan   Cessna 172 Skyhawk
  Position   59.6500 N  17.9200 E   137 ft   hdg 210°T
  Attityd    pitch 0.4°   bank −0.1°   på marken
  Bränsle    53.0 gal   (L 26.5 / R 26.5 / C 0.0)
  Payload    1 station, 170 lbs

  Namn på sparfil [SE-ABC]: ▏

  ⚠ SE-ABC.json finns redan. Skriv över? [j/N]: ▏
  ✓ Sparat → %APPDATA%\msfssave\SE-ABC.json
```

### Ladda

Tabell, välj nummer, bekräfta. Visa skrivstegen var för sig så att
best-effort-ATC-ID syns tydligt:

```
── Ladda position ──────────────────────────────────────────
  #   Reg      Flygplan              Sparad
  1   SE-ABC   Cessna 172 Skyhawk    2026-06-30 18:30
  2   SE-MAF   Piper PA-28-181       2026-06-29 09:12
  3   D-EXYZ   Extra 330             2026-06-25 14:40

  Välj (1–3, ESC avbryt): 1

  ⚠ Laddat plan är "Piper PA-28" men sparfilen är "Cessna 172"
    — position/bränsle kanske inte passar. Fortsätt? [j/N]: j

  ✓ Position & attityd  (INITPOSITION, airspeed 0, on ground)
  ✓ Bränsle             53.0 gal
  ✓ Payload             1 station
  ✗ ATC ID              kunde inte sättas (stöds ej av detta plan)

  Klart.
```

### Lista / Ta bort

Delar samma tabell. Borttagning lägger till en bekräftelse:

```
── Ta bort sparad ──────────────────────────────────────────
  (samma tabell som Ladda)
  Välj att ta bort (1–3, ESC avbryt): 2
  Ta bort SE-MAF (Piper PA-28-181)? [j/N]: ▏
  ✓ Borttagen
```

## 5. Tanka

Arbetar **live mot simulatorn**: läs nuvarande tankkvantiteter → editera →
skriv tillbaka. Klampa varje tank mot kapacitet (samma logik som vid laddning).

```
── Tanka ────────────────────────────────────────────────────
  Tank          Nu              Kapacitet
  Left Main     26.5 gal  100 l      26.5 gal   ████████ 100%
  Right Main    26.5 gal  100 l      26.5 gal   ████████ 100%
  Center         0.0 gal    0 l      19.0 gal   ░░░░░░░░   0%
  ─────────────────────────────────────────────
  Totalt        53.0 gal  201 l    →  318 lbs   144 kg

  [F] Fyll alla till %   [T] Sätt total   [E] Editera per tank
  [M] Min (0)            [Enter] Skriv till sim    [ESC] Avbryt
```

### Designval

- **Enhet:** defaulta liter/kg i visningen (metriskt, i linje med PA-28-
  kalkylatorerna), men lagra och skriv simens native gallons/lbs. `[U]` växlar
  l↔gal. Notera att liter är ren volym; vikten beror på bränsletyp.
- **Vikt per gallon:** läs från simen i stället för att anta 6.0 (avgas) /
  6.7 (Jet-A) — den är planberoende. **Se reservation nedan.**
- **"Sätt total":** distribuera proportionellt mot kapacitet (enklast, håller
  mains symmetriska). Per-tank-editering finns kvar för medveten obalans.

## 6. Lasta (PAX & bagage)

Arbetar **live mot simulatorn**: läs `PAYLOAD STATION NAME/WEIGHT:n` per index →
editera → skriv `PAYLOAD STATION WEIGHT:n` tillbaka.

```
── Lasta (PAX & bagage) ──────────────────────────────────────
  #  Station            Vikt
  1  Pilot              170 lb   77 kg
  2  Co-pilot             0 lb    0 kg
  3  Passenger rear     340 lb  154 kg   (2 × 170)
  4  Baggage             40 lb   18 kg
  ──────────────────────────────────────
     Payload            550 lb  249 kg

  [Nr] Editera station   [P] Lägg PAX-preset   [Enter] Skriv
  [U] Växla lb/kg        [ESC] Avbryt
```

### Designval

- **Stationstyp är en gissning.** Simen ger bara namn + vikt per index, inte
  "sittplats eller lastrum". Heuristik på namnet: innehåller
  "pilot"/"passenger"/"pax" → PAX-preset; "baggage"/"cargo" → fri vikt.
  Bra nog, men ska flaggas i UI som en gissning.
- **PAX-preset:** standardvikt (t.ex. 77 kg vuxen, eller EASA standardmassor om
  vi vill vara formella) gånger antal i raden. Bagage skrivs som fri vikt.

## Gemensam vikt & balans-panel

Visas nederst i både Tanka och Lasta — det är det som gör skärmarna
meningsfulla:

```
── Vikt & balans ─────────────────────────────────────────────
  Tomvikt     1480 lb        Bränsle     318 lb
  Payload      550 lb        Brutto     2348 lb / max 2550 lb
                                         ████████░░  92%   OK
  CG          18.4 % MAC
```

- `TOTAL WEIGHT`, `EMPTY WEIGHT`, `MAX GROSS WEIGHT` är läsbara simvars → ger
  brutto-vs-max direkt, och vi kan varna vid överlast.
- **CG:** `CG PERCENT` (% MAC) går att läsa för visning. Men simen ger **ingen
  CG-envelope** (fram/bak-gränser) — den bor i `flight_model.cfg`, inte i en
  simvar. Slutsats för v1: **brutto kan valideras, CG-läge kan bara visas**, inte
  valideras mot envelope, om vi inte själva matar in gränser per flygplanstyp.

## Loop-skelett

```csharp
while (true)
{
    AnsiConsole.Clear();
    RenderHeader(sim.Status);          // ● ANSLUTEN … / ○ EJ ANSLUTEN
    RenderMenu();
    var key = Console.ReadKey(intercept: true).Key;
    switch (key)
    {
        case ConsoleKey.D1: SaveFlow();   break;
        case ConsoleKey.D2: LoadFlow();   break;
        case ConsoleKey.D3: ListFlow();   break;
        case ConsoleKey.D4: DeleteFlow(); break;
        case ConsoleKey.D5: FuelFlow();   break;   // Tanka
        case ConsoleKey.D6: PayloadFlow();break;   // Lasta
        case ConsoleKey.R:  sim.TryReconnect(); break;
        case ConsoleKey.Escape: return;
    }
}
```

Färgsättning: grön = ansluten, amber/gul = väntar, röd = fel — i linje med
amber/IBM-Plex-estetiken. Spectre-`Markup` räcker; ingen egen ANSI-hantering.

## Header-uppdatering

Headern ritas om vid varje loop-varv (när en tangent trycks), inte sekund för
sekund medan man sitter still vid menyn. Det matchar "öppnas en gång, hålls
öppen" och kostar nästan inget. Live-uppdatering medan man står kvar i menyn
kräver antingen bakgrundspoll + `Live`-region (krockar lätt med Spectres
prompts) eller Terminal.Gui med timer. **Rekommendation: inte live i v1** —
`[R] försök igen` täcker behovet.

## Reservationer att verifiera mot SimConnect SDK

Dessa är inte bekräftade — bör dubbelkollas innan vi litar på siffrorna:

- `PAYLOAD STATION WEIGHT:n` skrivbart — stämmer med spara/ladda-designen,
  men verifiera units (lbs).
- `FUEL WEIGHT PER GALLON` — osäkert om den exponeras läsbart/skrivbart på alla
  plan, och vilka units.
- `CG PERCENT` — verifiera units (% MAC) och att den är stabil att läsa.

## Öppen fråga som avgör datamodellen

Ska Tanka/Lasta **bara** jobba live mot ett laddat plan, eller ska de också
kunna editera värden i en **sparad JSON utan körande sim** (offline)?

- **Live (rekommenderat för v1):** en kodväg, enklast. Man tankar/lastar det
  faktiska planet och `Spara` fångar resultatet efteråt.
- **Offline-editering:** naturlig framtida variant, men inför en andra kodväg.
