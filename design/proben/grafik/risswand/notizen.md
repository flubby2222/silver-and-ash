# Rissige Wand (Issue #246), Probestück

Blätter (Spielfotos, Debug-Build `--photo`, Körnung und Nebel; Waffen im Bild):
- `blatt.png`: Spalten Variante A dezent, A deutlich, B dezent, B deutlich; Zeilen 2 m, 4 m, 10 m. Links und rechts der Risszelle liegt jeweils normale Wand derselben Variante (Wandreihe mit der Zelle in der Mitte).
- `blatt_variante_c_und_geheimraum.png`: oben Variante C bei 4 m (dezent, deutlich); darunter die Wand **von der Seite des Geheimraums** aus 1 m (A dezent, A deutlich, B dezent, B deutlich).
- Einzelbilder in `fotos/` (18). Skripte: `risswand.py` (Modelle und Texturen), `patch_lokal3.py` + `fotos3.py` (lokale Foto-Hilfe, stellt `src` und `assets` wieder her), `montage.py`.

## Vorschlag: eine Platte je Wandvariante, mit der echten Wand-Textur
Der Shader hat keine Transparenz, also kann man keine weichen Risse über die Wand legen (die schwarzen Linien aus Türen-Runde 1 waren Geometrie und wirkten deshalb aufgemalt). Stattdessen gilt:
- Die Textur ist die **echte Wand-Textur** (`catacomb_wall.png`, `_b`, `_c`, nur gelesen), 2 × 2 Mal gekachelt: 128 × 128 Pixel = 2 m × 2 m, 64 px je Meter wie die Wand. Nur dort, wo ich Risse, Abplatzung oder Staub gemalt habe, ist ein Pixel anders; überall sonst ist es **bitgleich** mit der Wand. So setzt die Platte die Wand ohne Naht fort (im Foto sieht man den Rand der Platte nicht).
- Das geht, weil die Wand-Texturkoordinaten an der Welt hängen (eine Kachel je Meter, `SurfaceTexturing`) und die Zelle 2 m breit auf geraden Metern sitzt. Die Platte ist 1 cm vor beiden Zellflächen angebracht (z = ±1,006 bis ±1,010).
- **Eine Fassung je Wandvariante** (a, b, c) und je Stärke: sechs Modelle `crack_<variante>_<stärke>.glb` mit eingebetteter Textur. Dass es die Variante des Spiels ist, schreibt die Zelle selbst vor: `SurfaceVariants.At(door.X, door.Z, SurfaceKind.Wall)` (die Türzelle sieht aus wie der Raum auf ihrer Innenseite).

| Knoten | Inhalt | Dreiecke |
|---|---|---|
| `panel` (Drehpunkt 0;0;0) | die Platte, zwei Quader 2 m × 2 m × 4 mm vor den beiden Flächen; Rückseite gespiegelt, damit die Wand-Kachel von beiden Seiten richtig herum liegt | 24 |
| `debris` (0;0;0) | kleine Brocken am Fuß, vor beiden Seiten (dezent 3 je Seite, deutlich 7 je Seite); Textur ist ein unbemaltes Stück Wand | 120 / 280 |
| `chunk` (0;0;0) | **ein** Brocken um den Ursprung, etwa 0,25 m, als Vorlage für den Bruch-Effekt (#185): `WallBreakView` zeichnet ihn statt der Farb-Quader, skaliert und gedreht wie bisher, mit der Stein-Textur der Zelle | 12 |

Textur: 128 × 128, **31 Farben** (24 der Wand plus 7 für Riss tief, Riss Rand, helle Kante, Abplatzung, Schatten, Bruchkante, Staub).

## Die zwei Stärken
- **Dezent:** ein dünner, gezackter Riss von etwa 0,8 m Länge in Randfarbe mit wenigen tiefen Pixeln, ein kurzer Zweig, eine winzige Abplatzung, wenig Staub am Fuß, 3 Brocken je Seite. Aus 2 m gut, aus 4 m mit Aufmerksamkeit zu finden, aus 10 m nicht zu sehen.
- **Deutlich:** tiefer, 1 bis 2 Pixel breiter Riss von etwa 1,2 m mit zwei Zweigen, heller Kante, deutlicher Abplatzung, mehr Staub, 7 Brocken je Seite. Aus 2 m und 4 m klar, aus 10 m (im dunklen Raum) ebenfalls kaum.
Der Riss sitzt oben und rechts der Mitte (Höhe 0,3 m bis 1,9 m, bei x ≈ +0,15 m), damit ihn die Waffen im Bild nicht verdecken; beide Stärken haben denselben Aufbau, nur anders gewichtet.

## Was die Code-Sitzung wissen muss
- **Zeichnen:** In `DoorView` für `DoorKind.Cracked`, solange die Wand nicht gebrochen ist, das Modell `crack_<variante>_<stärke>` laden (Variante aus `SurfaceVariants.At` der Türzelle, Stärke eine Einstellung/Konstante) und `panel` und `debris` zeichnen, ausgerichtet wie der Platzhalter (`place` mit Drehung für Ost/West; Ursprung des Modells ist die Mitte der Zelle **am Boden**, nicht in der Zellmitte). Die Rotation um 90° erhält die Ausrichtung der Wand-Kachel (geprüft: die Wand-Kachel liegt auf allen vier Seiten richtig).
- **Rückfall:** fehlt die Datei oder ist sie kaputt, bleiben die alten Linien als Platzhalter.
- **Bruch (#185):** Beim Bruch (`WallBroken`) wird das Modell nicht mehr gezeichnet (die Zelle ist offen). `chunk` ersetzt die Farb-Quader: Textur der Variante, Farbe darf zusätzlich ±25 % abgedunkelt werden.
- Prüfsummen (`checksums.py`) und `HERKUNFT.md` je Modell; **sechs Dateien** statt einer.
- Variantenfälle: Variante der Zelle = Variante des Raumes dahinter oder davor je nach `RoomA`; die Zelle zeigt auf beiden Seiten dieselbe Variante.

## Was noch nicht stimmt
- Aus 10 m sieht man in der Gangszene weder dezent noch deutlich (Nebel plus Dunkelheit); das ist beabsichtigt dezent, aber auch die deutliche Fassung ist dort nur zu erahnen. Wenn „deutlich“ aus 10 m eine Spur zeigen soll, brauchen die Risse mehr Helligkeit (helle Kanten, Staub).
- Der Riss wiederholt sich je Variante in gleicher Form (nur die Stellen, die auf der Wand-Kachel liegen, ändern sich). Zwei Rissbilder je Variante gäbe Abwechslung zwischen Runs.
- Die Abplatzung ist ein flacher dunkler Fleck, kein Loch; aus 1 m (Geheimraum) wirkt sie wie ein dunkler Fleck mit Rand.
- Das Spiel zeichnet die Brocken weiterhin mit flachen Farben, bis `chunk` eingebaut ist.
- Nicht geprüft: Aussehen in einem echten Seed-Level (nur eine Wandreihe in einem Saal), Ost/West-Zelle im Foto (rechnerisch gleich), Variante C bei 2 m und 10 m (nur 4 m), Staub und Brocken aus der Seite.
- Der Mensch hat bei den Türen die Boss-Tür gewählt, diese Wand hat damit nichts zu tun.
