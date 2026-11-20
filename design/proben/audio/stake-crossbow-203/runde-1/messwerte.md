# Messwerte Stake-Crossbow-Hörprobe, Runde 1 (#203)

Erzeugt von `messen.py`. Alles gemessen, nichts gehört. Spitze = höchster Abtastwert; Mittel = ffmpeg `volumedetect` über die ganze Datei (Stille eingerechnet); über 3 kHz = Pegel nach Hochpass 3 kHz minus Gesamtpegel (je kleiner, desto weniger Höhen); Einsätze = Pegelsprünge über 8 dB in 50 ms (10-ms- und 30-ms-Fenster, siehe Hinweis in der LIESMICH); Abklang = Zeit bis dauerhaft 40 dB unter der Spitze; Ende = Spitze der letzten 50 ms (Soll höchstens -50 dBFS).

| Datei | Länge s | Spitze dBFS | Mittel dB | über 3 kHz dB | Einsätze 10/30 ms | Abklang s | Ende dBFS | Spektrogramm |
|---|---|---|---|---|---|---|---|---|
| einschlag-fleisch-a-dumpf.wav | 0.46 | -6.9 | -22.6 | -47.9 | 0 / 0 | 0.29 | -180.0 | [Bild](spektrogramme/einschlag-fleisch-a-dumpf.png) |
| einschlag-fleisch-b-knochen.wav | 0.51 | -8.2 | -22.6 | -37.9 | 0 / 0 | 0.37 | -90.3 | [Bild](spektrogramme/einschlag-fleisch-b-knochen.png) |
| einschlag-fleisch-c-nass.wav | 0.48 | -4.5 | -22.6 | -28.1 | 0 / 0 | 0.24 | -180.0 | [Bild](spektrogramme/einschlag-fleisch-c-nass.png) |
| einschlag-hart-a-holz-steckt.wav | 0.56 | -6.6 | -24.6 | -34.3 | 0 / 0 | 0.43 | -84.3 | [Bild](spektrogramme/einschlag-hart-a-holz-steckt.png) |
| einschlag-hart-b-stein-prallt-ab.wav | 0.34 | -4.5 | -24.6 | -19.3 | 0 / 0 | 0.24 | -84.3 | [Bild](spektrogramme/einschlag-hart-b-stein-prallt-ab.png) |
| einschlag-hart-c-holz-splittert.wav | 0.26 | -9.6 | -24.6 | -24.0 | 0 / 0 | 0.14 | -84.3 | [Bild](spektrogramme/einschlag-hart-c-holz-splittert.png) |
| hoerprobe.wav | 32.00 | -1.5 | -25.8 | -24.9 | 65 / 55 | 31.24 | -180.0 | [Bild](spektrogramme/hoerprobe.png) |
| schuss-a-sehnenschlag.wav | 0.53 | -1.5 | -17.9 | -25.6 | 0 / 0 | 0.44 | -90.3 | [Bild](spektrogramme/schuss-a-sehnenschlag.png) |
| schuss-b-holzschlag.wav | 0.38 | -1.9 | -17.9 | -26.1 | 0 / 0 | 0.23 | -90.3 | [Bild](spektrogramme/schuss-b-holzschlag.png) |
| schuss-c-stahlbogen.wav | 0.56 | -2.0 | -17.9 | -21.2 | 0 / 0 | 0.48 | -84.3 | [Bild](spektrogramme/schuss-c-stahlbogen.png) |
| spannen-a-kurbel.wav | 1.45 | -8.0 | -26.8 | -24.6 | 14 / 8 | 1.28 | -90.3 | [Bild](spektrogramme/spannen-a-kurbel.png) |
| spannen-b-hebel.wav | 1.17 | -11.8 | -26.8 | -34.2 | 5 / 3 | 1.05 | -84.3 | [Bild](spektrogramme/spannen-b-hebel.png) |
| spannen-c-zwei-stufen.wav | 1.20 | -10.4 | -26.8 | -29.7 | 6 / 4 | 1.09 | -73.4 | [Bild](spektrogramme/spannen-c-zwei-stufen.png) |
| zusammenhang-a.wav | 3.26 | -1.5 | -23.6 | -26.2 | 16 / 10 | 3.07 | -180.0 | [Bild](spektrogramme/zusammenhang-a.png) |
| zusammenhang-b.wav | 2.88 | -1.9 | -24.0 | -28.4 | 7 / 5 | 2.71 | -90.3 | [Bild](spektrogramme/zusammenhang-b.png) |
| zusammenhang-c.wav | 3.06 | -2.0 | -23.3 | -22.8 | 8 / 6 | 2.81 | -180.0 | [Bild](spektrogramme/zusammenhang-c.png) |
