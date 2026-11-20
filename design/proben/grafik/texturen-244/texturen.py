"""Probestueck Texturen (Issue #244): je drei weitere Fassungen (d, e, f) fuer Wand, Boden und Decke der Katakomben.

Aufruf (aus dem Repository-Wurzelverzeichnis, Blender 5.3):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/texturen-244/texturen.py

Schreibt nach design/proben/grafik/texturen-244/:
    texturen/catacomb_<wall|floor|ceiling>_<d|e|f>.png   neun Texturen, 64 x 64, nahtlos, 1 m je Kachel
    blatt.png                                            Probenblatt (3 x 3 gekachelt, gemischt, im Laternenlicht)
    im-spiel-bestand.png, im-spiel-neu.png               Gang in Spielaufloesung 854 x 480
    belege.txt                                           Naht-, Paletten- und Prueflauf-Ergebnisse
Die Varianten a, b, c (Bestand) bleiben unveraendert und werden nur gelesen (assets/textures/).

Regeln aus der Art Bible: Pixel-Stil und Palette des Bestands; jede Pixelfarbe ist ein Wert der Hauptpalette
(Reihe Stein, Moos nur als Haut/Knochen 1 und 2 und nur in Fugen); gleiche mittlere Helligkeit wie die Fassungen a bis c,
damit nichts herausfaellt; kein Dithering, keine Risse auf kleinen Steinen (sie wiederholen sich jeden Meter);
gemalte Tiefe nur als Licht von oben. Alles per Skript, nichts heruntergeladen. Zufall nur mit festen Seeds.
"""
import hashlib
import json
import math
import os
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
TOOLS = os.path.join(ROOT, "tools", "textures")
src = open(os.path.join(TOOLS, "catacomb.py"), encoding="utf-8").read()
src = src.replace("\nmain()\n", "\n").replace("__file__", repr(os.path.join(TOOLS, "catacomb.py")))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
exec(compile(src, "catacomb.py", "exec"))   # tnoise, tnoise_xy, courses, soft_joints, SIZE, PAINT, WORLD_*, bpy, ...

import blf    # noqa: E402
import imbuf  # noqa: E402

TEX_DIR = os.path.join(HERE, "texturen")
BESTAND = os.path.join(ROOT, "assets", "textures")
FOG = np.array((16, 11, 20), float)

PAL = json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))
STEIN = np.array(next(r for r in PAL["grund"] if r["name"] == "Stein")["farben"], float)
MOOS = np.array(next(r for r in PAL["grund"] if r["name"] == "Haut/Knochen")["farben"][1:3], float)   # Stufen 1 und 2


# --- Bild-Ein- und Ausgabe (Zeilen von oben) ------------------------------------------------------------------------

def save_png_top(arr, path):
    h, w = arr.shape[:2]
    im = bpy.data.images.new(os.path.basename(path), w, h)
    im.pixels = np.concatenate([np.clip(arr, 0, 255)[::-1] / 255.0, np.ones((h, w, 1))], axis=2).ravel()
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)


def load_png_top(path):
    im = bpy.data.images.load(path, check_existing=False)
    w, h = im.size
    a = np.array(im.pixels[:], np.float32).reshape(h, w, 4)[::-1, :, :3] * 255
    bpy.data.images.remove(im)
    return np.rint(a)


def text(img, s, size, x, y, color=(235, 230, 240)):
    f = blf.load("C:/Windows/Fonts/arial.ttf")
    blf.size(f, size)
    w, h = blf.dimensions(f, s)
    wd, hd = int(w) + 8, int(size * 1.6)
    ib = imbuf.new((wd, hd))
    blf.color(f, 1, 1, 1, 1)
    blf.position(f, 4, int(size * 0.4), 0)
    with blf.bind_imbuf(f, ib):
        blf.draw_buffer(f, s)
    tmp = os.path.join(tempfile.gettempdir(), "texturen244_txt.png")
    ib.filepath = tmp
    imbuf.write(ib)
    im = bpy.data.images.load(tmp)
    im.colorspace_settings.name = "Non-Color"
    a = np.array(im.pixels[:], np.float32).reshape(hd, wd, 4)[::-1, :, 3]
    bpy.data.images.remove(im)
    hh, ww = img.shape[:2]
    ye, xe = min(hh, y + hd), min(ww, x + wd)
    if ye <= y or xe <= x:
        return
    mk = (a[:ye - y, :xe - x] > 0.5)[..., None]
    img[y:ye, x:xe] = np.where(mk, np.array(color, float), img[y:ye, x:xe])


