"""Effekt-Probestueck: brennende Fackel in drei Varianten (Silver & Ash, Issue #353).

Dieselbe Szene fuer alle drei Varianten: Fackel-Platzhalter an der Katakombenwand (Textur aus assets/textures),
Kamera auf Augenhoehe 1,6 m, 70 Grad senkrechter Blickwinkel, 854 x 480, Laterne und Nebel wie in level.fs,
am Ende 15 Bit mit 4 x 4-Punktmuster (ADR 0010). Es aendert sich nur der Effekt: Flamme, Funken, Flackerlicht.

    A  Daumenkino     - gemalte Flammen-Textur (5 Bilder, 10 je Sekunde), ruhig atmendes Licht, 3 Funken
    B  Teilchenflamme - 12 grobe Quadrat-Teilchen, 6 Funken, unruhiges Licht, dessen Mitte zuckt
    C  Polygonflamme  - drei flache Polygon-Schichten mit springenden Ecken, Licht in harten Stufenringen,
                        8 langsame Glutfunken

Ausgabe in diesem Ordner: je Variante ein Bildstreifen (10 Einzelbilder einer Sekunde, Ausschnitt um die Fackel)
und ein GIF (20 Bilder, 1 s, nahtlose Schleife, volles Bild), dazu das Blatt mit allen drei Streifen.

Werkzeug: das Python, das Blender mitbringt (mit NumPy); PNG und GIF schreibt dieses Skript selbst (zlib, LZW).
Kein Blender-Rendering, kein weiteres Paket, nichts heruntergeladen. Aufruf aus dem Repository-Wurzelverzeichnis:

    "<Blender-Ordner>\\5.3\\python\\bin\\python.exe" design/proben/effekte/fackel/fackel.py

Alle Bewegungen sind periodisch ueber genau eine Sekunde, deshalb laeuft das GIF ohne Sprung.
"""

import json
import math
import os
import struct
import zlib

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))

# --- Rahmen wie im Spiel (LookSettings.cs, level.fs) -------------------------------------------------------------

W, H = 854, 480
FOV_V = math.radians(70.0)
TAN = math.tan(FOV_V / 2)
EYE = np.array([0.0, 1.6, 0.0])
FOG_COLOR = np.array([16.0, 11.0, 20.0])
FOG_START, FOG_END, LANTERN_MIN = 3.25, 18.2, 0.35
WALL_BRIGHT, FLOOR_BRIGHT, CEIL_BRIGHT = 0.82, 0.9, 0.55   # Wand mit Blick nach +z, Boden, Decke
CEILING_Y = 3.5
WALL_Z = -2.4

FPS = 20           # Bilder je Sekunde im GIF; der Streifen nimmt jedes zweite
FRAMES = 20
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], float)

# --- Palette ------------------------------------------------------------------------------------------------------

with open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8") as f:
    _pal = json.load(f)
ROWS = {r["name"]: r["farben"] for r in _pal["grund"] + _pal["akzent"]}
GOLD = [np.array(c, float) for c in ROWS["Messing/Gold"]]   # 0 dunkel .. 7 hell
WOOD = [np.array(c, float) for c in ROWS["Holz"]]
IRON = [np.array(c, float) for c in ROWS["Eisen"]]
STONE = [np.array(c, float) for c in ROWS["Stein"]]
WARM = GOLD[5] / GOLD[5].max()                             # Lichtfarbe der Fackel: Messing 5, auf 1 normiert

# --- PNG lesen und schreiben (nur was hier gebraucht wird) -----------------------------------------------------------


def load_png_rgb(path):
    """8-Bit-RGB-PNG ohne Interlacing lesen (so sind die Level-Texturen gespeichert)."""
    data = open(path, "rb").read()
    pos, idat, w = 8, b"", 0
    while pos < len(data):
        n, kind = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + n]
        if kind == b"IHDR":
            w, h, depth, ctype = struct.unpack(">IIBB", body[:10])
            assert depth == 8 and ctype == 2, "nur 8-Bit-RGB"
        elif kind == b"IDAT":
            idat += body
        pos += 12 + n
    raw = zlib.decompress(idat)
    bpp, stride = 3, w * 3
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
            if ft == 0:
                p = 0
            elif ft == 1:
                p = a
            elif ft == 2:
                p = b
            elif ft == 3:
                p = (a + b) // 2
            else:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                p = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
            cur[x] = (row[x] + p) & 255
        out[y], prev = cur, cur
    return out.reshape(h, w, 3).astype(float)


