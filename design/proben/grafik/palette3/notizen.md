# Hauptpalette, Runde 3 (Issue #279): der heutige Stein ist der Maßstab

**Urteil des Menschen zu Runde 2:** zu bunt, regenbogenartiges Muster durch die Wiederholung der Texturen; der heutige Stand ist besser; Kontraste, die nicht ins Leitbild passen, für Akzente aufsparen. Daraus: **eine Fläche, eine Rampe**, kein Warm/Kalt-Einstreuen, kein zweiter Farbton im Stein.

**Blätter** (Spielfotos, Debug-Build `--photo`, Körnung und Nebel; nur die Umgebung ändert sich, Figuren, Waffen, Ärmel, Kisten bleiben wie in `main`):
- `blatt.png`: Spalten **heute | A | B**. Oben kleine Farbfelder: links die Grund-Reihen (Stein 15 Stufen, Haut/Knochen, Stoff), rechts die Akzent-Reihen (Holz, Eisen, Messing/Gold, Blutrot); beim heutigen Stand die Farben von Wand, Boden und Decke. Zeilen: Saal 4 m (Holztür, vier Kisten, Ghul); große Wandfläche aus 6 m; Holztür im Gang; Bosstor; Wand mit Riss aus 2 m.
- `blatt_kacheln.png`: Wand a (oben) und Boden a (unten), je 4 × 4 Kacheln gekachelt, Spalten heute | A | B: so sieht man die Wiederholung.
- Palette: `paletten/palette_stein.json` und `.gpl` (Reihen, Grund/Akzent, Katakomben-Auswahl). Neue Dateien nur zum Ansehen in `neu/A` und `neu/B`. Einzelbilder in `fotos/` (15).
- Skripte: `stein.py` (Rampe und Texturen A/B), `tueren_neu3.py` (Türen und rissige Wand), `patch_lokal5.py` und `fotos6.py` (Foto-Hilfe, stellt `src` und `assets` wieder her), `blatt3.py`.

## Die Palette (Stein aus dem heutigen Stand)
- **Stein (Grund), 15 Stufen:** Stufe 0 ist die Nebelfarbe 16/11/20; die Stufen 1 bis 14 sind **gleichmäßig in der Helligkeit** zwischen dem dunkelsten und dem hellsten Ton der neun heutigen Texturen verteilt (Lab L), jede Stufe ist das Mittel der Pixel um diese Helligkeit. Dadurch behält die Rampe den heutigen Farbton (violettgrau bis rosagrau) und die Abstände sind gleich (RGB 38/34/40 bis 151/127/137). Ein Farbton je Stufe.
- **Grund-Reihen (dürfen Flächen füllen):** Stein; Haut/Knochen und Stoff (für Figuren und Ärmel, wie in Runde 2, ändern sich in diesem Vergleich nicht).
- **Akzent-Reihen (nur kleine Flächen mit Zweck: Beute, Türen, Gefahr, später Props und Licht):** Holz (mit Worn 110/76/46), Eisen (mit Stahlblau 92/112/140), Messing/Gold (214/170/64), Blutrot (150/20/30 und 200/30/40). Das bunte Warm/Kalt aus Runde 2 ist weg: Wand, Boden und Decke bestehen nur aus der Stein-Reihe.
- **Katakomben-Auswahl, 24 Farben (vorher 24, jetzt nur Stein groß):** Stein (15), Holz 1–4 (die Türen), Eisen 1–3 (Beschläge), Knochen 6–7 (Bogenläufe des Bosstors). Alle Farben außer den vier Akzent-Reihen sind „Grund“.

