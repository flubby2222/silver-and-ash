# Messwerte grosses Tor der Bossraum-Tuer (#549)

Gemessen, nicht gehoert. Erzeugt von `messen.py`. Zeitbasis: Bewegungsentwurf (60 Ticks/s, 1 Tick = 735 Samples = 16,67 ms).

## Pegel und Spektrum

| Datei | Dauer s | Spitze dBFS | Mittel (RMS) dBFS | Schwerpunkt Hz | 95 % der Energie unter Hz | Energie 200-800 Hz dB | Energie > 1 kHz dB | Energie > 3 kHz dB | Letzte 50 ms dBFS |
|---|---|---|---|---|---|---|---|---|---|
| r2-tor-oeffnen-1-geroell-fein.wav | 3.00 | -8.8 | -24.0 | 345 | 991 | -3.5 | -13.1 | -53.5 | -200.0 |
| r2-tor-oeffnen-2-geroell-grob.wav | 3.20 | -8.8 | -24.0 | 184 | 624 | -7.5 | -18.3 | -56.9 | -200.0 |
| r2-tor-oeffnen-3-geroell-mit-starkem-grollen.wav | 4.20 | -7.7 | -24.0 | 153 | 366 | -7.2 | -22.6 | -62.2 | -200.0 |
| r2-tor-schliessen-1-grollen-kurzer-schlag.wav | 2.73 | -2.5 | -20.8 | 79 | 215 | -12.1 | -36.4 | -74.7 | -200.0 |
| r2-tor-schliessen-2-starkes-grollen-langer-nachklang.wav | 4.63 | -2.5 | -20.7 | 99 | 237 | -10.0 | -32.8 | -71.2 | -200.0 |
| r2-tor-schliessen-3-grollen-mit-schutt-rieseln.wav | 4.23 | -2.5 | -22.6 | 81 | 226 | -11.4 | -33.2 | -71.4 | -200.0 |
| tor-oeffnen-1-kurzes-knarren.wav | 3.00 | -8.8 | -24.0 | 151 | 377 | -6.4 | -45.8 | -78.8 | -200.0 |
| tor-oeffnen-2-langes-knarren.wav | 4.60 | -10.6 | -24.0 | 165 | 377 | -5.3 | -30.8 | -78.1 | -200.0 |
| tor-oeffnen-3-mit-steinschleifen.wav | 3.40 | -11.3 | -24.0 | 149 | 355 | -8.3 | -37.6 | -78.6 | -200.0 |
| tor-schliessen-1-dumpfer-schlag.wav | 2.73 | -2.5 | -20.0 | 78 | 205 | -13.0 | -45.0 | -82.2 | -200.0 |
| tor-schliessen-2-mit-nachhall.wav | 4.63 | -2.5 | -22.1 | 81 | 205 | -12.8 | -45.1 | -80.2 | -200.0 |
| tor-schliessen-3-mit-bodengrollen.wav | 4.03 | -2.5 | -23.2 | 81 | 183 | -13.7 | -46.7 | -79.2 | -200.0 |

Schwerpunkt und Energieanteile aus dem mittleren Spektrum (Hann 4096). Energie > x kHz als dB unter der Gesamtenergie.

## Schliessen: sitzt der Anschlag bei Tick 78 (1.30 s)?

Sollzeit 78 Ticks = 57330 Samples = 1300.0 ms. Einsatz = 32-Sample-Block mit dem staerksten Pegelanstieg gegen die 8 ms davor, im Fenster +-60 ms.

| Datei | lautestes Sample bei s | Einsatz bei ms | Abweichung ms | Anstieg dB |
|---|---|---|---|---|
| r2-tor-schliessen-1-grollen-kurzer-schlag.wav | 1.313 | 1301.0 | +1.0 | 15.5 |
| r2-tor-schliessen-2-starkes-grollen-langer-nachklang.wav | 1.335 | 1346.7 | +46.7 | 12.4 |
| r2-tor-schliessen-3-grollen-mit-schutt-rieseln.wav | 1.313 | 1301.0 | +1.0 | 14.9 |
| tor-schliessen-1-dumpfer-schlag.wav | 1.312 | 1301.0 | +1.0 | 16.2 |
| tor-schliessen-2-mit-nachhall.wav | 1.337 | 1297.3 | -2.7 | 9.1 |
| tor-schliessen-3-mit-bodengrollen.wav | 1.337 | 1301.0 | +1.0 | 13.6 |

