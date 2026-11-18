# Hörprobe: Klang für einen neuen Skillpunkt (Issue #372)

Drei kurze, einfache Varianten, per Synthese-Skript erzeugt (Python, nur Standardbibliothek). Probestücke, nicht im Spiel. Gehört hat sie noch niemand: Alles hier ist gebaut und gemessen, nicht beurteilt. **Frage an den Menschen: a, b oder c? Und was stört?** Lautstärke beim ersten Stück einstellen; alle haben die gleiche Spitze.

## Varianten

- **a – Glocke** (`a-glocke.wav`): ein einzelner weicher Glockenton aus Grundton und zwei leisen Obertönen, langsam ausklingend. Grundton 330 Hz (E4), 0,90 s.
- **b – Aufstieg** (`b-aufstieg.wav`): zwei weiche Töne kurz hintereinander, eine Quinte aufwärts (220 Hz, dann 330 Hz), jeder mit leichtem Oberton. 0,65 s.
- **c – Zupfton** (`c-zupfton.wav`): eine gezupfte, dumpfe Saite (Karplus-Strong, mit Tiefpass bei etwa 600 Hz). Grundton 196 Hz (G3), 0,70 s.

## Erzeugen

```bash
python design/proben/audio/skillpunkt/bauen.py
```

Schreibt die drei WAV-Dateien neben das Skript und druckt die Tabelle unten. Zwei Läufe liefern byte-gleiche Dateien (geprüft per SHA-256).

## Messwerte (gemessen, ungehört)

Mono, 44 100 Hz, 16 Bit. Spitze mit `ffmpeg -af volumedetect` gegengeprüft.

| Datei | Dauer (s) | Spitze (dBFS) | mittlere Lautstärke (dB) | Hauptfrequenz (Hz) | Energie über 2 kHz (%) | erstes/letztes Sample |
|---|---|---|---|---|---|---|
| a-glocke.wav | 0,90 | −3,0 | −18,1 | 330 | 0,00 | 0 / 0 |
| b-aufstieg.wav | 0,65 | −3,0 | −14,6 | 220 (erster Ton) | 0,00 | 0 / 0 |
| c-zupfton.wav | 0,70 | −3,0 | −13,1 | 393 (2. Teilton von 196) | 7,4 (Anschlag) | 0 / 0 |

Alles unter dem Rahmen der Audio-Bible: Spitze höchstens −1 dBFS, kein Übersteuern, Anfang und Ende bei Stille. Hell ist keine Variante: Bei a und b liegt gar nichts über 2 kHz, bei c nur der kurze Anschlag.
