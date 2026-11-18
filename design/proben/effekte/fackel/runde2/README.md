# Effekt-Probestück Fackel, Runde 2 (Issue #353)

Urteil zu Runde 1 (2026-11-18): Die Flamme aus A (gemaltes Daumenkino) wird mit dem unruhigen Licht aus B verbunden, dessen Mitte wandert. Abgelehnt wurden die Teilchenflamme aus B und das Stufenlicht aus C (siehe Art-Bible, Abschnitt „Effekte“). Die Dateien der ersten Runde liegen eine Ebene höher.

In Runde 2 zeigen alle drei Varianten dieselbe Flamme, dieselben 4 Funken und dieselbe Szene. **Es ändert sich nur, wie stark das Licht flackert** und wie weit seine Mitte wandert. Alle drei nutzen dieselbe Zufallsfolge, nur unterschiedlich stark; die hellen und dunklen Momente liegen deshalb an denselben Stellen.

## Varianten

| Variante | Helligkeit | Mitte wandert bis (seitlich / hoch) | Eindruck |
|---|---|---|---|
| **A – ruhig** | ±15 % | 2,1 / 1,6 cm | Das Licht atmet leicht; die Wand bewegt sich kaum. |
| **B – mittel** | ±25 % | 3,5 / 2,7 cm | Deutlich lebendig; man sieht das Flackern, ohne dass es unruhig wirkt. |
| **C – stark** | ±32 % (wie B in Runde 1) | 4,5 / 3,5 cm | Kräftiges Zucken, die Wand pumpt sichtbar. |

## Dateien

| Datei | Inhalt |
|---|---|
| `fackel-r2-blatt.png` | alle drei Streifen untereinander, beschriftet A/B/C |
| `fackel-r2-A-streifen.png`, `-B-`, `-C-` | je 10 Einzelbilder einer Sekunde (alle 0,1 s), Ausschnitt um die Fackel, darunter ein Balken für die Helligkeit des Fackellichts |
| `fackel-r2-A.gif`, `-B.gif`, `-C.gif` | volles Bild 854 × 480, 20 Bilder je Sekunde, nahtlose Endlosschleife |
| `fackel_runde2.py` | erzeugt alles oben; Szene, Flamme, Körnung und Kodierer kommen aus `../fackel.py` |

## Erzeugen

Aus dem Repository-Wurzelverzeichnis, mit dem Python, das Blender mitbringt:

```
"<Blender-Ordner>\5.3\python\bin\python.exe" design/proben/effekte/fackel/runde2/fackel_runde2.py
```

Laufzeit etwa 20 Sekunden, Ergebnis bei jedem Lauf gleich. Szene, Stil und Grenzen wie in der README der ersten Runde.