## Die zwei Fassungen
- **A: heutiger Stand, in die Rampe einsortiert.** Jeder Pixel der neun Texturen (Wand, Boden, Decke, a/b/c) wird nach Helligkeit der nächsten Stufe zugeordnet. Eine Textur hat danach 5 bis 8 Farben statt 24 bis 20 (Streuung der Helligkeit praktisch unverändert), sieht im Spiel **praktisch gleich** aus. Der Gewinn ist die Palettendatei.
- **B: dasselbe, ruhiger und homogener** (nach der Prüfung unten): die grobe Helligkeitsfleckung (Wellenlänge über ~10 Pixel, die sich jeden Meter wiederholt) auf 30 % zurückgenommen, helle Ausreißer (Einzelsteine, Flecken über dem Mittel) auf 70 %; die Fugen bleiben. Die Streuung der Helligkeit sinkt bei der Wand von 5,7 auf 4,8, beim Boden von 4,6 auf 3,8, bei der Decke von 2,1 auf 1,3. Farbton unverändert.
- **Türen und Bogenfelder:** Der Stein der Türen (Feld „stone“: Bogenfelder und Pfosten) ist jetzt **die echte neue Wand-Kachel** der Fassung (A oder B), also dieselbe Rampe und dieselbe Fugenzeichnung wie die Wand; die Fugen des Türbogens passen. Den Rest der Tür (Holz, Eisen, Werkstein der Bogenläufe) habe ich nicht angefasst. Die rissige Wand ist auf den neuen Wänden neu erzeugt (nahtlos).

## Wiederholung der Kacheln (Punkt 4), auch im heutigen Stand
Ja, der heutige Stand wiederholt sich sichtbar, besonders an großen Flächen (Zeile 6 m und Gang im Blatt, und `blatt_kacheln.png`):
- **Wand:** helle waagerechte Bänder und helle weiche Flecken (Wertrauschen mit 8 Zellen je Kachel) liegen jeden Meter an derselben Stelle: es entsteht ein Gitter aus hellen Feldern; dazu die senkrechten Feuchtigkeitsstreifen (hellere und dunklere Bahnen) und ein, zwei auffällig dunkle Einzelsteine. Die Fugen selbst sind unregelmäßig und stören weniger.
- **Boden:** ein dunklerer Stein je Kachel und die ausgetretenen Mitten wiederholen sich als Raster.
- **Ursache:** die Zufallswerte der Töne hängen an der Kachel, nicht an der Wand; eine Kachel = 1 m.
- **B** nimmt die hellen Felder und Ausreißer zurück (siehe oben); das Gitter bleibt als leises Muster, verschwindet aber nicht, weil Fugen und Steinlagen dieselbe Kachel wiederholen. Mehr würde nur eine größere Kachel (zwei Meter) oder je Wandseite verschiedene Varianten bringen; beides ist eine Entscheidung für die Bausteine, nicht für die Palette.

## Abstände (Lab-ΔE, mittlere Farbe; Ghul unverändert aus `main`)
| | heute | A | B |
|---|---|---|---|
| Ghul–Wand | 18,3 | 18,7 | 18,6 |
| Ghul–Boden | 23,9 | 23,9 | 24,2 |
Regel 1 der Art Bible bleibt wie heute erfüllt.

## Empfehlung
**Fassung A als Hauptpalette der Katakomben** (Stein-Reihe aus dem heutigen Stand) und **B als Ziel für die neu gemalten Texturen** der Bausteine: Die Zeichnung von B ist ruhiger, ohne dass der Stein anders aussieht. Neue Bausteine malen ihren Stein mit dieser Rampe (Helligkeit in Stufen), nie mit einem eigenen Farbton. Die anderen Ebenen (Kerker, Schlosshalle) bekommen **eigene Stein-Reihen** mit demselben Aufbau (15 Stufen, ein Farbton), nicht mehr Farbtöne in einer Fläche. Akzente nur über die vier Akzent-Reihen.

## Was noch nicht stimmt
- Die Rampe ist aus den Pixeln gemittelt; Wand, Boden und Decke haben leicht verschiedene Farbtöne, die jetzt zu einem Farbton je Stufe verschmolzen sind (praktisch nicht zu sehen, aber nicht Pixel für Pixel gleich).
- B ist nur eine Bildfilterung der heutigen Textur (Rauschen und Fleckung zurückgenommen), keine neue Zeichnung; die Fugen und Steinlagen wiederholen sich wie vorher.
- Die Bogenläufe des Bosstors (Werkstein) und das Holz/Eisen der Türen sind unverändert; sie gehören zu den Akzenten bzw. werden bei den Bausteinen angepasst.
- Die Fassung der großen Wandfläche aus 6 m liegt im Saal vor der Laterne dunkel; die Wiederholung sieht man dort weniger gut als im Gang (Zeile 3 und 4).
- Haut/Knochen und Stoff sind unverändert aus Runde 2 übernommen (nur Figuren, nicht Teil dieses Vergleichs).
