# Ghul – Probestück 1 (2026-11-16)

Erzeugt von [`ghul.py`](ghul.py) mit Blender 5.3, ohne Fenster. Das Skript baut das Modell, exportiert [`ghul.glb`](ghul.glb) und rendert die sechs Bilder:

```bash
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/ghul/ghul.py
```

| Bild | Was |
|---|---|
| `ansicht_vorn.png`, `ansicht_seite.png`, `ansicht_hinten.png`, `ansicht_schraeg_oben.png` | 640 × 640, Flat Shading nach der Regel aus `FlatShading.cs`, kein Nebel, Hintergrund Nebelfarbe |
| `spiel_4m.png`, `spiel_10m.png` | 854 × 480, 70° vertikal, Augen auf 1,6 m, 4 m bzw. 10 m waagerecht entfernt, Blick auf 0,5 m Höhe des Ghuls; Laterne und Nebel pro Fläche nach den Formeln der Art Bible; Gang aus Boden, Wänden und Decke in den Spielfarben (5 m breit, 3,5 m hoch); harte Pixel, keine Kantenglättung |

Geprüft: Ein Pixel der Haut auf einer nach vorn zeigenden Fläche ist im Bild 102, 108, 92 = 124, 132, 112 · 0,82, der Hintergrund genau 16, 11, 20. Die Farbumrechnung in Blender verfälscht also nichts.

## Zahlen

**264 Dreiecke** (Richtwert 400): Körper 120, Kopf 52, Kiefer 12, je Arm 40.

Maße in Spiel-Achsen (Meter, Ursprung zwischen den Füßen auf dem Boden):

- Höchster Punkt 0,97 m (Schädeldecke), Körper ohne Kopf bis 0,88 m (Wirbel) – passt zu `BodySizes.Ghoul.Height = 1`.
- Kopf mit Kiefer von 0,70 m bis 0,97 m hoch, 0,23 m breit, beginnt 0,19 m vor der Körpermitte – liegt in der Kopfzone (`HeadHeight` 0,32 → ab 0,68 m, `HeadWidth` 0,3, `HeadFrontStart` 0,19).
- Seitlich reichen die Ellbogen bis 0,46 m, die Krallen nach vorn bis 0,64 m – etwas über `Radius = 0,4` hinaus, ähnlich wie beim Platzhalter (Hände bei 0,56 m).

## Farben (sRGB)

| Farbe | RGB | Wo |
|---|---|---|
| Haut | 124, 132, 112 | Arme, Beine (wie der Platzhalter) |
| Lumpen | 92, 76, 80 | Rumpf, Fetzen am Becken – heller als beim Platzhalter (62, 52, 54), sonst verschwindet der Rumpf aus 10 m im Nebel |
| Schädel | 170, 172, 144 | Kopf, Kiefer, Krallen, Wirbel – die hellste Farbe, damit der Kopf als Ziel heraussticht |
| Augen (Akzent) | 200, 226, 150 | zwei kleine Pyramiden; aus 4 m kaum, aus 10 m gar nicht zu sehen |

Kein Rot, kein Gelb, keine Stufenfarbe.

## Teile und Drehpunkte

Jedes Teil ist ein eigener Knoten in `ghul.glb`, sein Ursprung ist der Drehpunkt. Angaben in Spiel-Achsen (x seitlich, y oben, z vorn):

| Teil | Drehpunkt | Bewegung im Code |
|---|---|---|
| `body` | 0, 0, 0 | ganzer Ghul (Aufrichten, Ausfallschritt, Zusammensacken wie beim Platzhalter); Beine sind fest daran |
| `head` | 0, 0,74, 0,24 (Hals) | Nicken, Zucken bei Treffer |
| `jaw` | 0, 0,77, 0,30 (Kiefergelenk) | Maul öffnen beim Ausholen; dreht sich **mit dem Kopf** (Kopf-Matrix × Kiefer-Drehung). Im glTF ist er kein Kind des Kopfs, sondern liegt in Ruhe-Haltung daneben |
| `arm_px`, `arm_nx` | ±0,27, 0,66, 0,14 (Schulter) | Drehen um die x-Achse: Kriechen, Heben beim Ausholen, Schlag |

**Ruhehaltung der Arme:** Schulter zur Kralle zeigt etwa 30° nach vorn (Platzhalter: 25°). Der Code dreht also um `armTilt − 30°`, nicht um `armTilt`.

## Vorschlag zu „Vom Modell ins Spiel“

**Format: glTF binär (`.glb`).** Raylib lädt es mit `LoadModel` ohne Zusatzpaket, liest Positionen, Normalen und Vertex-Farben (`COLOR_0`), und Blender exportiert es mit Bordmitteln. OBJ kann keine Vertex-Farben, die Raylib liest.