Die zwei kleinen Rueckpraller (Anschlag +14 und +22 Ticks, Pegel 0,20 und 0,06 des Schlags) sind im Nachklang nicht verlaesslich messbar und werden hier nicht belegt; sie sind im Skript als eigene Schlaege gesetzt (`kern_schlag`).

## Oeffnen: Verlauf folgt der Bewegung

Bewegung: 132 Ticks (2,20 s), Anlauf 30 Ticks (0,50 s), Auslauf ab Tick 92 (1,53 s). Es gibt keinen Anschlag. Gemessen wird der Pegel je 100 ms.

| Datei | lautestes 100-ms-Fenster bei s | Pegel Anlauf (0 bis 0,5 s) dB | Pegel Plateau (0,5 bis 1,53 s) dB | Pegel Auslauf (1,53 bis 2,2 s) dB | lautestes Sample bei s |
|---|---|---|---|---|---|
| r2-tor-oeffnen-1-geroell-fein.wav | 0.9 | -26.8 | -20.6 | -25.7 | 0.946 |
| r2-tor-oeffnen-2-geroell-grob.wav | 0.7 | -24.4 | -20.6 | -25.3 | 1.083 |
| r2-tor-oeffnen-3-geroell-mit-starkem-grollen.wav | 1.4 | -24.6 | -19.3 | -23.9 | 0.547 |
| tor-oeffnen-1-kurzes-knarren.wav | 0.6 | -22.0 | -20.8 | -35.9 | 0.592 |
| tor-oeffnen-2-langes-knarren.wav | 1.2 | -24.3 | -19.4 | -21.7 | 0.574 |
| tor-oeffnen-3-mit-steinschleifen.wav | 1.3 | -24.3 | -20.3 | -25.0 | 0.491 |

## Pegel je 100 ms (RMS dBFS)

