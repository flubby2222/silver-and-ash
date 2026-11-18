"""HUD-Vorlagen (Issue #341): drei Designs x drei Aufloesungen = neun Blaetter.

Aufruf:
  "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python hud_blaetter.py [-- <ausgabeordner>]

Blender dient nur als Python mit numpy und Schrift-Rasterer (blf); es wird nichts gerendert und nichts heruntergeladen.
Grundlage ist basis-854x480.png (Foto-Skript, Szene room-corridor, ohne HUD).
Alle Formen sind Vektoren in "Einheiten" des 854x480-Bildes; die drei Aufloesungen zeichnen dieselben Koordinaten:
  A  854x480 im Spielbild, harte Kanten, 15 Bit mit 4x4-Bayer (wie das Spielbild, ADR 0010)
  B  HUD-Ebene in 1708x960 (doppelt), weich, ueber dem hart hochskalierten Spielbild
  C  HUD-Ebene in 1920x1080 (Fenster), weich, ueber dem hart hochskalierten Spielbild
Alle Blaetter haben die Endgroesse 1920x1080.
"""
import bpy, blf, imbuf, numpy as np, math, os, sys, tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else HERE
os.makedirs(OUT, exist_ok=True)
FINAL_W, FINAL_H = 1920, 1080
UX, UY = 854.0, 480.0

# ---------------------------------------------------------------- Bild-Ein/Ausgabe


def load_png(path):
    im = bpy.data.images.load(path)
    im.colorspace_settings.name = "Non-Color"
    w, h = im.size
    a = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]
    return a[..., :3].copy()


def save_png(path, rgb):
    h, w, _ = rgb.shape
    a = np.ones((h, w, 4), np.float32)
    a[..., :3] = np.clip(rgb, 0, 1)
    im = bpy.data.images.new(os.path.basename(path), w, h, alpha=False)
    im.colorspace_settings.name = "Non-Color"
    im.pixels.foreach_set(a[::-1].ravel())
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)


def nearest(a, H, W):
    h, w = a.shape[:2]
    iy = np.minimum(((np.arange(H) + 0.5) * h / H).astype(int), h - 1)
    ix = np.minimum(((np.arange(W) + 0.5) * w / W).astype(int), w - 1)
    return a[iy][:, ix]


def bilinear(a, H, W):
    h, w = a.shape[:2]
    y = (np.arange(H) + 0.5) * h / H - 0.5
    x = (np.arange(W) + 0.5) * w / W - 0.5
    y0 = np.clip(np.floor(y).astype(int), 0, h - 1); y1 = np.minimum(y0 + 1, h - 1)
    x0 = np.clip(np.floor(x).astype(int), 0, w - 1); x1 = np.minimum(x0 + 1, w - 1)
    fy = np.clip(y - y0, 0, 1)[:, None, None] if a.ndim == 3 else np.clip(y - y0, 0, 1)[:, None]
    fx = np.clip(x - x0, 0, 1)[None, :, None] if a.ndim == 3 else np.clip(x - x0, 0, 1)[None, :]
    top = a[y0][:, x0] * (1 - fx) + a[y0][:, x1] * fx
    bot = a[y1][:, x0] * (1 - fx) + a[y1][:, x1] * fx
    return top * (1 - fy) + bot * fy


BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], np.float32)

# ---------------------------------------------------------------- Schrift (blf -> Maske)

FONTS = {}
_mask_cache = {}


def font(path):
    if path not in FONTS:
        FONTS[path] = blf.load(path)
    return FONTS[path]


def text_mask(fpath, size_px, s):
    key = (fpath, round(size_px, 2), s)
    if key in _mask_cache:
        return _mask_cache[key]
    f = font(fpath)
    blf.size(f, size_px)
    w, h = blf.dimensions(f, s)
    pad = int(math.ceil(size_px * 0.35)) + 2
    dsc = int(math.ceil(size_px * 0.4))
    W = int(math.ceil(w)) + 2 * pad
    H = int(math.ceil(size_px * 1.5)) + 2 * pad
    ib = imbuf.new((W, H))
    blf.color(f, 1, 1, 1, 1)
    blf.position(f, pad, pad + dsc, 0)
    with blf.bind_imbuf(f, ib):
        blf.draw_buffer(f, s)
    tmp = os.path.join(tempfile.gettempdir(), "hud_txt.png")
    ib.filepath = tmp
    imbuf.write(ib)
    im = bpy.data.images.load(tmp)
    im.colorspace_settings.name = "Non-Color"
    a = np.array(im.pixels[:], np.float32).reshape(H, W, 4)[::-1, :, 3].copy()
    bpy.data.images.remove(im)
    res = (a, H - (pad + dsc), pad, w)  # Maske, Grundlinie (Zeile), Rand links, Breite
    _mask_cache[key] = res
    return res


# ---------------------------------------------------------------- Ebene mit Formen


