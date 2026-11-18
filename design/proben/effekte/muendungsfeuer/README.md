# Effekt-Probestück: Mündungsfeuer der Pistole (Issue #361)

Zweites Probestück des Effektdesigners (Rolle: #352). Schuss der Pistole in der Ich-Sicht – Blitz, Funken, Rauch und kurzes Aufleuchten der Umgebung – in drei Varianten. Die Szene ist bei allen dreien dasselbe Foto und wird nicht beurteilt; es ändert sich nur der Effekt. 

**Urteil des Menschen (2026-11-18): gewählt ist C, Pulverdampf, ohne Nacharbeit** (Treffer 2 von 3 bei den Effekten). A und B wurden abgelehnt, einen Grund nannte er nicht. Die Regel steht in `docs/design/art-bible.md`, Abschnitt „Effekte“.

## Dateien

| Datei | Inhalt |
|---|---|
| `muendungsfeuer-blatt.png` | alle drei Streifen untereinander, beschriftet A/B/C |
| `muendungsfeuer-A-streifen.png`, `-B-`, `-C-` | je 10 Einzelbilder (0, 16, 33, 50, 66, 100, 150, 233, 366, 600 ms nach dem Schuss), Ausschnitt 260 × 210 um die Mündung; darunter ein Balken für die Helligkeit des Aufleuchtens |
| `muendungsfeuer-A.gif`, `-B.gif`, `-C.gif` | volles Bild 854 × 480, 30 Bilder je Sekunde, Schuss und Abklingen (0,9 s), danach 0,9 s Pause, endlos |
| `muendungsfeuer.py` | erzeugt alles oben |

## Die drei Varianten

| | A – Sternblitz | B – Funkenstoß | C – Pulverdampf |
|---|---|---|---|
| Blitz | Stern aus 5, dann 3 Spitzen, 3 Bilder (50 ms), weißlicher Kern | nur ein harter Kern, 2 Bilder | breiter flacher Kegel aus 2 Polygonen, 3 Bilder |
| Funken | 4, fliegen nach vorn und fallen (bis 0,35 s) | 10, als Kegel von etwa ±21°, fallen (bis 0,5 s) | keine |
| Rauch | Faden aus 3 grauen Quadraten (0,5 s) | keiner | Wolke aus 10 Quadraten, wächst auf bis 34 px, steht 0,6 s |
| Aufleuchten | Spitze +80 %, klingt in 4 Bildern (67 ms) ab | Spitze +75 %, 1 Bild, dann +25 % | schwächer: +50 %, 4 Bilder; färbt frischen Rauch gold |

Zeiten in Ticks (60 je Sekunde): A-Licht 0 bis 3, B-Licht 0 bis 1, C-Licht 0 bis 3.

## Szene und Stil

- `design/proben/grafik/hud-vorlagen/basis-854x480.png` (Gang, Schwert links, Pistole rechts, ohne HUD), nur gelesen.
- Blitz, Funken und Rauch sind harte Quadrate bzw. Polygone ohne Kantenglättung, Farben nur aus der Hauptpalette (Blitz und Funken Messing/Gold 5 bis 7, Rauch Stein 6 bis 12), kein Rot, kein Leuchtschein.
- Das ganze Bild wird nach dem Zeichnen mit dem 4 × 4-Punktmuster auf 15 Bit gerastert (wie `fackel.py`, ADR 0010). Ohne Effekt bleibt jeder Bildpunkt des Fotos unverändert.
- Aufleuchten: warmer Lichtzuschlag (Messing 5) auf das Foto, Abfall in 8 groben Stufen vom Mittelpunkt 30 px vor der Mündung (Radius 130 bis 150 px), nach dem Zuschlag mitgekörnt.

## Erzeugen

Werkzeug ist das Python, das Blender mitbringt, mit NumPy. PNG und GIF schreibt `../fackel/fackel.py` (als Modul geladen). Aufruf aus dem Repository-Wurzelverzeichnis:

```
"<Blender-Ordner>\5.3\python\bin\python.exe" design/proben/effekte/muendungsfeuer/muendungsfeuer.py
```

Laufzeit etwa 15 Sekunden, Ergebnis bei jedem Lauf gleich (fester Seed).

## Grenzen

- Das Aufleuchten ist eine Bildschirm-Näherung ohne Tiefe: Es hellt Wand, Boden und die Hände rund um die Mündung gleichmäßig auf. Im Spiel würde ein Punktlicht an der Wand sauberer abfallen.
- Die Waffe bleibt in allen Bildern ruhig; Rückstoß gehört dem Animationsdesigner.
- Mündungsposition und -richtung sind von Hand im Foto gemessen (`MUZZLE`, `DIR`); sie gelten nur für diese Pose.
- GIFs mit höchstens 255 Farben je Bild; Streifen und Blatt sind das genaue Bild.
- Nicht in einem Browser gegengelesen, nur die Streifen angesehen.
