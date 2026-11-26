# Messwerte Whip-Hörprobe, Runde 1 (#202)

Erzeugt von `messen.py`. Alles gemessen, nichts gehört. Spitze = höchster Abtastwert; Mittel = ffmpeg `volumedetect` über die ganze Datei (Stille eingerechnet); über 4 kHz = Pegel nach Hochpass 4 kHz (zwei Stufen hintereinander, 24 dB je Oktave) minus Gesamtpegel (je kleiner, desto weniger Höhen; zusätzlich die Spitze über 4 kHz in dBFS); Einsätze = Pegelsprünge über 8 dB in 50 ms (10-ms- und 30-ms-Fenster); Abklang = Zeit bis dauerhaft 40 dB unter der Spitze; Ende = Spitze der letzten 50 ms (Soll höchstens -50 dBFS).

| Datei | Länge s | Spitze dBFS | Mittel dB | über 4 kHz dB | Spitze über 4 kHz dBFS | Einsätze 10/30 ms | Abklang s | Ende dBFS | Spektrogramm |
|---|---|---|---|---|---|---|---|---|---|
| aufladen-a-leder-knarrt.wav | 1.35 | -12.1 | -26.8 | -50.3 | -56.2 | 4 / 0 | 1.24 | -180.0 | [Bild](spektrogramme/aufladen-a-leder-knarrt.png) |
| aufladen-b-wirbeln.wav | 1.28 | -10.0 | -26.8 | -32.9 | -40.6 | 6 / 2 | 1.16 | -90.3 | [Bild](spektrogramme/aufladen-b-wirbeln.png) |
| aufladen-c-spannen.wav | 1.40 | -11.5 | -26.8 | -49.7 | -54.9 | 8 / 2 | 1.30 | -90.3 | [Bild](spektrogramme/aufladen-c-spannen.png) |
| heranziehen-a-ruck.wav | 0.36 | -6.0 | -21.5 | -36.0 | -37.8 | 0 / 0 | 0.28 | -78.3 | [Bild](spektrogramme/heranziehen-a-ruck.png) |
| heranziehen-b-schleifen.wav | 0.51 | -6.0 | -23.3 | -36.2 | -40.6 | 0 / 0 | 0.45 | -84.3 | [Bild](spektrogramme/heranziehen-b-schleifen.png) |
| heranziehen-c-lederruck.wav | 0.33 | -6.0 | -23.3 | -44.7 | -47.8 | 1 / 1 | 0.23 | -74.7 | [Bild](spektrogramme/heranziehen-c-lederruck.png) |
| schlag-a-scharfer-knall.wav | 0.60 | -6.0 | -28.4 | -21.6 | -25.1 | 3 / 1 | 0.24 | -180.0 | [Bild](spektrogramme/schlag-a-scharfer-knall.png) |
| schlag-b-dumpfer-lederschlag.wav | 0.70 | -6.0 | -27.0 | -40.3 | -45.7 | 3 / 2 | 0.50 | -180.0 | [Bild](spektrogramme/schlag-b-dumpfer-lederschlag.png) |
| schlag-c-langes-zischen.wav | 0.93 | -6.0 | -22.9 | -21.4 | -27.7 | 1 / 1 | 0.75 | -180.0 | [Bild](spektrogramme/schlag-c-langes-zischen.png) |
| treffer-fleisch-a-klatsch.wav | 0.35 | -4.5 | -21.3 | -42.8 | -37.9 | 0 / 0 | 0.20 | -84.3 | [Bild](spektrogramme/treffer-fleisch-a-klatsch.png) |
| treffer-fleisch-b-striemen.wav | 0.29 | -4.5 | -19.2 | -36.9 | -38.1 | 0 / 0 | 0.21 | -55.5 | [Bild](spektrogramme/treffer-fleisch-b-striemen.png) |
| treffer-fleisch-c-nass-dumpf.wav | 0.40 | -4.5 | -19.4 | -49.1 | -47.6 | 0 / 0 | 0.31 | -65.7 | [Bild](spektrogramme/treffer-fleisch-c-nass-dumpf.png) |
| treffer-hart-a-stein-knallt.wav | 0.20 | -4.5 | -24.5 | -33.3 | -35.5 | 0 / 0 | 0.11 | -65.2 | [Bild](spektrogramme/treffer-hart-a-stein-knallt.png) |
| treffer-hart-b-holz-klopft.wav | 0.33 | -4.5 | -22.5 | -49.5 | -44.8 | 0 / 0 | 0.20 | -180.0 | [Bild](spektrogramme/treffer-hart-b-holz-klopft.png) |
| treffer-hart-c-stein-rieselt.wav | 0.45 | -4.5 | -20.5 | -43.3 | -42.0 | 0 / 0 | 0.33 | -90.3 | [Bild](spektrogramme/treffer-hart-c-stein-rieselt.png) |
| zusammenhang-a.wav | 0.60 | -3.2 | -21.9 | -28.1 | -25.1 | 4 / 1 | 0.38 | -180.0 | [Bild](spektrogramme/zusammenhang-a.png) |
| zusammenhang-b.wav | 0.70 | -5.6 | -21.5 | -37.8 | -38.1 | 3 / 2 | 0.53 | -180.0 | [Bild](spektrogramme/zusammenhang-b.png) |
| zusammenhang-c.wav | 0.97 | -4.1 | -20.1 | -24.4 | -27.7 | 2 / 2 | 0.88 | -65.7 | [Bild](spektrogramme/zusammenhang-c.png) |
| p1-schwung-1.wav (Bestand) | 0.50 | -9.0 | -29.5 | -5.5 | -15.4 | 1 / 1 | 0.39 | -58.9 | [Bild](spektrogramme/bestand-p1-schwung-1.png) |
| p1-treffer-1.wav (Bestand) | 0.45 | -4.5 | -27.6 | -3.2 | -6.6 | 0 / 0 | 0.23 | -61.7 | [Bild](spektrogramme/bestand-p1-treffer-1.png) |
| klinge-1.wav (Bestand) | 0.24 | -4.9 | -23.7 | -16.0 | -13.5 | 0 / 0 | 0.20 | -48.2 | [Bild](spektrogramme/bestand-klinge-1.png) |