class Layer:
    def __init__(self, W, H, hard):
        self.W, self.H, self.hard = W, H, hard
        self.sx, self.sy = W / UX, H / UY
        self.s = (self.sx + self.sy) / 2
        self.hi = self.s >= 1.9
        self.rgb = np.zeros((H, W, 3), np.float32)  # vormultipliziert
        self.a = np.zeros((H, W), np.float32)

    @property
    def hair(self):
        return 1.0 if self.hard else 0.6

    def _grid(self, bbox, pad=2):
        x0, y0, x1, y1 = bbox
        i0 = max(int(math.floor(x0 * self.sx)) - pad, 0); i1 = min(int(math.ceil(x1 * self.sx)) + pad, self.W)
        j0 = max(int(math.floor(y0 * self.sy)) - pad, 0); j1 = min(int(math.ceil(y1 * self.sy)) + pad, self.H)
        if i1 <= i0 or j1 <= j0:
            return None
        off = 1e-3 if self.hard else 0.0
        px = ((np.arange(i0, i1) + 0.5) / self.sx + off)[None, :]
        py = ((np.arange(j0, j1) + 0.5) / self.sy + off)[:, None]
        shp = (j1 - j0, i1 - i0)
        return i0, i1, j0, j1, np.broadcast_to(px, shp), np.broadcast_to(py, shp)

    def _blend(self, j0, j1, i0, i1, cov, col, alpha):
        sa = (cov * alpha).astype(np.float32)
        d = self.a[j0:j1, i0:i1]
        self.rgb[j0:j1, i0:i1] = col * sa[..., None] + self.rgb[j0:j1, i0:i1] * (1 - sa[..., None])
        self.a[j0:j1, i0:i1] = sa + d * (1 - sa)

    def shape(self, bbox, f, color, alpha=1.0, stroke=None, clipf=None):
        g = self._grid(bbox)
        if g is None:
            return
        i0, i1, j0, j1, px, py = g
        d = f(px, py)
        if stroke is not None:
            d = np.abs(d) - max(stroke, 1 / self.s if self.hard else 0.0) / 2
        if clipf is not None:
            d = np.maximum(d, clipf(px, py))
        if self.hard:
            cov = (d * self.s < 0).astype(np.float32)
        else:
            cov = np.clip(0.5 - d * self.s, 0, 1)
        col = color(px, py) if callable(color) else np.array(color, np.float32) / 255.0
        if not callable(color):
            col = np.broadcast_to(col, px.shape + (3,))
        self._blend(j0, j1, i0, i1, cov, col, alpha)

    def text(self, fpath, size, s, x, y, color, anchor="l", shadow=None, alpha=1.0):
        """Schrift; y ist die Grundlinie in Einheiten, x je nach anchor links/Mitte/rechts."""
        px_size = size * self.s
        mask, bl, pad, w = text_mask(fpath, px_size, s)
        wu = w / self.sx
        if anchor == "c":
            x = x - wu / 2
        elif anchor == "r":
            x = x - wu
        left = int(round(x * self.sx)) - pad
        top = int(round(y * self.sy)) - bl
        m = mask
        if self.hard:
            m = (m >= 0.5).astype(np.float32)
        H, W = m.shape
        for (dx, dy, c, aa) in ([shadow + (0.9,)] if shadow and len(shadow) == 3 else []) + [(0, 0, color, 1.0)]:
            ox = left + int(round(dx * self.sx)); oy = top + int(round(dy * self.sy))
            i0, j0 = max(ox, 0), max(oy, 0)
            i1, j1 = min(ox + W, self.W), min(oy + H, self.H)
            if i1 <= i0 or j1 <= j0:
                continue
            cov = m[j0 - oy:j1 - oy, i0 - ox:i1 - ox]
            col = np.broadcast_to(np.array(c, np.float32) / 255.0, cov.shape + (3,))
            self._blend(j0, j1, i0, i1, cov, col, alpha * aa)
        return wu


def textw(layer, fpath, size, s):
    return text_mask(fpath, size * layer.s, s)[3] / layer.sx


def vg(y0, y1, c0, c1):
    c0 = np.array(c0, np.float32) / 255; c1 = np.array(c1, np.float32) / 255

    def f(px, py):
        t = np.clip((py - y0) / (y1 - y0), 0, 1)[..., None]
        return c0 * (1 - t) + c1 * t
    return f


# ---------------------------------------------------------------- Abstandsfunktionen (negativ = innen)


