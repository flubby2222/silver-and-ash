"""Runde 4 der Statuseffekte (#616): nur Cleansing Flame neu (frueher Holy Fire). Leech b1 und alles andere steht fest.

Urteil zu Runde 3: Die Fackel nicht als Effekt aufsetzen, sondern von ihr inspirieren lassen; kleinere Flammen, dafuer mehr.

Eigene Flammenbilder (nichts aus fackel.py importiert oder skaliert). Geliehen wird nur das Wesen der Fackel:
Tropfenform mit seitlichem Pendeln, abreissende Zunge auf jedem zweiten Bild, Takt 0,1 s (5 Bilder, Schleife 1 s),
vier Farbstufen Messing 7 / 6 / 4 / 3 (Rand 4 statt 5), wenige Funken.

  c1: 9 Flammen in drei Groessen (Texel 0,020 m: 0,22 / 0,16 / 0,12 m hoch, also etwa die Haelfte bis ein Viertel der Fackel)
  c2: 16 Flammen, kleiner (Texel 0,015 m)
Die Flammen sitzen auf der Silhouette (Oberkanten von Kopf, Schultern, Armen, Ruecken, Hueften), gleichmaessig verteilt
(Mindestabstand), mit versetztem Takt, teils gespiegelt. Ueber den Augen steht keine Flamme.
"""

import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
PARENT = os.path.dirname(HERE)
sys.path.insert(0, os.path.join(PARENT, "r3"))
import effekte_r3 as R3  # noqa: E402  (setzt die Suchpfade, registriert Runde 3)
import effekte as E  # noqa: E402

R2 = R3.R2
P = R2.P
G = R2.G
SIZES = {"L": (6, 11), "M": (5, 8), "S": (4, 6)}


def _make_sprites():
    """Eigene Daumenkinos: je Groesse 5 Bilder; Index 0 Kern, 1 Mitte, 2 Rand, 3 Spitze, -1 leer; y = 0 unten."""
    sprites = {}
    for name, (w, h) in SIZES.items():
        rng = np.random.default_rng(40 + h)
        frames = []
        for k in range(5):
            ph = 2 * math.pi * k / 5
            img = np.full((h, w), -1)
            for y in range(h):
                f = y / (h - 1.0)
                half = (w / 2.0) * (1 - f ** 1.4) * (0.55 + 0.45 * min(1.0, f * 5))
                cx = (w - 1) / 2.0 + (0.9 * math.sin(ph + f * 2.6) * f + 0.35 * math.sin(2 * ph + 1.3) * f * f) * w / 5.0 * 2
                for x in range(w):
                    r = abs(x - cx) / max(half, 0.45)
                    if r > 1.0:
                        continue
                    jit = rng.uniform(-0.08, 0.08)
                    if f > 0.82 + jit:
                        img[y, x] = 3
                    elif r > 0.72 + jit or f > 0.62:
                        img[y, x] = 2
                    elif r < 0.4 and f < 0.45:
                        img[y, x] = 0
                    else:
                        img[y, x] = 1
            if k % 2 == 1 and h >= 8:              # abgerissene Zunge oben
                cx = int(round((w - 1) / 2.0 + 0.8 * math.sin(ph)))
                img[h - 2:, :] = -1
                img[h - 3, cx] = 2
                img[h - 1, cx] = 3
            frames.append(img)
        sprites[name] = frames
    return sprites


SPRITES = _make_sprites()


def _draw(c, x, by, name, texel_m, phase, mirror):
    tp = max(1, int(texel_m * c.S + 0.5))
    img = SPRITES[name][(int(math.floor(c.t * 10 + 1e-6)) + phase) % 5]
    h, w = img.shape
    cols = [P(G, 7), P(G, 6), P(G, 4), P(G, 3)]
    for ty in range(h):
        for tx in range(w):
            k = img[ty, w - 1 - tx if mirror else tx]
            if k >= 0:
                c.put(x + (tx - w / 2.0) * tp, by - (ty + 1) * tp, cols[k], tp)
    return h * tp


