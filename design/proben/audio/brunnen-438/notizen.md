# Hörprobe Brunnen "Well of Sirius" (#438)

Probestücke außerhalb des Spiels. Weder Leitung noch Audiodesigner können hören: Alles unten ist Aufbau und Messung, kein Klangurteil. Das Urteil gibt der Mensch.

## Ereignisse in Core

Heute gibt es nur `WellOfSiriusUsed(PlayerId)`: Der Brunnen wird benutzt und ist danach verbraucht (`IsSpent`). Preis, Chancen und "Glück oder nicht" sind noch nicht entschieden (Balance.cs, `WellBalance`). Die Probe nimmt deshalb drei Klangfälle an, die das Spiel später unterscheiden müsste:

1. **Wurf**: Silber fällt ins Wasser (bei jeder Benutzung, `WellOfSiriusUsed`).
2. **Glück**: Der Brunnen gibt einen Gegenstand. Dafür fehlt in Core ein eigenes Ereignis; wahrscheinlich reicht das vorhandene `EquipmentDropped`, wenn es vom Brunnen kommt, sonst braucht es etwa `WellGaveItem`. Das meldet die Leitung bei Bedarf als Issue, ich ändere Core nicht.
3. **Wasser in der Nähe**: Dauerschleife, vom Spiel nach Abstand zum Brunnen leiser oder lauter gestellt; solange er bereit ist (und je nach Entscheidung auch danach, "trocken" wäre dann Stille).

**Kein Glück:** Vorschlag: kein eigener Laut. Der Wurf (1) sagt schon "Silber ist weg"; bleibt danach das Glück-Geräusch (2) aus, ist das die Antwort. Ein eigener Niete-Laut würde den Verlust betonen und den Wurf doppeln. Wer es später doch will, nimmt die Wurf-Fassung C (sinkt in die Tiefe) für die Niete und A oder B für den Gewinn.

## Rahmen

Dunkel, keine hohen Töne: Grundtöne 90 bis 880 Hz, Tiefpässe bei 0,9 bis 2,4 kHz, gemessen höchstens -25 dB Anteil über 3 kHz (meist -31 bis -42). Mono, 16 Bit, 44 100 Hz. Einzelklänge Spitze -6 dBFS (zwischen Paket 1 mit -7 bis -8 und Skillpunkt/Teleporter mit -3), Schleifen Spitze -18 dBFS. Jeder Einzelklang läuft über Kosinus auf null aus (Regel 7): Spitze der letzten 50 ms unter -90 dBFS, Soll -50. Alle Tabellen mit Spektrogrammen: [messwerte.md](messwerte.md).

## (1) Silber fällt ins Wasser

| Variante | Datei | Länge | Aufbau |
|---|---|---|---|
| A Münze plumpst | `wurf-a-muenze-plumpst.wav` | 0,80 s | Kurzes dumpfes Metall-Tick (430 Hz mit Teiltönen 2,13 und 3,4, Tiefpass 1,8 kHz), 40 ms danach eine tiefe Wasserblase (150 Hz steigend). Kurzer Hall. |
| B Platsch | `wurf-b-platsch.wav` | 1,00 s | Nur Wasser: Rauschband um 520 Hz als weicher Schlag, darunter tiefer Schlag (90 auf 55 Hz). Kein Metall. Etwas längerer Hall. |
| C Sinken | `wurf-c-sinken.wav` | 1,40 s | Metallton, der unter Wasser sinkt: Tonhöhe fällt 640 auf 300 Hz, Tiefpass schließt sich von 1,2 kHz auf 300 Hz, am Ende eine kleine Blase. Das längste und leiseste Stück. |

Unterschied: A hat Metall und ist kurz (erkennt man als "Münze"), B ist reines Wasser, C erzählt das Sinken.

## (2) Der Brunnen gibt einen Gegenstand

| Variante | Datei | Länge | Aufbau |
|---|---|---|---|
| A Funkeln steigt | `glueck-a-funkeln-steigt.wav` | 2,70 s | 34 leise Sinus-Körnchen (Moll-Pentatonik 294 bis 880 Hz), Tonhöhe und Dichte steigen; darunter ein warmer Ton (147 Hz), der anschwillt. Das "goldene Funkeln" aus dem Bild. |
| B Glocke aus der Tiefe | `glueck-b-glocke-aus-der-tiefe.wav` | 3,02 s | Drei tiefe aufsteigende Blasen, im selben Atemzug schlägt eine dunkle Glocke an (196 Hz, Teiltöne 1 : 2,76 : 5,4) und klingt lang aus. |
| C Aufquellen | `glueck-c-aufquellen.wav` | 2,60 s | Blasen werden schneller und steigen in der Tonhöhe, dann schwillt ein weicher tiefer Ton (110 auf 165 Hz) an. Kein Metall, kein Glimmen. |

