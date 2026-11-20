"""Skizzen zur Formenlehre Gotik (Issue #281): docs/design/formenlehre-gotik.md.

Aufruf (reines Python 3, ohne Blender, ohne Zusatzpakete; nichts wird heruntergeladen):
  python tools/design/formenlehre_gotik.py [<Ausgabeordner>]

Ohne Ausgabeordner schreibt das Skript nach docs/design/bilder. Es zeichnet alle Skizzen selbst (eigene Linien- und
Flaechenroutinen, eigener PNG-Schreiber, Schrift "Kapitell" aus design/proben/grafik/schrift). Farben kommen nur aus
assets/palette/hauptpalette.json. Kein Zufall, keine Zeitstempel: ein zweiter Lauf ergibt bitgleiche Dateien.

Alle Skizzen: 128 Pixel je Meter (zwei Pixel je Texel der Spieltextur mit 64 Pixel je Meter), die Profil-Skizze
256 Pixel je Meter. Die Zahlen der Bausteine stehen in den Funktionen; die Bogenarten werden aus Formeln gerechnet
und beim Lauf auf der Konsole ausgegeben.
"""
import json
import math
import os
import struct
import sys
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(REPO, "design", "proben", "grafik", "schrift"))
import schrift as SCH  # noqa: E402

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(REPO, "docs", "design", "bilder")

# ---------------------------------------------------------------- Farben (nur Hauptpalette)
with open(os.path.join(REPO, "assets", "palette", "hauptpalette.json"), encoding="utf-8") as _f:
    _pal = json.load(_f)


def _find_rows(node, out):
    if isinstance(node, dict):
        if "name" in node and "farben" in node:
            out[node["name"]] = [tuple(c) for c in node["farben"]]
        for v in node.values():
            _find_rows(v, out)
    elif isinstance(node, list):
        for v in node:
            _find_rows(v, out)


_rows = {}
_find_rows(_pal, _rows)
STEIN, WERK, GOLD, ROT = _rows["Stein"], _rows["Werkstein"], _rows["Messing/Gold"], _rows["Blutrot"]

BG = STEIN[1]
WAND, FUGE = STEIN[4], STEIN[2]
OEFFNUNG = STEIN[0]
BAU, BAU_FUGE, BAU_HELL = STEIN[8], STEIN[5], STEIN[12]
ZIER = STEIN[10]
TEXT, TITEL, MASS = STEIN[13], WERK[3], GOLD[5]
FEHLER = ROT[5]

S = 128           # Pixel je Meter
H_GANG = 3.5
SCHICHT = 0.25    # Lagerfugen-Abstand der Wand (16 Pixel in der Spieltextur)
SOCKEL = 0.5
ABSCHLUSS = 0.5
PW, PH = 1240, 680   # Groesse jeder Skizze

_ids, _plist = {}, []


def ci(c):
    if c not in _ids:
        _ids[c] = len(_plist)
        _plist.append(c)
    return _ids[c]


