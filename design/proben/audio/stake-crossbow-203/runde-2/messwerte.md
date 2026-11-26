# Messwerte Hörprobe Stake Crossbow, Runde 2 (#203)

Erzeugt von `messen.py`. Alles gemessen, nichts gehört. Spitze = höchster Abtastwert (ffmpeg `volumedetect`); Mittel = `volumedetect` über die ganze Datei; über 3 kHz = Pegel nach Hochpass 3 kHz minus Gesamtpegel (je kleiner, desto weniger Höhen); Schwerpunkt = energiegewichtete Mitte des Spektrums; Einsätze = Pegelsprünge über 8 dB in 50 ms (10- und 30-ms-Fenster; der Beginn zählt nicht mit); Abklang = Zeit bis dauerhaft 40 dB unter der Spitze; Ende = Spitze der letzten 50 ms (Soll höchstens -50 dBFS).

## Schuss

| Datei | Länge s | Spitze dBFS | Mittel dB | über 3 kHz dB | Schwerpunkt Hz | Einsätze 10/30 ms | Abklang s | Ende dBFS | Spektrogramm |
|---|---|---|---|---|---|---|---|---|---|
| schuss-a-sehne-zischen.wav | 0.55 | -1.5 | -18.2 | -26.9 | 120 | 0 / 0 | 0.35 | -180.0 | [Bild](spektrogramme/schuss-a-sehne-zischen.png) |
| schuss-b-dumpfer-stoss.wav | 0.36 | -3.5 | -18.2 | -38.0 | 74 | 0 / 0 | 0.26 | -90.3 | [Bild](spektrogramme/schuss-b-dumpfer-stoss.png) |
| schuss-c-schiene-schleift.wav | 0.44 | -2.3 | -18.2 | -25.6 | 216 | 1 / 1 | 0.32 | -73.4 | [Bild](spektrogramme/schuss-c-schiene-schleift.png) |

## Treffer Fleisch

| Datei | Länge s | Spitze dBFS | Mittel dB | über 3 kHz dB | Schwerpunkt Hz | Einsätze 10/30 ms | Abklang s | Ende dBFS | Spektrogramm |
|---|---|---|---|---|---|---|---|---|---|
| treffer-fleisch-a-stich.wav | 0.36 | -7.2 | -22.1 | -27.2 | 130 | 0 / 0 | 0.24 | -180.0 | [Bild](spektrogramme/treffer-fleisch-a-stich.png) |
| treffer-fleisch-b-knirschen.wav | 0.36 | -7.7 | -22.1 | -27.5 | 127 | 1 / 0 | 0.24 | -180.0 | [Bild](spektrogramme/treffer-fleisch-b-knirschen.png) |
| treffer-fleisch-c-reissen.wav | 0.42 | -6.2 | -22.1 | -23.7 | 187 | 0 / 0 | 0.30 | -180.0 | [Bild](spektrogramme/treffer-fleisch-c-reissen.png) |

## Treffer hart

| Datei | Länge s | Spitze dBFS | Mittel dB | über 3 kHz dB | Schwerpunkt Hz | Einsätze 10/30 ms | Abklang s | Ende dBFS | Spektrogramm |
|---|---|---|---|---|---|---|---|---|---|
| treffer-hart-a-holz-splittert.wav | 0.40 | -6.7 | -23.0 | -25.6 | 211 | 2 / 0 | 0.24 | -84.3 | [Bild](spektrogramme/treffer-hart-a-holz-splittert.png) |
| treffer-hart-b-stein-bricht.wav | 0.44 | -4.5 | -23.0 | -22.5 | 160 | 2 / 1 | 0.24 | -90.3 | [Bild](spektrogramme/treffer-hart-b-stein-bricht.png) |
| treffer-hart-c-schabt-ab.wav | 0.32 | -7.3 | -23.0 | -21.8 | 238 | 0 / 0 | 0.22 | -74.7 | [Bild](spektrogramme/treffer-hart-c-schabt-ab.png) |

## Nachladen (T = 2,5 s)

