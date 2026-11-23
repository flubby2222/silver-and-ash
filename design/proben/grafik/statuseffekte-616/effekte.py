"""Die fuenf Statuseffekte als harte Pixel auf dem fertigen Bild (Issue #616).

Reines NumPy, keine Blender-Aufrufe. Jeder Effekt zeichnet nach Laterne und Nebel, aber vor der 15-Bit-Koernung
(Art Bible, Effekte: "mitgekoernt"). Effektfarben leuchten selbst (keine Laterne), werden aber vom Nebel mitgenommen,
sonst waeren sie aus 12 m heller als die Welt. Alle Farben stammen aus assets/palette/hauptpalette.json; die
Tabelle EFFEKTE nennt Reihe und Stufe (Stufe = Index in der Reihe, bei 0 beginnend wie in der Art Bible).
Kein Rot: die Reihe Blutrot kommt hier nicht vor (Pruefung in pruefe_palette()).
"""

import json
import math
import os

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
PALETTE_JSON = os.path.join(ROOT, "assets", "palette", "hauptpalette.json")
FOG = np.array((16.0, 11.0, 20.0))
BAYER4 = (np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) + 0.5) / 16
H_PX = 480
TAN_HALF = math.tan(math.radians(35.0))


def _load():
    with open(PALETTE_JSON, encoding="utf-8") as f:
        p = json.load(f)
    rows = {}
    for key in ("grund", "akzent"):
        for r in p[key]:
            rows[r["name"]] = [tuple(int(v) for v in c) for c in r["farben"]]
    return rows


ROWS = _load()


def pal(row, step):
    """Farbe aus der Hauptpalette: Reihe und Stufe (Index ab 0)."""
    return ROWS[row][step]


# Die Farbtabelle: Effekt -> Rolle -> (Reihe, Stufe). Genau diese Eintraege landen im Blatt und in farbtabelle.csv.
EFFEKTE = {
    "marked": {
        "name": "Marked", "farben": {
            "Zeichen/Umriss hell": ("Haut/Knochen", 7), "Zeichen mittel": ("Haut/Knochen", 6),
            "Zeichen Rand": ("Stein", 0)}},
    "holy_fire": {
        "name": "Holy Fire", "farben": {
            "Flammenfuss (weissgold)": ("Messing/Gold", 7), "Flammenmitte": ("Messing/Gold", 6),
            "Flammenspitze": ("Messing/Gold", 4)}},
    "grave_chill": {
        "name": "Grave Chill", "farben": {
            "Reif hell": ("Stoff", 7), "Kaelte (Tönung, Boden)": ("Stoff", 6), "Bodenreif dunkel": ("Stoff", 5)}},
    "stun": {
        "name": "Stun", "farben": {
            "Funke Mitte": ("Eisen", 7), "Funke Arme": ("Eisen", 6)}},
    "leech": {
        "name": "Leech", "farben": {
            "Faden/Tropfen": ("Stoff", 5), "Tropfen hell": ("Stoff", 6), "Tönung dunkel": ("Stoff", 4)}},
}


def rgb(effect, role):
    row, step = EFFEKTE[effect]["farben"][role]
    return pal(row, step)


def pruefe_palette():
    """Jede Farbe kommt aus der Palette, keine aus der Reihe Blutrot (Rot gehoert der Angriffs-Ankuendigung)."""
    for e in EFFEKTE.values():
        for row, step in e["farben"].values():
            assert row != "Blutrot", e["name"]
            assert 0 <= step < len(ROWS[row])
    return True


def px_per_metre(d):
    """Pixel je Meter bei 854 x 480, 70 Grad vertikal, in d Metern Abstand."""
    return H_PX / (2 * d * TAN_HALF)


def fog_amount(d):
    t = min(max((d - 3.25) / (18.2 - 3.25), 0.0), 1.0)
    return t * t * (3 - 2 * t)