# --- Malen ----------------------------------------------------------------------------------------------------------

def snap(small, palette):
    """Jeden Pixel auf die naechste Farbe der Palette setzen (kein Dithering)."""
    d = ((small[:, :, None, :] - palette[None, None, :, :]) ** 2).sum(-1)
    return palette[d.argmin(-1)]


def finish2(img, base, extra=None):
    """Auf 64 x 64 verkleinern, mittlere Helligkeit = base, auf die Stein-Reihe setzen; extra: Moos-Maske (64 x 64)."""
    small = img.reshape(SIZE, PAINT, SIZE, PAINT, 3).mean(axis=(1, 3))
    small *= np.asarray(base, float).mean() / small.mean()
    out = snap(np.clip(small, 0, 255), STEIN)
    if extra is not None:
        moss, shade = extra
        out = np.where(moss[..., None], MOOS[shade.astype(int)], out)
    return out


def coords(n):
    ys, xs = np.mgrid[0:n, 0:n]
    return ys / PAINT, xs / PAINT          # Kachel-Pixel (0..64)


def voronoi(n, k, seed):
    """Periodische Voronoi-Zellen (k x k Keime). Rueckgabe: Zellennummer, Abstand zur Fuge (Kachel-Pixel)."""
    rng = np.random.default_rng(seed)
    pts = np.array([(i + rng.uniform(0.2, 0.8), j + rng.uniform(0.2, 0.8)) for i in range(k) for j in range(k)]) / k * n
    ys, xs = np.mgrid[0:n, 0:n]
    best = np.full((n, n), 1e9)
    second = np.full((n, n), 1e9)
    which = np.zeros((n, n), int)
    for idx, (py, px) in enumerate(pts):
        d = np.full((n, n), 1e9)
        for oy in (-n, 0, n):
            for ox in (-n, 0, n):
                d = np.minimum(d, np.hypot(ys - (py + oy), xs - (px + ox)))
        closer = d < best
        second = np.where(closer, best, np.minimum(second, d))
        which = np.where(closer, idx, which)
        best = np.where(closer, d, best)
    return which, (second - best) / 2 / PAINT


def joint_mask_small(dx, dy, w=1.3):
    """Fugen als 64 x 64 Maske aus den Abstaenden der Mauerlagen."""
    j = ((dx < w) | (dy < w * 0.9)).astype(float)
    return j.reshape(SIZE, PAINT, SIZE, PAINT).mean(axis=(1, 3)) > 0.5


def paint_wall_d():
    """Bossenquader: gleichmaessige Lagen, jeder Stein leicht gewoelbt, Oberkante hell, Unterkante dunkel (Licht von oben)."""
    n = SIZE * PAINT
    rng = np.random.default_rng(411)
    ids, dx, dy = courses(n, [16, 16, 16, 16], (14, 22), 412)
    tone = rng.uniform(0.94, 1.06, ids.max() + 1)
    img = np.array(WORLD_WALL, float) * tone[ids][..., None]
    img *= (0.92 + 0.14 * tnoise(n, 6, 413))[..., None]
    img *= (0.95 + 0.1 * tnoise(n, 32, 414))[..., None]
    ys, _ = coords(n)
    ly = ys % 16
    pillow = np.clip(np.minimum(dx, dy) / 5.0, 0, 1)
    img *= (0.92 + 0.12 * pillow)[..., None]
    img *= np.where(ly < 2.2, 1.09, np.where(ly > 13.6, 0.9, 1.0))[..., None]
    img = soft_joints(img, dx, dy, n, 415, depth=0.18, width=1.0)
    return finish2(img, WORLD_WALL)