Unterschied: A hell und glitzernd (Körnchen), B Glocke (nahe am Klang des Skillpunkts), C nur Wasser und Tiefe. Die Längen liegen über den Meldeklängen (Skillpunkt unter 1 s), weil der Gewinn selten ist und lang ausklingen darf; der Hauptteil steht in den ersten 1,0 bis 1,5 s. Kürzen ist bei Bedarf leicht.

## (3) Wasser in der Nähe (Schleifen, 12 s)

| Variante | Datei | Aufbau |
|---|---|---|
| A Rieseln | `wasser-a-rieseln.wav` | Stetiges dunkles Rauschen (150 bis 900 Hz), Pegel atmet langsam (Perioden je Schleife ganzzahlig). Keine Einzelereignisse, daher keine erkennbare Wiederholung. |
| B Tropfen im Becken | `wasser-b-tropfen-im-becken.wav` | Sehr leises Rauschbett unter 400 Hz und 15 tiefe Tropfen (150 bis 420 Hz), Ausklang läuft über die Naht. Wiederholt sich hörbar alle 12 s. `-ohne-tropfen`: das Bett allein (für die Messung nach Regel 6). |
| C Gurgeln | `wasser-c-gurgeln.wav` | Etwa 28 kurze Blasen je Sekunde (90 bis 320 Hz) über leisem Bett; keine einzelne sticht heraus. |

Schleifen: kreisförmig gefiltert und Ereignisse über die Naht herumgewickelt, Sprung an der Naht 0,00003 bis 0,0007 gegen größten Nachbarsprung 0,005 bis 0,015 (Tabelle). Regel 6 (Mitte 300 bis 3000 Hz mindestens 25 dB unter Gesamtpegel) **erfüllt keine der Schleifen** (Mitte nur -1 bis -5 dB): Das ist bei Wasser in der Nähe gewollt, es ist ein Ort-Geräusch und keine Ambiente-Fläche. Es liegt aber in der Mitte, wo Ankündigungen hörbar bleiben müssen; beim Einbau deshalb nur im Nahbereich und leise (Spitze hier -18 dBFS, Mittel -32 bis -39 dB).

## Messwerte in Kürze

- Spitze Einzelklänge -6,0 dBFS, Schleifen -18,0 dBFS.
- Mittel Einzelklänge -18,9 bis -24,6 dB; am lautesten wirkt `glueck-a` (-18,9), am leisesten `wurf-b` (-24,6). Zum Vergleich Skillpunkt -23,0, Teleporter -11 bis -20.
- Länge Einzelklänge 0,8 bis 3,0 s, Ausklang bis -40 dB 0,4 bis 2,2 s.
- Einsätze (Regel 2, 30-ms-Fenster): `wurf-a`, `wurf-b` und `glueck-b` zählen 0, weil ihr Anschlag langsamer als 8 dB in 50 ms steigt; `wurf-c`, `glueck-a` genau 1; `glueck-c` zählt 2 bis 3 (Blasen werden schneller, gewollt: das ist das Anschwellen selbst, kein Nachschlag).
- Ende der Einzelklänge: alle unter -90 dBFS.

## Reproduzierbarkeit

`python bauen.py` (nur Standardbibliothek, fester Seed je Stück) erzeugt alle zehn Dateien in etwa 15 s. Ein zweiter Lauf in einen anderen Ordner lieferte zehn bitgleiche Dateien (`diff` der Prüfsummen leer); die Summen stehen in `wav.sha256`. `python messen.py` erzeugt `messwerte.md` und die Spektrogramme (braucht `ffmpeg`).

## Empfehlung (Vermutung, ungehört)

- **Wurf: A.** Man erkennt Silber (Metall-Tick) und Wasser in einer kurzen Geste; B ist reines Wasser ohne Hinweis auf Silber, C ist am längsten und hat den höchsten Höhenanteil der Würfe (-25 dB).
- **Glück: A**, weil es das goldene Funkeln der Beute im Bild aufnimmt und als einziges "Gewinn" von "Wurf" abhebt, ohne wie der Skillpunkt-Schlag (B) zu klingen. B ist die Alternative, wenn der Mensch eine Glocke als Belohnungsfamilie will.
- **Wasser: A**, weil es keine Wiederholung hörbar macht; C ist lebendiger, B hat die Wiederholung alle 12 s.
- **Niete:** kein eigener Laut (siehe oben).

Fragen an den Menschen: Wurf, welche Variante? Glück, welche? Wasser, welche, und soll es nach dem Verbrauch weiterlaufen? Was stört?