| Datei | Länge s | Spitze dBFS | Mittel dB | über 3 kHz dB | Schwerpunkt Hz | Einsätze 10/30 ms | Abklang s | Ende dBFS | Spektrogramm |
|---|---|---|---|---|---|---|---|---|---|
| nachladen-a-kurbel.wav | 2.49 | -8.0 | -28.4 | -23.0 | 303 | 16 / 7 | 2.34 | -90.3 | [Bild](spektrogramme/nachladen-a-kurbel.png) |
| nachladen-b-seilzug.wav | 2.49 | -16.4 | -28.4 | -37.7 | 111 | 4 / 3 | 2.34 | -180.0 | [Bild](spektrogramme/nachladen-b-seilzug.png) |
| nachladen-c-zwei-zuege.wav | 2.49 | -13.3 | -28.4 | -35.5 | 115 | 6 / 5 | 2.34 | -180.0 | [Bild](spektrogramme/nachladen-c-zwei-zuege.png) |

## Dehnung des Nachladens (T = 1,5 s und 3,5 s)

| Datei | Länge s | Spitze dBFS | Mittel dB | über 3 kHz dB | Schwerpunkt Hz | Einsätze 10/30 ms | Abklang s | Ende dBFS | Spektrogramm |
|---|---|---|---|---|---|---|---|---|---|
| nachladen-a-kurbel-1.5s.wav | 1.49 | -8.0 | -28.5 | -23.9 | 287 | 10 / 5 | 1.34 | -90.3 | [Bild](spektrogramme/nachladen-a-kurbel-1.5s.png) |
| nachladen-a-kurbel-3.5s.wav | 3.49 | -8.0 | -28.5 | -22.9 | 319 | 26 / 10 | 3.34 | -90.3 | [Bild](spektrogramme/nachladen-a-kurbel-3.5s.png) |
| nachladen-b-seilzug-1.5s.wav | 1.49 | -13.5 | -28.5 | -33.8 | 123 | 4 / 3 | 1.34 | -90.3 | [Bild](spektrogramme/nachladen-b-seilzug-1.5s.png) |
| nachladen-b-seilzug-3.5s.wav | 3.49 | -14.8 | -28.5 | -39.4 | 110 | 4 / 3 | 3.34 | -180.0 | [Bild](spektrogramme/nachladen-b-seilzug-3.5s.png) |
| nachladen-c-zwei-zuege-1.5s.wav | 1.49 | -12.5 | -28.5 | -34.8 | 123 | 5 / 5 | 1.34 | -180.0 | [Bild](spektrogramme/nachladen-c-zwei-zuege-1.5s.png) |
| nachladen-c-zwei-zuege-3.5s.wav | 3.49 | -13.8 | -28.5 | -38.3 | 109 | 6 / 5 | 3.34 | -180.0 | [Bild](spektrogramme/nachladen-c-zwei-zuege-3.5s.png) |

## Dreierfolgen Fleisch (Abstand 40 ms, je dreimal dieselbe Fassung)

| Datei | Länge s | Spitze dBFS | Mittel dB | über 3 kHz dB | Schwerpunkt Hz | Einsätze 10/30 ms | Abklang s | Ende dBFS | Spektrogramm |
|---|---|---|---|---|---|---|---|---|---|
| dreierfolge-fleisch-a.wav | 0.44 | -5.2 | -17.8 | -27.9 | 123 | 0 / 0 | 0.32 | -180.0 | [Bild](spektrogramme/dreierfolge-fleisch-a.png) |
| dreierfolge-fleisch-b.wav | 0.44 | -5.0 | -18.7 | -27.0 | 136 | 3 / 0 | 0.32 | -180.0 | [Bild](spektrogramme/dreierfolge-fleisch-b.png) |
| dreierfolge-fleisch-c.wav | 0.50 | -3.0 | -18.4 | -23.3 | 197 | 0 / 0 | 0.38 | -180.0 | [Bild](spektrogramme/dreierfolge-fleisch-c.png) |

## Vergleich: Bestand im Spiel

