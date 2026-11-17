# Türrahmen als erster Baustein (Teil von #247), Probestück

Regeln aus [Formenlehre Gotik](../formenlehre/formenlehre.md) (#281), Abschnitt 2, 9 und 10, an den drei vorhandenen Türen umgesetzt. Nur der Rahmen ändert sich; Flügel, Beschläge und Look bleiben (Funktionen aus `tools/models/door.py`, unverändert übernommen).

## Blätter (Spielfotos, Debug-Build, Körnung und Nebel an; Waffen im Bild)
Markierung ohne Text: **roter Balken = heute** (Modelle aus `main`), **grüner Balken = neu**, blauer Balken = nur zur Ansicht (hohe Fassung bzw. hellerer Werkstein). In beiden Spalten liegt dieselbe Umgebung (Wandkacheln der Palette A, siehe unten).

| Datei | Inhalt |
|---|---|
| `blatt_normal.png`, `blatt_locked.png`, `blatt_boss.png` | je Türart: Spalten heute zu, neu zu, heute offen, neu offen; Zeilen Wand a 4 m, Wand a 10 m, Wand b 4 m, Wand b 10 m |
| `blatt_nahaufnahme.png` | Stelle, an der Rahmen und Wand zusammenstoßen: Spalten heute Kämpfer, neu Kämpfer, heute Sockel, neu Sockel; Zeilen Normal, Verschlossen, Boss A |
| `blatt_wand_c.png` | Wandvariante c, 4 m, heute und neu |
| `blatt_seitlich.png` | Tür in einer Ost-West-Wand (um 90° gedreht): laufen die Fugen auch dort weiter? Normal (Wand a) und Boss (Wand b) |
| `blatt_raum.png` | Boss A im 6-m-Raum von der Raumseite: heute, neu, neu mit hohem Relief; 4 m und 6 m. Weitere Ansichten des hohen Reliefs: `fotos/raum_boss_b_4m_hoch.png`, `raum_boss_b_6m_hoch.png` |
| `blatt_boss_hell.png` | nur zur Ansicht: Boss A mit Werkstein heller als die Stein-Rampe (blau) |
| `bewegung_normal_heute.png`, `bewegung_normal_neu.png`, `bewegung_boss_heute.png`, `bewegung_boss_neu.png` | Öffnungsbewegung als Übersichtsbild (`--clip door-normal-opening` bzw. `door-boss-opening`, `tools/clip.ps1 -Sheet`) |

Einzelbilder: `fotos/` (90). Prüfung der Bewegung: `pruefung_ergebnis.txt`.

## Was geändert ist
1. **Stein aus der Weltposition.** Alle Steinflächen (Bogenfeld, Läufe, Leibungen, Pfosten, Platten) bekommen ihre Textur-Koordinaten wie `SurfaceTexturing` (eine 64-px-Kachel je Meter, u am Wandverlauf, v mit der Höhe) und sind an den Metergrenzen in Stücke geteilt. Die Fugen laufen im Raster der Wand weiter: auf Wand a, b und c (`blatt_normal.png`, `blatt_wand_c.png`) und bei der um 90° gedrehten Tür (`blatt_seitlich.png`). Die Stein-Kachel ist **die echte Wandkachel der Variante** (64 × 64, bitgleich).
2. **Boss A: konzentrische Läufe.** Drei Bogenläufe, alle mit denselben Kreismittelpunkten, Ringbreite überall **0,125**; Läufe 1,00 / 0,875 / 0,75 / 0,625, Radien 1,75 / 1,625 / 1,50 / 1,375, Kämpfer 1,875, Scheitel außen 3,456, Tiefen 0,9375 / 0,625 / 0,3125. Alle Maße Vielfache von 1/16 m (außer den versteckten Scharnierpfosten).
3. **Kämpferplatte und Sockel.** Platten 0,125 hoch und 0,125 vor der Wand, mit Fase 1/16 an der freien senkrechten Kante (Kämpfer unter dem Bogenansatz, Sockel 0,50 hoch, Oberseite sichtbar). Pfosten und Bogenläufe sind **Stein**; Eisen nur als Beschlag (Band über dem Flügel, Scharniere).
4. **Normal und Verschlossen:** ein Lauf 0,125 breit, 1/16 hinter der Wandebene (Rücksprung), Bogen aus R = 1,5625 (Scheitel 3,458, passt unter 3,50). Der Rahmen ist nach innen gerückt (Öffnung 1,75 m); die Flügel behalten ihre Drehpunkte (±0,895, ±0,865) und liegen mit dem Scharnier hinter dem Pfostenband.
5. **Flügel unverändert,** bis auf **Boss A: Drehpunkte ±0,49 statt ±0,525**, damit der innerste Lauf auf dem 1/16-Raster (0,625) liegt und der offene Flügel (bis x = 0,62) nicht in den Lauf sticht; die Beschlagbänder der Flügel sind bei Boss A um 3 cm gekürzt (sie reichten sonst über die Mitte und stießen aneinander). Knotennamen und Reihenfolge bleiben.

## Zellhöhe des Bosstors (Auftrag 5)
Im Code geprüft: **Türzellen sind immer 3,5 m hoch**, auch zwischen Räumen von 6 m (`LevelGenerator.cs` Zeile 206 „Door cells are floor with the low standard ceiling“; `Door.BoxMax.Y` ist `cell.CeilingHeight`; Räume `RoomTemplate.RoomCeilingHeight = 6`). Darüber (3,5 bis 6 m) zeichnet `LevelGeometry` die Wand des Raumes (Zeilen 76 bis 78). Ein Lanzettbogen **in** der Zelle ist deshalb nicht möglich; der Scheitel bleibt unter 3,5.

**Zweite Fassung „hoch“** (`door_bosshoch_*.glb`): ein Lanzettbogen als **Relief 1/16 vor der Wand** auf der Raumseite um die Tür (Kämpfer 2,50, Läufe 1,375 / 1,25 breit, R = 2,9375, Scheitel 4,99 m, Ringbreite 0,125 mit Fase, eigener Sockel und Kämpfer). Er steht nur auf der **+z-Seite** der Datei (Knoten `portal_front`); der Code müsste die Tür so drehen, dass +z zum Raum zeigt, und die Fassung nur für das Tor zwischen Boss-Raum und Gang nehmen. Auf `blatt_raum.png` und in `fotos/raum_boss_b_*` zu sehen. Wirkt wie eine große gotische Rahmung (gut), bleibt als 1/16-Relief aber eine dünne Linie.

## Was die Code-Sitzung wissen muss
- **Dateien:** `door_<art>_<variante>.glb` mit Art normal, locked, boss und Variante a, b, c (9 Dateien, Textur eingebettet, 128 × 192, je 32 Farben) in `modelle/`. Dazu `door_bosshoch_{a,b,c}` (hohe Fassung) und `door_bosshell_{a,b}` (nur zur Ansicht, nicht einbauen).
- **Neue Knoten:** `portal` (fest, alles ohne Stein: Kopffeld, Beschlag, Glas), **`portal_front`** (Stein der +z-Seite) und **`portal_back`** (Stein der −z-Seite), beide mit Eltern `portal`, beide fest (`Turns` gibt Einheitsmatrix). `leaf_a`, `leaf_b`, `leaf`, `lock` wie bisher. `DoorPose.Parts` braucht also zwei Teile mehr je Art.
- **Wandvariante je Seite:** Wie bei der rissigen Wand: `CrackedWallLook.Variants(surfaces, x, z, side)` gibt (vorn, hinten); `portal_front` aus der Datei der Vorderseite zeichnen, `portal_back` aus der der Rückseite; `portal` und die Flügel aus einer der beiden (sind in allen Varianten gleich). Fehlt die Datei einer Variante: Fall zurück auf a, wie `textures.Resolve`.
- **Prüfsummen und Herkunft:** `python tools/models/checksums.py`; Einträge in `assets/HERKUNFT.md` je Datei. Zielskript: `rahmen.py` (liest die Flügelfunktionen aus `tools/models/door.py`); beim Einbau nach `tools/models/` verschieben und die Wandkacheln aus `assets/textures/` lesen (Aufruf-Argument: Kachel-Ordner).
- **Textur:** Atlas 128 × 192 statt 128 × 128 (zwei 64-px-Felder neu: „stone“ = die Wandkachel, „werk“ = dieselbe Kachel in der Stein-Rampe heller). Die Wandkacheln müssen die der Palette A sein (sechs bis acht Farben); mit den Katakomben-Kacheln von heute (24 Farben) blieben nur 8 freie Farben für Holz, Eisen und Glas.
- **Dreiecke:** Normal 1 132 (vorher 928), Verschlossen 780 (576), Boss A 1 100 (1 008), Boss hoch 1 276. Normal liegt um 3 % über dem Richtwert 1 100 der Formenlehre.
- **Ragt in die Nachbarzelle:** Kämpferplatte und Sockel stehen 0,125 vor der Wandebene (z = ±1,0 bis ±1,125), das Relief der hohen Fassung 0,0625. Die Kollision ändert sich nicht (Spieler hält 0,35 m Abstand zur Wand, Tür 1 m).
- **Öffnungsbewegung:** `DoorPose.Turns` und die Drehwinkel (90°, 80° beim Verschlossenen) bleiben; nur der Boss-Drehpunkt ist 3,5 cm weiter innen.

## Prüfung der Bewegung (`pruefung.py`, Blender-Python)
Vier Phasen, beide Richtungen, alle Rahmenteile als feste Fläche: **0 Durchstiche** der Flügel durch Läufe, Platten, Leibungen, Kopffeld und 0 zwischen den beweglichen Teilen, alle Ecken in der Zelle (`pruefung_ergebnis.txt`; geprüft Wand a, die Geometrie ist in allen Varianten gleich). Ausgenommen sind nur die Scharnierpfosten in der Türebene (|z| ≤ 0,16), in denen der Flügel sitzt (wie bei den Eisenpfosten vorher). Eine erste Fassung hatte beim Boss 104 Stiche zwischen den Beschlagbändern der beiden Flügel (Drehpunkt weiter innen); das Band ist gekürzt.

## Fotowerkzeug (nichts in `src/` oder `assets/` eingecheckt)
- `patch_lokal6.py` schreibt vorläufig eine Szene `frame-view` (Umgebungsvariablen für Art, Abstand, offen, Hallenhöhe 6 m, eigene Kamera, Ost-West), eine erzwungene Wandvariante (`SurfaceVariants.At`) und die zwei Steinknoten in `DoorPose.Parts` in `src`. Zurücksetzen: `git checkout src`.
- `fotos7.py` baut nichts: Es tauscht **nur im Ausgabeordner `bin/Debug`** Modelle, Wandkacheln und Prüfsummen-Listen und ruft die Exe direkt auf: **etwa 1,5 s je Bild** statt 40 s mit `dotnet run`. `python fotos7.py` (alles), `python fotos7.py <Präfix>`, `python fotos7.py --clips`.
- `rahmen.py` (Blender) erzeugt die Modelle, `blatt7.py` die Blätter, `pruefung.py` die Bewegungsprüfung, `pruefe_kacheln.py` zählt die Farben der Wandkacheln.

## Was noch nicht stimmt
- **Boss A ist schwächer als heute.** Die Stein-Rampe endet bei (151, 127, 137); der Werkstein von heute ist heller und neutraler (196, 188, 172). Mit den hellsten Rampenstufen liest sich der Bogen aus 4 m gut, aus 10 m nur noch als leichter Umriss (`blatt_boss.png`). `blatt_boss_hell.png` zeigt, was ein hellerer, neutralerer Werkstein bringt (x 1,45, zum Grau gemischt): **Vorschlag, die Palette um eine kurze Reihe „Werkstein“ (4 Stufen, gleicher Farbton, heller) zu ergänzen** – das ist eine Palette-Entscheidung des Menschen, nicht meine.
- **Normal und Verschlossen fallen aus 10 m weniger auf** als heute: Das dunkle Bogenfeld von heute war ein Blickfang, der neue Rahmen verschwindet absichtlich in der Wand. Bleibt der Rahmen stärker erwünscht, hilft der hellere Werkstein auch hier.
- **Beide Seiten einer Tür** zeigen mit nur einer Datei dieselbe Wandvariante; dafür sind die Knoten `portal_front`/`portal_back` da, im Spiel noch nicht erprobt (die Fotos nutzen eine Variante für beide Seiten).
- **Hohes Relief** nur 1/16 tief: Auf `fotos/raum_boss_b_*` eine feine Linie; ein zweiter Lauf oder ein Fries im Bogenfeld würde es kräftiger machen (nicht im Auftrag).
- **Wandzonen (#282)** sind noch nicht entschieden: Das Sockelband der Wand müsste an der Sockelplatte enden (Regel „Bandende = Bausteinkante“); das ist erst prüfbar, wenn die Zonen gebaut sind.
- Nicht geprüft: Tür in einem echten Seed-Level mit Räumen verschiedener Varianten, Treffer-Blitz und Tönung des Boss-Glases, Ton (Quietschen, Kette) – Hörproben brauchen den Menschen.
