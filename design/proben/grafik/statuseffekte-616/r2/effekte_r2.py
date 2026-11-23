"""Runde 2 der Statuseffekte (#616): gewaehlte Fassungen, detailreicher. Stun A, Grave Chill A, Marked B, Holy Fire B,
Leech B (Urteil des Menschen), dazu die neue Reihe Violett (violett-vorschlag.json, nicht in der Hauptpalette).

Baut auf effekte.py (Runde 1) auf: gleiche Zeichenflaeche (Ctx), gleiche Regeln (harte Pixel, nach Nebel, vor der
Koernung, nur Palettenfarben, kein Rot, Royal-Gold = Messing/Gold 5 wird gemieden). Registriert die Fassungen in
E.EFFECT_FUNCS unter den Namen "R2" bzw. "R2a" (zurueckhaltend) und "R2b" (kraeftiger).
"""

import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import effekte as E  # noqa: E402

with open(os.path.join(HERE, "violett-vorschlag.json"), encoding="utf-8") as _f:
    VIOLETT = [tuple(int(v) for v in c) for c in json.load(_f)["farben"]]
E.ROWS["Violett"] = VIOLETT   # nur im Speicher dieses Probestuecks

G = "Messing/Gold"
K = "Haut/Knochen"
ST = "Stoff"
EI = "Eisen"
V = "Violett"

# Farbtabelle Runde 2: Effekt -> Rolle -> (Reihe, Stufe). Landet im Blatt und in farbtabelle.csv.
EFFEKTE2 = {
    "marked": {"name": "Marked (B, Umriss)", "farben": {
        "Umriss hell": (K, 7), "Umriss wandernd": (K, 6), "Innenrand": (K, 4), "Aussenkontur": ("Stein", 0)}},
    "holy_fire": {"name": "Holy Fire (B, Flammen und Toenung)", "farben": {
        "Kern weissgold": (G, 7), "Flammenmitte": (G, 6), "Flammenrand": (G, 4), "Spitze/Glut dunkel": (G, 3)}},
    "grave_chill": {"name": "Grave Chill (A, Toenung)", "farben": {
        "Reif hell": (ST, 7), "Toenung oben/Hauch": (ST, 6), "Toenung unten/Bodenreif": (ST, 5), "Risse/Schatten": (ST, 4)}},
    "stun": {"name": "Stun (A, Funkenkreuze)", "farben": {
        "Funke Mitte": (EI, 7), "Funke Arme": (EI, 6), "Schweif": (EI, 5), "Bahn dunkel": (EI, 4)}},
    "leech": {"name": "Leech (B, Toenung und Tropfen)", "farben": {
        "Faden/Tropfen": (V, 5), "Tropfen hell": (V, 6), "Glanzpunkt": (V, 7), "Toenung": (V, 3),
        "Toenung kraeftig": (V, 4), "Tropfenrand dunkel": (V, 2)}},
}


def pruefe_palette2():
    for e in EFFEKTE2.values():
        for row, step in e["farben"].values():
            assert row != "Blutrot"
            assert 0 <= step < len(E.ROWS[row])
        # Royal-Gold meiden
        assert all(not (r == G and s == 5) for r, s in e["farben"].values())
    assert len(VIOLETT) == 8


def P(row, step):
    return E.pal(row, step)


# --- Hilfen ---------------------------------------------------------------------------------------------------------

def _sh(m, dy, dx):
    """out[y, x] = m[y - dy, x - dx], Rand mit False."""
    o = np.zeros_like(m)
    H, W = m.shape
    o[max(dy, 0):H + min(dy, 0), max(dx, 0):W + min(dx, 0)] = m[max(-dy, 0):H + min(-dy, 0), max(-dx, 0):W + min(-dx, 0)]
    return o


def _dilate(m, k):
    g = m.copy()
    for _ in range(k):
        g = g | _sh(g, 1, 0) | _sh(g, -1, 0) | _sh(g, 0, 1) | _sh(g, 0, -1)
    return g


def _inner_rim(m):
    return m & ~(_sh(m, 1, 0) & _sh(m, -1, 0) & _sh(m, 0, 1) & _sh(m, 0, -1))