class Canvas:
    def __init__(self, w, h, bg=BG):
        self.w, self.h = w, h
        self.buf = bytearray([ci(bg)]) * (w * h)

    def rect(self, x0, y0, x1, y1, c):
        i = ci(c)
        x0, x1 = max(0, x0), min(self.w, x1)
        if x1 <= x0:
            return
        for y in range(max(0, y0), min(self.h, y1)):
            o = y * self.w
            self.buf[o + x0:o + x1] = bytes([i]) * (x1 - x0)

    def poly(self, pts, c):
        i = ci(c)
        ys = [p[1] for p in pts]
        n = len(pts)
        for y in range(max(0, int(math.floor(min(ys)))), min(self.h, int(math.ceil(max(ys))) + 1)):
            yc = y + 0.5
            xs = []
            for k in range(n):
                (xa, ya), (xb, yb) = pts[k], pts[(k + 1) % n]
                if (ya <= yc < yb) or (yb <= yc < ya):
                    xs.append(xa + (yc - ya) * (xb - xa) / (yb - ya))
            xs.sort()
            for a in range(0, len(xs) - 1, 2):
                xa = max(0, int(math.ceil(xs[a] - 0.5)))
                xb = min(self.w, int(math.ceil(xs[a + 1] - 0.5)))
                if xb > xa:
                    o = y * self.w
                    self.buf[o + xa:o + xb] = bytes([i]) * (xb - xa)

    def line(self, x0, y0, x1, y1, c, wd=2):
        x0, y0, x1, y1 = int(round(x0)), int(round(y0)), int(round(x1)), int(round(y1))
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
        err, h = dx + dy, wd // 2
        while True:
            self.rect(x0 - h, y0 - h, x0 - h + wd, y0 - h + wd, c)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy

    def path(self, pts, c, wd=2, close=False):
        for k in range(len(pts) - (0 if close else 1)):
            a, b = pts[k], pts[(k + 1) % len(pts)]
            self.line(a[0], a[1], b[0], b[1], c, wd)

    def dashed(self, x0, y0, x1, y1, c, wd=2, on=10, off=8):
        ln = math.hypot(x1 - x0, y1 - y0)
        if ln == 0:
            return
        k = 0
        while k * (on + off) < ln:
            a, b = k * (on + off) / ln, min(1.0, (k * (on + off) + on) / ln)
            self.line(x0 + (x1 - x0) * a, y0 + (y1 - y0) * a, x0 + (x1 - x0) * b, y0 + (y1 - y0) * b, c, wd)
            k += 1

    def blit(self, o, x, y):
        for yy in range(o.h):
            if 0 <= y + yy < self.h:
                a = (y + yy) * self.w + x
                self.buf[a:a + o.w] = o.buf[yy * o.w:(yy + 1) * o.w]

    def text(self, x, y, s, c=TEXT, sc=2, anchor="l"):
        s = s.replace("ß", "ss").replace("Ø", "D")
        w = text_width(s, sc)
        x = x - w // 2 if anchor == "m" else (x - w if anchor == "r" else x)
        for ch in s:
            if ch == " ":
                x += (SCH.SPACE + SCH.GAP) * sc
                continue
            px, gw = glyph(ch)
            for gx, gy in px:
                self.rect(x + gx * sc, y + gy * sc, x + (gx + 1) * sc, y + (gy + 1) * sc, c)
            x += (gw + SCH.GAP) * sc

    def save(self, path, palette):
        """Schreibt eine Indexfarben-PNG; die Palette ist die gemeinsame Liste aller Skizzen (Reihenfolge fest)."""
        os.makedirs(os.path.dirname(path), exist_ok=True)
        raw = b"".join(b"\x00" + bytes(self.buf[r * self.w:(r + 1) * self.w]) for r in range(self.h))

        def chunk(t, d):
            return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
        data = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", self.w, self.h, 8, 3, 0, 0, 0))
                + chunk(b"PLTE", b"".join(bytes(c) for c in palette)) + chunk(b"IDAT", zlib.compress(raw, 9))
                + chunk(b"IEND", b""))
        with open(path, "wb") as f:
            f.write(data)


_FONT = SCH.font_a()
_UML = {"ä": "a", "ö": "o", "ü": "u", "Ä": "A", "Ö": "O", "Ü": "U"}


def glyph(ch):
    if ch in _UML:
        px, w = _FONT.glyphs[_UML[ch]]
        top = min(y for _, y in px)
        return set(px) | {(1, top - 2), (max(2, w - 2), top - 2)}, w
    if ch == "×":
        ch = "x"
    if ch not in _FONT.glyphs:
        ch = "?"
    return _FONT.glyphs[ch]


def text_width(s, sc=2):
    w = 0
    for k, ch in enumerate(s):
        w += SCH.SPACE if ch == " " else glyph(ch)[1]
        if k < len(s) - 1:
            w += SCH.GAP
    return w * sc


def fmt(v, nd=3):
    t = ("%." + str(nd) + "f") % v
    t = t.rstrip("0").rstrip(".") if "." in t else t
    return t.replace(".", ",")


class T:
    """Meter -> Pixel (y nach oben)."""
    def __init__(self, ox, oy, s=S):
        self.ox, self.oy, self.s = ox, oy, s

    def x(self, v):
        return int(round(self.ox + v * self.s))

    def y(self, v):
        return int(round(self.oy - v * self.s))

    def p(self, v):
        return (self.x(v[0]), self.y(v[1]))


# ---------------------------------------------------------------- Zeichenhilfen
def panel(title, sub):
    c = Canvas(PW, PH)
    c.text(30, 22, title, TITEL, 3)
    c.text(30, 58, sub, TEXT, 2)
    return c


def notes(c, x, y, lines, step=26):
    for k, ln in enumerate(lines):
        col = TITEL if ln.startswith("#") else TEXT
        c.text(x, y + k * step, ln.lstrip("#"), col, 2)


