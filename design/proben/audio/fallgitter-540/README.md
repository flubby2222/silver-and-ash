# Hoerprobe Fallgitter der Arena (#540)

Neue Geraeusch-Kategorie, Probe ausserhalb des Spiels. Nichts unter `assets/`, kein Spielcode. Alles per Synthese-Skript (`bauen.py`, nur Python-Standardbibliothek), mono, 16 Bit, 44 100 Hz, Spitze -3 dBFS. Zeitbasis ist `zeitverlauf.csv` des Bewegungsentwurfs (`design/proben/animation/fallgitter-540/`, 60 Ticks je Sekunde). Die Datei beginnt im Tick 0 (Ausloeser); Tick t liegt auf Sample t x 735.

Weder die Leitung noch der Audiodesigner kann hoeren: Die Zahlen unten sind gemessen, ob etwas passt, entscheidet der Mensch.

## Dateien

Je Bewegung zwei Varianten, die sich im Charakter unterscheiden (nicht in der Zahl der Ereignisse). Zu jeder Datei gibt es ein Bild der Wellenform (`<name>.png`) mit den Ticks als senkrechten Linien (rot = Hauptereignis, orange = Nachfedern, grau = Hilfsmarke).

| Datei | Dauer | Was sie zeigt |
|---|---|---|
| `fallen-a-variante-1.wav` | 1,37 s | Ein Fall, Aufschlag in Tick 24. **Dumpf:** Eisen schleift rau im Schlitz, Kette dumpf, Aufschlag = tiefer Wumms mit kaum Klang, kurzer Nachhall (0,55 s) |
| `fallen-a-variante-2.wav` | 1,37 s | Derselbe Fall. **Eisern-klingend:** Schleifen als Ruckeln (Haften und Gleiten), Aufschlag mit tiefem Eisenklang (Teiltoene 170 bis 550 Hz), laengerer Nachhall (0,85 s) |
| `fallen-b-variante-1.wav` | 1,75 s | Zwei Rucke: Ruck 1 in Tick 12 (Kette faengt, Schlag plus Klirren), Schwingen bis 24, Ruck 2 in Tick 26 (Kette gibt nach), Aufschlag in Tick 47. Klangcharakter wie a1 |
| `fallen-b-variante-2.wav` | 1,75 s | Wie b1, Klangcharakter wie a2 |
| `heben-variante-1.wav` | 2,90 s | Gleichmaessiges Heben (Tick 6 bis 126): Winde mit **Sperrklinke** (dumpfes Klacken im Takt der Geschwindigkeit), Rumpeln, selten Kettenglieder. Einrasten in Tick 126 = dumpfer Schlag unter 120 Hz |
| `heben-variante-2.wav` | 2,90 s | Gleichmaessiges Heben: **Kette laeuft ueber die Trommel** (dichter, gleichmaessiger Gliederlauf), knarrende Reibung, Rumpeln, keine Klinke. Einrasten = dumpfer Schlag mit etwas Luft dahinter |

Die Dauern sind laenger als die Bewegung (a 0,67 s, b 1,05 s, Heben 2,1 s), weil Nachhall und Ausklang folgen: 0,7 s bei a und b, 0,8 s beim Heben, alles laeuft auf null aus.

## Aufbau (je Ereignis aus dem Zeitverlauf)

- **Kette:** einzelne Kettenglieder (zwei unharmonische Teiltoene 180 bis 700 Hz plus dumpfes Koernchen); Dichte und Pegel folgen der Fallgeschwindigkeit aus der CSV (Hoehe je Tick, daraus Geschwindigkeit). Beim Heben dagegen konstant.
- **Eisen im Fuehrungsschlitz:** gefiltertes Rauschen (90 bis 800 Hz) plus Rumpeln unter 150 Hz, Pegel = Geschwindigkeit. Bei b setzt es nach Ruck 1 fast aus (die Hoehe aendert sich zwischen Tick 12 und 26 kaum) und kommt mit Ruck 2 zurueck.
- **Aufschlag:** Schlag (Sinus 100 auf 44 Hz fallend), dumpfes Rauschen als Knall, bei Variante 2 zusaetzlich Eisenklang; Einsatz genau im Tick des Aufpralls (24 bei a, 47 bei b). Nachfedern kleiner in Tick 35 und 40 (a) bzw. 58 und 63 (b), leiser und etwas hoeher gestimmt. Alles geht durch einen kurzen Gruftnachhall (vier Kammfilter, 0,55 s bzw. 0,85 s).
- **Hoehen:** nach der Mischung Tiefpass 2,4 kHz, 24 dB je Oktave; Energie ueber 3 kHz liegt bei -40 bis -50 dB (siehe `messwerte.md`). Keine hellen Toene, kein "Ding" beim Einrasten.
- **Ereignisse nach `README` des Bewegungsentwurfs:** Ausloesen (0), Kette faengt (12, nur b), zweites Loesen (26, nur b), Aufschlag (24 / 47), Nachfedern (35 / 58), Kette laeuft (6 bis 126, Heben), Einrasten (126). Beim Heben zusaetzlich ein kurzes Straffen der Kette (Tick 0 bis 6, Knarren) vor dem Anlauf.

