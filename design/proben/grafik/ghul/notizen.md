# Ghul – Probestück 1, zweite Fassung (2026-11-16)

Erzeugt von [`ghul.py`](ghul.py) mit Blender 5.3, ohne Fenster. Das Skript baut das Modell, exportiert [`ghul.glb`](ghul.glb) und rendert alle Bilder:

```bash
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/ghul/ghul.py
```

Die erste Fassung (Lumpen 92, 76, 80, Kopf ohne Gesicht) liegt in Commit `7f77aac`.

## Was sich gegenüber der ersten Fassung geändert hat

Die Leitung hat drei Punkte zurückgegeben. So habe ich sie umgesetzt:

1. **Rumpf-Farbe mit eigenem Farbton.** Zwei Vorschläge, beide aus 4 m und 10 m gerendert:
   - **Blaugrau 84, 100, 118** (im Modell und in den vier Ansichten). Kaltes, schmutziges Blau, Blau > Grün > Rot. Stein, Boden und Decke sind dagegen rosagrau (Rot > Blau > Grün).
   - **Leinen 148, 130, 100** (nur in den Spielbildern). Fahles Knochenbeige, Rot > Grün > Blau, heller als die Wand.

   **Mein Vorschlag ist Blaugrau.** In `spiel_10m_blaugrau.png` hebt sich der Rumpf als kühler Fleck klar vom warmen Boden ab, und der Ghul bleibt eine Gestalt. Leinen trennt sich ebenfalls vom Boden, liegt im Farbton aber zwischen Haut und Schädel. Dadurch verlieren Kopf und Arme an Abstand, und im Nebel wirkt der Rumpf bräunlich, also wieder näher am Stein. Blaugrau hat außerdem den größten Farbtonabstand zu allen drei anderen Farben des Modells.
2. **Gesicht von vorn.** Vorn sitzt jetzt eine Gesichtsplatte, um 12° nach oben gekippt, weil der Spieler von oben schaut. Sie hat **zwei eingesenkte Augenhöhlen** (4,5 × 4 cm, 2,5 cm tief, zur Mitte hin schräg abfallend für den bösen Blick) und **ein eingesenktes Maul** (14 × 3,5 cm). Zwischen ihnen liegen Stege in Schädelfarbe, damit die drei Flecken nicht zu einem verschmelzen. Das war das Problem meines ersten Versuchs, die Vertiefungen direkt in den Kopf-Loft zu setzen. Die Platte verjüngt sich zum Kinn (unten 70 % der Breite), und ihre Seiten sind angeschrägt.
   - **Im Bild nachgemessen** in `spiel_4m_blaugrau.png`: jede Augenhöhle 4 × 4 Pixel, das Maul 10 × 4 Pixel.
   - Auch aus 10 m sind Augen und Maul noch als dunkle Punkte zu erkennen.
   - Die hellen Augen-Pyramiden sind weg. Die Oberseite des Unterkiefers ist jetzt dunkel; sie wird sichtbar, sobald das Maul aufgeht.
3. **Ankündigung.** Die Ausholen-Haltung ist aus 4 m gerendert, daneben die Ruhehaltung in `vergleich_ruhe_ausholen_4m.png`. Sie bewegt die Teile genau wie der Platzhalter in `GhoulViews.cs`: Der Rumpf richtet sich um 22° auf (Drehpunkt sind die Füße), die Arme gehen von 25° auf 140° hoch, der Kiefer öffnet sich um 30° (ein Wert von mir, der Platzhalter hat keinen Kiefer). Die Silhouette wechselt von breit und flach zu hoch und schmal mit zwei Krallen über dem Kopf, und das offene Maul wird zu einem großen dunklen Fleck. Den Unterschied sieht man sofort.

Dazu der **Vergleich der Helligkeitsregeln** für die Stil-Abnahme: `spiel_4m_blaugrau.png` (Sechs-Stufen-Regel des Spiels) gegen `spiel_4m_stufenlos.png`, nebeneinander in `vergleich_helligkeit_4m.png`.

Die stufenlose Regel mischt die sechs Helligkeiten des Spiels nach dem Quadrat der Normalen-Anteile: `nx²·(x-Wand) + ny²·(Boden/Decke) + nz²·(z-Wand)`. Auf den sechs Hauptrichtungen ist sie gleich wie die Spiel-Regel, dazwischen weich. Aus 4 m von vorn ist der Unterschied klein: Bei der stufenlosen Regel bekommen die schrägen Flächen von Rumpf und Beinen eigene Töne, so dass die Form etwas plastischer wirkt.