def dim_h(c, t, x0, x1, y, label, below=False):
    a, b, yy = t.x(x0), t.x(x1), t.y(y)
    c.line(a, yy, b, yy, MASS, 2)
    for xx in (a, b):
        c.line(xx, yy - 6, xx, yy + 6, MASS, 2)
    c.text((a + b) // 2, yy + 10 if below else yy - 26, label, MASS, 2, "m")


def dim_v(c, t, x, y0, y1, label, right=False):
    xx, a, b = t.x(x), t.y(y0), t.y(y1)
    c.line(xx, a, xx, b, MASS, 2)
    for yy in (a, b):
        c.line(xx - 6, yy, xx + 6, yy, MASS, 2)
    mid = (a + b) // 2 - 7
    if right:
        c.text(xx + 10, mid, label, MASS, 2, "l")
    else:
        c.text(xx - 10, mid, label, MASS, 2, "r")


def draw_wall(c, t, x0, x1, y0, y1, base=WAND, fuge=FUGE):
    """Wandflaeche: Lagerfugen alle 0,25 m, Stossfugen im 0,25-m-Raster, je Schicht versetzt."""
    c.rect(t.x(x0), t.y(y1), t.x(x1), t.y(y0), base)
    lens = [1.0, 0.75, 1.25, 0.5, 1.0, 0.75]
    k0, k1 = int(math.floor(y0 / SCHICHT + 1e-6)), int(math.ceil(y1 / SCHICHT - 1e-6))
    for k in range(k0, k1):
        ya, yb = max(y0, k * SCHICHT), min(y1, (k + 1) * SCHICHT)
        cum, j = -8.0 - ((k * 5) % 8) * 0.25, k
        while cum < x1:
            if x0 < cum < x1:
                c.rect(t.x(cum) - 1, t.y(yb), t.x(cum) + 1, t.y(ya), fuge)
            cum += lens[j % len(lens)]
            j += 1
    for k in range(k0, k1 + 1):
        yy = k * SCHICHT
        if y0 < yy < y1:
            c.rect(t.x(x0), t.y(yy) - 1, t.x(x1), t.y(yy) + 1, fuge)


def stone_block(c, t, x0, x1, y0, y1, fill=BAU, fuge=BAU_FUGE, hell=BAU_HELL, shift=0.0):
    """Bauteil in Vorderansicht; Lagerfugen auf den Hoehen der Wand (shift nur fuer das Falsch-Beispiel)."""
    c.rect(t.x(x0), t.y(y1), t.x(x1), t.y(y0), fill)
    for k in range(-2, int(y1 / SCHICHT) + 3):
        yy = k * SCHICHT + shift
        if y0 < yy < y1:
            c.rect(t.x(x0), t.y(yy) - 1, t.x(x1), t.y(yy) + 1, fuge)
    c.rect(t.x(x0), t.y(y1), t.x(x1), t.y(y1) + 2, hell)
    c.rect(t.x(x0), t.y(y1), t.x(x0) + 2, t.y(y0), hell)


def zone_lines(c, t, x0, x1, h=H_GANG):
    for y in (SOCKEL, h - ABSCHLUSS):
        c.dashed(t.x(x0), t.y(y), t.x(x1), t.y(y), MASS, 2, 14, 8)


def wall_zoned(c, t, x0, x1, h=H_GANG):
    draw_wall(c, t, x0, x1, 0, h)
    for ya, yb in ((0, SOCKEL), (h - ABSCHLUSS, h)):
        draw_wall(c, t, x0, x1, ya, yb, base=STEIN[6], fuge=STEIN[3])
        c.rect(t.x(x0), t.y(yb), t.x(x1), t.y(yb) + 2, BAU_HELL)


def arc_half(hw, R, spring, n=40):
    """Rechte Haelfte; Mittelpunkt (cx, spring) mit cx = hw - R (negativ bei Spitzbogen); Ansatz -> Scheitel."""
    cx = hw - R
    a_end = math.acos(max(-1.0, min(1.0, -cx / R)))
    return [(cx + R * math.cos(a_end * k / n), spring + R * math.sin(a_end * k / n)) for k in range(n + 1)]


def full_arc(hw, R, spring, n=40):
    r = arc_half(hw, R, spring, n)
    return [(-x, y) for x, y in r] + [(x, y) for x, y in reversed(r)][1:]


def arch_ring(hw_out, R_out, width, spring, n=40):
    """Ring um zwei Mittelpunkte; Innenkante gleicher Mittelpunkt, Radius R_out - width."""
    cx = hw_out - R_out

    def half(R):
        a_end = math.acos(max(-1.0, min(1.0, -cx / R)))
        return [(cx + R * math.cos(a_end * k / n), spring + R * math.sin(a_end * k / n)) for k in range(n + 1)]

    def mir(r):
        return [(-x, y) for x, y in r] + [(x, y) for x, y in reversed(r)][1:]
    return mir(half(R_out)), mir(half(R_out - width))


def fill_ring(c, t, outer, inner, color):
    for k in range(len(outer) - 1):
        c.poly([t.p(outer[k]), t.p(outer[k + 1]), t.p(inner[k + 1]), t.p(inner[k])], color)


# ---------------------------------------------------------------- 1 Bogenarten
def bogenarten():
    hw = 1.0
    arten = [("Rundbogen", 1.0, "Nische, Kammer"), ("gedrückter Spitzbogen", 1.5625, "Gang, Türrahmen"),
             ("gleichseitiger Bogen", 2.0, "Halle, hoher Raum"), ("Lanzettbogen", 3.0, "nur Räume ab 6 m")]
    c = panel("BOGENARTEN", "gleiche Spannweite 2 m, Lauf 0,125 m; Kämpferlinie gestrichelt, Mittelpunkte als Kreuz")
    rows = []
    for k, (name, R, use) in enumerate(arten):
        t = T(190 + k * 300, 420)
        cx = hw - R
        rise = math.sqrt(R * R - cx * cx)
        rows.append((name, R, R / 2.0, rise, use))
        outer, inner = arch_ring(hw, R, 0.125, 0.0)
        c.rect(t.x(-1.0), t.y(rise), t.x(1.0), t.y(-0.5), WAND)
        c.poly([t.p((-1.0, 0))] + [t.p(p) for p in outer[1:-1]] + [t.p((1.0, 0)), t.p((0.875, -0.5)), t.p((-0.875, -0.5))],
               OEFFNUNG)
        fill_ring(c, t, outer, inner, BAU)
        c.rect(t.x(-1.0), t.y(0), t.x(-0.875), t.y(-0.5), BAU)
        c.rect(t.x(0.875), t.y(0), t.x(1.0), t.y(-0.5), BAU)
        c.dashed(t.x(-1.1), t.y(0), t.x(1.1), t.y(0), MASS, 2, 10, 8)
        for mx in (cx, -cx):
            c.line(t.x(mx) - 6, t.y(0), t.x(mx) + 6, t.y(0), MASS, 2)
            c.line(t.x(mx), t.y(0) - 6, t.x(mx), t.y(0) + 6, MASS, 2)
        c.text(t.x(0), 500, name, TITEL, 2, "m")
        c.text(t.x(0), 526, "R = %s m (%s mal Spannweite)" % (fmt(R, 4), fmt(R / 2.0, 3)), TEXT, 2, "m")
        c.text(t.x(0), 552, "Stich %s m" % fmt(rise, 2), TEXT, 2, "m")
        c.text(t.x(0), 578, use, TEXT, 2, "m")
    c.text(30, 626, "Regel: Scheitel der Innenkante höchstens Deckenhöhe. Im 3,5-m-Gang passt bei Kämpfer 2 m nur der gedrückte Bogen.", TEXT, 2)
    return c, rows


# ---------------------------------------------------------------- 2 Wand in drei Zonen
def wandzonen():
    c = panel("WAND IN DREI ZONEN", "Sockel, Steinfläche, Abschluss; Lagerfugen alle 0,25 m laufen durch alle drei Zonen")
    t = T(330, 640)
    wall_zoned(c, t, -2.0, 2.0)
    zone_lines(c, t, -2.45, 2.1)
    dim_v(c, t, -2.2, 0, SOCKEL, "0,5")
    dim_v(c, t, -2.2, SOCKEL, H_GANG - ABSCHLUSS, "2,5")
    dim_v(c, t, -2.2, H_GANG - ABSCHLUSS, H_GANG, "0,5")
    dim_h(c, t, -1.0, -0.75, 1.0, "0,25")
    c.text(t.x(0), t.y(0.25) - 7, "SOCKEL", TITEL, 2, "m")
    c.text(t.x(0), t.y(1.75) - 7, "STEINFLÄCHE", TITEL, 2, "m")
    c.text(t.x(0), t.y(3.25) - 7, "ABSCHLUSS", TITEL, 2, "m")
    ts = T(880, 640)
    c.rect(ts.x(0), ts.y(H_GANG), ts.x(0.5), ts.y(0), WAND)
    c.poly([ts.p((0, 0)), ts.p((-0.0625, 0)), ts.p((-0.0625, 0.4375)), ts.p((0, 0.5))], BAU)
    c.poly([ts.p((0, 3.0)), ts.p((-0.125, 3.125)), ts.p((-0.125, H_GANG)), ts.p((0, H_GANG))], BAU)
    c.line(ts.x(0), ts.y(0), ts.x(0), ts.y(H_GANG), BAU_HELL, 2)
    c.line(ts.x(-0.9), ts.y(0), ts.x(0.9), ts.y(0), TEXT, 2)
    c.line(ts.x(-0.9), ts.y(H_GANG), ts.x(0.9), ts.y(H_GANG), TEXT, 2)
    c.text(ts.x(0), 106, "SCHNITT", TITEL, 2, "m")
    c.text(ts.x(-0.9), ts.y(0) + 8, "Boden", TEXT, 2)
    c.text(ts.x(-0.9), ts.y(H_GANG) - 24, "Decke", TEXT, 2)
    dim_h(c, ts, -0.0625, 0, 0.7, "1/16")
    dim_h(c, ts, -0.125, 0, 2.7, "1/8")
    notes(c, 1000, 150, ["#Sockel:", "tritt 1/16 m vor,", "oben Fase 1/16", "#Abschluss:", "tritt 1/8 m vor,", "unten Schräge 1/8",
                         "Raum 6 m:", "nur die Stein-", "fläche wird höher"], 24)
    return c


# ---------------------------------------------------------------- 3 Profile
def profile():
    c = panel("PROFILE IM GRUNDRISS", "Maßstab 256 Pixel je Meter; alle Kanten Vielfache von 1/16 m")
    s = 256
    t = T(150, 300, s)
    f = 0.0625
    pf = [(-0.25, 0), (0.25, 0), (0.25, -0.25 + f), (0.25 - f, -0.25), (-0.25 + f, -0.25), (-0.25, -0.25 + f)]
    c.rect(t.x(-0.5), t.y(0.3), t.x(0.5), t.y(0), WAND)
    c.poly([t.p(p) for p in pf], BAU)
    c.path([t.p(p) for p in pf], BAU_HELL, 2, True)
    dim_h(c, t, -0.25, 0.25, -0.38, "0,5", below=True)
    dim_v(c, t, 0.4, 0, -0.25, "0,25", right=True)
    c.text(t.x(0), 440, "WANDPFEILER", TITEL, 2, "m")
    t = T(480, 250, s)
    oc = [(0.45 * math.cos(math.radians(22.5 + 45 * k)), 0.45 * math.sin(math.radians(22.5 + 45 * k))) for k in range(8)]
    oc2 = [(0.5 * math.cos(math.radians(22.5 + 45 * k)), 0.5 * math.sin(math.radians(22.5 + 45 * k))) for k in range(8)]
    c.poly([t.p(p) for p in oc], BAU)
    c.path([t.p(p) for p in oc], BAU_HELL, 2, True)
    c.path([t.p(p) for p in oc2], MASS, 2, True)
    dim_h(c, t, -0.45, 0.45, -0.58, "0,9", below=True)
    c.text(t.x(0), 440, "SÄULE (ACHTECK)", TITEL, 2, "m")
    c.text(t.x(0), 466, "Sockel und Kapitell 1,0", TEXT, 2, "m")
    t = T(810, 300, s)
    pr = [(-0.25, 0.0), (0.25, 0.0), (0.25, -0.1875), (0.1875, -0.25), (-0.1875, -0.25), (-0.25, -0.1875)]
    c.rect(t.x(-0.5), t.y(0.2), t.x(0.5), t.y(0), WAND)
    c.poly([t.p(p) for p in pr], BAU)
    c.path([t.p(p) for p in pr], BAU_HELL, 2, True)
    dim_h(c, t, -0.25, 0.25, -0.38, "0,5", below=True)
    dim_v(c, t, 0.4, 0, -0.25, "0,25", right=True)
    c.text(t.x(0), 440, "GURTBOGEN (QUERSCHNITT)", TITEL, 2, "m")
    c.text(t.x(0), 466, "oben in der Decke, unten Fase 1/16", TEXT, 2, "m")
    t = T(1090, 560, s)
    prof = [(0, 0), (0.0625, 0), (0.0625, 0.125), (0.125, 0.125), (0.125, 0.25), (0.0625, 0.25), (0.0625, 0.375), (0, 0.5)]
    c.rect(t.x(-0.4), t.y(0.6), t.x(0), t.y(-0.05), WAND)
    c.poly([t.p(p) for p in prof] + [t.p((-0.25, 0.5)), t.p((-0.25, 0))], BAU)
    c.path([t.p(p) for p in prof], BAU_HELL, 2)
    c.line(t.x(0), t.y(-0.05), t.x(0), t.y(0.6), BAU_HELL, 2)
    dim_v(c, t, 0.25, 0, 0.5, "0,5", right=True)
    c.text(t.x(-0.4), 380, "KAPITELL SEITE", TITEL, 2)
    notes(c, 30, 520, ["#Regeln für alle Profile", "Fase an jeder Kante, die ein Spieler sieht: 1/16 m",
                       "Ausladung gegenüber dem Schaft höchstens 1/8 m", "Kapitell 0,5 m hoch, in Stufen von 1/16 und 1/8 m",
                       "Gurtbogen und Rippe: Fuß ist so breit wie der Pfeiler, auf dem er steht"], 26)
    return c


# ---------------------------------------------------------------- Bausteine
def wandpfeiler():
    c = panel("WANDPFEILER", "Gang 3,5 m: Sockel 0,5, Schaft 2,5, Kapitell 0,5; im 6-m-Raum wird nur der Schaft länger")
    t = T(300, 640)
    wall_zoned(c, t, -1.25, 1.25)
    stone_block(c, t, -0.3125, 0.3125, 0, 0.5)
    stone_block(c, t, -0.25, 0.25, 0.5, 3.0)
    stone_block(c, t, -0.3125, 0.3125, 3.0, 3.5)
    zone_lines(c, t, -1.4, 1.4)
    dim_h(c, t, -0.25, 0.25, 3.75, "0,5")
    dim_v(c, t, 0.45, 0, 0.5, "0,5", right=True)
    dim_v(c, t, 0.45, 0.5, 3.0, "2,5", right=True)
    dim_v(c, t, 0.45, 3.0, 3.5, "0,5", right=True)
    tp = T(900, 330)
    c.rect(tp.x(-1.25), tp.y(0.5), tp.x(1.25), tp.y(0), WAND)
    c.rect(tp.x(-0.3125), tp.y(0.125), tp.x(0.3125), tp.y(0), BAU_FUGE)
    c.rect(tp.x(-0.25), tp.y(0), tp.x(0.25), tp.y(-0.25), BAU)
    c.path([tp.p((-0.3125, 0)), tp.p((-0.3125, -0.3125)), tp.p((0.3125, -0.3125)), tp.p((0.3125, 0))], MASS, 2)
    c.text(tp.x(0), 100, "GRUNDRISS", TITEL, 2, "m")
    c.text(tp.x(0), tp.y(-0.3125) + 14, "Wand oben, Raum unten", TEXT, 2, "m")
    notes(c, 700, 440, ["#Kontakt zur Wand", "Zahn 1/8 m tief in die Wand, alle 0,5 m", "Lagerfugen auf Höhe der Wandfugen",
                        "Oben im Abschluss, Fuß im Sockel", "#Dreiecke: etwa 40"], 26)
    return c


def saeule():
    c = panel("FREIE SÄULE", "Umkreis 0,9 m, acht Seiten; freistehend; Gang 3,5 m")
    t = T(300, 640)
    wall_zoned(c, t, -1.75, 1.75)
    for y0, y1, hw in ((0, 0.5, 0.5), (0.5, 3.0, 0.45), (3.0, 3.5, 0.5)):
        stone_block(c, t, -hw, hw, y0, y1)
    for sx in (-0.186, 0.186):
        c.rect(t.x(sx) - 1, t.y(3.0), t.x(sx) + 1, t.y(0.5), BAU_FUGE)
    dim_h(c, t, -0.45, 0.45, 3.75, "0,9")
    dim_h(c, t, -0.5, 0.5, -0.1, "1,0", below=True)
    dim_v(c, t, 0.7, 0.5, 3.0, "2,5", right=True)
    dim_v(c, t, 0.7, 3.0, 3.5, "0,5", right=True)
    tp = T(900, 330)
    oc = [(0.5 * math.cos(math.radians(22.5 + 45 * k)), 0.5 * math.sin(math.radians(22.5 + 45 * k))) for k in range(8)]
    ic = [(0.45 * math.cos(math.radians(22.5 + 45 * k)), 0.45 * math.sin(math.radians(22.5 + 45 * k))) for k in range(8)]
    c.poly([tp.p(p) for p in oc], BAU_FUGE)
    c.poly([tp.p(p) for p in ic], BAU)
    c.path([tp.p(p) for p in oc], MASS, 2, True)
    c.text(tp.x(0), 100, "GRUNDRISS", TITEL, 2, "m")
    notes(c, 700, 440, ["#Kontakt zur Wand", "Freistehend: mindestens 0,5 m Abstand zur Wand",
                        "Lagerfugen alle 0,25 m, Trommeln 0,5 m hoch", "Mehrere Säulen: Abstand mindestens 2 m",
                        "#Dreiecke: etwa 80"], 26)
    return c


def deckenbogen():
    c = panel("DECKENBOGEN (GURTBOGEN)", "quer über den Gang: lichte Weite 3,5 m, Kämpfer 1,625 m, R = 1,875 m")
    t = T(350, 640)
    hw, R, spring = 1.75, 1.875, 1.625
    wall_zoned(c, t, -2.6, 2.6)
    outer, inner = arch_ring(hw + 0.25, R + 0.25, 0.25, spring)
    c.poly([t.p((-hw, 0))] + [t.p(p) for p in inner] + [t.p((hw, 0))], OEFFNUNG)
    fill_ring(c, t, outer, inner, BAU)
    c.rect(0, 0, PW, t.y(H_GANG), BG)
    c.text(30, 22, "DECKENBOGEN (GURTBOGEN)", TITEL, 3)
    c.text(30, 58, "quer über den Gang: lichte Weite 3,5 m, Kämpfer 1,625 m, R = 1,875 m", TEXT, 2)
    c.line(t.x(-2.6), t.y(H_GANG), t.x(2.6), t.y(H_GANG), TEXT, 2)
    for sx in (-1, 1):
        a, b = sorted((sx * 1.75, sx * 2.25))
        stone_block(c, t, a, b, 0, spring - 0.125)
        stone_block(c, t, a, b, spring - 0.125, spring)
    c.dashed(t.x(-2.5), t.y(spring), t.x(2.5), t.y(spring), MASS, 2, 14, 8)
    dim_h(c, t, -1.75, 1.75, 0.2, "3,5")
    dim_v(c, t, 2.5, 0, spring, "1,625", right=True)
    dim_v(c, t, 2.5, spring, H_GANG, "1,875", right=True)
    c.text(t.x(0), 100, "Decke 3,5 m: Scheitel innen etwa 3,5, der Ring taucht ein", TEXT, 2, "m")
    notes(c, 880, 150, ["#Maße", "Ring 0,25 m dick,", "0,5 m tief", "Fuß = Grundriss des", "Wandpfeilers", "Kämpfer zwei Fugen hoch",
                        "#Kontakt", "Fuß sitzt auf dem Kapitell,", "Scheitel taucht in die Decke:", "keine nackte Kante", "#Dreiecke: etwa 100"], 26)
    return c


def nische():
    c = panel("WANDNISCHE", "Rundbogen (Katakomben): Weite 1 m, Tiefe 0,5 m, Sohle 0,5 m über dem Boden")
    t = T(330, 640)
    wall_zoned(c, t, -1.5, 1.5)
    outer = full_arc(0.5, 0.5, 1.5)
    c.poly([t.p((-0.5, 0.5))] + [t.p(p) for p in outer[1:-1]] + [t.p((0.5, 0.5))], OEFFNUNG)
    o2, i2 = arch_ring(0.5625, 0.5625, 0.0625, 1.5)
    fill_ring(c, t, o2, i2, BAU)
    stone_block(c, t, -0.5625, -0.5, 0.5, 1.5)
    stone_block(c, t, 0.5, 0.5625, 0.5, 1.5)
    stone_block(c, t, -0.5625, 0.5625, 0.4375, 0.5)
    dim_h(c, t, -0.5, 0.5, 2.25, "1,0")
    dim_v(c, t, 0.9, 0.5, 2.0, "1,5", right=True)
    c.dashed(t.x(-0.8), t.y(1.5), t.x(0.8), t.y(1.5), MASS, 2, 10, 8)
    tp = T(900, 330)
    c.rect(tp.x(-1.25), tp.y(0.5), tp.x(1.25), tp.y(0), WAND)
    c.rect(tp.x(-0.5), tp.y(0.5), tp.x(0.5), tp.y(0), OEFFNUNG)
    c.path([tp.p((-0.5625, 0)), tp.p((-0.5625, 0.5)), tp.p((0.5625, 0.5)), tp.p((0.5625, 0))], BAU_HELL, 2)
    c.text(tp.x(0), 100, "GRUNDRISS", TITEL, 2, "m")
    dim_v(c, tp, 0.9, 0, 0.5, "0,5", right=True)
    notes(c, 700, 440, ["#Kontakt zur Wand", "Rahmen 1/16 m, bündig mit der Wandebene", "Wandfugen laufen bis an den Rahmen",
                        "Sohlbank 1/16 m, Sockelhöhe 0,5", "#Dreiecke: etwa 60"], 26)
    return c


def tuerrahmen():
    c = panel("TÜRRAHMEN (FERTIGES BEISPIEL)", "Bestand tools/models/door.py: Kämpfer 2 m, gedrückter Spitzbogen R = 1,5625 m")
    t = T(330, 640)
    wall_zoned(c, t, -2.2, 2.2)
    R, spring = 1.5625, 2.0
    outer, inner = arch_ring(1.0, R, 0.125, spring)
    c.poly([t.p((-0.875, 0)), t.p((-0.875, spring))] + [t.p(p) for p in inner[1:-1]] + [t.p((0.875, spring)), t.p((0.875, 0))], OEFFNUNG)
    fill_ring(c, t, outer, inner, BAU)
    for sx in (-1, 1):
        a, b = sorted((sx * 0.875, sx * 1.0))
        stone_block(c, t, a, b, 0, spring - 0.125)
        stone_block(c, t, a, b, spring - 0.125, spring, fill=ZIER)
    c.dashed(t.x(-1.3), t.y(spring), t.x(1.3), t.y(spring), MASS, 2, 14, 8)
    dim_h(c, t, -0.875, 0.875, 0.25, "1,75 licht")
    dim_v(c, t, 1.5, 0, spring, "2,0", right=True)
    dim_v(c, t, 1.5, spring, 3.458, "1,46", right=True)
    notes(c, 800, 150, ["#Maße", "Pfostenband 1/8 m", "Kämpferplatte 1/8 m hoch", "Lauf 1/8 m, Scheitel 3,458 m",
                        "Boss: drei gestaffelte Läufe,", "Kämpfer 1,875 m, R = 1,75 m", "#Kontakt", "Bogenfeld bündig in der Wandebene",
                        "Wandfugen enden am Rahmen", "#Dreiecke: etwa 120 (Boss 200)"], 26)
    return c


def kontakt():
    c = panel("KONTAKT ZUR WAND", "links falsch (nackte Kante, Fugen versetzt), rechts richtig (Fugen laufen weiter)")
    for k, ok in enumerate((False, True)):
        t = T(300 + k * 620, 640)
        draw_wall(c, t, -1.5, 1.5, 0, 3.0)
        if ok:
            stone_block(c, t, -0.25, 0.25, 0.0, 3.0)
            stone_block(c, t, -0.3125, 0.3125, 2.5, 3.0)
        else:
            stone_block(c, t, -0.25, 0.25, 0.0, 2.3, shift=0.1)
            c.path([t.p((-0.25, 2.3)), t.p((0.25, 2.3)), t.p((0.25, 0)), t.p((-0.25, 0))], FEHLER, 3)
        c.text(t.x(0), 100, "RICHTIG" if ok else "FALSCH", TITEL if ok else FEHLER, 3, "m")
        c.text(t.x(0), 140, "Fugen auf Wandhöhe, Ende im Abschluss" if ok else "Fugen versetzt, Ende frei auf der Fläche",
               TEXT, 2, "m")
    return c


def farben():
    c = panel("FARBEN DER SKIZZEN", "alle aus assets/palette/hauptpalette.json")
    items = [("Wand", WAND), ("Fuge", FUGE), ("Öffnung", OEFFNUNG), ("Baustein", BAU), ("Baustein-Fuge", BAU_FUGE),
             ("Kante hell", BAU_HELL), ("Zier", ZIER), ("Maß", MASS), ("Fehler", FEHLER), ("Titel", TITEL)]
    for k, (n, col) in enumerate(items):
        x, y = 40 + (k % 5) * 235, 120 + (k // 5) * 150
        c.rect(x, y, x + 200, y + 70, col)
        c.text(x, y + 82, n, TEXT, 2)
    notes(c, 40, 440, ["#Regel: Werkstein (helle Reihe) nur für besondere Portale, nie auf der Wandfläche",
                       "Blutrot nur für Gefahr und hier für den Fehlerfall"], 28)
    return c


# ---------------------------------------------------------------- Lauf
def main():
    arten_c, rows = bogenarten()
    print("Bogenarten (Spannweite 2 m): Name, R, R/s, Stich")
    for name, R, rs, rise, use in rows:
        print("  %s  R=%.4f  R/s=%.4f  Stich=%.3f" % (name, R, rs, rise))
    pages = [("bogenarten", arten_c), ("wandzonen", wandzonen()), ("profile", profile()), ("wandpfeiler", wandpfeiler()),
             ("saeule", saeule()), ("deckenbogen", deckenbogen()), ("nische", nische()), ("tuerrahmen", tuerrahmen()),
             ("kontakt", kontakt()), ("farben", farben())]
    cols, rows_n = 2, (len(pages) + 1) // 2
    ov = Canvas(PW * cols, 90 + PH * rows_n)
    ov.text(30, 20, "FORMENLEHRE GOTIK - ÜBERSICHT", TITEL, 4)
    ov.text(30, 62, "Silver & Ash, Issue 281; Zelle 2 m, Gang 3,5 m, Textur 64 Pixel je Meter", TEXT, 2)
    for k, (_, pc) in enumerate(pages):
        ov.blit(pc, (k % cols) * PW, 90 + (k // cols) * PH)
    palette = list(_plist)
    for name, pc in pages:
        pc.save(os.path.join(OUT, name + ".png"), palette)
    ov.save(os.path.join(OUT, "uebersicht.png"), palette)
    print("geschrieben nach", OUT)


if __name__ == "__main__":
    main()
