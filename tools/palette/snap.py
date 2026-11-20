"""Texturen auf die Hauptpalette legen (Issue #280).

Hilfsmodul fuer die Erzeuger-Skripte der Umgebung (tools/textures/catacomb*.py, tools/models/door.py,
tools/models/cracked_wall.py). Liest die Reihen aus assets/palette/hauptpalette.json und setzt jedes Pixel eines
gemalten Bildes auf die naechste Farbe aus den erlaubten Reihen. Die Palette wird hier nie geaendert, nur gelesen.

Aufruf als Modul (kein eigener Aufruf): sys.path um tools/palette erweitern, dann `import snap`. Braucht NumPy
(in Blender dabei).

Welche Reihen fuer welche Umgebung gelten (docs/design/art-bible.md, Abschnitt Farben):
- Wand, Boden, Decke, rissige Wand, Tuerrahmen: nur die Reihe Stein (Grund-Reihe); der Rahmen des Bosstors darf dazu
  den Werkstein (Akzent, Regel 6) tragen.
- Tuerfluegel sind noch nicht umgerechnet: Holz und Eisen der Palette sind deutlich satter als der Bestand (Issue #280).
"""
import json
import os

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PALETTE = os.path.join(ROOT, "assets", "palette", "hauptpalette.json")

# Abstand mit Gewichten fuer R, G, B (Helligkeit zaehlt mehr als Farbton: der Stein ist ueber seine Helligkeit gebildet).
WEIGHTS = np.array((0.30, 0.59, 0.11)) * 3


def rows(path=PALETTE):
    """Alle Reihen der Palette als {Name: Feld (n, 3) ganze Zahlen}, Grund- und Akzent-Reihen zusammen."""
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    return {r["name"]: np.array(r["farben"], int) for r in data["grund"] + data["akzent"]}


def colours(*names, path=PALETTE):
    """Die Farben der genannten Reihen untereinander, ohne doppelte, als Feld (n, 3)."""
    table = rows(path)
    stacked = np.concatenate([table[name] for name in names])
    return np.unique(stacked, axis=0)


def snap(arr, allowed):
    """Jedes Pixel von arr (Form (..., 3), 0..255) auf die naechste Farbe aus allowed (n, 3) setzen."""
    arr = np.asarray(arr, float)
    px = arr.reshape(-1, 3)
    allowed = np.asarray(allowed, float)
    out = np.empty_like(px)
    for start in range(0, len(px), 8192):                       # in Stuecken, damit der Zwischenspeicher klein bleibt
        chunk = px[start:start + 8192]
        d = (((chunk[:, None, :] - allowed[None, :, :]) ** 2) * WEIGHTS).sum(-1)
        out[start:start + 8192] = allowed[d.argmin(1)]
    return out.reshape(arr.shape)
