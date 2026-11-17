# Probestück 12: Kisten mit sichtbarer Textur (#258)

**Dateien** (`modelle/`, Namen wie im Spiel): `chest_beggar|guard|king|vampire.glb` und `.png`. Knoten (`base`, `lid`, `lock`), Drehpunkte (Deckel (0, 0,455, −0,365), Schloss (0, 0,47, 0,39)), Größe, Kollision und Geometrie sind unverändert; **geändert sind nur die vier Texturen** (64 × 64, jetzt 32 statt 24 Farben).
**Skripte:** `chest.py` (aus `tools/models/chest.py`, nur die Maler neu), `fotos.sh neu`, `vergleich.py`. Fotos `alt/` und `neu/`: `chests-4m`, `room-treasure`.

## Was neu gemalt ist
- **Bretter:** je Brett eigener Ton, Jahresringe als Streifen entlang des Brettes, Astlöcher, eine senkrechte Stoßfuge je Brett, tiefe Fugen mit heller Kante, Nägel an den Seiten.
- **Abnutzung:** hellere Kanten links und rechts, Schmutz unten, dunkle Flecken.
- **Eisenbänder:** heller Rand (Kante liest sich), größere Nieten mit Schatten darunter, Rostflecken.
- **Stufen** (neben Geometrie und Farbe, die schon unterschieden): Beggar rohes Holz, Guard Holz mit Glanzbändern wie Blech, King Leisten und Zierfelder in Gold, Vampire Laufspuren von Blut von oben.

## Was mir nicht gefällt
- Das Bild ist **rustikaler, aber nicht deutlich anders** aus 4 m: Die Kisten sind im Foto nur 110 bis 130 Pixel breit, Maserung und Fugen kommen als Unruhe, nicht als erkennbare Bretter an. Die Bänder und Nieten lesen sich besser als das Holz.
- Guard und King wirken zu bunt und fleckig statt wie Blech und Gold; mehr braucht ein größeres Texturfeld (128 × 128 statt 64 × 64, Art Bible sagt für Requisiten 64) oder weniger, dafür größere Muster.
- Der Deckel ist aus nur einem Brettfeld über die Wölbung gestreckt; die Maserung läuft dort verzerrt.
- Die Stufen unterscheiden sich hauptsächlich weiter durch Farbe und Aufsatz (Henkel, Krone, Dornen), nicht durch das Material.

---

# Zweite Runde (neue Richtung der Leitung: gedeckte Töne, Holz mit Metall)

**Geändert gegenüber Runde 1:** Texturatlas 128 × 128 (statt 64 × 64), 32 Farben; neue Felder `lidplanks` und `lidband` für den Deckel (UV des Deckels nach Bogenlänge neu gelegt: zehn Bretter laufen über die Wölbung, Bänder mit 14 Nieten statt gestreckt). Geometrie, Knoten, Drehpunkte, Größe unverändert. Skript `chest.py` (Runde 1 liegt im Git davor).
**Töne:** Beggar graues rissiges Holz, rostige schmale Bänder (nur im mittleren Streifen des Bandfelds, daneben Holz); Guard dunkle Eiche, graue Eisenbänder mit großen Nieten und Eckbeschlägen, Stahlblau nur als Akzent; King dunkles poliertes Holz mit Glanzbahn, Messingbänder, Perlen, Messinglinien als Zierkante, Gold nur an Bändern, Schloss und Krone; Vampire schwarz gebeiztes Holz (54, 45, 58), Eisen, tiefrote Einlage in den Bändern und rotes Siegel am Schloss, rote Spitzen. Bretter: drei bis vier je Seite, dunkle Fugen mit heller Kante darunter, zwei bis drei lange Maserlinien je Brett, kein Rauschen.

**Was mir nicht gefällt:**
- **Vampire** ist aus 4 m sehr dunkel; der Akzent liest sich nur als feine rote Linien. Silhouette (Dornen) trägt die Unterscheidung. Wenn er mehr auffallen soll, Holz etwas heller oder mehr Rot am Deckel.
- **Beggar:** die Risse wirken auf der Front wie schwarze Haken, nicht wie Maserung.
- Die vier Stufen sind jetzt im Foto nur noch durch Ton, Metall und Aufsatz unterscheidbar, nicht mehr „auf einen Blick“ durch Farbe; das ist gewollt, aber gegenüber den Spielfarben in `ChestView.cs` (Kisten-Rahmen, Beute-Anzeige) ein Bruch, den der Einbau prüfen muss.
- Die Deckelbretter sind aus 4 m kaum zu erkennen; die Bänder lesen sich dort besser.
