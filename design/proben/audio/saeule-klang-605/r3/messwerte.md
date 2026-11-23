# Messwerte Saeulen-Klang (#605)

Gemessen mit ffmpeg und Python (`messen.py`). Gemessen heisst nicht gehoert: ob es gut klingt, entscheidet der Mensch.

| Datei | Laenge s | Spitze dBFS | Mittel dBFS (RMS) | Lautheit LUFS | Anteil ueber 4 kHz dB | letzte 50 ms dBFS | lautestes 10-ms-Fenster bei s |
|---|---|---|---|---|---|---|---|
| saeule-bruch-a.wav | 2.00 | -3.9 | -19.4 | -20.4 | -33.6 | -91.0 | 0.808 (-6 dBFS) |
| saeule-bruch-b.wav | 2.00 | -3.0 | -19.4 | -21.0 | -32.8 | -91.0 | 0.733 (-6 dBFS) |
| saeule-treffer-stufe1-a.wav | 0.45 | -3.4 | -16.6 | -18.8 | -27.9 | -91.0 | 0.015 (-8 dBFS) |
| saeule-treffer-stufe1-b.wav | 0.45 | -3.0 | -16.6 | -17.5 | -22.7 | -55.3 | 0.030 (-8 dBFS) |
| saeule-treffer-stufe2-a.wav | 0.60 | -3.5 | -14.4 | -21.6 | -32.6 | -91.0 | 0.035 (-6 dBFS) |
| saeule-treffer-stufe2-b.wav | 0.60 | -3.0 | -14.4 | -19.4 | -33.7 | -91.0 | 0.085 (-5 dBFS) |

Anteil ueber 4 kHz: Pegel des doppelt (24 dB/Okt.) bei 4 kHz hochgepassten Signals minus Gesamtmittel; je kleiner, desto weniger Hoehen.

## Pegel je 100 ms (dBFS)

- saeule-bruch-a.wav: -36 -34 -33 -31 -23 -22 -21 -10 -11 -17 -34 -34 -34 -36 -59 -82 -180 -180 -180 -180
- saeule-bruch-b.wav: -49 -33 -31 -32 -29 -23 -24 -10 -11 -21 -21 -23 -30 -30 -38 -35 -46 -70 -180 -180
- saeule-treffer-stufe1-a.wav: -11 -17 -55 -180
- saeule-treffer-stufe1-b.wav: -11 -20 -22 -29
- saeule-treffer-stufe2-a.wav: -9 -11 -25 -47 -102 -180
- saeule-treffer-stufe2-b.wav: -8 -14 -18 -21 -42 -113
