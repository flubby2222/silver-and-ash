# Messwerte Fallgitter-Hoerprobe (#540)

Gemessen mit Python (Einsatz, Verlauf) und `ffmpeg` (volumedetect, astats). Gemessen, nicht gehoert.

| Datei | Dauer s | Spitze dBFS | Mittel dBFS | Letzte 50 ms dBFS | Energie > 3 kHz dB |
|---|---|---|---|---|---|
| fallen-a-variante-1.wav | 1.37 | -3.0 | -23.0 | -91.0 | -40.6 |
| fallen-a-variante-2.wav | 1.37 | -3.0 | -22.4 | -90.3 | -44.0 |
| fallen-b-variante-1.wav | 1.75 | -3.0 | -22.1 | -91.0 | -39.5 |
| fallen-b-variante-2.wav | 1.75 | -3.0 | -21.5 | -90.3 | -43.4 |
| heben-variante-1.wav | 2.90 | -3.0 | -21.2 | -91.0 | -49.2 |
| heben-variante-2.wav | 2.90 | -3.0 | -18.8 | -84.3 | -50.3 |

## Einsatz auf dem Tick

Block (32 Samples) mit dem staerksten Pegelanstieg im Fenster +-40 ms um den Tick; Abweichung seines Anfangs vom Tick-Sample (Tick x 735). Negativ = vor dem Tick. Anstieg = Blockspitze gegen Mittel der 4 Bloecke davor.

| Datei | Tick | Abweichung Samples | Abweichung ms | Anstieg dB |
|---|---|---|---|---|
| fallen-a-variante-1 | 24 | -12 | -0.27 | 19.3 |
| fallen-a-variante-1 | 35 | 380 | 8.62 | 10.4 |
| fallen-a-variante-2 | 24 | 4 | 0.09 | 18.3 |
| fallen-a-variante-2 | 35 | 644 | 14.60 | 10.7 |
| fallen-b-variante-1 | 12 | 4 | 0.09 | 18.7 |
| fallen-b-variante-1 | 26 | 12 | 0.27 | 28.6 |
| fallen-b-variante-1 | 47 | -4 | -0.09 | 14.8 |
| fallen-b-variante-1 | 58 | -4 | -0.09 | 10.1 |
| fallen-b-variante-2 | 12 | -4 | -0.09 | 18.5 |
| fallen-b-variante-2 | 26 | -188 | -4.26 | 16.0 |
| fallen-b-variante-2 | 47 | 4 | 0.09 | 24.8 |
| fallen-b-variante-2 | 58 | 636 | 14.42 | 10.3 |
| heben-variante-1 | 126 | -4 | -0.09 | 35.1 |
| heben-variante-2 | 126 | -4 | -0.09 | 28.5 |

## Pegel je 100 ms (RMS dBFS)

- fallen-a-variante-1: -27 -31 -27 -23 -13 -20 -28 -37 -48 -60 -74 -84 -102
- fallen-a-variante-2: -29 -31 -27 -25 -13 -18 -24 -29 -37 -43 -51 -60 -74
- fallen-b-variante-1: -26 -30 -18 -31 -24 -31 -27 -16 -14 -21 -29 -39 -49 -61 -74 -86 -106
- fallen-b-variante-2: -27 -31 -17 -25 -25 -31 -27 -17 -13 -18 -24 -29 -37 -43 -51 -60 -75
- heben-variante-1: -28 -30 -23 -20 -19 -20 -20 -21 -21 -21 -21 -20 -22 -22 -21 -20 -21 -20 -21 -21 -19 -13 -30 -41 -57 -71 -78 -95 -180
- heben-variante-2: -26 -29 -20 -18 -19 -18 -19 -17 -18 -18 -18 -18 -20 -18 -19 -18 -18 -18 -18 -18 -19 -11 -21 -28 -37 -47 -52 -65 -84