def save_png(arr, path):
    a = np.clip(np.rint(arr), 0, 255).astype(np.uint8)
    h, w = a.shape[:2]
    raw = b"".join(b"\x00" + a[y].tobytes() for y in range(h))

    def chunk(kind, body):
        return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xffffffff)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)
    print("Bild:", os.path.basename(path))


# --- GIF schreiben (LZW, je Bild eigene Farbtabelle, nur geaenderte Pixel) ------------------------------------------


def lzw(indices, min_size):
    clear, eoi = 1 << min_size, (1 << min_size) + 1
    size, nxt = min_size + 1, eoi + 1
    table = {}
    out = bytearray()
    buf = nbits = 0

    def emit(code):
        nonlocal buf, nbits
        buf |= code << nbits
        nbits += size
        while nbits >= 8:
            out.append(buf & 255)
            buf >>= 8
            nbits -= 8

    emit(clear)
    it = iter(indices)
    prefix = next(it)
    for b in it:
        key = (prefix << 8) | b
        code = table.get(key)
        if code is not None:
            prefix = code
            continue
        emit(prefix)
        if nxt < 4095:
            table[key] = nxt
            nxt += 1
            if nxt > (1 << size) and size < 12:
                size += 1
        else:
            emit(clear)
            table.clear()
            size, nxt = min_size + 1, eoi + 1
        prefix = b
    emit(prefix)
    emit(eoi)
    if nbits:
        out.append(buf & 255)
    return bytes(out)


def save_gif(frames, path, delay_cs):
    """frames: Liste von H x W x 3 (0..255, ganzzahlig). Bild 0 voll, danach nur das Rechteck mit Aenderungen;
    unveraenderte Pixel darin sind durchsichtig. Laeuft endlos (NETSCAPE-Block)."""
    h, w = frames[0].shape[:2]
    out = bytearray(b"GIF89a" + struct.pack("<HHBBB", w, h, 0x70, 0, 0))
    out += b"\x21\xff\x0bNETSCAPE2.0\x03\x01\x00\x00\x00"
    prev = None
    worst = 0
    for fr in frames:
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
            # Mehr als 255 Farben: die haeufigsten 255 behalten, den Rest auf die naechste davon legen.
            keep = np.argsort(-counts)[:255]
            rgb = np.stack([(cols >> 16) & 255, (cols >> 8) & 255, cols & 255], 1).astype(float)
            d = ((rgb[:, None, :] - rgb[keep][None, :, :]) ** 2).sum(2)
            remap = np.argmin(d, 1)
            inv = remap[inv]
            cols = cols[keep]
        worst = max(worst, len(cols))
        idx = np.full(sub.shape, len(cols), np.int64)     # Index len(cols) = durchsichtig
        idx[~sub_same] = inv
        bits = max(2, int(math.ceil(math.log2(len(cols) + 1))))
        table = np.zeros((1 << bits, 3), np.uint8)
        table[:len(cols)] = np.stack([(cols >> 16) & 255, (cols >> 8) & 255, cols & 255], 1)
        out += b"\x21\xf9\x04" + struct.pack("<BHBB", 0x05, delay_cs, len(cols), 0)   # nicht entsorgen, durchsichtig
        out += b"\x2c" + struct.pack("<HHHHB", x0, y0, x1 - x0, y1 - y0, 0x80 | (bits - 1)) + table.tobytes()
        data = lzw(idx.ravel().tolist(), bits)
        out.append(bits)
        for i in range(0, len(data), 255):
            part = data[i:i + 255]
            out.append(len(part))
            out += part
        out.append(0)
        prev = key
    out.append(0x3b)
    with open(path, "wb") as f:
        f.write(out)
    print("GIF:", os.path.basename(path), f"({len(out) // 1024} KB, hoechstens {worst} Farben je Bild)")


# --- Kamera -------------------------------------------------------------------------------------------------------

YAW, PITCH = math.radians(20.0), math.radians(-4.0)
_fwd = np.array([-math.sin(YAW), 0.0, -math.cos(YAW)])
RIGHT = np.array([math.cos(YAW), 0.0, -math.sin(YAW)])
FWD = _fwd * math.cos(PITCH) + np.array([0, 1.0, 0]) * math.sin(PITCH)
UP = np.cross(RIGHT, FWD)
PX_PER_UNIT = H / (2 * TAN)   # Bildpunkte je Meter in 1 m Tiefe


def rays():
    jj, ii = np.meshgrid(np.arange(W) + 0.5, np.arange(H) + 0.5)
    sx = (2 * jj / W - 1) * TAN * W / H
    sy = (1 - 2 * ii / H) * TAN
    d = FWD[None, None] + sx[..., None] * RIGHT + sy[..., None] * UP
    return d / np.linalg.norm(d, axis=2, keepdims=True)


