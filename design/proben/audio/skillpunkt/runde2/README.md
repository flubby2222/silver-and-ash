# Hörprobe Skillpunkt, Runde 2 (Issue #372)

Runde 1 (`../`) wurde abgelehnt („Passt nicht zur Stimmung“, „Zu lang oder zu träge“) und bleibt als abgelehnt liegen. Runde 2: drei verschiedene Richtungen, düsterer, schwerer und kürzer, mit klarem Einsatz. Gebaut und gemessen, **nicht gehört**. **Frage an den Menschen: 1, 2 oder 3? Und was stört?** Alle haben die gleiche Spitze; Lautstärke beim ersten Stück einstellen.

## Varianten

- **1 – Turmglocke** (`1-turmglocke.wav`): tiefer, dumpfer Schlag (70 Hz) mit einer Glocke aus Schlagton 110 Hz und tieferen, inharmonischen Teiltönen, kurzer dunkler Hall. Hauptteil 0,24 s, Nachklang bis 0,46 s.
- **2 – Klinge** (`2-klinge.wav`): kurzes Metallklingen aus drei Teiltönen (620, 1 010, 1 670 Hz) mit einem Hauch tiefpassgefiltertem Rauschen im Anschlag, rasch abfallend. Hauptteil 0,18 s, Nachklang bis 0,35 s. Nichts Nennenswertes über 4 kHz.
- **3 – Schlag mit Chor** (`3-schlag-chor.wav`): tiefer Sinusschlag (90 → 45 Hz) und knapp danach ein dunkler a-Moll-Akkord (110, 131, 165, 220 Hz) mit „o“-Klangfarbe und leichtem Vibrato. Hauptteil 0,35 s, Nachklang bis 0,56 s.

## Erzeugen

```bash
python design/proben/audio/skillpunkt/runde2/bauen.py
```

Schreibt die drei WAV-Dateien neben das Skript und druckt die Tabelle. Zwei Läufe liefern byte-gleiche Dateien (SHA-256 geprüft).

## Messwerte (gemessen, ungehört)

Mono, 44 100 Hz, 16 Bit. Spitze und mittlere Lautstärke mit `ffmpeg -af volumedetect` gegengeprüft. „Hauptteil“ = Zeit, bis der Pegel (10-ms-Fenster) 20 dB unter die Spitze gefallen ist; „Nachklang“ = bis 40 dB darunter. Die Dateien sind länger (siehe „Gesamt“), weil der Hall leise bis in die Stille ausläuft.

| Datei | Gesamt (s) | Hauptteil (s) | Nachklang (s) | Spitze (dBFS) | mittlere Lautstärke (dB) | Hauptfrequenz (Hz) | Energie >2 kHz (%) | Energie >4 kHz (%) |
|---|---|---|---|---|---|---|---|---|
| 1-turmglocke.wav | 0,90 | 0,24 | 0,46 | −3,0 | −24,4 | 110 | 0,00 | 0,00 |
| 2-klinge.wav | 0,65 | 0,18 | 0,35 | −3,0 | −22,6 | 620 | 0,10 | 0,04 |
| 3-schlag-chor.wav | 0,85 | 0,35 | 0,56 | −3,0 | −22,0 | 48 (Schlag; Akkord bei 110–220 Hz gemessen) | 0,00 | 0,00 |

Anfang und Ende liegen bei Stille (erstes/letztes Sample 0), kein Übersteuern. Ob eine Variante nach Turmglocke, Klinge oder Chor klingt, kann nur der Mensch beurteilen.
