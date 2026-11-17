# Hauptpalette, Runde 2 (Issue #279): nur die Umgebung, neu gemalt

**Was sich gegenüber Runde 1 ändert:** Es ändert sich nur die Umgebung: Wand, Boden, Decke (alle Varianten a, b, c), die Türen samt Bogenfeld und die rissige Wand. Ärmel, Waffen, Ghul, Gorm, Kisten und HUD sind in allen Spalten genau die aus `main`. Wand, Boden und Decke sind **mit den Rampen neu gemalt**, nicht mehr „nächste Farbe“.

**Blätter** (Spielfotos, Debug-Build `--photo`, Körnung und Nebel):
- `blatt.png`: Spalten Palette 1, 2, 3, heutiger Stand. Oben die Farbfelder: die Reihen der Umgebung groß (Stein kalt, Stein warm, Holz, Eisen; weißer Punkt = Katakomben-Auswahl), die übrigen Reihen klein; beim heutigen Stand die Farben von Wand, Boden und Decke je eine Zeile. Darunter, von oben: Saal mit Holztür, vier Kisten und Ghul aus 4 m; derselbe aus 10 m; Gang mit Tür (Level 0, Seed 1); Bosstor (Seed 2); die Wand mit dem Riss aus 2 m.
- `blatt_texturen.png`: Wand a, Boden a, Decke a, Wand b, Wand c, Boden c, vierfach vergrößert; Spalten heute | 1 | 2 | 3.
- Einzelbilder in `fotos/` (20). Paletten: `paletten/palette_<n>.json` und `.gpl`. Neu gemalte Dateien: `neu/<n>/` (nur zum Ansehen).
- Skripte: `paletten2.py`, `texturen_neu.py`, `tueren_neu.py`, `fotos5.py`, `blatt2.py`, `kennzahlen2.py` (die Szene „Saal“ kommt aus `../palette/patch_lokal4.py`). Die Skripte in `tools/` sind **unverändert**: `texturen_neu.py` liest `tools/textures/catacomb.py` und `catacomb_variants.py` und lädt ihre Malfunktionen, ersetzt nur den letzten Schritt (`finish`).

## Korrekturen an den Paletten
- Katakomben-Auswahl **24 Farben** ohne die Haut-Stufen 4 und 5 (nur Knochen 6 und 7): Stein kalt (8), Stein warm (0, 1, 2, 4, 6, 7), Holz (1–4), Eisen (1, 2, 3, 5), Knochen (6, 7).
- Palette 1: kühlere Schatten im Stein (Stein kalt Stufe 2 von Violettbraun zu Blauviolett, 70/51/66 → 62/49/65).
- Palette 3: Stein kalt etwas rötlicher-grau (nicht gelblich), damit die olivgraue Haut des Ghuls nicht in die Wand läuft.
- Alle 64 Farben bleiben nach der 5-Bit-Rasterung verschieden; Nebelfarbe 16/11/20, Stufenfarben (Holz 110/76/46, Stahlblau 92/112/140, Gold 214/170/64, Blutrot 150/20/30 und 200/30/40) unverändert in jeder Palette.

## Wie die Texturen jetzt gemalt sind
Dieselbe Zeichnung wie heute (gleiche Steinlagen, Fugen, Feuchtigkeit, Risse im Boden, Pflaster, dieselben Zufallszahlen). Nur der letzte Schritt ist neu: Die Helligkeit des gemalten Bildes wird in **Stufen der Rampe Stein kalt** gelegt (Stein, der heller oder dunkler gewürfelt ist, springt eine Stufe; die Fugen und Feuchtigkeit liegen eine Stufe darunter); etwa jeder zehnte Pixel mit dem wärmsten Farbton (Feuchtigkeit, einzelne Steine) nimmt dieselbe Stufe der Reihe **Stein warm**; einzelne Pixel, die sich von allen vier Nachbarn unterscheiden, werden angeglichen. Wand: 6–8 Farben, Boden 4–6, Decke 2–3 (heute je 24, aber nur eine Stufe Abstand zwischen den Farben).
- **Was das kostet:** Die heutige Textur schwankt in der Helligkeit nur um etwa ±12 %, eine Stufe der Palette sind etwa 25 %. Die **feinen Flecken und weichen Übergänge gehen verloren**; Stein und Fuge bleiben, aber die Wand wirkt flacher und gröber, besonders aus 2 m (letzte Zeile des Blatts). Das ist keine Schwäche der Umrechnung, sondern der Palette: 8 Stufen je Reihe tragen weniger Feinheit als die 24 freien Töne. Wer mehr will, braucht zwei Reihen für Stein mit Zwischenstufen (z. B. 12 Stufen) oder muss die Zeichnung selbst kontrastreicher machen (größere Steinunterschiede statt weicher Flecken).
- Türen: umgerechnet auf die ganzen 64 Farben (nächste Farbe), sie haben viele gemalte Töne und leiden nur wenig (32 → 17 bis 21 Farben). Rissige Wand: **neu erzeugt** mit `tools/models/cracked_wall.py` (lokale Kopie der Pfade) auf den neu gemalten Wänden, damit die Platte nahtlos bleibt (13–15 Farben).