def project(p):
    """Weltpunkt -> (Zeile, Spalte, Tiefe entlang der Blickrichtung, Abstand)."""
    v = np.asarray(p, float) - EYE
    z = v @ FWD
    x, y = (v @ RIGHT) / z, (v @ UP) / z
    col = (x / (TAN * W / H) + 1) / 2 * W
    row = (1 - y / TAN) / 2 * H
    return row, col, z, float(np.linalg.norm(v))


# --- Szene: Wand, Boden, Decke, Fackel-Platzhalter ----------------------------------------------------------------

TORCH_X = -0.62
TILT = math.radians(24.0)
AXIS = np.array([0.0, math.cos(TILT), math.sin(TILT)])       # Stiel lehnt oben von der Wand weg
BASE = np.array([TORCH_X, 1.38, WALL_Z + 0.07])
HANDLE_LEN = 0.44
FLAME_BASE = BASE + AXIS * (HANDLE_LEN + 0.09)               # oben auf dem Kopf der Fackel


def tilted_frame():
    x = np.array([1.0, 0, 0])
    y = AXIS
    z = np.cross(x, y)
    return np.stack([x, y, z], 1)                            # Spalten = lokale Achsen in Weltkoordinaten


BOXES = [  # (Mitte, Drehung, halbe Kantenlaengen, Farbe) – Platzhalter, wird nicht beurteilt
    (np.array([TORCH_X, 1.47, WALL_Z + 0.012]), np.eye(3), np.array([0.045, 0.10, 0.012]), IRON[3]),     # Wandplatte
    (np.array([TORCH_X, 1.50, WALL_Z + 0.06]), np.eye(3), np.array([0.012, 0.012, 0.05]), IRON[3]),      # Arm
    (BASE + AXIS * 0.13, tilted_frame(), np.array([0.042, 0.016, 0.042]), IRON[4]),                     # Ring
    (BASE + AXIS * (HANDLE_LEN / 2), tilted_frame(), np.array([0.024, HANDLE_LEN / 2, 0.024]), WOOD[3]),  # Stiel
    (BASE + AXIS * (HANDLE_LEN + 0.05), tilted_frame(), np.array([0.034, 0.05, 0.034]), WOOD[1]),       # Kopf
]


def face_brightness(n):
    """Helligkeit je Flaechenrichtung wie LookSettings (Boden 0,9 oben, Waende nach Richtung)."""
    b = np.full(n.shape[:-1], 0.72)
    ax = np.argmax(np.abs(n), -1)
    s = np.take_along_axis(n, ax[..., None], -1)[..., 0] > 0
    b = np.where((ax == 1) & s, 1.0, b)
    b = np.where((ax == 1) & ~s, 0.5, b)
    b = np.where((ax == 0) & s, 1.0, b)
    b = np.where((ax == 0) & ~s, 0.62, b)
    b = np.where((ax == 2) & s, 0.82, b)
    return b


