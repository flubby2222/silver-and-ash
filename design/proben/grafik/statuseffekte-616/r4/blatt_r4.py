"""Probenblatt Runde 4 (#616): nur Cleansing Flame (frueher Holy Fire; c1, c2). Aufruf: siehe LIESMICH.md."""

import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import effekte_r4 as R4  # noqa: E402  (setzt die Suchpfade fuer r2, Runde 1 und die Fackel)
import basis as B  # noqa: E402
import effekte as E  # noqa: E402
import effekte_r2 as R2  # noqa: E402
import gifschreiber as GIF  # noqa: E402
import fackel as F  # noqa: E402
sys.path.insert(0, os.path.join(R4.R3.ROOT, "design", "proben", "effekte", "fackel", "runde2"))
import fackel_runde2 as FR  # noqa: E402

BG = np.array((22, 18, 28), float)
WIDTH = 1280
PW, PH = 300, 280
PANEL = {4: (150, 140, 2), 8: (75, 70, 4), 12: (60, 56, 5)}
HF = {"c1": ("holy_fire", "R4c1"), "c2": ("holy_fire", "R4c2"), "a1": ("holy_fire", "R3a1")}
LE = {"b1": ("leech", "R3b1")}
FIXED = [("marked", "R2"), None, ("grave_chill", "R2"), ("stun", "R2"), None]
NAMEN = ["Marked B", "Cleansing Flame", "Grave Chill A", "Stun A", "Leech"]


def out(n):
    return os.path.join(HERE, n)


def blank(h):
    return np.tile(BG, (h, WIDTH, 1))


