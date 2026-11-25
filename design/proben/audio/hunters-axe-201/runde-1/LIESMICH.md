# Hörprobe Hunter's Axe (Pfahlaxt), Runde 1 (Issue #201)

Jede Fassung ist **eine eigene WAV-Datei** mit sprechendem Namen. Hör sie in dieser Reihenfolge (Kategorie für Kategorie, je A, B, C) und sag mir je Kategorie: **welche Fassung, und was stört?**

Alles ist per Skript (`bauen.py`) synthetisiert, nichts heruntergeladen. **Alles gemessen, nichts gehört.** Die Axt ist bewusst dunkler und tiefer als Klinge und Keule im Bestand: Schläge bei 40 bis 70 Hz, Tiefpass 2,8 kHz, keine Töne über 400 Hz. Bestand zum Vergleich: Schwung `p1-schwung-1.wav` (0,50 s, Spitze -9,0 dBFS, Mittel -29,5 dB), Treffer `p1-treffer-1.wav` (0,45 s, Spitze -4,5, Mittel -27,6). Die Spitzen der Axt liegen gleich hoch, das Mittel 5 bis 7 dB höher (dichte Tiefe statt kurzer heller Spitze).

Messwerte: Länge in s, Spitze in dBFS, Mittel in dB (ffmpeg `volumedetect`). Ausführlicher in `messwerte.md`, Bilder in `spektrogramme/`.

## 1. Hieb durch die Luft (schwerer Schwung)

| Datei | Beschreibung | Länge | Spitze | Mittel |
|---|---|---|---|---|
| `hieb-a-tiefes-rauschen.wav` | Breites Luftband fährt von 520 auf 190 Hz, darunter ein Druckton 70 auf 48 Hz; schwillt langsam an, läuft weich aus. Nur Luft, kein Ton. | 0,75 | -9,0 | -22,7 |
| `hieb-b-eisenkopf.wav` | Der Eisenkopf summt metallisch mit (95 Hz, unharmonische Teiltöne), Tonhöhe fällt nach dem Gipfel wie beim Vorbeiziehen; dazu schmales Luftband. | 0,80 | -10,8 | -22,7 |
| `hieb-c-luftdruck.wav` | Kürzer und gedrungener, fast nur Tiefe: Druckwelle 48 auf 34 Hz, Gipfel früh, langer weicher Abfall. | 0,60 | -10,6 | -22,7 |

## 2. Aufladen (Spannung baut sich auf, dann rastet es hörbar ein)

Die Spannung wächst 1,0 s lang, dann kommt das Einrasten (Eisenschlag). Alle Töne unter 400 Hz.

| Datei | Beschreibung | Länge | Spitze | Mittel |
|---|---|---|---|---|
| `aufladen-a-knarzen.wav` | Holz und Leder knarzen unter Zug (Rauschband 150 auf 320 Hz, rauh), darunter Brummen 62 auf 96 Hz; Einrasten: Eisenschlag 150 Hz mit tiefem Ring. | 1,40 | -11,4 | -29,1 |
| `aufladen-b-brummen.wav` | Nur ein glatter Dröhnton, der steigt (48 auf 105 Hz) und lauter wird, kein Rauschen; Einrasten: tiefer Glockenschlag mit Eisenring. | 1,60 | -14,3 | -29,1 |
| `aufladen-c-ratsche.wav` | Dumpfe Holzschläge, die schneller (150 auf 42 ms Abstand), höher (120 auf 260 Hz) und lauter werden und zur Dauer verschmelzen; Einrasten: schwerer Eisenschlag 140 Hz. | 1,55 | -10,0 | -29,1 |

## 3. Treffer in Fleisch (wuchtig)

| Datei | Beschreibung | Länge | Spitze | Mittel |
|---|---|---|---|---|
| `treffer-fleisch-a-dumpf.wav` | Sehr tiefer Schlag 90 auf 40 Hz, nasses Rauschen (320 Hz), kurzes Knirschen der Schneide. | 0,56 | -4,6 | -20,9 |
| `treffer-fleisch-b-knochen.wav` | Tiefer Schlag 46 Hz, hölzernes Knacken (Knochen bricht), danach dröhnt der Stiel kurz nach (125 Hz). | 0,56 | -7,0 | -20,9 |
| `treffer-fleisch-c-nass.wav` | Rauschband 480 auf 220 Hz mit grobem Schmatzen, runder Schlag 55 Hz, nachziehender Schwall. | 0,56 | -4,5 | -20,9 |

## 4. Treffer auf Stein oder Holz (Fehlschlag an der Wand)

| Datei | Beschreibung | Länge | Spitze | Mittel |
|---|---|---|---|---|
| `treffer-hart-a-funkenschlag.wav` | Eisen auf Stein: harter Klang (210 Hz, unharmonisch bis 1,4 kHz) mit tiefem Schlag, danach rieseln leise Rauschkörner (Funken). | 0,40 | -5,5 | -22,2 |
| `treffer-hart-b-stiel-prallt.wav` | Axt in Balken oder Holz: dumpfer breiter Schlag, kurzes Splittern, Holzstiel dröhnt lange nach (150 Hz). | 0,63 | -4,5 | -22,2 |
| `treffer-hart-c-stein-broeckelt.wav` | Tiefer Schlag 60 Hz mit hartem Anschlag, danach bröckelt es: Rauschen unter 1 kHz in groben Körnern, ohne Ton. | 0,54 | -4,8 | -22,2 |

## Zusammenhang (Aufladen, Pause 0,35 s, Hieb; der Treffer in Fleisch setzt auf dem Gipfel des Hiebs ein)

Jede Fassung nutzt ihre drei Buchstaben-Teile (A mit A usw.), Treffer = Fleisch. Spitze auf höchstens -1,5 dBFS begrenzt (nur nach unten gerechnet).

| Datei | Enthält | Länge | Spitze | Mittel |
|---|---|---|---|---|
| `zusammenhang-a.wav` | tiefes Rauschen, Knarzen, dumpf | 2,76 | -3,6 | -24,7 |
| `zusammenhang-b.wav` | Eisenkopf, Brummen, Knochen | 2,96 | -5,4 | -24,2 |
| `zusammenhang-c.wav` | Luftdruck, Ratsche, nass | 2,91 | -3,9 | -24,7 |

## Hinweise zu den Messwerten

- Innerhalb einer Kategorie sind A, B, C auf gleiche mittlere Lautstärke gebracht (nur nach unten gerechnet); deshalb liegt manche Spitze unter dem Ziel.
- Alle Dateien laufen auf Stille aus (letzte 50 ms unter -50 dBFS, gemessen -54 bis -180 dBFS). Mono, 16 Bit, 44 100 Hz.
- Der Einsatzzähler in `messwerte.md` zählt bei tiefen Tönen und bei der Ratsche mehr als einen Einsatz: bei den tiefen Tönen sind es Perioden über 6 ms (das 30-ms-Fenster zeigt den wahren Wert), bei `aufladen-c-ratsche.wav` ist der Puls gewollt. Das Einrasten am Ende jeder Aufladung ist ein zweites Ereignis, wie gewünscht.
- Zwei Läufe von `bauen.py` ergeben bitgleiche Dateien (`wav.sha256`).
- `treffer-hart-b-stiel-prallt.wav` ist mit 0,63 s etwas länger als die 0,6 s der Audio Bible für Treffer (der Stiel-Nachklang ist die Idee dieser Fassung).
- Nachbau: `python bauen.py` (Dateien), `python messen.py` (Messwerte und Spektrogramme, braucht `ffmpeg`).
