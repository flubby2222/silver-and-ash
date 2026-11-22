# Bossraum-Tür und Vorraum (Probestück zu #549)

Erzeugt von `bossraum_tuer.py` (Blender-Python, nichts heruntergeladen). Probestück außerhalb des Spiels, keine Integration.

## Blätter

- `blatt-bossraum-tuer-549.png`: drei Entwürfe geschlossen, Augenhöhe 1,6 m, 6 m Abstand, Wandvariante a.
- `groessenvergleich-549.png`: Spieler 1,8 m und Gorm 3,2 m vor jedem Entwurf, Höhenmarken 2/4/6 m.
- `vorraum-549.png`: Blick aus dem Gang (Spielauflösung), zwei Schrägansichten, zwei Grundrisse.
- Einzelansichten je Entwurf in `ansichten/`: `<a|b|c>_vorn`, `_seite`, `_hinten`, `_schraeg-oben`, `_spiel` (854 x 480), dazu `vorraum_spiel.png`.
- Modelle in `modelle/`: je Entwurf `bosstor_gross_<x>_portal.glb` (Knoten `portal_front`, `portal_back`) und `_fluegel.glb`.

## Maße und Dreiecke

Portal füllt 4 m Wandbreite (zwei Zellen) und 6 m Höhe, Wandebenen bei z = +-1 (Zelle 2 m tief), Flügel in z = 0. Türöffnung 2 m breit. Textur eine 64-px-Kachel je Meter, Fugen im Raster.

| Entwurf | Form | Flügel | lichte Öffnung | Portal (Dreiecke) | Flügel (Dreiecke) | gesamt |
|---|---|---|---|---|---|---|
| a | Dreilauf-Spitzbogen, Strebepfeiler mit Fialen | zwei, Beschlagbänder, Mittelrippe, Kickplatten | 2 x 5,2 m (Scheitel), Portal bis 5,95 m | 1428 | 864 | 2292 |
| b | Sturzportal, zwei Stufen, Konsolen, Schlussblock, Gesims | einer, Eisenrahmen mit Feldern, Zugplatte | 2 x 5,0 m, Portal bis 6,0 m | 632 | 1528 | 2160 |
| c | Lanzett-Zweilauf unter Wimperg mit Krabben und Kreuzblume | zwei, X-Diagonalbänder, Querschienen | 2 x 4,9 m (Scheitel), Portal bis 6,0 m | 1292 | 1044 | 2336 |

Abweichung von "6 m hoch": Die Öffnung ist nicht 6 m hoch, weil die Läufe unter der 6-m-Decke bleiben müssen. Das Portal selbst misst 6 m, die lichte Öffnung 4,9 bis 5,2 m.

## Prüfungen

- Flügel: `check_closed.py` meldet alle drei geschlossen (0 offene Kanten).
- Portale: offen nur an Wandebene, Boden, Oberkante, seitlicher Zellgrenze und Türfuge, wie die bestehenden Stufenportale (Skript klassifiziert jede offene Kante, sonst bricht es ab). `check_closed.py` meldet sie deshalb als offen.
- Farben: Portal nur aus den Reihen Stein und Werkstein (per Assert); Flügel 32 Farben, Holz und Eisen wie `door_boss`, kein Rot (Assert). Kein Messing, kein Leuchtglas.
- Zwei Läufe des Skripts liefern bitgleiche Dateien (md5).

## Unterschiede

- a: höchste Wirkung aus der Ferne durch den dreifach gestuften Bogen; die Pfeiler rahmen die Tür als Turm-Paar.
- b: streng und schwer, am wenigsten Gotik, dafür die ruhigste Silhouette und nur eine Flügelfuge.
- c: Giebel (Wimperg) gibt die auffälligste Silhouette; die Spitzen stehen frei und sind das feinste Detail.

## Vorraum

Gang 2 m breit, 3,5 m hoch, mündet in der Rückwand eines 6 m hohen Raums; an der Stirnwand das Portal, in der Mitte der Teleporter (Platzhalter, Hornkristall-Form C).

- Empfehlung 3 x 3 Zellen (6 x 6 m): Der Teleporter steht frei, es bleibt 3 m Weg bis zur Tür, und die Tür lässt sich aus dem Gang in voller Höhe überblicken.
- Alternative 3 x 2 Zellen (6 x 4 m): kompakter, aber Teleporter und Tür rücken auf 2 m zusammen; die Tür ist nur aus der Nähe ganz zu sehen.

## Empfehlung

Entwurf a. Er liest sich im Nebel am besten, fügt sich mit Spitzbogen und Fialen in das Gotik-Setting und besteht aus zwei Flügeln, die sich in der Mitte schließen. Entwurf c ist die Wahl, wenn die Tür auffälliger werden soll, Entwurf b, wenn sie schlichter sein soll.

## Offene Fragen

- Wandvariante des Bossraums ist im Code nicht festgelegt; gezeigt wurde Variante a.
- Der Gang-Ausschnitt in `vorraum-549.png` zeigt die Tür aus der Distanz von 10 m; ob die Tür dort nahe genug steht, hängt von der Raumgröße ab.
- Die Teleporter-Form ist ein Platzhalter nach #436.