def crop(fr, an, d, w, h, up=0.28):
    S = E.px_per_metre(d)
    cx, cy = int(an["chest"][0]), int(an["chest"][1] - up * S)
    x0 = min(max(cx - w // 2, 0), fr.shape[1] - w)
    y0 = min(max(cy - h // 2, 0), fr.shape[0] - h)
    return fr[y0:y0 + h, x0:x0 + w]


def panel(effs, d, t=0.0):
    fr = B.frame("one", d, [effs], t=t)
    _, _, an, dd = B.scene("one", d)
    w, h, f = PANEL[d]
    return B.up(crop(fr, an[0], dd, w, h), f)


def put_panel(blk, img, x, y, label):
    blk[y:y + img.shape[0], x:x + img.shape[1]] = img
    B.text(blk, label, x + 2, y + img.shape[0] + 1, 13)


def strip(fr, anchors, f):
    xs = [a["chest"][0] for a in anchors]
    ys = [a["chest"][1] for a in anchors]
    x0, x1 = int(max(min(xs) - 60, 0)), int(min(max(xs) + 60, 854))
    y0, y1 = int(np.mean(ys) - 60), int(np.mean(ys) + 40)
    return B.up(fr[y0:y1, x0:x1], f), (x0, y0)


class Torch:
    """Die Fackel aus Issue #353 (Runde 2, Variante B: Flamme Daumenkino, Licht mittel), gerendert mit ihrem eigenen Code."""

    def __init__(self):
        self.scene = F.build_scene()
        self.flip = F.flipbook_images()
        row, col, _, _ = F.project(F.FLAME_BASE + np.array([0, 0.12, 0]))
        self.cy, self.cx = int(row), int(col)

    def frame(self, t):
        img, _ = FR.variant(self.scene, t, self.flip, FR.AMPLITUDES["B"])
        return F.quantize(img, self.scene["thr"]).astype(float)

    def panel(self, t):
        fr = self.frame(t)
        x0 = min(max(self.cx - 75, 0), 854 - 150)
        y0 = min(max(self.cy - 70, 0), 480 - 140)
        return B.up(fr[y0:y0 + 140, x0:x0 + 150], 2)


class Torch:
    """Die Fackel aus Issue #353 (Runde 2, Variante B: Flamme Daumenkino, Licht mittel), gerendert mit ihrem eigenen Code."""

    def __init__(self):
        self.scene = F.build_scene()
        self.flip = F.flipbook_images()
        row, col, _, _ = F.project(F.FLAME_BASE + np.array([0, 0.12, 0]))
        self.cy, self.cx = int(row), int(col)

    def frame(self, t):
        img, _ = FR.variant(self.scene, t, self.flip, FR.AMPLITUDES["B"])
        return F.quantize(img, self.scene["thr"]).astype(float)

    def panel(self, t):
        fr = self.frame(t)
        x0 = min(max(self.cx - 75, 0), 854 - 150)
        y0 = min(max(self.cy - 70, 0), 480 - 140)
        return B.up(fr[y0:y0 + 140, x0:x0 + 150], 2)


def header():
    h = blank(250)
    B.text(h, "Statuseffekte am Gegner - Probenblatt Runde 4 (#616): nur Cleansing Flame neu (frueher Holy Fire)", 16, 8, 26, bold=True)
    lines = [
        ("Urteil Runde 3", "Fackel nicht als Effekt aufsetzen, nur inspirieren lassen; kleinere Flammen, dafuer mehr. Leech b1."),
        ("Eigene Flammen", "3 selbst gezeichnete Groessen (6x11, 5x8, 4x6 Texel), 5 Bilder, Takt 0,1 s, Tropfenform mit Pendeln,"),
        ("", "abreissende Zunge auf jedem 2. Bild, Farben Messing 7/6/4/3, 3 Funken. Nichts aus fackel.py skaliert."),
        ("c1", "9 Flammen, Texel 0,020 m (0,22 / 0,16 / 0,12 m hoch; die Fackel ist 0,42 m hoch)."),
        ("c2", "16 Flammen, Texel 0,015 m. Ab 10 m (12 m): kraeftigere Glut-Toenung und hellerer Innenrand."),
        ("Verteilt", "auf Oberkanten der Silhouette mit Mindestabstand, versetzter Takt, teils gespiegelt, Augen bleiben frei."),
        ("Vergleich", "a1 aus Runde 3 und die Fackel selbst (Vorbild) stehen im Block Cleansing Flame daneben."),
    ]
    y = 44
    for k, v in lines:
        B.text(h, k, 16, y, 14, bold=True)
        B.text(h, v, 180, y, 14)
        y += 26
    return h


def main():
    B.ROOM_BACK = -16.0
    R4.R3.pruefe_palette3()
    E.pruefe_palette()
    torch = Torch()
    canvas = [header()]
    for d, f in ((8, 4), (12, 3)):
        for n in ("c1", "c2"):
            effs = [[g] if g else [] for g in FIXED]
            effs[1] = [HF[n]]
            effs[4] = [LE["b1"]]
            fr = B.frame("line", d, effs)
            _, _, an, _ = B.scene("line", d)
            s, org = strip(fr, an, f)
            blk = blank(s.shape[0] + 64)
            B.text(blk, f"Aufstellung mit Cleansing Flame {n} und Leech b1, {d} m, {f}-fach vergroessert", 16, 4, 18, bold=True)
            blk[34:34 + s.shape[0], :min(WIDTH, s.shape[1])] = s[:, :WIDTH]
            for a, nm in zip(an, NAMEN):
                B.text(blk, nm, int((a["chest"][0] - org[0]) * f) - 40, 34 + s.shape[0] + 2, 15)
            canvas.append(blk)
            B.save(fr, out(f"spiel_{d}m_{n}.png"))
    x = [0, 320, 640, 960]
    y1, y2 = 34, 34 + PH + 24
    blk = blank(2 * (PH + 24) + 36)
    B.text(blk, "Cleansing Flame: c1 und c2 bei 4 m, 8 m, 12 m", 16, 4, 18, bold=True)
    put_panel(blk, panel([HF["c1"]], 4), x[0], y1, "c1, 4 m (2-fach)")
    put_panel(blk, panel([HF["c2"]], 4), x[1], y1, "c2, 4 m (2-fach)")
    put_panel(blk, panel([HF["c1"]], 8), x[2], y1, "c1, 8 m (4-fach)")
    put_panel(blk, panel([HF["c2"]], 8), x[3], y1, "c2, 8 m (4-fach)")
    put_panel(blk, panel([HF["c1"]], 12), x[0], y2, "c1, 12 m (5-fach)")
    put_panel(blk, panel([HF["c2"]], 12), x[1], y2, "c2, 12 m (5-fach)")
    put_panel(blk, panel([HF["a1"]], 4), x[2], y2, "Vergleich: a1 aus Runde 3, 4 m")
    put_panel(blk, torch.panel(0.0), x[3], y2, "Vergleich: die Fackel (2,4 m)")
    canvas.append(blk)
    blk = blank(2 * (PH + 24) + 36)
    B.text(blk, "Cleansing Flame in vier Phasen (4 m): c1 oben, c2 unten (t = 0, 0,3, 0,6, 0,9 s)", 16, 4, 16, bold=True)
    for r, n in enumerate(("c1", "c2")):
        for j, t in enumerate((0.0, 0.3, 0.6, 0.9)):
            put_panel(blk, panel([HF[n]], 4, t), x[j], 34 + r * (PH + 24), f"{n}, t = {t} s")
    canvas.append(blk)
    for d in (4, 8, 12):
        for n in ("c1", "c2"):
            B.save(B.frame("one", d, [[HF[n]]]), out(f"spiel_cleansing_flame_{n}_{d}m.png"))
    B.save(np.concatenate(canvas, axis=0), out("probenblatt_r4.png"))
    gap = np.tile(BG, (PH, 8, 1))
    frames = []
    for k in range(10):
        t = k / 10.0
        frames.append(np.concatenate([panel([HF["c1"]], 4, t), gap, panel([HF["c2"]], 4, t)], axis=1))
    GIF.write_gif(out("cleansing_flame_c1_c2.gif"), frames, delay_cs=10)


main()