def build_scene():
    d = rays()
    tex = {k: load_png_rgb(os.path.join(ROOT, "assets", "textures", f"catacomb_{k}.png"))
           for k in ("wall", "floor", "ceiling")}
    inf = np.full((H, W), np.inf)
    t_wall = np.where(d[..., 2] < 0, (WALL_Z - EYE[2]) / np.minimum(d[..., 2], -1e-9), inf)
    t_floor = np.where(d[..., 1] < 0, -EYE[1] / np.minimum(d[..., 1], -1e-9), inf)
    t_ceil = np.where(d[..., 1] > 0, (CEILING_Y - EYE[1]) / np.maximum(d[..., 1], 1e-9), inf)
    t = np.minimum(np.minimum(t_wall, t_floor), t_ceil)
    p = EYE + d * t[..., None]
    albedo = np.zeros((H, W, 3))
    normal = np.zeros((H, W, 3))
    bright = np.zeros((H, W))

    def sample(img, u, v):
        tx = np.floor(u * 64).astype(int) % 64
        ty = np.floor(v * 64).astype(int) % 64
        return img[ty, tx]

    m = t == t_wall
    albedo[m] = sample(tex["wall"], p[m][:, 0], -p[m][:, 1])
    normal[m] = (0, 0, 1)
    bright[m] = WALL_BRIGHT
    m = (t == t_floor) & ~(t == t_wall)
    albedo[m] = sample(tex["floor"], p[m][:, 0], p[m][:, 2])
    normal[m] = (0, 1, 0)
    bright[m] = FLOOR_BRIGHT
    m = (t == t_ceil) & ~(t == t_wall) & ~(t == t_floor)
    albedo[m] = sample(tex["ceiling"], p[m][:, 0], p[m][:, 2])
    normal[m] = (0, -1, 0)
    bright[m] = CEIL_BRIGHT

    for centre, rot, half, colour in BOXES:            # Strahl gegen gedrehten Quader
        o = (EYE - centre) @ rot
        dl = d @ rot
        dl = np.where(np.abs(dl) < 1e-9, 1e-9, dl)
        t1, t2 = (-half - o) / dl, (half - o) / dl
        lo, hi = np.minimum(t1, t2), np.maximum(t1, t2)
        tmin, tmax = lo.max(-1), hi.min(-1)
        hit = (tmax >= np.maximum(tmin, 0)) & (tmin > 0) & (tmin < t)
        ax = np.argmax(lo, -1)
        nl = np.zeros((H, W, 3))
        np.put_along_axis(nl, ax[..., None], -np.sign(np.take_along_axis(dl, ax[..., None], -1)), -1)
        nw = nl @ rot.T
        t = np.where(hit, tmin, t)
        p[hit] = EYE + d[hit] * tmin[hit][:, None]
        albedo[hit] = colour
        normal[hit] = nw[hit]
        bright[hit] = face_brightness(nw[hit])

    lantern = 1 - (1 - LANTERN_MIN) * np.clip(t / FOG_END, 0, 1)
    x = np.clip((t - FOG_START) / (FOG_END - FOG_START), 0, 1)
    fog = x * x * (3 - 2 * x)
    rr = np.arange(H)[:, None] % 4
    cc = np.arange(W)[None, :] % 4
    thr = (BAYER[3 - rr, cc] + 0.5) / 16.0             # Bildzeile y bekommt Musterzeile 3 - y % 4 (level.fs)
    return dict(p=p, n=normal, albedo=albedo, bright=bright, lantern=lantern, fog=fog, depth=t, thr=thr)


def torch_light(scene, pos, intensity, radius=3.6, strength=1.9, bands=0):
    """Beitrag der Fackel je Pixel (ohne Farbe). bands > 0: harte Stufenringe statt weichem Abfall."""
    v = pos - scene["p"]
    dist = np.linalg.norm(v, axis=2)
    ndl = np.clip((scene["n"] * v).sum(2) / np.maximum(dist, 1e-6), 0, 1)
    fall = np.clip(1 - dist / radius, 0, 1) ** 2 * (0.35 + 0.65 * ndl)
    if bands:
        fall = np.floor(fall * bands + 0.55) / bands
    return strength * intensity * fall


def shade(scene, light):
    lit = scene["albedo"] * (scene["bright"] * scene["lantern"])[..., None] + \
        scene["albedo"] * light[..., None] * WARM
    return lit * (1 - scene["fog"][..., None]) + FOG_COLOR * scene["fog"][..., None]


def quantize(img, thr):
    q = np.clip(np.floor(np.clip(img / 255.0, 0, 1) * 31 + thr[..., None]), 0, 31) / 31 * 255
    return np.rint(q).astype(np.int64)


# --- Zeichnen von Teilchen (harte Bildpunkte, Tiefentest gegen die Szene) -----------------------------------------