## Bilder

| Bild | Was |
|---|---|
| `ansicht_vorn.png`, `ansicht_seite.png`, `ansicht_hinten.png`, `ansicht_schraeg_oben.png` | 640 × 640, Lumpen Blaugrau, Flat Shading nach `FlatShading.cs`, kein Nebel, Hintergrund Nebelfarbe |
| `spiel_4m_blaugrau.png`, `spiel_10m_blaugrau.png` | Spielbedingungen, Lumpen Blaugrau |
| `spiel_4m_leinen.png`, `spiel_10m_leinen.png` | Spielbedingungen, Lumpen Leinen |
| `spiel_4m_ausholen.png` | Ausholen-Haltung aus 4 m (Blaugrau) |
| `vergleich_ruhe_ausholen_4m.png` | links Ruhe, rechts Ausholen, je 854 × 480 |
| `spiel_4m_stufenlos.png` | wie `spiel_4m_blaugrau.png`, aber mit stufenloser Helligkeit |
| `vergleich_helligkeit_4m.png` | links Sechs-Stufen-Regel, rechts stufenlos |

**Spielbedingungen** heißt hier: 854 × 480, 70° vertikal, Augen auf 1,6 m, 4 m bzw. 10 m waagerecht entfernt, Blick auf 0,5 m Höhe des Ghuls. Laterne und Nebel werden pro Fläche nach den Formeln der Art Bible berechnet. Der Ghul steht in einem Gang aus Boden, Wänden und Decke in den Spielfarben (5 m breit, 3,5 m hoch). Harte Pixel, keine Kantenglättung.

Geprüft (erste Fassung, Verfahren unverändert): Ein Hautpixel auf einer nach vorn zeigenden Fläche ist im Bild genau Grundfarbe · 0,82, der Hintergrund genau 16, 11, 20.

## Zahlen

**352 Dreiecke** (Richtwert 400; erste Fassung 264):

| Teil | Dreiecke |
|---|---|
| Körper | 120 |
| Kopf | 140 |
| Kiefer | 12 |
| je Arm | 40 |

Von den 140 Dreiecken des Kopfs gehen etwa 108 auf die Gesichtsplatte: das Raster der Vorderseite, die Wände der drei Vertiefungen und die schrägen Seiten. Wenn Dreiecke gespart werden müssen, dann dort. Möglich wäre etwa eine Zeile weniger unter dem Maul, dann säße das Maul direkt an der Unterkante.

Maße in Spiel-Achsen (Meter, Ursprung zwischen den Füßen auf dem Boden), aus der exportierten Datei gelesen:

- **Höhe:** höchster Punkt 0,98 m (Schädeldecke), passt zu `BodySizes.Ghoul.Height = 1`.
- **Kopf und Kopfzone:** Kopf mit Kiefer von 0,70 bis 0,98 m hoch, 0,23 m breit, von 0,19 bis 0,50 m vor der Körpermitte. Das liegt in der Kopfzone (`HeadHeight` 0,32 → ab 0,68 m, `HeadWidth` 0,3, `HeadFrontStart` 0,19).
- **Über den Radius hinaus:** Die Ellbogen reichen seitlich bis 0,46 m, die Krallen nach vorn bis 0,64 m, also etwas über `Radius = 0,4` hinaus. Beim Platzhalter reichen die Hände bis 0,56 m.

## Farben (sRGB)

| Farbe | RGB | Wo |
|---|---|---|
| Haut | 124, 132, 112 | Arme, Beine (wie der Platzhalter) |
| Lumpen | 84, 100, 118 (Blaugrau; Variante Leinen 148, 130, 100) | Rumpf, Fetzen am Becken |
| Schädel | 170, 172, 144 | Kopf, Kiefer, Krallen, Wirbel – die hellste Farbe, damit der Kopf als Ziel heraussticht |
| Schlund | 30, 20, 26 | Augenhöhlen, Maul, Oberseite des Unterkiefers – macht den Kopf von vorn zum Gesicht |

Vier Farben. Kein Rot, kein Gelb, keine Stufenfarbe. Der Schlund ist dunkelviolett-grau, nicht rot.

## Teile und Drehpunkte

