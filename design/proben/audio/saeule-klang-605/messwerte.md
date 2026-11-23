# Messwerte Saeulen-Klang (#605)

Gemessen mit ffmpeg und Python (`messen.py`). Gemessen heisst nicht gehoert: ob es gut klingt, entscheidet der Mensch.

| Datei | Laenge s | Spitze dBFS | Mittel dBFS (RMS) | Lautheit LUFS | Anteil ueber 4 kHz dB | letzte 50 ms dBFS | lautestes 10-ms-Fenster bei s |
|---|---|---|---|---|---|---|---|
| saeule-bruch-a.wav | 2.00 | -3.0 | -22.5 | -23.8 | -56.0 | -91.0 | 0.708 (-10 dBFS) |
| saeule-bruch-b.wav | 2.00 | -5.7 | -22.5 | -22.8 | -54.8 | -91.0 | 0.843 (-11 dBFS) |
| saeule-treffer-stufe1-a.wav | 0.45 | -3.0 | -19.6 | -20.4 | -49.5 | -78.3 | 0.005 (-8 dBFS) |
| saeule-treffer-stufe1-b.wav | 0.45 | -5.3 | -19.6 | -21.2 | -62.3 | -58.9 | 0.005 (-10 dBFS) |
| saeule-treffer-stufe2-a.wav | 0.60 | -3.0 | -18.0 | -22.6 | -56.5 | -76.3 | 0.000 (-9 dBFS) |
| saeule-treffer-stufe2-b.wav | 0.60 | -3.2 | -18.0 | -22.7 | -66.3 | -57.2 | 0.000 (-9 dBFS) |

Anteil ueber 4 kHz: Pegel des doppelt (24 dB/Okt.) bei 4 kHz hochgepassten Signals minus Gesamtmittel; je kleiner, desto weniger Hoehen.

## Pegel je 100 ms (dBFS)

- saeule-bruch-a.wav: -48 -31 -36 -27 -30 -23 -26 -12 -16 -20 -23 -27 -30 -33 -37 -39 -67 -87 -180 -180
- saeule-bruch-b.wav: -48 -39 -28 -31 -23 -26 -18 -14 -15 -20 -26 -30 -33 -37 -42 -65 -84 -106 -180 -180
- saeule-treffer-stufe1-a.wav: -13 -25 -38 -55
- saeule-treffer-stufe1-b.wav: -14 -21 -29 -40
- saeule-treffer-stufe2-a.wav: -12 -17 -22 -27 -35 -52
- saeule-treffer-stufe2-b.wav: -12 -16 -19 -25 -33 -48