def sd_box(x0, y0, x1, y1, r=0.0):
    cx, cy, hx, hy = (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2, (y1 - y0) / 2

    def f(px, py):
        qx = np.abs(px - cx) - hx + r; qy = np.abs(py - cy) - hy + r
        return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r
    return f


def sd_circle(cx, cy, r):
    return lambda px, py: np.hypot(px - cx, py - cy) - r


def sd_ellipse(cx, cy, rx, ry):
    return lambda px, py: (np.hypot((px - cx) / rx, (py - cy) / ry) - 1) * min(rx, ry)


def sd_seg(a, b, w):
    ax, ay = a; bx, by = b
    ex, ey = bx - ax, by - ay
    ll = ex * ex + ey * ey

    def f(px, py):
        t = np.clip(((px - ax) * ex + (py - ay) * ey) / ll, 0, 1)
        return np.hypot(px - ax - ex * t, py - ay - ey * t) - w / 2
    return f


def sd_poly(pts):
    P = [tuple(map(float, p)) for p in pts]
    n = len(P)

    def f(px, py):
        d = np.full(px.shape, 1e9, np.float32)
        inside = np.zeros(px.shape, bool)
        for i in range(n):
            ax, ay = P[i]; bx, by = P[(i + 1) % n]
            ex, ey = bx - ax, by - ay
            t = np.clip(((px - ax) * ex + (py - ay) * ey) / (ex * ex + ey * ey), 0, 1)
            d = np.minimum(d, np.hypot(px - ax - ex * t, py - ay - ey * t))
            if ay != by:
                cross = (ay <= py) != (by <= py)
                xint = ax + (py - ay) * ex / ey
                inside ^= cross & (px < xint)
        return np.where(inside, -d, d)
    return f


def sd_arc(cx, cy, r, a0, a1, w):
    """Kreisbogen von a0 bis a1 Grad (Bildschirmwinkel, y nach unten), Dicke w, runde Kappen."""
    a0r, a1r = math.radians(a0), math.radians(a1)
    e0 = (cx + r * math.cos(a0r), cy + r * math.sin(a0r))
    e1 = (cx + r * math.cos(a1r), cy + r * math.sin(a1r))

    def f(px, py):
        ang = np.arctan2(py - cy, px - cx)
        ang = np.mod(ang - a0r, 2 * math.pi) + a0r
        on = ang <= a1r
        dr = np.abs(np.hypot(px - cx, py - cy) - r)
        de = np.minimum(np.hypot(px - e0[0], py - e0[1]), np.hypot(px - e1[0], py - e1[1]))
        return np.where(on, dr, de) - w / 2
    return f


def u_(*fs):
    return lambda px, py: np.minimum.reduce([f(px, py) for f in fs])


def i_(*fs):
    return lambda px, py: np.maximum.reduce([f(px, py) for f in fs])


def xform(f, cx, cy, s, rot=0.0):
    c, sn = math.cos(math.radians(rot)), math.sin(math.radians(rot))

    def g(px, py):
        dx, dy = (px - cx) / s, (py - cy) / s
        return f(dx * c + dy * sn, -dx * sn + dy * c) * s
    return g


# ---------------------------------------------------------------- Zeichen (Icons) im Einheitsfeld [-1,1]

ICON = {}
ICON["flame"] = (sd_poly([(0, -1), (0.45, -0.35), (0.65, 0.3), (0.4, 0.8), (0, 1), (-0.4, 0.8), (-0.65, 0.3), (-0.45, -0.05),
                          (-0.2, 0.2), (-0.25, -0.55)]), sd_ellipse(0.02, 0.55, 0.2, 0.3))
ICON["shield"] = (sd_poly([(-0.75, -0.85), (0.75, -0.85), (0.75, 0.15), (0.4, 0.65), (0, 1), (-0.4, 0.65), (-0.75, 0.15)]),
                  sd_box(-0.07, -0.65, 0.07, 0.6))
ICON["coin"] = (sd_circle(0, 0, 1), lambda px, py: np.abs(np.hypot(px, py) - 0.55) - 0.09)
ICON["key"] = (u_(sd_circle(-0.5, 0, 0.5), sd_box(-0.1, -0.13, 1.0, 0.13), sd_box(0.62, 0, 0.84, 0.5), sd_box(0.3, 0, 0.48, 0.38)),
               sd_circle(-0.5, 0, 0.2))
ICON["flask"] = (u_(sd_box(-0.22, -1, 0.22, -0.3), sd_circle(0, 0.38, 0.62), sd_box(-0.36, -1, 0.36, -0.84)),
                 sd_box(-0.5, 0.3, 0.5, 0.42))
ICON["eye"] = (i_(sd_circle(0, 0.95, 1.35), sd_circle(0, -0.95, 1.35)), sd_circle(0, 0, 0.28))
ICON["star"] = (sd_poly([(0, -1), (0.2, -0.2), (1, 0), (0.2, 0.2), (0, 1), (-0.2, 0.2), (-1, 0), (-0.2, -0.2)]), None)
ICON["diamond"] = (sd_poly([(0, -1), (1, 0), (0, 1), (-1, 0)]), None)
# Waffen, waagerecht, x in [-1,1], y in [-0.5,0.5]
ICON["blade"] = (u_(sd_poly([(-0.3, -0.07), (0.82, -0.07), (1.0, 0), (0.82, 0.07), (-0.3, 0.07)]), sd_box(-0.42, -0.3, -0.3, 0.3),
                    sd_box(-0.85, -0.05, -0.42, 0.05), sd_circle(-0.92, 0, 0.1)), None)
ICON["pistol"] = (u_(sd_box(-0.05, -0.28, 1.0, -0.08), sd_poly([(-0.5, -0.28), (0.1, -0.28), (0.1, 0.0), (-0.12, 0.0), (-0.38, 0.5),
                                                              (-0.66, 0.4), (-0.52, -0.04)]),
                     sd_poly([(-0.5, -0.28), (-0.62, -0.42), (-0.46, -0.42), (-0.34, -0.28)])), None)
ICON["rifle"] = (u_(sd_box(-0.2, -0.12, 1.0, -0.02),
                    sd_poly([(-1.0, 0.02), (-1.0, 0.42), (-0.78, 0.34), (-0.3, 0.1), (-0.2, 0.1), (-0.2, -0.14), (-0.72, -0.08)]),
                    sd_box(0.1, 0.0, 0.8, 0.07)), None)
ICON["blunder"] = (u_(sd_poly([(0.05, -0.12), (0.62, -0.14), (1.0, -0.4), (1.0, 0.24), (0.62, 0.02), (0.05, 0.04)]),
                      sd_poly([(-0.9, 0.0), (-0.9, 0.42), (-0.7, 0.34), (-0.25, 0.1), (-0.05, 0.1), (-0.05, -0.12), (-0.6, -0.08)])), None)


def glyph(L, name, cx, cy, s, c1, c2=None, detail=(20, 14, 28), line=None, rot=0.0):
    """Icon zeichnen: flaechig (c1 oben, c2 unten) oder als Umriss (line=Dicke)."""
    f, det = ICON[name]
    g = xform(f, cx, cy, s, rot)
    bb = (cx - s * 1.4, cy - s * 1.4, cx + s * 1.4, cy + s * 1.4)
    if line is None:
        col = vg(cy - s, cy + s, c1, c2 or c1)
        L.shape(bb, g, col)
        if det is not None:
            L.shape(bb, xform(det, cx, cy, s, rot), detail, alpha=0.9)
    else:
        L.shape(bb, g, c1, stroke=line)
        if det is not None and name in ("eye", "coin", "key"):
            L.shape(bb, xform(det, cx, cy, s, rot), c1, stroke=None if name != "eye" else None, alpha=1.0 if name == "eye" else 0.0)
            if name in ("coin",):
                L.shape(bb, xform(det, cx, cy, s, rot), c1, alpha=0.9)


# ---------------------------------------------------------------- Zustand fuer alle Blaetter

HP = 0.5          # halbes Leben
STAM = 0.62       # Ausdauer teils verbraucht
XP = 0.45
BOSS = 0.62
LEVEL = 7
SILVER = 128
KEYS = 2
AMMO = [None, 3, 5, 2]  # Klinge, Pistole, Gewehr, Donnerbuechse
ACTIVE = 1
RELOAD = 0.6      # Schleier ueber 60 % des Waffenbildes

# Farben aus assets/palette/hauptpalette.json
STONE = {0: (16, 11, 20), 1: (38, 34, 40), 2: (45, 40, 47), 4: (61, 52, 56), 7: (86, 74, 80), 10: (113, 97, 104), 14: (151, 127, 137)}
BONE = (216, 210, 192); BONE2 = (195, 192, 164); BONE3 = (140, 148, 115)
BRASS_D = (111, 70, 31); BRASS_M = (188, 135, 45); BRASS = (214, 170, 64); BRASS_L = (227, 202, 111); GOLD_W = (238, 228, 160)
IRON0 = (20, 18, 28); IRON1 = (38, 36, 56); IRON2 = (55, 57, 84); IRON3 = (73, 83, 112); IRON4 = (92, 112, 140)
IRON5 = (126, 144, 165); IRON6 = (163, 175, 187); IRON7 = (198, 205, 210)
RED0 = (27, 9, 15); RED1 = (64, 17, 28); RED2 = (105, 20, 34); RED3 = (150, 20, 30); RED5 = (200, 30, 40); RED6 = (225, 58, 48)
FOG = (16, 11, 20)
ROYAL = (214, 170, 64)

F_CINZEL = "C:/Windows/Fonts/Cinzel-Black.ttf"
F_MONO = "C:/Windows/Fonts/UbuntuMono-Bold.ttf"
F_BELL = "C:/Windows/Fonts/BELLI.TTF"


def arch(x0, y0, x1, y1, rise=0.35):
    cx = (x0 + x1) / 2; sh = y0 + (y1 - y0) * rise
    return [(x0, y1), (x0, sh), (cx, y0), (x1, sh), (x1, y1)]


def chamfer(x0, y0, x1, y1, c):
    return [(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c), (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)]


def pointed(x0, y0, x1, y1, p):
    """Balken mit spitzen Enden (Gotik)."""
    cy = (y0 + y1) / 2
    return [(x0, cy), (x0 + p, y0), (x1 - p, y0), (x1, cy), (x1 - p, y1), (x0 + p, y1)]


def clipx(x):  # nur links von x
    return lambda px, py: px - x


def veil(L, x0, y0, x1, y1, frac):
    """Halbdurchsichtiger Schleier, der sich ueber das Waffenbild zieht (von links), mit heller Vorderkante."""
    xe = x0 + (x1 - x0) * frac
    L.shape((x0, y0, xe + 2, y1), sd_box(x0, y0, x1, y1), (14, 9, 22), alpha=0.72, clipf=clipx(xe))
    L.shape((xe - 2, y0, xe + 2, y1), sd_seg((xe, y0), (xe, y1), L.hair * 1.2), IRON6, alpha=0.9, clipf=sd_box(x0, y0, x1, y1))


# ================================================================ Design 1: Kathedrale (Messing, Spitzbogen, Cinzel)


def design1(L):
    F = F_CINZEL
    sh = (0.8, 0.8, FOG)
    hi = L.hi

    def panel(poly, active=False, a=0.82):
        bb = (min(p[0] for p in poly) - 2, min(p[1] for p in poly) - 2, max(p[0] for p in poly) + 2, max(p[1] for p in poly) + 2)
        f = sd_poly(poly)
        L.shape(bb, f, vg(bb[1], bb[3], STONE[2], STONE[0]), alpha=a)
        L.shape(bb, f, BRASS_L if active else BRASS_M, stroke=1.2 if not L.hard else 1)
        if hi:  # feine Innenlinie
            L.shape(bb, lambda px, py: f(px, py) + 1.6, BRASS_D, stroke=0.5)

    # oben links: Stufe, XP, Skillpunkt, Skills
    panel(arch(14, 12, 44, 44, 0.3))
    L.text(F, 17, str(LEVEL), 29, 38, BRASS_L, "c", shadow=sh)
    L.text(F, 10, "LV", 52, 22, BONE, shadow=sh)
    bar = sd_box(52, 26, 134, 32)
    L.shape((52, 26, 134, 32), bar, IRON0, alpha=0.85)
    L.shape((52, 26, 134, 32), bar, vg(26, 32, BRASS_L, BRASS_D), clipf=clipx(52 + 82 * XP))
    L.shape((52, 26, 134, 32), bar, BRASS_M, stroke=1)
    glyph(L, "star", 146, 29, 6.5, BRASS_L, BRASS)
    if hi:
        L.shape((136, 19, 156, 39), sd_circle(146, 29, 9), BRASS, alpha=0.18)
    for i, g in enumerate(("flame", "shield")):
        x = 14 + i * 34
        panel(arch(x, 52, x + 28, 84, 0.3))
        glyph(L, g, x + 14, 71, 8.5, BONE, BONE3)

    # oben rechts: Silber, Schluessel
    for row, (g, val) in enumerate((("coin", SILVER), ("key", KEYS))):
        y = 28 + row * 22
        w = L.text(F, 14, str(val), 840, y, BONE, "r", shadow=sh)
        if g == "coin":
            glyph(L, "coin", 840 - w - 12, y - 5, 6.2, IRON7, IRON4, detail=IRON3)
        else:
            glyph(L, "key", 840 - w - 16, y - 5, 7.5, BRASS_L, BRASS_M)

    # oben Mitte: Boss
    L.text(F, 12, "GORM", 427, 17, BONE, "c", shadow=sh)
    L.shape((262, 8, 292, 16), sd_poly([(279, 9), (283, 12.5), (279, 16), (275, 12.5)]), BRASS, alpha=0.0)
    for sgn in (-1, 1):
        xx = 427 + sgn * 38
        glyph(L, "diamond", xx, 12.5, 2.4, BRASS_L)
        L.shape((0, 12, 854, 13), sd_seg((xx + sgn * 6, 12.5), (xx + sgn * 56, 12.5), L.hair), BRASS_M, alpha=0.8)
    poly = pointed(267, 21, 587, 32, 6)
    f = sd_poly(poly)
    L.shape((265, 19, 589, 34), f, IRON0, alpha=0.88)
    L.shape((265, 19, 589, 34), f, vg(21, 32, BONE, BONE3), clipf=clipx(267 + 320 * BOSS))
    L.shape((265, 19, 589, 34), f, BRASS_M, stroke=1.3)

    # Bildmitte
    L.shape((420, 233, 434, 247), sd_circle(427, 240, 2.2), (16, 11, 20), alpha=0.85)
    L.shape((420, 233, 434, 247), sd_circle(427, 240, 1.4), BONE)
    capx = 427 - (14 + 5 + textw(L, F, 12, "to open")) / 2
    panel(chamfer(capx, 255, capx + 14, 270, 2), a=0.9)
    L.text(F, 11, "F", capx + 7, 267, BRASS_L, "c")
    L.text(F, 12, "to open", capx + 19, 267, BONE, shadow=sh)

    # unten Mitte: Leben (spitze Leiste) und Ausdauer (5 Glieder)
    poly = pointed(312, 436, 542, 452, 8)
    f = sd_poly(poly)
    L.shape((310, 434, 544, 454), f, RED0, alpha=0.9)
    fx = 312 + 230 * HP
    L.shape((310, 434, 544, 454), f, vg(436, 452, RED5, RED2), clipf=clipx(fx))
    L.shape((310, 434, 544, 454), f, RED6, alpha=0.8, clipf=lambda px, py: np.maximum(px - fx, py - 440))
    L.shape((fx - 2, 435, fx + 2, 453), sd_seg((fx, 437), (fx, 451), L.hair), RED6, clipf=f)
    L.shape((310, 434, 544, 454), f, BRASS_M, stroke=1.6)
    segs = 5; sw = 41; gap = 4; x0 = 427 - (segs * sw + (segs - 1) * gap) / 2
    for i in range(segs):
        sx0 = x0 + i * (sw + gap)
        b = sd_box(sx0, 458, sx0 + sw, 463, 1)
        L.shape((sx0 - 2, 456, sx0 + sw + 2, 465), b, IRON0, alpha=0.85)
        fill = np.clip(STAM * segs - i, 0, 1)
        if fill > 0:
            L.shape((sx0 - 2, 456, sx0 + sw + 2, 465), b, vg(458, 463, IRON7, IRON4), clipf=clipx(sx0 + sw * fill))
        L.shape((sx0 - 2, 456, sx0 + sw + 2, 465), b, BRASS_D, stroke=1)

    # unten links: Items, Meldung
    for i, g in enumerate(("flask", "eye")):
        x = 14 + i * 34
        panel(chamfer(x, 440, x + 28, 468, 3))
        glyph(L, g, x + 14, 454, 8, BONE if g == "flask" else BRASS_L, BONE3 if g == "flask" else BRASS, detail=(40, 20, 10))
    glyph(L, "eye", 20, 425, 4.6, ROYAL, BRASS_M, detail=(40, 20, 10))
    L.text(F, 11, "Picked up Hunter's Eye", 30, 429, ROYAL, shadow=sh)

    # unten rechts: Waffen
    for i, (g, am) in enumerate(zip(("blade", "pistol", "rifle", "blunder"), AMMO)):
        x = 628 + i * 54
        act = i == ACTIVE
        panel(chamfer(x, 426, x + 50, 468, 3), active=act, a=0.88 if act else 0.78)
        glyph(L, g, x + 25, 440, 19, BONE if act else BONE2, BONE3 if act else IRON6)
        if am is not None:
            L.text(F, 12, str(am), x + 25, 463, BRASS_L if act else BONE, "c", shadow=sh)
        if act:
            veil(L, x + 2, 428, x + 48, 449, RELOAD)


# ================================================================ Design 2: Eisenwerk (Platten, Nieten, Orb, Mono)


def design2(L):
    F = F_MONO
    sh = (0.8, 0.8, FOG)
    hi = L.hi

    def rivet(x, y):
        L.shape((x - 2, y - 2, x + 2, y + 2), sd_circle(x, y, 1.1 if hi else 1.0), IRON6)

    def plate(x0, y0, x1, y1, active=False, rivets=True, a=0.88):
        f = sd_box(x0, y0, x1, y1, 0)
        bb = (x0 - 2, y0 - 2, x1 + 2, y1 + 2)
        L.shape(bb, f, vg(y0, y1, IRON2, IRON0), alpha=a)
        L.shape(bb, f, BRASS if active else IRON0, stroke=2.2 if not L.hard else 2)
        L.shape(bb, f, BRASS_L if active else IRON4, stroke=1, clipf=lambda px, py: np.maximum(px - x1 + 1.2, py - y1 + 1.2) * -1 + 0.0 if False else 0 * px - 1)
        if hi:
            L.shape(bb, lambda px, py: f(px, py) + 2.6, IRON3, stroke=0.5, alpha=0.7)
        if rivets:
            for (rx, ry) in ((x0 + 3.5, y0 + 3.5), (x1 - 3.5, y0 + 3.5), (x0 + 3.5, y1 - 3.5), (x1 - 3.5, y1 - 3.5)):
                rivet(rx, ry)

    # oben links
    plate(14, 12, 44, 42)
    L.text(F, 20, str(LEVEL), 29, 35, IRON7, "c", shadow=sh)
    L.text(F, 11, "LV", 52, 21, IRON6, shadow=sh)
    nb = 10; bw = 7.2; bg = 1.2
    for i in range(nb):
        x = 52 + i * (bw + bg)
        fill = np.clip(XP * nb - i, 0, 1)
        L.shape((x - 1, 24, x + bw + 1, 33), sd_box(x, 25, x + bw, 32), IRON0, alpha=0.9)
        if fill > 0:
            L.shape((x - 1, 24, x + bw + 1, 33), sd_box(x, 25, x + bw, 32), vg(25, 32, BRASS_L, BRASS_M), clipf=clipx(x + bw * fill))
    glyph(L, "diamond", 148, 29, 6, BRASS_L, BRASS)
    glyph(L, "diamond", 148, 29, 3, BRASS_D)
    for i, g in enumerate(("flame", "shield")):
        x = 14 + i * 34
        plate(x, 52, x + 28, 80, rivets=False)
        glyph(L, g, x + 14, 66, 8.5, IRON7, IRON4, detail=IRON1)

    # oben rechts: eine Platte mit zwei Zeilen
    plate(766, 12, 840, 52)
    for row, (g, val) in enumerate((("coin", SILVER), ("key", KEYS))):
        y = 29 + row * 16
        L.text(F, 14, str(val), 830, y, IRON7, "r", shadow=sh)
        if g == "coin":
            glyph(L, "coin", 781, y - 4.5, 5.2, IRON7, IRON5, detail=IRON3)
        else:
            glyph(L, "key", 781, y - 4.5, 6.4, BRASS_L, BRASS_M)

    # oben Mitte: Boss
    plate(252, 12, 602, 36)
    L.text(F, 11, "GORM", 262, 28, IRON7, shadow=sh)
    n = 24; bw = 10.5; bg = 1.2; x0 = 303
    for i in range(n):
        x = x0 + i * (bw + bg)
        fill = np.clip(BOSS * n - i, 0, 1)
        L.shape((x - 1, 18, x + bw + 1, 31), sd_box(x, 19, x + bw, 30), IRON0, alpha=0.9)
        if fill > 0:
            L.shape((x - 1, 18, x + bw + 1, 31), sd_box(x, 19, x + bw, 30), vg(19, 30, BONE, BONE3), clipf=clipx(x + bw * fill))

    # Bildmitte
    L.shape((420, 233, 434, 247), sd_circle(427, 240, 2.2), (16, 11, 20), alpha=0.85)
    L.shape((420, 233, 434, 247), sd_circle(427, 240, 1.4), IRON7)
    capx = 427 - (15 + 5 + textw(L, F, 13, "to open")) / 2
    plate(capx, 254, capx + 15, 270, active=True, rivets=False)
    L.text(F, 13, "F", capx + 7.5, 266.5, BRASS_L, "c")
    L.text(F, 13, "to open", capx + 20, 266.5, IRON7, shadow=sh)

    # unten Mitte: Orb mit Ausdauerbogen
    cx, cy, r = 427, 441, 24
    # Glas
    L.shape((cx - r - 3, cy - r - 3, cx + r + 3, cy + r + 3), sd_circle(cx, cy, r), RED0, alpha=0.92)
    surf = cy + r * (1 - 2 * HP)
    wave = lambda px, py: py - (surf + 1.6 * np.sin(px * 0.55))
    L.shape((cx - r - 3, cy - r - 3, cx + r + 3, cy + r + 3), sd_circle(cx, cy, r), vg(surf, cy + r, RED6, RED2), clipf=lambda px, py: -wave(px, py))
    L.shape((cx - r - 3, cy - r - 3, cx + r + 3, cy + r + 3), sd_circle(cx, cy, r), RED6, alpha=0.9, stroke=None,
            clipf=lambda px, py: np.maximum(np.abs(wave(px, py)) - L.hair * 0.8, 0))
    L.shape((cx - 18, cy - 20, cx - 6, cy - 8), sd_ellipse(cx - 11, cy - 14, 4.5, 2.5), (255, 255, 255), alpha=0.28)
    # Ring
    ring = lambda px, py: np.abs(np.hypot(px - cx, py - cy) - (r + 2.5)) - 2.7
    L.shape((cx - r - 7, cy - r - 7, cx + r + 7, cy + r + 7), ring, vg(cy - r, cy + r, IRON4, IRON1))
    L.shape((cx - r - 7, cy - r - 7, cx + r + 7, cy + r + 7), lambda px, py: np.hypot(px - cx, py - cy) - (r + 5.2), IRON0, stroke=1)
    for ang in (30, 90, 150, 210, 270, 330):
        rivet(cx + (r + 2.5) * math.cos(math.radians(ang)), cy + (r + 2.5) * math.sin(math.radians(ang)))
    # Ausdauer: Bogen aus 8 Gliedern um die Oberseite
    nseg = 8; a_start = 140; a_end = 400; step = (a_end - a_start) / nseg
    for i in range(nseg):
        a0 = a_start + i * step + 2; a1 = a_start + (i + 1) * step - 2
        f = sd_arc(cx, cy, r + 11.5, a0, a1, 4.2)
        bb = (cx - 45, cy - 45, cx + 45, cy + 45)
        L.shape(bb, f, IRON0, alpha=0.85)
        fill = np.clip(STAM * nseg - i, 0, 1)
        if fill > 0:
            L.shape(bb, sd_arc(cx, cy, r + 11.5, a0, a0 + (a1 - a0) * fill, 3.0), vg(cy - 36, cy, IRON7, IRON4))

    # unten links
    for i, g in enumerate(("flask", "eye")):
        x = 14 + i * 34
        plate(x, 440, x + 28, 468, rivets=False)
        glyph(L, g, x + 14, 454, 8, IRON7 if g == "flask" else BRASS_L, IRON5 if g == "flask" else BRASS, detail=IRON1 if g == "flask" else (40, 20, 10))
    L.shape((14, 413, 200, 431), sd_box(14, 414, 196, 430), IRON0, alpha=0.7)
    glyph(L, "eye", 23, 422, 4.6, ROYAL, BRASS_M, detail=(40, 20, 10))
    L.text(F, 11.5, "Picked up Hunter's Eye", 32, 426, ROYAL, shadow=sh)

    # unten rechts
    for i, (g, am) in enumerate(zip(("blade", "pistol", "rifle", "blunder"), AMMO)):
        x = 628 + i * 54
        act = i == ACTIVE
        plate(x, 426, x + 50, 468, active=act, a=0.9)
        glyph(L, g, x + 25, 441, 18, IRON7 if act else IRON5, IRON5 if act else IRON3)
        if am is not None:
            L.text(F, 13, str(am), x + 25, 463, BRASS_L if act else IRON7, "c", shadow=sh)
        if act:
            veil(L, x + 3, 429, x + 47, 452, RELOAD)


# ================================================================ Design 3: Haarlinie (nur Linien, Raute, Bell kursiv)


def design3(L):
    F = F_BELL
    sh = (0.9, 0.9, FOG)
    hi = L.hair
    LINE = IRON6
    LINE_D = IRON3

    def bracket(x0, y0, x1, y1, col=LINE, ln=5.0, w=None):
        w = w or L.hair
        for (cx, cy, dx, dy) in ((x0, y0, 1, 1), (x1, y0, -1, 1), (x0, y1, 1, -1), (x1, y1, -1, -1)):
            L.shape((min(cx, cx + dx * ln) - 2, min(cy, cy + dy * ln) - 2, max(cx, cx + dx * ln) + 2, max(cy, cy + dy * ln) + 2),
                    u_(sd_seg((cx, cy), (cx + dx * ln, cy), w), sd_seg((cx, cy), (cx, cy + dy * ln), w)), col)

    def softbg(x0, y0, x1, y1, a=0.45):
        L.shape((x0 - 3, y0 - 3, x1 + 3, y1 + 3), sd_box(x0, y0, x1, y1), (16, 11, 20), alpha=a)

    # oben links
    L.text(F, 12, "Lv", 15, 22, BONE3 if False else IRON6, shadow=sh)
    L.text(F, 28, str(LEVEL), 14, 52, BONE, shadow=sh)
    xs, xe, yb = 38, 134, 44
    L.shape((xs - 2, yb - 3, xe + 2, yb + 3), sd_seg((xs, yb), (xe, yb), L.hair), LINE_D, alpha=0.9)
    L.shape((xs - 2, yb - 3, xe + 2, yb + 3), sd_seg((xs, yb), (xs + (xe - xs) * XP, yb), L.hair * 3.2), BRASS_L)
    glyph(L, "star", 148, 44, 6, BRASS_L, BRASS)
    for i, g in enumerate(("flame", "shield")):
        x = 14 + i * 34
        softbg(x, 62, x + 28, 90, 0.35)
        bracket(x, 62, x + 28, 90)
        glyph(L, g, x + 14, 76, 8, IRON7, line=L.hair * 1.1)

    # oben rechts
    for row, (g, val) in enumerate((("coin", SILVER), ("key", KEYS))):
        y = 30 + row * 22
        w = L.text(F, 17, str(val), 840, y, BONE, "r", shadow=sh)
        if g == "coin":
            glyph(L, "coin", 840 - w - 11, y - 6, 6.2, IRON7, line=L.hair * 1.1)
        else:
            glyph(L, "key", 840 - w - 16, y - 6, 7.4, BRASS_L, line=L.hair * 1.1)

    # oben Mitte: Boss
    L.text(F, 14, "Gorm", 427, 20, BONE, "c", shadow=sh)
    xs, xe, yb = 267, 587, 29
    L.shape((xs - 6, yb - 6, xe + 6, yb + 6), sd_seg((xs, yb), (xe, yb), L.hair), LINE_D)
    L.shape((xs - 6, yb - 6, xe + 6, yb + 6), sd_seg((xs, yb), (xs + (xe - xs) * BOSS, yb), L.hair * 4), BONE2)
    for xx in (xs - 7, xe + 7):
        glyph(L, "diamond", xx, yb, 3.2, BRASS_L)

    # Bildmitte
    L.shape((420, 233, 434, 247), sd_circle(427, 240, 2.2), (16, 11, 20), alpha=0.85)
    L.shape((420, 233, 434, 247), sd_circle(427, 240, 1.4), BONE)
    capx = 427 - (12 + 6 + textw(L, F, 14, "to open")) / 2
    L.text(F, 15, "F", capx, 270, BRASS_L, shadow=sh)
    bracket(capx - 3, 257, capx + 9, 274, BRASS_M, ln=3)
    L.text(F, 14, "to open", capx + 15, 270, BONE, shadow=sh)

    # unten Mitte: Leben als 12 Scherben, Ausdauer als Linie
    n = 12; w = 16; g = 3.2; x0 = 427 - (n * w + (n - 1) * g) / 2
    for i in range(n):
        x = x0 + i * (w + g)
        poly = [(x, 446), (x + 3.5, 438), (x + w - 3.5, 438), (x + w, 446), (x + w - 3.5, 454), (x + 3.5, 454)]
        f = sd_poly(poly)
        bb = (x - 2, 436, x + w + 2, 456)
        L.shape(bb, f, (16, 11, 20), alpha=0.55)
        if i < round(HP * n):
            L.shape(bb, f, vg(438, 454, RED5, RED2))
            L.shape(bb, f, RED6, alpha=0.55, clipf=lambda px, py, x=x: np.maximum(py - 443, 0 * px))
        L.shape(bb, f, RED3 if i < round(HP * n) else RED2, stroke=L.hair)
    xs, xe, yb = x0, x0 + n * w + (n - 1) * g, 462
    L.shape((xs - 4, yb - 5, xe + 4, yb + 5), sd_seg((xs, yb), (xe, yb), L.hair), LINE_D)
    xm = xs + (xe - xs) * STAM
    L.shape((xs - 4, yb - 5, xe + 4, yb + 5), sd_seg((xs, yb), (xm, yb), L.hair * 3), vg(yb - 2, yb + 2, IRON7, IRON5))
    glyph(L, "diamond", xm, yb, 3, IRON7)
    for k in range(1, 5):
        xx = xs + (xe - xs) * k / 5
        L.shape((xx - 2, yb - 5, xx + 2, yb + 5), sd_seg((xx, yb - 2.5), (xx, yb + 2.5), L.hair), LINE_D)

    # unten links
    for i, gl in enumerate(("flask", "eye")):
        x = 14 + i * 34
        softbg(x, 440, x + 28, 468, 0.35)
        bracket(x, 440, x + 28, 468)
        glyph(L, gl, x + 14, 454, 8, IRON7 if gl == "flask" else BRASS_L, line=L.hair * 1.1)
    glyph(L, "eye", 20, 422, 4.8, ROYAL, line=L.hair * 1.1)
    L.text(F, 13, "Picked up Hunter's Eye", 30, 426, ROYAL, shadow=sh)

    # unten rechts
    for i, (gl, am) in enumerate(zip(("blade", "pistol", "rifle", "blunder"), AMMO)):
        x = 628 + i * 54
        act = i == ACTIVE
        softbg(x, 426, x + 50, 468, 0.35)
        bracket(x, 426, x + 50, 468, BRASS_L if act else LINE, ln=6, w=L.hair * (1.5 if act else 1))
        glyph(L, gl, x + 25, 440, 18, IRON7, line=L.hair * (1.4 if L.hard else 1.1))
        if am is not None:
            L.text(F, 15, str(am), x + 41, 460.5, BRASS_L if act else BONE, "r", shadow=sh)
        if act:
            veil(L, x + 3, 429, x + 47, 452, RELOAD)


DESIGNS = [("1", "kathedrale", "Kathedrale", design1), ("2", "eisenwerk", "Eisenwerk", design2), ("3", "haarlinie", "Haarlinie", design3)]
RES = [("A", "854", "A  854x480, mitgekoernt"), ("B", "doppelt", "B  HUD doppelt (1708x960)"), ("C", "fenster", "C  HUD Fenster (1920x1080)")]

# ---------------------------------------------------------------- Zusammensetzen


def compose(scene, design, res):
    if res == "A":
        L = Layer(854, 480, True)
        design(L)
        out = scene.copy()
        m = L.a > 0
        comp = L.rgb + scene * (1 - L.a[..., None])
        yy, xx = np.nonzero(m)
        thr = (BAYER[yy % 4, xx % 4] + 0.5) / 16.0
        q = np.floor(np.clip(comp[yy, xx], 0, 1) * 31 + thr[:, None]) / 31
        out[yy, xx] = np.clip(q, 0, 1)
        return nearest(out, FINAL_H, FINAL_W)
    big = nearest(scene, FINAL_H, FINAL_W)
    if res == "B":
        L = Layer(1708, 960, False)
        design(L)
        rgb = bilinear(L.rgb, FINAL_H, FINAL_W); a = bilinear(L.a, FINAL_H, FINAL_W)
    else:
        L = Layer(1920, 1080, False)
        design(L)
        rgb, a = L.rgb, L.a
    return np.clip(rgb + big * (1 - a[..., None]), 0, 1)


def overview(tiles):
    """3x3, Zeilen = Designs, Spalten = Aufloesungen; jede Kachel 640x360 (Blockmittel aus 1920x1080)."""
    tw, th, gap, left, top = 640, 360, 10, 170, 44
    W = left + 3 * tw + 4 * gap; H = top + 3 * th + 4 * gap
    cv = np.full((H, W, 3), np.array((20, 16, 26), np.float32) / 255)
    L = Layer(W, H, False)
    L.sx = L.sy = L.s = 1.0
    for r in range(3):
        for c in range(3):
            t = tiles[r][c].reshape(th, 3, tw, 3, 3).mean(axis=(1, 3))
            y = top + gap + r * (th + gap); x = left + gap + c * (tw + gap)
            cv[y:y + th, x:x + tw] = t
    L.rgb = cv * 0.0
    for c, (_, _, label) in enumerate(RES):
        L.text(F_MONO, 17, label, left + gap + c * (tw + gap) + tw / 2, 30, BONE, "c")
    for r, (n, _, name, _) in enumerate(DESIGNS):
        y = top + gap + r * (th + gap) + th / 2
        L.text(F_CINZEL, 22, name, left / 2 + 2, y + 3, BRASS_L, "c")
        L.text(F_MONO, 15, "Design " + n, left / 2 + 2, y + 26, BONE, "c")
    comp = cv * (1 - L.a[..., None]) + L.rgb
    return np.clip(comp, 0, 1)


def main():
    scene = load_png(os.path.join(HERE, "basis-854x480.png"))
    tiles = []
    for n, slug, name, fn in DESIGNS:
        row = []
        for rc, rslug, _ in RES:
            img = compose(scene, fn, rc)
            save_png(os.path.join(OUT, f"blatt-{n}{rc}-{slug}-{rslug}.png"), img)
            row.append(img)
            print("ok", n, rc, flush=True)
        tiles.append(row)
    save_png(os.path.join(OUT, "uebersicht.png"), overview(tiles))
    # Lesbarkeitspruefung: jedes Blatt auf 854x480 verkleinert (Blockmittel nicht moeglich -> bilinear mit Vorfilter)
    pr = os.path.join(OUT, "pruefung"); os.makedirs(pr, exist_ok=True)
    for (n, slug, _, _), row in zip(DESIGNS, tiles):
        for (rc, rslug, _), img in zip(RES, row):
            save_png(os.path.join(pr, f"{n}{rc}-{slug}-{rslug}-854.png"), bilinear(img.reshape(1080, 1920, 3), 480, 854))


if __name__ == "__main__":
    main()
