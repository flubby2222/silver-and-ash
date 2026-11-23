"""Probenblatt Runde 3 (#616): nur Holy Fire (a1, a2, Fackel als Vorbild) und Leech (b1, b2). Aufruf aus dem Wurzelordner:

    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/statuseffekte-616/r3/blatt_r3.py

Erzeugt in diesem Ordner: probenblatt_r3.png, spiel_*.png (854 x 480), holy_fire_a1_a2.gif, leech_b1_b2.gif.
"""

import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import effekte_r3 as R3  # noqa: E402  (setzt die Suchpfade fuer r2, Runde 1 und die Fackel)
import basis as B  # noqa: E402
import effekte as E  # noqa: E402
import effekte_r2 as R2  # noqa: E402
import gifschreiber as GIF  # noqa: E402
import fackel as F  # noqa: E402
sys.path.insert(0, os.path.join(R3.ROOT, "design", "proben", "effekte", "fackel", "runde2"))
import fackel_runde2 as FR  # noqa: E402

BG = np.array((22, 18, 28), float)
WIDTH = 1280
PW, PH = 300, 280
PANEL = {4: (150, 140, 2), 8: (75, 70, 4), 12: (60, 56, 5)}
HF = {"a1": ("holy_fire", "R3a1"), "a2": ("holy_fire", "R3a2")}
LE = {"b1": ("leech", "R3b1"), "b2": ("leech", "R3b2"), "r2b": ("leech", "R2b")}
FIXED = [("marked", "R2"), None, ("grave_chill", "R2"), ("stun", "R2"), None]
NAMEN = ["Marked B", "Holy Fire", "Grave Chill A", "Stun A", "Leech"]


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


def header():
    h = blank(440)
    B.text(h, "Statuseffekte am Gegner - Probenblatt Runde 3 (#616)", 16, 8, 28, bold=True)
    B.text(h, "Nur Holy Fire und Leech neu. Holy Fire: Flamme nach der Fackel. Leech: Stufe b mit weniger Teilchen.",
           16, 46, 15, (190, 185, 200))
    lines = [
        ("Vorbild Fackel", "design/proben/effekte/fackel/fackel.py (flipbook_images) und runde2/fackel_runde2.py, Variante B"),
        ("Uebernommen", "dieselben 5 Flammenbilder (9 x 15 Texel), Bildwechsel alle 0,1 s, Tropfenform mit seitlichem Pendeln,"),
        ("", "abgerissene Zunge auf jedem zweiten Bild, 4 Farbstufen, 4 Funken (0,6 s, um 0,25 s versetzt)."),
        ("Abweichung", "Flammenrand Messing 4 statt 5 (Royal-Gold wird am Gegner gemieden); Funken mind. 2 statt 3 Pixel."),
        ("Holy Fire a1", "nah am Vorbild: je eine Fackelflamme auf Kopf und beiden Schultern, Toenung der Stufe a, 4 Funken."),
        ("Holy Fire a2", "wie a1, dazu 4 kleine Fackelflammen aus dem Koerper (gespiegelt, anderer Takt) als eigene Form."),
        ("Leech b1", "Stufe b, etwa 2/3 der Teilchen: 2 Perlen (statt 3), 3 Tropfen (5), 5 Spuren (8), Faden halb gestrichelt."),
        ("Leech b2", "etwa die Haelfte: 1 Perle, 2 Tropfen, 4 Spuren, Faden zu einem Drittel. Violett-Umriss bleibt in beiden."),
    ]
    y = 80
    for k, v in lines:
        B.text(h, k, 16, y, 14, bold=True)
        B.text(h, v, 180, y, 14)
        y += 26
    y += 10
    for role, (row, step) in {**R3.EFFEKTE3["holy_fire"]["farben"], **R3.EFFEKTE3["leech"]["farben"]}.items():
        pass
    x, y2 = 16, y
    for e in ("holy_fire", "leech"):
        B.text(h, R3.EFFEKTE3[e]["name"], 16, y2, 14, bold=True)
        x = 300
        for role, (row, step) in R3.EFFEKTE3[e]["farben"].items():
            c = E.pal(row, step)
            h[y2 + 2:y2 + 22, x:x + 26] = c
            B.text(h, f"{role}", x + 30, y2 - 8, 10, (190, 185, 200))
            B.text(h, f"{row} {step} ({c[0]},{c[1]},{c[2]})", x + 30, y2 + 5, 10)
            x += 160
        y2 += 34
    return h


def tables():
    rows = []
    for e in ("holy_fire", "leech"):
        for role, (row, step) in R3.EFFEKTE3[e]["farben"].items():
            rows.append((R3.EFFEKTE3[e]["name"], role, row, step, E.pal(row, step)))
    with open(out("farbtabelle_r3.md"), "w", encoding="utf-8") as f:
        f.write("| Effekt | Rolle | Reihe | Stufe (ab 0) | RGB |\n|---|---|---|---|---|\n")
        for n, role, row, step, c in rows:
            f.write(f"| {n} | {role} | {row} | {step} | {c[0]}, {c[1]}, {c[2]} |\n")