class Canvas:
    def __init__(self, scene, light):
        self.img = shade(scene, light)
        self.depth = scene["depth"].copy()

    def square(self, world, size_m, colour, min_px=1):
        row, col, z, dist = project(world)
        if z <= 0.05:
            return
        s = max(min_px, int(round(size_m / z * PX_PER_UNIT)))
        r0, c0 = int(math.floor(row - s / 2 + 0.5)), int(math.floor(col - s / 2 + 0.5))
        r1, c1 = max(r0, 0), max(c0, 0)
        r2, c2 = min(r0 + s, H), min(c0 + s, W)
        if r2 <= r1 or c2 <= c1:
            return
        m = self.depth[r1:r2, c1:c2] > dist
        self.img[r1:r2, c1:c2][m] = colour

    def sprite(self, base, texels, texel_m, palette):
        """Billboard-Sprite: texels[y, x] = Farbindex (y = 0 unten, -1 durchsichtig), Fuss mittig auf base."""
        th, tw = texels.shape
        row, col, z, dist = project(base)
        sc = texel_m / z * PX_PER_UNIT                  # Bildpunkte je Texel
        left, bottom = col - tw / 2 * sc, row
        r1, r2 = max(int(math.floor(bottom - th * sc)), 0), min(int(math.ceil(bottom)), H)
        c1, c2 = max(int(math.floor(left)), 0), min(int(math.ceil(left + tw * sc)), W)
        rr, cc = np.meshgrid(np.arange(r1, r2) + 0.5, np.arange(c1, c2) + 0.5, indexing="ij")
        tx = np.floor((cc - left) / sc).astype(int)
        ty = np.floor((bottom - rr) / sc).astype(int)
        ok = (tx >= 0) & (tx < tw) & (ty >= 0) & (ty < th)
        idx = np.full(rr.shape, -1)
        idx[ok] = texels[ty[ok], tx[ok]]
        m = (idx >= 0) & (self.depth[r1:r2, c1:c2] > dist)
        region = self.img[r1:r2, c1:c2]
        region[m] = np.array(palette)[idx[m]]

    def polygon(self, base, pts_m, colour):
        """Flaches Polygon zur Kamera gedreht; pts_m in Metern (x rechts, y oben) relativ zu base."""
        _, _, z, dist = project(base)
        scr = []
        for x, y in pts_m:
            r, c, _, _ = project(base + RIGHT * x + np.array([0, 1.0, 0]) * y)
            scr.append((r, c))
        scr = np.array(scr)
        r1, r2 = max(int(scr[:, 0].min()), 0), min(int(scr[:, 0].max()) + 2, H)
        c1, c2 = max(int(scr[:, 1].min()), 0), min(int(scr[:, 1].max()) + 2, W)
        rr, cc = np.meshgrid(np.arange(r1, r2) + 0.5, np.arange(c1, c2) + 0.5, indexing="ij")
        inside = np.zeros(rr.shape, bool)
        n = len(scr)
        for i in range(n):                              # gerade-ungerade-Regel
            (ra, ca), (rb, cb) = scr[i], scr[(i + 1) % n]
            cross = (ra > rr) != (rb > rr)
            xint = ca + (rr - ra) * (cb - ca) / np.where(rb == ra, 1e-9, rb - ra)
            inside ^= cross & (cc < xint)
        m = inside & (self.depth[r1:r2, c1:c2] > dist)
        self.img[r1:r2, c1:c2][m] = colour


def loop_noise(seed, steps):
    """steps Zufallswerte in -1..1, als Schleife ueber eine Sekunde."""
    return np.random.default_rng(seed).uniform(-1, 1, steps)


def sample_loop(values, t, smooth=True):
    n = len(values)
    x = t * n
    i = int(math.floor(x)) % n
    if not smooth:
        return values[i]
    f = x - math.floor(x)
    f = f * f * (3 - 2 * f)
    return values[i] * (1 - f) + values[(i + 1) % n] * f


# --- Variante A: Daumenkino ----------------------------------------------------------------------------------------

A_W, A_H, A_TEXEL = 9, 15, 0.028
A_COLOURS = [GOLD[7], GOLD[6], GOLD[5], GOLD[3]]


def flipbook_images(count=5, seed=353):
    rng = np.random.default_rng(seed)
    images = []
    for k in range(count):
        ph = 2 * math.pi * k / count
        img = np.full((A_H, A_W), -1)
        wob = rng.uniform(-0.5, 0.5, A_H)
        grain = rng.uniform(-1, 1, (A_H, A_W))
        for y in range(A_H):
            h = y / (A_H - 1)
            s = 1.1 * h * math.sin(ph + h * 3.2) + 0.5 * h * h * math.sin(2 * ph + 1.0)
            w = 4.6 * (1 - h) ** 0.75 * min(1.0, 0.6 + h * 4) + wob[y] * 0.6
            for x in range(A_W):
                heat = 1 - abs(x - (A_W - 1) / 2 - s) / max(w, 0.1) - 0.45 * h + 0.12 * grain[y, x]
                if heat > 0.62:
                    img[y, x] = 0
                elif heat > 0.42:
                    img[y, x] = 1
                elif heat > 0.2:
                    img[y, x] = 2
                elif heat > 0.0:
                    img[y, x] = 3
        if k % 2 == 1:                                  # abgerissene Zunge oben
            cx = int(round((A_W - 1) / 2 + 1.6 * math.sin(ph)))
            img[A_H - 2, cx] = 2
            img[A_H - 1, cx] = 3
        images.append(img)
    return images


