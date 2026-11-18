"""Effekt-Probestueck: Muendungsfeuer der Pistole in drei Varianten (Silver & Ash, Issue #361).

Szene: das Foto `design/proben/grafik/hud-vorlagen/basis-854x480.png` (Ich-Sicht, Gang, Pistole rechts, ohne HUD).
Es ist bei allen drei Varianten dasselbe Bild und wird nicht beurteilt. Der Effekt liegt in Bildpunkten darueber:
Blitz, Funken und Rauch als harte Quadrate (keine Kantenglaettung, kein Leuchtschein), danach wird das ganze Bild
mit demselben 4 x 4-Punktmuster auf 15 Bit gerastert wie im Spiel (ADR 0010; ohne Effekt aendert sich kein Pixel).
Das Aufleuchten der Umgebung ist ein warmer Lichtzuschlag mit harten, im Muster gekoernten Stufen am Rand –
keine Kantenglaettung, kein Bloom. Er ist eine Annaeherung an ein Punktlicht an der Muendung (kein Tiefenpuffer).

    A  Sternblitz   - Blitz als Stern aus Spitzen (3 Bilder, 50 ms), 4 Funken, kleiner Rauchfaden, Licht kurz
    B  Funkenstoss  - winziger Blitz, 10 grobe Funken fliegen als Kegel nach vorn und fallen, kein Rauch, Licht 1 Bild
    C  Pulverdampf  - Blitz als flacher Polygon-Kegel, dicke Rauchwolke (10 Quadrate, 0,6 s), ohne Funken, Licht schwach,
                      beleuchtet auch den Rauch

Ausgabe in diesem Ordner: je Variante ein Bildstreifen (10 Einzelbilder, Ausschnitt um die Muendung, Balken = Helligkeit
des Aufleuchtens), ein GIF (volles Bild, Schuss, dann Pause), dazu das Blatt mit allen drei Streifen.

Werkzeug: das Python, das Blender mitbringt (mit NumPy); PNG und GIF schreibt `../fackel/fackel.py` (als Modul geladen).
Aufruf aus dem Repository-Wurzelverzeichnis:

    "<Blender-Ordner>\\5.3\\python\\bin\\python.exe" design/proben/effekte/muendungsfeuer/muendungsfeuer.py

Zeit in Spiel-Ticks (60 je Sekunde). Alle Zufallswerte haben festen Seed, jeder Lauf liefert dieselben Dateien.
"""

import math
import os
import struct
import sys
import zlib

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "design", "proben", "effekte", "fackel"))
import fackel as F  # noqa: E402  (PNG-/GIF-Schreiber, Schrift, Palette, Koernung)

W, H = F.W, F.H
GOLD, STONE = F.GOLD, F.STONE
WARM = F.WARM
THR = (F.BAYER[3 - (np.arange(H)[:, None] % 4), np.arange(W)[None, :] % 4] + 0.5) / 16.0

MUZZLE = (285, 491)            # Zeile, Spalte der Muendung im Bild (Laufende der Pistole)
DIR = np.array([-0.47, -0.88])  # (Zeile, Spalte): Mündungsrichtung im Bild, nach links und leicht nach oben
DIR = DIR / np.linalg.norm(DIR)
PERP = np.array([-DIR[1], DIR[0]])

TICKS_SHOT = 54                # Dauer des Streifens/GIF-Schusses in Ticks (0,9 s)
STRIP_TICKS = [0, 1, 2, 3, 4, 6, 9, 14, 22, 36]
PAUSE_CS = 90                  # Pause am Ende des GIF (Hundertstel s)