def _spots(c, n, flame_px):
    """n Fusspunkte auf der Silhouette: Oberkanten (Pixel, ueber denen nichts mehr zum Koerper gehoert), mit
    Mindestabstand verteilt; die Reihenfolge ist fest (Seed), damit die Bilder wiederholbar sind."""
    edge = c.mask & ~R2._sh(c.mask, 1, 0)
    ys, xs = np.where(edge)
    hx, hy = c.A["head"]
    keep = ~((np.abs(xs - hx) < 0.10 * c.S + flame_px / 2) & (ys > hy - 0.04 * c.S))   # Augen frei halten
    ys, xs = ys[keep], xs[keep]
    if len(xs) == 0:
        return []
    rng = np.random.default_rng(17)
    pts = np.stack([xs, ys], axis=1).astype(float)
    chosen = [int(np.argmin(pts[:, 1]))]
    while len(chosen) < n:
        far = np.linalg.norm(pts[:, None, :] - pts[chosen][None, :, :], axis=2).min(axis=1)
        far = far + rng.uniform(0, 0.15 * flame_px, len(far))     # kleines Rauschen: nicht zu gleichmaessig
        chosen.append(int(np.argmax(far)))
    return [(int(pts[i][0]), int(pts[i][1])) for i in chosen]


def holy_fire_r4(c, n, texel_m):
    """Der Koerper brennt mit vielen kleinen Flammen. Ab 10 m tragen Toenung und Rand mehr (Flammen sind dort Pixel)."""
    far = c.d >= 10
    amt = (0.28, 0.42, 0.54) if far else (0.18, 0.30, 0.40)
    R2._bands(c, [(0.0, 0.4, P(G, 3), amt[0]), (0.4, 0.75, P(G, 3), amt[1]), (0.75, 1.01, P(G, 4), amt[2])])
    rim = R2._inner_rim(c.mask)
    c.img[rim] = c.img[rim] * 0.2 + c.c(P(G, 6 if far else 4)) * 0.8
    flame_px = max(1, int(texel_m * c.S + 0.5)) * 6
    spots = _spots(c, n, flame_px)
    rng = np.random.default_rng(23)
    names = list(rng.choice(["L", "M", "M", "S", "S"], size=len(spots)))
    top = int(np.argmin([p[1] for p in spots])) if spots else 0
    if spots:
        names[top] = "L"                                          # die oberste Flamme (Kopf) ist die groesste
    order = sorted(range(len(spots)), key=lambda i: spots[i][1])
    tops = []
    for i in order:
        x, y = spots[i]
        hpx = _draw(c, x, y + max(1, int(0.015 * c.S)), names[i], texel_m, int(rng.integers(0, 5)), bool(i % 2))
        tops.append((x, y - hpx))
    # wenige Funken wie bei der Fackel: 3 Stueck von den obersten Flammen, 0,6 s, aufsteigend
    s = max(2, int(round(0.03 * c.S)))
    for k in range(min(3, len(tops))):
        a = ((c.t + k / 3.0) % 1.0) / 0.6
        if a >= 1:
            continue
        x, y = tops[k]
        c.put(x + (0.03 + 0.07 * a) * c.S * (1 if k % 2 else -1), y - (0.04 + 0.25 * a) * c.S,
              P(G, 7) if a < 0.6 else P(G, 4), s)
    ys, xs = np.where(c.mask)
    for i in rng.choice(len(xs), size=min(4, len(xs)), replace=False):
        c.put(xs[i], ys[i], P(G, 7), max(1, int(round(0.02 * c.S))))


E.EFFECT_FUNCS.update({
    ("holy_fire", "R4c1"): lambda c: holy_fire_r4(c, 9, 0.020),
    ("holy_fire", "R4c2"): lambda c: holy_fire_r4(c, 16, 0.015),
})
