"""Runde 3 der Statuseffekte (#616): nur Holy Fire und Leech neu. Alles andere (Stun A, Grave Chill A, Marked B,
Reihe Violett) steht fest und kommt aus Runde 2.

Urteil des Menschen zu Runde 2:
  Holy Fire: Stufe a, aber der Burn-Effekt gefaellt noch nicht; er soll sich an der Fackel-Animation orientieren.
  Leech: Stufe b, aber etwas weniger Partikel.

Vorbild Fackel (design/proben/effekte/fackel/fackel.py, Funktion flipbook_images, und runde2/fackel_runde2.py):
  - Flamme = gemaltes Daumenkino aus 5 Bildern zu 9 x 15 Texeln, Bildwechsel alle 0,1 s (zwei Durchlaeufe je Sekunde),
  - Form: Tropfen, unten breit, nach oben schmaler, pendelt seitlich; auf jedem zweiten Bild eine abgerissene Zunge,
  - 4 Farbstufen: Kern Messing 7, Mitte 6, Rand 5, Spitze 3 (hier Rand 4, weil Messing 5 am Gegner gemieden wird),
  - 4 Funken, je 0,6 s Lebenszeit, um 0,25 s versetzt, aufsteigend, quadratisch, mind. 3 Pixel (hier 2),
  - harte Texel, keine Teilchenhaufen, kein Schein.
Das Daumenkino wird direkt aus fackel.py uebernommen (keine Kopie), damit es dieselben Bilder sind.

Fassungen:
  Holy Fire a1: nah am Vorbild: je eine Fackelflamme auf Kopf und beiden Schultern, 4 Funken wie bei der Fackel.
  Holy Fire a2: wie a1, dazu vier kleine Fackelflammen, die aus dem Koerper wachsen (gespiegelt, anderer Takt).
  Leech b1: Stufe b mit etwa zwei Dritteln der Teilchen; b2: etwa die Haelfte.
"""

import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
PARENT = os.path.dirname(HERE)
ROOT = os.path.normpath(os.path.join(PARENT, "..", "..", "..", ".."))
sys.path.insert(0, os.path.join(PARENT, "r2"))
sys.path.insert(0, PARENT)
sys.path.insert(0, os.path.join(ROOT, "design", "proben", "effekte", "fackel"))
import effekte as E  # noqa: E402
import effekte_r2 as R2  # noqa: E402
import fackel as F  # noqa: E402

P = R2.P
G, V = R2.G, R2.V

EFFEKTE3 = {
    "holy_fire": {"name": "Holy Fire (a1/a2, Fackel-Flamme)", "farben": {
        "Kern weissgold": (G, 7), "Flammenmitte": (G, 6), "Flammenrand (Fackel: 5)": (G, 4),
        "Spitze/Glut dunkel": (G, 3)}},
    "leech": {"name": "Leech (b1/b2, Violett)", "farben": {
        "Faden/Tropfen": (V, 5), "Tropfen hell": (V, 6), "Glanzpunkt": (V, 7), "Toenung": (V, 3),
        "Toenung kraeftig": (V, 4), "Tropfenrand dunkel": (V, 2)}},
}

FLIP = F.flipbook_images()        # 5 Bilder 9 x 15, y = 0 unten, Index 0..3 = Kern, Mitte, Rand, Spitze
TEXEL_W, TEXEL_H = F.A_W, F.A_H


def pruefe_palette3():
    R2.pruefe_palette2()
    for e in EFFEKTE3.values():
        for row, step in e["farben"].values():
            assert row != "Blutrot" and not (row == G and step == 5)


def _torch_flame(c, x, by, texel_m, phase, mirror=False):
    """Fackel-Flamme (9 x 15 Texel) mit Fuss mittig auf (x, by); Takt der Fackel: Bildwechsel alle 0,1 s."""
    tp = max(1, int(round(texel_m * c.S + 0.35)))
    img = FLIP[(int(math.floor(c.t * 10 + 1e-6)) + phase) % len(FLIP)]
    cols = [P(G, 7), P(G, 6), P(G, 4), P(G, 3)]
    for ty in range(TEXEL_H):
        for tx in range(TEXEL_W):
            k = img[ty, TEXEL_W - 1 - tx if mirror else tx]
            if k < 0:
                continue
            c.put(x + (tx - TEXEL_W / 2.0) * tp, by - (ty + 1) * tp, cols[k], tp)
    return TEXEL_H * tp


def _sparks(c, x, y, rise_m):
    """4 Funken wie bei der Fackel: 0,6 s Lebenszeit, um 0,25 s versetzt, steigen auf."""
    s = max(2, int(round(0.03 * c.S)))
    for k in range(4):
        a = ((c.t + k / 4.0) % 1.0) / 0.6
        if a >= 1:
            continue
        side = (-1, 1, 0.4, -0.5)[k]
        px = x + (side * (0.03 + 0.12 * a) + 0.02 * math.sin(7 * a + k)) * c.S
        py = y - (0.05 + rise_m * a) * c.S
        c.put(px, py, P(G, 7) if a < 0.6 else P(G, 4), s)


