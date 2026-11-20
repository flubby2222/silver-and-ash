"""Probenblatt Zielwahl der Teleporter (Issue #477, Teil von #436).

Drei Entwuerfe, je in 1280 x 720 und 854 x 480, alles in reinem Python (kein Blender, kein numpy, kein PIL,
nichts heruntergeladen). Schrift: Pixelschrift A "Kapitell" aus ../schrift (Bitmuster im Skript). Stil wie das HUD im Spiel:
harte Pixel, Silberrahmen mit Lichtkante oben links, zweiter Rahmen als Mulde um den Inhalt, Nieten (Schmuck S1) in den Ecken.

Aufruf:   python teleporter_karte.py          (schreibt alle Bilder neben das Skript; zweiter Lauf bitgleich)

Ausgabe:  A-karte-1280.png  A-karte-854.png  B-liste-*.png  C-karte-liste-*.png  blatt.png  verdeckt.txt
Der Hintergrund ist ein nachgebauter Spielausschnitt (Ziegelwand mit Nebel, 15 Bit, Bayer 4 x 4) mit Platzhalter-HUD.
"""
import os
import struct
import sys
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "schrift"))
import schrift as SC  # noqa: E402

FONT = SC.font_a()
SC.check(FONT, unique=True)

# ------------------------------------------------------------------ Farben (Hauptpalette)
def _desat(c, f=0.8):
    g = 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]
    return tuple(int(round(g + (v - g) * (1 - f))) for v in c)


IRON = [(20, 18, 28), (38, 36, 56), (55, 57, 84), (73, 83, 112), (92, 112, 140), (126, 144, 165), (163, 175, 187), (198, 205, 210)]
NL = [_desat(c) for c in (IRON[2], IRON[3], IRON[4], IRON[5], IRON[6], IRON[7], (226, 228, 230))]  # Silber, dunkel -> hell
INK = (20, 18, 28)         # Eisen 0, dunkler Grund
GROUND0, GROUND1 = (20, 18, 28), (38, 36, 56)
BONE, BONE2 = (216, 210, 192), (195, 192, 164)
DIMTXT = NL[2]
SHADOW = (10, 8, 14)
GOLD, GOLD_L, GOLD_D = (214, 170, 64), (227, 202, 111), (149, 100, 39)
BLOOD, BLOOD_D = (150, 20, 30), (105, 20, 34)
STONE = {0: (16, 11, 20), 2: (45, 40, 47), 3: (52, 46, 54), 4: (61, 52, 56), 5: (71, 61, 66), 6: (79, 67, 73),
         7: (86, 74, 80), 8: (95, 81, 88), 10: (113, 97, 104)}
BAYER = ((0, 8, 2, 10), (12, 4, 14, 6), (3, 11, 1, 9), (15, 7, 13, 5))

# ------------------------------------------------------------------ PNG und Leinwand
def write_png(path, w, h, data):
    raw = b"".join(b"\x00" + bytes(data[y * w * 3:(y + 1) * w * 3]) for y in range(h))

    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)

    png = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
           + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))
    with open(path, "wb") as f:
        f.write(png)


