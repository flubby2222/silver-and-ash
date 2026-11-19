# Brunnen (Issue #438): Probestück, drei Entwürfe

Bezug: Issue #438. Nur Bild-Entwurf außerhalb des Spiels; kein Spielcode, nichts unter `assets/`.

## Dateien

- `brunnen.py`: erzeugt alles (Geometrie, Farben, `.glb`, Bilder, Blatt). Aufruf steht im Kopf des Skripts; zwei Läufe liefern bitgleiche Dateien (per SHA-256 geprüft).
- `blatt.png`: Probenblatt mit A/B/C, Zuständen, Maßstab und Empfehlung.
- `ansichten_a.png`, `ansichten_b.png`, `ansichten_c.png`: je vier Ansichten (vorn, Seite, hinten, schräg oben), bereit und verbraucht, auf der Bodenplatte einer 2 x 2 m großen Zelle.
- `spiel_<a|b|c>_<bereit|verbraucht>_<2|4|10>m.png`: 854 x 480, 70 Grad, Augenhöhe 1,6 m, Laterne, Nebel, 15-Bit-Körnung (Bayer 4 x 4).
- `massstab.png`: Frontansicht aller drei im gleichen Maßstab mit Meterstab, Maßfigur 1,8 m und der Kiste (Worn, 1,0 x 0,7 x 0,7 m).
- `brunnen_<a|b|c>_<bereit|verbraucht>.glb`: die sechs Modelle, Spiel-Achsen, Meter.

## Entwürfe, Maße, Dreiecke

| | Entwurf | Maße x / y / z in m | Dreiecke bereit / verbraucht |
|---|---|---|---|
| A | Schalenbrunnen, achteckig, freistehend, vier Zinnen, Mittelsäule mit Schale und Spitze | 1,37 / 1,78 / 1,37 | 382 / 338 |
| B | Wandbrunnen, Spitzgiebel-Platte mit Nische, Schlussstein, Eisen-Speier, Rinne; hält eine Wand (Wand bei z = 0, Front nach -z) | 1,56 / 2,55 / 0,76 | 184 / 172 |
| C | Schachtbrunnen mit Dach: Ring, zwei Pfosten, Giebeldach, Eisenwinde; verbraucht = Eisendeckel zu | 1,60 / 2,29 / 1,18 | 200 / 228 |

Alle liegen im Rahmen für Requisiten (100 bis 400 Dreiecke). Alle sechs Dateien bestehen `tools/models/check_closed.py` (0 offene Kanten); Ringe und Kegelstümpfe sind geschlossene Körper (Regel 21). Maßstab: A und C passen in eine Zelle (2 x 2 m) und lassen etwa 0,2 bis 0,3 m je Seite frei; B belegt nur 0,76 m Tiefe.

## Zustände (vorläufige Annahme)

Das Issue nennt keine Zustände. Ich habe angenommen: **bereit** (man kann Silber hineinwerfen) und **verbraucht** (die Höchstzahl an Würfen ist erreicht, siehe Abnahmekriterium „Höchstzahl an Würfen“). Jeder Entwurf zeigt den Wechsel anders: A trocknet aus (Becken leer, Brocken, stumpfe Münzen), B versiegt (kein Strahl, trockene Rinne, dunkle Spur), C wird verschlossen (Eisendeckel mit Riegelkreuz).

## Farben

Nur Farben aus `assets/palette/hauptpalette.json`, nach Flächenneigung in harten Stufen (Licht oben links), keine Textur, kein Rot.

- Stein: Reihe Stein (Becken, Platten, Säulen), mit Streuung ±1 Stufe je Fläche.
- **Wasser: Reihe Stoff (kalt, 2 bis 5).** Die Palette hat kein eigenes Wasser; Stoff ist die einzige blaue Reihe. Das ist eine Begründung für eine kleine Akzentfläche, keine neue Farbe. Frage 3.
- Eisen: Speier, Winde, Deckel, stumpfe Münzen. Messing/Gold 5: nur das Funkeln der Beute im Wasser (warm = „was der Spieler haben will“).
- Dachbalken und Pfosten in C sind Stein, nicht Holz (Holz ist Akzent für Türen und Kisten).

## Bildbedingungen und Grenzen

Die Bilder entstehen im Workbench-Renderer mit Flächenfarben aus der Palette; Laterne und Nebel werden je Fläche aus dem Abstand zur Kamera gerechnet (Raumkacheln 0,5 m), die Körnung zum Schluss am Bildschirmraster. Das ist eine Annäherung an das Spiel, kein Ersatz. Die Raumwände sind nur Kulisse und nicht geschlossen modelliert. Aus 10 m sind alle drei im Nebel kaum zu lesen (wie jedes Requisit); B ist dank der hellen Platte noch am ehesten als Form zu erkennen. Wasser und Funkeln sind aus 4 m wenige Pixel groß.

## Empfehlung

**C Schachtbrunnen**: das klassische Bild für „Silber hineinwerfen“, die größte Silhouette aus 4 m (Dach, 2,29 m), der deutlichste Zustandswechsel (Wasser mit Funkeln gegen geschlossenen Eisendeckel). A ist die Alternative mit der schöneren Nähe, aber kleiner. B ist die Wahl, wenn Zellen knapp sind (nur 0,76 m Tiefe). Nur eine Einschätzung des Grafikdesigners.

## Offene Fragen an den Menschen

1. Welcher Entwurf (A, B oder C)? Soll er die ganze Zelle füllen oder darf er an der Wand stehen?
2. Zustände: Reicht „bereit / verbraucht“? Soll der Brunnen nach dem Wurf kurz reagieren (Funkeln, Spritzer), oder ist das Sache des Effektdesigners?
3. Ist blaues Wasser (Reihe Stoff) als kleiner Akzent in Ordnung, oder soll es dunkel bleiben (Stein/Eisen)?
4. Soll das Gold-Funkeln „da ist noch etwas zu holen“ anzeigen? Es ist die einzige warme Farbe am Brunnen.
5. Soll C ein Dach haben (Kopfhöhe: Unterkante 1,95 m, Spieler 1,8 m, aber Pfosten schmal), oder lieber ohne?

## Was der Einbau in Core und Game bräuchte (nicht Teil dieses PR)

- **Core:** Brunnen als Ort/Zelle im Raster (heute gibt es ihn dort nicht): eigener Zelltyp oder Prop mit Zustand (bereit/verbraucht), Wurfzähler, Preis und Chancen aus `balance.json`, Zufall mit Seed im Spielzustand; Trefferkörper. Maße: A rund Radius 0,72 (Kollision etwa Kreis 0,75), B Rechteck 1,6 x 0,76 an der Wand, C Kreis 0,65 (Pfosten und Dach außen 0,8 in x). Ein Ereignis je Wurf für Klang und Effekt.
- **Game:** Modell laden (`assets/models/fountain_*.glb`, Skript unter `tools/models/`), Zustandswechsel über zwei Modelle oder getrennte Teile (Wasser, Deckel, Strahl als eigener Knoten; dann reicht eine Datei). Beim Einbau braucht das Modell eine Textur nach Art Bible (64 x 64, 16 bis 32 Farben); der Entwurf hier ist farbig je Fläche und noch nicht texturiert. Jede neue Datei in `assets/HERKUNFT.md`.
- **Generator:** höchstens ein Brunnen je Ebene (Abnahmekriterium im Issue); B braucht eine Wandzelle, A und C eine freie Zelle mit mindestens 2 x 2 m.
- Modell und Geräusche laufen laut Issue in eigenen Issues; die Hörprobe vor der ersten Integration bleibt Sache des Menschen.
