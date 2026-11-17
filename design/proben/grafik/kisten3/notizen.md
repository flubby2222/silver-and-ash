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
