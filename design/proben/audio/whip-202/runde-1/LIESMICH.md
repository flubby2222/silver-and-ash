# Hörprobe Whip (Lederpeitsche), Runde 1 (Issue #202)

Jede Fassung ist **eine eigene WAV-Datei** mit sprechendem Namen. Hör sie in dieser Reihenfolge (Gruppe für Gruppe, je A, B, C) und sag mir je Gruppe: **welche Fassung, und was stört?**

Alles ist per Skript (`bauen.py`) synthetisiert, nichts heruntergeladen. **Alles gemessen, nichts gehört.** Die Angriffe sind die aus dem Issue: Schlag mit langer Reichweite, Heranziehen von Ghulen und Fledermäusen, Aufladen (bricht rissige Wände). Ein Peitschenknall ist von Natur aus hell; deshalb ist er hier kurz (Knallspitze 1 bis 4 ms) und die Energie über 4 kHz ist begrenzt: Tiefpass 4. Ordnung bei 3,0 kHz (Schlag A, Treffer Stein A) bzw. 2,4 kHz (alles andere). Gemessen liegt der Pegel über 4 kHz in allen Dateien mindestens 21,4 dB unter dem Gesamtpegel (siehe Spalte unten und `messwerte.md`). Keine tonalen Schlussereignisse: Einrasten, Klopfen und Tock sind tiefe Schläge unter 200 Hz oder Rauschen mit niedriger Güte, kein Ding.

Bestand zum Vergleich (aus `assets/audio`): `p1-schwung-1.wav` (0,50 s, Spitze -9,0 dBFS, Mittel -29,5 dB, über 4 kHz -5,5 dB); `p1-treffer-1.wav` (0,45 s, Spitze -4,5 dBFS, Mittel -27,6 dB, über 4 kHz -3,2 dB); `klinge-1.wav` (0,24 s, Spitze -4,9 dBFS, Mittel -23,7 dB, über 4 kHz -16,0 dB).

Messwerte: Länge in s, Spitze in dBFS, Mittel in dB (ffmpeg `volumedetect`), über 4 kHz = Pegel der Anteile über 4 kHz relativ zum Gesamtpegel in dB. Ausführlicher in `messwerte.md`, Bilder in `spektrogramme/`.

## 1. Schlag (Ausholen und Knall)

| Datei | Beschreibung | Länge s | Spitze dBFS | Mittel dB | über 4 kHz dB |
|---|---|---|---|---|---|
| `schlag-a-scharfer-knall.wav` | Kurzes Ausholen (Rauschband steigt 0,16 s an), dann ein kurzer harter Knall (Rauschklick plus Körper 2 kHz, 50 ms) mit kleinem Schlag darunter. | 0,60 | -6,0 | -28,4 | -21,6 |
| `schlag-b-dumpfer-lederschlag.wav` | Langsameres tiefes Ausholen (0,3 s), der Aufschlag ist ein dumpfer, nasser Lederklatsch um 700 Hz mit Schlag 85 Hz, kein Klick. | 0,70 | -6,0 | -27,0 | -40,3 |
| `schlag-c-langes-zischen.wav` | Langes Zischen (0,55 s, steigt von 0,9 auf 2,8 kHz), dann ein kleiner kurzer Knall und ein Rest Zischen, der ausläuft. | 0,93 | -6,0 | -22,9 | -21,4 |

## 2. Treffer in Fleisch

| Datei | Beschreibung | Länge s | Spitze dBFS | Mittel dB | über 4 kHz dB |
|---|---|---|---|---|---|
| `treffer-fleisch-a-klatsch.wav` | Trockener Lederklatsch: Rauschstoß um 900 Hz, Schlag 110 Hz, kurzes Knacken des Lederendes. | 0,35 | -4,5 | -21,3 | -42,8 |
| `treffer-fleisch-b-striemen.wav` | Knacken des Lederendes, dann zieht das Leder über die Haut (Reibe-Rauschen fällt von 1,1 auf 0,35 kHz), leiser Schlag darunter. | 0,29 | -4,5 | -19,2 | -36,9 |
| `treffer-fleisch-c-nass-dumpf.wav` | Runder tiefer Schlag 65 Hz, feuchtes Schmatzen (Rauschband 400 auf 200 Hz, grob gekörnt), weicher Stoß um 600 Hz. | 0,40 | -4,5 | -19,4 | -49,1 |

## 3. Treffer auf Stein oder Holz

| Datei | Beschreibung | Länge s | Spitze dBFS | Mittel dB | über 4 kHz dB |
|---|---|---|---|---|---|
| `treffer-hart-a-stein-knallt.wav` | Harter Knall des Lederendes auf Stein (Klick, Körper 1,6 kHz) mit trockenem Tock aus Rauschen um 420 Hz und Schlag 140 Hz. | 0,20 | -4,5 | -24,5 | -33,3 |
| `treffer-hart-b-holz-klopft.wav` | Leder auf Holzbalken: dumpfer Klopf 190 auf 150 Hz, klingt in 50 ms aus, knappe Rauschspitze, tiefer Schlag 70 Hz. | 0,33 | -4,5 | -22,5 | -49,5 |
| `treffer-hart-c-stein-rieselt.wav` | Kleiner Knall auf Stein, Schlag 70 Hz, danach rieselt Staub (gekörntes Rauschen 0,3 bis 1,5 kHz, fällt in 0,3 s ab). | 0,45 | -4,5 | -20,5 | -43,3 |