- r2-tor-oeffnen-1-geroell-fein.wav: -47 -34 -29 -25 -22 -21 -20 -20 -21 -20 -22 -20 -21 -20 -21 -22 -21 -24 -27 -33 -40 -51 -72 -84 -95 -200 -200 -200 -200 -200
- r2-tor-oeffnen-2-geroell-grob.wav: -50 -41 -31 -20 -22 -23 -20 -19 -22 -20 -20 -22 -21 -22 -21 -22 -19 -27 -25 -35 -41 -46 -59 -66 -73 -81 -88 -96 -200 -200 -200 -200
- r2-tor-oeffnen-3-geroell-mit-starkem-grollen.wav: -58 -38 -27 -22 -21 -20 -21 -19 -20 -19 -19 -20 -20 -19 -16 -20 -20 -22 -25 -29 -34 -35 -37 -42 -45 -48 -49 -53 -56 -59 -63 -65 -67 -72 -74 -78 -81 -82 -88 -93 -106 -200
- r2-tor-schliessen-1-grollen-kurzer-schlag.wav: -59 -47 -40 -34 -32 -28 -25 -25 -23 -20 -20 -18 -17 -9 -16 -20 -29 -31 -43 -48 -51 -52 -73 -89 -200 -200 -200
- r2-tor-schliessen-2-starkes-grollen-langer-nachklang.wav: -52 -42 -37 -32 -29 -25 -23 -21 -19 -18 -16 -14 -13 -10 -13 -15 -16 -19 -22 -24 -26 -30 -31 -34 -35 -36 -39 -42 -45 -47 -51 -51 -53 -58 -59 -63 -63 -66 -67 -72 -74 -76 -82 -90 -94 -200
- r2-tor-schliessen-3-grollen-mit-schutt-rieseln.wav: -57 -48 -39 -35 -33 -29 -27 -23 -25 -22 -21 -20 -18 -9 -15 -19 -26 -29 -37 -41 -46 -50 -56 -53 -55 -59 -58 -55 -56 -60 -54 -68 -73 -81 -85 -92 -98 -109 -200 -200 -200 -200
- tor-oeffnen-1-kurzes-knarren.wav: -32 -29 -23 -20 -19 -18 -17 -17 -19 -20 -22 -27 -30 -32 -30 -31 -33 -34 -38 -46 -57 -76 -89 -100 -200 -200 -200 -200 -200 -200
- tor-oeffnen-2-langes-knarren.wav: -37 -36 -28 -23 -19 -18 -20 -20 -19 -20 -23 -20 -18 -18 -20 -18 -18 -20 -25 -27 -29 -33 -34 -37 -39 -42 -47 -48 -52 -53 -56 -57 -62 -63 -65 -68 -70 -71 -72 -76 -80 -81 -86 -92 -101 -200
- tor-oeffnen-3-mit-steinschleifen.wav: -34 -36 -26 -22 -20 -20 -22 -23 -21 -20 -20 -19 -20 -19 -20 -22 -22 -22 -24 -29 -37 -45 -55 -61 -65 -75 -84 -87 -91 -98 -200 -200 -200 -200
- tor-schliessen-1-dumpfer-schlag.wav: -50 -42 -34 -31 -28 -26 -25 -26 -25 -24 -21 -18 -18 -8 -14 -19 -26 -30 -41 -42 -48 -51 -67 -88 -200 -200 -200
- tor-schliessen-2-mit-nachhall.wav: -52 -41 -35 -30 -31 -27 -27 -26 -23 -24 -22 -19 -19 -9 -14 -16 -20 -21 -25 -27 -27 -30 -32 -36 -39 -39 -42 -44 -45 -47 -51 -54 -58 -58 -61 -63 -65 -66 -68 -72 -75 -76 -82 -88 -94 -200
- tor-schliessen-3-mit-bodengrollen.wav: -51 -41 -37 -35 -31 -28 -28 -28 -25 -25 -21 -19 -21 -12 -14 -17 -19 -23 -24 -26 -29 -31 -33 -35 -37 -40 -42 -44 -46 -48 -49 -52 -54 -65 -69 -75 -86 -94 -104 -200

## Spektrogramme

- `r2-tor-oeffnen-1-geroell-fein.png` (Frequenz logarithmisch, 20 Hz bis 22 kHz)
- `r2-tor-oeffnen-2-geroell-grob.png` (Frequenz logarithmisch, 20 Hz bis 22 kHz)
- `r2-tor-oeffnen-3-geroell-mit-starkem-grollen.png` (Frequenz logarithmisch, 20 Hz bis 22 kHz)
- `r2-tor-schliessen-1-grollen-kurzer-schlag.png` (Frequenz logarithmisch, 20 Hz bis 22 kHz)
- `r2-tor-schliessen-2-starkes-grollen-langer-nachklang.png` (Frequenz logarithmisch, 20 Hz bis 22 kHz)
- `r2-tor-schliessen-3-grollen-mit-schutt-rieseln.png` (Frequenz logarithmisch, 20 Hz bis 22 kHz)
- `tor-oeffnen-1-kurzes-knarren.png` (Frequenz logarithmisch, 20 Hz bis 22 kHz)
- `tor-oeffnen-2-langes-knarren.png` (Frequenz logarithmisch, 20 Hz bis 22 kHz)
- `tor-oeffnen-3-mit-steinschleifen.png` (Frequenz logarithmisch, 20 Hz bis 22 kHz)
- `tor-schliessen-1-dumpfer-schlag.png` (Frequenz logarithmisch, 20 Hz bis 22 kHz)
- `tor-schliessen-2-mit-nachhall.png` (Frequenz logarithmisch, 20 Hz bis 22 kHz)
- `tor-schliessen-3-mit-bodengrollen.png` (Frequenz logarithmisch, 20 Hz bis 22 kHz)