def main():
    B.ROOM_BACK = -16.0
    R3.pruefe_palette3()
    E.pruefe_palette()
    tables()
    torch = Torch()
    canvas = [header()]

    # Aufstellung 8 m und 12 m: Fassung 1 (a1 + b1) und Fassung 2 (a2 + b2), die festen Effekte wie in Runde 2
    for d, f in ((8, 4), (12, 3)):
        for n, (h, l) in enumerate((("a1", "b1"), ("a2", "b2")), start=1):
            effs = [[g] if g else [] for g in FIXED]
            effs[1] = [HF[h]]
            effs[4] = [LE[l]]
            fr = B.frame("line", d, effs)
            _, _, an, _ = B.scene("line", d)
            s, org = strip(fr, an, f)
            blk = blank(s.shape[0] + 64)
            B.text(blk, f"Fassung {n} (Holy Fire {h}, Leech {l}), {d} m, {f}-fach vergroessert", 16, 4, 18, bold=True)
            blk[34:34 + s.shape[0], :min(WIDTH, s.shape[1])] = s[:, :WIDTH]
            for a, nm in zip(an, NAMEN):
                B.text(blk, nm, int((a["chest"][0] - org[0]) * f) - 40, 34 + s.shape[0] + 2, 15)
            canvas.append(blk)
            B.save(fr, out(f"spiel_{d}m_fassung{n}.png"))

    # Holy Fire: a1, a2 und die Fackel daneben
    blk = blank(2 * (PH + 24) + 36)
    B.text(blk, "Holy Fire: a1 (nah an der Fackel), a2 (mit Koerperflammen) und die Fackel selbst als Vorbild", 16, 4, 18,
           bold=True)
    x = [0, 320, 640, 960]
    y1, y2 = 34, 34 + PH + 24
    put_panel(blk, panel([HF["a1"]], 4), x[0], y1, "a1, 4 m (2-fach)")
    put_panel(blk, panel([HF["a2"]], 4), x[1], y1, "a2, 4 m (2-fach)")
    put_panel(blk, torch.panel(0.0), x[2], y1, "Fackel (Vorbild, 2,4 m, 2-fach)")
    put_panel(blk, torch.panel(0.3), x[3], y1, "Fackel, spaetere Phase")
    put_panel(blk, panel([HF["a1"]], 8), x[0], y2, "a1, 8 m (4-fach)")
    put_panel(blk, panel([HF["a2"]], 8), x[1], y2, "a2, 8 m (4-fach)")
    put_panel(blk, panel([HF["a1"]], 12), x[2], y2, "a1, 12 m (5-fach)")
    put_panel(blk, panel([HF["a2"]], 12), x[3], y2, "a2, 12 m (5-fach)")
    canvas.append(blk)
    # zweite Zeile: spaetere Phasen
    blk = blank(PH + 24 + 36)
    B.text(blk, "Holy Fire in vier Phasen (4 m): a1 oben, a2 unten ist im GIF; hier a1 und a2 bei t = 0,3 und 0,6 s", 16, 4,
           16, bold=True)
    put_panel(blk, panel([HF["a1"]], 4, 0.3), x[0], 34, "a1, t = 0,3 s")
    put_panel(blk, panel([HF["a1"]], 4, 0.6), x[1], 34, "a1, t = 0,6 s")
    put_panel(blk, panel([HF["a2"]], 4, 0.3), x[2], 34, "a2, t = 0,3 s")
    put_panel(blk, panel([HF["a2"]], 4, 0.6), x[3], 34, "a2, t = 0,6 s")
    canvas.append(blk)
    for d in (4, 8, 12):
        for v in ("a1", "a2"):
            B.save(B.frame("one", d, [[HF[v]]]), out(f"spiel_holy_fire_{v}_{d}m.png"))

    # Leech: b1, b2, dazu Runde 2 b zum Vergleich
    blk = blank(2 * (PH + 24) + 36)
    B.text(blk, "Leech (Violett): b1 (etwa 2/3 der Teilchen), b2 (etwa die Haelfte), zum Vergleich b aus Runde 2", 16, 4, 18,
           bold=True)
    put_panel(blk, panel([LE["b1"]], 4), x[0], y1, "b1, 4 m (2-fach)")
    put_panel(blk, panel([LE["b2"]], 4), x[1], y1, "b2, 4 m (2-fach)")
    put_panel(blk, panel([LE["b1"]], 8), x[2], y1, "b1, 8 m (4-fach)")
    put_panel(blk, panel([LE["b2"]], 8), x[3], y1, "b2, 8 m (4-fach)")
    put_panel(blk, panel([LE["b1"]], 12), x[0], y2, "b1, 12 m (5-fach)")
    put_panel(blk, panel([LE["b2"]], 12), x[1], y2, "b2, 12 m (5-fach)")
    put_panel(blk, panel([LE["r2b"]], 4), x[2], y2, "Runde 2 b, 4 m (zum Vergleich)")
    put_panel(blk, panel([LE["r2b"]], 12), x[3], y2, "Runde 2 b, 12 m (zum Vergleich)")
    canvas.append(blk)
    for d in (4, 8, 12):
        for v in ("b1", "b2"):
            B.save(B.frame("one", d, [[LE[v]]]), out(f"spiel_leech_{v}_{d}m.png"))

    sheet = np.concatenate(canvas, axis=0)
    B.save(sheet, out("probenblatt_r3.png"))

    # GIFs: zehn Phasen je Sekunde (Fackeltakt), 0,1 s je Bild
    gap = np.tile(BG, (PH, 8, 1))
    frames = []
    for k in range(10):
        t = k / 10.0
        frames.append(np.concatenate([panel([HF["a1"]], 4, t), gap, panel([HF["a2"]], 4, t), gap, torch.panel(t)], axis=1))
    GIF.write_gif(out("holy_fire_a1_a2.gif"), frames, delay_cs=10)
    frames = []
    for k in range(10):
        t = k / 10.0
        frames.append(np.concatenate([panel([LE["b1"]], 4, t), gap, panel([LE["b2"]], 4, t)], axis=1))
    GIF.write_gif(out("leech_b1_b2.gif"), frames, delay_cs=10)


main()