def variant_a(scene, t, flip):
    intensity = 1.0 + 0.07 * math.sin(2 * math.pi * t) + 0.05 * math.sin(2 * math.pi * 3 * t + 1.3)
    cv = Canvas(scene, torch_light(scene, FLAME_BASE + np.array([0, 0.1, 0]), intensity))
    cv.sprite(FLAME_BASE, flip[int(math.floor(t * 10)) % len(flip)], A_TEXEL, A_COLOURS)
    for k in range(3):                                  # drei Funken, je 0,6 s Lebenszeit, versetzt
        a = ((t + k / 3) % 1.0) / 0.6
        if a >= 1:
            continue
        side = (-1, 1, 0.4)[k]
        pos = FLAME_BASE + np.array([side * (0.03 + 0.12 * a) + 0.02 * math.sin(7 * a + k),
                                     0.3 + 0.5 * a, 0.02 * side])
        cv.square(pos, 0.016, GOLD[7] if a < 0.6 else GOLD[5], min_px=3)
    return cv.img, intensity


# --- Variante B: Teilchenflamme ------------------------------------------------------------------------------------

B_RNG = np.random.default_rng(7)
B_FLAME = B_RNG.uniform(0, 1, (12, 2, 5))   # je Teilchen und Durchlauf: Versatz, Seitwaertsflug, Aufstieg, Groesse, Phase
B_SPARK = B_RNG.uniform(0, 1, (6, 4))
B_NOISE = loop_noise(11, 20), loop_noise(12, 10), loop_noise(13, 10)


def variant_b(scene, t):
    n_i, n_x, n_y = B_NOISE
    intensity = 0.85 + 0.32 * sample_loop(n_i, t)
    jitter = np.array([0.045 * sample_loop(n_x, t), 0.035 * sample_loop(n_y, t), 0])
    cv = Canvas(scene, torch_light(scene, FLAME_BASE + np.array([0, 0.1, 0]) + jitter, intensity))
    life = 0.5
    parts = []
    for k in range(12):
        x = t + k * life / 12
        a = (x % life) / life
        r = B_FLAME[k, int(math.floor(x / life)) % 2]
        tau = a * life
        pos = FLAME_BASE + np.array([
            (r[0] - 0.5) * 0.11 + (r[1] - 0.5) * 0.3 * tau + 0.02 * math.sin(2 * math.pi * (1.5 * a + r[4])),
            (0.6 + 0.3 * r[2]) * tau * (1 - 0.25 * a) + 0.02,
            0.0])
        size = (0.06 + 0.035 * r[3]) * (1 - 0.65 * a)
        colour = GOLD[7] if a < 0.3 else GOLD[6] if a < 0.55 else GOLD[4] if a < 0.8 else GOLD[2]
        parts.append((a, pos, size, colour))
    for a, pos, size, colour in sorted(parts, key=lambda p: -p[0]):   # alte hinten, junge vorn
        cv.square(pos, size, colour, min_px=2)
    for k in range(6):                                 # Funken: Lebenszeit 0,5 s oder 1 s
        r = B_SPARK[k]
        life_s = 0.5 if k % 2 else 1.0
        a = ((t + r[0]) % life_s) / life_s
        if a > 0.85:
            continue
        tau = a * life_s
        pos = FLAME_BASE + np.array([(r[1] - 0.5) * 0.5 * tau, 0.2 + (0.7 + 0.4 * r[2]) * tau - 0.3 * tau * tau,
                                     (r[3] - 0.5) * 0.1 * tau])
        colour = GOLD[7] if a < 0.35 else GOLD[6] if a < 0.65 else GOLD[5]
        cv.square(pos, 0.016 if a < 0.5 else 0.01, colour, min_px=2)
    return cv.img, intensity


# --- Variante C: Polygonflamme mit Stufenlicht --------------------------------------------------------------------

C_LEVELS = [0.72, 0.95, 1.2]
C_STEPS = [1, 2, 1, 0, 1, 2, 2, 1, 0, 1]                  # Helligkeit springt alle 0,1 s
C_RNG = np.random.default_rng(29)
C_JITTER = C_RNG.uniform(-1, 1, (10, 8, 2))               # je Schritt und Ecke
C_EMBER = C_RNG.uniform(0, 1, (8, 3))
C_SHAPE = [(-0.07, 0.0), (-0.1, 0.08), (-0.075, 0.19), (0.0, 0.36), (0.075, 0.19), (0.1, 0.08), (0.07, 0.0)]