## Messwerte (Auszug, Volltabelle in `messwerte.md`)

| Datei | Dauer s | Spitze dBFS | Mittel dBFS | Letzte 50 ms dBFS |
|---|---|---|---|---|
| fallen-a-variante-1 | 1,37 | -3,0 | -23,0 | -91 |
| fallen-a-variante-2 | 1,37 | -3,0 | -22,4 | -90 |
| fallen-b-variante-1 | 1,75 | -3,0 | -22,1 | -91 |
| fallen-b-variante-2 | 1,75 | -3,0 | -21,5 | -90 |
| heben-variante-1 | 2,90 | -3,0 | -21,2 | -91 |
| heben-variante-2 | 2,90 | -3,0 | -18,8 | -84 |

Vergleich mit dem Bestand (gemessen): `p1-gorm-aufschlag` Spitze -2,5 / Mittel -23,1 dBFS, `p1-gorm-kette` -2,5 / -17,6, `p1-kiste-oeffnen` -7,0 / -28,6, `klinge-1` -4,9 / -23,7. Die Faelle liegen im Bereich des Bestands. **Heben ist ein Dauerklang und liegt im Mittel hoeher** (-21 bzw. -19), vor allem Variante 2; die Spitze ist der Riegel. Soll Heben leiser sein, ist das ein Faktor im Einbau.

**Einsatz auf dem Tick** (Volltabelle `messwerte.md`): Die lauten Ereignisse beginnen im Tick-Sample mit einer Abweichung von -12 bis +12 Samples (unter 0,3 ms): Aufschlag 24 (a1 -0,27 ms, a2 +0,09 ms), Aufschlag 47 (b1 -0,09, b2 +0,09), Ruck 1 in Tick 12 (+0,09 / -0,09), Ruck 2 in Tick 26 (b1 +0,27 ms; bei b2 meldet die Messung ein Kettenglied 4,3 ms *vor* dem Tick, der Schlag selbst sitzt im Tick), Riegel 126 (beide -0,09 ms). Die kleinen Nachfedern (35, 58) gehen im Nachhall des Aufschlags unter; die Messung trifft dort teils den Hall (Abweichung bis 14 ms), im Skript liegen sie auf dem Tick.

## Empfehlung

Aus den Messwerten allein ist keine Variante die "richtige"; mein Vorschlag fuer die Hoerreihenfolge und eine Tendenz:

- **Fallen:** Variante 1 zuerst (dumpfer, trockener, passt zur Leitlinie "trocken, nah, koerperlich"). Variante 2 hat mehr Eisenklang und laengeren Hall; sie ist die, bei der das Risiko "zu klingend" liegt (Teiltoene bis 550 Hz, kein heller Anteil). Wenn der Mensch Eisen hoeren will statt Stein, ist es 2.
- **Heben:** Variante 1 (Klinke) macht das Heben als regelmaessigen Takt lesbar und laeuft im Takt der Geschwindigkeit; Variante 2 (Gliederlauf) ist ruhiger. Bei Variante 1 besteht das Risiko "zu mechanisch/tickend", bei 2 "zu dicht". Der Riegel ist in beiden dumpf.
- **b vs. a:** Der Klang folgt der Bewegung. Bei b sind die zwei Rucke deutlich hoerbar (Ruck 1 ist der zweitlauteste Einsatz der Datei, Spitze lokal etwa -7,5 dBFS), die Datei ist 0,4 s laenger.

## Fragen an den Menschen

- Fallen: eher dumpf (1) oder eher klingendes Eisen (2)?
- Heben: Klinke (1) oder Kettenlauf (2)?
- Lauter oder leiser? Heben ist im Mittel hoeher als die Faelle.

## Nachbauen

```
python design/proben/audio/fallgitter-540/bauen.py
python design/proben/audio/fallgitter-540/messen.py
```

`bauen.py` liest `zeitverlauf.csv` aus demselben Ordner (Kopie aus dem Bewegungsentwurf), Seed fest; zwei Laeufe gaben dieselben SHA-256 (`wav.sha256`). `messen.py` braucht `ffmpeg` und schreibt `messwerte.md` und die PNG-Bilder.

## Fuer den Einbau (nur vorgemerkt, nichts davon gebaut)

Das Spiel kennt `RoomSealed` und `RoomCleared` je Raum; fuer einen Klang an der Tuer (gerichtet) braeuchte es ein Ereignis je Tuer mit Tuerort und Bewegungsart (z. B. `GrateStarted(door, falling)`), das ist eine Aufgabe fuer die Leitung und `Core`. Der Klang beginnt im Ausloeser-Tick; das Bild folgt bei a und b etwa 8 Ticks spaeter (Gitter erst ab etwa Tick 8 im Bild).
