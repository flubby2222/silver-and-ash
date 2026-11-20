# Messwerte Hunter's-Axe-Hörprobe, Runde 1 (#201)

Erzeugt von `messen.py`. Alles gemessen, nichts gehört. Spitze = höchster Abtastwert; Mittel = ffmpeg `volumedetect` über die ganze Datei (Stille eingerechnet); über 3 kHz = Pegel nach Hochpass 3 kHz minus Gesamtpegel (je kleiner, desto weniger Höhen); Einsätze = Pegelsprünge über 8 dB in 50 ms (10-ms- und 30-ms-Fenster, siehe Hinweis in der LIESMICH); Abklang = Zeit bis dauerhaft 40 dB unter der Spitze; Ende = Spitze der letzten 50 ms (Soll höchstens -50 dBFS).

| Datei | Länge s | Spitze dBFS | Mittel dB | über 3 kHz dB | Einsätze 10/30 ms | Abklang s | Ende dBFS | Spektrogramm |
|---|---|---|---|---|---|---|---|---|
| aufladen-a-knarzen.wav | 1.40 | -11.4 | -29.1 | -29.8 | 3 / 1 | 1.28 | -180.0 | [Bild](spektrogramme/aufladen-a-knarzen.png) |
| aufladen-b-brummen.wav | 1.60 | -14.3 | -29.1 | -42.6 | 4 / 0 | 1.46 | -180.0 | [Bild](spektrogramme/aufladen-b-brummen.png) |
| aufladen-c-ratsche.wav | 1.55 | -10.0 | -29.1 | -31.6 | 11 / 5 | 1.30 | -180.0 | [Bild](spektrogramme/aufladen-c-ratsche.png) |
| hieb-a-tiefes-rauschen.wav | 0.75 | -9.0 | -22.7 | -21.1 | 2 / 1 | 0.70 | -54.2 | [Bild](spektrogramme/hieb-a-tiefes-rauschen.png) |
| hieb-b-eisenkopf.wav | 0.80 | -10.8 | -22.7 | -28.8 | 1 / 1 | 0.74 | -62.0 | [Bild](spektrogramme/hieb-b-eisenkopf.png) |
| hieb-c-luftdruck.wav | 0.60 | -10.6 | -22.7 | -42.4 | 3 / 1 | 0.54 | -56.2 | [Bild](spektrogramme/hieb-c-luftdruck.png) |
| treffer-fleisch-a-dumpf.wav | 0.56 | -4.6 | -20.9 | -42.0 | 0 / 0 | 0.39 | -180.0 | [Bild](spektrogramme/treffer-fleisch-a-dumpf.png) |
| treffer-fleisch-b-knochen.wav | 0.56 | -7.0 | -20.9 | -37.5 | 0 / 0 | 0.42 | -90.3 | [Bild](spektrogramme/treffer-fleisch-b-knochen.png) |
| treffer-fleisch-c-nass.wav | 0.56 | -4.5 | -20.9 | -27.9 | 0 / 0 | 0.41 | -90.3 | [Bild](spektrogramme/treffer-fleisch-c-nass.png) |
| treffer-hart-a-funkenschlag.wav | 0.40 | -5.5 | -22.2 | -28.7 | 0 / 0 | 0.25 | -180.0 | [Bild](spektrogramme/treffer-hart-a-funkenschlag.png) |
| treffer-hart-b-stiel-prallt.wav | 0.63 | -4.5 | -22.2 | -29.1 | 0 / 0 | 0.48 | -180.0 | [Bild](spektrogramme/treffer-hart-b-stiel-prallt.png) |
| treffer-hart-c-stein-broeckelt.wav | 0.54 | -4.8 | -22.2 | -24.3 | 0 / 0 | 0.36 | -180.0 | [Bild](spektrogramme/treffer-hart-c-stein-broeckelt.png) |
| zusammenhang-a.wav | 2.76 | -3.6 | -24.7 | -24.5 | 6 / 2 | 2.58 | -180.0 | [Bild](spektrogramme/zusammenhang-a.png) |
| zusammenhang-b.wav | 2.96 | -5.4 | -24.2 | -32.3 | 6 / 2 | 2.81 | -90.3 | [Bild](spektrogramme/zusammenhang-b.png) |
| zusammenhang-c.wav | 2.91 | -3.9 | -24.7 | -30.4 | 15 / 7 | 2.76 | -90.3 | [Bild](spektrogramme/zusammenhang-c.png) |
| p1-schwung-1.wav (Bestand) | 0.50 | -9.0 | -29.5 | -2.5 | 1 / 1 | 0.39 | -58.9 | [Bild](spektrogramme/bestand-p1-schwung-1.png) |
| p1-treffer-1.wav (Bestand) | 0.45 | -4.5 | -27.6 | -1.8 | 0 / 0 | 0.23 | -61.7 | [Bild](spektrogramme/bestand-p1-treffer-1.png) |
| klinge-1.wav (Bestand) | 0.24 | -4.9 | -23.7 | -10.2 | 0 / 0 | 0.20 | -48.2 | [Bild](spektrogramme/bestand-klinge-1.png) |
