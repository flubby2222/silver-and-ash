# Modelle im Spiel

Wie das Spiel ein Modell der Designer lädt, bewegt und zeichnet (Issue #232, Stil aus [ADR 0010](entscheidungen/0010-stil-ps1-nah-texturen.md)). Erster Einsatz: der Ghul, danach die Kiste in vier Stufen (Issue #249).

## Die Datei

- **Ablage:** `assets/models/<name>.glb`, Textur daneben als `<name>.png` (zur Ansicht; das Spiel nimmt die in der `.glb` eingebettete). Erzeuger-Skript `tools/models/<name>.py`, Eintrag in [`assets/HERKUNFT.md`](../assets/HERKUNFT.md).
- **Format:** glTF binär (`.glb`). x seitlich, y oben, z vorn, Meter; Figuren blicken nach +z.
- **Teile:** Jedes bewegliche Teil ist ein eigener Knoten mit genau einem Mesh aus einem Primitiv (Dreiecksliste mit Indizes). Die Verschiebung des Knotens ist sein Drehpunkt. Knoten sind nicht verschachtelt und nicht gedreht oder skaliert. Was davon abweicht, lehnt der Lader ab.
- **Daten je Ecke:** Position, Normale, Texturkoordinate. Keine Vertex-Farben.

## Wie Raylib lädt (geprüft mit Raylib-cs 8.1; das Spiel meldet beim Start raylib 6.0)

- `LoadModel` legt **ein Mesh je Knoten mit Mesh** an, in der **Reihenfolge der Knoten** in der Datei (Blender schreibt sie alphabetisch).
- Es **rechnet die Knoten-Verschiebung in die Ecken ein**: Die Ecken liegen danach dort, wo sie im Modell stehen, nicht um den Drehpunkt.
- **Namen und Drehpunkte gehen verloren.** Deshalb liest [`GlbScene`](../src/SilverAndAsh.Core/Presentation/GlbScene.cs) (Core, getestet) die Beschreibung der Datei selbst: Namen, Drehpunkte, Zahl der Ecken, kleinste Ecke je Teil.
- Der Lader ([`TexturedModel`](../src/SilverAndAsh.Game/Rendering/TexturedModel.cs)) **verlässt sich nicht auf die Reihenfolge**, sondern prüft je Teil: gleiche Zahl der Ecken, und die kleinste Ecke von Raylib liegt bei „kleinste Ecke der Datei + Drehpunkt“. Stimmt etwas nicht, nimmt er das Modell nicht.
- Danach kopiert er die Ecken **relativ zum Drehpunkt** (Raylib-Ecke − Drehpunkt), die Normalen, Texturkoordinaten und Indizes.
- **Textur:** Raylib lädt die eingebettete PNG ohne Mipmaps (geprüft: `Mipmaps == 1`); das Spiel stellt den Filter auf `Point`. Das `minFilter`-Feld der Datei (`NEAREST_MIPMAP_NEAREST`) wird nicht gebraucht.

## Teile drehen

- Wer an wem hängt, steht im Code (z. B. [`GhoulPose.Parts`](../src/SilverAndAsh.Core/Presentation/GhoulPose.cs)), die Drehpunkte kommen aus der Datei.
- [`Rig`](../src/SilverAndAsh.Core/Presentation/Rig.cs) (Core, getestet) rechnet je Teil: `Rahmen = Drehung × Verschiebung(Drehpunkt − Drehpunkt der Eltern) × Rahmen der Eltern`; ohne Eltern ist der „Rahmen der Eltern“ die Lage der ganzen Figur in der Welt. (System.Numerics: Zeilenvektoren, `a × b` heißt „erst a, dann b“.)
- „Heben“ um x heißt: das vordere Ende geht nach oben, ein hängendes Teil schwingt erst nach vorn. Das ist `Rig.Lift(grad)` = Drehung um −x. Im Bild geprüft (Ghul beim Ausholen: Arme oben, Kopf höher).

## Zeichnen

- **Ein Weg für die ganze 3D-Welt:** Modelle gehen durch denselben Shader wie Level und Platzhalter ([`assets/shaders/level.fs`](../assets/shaders/level.fs)): Laterne, Nebel und später die 15-Bit-Körnung gelten für alles gleich. Der Shader multipliziert jetzt mit der Textur; alles ohne Textur bekommt Raylibs weiße 1 × 1-Textur und sieht unverändert aus (geprüft: alle Fotos ohne Ghul sind byte-gleich mit vorher).
- **Ecken in C# in die Welt:** wie bei den Platzhalter-Quadern werden die Ecken jedes Bild in C# gedreht und über `Rlgl` gezeichnet. So bekommt jede Ecke die Helligkeit ihrer Normale **nach** dem Drehen.
- **Weiche Schattierung (Gouraud):** [`SmoothShading`](../src/SilverAndAsh.Core/Presentation/SmoothShading.cs) (Core, getestet) mischt die sechs Helligkeiten der flachen Schattierung mit den Quadraten der Normalen-Anteile: `x²·(x>0 ? +x : −x) + y²·(y>0 ? oben : unten) + z²·(z>0 ? +z : −z)`. Die Grafikkarte lässt den Wert über das Dreieck verlaufen.
- **Treffer-Blitz:** Ein Quader wechselt einfach die Farbe; ein Modell hat seine Farbe in der Textur. Der Shader mischt deshalb die Textur zur Blitzfarbe (`uniform vec4 flash`, rgb Farbe, a Anteil), vor Schattierung und Laterne – wie beim Platzhalter.

## Farbraum

- Die Textur steht als **sRGB** in der Datei (die Farbwerte, wie man sie in einem Bildprogramm sieht).
- Das Spiel rechnet **überall direkt mit diesen Werten** (keine Umrechnung nach linear und zurück): Level-Farben, Platzhalter und jetzt die Textur. Helligkeit und Laterne sind Faktoren auf den sRGB-Werten.
- Blender rechnet Licht dagegen linear. Ein Probebild aus Blender kann deshalb in den Schatten etwas anders aussehen als im Spiel; die Farben der Textur selbst kommen unverändert an.
- **Belegt im Bild** (Foto `ghoul-4m` mit Schattierung kurz auf 1 gestellt): Alle 2 375 Pixel des Ghuls sind genau „Texel × Laterne“ (±1/255), z. B. Kopf-Pixel (424, 211): Texel (125, 125, 100) × 0,859 = (108, 108, 87). Mit Schattierung ist es (101, 101, 80), also Helligkeit 0,935. Wäre die Textur fälschlich von sRGB nach linear gewandelt, passte kein einziges Pixel.

## Wenn es nicht klappt

Fehlt die Datei, ist sie abgeschnitten oder kaputt, fehlt ein Teil oder passt Raylibs Ergebnis nicht zur Beschreibung: eine Warnung ins technische Log (`%LOCALAPPDATA%\SilverAndAsh\logs`), und die Figur wird als Platzhalter gezeichnet. Das Spiel läuft weiter.

Auf manche kaputte Dateien stürzt Raylib im nativen Code ab, das kann kein `catch` auffangen. Deshalb prüft das Spiel jede Datei, bevor Raylib sie sieht:

1. **Prüfsumme (die eigentliche Sicherung):** [`assets/models/models.sha256`](../assets/models/models.sha256) nennt für jede `.glb` und `.png` Größe und SHA-256. [`ModelChecksums`](../src/SilverAndAsh.Core/Presentation/ModelChecksums.cs) (Core, getestet) vergleicht die Datei auf der Platte damit. Fehlt der Eintrag oder die Liste, oder stimmt Größe oder Summe nicht, wird Raylib für die Datei gar nicht aufgerufen. Dauer beim Ghul: rund 10 ms beim Start.
2. **Beschreibung (einfache zweite Stufe):** `GlbScene` liest Namen und Drehpunkte und prüft dabei grob Kopf, Abschnitte, Puffer, Puffer-Ansichten und Zugriffe auf ihre Längen.

**Was das abdeckt und was nicht:** Geschützt ist das Spiel gegen eine beschädigte, halb kopierte oder versehentlich geänderte Datei auf der Platte. Eine Datei, deren Prüfsumme stimmt, gilt als heil: Sie ist byte-gleich mit der Fassung im Repository, die getestet und angesehen wurde. Eine böswillig gebaute Datei mit passender Liste fängt das nicht ab, und die zweite Stufe kennt nicht jeden Sonderfall von glTF. Die Modelle kommen nur aus diesem Repository.

**Nach jedem Neu-Erzeugen eines Modells** die Liste neu schreiben (normales Python 3):

```
python tools/models/checksums.py
```

Ein Test (`ModelChecksumsTests.The_checked_in_list_matches_every_checked_in_model_file`) wird rot, wenn Liste und Dateien nicht zusammenpassen.

Das Skript berechnet bei jedem Lauf alle drei Listen (Modelle, Texturen, Geräusche), schreibt eine Liste aber nur, wenn sich ihr Inhalt ändert, und dann immer mit LF. `.gitattributes` führt alle `*.sha256` mit `eol=lf`, ein Checkout legt sie also genauso ab. Ein Lauf ohne echte Änderung lässt `git status` darum leer, auch mit `core.autocrlf=true` (Issue #636). Früher zeigte Git die Listen danach als geändert: Das Skript schrieb LF über die CRLF-Fassung des Checkouts, und weicht die Dateigröße von der im Index gemerkten ab, meldet Git „geändert“, ohne den Inhalt zu vergleichen.

Nur nachsehen, ohne etwas zu schreiben:

```
python tools/models/checksums.py --check
```

Exit-Code `0`, wenn alle Listen zu den Dateien passen; sonst Exit-Code `1` und je betroffene Datei eine Zeile: Liste fehlt, Datei ohne Eintrag, Eintrag ohne Datei, Datei mit anderer Größe oder Summe, oder eine verwaiste `.sha256` im selben Ordner, die kein Skript schreibt. `--help` zeigt nur die Hilfe, eine unbekannte Option bricht mit Exit-Code `2` ab; in beiden Fällen bleibt jede Datei unverändert (Issue #604). Selbsttest des Skripts (ohne Blender und dotnet): `python tools/models/checksums_test.py`.

## Ein neues Modell einbauen (Kiste, Pistole, Klinge, Fledermaus, Gorm, Tür, Treppe)

1. `.glb`, `.png` und Skript an ihren Platz, Zeile in `assets/HERKUNFT.md`, `python tools/models/checksums.py`.
2. Im Core eine Haltungs-Klasse wie `GhoulPose`: Teile mit Eltern (`RigPart`) und die Winkel je Zustand, mit Tests.
3. Im Game eine Ansicht hinter der Schnittstelle der Figur: `TexturedModel.TryLoad(pfad, Teile, log)` einmal nach dem Shader laden, je Bild `Rig.Pose(...)` und `TexturedModel.Draw(...)`. Fehlt das Modell, bleibt der Platzhalter.
4. Teile, die nicht immer zu sehen sind, zeichnet die Ansicht einzeln mit `TexturedModel.DrawPart(index, frame, tint)` – so lässt die Kiste ihr Schloss weg, sobald sie aufgeschlossen ist. `tint` hellt ein Teil auf oder dunkelt es ab (die pulsierende Vampirkiste).
5. Mehrere Modelle mit denselben Teilen (die vier Kisten-Stufen) laden je eine eigene Datei; fehlt eine, bekommt nur diese Stufe den Platzhalter ([`ChestView`](../src/SilverAndAsh.Game/Rendering/ChestView.cs)).
6. **Waffen in der Hand** ([`WeaponPose`](../src/SilverAndAsh.Core/Presentation/WeaponPose.cs) im Core, [`WeaponView`](../src/SilverAndAsh.Game/Rendering/WeaponViews.cs) und [`WeaponModels`](../src/SilverAndAsh.Game/Rendering/WeaponModels.cs) im Game):
   - **Spiegeln:** Die Kamera-Matrix der Platzhalter spiegelt (rechts ist dort −x). Die Dateien sind normale Modelle (+x = linke Seite der Waffe), deshalb beginnt jede Haltung mit `FileToCamera`, einer Spiegelung von x. Beide zusammen ergeben eine echte Drehung, die Flächen behalten ihre Vorderseite (getestet: Determinante +1).
   - **Verkleinern zum Auge hin:** Die Modelle werden im Kameraraum auf die Hälfte verkleinert (`ShrinkTowardsEye` = 0,5). Jede Ecke bleibt auf ihrer Sichtlinie, das Bild ändert sich also nicht – aber Lauf, Mündungsfeuer und Klinge bleiben innerhalb von 30 cm und tauchen nie in eine Wand (der Spieler hält 35 cm Abstand). Getestet auf allen Dreiecken in allen Haltungen; nur der Ärmel der Klingenhand reicht auf der Höhe des Schlags bis etwa 42 cm, am linken Bildrand.
   - **Nicht kleiner:** Raylib zeichnet nichts, was näher als **5 cm** am Auge liegt (`Rlgl.GetCullDistanceNear()`, nicht 1 cm). Bei 0,3 schnitt diese Ebene Handschuh und Ärmel ab: ein heller Keil statt der Hand.
   - **Maßstab im Skript einbacken:** Blender schrieb den Maßstab 1,8 als Knoten-Maßstab in die Datei; `GlbScene` lehnt solche Knoten ab. `bake_scale` in `tools/models/psx.py` überträgt ihn vor dem Export auf die Ecken.
   - Fehlt die Waffe **oder** ihre Hand, zeichnet die Ansicht für diese Waffe die alten Quader. Keule, Gewehr und Donnerbüchse sind noch Quader ohne Hand.
7. **Fledermaus** ([`BatPose`](../src/SilverAndAsh.Core/Presentation/BatPose.cs) im Core, `ModelBatView` in [`BatViews.cs`](../src/SilverAndAsh.Game/Rendering/BatViews.cs)):
   - Drei Haltungen (`BatStance`): **Schweben** schlägt die Flügel, der äußere Flügel folgt etwas später (`OuterWingLag`). **Kreischen** steht hoch statt breit, denn die Ankündigung des Sturzflugs muss im Umriss lesbar sein (getestet: Flügelspitze mehr als doppelt so hoch wie seitlich). **Sturzflug** legt die Flügel nach hinten; eine tote Fledermaus trudelt in dieser Haltung zu Boden.
   - Ein Flügel wird erst um seine Längsachse gehoben, dann nach hinten geschwenkt; der linke dreht gespiegelt.
   - Der Trefferkörper (`BodySizes.Bat`) ist schmaler als die ausgebreiteten Flügel; das Modell ändert ihn nicht (Issue #240).
8. **Gorm** ([`GormPose`](../src/SilverAndAsh.Core/Presentation/GormPose.cs) mit `GormChain` im Core, [`ModelGormView`](../src/SilverAndAsh.Game/Rendering/ModelGormView.cs) im Game):
   - **Zwei Bilder und ein drittes für Phase 2:** Körper und Glieder haben je eine Textur (`gorm_body.png`, `gorm_limbs.png`). `TexturedModel.TryLoad` nimmt zusätzlich `gorm_body_phase2.png` als Ersatzbild 0; in Phase 2 zeichnet die Ansicht Rumpf, Kopf und Kiefer damit (`DrawPart(..., alternate: 0)`). Fehlt eine der Dateien oder ihre Prüfsumme, bleibt der Platzhalter.
   - **Die Kette ist so lang, wie sie im Spiel reicht.** Die Glieder legt `GormChain.Lay` einzeln von der Faust zum Ziel; das Ziel kommt immer aus dem Core: beim Schwung `GormChain.SwingEnd` aus `Gorm.SwingArea` (Reichweite, Höhe des Treffers), beim Wirbeln vorher `WhirlEnd` mit derselben Reichweite, beim Wurf die Stelle des Hakens in der Simulation (seine Mitte liegt dort). Getestet in `GormPoseTests`.
   - **Decke:** Räume sind 3,5 m hoch, Gorm 3,2 m. Erhobene Keule und gewirbelte Kette bleiben darunter (getestet). Nur das Brüllen reckt die Fäuste noch in die Decke.
   - Kreis am Boden für den Hammer und Schockwelle zeichnet weiter `GormFloorEffects` aus dem Core-Zustand, wie beim Platzhalter.
9. **Türen** ([`DoorPose`](../src/SilverAndAsh.Core/Presentation/DoorPose.cs) im Core, [`DoorView`](../src/SilverAndAsh.Game/Rendering/DoorView.cs) im Game, Skripte `tools/models/door.py` für Holz-, Eisen- und Risswand-Tür und `tools/models/boss_portal.py` für das Bosstor, Issues #245 und #568):
   - **Eine Datei je Türart** (`door_normal.glb`, `door_locked.glb`, `door_boss.glb`, `door_cracked.glb`). Ursprung ist die Mitte des Durchgangs am Boden, der Durchgang läuft entlang z. Fest steht nichts (Issue #429): `leaf_a` hängt rechts (+x), `leaf_b` links, die Eisentür hat einen Flügel `leaf` mit dem Schloss `lock` daran. Bei Holz- und Eisentür trägt jeder Flügel seinen Teil von Kopfblende, Eisenbändern und Glas im Spitzbogen, so öffnet sich die Tür auf ganzer Höhe; die Flügel des Bosstors sind nur Holz und Eisen, ohne Glas (Issue #568). Die rissige Wand ist nur `cracks` – die Wand selbst zeichnet der Level.
   - **Steinrahmen** (Issue #303): Der Stein drumherum steht je Türart und Wand-Spielart in eigenen Dateien `door_frame_{normal,locked,boss}_{a,b,c}.glb` mit den Teilen `portal_front` (Seite +z der Datei) und `portal_back` (Seite −z). Ihr Atlas (128 × 64) trägt links die echte Wand-Kachel der Spielart (byte-gleich zu `textures/catacomb_wall*.png`), rechts dieselbe Kachel als Werkstein; die Steinflächen sind in Welt-Koordinaten gemappt (eine Kachel je Meter), so läuft die Mauer ohne Naht in den Rahmen. Welche Spielart auf welcher Seite liegt, sagt [`DoorFrameLook`](../src/SilverAndAsh.Core/Presentation/DoorFrameLook.cs) wie bei der rissigen Wand. Fehlt die Datei der Spielart, nimmt `DoorView` die von A; fehlt auch die, ist die ganze Tür der Platzhalter (Flügel ohne Rahmen hingen sonst in einem Loch). Ändert sich eine Wand-Textur, müssen `door.py` und `boss_portal.py` neu laufen – das prüfen Tests gegen `tools/models/door_frame_sources.sha256` und `tools/models/boss_portal_sources.sha256`.
   - **Bosstor** (Issue #568): Das Portal ist 4 m breit und 6 m hoch und steht einmal in der Mitte beider Zellen der Bosstür (`DoorFrameLook.CellsWide`). Eine Bosstür aus nur einer Zelle (heute nur im Musterraum, Issue #599) bekommt den Platzhalter.
   - **Rissige Wand im Gemäuer** (Skript `tools/models/cracked_wall.py`, Issue #246): je Wand-Spielart eine Datei `cracked_wall_a.glb`, `_b`, `_c` mit den Platten `panel_front`/`panel_back` (Riss im Stein der Mauer), `debris_front`/`debris_back` (Schutt am Fuß) und `chunk` (Brocken beim Zerschlagen). Welche Spielart auf welcher Seite liegt, sagt [`CrackedWallLook`](../src/SilverAndAsh.Core/Presentation/CrackedWallLook.cs) im Core. Fehlt eine Datei, zeichnet `DoorView` wie bisher `door_cracked`, fehlt auch das, den Platzhalter.
   - **Öffnen:** Der Core zählt den Fortschritt (`Door.OpenFraction`, 0..1) gleichmäßig je Tick über die Zeit aus `balance.json` (`doors`). Geglättet (`DoorPose.Ease`) wird nur im Bild. Die Flügel schwingen von dem weg, der geöffnet hat (`Door.OpenedFromLowSide`); 90° bei Holz- und Bosstür, 80° bei der Eisentür.
   - **Ost-West:** `DoorPose.Yaw` dreht die ganze Tür um 90°, so wird +z der Datei zu +x der Welt; die Regel für die Seite bleibt dieselbe.
   - **Durchgang:** Die Zelle ist wie bisher im Tick des Öffnens frei; die Flügel sind nur Bild.
10. **Wendeltreppe** ([`StairsLook`](../src/SilverAndAsh.Core/Presentation/StairsLook.cs) im Core, [`StairsView`](../src/SilverAndAsh.Game/Rendering/StairsView.cs) im Game, Skript `tools/models/stairs.py`, Issue #300):
   - **Eine Datei je Wand-Spielart** (`stairs_spiral_a.glb`, `_b`, `_c`) mit zwei Teilen, die sich in der Datei nicht bewegen: `stairs` (Spindel mit Fuß und Kopf, zwanzig Stufenplatten, Eisengeländer mit Pfosten und eckigem Handlauf ab der vierten Stufe bis zur 18.) und `glow` (die Lampe oben). Ursprung ist die Mitte am Boden, die Treppe füllt den Kasten von `Stairs` (±1,2 m, 6 m hoch wie der Bossraum; `Stairs.Height` folgt `RoomTemplate.RoomCeilingHeight`, Issue #723). Der Atlas trägt die echte Wand-Kachel der Spielart, auf den Trittflächen die Boden-Kachel desselben Buchstabens, dazu Werkstein, Messing und eine Eisenfläche (dunkle Stufen der Reihe Eisen) aus der Hauptpalette; der Atlas ist 256 × 512 (Ausnahme der Art Bible). 1228 Dreiecke je Datei, alle Körper geschlossen; die Lampe (`glow`) sitzt bei 5,75 bis 6 m. Die Treppe hat keine Kollision (`Stairs` in `Core`), das Geländer ändert keine Spielregel.
   - **Welche Spielart:** die der Wände des Raums, in dem die Treppe steht (`StairsLook.Variant`); ihre Zelle bestimmt `Stairs.CellIn` (die Bodenzelle nächst der Raummitte). Fehlt die Datei, nimmt `StairsView` die von A, fehlt auch die, den Platzhalter aus Kästen.
   - **Aufsteigen:** Die ganze Treppe kommt in `Stairs.RiseFraction` aus dem Boden (`StairsLook.Sink`, erst schnell, dann langsam); die Lampe leuchtet erst, wenn sie steht, und pulsiert (`StairsLook.GlowTint`). Ändert sich eine Wand- oder Boden-Textur, muss `stairs.py` neu laufen – das prüft ein Test gegen `tools/models/stairs_sources.sha256`.
   - **Keine Kollision:** Man kann noch in die Treppe hineinlaufen (Issue #302).
