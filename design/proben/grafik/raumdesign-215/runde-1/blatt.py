"""Blatt der Probe Raumdesign Runde 1 (Issue #215): Draufsicht der sechs Vorlagen R1 bis R6.

Reines Python (kein PIL, kein numpy, nichts heruntergeladen). Schrift: Pixelschrift A "Kapitell" aus ../../schrift.
Farben aus der Hauptpalette (assets/palette/hauptpalette.json): Hoehen als Stufen der Rampe Stein (tief = dunkel,
hoch = hell), Tueren Holz, Kistenplatz Messing, runde Saeule Stein dunkel. Gezeigt wird je Vorlage die beabsichtigte
Fassung: rN-*.neu.txt, falls vorhanden, sonst rN-*.txt.

Aufruf:   python blatt.py      (schreibt blatt.png neben das Skript; zweiter Lauf bitgleich)
"""
import os
import struct
import sys
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "..", "schrift"))
sys.path.insert(0, HERE)
import schrift as SC  # noqa: E402
import pruefen as PR  # noqa: E402

FONT = SC.font_a()
SC.check(FONT, unique=True)

# ------------------------------------------------------------------ Farben (Hauptpalette)
STONE = [(16, 11, 20), (38, 34, 40), (45, 40, 47), (52, 46, 54), (61, 52, 56), (71, 61, 66), (79, 67, 73),
         (86, 74, 80), (95, 81, 88), (105, 91, 97), (113, 97, 104), (123, 105, 113), (132, 113, 121),
         (140, 120, 129), (151, 127, 137)]
WOOD = [(30, 19, 26), (52, 27, 34), (80, 41, 37), (110, 76, 46), (138, 99, 58), (167, 125, 69), (187, 150, 89),
        (199, 172, 117)]
BRASS = [(38, 21, 13), (74, 44, 23), (111, 70, 31), (149, 100, 39), (188, 135, 45), (214, 170, 64), (227, 202, 111),
         (238, 228, 160)]
WERK = (209, 193, 200)          # Werkstein, hellste Stufe: Schrift
WERK_D = (171, 159, 164)
BG = STONE[0]
WALL = STONE[1]
SHADOW = (10, 8, 14)


# Hoehe in Vierteln (0,25 m) -> Stufe der Rampe Stein. 0 m = Stufe 7; hinab und die ersten Stufen hinauf je zwei
# Rampenstufen, damit auch 0,25 m auf einen Blick zu sehen sind; darueber eine Stufe je 0,25 m.
STEP_TO_STONE = {-8: 2, -6: 2, -4: 2, -3: 3, -2: 4, -1: 5, 0: 7, 1: 9, 2: 10, 3: 11, 4: 12, 5: 13, 6: 14}


