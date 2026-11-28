"""Throwing Knives (Issue #204), Bewegungsprobe: Bildblatt mit den Schluesselhaltungen als Bildfolge.

Jedes Bild ist die Ich-Sicht 854 x 480 mit Gang, Silver Blade links, Aermel und Messern rechts. In den ersten Ticks
eines Wurfs liegt das geworfene Messer (messer-e-geschoss.glb) dort, wo es der Spielkern fuehrt: am Auge gestartet,
0,5 m je Tick entlang der Blickrichtung; seine Drehung auf dem Blatt ist nur Anschauung.

Schreibt blatt-bewegung.png neben das Skript und, wenn nach "--" ein Ordner steht, dorthin dasselbe Blatt und alle
Einzelbilder (einzeln/).
Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python blatt_bewegung.py -- <Ordner>
"""

import os
import sys

import numpy as np

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bewegung as M  # noqa: E402
import szene as Z  # noqa: E402

B = Z.B
W, H, BAR, GAP, HEAD = 854, 480, 30, 6, 44
COLS = 4


def cells():
    """(Dateiname, Beschriftung, Haltung, Messer in der Hand, Flug-Ticks) je Bild, zeilenweise."""
    out = []
    a = [(0, "Loslassen: links fehlt, Hand schnappt"), (1, "schnappt weiter"), (2, "Hoehepunkt"), (4, "haelt bis hier"),
         (8, "Rueckweg, abgebremst"), (12, "fast zurueck"), (16, "schwingt leicht nach hinten aus"), (18, "Ruhe bis Tick 60")]
    for t, note in a:
        out.append((f"wurf-a-tick-{t:02d}", f"Wurf A Schnapp, Tick {t} von 60: {note}", M.throw_a(t), M.held(3, t), t))
    b = [(0, "Loslassen: links fehlt, Arm zieht an"), (2, "zieht nach vorn-innen-unten"), (6, "Hoehepunkt"),
         (10, "laeuft aus"), (16, "Rueckweg beginnt weich"), (22, "kommt erst hoch"), (28, "dann nach aussen"),
         (34, "schwingt bis Tick 40 aus, Ruhe bis 60")]
    for t, note in b:
        out.append((f"wurf-b-tick-{t:02d}", f"Wurf B Durchzug, Tick {t} von 60: {note}", M.throw_b(t), M.held(3, t), t))
    for n, note in ((3, "drei Messer"), (2, "zwei: Mitte und rechts"), (1, "eins: das gerade in der Mitte"), (0, "leer")):
        out.append((f"ruhe-{n}-messer", f"Ruhe im Magazin: {note}", M.REST, M.magazine(n), None))
    out.append(("wurf-a-2-auf-1", "Wurf A, Tick 2, zweiter Wurf (rechts fliegt)", M.throw_a(2), M.held(2, 2), 2))
    out.append(("wurf-a-1-auf-0", "Wurf A, Tick 2, dritter Wurf (Mitte fliegt)", M.throw_a(2), M.held(1, 2), 2))
    out.append(("wurf-b-2-auf-1", "Wurf B, Tick 6, zweiter Wurf", M.throw_b(6), M.held(2, 6), None))
    out.append(("wurf-b-1-auf-0", "Wurf B, Tick 6, dritter Wurf", M.throw_b(6), M.held(1, 6), None))
    r = [(0.00, "leere Manschette"), (0.05, "taucht weg, beschleunigt"), (0.09, "fast unter dem Rand"),
         (0.30, "unter dem Bildrand: Messer erscheinen"), (0.54, "kommt hoch, Faecher 15 Grad"), (0.58, "abgebremst"),
         (0.63, "oben, schwingt leicht ueber"), (0.72, "Faecher beginnt zu oeffnen"), (0.78, "Faecher oeffnet auf 17 Grad"),
         (0.88, "Faecher offen"), (0.94, "kleiner Ruck: Messer sitzen"), (1.00, "bereit")]
    for p, note in r:
        tick = round(p * M.RELOAD_TICKS)
        out.append((f"nachladen-{int(round(p * 100)):03d}", f"Nachladen {p:.2f} (Tick {tick} von 120): {note}",
                    M.reload(p), M.magazine(M.reload_count(p)), None))
    return out


def main():
    extra = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1 else None
    only = sys.argv[sys.argv.index("--") + 2:] if extra else []
    s = Z.setup()
    todo = cells()
    rows = (len(todo) + COLS - 1) // COLS
    sheet = np.zeros((HEAD + rows * (H + BAR + GAP), COLS * (W + GAP) + GAP, 3)) + np.array((16.0, 11.0, 20.0))
    B.text(sheet, "Silver & Ash - Throwing Knives (Issue 204), Bewegung in der Ich-Sicht: Wurf A Schnapp, Wurf B Durchzug, "
                  "Magazin 3/2/1/0, Nachladen. 60 Ticks je Sekunde; Wurfabstand 60 Ticks, Nachladen 120 Ticks.", 10, 6, 22)
    if extra:
        os.makedirs(os.path.join(extra, "einzeln"), exist_ok=True)
    for i, (name, label, pose, shown, flight) in enumerate(todo):
        if only and not any(name.startswith(o) for o in only):
            continue
        Z.pose(s, pose)
        img = Z.frame(s, shown, flight=flight if flight and flight <= 4 else None)
        x, y = GAP + (i % COLS) * (W + GAP), HEAD + (i // COLS) * (H + BAR + GAP)
        B.text(sheet, label, x + 4, y + 2, 18)
        B.paste(sheet, img, x, y + BAR)
        if extra:
            Z.S.save_png(img[::-1], os.path.join(extra, "einzeln", name + ".png"))
    if only:
        return
    Z.S.save_png(sheet[::-1], os.path.join(HERE, "blatt-bewegung.png"))
    if extra:
        Z.S.save_png(sheet[::-1], os.path.join(extra, "blatt-bewegung.png"))


if __name__ == "__main__":
    main()