class Cv:
    def __init__(self, w, h, fill=(0, 0, 0)):
        self.w, self.h = w, h
        self.b = bytearray(bytes(fill) * (w * h))

    def copy(self):
        n = Cv(self.w, self.h)
        n.b[:] = self.b
        return n

    def set(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            i = (y * self.w + x) * 3
            self.b[i:i + 3] = bytes(c)

    def get(self, x, y):
        i = (y * self.w + x) * 3
        return tuple(self.b[i:i + 3])

    def rect(self, x0, y0, x1, y1, c):
        x0, y0, x1, y1 = max(0, x0), max(0, y0), min(self.w, x1), min(self.h, y1)
        if x1 <= x0 or y1 <= y0:
            return
        row = bytes(c) * (x1 - x0)
        for y in range(y0, y1):
            i = (y * self.w + x0) * 3
            self.b[i:i + len(row)] = row

    def mix(self, x, y, c, a):
        if 0 <= x < self.w and 0 <= y < self.h:
            i = (y * self.w + x) * 3
            b = self.b
            b[i] = int(b[i] + (c[0] - b[i]) * a)
            b[i + 1] = int(b[i + 1] + (c[1] - b[i + 1]) * a)
            b[i + 2] = int(b[i + 2] + (c[2] - b[i + 2]) * a)

    def blit(self, other, x0, y0):
        for y in range(other.h):
            if 0 <= y0 + y < self.h:
                xs = max(0, x0)
                xe = min(self.w, x0 + other.w)
                if xe > xs:
                    i = ((y0 + y) * self.w + xs) * 3
                    j = (y * other.w + (xs - x0)) * 3
                    self.b[i:i + (xe - xs) * 3] = other.b[j:j + (xe - xs) * 3]

    def save(self, path):
        write_png(path, self.w, self.h, self.b)


# ------------------------------------------------------------------ Text
def text_w(s, k):
    return FONT.width(s, k)


def text(c, s, x, top, k, col, shadow=True, anchor="l"):
    """Schrift A; (x, top) = obere linke Ecke der Versalien. anchor l, m, r."""
    w = text_w(s, k)
    if anchor == "m":
        x -= w // 2
    elif anchor == "r":
        x -= w
    base = top + 6 * k
    pts = FONT.layout(s)
    if shadow:
        for gx, gy in pts:
            c.rect(x + gx * k + k, base + gy * k + k, x + gx * k + 2 * k, base + gy * k + 2 * k, SHADOW)
    for gx, gy in pts:
        c.rect(x + gx * k, base + gy * k, x + gx * k + k, base + gy * k + k, col)
    return w


# ------------------------------------------------------------------ Rahmen, Nieten, Giebel, Marken
def bay(x, y):
    return BAYER[y & 3][x & 3]


def levels(fw):
    if fw <= 1:
        return [3], [1]
    if fw == 2:
        return [4, 2], [0, 3]
    return [4] + [3] * (fw - 2) + [1], [0] + [2] * (fw - 2) + [3]


def frame(c, x0, y0, x1, y1, fw, ch=3, fill=None, alpha=0.72, grain=True):
    """Silberrahmen mit Fase (x1, y1 ausserhalb). fill = (oben, unten) fuer den Grund, alpha = Deckkraft."""
    lt, dk = levels(fw)
    for y in range(y0, y1):
        dy = min(y - y0, y1 - 1 - y)
        top = (y - y0) < (y1 - 1 - y)
        for x in range(x0, x1):
            dx = min(x - x0, x1 - 1 - x)
            corner = dx < ch and dy < ch
            if corner and dx + dy < ch:
                continue
            d = min(dx, dy, dx + dy - ch) if corner else min(dx, dy)
            left = (x - x0) < (x1 - 1 - x)
            if d < fw:
                if corner and dx + dy - ch <= min(dx, dy) or dx == dy:
                    side = 0 if (top and left) else (2 if (not top and not left) else 1)
                elif dx < dy:
                    side = 0 if left else 2
                else:
                    side = 0 if top else 2
                if side == 0:
                    col = NL[lt[d]]
                elif side == 2:
                    col = NL[dk[d]]
                else:
                    col = NL[2]
                if grain and d == fw // 2 and ((x * 7 + y * 13) % 9 == 0):
                    col = NL[max(0, NL.index(col) - 1)]
                c.set(x, y, col)
            elif fill is not None:
                t = (y - y0) / max(1, y1 - y0)
                col = fill[0] if bay(x, y) / 16 >= t * 1.3 else fill[1]
                c.mix(x, y, col, alpha)


def rivet(c, cx, cy, r):
    """Niete (S1): runder Kopf, Licht oben links, dunkler Rand unten rechts."""
    for dy in range(-r - 1, r + 2):
        for dx in range(-r - 1, r + 2):
            d2 = dx * dx + dy * dy
            if d2 > (r + 1) * (r + 1):
                continue
            if d2 > r * r:
                c.set(cx + dx, cy + dy, INK)
            elif dx + dy < -r * 0.55 and d2 <= (r - 0.2) ** 2:
                c.set(cx + dx, cy + dy, NL[6])
            elif dx + dy > r * 0.7:
                c.set(cx + dx, cy + dy, NL[1])
            else:
                c.set(cx + dx, cy + dy, NL[4])


def rivets(c, x0, y0, x1, y1, r, inset):
    for cx, cy in ((x0 + inset, y0 + inset), (x1 - 1 - inset, y0 + inset), (x0 + inset, y1 - 1 - inset), (x1 - 1 - inset, y1 - 1 - inset)):
        rivet(c, cx, cy, r)


def gable(c, mx, y0, hw, h):
    """Spitzgiebel ueber der Oberkante y0 (aus dem Menue C2)."""
    top = y0 - h
    for y in range(top, y0 + 2):
        w = hw * (y - top) / h
        xl, xr = int(round(mx - w)), int(round(mx + w))
        for x in range(xl, xr + 1):
            if x == xl:
                col = NL[5]
            elif x == xr:
                col = NL[0]
            elif x < mx:
                col = NL[3]
            else:
                col = NL[2]
            c.set(x, y, col)
    c.set(mx, y0 - max(2, h // 4), INK)


def well(c, x0, y0, x1, y1, fw=1):
    """Zweiter Rahmen: Mulde um den Inhalt (schmale Silberlinie, dunkel innen), wie die Balken im HUD."""
    c.rect(x0, y0, x1, y1, GROUND0)
    c.rect(x0, y0, x1, y0 + fw, NL[1])
    c.rect(x0, y0, x0 + fw, y1, NL[1])
    c.rect(x0, y1 - fw, x1, y1, NL[3])
    c.rect(x1 - fw, y0, x1, y1, NL[3])


def rule(c, x0, x1, y, fw=1):
    c.rect(x0, y, x1, y + fw, NL[0])
    c.rect(x0, y + fw, x1, y + 2 * fw, NL[3])


def diamond(c, cx, cy, r, fill, edge=INK, lit=None, dark=None):
    for dy in range(-r - 1, r + 2):
        for dx in range(-r - 1, r + 2):
            d = abs(dx) + abs(dy)
            if d > r + 1:
                continue
            if d == r + 1:
                c.set(cx + dx, cy + dy, edge)
                continue
            col = fill
            if lit and dx + dy < -r * 0.5 and d >= r - 1:
                col = lit
            elif dark and dx + dy > r * 0.5 and d >= r - 1:
                col = dark
            c.set(cx + dx, cy + dy, col)


def brackets(c, cx, cy, r, arm, col, w=1):
    """Vier Eckwinkel um (cx, cy) im Abstand r."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            ex, ey = cx + sx * r, cy + sy * r
            for t in range(arm):
                for k in range(w):
                    c.set(ex - sx * t, ey - sy * k, col)
                    c.set(ex - sx * k, ey - sy * t, col)


CURSOR = [
    "X...........",
    "XX..........",
    "XOX.........",
    "XOOX........",
    "XOOOX.......",
    "XOOOOX......",
    "XOOOOOX.....",
    "XOOOOOOX....",
    "XOOOOOOOX...",
    "XOOOOOXXXXX.",
    "XOOXOOX.....",
    "XOX.XOOX....",
    "XX..XOOX....",
    "X....XOOX...",
    ".....XOOX...",
    "......XX....",
]


def cursor(c, x, y, k):
    for j, row in enumerate(CURSOR):
        for i, ch in enumerate(row):
            if ch != ".":
                c.rect(x + i * k, y + j * k, x + (i + 1) * k, y + (j + 1) * k, INK if ch == "X" else (240, 238, 230))


def tooltip(c, P, x, y, s, col=BONE):
    k = P["ts"]
    w = text_w(s, k) + 8 * k
    h = 7 * k + 6 * k
    frame(c, x, y, x + w, y + h, 1, ch=1, fill=(GROUND0, GROUND0), alpha=0.92, grain=False)
    text(c, s, x + 4 * k, y + 3 * k, k, col)
    return w, h


def keycap(c, P, x, y, s):
    """Tastenschild; (x, y) = oben links. Gibt Breite zurueck."""
    k = P["ts"]
    w = text_w(s, k) + 6 * k
    h = 7 * k + 4 * k
    frame(c, x, y, x + w, y + h, 1, ch=1, fill=(GROUND1, GROUND0), alpha=1.0, grain=False)
    text(c, s, x + 3 * k, y + 2 * k, k, BONE, shadow=False)
    return w


def hints(c, P, x, y, items, gap=None):
    """Zeile aus Tastenschildern und Text; items: ('k', 'Enter') | ('t', 'Travel') | ('s', Abstand)."""
    k = P["ts"]
    gap = gap or 4 * k
    cx = x
    for kind, s in items:
        if kind == "k":
            cx += keycap(c, P, cx, y, s) + gap
        elif kind == "t":
            cx += text(c, s, cx, y + 2 * k, k, BONE2) + gap * 2
        else:
            cx += s
    return cx


def hints_w(P, items):
    k = P["ts"]
    gap = 4 * k
    w = 0
    for kind, s in items:
        if kind == "k":
            w += text_w(s, k) + 6 * k + gap
        elif kind == "t":
            w += text_w(s, k) + gap * 2
        else:
            w += s
    return w


# ------------------------------------------------------------------ Spielwelt (Beispielebene)
# Raeume und Gaenge als Raster (x, y, Breite, Tiefe) in Zellen; vereinfachte Raumkarte aus den Raeumen des Generators.
ROOMS = [
    ("S", 1, 5, 5, 4, False),      # Startraum
    ("K1", 6, 6, 3, 1, True),
    ("A", 9, 4, 5, 6, False),      # Kampfraum (leer)
    ("K3", 11, 2, 1, 2, True),
    ("M1", 9, 0, 5, 2, False),     # Haendler I
    ("K4", 11, 10, 1, 2, True),
    ("M2", 9, 12, 5, 3, False),    # Haendler II
    ("K5", 3, 9, 1, 2, True),
    ("AL", 1, 11, 5, 4, False),    # Altar
    ("K2", 14, 6, 3, 1, True),
    ("B", 17, 3, 4, 7, False),     # Kampfraum vor dem Boss-Tor (versiegelt)
]
GRID = (1, 0, 21, 15)  # x0, y0, x1, y1 der Karte in Zellen
SEALED_ROOM = "B"

TELS = [
    dict(id=0, num=1, name="Start Hall", state="here", pos=(3.5, 6.5), status="You are here", lab="b"),
    dict(id=1, num=2, name="Merchant I", state="ready", pos=(11.5, 1.0), status="Ready - 3 rooms away", lab="r"),
    dict(id=2, num=3, name="Merchant II", state="locked", pos=(11.5, 13.5), status="Locked - step onto it once", lab="r"),
    dict(id=3, num=4, name="Altar", state="ready", pos=(3.5, 13.0), status="Ready - 2 rooms away", lab="r"),
    dict(id=4, num=5, name="Boss Gate", state="sealed", pos=(19.0, 6.5), status="Sealed - fight in progress", lab="l"),
]
SEL = 1  # gewaehlt: Merchant I
LEGEND = [("here", "You are here"), ("ready", "Ready"), ("sel", "Chosen"), ("locked", "Locked"), ("sealed", "Sealed")]


def U(P, n):
    return int(round(n * P["u"]))


# ------------------------------------------------------------------ Hintergrund und Platzhalter-HUD
def make_bg(W, H, u):
    bh, bw = int(14 * u), int(28 * u)
    lines = int(2 * u) if u > 1 else 1
    base = []
    for _ in range(1):
        pass
    c = Cv(W, H)
    vx = []
    for x in range(W):
        t = abs(x - W / 2) / (W / 2)
        vx.append(1.0 - 0.55 * t ** 2)
    vy = []
    for y in range(H):
        t = y / H
        f = 0.55 + 0.6 * (1 - abs(t - 0.45) * 1.6) if True else 1
        vy.append(max(0.25, min(1.0, f)))
    stone = [STONE[3], STONE[4], STONE[5], STONE[6]]
    b = c.b
    for y in range(H):
        row = y // bh
        off = (bw // 2) if row % 2 else 0
        fy = vy[y]
        floor = y > H * 0.62
        ceil = y < H * 0.07
        for x in range(W):
            xo = x + off
            col_i = xo // bw
            if (y % bh) < lines or (xo % bw) < lines:
                base_c = STONE[0] if not floor else (22, 16, 26)
                m = 1.0
            else:
                n = (row * 31 + col_i * 17 + (row * col_i) % 5) % 4
                base_c = stone[n]
                if floor:
                    base_c = (base_c[0] * 0.8, base_c[1] * 0.8, base_c[2] * 0.85)
                if ceil:
                    base_c = (base_c[0] * 0.45, base_c[1] * 0.45, base_c[2] * 0.5)
                m = 1.0
            f = vx[x] * fy * 0.55
            t = BAYER[y & 3][x & 3] * 0.5
            i = (y * W + x) * 3
            b[i] = min(248, int(base_c[0] * f + t) // 8 * 8)
            b[i + 1] = min(248, int(base_c[1] * f + t) // 8 * 8)
            b[i + 2] = min(248, int(base_c[2] * f + t) // 8 * 8)
    return c


def hud_box(c, x0, y0, x1, y1, fw, r):
    frame(c, x0, y0, x1, y1, fw, ch=2, fill=(GROUND0, GROUND0), alpha=1.0, grain=False)
    rivets(c, x0, y0, x1, y1, r, 1)


def placeholder_hud(c, u):
    W, H = c.w, c.h
    fw = 2 if u <= 1 else 3
    gap = int(6 * u)
    s30 = int(30 * u)
    yb = H - gap - s30
    mid = int(360 * u)
    hud_box(c, W // 2 - mid // 2, yb, W // 2 + mid // 2, yb + s30, fw, 1)
    pad = int(8 * u)
    c.rect(W // 2 - mid // 2 + pad, yb + pad, W // 2 + int(mid * 0.18), yb + int(17 * u), BLOOD)
    c.rect(W // 2 - mid // 2 + pad, yb + pad, W // 2 + int(mid * 0.18), yb + pad + max(1, int(u)), (200, 30, 40))
    seg = (mid - 2 * pad) // 5
    for i in range(5):
        c.rect(W // 2 - mid // 2 + pad + i * seg, yb + int(21 * u), W // 2 - mid // 2 + pad + (i + 1) * seg - 2, yb + int(25 * u), NL[4])
    for i in range(3):
        x = gap + i * (s30 + gap)
        hud_box(c, x, yb, x + s30, yb + s30, fw, 1)
    s38 = int(38 * u)
    for i in range(4):
        x = W - gap - (4 - i) * s38 - (3 - i) * gap
        hud_box(c, x, yb - int(8 * u), x + s38, yb + s30, fw, 1)
    hud_box(c, gap, gap, gap + s30, gap + s30, fw, 1)
    hud_box(c, gap + s30 + gap, gap + int(5 * u), gap + s30 + gap + int(50 * u), gap + int(23 * u), fw, 1)
    hud_box(c, W - gap - int(100 * u), gap, W - gap, gap + s30, fw, 1)


# ------------------------------------------------------------------ Karte
def cell_px(P, ox, oy, x, y):
    return ox + int(round((x - GRID[0]) * P["cell"])), oy + int(round((y - GRID[1]) * P["cell"]))


def draw_map(c, P, ox, oy, selected, hover=None, labels="names", route=True, numbers=False):
    """Zeichnet die Raumkarte mit (ox, oy) als oberer linker Ecke der Zellen. Gibt die Pixelmitte jeder Marke zurueck."""
    cell = P["cell"]
    ol = max(1, P["line"])
    rooms = {}
    for name, x, y, w, d, corr in ROOMS:
        x0, y0 = cell_px(P, ox, oy, x, y)
        x1, y1 = cell_px(P, ox, oy, x + w, y + d)
        rooms[name] = (x0, y0, x1, y1, corr)
    for name, (x0, y0, x1, y1, corr) in rooms.items():
        c.rect(x0, y0, x1, y1, STONE[7] if not corr else STONE[5])
        c.rect(x0 + ol, y0 + ol, x1 - ol, y1 - ol, STONE[3] if not corr else STONE[2])
    # Tueren: Lücke in den Umrissen dort, wo ein Gang an einen Raum stoesst
    for gn, (gx0, gy0, gx1, gy1, gcorr) in rooms.items():
        if not gcorr:
            continue
        for rn, (rx0, ry0, rx1, ry1, rcorr) in rooms.items():
            if rcorr:
                continue
            if gx1 == rx0 or gx0 == rx1:   # Gang neben dem Raum (links/rechts)
                lo, hi = max(gy0, ry0), min(gy1, ry1)
                if hi > lo:
                    x = rx0 if gx1 == rx0 else rx1
                    c.rect(x - ol, lo + ol, x + ol, hi - ol, STONE[2])
            elif gy1 == ry0 or gy0 == ry1:  # Gang ueber/unter dem Raum
                lo, hi = max(gx0, rx0), min(gx1, rx1)
                if hi > lo:
                    y = ry0 if gy1 == ry0 else ry1
                    c.rect(lo + ol, y - ol, hi - ol, y + ol, STONE[2])
    # versiegelter Raum: Schraffur
    x0, y0, x1, y1, _ = rooms[SEALED_ROOM]
    hp = max(5, int(6 * P["u"]))
    for y in range(y0 + ol, y1 - ol):
        for x in range(x0 + ol, x1 - ol):
            if (x + y) % hp < max(1, ol):
                c.set(x, y, NL[0])
    # Weg zum gewaehlten Ziel (gestrichelt)
    pos = {}
    for t in TELS:
        px, py = cell_px(P, ox, oy, *t["pos"])
        pos[t["id"]] = (px, py)
    if route and selected is not None:
        here = [t for t in TELS if t["state"] == "here"][0]
        (ax, ay), (bx, by) = pos[here["id"]], pos[selected]
        n = max(abs(bx - ax), abs(by - ay))
        step = max(6, int(8 * P["u"]))
        for i in range(0, n, step):
            if (i // step) % 2 == 0:
                for j in range(max(1, ol)):
                    x = ax + (bx - ax) * i // n
                    y = ay + (by - ay) * i // n
                    c.rect(x, y, x + max(1, ol + (1 if P["u"] > 1 else 0)), y + max(1, ol + (1 if P["u"] > 1 else 0)), GOLD_D)
    for t in TELS:
        draw_mark(c, P, pos[t["id"]][0], pos[t["id"]][1], t, selected == t["id"], hover == t["id"], numbers)
    for t in TELS:
        if labels == "names":
            map_label(c, P, pos[t["id"]], t, selected == t["id"])
    return pos


def draw_mark(c, P, x, y, t, sel, hov, numbers=False):
    r = P["mark"]
    st = t["state"]
    if st == "locked":
        diamond(c, x, y, r, GROUND0, edge=NL[2])
        c.rect(x - 1, y - 1, x + 1 + (P["u"] > 1), y + 1 + (P["u"] > 1), NL[2])
    elif st == "sealed":
        diamond(c, x, y, r, NL[1], edge=INK)
        bw = max(1, int(P["u"]))
        for dx in (-r // 3 - bw, r // 3):
            c.rect(x + dx, y - r // 2, x + dx + bw + (1 if P["u"] > 1 else 0), y + r // 2 + 1, NL[4])
    else:  # here, ready
        diamond(c, x, y, r, NL[4], lit=NL[6], dark=NL[2])
    if st == "here":
        k = max(1, int(P["u"]))
        ty = y - r - 3 * k
        for j in range(5 * k):
            w = j // 2 if False else (j * 3) // (5 * k) + 0
            for i in range(-(j // 2) - 1, (j // 2) + 2):
                c.set(x + i, ty - 5 * k + j + 2, BONE)
        # dunkler Rand
        for j in range(5 * k):
            xo = j // 2 + 2
            c.set(x - xo, ty - 5 * k + j + 2, INK)
            c.set(x + xo, ty - 5 * k + j + 2, INK)
        c.rect(x - 4, ty + 1, x + 5, ty + 2, INK)
    if sel:
        diamond(c, x, y, r + 2, GOLD, edge=INK, lit=GOLD_L, dark=GOLD_D)
        brackets(c, x, y, r + 7 + (P["u"] > 1) * 2, 4 + int(2 * P["u"]), GOLD, w=max(1, int(P["u"])))
    elif hov:
        brackets(c, x, y, r + 5 + (P["u"] > 1) * 2, 3 + int(2 * P["u"]), BONE, w=max(1, int(P["u"])))
    if numbers:
        k = P["ts"]
        bw = text_w(str(t["num"]), k) + 4 * k
        bx, by = x + r + 3 * k, y - r - 8 * k
        frame(c, bx, by, bx + bw, by + 7 * k + 4 * k, 1, ch=1, fill=(GROUND1, GROUND0), alpha=1.0, grain=False)
        text(c, str(t["num"]), bx + 2 * k, by + 2 * k, k, GOLD if sel else BONE, shadow=False)


def map_label(c, P, p, t, sel):
    k = P["ts"]
    x, y = p
    r = P["mark"]
    col = GOLD if sel else (DIMTXT if t["state"] == "locked" else BONE)
    s = t["name"]
    w = text_w(s, k)
    if t["lab"] == "r":
        text(c, s, x + r + 6 * k, y - 3 * k, k, col)
    elif t["lab"] == "b":
        text(c, s, x, y + r + 5 * k, k, col, anchor="m")
    else:
        text(c, s, x - r - 6 * k, y - 3 * k, k, col, anchor="r")


def draw_legend(c, P, x, y, pitch, only=None):
    k = P["ts"]
    fake = lambda st: dict(state=st, lab="r", num=0, name="")
    for i, (key, label) in enumerate(LEGEND):
        cy = y + i * pitch + 4 * k
        t = fake({"sel": "ready"}.get(key, key))
        draw_mark(c, dict(P, mark=max(3, P["mark"] - 1)), x + 12 * k, cy, t, key == "sel", False)
        text(c, label, x + 12 * k + P["mark"] + 12 * k, cy - 3 * k, k, GOLD if key == "sel" else BONE2)


# ------------------------------------------------------------------ Parameter je Fenstergroesse
def params(W):
    if W == 854:
        return dict(W=854, H=480, u=1.0, ts=1, tn=2, tt=3, fw=2, rv=3, line=1, cell=18, mark=5, cur=1)
    return dict(W=1280, H=720, u=1.5, ts=2, tn=3, tt=4, fw=3, rv=4, line=2, cell=27, mark=8, cur=2)


def panel(c, P, x0, y0, x1, y1, title=None, gab=True):
    frame(c, x0, y0, x1, y1, P["fw"], ch=U(P, 4), fill=(GROUND1, GROUND0))
    rivets(c, x0, y0, x1, y1, P["rv"] - 1 if P["fw"] == 2 else P["rv"] - 1, P["fw"])
    if gab:
        gable(c, (x0 + x1) // 2, y0 + 1, U(P, 15), U(P, 18))
    if title:
        text(c, title, x0 + U(P, 16), y0 + U(P, 12), P["tt"], BONE)
        rule(c, x0 + U(P, 14), x1 - U(P, 14), y0 + U(P, 12) + 7 * P["tt"] + U(P, 6))


# ------------------------------------------------------------------ Entwurf A: Karte der Ebene
def design_a(base, P):
    c = base.copy()
    W, H = P["W"], P["H"]
    px0, py0 = U(P, 97), U(P, 44)
    px1, py1 = W - px0, H - U(P, 44)
    panel(c, P, px0, py0, px1, py1, "Teleporters")
    k = P["ts"]
    text(c, "4 of 5 found", px1 - U(P, 16), py0 + U(P, 12) + 7 * P["tt"] - 7 * k, k, BONE2, anchor="r")
    cell = P["cell"]
    pad = U(P, 6)
    mx0 = px0 + U(P, 16)
    my0 = py0 + U(P, 12) + 7 * P["tt"] + U(P, 14)
    mw, mh = (GRID[2] - GRID[0]) * cell, (GRID[3] - GRID[1]) * cell
    well(c, mx0, my0, mx0 + mw + 2 * pad, my0 + mh + 2 * pad, 1)
    pos = draw_map(c, P, mx0 + pad, my0 + pad, SEL, hover=4)
    # rechte Spalte: Karte des Gewaehlten, Zeichenerklaerung
    rx0 = mx0 + mw + 2 * pad + U(P, 14)
    rx1 = px1 - U(P, 16)
    sel = TELS[SEL]
    ch = U(P, 100)
    well(c, rx0, my0, rx1, my0 + ch, 1)
    text(c, "Chosen", rx0 + U(P, 10), my0 + U(P, 9), k, BONE2)
    text(c, sel["name"], rx0 + U(P, 10), my0 + U(P, 9) + 7 * k + U(P, 6), P["tn"], GOLD)
    text(c, sel["status"], rx0 + U(P, 10), my0 + U(P, 9) + 7 * k + U(P, 6) + 7 * P["tn"] + U(P, 6), k, BONE)
    # Knopf
    by0 = my0 + ch - U(P, 10) - 7 * P["tn"] - U(P, 10)
    bx0, bx1 = rx0 + U(P, 10), rx1 - U(P, 10)
    button(c, P, bx0, by0, bx1, my0 + ch - U(P, 8), "Travel", "Enter")
    ly = my0 + ch + U(P, 12)
    text(c, "Legend", rx0, ly, k, BONE2)
    rule(c, rx0, rx1, ly + 7 * k + U(P, 3))
    draw_legend(c, P, rx0, ly + 7 * k + U(P, 10), U(P, 19))
    # Bedienung
    hy = py1 - U(P, 12) - 2 * (7 * k + 4 * k) - U(P, 5)
    text(c, "Keys", px0 + U(P, 16), hy + 2 * k, k, GOLD)
    kx = px0 + U(P, 16) + text_w("Mouse", k) + U(P, 12)
    hints(c, P, kx, hy, [("k", "Arrows"), ("t", "Choose"), ("k", "Enter"), ("t", "Travel"), ("k", "Esc"), ("t", "Close")])
    hy2 = hy + 7 * k + 4 * k + U(P, 5)
    text(c, "Mouse", px0 + U(P, 16), hy2 + 2 * k, k, GOLD)
    hints(c, P, kx, hy2, [("k", "Click"), ("t", "Choose"), ("k", "Double-click"), ("t", "Travel"), ("t", "Hover: details")])
    # Zeiger und Hinweis ueber dem versiegelten Teleporter
    bx, by = pos[4]
    tip = "Sealed: wait for the fight to end"
    tw = text_w(tip, k) + 8 * k
    tx = min(bx - tw // 2, mx0 + mw + 2 * pad - tw - U(P, 4))
    tooltip(c, P, tx, by - P["mark"] - 7 * k - 6 * k - U(P, 34), tip, BONE)
    cursor(c, bx + 2, by + 2, P["cur"])
    return c


def button(c, P, x0, y0, x1, y1, label, key):
    """Knopf: Silberfassung mit goldener Schrift und Tastenschild (Ansicht wie die Auswahl im Menue)."""
    frame(c, x0, y0, x1, y1, max(2, P["fw"] - 1), ch=U(P, 3), fill=(GROUND1, GROUND0), alpha=1.0, grain=False)
    k = P["tn"]
    kc = P["ts"]
    capw = text_w(key, kc) + 6 * kc
    total = text_w(label, k) + U(P, 8) + capw
    x = (x0 + x1) // 2 - total // 2
    mid = (y0 + y1) // 2
    text(c, label, x, mid - (7 * k) // 2, k, GOLD)
    keycap(c, P, x + text_w(label, k) + U(P, 8), mid - (7 * kc + 4 * kc) // 2, key)


# ------------------------------------------------------------------ Zeilen fuer B und C
def row(c, P, x0, y0, x1, y1, t, sel, hov, show_num=False):
    k, kn = P["ts"], P["tn"]
    st = t["state"]
    if sel:
        cursor_frame(c, P, x0, y0, x1, y1)
    elif hov:
        c.rect(x0 + 2, y0 + 2, x1 - 2, y1 - 2, GROUND1)
        c.rect(x0 + 2, y0 + 2, x1 - 2, y0 + 3, NL[1])
        c.rect(x0 + 2, y1 - 3, x1 - 2, y1 - 2, NL[1])
    col = GOLD if sel else (DIMTXT if st == "locked" else BONE)
    mid = (y0 + y1) // 2
    ix = x0 + U(P, 28)
    if show_num:
        nw = text_w(str(t["num"]), kn) + 6 * kn
        nx = x0 + U(P, 16)
        frame(c, nx, mid - (7 * kn + 6 * kn) // 2, nx + nw, mid + (7 * kn + 6 * kn) // 2 + 1, 1, ch=1, fill=(GROUND1, GROUND0), alpha=1.0, grain=False)
        text(c, str(t["num"]), nx + 3 * kn, mid - (7 * kn) // 2, kn, col, shadow=False)
        ix = nx + nw + U(P, 14)
    draw_mark(c, P, ix, mid, dict(t, state=st), sel, False)
    tx = ix + P["mark"] + U(P, 16)
    text(c, t["name"], tx, y0 + U(P, 7), kn, col)
    text(c, t["status"], tx, y0 + U(P, 7) + 7 * kn + U(P, 5), k, GOLD_L if sel else (DIMTXT if st == "locked" else BONE2))


def cursor_frame(c, P, x0, y0, x1, y1):
    """Spitze Silberfassung als Auswahl (wie im Menue), Grund dunkel, goldene Schrift darin."""
    h = y1 - y0
    cy = (y0 + y1 - 1) / 2
    pt = int(h * 0.30)
    fw = P["fw"]

    def inside(x, y):
        if not (y0 <= y < y1):
            return False
        ins = pt * abs(y - cy) / (h / 2)
        return x0 + ins <= x < x1 - ins

    lt, dk = levels(fw)
    for y in range(y0, y1):
        for x in range(x0, x1):
            if not inside(x, y):
                continue
            edge = any(not inside(x + dx, y + dy) for dx in range(-fw, fw + 1) for dy in range(-fw, fw + 1) if abs(dx) + abs(dy) <= fw)
            if edge:
                near_out = not inside(x - 1, y) or not inside(x + 1, y) or not inside(x, y - 1) or not inside(x, y + 1)
                upper = y < cy
                c.set(x, y, NL[4] if (upper and near_out) else NL[3] if upper else NL[0] if near_out else NL[2])
            else:
                c.set(x, y, GROUND0 if (bay(x, y) / 16) > 0.35 else GROUND1)


# ------------------------------------------------------------------ Entwurf B: Liste mit Ortsnamen
def design_b(base, P):
    c = base.copy()
    W, H = P["W"], P["H"]
    k, kn = P["ts"], P["tn"]
    pw = U(P, 372)
    px0, py0 = (W - pw) // 2, U(P, 44)
    px1, py1 = px0 + pw, H - U(P, 44)
    panel(c, P, px0, py0, px1, py1, None)
    text(c, "Teleporters", (px0 + px1) // 2, py0 + U(P, 14), P["tt"], BONE, anchor="m")
    rule(c, px0 + U(P, 16), px1 - U(P, 16), py0 + U(P, 14) + 7 * P["tt"] + U(P, 6))
    ry = py0 + U(P, 14) + 7 * P["tt"] + U(P, 14)
    rh = U(P, 48)
    gap = U(P, 3)
    for i, t in enumerate(TELS):
        row(c, P, px0 + U(P, 14), ry + i * (rh + gap), px1 - U(P, 14), ry + i * (rh + gap) + rh, t, t["id"] == SEL, t["id"] == 3)
    # Auswahlhilfe unten
    hy = ry + 5 * (rh + gap) + U(P, 6)
    rule(c, px0 + U(P, 16), px1 - U(P, 16), hy)
    kk = hy + U(P, 10)
    items1 = [("k", "Up"), ("k", "Down"), ("t", "Choose"), ("k", "Enter"), ("t", "Travel")]
    items2 = [("k", "Click"), ("t", "Choose"), ("k", "Double-click"), ("t", "Travel"), ("k", "Esc"), ("t", "Close")]
    hints(c, P, px0 + U(P, 16), kk, items1)
    hints(c, P, px0 + U(P, 16), kk + 7 * k + 4 * k + U(P, 5), items2)
    # Zeiger ueber der dritten Zeile (Altar, Hover)
    ay = ry + 3 * (rh + gap) + rh // 2
    cursor(c, px0 + pw // 2 + U(P, 40), ay + U(P, 2), P["cur"])
    return c


# ------------------------------------------------------------------ Entwurf C: Karte plus Liste
def design_c(base, P):
    c = base.copy()
    W, H = P["W"], P["H"]
    k, kn = P["ts"], P["tn"]
    px0, py0 = U(P, 36), U(P, 44)
    px1, py1 = W - px0, H - U(P, 44)
    panel(c, P, px0, py0, px1, py1, "Teleporters")
    text(c, "4 of 5 found", px1 - U(P, 16), py0 + U(P, 12) + 7 * P["tt"] - 7 * k, k, BONE2, anchor="r")
    top = py0 + U(P, 12) + 7 * P["tt"] + U(P, 14)
    lw = U(P, 262)
    lx0 = px0 + U(P, 16)
    rh = U(P, 46)
    gap = U(P, 3)
    well(c, lx0, top, lx0 + lw, top + 5 * (rh + gap) + U(P, 8), 1)
    for i, t in enumerate(TELS):
        row(c, P, lx0 + U(P, 4), top + U(P, 4) + i * (rh + gap), lx0 + lw - U(P, 4), top + U(P, 4) + i * (rh + gap) + rh, t, t["id"] == SEL, t["id"] == 2, show_num=True)
    # Karte rechts
    P2 = dict(P, cell=int(P["cell"] * 0.92) if P["u"] == 1 else P["cell"])
    cell = P2["cell"]
    pad = U(P, 6)
    mx0 = lx0 + lw + U(P, 16)
    mw, mh = (GRID[2] - GRID[0]) * cell, (GRID[3] - GRID[1]) * cell
    mw_box = px1 - U(P, 16) - mx0
    ox = mx0 + (mw_box - mw) // 2 - pad // 2
    well(c, mx0, top, px1 - U(P, 16), top + mh + 2 * pad, 1)
    pos = draw_map(c, P2, ox + pad // 2, top + pad, SEL, hover=2, labels="none", numbers=True)
    # Legende unter der Karte (eine Zeile)
    ly = top + mh + 2 * pad + U(P, 10)
    x = mx0
    for key, label in LEGEND:
        t = dict(state={"sel": "ready"}.get(key, key), lab="r", num=0, name="")
        draw_mark(c, dict(P, mark=max(3, P["mark"] - 1)), x + 6 * k, ly + 4 * k, t, key == "sel", False)
        w = text(c, label, x + 6 * k + P["mark"] + 8 * k, ly + k, k, GOLD if key == "sel" else BONE2)
        x += w + P["mark"] + 24 * k
    # Hinweis am Zeiger: Merchant II ist gesperrt
    bx, by = pos[2]
    tip = "Locked: step onto it once"
    tw = text_w(tip, k) + 8 * k
    tooltip(c, P, min(bx - tw // 2, px1 - U(P, 20) - tw), by - P["mark"] - 7 * k - 6 * k - U(P, 22), tip, DIMTXT if False else BONE)
    cursor(c, bx + 2, by + 2, P["cur"])
    # Bedienung unten
    hy = py1 - U(P, 12) - 7 * k - 4 * k
    hints(c, P, px0 + U(P, 16), hy, [("k", "Arrows"), ("t", "or"), ("k", "1-5"), ("t", "Choose"), ("k", "Enter"), ("t", "Travel"),
                                   ("k", "Click"), ("t", "Choose"), ("k", "Double-click"), ("t", "Travel"), ("k", "Esc"), ("t", "Close")], gap=U(P, 4) if k == 1 else 8)
    return c


# ------------------------------------------------------------------ Blatt
DESIGNS = [
    ("A", "karte", "A  Map", design_a,
     ["The level as a room map. Marks sit where the teleporters stand.",
      "Best sense of place; small list-less reading, names on the map.",
      "Large panel in the centre of the view."]),
    ("B", "liste", "B  List", design_b,
     ["A narrow list in the style of the pause menu, one line per place.",
      "Calm and quick, the game stays visible. No sense of where.",
      "Scales to many entries by scrolling, keyboard first."]),
    ("C", "karte-liste", "C  Map and list", design_c,
     ["List with numbers on the left, the same numbers on the map.",
      "Both readings at once, keys 1 to 5 as a shortcut.",
      "Widest panel, most to read; tight at 854."]),
]


def measure(base, c):
    n = 0
    for i in range(0, len(base.b), 3):
        if base.b[i:i + 3] != c.b[i:i + 3]:
            n += 1
    return 100.0 * n / (base.w * base.h)


def main():
    outs = {}
    report = []
    for W in (1280, 854):
        P = params(W)
        bg = make_bg(P["W"], P["H"], P["u"])
        placeholder_hud(bg, P["u"])
        for key, slug, title, fn, _ in DESIGNS:
            img = fn(bg, P)
            name = "%s-%s-%d.png" % (key, slug, W)
            img.save(os.path.join(HERE, name))
            outs[(key, W)] = img
            report.append("%s %d x %d: %.1f %% des Bildes verdeckt (Unterschied zum Hintergrund mit Platzhalter-HUD)" % (key, P["W"], P["H"], measure(bg, img)))
    with open(os.path.join(HERE, "verdeckt.txt"), "w", newline="\n") as f:
        f.write("\n".join(report) + "\n")
    # Blatt: je Spalte Titel, 1280er Bild, darunter 854er Bild und rechts daneben die Kurzbeschreibung
    P1, P2 = params(1280), params(854)
    gutter = 24
    colw = 1280
    top = 96
    sheet = Cv(gutter + 3 * (colw + gutter), top + 720 + gutter + 480 + gutter + 30, (14, 12, 20))
    text(sheet, "Teleporter destination choice - three drafts", gutter, 14, 3, BONE)
    text(sheet, "Chosen: Merchant I. Mouse over: A Boss Gate, B Altar, C Merchant II. Shown at 1280 x 720 (top) and 854 x 480 (bottom), pixels 1:1.", gutter, 14 + 7 * 3 + 10, 1, BONE2)
    for i, (key, slug, title, fn, notes) in enumerate(DESIGNS):
        x = gutter + i * (colw + gutter)
        text(sheet, title, x, top - 26, 3, GOLD)
        sheet.blit(outs[(key, 1280)], x, top)
        y2 = top + 720 + gutter
        sheet.blit(outs[(key, 854)], x, y2)
        ty = y2 + 8
        for line in notes:
            text(sheet, line, x + 854 + 14, ty, 1, BONE if line is notes[0] else BONE2)
            ty += 7 + 10
        text(sheet, report[i].split(": ")[1].split(" (")[0].replace("des Bildes verdeckt", "of the 1280 view covered"), x + 854 + 14, ty + 8, 1, GOLD_L)
        text(sheet, report[3 + i].split(": ")[1].split(" (")[0].replace("des Bildes verdeckt", "of the 854 view covered"), x + 854 + 14, ty + 8 + 17, 1, GOLD_L)
    sheet.save(os.path.join(HERE, "blatt.png"))


if __name__ == "__main__":
    main()
