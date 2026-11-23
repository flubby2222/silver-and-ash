# Messwerte Saeulen-Klang (#605)

Gemessen mit ffmpeg und Python (`messen.py`). Gemessen heisst nicht gehoert: ob es gut klingt, entscheidet der Mensch.

| Datei | Laenge s | Spitze dBFS | Mittel dBFS (RMS) | Lautheit LUFS | Anteil ueber 4 kHz dB | letzte 50 ms dBFS | lautestes 10-ms-Fenster bei s |
|---|---|---|---|---|---|---|---|
| saeule-bruch-a.wav | 2.00 | -5.3 | -21.4 | -23.5 | -51.4 | -91.0 | 0.808 (-7 dBFS) |
| saeule-bruch-b.wav | 2.00 | -4.5 | -21.4 | -23.0 | -51.5 | -91.0 | 0.733 (-7 dBFS) |
| saeule-bruch-c.wav | 2.00 | -5.3 | -21.4 | -23.6 | -65.9 | -91.0 | 0.808 (-7 dBFS) |
| saeule-treffer-stufe1-a.wav | 0.45 | -9.2 | -21.7 | -26.7 | -45.7 | -91.0 | 0.020 (-12 dBFS) |
| saeule-treffer-stufe1-b.wav | 0.45 | -8.8 | -21.7 | -26.0 | -39.9 | -57.2 | 0.030 (-12 dBFS) |
| saeule-treffer-stufe1-c.wav | 0.45 | -10.1 | -21.7 | -27.7 | -65.6 | -91.0 | 0.020 (-12 dBFS) |
| saeule-treffer-stufe2-a.wav | 0.60 | -8.8 | -19.5 | -26.7 | -51.9 | -91.0 | 0.035 (-10 dBFS) |
| saeule-treffer-stufe2-b.wav | 0.60 | -8.1 | -19.5 | -25.8 | -53.0 | -91.0 | 0.085 (-10 dBFS) |
| saeule-treffer-stufe2-c.wav | 0.60 | -9.6 | -19.5 | -27.1 | -70.8 | -91.0 | 0.035 (-11 dBFS) |

Anteil ueber 4 kHz: Pegel des doppelt (24 dB/Okt.) bei 4 kHz hochgepassten Signals minus Gesamtmittel; je kleiner, desto weniger Hoehen.

## Pegel je 100 ms (dBFS)

- saeule-bruch-a.wav: -42 -39 -40 -35 -28 -27 -25 -12 -13 -19 -39 -37 -39 -39 -64 -89 -180 -180 -180 -180
- saeule-bruch-b.wav: -53 -39 -36 -38 -34 -28 -28 -12 -12 -23 -24 -26 -37 -34 -44 -40 -54 -78 -180 -180
- saeule-bruch-c.wav: -44 -39 -43 -37 -29 -28 -27 -12 -12 -18 -40 -38 -40 -39 -66 -95 -180 -180 -180 -180
- saeule-treffer-stufe1-a.wav: -16 -21 -59 -180
- saeule-treffer-stufe1-b.wav: -16 -27 -30 -36
- saeule-treffer-stufe1-c.wav: -16 -21 -58 -180
- saeule-treffer-stufe2-a.wav: -14 -16 -34 -58 -180 -180
- saeule-treffer-stufe2-b.wav: -13 -19 -25 -26 -47 -180
- saeule-treffer-stufe2-c.wav: -14 -16 -36 -61 -180 -180
