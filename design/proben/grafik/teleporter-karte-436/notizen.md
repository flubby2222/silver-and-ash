# Zielwahl der Teleporter: drei Entwuerfe (Issue #477, Teil von #436)

Erzeugt mit `python teleporter_karte.py` (reines Python, Schrift A aus `../schrift`, kein Download, zweiter Lauf bitgleich).
Uebersicht: `blatt.png`. Einzelbilder: `A-karte-*.png`, `B-liste-*.png`, `C-karte-liste-*.png` (je 1280 x 720 und 854 x 480).
Gezeigt in jedem Entwurf: Start Hall (hier), Merchant I (bereit, gewaehlt), Merchant II (gesperrt), Altar (bereit), Boss Gate (Raum versiegelt), Tastatur und Maus.
Der Hintergrund ist ein nachgebauter Spielausschnitt mit Platzhalter-HUD, kein echter Screenshot.

## Die Entwuerfe

| | Idee | Verdeckte Bildflaeche | Stark | Schwach |
|---|---|---|---|---|
| A Karte | Raumkarte der Ebene, Marken, Namen an der Karte, rechts Detail und Knopf Travel | 63 % | Ortssinn: man sieht, wohin es geht | Viel Flaeche; Namen auf der Karte muessen kollidieren-frei platziert werden |
| B Liste | Schmale Liste im Stil des Pausenmenues | 36 % | Ruhig, schnell, wenig verdeckt, Menuestil | Kein Ortssinn, bei vielen Eintraegen Scrollen |
| C Karte plus Liste | Liste mit Nummern links, gleiche Nummern an der Karte, Tasten 1 bis 5 | 75 % | Beides zugleich, Kurzwahl | Breitestes Fenster, bei 854 eng, am meisten zu lesen |

Gemeinsam: Gold nur fuer die Auswahl, Rot gar nicht, gesperrt = dunkle Raute mit Punkt, versiegelt = graue Raute mit Riegel und schraffierter Raum, "Hier" = Pfeilspitze ueber der Marke. Der Hinweis zum versiegelten oder gesperrten Ziel erscheint am Mauszeiger.

## Abweichung vom Auftrag

Der Auftrag nennt "doppelter Rahmen". Art Bible Regel 19 und Menuregel C2 verbieten einen inneren Doppelrahmen um Texte. Ich habe den Silberrahmen mit Nieten (S1) und Giebel verwendet und nur die Inhaltsflaechen als schmale Mulde abgesetzt (wie die Balken im HUD). Falls ein echter Doppelrahmen gewollt ist, ist das ein Beschluss gegen die Art Bible.

## Empfehlung

B als Grundlage, mit der Karte aus A als kleines Anhaengsel erst spaeter. Begruendung: Der Spieler steht in einem Kampfspiel und will schnell los; B verdeckt am wenigsten, folgt dem Menuestil und passt zu "Core kennt nur Ids". Eine Karte lohnt sich erst, wenn Ebenen mehr als etwa fuenf Teleporter haben. Wenn die Leitung den Ortssinn hoeher gewichtet, dann C (Nummern verbinden beides), nicht A.

## Offene Fragen an den Menschen

1. Reicht eine Liste (B), oder soll die Karte dabei sein (C)?
2. Gesperrte Teleporter in der Liste zeigen (wie hier) oder ganz ausblenden, bis sie gefunden sind?
3. Namen der Orte: hier erfunden (Start Hall, Merchant I und II, Altar, Boss Gate). Passen sie, oder soll es nur "Teleporter 1..5" heissen?
4. Doppelrahmen oder Art-Bible-Rahmen (siehe oben)?
5. Zeigerhinweis zum versiegelten Ziel: genuegt Text, oder soll der Raum auf der Karte zusaetzlich blinken?

## Unsicher

Die Raumkarte ist ein Beispiel, nicht die echte Ebene. Die Hoehe der Zeilen und Textgroessen bei 854 ist klein (Schriftmass 1); Statuszeilen bei 854 sind knapp lesbar.
