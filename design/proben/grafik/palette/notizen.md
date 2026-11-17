# Hauptpalette (Issue #279): drei Vorschläge

**Blätter** (Spielfotos, Debug-Build `--photo`, Körnung und Nebel; Waffen im Bild):
- `blatt.png`: Spalten Palette 1 (Violettgrau), 2 (kalt, blau), 3 (warm, Ocker), 4 = heutiger Stand. Obere Zeile die Farbfelder (8 Reihen von oben: Stein kalt, Stein warm, Holz, Eisen, Messing/Gold, Stoff, Haut/Knochen, Blutrot; Stufe 0 links = dunkel, 7 rechts = hell; weißer Punkt = in der Katakomben-Auswahl). Beim heutigen Stand sind es die Farben der Level-Texturen nach Helligkeit. Darunter die Szenen: Saal mit Holztür, vier Kisten und Ghul aus 4 m und aus 10 m, das Bosstor von Level 0 (Seed 2), Gorm aus 12 m.
- `blatt_texturen.png`: Wand, Boden und Decke (Variante A) vorher und nachher, vierfach vergrößert (Spalten: heute, 1, 2, 3).
- Einzelbilder in `fotos/` (16); die Szene „Saal“ ist lokal gebaut (`patch_lokal4.py`), die anderen sind Szenen des Spiels.

**Dateien:** `paletten/palette_<n>.json` (Reihen mit Farbwerten, Katakomben-Auswahl, Prüfwerte) und `.gpl` (GIMP/Aseprite); erzeugt von `paletten.py`. Umgerechnete Kopien der Texturen und Modelle: `umgerechnet/<n>/` (nur zum Ansehen). Skripte: `umrechnen.py`, `fotos4.py`, `blatt.py`, `kennzahlen.py`.

## Wissen vorab (nur Nachschlagewerke, nichts übernommen, keine fremde Palette kopiert)
- gamedev.net „Color Palettes“ und Slynyrd „Pixelblog 1“: Eine Rampe ändert nicht nur die Helligkeit, sondern verschiebt den Farbton (Schatten kühler/violett, Lichter wärmer/gelblich); gleichmäßige Abstände der Helligkeit; wenige gute Rampen statt vieler Farben.
- Wikipedia „PlayStation technical specifications“, psx-spx: PS1-Texturen hatten 4 Bit (16 Farben), 8 Bit (256) oder 15 Bit; jede Textur hatte ihre eigene Farbtabelle. Das passt zum Plan: Die Hauptpalette ist der Vorrat, jede Textur (und jede Ebene) nimmt daraus 24–32 Farben.
- Angewendet: 8 Reihen × 8 Stufen; Ankerpunkte je Rampe (dunkel, Mitte/feste Farbe, hell) mit Farbton, Sättigung und Helligkeit, dazwischen linear; Schatten Richtung Violett/Blau, Lichter Richtung Gelb/Warm; Sättigung in der Mitte am höchsten.

## Aufbau (gleich in allen drei)
| Reihe | Inhalt |
|---|---|
| Stein kalt | Stufe 0 ist **die Nebelfarbe RGB 16/11/20**; Wände, Böden, Decken |
| Stein warm | Sandstein, Mauer, Putz, warm-braune Steine |
| Holz | Stufe 3 = **110/76/46** (Stufe Worn/Beggar, `ChestView.Colours`) |
| Eisen | Stufe 4 = **92/112/140** (Stahlblau, Forged/Guard) |
| Messing/Gold | Stufe 5 = **214/170/64** (Gold, Royal/King) |
| Stoff | Ärmel, Mäntel, Banner (dunkelblau bis bläulich hell) |
| Haut/Knochen | Haut der Untoten (olivgrau) bis Knochen und Spitze (hell, warm) |
| Blutrot | Stufen 3 und 5 = **150/20/30 und 200/30/40** (Vampiric, Mitte des Pulses) |
Die Stufenfarben stehen genau so in jeder Palette (sie bleiben erkennbar, weil sie feste Ankerpunkte sind und die Reihen um sie herum geführt werden). **Prüfwerte:** In jeder Palette sind alle 64 Farben auch nach der 5-Bit-Rasterung des Spiels verschieden (zwei Paare fielen zusammen und wurden um ein paar Stufen aufgehellt); der kleinste Abstand zweier Nachbarn in einer Rampe ist ΔE 5,4 bis 5,7 (Lab), der mittlere etwa 13.

## Die drei Vorschläge
1. **Violettgrau:** Stein kalt von der Nebelfarbe über Violettgrau zu warmem Hellgrau; Stein warm rosa-braun; wie heute, nur geordnet.
2. **Kalt und blau:** Stein kalt blau (Farbton 210–235), Holz mit blauen Schatten, Eisen und Stoff kühl, Gold blasser, Haut grünlich.
3. **Warm mit Braun und Ocker:** Stein grau-braun, Stein warm ocker bis sandfarben, Holz und Gold satter, Blutrot orangenah, Haut gelbolivgrün bis Knochen.
**Katakomben-Auswahl (je 25 Farben, markiert in den Feldern):** Stein kalt (8), Stein warm (Stufen 0, 2, 4, 6, 7), Holz (1–4), Eisen (1, 2, 3, 5), Haut/Knochen (4–7; siehe Bosstor-Befund unten: besser nur 6 und 7). Alle anderen Reihen (Blutrot, Stufenfarben von Holz bis Gold, Stoff, Haut der Gegner) sind in allen Ebenen gleich; für Kerker und Schlosshalle würde man die Auswahl der Umgebung wechseln (Eisen und Rost, warmer Stein und Holz).