def paint_wall_e():
    """Hochplatten: schmale, hohe Steine in zwei Lagen; in den senkrechten Fugen ein wenig Moos."""
    n = SIZE * PAINT
    rng = np.random.default_rng(421)
    ids, dx, dy = courses(n, [32, 32], (13, 22), 422)
    tone = rng.uniform(0.92, 1.08, ids.max() + 1)
    img = np.array(WORLD_WALL, float) * tone[ids][..., None]
    img *= (0.9 + 0.18 * tnoise(n, 7, 423))[..., None]
    img *= (0.94 + 0.1 * tnoise(n, 32, 424))[..., None]
    img = soft_joints(img, dx, dy, n, 425, depth=0.2)
    joint = joint_mask_small(dx, dy, 1.4)
    grow = tnoise(SIZE, 8, 426)
    moss = joint & (grow > 0.74)
    shade = (tnoise(SIZE, 16, 427) > 0.6).astype(int)           # 0 = Stufe 1 (dunkel), 1 = Stufe 2
    return finish2(img, WORLD_WALL, (moss, shade))


def paint_wall_f():
    """Putzwand mit Abplatzungen: glatter, etwas hellerer Putz, dazwischen freiliegender Bruchstein."""
    n = SIZE * PAINT
    rng = np.random.default_rng(431)
    ids, dx, dy = courses(n, [16, 16, 16, 16], (13, 24), 432)
    tone = rng.uniform(0.9, 1.1, ids.max() + 1)
    stone = np.array(WORLD_WALL, float) * tone[ids][..., None]
    stone *= (0.9 + 0.18 * tnoise(n, 8, 433))[..., None]
    stone = soft_joints(stone, dx, dy, n, 435, depth=0.24)
    stone *= 0.9
    plaster = np.array(WORLD_WALL, float) * (1.03 + 0.06 * tnoise(n, 5, 436))[..., None]
    plaster *= (0.96 + 0.08 * tnoise(n, 32, 437))[..., None]
    t = 0.45 * tnoise(n, 6, 438) + 0.55 * tnoise(n, 13, 439)
    bare = (t > 0.6)[..., None]
    rim = ((t > 0.565) & ~(t > 0.6))[..., None]
    img = np.where(bare, stone, plaster)
    img = np.where(rim, plaster * 0.84, img)
    return finish2(img, WORLD_WALL)