## Abstände (Lab-ΔE, mittlere Farbe; Ghul unverändert aus `main`)
| | heute | Palette 1 | Palette 2 | Palette 3 |
|---|---|---|---|---|
| Ghul–Boden | 23,9 | 26,3 | 32,0 | 23,3 |
| Ghul–Wand | 18,3 | 18,3 | 26,3 | **14,2** |
Regel 1 der Art Bible: Palette 1 hält den heutigen Abstand, Palette 2 ist besser (kühle Umgebung gegen den grünlichen Ghul), **Palette 3 liegt trotz der rötlicheren Steine bei 14 statt 18** – die Ocker-Anteile (Stein warm, Feuchtigkeit) liegen im Farbton nahe an der Haut. Besser wäre dort ein noch rötlicherer/violettstichiger Stein oder weniger Ocker in der Wand (nur im Kopf der Gangwände).

## Eindruck im Bild (nur Umgebung verglichen)
- **Palette 1:** wie heute, etwas gröber; die Farben von Stein und Boden bleiben, rötliche Flecken im Boden.
- **Palette 2:** deutlich kühler und blauer; der Boden wird zu blaugrauen Platten mit rosa Flecken (die warme Reihe fällt dort stark auf); Kisten und Tore heben sich gut ab.
- **Palette 3:** Stein brauner, mit gelben Flecken (die warme Reihe ist dort sandfarben-ocker, fällt aber auch auf); die Wand verliert Kontrast zum Ghul (siehe Abstand).
- **Die Stufenfarben der Kisten sind in allen Spalten unverändert und bleiben klar erkennbar** (die Kisten sind in dieser Runde nicht umgerechnet).

## Empfehlung
**Palette 1 als Hauptpalette.** Sie ändert am wenigsten und hält Regel 1. Palette 2 eignet sich als Kerker (kalt), Palette 3 nur als Auswahl für die Schlosshalle, nicht als Hauptreihe. Für die Steine der Katakomben schlage ich vor, die Stein-Reihen **um Zwischenstufen zu erweitern** (z. B. Stein kalt mit 12 statt 8 Stufen, 72 Farben insgesamt), damit Fugen und Flecken wieder zwei Töne unterhalb des Steins haben.

## Was noch nicht stimmt
- Wand, Boden und Decke haben weniger Feinheit als heute; aus 2 m sieht man Steinblöcke mit nur wenigen Tönen. Ob der Mensch das annimmt, ist die Frage des Bildes.
- Die Zufallsverteilung warm/kalt (das wärmste Zehntel der Pixel) ist ein Griff von mir: In Palette 2 und 3 fällt die warme Reihe als zweite Farbe stark auf; besser wäre eine Wahl je Stein (die Zeichnung kennt dann die Steinnummer, was ein Eingriff in die `paint_*`-Funktionen wäre, nicht nur in `finish`).
- Das Oberflächenbild der Decke ist mit 2–3 Farben fast flach; Decke im Saal (4 m) wirkt als dunkle Fläche (wie heute, nur gröber).
- Türen sind nur umgerechnet: die Steinteile der Bogenfelder (Texturfelder „stone“/„arch“) nehmen die ganzen 64 Farben statt der Katakomben-Auswahl; sie passen daher nicht Pixel für Pixel zur neuen Wand.
- Nicht geprüft: Kerker, Schlosshalle, Ebenen-Paletten außer der Katakomben-Auswahl, HUD, Waffenfeuer; Farben im Code (flache Quader, Nebel, Hintergrund) bleiben wie in `main`.