def holy_fire_r3(c, own_shape):
    """Der Koerper brennt: leichte Toenung (Stufe a aus Runde 2), glimmender Innenrand, Fackel-Flammen."""
    R2._bands(c, [(0.0, 0.4, P(G, 3), 0.18), (0.4, 0.75, P(G, 3), 0.30), (0.75, 1.01, P(G, 4), 0.40)])
    rim = R2._inner_rim(c.mask)
    c.img[rim] = c.img[rim] * 0.2 + c.c(P(G, 4)) * 0.8
    S = c.S
    hx, hy = c.A["head_top"]
    lx, ly = c.A["shoulderL"]
    rx, ry = c.A["shoulderR"]
    # Koerperflammen zuerst (kleiner, kommen hinter die grossen)
    if own_shape:
        ys, xs = np.where(c.mask)
        lo = ys > c.y0 + 0.55 * (c.y1 - c.y0)
        ys, xs = ys[lo], xs[lo]
        order = np.argsort(xs)
        for k in range(4):
            if len(xs) == 0:
                break
            i = order[int((k + 0.5) / 4 * (len(order) - 1))]
            _torch_flame(c, xs[i], ys[i] + 0.02 * S, 0.024, 1 + 2 * k, mirror=bool(k % 2))
    _torch_flame(c, lx, ly + 0.04 * S, 0.028, 2)
    _torch_flame(c, rx, ry + 0.04 * S, 0.028, 4, mirror=True)
    h = _torch_flame(c, hx, hy + 0.05 * S, 0.036, 0)
    _sparks(c, hx, hy + 0.05 * S - h, 0.35)
    ys, xs = np.where(c.mask)
    rng = np.random.default_rng(3)
    for i in rng.choice(len(xs), size=min(5, len(xs)), replace=False):
        c.put(xs[i], ys[i], P(G, 7), max(1, int(round(0.02 * S))))


def leech_r3(c, beads, drops, streaks, thread_keep):
    """Leech Stufe b mit weniger Teilchen. beads/drops/streaks: Anzahl; thread_keep: Anteil der Faden-Strichel (Muster
    i % 6 < keep)."""
    pulse = (c.t % 1.0) < 0.5
    R2._bands(c, [(0.0, 0.5, P(V, 3), 0.40 if pulse else 0.50), (0.5, 1.01, P(V, 4), 0.46 if pulse else 0.56)])
    rim = R2._inner_rim(c.mask)
    c.img[rim] = c.img[rim] * 0.25 + c.c(P(V, 5)) * 0.75
    ring = R2._dilate(c.mask, max(1, int(math.ceil(c.S / 50)))) & ~c.mask
    yy, xx = np.indices(ring.shape)
    c.img[ring & (((xx + yy) % 2) == 0)] = c.c(P(V, 6))
    c.img[ring & (((xx + yy) % 2) == 1)] = c.c(P(V, 3))
    S = c.S
    rng = np.random.default_rng(9)
    p0 = c.A["chest"]
    target = (c.w / 2.0, c.h + 10.0)
    ys, xs = np.where(c.mask)
    for i in rng.choice(len(xs), size=min(streaks, len(xs)), replace=False):
        L = int(rng.integers(3, 4 + int(0.22 * S)))
        for j in range(L):
            if 0 <= ys[i] + j < c.h and c.mask[ys[i] + j, xs[i]]:
                c.put(xs[i], ys[i] + j, P(V, 5) if j < L - 1 else P(V, 7))
    L = 0.42
    p2 = (p0[0] + (target[0] - p0[0]) * L, p0[1] + (target[1] - p0[1]) * L)
    p1 = ((p0[0] + p2[0]) / 2 + (p2[0] - p0[0]) * 0.1, (p0[1] + p2[1]) / 2 - abs(p2[1] - p0[1]) * 0.18)
    w = max(2, int(round(S / 30)))
    for i in range(44):
        if i % 6 >= thread_keep:
            continue
        x, y = E._bezier(p0, p1, p2, i / 44.0)
        c.put(x - w // 2, y - w // 2, P(V, 5), w)
    for k in range(beads):
        u = (c.t + k / float(beads)) % 1.0
        x, y = E._bezier(p0, p1, p2, u)
        b = max(3, int(round(0.055 * S)))
        c.put(x - b // 2 - 1, y - b // 2 - 1, P(V, 2), b + 2)
        c.put(x - b // 2, y - b // 2, P(V, 6), b)
        c.put(x - b // 2, y - b // 2, P(V, 7), max(1, b // 3))
    drop = max(3, int(round(0.055 * S)))
    for k in range(drops):
        u = ((c.t + k / float(drops)) % 1.0) * 0.42
        x = p0[0] + (target[0] - p0[0]) * u * 0.8 + math.sin(k * 2.1) * 0.06 * S
        y = p0[1] + (target[1] - p0[1]) * u + 0.08 * S
        c.put(x - drop // 2 - 1, y - drop // 2 - 1, P(V, 2), drop + 2)
        c.put(x - drop // 2, y - drop // 2, P(V, 5), drop)
        c.put(x - drop // 2, y - drop // 2, P(V, 7), max(1, drop // 3))


E.EFFECT_FUNCS.update({
    ("holy_fire", "R3a1"): lambda c: holy_fire_r3(c, False),
    ("holy_fire", "R3a2"): lambda c: holy_fire_r3(c, True),
    ("leech", "R3b1"): lambda c: leech_r3(c, 2, 3, 5, 3),
    ("leech", "R3b2"): lambda c: leech_r3(c, 1, 2, 4, 2),
})
