# Prüfung der PS1-Kette (`ps1_klang.py`)

Erzeugt mit `python ps1_pruefung.py`. Alle Zahlen sind gemessen; was sie für den Klang bedeuten, entscheidet die Hörprobe.

## 1. ADPCM

- Stille rein, Stille raus: ja
- Blockformat (28 Werte zu 4 Bit, Shift 0–12, Filter 0–4): ja
- Zweimal kodiert gleich einmal kodiert: 99.7 % der Samples identisch (ein guter Kodierer findet die Blöcke wieder)
- Benutzte Filter bei einem 300-Hz-Ton (Filter: Zahl der Blöcke): {2: 148, 4: 89} – der Kodierer wählt je Block den Filter mit dem kleinsten Fehler

## 2. Gauß-Tabelle (Annäherung)

Stützstellen aus psx-spx, berechnete Werte aus der Näherungsformel:

| Index | psx-spx | berechnet | Abweichung |
|---|---|---|---|
| 000h | -1 | -1 | +0 |
| 020h | 3 | 3 | +0 |
| 040h | 39 | 39 | +0 |
| 060h | 156 | 156 | +0 |
| 080h | 424 | 424 | +0 |
| 0A0h | 935 | 934 | -1 |
| 0C0h | 1789 | 1788 | -1 |
| 0E0h | 3079 | 3080 | +1 |
| 0FFh | 4807 | 4808 | +1 |
| 100h | 4871 | 4872 | +1 |
| 120h | 7170 | 7170 | +0 |
| 140h | 9901 | 9901 | +0 |
| 160h | 12905 | 12905 | +0 |
| 180h | 15948 | 15949 | +1 |
| 1A0h | 18746 | 18746 | +0 |
| 1C0h | 21004 | 21003 | -1 |
| 1E0h | 22467 | 22467 | +0 |
| 1FFh | 22963 | 22964 | +1 |

Größte Abweichung: 1 (von 22 963). Summe der vier Gewichte je Phase: 32638–32643; psx-spx nennt für die echte Tabelle 32 639–32 641 (7F7Fh–7F81h).

## 3. Frequenzgang

Sinuston −6 dBFS, 0,4 s; Pegel der Ausgabe relativ zur Eingabe (mittlere Hälfte gemessen), ohne Hall. „Interpolation“ = nur Umrechnen und Abspielen mit Gauß-Interpolation; „+ ADPCM“ = ganze Kette.

| Frequenz | 22 050 Hz: Interpolation | 22 050 Hz: + ADPCM | 11 025 Hz: Interpolation | 11 025 Hz: + ADPCM |
|---|---|---|---|---|
| 100 Hz | -0.0 dB | -0.0 dB | -0.0 dB | -0.0 dB |
| 500 Hz | -0.1 dB | -0.1 dB | -0.1 dB | -0.1 dB |
| 1000 Hz | -0.1 dB | -0.1 dB | -0.5 dB | -0.5 dB |
| 2000 Hz | -0.5 dB | -0.5 dB | -1.7 dB | -1.7 dB |
| 4000 Hz | -1.7 dB | -1.7 dB | -6.9 dB | -7.1 dB |
| 6000 Hz | -3.9 dB | -4.0 dB | -46.6 dB | -46.6 dB |
| 8000 Hz | -6.9 dB | -7.1 dB | unter −60 dB | unter −60 dB |
| 10000 Hz | -11.5 dB | -11.3 dB | unter −60 dB | unter −60 dB |
| 12000 Hz | -46.6 dB | -46.7 dB | unter −60 dB | unter −60 dB |
| 16000 Hz | unter −60 dB | unter −60 dB | unter −60 dB | unter −60 dB |

Erwartet: oberhalb der halben Rate (11 025 bzw. 5 513 Hz) nichts mehr; darunter ein sanfter Abfall der Höhen durch die Gauß-Interpolation, der zur halben Rate hin stärker wird.

## 4. Rauschabstand der ADPCM-Stufe

Signal geteilt durch Fehler (dekodiert minus Eingang der ADPCM-Stufe), bei gleicher Rate.

| Signal | 22 050 Hz | 11 025 Hz |
|---|---|---|
| Sinus 1 kHz, −6 dBFS | 49.7 dB | 43.7 dB |
| Sinus 1 kHz, −30 dBFS | 49.7 dB | 43.4 dB |
| `pistole-d-hell-kurz.wav` (Hörprobe 2) | 31.9 dB | 30.7 dB |

Zum Vergleich: 16-Bit-PCM hat theoretisch etwa 98 dB. Weil ADPCM Shift und Filter je Block anpasst, hängt der Abstand kaum vom Pegel ab, wohl aber davon, wie gut sich das Signal vorhersagen lässt (Rauschen schlecht, tiefe Töne gut).

## 5. Hall-Voreinstellungen

Impulsantwort (ein Impuls 0,5 auf beide Eingänge), gemessen am linken Ausgang: Pegel in 50-ms-Fenstern; „Abklingzeit“ = Zeit, bis der Pegel 60 dB unter dem lautesten Fenster liegt (Kurve über 30 dB hochgerechnet, falls nötig).

| Voreinstellung | Speicher | lautestes Fenster | −30 dB nach | Abklingzeit (geschätzt) | stabil |
|---|---|---|---|---|---|
| Room | 9920 Byte | 0.05 s | 0.50 s | 1.10 s | ja |
| Studio Small | 8000 Byte | 0.00 s | 0.50 s | 1.10 s | ja |
| Studio Medium | 18496 Byte | 0.05 s | 0.60 s | ≈ 1.20 s | ja |
| Studio Large | 28640 Byte | 0.10 s | 1.25 s | ≈ 2.50 s | ja |
| Hall | 44512 Byte | 0.15 s | 1.35 s | ≈ 2.70 s | ja |
| Half Echo | 15360 Byte | 0.05 s | 0.65 s | ≈ 1.30 s | ja |
| Space Echo | 63168 Byte | 0.35 s | 2.60 s | ≈ 5.20 s | ja |

