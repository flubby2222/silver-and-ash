# Gruftwächter, Runde 5 (Issue 335)

Urteil aus Runde 4: "G2, aber das Gesicht soll schwarz sein - ein rotes Auge". Körper, Ausrüstung, Textur, Haltung und Maße sind G2 unverändert (Skript `gruftwaechter_r5.py` führt das r4-Skript aus und ersetzt nur den Kopf).

## Fassungen
- **H1**: Auge in Ruhe dunkel und matt (Stein, kein Rot), leuchtet nur in der Ankündigung in Blutrot.
- **H2**: Auge leuchtet dauernd rot; in der Ankündigung größer (6,2 x 4,1 cm statt 3,6 x 2,4 cm) und heller (Blutrot-Stufen 5 bis 7). H2 weicht damit bewusst von Art-Bible Regel 22 ab (Rot nur als kurze Ankündigung); Grund ist das Urteil des Menschen.

## Gesicht
Gesichtsfläche der Haube (2 x 2 Vierecke) komplett im dunkelsten Ton der Hauptpalette (Stein 0, gleich der Nebelfarbe), keine Züge. Das Auge ist ein Achtkant mit drei Stufen (Rand, Mitte, Kern), auf die Haubenkante gesetzt und per Strahl an die facettierte Haube angepasst.
Lage: mittig, weil aus 4 m die Scheibe auf der Blicklinie liegt und beim Drehen in der Ankündigung im schwarzen Gesicht bleibt; ein versetztes Auge läse sich als Wange oder Halbprofil.

## Zahlen
- Je Fassung 2944 Dreiecke (Grenze 3000); Auge 32 Dreiecke, Kopf 160.
- 0 offene Kanten, 0 Kanten mit mehr als zwei Flächen (`check_closed.txt`, `pruefung.txt`).
- Durchdringung in allen vier Zuständen nur das schon in Runde 4 erlaubte Paar Unterarm-Manschette gegen Stab.
- Höchstens 32 Farben, nur Palettenfarben (im Skript geprüft). Zweiter Lauf bit-identisch (`pruefsummen.txt`).

## Unsicher
- Das matte H1-Auge ist im Spielbild aus 4 m kaum zu erkennen (nur der schwarze Fleck); das ist so gewollt, ob es reicht, entscheidet der Mensch.
- Das schwarze Gesicht hat die Nebelfarbe; bei starkem Nebel verschwimmt es mit dem Hintergrund, die Haubenkante trägt dann die Silhouette.
- Das rote Auge ist aus 4 m etwa 2 bis 3 Pixel breit (H2 in Ruhe), an der Lesbarkeitsgrenze.
