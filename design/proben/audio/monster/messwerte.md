# Messwerte Monsterklaenge (Issue 392)

Gemessen, **nicht gehoert**. Mono, 44 100 Hz, 16 Bit. Lautheit (RMS) je Klang an die alte Datei angeglichen; Spitze hoechstens -1,5 dBFS. Baender = Energie in dB unter Gesamtenergie. Einsaetze = Stellen mit Pegelanstieg > 8 dB in 50 ms (Beginn zaehlt mit), Fenster 10 ms / 30 ms. Spektrogramme: `bilder/`.

| Datei | Laenge (s) | Spitze (dBFS) | Mittel (dB, ffmpeg) | alt: Mittel (dB) | <300 Hz | 300-3k Hz | >3k Hz | Einsaetze 10/30 ms |
|---|---|---|---|---|---|---|---|---|
| ghul-ankuendigung-a.wav | 1.00 | -2.4 | -20.2 | -20.2 | -3.1 | -3.9 | -17.5 | 2 / 1 |
| ghul-ankuendigung-b.wav | 1.00 | -3.5 | -20.2 | -20.2 | -25.2 | -0.8 | -10.0 | 1 / 1 |
| ghul-entdecken-a.wav | 0.90 | -4.7 | -19.8 | -19.8 | -3.4 | -3.7 | -20.1 | 1 / 1 |
| ghul-entdecken-b.wav | 0.90 | -3.1 | -19.8 | -19.8 | -6.8 | -1.5 | -22.6 | 12 / 4 |
| ghul-sterben-a.wav | 1.30 | -1.5 | -21.9 | -21.4 | -6.8 | -1.8 | -13.2 | 1 / 1 |
| ghul-sterben-b.wav | 1.30 | -5.5 | -21.9 | -21.4 | -19.1 | -0.8 | -10.6 | 2 / 1 |
| fledermaus-spuck-ankuendigung-a.wav | 0.70 | -6.8 | -23.5 | -23.5 | -49.3 | -2.5 | -5.4 | 0 / 0 |
| fledermaus-spuck-ankuendigung-b.wav | 0.70 | -5.3 | -23.5 | -23.5 | -9.8 | -0.9 | -19.1 | 6 / 1 |
| fledermaus-spucken-a.wav | 0.30 | -10.3 | -28.9 | -28.9 | -37.0 | -3.7 | -3.6 | 1 / 1 |
| fledermaus-spucken-b.wav | 0.20 | -10.9 | -28.9 | -28.9 | -3.9 | -3.4 | -13.3 | 1 / 1 |
| fledermaus-kreischen-a.wav | 0.55 | -1.5 | -18.2 | -8.3 | -21.8 | -2.9 | -4.7 | 1 / 1 |
| fledermaus-kreischen-b.wav | 0.55 | -2.7 | -18.2 | -8.3 | -11.9 | -1.1 | -20.0 | 3 / 0 |
| fledermaus-flattern-a.wav | 0.22 | -17.3 | -31.0 | -31.0 | -15.2 | -0.6 | -16.4 | 2 / 1 |
| fledermaus-flattern-b.wav | 0.22 | -16.1 | -31.0 | -31.0 | -8.4 | -1.6 | -13.7 | 1 / 1 |
| fledermaus-biss-a.wav | 0.25 | -6.9 | -28.4 | -28.4 | -2.4 | -5.1 | -12.6 | 1 / 1 |
| fledermaus-biss-b.wav | 0.25 | -11.0 | -28.4 | -28.4 | -10.5 | -1.4 | -11.7 | 1 / 1 |
| fledermaus-treffer-a.wav | 0.30 | -13.5 | -28.9 | -28.9 | -27.0 | -2.5 | -5.4 | 1 / 1 |
| fledermaus-treffer-b.wav | 0.30 | -13.2 | -28.9 | -28.9 | -11.1 | -1.2 | -15.8 | 1 / 1 |
| fledermaus-sterben-a.wav | 0.70 | -1.5 | -17.5 | -15.9 | -20.1 | -0.6 | -13.8 | 1 / 1 |
| fledermaus-sterben-b.wav | 0.70 | -1.6 | -17.5 | -15.9 | -19.3 | -2.6 | -5.3 | 1 / 1 |
| fledermaus-fehlsturz-a.wav | 0.45 | -13.7 | -28.0 | -28.0 | -22.3 | -1.0 | -9.2 | 1 / 1 |
| fledermaus-fehlsturz-b.wav | 0.45 | -14.5 | -28.0 | -28.0 | -4.7 | -2.5 | -26.5 | 1 / 1 |
| gorm-bruellen-a.wav | 1.50 | -9.7 | -16.4 | -16.4 | -2.5 | -4.8 | -14.6 | 2 / 1 |
| gorm-bruellen-b.wav | 1.50 | -7.5 | -16.4 | -16.4 | -1.4 | -6.8 | -17.9 | 1 / 2 |
| gorm-treffer-a.wav | 0.35 | -13.1 | -26.0 | -26.0 | -1.9 | -5.8 | -17.7 | 1 / 1 |
| gorm-treffer-b.wav | 0.35 | -9.6 | -26.0 | -26.0 | -6.5 | -1.8 | -23.7 | 1 / 1 |
| gorm-sterben-a.wav | 1.50 | -9.4 | -18.7 | -17.4 | -2.8 | -4.3 | -13.3 | 1 / 1 |
| gorm-sterben-b.wav | 1.50 | -1.5 | -18.7 | -17.4 | -3.8 | -3.2 | -26.1 | 3 / 1 |
| gorm-ankuendigung-hammer-a.wav | 1.00 | -1.5 | -18.2 | -13.0 | -18.1 | -0.3 | -23.0 | 1 / 1 |
| gorm-ankuendigung-hammer-b.wav | 1.00 | -8.2 | -18.2 | -13.0 | -1.9 | -5.6 | -17.2 | 6 / 1 |

Ankuendigungen (Regel 2 der Audio-Bible: genau ein Einsatz): ghul-ankuendigung, fledermaus-spuck-ankuendigung, fledermaus-kreischen, gorm-ankuendigung-hammer.
