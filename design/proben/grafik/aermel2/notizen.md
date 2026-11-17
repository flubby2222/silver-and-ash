# Drei Entwürfe für die Waffenärmel (Issue #265)

Fotos im Spiel (`room-corridor`, `pistol-recoil`, `blade-charged`), je „heute“ (Stand von `main`, Mantelärmel aus PR #262), a, b, c. Blätter (2 × 2: links oben heute, rechts oben a, links unten b, rechts unten c): `blatt_gang.png` (ganze Bilder), `blatt_pistole.png`, `blatt_klinge_ruhe.png`, `blatt_klinge_aufgeladen.png` (Ausschnitte, doppelt vergrößert). Modelle: `modelle_a|b|c/` (Namen wie im Spiel: `pistol.glb`, `pistol_hand.glb`, `blade.glb`, `blade_hand.glb`, `pistol.png`, `blade.png`; Knoten und Drehpunkte unverändert). Skripte: `pistol.py`, `blade.py`, `arm.py` (Entwurf per Umgebungsvariable `AERMEL=a|b|c`), `fotos.sh <x>`, `blaetter.py`. Regeln aus der Recherche: `recherche.md`.

| Entwurf | Was | Hand | Dreiecke Arm |
|---|---|---|---|
| **a** Leder und Stahl | enger schwarzer Lederärmel mit Naht und Faltenbahnen, **Armschiene** aus Stahlblech mit Längsrippen und Nieten am Handgelenk, schwarzer **Lederhandschuh** (große Formen, zweite Fassung des Handschuhs) | ja | Pistole 376, Klinge 396 |
| **b** Wolle mit Stulpe | weiter grauer Wollärmel mit Falten, **breite umgeschlagene Stulpe in Ocker** mit heller Naht und drei Silberknöpfen | nein | je 168 |
| **c** Spitze | dunkelblauer Ärmel, **helle Rüschenmanschette** mit zackigem Rand und Spitzenmuster | nein | je 168 |

## Rangfolge und Begründung
1. **c (Spitze), wenn ohne Hand gewünscht.** Stärkste eigene Erscheinung: Der helle Zackenrand setzt die Hand sofort ab und liest sich bei der Klinge als Rüschenmanschette. Passt zu Gotik und Jäger, wie ein Offiziersrock des 18. Jahrhunderts. Schwäche: bei der Pistole sieht man die Manschette von oben als flache Scheibe mit Strahlen.
2. **a (Leder und Stahl).** Als Arm am besten lesbar (die Faust und der Unterarm zeigen Richtung und Länge), die Armschiene gibt den Materialwechsel. Es ist aber wieder eine Hand, und der Mensch hat Hände abgelehnt; ob ein schwarzer, schlichter Handschuh anders wirkt, kann nur er sagen.
3. **b (Wolle mit Stulpe).** Sauber, aber der Ocker der Stulpe ist braun und steht nahe am Schaft der Pistole (Regel 3 der Recherche); bei der Pistole die schwächste Lesbarkeit der drei.

## Was mir nicht gefällt
- **Pistole b und c:** Von oben ist die Öffnung eine Scheibe mit unruhiger Textur (Ocker bzw. Spitze) und nicht ein sauberer Ring. Nach drei Anläufen aufgehört; die Lösung wäre eine andere Lage des Griffs im Bild (Code) oder ein Ring aus eigener Geometrie mit eigenem Texturfeld.
- **Klinge aufgeladen:** das Heft liegt vor dem Auge (siehe #263); in b und c ist das braune Eck unten weiter zu sehen.
- **a:** der Handschuh ist groß und dunkel, dadurch klumpig; die Armschiene sieht man bei der Klinge kaum.
- Die Recherche ist dünn: Text ohne Bilder, die Regeln stützen sich zum Teil auf mein Wissen (so gekennzeichnet).