def _bands(c, spec):
    """Harte Toenungsbaender ueber die Koerperhoehe: spec = [(von, bis, Farbe, Anteil)] mit Anteilen 0..1 der Hoehe."""
    yy = np.arange(c.h)[:, None] * np.ones((1, c.w))
    v = (yy - c.y0) / max(1.0, c.y1 - c.y0)
    for lo, hi, col, amt in spec:
        m = c.mask & (v >= lo) & (v < hi)
        c.img[m] = c.img[m] * (1 - amt) + c.c(col) * amt


def _dither(c, m, col):
    """Jeder zweite Pixel (Schachbrett) der Flaeche m in Farbe col."""
    yy, xx = np.indices(m.shape)
    sel = m & (((xx + yy) % 2) == 0)
    c.img[sel] = c.c(col)


# --- Marked B -------------------------------------------------------------------------------------------------------

def marked_r2(c):
    """Umriss in zwei Weissstufen, die um die Gestalt wandern; dunkle Aussenkontur; Innenrand."""
    m = c.mask
    k = max(2, int(math.ceil(c.S / 32)))
    grown = _dilate(m, k)
    ring = grown & ~m
    outer = _dilate(grown, 1) & ~grown
    yy, xx = np.indices(m.shape)
    shift = int(c.t * 6)
    stripe = (((xx + yy + shift) // 3) % 3) == 0
    hell, wand = P(K, 7), P(K, 6)
    c.img[outer] = c.c(P("Stein", 0))
    c.img[ring] = c.c(hell)
    c.img[ring & stripe] = c.c(wand)
    inner = _inner_rim(m)
    c.img[inner] = c.img[inner] * 0.5 + c.c(P(K, 4)) * 0.5


# --- Holy Fire B ----------------------------------------------------------------------------------------------------

def _flame(c, x, by, h, w, sway, cols, fork=False):
    """Eine Flammenzunge von (x, by) nach oben: Kern weissgold, Mitte gold, Rand orange, Spitze dunkel."""
    h = int(max(5, h))
    w = max(4, int(w))
    for row in range(h):
        f = row / h
        half = (w / 2.0) * (1 - f ** 1.5)
        cx = x + sway * f * f
        if fork and f > 0.5:
            off = (f - 0.5) * w
            spans = [(cx - off, half * 0.62), (cx + off, half * 0.62)]
        else:
            spans = [(cx, half)]
        for sx, hf in spans:
            hf = max(hf, 0.5)
            for X in range(int(math.floor(sx - hf)), int(math.ceil(sx + hf)) + 1):
                r = abs(X - sx) / hf
                if r > 1.0:
                    continue
                if f > 0.85:
                    col = cols["tip"]
                elif r > 0.7 or f > 0.68:
                    col = cols["rim"]
                elif r < 0.4 and f < 0.55:
                    col = cols["core"]
                else:
                    col = cols["mid"]
                c.put(X, by - row, col)


def _flames(c, n_top, n_side, big, small, fork, n_inner=0):
    rng = np.random.default_rng(5)
    top = c.top_profile()
    xs = sorted(top)
    cols = dict(core=P(G, 7), mid=P(G, 6), rim=P(G, 4), tip=P(G, 3))
    items = []
    for k in range(n_top):
        x = xs[min(len(xs) - 1, int((k + 0.5) / n_top * len(xs)))]
        items.append((top[x] + 2, x, big if k % 2 == 0 else small))
    for k in range(n_side):
        side = (-1, 1)[k % 2]
        y = int(c.y0 + rng.uniform(0.3, 0.85) * (c.y1 - c.y0))
        row = np.where(c.mask[y])[0]
        if len(row):
            items.append((y, int(row.min() if side < 0 else row.max()), small * 0.8))
    # Flammen mitten auf dem Koerper (laufen am Koerper hoch): Fuss in der unteren Haelfte, kleiner, dichter bei strong
    ys, xs = np.where(c.mask & (np.arange(c.h)[:, None] > c.y0 + 0.45 * (c.y1 - c.y0)))
    for k in rng.choice(len(xs), size=min(n_inner, len(xs)), replace=False) if len(xs) else []:
        items.append((int(ys[k]), int(xs[k]), small * 0.7))
    items.sort(key=lambda it: it[0])
    for i, (by, x, size) in enumerate(items):
        ph = 0.37 * i
        flick = 0.8 + 0.25 * math.sin(2 * math.pi * (c.t * 2 + ph)) + 0.12 * ((i * 5) % 3 - 1)
        w = max(4, int(round(size * c.S * 0.4)))
        sway = (0.7 * math.sin(i * 2.3) + 0.5 * math.sin(2 * math.pi * (c.t + ph))) * w
        _flame(c, x, by, size * c.S * flick, w, sway, cols, fork=fork and size >= big * 0.99 and i % 4 == 0)


def _embers(c, n, rise):
    rng = np.random.default_rng(11)
    top = c.top_profile()
    xs = sorted(top)
    s = max(2, int(round(0.03 * c.S)))
    for k in range(n):
        x = xs[int(rng.integers(0, len(xs)))] + int(rng.integers(-3, 4))
        u = (c.t + k / n) % 1.0
        y = top.get(min(max(x, xs[0]), xs[-1]), c.y0) - (0.18 + rise * u) * c.S
        col = P(G, 7) if u < 0.35 else (P(G, 6) if u < 0.65 else (P(G, 4) if u < 0.85 else P(G, 3)))
        c.put(x, y, col, s)


def holy_fire_r2(c, strong):
    """Der Koerper brennt: Toenungsbaender (unten heisser), glimmender Rand, Flammen auf Kopf, Schultern und an den
    Seiten, Glutpunkte darueber. strong = kraeftigere Fassung (b)."""
    amt = (0.30, 0.42, 0.52) if strong else (0.18, 0.30, 0.40)
    _bands(c, [(0.0, 0.4, P(G, 3), amt[0]), (0.4, 0.75, P(G, 3), amt[1]), (0.75, 1.01, P(G, 4), amt[2])])
    rim = _inner_rim(c.mask)
    c.img[rim] = c.img[rim] * 0.2 + c.c(P(G, 6 if strong else 4)) * 0.8
    if strong:
        aura = _dilate(c.mask, 1) & ~c.mask
        _dither(c, aura, P(G, 3))
        _flames(c, 11, 6, 0.42, 0.26, True, n_inner=11)
        _embers(c, 8, 0.55)
    else:
        _flames(c, 5, 2, 0.34, 0.22, False, n_inner=3)
        _embers(c, 4, 0.45)
    # Funken auf dem Koerper (Glutflecken)
    ys, xs = np.where(c.mask)
    rng = np.random.default_rng(3)
    for i in rng.choice(len(xs), size=min(10 if strong else 5, len(xs)), replace=False):
        c.put(xs[i], ys[i], P(G, 7), max(1, int(round(0.02 * c.S))))


# --- Grave Chill A --------------------------------------------------------------------------------------------------

def grave_chill_r2(c):
    """Kalt getoent (unten dunkler und dichter), Reifkruste auf den Oberkanten, Eiszapfen, Reifkristalle,
    Atemwolken am Kopf, Reifscheibe mit Rissen und Eisdornen am Boden."""
    _bands(c, [(0.0, 0.55, P(ST, 6), 0.34), (0.55, 1.01, P(ST, 5), 0.48)])
    m = c.mask
    hell = P(ST, 7)
    # Kruste auf Oberkanten
    above = _sh(m, 1, 0)
    crust = m & ~above
    c.img[crust] = c.c(hell)
    E._ground_patch(c)
    S = c.S
    gx, gy = c.A["ground"]
    rx = 0.62 * S
    ry = max(2.0, rx * 1.6 / c.d * 1.05)
    # Risse in der Scheibe
    rng = np.random.default_rng(21)
    for _ in range(3):
        a = rng.uniform(0, 2 * math.pi)
        p = (gx + math.cos(a) * rx * 0.2, gy + math.sin(a) * ry * 0.2)
        q = (gx + math.cos(a) * rx * 0.85, gy + math.sin(a) * ry * 0.85)
        for i in range(int(rx * 0.7)):
            u = i / max(1, int(rx * 0.7) - 1)
            x, y = p[0] + (q[0] - p[0]) * u, p[1] + (q[1] - p[1]) * u
            xi, yi = int(round(x)), int(round(y))
            if 0 <= xi < c.w and 0 <= yi < c.h and not c.mask[yi, xi]:
                c.put(xi, yi, P(ST, 4))
    # Eisdornen am Scheibenrand
    for k in range(7):
        a = math.pi * (0.1 + 0.8 * k / 6.0) + (math.pi if k % 2 else 0)
        x = gx + math.cos(a) * rx * 0.98
        y = gy + math.sin(a) * ry * 0.98
        hh = max(3, int(0.09 * S * (1 + 0.5 * ((k * 3) % 3 - 1) / 2)))
        for row in range(hh):
            half = max(0, int(round((hh - row) / hh * max(1, S / 60))))
            for dx in range(-half, half + 1):
                if 0 <= int(y - row) < c.h and not c.mask[int(y - row), min(max(int(x + dx), 0), c.w - 1)]:
                    c.put(x + dx, y - row, hell if row > 0 else P(ST, 5))
    # Eiszapfen unter den Kanten (Arme, Rumpf)
    ys, xs = np.where(m & ~_sh(m, -1, 0))
    for i in rng.choice(len(xs), size=min(9, len(xs)), replace=False):
        L = int(rng.integers(2, 4 + int(S / 20)))
        for j in range(1, L + 1):
            c.put(xs[i], ys[i] + j, hell if j < L else P(ST, 6), 1, under=False)
    # Reifkristalle (Pluszeichen) und Flecken auf dem Koerper
    ys, xs = np.where(m)
    s = max(1, int(round(0.03 * S)))
    for i in rng.choice(len(xs), size=min(16, len(xs)), replace=False):
        c.put(xs[i], ys[i], hell, s)
    for i in rng.choice(len(xs), size=min(5, len(xs)), replace=False):
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            c.put(xs[i] + dx * s, ys[i] + dy * s, hell, s)
    # Atemwolken (steigen neben dem Kopf auf, werden groesser und dunkler)
    hx, hy = c.A["head"]
    for k in range(3):
        u = (c.t + k / 3.0) % 1.0
        sz = max(2, int(round((0.035 + 0.05 * u) * S)))
        col = hell if u < 0.4 else (P(ST, 6) if u < 0.75 else P(ST, 5))
        c.put(hx + (0.09 + 0.12 * u) * S * (1 if k % 2 else -1), hy - 0.04 * S - u * 0.24 * S, col, sz)


# --- Stun A ---------------------------------------------------------------------------------------------------------

def stun_r2(c):
    """Drei Funkenkreuze kreisen ueber dem Kopf: Schweif aus kleinen Punkten, gestrichelte Bahn, kleine Nachfunken."""
    cx, cy = c.A["head_top"]
    S = c.S
    rx, ry = 0.32 * S, 0.08 * S
    cy -= 0.11 * S
    mid, armc, tail, orbit = P(EI, 7), P(EI, 6), P(EI, 5), P(EI, 4)
    for i in range(0, 24, 2):
        a = 2 * math.pi * i / 24
        c.put(cx + rx * math.cos(a), cy + ry * math.sin(a), orbit, 1)
    for k in range(3):
        a0 = 2 * math.pi * (c.t + k / 3.0)
        big = 1.0 if k == 0 else 0.75
        for j in range(4, 0, -1):
            a = a0 - 0.22 * j
            c.put(cx + rx * math.cos(a), cy + ry * math.sin(a), tail if j < 3 else orbit, 2 if j < 3 else 1)
        x, y = cx + rx * math.cos(a0), cy + ry * math.sin(a0)
        arm = max(3, int(round(0.085 * S * big)))
        thick = max(1, int(round(arm / 2.5)))
        for i in range(-arm, arm + 1):
            col = armc if abs(i) > 1 else mid
            c.put(x + i - thick // 2, y - thick // 2, col, thick)
            c.put(x - thick // 2, y + i - thick // 2, col, thick)
        d = max(1, arm // 2)
        for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
            c.put(x + sx * d, y + sy * d, tail, 1)
        c.put(x - thick // 2, y - thick // 2, mid, thick + 1)


# --- Leech B (Violett) ----------------------------------------------------------------------------------------------

def _bez(p0, p1, p2, u):
    return E._bezier(p0, p1, p2, u)


def leech_r2(c, strong):
    """Der Koerper dunkelt violett ein (pulsiert), Tropfspuren laufen herunter, gerandete Tropfen fallen zum Spieler.
    strong: gepunktete Umrisslinie in Violett, Violett-Rand innen, Faden mit Perlen zur Kamera, mehr Tropfen."""
    pulse = (c.t % 1.0) < 0.5
    _bands(c, [(0.0, 0.5, P(V, 3), 0.40 if pulse else 0.50), (0.5, 1.01, P(V, 4), 0.46 if pulse else 0.56)])
    rim = _inner_rim(c.mask)
    c.img[rim] = c.img[rim] * 0.25 + c.c(P(V, 5)) * 0.75
    if strong:
        ring = _dilate(c.mask, max(1, int(math.ceil(c.S / 50)))) & ~c.mask
        yy, xx = np.indices(ring.shape)
        c.img[ring & (((xx + yy) % 2) == 0)] = c.c(P(V, 6))
        c.img[ring & (((xx + yy) % 2) == 1)] = c.c(P(V, 3))
    S = c.S
    rng = np.random.default_rng(9)
    p0 = c.A["chest"]
    target = (c.w / 2.0, c.h + 10.0)
    # Tropfspuren auf dem Koerper
    ys, xs = np.where(c.mask)
    n = 8 if strong else 5
    for i in rng.choice(len(xs), size=min(n, len(xs)), replace=False):
        L = int(rng.integers(3, 4 + int(0.22 * S)))
        for j in range(L):
            if 0 <= ys[i] + j < c.h and c.mask[ys[i] + j, xs[i]]:
                c.put(xs[i], ys[i] + j, P(V, 5) if j < L - 1 else P(V, 7))
    # Faden mit Perlen zur Kamera
    if strong:
        L = 0.42
        p2 = (p0[0] + (target[0] - p0[0]) * L, p0[1] + (target[1] - p0[1]) * L)
        p1 = ((p0[0] + p2[0]) / 2 + (p2[0] - p0[0]) * 0.1, (p0[1] + p2[1]) / 2 - abs(p2[1] - p0[1]) * 0.18)
        w = max(2, int(round(S / 30)))
        for i in range(44):
            if i % 3 == 2:
                continue
            x, y = _bez(p0, p1, p2, i / 44.0)
            c.put(x - w // 2, y - w // 2, P(V, 5), w)
        for k in range(3):
            u = (c.t + k / 3.0) % 1.0
            x, y = _bez(p0, p1, p2, u)
            b = max(3, int(round(0.055 * S)))
            c.put(x - b // 2 - 1, y - b // 2 - 1, P(V, 2), b + 2)
            c.put(x - b // 2, y - b // 2, P(V, 6), b)
            c.put(x - b // 2, y - b // 2, P(V, 7), max(1, b // 3))
    # Fallende Tropfen mit dunklem Rand und Glanzpunkt
    drop = max(3, int(round(0.055 * S)))
    for k in range(5 if strong else 4):
        u = ((c.t + k / (5.0 if strong else 4.0)) % 1.0) * 0.42
        x = p0[0] + (target[0] - p0[0]) * u * 0.8 + math.sin(k * 2.1) * 0.06 * S
        y = p0[1] + (target[1] - p0[1]) * u + 0.08 * S
        c.put(x - drop // 2 - 1, y - drop // 2 - 1, P(V, 2), drop + 2)
        c.put(x - drop // 2, y - drop // 2, P(V, 5), drop)
        c.put(x - drop // 2, y - drop // 2, P(V, 7), max(1, drop // 3))


E.EFFECT_FUNCS.update({
    ("marked", "R2"): marked_r2,
    ("holy_fire", "R2a"): lambda c: holy_fire_r2(c, False),
    ("holy_fire", "R2b"): lambda c: holy_fire_r2(c, True),
    ("grave_chill", "R2"): grave_chill_r2,
    ("stun", "R2"): stun_r2,
    ("leech", "R2a"): lambda c: leech_r2(c, False),
    ("leech", "R2b"): lambda c: leech_r2(c, True),
})

# Gewaehlte Fassung je Effekt (Urteil des Menschen), in der Reihenfolge der Aufstellung
GEWAEHLT = [("marked", "R2"), ("holy_fire", "R2b"), ("grave_chill", "R2"), ("stun", "R2"), ("leech", "R2b")]
