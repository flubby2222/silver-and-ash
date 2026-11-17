"""Rissige Wand (Issue #246), Probestueck: je Wandvariante (a, b, c) und Staerke (dezent, deutlich) ein Modell mit Textur.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/risswand/risswand.py

Alles per Skript, nichts heruntergeladen. Die Textur ist die echte Wand-Textur der Ebene (assets/textures/catacomb_wall*.png,
nur gelesen), 2 x 2 Mal gekachelt (128 x 128 = 2 m x 2 m), mit Rissen, Abplatzung und Staub am Fuss hineingemalt. Dort, wo nichts
gemalt ist, ist jedes Pixel gleich dem der Wand: die Platte setzt die Wand ohne Naht fort (die Wand-Textur ist an der
Welt ausgerichtet, eine Kachel je Meter; die Zelle ist 2 m breit und sitzt auf geraden Metern).

Knoten (Spiel-Achsen, Ursprung Mitte der Zelle auf dem Boden, Wandflaeche in x/y, Zellentiefe in z):
  panel   die Platte 2 m x 2 m, 1 cm vor beiden Flaechen der Zelle (z = +-1,006 bis +-1,010)
  debris  ein paar Brocken am Fuss, vor beiden Seiten
  chunk   ein einzelner Brocken (Vorlage fuer den Bruch-Effekt #185), um den Ursprung, etwa 0,25 m
"""
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import bpy  # noqa: E402
from psx import V, Part, rects_from_pixels, save_image, export_material, export_glb, report_parts  # noqa: E402

OUT_DIR = os.path.join(HERE, "modelle")
TEX = 128
WALL_FILES = {"a": "catacomb_wall.png", "b": "catacomb_wall_b.png", "c": "catacomb_wall_c.png"}

# Pixel-Rechtecke (y von unten). "stone": ein Stueck Wand oben links, das nie gemalt wird (fuer Brocken).
RECTS = {"panel": (0, 0, 128, 128), "stone": (4, 100, 28, 124)}
UV = rects_from_pixels(RECTS, TEX)

# Zusaetzliche Farben (die Wand-Textur hat 24; zusammen bleibt es unter 32)
C_DEEP = np.array((46, 38, 44), float)        # tiefster Riss
C_MID = np.array((78, 66, 72), float)         # Riss, Rand
C_HI = np.array((150, 134, 138), float)       # helle Kante unter dem Riss
C_SPALL = np.array((88, 76, 82), float)       # abgeplatzte Stelle
C_SHADOW = np.array((60, 50, 56), float)      # Schatten oben in der Abplatzung
C_RIM = np.array((146, 130, 134), float)      # Bruchkante
C_DUST = np.array((150, 138, 134), float)     # Staub


def load_tile(name):
    path = os.path.join(ROOT, "assets", "textures", name)
    im = bpy.data.images.load(path)
    w, h = im.size
    a = np.array(im.pixels[:], dtype=float).reshape(h, w, 4)[..., :3] * 255.0
    return np.round(a[::-1])                     # Zeile 0 oben, wie das Bild aussieht


def crack_path(rng, x, y, length, jitter):
    """Ein gezackter Riss von (x, y) abwaerts: je Schritt 1 Pixel nach unten, seitlich ein wenig hin und her, mit kleinen
    waagerechten Sprüngen (Riss folgt einer Fuge)."""
    pts = []
    drift = 0
    for i in range(length):
        if rng.random() < jitter:
            drift = int(rng.choice([-1, 0, 1]))
        x += drift if rng.random() < 0.7 else 0
        if rng.random() < 0.12:                  # kurzer waagerechter Lauf
            run = int(rng.integers(2, 5)) * (1 if rng.random() < 0.5 else -1)
            for _ in range(abs(run)):
                x += int(math.copysign(1, run))
                pts.append((x, y))
        y += 1
        pts.append((x, y))
    return pts


