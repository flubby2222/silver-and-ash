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


# Runde 2: Schalenbrunnen A in drei Ausführungen (Issue #438)

Der Mensch hat Form A gewählt (B und C verworfen), blaues Wasser und goldenes Funkeln: ja. Dazu: "Vielleicht mit Moos". Diesmal **mit Textur** nach Art Bible (64 x 64, höchstens 32 Farben, nur Palette, keine Mischung).

## Dateien (Runde 2)

- `brunnen2.py`: erzeugt alles dieser Runde (Geometrie mit UVs, Texturen, `.glb`, Bilder, GIFs, Blatt). Zwei Läufe liefern bitgleiche Dateien (SHA-256 aller Ausgaben verglichen).
- `blatt2.png`: Blatt mit A1/A2/A3, bereit und verbraucht, aus 4 m und 2 m im Spiel, Texturen, Maßstab, Empfehlung.
- `ansichten_a1|a2|a3.png`: vier Ansichten (vorn, Seite, hinten, schräg oben), bereit und verbraucht, helle Ansicht ohne Laterne und Nebel. `spiel_a1|a2|a3_<bereit|verbraucht>_<2|4>m.png`: Spielbedingungen.
- `textur_a1|a2|a3.png` (64 x 64), `texturen.png` (sechsfach vergrößert), `massstab2.png`.
- `wasser_a1|a2|a3.gif`: Wasserfall und Wasser, vier Bilder zu je 150 ms, saubere Schleife (helle Ansicht); `wasser_*_spiel.gif`: dasselbe in Spielbedingungen aus 3 m.
- `brunnen_a1|a2|a3_<bereit|verbraucht>.glb`: sechs Modelle (Bild 0 der Animation), alle geschlossen (`tools/models/check_closed.py`, 0 offene Kanten).

## Ausführungen

| | Ausführung | Maße x / y / z in m | Dreiecke bereit / verbraucht | Farben | Moos |
|---|---|---|---|---|---|
| A1 | Zweischaler: achteckiges Becken, schlanke Säule, eine Schale, Eisenspitze | 1,70 / 1,90 / 1,70 | 344 / 240 | 28 | wenig |
| A2 | Dreischaler: sechseckig, dreistufiger Sockel, zwei Schalen, Steinspitze | 1,70 / 2,30 / 1,96 | 372 / 264 | 28 | stark |
| A3 | Figurenbrunnen: breites flaches Becken, Sockel mit vier Eisen-Speiern, Kapuzenfigur ohne Hände | 1,81 / 2,22 / 1,81 | 384 / 308 | 25 | keines |

Alle im Rahmen für Requisiten (100 bis 400) und in einer Zelle von 2 x 2 m (A2 mit 1,96 m in z knapp; Kollision besser als Kreis mit Radius 0,95). Verbraucht = trockenes Becken mit Brocken und stumpfen Münzen, ohne Wasser und Fälle.

## Textur, Moos, Palette

- Eine Textur je Ausführung, 15 Felder (Wand, Deckplatte, Sockel, Säule, Figur, Eisen, trockener Boden, vier Wasserfall-Bilder, vier Wasser-Bilder), nur Palettenfarben, in Indexform gemalt, keine Mischfarben.
- Moos in Fugen, Sockel, Schalenrand und auf der Wetterseite (unten, an Fugen dichter) in harten Stufen. Es gibt in der Palette nur ein Grün: **Haut/Knochen 1 bis 3** (Moos = Stufen 1, 2, 3). Es ist die nächste passende Farbe, keine neue; siehe Frage 1.
- Wasser: Reihe Stoff. Funkeln: Messing/Gold 5 bis 7, fünf Punkte, wandern mit der Animation.

## Wasser-Animation, was der Einbau bräuchte

Vier Bilder im selben Atlas: `water0..3` (16 x 16, Diagonalwellen, Phase je Bild um ein Viertel) und `fall0..3` (8 x 16, Strähnen, je Bild vier Zeilen nach unten). Wasserplatten und Fälle sind **eigene Teile** (`water_*`, `falls`); die Animation tauscht nur deren UV-Rechtecke alle 150 ms, in harten Stufen, ohne Leuchten. Das Funkeln steckt in den Wasserbildern. Der Spielcode bräuchte: Brunnen-Modell mit getrennten Wasser-Teilen laden, einen Animationstakt (150 ms, vier Bilder) und das Verschieben der UVs dieser Teile (oder vier Texturen-Ausschnitte). Der Zustand verbraucht lässt die Wasser-Teile weg (zweites Modell oder ausgeblendete Knoten).

## Weiter wie in Runde 1

Alles zum Einbau in Core und Game (Zelle, Zustand, Zähler, Kollision, Datei in `assets/HERKUNFT.md`, Hörprobe vor dem Klang) gilt unverändert; die Maße oben ersetzen die von A.

## Empfehlung (nur Einschätzung)

**A2 Dreischaler**: die gestuften Schalen geben aus 4 m im Nebel die unverwechselbarste Silhouette, das Wasser blitzt auf jeder Stufe, das kräftige Moos erzählt Alter. A1 ist die ruhigere, schlichtere Wahl. A3 ist ohne Moos die schmuckloseste.

## Offene Fragen an den Menschen