def paint_floor_d():
    """Quadratplatten, 0,5 m, im Raster; Mitte ausgetreten (heller Rand, dunklere Mitte), ein Riss."""
    n = SIZE * PAINT
    rng = np.random.default_rng(511)
    ys, xs = coords(n)
    ids = (ys // 32).astype(int) * 2 + (xs // 32).astype(int)
    dx = np.minimum(xs % 32, 32 - xs % 32)
    dy = np.minimum(ys % 32, 32 - ys % 32)
    tone = rng.uniform(0.9, 1.1, 4)
    img = np.array(WORLD_FLOOR, float) * tone[ids][..., None]
    img *= (0.9 + 0.18 * tnoise(n, 6, 513))[..., None]
    img *= (0.94 + 0.1 * tnoise(n, 32, 514))[..., None]
    center = np.clip(np.minimum(dx, dy) / 10, 0, 1)
    img *= (0.88 + 0.14 * center)[..., None]
    img = soft_joints(img, dx, dy, n, 515, depth=0.22, width=1.3)
    cracks(img, rng, 1, n, 0.84)
    return finish2(img, WORLD_FLOOR)


def paint_floor_e():
    """Unregelmaessige Platten (Polygone, flach), breite ruhige Fugen, kaum Wölbung."""
    n = SIZE * PAINT
    rng = np.random.default_rng(521)
    which, edge = voronoi(n, 4, 522)
    tone = rng.uniform(0.9, 1.1, which.max() + 1)
    img = np.array(WORLD_FLOOR, float) * tone[which][..., None]
    img *= (0.9 + 0.18 * tnoise(n, 6, 523))[..., None]
    img *= (0.95 + 0.1 * tnoise(n, 32, 524))[..., None]
    var = tnoise(n, 10, 525)
    amount = 0.24 * (0.6 + 0.8 * var)
    img = np.where((edge < 1.3)[..., None], img * (1 - amount)[..., None], img)
    near = ((edge >= 1.3) & (edge < 2.6))[..., None]
    img = np.where(near, img * (1 - 0.35 * amount)[..., None], img)
    return finish2(img, WORLD_FLOOR)


def paint_floor_f():
    """Lehm und Kies: gestampfter Boden mit eingedrueckten Kieseln, weiche Pfuetzenflecken."""
    n = SIZE * PAINT
    rng = np.random.default_rng(531)
    img = np.array(WORLD_FLOOR, float) * (0.9 + 0.2 * tnoise(n, 5, 532))[..., None]
    img *= (0.94 + 0.1 * tnoise(n, 24, 533))[..., None]
    ys, xs = np.mgrid[0:n, 0:n]
    for _ in range(70):
        cy, cx = rng.uniform(0, n, 2)
        ry, rx = rng.uniform(1.8, 3.6) * PAINT / 2, rng.uniform(2.2, 4.4) * PAINT / 2
        tone = rng.uniform(0.82, 1.22)
        for oy in (-n, 0, n):
            for ox in (-n, 0, n):
                m = ((ys - cy - oy) / ry) ** 2 + ((xs - cx - ox) / rx) ** 2 < 1
                if m.any():
                    shade = np.where((ys - cy - oy) > 0.3 * ry, 0.86, 1.0)       # Licht von oben
                    img[m] = (np.array(WORLD_FLOOR, float) * tone * shade[m][:, None])
    wet = np.clip((tnoise(n, 6, 534) - 0.62) * 2.0, 0, 0.12)
    img *= (1 - wet)[..., None]
    return finish2(img, WORLD_FLOOR)


CEIL_BASE = np.asarray(WORLD_CEILING, float) * CEILING_DARKER


def paint_ceiling_d():
    """Kreuzrippen: zwei Rippen kreuzen die Kachel diagonal, dazwischen glatte Felder, Russ."""
    n = SIZE * PAINT
    ys, xs = coords(n)
    d1 = np.abs(((xs - ys + 32) % 64) - 32) / math.sqrt(2)
    d2 = np.abs(((xs + ys + 32) % 64) - 32) / math.sqrt(2)
    dist = np.minimum(d1, d2)
    img = np.array(WORLD_CEILING, float) * np.ones((n, n, 1))
    rib = np.clip(1 - dist / 3.2, 0, 1)
    img *= (1 + 0.14 * rib)[..., None]
    edge = (dist > 3.0) & (dist < 4.6)
    img *= np.where(edge, 0.88, 1.0)[..., None]                    # Schatten neben der Rippe
    img *= (0.9 + 0.16 * tnoise(n, 8, 613))[..., None]
    img *= (0.82 + 0.25 * tnoise(n, 3, 614))[..., None]
    return finish2(img, CEIL_BASE)


def paint_ceiling_e():
    """Laengsbahnen: breite Steinbahnen mit nur ganz wenigen Querfugen, Russ."""
    n = SIZE * PAINT
    rng = np.random.default_rng(621)
    ids, dx, dy = courses(n, [10, 22, 10, 22], (40, 64), 622)
    tone = rng.uniform(0.94, 1.06, ids.max() + 1)
    img = np.array(WORLD_CEILING, float) * tone[ids][..., None]
    img *= (0.9 + 0.16 * tnoise(n, 7, 623))[..., None]
    img = soft_joints(img, dx, dy, n, 624, depth=0.13)
    img *= (0.82 + 0.25 * tnoise(n, 3, 625))[..., None]
    return finish2(img, CEIL_BASE)


def paint_ceiling_f():
    """Kassetten: vier eingetiefte Felder (je 0,5 m) in einem Rahmen, Russ."""
    n = SIZE * PAINT
    ys, xs = coords(n)
    dx = np.minimum(xs % 32, 32 - xs % 32)
    dy = np.minimum(ys % 32, 32 - ys % 32)
    d = np.minimum(dx, dy)
    img = np.array(WORLD_CEILING, float) * np.ones((n, n, 1))
    frame = d < 3.2
    img *= np.where(frame, 1.1, 0.95)[..., None]                   # Rahmen heller, Feld eingetieft
    ring = (d >= 3.2) & (d < 5)
    img *= np.where(ring, 0.86, 1.0)[..., None]                    # Schatten am Felderrand
    img *= (0.9 + 0.16 * tnoise(n, 8, 633))[..., None]
    img *= (0.82 + 0.25 * tnoise(n, 3, 634))[..., None]
    return finish2(img, CEIL_BASE)


NEW = {
    "wall": {"d": paint_wall_d, "e": paint_wall_e, "f": paint_wall_f},
    "floor": {"d": paint_floor_d, "e": paint_floor_e, "f": paint_floor_f},
    "ceiling": {"d": paint_ceiling_d, "e": paint_ceiling_e, "f": paint_ceiling_f},
}
OLD_NAMES = {"a": "", "b": "_b", "c": "_c"}


def generate():
    out = {}
    for surf, fs in NEW.items():
        for v, fn in fs.items():
            out[(surf, v)] = fn()
    return out


# --- Belege ---------------------------------------------------------------------------------------------------------

def seam_ratio(a):
    """Sprung ueber den Kachelrand im Vergleich zu den inneren Spalten- und Zeilenpaaren. Fuer jedes Paar benachbarter
    Spalten (i, i+1) wird der mittlere Farbsprung gemessen, fuer die Randpaare (63, 0) ebenso; dasselbe fuer Zeilen.
    Rueckgabe: Randsprung geteilt durch den groessten Sprung innerhalb der Kachel (<= 1: der Rand faellt nicht auf,
    er ist nicht groesser als die staerkste echte Steinkante)."""
    a = a.astype(float)
    cols = np.abs(np.diff(np.concatenate([a, a[:, :1]], axis=1), axis=1)).mean(axis=(0, 2))    # 64 Paare, das letzte = Rand
    rows = np.abs(np.diff(np.concatenate([a, a[:1]], axis=0), axis=0)).mean(axis=(1, 2))
    return max(cols[-1] / cols[:-1].max(), rows[-1] / rows[:-1].max())


def sha(a):
    return hashlib.sha256(np.ascontiguousarray(a.astype(np.uint8)).tobytes()).hexdigest()


# --- Probenblatt ----------------------------------------------------------------------------------------------------

def tile(a, nx, ny, scale=1):
    t = np.tile(a, (ny, nx, 1))
    return np.repeat(np.repeat(t, scale, 0), scale, 1)


def lantern(strip):
    """Wie im Spiel: Laterne vorn (hell, Faktor 1) fallend auf Mindesthelligkeit 0,35, dazu Nebel in die Nebelfarbe."""
    h, w = strip.shape[:2]
    x = np.abs(np.arange(w) - w / 2) / (w / 2)            # 0 = Mitte (nah), 1 = Rand (fern)
    dist = 1.5 + x * 14
    b = 0.35 + 0.65 * np.clip(1 - dist / 9, 0, 1) ** 2
    fog = np.clip((dist - 3.25) / (18.2 - 3.25), 0, 1)
    out = strip * b[None, :, None]
    return out * (1 - fog[None, :, None]) + FOG * fog[None, :, None]


def sheet(textures, old):
    scale, ts = 2, SIZE * 2
    cols = ["Bestand a", "neu d", "neu e", "neu f"]
    names = {"wall": "WAND", "floor": "BODEN", "ceiling": "DECKE"}
    mix_order = ["a", "d", "b", "e", "c", "f"] * 2
    cw = 3 * ts
    gap = 24
    W = 4 * cw + 5 * gap
    block = 52 + cw + 40 + ts + 40 + ts + 30
    H = 70 + 3 * block
    img = np.tile(np.array((24, 20, 30.0)), (H, W, 1))
    text(img, "Silver & Ash - Probestueck Texturen (#244): je drei weitere Fassungen fuer Wand, Boden, Decke", 14, 10, 20, (235, 230, 240))
    text(img, "Kacheln 3 x 3, 2-fach vergroessert (Pixel sichtbar); Naehte pruefen: kein Band, keine Linie am Kachelrand", 12, 14, 44, (190, 185, 200))
    y = 70
    for surf in ("wall", "floor", "ceiling"):
        text(img, names[surf], 18, gap, y + 6, (240, 220, 160))
        for i, v in enumerate(("a", "d", "e", "f")):
            a = old[(surf, "a")] if v == "a" else textures[(surf, v)]
            x = gap + i * (cw + gap)
            text(img, cols[i], 13, x, y + 26, (210, 205, 220))
            img[y + 52:y + 52 + cw, x:x + cw] = tile(a, 3, 3, scale)
        yy = y + 52 + cw
        pick = {k: (old[(surf, k)] if k in "abc" else textures[(surf, k)]) for k in "abcdef"}
        strip = np.concatenate([tile(pick[k], 1, 1, scale) for k in mix_order], axis=1)       # 12 Kacheln
        xs = gap
        text(img, "gemischt: a d b e c f a d b e c f (Bestand und neu abwechselnd, wie in einem Raum)", 12, xs, yy + 6, (210, 205, 220))
        img[yy + 36:yy + 36 + ts, xs:xs + strip.shape[1]] = strip
        yy2 = yy + 36 + ts
        text(img, "im Laternenlicht (Mitte nah, Raender fern, mit Nebel)", 12, xs, yy2 + 6, (210, 205, 220))
        img[yy2 + 36:yy2 + 36 + ts, xs:xs + strip.shape[1]] = lantern(strip)
        y += block
    return img


# --- Gang in Spielaufloesung ------------------------------------------------------------------------------------------

def corridor(wall, floor, ceil, w=854, h=480, fov=70.0):
    """Einfacher Gang (2 m breit, 3,2 m hoch, 16 m lang), Auge 1,6 m, Laterne und Nebel wie im Spiel. wall/floor/ceil:
    Listen von Kacheln, je 4 m Gang wechselt die Kachel; die beiden Waende verschieden."""
    tanf = math.tan(math.radians(fov / 2))
    ys, xs = np.mgrid[0:h, 0:w]
    u = (xs + 0.5 - w / 2) / (h / 2) * tanf
    v = -(ys + 0.5 - h / 2) / (h / 2) * tanf
    t_floor = np.where(v < 0, -1.6 / np.where(v < 0, v, -1), 1e9)
    t_ceil = np.where(v > 0, 1.6 / np.where(v > 0, v, 1), 1e9)
    t_wall = np.where(u != 0, np.where(u > 0, 1.0, -1.0) / np.where(u == 0, 1, u), 1e9)
    t_end = np.full_like(t_wall, 16.0)
    t = np.minimum.reduce([t_floor, t_ceil, t_wall, t_end])
    z, x, y = t, u * t, 1.6 + v * t
    col = np.zeros((h, w, 3))

    def sample(tiles, seg, uu, vv):
        stack = np.stack(tiles)
        iu = np.floor(uu * SIZE).astype(int) % SIZE
        iv = np.floor(vv * SIZE).astype(int) % SIZE
        return stack[seg % len(tiles), iv, iu]

    seg = np.floor(z / 4).astype(int)
    m_floor, m_ceil = t == t_floor, (t == t_ceil) & (t != t_floor)
    m_wall = (t == t_wall) & ~m_floor & ~m_ceil
    m_end = ~(m_floor | m_ceil | m_wall)
    col[m_floor] = sample(floor, seg + 0, x, z)[m_floor]
    col[m_ceil] = sample(ceil, seg + 0, x, z)[m_ceil]
    left = u < 0
    for side, off in ((left, 0), (~left, 1)):
        mm = m_wall & side
        col[mm] = sample(wall, seg + off, z, 1 - y)[mm]
    col[m_end] = FOG
    b = 0.35 + 0.65 * np.clip(1 - t / 10, 0, 1) ** 2
    fog = np.clip((t - 3.25) / (18.2 - 3.25), 0, 1)
    return col * b[..., None] * (1 - fog[..., None]) + FOG * fog[..., None]


# --- Hauptlauf ------------------------------------------------------------------------------------------------------

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    os.makedirs(TEX_DIR, exist_ok=True)
    tex = generate()
    again = generate()
    old = {}
    for surf in NEW:
        for v, suf in OLD_NAMES.items():
            old[(surf, v)] = load_png_top(os.path.join(BESTAND, "catacomb_%s%s.png" % (surf, suf)))

    print("Texturen fertig", flush=True)
    lines = ["Belege Probestueck Texturen #244", ""]
    ok = True
    stein = {tuple(c) for c in STEIN.astype(int)}
    moos = {tuple(c) for c in MOOS.astype(int)}
    lines.append("Nahtlos: Randsprung / Innensprung (Rand / staerkste Innenkante, hoechstens 1; Bestand zum Vergleich); mittlere Helligkeit; Farben")
    for surf in NEW:
        for v in "abc":
            a = old[(surf, v)]
            lines.append("  Bestand %-8s %s  Naht %.2f  Mittel %5.1f  Farben %2d" % (
                surf, v, seam_ratio(a), a.mean(), len({tuple(p) for p in a.astype(int).reshape(-1, 3)})))
    for (surf, v), a in tex.items():
        save_png_top(a, os.path.join(TEX_DIR, "catacomb_%s_%s.png" % (surf, v)))
        back = load_png_top(os.path.join(TEX_DIR, "catacomb_%s_%s.png" % (surf, v)))
        cs = {tuple(p) for p in back.astype(int).reshape(-1, 3)}
        r = seam_ratio(back)
        seam_ok = r <= 1.0
        pal_ok = cs <= (stein | moos)
        moss_px = int(sum(1 for p in back.astype(int).reshape(-1, 3) if tuple(p) in moos and tuple(p) not in stein))
        same = sha(a) == sha(again[(surf, v)]) == sha(back)
        ok &= seam_ok and pal_ok and same and back.shape == (64, 64, 3) and len(cs) <= 32
        lines.append("  neu     %-8s %s  Naht %.2f  Mittel %5.1f  Farben %2d  Palette %s  Moos-Pixel %3d  bitgleich %s  sha256 %s" % (
            surf, v, r, back.mean(), len(cs), "ok" if pal_ok else "FEHLT", moss_px, "ja" if same else "NEIN", sha(back)[:16]))
    lines += ["", "Gesamt: %s" % ("alles in Ordnung" if ok else "FEHLER")]

    print("Blatt ...", flush=True)
    img = sheet(tex, old)
    print("Blatt fertig", flush=True)
    save_png_top(img, os.path.join(HERE, "blatt.png"))
    bw = [old[("wall", "a")], old[("wall", "b")], old[("wall", "c")]]
    bf = [old[("floor", "a")], old[("floor", "b")], old[("floor", "c")]]
    bc = [old[("ceiling", "a")], old[("ceiling", "b")], old[("ceiling", "c")]]
    nw = [tex[("wall", k)] for k in "def"]
    nf = [tex[("floor", k)] for k in "def"]
    nc = [tex[("ceiling", k)] for k in "def"]
    save_png_top(corridor(bw, bf, bc), os.path.join(HERE, "im-spiel-bestand.png"))
    save_png_top(corridor([bw[0], nw[0], bw[1], nw[1], bw[2], nw[2]], [bf[0], nf[0], bf[1], nf[1], bf[2], nf[2]],
                          [bc[0], nc[0], bc[1], nc[1], bc[2], nc[2]]), os.path.join(HERE, "im-spiel-neu.png"))
    open(os.path.join(HERE, "belege.txt"), "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
    print("\n".join(lines))


main()