def paint(tile, strength, seed, mirror=False):
    """Eine 128 x 128 Platte (Zeile 0 oben) aus der gekachelten Wand mit Rissen. strength: 'dezent' oder 'deutlich'."""
    rng = np.random.default_rng(seed)
    img = np.tile(tile, (2, 2, 1)).copy()
    strong = strength == "deutlich"
    px = set()
    main = crack_path(rng, 74, 4 if strong else 16, 76 if strong else 52, 0.35)
    px.update(main)
    deep = set(main[::2]) if strong else set(main[::3])
    if strong:
        bx, by = main[28]
        px.update(crack_path(rng, bx, by, 30, 0.5))                              # Zweig nach unten
        bx, by = main[52]
        px.update((bx - dx, by + dy) for dx, dy in crack_path(rng, 0, 0, 20, 0.5))   # zweiter, nach links
    else:
        bx, by = main[30]
        px.update(crack_path(rng, bx, by, 14, 0.5))                              # kurzer Zweig
    # Abplatzung: unregelmaessiger Fleck neben dem Riss
    cx, cy = (main[40][0] + 4, main[40][1]) if strong else (main[24][0] + 2, main[24][1])
    rx, ry = (9, 7) if strong else (4, 3)
    ys, xs = np.mgrid[0:128, 0:128]
    ang = np.arctan2(ys - cy, xs - cx)
    wob = 1 + 0.28 * np.sin(3 * ang + seed) + 0.18 * np.sin(5 * ang + 2 * seed)
    spall = ((xs - cx) / (rx * wob)) ** 2 + ((ys - cy) / (ry * wob)) ** 2 < 1
    img[spall] = C_SPALL
    top = spall & ~np.roll(spall, 1, axis=0)
    img[top] = C_SHADOW
    bottom = spall & ~np.roll(spall, -1, axis=0)
    right = spall & ~np.roll(spall, -1, axis=1)
    img[(bottom | right) & ~top] = C_RIM
    # Riss: Kern und Rand, helle Kante rechts/unten
    core = {p for p in px if 0 <= p[0] < 128 and 0 <= p[1] < 128}
    for (x, y) in core:
        img[y, x] = C_DEEP if ((x, y) in deep or strong) else C_MID
    if strong:
        for (x, y) in core:
            for dx, dy in ((-1, 0), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < 128 and 0 <= ny < 128 and (nx, ny) not in core and not spall[ny, nx] and rng.random() < 0.7:
                    img[ny, nx] = C_MID
    for (x, y) in core:
        nx, ny = x + 1, y + 1
        if 0 <= nx < 128 and 0 <= ny < 128 and (nx, ny) not in core and not spall[ny, nx] and rng.random() < (0.5 if strong else 0.25):
            img[ny, nx] = C_HI
    # Staub am Fuss (unterste 6 Pixel), nach oben seltener
    for r in range(6):
        p = (0.55 if strong else 0.35) * (1 - r / 6)
        row = 127 - (5 - r)
        mask = rng.random(128) < p
        img[row, mask] = C_DUST
    if mirror:
        img = img[:, ::-1]
    return img


# --- Geometrie ----------------------------------------------------------------------------------------------------

def panel_part():
    part = Part("panel", (0, 0, 0), UV)
    part.box((-1.0, 0.0, 1.006), (1.0, 2.0, 1.010), "panel")
    part.box((-1.0, 0.0, -1.010), (1.0, 2.0, -1.006), "panel")
    u0, u1 = part.rects["panel"][0], part.rects["panel"][2]
    for f in part.bm.faces:                       # Rueckseite (Blick nach +z): u waechst nach rechts = -x, also spiegeln
        if f.normal.y > 0.9:
            for l in f.loops:
                uv = l[part.uv].uv
                uv[0] = u0 + u1 - uv[0]
    return part


def debris_part(strong, seed):
    part = Part("debris", (0, 0, 0), UV)
    rng = np.random.default_rng(seed)
    n = 7 if strong else 3
    for side in (1, -1):
        for k in range(n):
            cx = rng.uniform(-0.45, 0.55)
            cz = side * (1.012 + rng.uniform(0.04, 0.28 if strong else 0.2))
            r = rng.uniform(0.035, 0.085 if strong else 0.06)
            h = rng.uniform(0.03, 0.08)
            pts = [(cx + r * math.cos(a) * rng.uniform(0.7, 1.2), 0.0, cz + r * math.sin(a) * rng.uniform(0.7, 1.2))
                   for a in np.linspace(0, 2 * math.pi, 6, endpoint=False)]
            part.prism(pts, (0, h, 0), "stone")
    return part


def chunk_part():
    """Ein Brocken um den Ursprung (etwa 0,25 m): ein Wuerfel mit versetzten Ecken, sechs Flaechen mit dem Wand-Stueck."""
    part = Part("chunk", (0, 0, 0), UV)
    rng = np.random.default_rng(5)
    s = 0.125
    c = {(i, j, k): V(i * s * rng.uniform(0.7, 1.15), j * s * rng.uniform(0.6, 1.1), k * s * rng.uniform(0.7, 1.15))
         for i in (-1, 1) for j in (-1, 1) for k in (-1, 1)}
    quads = [[(1, -1, -1), (1, 1, -1), (1, 1, 1), (1, -1, 1)], [(-1, -1, 1), (-1, 1, 1), (-1, 1, -1), (-1, -1, -1)],
             [(-1, 1, -1), (-1, 1, 1), (1, 1, 1), (1, 1, -1)], [(-1, -1, 1), (-1, -1, -1), (1, -1, -1), (1, -1, 1)],
             [(-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)], [(1, -1, -1), (-1, -1, -1), (-1, 1, -1), (1, 1, -1)]]
    part.convex([[c[k] for k in q] for q in quads], "stone")
    return part


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for var, name in WALL_FILES.items():
        tile = load_tile(name)
        for strength in ("dezent", "deutlich"):
            bpy.ops.wm.read_factory_settings(use_empty=True)
            coll = bpy.data.collections.new("Risse")
            bpy.context.scene.collection.children.link(coll)
            img = paint(tile, strength, seed={"a": 11, "b": 23, "c": 37}[var] + (100 if strength == "deutlich" else 0))
            colours = len({tuple(p) for p in img.reshape(-1, 3).astype(int)})
            label = f"crack_{var}_{strength}"
            image = save_image(label, img[::-1], os.path.join(OUT_DIR, f"{label}.png"))     # save_image: Zeile 0 unten
            mat = export_material(label, image)
            objs = [p.build(coll, mat, smooth=False) for p in (panel_part(), debris_part(strength == "deutlich", 3), chunk_part())]
            export_glb(objs, os.path.join(OUT_DIR, f"{label}.glb"))
            report_parts(label, objs)
            print(f"TEXTUR {label} {TEX}x{TEX} Farben {colours}")


main()