def floor_col(h):
    q = max(-8, min(6, int(round(h / 0.25))))
    while q not in STEP_TO_STONE:
        q += 1 if q < 0 else -1
    return STONE[STEP_TO_STONE[q]]


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
    def __init__(self, w, h, fill):
        self.w, self.h = w, h
        self.b = bytearray(bytes(fill) * (w * h))

    def set(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            i = (y * self.w + x) * 3
            self.b[i:i + 3] = bytes(c)

    def rect(self, x0, y0, x1, y1, c):
        x0, y0, x1, y1 = max(0, x0), max(0, y0), min(self.w, x1), min(self.h, y1)
        if x1 <= x0 or y1 <= y0:
            return
        row = bytes(c) * (x1 - x0)
        for y in range(y0, y1):
            i = (y * self.w + x0) * 3
            self.b[i:i + len(row)] = row

    def frame(self, x0, y0, x1, y1, c, t=1):
        self.rect(x0, y0, x1, y0 + t, c)
        self.rect(x0, y1 - t, x1, y1, c)
        self.rect(x0, y0, x0 + t, y1, c)
        self.rect(x1 - t, y0, x1, y1, c)

    def disc(self, cx, cy, r, c):
        for y in range(int(cy - r) - 1, int(cy + r) + 2):
            for x in range(int(cx - r) - 1, int(cx + r) + 2):
                if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= r * r:
                    self.set(x, y, c)

    def save(self, path):
        write_png(path, self.w, self.h, self.b)


# ------------------------------------------------------------------ Schrift mit Umlauten
UML = {"ä": "a", "ö": "o", "ü": "u", "Ä": "A", "Ö": "O", "Ü": "U"}


def layout(s):
    """Wie Font.layout, aber mit Umlauten (zwei Punkte ueber dem Grundzeichen) und ss fuer sz."""
    s = s.replace("ß", "ss")
    out, x = [], 0
    for ch in s:
        if ch == " ":
            x += SC.SPACE + SC.GAP
            continue
        base = UML.get(ch, ch)
        px, w = FONT.glyphs[base]
        for gx, gy in px:
            out.append((x + gx, gy - FONT.base))
        if ch in UML:
            top = min(gy for _, gy in px) - FONT.base - 2
            out += [(x + 1, top), (x + w - 2, top)]
        x += w + SC.GAP
    return out, max(0, x - SC.GAP)


def text_w(s, k):
    return layout(s)[1] * k


def text(c, s, x, top, k, col, shadow=True, anchor="l"):
    """(x, top) = obere linke Ecke der Versalien."""
    pts, w = layout(s)
    w *= k
    if anchor == "m":
        x -= w // 2
    elif anchor == "r":
        x -= w
    base = top + 6 * k
    if shadow:
        for gx, gy in pts:
            c.rect(x + gx * k + k, base + gy * k + k, x + gx * k + 2 * k, base + gy * k + 2 * k, SHADOW)
    for gx, gy in pts:
        c.rect(x + gx * k, base + gy * k, x + gx * k + k, base + gy * k + k, col)
    return w


# ------------------------------------------------------------------ Inhalt
ROOMS = [
    ("R1", "r1-l-form", "L-Form", "Kampf, 16 x 16 - neu: Knick, Podeste bis 0,5 m"),
    ("R2", "r2-t-form", "T-Form", "Kampf, 18 x 14 - neu: Kanzel 0,5 m am T-Kopf"),
    ("R3", "r3-nischen", "Nischen", "Kampf/Geheim, 14 x 14 - neu: Mulde -0,5 m"),
    ("R4", "r4-saeulenhalle", "Säulenhalle", "Kampf, 16 x 14 - neu: 8 runde Säulen, Chor"),
    ("R5", "r5-engstelle", "Engstelle", "Kampf, 16 x 12 - neu: Steg über Grube -1 m"),
    ("R6", "r6-empore", "Empore", "Schatz, 12 x 14 - neu: Empore 1,5 m, 2 Treppen"),
]

CELL = 26
PANEL_W, PANEL_H = 600, 600
MARGIN = 28
HEAD_H = 96
LEGEND_H = 128
COLS = 3


def load_room(stem):
    neu = os.path.join(HERE, stem + ".neu.txt")
    path = neu if os.path.exists(neu) else os.path.join(HERE, stem + ".txt")
    rows = PR.load(path)
    w = max(len(r) for r in rows)
    g = [[(r[x] if x < len(r) else "#") for x in range(w)] for r in rows]
    return os.path.basename(path), g


def heights(g):
    d, w = len(g), len(g[0])
    h = [[PR.height_of(g[z][x]) if g[z][x] != "O" else 0.0 for x in range(w)] for z in range(d)]
    for z in range(d):
        for x in range(w):
            if g[z][x] == "C":
                nb = {h[z + j][x + i] for i, j in ((1, 0), (-1, 0), (0, 1), (0, -1))
                      if 0 <= x + i < w and 0 <= z + j < d and h[z + j][x + i] is not None and g[z + j][x + i] != "C"}
                if len(nb) == 1:
                    h[z][x] = nb.pop()
    return h


def fmt_h(v):
    if v == 0:
        return "0 m"
    s = f"{v:+.2f}".rstrip("0").rstrip(".").replace(".", ",")
    return s + " m"


def regions(g, h):
    """Zusammenhaengende Flaechen gleicher Hoehe (ohne 0 m) -> (Hoehe, Zellen)."""
    d, w = len(g), len(g[0])
    seen, out = set(), []
    for z in range(d):
        for x in range(w):
            v = h[z][x]
            if v is None or v == 0.0 or (x, z) in seen or g[z][x] in "+":
                continue
            comp, todo = [], [(x, z)]
            seen.add((x, z))
            while todo:
                cx, cz = todo.pop()
                comp.append((cx, cz))
                for nx, nz in ((cx + 1, cz), (cx - 1, cz), (cx, cz + 1), (cx, cz - 1)):
                    if 0 <= nx < w and 0 <= nz < d and (nx, nz) not in seen and h[nz][nx] == v and g[nz][nx] != "+":
                        seen.add((nx, nz))
                        todo.append((nx, nz))
            out.append((v, comp))
    return out


def wall_cell(c, x0, y0, n=CELL):
    """Wand: dunkel mit Schraffur, damit sie sich von einer tiefen Grube (flach dunkel) unterscheidet."""
    c.rect(x0, y0, x0 + n, y0 + n, WALL)
    for y in range(y0, y0 + n):
        for x in range(x0, x0 + n):
            if (x + y) % 7 == 0:
                c.set(x, y, STONE[3])


def draw_room(c, ox, oy, tag, stem, title, desc):
    fname, g = load_room(stem)
    h = heights(g)
    d, w = len(g), len(g[0])
    # Kopf: Kennung gross, Name daneben
    text(c, tag, ox, oy, 6, WERK)
    tx = ox + text_w(tag, 6) + 18
    text(c, title, tx, oy + 2, 3, WERK)
    text(c, fname, tx, oy + 30, 2, WERK_D, shadow=False)
    # Karte
    my = oy + 64
    mx = ox + (PANEL_W - 40 - w * CELL) // 2
    for z in range(d):
        for x in range(w):
            ch = g[z][x]
            x0, y0 = mx + x * CELL, my + z * CELL
            if ch in "# ":
                wall_cell(c, x0, y0)
                continue
            if ch == "+":
                wall_cell(c, x0, y0)
                # Tuer: Holzbalken quer zur Wand, mit Pfeil nach innen
                horiz = z in (0, d - 1)
                if horiz:
                    c.rect(x0 + 2, y0 + 7, x0 + CELL - 2, y0 + CELL - 7, WOOD[5])
                    c.rect(x0 + 2, y0 + 7, x0 + CELL - 2, y0 + 9, WOOD[7])
                else:
                    c.rect(x0 + 7, y0 + 2, x0 + CELL - 7, y0 + CELL - 2, WOOD[5])
                    c.rect(x0 + 7, y0 + 2, x0 + 9, y0 + CELL - 2, WOOD[7])
                continue
            col = floor_col(h[z][x])
            c.rect(x0, y0, x0 + CELL, y0 + CELL, col)
            # Rasterlinie: eine Stufe dunkler
            dark = floor_col(h[z][x] - 0.25)
            c.rect(x0, y0, x0 + CELL, y0 + 1, dark)
            c.rect(x0, y0, x0 + 1, y0 + CELL, dark)
            if ch == "O":
                c.disc(x0 + CELL / 2 + 1, y0 + CELL / 2 + 1, CELL * 0.23, SHADOW)
                c.disc(x0 + CELL / 2, y0 + CELL / 2, CELL * 0.23, STONE[2])
                c.disc(x0 + CELL / 2 - 2, y0 + CELL / 2 - 2, 2.2, STONE[5])
            if ch == "C":
                c.rect(x0 + 5, y0 + 7, x0 + CELL - 5, y0 + CELL - 5, BRASS[2])
                c.rect(x0 + 6, y0 + 8, x0 + CELL - 6, y0 + CELL - 6, BRASS[5])
                c.rect(x0 + 6, y0 + 12, x0 + CELL - 6, y0 + 14, BRASS[2])
    # Kanten zwischen Hoehen: dunkle Linie an der tieferen Seite (Absatz sichtbar)
    for z in range(d):
        for x in range(w):
            if h[z][x] is None or g[z][x] in "#+ ":
                continue
            for i, j in ((1, 0), (0, 1)):
                nx, nz = x + i, z + j
                if nx >= w or nz >= d or h[nz][nx] is None or g[nz][nx] in "#+ ":
                    continue
                diff = abs(h[z][x] - h[nz][nx])
                if diff < 0.01:
                    continue
                t = 1 if diff <= 0.4 else 3
                col = SHADOW if diff > 0.4 else STONE[3]
                if i:
                    xx = mx + nx * CELL
                    c.rect(xx - t // 2, my + z * CELL, xx - t // 2 + t, my + (z + 1) * CELL, col)
                else:
                    yy = my + nz * CELL
                    c.rect(mx + x * CELL, yy - t // 2, mx + (x + 1) * CELL, yy - t // 2 + t, col)
    # Hoehen beschriften: je Flaeche einmal, auf der Zelle nahe der Mitte
    # Gesucht wird die Zelle nahe der Flaechenmitte, auf der die Beschriftung ganz in der Flaeche liegt (erst gross,
    # dann klein); passt sie nirgends, bleibt die Flaeche unbeschriftet (die Farbe sagt es dann).
    for v, comp in regions(g, h):
        cells = set(comp)
        sx = sum(p[0] for p in comp) / len(comp)
        sz = sum(p[1] for p in comp) / len(comp)
        order = sorted(comp, key=lambda p: ((p[0] - sx) ** 2 + (p[1] - sz) ** 2, p))
        lab = fmt_h(v).replace(" m", "")
        col = SHADOW if v > 0.6 else WERK
        done = False
        for k in (2, 1):
            tw, th = text_w(lab, k), 7 * k
            for bx, bz in order:
                for shift in (0, CELL // 2, -(CELL // 2)):
                    cx = mx + bx * CELL + CELL // 2 + shift
                    cy = my + bz * CELL + CELL // 2
                    x0, y0, x1, y1 = cx - tw // 2 - 2, cy - th // 2 - 2, cx + tw // 2 + 2, cy + th // 2 + 2
                    cov = {(px, pz) for px in range((x0 - mx) // CELL, (x1 - 1 - mx) // CELL + 1)
                           for pz in range((y0 - my) // CELL, (y1 - 1 - my) // CELL + 1)}
                    if cov <= cells:
                        text(c, lab, cx, cy - th // 2, k, col, shadow=False, anchor="m")
                        done = True
                        break
                if done:
                    break
            if done:
                break
    c.frame(mx - 1, my - 1, mx + w * CELL + 1, my + d * CELL + 1, STONE[4])
    # Massstab: 5 Zellen = 10 m
    sy = my + 16 * CELL + 12
    for i in range(5):
        c.rect(mx + i * CELL, sy, mx + (i + 1) * CELL, sy + 6, WERK if i % 2 == 0 else STONE[6])
    text(c, "0", mx, sy + 10, 1, WERK_D, shadow=False, anchor="m")
    text(c, "10 m", mx + 5 * CELL, sy + 10, 1, WERK_D, shadow=False, anchor="m")
    text(c, "1 Zelle = 2 m", mx + 5 * CELL + 30, sy, 2, WERK_D, shadow=False)
    # Beschreibung
    text(c, desc, ox, sy + 34, 2, WERK)


def legend(c, x, y):
    items = [(f"{fmt_h(v)}", floor_col(v)) for v in (-1.0, -0.5, -0.25, 0.0, 0.25, 0.5, 0.75, 1.0, 1.5)]
    text(c, "Höhe (Stein-Rampe, je 0,25 m eine Stufe):", x, y, 2, WERK)
    xx = x
    for lab, col in items:
        c.rect(xx, y + 26, xx + 56, y + 46, col)
        c.frame(xx, y + 26, xx + 56, y + 46, STONE[4])
        text(c, lab, xx + 28, y + 52, 2, WERK_D, shadow=False, anchor="m")
        xx += 72
    # Zeichen
    xx += 30
    text(c, "Zeichen:", xx, y, 2, WERK)
    sym = xx
    wall_cell(c, sym, y + 26, 20)
    c.frame(sym, y + 26, sym + 20, y + 46, STONE[4])
    text(c, "Wand", sym + 28, y + 32, 2, WERK_D, shadow=False)
    sym += 110
    wall_cell(c, sym, y + 26, 20)
    c.rect(sym + 2, y + 33, sym + 18, y + 39, WOOD[5])
    text(c, "Türplatz", sym + 28, y + 32, 2, WERK_D, shadow=False)
    sym += 140
    c.rect(sym, y + 26, sym + 20, y + 46, floor_col(0))
    c.rect(sym + 3, y + 30, sym + 17, y + 43, BRASS[2])
    c.rect(sym + 4, y + 31, sym + 16, y + 42, BRASS[5])
    text(c, "Kiste C", sym + 28, y + 32, 2, WERK_D, shadow=False)
    sym += 130
    c.rect(sym, y + 26, sym + 20, y + 46, floor_col(0))
    c.disc(sym + 10, y + 36, 6, STONE[2])
    text(c, "runde Säule O (neu)", sym + 28, y + 32, 2, WERK_D, shadow=False)
    # Kanten
    text(c, "Linie dünn: Stufe bis 0,4 m (gehen)   Linie dick: Absatz über 0,4 m (Sprung oder Fall)",
         x, y + 84, 2, WERK_D, shadow=False)


def main():
    rows = (len(ROOMS) + COLS - 1) // COLS
    W = MARGIN * 2 + COLS * PANEL_W
    H = HEAD_H + rows * PANEL_H + LEGEND_H + MARGIN
    c = Cv(W, H, BG)
    text(c, "Raumdesign Runde 1 - Issue #215", MARGIN, 22, 4, WERK)
    text(c, "Draufsicht, Norden oben. Gezeigt ist die beabsichtigte Fassung (.neu.txt mit vorgeschlagenen Zeichen).",
         MARGIN, 66, 2, WERK_D, shadow=False)
    for i, room in enumerate(ROOMS):
        col, row = i % COLS, i // COLS
        ox = MARGIN + col * PANEL_W
        oy = HEAD_H + row * PANEL_H + 10
        draw_room(c, ox, oy, *room)
    c.rect(MARGIN, H - LEGEND_H - MARGIN + 2, W - MARGIN, H - LEGEND_H - MARGIN + 3, STONE[4])
    legend(c, MARGIN, H - LEGEND_H - MARGIN + 18)
    c.save(os.path.join(HERE, "blatt.png"))
    print(f"blatt.png {W} x {H}")


if __name__ == "__main__":
    main()
