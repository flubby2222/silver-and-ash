# Übergabe der Modelle an den Code (Issue #232)

Stand 2026-11-16, Branch `claude/design-probe-ghul-45a8c4`. Angenommene Probestücke, aufbereitet für den Einbau.

```
design/uebergabe/
  tools/models/   -> gehört nach tools/models/   (Erzeuger-Skripte)
  assets/models/  -> gehört nach assets/models/  (Modelle und Texturen)
```

Die Ordnerstruktur ist dieselbe wie im Repository: Ein Skript in `tools/models/` schreibt nach `../../assets/models/`. Werden beide Ordner nach `tools/models/` und `assets/models/` kopiert, erzeugt jedes Skript dort neu.

```bash
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/models/ghoul.py
```

Die Skripte erzeugen nur Modell und Textur, keine Bilder. Die Probebilder entstehen weiter mit den Skripten unter `design/proben/grafik/`.

**Neue Zeilen für `assets/HERKUNFT.md`** (Datei, Skript, Datum):

| Datei | Skript | Datum |
|---|---|---|
| `assets/models/ghoul.glb`, `assets/models/ghoul.png` | `tools/models/ghoul.py` | 2026-11-16 |
| `assets/models/chest_beggar.glb`, `chest_guard.glb`, `chest_king.glb`, `chest_vampire.glb` und je `.png` | `tools/models/chest.py` | 2026-11-17 |
| `assets/models/pistol.glb`, `assets/models/pistol_hand.glb`, `assets/models/pistol.png` | `tools/models/pistol.py` | 2026-11-17 |
| `assets/models/blade.glb`, `assets/models/blade_hand.glb`, `assets/models/blade.png` | `tools/models/blade.py` | 2026-11-17 |
| `assets/models/bat.glb`, `assets/models/bat.png` | `tools/models/bat.py` | 2026-11-16 |

**Hilfsmodule** (keine eigenen Assets):

- `tools/models/psx.py`: Bauteile, UVs, Texturmalerei, Export.
- `tools/models/arm.py`: Hand im Handschuh mit Ärmel.

Werkzeuge: nur Blender 5.3 mit den mitgelieferten Modulen (bpy, bmesh, mathutils, NumPy).

## Für alle Modelle gilt

**Format und Achsen:**

- glTF binär (`.glb`), mit +Y oben exportiert.
- In der Datei gilt: x seitlich, y oben, z vorn, eine Einheit ist ein Meter.
- Figuren blicken nach +z.

**Teile:**

- Jedes bewegliche Teil ist ein eigener Knoten mit eigenem Mesh.
- Die Knoten-Verschiebung ist der Drehpunkt des Teils.
- Die Ecken liegen in Ruhehaltung, also so, wie sie im Modell stehen.

**Was Raylib beim Laden macht** (`LoadModel`):

- Soweit ich weiß, rechnet Raylib die Knoten-Verschiebung in die Ecken ein. Danach liegt jedes Mesh fertig an seinem Platz, und die Drehpunkte stehen nur noch in dieser Tabelle.
- Die Meshes kommen in der Reihenfolge der Datei. Blender schreibt sie **alphabetisch nach Namen** (Tabellen unten).
- Sicherer ist es, die Namen beim Laden mit cgltf zu lesen statt sich auf die Reihenfolge zu verlassen. Das muss die Code-Sitzung prüfen.

**Ein Teil drehen:**

- Formel: `Welt = Eltern-Matrix × Verschiebung(Drehpunkt − Drehpunkt der Eltern) × Drehung`.
- Für ein Teil ohne Eltern: `Welt = Lage der Figur × Verschiebung(Drehpunkt) × Drehung`.
- Weil Raylib die Ecken schon an den Drehpunkt gerechnet hat, braucht man pro Teil zusätzlich `Verschiebung(−Drehpunkt)` vor der Drehung.
- So rechnet `pose()` in `psx.py`; das ist die Referenz.

**Drehrichtung „heben“ (+°):**

- Gemeint ist eine Drehung um die x-Achse des Teils, bei der das vordere Ende nach oben geht.
- Ein Teil, das nach unten hängt, schwingt dabei erst nach vorn, dann nach oben.
- Im rechtshändigen Spiel-System mit y oben und z vorn ist das eine Drehung um −x, also `Matrix4x4.CreateRotationX(-Winkel)`. Die Vorzeichen bitte im Spiel am Bild prüfen.

**Textur:**

