# Treppe zur nächsten Ebene (Issue #300), Probestück mit zwei Entwürfen

## Was das Spiel heute zeichnet und tut (nachgesehen, nichts geändert)
- `Stairs` (Core): Wendeltreppe in der **Mitte des Boss-Raums**, erscheint nach Gorms Tod. Kasten `±1,2 m` (`Stairs.Radius`) und `4 m` hoch (`Stairs.Height`). Sie **steigt in 3 s aus dem Boden** (`Balance.Gorm.StairsRiseTime`, im Bild `rise = 1 − (1 − t)²`, die ganze Treppe wird nach unten verschoben, was unter dem Boden liegt, verdeckt der Boden). `CanInteract` erst, wenn sie ganz oben steht; dann „[F] Climb the stairs“ (`IInteractable`, Angebot „Climb the stairs“) → `StairsClimbed` → Sieg.
- `StairsView` (Game): Pfosten 0,35 m, 16 flache Kästen (Radius 1,2, 0,12 dick, 0,55 breit), 45° je Stufe, einfarbig grau (120/114/108), unten und oben ohne Abschluss. Oben ein Licht (Kasten 0,5 × 0,1 × 0,5, Messington 210/190/140), das mit `sin(seconds · 2)` zwischen 0 und 100 % **pulsiert** (Periode etwa 3,1 s), erst wenn sie steht.
- **Keine Kollision:** `Stairs` ist nur ein `IInteractable` mit Kasten; man läuft hinein und hindurch. Ausgelöst wird mit F in Reichweite, nicht durch Darauflaufen.

## Blätter (Spielfotos, Debug-Build, Körnung und Nebel; Saal 6 m hoch, Spieler vor der Treppe; Wandkacheln der Palette A in allen Spalten)
Balken oben: **rot = heute** (Kasten-Platzhalter), **grün = Entwurf 1 Wendeltreppe**, **blau = Entwurf 2 Abgangsportal**.

| Datei | Inhalt |
|---|---|
| `blatt_wand_a.png`, `blatt_wand_b.png` | Zeilen 4 m, 10 m; Wandvariante a bzw. b |
| `blatt_nah.png` | aus 2,6 m, von oben (1,9 m davor, Blick nach unten), von der Seite (5,5 m); Wand a |
| `blatt_wand_c_gang.png` | Wand c 4 m; Wand a 4 m in einem Raum von nur 4,2 m Höhe |
Einzelbilder: `fotos/` (27). Der Musterraum (`--showroom`) lässt sich nicht mit `--photo` verbinden; die Treppe steht dort mit dem Platzhalter und würde das Modell zeigen, sobald das Spiel es lädt (nicht möglich ohne Einbau).

## Gemeinsam (nach Formenlehre und Palette A)
- Stein: **die echte Wandkachel** der Variante (Wände, Sockel, Pfosten) und die **Bodenkachel** auf allen nach oben zeigenden Flächen (Stufen, Schwelle); Textur-Koordinaten aus der Weltposition, an Metergrenzen geteilt (wie bei den Tür-Rahmen). Der Mittelpunkt liegt auf einer Zellmitte (ungerade Meter), die Treppe wird nicht gedreht: Fugen und Platten laufen im Raster von Wand und Boden weiter. Einzige Ausnahme: beim Aufsteigen verschiebt sich die Treppe, die Fugen „schwimmen“ in den 3 s.
- **Werkstein** (die neue Reihe, vier Stufen) nur dort, wo er einen Zweck hat: beim Wendel das Kapitell (Schluss der Spindel, Ziel des Blicks), beim Portal Bogenring und Gesims (rahmen die dunkle Öffnung). Lampe/Glimmen: Messing/Gold. Sonst keine Farbe: Kontraste bleiben aufgespart.
- Beide Entwürfe liegen im Kasten von heute (±1,2 m, 4 m); Knoten **`stairs`** (Stein) und **`glow`** (Lampe bzw. Glimmen, damit das Spiel nur ihn pulsieren lassen kann; im Foto: Tönung `0,65 + 0,35 · sin(2 t)`). Textur 128 × 128, 23 bis 25 Farben (die Kacheln haben 6 bis 8).

## Entwurf 1: Wendeltreppe aus Stein (`stairs_spiral_{a,b,c}.glb`, 408 Dreiecke)
Zwei Sockelstufen (Achteck 1,1875 und 1,0625), achteckige Spindel (Radius 0,3125) bis 3,5 m, **12 Stufen** zu je 0,25 m Höhe und 45° (anderthalb Umdrehungen, Stufen als Keile von der Spindel bis 1,0625 m, so dass keine Lücken zwischen den Stufen klaffen), Kapitell aus Werkstein (3,5 bis 3,75), Lampe (3,75 bis 4,0). Bleibt, was das Spiel heute hat, nur mit Stein; Silhouette wie heute.
**Was nicht stimmt:** Aus 10 m verliert sich die dünne Silhouette im Nebel (wie heute); lesbar bleibt nur, dass etwas Helles steht. Der Akzent oben (Lampe) ist klein; die Lampe müsste größer oder heller sein, damit sie aus 10 m trägt. Stufen in der Mitte der Umdrehung zeigen Lücken zwischen den Windungen (nur für die Optik, nicht begehbar).