def variant_c(scene, t):
    step = int(math.floor(t * 10)) % 10
    intensity = C_LEVELS[C_STEPS[step]]
    cv = Canvas(scene, torch_light(scene, FLAME_BASE + np.array([0, 0.1, 0]), intensity, bands=4))
    jit = C_JITTER[step]
    pts = []
    for i, (x, y) in enumerate(C_SHAPE):
        if y == 0:
            pts.append((x, y))
        elif i == 3:                                     # Spitze springt weiter
            pts.append((x + 0.045 * jit[i, 0], y + 0.06 * jit[i, 1]))
        else:
            pts.append((x + 0.02 * jit[i, 0], y + 0.02 * jit[i, 1]))
    for scale, colour in ((1.0, GOLD[4]), (0.66, GOLD[6]), (0.36, GOLD[7])):
        cv.polygon(FLAME_BASE, [(x * scale, y * scale + 0.01 * (1 - scale)) for x, y in pts], colour)
    for k in range(8):                                   # Glutfunken: langsam, 1 s Lebenszeit
        r = C_EMBER[k]
        a = (t + k / 8) % 1.0
        if a > 0.85:
            continue
        pos = FLAME_BASE + np.array([(r[0] - 0.5) * 0.12 + 0.05 * math.sin(2 * math.pi * (a + r[1])),
                                     0.2 + (0.18 + 0.12 * r[2]) * a * 1.8, 0.0])
        colour = GOLD[7] if a < 0.3 else GOLD[5] if a < 0.6 else GOLD[3]
        cv.square(pos, 0.016, colour, min_px=3)
    return cv.img, intensity


# --- Schrift (5 x 7, eigene Zeichen) -------------------------------------------------------------------------------

FONT = {
    "A": [" ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #"],
    "B": ["#### ", "#   #", "#   #", "#### ", "#   #", "#   #", "#### "],
    "C": [" ### ", "#   #", "#    ", "#    ", "#    ", "#   #", " ### "],
    "D": ["#### ", "#   #", "#   #", "#   #", "#   #", "#   #", "#### "],
    "E": ["#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#####"],
    "F": ["#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#    "],
    "G": [" ### ", "#   #", "#    ", "# ###", "#   #", "#   #", " ####"],
    "H": ["#   #", "#   #", "#   #", "#####", "#   #", "#   #", "#   #"],
    "I": [" ### ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### "],
    "J": ["  ###", "   # ", "   # ", "   # ", "   # ", "#  # ", " ##  "],
    "K": ["#   #", "#  # ", "# #  ", "##   ", "# #  ", "#  # ", "#   #"],
    "L": ["#    ", "#    ", "#    ", "#    ", "#    ", "#    ", "#####"],
    "M": ["#   #", "## ##", "# # #", "# # #", "#   #", "#   #", "#   #"],
    "N": ["#   #", "##  #", "# # #", "#  ##", "#   #", "#   #", "#   #"],
    "O": [" ### ", "#   #", "#   #", "#   #", "#   #", "#   #", " ### "],
    "P": ["#### ", "#   #", "#   #", "#### ", "#    ", "#    ", "#    "],
    "Q": [" ### ", "#   #", "#   #", "#   #", "# # #", "#  # ", " ## #"],
    "R": ["#### ", "#   #", "#   #", "#### ", "# #  ", "#  # ", "#   #"],
    "S": [" ####", "#    ", "#    ", " ### ", "    #", "    #", "#### "],
    "T": ["#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  "],
    "U": ["#   #", "#   #", "#   #", "#   #", "#   #", "#   #", " ### "],
    "V": ["#   #", "#   #", "#   #", "#   #", "#   #", " # # ", "  #  "],
    "W": ["#   #", "#   #", "#   #", "# # #", "# # #", "# # #", " # # "],
    "X": ["#   #", "#   #", " # # ", "  #  ", " # # ", "#   #", "#   #"],
    "Y": ["#   #", "#   #", " # # ", "  #  ", "  #  ", "  #  ", "  #  "],
    "Z": ["#####", "    #", "   # ", "  #  ", " #   ", "#    ", "#####"],
    "0": [" ### ", "#   #", "#  ##", "# # #", "##  #", "#   #", " ### "],
    "1": ["  #  ", " ##  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### "],
    "2": [" ### ", "#   #", "    #", "   # ", "  #  ", " #   ", "#####"],
    "3": ["#####", "   # ", "  #  ", "   # ", "    #", "#   #", " ### "],
    "4": ["   # ", "  ## ", " # # ", "#  # ", "#####", "   # ", "   # "],
    "5": ["#####", "#    ", "#### ", "    #", "    #", "#   #", " ### "],
    "6": ["  ## ", " #   ", "#    ", "#### ", "#   #", "#   #", " ### "],
    "7": ["#####", "    #", "   # ", "  #  ", " #   ", " #   ", " #   "],
    "8": [" ### ", "#   #", "#   #", " ### ", "#   #", "#   #", " ### "],
    "9": [" ### ", "#   #", "#   #", " ####", "    #", "   # ", " ##  "],
    " ": ["     "] * 7,
    ":": ["     ", "  #  ", "  #  ", "     ", "  #  ", "  #  ", "     "],
    ".": ["     ", "     ", "     ", "     ", "     ", " ##  ", " ##  "],
    ",": ["     ", "     ", "     ", "     ", " ##  ", "  #  ", " #   "],
    "(": ["   # ", "  #  ", " #   ", " #   ", " #   ", "  #  ", "   # "],
    ")": [" #   ", "  #  ", "   # ", "   # ", "   # ", "  #  ", " #   "],
    "/": ["    #", "    #", "   # ", "  #  ", " #   ", "#    ", "#    "],
    "=": ["     ", "     ", "#####", "     ", "#####", "     ", "     "],
    "-": ["     ", "     ", "     ", "#####", "     ", "     ", "     "],
    "+": ["     ", "  #  ", "  #  ", "#####", "  #  ", "  #  ", "     "],
    "#": [" # # ", "#####", " # # ", " # # ", " # # ", "#####", " # # "],
}


