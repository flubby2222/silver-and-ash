# Changelog

Alle wichtigen Änderungen an Silver & Ash, pro Version. Format nach [Keep a Changelog](https://keepachangelog.com/de/1.1.0/), Versionsnummern nach [Semantic Versioning](https://semver.org/lang/de/).

Neue Einträge kommen zuerst unter **Unveröffentlicht**. Beim Release wird daraus ein Abschnitt mit Versionsnummer und Datum (siehe [docs/release.md](docs/release.md)).

## [Unveröffentlicht]

## [0.3.2] – 2026-11-11 – Fenster-Fix

Eine Fehlerkorrektur für 0.3.0.

### Behoben
- Beim Start auf einem Bildschirm links vom Hauptbildschirm war das Fenster unsichtbar (weit außerhalb aller Bildschirme) und die Maus eingefangen. Liegt das Fenster nach dem Start auf keinem Bildschirm, wird es jetzt auf den Hauptbildschirm geholt

## [0.3.1] – 2026-11-11 – fehlerhaftes Release

Versehentlich auf dem Stand von 0.3.0 veröffentlicht – enthält den Fenster-Fix **nicht**. Bitte 0.3.2 verwenden.

## [0.3.0] – 2026-11-11 – PS2-Look

Das Schloss bekommt Stimmung: grobe Pixel, Licht wie von einer Laterne, Dunkelheit in der Ferne – und erstmals Höhenunterschiede. Dazu wird die Bewegung zum Können: Perfect Jump, Stamina, Dash in der Luft und Dodge am Boden.

### Geändert
- Retro-Look: Die 3D-Welt wird in 854 × 480 gezeichnet und mit harten Pixelkanten hochskaliert; Texte bleiben scharf
- Das Fenster lässt sich in der Größe ändern; das Bild behält sein Seitenverhältnis (schwarze Balken bei abweichender Form)
- Flat Shading: Wände, Boden und Decke haben je nach Ausrichtung eine eigene Helligkeit; Ecken sind ohne Kantenlinien erkennbar
- Sprung schwebender: 1,3 m hoch und 0,8 s in der Luft, mehr Kontrolle in der Luft und ein kleiner Tempo-Schub beim Springen
- Räume sind 3,5 m hoch (vorher 3 m), damit der höhere Sprung nicht an die Decke stößt

### Hinzugefügt
- Laternenlicht und Nebel: In der Nähe ist es hell, ab etwa 18 m versinkt alles in violett-schwarzem Dunkel
- Level-Format kennt Podeste (`1`, `2`, `3`) und Gruben (`_`); Beschreibung in `docs/level-format.md`
- Stufen, Podeste und Gruben werden gezeichnet; man steigt Treppen hinauf, springt auf hohe Podeste und aus Gruben heraus
- Zweites Testlevel „heights“ mit Treppe, hohem Podest und Grube – starten mit `--level heights`
- Landungs-Nicken: Beim Aufkommen sackt der Blick kurz und kräftig ab (normaler Sprung etwa 12 cm, 0,3 s) und neigt sich – je tiefer der Fall, desto stärker
- Perfect Jump: Wer kurz vor oder nach dem Aufkommen erneut springt, bekommt +20 % Tempo (nicht stapelbar); die Landung fällt dabei sanfter aus
- Stamina-Balken (unten in der Mitte) mit 5 Segmenten: Jeder Sprung kostet eins. Er lädt nur am Boden, in 3 s von leer bis voll. Ein Sprung mit leerem Balken ist erschöpft: 20 % langsamer als Laufen (4,8 statt 6 m/s) bis zum Ende der Sprungkette
- Dash in der Luft mit der linken Umschalttaste: 4 m in 0,15 s in Laufrichtung (ohne Richtung nach vorn), ohne Schwerkraft. Einmal pro Sprung, kostet ein Stamina-Segment. Wer am Ende des Dash springt, bekommt bei der Landung einen Perfect Jump
- Dodge am Boden mit der linken Umschalttaste: 0,4 s geduckt und schnell (10 m/s, etwa 4 m) in Laufrichtung, frei lenkbar (ohne Taste behält er die Richtung); die Kamera sinkt dabei weich ab. Kostet zwei Stamina-Segmente, währenddessen kein Aufladen. Springen nur am Ende: Wer kurz vor oder nach dem Ende des Dodge springt, bekommt einen Perfect Jump – und kann in dessen Luftphase wieder dashen

## [0.2.0] – 2026-11-10 – Bewegung & Physik

Zum ersten Mal ein begehbarer Ort: Du stehst in einem Level, siehst dich um, läufst und springst.

### Hinzugefügt
- Level aus Textdateien (Raster aus 2 × 2 m großen Zellen mit eigener Boden- und Deckenhöhe), erstes Testlevel mit vier Räumen
- Darstellung des Levels mit Wänden, Boden und Decke
- Ego-Perspektive: Umsehen mit der Maus (nicht invertiert, Blick nach oben und unten begrenzt)
- Laufen mit WASD (6 m/s, kurzes Anfahren und Abbremsen), flüssig auch auf Monitoren über 60 Hz
- Kollision mit Wänden: Man gleitet an Wänden entlang und rutscht um Türkanten herum
- Schwerkraft und Springen mit der Leertaste (ca. 1 m hoch), in der Luft eingeschränkt lenkbar
- Esc gibt die Maus frei, ein Klick ins Fenster fängt sie wieder ein

### Geändert
- Esc beendet das Spiel nicht mehr – Beenden über das X des Fensters
- Kein Konsolenfenster mehr hinter dem Spiel
- Texte im Spiel sind englisch

### Entfernt
- Der drehende Würfel aus 0.1.0

## [0.1.0] – 2026-11-10 – Walking Skeleton

Das technische Grundgerüst: noch kein Spiel, aber alles, was ein Spiel braucht, um entwickelt, geprüft und ausgeliefert zu werden.

### Hinzugefügt
- Fenster mit drehendem Würfel als erstes Lebenszeichen des Spiels
- Feste Zeitschritte: Die Simulation läuft mit 60 Ticks pro Sekunde, unabhängig von der Bildrate
- Technisches Log unter `%LOCALAPPDATA%\SilverAndAsh\logs\` mit Absturzprotokoll
- Versionsnummer unten rechts im Spiel
- Automatische Prüfung (CI) jedes Pull Requests
- Automatisches Windows-Paket bei jedem Versions-Tag

[Unveröffentlicht]: https://github.com/flubby2222/silver-and-ash/compare/v0.3.2...HEAD
[0.3.2]: https://github.com/flubby2222/silver-and-ash/compare/v0.3.1...v0.3.2
[0.3.1]: https://github.com/flubby2222/silver-and-ash/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/flubby2222/silver-and-ash/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/flubby2222/silver-and-ash/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/flubby2222/silver-and-ash/releases/tag/v0.1.0
