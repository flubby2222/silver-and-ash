# Fassung i (Issue #265): Entwurf C, kleinster Schritt

Blatt `blatt_C_i.png` (links C aus Probestück 13, rechts i; oben Gang, Mitte Schuss, unten aufgeladene Klinge). Modelle `modelle_c/` (enthält i), Fotos `fotos_c/`, Skripte `pistol.py`, `blade.py`, `arm.py` (Voreinstellung C), `fotos.sh c`, `blatt.py`. Haltung wie `main`; die 15 Tests `WeaponPoseTests` laufen mit den Dateien grün.

## Was gegenüber C geändert ist (und sonst nichts)
1. **Klingenärmel:** Geometrie, Radien, Rüschensaum, Textur und Atlas-Feld wie C. Neu: ein achter Ring weit hinten (Richtung des Rohrs weitergeführt), UV so umgerechnet, dass die Textur wie bei C liegt; zwei kleine Silberknöpfe und eine schmale dunkle Naht hinter der Rüsche.
2. **Pistolenärmel:** derselbe Ärmel gespiegelt (Ausrichtung am Pistolenursprung wie am Klingenursprung), nur 2,6 cm nach unten verschoben. Ohne diese Verschiebung (und mit 0,8 cm nach hinten oder mit 2 cm und 1,2 cm zusammen) deckte die Manschette Schloss und Hahn zu oder sah die Rüsche kantenparallel aus; reine Verschiebung nach unten behält Form und Winkel.
3. Prüfung am Foto `room-corridor`: mittlere Abweichung C zu i links unten (x < 427, untere 200 Zeilen) 5,2 von 255, der Unterschied sind Naht, Knöpfe und das längere Ende des Rohrs.

## Was noch nicht stimmt
- Die rechte Seite ist **kein exaktes Spiegelbild**: Die Pistole liegt 1,5 cm höher und mit anderem Rollwinkel (+15° statt −20°) als die Klinge; die Rüsche ist rechts weißer und füllt mehr Bild, vom blauen Ärmel ist rechts weniger zu sehen als links.
- Die Rüsche der Pistole (Spitzenmuster) ist ein wenig zu hell; sie fällt gegen den dunklen Boden auf.
- Der volle Schwung ist nicht fotografiert; der Test zu Anschnitt und Reichweite ist grün.
- Die Knöpfe sind im Foto kaum zu erkennen (ein heller Fleck links unten).
