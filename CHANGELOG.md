# Changelog

Alle wichtigen Änderungen an Silver & Ash, pro Version. Format nach [Keep a Changelog](https://keepachangelog.com/de/1.1.0/), Versionsnummern nach [Semantic Versioning](https://semver.org/lang/de/).

Neue Einträge kommen zuerst unter **Unveröffentlicht**. Beim Release wird daraus ein Abschnitt mit Versionsnummer und Datum (siehe [docs/release.md](docs/release.md)).

## [Unveröffentlicht]

### Hinzugefügt
- Karte auf **M** (halten): zeigt von oben die schon betretenen Räume und Gänge, ihre Türen (zu, offen, vergittert, Boss) und wo man steht und hinschaut. Das Spiel läuft dabei weiter
- Endbildschirm „YOU DIED“ bzw. „VICTORY“ mit der Statistik des Durchlaufs (Zeit, getötete Ghule und Fledermäuse, Schaden ausgeteilt und genommen, erreichte Stufe, geräumte Räume, Seed) und der Wahl „New run“ / „Title“. Jeder Durchlauf wird außerdem als Zeile in `%LOCALAPPDATA%\SilverAndAsh\runs.log` festgehalten
- Titelbildschirm mit „Start“ und „Quit“
- Esc-Menü: weiterspielen, Mausempfindlichkeit einstellen (bleibt gespeichert), zurück zum Titel, beenden. Solange es offen ist, steht das Spiel still
- **Durchläufe auf Ebene 0 „The Catacombs“:** Das Spiel startet auf einer zufällig erzeugten Ebene aus handgebauten Räumen, mit Gängen und Schleifen, der Bossraum am weitesten vom Start. Beim Start erscheint kurz der Name der Ebene. Nach dem Tod beginnt ein neuer Durchlauf mit neuem Grundriss. Ist der Bossraum geräumt, ist der Durchlauf gewonnen. Mit `--seed <Zahl>` immer derselbe Grundriss; die alten Testlevel gibt es weiter mit `--level test` / `--level heights`
- Türen zwischen den Räumen der erzeugten Ebenen: Alle sind zu und halten Spieler, Gegner, Sicht und Schüsse auf. Mit **F** öffnet man die Tür, auf die man aus der Nähe schaut („[F] Open“); offen bleibt offen. Die Tür zum Bossraum ist dunkel, mit Schädeln und rotem Schimmer
- Kampfräume werden zur Arena: Beim ersten Betreten fallen an allen Türen Gitter, die schlafenden Gegner erwachen. Sind alle tot, heben sich die Gitter und die Tür, durch die man kam, öffnet sich
- Je weiter ein Kampfraum vom Start entfernt ist, desto mehr Ghule und Fledermäuse warten darin; der Bossraum ist voll besetzt. Einstellbar in `balance.json` unter `difficulty`

### Geändert
- Nach dem Tod startet der nächste Durchlauf nicht mehr von selbst – erst der Endbildschirm, dann „New run“. Das große „YOU DIED“ mitten im Bild entfällt, nur das Bild färbt sich rot
- Esc gibt nicht mehr nur die Maus frei, sondern öffnet das Menü; ein Klick ins Bild fängt die Maus nicht mehr ein (das macht „Resume“). Auch wenn das Fenster den Fokus verliert, öffnet sich das Menü
- Ist die Silberklinge voll aufgeladen, füllt sich die Ausdauer wieder auf, auch wenn man die Taste weiter hält. Während des Ladens selbst füllt sie sich weiterhin nicht

## [0.7.0] – 2026-11-15 – Stufen & Beute

Kämpfen lohnt sich: Gegner lassen Blutphiolen und Geweihte Munition fallen, Kills bringen Erfahrung, und jeder Aufstieg bietet eine Wahl – dazu alle Werte in einer Datei zum Nachjustieren.

### Hinzugefügt
- Balancing-Datei `assets/balance.json`: Leben, Schaden, Abklingzeiten, Geschwindigkeiten und Zeiten lassen sich ändern, ohne das Spiel neu zu bauen – mit Kommentaren zu jedem Wert. Fehler in der Datei halten das Spiel nicht auf: Es nimmt dann die Standardwerte und schreibt eine Warnung ins Log. In der Testversion lädt F5 die Datei neu. Anleitung: `docs/balancing.md`
- Blutphiolen: Getötete Ghule lassen in 30 % der Fälle, Fledermäuse in 15 % eine Blutphiole fallen – eine kleine, rot pulsierende Flasche, die über dem Boden schwebt. Drüberlaufen heilt 50 Leben (nie über das Maximum), neben der Lebensanzeige steigt „+50“ auf. Bei vollem Leben bleibt sie liegen
- Geweihte Munition: Etwa jeder dritte Drop sind statt einer Blutphiole drei Patronen mit silbern leuchtenden Spitzen. Aufheben (auch mit vollem Leben) lädt sie sofort: 10 s lang schießt die Pistole doppelt so schnell (alle 0,4 statt 0,8 s), der Schaden bleibt gleich. Der Lauf schimmert hell, der Mündungsblitz wird weiß-blau, über der Lebensanzeige läuft „CONSECRATED 10s“ ab (blinkt in den letzten 3 s). Nochmal aufheben startet die Zeit neu; der Tod beendet sie
- Erfahrung und Stufen: Wer einen Gegner tötet, bekommt Erfahrung – 30 für einen Ghul, 20 für eine Fledermaus. Stufe 2 braucht 100, jede weitere ein Viertel mehr als die vorige (100, 125, 156, 195, 244 …); was übrig bleibt, zählt für die nächste. Unten links stehen Stufe und ein goldener Erfahrungsbalken. Der Tod setzt wieder auf Stufe 1
- Stufenaufstieg mit Wahl: Bei jedem Aufstieg hält das Spiel an und „LEVEL UP – choose a boon“ bietet drei zufällige Boni aus einem Pool von vier – kleine Schritte, jeweils vom Grundwert gerechnet und beliebig oft wählbar: Ausdauer-Regeneration +5 %, maximales Leben +5 (sofort geheilt), Schaden +5 % mit allen Waffen, Erfahrung +2,5 %. Jeder Kasten zeigt auch, wie viel man schon hat. Wahl mit 1 / 2 / 3 oder Mausklick (Klicks zählen erst nach einem kurzen Moment, damit ein Angriffsklick nicht aus Versehen wählt). Mehrere Aufstiege auf einmal: nacheinander wählen. Der Tod nimmt alle Boni

## [0.6.0] – 2026-11-14 – Vampirfledermaus

Der Angriff kommt von oben: Vampirfledermäuse kreisen um dich, spucken Blutgeschosse und stoßen herab – alles mit Vorwarnung, alles ausweichbar.

### Hinzugefügt
- Vampirfledermäuse in beiden Testleveln (im Level-Format: `B`): klein, mit schlagenden Flügeln, schweben sie über deinem Kopf – du läufst unter ihnen durch. 30 Leben: ein Pistolenschuss oder ein Klingenhieb, wenn du zu ihr hochschaust. Getroffen blinken sie, tot trudeln sie zu Boden und verschwinden kurz darauf
- Blutgeschosse: glühend rote Kugeln, die man fliegen sieht (12 m/s) und denen man ausweichen kann – Seitschritt, Sprung oder Dodge (geduckt fliegen sie über dich hinweg). Ein Treffer kostet 15 Leben; an Wänden zerplatzen sie. Gegner und Puppen treffen sie nicht
- Fledermäuse greifen an: Sie hängen still, bis sie dich sehen (bis 15 m) oder getroffen werden. Dann kreisen sie um dich – jede mit eigenem Abstand (4–6 m), eigener Richtung und eigenem Auf und Ab –, wechseln alle 2–4 s zufällig die Richtung und finden im Flug ihren Weg um Ecken. Jedes Spiel und jeder Neustart fliegt anders. Etwa alle 2 s spucken sie ein Blutgeschoss – vorher leuchten sie kurz rot auf. Etwa alle 6 s kreischen sie (Flügel hoch) und stoßen dann auf die Stelle herab, an der du beim Kreischen standest: Wer jetzt zur Seite geht oder dodged, lässt sie ins Leere stoßen, sonst beißen sie für 15 Leben. Im Sturzflug sind sie tief – ein guter Moment für die Klinge

### Geändert
- Die Silberklinge trifft nahe Gegner leichter: Ihr Treffkegel beginnt jetzt etwas hinter dir und ist dadurch direkt vor dir schon gut einen Meter breit – auch Gegner seitlich vor dir werden getroffen. Reichweite (2 m) und Winkel (90°) bleiben

## [0.5.0] – 2026-11-11 – Ghul

Der erste echte Gegner: Ghule lauern in den Gängen, jagen dich um Ecken und schlagen zurück – und zum ersten Mal kannst du sterben.

### Hinzugefügt
- Ghule in beiden Testleveln (im Level-Format: `G`): kriechende Gestalten, niedriger als du, mit dem Kopf vorn. 80 Leben – zwei Klingenhiebe oder ein Pistolenschuss auf den Kopf. Sie blinken bei Treffern (blutrot bei Kopfschuss), zucken zurück und brechen bei 0 Leben zur Seite zusammen; kurz darauf ist der Körper verschwunden. Sie lauern still, bis sie dich in weniger als 12 m sehen oder getroffen werden. Dann kriechen sie dir nach (4,5 m/s, etwas langsamer als du), finden den Weg um Ecken und durch Türen, richten sich 0,5 s lang drohend auf und schlagen zu: 20 Schaden, bis 2,4 m weit. Ein kleiner Schritt zurück reicht nicht, wer richtig ausweicht oder dodget, wird verfehlt. Danach brauchen sie kurz Erholung
- Schaden und Tod: Bei jedem Treffer leuchten die Bildränder rot auf und die Kamera zuckt kurz, die Lebensanzeige sinkt. Bei 0 Leben färbt sich das Bild rot, „YOU DIED“ erscheint, und nach 2 s beginnt das Level von vorn – mit vollem Leben und allen Gegnern wie am Anfang

## [0.4.0] – 2026-11-11 – Waffen & HUD

Silber gegen Stroh: Klinge und Pistole treffen Trainingspuppen – mit Kopfschüssen, aufgeladenen Schlägen, Waffen im Bild und einem HUD.

### Hinzugefügt
- Trainingspuppen aus Holz und Stroh in beiden Testleveln (im Level-Format: `D`). Man läuft nicht durch sie hindurch. Sie haben 100 Leben, blinken bei Treffern hell auf, kippen bei 0 vom Spieler weg um und stehen nach 3 s wieder auf
- Silberklinge (linke Maustaste): trifft alles in einem Kegel vor dir – 2 m weit, 90° breit, auch nach oben und unten –, aber nicht durch Wände. 40 Schaden, ein Schlag alle 0,4 s; drei Treffer werfen eine Trainingspuppe um
- Pistole mit Silberkugeln (rechte Maustaste): trifft sofort genau dort, wohin du schaust – das erste Ziel bis 50 m. 35 Schaden, ein Schuss alle 0,8 s; drei Treffer werfen eine Trainingspuppe um. An Wänden, Boden und Decke bleibt ein Einschlag zurück: kurzer Funke, dann einige Sekunden ein dunkler Fleck
- Silberklinge aufladen: Linke Maustaste halten, beim Loslassen wird zugeschlagen. Der Schaden steigt linear bis 200 % nach 1 s Halten (80 statt 40), das kostet bis zu einem Stamina-Segment. Reicht die Stamina nicht, lädt die Klinge nur so weit wie möglich. Die Klinge holt dabei sichtbar aus und zittert, wenn sie voll ist
- Kritische Treffer: Ein Pistolenschuss auf den Kopf einer Trainingspuppe macht 300 % Schaden (105) und wirft sie mit einem Schuss um; die Puppe blitzt dabei blutrot statt weiß auf. Die Silberklinge bekommt keinen Zonenbonus
- HUD für den Kampf: Fadenkreuz in der Bildmitte; links und rechts davon zeigen kleine Balken, ob Klinge und Pistole bereit sind; darunter ein Ladebalken beim Aufladen der Klinge (golden, wenn voll); unten links das Leben
- Waffen im Bild: links die Silberklinge, rechts die Pistole (Platzhalter aus Grundformen). Sie bewegen sich weich, als würde man durch die Welt gleiten: ruhiges Wippen beim Laufen, Nachziehen beim Umsehen und Seitwärtslaufen, Mitnicken beim Landen. Die Klinge schwingt beim Schlag quer durchs Bild, die Pistole kickt beim Schuss nach oben, vorn blitzt die Mündung

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

[Unveröffentlicht]: https://github.com/flubby2222/silver-and-ash/compare/v0.7.0...HEAD
[0.7.0]: https://github.com/flubby2222/silver-and-ash/compare/v0.6.0...v0.7.0
[0.6.0]: https://github.com/flubby2222/silver-and-ash/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/flubby2222/silver-and-ash/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/flubby2222/silver-and-ash/compare/v0.3.2...v0.4.0
[0.3.2]: https://github.com/flubby2222/silver-and-ash/compare/v0.3.1...v0.3.2
[0.3.1]: https://github.com/flubby2222/silver-and-ash/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/flubby2222/silver-and-ash/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/flubby2222/silver-and-ash/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/flubby2222/silver-and-ash/releases/tag/v0.1.0
