# Probenblatt Statuseffekte am Gegner (#616)

Probestück außerhalb des Spiels. Nichts in `assets/` oder im Spielcode wurde geändert.

## Dateien

- `probenblatt.png`: das Blatt (Farbtabelle, Aufstellung bei 8 m und 12 m, Kontrolle ohne Effekt, drei Effekte gleichzeitig).
- `spiel_8m_A.png`, `spiel_8m_B.png`, `spiel_12m_A.png`, `spiel_12m_B.png`: Spielauflösung 854 x 480 mit Körnung.
- `spiel_dreifach_<1-4>_<4m|8m>.png`: drei Effekte auf einem Ghul (1 und 2: Marked + Holy Fire + Stun, A und B; 3 und 4: Grave Chill + Leech + Marked, A und B).
- `farbtabelle.csv`, `farbtabelle.md`: Effekt, Rolle, Reihe, Stufe (ab 0), RGB.
- `messwerte.md`: Effektpixel, Mittelfarben und Abstände bei 8 m.
- Skripte: `statuseffekte.py` (Hauptskript), `basis.py`, `effekte.py` (die Effekte, reines NumPy), `szene_ghul.py` (Szene mit `tools/models/psx.py`).

Aufruf aus dem Repository-Wurzelordner:

    "D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/statuseffekte-616/statuseffekte.py

## Vorgehen

Der Ghul (`ghoul.glb`, kleiner Kiefer-Ruhewinkel wie im Spiel) steht in einem Gang mit Laterne und Nebel nach den Shader-Formeln. Die Effekte werden als harte 2D-Pixel auf das fertige Bild gezeichnet, nach dem Nebel und vor der 15-Bit-Körnung: ungelit, aber vom Nebel verblasst, und mit der Wand gemeinsam gekörnt. Anker (Brust, Kopf, Boden) kommen aus der Kameraprojektion; die Größe skaliert mit dem Abstand. Nur Palettenfarben, kein Rot, keine weichen Verläufe.

## Vorschläge und Empfehlung

| Effekt | Variante A | Variante B | Empfehlung |
|---|---|---|---|
| Marked | Raute-Zeichen auf der Brust, aschweiß | pulsierender Umriss, 2 px | A. Das Zeichen bleibt auch bei 12 m lesbar und deckt den Ghul nicht zu; der Umriss ist bei Rudeln unruhig. |
| Holy Fire | gelbweiße Flammenzungen und Funken | Goldtönung und niedrige Flammen | A. Weiß-Gold Stufe 7/6, Spitze Stufe 4 (nicht Royal-Gold Stufe 5), Silhouette wird größer. |
| Grave Chill | Blautönung und Reif plus Bodenscheibe | Eiskruste an den oberen Kanten plus Bodenscheibe | B für Nahkampf, die Bodenscheibe trägt beide. Als Einziger hat er eine Fläche am Boden. |
| Stun | drei kreisende Funkenkreuze über dem Kopf | zwei Zickzack-Blitze | B. Zickzack ist die eindeutigste Form im Dunkeln; die Kreuze sind bei 12 m nur 3 px. |
| Leech | gestrichelter Faden von der Brust zum Spieler, Tropfen | dunkle Blautönung, Tropfen | A, aber schwächster Effekt (siehe Grenzen). |

Die Effekte lassen sich nach Form trennen: Zeichen auf der Brust (Marked), Flammen (Holy Fire), Bodenscheibe plus Blaufläche (Grave Chill), Zeichen über dem Kopf (Stun), Faden Richtung Kamera (Leech).

## Grenzen und offene Punkte

- Die Palette hat kein Violett. Leech nutzt die Reihe Stoff (Blauindigo) und liegt farblich nah an Grave Chill (Mittelfarbenabstand 21 in A). Getrennt werden beide über die Form. Frage an den Menschen: eine neue Palettenreihe „Violett“?
- Leech bei 12 m im Dunkeln (Variante B fast unsichtbar, A nur als Faden) ist der schwächste Fall. Da Leech keinen Zeitgeber hat, wäre eine kräftigere Tönung eine Folgearbeit.
- Zeitgeber über dem Kopf gehören zu #506 und sind hier nicht gezeichnet; Stun-Zeichen und Zeitgeber teilen sich den Platz über dem Kopf.
- Die Pulsphase ist im Blatt ein Standbild (t = 0); Zeitverläufe sind nicht animiert.