| Was | Wert |
|---|---|
| Format | PNG, sRGB, eingebettet in die glb und zusätzlich als `.png` daneben |
| Größe | Figuren, Waffen und Kisten 128 × 128; höchstens 32 Farben |
| Filterung | **keine**. Raylib: `TEXTURE_FILTER_POINT`, **keine Mipmaps** erzeugen. Der Exporter schreibt `magFilter NEAREST`, `minFilter NEAREST_MIPMAP_NEAREST`; das Mipmap-Feld bitte ignorieren |
| Vertex-Farben | **keine**. Die Datei hat nur Position, Normale und UV (`TEXCOORD_0`). Die Umrechnung linear → sRGB, die bei Vertex-Farben nötig gewesen wäre, entfällt |

**Schattierung:**

- **Wie:** Gouraud, also Helligkeit je Ecke aus der Welt-Normale nach dem Drehen, über die Fläche verlaufend.
- **Formel:** stufenlos aus den sechs Helligkeiten von `FlatShading`, gewichtet mit den Quadraten der Normalen-Anteile: `nx²·(nx>0 ? 1,0 : 0,62) + ny²·(ny>0 ? 0,9 : 0,55) + nz²·(nz>0 ? 0,82 : 0,72)`.
- **Harte Kanten:** Teile mit harten Kanten (Kisten, Flügelhäute, Lauf, Beschläge, Klinge) haben in der Datei getrennte Ecken je Fläche, dort wird es automatisch flach.
- **Danach:** mal Textur, dann Laterne und Nebel wie in `level.fs`, dann die 15-Bit-Körnung (Entscheidung des Menschen vom 2026-11-16).

## Ghul – `ghoul.glb`

**Ursprung:** zwischen den Füßen auf dem Boden.

**Größe:**

- 1 024 Dreiecke.
- Höhe bis 0,935 m.
- Kopf mit Kiefer 0,70 bis 0,935 m hoch und 0,14 m breit, von 0,23 bis 0,43 m vor der Mitte. Das liegt in der Kopfzone von `BodySizes.Ghoul`.

| Teil (Reihenfolge in der Datei) | Drehpunkt (x, y, z) | hängt an | Dreiecke |
|---|---|---|---|
| `forearm_nx` (mit Hand) | −0,31, 0,38, 0,22 (Ellbogen) | upper_arm_nx | 162 |
| `forearm_px` (mit Hand) | 0,31, 0,38, 0,22 | upper_arm_px | 162 |
| `head` | 0, 0,68, 0,24 (Halsansatz) | torso | 120 |
| `jaw` | 0, 0,76, 0,33 (Kiefergelenk) | head | 36 |
| `shin_nx` (mit Fuß) | −0,22, 0,07, −0,04 (Knie) | thigh_nx | 72 |
| `shin_px` (mit Fuß) | 0,22, 0,07, −0,04 | thigh_px | 72 |
| `thigh_nx` | −0,10, 0,44, −0,22 (Hüftgelenk) | – | 36 |
| `thigh_px` | 0,10, 0,44, −0,22 | – | 36 |
| `torso` (mit Hals und Lumpen) | 0, 0,46, −0,23 (Hüfte) | – | 256 |
| `upper_arm_nx` | −0,19, 0,67, 0,16 (Schulter) | torso | 36 |
| `upper_arm_px` | 0,19, 0,67, 0,16 | torso | 36 |

**Haltungen** (Grad „heben“ um x, relativ zur Datei):

| Haltung | Winkel |
|---|---|
| **Ruhe** | `jaw` −12 (Maul hängt offen). Alles andere 0; die Arme greifen in der Datei schon etwa 30° nach vorn auf den Boden |
| **Ausholen** (`WindUpProgress` = 1) | `torso` +34 (richtet sich um die Hüfte auf), `head` −24 (schaut weiter zum Spieler), `jaw` −38, `upper_arm_px` +120, `upper_arm_nx` +128, `forearm_px` −25, `forearm_nx` −20 |

- **Zwischenstufen:** linear mit `WindUpProgress` mischen.
- **Kriechen**, nicht im Probestück: Oberarme gegengleich ±20° um die Ruhe, Unterarme dazu −10°/+10°; Oberschenkel gegengleich ±12°. Das ist mein Vorschlag, nicht geprüft.
- **Schlag** (Ausfallschritt wie im Platzhalter): `torso` −14 und 0,25 m nach vorn, `upper_arm_*` +55. Ebenfalls Vorschlag.
- **Treffer und Tod** wie im Platzhalter auf die ganze Figur anwenden.

## Kiste – `chest_beggar.glb`, `chest_guard.glb`, `chest_king.glb`, `chest_vampire.glb`

**Aufbau:**

- Je Stufe eine Datei mit eigener Textur. Die Geometrie ist gleich, nur das Formmerkmal oben auf dem Deckel unterscheidet sich (Guard Griff, King Krone, Vampire Dornen).
- Ursprung in der Mitte am Boden.
- Die Vorderseite mit dem Schloss blickt nach +z. Laut #209 dreht das Spiel die Kiste nach der Vorderseite aus `ChestPlan`.