Unverändert gegenüber der ersten Fassung. Jedes Teil ist ein eigener Knoten in `ghul.glb`, sein Ursprung ist der Drehpunkt. Angaben in Spiel-Achsen (x seitlich, y oben, z vorn):

| Teil | Drehpunkt | Bewegung im Code |
|---|---|---|
| `body` | 0, 0, 0 | ganzer Ghul (Aufrichten, Ausfallschritt, Zusammensacken wie beim Platzhalter); die Beine sind fest daran |
| `head` | 0, 0,74, 0,24 (Hals) | Nicken, Zucken bei Treffer |
| `jaw` | 0, 0,77, 0,30 (Kiefergelenk) | Maul öffnen beim Ausholen (im Bild 30°). Dreht sich **mit dem Kopf** (Kopf-Matrix × Kiefer-Drehung). Im glTF ist er kein Kind des Kopfs, sondern liegt in Ruhehaltung daneben |
| `arm_px`, `arm_nx` | ±0,27, 0,66, 0,14 (Schulter) | Drehen um die x-Achse: Kriechen, Heben beim Ausholen, Schlag |

**Ruhehaltung der Arme:** Die Linie von der Schulter zur Kralle zeigt etwa 30° nach vorn (Platzhalter: 25°). Der Code dreht also um `armTilt − 30°`, nicht um `armTilt`. Die Funktion `pose()` im Skript zeigt das genau so.

## Vom Modell ins Spiel

Von der Leitung angenommen:

- **Format:** `.glb`
- **Achsen:** Export mit +Y oben, damit gelten in der Datei die Achsen des Spiels
- **Ursprung:** zwischen den Füßen auf dem Boden
- **Ablage:** `assets/models/ghoul.glb`, Erzeuger-Skript `tools/models/ghoul.py`

Für den Einbau gilt weiter, was in der ersten Fassung stand:

- **Farbraum:** glTF speichert Vertex-Farben linear. Beim Laden muss das Spiel `mesh.colors` einmal von linear nach sRGB zurückrechnen. Neu hinzugekommen ist die sehr dunkle Schlund-Farbe; auf dem Umweg über 8 Bit kann sie um ein bis zwei Stufen abweichen. Das ist unkritisch, sie soll nur dunkel sein.
- **Schattierung:** Die Datei enthält nur die reinen Grundfarben. Die Helligkeit je Fläche berechnet das Spiel nach dem Drehen aus der Welt-Normale.
- **Knoten:** Wie Raylib die Teile beim Laden zuordnet (Reihenfolge der Meshes, Drehpunkte nur im Code), hat die Leitung an die Code-Sitzung weitergegeben.

## Wo ich unsicher bin

- **Maske statt Schädel?** Von vorn und aus Spielentfernung liest sich der Kopf jetzt klar als Gesicht. Schräg von oben und von der Seite (`ansicht_schraeg_oben.png`, `ansicht_seite.png`) sieht man aber, dass die Gesichtsplatte ein eigener Klotz vor dem Schädel ist. Der Kopf wirkt dort ein wenig wie mit einer Maske. Außerdem ist die lange, spitze Schnauze der ersten Fassung einem flacheren Gesicht gewichen; vorgeschoben ist der Kopf weiterhin. Wenn das stört, wäre der nächste Schritt, Schädel und Platte als einen Körper zu bauen, so dass der Schädel-Loft hinten direkt in den Rand der Platte übergeht. Das kostet aber ein eigenes Netz statt der zwei Bausteine.
- **Lesbarkeit des Gesichts:** Die Augenhöhlen haben aus 4 m gerade die verlangte Größe. Wird der Ghul im Spiel kleiner gesehen, etwa schräg von der Seite, verschwinden sie zuerst.
- **Ausholen-Haltung:** In der Ausholen-Haltung reichen die Krallen bis etwa 1,4 m hoch, über `Height = 1` hinaus. Das ist als Warnung gewollt (wie beim Platzhalter), aber Modell und Trefferkörper passen in dem Moment nicht zusammen. Ob Treffer auf die erhobenen Arme zählen sollen, ist eine Spielregel-Frage.
- **Stufenlose Helligkeit:** Aus 4 m von vorn ist der Unterschied klein. Deutlicher wäre er in der Seitenansicht oder bei bewegten Armen. Ich habe nur das verlangte Bild gerendert.