## 4. Aufladen (der geladene Schlag, der rissige Wände bricht)

| Datei | Beschreibung | Länge s | Spitze dBFS | Mittel dB | über 4 kHz dB |
|---|---|---|---|---|---|
| `aufladen-a-leder-knarrt.wav` | Leder knarrt unter Zug (gekörntes Rauschband 180 auf 420 Hz, wird lauter), Brummen 55 auf 85 Hz, am Ende zieht es fest: tiefer Schlag 60 Hz. | 1,35 | -12,1 | -26,8 | -50,3 |
| `aufladen-b-wirbeln.wav` | Das Leder kreist durch die Luft: Rauschband 300 auf 900 Hz pulst immer schneller (3 auf 11 Hz) und wird lauter, endet ohne Schlag im weichen Abfall. | 1,28 | -10,0 | -26,8 | -32,9 |
| `aufladen-c-spannen.wav` | Dunkles gedehntes Reiben (120 auf 260 Hz) mit Brummen 50 auf 80 Hz und drei kurzen Lederquietschern, endet mit tiefem Schlag 50 Hz. | 1,40 | -11,5 | -26,8 | -49,7 |

## 5. Heranziehen (Ghul oder Fledermaus wird herangezogen)

| Datei | Beschreibung | Länge s | Spitze dBFS | Mittel dB | über 4 kHz dB |
|---|---|---|---|---|---|
| `heranziehen-a-ruck.wav` | Kurzes Reißen: Rauschband fällt von 1,5 kHz auf 300 Hz, dazu ein dumpfer Schlag 75 Hz (Ruck am Ende der Leine). | 0,36 | -6,0 | -21,5 | -36,0 |
| `heranziehen-b-schleifen.wav` | Ein Körper schleift über Stein: gekörntes Rauschband 500 auf 200 Hz, fällt gleichmäßig ab, leiser Stoß 60 Hz am Anfang. | 0,51 | -6,0 | -23,3 | -36,2 |
| `heranziehen-c-lederruck.wav` | Die Leine spannt sich: kurzes gekörntes Rauschband 260 auf 520 Hz mit Schlag 90 Hz, die kürzeste der drei. | 0,33 | -6,0 | -23,3 | -44,7 |

## Zusammenhang (Schlag, dann Treffer in Fleisch derselben Fassung)

| Datei | Beschreibung | Länge s | Spitze dBFS | Mittel dB | über 4 kHz dB |
|---|---|---|---|---|---|
| `zusammenhang-a.wav` | Schlag A (scharfer Knall), 20 ms nach dem Knall Treffer in Fleisch A (Klatsch). | 0,60 | -3,2 | -21,9 | -28,1 |
| `zusammenhang-b.wav` | Schlag B (dumpfer Lederschlag), 20 ms nach dem Knall Treffer in Fleisch B (Striemen). | 0,70 | -5,6 | -21,5 | -37,8 |
| `zusammenhang-c.wav` | Schlag C (langes Zischen), 20 ms nach dem Knall Treffer in Fleisch C (nass-dumpf). | 0,97 | -4,1 | -20,1 | -24,4 |

## Hinweise zu den Messwerten

- Spitzen: Schlag und Heranziehen -6, Treffer -4,5, Aufladen -10 dBFS (Ziele); die Aufladen-Fassungen sind auf gleiche mittlere Lautstärke gebracht (nur nach unten gerechnet), bei Schlag, Treffern und Heranziehen sind die Spitzen gleich, die mittlere Lautstärke unterscheidet sich (ein kurzer Knall hat naturgemäß ein tiefes Mittel).
- Alle Dateien laufen auf Stille aus (letzte 50 ms: höchster Wert -55,5 dBFS, Soll höchstens -50). Mono, 16 Bit, 44 100 Hz. Längste Datei: 1,40 s.
- Der Einsatzzähler in `messwerte.md` zählt bei tiefen Tönen mehr als einen Einsatz (Perioden über 6 ms, das 30-ms-Fenster zeigt den wahren Wert). Beim Schlag sind Ausholen und Knall absichtlich eine Geste (das Ausholen schwillt bis zum Knall an); die Aufladen-Fassungen A und C enden mit einem tiefen Schlag, B ohne.
- Schlag B (0,70 s) und Schlag C (0,93 s) sowie alle Aufladen- und Zusammenhang-Dateien liegen über den 0,6 s, die die Audio Bible für Schuss und Treffer nennt; das Ausholen gehört dort zur Geste (Länge samt Hall-Nachlauf). Alle Treffer und Heranziehen-Dateien bleiben unter 0,6 s.
- Zwei Läufe von `bauen.py` ergeben bitgleiche Dateien (`wav.sha256`).
- Nachbau: `python bauen.py` (Dateien), `python messen.py` (Messwerte, Spektrogramme, diese Datei und `wav.sha256`, braucht `ffmpeg`).