## Wie ich umgerechnet habe, und was dabei kaputtgeht
Nächste Farbe (Lab, Helligkeit doppelt gewichtet), ohne Dithering in der Textur. Umgebung (Wand, Boden, Decke, Türen, Risswand) nimmt nur die Katakomben-Auswahl, Figuren, Waffen, Ärmel und Kisten die ganzen 64 Farben.
- **Flächen laufen zusammen (der größte Befund):** Die Wand-Textur hat 24 eng benachbarte Töne (weiche Flecken im Stein), jede Palette liefert dafür nur 3 bis 5 verschiedene Farben; Boden und Decke 2 bis 5 (siehe `blatt_texturen.png`). Das Steinbild wird flach und klotzig mit „Pfützen“; die Palette 2 macht aus dem Boden blaue Flecken, weil für das Violett der Textur kein Nachbar da ist. Hinzu kommt: Modelle mit 32 Farben kommen bei Palette 1 auf 8 bis 19, bei Palette 3 auf 10 bis 14 Farben; Kisten und Ghul verlieren ihre Feinheit (Ghul 32 → 7 bis 10), die Gesichtszüge bleiben aber lesbar. **Folgerung: Umrechnen mit „nächste Farbe“ taugt nur zur Vorschau. Für den Bestand müssen die Texturen mit den Rampen neu gemalt werden** (die Skripte der Texturen malen mit den Reihen statt mit freien Farben; Flecken und Fugen werden dann Stufen einer Rampe).
- **Regel 1 der Art Bible (Figur hebt sich vom Boden ab):** Abstand der Haut des Ghuls zur Wand (ΔE Lab, mittlere Farbe): heute 18, Palette 1 20, Palette 2 22, **Palette 3 nur 10** – dort läuft der Ghul in die Wand (Haut olivgelb, Wand grau-braun). Zum Boden: heute 24, 1: 28, 2: 31, 3: 21. Bei Palette 3 müsste die Haut-Reihe kälter/grüner werden oder der Stein weniger gelb.
- **Stufenfarben:** In allen drei bleiben die vier Kisten klar unterscheidbar (braun, blau, gold, rot) – die Kisten im Foto sind der Test; die Palette 3 macht Gold und Braun ähnlicher, die Palette 2 Blau und Stein.
- **10 m:** In allen vier Spalten fällt der Saal im Nebel gleich ab; die Kisten und der Ghul sind aus 10 m nur als dunkle Punkte zu erkennen, bei Palette 2 am wenigsten (dunkelblau auf dunkelblau).
- **Bosstor (helle, nahe Wand):** Die Wand wird in Palette 1 olivgrau statt rosagrau, in 2 violettblau, in 3 olivgelb. Bei 1 liegt das an meiner Auswahl: Die Haut-Stufen 4 und 5 (Olivgrau) sind in der Katakomben-Auswahl und ziehen das helle Rosagrau der Wand an sich. **Die Auswahl für die Umgebung sollte keine Haut-Stufen enthalten, nur Knochen (6, 7)**; dann bleibt Palette 1 näher am heutigen Stand.
- Was sonst noch kaputtgeht: HUD und Code-Farben (flache Quader, Platzhalter, TierColours, Nebel, Hintergrund) sind in dieser Probe nicht umgerechnet. Die Nebelfarbe (16/11/20) rundet das Spiel auf 5 Bit (16/8/16 etwa); sie ist Stufe 0 der Reihe Stein kalt.

## Empfehlung
**Palette 1 (Violettgrau) als Hauptpalette,** mit zwei Änderungen: die Katakomben-Auswahl ohne die olivgrauen Haut-Stufen 4 und 5 (Bosstor-Befund) und etwas kühlere Schatten im Stein (aus 2, Stufen 1–2). Gründe: Sie hält die Figuren am besten vom Stein getrennt (Ghul–Wand ΔE 20), sie ist am nächsten am bestätigten Stil (der Mensch hat „Ja, Richtung stimmt“ zum Violettgrau gesagt), und Warm für die Schlosshalle ist als Auswahl der Stein-warm-Reihe da. Palette 3 gehört in die Reihe Stein warm der Schlosshalle (Ocker-Stein), nicht in die Hauptreihe; Palette 2 liefert die Kerker-Anmutung (kalt, eisern).

## Was noch nicht stimmt
- Die Rampen sind nach Augenmaß aus Ankerpunkten gebaut, nicht geprüft an allen Texturen; der kleinste Abstand ΔE 5,4 ist knapp (die Körnung des Spiels mischt benachbarte Stufen).
- Der „heutige Stand“ in der Farbfeld-Zeile besteht aus den 64 gleichmäßig ausgewählten Farben der neun Level-Texturen, nicht aus einer Palette; er ist nur ein Vergleich.
- Nicht geprüft: Kerker, Schlosshalle, Ebenen-Paletten (nur die Katakomben-Auswahl), HUD, Waffenfeuer, Blut-Effekte, Treffer-Blitz.
- Die Umrechnung ändert nur Texturen; Farben im Code (ShadedBox, Nebel, Hintergrund, Wasserzeichen) bleiben und stimmen bei Palette 2 und 3 nicht mehr zur Umgebung.