- **Vertex-Farben ohne Material:** Export mit `export_vertex_color="ACTIVE"` und `export_materials="NONE"`. Jede Fläche hat eigene Ecken (Flat Shading, keine geteilten Normalen).
- **Achtung, Farbraum:** glTF speichert Vertex-Farben nach Norm **linear**, Blender rechnet beim Export um (Haut 124, 132, 112 steht als 51, 59, 41 in der Datei). Raylib übernimmt die Werte ungefragt. Beim Laden muss das Spiel `mesh.colors` einmal von linear nach sRGB zurückrechnen, sonst ist der Ghul viel zu dunkel. Nachgerechnet: Der Umweg über 8 Bit verfälscht keine der vier Farben um mehr als 1. Die Alternative (sRGB-Werte unverändert in die Datei schreiben) wäre kürzer im Code, aber die Datei wäre falsch und sähe in jedem anderen Programm zu hell aus.
- **Schattierung im Spiel, nicht in der Datei:** Die Datei hat die reinen Grundfarben. Die Helligkeit je Fläche muss das Spiel nach dem Drehen aus der Welt-Normale berechnen (dieselbe Regel wie `FlatShading.Brightness`), sonst stimmt sie bei bewegten Armen nicht mehr. Das geht per Shader (Normale ist in der Datei) oder in C# wie bei `ShadedBox`.

**Achsen und Einheit:** Export mit „+Y oben“. Dann gilt in der Datei genau das Spiel-System: x seitlich, y oben, z vorn (der Ghul blickt nach +z), 1 Einheit = 1 m. Im Skript schreibe ich jeden Punkt in Spiel-Achsen (`P(x, oben, vorn)`); in Blender blickt das Modell nach −y.

**Ursprung:** zwischen den Füßen auf dem Boden, wie beim Platzhalter. Jeder Teil-Knoten trägt seinen Drehpunkt als Verschiebung.

**Ablage:** ein Modell pro Figur, `assets/models/ghoul.glb` (englisch, wie alles im Spiel), Erzeuger-Skript `tools/models/ghoul.py`. Die Bilder bleiben bei der Probe unter `design/`, nur Skript und Modell wandern bei der Integration.

## Wo ich unsicher bin

- **Wie Raylib die Knoten lädt.** Soweit ich weiß, verschiebt `LoadModel` die Ecken jedes Knotens an seinen Platz (Ruhehaltung) und vergisst den Knoten selbst. Dann liegen die Drehpunkte nur noch im Code (wie heute `ShoulderHeight`), und die Reihenfolge der Meshes im `Model` ist die einzige Zuordnung zu den Teilen. Sicherer wäre, die Teile beim Laden über ihre Namen zu finden oder pro Teil eine eigene Datei zu schreiben. Das sollte die Code-Sitzung beim Einbau prüfen. Ob Raylib `COLOR_0` als 16-Bit-Wert liest (so exportiert Blender es), habe ich nicht im Spiel geprüft.
- **Helligkeitsregel und schräge Flächen.** Die Regel aus dem Spiel kennt nur sechs Helligkeiten und ordnet jede schräge Fläche ihrer Hauptrichtung zu. Bei Kisten passt das, beim Ghul fallen dadurch Nachbarflächen manchmal zusammen (zu sehen an Armen und Beinen in der Seitenansicht). Eine stufenlose Mischung je nach Normale würde die Form besser zeigen, wäre aber eine Stiländerung für alles. Entscheidung der Leitung.
- **Rumpf im Nebel.** Aus 10 m bleibt der Rumpf vor dem fernen Nebel dunkel; erkennbar sind vor allem Kopf und Arme. Die Gestalt liest sich trotzdem als geduckt mit langen Armen, weil der Rumpf sich vom Boden abhebt. Wenn er heller werden soll: Lumpen eher Richtung 105, 88, 92 – dann aber weniger Abstand zur Haut.
- **Kopf von vorn.** Von vorn sieht man nicht, dass der Kopf vorgeschoben ist; er sitzt im Bild über den Schultern. Er ist trotzdem klar abgesetzt (hellste Farbe, dünner Hals). Seitlich und schräg sieht man die lange Schnauze.
- **Augen** sind im Spiel praktisch unsichtbar. Sie kosten acht Dreiecke; sie können ohne Verlust weg oder größer werden.
- **Ausholen und Schlag** habe ich nicht gerendert, nur die Ruhehaltung. Ob die Arme hoch erhoben gut aussehen, zeigt erst das Spiel oder ein weiteres Bild.
