# Messwerte Hunter's-Axe-Hörprobe, Runde 2 (#201): Aufladen

Erzeugt von `messen.py`. Alles gemessen, nichts gehört. Spitze = höchster Abtastwert; Mittel = ffmpeg `volumedetect` über die ganze Datei; Einsätze = Pegelsprünge über 8 dB in 50 ms (10-/30-ms-Fenster); Ende = Spitze der letzten 50 ms (Soll höchstens -50 dBFS). Die letzten drei Spalten messen das Spektrum der **letzten 300 ms** (Hann, FFT 16384, Auflösung 2,7 Hz): Spitze > 400 Hz = höchster Bin über 400 Hz in dB unter dem höchsten Bin des Fensters (bei Frequenz in Klammern); Hervortreten = stärkste lokale Spitze über 400 Hz minus Mittel der Mediane der Bins links und rechts im Abstand 40 bis 150 Hz (um 0 bis 10 dB = Rauschen, über 20 dB = stehender Teilton); bei zusammenhang-* nicht gemessen (Ende ist der Treffer, kein Aufladen); Energie > 400 Hz = Anteil an der Fensterenergie.

| Datei | Länge s | Spitze dBFS | Mittel dB | Einsätze 10/30 ms | Ende dBFS | Spitze > 400 Hz dB (Hz) | Hervortreten dB | Energie > 400 Hz dB | Spektrogramm |
|---|---|---|---|---|---|---|---|---|---|---|
| aufladen-a-spannung-steht.wav | 1.55 | -15.5 | -29.1 | 2 / 1 | -180.0 | -18.3 (471) | 12.0 | -10.3 | -21.6 (436) / 11.0 / -13.1 | [Bild](spektrogramme/aufladen-a-spannung-steht.png) |
| aufladen-b-faustschlag.wav | 1.31 | -8.8 | -29.1 | 3 / 0 | -180.0 | -31.0 (404) | 16.3 | -36.0 | -33.4 (404) / 15.1 / -38.2 | [Bild](spektrogramme/aufladen-b-faustschlag.png) |
| aufladen-c-luftstoss.wav | 1.55 | -12.0 | -29.1 | 11 / 6 | -180.0 | -9.4 (404) | 11.4 | -10.5 | -10.6 (404) / 14.9 / -14.8 | [Bild](spektrogramme/aufladen-c-luftstoss.png) |
| zusammenhang-a.wav | 2.91 | -3.9 | -24.7 | 6 / 3 | -90.3 | - | - | - | - | [Bild](spektrogramme/zusammenhang-a.png) |
| zusammenhang-b.wav | 2.67 | -3.9 | -24.5 | 7 / 2 | -90.3 | - | - | - | - | [Bild](spektrogramme/zusammenhang-b.png) |
| zusammenhang-c.wav | 2.91 | -3.9 | -24.7 | 15 / 8 | -90.3 | - | - | - | - | [Bild](spektrogramme/zusammenhang-c.png) |
| aufladen-a-knarzen.wav (Runde 1, zum Vergleich) | 1.40 | -11.4 | -29.1 | 3 / 1 | -180.0 | -15.1 (662) | 49.1 | -14.8 | -10.9 (662) / 48.0 / -10.9 | [Bild](spektrogramme/runde1-aufladen-a-knarzen.png) |
| aufladen-b-brummen.wav (Runde 1, zum Vergleich) | 1.60 | -14.3 | -29.1 | 4 / 0 | -180.0 | -26.1 (525) | 43.0 | -27.8 | -14.3 (525) / 52.6 / -17.4 | [Bild](spektrogramme/runde1-aufladen-b-brummen.png) |
| aufladen-c-ratsche.wav (Runde 1, zum Vergleich) | 1.55 | -10.0 | -29.1 | 11 / 5 | -180.0 | -19.5 (619) | 15.4 | -15.2 | -11.8 (616) / 54.3 / -11.6 | [Bild](spektrogramme/runde1-aufladen-c-ratsche.png) |