class Ctx:
    """Zeichenflaeche eines Ghuls: Bild (Zeilen von oben), Maske, Anker in Pixeln, Abstand, Phase t in 0..1."""

    def __init__(self, img, mask, anchors, d, t=0.0, seed=1):
        self.img, self.mask, self.A, self.d, self.t = img, mask, anchors, d, t
        self.S = px_per_metre(d)
        self.f = fog_amount(d)
        self.rng = np.random.default_rng(seed)
        self.h, self.w = img.shape[:2]
        ys, xs = np.where(mask)
        self.x0, self.x1, self.y0, self.y1 = xs.min(), xs.max(), ys.min(), ys.max()

    def c(self, rgbv):
        return np.array(rgbv, float) * (1 - self.f) + FOG * self.f

    def put(self, x, y, color, w=1, under=False):
        x, y = int(round(x)), int(round(y))
        x0, y0, x1, y1 = max(x, 0), max(y, 0), min(x + w, self.w), min(y + w, self.h)
        if x1 <= x0 or y1 <= y0:
            return
        sl = (slice(y0, y1), slice(x0, x1))
        if under:
            keep = ~self.mask[sl]
            self.img[sl][keep] = self.c(color)
        else:
            self.img[sl] = self.c(color)

    def line(self, p, q, color, w=1, dash=None):
        n = int(max(abs(q[0] - p[0]), abs(q[1] - p[1]))) + 1
        for i in range(n + 1):
            if dash and (i // dash[0]) % dash[1] != 0:
                continue
            u = i / max(n, 1)
            self.put(p[0] + (q[0] - p[0]) * u - w // 2, p[1] + (q[1] - p[1]) * u - w // 2, color, w)

    def blend_mask(self, color, amount):
        """Koerper in Richtung Farbe tönen: harte, feste Mischung (kein Verlauf)."""
        self.img[self.mask] = self.img[self.mask] * (1 - amount) + self.c(color) * amount

    def outline(self, color):
        k = max(1, int(math.ceil(self.S / 40)))
        m = self.mask.copy()
        grown = m.copy()
        for _ in range(k):
            g = grown.copy()
            g[1:] |= grown[:-1]
            g[:-1] |= grown[1:]
            g[:, 1:] |= grown[:, :-1]
            g[:, :-1] |= grown[:, 1:]
            grown = g
        ring = grown & ~m
        self.img[ring] = self.c(color)
        return ring

    def top_profile(self):
        top = {}
        for x in range(self.x0, self.x1 + 1):
            ys = np.where(self.mask[:, x])[0]
            if len(ys):
                top[x] = ys.min()
        return top


# --- Marked ---------------------------------------------------------------------------------------------------------

def marked_a(c):
    """Zeichen auf der Brust: Raute mit Punkt, dunkler Rand, die Farbe pulsiert zwischen Knochen 7 und 6."""
    cx, cy = c.A["chest"]
    r = max(3, int(round(0.13 * c.S)))
    th = max(1, int(round(r / 3.5)))
    bright = rgb("marked", "Zeichen/Umriss hell") if (c.t % 1.0) < 0.5 else rgb("marked", "Zeichen mittel")
    edge = rgb("marked", "Zeichen Rand")
    for dy in range(-r - 2, r + 3):
        for dx in range(-r - 2, r + 3):
            m = abs(dx) + abs(dy)
            if m <= r + 1 + 0:
                col = None
                if m >= r - th + 1:
                    col = bright
                elif m <= max(0, th - 1):
                    col = bright
                elif m == r + 2:
                    col = edge
                else:
                    col = edge
                if m <= r + 1:
                    c.put(cx + dx, cy + dy, col)
    # Eckspitzen nach aussen, damit das Zeichen auch klein als Fadenkreuz liest
    tip = max(1, int(round(r / 2.5)))
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        for i in range(r + 2, r + 2 + tip):
            c.put(cx + dx * i, cy + dy * i, bright)


def marked_b(c):
    """Heller Umriss um die ganze Gestalt, pulsiert zwischen Knochen 7 und 6."""
    bright = rgb("marked", "Zeichen/Umriss hell") if (c.t % 1.0) < 0.5 else rgb("marked", "Zeichen mittel")
    c.outline(bright)


# --- Holy Fire ------------------------------------------------------------------------------------------------------

def _tongues(c, height_m, width_m, step_scale=0.8):
    """Flammenzungen auf der Oberkante der Gestalt: Rand Messing 4, Innen Messing 6, heisser Kern unten Messing 7."""
    top = c.top_profile()
    w = max(5, int(round(width_m * c.S)))
    xs = list(range(c.x0 + w // 2, c.x1 - w // 2 + 1, max(2, int(w * step_scale))))
    rim, mid, core = (rgb("holy_fire", "Flammenspitze"), rgb("holy_fire", "Flammenmitte"),
                      rgb("holy_fire", "Flammenfuss (weissgold)"))
    for k, x in enumerate(xs):
        base_y = top.get(x, c.y0) + 3
        ph = 0.37 * k
        hgt = height_m * c.S * (0.75 + 0.3 * math.sin(2 * math.pi * (c.t * 2 + ph)) + 0.2 * ((k * 5) % 3 - 1) / 2)
        hgt = max(4, hgt)
        wob = math.sin(2 * math.pi * (c.t + ph)) * max(1.0, w * 0.35)
        for row in range(int(hgt)):
            frac = row / hgt
            half = (w / 2.0) * (1 - frac ** 1.7) if frac < 0.97 else 0.4
            cxr = x + wob * frac * frac
            lo, hi = int(math.floor(-half)), int(math.ceil(half))
            for dx in range(lo, hi + 1):
                if abs(dx) <= half + 0.01:
                    edge = abs(dx) >= half - 1.0 or row >= hgt - 2
                    if edge and w >= 6:
                        col = rim
                    elif frac < 0.5:
                        col = core
                    else:
                        col = mid
                    c.put(cxr + dx, base_y - row, col)


def holy_fire_a(c):
    """Flammenzungen auf Kopf, Schultern und Ruecken; dazu wenige aufsteigende Funken."""
    _tongues(c, 0.34, 0.12)
    top = c.top_profile()
    xs = list(top)
    for k in range(3):
        x = xs[(k * 7 + 3) % len(xs)]
        u = (c.t + k / 3.0) % 1.0
        y = top[x] - (0.34 + 0.32 * u) * c.S
        s = max(2, int(round(0.03 * c.S)))
        c.put(x + (k - 1) * s, y, rgb("holy_fire", "Flammenfuss (weissgold)"), s)


def holy_fire_b(c):
    """Der Koerper brennt: Toenung ins Weissgold im Takt, dazu niedrige Flammen."""
    if (c.t % 1.0) < 0.5:
        c.blend_mask(rgb("holy_fire", "Flammenspitze"), 0.40)
    else:
        c.blend_mask(rgb("holy_fire", "Flammenmitte"), 0.30)
    _tongues(c, 0.18, 0.10)


# --- Grave Chill ----------------------------------------------------------------------------------------------------

def _ground_patch(c):
    gx, gy = c.A["ground"]
    rx = 0.62 * c.S
    ry = max(2.0, rx * 1.6 / c.d * 1.05)
    edge = rgb("grave_chill", "Reif hell")
    dark = rgb("grave_chill", "Bodenreif dunkel")
    mid = rgb("grave_chill", "Kaelte (Tönung, Boden)")
    for y in range(int(gy - ry - 1), int(gy + ry + 2)):
        for x in range(int(gx - rx - 1), int(gx + rx + 2)):
            if x < 0 or y < 0 or x >= c.w or y >= c.h or c.mask[y, x]:
                continue
            e = ((x - gx) / rx) ** 2 + ((y - gy) / ry) ** 2
            if e <= 1.0:
                if e > 0.72:
                    col = edge
                else:
                    col = mid if BAYER4[y % 4, x % 4] < 0.5 else dark
                c.put(x, y, col)


def grave_chill_a(c):
    """Kalt getoent, Reifflecken auf dem Koerper und eine Reifscheibe auf dem Boden."""
    c.blend_mask(rgb("grave_chill", "Kaelte (Tönung, Boden)"), 0.4)
    _ground_patch(c)
    ys, xs = np.where(c.mask)
    hell = rgb("grave_chill", "Reif hell")
    s = max(1, int(round(0.03 * c.S)))
    for i in np.random.default_rng(7).choice(len(xs), size=min(14, len(xs)), replace=False):
        c.put(xs[i], ys[i], hell, s)


def grave_chill_b(c):
    """Eisschicht auf allen nach oben zeigenden Kanten und die Reifscheibe auf dem Boden, Koerper bleibt grün."""
    _ground_patch(c)
    hell = rgb("grave_chill", "Reif hell")
    mid = rgb("grave_chill", "Kaelte (Tönung, Boden)")
    depth = max(1, int(round(c.S / 40)))
    m = c.mask
    crust = np.zeros_like(m)
    for k in range(1, depth + 3):
        shifted = np.zeros_like(m)
        shifted[k:] = m[:-k]
        crust |= m & ~shifted
    c.img[crust] = c.c(hell)
    below = np.zeros_like(m)
    below[1:] = crust[:-1]
    c.img[below & m & ~crust] = c.c(mid)


# --- Stun -----------------------------------------------------------------------------------------------------------

def stun_a(c):
    """Drei kreisende Sterne ueber dem Kopf (Plus-Form, Mitte hell, Arme etwas dunkler)."""
    cx, cy = c.A["head_top"]
    rx, ry = 0.30 * c.S, 0.075 * c.S
    arm = max(3, int(round(0.075 * c.S)))
    thick = max(1, int(round(arm / 2.5)))
    mid, armc = rgb("stun", "Funke Mitte"), rgb("stun", "Funke Arme")
    cy -= 0.10 * c.S
    for k in range(3):
        a = 2 * math.pi * (c.t + k / 3.0)
        x, y = cx + rx * math.cos(a), cy + ry * math.sin(a)
        for i in range(-arm, arm + 1):
            c.put(x + i - thick // 2, y - thick // 2, armc, thick)
            c.put(x - thick // 2, y + i - thick // 2, armc, thick)
        c.put(x - thick // 2, y - thick // 2, mid, thick)
        c.put(x - thick // 2 - 0, y - thick // 2 - 0, mid, thick)


def stun_b(c):
    """Zwei Zickzack-Blitze von den Schultern ueber den Kopf, zucken von Bild zu Bild."""
    mid = rgb("stun", "Funke Mitte")
    armc = rgb("stun", "Funke Arme")
    rng = np.random.default_rng(int(c.t * 100) + 3)
    w = max(1, int(round(c.S / 60)))
    for side, key in ((-1, "shoulderL"), (1, "shoulderR")):
        x, y = c.A[key]
        px = x
        py = y
        n = 4
        for i in range(n):
            nx = x + side * (0.04 + 0.02 * i) * c.S * (1 if i % 2 else -1.4) + rng.uniform(-1, 1) * 0.03 * c.S
            ny = py - 0.12 * c.S
            c.line((px, py), (nx, ny), armc, w + 1)
            c.line((px, py), (nx, ny), mid, w)
            px, py = nx, ny


# --- Leech ----------------------------------------------------------------------------------------------------------

def _bezier(p0, p1, p2, u):
    return ((1 - u) ** 2 * p0[0] + 2 * (1 - u) * u * p1[0] + u * u * p2[0],
            (1 - u) ** 2 * p0[1] + 2 * (1 - u) * u * p1[1] + u * u * p2[1])


def leech_a(c):
    """Gestrichelter Faden von der Brust zum Spieler (unten Bildmitte), darauf laufende Tropfen."""
    p0 = c.A["chest"]
    target = (c.w / 2.0, c.h + 10.0)
    L = 0.42
    p2 = (p0[0] + (target[0] - p0[0]) * L, p0[1] + (target[1] - p0[1]) * L)
    p1 = ((p0[0] + p2[0]) / 2 + (p2[0] - p0[0]) * 0.1, (p0[1] + p2[1]) / 2 - abs(p2[1] - p0[1]) * 0.18)
    faden, hell = rgb("leech", "Faden/Tropfen"), rgb("leech", "Tropfen hell")
    w = max(2, int(round(c.S / 30)))
    for i in range(40):
        if i % 3 == 2:
            continue
        x, y = _bezier(p0, p1, p2, i / 40.0)
        c.put(x - w // 2, y - w // 2, faden, w)
    drop = max(2, int(round(0.045 * c.S)))
    for k in range(3):
        u = (c.t + k / 3.0) % 1.0
        x, y = _bezier(p0, p1, p2, u)
        c.put(x - drop // 2, y - drop // 2, hell, drop)


def leech_b(c):
    """Der Koerper dunkelt ein und blutet blasse Tropfen nach unten zum Spieler; keine Linie."""
    c.blend_mask(rgb("leech", "Tönung dunkel"), 0.35 if (c.t % 1.0) < 0.5 else 0.5)
    p0 = c.A["chest"]
    target = (c.w / 2.0, c.h + 10.0)
    faden, hell = rgb("leech", "Faden/Tropfen"), rgb("leech", "Tropfen hell")
    drop = max(2, int(round(0.05 * c.S)))
    for k in range(4):
        u = ((c.t + k / 4.0) % 1.0) * 0.40
        x = p0[0] + (target[0] - p0[0]) * u * 0.8 + math.sin(k * 2.1) * 0.05 * c.S
        y = p0[1] + (target[1] - p0[1]) * u
        c.put(x - drop // 2 - 1, y - drop // 2 - 1, faden, drop + 2)
        c.put(x - drop // 2, y - drop // 2, hell, drop)


EFFECT_FUNCS = {
    ("marked", "A"): marked_a, ("marked", "B"): marked_b,
    ("holy_fire", "A"): holy_fire_a, ("holy_fire", "B"): holy_fire_b,
    ("grave_chill", "A"): grave_chill_a, ("grave_chill", "B"): grave_chill_b,
    ("stun", "A"): stun_a, ("stun", "B"): stun_b,
    ("leech", "A"): leech_a, ("leech", "B"): leech_b,
}

# Reihenfolge beim Zeichnen: Toenungen und Boden zuerst, dann Umriss, Zeichen, Flammen, Funken, Faeden
ORDER = ["grave_chill", "leech", "marked", "holy_fire", "stun"]


def apply(img, mask, anchors, d, effects, t=0.0, seed=1):
    """effects: Liste (effekt, variante). Veraendert img (Zeilen von oben, Gleitkomma 0..255) und gibt es zurueck."""
    c = Ctx(img, mask, anchors, d, t, seed)
    for eff in sorted(effects, key=lambda e: ORDER.index(e[0])):
        EFFECT_FUNCS[eff](c)
    return img


def bayer15(img):
    """Koernung wie im Spiel und in psx.compose('spiel'): 15 Bit mit festem 4 x 4-Punktmuster an der Bildschirmposition
    (ADR 0010), nach Licht, Nebel und Effekten."""
    h, w = img.shape[:2]
    thr = np.tile(BAYER4, (h // 4 + 1, w // 4 + 1))[:h, :w][..., None]
    return np.clip(np.floor((img + (thr - 0.5) * 8) / 8) * 8 + 4, 0, 255)
