# Hörprobe Hunter's Axe (Pfahlaxt), Runde 2: Aufladen (Issue #201)

Runde 1 hat der Mensch so entschieden: Hieb C (Luftdruck), Treffer Fleisch C (nass) und Treffer hart B (Stiel prallt) sind gewählt. Zum Aufladen sagte er: „Das Glöckchen („Ding“) am Ende des Aufladens stört.“ Das Einrasten am Ende (Eisenschlag, bei B ein Glockenschlag) wurde also als helles „Ding“ gehört und ist abgelehnt.

In Runde 2 gibt es nur das **Aufladen**, in drei Fassungen, und **keine Fassung endet mit einem tonalen, glockigen oder metallischen Ereignis**. Jede Fassung ist eine eigene WAV. Alles per Skript (`bauen.py`, Python-Standardbibliothek, fester Seed) synthetisiert; **alles gemessen, nichts gehört**. Mono, 16 Bit, 44 100 Hz, alle Töne unter 400 Hz, Tiefpass 2,8 kHz.

Frage an den Menschen: **Welche Fassung des Aufladens? Und stört am Ende noch etwas?** Dazu je Fassung ein Zusammenhang (Aufladen, 0,35 s Pause, Hieb C, Treffer Fleisch C), damit man das Aufladen im Ablauf hört.

## Aufladen

Der Aufbau davor stammt aus Runde 1 (Knarzen, Brummen, Ratsche); nur der Schluss ist neu.

| Datei | Beschreibung | Länge s | Spitze dBFS | Mittel dB |
|---|---|---|---|---|
| `aufladen-a-spannung-steht.wav` | Aufbau aus Runde 1 **A (Knarzen)**: Holz und Leder knarzen unter Zug, dazu Brummen. Kein Schlussereignis: die Spannung wächst bis 1,0 s, bleibt dann stehen und klingt in 0,45 s weich auf null aus. | 1,55 | -15,5 | -29,1 |
| `aufladen-b-faustschlag.wav` | Aufbau aus Runde 1 **B (Brummen)**: ein glatter Dröhnton, der steigt und lauter wird. Schluss: ein dumpfer, tonloser Schlag wie eine Faust auf Holz (Rauschen, tiefpassgefiltert bei 110 Hz, kein Ring, kein Nachklang). | 1,31 | -8,8 | -29,1 |
| `aufladen-c-luftstoss.wav` | Aufbau aus Runde 1 **C (Ratsche)**: dumpfe Holzschläge, die schneller und höher werden. Schluss: ein kurzer Luftstoß mit Lederzug (Rauschen unter 330 Hz), kein Ton. | 1,55 | -12,0 | -29,1 |

Warum diese Aufbauten: A (Knarzen) hat die gleichmäßigste Spannung und trägt ein Ausklingen ohne Ereignis; B (Brummen) war in Runde 1 der Ton ohne Rauschen und verträgt einen tiefen Schlag als Gegenstück, ohne dass Metall nötig wird; C (Ratsche) hat schon einen Puls, der von einem Luftstoß aufgelöst wird. Das ist die Wahl des Audiodesigners, nicht ein Klangurteil.

Alle drei liegen im Mittel bei -29,1 dB wie die Aufladen-Fassungen aus Runde 1; die Spitzen ergeben sich daraus (bei B steht der Schlag oben, bei A das Knarzen).

## Zusammenhang

Aufladen, 0,35 s Pause, Hieb C (`hieb-c-luftdruck.wav` aus Runde 1), und auf dem Gipfel des Hiebs (0,45 s nach Hiebbeginn) Treffer Fleisch C (`treffer-fleisch-c-nass.wav`). Spitze auf höchstens -1,5 dBFS begrenzt.

| Datei | Enthält | Länge s | Spitze dBFS | Mittel dB |
|---|---|---|---|---|
| `zusammenhang-a.wav` | Aufladen A (Spannung steht), Hieb C, Treffer Fleisch C | 2,91 | -3,9 | -24,7 |
| `zusammenhang-b.wav` | Aufladen B (Faustschlag), Hieb C, Treffer Fleisch C | 2,67 | -3,9 | -24,5 |
| `zusammenhang-c.wav` | Aufladen C (Luftstoß), Hieb C, Treffer Fleisch C | 2,91 | -3,9 | -24,7 |

## Messung des Schlusses: kein Teilton über 400 Hz

Gemessen wird das Spektrum der **letzten 300 ms** jeder Aufladen-Datei und zusätzlich das Fenster **1,0 bis 1,3 s** (ab Ende des Zugs, dort liegt bei B und C das Schlussereignis; bei C sind die letzten 300 ms schon fast Stille). Hervortreten = stärkste lokale Spitze über 400 Hz minus der mittlere Pegel ihrer Umgebung (Abstand 40 bis 150 Hz). Rauschen liegt dabei bei etwa 10 bis 15 dB, ein stehender Ton deutlich darüber.

| Datei | Hervortreten, letzte 300 ms | Hervortreten, 1,0 bis 1,3 s | Energie über 400 Hz (1,0 bis 1,3 s) |
|---|---|---|---|
| `aufladen-a-spannung-steht.wav` | 12,0 dB | 11,0 dB | -13,1 dB |
| `aufladen-b-faustschlag.wav` | 16,3 dB | 15,1 dB | -38,2 dB |
| `aufladen-c-luftstoss.wav` | 11,4 dB | 14,9 dB | -14,8 dB |
| (Runde 1) `aufladen-a-knarzen.wav` | 49,1 dB | 48,0 dB | -10,9 dB |
| (Runde 1) `aufladen-b-brummen.wav` | 43,0 dB | 52,6 dB | -17,4 dB |
| (Runde 1) `aufladen-c-ratsche.wav` | 15,4 dB | 54,3 dB | -11,6 dB |

In Runde 1 stand im Schlussfenster ein Teilton 43 bis 54 dB über seiner Umgebung (bei 525 bis 662 Hz; das ist der Messbefund zum „Ding“). In Runde 2 liegt der höchste Wert bei 16,3 dB, also im Bereich von Rauschen. Das ist eine Messung, kein Klangurteil. B hat über 400 Hz fast keine Energie (-38 dB), weil der Schlag unter 120 Hz bleibt.

Alle Aufladen-Dateien laufen auf Stille aus (Spitze der letzten 50 ms -91 dBFS, Soll höchstens -50). Zwei Läufe von `bauen.py` ergeben bitgleiche Dateien (`wav.sha256`).

## Unsicherheiten

- Ob ein Faustschlag (B) oder ein Luftstoß (C) als „Schluss“ gehört wird oder wieder als Ereignis stört, entscheidet nur das Ohr. A hat die geringste Gefahr, ein Ereignis zu sein, dafür fehlt dem Ende ein deutlicher Schlusspunkt.
- Der Einsatzzähler in `messwerte.md` zählt bei tiefen Tönen mehr als einen Einsatz (Perioden über 6 ms; das 30-ms-Fenster zeigt den wahren Wert) und bei C, weil die Ratsche gewollt aus Pulsen besteht.
- Messwerte und Spektrogramme: `messwerte.md`, `spektrogramme/` (je Datei ein Bild, dazu die drei Runde-1-Aufladen-Dateien zum Vergleich).
- Nachbau: `python bauen.py` (Dateien; liest `hieb-c-luftdruck.wav` und `treffer-fleisch-c-nass.wav` aus `../runde-1/`), `python messen.py` (Messwerte, Spektrogramme; braucht `ffmpeg`).
