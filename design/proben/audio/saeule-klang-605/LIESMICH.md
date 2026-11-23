# Hörprobe Säulen-Klang (Issue #605, Schritt 1)

Probestücke außerhalb des Spiels. Alles per Skript (`bauen.py`, nur Python-Standardbibliothek) aus Rauschen und Sinus gebaut, Messung mit `messen.py` (siehe `messwerte.md`). Mono, 44 100 Hz, 16 Bit, Spitze höchstens −3 dBFS; a und b eines Paares haben gleiche mittlere Lautstärke. Gemessen, nicht gehört.

| Datei | Was |
|---|---|
| `saeule-treffer-stufe1-a.wav` | Treffer Stufe 1, kurzer trockener Knall (0,45 s) |
| `saeule-treffer-stufe1-b.wav` | Treffer Stufe 1, dumpfer und tiefer (0,45 s) |
| `saeule-treffer-stufe2-a.wav` | Treffer Stufe 2, ein schwerer Schlag mit Bröckeln dahinter (0,6 s) |
| `saeule-treffer-stufe2-b.wav` | Treffer Stufe 2, Hauptschlag plus nachrutschender Brocken, dichteres Bröckeln (0,6 s) |
| `saeule-bruch-a.wav` | Bruch, ein schwerer Aufschlag bei 0,70 s (2,0 s) |
| `saeule-bruch-b.wav` | Bruch, mehrere Brocken, Gewicht über 0,65 bis 0,85 s (2,0 s) |

**Treffer Stufe 1:** a ist ein kurzer, härterer Schlag mit etwas höherer Bassnote (95 Hz); b ist tiefer (68 Hz), runder und läuft etwas länger aus. Stufe 2 ist in beiden Fassungen tiefer (50 bis 55 Hz) und länger als Stufe 1 und hat Bröckeln.

**Treffer Stufe 2:** a ist ein einziger schwerer Schlag, danach rieseln einzelne Steinchen; b hat nach dem Hauptschlag einen zweiten, leiseren Brocken und dichteres, dunkleres Bröckeln.

**Bruch:** Beide folgen dem Spielablauf: 0 bis 0,70 s tiefes Knirschen, das anschwillt, mit drei dumpfen Rissen (Ticks 8/20/31 bei a, 12/26/36 bei b); Aufschlag bei Tick 42 (0,70 s); danach Nachrieseln bis etwa 1,35 s, dann leise Ausklang. a hat einen klaren Hauptschlag und drei kleinere Stücke dicht dahinter; b verteilt das Gewicht auf fünf Brocken (der lauteste Punkt liegt bei 0,84 s) und hat ein längeres Rollen und dichteres Nachrieseln.

Neu bauen: `python bauen.py` (bitgleich, Prüfsummen in `wav.sha256`), dann `python messen.py`.
