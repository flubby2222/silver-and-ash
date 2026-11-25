# Fallgitter Runde 2 (#540): Eisen rutscht

Urteil des Menschen zu Runde 1 (woertlich): Fallen "Nacharbeiten. soll eher ein rutschendes Geraeusch sein"; Heben "Welcher Hebel? Benutzt doch keiner."

**Deutung (des Audiodesigners, ungehoert):** Das Gitter ist schweres Eisen, das im Steinschlitz gleitet. Keine Kette als Hauptklang, keine Winde, keine Sperrklinke, nichts, das jemand bedient. Das Gitter bewegt sich von selbst.

Nur noch Fallen **b** (zwei Rucke, der Mensch hat b gewaehlt) und Heben. Gebaut mit `bauen.py` (nutzt die Helfer aus `../bauen.py`, Zeitbasis `../zeitverlauf.csv`, Seed fest, zwei Laeufe gleiche SHA-256 in `wav.sha256`). Gemessen mit `messen.py`; gehoert hat niemand.

| Datei | Dauer | Spitze / Mittel dBFS | Was sie zeigt |
|---|---|---|---|
| `fallen-b-rutschen-variante-1.wav` | 1,75 s | -3,0 / -23,1 | Ganz ohne Kette. Eisen rutscht rau auf Stein, Tempo aus der CSV; Stocken in Tick 12 und 26; dumpfer Aufschlag in Tick 47, Nachfedern 58 und 63 |
| `fallen-b-rutschen-variante-2.wav` | 1,75 s | -3,0 / -23,1 | Wie 1, dazu ein kaum hoerbarer Kettenrest (Pegel etwa 0,01 bis 0,045 gegen Rutschen 0,55, nur waehrend das Eisen laeuft) |
| `heben-rutschen-variante-1.wav` | 2,90 s | -9,9 / -24,0 | Rutschen aufwaerts, **rauer** (mehr Koernung, Bett bis 700 Hz), gleichmaessig, leises dumpfes Anschlagen in Tick 126 |
| `heben-rutschen-variante-2.wav` | 2,90 s | -9,8 / -24,0 | **glatter** (wenig Koernung, Bett bis 480 Hz), sonst gleich |

Bilder (Wellenform mit Ticks) liegen neben den Dateien (`<name>.png`), Volltabelle in `messwerte.md`.

## Aufbau

- **Rutschen:** drei Schichten, alle vom Tempo aus der CSV gefuehrt: Gleitbett (Rauschen 100 bis 700 Hz, Pegel steigt mit dem Tempo), Koernung (kurze Rauschkoerner, Dichte 80 bis 480 je s und Tonlage 170 bis 520 Hz steigen mit dem Tempo) und Rumpeln unter 150 Hz. Danach Tiefpass 1,4 kHz und 2,4 kHz; Energie ueber 3 kHz bei -41 bis -43 dB.
- **Zwei Rucke bei b:** Das Rutschen haelt von selbst an, denn die Hoehe aendert sich zwischen Tick 12 und 26 kaum. Dazu ein kurzer dumpfer Stoss (Sinus 80 auf 52 Hz plus tiefes Rauschen) in Tick 12 (laut) und Tick 26 (leiser), kein Klirren.
- **Aufschlag:** die dumpfe Fassung aus Runde 1 (Variante 1 dort), Hall 0,55 s.
- **Heben:** gleichmaessig, Pegel nur vom Tempo; Anlauf bis Tick 18 steigt von selbst. Kein Takt, keine Klinke. In Tick 126 ein leiser dumpfer Anschlag (Sinus 80 auf 50 Hz, tiefes Rauschen). Pegel auf Mittel -24 dBFS gerechnet (Runde 1: -21 und -19).

## Messung

Alle Einsaetze liegen im Tick-Sample mit hoechstens 12 Samples (0,27 ms) Abweichung: Stocken 12 und 26, Aufschlag 47, Anschlag 126. Letzte 50 ms bei -91 dBFS. Das Anschlagen beim Heben hat Pegelspitzen bei -9,9 bzw. -9,8 dBFS und liegt in seinem 100-ms-Fenster etwa 5 dB unter dem Rutschen (-27 gegen -22): bewusst leise; falls der Mensch es nicht hoert, ist das der Hebel.

## Fragen an den Menschen

- Fallen: Stimmt das Rutschen? Ohne Kette (1) oder mit Kettenrest (2)?
- Heben: rauer (1) oder glatter (2)? Hoerbar genug das Anschlagen am Ende?

## Regelvorschlag fuer die Audio-Bible

*Aus dem Urteil zu Fallgitter Runde 1 (Deutung des Audiodesigners, bestaetigt erst Runde 2):*

**Tueren und Gitter: Material rutscht, keine bediente Mechanik.** Bewegte Bauteile der Arena (Fallgitter, Tueren, Tore) klingen nach dem, woraus sie sind (Eisen auf Stein: Gleiten, Reibung, dumpfer Aufprall), nicht nach einer Maschine dahinter (Kette als Hauptklang, Winde, Sperrklinke, Takt). Niemand bedient sie. Tempo, Dichte und Tonlage des Rutschens folgen der Geschwindigkeit der Bewegung; Stocken ist ein Anhalten des Rutschens mit einem dumpfen Stoss. Ketten hoechstens als kaum hoerbarer Rest im Hintergrund. Einrasten ist ein leises dumpfes Anschlagen, kein Klacken im Takt.
