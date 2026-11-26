# Hörprobe Stake Crossbow (Pflock-Armbrust), Runde 2 (Issue #203)

Vier Kategorien, je drei Fassungen A, B, C als eigene WAV-Datei. Hör in dieser Reihenfolge und sag je Kategorie: **welche Fassung, und was stört?** Alles per Skript (`bauen.py`) synthetisiert, nichts heruntergeladen. **Alles gemessen, nichts gehört.** Runde 1 (Branch `feat/stake-crossbow-hoerprobe-203`) ist unabhängig davon entstanden; diese Runde ist bewusst anders gebaut (siehe unten).

Mono, 16 Bit, 44 100 Hz, Tiefpass 2,8 kHz, nichts Hohes oder Glockiges. Messwerte, Spektrogramme und Vergleich mit Bestand und Runde 1: `messwerte.md`.

| Kategorie | Datei | Aufbau |
|---|---|---|
| 1 Schuss | `schuss-a-sehne-zischen.wav` | Tiefes Seil schnellt (78 Hz), dumpfer Schlag, danach fällt ein Rauschband von 1,5 kHz auf 320 Hz (Pflock fliegt weg) |
| | `schuss-b-dumpfer-stoss.wav` | Nur Druck (42 Hz) und kurzes Holzklacken; keine Sehne, kein Zischen; kürzeste, tiefste Fassung |
| | `schuss-c-schiene-schleift.wav` | Schlag, dann gleitet der Pflock rauh und körnig über die Holzschiene (450 auf 900 Hz), zuletzt ein Tock |
| 2 Treffer Fleisch | `treffer-fleisch-a-stich.wav` | Knapper Anschlag (520 Hz), runder Schlag 70 auf 48 Hz, feuchtes Nachgleiten |
| | `treffer-fleisch-b-knirschen.wav` | Tiefer Körper (55 Hz) und neun kleine Körner (Knochen, Sehnen) in 0,11 s, am Ende dumpfer Austritt |
| | `treffer-fleisch-c-reissen.wav` | Rauhes Rauschband (230 steigt auf 520 Hz, zittert mit 65 Hz) unter einem kurzen Schlag (60 Hz) |
| 3 Treffer hart | `treffer-hart-a-holz-splittert.wav` | Knacken (1,1 kHz), Körper 130 Hz, sechs kleine Splitter |
| | `treffer-hart-b-stein-bricht.wav` | Breiter Geräuschstoß ohne Tonhöhe, tiefer Schlag, drei fallende Brocken |
| | `treffer-hart-c-schabt-ab.wav` | Pflock prallt ab und kratzt: harter Anschlag, dann körniges Schaben (1,3 kHz fällt auf 500 Hz) |
| 4 Nachladen (2,5 s) | `nachladen-a-kurbel.wav` | Griff, Ratsche (etwa 16 Klicks, enger und höher werdend, bis 520 Hz) über leisem Zug, Einrasten, Pflock einlegen |
| | `nachladen-b-seilzug.wav` | Griff, ein einziger langer Zug (Seil knarrt, tiefer Ton 48 auf 105 Hz steigt), keine Klicks, Einrasten, Einlegen |
| | `nachladen-c-zwei-zuege.wav` | Griff, zwei Züge mit leisem Rasten dazwischen, Einrasten, Einlegen |

Zusatzdateien: `dreierfolge-fleisch-a|b|c.wav` (derselbe Treffer dreimal im Abstand von 40 ms, zum Anhören und zur Übersteuerungsprobe) und `dehnung/` (Nachladen bei 1,5 s und 3,5 s).

## Dreierfolge Fleisch

Die Treffer sind kurz (0,36 bis 0,42 s) und legen ihre Energie in die ersten 30 ms; der Pegel ist so gesetzt, dass **jede der 27 Kombinationen** dreier Treffer im Abstand von 40 ms höchstens -1,5 dBFS erreicht (gemessen -1,50 dBFS). Die Einzeltreffer liegen dafür bei -6,2 bis -7,7 dBFS Spitze, also etwas leiser als Axt (-4,5); das Mittel ist gleich (-22,1 dB, Axt -20,9).

## Nachladen auf eine Zeit aus balance.json strecken

`nachladen(variante, T)` in `bauen.py` nimmt die Gesamtdauer T. Griff (0 s), Einrasten (T - 0,6 s) und Einlegen (T - 0,4 s) behalten ihre Länge, nur der Zug dazwischen (T - 0,7 s) wird gestreckt: bei A bleibt die Klickdichte (etwa 9 je Sekunde) und es kommen Klicks hinzu oder fallen weg, bei B und C wird der Zug länger oder kürzer. Beispiele in `dehnung/` für 1,5 s (Pistole) und 3,5 s. Zur Laufzeit würde das Spiel nicht strecken, sondern die Datei für den Wert aus `balance.json` vorab bauen; ändert sich `reloadTime`, wird sie neu gebaut. Ein Stake-Crossbow-Eintrag steht noch nicht in `balance.json`; Pistole 1,5 s, Gewehr 2,5 s, Schrotflinte 1,8 s.

## Unterschied zu Runde 1 und zum Bestand (gemessen)

Schwerpunkt in Hz. Bestand: Pistole 297, `p1-schuss-1` 1424, `p1-nachladen-start` 2388, Axt Fleisch 165, Axt hart 180. Runde 1: Schuss 181 bis 270, Fleisch 75 bis 162, hart 204 bis 580, Spannen 205 bis 457. Runde 2: Schuss 74 bis 216, Fleisch 127 bis 187, hart 160 bis 238, Nachladen 111 bis 303. Runde 2 ist durchweg dunkler als Pistole und Schuss-Bestand und enthält keinen Anteil über 3 kHz, der in die Nähe des Bestands käme (Anteil über 3 kHz -21,8 bis -38 dB gegen -8,6 bis -17,7 dB beim Schuss-Bestand). Von der Axt unterscheidet sich Fleisch weniger im Schwerpunkt als in Länge (0,36 bis 0,42 s gegen 0,56 s) und Aufbau.

## Nachbau

`python bauen.py` (Dateien, zwei Läufe bitgleich laut `wav.sha256`), `python messen.py [Ordner Runde 1]` (Messwerte und Spektrogramme, braucht `ffmpeg`).