def text(img, s, row, col, scale, colour):
    for ch in s.upper():
        glyph = FONT[ch]
        for gy, line in enumerate(glyph):
            for gx, c in enumerate(line):
                if c == "#":
                    img[row + gy * scale:row + (gy + 1) * scale, col + gx * scale:col + (gx + 1) * scale] = colour
        col += 6 * scale


# --- Streifen und Blatt -------------------------------------------------------------------------------------------

CROP_W, CROP_H = 300, 330
GAP = 4
BG = FOG_COLOR
INK = STONE[14]
BAR = GOLD[6]

LABELS = {
    "A": "A  DAUMENKINO: GEMALTE FLAMME (5 BILDER, 10/S), RUHIGES LICHT, 3 FUNKEN",
    "B": "B  TEILCHENFLAMME: 12 GROBE TEILCHEN, 6 FUNKEN, UNRUHIGES LICHT MIT WANDERNDER MITTE",
    "C": "C  POLYGONFLAMME: 3 FLAECHEN, LICHT IN HARTEN STUFEN (SPRINGT ALLE 0.1 S), 8 GLUTFUNKEN",
}


def strip_block(name, frames, intensities):
    fr, fc, _, _ = project(FLAME_BASE)
    top = int(round(fr)) - int(CROP_H * 0.42)
    left = int(round(fc)) - CROP_W // 2
    picks = list(range(0, FRAMES, 2))
    head, bar_h, foot = 36, 40, 22
    width = len(picks) * (CROP_W + GAP) + GAP
    block = np.tile(BG, (head + CROP_H + bar_h + foot, width, 1))
    text(block, LABELS[name], 8, GAP, 3, INK)
    for n, i in enumerate(picks):
        x = GAP + n * (CROP_W + GAP)
        block[head:head + CROP_H, x:x + CROP_W] = frames[i][top:top + CROP_H, left:left + CROP_W]
        h = int(round(intensities[i] / 1.3 * (bar_h - 8)))   # Balken: Helligkeit des Fackellichts
        block[head + CROP_H + bar_h - 2 - h:head + CROP_H + bar_h - 2, x + CROP_W // 2 - 20:x + CROP_W // 2 + 20] = BAR
        text(block, f"T={i / FPS:.2f} S", head + CROP_H + bar_h + 4, x + 4, 2, INK)
    return block


def main():
    scene = build_scene()
    flip = flipbook_images()
    variants = {
        "A": lambda t: variant_a(scene, t, flip),
        "B": lambda t: variant_b(scene, t),
        "C": lambda t: variant_c(scene, t),
    }
    blocks = []
    for name, fn in variants.items():
        frames, intensities = [], []
        for i in range(FRAMES):
            img, inten = fn(i / FPS)
            frames.append(quantize(img, scene["thr"]))
            intensities.append(inten)
        block = strip_block(name, frames, intensities)
        save_png(block, os.path.join(HERE, f"fackel-{name}-streifen.png"))
        save_gif(frames, os.path.join(HERE, f"fackel-{name}.gif"), 100 // FPS)
        blocks.append(block)
    title = np.tile(BG, (44, blocks[0].shape[1], 1))
    text(title, "FACKEL - DREI VARIANTEN, NUR DER EFFEKT AENDERT SICH (ISSUE #353).  BALKEN = HELLIGKEIT DES "
                "FACKELLICHTS", 10, GAP, 3, INK)
    sep = np.tile(BG, (12, blocks[0].shape[1], 1))
    sheet = np.concatenate([title] + [x for b in blocks for x in (b, sep)], 0)
    save_png(sheet, os.path.join(HERE, "fackel-blatt.png"))


if __name__ == "__main__":
    main()
