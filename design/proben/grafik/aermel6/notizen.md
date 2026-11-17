# Fassung g (Issue #265): Ärmel mit Rüschenmanschette, keine Hand

Entscheidung des Menschen: „Keine Hände“, Entwurf C. Blatt `blatt_C_g.png` (links C aus Probestück 13, rechts g; oben Gang, Mitte Schuss, unten aufgeladene Klinge). Modelle `modelle_g/`, Fotos `fotos_g/`, Skripte `pistol.py`, `blade.py`, `arm.py` (Fassung g am Ende), `fotos.sh`, `blatt.py`. Haltung: **die von `main`, unverändert**; mit den g-Dateien in `assets/models/` laufen die 15 Tests `WeaponPoseTests` grün (die Tests lesen die Dateien aus `assets/models/`).

## Umsetzung der Punkte
1. **Keine Hand:** ein Rohr je Arm, 240 Dreiecke.
2. **Pistolenärmel wie der Klingenärmel:** Die Manschette der Pistole liegt waagerecht um Griff und Schloss, ihr Mund zeigt nach vorn, die Waffe ragt heraus; das Rohr läuft zurück und dann schräg nach rechts unten aus der Bildecke. Man sieht auf die Außenseite der Manschette, nicht in die Öffnung. Spiegelbildlich zur Klinge.
3. **Länger:** Beide Rohre sind rund 0,7 m lang und enden weit außerhalb des Bildes; in Ruhe, Rückstoß und aufgeladen ist kein Ende, keine Innenseite und kein braunes Eck zu sehen. Schwung habe ich nicht fotografiert; der Test zu Anschnitt und Reichweite läuft grün.
4. **Details:** drei Lagen Rüsche (gebrochenes Weiß, dunkle Schattenlinie unter jeder Lage, kaum Rauschen), schmale Borte am Übergang, zwei Silberknöpfe am Schlitz, drei lange Faltenbahnen. Nicht mehr.
5. **Haltung:** unverändert gegenüber `main`.
6. **Eigene Palette:** der Arm hat seine eigene 64 × 64-Textur mit 32 Farben (eingebettet in `*_hand.glb`).

## Was noch nicht stimmt
- Die Manschetten sind große helle Blöcke und beherrschen die untere Bildhälfte; die Waffe wirkt kleiner. Kleinere Radien (etwa 0,028 statt 0,034) würden helfen.
- Die Rüsche liest sich als drei geschichtete Wülste, kaum noch als Spitze (bewusst ruhiger, wie verlangt).
- Zwischen Manschette und Bildrand ist nur wenig Ärmel zu sehen (Pistole etwa 6 cm, Klinge 12 cm); mehr geht nur mit einer anderen Haltung.
- Die Pistole ist im Gang nur noch ein Lauf mit Schloss: der Griff und der Kolben liegen in der Manschette.

## Für künftige Modelle
**Hände modellieren wir nicht; Arme enden in Kleidung.** Wenn eine Figur oder ein Ich-Sicht-Teil eine Hand bräuchte, wird sie von einem Ärmel, einer Manschette oder einem Handschuhrand verdeckt; die Waffe ragt aus der Öffnung. Die Hand selbst zu bauen hat in vier Anläufen (Handschuhe a, d, e, f) nicht überzeugt. Die Manschette ist dabei die Form, die wie ein Ring um das Gerät liest; ihr Mund soll von der Kamera abgewandt sein, damit man die Außenseite sieht.