def load_png_rgb(path):
    """8-Bit-PNG mit RGB oder RGBA lesen (das Foto ist RGBA); Alpha wird verworfen."""
    data = open(path, "rb").read()
    pos, idat = 8, b""
    while pos < len(data):
        n, kind = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + n]
        if kind == b"IHDR":
            w, h, depth, ctype = struct.unpack(">IIBB", body[:10])
            assert depth == 8 and ctype in (2, 6)
        elif kind == b"IDAT":
            idat += body
        pos += 12 + n
    bpp = 3 if ctype == 2 else 4
    raw = zlib.decompress(idat)
    stride = w * bpp
    out = np.zeros((h, stride), np.int32)
    prev = np.zeros(stride, np.int32)
    for y in range(h):
        ft = raw[y * (stride + 1)]
        row = np.frombuffer(raw, np.uint8, stride, y * (stride + 1) + 1).astype(np.int32)
        cur = np.zeros(stride, np.int32)
        for x in range(stride):
            a = cur[x - bpp] if x >= bpp else 0
            b = prev[x]
            c = prev[x - bpp] if x >= bpp else 0
            p = (0, a, b, (a + b) // 2, None)[ft] if ft < 4 else None
            if p is None:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                p = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
            cur[x] = (row[x] + p) & 255
        out[y], prev = cur, cur
    return out.reshape(h, w, bpp)[..., :3].astype(float)


# --- Zeichenhilfen ------------------------------------------------------------------------------------------------


class Frame:
    """Ein Bild: Hintergrund, darauf Teilchen als harte Quadrate (Zeilen/Spalten in Bildpunkten)."""

    def __init__(self, base, light, light_centre, light_radius, aniso=1.0):
        self.img = base.copy()
        if light > 0:
            self.img = self.img + self._light(base, light, light_centre, light_radius, aniso)

    @staticmethod
    def _light(base, light, centre, radius, aniso):
        rr, cc = np.mgrid[0:H, 0:W]
        d = np.hypot((rr - centre[0]) * aniso, cc - centre[1]) / radius
        fall = np.clip(1 - d, 0, 1) ** 2
        fall = np.floor(fall * 8 + 0.5) / 8        # grobe Stufen, im Punktmuster mitgekoernt (kein weicher Verlauf)
        return base * (light * fall)[..., None] * WARM[None, None, :] * 1.0

    def px(self, row, col, size, colour):
        """Quadrat mit Seitenlaenge size (Bildpunkte), Mitte bei (row, col)."""
        s = max(1, int(round(size)))
        r0, c0 = int(round(row - s / 2)), int(round(col - s / 2))
        r1, c1, r2, c2 = max(r0, 0), max(c0, 0), min(r0 + s, H), min(c0 + s, W)
        if r2 > r1 and c2 > c1:
            self.img[r1:r2, c1:c2] = colour

    def sprite(self, rows, cols_unused, texels, texel_px, palette, anchor):
        """Gemaltes Bild: texels[y][x] = Farbindex (-1 durchsichtig), anchor = (Zeile, Spalte) der Texel-Mitte unten."""
        th, tw = texels.shape
        top = int(round(anchor[0] - th * texel_px / 2))
        left = int(round(anchor[1] - tw * texel_px / 2))
        for y in range(th):
            for x in range(tw):
                k = texels[y, x]
                if k >= 0:
                    r, c = top + y * texel_px, left + x * texel_px
                    r1, c1, r2, c2 = max(r, 0), max(c, 0), min(r + texel_px, H), min(c + texel_px, W)
                    if r2 > r1 and c2 > c1:
                        self.img[r1:r2, c1:c2] = palette[k]

    def polygon(self, pts, colour):
        scr = np.array(pts, float)
        r1, r2 = max(int(scr[:, 0].min()), 0), min(int(scr[:, 0].max()) + 2, H)
        c1, c2 = max(int(scr[:, 1].min()), 0), min(int(scr[:, 1].max()) + 2, W)
        rr, cc = np.meshgrid(np.arange(r1, r2) + 0.5, np.arange(c1, c2) + 0.5, indexing="ij")
        inside = np.zeros(rr.shape, bool)
        n = len(scr)
        for i in range(n):
            (ra, ca), (rb, cb) = scr[i], scr[(i + 1) % n]
            cross = (ra > rr) != (rb > rr)
            xint = ca + (rr - ra) * (cb - ca) / np.where(rb == ra, 1e-9, rb - ra)
            inside ^= cross & (cc < xint)
        self.img[r1:r2, c1:c2][inside] = colour

    def result(self):
        return F.quantize(self.img, THR)


def at(dist, side=0.0):
    """Bildpunkt vor der Muendung: dist Pixel in Schussrichtung, side Pixel seitlich."""
    p = np.array(MUZZLE, float) + DIR * dist + PERP * side
    return p[0], p[1]


# --- Variante A: Sternblitz (Daumenkino) --------------------------------------------------------------------------

A_LIGHT = {0: 0.80, 1: 0.55, 2: 0.28, 3: 0.10}              # Aufleuchten je Tick (Anteil Zuschlag in der Mitte)
A_SPARK = np.random.default_rng(361).uniform(0, 1, (4, 4))


def spike(frame, angle, length, width, colours, start=2):
    """Eine Spitze aus aneinandergereihten Quadraten, vorn schmaler und dunkler; angle in Radiant zur Schussrichtung."""
    axis = DIR * math.cos(angle) + PERP * math.sin(angle)
    n = max(2, int(length / 3))
    for i in range(n):
        f = i / n
        p = np.array(MUZZLE, float) + DIR * start + axis * (i * 3)
        w = max(3, width * (1 - f) ** 0.8)
        frame.px(p[0], p[1], w, colours[0] if f < 0.35 else colours[1] if f < 0.7 else colours[2])


A_STARS = [  # je Tick: (Winkel in Grad, Laenge, Breite) der Spitzen
    [(0, 66, 14), (-34, 44, 9), (34, 44, 9), (-70, 26, 7), (70, 26, 7)],
    [(0, 44, 12), (-40, 26, 8), (40, 26, 8)],
    [(0, 24, 8), (-45, 12, 6), (45, 12, 6)],
]


def variant_a(base, tick):
    light = A_LIGHT.get(tick, 0.0)
    fr = Frame(base, light, at(34), 150, aniso=1.0)
    # Rauchfaden: 3 flache graue Quadrate, steigen leicht (Ticks 3..30)
    if 3 <= tick <= 30:
        a = (tick - 3) / 27
        for k in range(3):
            p = at(26 + 22 * k + 34 * a, 4 * math.sin(k + a * 3) - 12 * a)
            col = STONE[11] if a < 0.4 else STONE[9] if a < 0.75 else STONE[7]
            fr.px(p[0] - 18 * a, p[1], 8 + 5 * a + 2 * k, col)
    # Funken: 4 Stueck, fliegen in 0,25 s nach vorn und fallen
    for k in range(4):
        r = A_SPARK[k]
        life = 14 + int(8 * r[0])
        if 1 <= tick <= life:
            a = tick / life
            dist = 14 + (60 + 70 * r[1]) * a
            drop = 90 * a * a * (0.6 + r[2])
            p = at(dist, (r[3] - 0.5) * 50 * a)
            col = GOLD[7] if a < 0.4 else GOLD[6] if a < 0.7 else GOLD[5]
            fr.px(p[0] + drop, p[1], 4 if a < 0.6 else 3, col)
    # Blitz
    if tick <= 2:
        for ang, ln, wd in A_STARS[tick]:
            spike(fr, math.radians(ang), ln, wd, [GOLD[7], GOLD[6], GOLD[5]])
        fr.px(*at(6), 12 if tick == 0 else 8, GOLD[7])
    return fr.result(), light


# --- Variante B: Funkenstoss --------------------------------------------------------------------------------------

B_RNG = np.random.default_rng(362)
B_SPARK = B_RNG.uniform(0, 1, (10, 5))


def variant_b(base, tick):
    light = 0.75 if tick == 0 else 0.25 if tick == 1 else 0.0
    fr = Frame(base, light, at(30), 130)
    for k in range(10):
        r = B_SPARK[k]
        life = 16 + int(14 * r[0])
        if tick <= life:
            a = tick / life
            ang = (r[1] - 0.5) * 0.75                     # Kegel von etwa +-21 Grad
            speed = 150 + 170 * r[2]
            dist = 6 + speed * (1 - (1 - a) ** 2) * 0.5
            side = math.tan(ang) * dist
            drop = 120 * a * a * (0.5 + r[3])
            p = at(dist, side)
            col = GOLD[7] if a < 0.3 else GOLD[6] if a < 0.55 else GOLD[5] if a < 0.8 else GOLD[3]
            size = 5 if a < 0.35 else 4 if a < 0.7 else 3
            fr.px(p[0] + drop, p[1], size, col)
    if tick <= 1:                                         # winziger Blitz: ein harter Kern
        s = 12 if tick == 0 else 7
        fr.px(*at(5), s, GOLD[7])
        fr.px(*at(5), s - 6 if tick == 0 else 2, GOLD[6])
    return fr.result(), light


# --- Variante C: Pulverdampf --------------------------------------------------------------------------------------

C_RNG = np.random.default_rng(363)
C_PUFF = C_RNG.uniform(0, 1, (10, 4))
C_LIGHT = {0: 0.50, 1: 0.38, 2: 0.18, 3: 0.06}


def variant_c(base, tick):
    light = C_LIGHT.get(tick, 0.0)
    fr = Frame(base, light, at(30), 140)
    life = 36
    for k in range(10):
        r = C_PUFF[k]
        start = int(5 * r[0])                              # Wolke quillt in den ersten 5 Ticks
        if tick < start:
            continue
        a = min((tick - start) / life, 1.0)
        if a >= 1.0:
            continue
        dist = 8 + (34 + 70 * r[1]) * (1 - (1 - min(a * 2, 1)) ** 2) + 12 * a
        side = (r[2] - 0.5) * 70 * min(a * 2, 1)
        rise = -26 * a * (0.5 + r[3])                      # steigt: Bildzeile nimmt ab
        p = at(dist, side)
        size = 12 + 22 * min(a * 2.2, 1) * (0.6 + 0.6 * r[3])
        if tick <= 2 and a < 0.2:
            col = GOLD[6] if k % 2 else GOLD[5]            # frischer Rauch: vom Blitz gold gefaerbt
        else:
            col = STONE[12] if a < 0.3 else STONE[10] if a < 0.6 else STONE[8] if a < 0.85 else STONE[6]
        fr.px(p[0] + rise, p[1], size, col)
    if tick <= 2:                                          # Blitz: breiter flacher Kegel aus zwei Polygonen
        k = (1.0, 0.65, 0.35)[tick]
        o = np.array(MUZZLE, float) + DIR * 2
        for scale, colour in ((1.0, GOLD[6]), (0.6, GOLD[7])):
            L, Wd = 62 * k * scale, 22 * k * scale
            pts = [o + PERP * 7 * scale, o + DIR * L * 0.55 + PERP * Wd, o + DIR * L,
                   o + DIR * L * 0.55 - PERP * Wd, o - PERP * 7 * scale]
            fr.polygon([tuple(q) for q in pts], colour)
    return fr.result(), light


# --- Streifen, Blatt, GIF -----------------------------------------------------------------------------------------

CROP_W, CROP_H = 260, 210
GAP = 4
BG, INK, BAR = F.FOG_COLOR, F.STONE[14], F.GOLD[6]
LABELS = {
    "A": "A  STERNBLITZ: STERN AUS SPITZEN (3 BILDER), 4 FUNKEN, RAUCHFADEN, LICHT 4 BILDER",
    "B": "B  FUNKENSTOSS: WINZIGER BLITZ, 10 GROBE FUNKEN ALS KEGEL, KEIN RAUCH, LICHT 1 BILD",
    "C": "C  PULVERDAMPF: BLITZ ALS KEGEL, DICKE RAUCHWOLKE (10 QUADRATE), KEINE FUNKEN",
}


def strip_block(name, frames_by_tick, lights):
    top = MUZZLE[0] - int(CROP_H * 0.60)
    left = MUZZLE[1] - int(CROP_W * 0.80)
    head, bar_h, foot = 36, 36, 22
    width = len(STRIP_TICKS) * (CROP_W + GAP) + GAP
    block = np.tile(BG, (head + CROP_H + bar_h + foot, width, 1))
    F.text(block, LABELS[name], 8, GAP, 3, INK)
    for n, tick in enumerate(STRIP_TICKS):
        x = GAP + n * (CROP_W + GAP)
        block[head:head + CROP_H, x:x + CROP_W] = frames_by_tick[tick][top:top + CROP_H, left:left + CROP_W]
        h = int(round(lights[tick] / 0.9 * (bar_h - 8)))
        if h > 0:
            block[head + CROP_H + bar_h - 2 - h:head + CROP_H + bar_h - 2, x + CROP_W // 2 - 20:x + CROP_W // 2 + 20] = BAR
        F.text(block, f"{tick * 1000 // 60} MS", head + CROP_H + bar_h + 4, x + 4, 2, INK)
    return block


def save_gif_delays(frames, delays, path):
    """Wie F.save_gif, aber mit eigener Zeit je Bild (Hundertstel s); letztes Bild = Pause."""
    h, w = frames[0].shape[:2]
    out = bytearray(b"GIF89a" + struct.pack("<HHBBB", w, h, 0x70, 0, 0))
    out += b"\x21\xff\x0bNETSCAPE2.0\x03\x01\x00\x00\x00"
    prev = None
    for fr, delay in zip(frames, delays):
        key = (fr[..., 0].astype(np.int64) << 16) | (fr[..., 1].astype(np.int64) << 8) | fr[..., 2]
        if prev is None:
            y0, y1, x0, x1 = 0, h, 0, w
            same = np.zeros((h, w), bool)
        else:
            diff = key != prev
            if not diff.any():
                diff[0, 0] = True
            ys, xs = np.nonzero(diff)
            y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
            same = ~diff
        sub, sub_same = key[y0:y1, x0:x1], same[y0:y1, x0:x1]
        cols, inv, counts = np.unique(sub[~sub_same], return_inverse=True, return_counts=True)
        if len(cols) > 255:
            keep = np.argsort(-counts)[:255]
            rgb = np.stack([(cols >> 16) & 255, (cols >> 8) & 255, cols & 255], 1).astype(float)
            d = ((rgb[:, None, :] - rgb[keep][None, :, :]) ** 2).sum(2)
            inv = np.argmin(d, 1)[inv]
            cols = cols[keep]
        idx = np.full(sub.shape, len(cols), np.int64)
        idx[~sub_same] = inv
        bits = max(2, int(math.ceil(math.log2(len(cols) + 1))))
        table = np.zeros((1 << bits, 3), np.uint8)
        table[:len(cols)] = np.stack([(cols >> 16) & 255, (cols >> 8) & 255, cols & 255], 1)
        out += b"\x21\xf9\x04" + struct.pack("<BHBB", 0x05, int(delay), len(cols), 0)
        out += b"\x2c" + struct.pack("<HHHHB", x0, y0, x1 - x0, y1 - y0, 0x80 | (bits - 1)) + table.tobytes()
        data = F.lzw(idx.ravel().tolist(), bits)
        out.append(bits)
        for i in range(0, len(data), 255):
            out.append(len(data[i:i + 255]))
            out += data[i:i + 255]
        out.append(0)
        prev = key
    out.append(0x3b)
    with open(path, "wb") as f:
        f.write(out)
    print("GIF:", os.path.basename(path), f"({len(out) // 1024} KB)")


def main():
    base = load_png_rgb(os.path.join(ROOT, "design", "proben", "grafik", "hud-vorlagen", "basis-854x480.png"))
    variants = {"A": variant_a, "B": variant_b, "C": variant_c}
    blocks = []
    for name, fn in variants.items():
        by_tick, lights = {}, {}
        for tick in range(TICKS_SHOT):
            by_tick[tick], lights[tick] = fn(base, tick)
        block = strip_block(name, by_tick, lights)
        F.save_png(block, os.path.join(HERE, f"muendungsfeuer-{name}-streifen.png"))
        gif_ticks = list(range(0, TICKS_SHOT, 2))           # 30 Bilder je Sekunde
        frames = [by_tick[t] for t in gif_ticks]
        frames.append(by_tick[TICKS_SHOT - 1])             # Pause: Bild ohne Effekt
        save_gif_delays(frames, [3] * len(gif_ticks) + [PAUSE_CS], os.path.join(HERE, f"muendungsfeuer-{name}.gif"))
        # Einzelbilder voll fuer die Selbstkontrolle der Tick 0 bis 3 gehoeren nicht ins Repository
        blocks.append(block)
    title = np.tile(BG, (44, blocks[0].shape[1], 1))
    F.text(title, "MUENDUNGSFEUER - DREI VARIANTEN, NUR DER EFFEKT AENDERT SICH (ISSUE #361).  BALKEN = HELLIGKEIT DES "
                  "AUFLEUCHTENS, ZEIT NACH DEM SCHUSS", 10, GAP, 3, INK)
    sep = np.tile(BG, (12, blocks[0].shape[1], 1))
    sheet = np.concatenate([title] + [x for b in blocks for x in (b, sep)], 0)
    F.save_png(sheet, os.path.join(HERE, "muendungsfeuer-blatt.png"))


if __name__ == "__main__":
    main()
