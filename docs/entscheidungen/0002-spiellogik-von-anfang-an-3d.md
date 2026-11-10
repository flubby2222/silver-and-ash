# 0002 – Spiellogik von Anfang an in 3D

- **Status:** angenommen
- **Datum:** 2026-11-10

## Kontext

Das Spiel wird aus der Ego-Perspektive gespielt. Zur Wahl standen:

- **Nur horizontal umsehen** (wie die ersten Ego-Shooter): Die Spiellogik bliebe 2D – einfacher zu bauen und zu testen. Freies Umsehen käme später.
- **Frei umsehen mit vertikalem Zielen:** Die Spiellogik ist 3D – aufwendigere Treffer- und Kollisionsrechnung.

Außerdem sollen Level später mehr Tiefe bekommen (Treppen, Gruben, Emporen).

## Entscheidung

Die Spiellogik ist **von Anfang an 3D**: freies Umsehen, vertikales Zielen, Schwerkraft und Springen. Wir schreiben eine **eigene, einfache Physik** (Spieler = Kapsel, Wände = Quader, Geschosse = Strahl oder kleine Kugel) statt eine Physik-Engine zu verwenden. Level bestehen aus Zellen mit eigener Boden- und Deckenhöhe.

## Konsequenzen

- **Positiv:** Hitboxen, Bewegung und Physik müssen später nicht mitten im Projekt umgebaut werden. Komplexere Level brauchen kein neues System, nur andere Höhen.
- **Positiv:** Eigene Physik ist vollständig verständlich und testbar; jede Zeile kann im Review nachvollzogen werden.
- **Negativ:** Mehr Aufwand in Woche 2 als bei einer 2D-Logik.
- **Negativ:** Eigene Physik kann Sonderfälle haben (z. B. an Kanten hängenbleiben), die eine Engine schon gelöst hätte – dafür braucht es gute Tests.
