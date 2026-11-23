# Probestück Ash Altar (Issue #615)

Drei Entwürfe, erzeugt von `altar.py` (Blender 5.3, ohne Argumente aus dem Repository-Wurzelverzeichnis). Nichts in `assets/` geändert, nichts heruntergeladen.

- **Probenblatt:** `blatt.png` (Spielbild 854 x 480, 70 Grad, Augen 1,6 m, Nebel und Körnung wie im Spiel; links aus 3 m, rechts aus 8 m; blauer Quader = 1,8 m).
- **Vier Ansichten:** `ansichten.png` (vorn, Seite, hinten, schräg oben), Einzelbilder `ansicht-<x>-<1..4>.png`; Spielbilder in voller Größe `spiel-<x>-3m.png`, `spiel-<x>-8m.png`.
- **Modelle:** `altar_a.glb`, `altar_b.glb`, `altar_c.glb`, Textur `altar_<x>.png`.

## Thema und Begründung

Asche: Eine Silberschale voll Asche mit Glut in Messing (kein Rot), dazu Wachskerzen in Messing. Asche gehört zum Namen des Spiels, die Schale liest sich als flache helle Form auch auf Distanz, und die Glut ist der einzige warme Punkt im Stein. Eine Urne wäre ein geschlossenes Gefäß: Es hätte keine sichtbare Glut und wirkte wie Deko.

## Entwürfe

| | Dreiecke (Körper + Glut) | Maße (m) | Textur | Farben |
|---|---|---|---|---|
| a Aschenblock | 192 (156 + 36) | 1,10 x 0,80 x 1,61 | 64 x 64 | 22 |
| b Aschenkelch | 318 (282 + 36) | 1,48 x 1,48 x 1,70 | 64 x 64 | 22 |
| c Gruftschrein | 268 (232 + 36) | 1,70 x 0,80 x 2,20 | 64 x 64 | 22 |

Alle drei passen in eine Zelle (2 m). Der Atlas ist für alle gleich: 10 Felder à 16 x 16, nur Palettenfarben (Stein, Eisen, Messing/Gold, Haut/Knochen, Stoff für den Maßstab-Quader), keine Filterung.

- **a Aschenblock:** hüfthoher Steinblock wie im Entwurf, flache Silberschale mit Asche und drei Glutstücken, zwei Kerzen; der schlichteste, in der Silhouette aber der kleinste Eindruck.
- **b Aschenkelch:** achteckiger Fuß, breite Silberschale mit Aschehügel und vier Eisenspitzen, zwei Flammen vorn; die auffälligste Silhouette, wirkt aber wie eine Feuerschale und weniger wie ein Altar.
- **c Gruftschrein:** breiter Schrein mit Spitzgiebel und Fialen, goldenes Glutfenster im Giebel, kleine Schale, zwei Kerzen; am besten aus 8 m lesbar, braucht Platz an der Wand (Rückseite flach).

## Geprüft

- `python tools/models/check_closed.py altar_a.glb altar_b.glb altar_c.glb`: alle drei geschlossen, 0 offene Kanten.
- Dreiecke im Rahmen für Props der Art Bible (100 bis 400), Textur 64 x 64, 22 Farben (höchstens 32).
- Kein Rot, kein Werkstein, keine Hände oder Figuren, nichts aus den genannten Werken.

## Hinweise und Unsicherheiten

- Jedes Modell hat zwei Teile: `body` und `glut` (Glut, Flammen, Fenster). Im Probenblatt ist `glut` selbstleuchtend (Licht 1). Soll es im Spiel so aussehen, muss das Spiel das Teil ohne Laterne zeichnen (wie den Kristall des Teleporters).
- Die Kerzen "brennen, solange ein Punkt offen ist": Dafür bräuchte es zwei Zustände (Flammen an/aus); als Probe nur der brennende Zustand.
- Das Silber nutzt die Reihe Eisen (hell); die Palette hat keine eigene Silberreihe.
- In den 8-m-Ausschnitten ist die Glut nur noch wenige Pixel groß; am deutlichsten trägt das große Fenster in c.
