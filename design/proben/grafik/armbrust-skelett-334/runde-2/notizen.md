# Skelett mit Armbrust, Runde 2: Wächter in drei Ausbaustufen (Issue #334)

Gewählt wurde Fassung A "Wächter"; B und C sind verworfen. Grundform, Maße und Haltung bleiben, die Stufen bauen Einzelheiten auf. Alles per Skript (`armbrust2.py`, Blender-Python), nichts heruntergeladen, `assets/` unberührt. Je Stufe zwei Haltungen: `ruhe` (Armbrust bereit vor der Brust) und `ank` (Ankündigung).

## Stufen

| Stufe | Dreiecke | Höhe x Breite | neu gegenüber der Vorstufe |
|---|---|---|---|
| Runde 1, A | 1134 | 1,86 x 0,97 m | Vergleich |
| A1 vorsichtig | 1344 | 1,86 x 1,08 m | Unterkiefer als eigenes Teil mit Zahnreihe, Brustbein als helle Platte, Schlüsselbeine, Schulterblätter, Schultergelenke, Darmbeinschaufeln, Knie- und Ellbogenknoten, Wirbelsäule hinten im Brustkorb; breites Lederbandelier, Schulterplatte links, breiter Tuchfetzen; Armbrust mit Griff, Nuss, Abzug, Steigbügel |
| A2 mittel | 1518 | 1,86 x 1,08 m | siebtes Rippenpaar, dickere Rippen, Wangenknochen, Kniescheiben; Köcher mit drei Bolzen hinten rechts, zwei weitere Tuchfetzen, Bogenwicklung |
| A3 reich | 1760 | 1,86 x 1,08 m | Dornfortsätze am Rücken, Kettenkragen am Hals, rostige Unterarmschiene rechts, zweite Schulterlamelle, Gürtel mit Beutel, Lappen hinten, Steigbügel als Bügel, Verschnürung, Eisenband, Abzugsbügel, Federn am Bolzen |

Je Teil (A3): Skelett 1172, Armbrust 306, Ausrüstung 234, Stoff 48. Beide Haltungen haben je Stufe gleich viele Dreiecke. Ziel höchstens etwa 1800 eingehalten.

## Haltungen

- `ruhe`: Armbrust waagerecht auf Brusthöhe, beidhändig (wie Runde 1).
- `ank`: Armbrust steht am Schaft des Gesichts auf Augenhöhe, leicht zur Mitte gedreht, Kopf nach vorn auf den Schaft geneigt, Kiefer offen, breiterer Stand mit Ausfallschritt, Ellbogen hoch und nach außen. Die roten Pupillen (Blutrot, hart, ohne Schein) sind das Einzige Rote. Aus 4 m trägt die Haltung (Armbrust auf Augenhöhe, Kopf auf dem Schaft); die Pupillen sieht man von vorn nur als zwei rote Punkte.

## Textur

Atlas 128 x 128, zwölf Felder, Farben nur aus der Hauptpalette: 23 Farben (ruhe) und 25 (angriff), Grenze 32. Keine Filterung, keine Verläufe, kein Dithering. Neu gemalt: Risse in Knochen und Schädel, Schmutz in den Gelenken, Zahnlücken, Holzmaserung mit Ast, Lederstich, Kratzer im Eisen, Rost mit Laufspuren, Kettengeflecht, hellerer Tuchfetzen mit Saum und Flecken.

## Regeln der Art Bible

- Regel 21 (geschlossen): `check_closed.py` meldet für alle sechs Modelle 0 offene Kanten. In A3 meldet es bei der Armbrust eine Kante mit mehr als zwei Flächen: Verschnürung und Bogenmitte durchdringen sich an einer Kante. Das ist kein Loch, nur eine geteilte Kante.
- Regel 22 (Rot): nur in den Pupillen, nur in der Ankündigung, keine roten Umrisse.
- Keine modellierten Hände: Fäustlingsblöcke.
- Mindestens zwei Ungleichheiten: Schulterplatte nur links, Bandelier und Köcher nur rechts, Unterarmschiene nur rechts, Tuchfetzen unterschiedlich lang und in zwei Farben, Armbrust nach rechts versetzt.
- Kleiner Kopf (Schädel unverändert, 1,05 fach).
- Regeln 1 und 7 (Masse aus 10 m): siehe Messung.

## Messung aus 10 m im Spiel (854 x 480, Nebel, Körnung, ohne Raum)

| Figur | Fläche px | mittlere Helligkeit |
|---|---|---|
| Ghul | 402 | 40,5 |
| Runde 1 | 504 | 52,8 |
| A1 | 551 | 53,6 |
| A2 | 592 | 52,5 |
| A3 | 602 | 51,3 |

Die Fläche wächst um 9 bis 19 Prozent (Schulterplatte, Bandelier, breiterer Tuchfetzen, Köcher). Die Helligkeit steigt kaum: Der Nebel und die Laterne drücken die hellen Knochen schon aus 10 m auf Werte nahe dem Ghul. Das bekannte Problem "dünn und dunkel" ist damit nur zur Hälfte gelöst; die Figur bleibt aus 10 m eher dünn. Hilfreicher wäre mehr Breite am Oberkörper (breitere Schulterplatten auf beiden Seiten) als mehr Helligkeit. Im Blatt steht dieselbe Reihe zusätzlich auf 45 Prozent abgedunkelt.

## Unsicherheiten

- Das Bandelier und die Armbrust verdecken in `ruhe` von vorn den Brustkorb weitgehend; die Rippen kommen hauptsächlich von der Seite und von hinten zur Geltung.
- Die Pupillen sind aus 4 m nur wenige Pixel groß; die Haltung muss die Ankündigung tragen.
- A3 liegt mit 1760 Dreiecken knapp unter der Grenze; der Mehrwert gegenüber A2 liegt vor allem in Einzelheiten, die aus 10 m verschwinden.

## Empfehlung

A2. Sie hat alles, was aus 5 bis 10 m lesbar bleibt (Schulterplatte, Bandelier, Köcher, Wangenknochen, Tuchfetzen), kostet 240 Dreiecke weniger als A3 und liegt mit 1518 bequem unter der Grenze. A3 lohnt nur, wenn der Mensch die Nahaufnahme (Kettenkragen, Schiene, Verschnürung) schätzt; aus 10 m ist sie von A2 kaum zu unterscheiden.