| Teil | Drehpunkt | hängt an | Dreiecke (Beggar / Guard / King / Vampire) |
|---|---|---|---|
| `base` | 0, 0, 0 | – | 48 / 80 / 80 / 80 |
| `lid` | 0, 0,455, −0,365 (Scharnier hinten unten am Deckel) | base | 60 / 96 / 84 / 84 |
| `lock` | 0, 0,47, 0,39 (Oberkante der Schlossplatte) | base | 12 |

**Was sich gegenüber den Dateien im Spiel ändert (Probestück 12, Issue #258):** alle acht Dateien tauschen (`chest_<stufe>.glb` und `.png`), danach `python tools/models/checksums.py`. Sonst nichts:
- **Namen, Knoten, Drehpunkte, Eltern, Größe, Dreieckszahlen:** unverändert.
- **Atlas 128 × 128 statt 64 × 64**, höchstens 32 Farben statt 24. Felder: `planks` (Kasten, 128 × 64), `lidplanks` (Deckel, 64 × 64), `lidband` (Band über die Wölbung, 16 × 64), `band` (Band am Kasten, 16 × 32), `metal`, `lock`, `inside`.
- **Neue UV-Zuordnung des Deckels:** Die Bretter laufen über die Wölbung (v nach der Bogenlänge des Profils, zehn Reihen von hinten unten über den Deckel nach vorn unten, u quer), statt je Fläche ein Feld zu strecken. Die Deckelbänder haben ein eigenes Feld mit 14 Nieten. Die Stirnflächen und die Unterseite sind wie vorher.
- **Farben: gedeckte Töne, Stufenfarbe nur Akzent.** Beggar graues rissiges Holz mit schmalen rostigen Bändern; Guard dunkle Eiche, graue Eisenbänder, Stahlblau als Akzent; King dunkles poliertes Holz, Messing und Gold als Akzent; Vampire schwarzes Holz mit hellen Brettkanten, breite Blutrot-Bänder und Siegel, geschwärztes Eisen mit Glanzkante an den Dornen. Die Spielfarben in `ChestView.cs` (Rahmen, Beute) bleiben davon unberührt; das Pulsieren der Vampir-Stufe geht bei den neuen Texturen nur noch über das Rot (Bänder, Siegel), nicht mehr über die ganze Kiste.

**Haltungen:**

- **Deckel offen:** `lid` +110 („heben“, die Vorderkante geht nach oben hinten), wie der Platzhalter mit 1,9 rad.
- **Schloss bei aufgeschlossener Kiste:** ausblenden. Herunterklappen um `lock` wäre auch möglich.
- **Aufsteigen aus dem Boden:** ganze Kiste verschieben, wie heute.

**Vampir-Stufe:** Sie pulsiert im Spiel zwischen 150, 20, 30 und 200, 30, 40. In der Textur steht die Mitte. Das Pulsieren also als Tönung beim Zeichnen, zwischen etwa 86 % und 114 % im Rotkanal. Bänder und Schloss pulsieren dann leicht mit.

## Pistole – `pistol.glb` und `pistol_hand.glb`

**Größe:** in echter Größe. Die Skalierung 1,8 der Ich-Sicht ist eingebacken.

**Ursprung:** der Griffpunkt, die Lage `PistolHand` in `WeaponViews.cs`. Der Lauf zeigt nach +z.

**Wichtig – keine Spiegelung:**

- Das Modell ist in einem normalen, rechtshändigen System gebaut. **+x in der Datei ist die linke Seite der Waffe** aus Sicht des Spielers: Schloss und Hahn liegen bei −x, also rechts.
- `WeaponViews.CameraToWorld` spiegelt dagegen (Determinante −1). Mit dieser Matrix lägen Schloss und Hahn links, und Rückseiten-Culling würde die sichtbaren Flächen wegwerfen.
- Für echte Modelle braucht es eine echte Drehung als Kamera-Matrix (rechts = −x der Datei), oder die x-Achse wird beim Laden gespiegelt und die Dreiecke werden umgedreht.

| Teil (pistol.glb) | Drehpunkt (Meter) | hängt an | Dreiecke |
|---|---|---|---|
| `barrel` | 0, 0, 0 | stock | 96 |
| `cock` (Hahn, Fassung 4: kleiner, mit hellem Stein) | −0,0126, 0,018, 0,0072 | stock | 114 |
| `fittings` (Beschläge, Bügel, Ladestock, Batterie) | 0, 0, 0 | stock | 214 |
| `flash` (Mündungsfeuer, unbeleuchtet) | 0, 0, 0 | stock | 42 |
| `stock` | 0, 0, 0 | – | 144 |
| `arm` (in `pistol_hand.glb`: Mantelärmel mit Rüschenmanschette, ohne Hand) | 0, 0, 0 | stock | 192 |

**Haltungen:**

| Haltung | Lage im Kameraraum | Drehung | Teile |
|---|---|---|---|
| **Ruhe** | (0,095, −0,08, 0,18) | Lauf parallel zur Blickachse, 15° gekippt: Drehung um die Laufachse, Oberseite zur Bildmitte | – |
| **Rückstoß** (voll) | zusätzlich (0, +0,012, −0,035) | +14 „heben“ um den Griffpunkt | `cock` −38 (Hahn fällt nach vorn), `flash` sichtbar |

- **Zurückfedern:** wie im Platzhalter, `EaseOut` über 0,18 s.
- **Mündungsfeuer:** nur während des Blitzes zeichnen, mit Helligkeit 1 statt der Gouraud-Formel.
- **Nachladen:** im Platzhalter nicht vorhanden.

## Silberklinge – `blade.glb` und `blade_hand.glb`

**Größe und Ursprung:** in echter Größe (Skalierung 1,8 eingebacken), Ursprung am Griff, Klinge nach +z und 25° nach oben (wie der Platzhalter). Dieselbe Achsen-Regel wie bei der Pistole.

| Teil | Drehpunkt | hängt an | Dreiecke |
|---|---|---|---|
| `sword` (Klinge, Parierstange, Griff, Knauf) | 0, 0, 0 | – | 152 |
| `arm` (in `blade_hand.glb`: Mantelärmel mit Rüschenmanschette, ohne Hand) | 0, 0, 0 | sword | 192 |

**Haltungen** (aus `DrawBlade`; die Hand liegt bei (−0,11, −0,095, 0,19) im Kameraraum):

| Haltung | Neigung um die Längsachse | Drehung der Spitze um die Hochachse | Versatz der Hand |
|---|---|---|---|
| **Ruhe** | 20°, Oberseite zur Bildmitte | 0 | – |
| **Ausholen** beim Aufladen (voll) | 20° | 35° nach links | (−0,03, +0,015, −0,04) |
| **Schlag** (Höhepunkt) | 45° | 95° nach rechts | (+0,06, +0,04, 0) |

Zeitverlauf und Zittern bei voller Ladung wie im Platzhalter.

## Fledermaus – `bat.glb`

**Ursprung:** die **Rumpfmitte**, nicht der Boden. Im Spiel bei `Position + (0, Height/2, 0)` zeichnen, wie der Platzhalter mit +0,2 m. Sie blickt nach +z und wird wie heute nur um die Hochachse zum Spieler gedreht.

**Größe:**

- 680 Dreiecke.
- Spannweite 1,32 m. Das ist größer als `BodySizes.Bat.Radius` = 0,35 m; der Vorschlag dazu steht in den Notizen des Probestücks.

| Teil | Drehpunkt | hängt an | Dreiecke |
|---|---|---|---|
| `body` (mit Beinen) | 0, 0, 0 | – | 172 |
| `head` | 0, 0,03, 0,09 | body | 124 |
| `jaw` | 0, 0, 0,13 | head | 44 |
| `wing_in_nx` | −0,06, 0,02, 0,05 (Schulter) | body | 40 |
| `wing_in_px` | 0,06, 0,02, 0,05 | body | 40 |
| `wing_out_nx` | −0,30, 0,04, 0,11 (Handgelenk) | wing_in_nx | 130 |
| `wing_out_px` | 0,30, 0,04, 0,11 | wing_in_px | 130 |

In der Datei stehen die Flügel waagerecht ausgebreitet (alle Winkel 0).

**Drehungen:**

| Teil | Achse und Richtung |
|---|---|
| `body`, `head`, `jaw` | „heben“ um x |
| Flügel „heben“ | um die Längsachse z; positiv = Spitze nach oben, auf beiden Seiten gleich (links also mit umgekehrtem Vorzeichen um z) |
| Flügel „schwenken“ | um die Hochachse y; positiv = Spitze nach hinten |

Erst schwenken, dann heben: `Drehung = Schwenken × Heben`.

| Haltung | body | head | jaw | Innenflügel heben / schwenken | Außenflügel heben / schwenken |
|---|---|---|---|---|---|
| **Schweben** (Mitte des Schlags) | 15 | 0 | −12 | 8 / 0 | 4 / 0 |
| Flügelschlag oben | 15 | 0 | −10 | 40 / 0 | 20 / 0 |
| Flügelschlag unten | 15 | 0 | −10 | −35 / 0 | −15 / 0 |
| **Kreischen** (Ankündigung des Sturzflugs) | 12 | 30 | −55 | 65 / −15 | 25 / −20 |
| **Sturzflug** | −30 | −5 | −45 | −12 / 32 | −5 / 38 |

- **Flügelschlag:** zwischen „oben“ und „unten“ sinusförmig mit 7 Schlägen pro Sekunde, wie der Platzhalter. Der Außenflügel darf ein wenig nacheilen.
- **Beim Kreischen:** das Zittern des Platzhalters dazu.
- **Tod:** Flügel wie beim Sturzflug anlegen und trudeln, wie heute.
- **Rotes Aufglühen vor dem Spucken:** bleibt eine Tönung im Code (Gefahr). Das Modell selbst hat kein Rot.

## Offene Punkte für die Code-Sitzung

1. Lädt Raylib die Knoten-Verschiebungen in die Ecken ein? Bleibt die Reihenfolge der Meshes erhalten? Lassen sich Namen lesen?
2. Kamera-Matrix der Waffen ohne Spiegelung (siehe Pistole).
3. Shader mit Textur, Normale und Licht je Ecke; Point-Filter ohne Mipmaps.
4. Die Waffenhände (`pistol_hand.glb`, `blade_hand.glb`) sind seit Probestück 14 (Issue #265, Fassung j) **ein dunkelblauer Mantelärmel mit heller Rüschenmanschette, ohne Hand, Finger oder Handschuh** (Entscheidung des Menschen: „Keine Hände“, Entwurf C aus Probestück 13). Der Pistolenärmel ist derselbe Ärmel, gespiegelt. Sie hängen starr an der Waffe; Dateinamen, Knoten und Drehpunkte sind gleich geblieben.

### Was sich gegenüber den Dateien im Spiel ändert (Probestück 14, Fassung j, Issue #265)

Vier Dateien tauschen (`pistol.glb`, `pistol_hand.glb`, `blade.glb`, `blade_hand.glb`) und die zwei Atlanten `pistol.png`, `blade.png`, danach `python tools/models/checksums.py`. **Keine neuen PNGs, kein eigener Arm-Atlas:** Der Ärmel liegt wie in Entwurf C im Feld `sleeve` des Waffen-Atlasses (128 × 128, 32 Farben); so bleibt das Aussehen von C erhalten. (`pistol_hand.png` und `blade_hand.png` aus den Fassungen f bis h entfallen.)
- **Namen, Knoten, Drehpunkte, Eltern:** unverändert; `arm` ist ein Teil mit einem Mesh.
- **Haltungswerte: unverändert gegenüber `main`.** Es ist keine Änderung an `WeaponPose.cs` nötig; mit den neuen Dateien laufen die 15 Tests in `WeaponPoseTests` grün (lokal geprüft).
- **Geometrie:** Der Klingenärmel ist der aus Entwurf C (Rohr aus 12 Seiten, kurze Rüschenmanschette mit drei zackigen Ringen an der Parierstange). **Neu in Fassung j:** Die ersten vier Ringe (Manschette und das im Bild liegende Rohrstück) sind unverändert; **ab dem fünften Ring hängt das Rohr nach unten** (Dateirichtung (−0,71; −0,71; 0), das ist bei 45° Rollen genau senkrecht nach unten) und endet weit unten (letzter Ring bei (−0,87; −0,985; −0,12), 8 Ringe). Grund: Beim Schlag dreht die Waffe um die Hochachse (95°); ein Rohr, das in Ruhe nach links hinten zeigt, zeigte dabei nach vorn ins Bild, und sein Ende wurde sichtbar (Fassung i: blauer „Hut“ mit Ende oben links). Die Drehung um die Hochachse verschwenkt nur waagerechte Anteile; ein nach unten hängendes Rohr bleibt unten. Ursache und Beleg: `design/proben/grafik/aermel8/blatt_phasen_i.png` (vorher) und `design/proben/grafik/aermel9/blatt_phasen_j.png` (nachher), Rechnung `aermel9/simulation.py`. Der Pistolenärmel ist weiter derselbe Ärmel gespiegelt (Rohr wie in Fassung i), 2,6 cm nach unten verschoben; die Pistole schwenkt nicht. Je 192 Dreiecke.
- **Textur:** wie C; eine schmale dunkle Naht hinter der Rüsche; **die Knöpfe entfallen** (im Bild nicht zu erkennen); die Rüsche der Pistole ist im Atlas der Pistole um 14 % dunkler (gebrochenes Weiß wie links).
- **Prüfsummen** ändern sich; `assets/HERKUNFT.md`: gleiche Skripte, neues Datum.
- **Skripte:** `tools/models/arm.py` (Entwurf C am Anfang des Bereichs „Ärmel 2“, `build_design` mit `DESIGN == "c"`; die anderen Entwürfe stehen weiter darin, die Voreinstellung ist C), `pistol.py`, `blade.py`.
- **Was noch nicht stimmt** (siehe `design/proben/grafik/aermel9/notizen.md`): Das Rohr ist starr mit der Waffe; beim vollen Schlag zeigt es aus der Hand nach links und dann nach unten, ein Arm von der Schulter her wäre das nicht ganz. Die saubere Lösung wäre ein zweiter Knoten (Unterarm) mit Drehpunkt am Handgelenk, den der Code zur Schulter ausrichtet; das ist eine Änderung für die Code-Sitzung (siehe Notiz).

### Frühere Änderungen (Probestück 11, Issues #256 und #257): Hahn und handlose Ärmel; die Ärmel sind durch Probestück 14 ersetzt, der Hahn gilt weiter

Alle sechs Dateien tauschen (`pistol.glb`, `pistol_hand.glb`, `pistol.png`, `blade.glb`, `blade_hand.glb`, `blade.png`), danach `python tools/models/checksums.py`. Sonst nichts:
- **Namen, Knoten, Drehpunkte, Eltern, Meshreihenfolge:** unverändert. `arm` ist weiter ein Teil mit einem Mesh.
- **Geometrie:** `arm` ist ein Ärmel (Rohr aus 12 Seiten, Öffnung um den Griff, läuft aus dem Bild: Pistole nach rechts unten, Klinge nach links unten), Pistole 156 statt 376 Dreiecke, Klinge 168 statt 396. `cock` hat 114 statt 40 Dreiecke, ist ein Drittel kleiner als im Probestück 10 und trägt einen hellen Feuerstein.
- **Texturen:** `pistol.png` und `blade.png` sind neu gemalt (128 × 128, 32 Farben); das Feld `glove` entfällt, die Pistole hat dafür die Felder `lock` (Hahn, Pfanne, Batterie) und `flint` (Stein), Ärmel und Saum liegen im Feld `sleeve`. Die Ursache der „seltsamen Linien“: der Stein benutzte das Lauf-Feld mit Silberlinien.
- **Prüfsummen** ändern sich; `assets/HERKUNFT.md`: gleiche Skripte, neues Datum.
- **Nicht lösbar im Modell:** Blade „aufgeladen“: das Heft liegt etwa 1 cm vor dem Auge, die Nahebene schneidet den Ärmel an (Blick ins Innere). Lösung nur über die Haltung (`WeaponPose.Blade` weiter vor oder Nahebene näher).
- **Skripte:** `tools/models/arm.py` (neu: `build_sleeve4`, `paint_sleeve4`), `pistol.py`, `blade.py`, und `psx.py` mit der Funktion `bake_scale` (wie in `tools/models/psx.py` auf `main`). Aufruf wie bisher.

## Level-Texturen der Katakomben (Issue #233)

Ergänzt am 2026-11-16 auf Bitte der Leitung. Die Modell-Dateien oben sind dabei unverändert geblieben.

```
design/uebergabe/
  tools/textures/catacomb.py      -> tools/textures/catacomb.py
  assets/textures/catacomb_*.png  -> assets/textures/
```

Das Skript nutzt das Hilfsmodul `tools/models/psx.py` (Farbpalette, Speichern) und findet es über den Nachbarordner `../models`.

```bash
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/textures/catacomb.py
```

Die Dateien sind pixelgleich mit dem angenommenen Probestück 8, Fassung 2 (`design/proben/grafik/level/catacomb_*_v2.png`).

| Datei | Fläche im Level | Inhalt | mittlere Farbe (sRGB) |
|---|---|---|---|
| `catacomb_wall.png` | alle Wände, auch Säulen und Stirnseiten von Stufen | Bruchsteinmauer in vier ungleich hohen Lagen, leise unregelmäßige Fugen, feuchte Laufspuren | wie die Wandfarbe 122, 104, 112 |
| `catacomb_floor.png` | alle Böden | große, abgetretene Platten (0,5 m Lagen), leise Fugen, Schmutzflecken | wie die Bodenfarbe 84, 72, 78 |
| `catacomb_ceiling.png` | alle Decken | Gewölbemauerwerk, kaum sichtbare Fugen, weicher Ruß | **72 %** der Deckenfarbe 64, 56, 66, also etwa 46, 40, 48: Die Decke soll nach oben verschwinden |

**Größe und Farben:** 64 × 64 Pixel, je 24 Farben (erlaubt bis 32), sRGB-PNG.

**Filterung:** keine. `TEXTURE_FILTER_POINT`, keine Mipmaps; Wiederholung (`TEXTURE_WRAP_REPEAT`) muss an sein.

**Kacheln:**

- Alle drei sind nahtlos: Fugen, Rauschen und Laufspuren laufen über den Rand und kommen gegenüber wieder herein.
- **Eine Kachel ist 1 m × 1 m.** Ein Texel ist 1,56 cm; aus 4 m ist das etwa ein Bildpixel.

**UV aus Weltkoordinaten**, damit die Kacheln über Zellgrenzen hinweg durchlaufen und keine Naht an jeder Zelle entsteht:

| Fläche | u | v |
|---|---|---|
| Böden und Decken | Welt-x | Welt-z |
| Wände mit Blick nach ±x | Welt-z | Welt-y (Höhe) |
| Wände mit Blick nach ±z | Welt-x | Welt-y |

Damit liegt jede Steinlage auf gleicher Höhe ringsum. Das v der Wände mit der Höhe über dem Boden (y) beginnen, nicht mit der Unterkante der Fläche. So passen Stufen und unterschiedliche Bodenhöhen aneinander.

**Ausrichtung:** Bei v = 0 liegt die Unterkante einer Steinlage, und v soll mit der Höhe zunehmen. Die feuchten Laufspuren der Wand sind senkrecht; auf dem Kopf stehend fällt das nicht auf, quer gelegt schon. Wände also nie um 90° gedreht abbilden.

**Helligkeit:**

- Die Texturen ersetzen die Flächenfarbe; sie werden **nicht** mit ihr multipliziert.
- Die Helligkeit je Fläche aus `FlatShading` (Wand ±x/±z, Boden 0,9, Decke 0,55), die Laterne und der Nebel kommen wie heute dazu, dann die 15-Bit-Körnung.
- Wer die Textur mit der Flächenfarbe multipliziert, macht alles doppelt dunkel.

**Neue Zeilen für `assets/HERKUNFT.md`:**

| Datei | Skript | Datum |
|---|---|---|
| `assets/textures/catacomb_wall.png` | `tools/textures/catacomb.py` | 2026-11-16 |
| `assets/textures/catacomb_floor.png` | `tools/textures/catacomb.py` | 2026-11-16 |
| `assets/textures/catacomb_ceiling.png` | `tools/textures/catacomb.py` | 2026-11-16 |

## Gorm, the Chained – `gorm.glb` (Boss)

Ergänzt am 2026-11-16 auf Bitte der Leitung (Endfassung mit der vom Menschen gewählten Eisenmaske). Die Ghul-Dateien sind unverändert.

```
design/uebergabe/
  tools/models/gorm.py, gorm_base.py, gorm_heads.py  -> tools/models/
  assets/models/gorm.glb, gorm_body.png, gorm_body_phase2.png, gorm_limbs.png  -> assets/models/
```

```bash
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/models/gorm.py
```

`gorm.py` ist der Aufruf; `gorm_base.py` hat Körper, Waffen und Posen, `gorm_heads.py` die Köpfe (die Eisenmaske ist gewählt, die beiden anderen Köpfe stehen nur als Bilder auf dem Branch). Nutzt `psx.py` aus demselben Ordner.

**Neue Zeile für `assets/HERKUNFT.md`:**

| Datei | Skript | Datum |
|---|---|---|
| `assets/models/gorm.glb`, `gorm_body.png`, `gorm_body_phase2.png`, `gorm_limbs.png` | `tools/models/gorm.py` | 2026-11-16 |

**Größe und Ursprung:** zwischen den Füßen auf dem Boden, Blick nach +z, Höhe 3,1 m (Scheitel), 2 268 Dreiecke in der Datei. Die rechte Hand (+x, wie `GormView`) hält die Keule, die linke (−x) die Kette.

**Texturen** (128 × 128, 32 Farben, Point-Filter, keine Mipmaps), zwei Materialien in der Datei:

| Datei | gehört zu | Inhalt |
|---|---|---|
| `gorm_body.png` | `torso`, `head`, `jaw` | Rumpf, Eisenmaske, Halsring, Lendenschurz |
| `gorm_body_phase2.png` | dieselben Teile | Phase 2: Blutspuren, Blut unter der Maske. Der Code tauscht die Textur dieser drei Teile beim Wechsel in Phase 2 |
| `gorm_limbs.png` | alle anderen Teile | Arme, Beine, Keule, Eisen, Kette, Haken |

| Teil (Datei, alphabetisch) | Drehpunkt (x, y, z) | hängt an | Dreiecke |
|---|---|---|---|
| `torso` (Halsring, Lendenschurz) | 0, 1,15, 0 (Hüfte) | – | 432 |
| `head` (Eisenmaske) | 0, 2,5, 0,42 (Nacken) | torso | 264 |
| `jaw` | 0, 2,7, 0,58 | head | 60 |
| `upper_arm_px` | 0,86, 2,3, 0,26 (Schulter, Keulenseite) | torso | 72 |
| `upper_arm_nx` | −0,86, 2,22, 0,26 (Kettenseite, 8 cm tiefer) | torso | 72 |
| `forearm_px` (Handschelle, Faust) | 1,04, 1,46, 0,42 (Ellbogen) | upper_arm_px | 196 |
| `forearm_nx` | −1,04, 1,38, 0,42 | upper_arm_nx | 196 |
| `wrap_nx` (Kette dreimal um den Unterarm) | −1,04, 1,38, 0,42 | forearm_nx | 216 |
| `thigh_px`, `thigh_nx` | ±0,36, 1,12, 0 (Hüftgelenk) | torso | je 72 |
| `shin_px`, `shin_nx` (mit Fuß) | ±0,44, 0,6, 0,12 (Knie) | thigh | je 120 |
| `club` | 1,0, 0,56, 0,58 (Mitte der rechten Faust) | forearm_px | 188 |
| `chain_link` | Mitte des Glieds | – | 64 |
| `hook` | Öse oben, Spitze nach unten und vorn | – | 124 |

`chain_link` und `hook` sind **keine Körperteile**, sondern Bausteine, die der Code beliebig oft zeichnet.

**Kette im Code:**

- Glieder entlang eines Linienzugs aufreihen, **Abstand 0,19 m**, jedes zweite um 90° um die Kettenrichtung gedreht. Die Glieder sind 0,30 × 0,19 m groß.
- Am Ende der Haken, **1,8-fach vergrößert**.
- Ruhe: linke Faust → senkrecht zum Boden → ein Stück am Boden schleifend (etwa 1,1 m nach vorn links).
- Schwung: von der Faust zum Ziel; die Ankündigung waagerecht über dem Kopf, Länge wie `SwingArea.Reach`.
- Hakenwurf: Faust → Haken, wie heute.
- Dazu eine kleine Kette (halbe Gliedgröße) vom Kinnring der Maske zum Halsring. In Phase 1 hängt hinten am Halsring ein abgerissenes Stück; in Phase 2 entfällt es.
- **Dreiecke:** Hängend etwa 3 300 insgesamt, bei 8 m Reichweite etwa 5 000. Für den Schwung ein vereinfachtes Glied nehmen (zwei Vierecke, 16 Dreiecke) oder jedes zweite Glied weglassen; das entscheidet der Einbau.

Das Glühen des Hakens (Ankündigung des Wurfs) bleibt eine Tönung im Code (Warnfarbe 200, 30, 30, Gefahr). Das Modell selbst hat kein Rot.

**Haltungen** („heben“ um x wie oben; „abspreizen“ um die Längsachse z, positiv = Hand nach außen):

| Haltung | Winkel |
|---|---|
| **Ruhe** | `jaw` −6, `forearm_px` 14, `forearm_nx` 10 |
| **Ankündigung Kettenschwung** | `upper_arm_nx` 160 / abspreizen 25, `forearm_nx` 20, `head` 8, `jaw` −20, `torso` 6, `forearm_px` 14 |
| **Kettenschwung** | `upper_arm_nx` 15 / 85, `upper_arm_px` −10 / −15, `torso` −6, `jaw` −25, `forearm_px` 14 |
| **Ankündigung Haken** | `upper_arm_nx` −40 / 45, `forearm_nx` 60, `torso` 4, `head` −4, `jaw` −12, `forearm_px` 14 |
| **Phase 2 Brüllen** | `torso` 16, `head` 38, `jaw` −45, beide Oberarme 150 / 30, beide Unterarme 20 |
| **Phase 2 Ruhe** | `torso` −8, `head` −6, `jaw` −22, beide Oberarme 8 / 18, beide Unterarme 24 |

**Nicht gerendert, für den Einbau nötig:**

- **Hammerschlag:** `upper_arm_px` hoch (heben etwa 150, der Platzhalter hebt um 2,6 rad); die Keule folgt als Teil des Unterarms.
- **Keule steckt fest:** `torso` nach vorn beugen (Kopf tiefer um etwa `StuckBend` 0,8 m), die Keule glüht (Tönung im Code).
- **Tod:** wie im Platzhalter nach hinten fallen.

**Wichtig für den Einbau:**

- Die Keule bleibt, weil sie den Hammerschlag trägt. Sie ist bewusst dunkler als die Kette, damit die Kette als Waffe ins Auge fällt.
- Die Maske sitzt weiter vorn als der Kopf des Platzhalters (0,35 bis 0,94 m vor der Mitte). Die Code-Sitzung sollte prüfen, ob die Kopfzone von `BodySizes.Gorm` weit genug nach vorn reicht.