| Datei | Länge s | Spitze dBFS | Mittel dB | über 3 kHz dB | Schwerpunkt Hz | Einsätze 10/30 ms | Abklang s | Ende dBFS | Spektrogramm |
|---|---|---|---|---|---|---|---|---|---|
| pistole.wav | 0.40 | -1.5 | -18.2 | -17.7 | 297 | 0 / 0 | 0.25 | -63.9 |  |
| p1-schuss-1.wav | 0.60 | -1.5 | -19.7 | -8.6 | 1424 | 0 / 0 | 0.52 | -47.1 |  |
| p1-schuss-geweiht-1.wav | 0.60 | -1.5 | -20.4 | -9.7 | 1232 | 0 / 0 | 0.48 | -48.7 |  |
| p1-nachladen-start.wav | 0.30 | -8.0 | -31.5 | -5.1 | 2388 | 0 / 0 | 0.21 | -51.3 |  |
| p1-nachladen-bereit.wav | 0.25 | -8.0 | -31.9 | -3.9 | 3088 | 1 / 0 | 0.21 | -48.7 |  |
| p1-treffer-1.wav | 0.45 | -4.5 | -27.6 | -1.8 | 5780 | 0 / 0 | 0.23 | -61.7 |  |
| axt-treffer-fleisch.wav | 0.56 | -4.5 | -20.9 | -27.9 | 165 | 0 / 0 | 0.41 | -90.3 |  |
| axt-treffer-hart.wav | 0.63 | -4.5 | -22.2 | -29.1 | 180 | 0 / 0 | 0.48 | -180.0 |  |

## Vergleich: Crossbow Runde 1 (Branch `feat/stake-crossbow-hoerprobe-203`, ungehört)

| Datei | Länge s | Spitze dBFS | Mittel dB | über 3 kHz dB | Schwerpunkt Hz | Einsätze 10/30 ms | Abklang s | Ende dBFS | Spektrogramm |
|---|---|---|---|---|---|---|---|---|---|
| einschlag-fleisch-a-dumpf.wav | 0.46 | -6.9 | -22.6 | -47.9 | 110 | 0 / 0 | 0.29 | -180.0 |  |
| einschlag-fleisch-b-knochen.wav | 0.51 | -8.2 | -22.6 | -37.9 | 75 | 0 / 0 | 0.37 | -90.3 |  |
| einschlag-fleisch-c-nass.wav | 0.48 | -4.5 | -22.6 | -28.1 | 162 | 0 / 0 | 0.24 | -180.0 |  |
| einschlag-hart-a-holz-steckt.wav | 0.56 | -6.6 | -24.6 | -34.3 | 204 | 0 / 0 | 0.43 | -84.3 |  |
| einschlag-hart-b-stein-prallt-ab.wav | 0.34 | -4.5 | -24.6 | -19.3 | 580 | 0 / 0 | 0.24 | -84.3 |  |
| einschlag-hart-c-holz-splittert.wav | 0.26 | -9.6 | -24.6 | -24.0 | 213 | 0 / 0 | 0.14 | -84.3 |  |
| schuss-a-sehnenschlag.wav | 0.53 | -1.5 | -17.9 | -25.6 | 181 | 0 / 0 | 0.44 | -90.3 |  |
| schuss-b-holzschlag.wav | 0.38 | -1.9 | -17.9 | -26.1 | 246 | 0 / 0 | 0.23 | -90.3 |  |
| schuss-c-stahlbogen.wav | 0.56 | -2.0 | -17.9 | -21.2 | 270 | 0 / 0 | 0.48 | -84.3 |  |
| spannen-a-kurbel.wav | 1.45 | -8.0 | -26.8 | -24.6 | 457 | 14 / 8 | 1.28 | -90.3 |  |
| spannen-b-hebel.wav | 1.17 | -11.8 | -26.8 | -34.2 | 205 | 5 / 3 | 1.05 | -84.3 |  |
| spannen-c-zwei-stufen.wav | 1.20 | -10.4 | -26.8 | -29.7 | 353 | 6 / 4 | 1.09 | -73.4 |  |

## Dreierfolge Fleisch, alle 27 Kombinationen der Fassungen

Höchste Spitze: -1.50 dBFS (Grenze -1,0 dBFS; Ziel der Skripts -1,5).