1. Das Moos-Grün stammt aus Haut/Knochen 1 bis 3, weil die Palette sonst kein Grün hat. In Ordnung, oder soll eine eigene Moosreihe in die Palette aufgenommen werden?
2. Welche Ausführung (A1, A2, A3) und welche Moosmenge (keins, wenig, stark)?
3. Reicht die Animation (vier Bilder, 150 ms), oder soll beim Wurf zusätzlich etwas reagieren (Spritzer)?
4. Die Kapuzenfigur in A3 hat keine Hände und ist grob; soll sie als Figur lesbarer werden, oder ist eine Säulenfigur genug?


# Endfassung: Well of Sirius (Issue #438)

Urteil des Menschen zu Runde 2: Form **A2 Dreischaler** mit den **Texturen von A1**, Moos **wenig wie A1**, Wasser-Animation reicht, Name **Well of Sirius**.

## Dateien (Endfassung)

- `brunnen3.py`: erzeugt genau diese Fassung (Geometrie A2 aus `brunnen2.py`, Textur und Seed von A1: Moos 0,40, Seed 11). Zwei Läufe liefern bitgleiche Dateien (Blatt, beide glb, GIF per SHA-256 geprüft).
- `well_of_sirius_bereit.glb` (372 Dreiecke), `well_of_sirius_verbraucht.glb` (264 Dreiecke); beide geschlossen (`tools/models/check_closed.py`, 0 offene Kanten). Maße 1,70 x 2,30 x 1,96 m (x, y, z), 28 Farben.
- Teile in `bereit`: `body` 96, `column` 168, `water_basin` 20, `water_bowl1` 20, `water_bowl2` 20, `falls` 48. `verbraucht` hat nur `body` und `column`.
- `blatt3.png` (vier Ansichten aus 4 m in Augenhöhe, vier nah/von oben, verbraucht, Vergleich mit Runde 2, Textur, Spielbilder 4 m und 2 m, Maßstab), `wasser_final.gif` (hell) und `wasser_final_spiel.gif` (Spielbedingungen, 3 m), je vier Bilder zu 150 ms, `spiel_final_*.png`, `textur_f.png` (64 x 64), `textur_final_x6.png`, `massstab3.png`.

## Was von A1 übernommen wurde

Der gesamte Atlas: Wandstein, Deckplatte, Sockel, Säule, trockener Boden, Wasser- und Fall-Bilder, dazu dieselbe Moosmenge und derselbe Zufalls-Seed. Das Moos wächst nur in Fugen, am Sockel und am Schalenrand.

## Eigene Prüfung gegen Runde 2

- Fugenverlauf auf sechs Seiten: Eine Seitenfläche ist 0,80 x 0,50 m und zeigt das ganze Wandfeld (32 x 16 px), also zwei Steine breit und zwei Lagen. An den Kanten treffen sich Fuge und Fuge (untere Lage) beziehungsweise Steinmitte (obere Lage); das ergibt einen versetzten Verband über die Ecke, keine Sprünge. Auf A1 (acht Seiten à 0,62 m) war das Feld gut doppelt so stark vertikal gestreckt, auf A2 nur etwa 1,25-fach; die Steine sind hier also eher besser proportioniert.
- Verzerrung: Die schmalen Schalenränder (0,08 m hoch) zeigen das ganze Wandfeld und wirken als flache Bänder; das passt zu einem Rand. Spitze und Säule laufen kegelig, die Streifen der Säulentextur laufen mit. Keine sichtbaren Verzerrungen auf dem Blatt.
- Wasser und Funkeln lesen sich auf der Textur unverändert; aus 4 m im Spiel sind die Fälle als blaue Striche auf zwei Stufen sichtbar, das Funkeln nur nah.

## Offene Punkte für den Einbau (eigener PR, `Closes #438`)

- **Name:** im Spiel und im Code „Well of Sirius“ (Datei etwa `assets/models/well_of_sirius_*.glb`, Skript nach `tools/models/`, Eintrag in `assets/HERKUNFT.md`, `checksums.py` laufen lassen).
- **Zustände:** zwei Modelle (bereit/verbraucht) oder ein Modell mit ausgeblendeten Wasser-Knoten (`water_*`, `falls`).
- **Animation:** Spielcode tauscht alle 150 ms die UV-Rechtecke der Teile `water_*` und `falls` (Atlas-Felder `water0..3` und `fall0..3`, Reihenfolge im Skript `RECTS`). Alternativ vier Atlas-Ausschnitte.
- **Kollision:** Grundfläche sechseckig, Umkreis etwa 0,98 m (Sockel); als Kreis mit Radius 0,95 m ansetzen. Zelle 2 x 2 m: A2 füllt in z 1,96 m, also knapp.
- **Moosfarbe:** Haut/Knochen 1 bis 3 (nächstes Grün der Palette); der Mensch hat das Aussehen gebilligt, eine eigene Moosreihe ist damit nicht nötig.
- Texturen 64 x 64: die Maße der Art Bible für Props sind erfüllt; Skript wiederverwendbar, wenn das Wasserfeld später anders gemalt werden soll.
- Ein Spritzer beim Wurf ist laut Urteil nicht verlangt; der Hörprobe-Hinweis (Klang) bleibt unverändert.