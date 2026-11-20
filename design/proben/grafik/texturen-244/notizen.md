# Probestück Texturen (Issue #244): je drei weitere Fassungen

Der Bestand hat inzwischen je drei Fassungen (a, b, c; b und c aus `tools/textures/catacomb_variants.py`). Dieses Probestück liefert **d, e, f** für Wand, Boden und Decke. Alle: 64 × 64, nahtlos, 1 m je Kachel, nichts unter `assets/` geändert.

Erzeuger: `texturen.py` (Blender-Python, nutzt die Mal-Hilfen aus `tools/textures/catacomb.py`). Blatt: `blatt.png` (3 × 3 gekachelt neben Bestand a, je eine Zeile gemischt und im Laternenlicht). Gang in Spielauflösung: `im-spiel-bestand.png` (nur a, b, c) und `im-spiel-neu.png` (a, d, b, e, c, f, je 4 m Gang wechselnd; die zwei Wände versetzt). Belege: `belege.txt`.

## Die neun Texturen

| | d | e | f |
|---|---|---|---|
| Wand | Bossenquader: regelmäßige Lagen, Oberkante hell, Unterkante dunkel | Hochplatten: schmale, hohe Steine; in den Fugen wenig Moos (0,9 % der Pixel) | Putz mit Abplatzungen: glatter Putz, dazwischen Bruchstein |
| Boden | Quadratplatten 0,5 m, Mitte ausgetreten | unregelmäßige Polygonplatten, flache Fugen | Lehm und Kies, sehr ruhig |
| Decke | Kreuzrippen (Diagonalen) | Längsbahnen mit wenigen Querfugen | Kassetten 0,5 m |

## Regeln der Art Bible

- Nur Farben der Hauptpalette: Reihe Stein (15 Stufen); Moos nur Haut/Knochen 1 und 2, nur in Fugen von Wand e. Jeder Pixel wird auf die nächste Palettenfarbe gesetzt, kein Dithering. Eine Fläche bleibt in einer Rampe.
- Gleiche mittlere Helligkeit wie der Bestand (Wand 112, Boden 78, Decke 45), Decke bewusst dunkel und ruhig.
- Keine Risse auf kleinen Steinen; Tiefe nur als Licht von oben.

## Empfehlung

- **Gut, in den Bestand:** Wand d, Boden d, Boden e, Decke d, Decke f, Decke e. Sie lesen sich im gemischten Streifen und im Gang als Abwechslung im selben Stil.
- **Mit Vorbehalt:** Wand e (Moos ist kaum zu sehen, die hohen Platten wirken neben den Quadern etwas unruhig; Moos-Frage an die Leitung: Haut/Knochen ist das einzige Grün der Palette, Regel „Fläche bleibt in einer Rampe“ spricht dagegen; ohne Moos wäre e unbedenklich). Boden f (sehr gleichförmig, taugt nur als Gegengewicht, etwa für Schatz- oder Geheimräume).
- **Nicht empfohlen:** Wand f. Die Putzflecken wiederholen sich im Meterraster als erkennbares Muster und der Putz wird zur größten Fläche mit dem stärksten Kontrast der Wand; wenn überhaupt, mit kleineren, seltenen Flecken überarbeiten.
- Die Decken d und f sind auffälliger als der Bestand; als Raumart-Merkmal (Boss, Schatz) sinnvoller denn als Zufallsvariante.

## Unsicher

- Mit der Stein-Reihe allein haben die Decken nur 3 bis 4 Farben (die dunklen Stufen liegen weit auseinander); der Bestand nutzt 20 bis 24 Farben ohne Bindung an die Reihe. Dadurch wirken sie flacher.
- Nahtbeleg ist ein Randvergleich (siehe `belege.txt`); die Mal-Funktionen sind selbst periodisch.

## Prueflauf

Zwei getrennte Blender-Laeufe: sha256 aller neun PNG gleich (Vergleich der Summen per `sha256sum`, kein Unterschied).
