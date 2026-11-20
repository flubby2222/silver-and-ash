# Fallgitter mit Stein-Führung, Probestück zu Issue #540

Nur Probestück außerhalb des Spiels. Kein Einbau, kein Eintrag in `assets/`.

## Erzeugen

```
blender --background --python design/proben/grafik/fallgitter-540/fallgitter.py
```

Umgebungsvariablen: `FG_OUT` (anderer Ausgabeordner), `FG_MODELS_ONLY=1` (nur Modelle). Das Skript nutzt die Pipeline `tools/models/psx.py` und lädt die Türrahmen `assets/models/door_frame_<art>_a.glb` nur für die Bilder. Zwei Läufe mit `FG_MODELS_ONLY=1` liefern 16 byte-gleiche Dateien (Hashes verglichen, auch gegen die abgelegten Modelle).

## Aufbau

Zwei getrennte Modelle, damit das Gitter als Ganzes senkrecht verschoben werden kann:

- **Gitter** (`gitter_breit`, `gitter_schmal`): Ursprung Unterkante, Mitte. Verschiebung nur entlang y.
- **Führung** (`fuehrung_normal_{a,b,c}`, `fuehrung_boss_{a,b,c}`): Sturzring aus Stein, 12 Segmente, setzt die Leibung des Bogens in der Türebene fort. a, b, c sind Wandkacheln wie bei den Pfeilern.

## Maße (Meter)

- Stab 0,07 x 0,07, vierkant. Spitze unten, Länge 0,22, Pyramide.
- Querbänder 3 Stück auf 0,62 / 1,55 / 2,48, je 0,10 hoch, 0,10 tief. Oberes Band (Schiene) von 3,38 bis 3,50.
- Gesamthöhe 3,5. Stäbe enden oben bei 3,46.
- **breit** (normal und locked): 9 Stäbe, halbe Breite 0,85 (Band 0,855), Mittenabstand 0,204, **Lücke 0,134**. Passt in Normal (Pfosten innen 0,895) und Locked (0,865, etwa 1 cm Luft).
- **schmal** (boss): 5 Stäbe, halbe Breite 0,47 (Band 0,475), Mittenabstand 0,2175, **Lücke 0,1475**. Boss-Pfosten innen 0,49.
- Gitter 0,10 tief (z +-0,05), Türebene wie `door.py` (normal 0,07, boss 0,10).
- Hubstellungen in den Bildern: zu 0, halb offen 1,7, offen 3,4 (Unterkante über dem Scheitel 3,32).

## Dreiecke

Gitter breit 174, Gitter schmal 118, Führung je 100. Ein Stab mit Spitze ist ein einziger konvexer Körper (14 Dreiecke). Alle Modelle rundum geschlossen: `tools/models/check_closed.py` meldet "geschlossen", die Kantenzählung im Skript 0 offene und 0 mehrfache Kanten.

## Lesbarkeit in Spielauflösung (854 x 480, 70 Grad)

Gemessen auf Augenhöhe 1,6 m, frontal (Zeile `MESSUNG` im Lauf):

- aus 3 m: Stab 9 px, Lücke 14 px
- aus 8 m: Stab 3 px, Lücke 5,5 px, alle 9 Stäbe zählbar

Nichts flimmert oder verschwindet. Durchblick: der Gang dahinter ist erkennbar (`durchblick-gang.png`).

## Textur und Farben

Atlas 64 x 64 je Gitter (Stab links, Band rechts), nur Stufen 2 bis 5 der Reihe **Eisen**. Führung nur Reihe **Stein**. Flach schattiert, keine Vertex-Farben nötig. `check_palette` im Skript prüft beide Atlanten gegen die Hauptpalette.

## Bilder

- `zustaende/<zu|halb|offen>-<frontal|schraeg>-<3|8>m-<spiel|hell>.png`: 24 Bilder
- `uebersicht-<3|8>m-<spiel|hell>.png`: je Entfernung und Modus alle Zustände nebeneinander
- `detail-spitzen-querband.png`, `durchblick-gang.png`, `groessenvergleich.png`
- `rahmen-vergleich.png`: normal, locked, boss
- `vergleich-eisen.png`: Eisen-Reihe gegen neutraleres Grau. **Das Grau liegt außerhalb der Palette**, ist nur ein Vorschlag zu #543 und nicht als Modell abgelegt.
- `einzel-gitter.png`, `einzel-gitter-schmal.png`, `einzel-fuehrung.png`, `einzel-zusammen.png`: vorn, Seite, hinten, schräg oben
- `modelle/*.glb` und `.png`

## Schwächen

- Die Führung ist nur ein Ring ohne Nut. Das Gitter ragt im Bogenbereich in den Stein hinein (unsichtbar) und läuft nicht in einer sichtbaren Schiene.
- Das Eisen der Hauptpalette ist deutlich bläulich, neben dem rosigen Stein wirkt es kühl (siehe #543).
- Der Werkstein der Rahmen-Dateien im Boss-Rahmen wirkt sehr hell. Das liegt an den Rahmen, nicht am Gitter.
- Das Gitter ist rechteckig, der Bogen darüber spitz. Zwischen Gitterecke und Bogen bleibt eine Lücke, durch die man in den Stein sieht.
- Seitlich bleibt etwa 2,5 cm Luft zur Leibung.
- Gitter und Stäbe sind in 3 m und 8 m ausgewertet, nicht in 15 m.

## Offene Fragen

- Offen steht das Gitter über der Decke (Unterkante 3,4, Gesamthöhe 3,5, Decke 3,5). Im Spiel braucht es einen Schlitz oder es muss in der Wand verschwinden. Wer löst das?
- Halb offen (1,7) lässt eine 1,8-m-Figur nicht durch. Zählt das als offen für die Spielphysik oder erst bei 2,0?
- Ist ein neutraleres Eisen gewünscht (#543)?
- Soll die Führung eine echte Nut bekommen, auch wenn sie mehr Dreiecke kostet?
