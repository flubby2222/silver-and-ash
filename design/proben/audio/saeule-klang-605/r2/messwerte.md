# Messwerte Saeulen-Klang (#605)

Gemessen mit ffmpeg und Python (`messen.py`). Gemessen heisst nicht gehoert: ob es gut klingt, entscheidet der Mensch.

| Datei | Laenge s | Spitze dBFS | Mittel dBFS (RMS) | Lautheit LUFS | Anteil ueber 4 kHz dB | letzte 50 ms dBFS | lautestes 10-ms-Fenster bei s |
|---|---|---|---|---|---|---|---|
| saeule-bruch-a.wav | 2.00 | -3.0 | -21.4 | -19.4 | -32.3 | -91.0 | 0.703 (-7 dBFS) |
| saeule-bruch-b.wav | 2.00 | -4.0 | -21.4 | -20.3 | -32.9 | -91.0 | 0.713 (-8 dBFS) |
| saeule-treffer-stufe1-a.wav | 0.45 | -3.0 | -21.7 | -21.3 | -31.3 | -91.0 | 0.000 (-9 dBFS) |
| saeule-treffer-stufe1-b.wav | 0.45 | -6.7 | -21.7 | -21.8 | -38.3 | -70.3 | 0.000 (-12 dBFS) |
| saeule-treffer-stufe2-a.wav | 0.58 | -3.0 | -19.5 | -18.4 | -31.5 | -91.0 | 0.005 (-8 dBFS) |
| saeule-treffer-stufe2-b.wav | 0.58 | -5.0 | -19.5 | -21.1 | -36.9 | -91.0 | 0.005 (-9 dBFS) |

Anteil ueber 4 kHz: Pegel des doppelt (24 dB/Okt.) bei 4 kHz hochgepassten Signals minus Gesamtmittel; je kleiner, desto weniger Hoehen.

## Pegel je 100 ms (dBFS)

- saeule-bruch-a.wav: -50 -28 -27 -37 -25 -36 -24 -10 -17 -25 -29 -33 -38 -34 -49 -74 -97 -180 -180 -180
- saeule-bruch-b.wav: -48 -32 -41 -28 -35 -26 -25 -11 -15 -21 -24 -24 -23 -30 -39 -41 -60 -91 -180 -180
- saeule-treffer-stufe1-a.wav: -15 -38 -45 -180
- saeule-treffer-stufe1-b.wav: -15 -28 -39 -51
- saeule-treffer-stufe2-a.wav: -12 -27 -31 -41 -70
- saeule-treffer-stufe2-b.wav: -13 -20 -32 -40 -52