## Entwurf 2: Abgangsportal (`stairs_abgang_{a,b,c}.glb`, 820 Dreiecke)
Freistehende **Bogenwand** (2,375 breit, 0,375 dick, 3,5 hoch) mit spitzem Durchgang (R = 1,25, Scheitel 2,83), den eine **dunkle Fläche** füllt, davor auf **beiden Seiten drei Stufen** (0,1875 hoch) zur Schwelle; Bogenring (0,125, 1/16 vor der Wand, Fase) und Gesims aus Werkstein, unten an der Schwelle ein Messing-Glimmen (`glow`). Beide Seiten gleich, also muss der Code die Treppe nicht drehen. Der Auftrag dachte an eine Öffnung im Boden; die geht nicht, ohne den Boden des Levels zu ändern (der Boden wird vom Level gezeichnet, das Modell kann kein Loch hineinschneiden). Das Portal gibt den „Abgang ins Dunkle“ als Form.
**Was gut ist:** liest sich aus 10 m klar als Ausgang (hohes helles Gesims und Bogen, schwarze Öffnung), mit sparsamem Akzent.
**Was nicht stimmt:** Von der Seite ist es eine 0,375 m dünne Wand (Foto „seite“): ein Pfeiler, kein Portal. Der Kasten ist 2,4 m breit und 0,4 m tief, die Treppe müsste ausgerichtet werden (zu den Türen) oder ein zweites Portal im rechten Winkel bekommen (Kreuzform). Die Öffnung ist ein flaches Schwarz, keine Tiefe; ein Durchgang dahinter ginge nur mit einem Level-Eingriff. Das Glimmen ist matt und bräuchte die hellste Stufe.

## Was die Entwürfe für Kollision und Auslösen bedeuten würden (nichts davon geändert)
- **Wendeltreppe:** wie heute: kein Hindernis, F in Reichweite; die Spindel ist 0,6 m dick, man liefe durch sie hindurch (wie heute).
- **Portal:** eine massive Wand mit 0,375 m Dicke; **durch sie hindurchzulaufen fällt auf** (der Spieler stünde in der Mauer), anders als bei der dünnen Spindel. Es braucht daher eine Kollision (ein Kasten ±1,19 × ±0,19 und die Stufen als begehbare Rampe oder Kasten) oder eine kleinere Auslösereichweite von vorn. Das ändert Kollision und Auslösen und ist damit Entscheidung der Code-Sitzung/des Menschen, nicht dieses Probestücks. F löst aus wie bisher.

## Was die Code-Sitzung wissen muss
- Zwei Knoten `stairs` und `glow` je Datei (kein Eltern-Knoten), Ursprung Mitte am Boden, Dateien je Wandvariante (a, b, c); die Wandvariante der Zelle der Treppe (`SurfaceVariants.At`) wählt die Datei. Prüfsummen (`checksums.py`), `HERKUNFT.md`, Rückfall auf die Kästen von `StairsView`.
- `StairsView.Draw`: Aufstieg wie heute als Verschiebung der Matrix `place`; `glow` mit der heutigen Pulsfunktion als Tönung (`DrawPart(i, frame, tint)`).
- Das Skript `treppe.py` (Blender) liest die Wand- und Bodenkachel aus einem Ordner (Aufruf-Argument) und die Reihen `Stein`, `Messing/Gold` aus `assets/palette/hauptpalette.json`; die **Werkstein-Stufen** stehen dort als Konstante (171/159/164 bis 209/193/200, Stand des Türrahmen-PR) und müssen nach dem Merge aus der Reihe gelesen werden.

## Fotowerkzeug (nichts in `src/` oder `assets/`)
`patch_lokal7.py` (lokal: Szene `stairs-view` mit stehender Treppe im Saal, Umgebungsvariablen für Abstand, Höhe, Kamera, Modell, Wandvariante), `fotos8.py` (wie `tuerrahmen/fotos7.py`: Exe direkt, nur im `bin`-Ordner getauscht), `blatt8.py`. Hinweis an die Code-Sitzung: Eine Foto-Szene `stairs-view` gibt es noch nicht im Repository; `ShowroomExhibits` mit einer Treppe reicht dafür.
