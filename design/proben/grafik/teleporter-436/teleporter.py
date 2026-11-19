"""Probestueck Teleporter (Issue #436): drei Entwuerfe, je zwei Zustaende (ruhend / aktiv), Probenblatt und Bilder.

Alles entsteht aus diesem Skript: Geometrie, Textur-Atlas (nur Farben der Hauptpalette), glb-Modelle, Bilder.
Nichts wird heruntergeladen; das Spiel wird nicht angefasst.

Aufruf (aus dem Repository-Wurzelverzeichnis, ohne Argumente):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/teleporter-436/teleporter.py

Ausgabe in diesem Ordner: blatt.png, ansichten-<A|B|C>.png, spiel-<A|B|C>-<ruhend|aktiv>-<m>m.png,
modelle/teleporter_<A|B|C>_<ruhend|aktiv>.glb, texturen/atlas_<ruhend|aktiv>.png, masse.txt.
Koordinaten: Spiel-Achsen (x seitlich, y oben, z vorn = Vorderseite), Meter, Ursprung Mitte des Sockels am Boden.
"""

import json
import math
import os
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import blf  # noqa: E402
import bpy  # noqa: E402
import imbuf  # noqa: E402
from mathutils import Matrix  # noqa: E402
from psx import (V, G, FOG_COLOR, Part, rects_from_pixels, save_image, export_material, pass_material,  # noqa: E402
                 setup_render, camera, aim, render_passes, compose, save_png, write_light, stone_textures,
                 build_room, export_glb, to_game, MATS)

MODELS = os.path.join(ROOT, "assets", "models")
STATES = ("ruhend", "aktiv")
VARIANTS = ("A", "B", "C")
NAMES = {"A": "Bogentor", "B": "Säulenkreis", "C": "Hornkristall"}

PAL = {r["name"]: np.array(r["farben"], float)
       for k in ("grund", "akzent")
       for r in json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json")))[k]}
ST, EI, SF, HK, WK = PAL["Stein"], PAL["Eisen"], PAL["Stoff"], PAL["Haut/Knochen"], PAL["Werkstein"]
GOLD = PAL["Messing/Gold"][5]

# --- Textur-Atlas 64 x 64, 16 x 16 je Feld, nur Palettenfarben -------------------------------------------------------

FIELDS = {"stone": (0, 0), "werk": (16, 0), "dark": (32, 0), "iron": (48, 0),
          "glow": (0, 16), "rune": (16, 16), "cloth": (32, 16), "skin": (48, 16)}
UV = rects_from_pixels({k: (x, y, x + 16, y + 16) for k, (x, y) in FIELDS.items()}, 64)


def blocks(tones, mortar):
    out = np.zeros((16, 16, 3))
    for y in range(16):
        row = y // 4
        off = (row % 2) * 4
        for x in range(16):
            bx = (x + off) % 8
            out[y, x] = mortar if (y % 4 == 0 or bx == 0) else tones[(row * 3 + ((x + off) // 8) * 5 + 1) % len(tones)]
    return out


def field_glow(state):
    out = np.zeros((16, 16, 3))
    for y in range(16):
        for x in range(16):
            if state == "aktiv":
                out[y, x] = SF[5] if y < 5 else SF[6] if y < 10 else SF[7]
                if x in (7, 8):
                    out[y, x] = EI[7]
            else:
                out[y, x] = EI[2] if (y // 4) % 2 == 0 else EI[1]
    return out


def make_atlas(state):
    a = np.zeros((64, 64, 3)) + ST[1]

    def put(name, arr):
        x, y = FIELDS[name]
        a[y:y + 16, x:x + 16] = arr

    put("stone", blocks([ST[4], ST[5], ST[6], ST[7]], ST[2]))
    put("werk", blocks([WK[0], WK[1], WK[2], WK[3]], ST[11]))
    dark = np.zeros((16, 16, 3)) + ST[1]
    dark[(np.indices((16, 16)).sum(0) % 5) == 0] = ST[2]
    put("dark", dark)
    iron = np.zeros((16, 16, 3)) + EI[2]
    iron[(np.arange(16) % 4 < 2)] = EI[3]
    iron[:, 0] = EI[4]
    put("iron", iron)
    put("glow", field_glow(state))
    put("rune", np.zeros((16, 16, 3)) + (EI[7] if state == "aktiv" else EI[3]))
    put("cloth", np.zeros((16, 16, 3)) + SF[3])
    put("skin", np.zeros((16, 16, 3)) + HK[3])
    return a


# --- Bausteine -----------------------------------------------------------------------------------------------------

def oct_pts(r, y, rot=math.radians(22.5)):
    return [(r * math.cos(rot + k * math.pi / 4), y, r * math.sin(rot + k * math.pi / 4)) for k in range(8)]


def top_dark(n):
    return "dark" if n.y > 0.5 else "stone"


def dais(p):
    """Zwei Stufen: unten 0,12 m hoch (Radius 0,95), oben bis 0,20 m (Radius 0,78). Unter der Schritthoehe (0,4 m)."""
    p.prism(oct_pts(0.95, 0.0), (0, 0.12, 0), top_dark)
    p.prism(oct_pts(0.78, 0.08), (0, 0.12, 0), top_dark)


def pyramid_top(p, cx, cz, y, half_side, tip_y, rect):
    """Spitze auf einem quadratischen Schaft (Seite 2*half_side); Fuss 2 cm im Schaft."""
    p.spike((cx, y - 0.02, cz), (cx, tip_y, cz), half_side * math.sqrt(2), rect)


def octa(p, c, r, h, rect):
    cx, cy, cz = c
    e = [V(cx + r, cy, cz), V(cx, cy, cz + r), V(cx - r, cy, cz), V(cx, cy, cz - r)]
    top, bot = V(cx, cy + h, cz), V(cx, cy - h, cz)
    polys = []
    for k in range(4):
        polys += [[e[k], e[(k + 1) % 4], top], [e[k], e[(k + 1) % 4], bot]]
    p.convex(polys, rect)


def rune_slits(p, xs, ys, z, w=0.05, h=0.24, depth=0.03):
    """Schmale Runenschlitze, 5 mm in der Flaeche versenkt, 'depth' - 0,005 m davor. z = Lage der Flaeche (+ vorn)."""
    for x in xs:
        for y in ys:
            for s in (1, -1):
                a, b = s * (abs(z) - 0.005), s * (abs(z) - 0.005 + depth)
                p.box((x - w / 2, y, min(a, b)), (x + w / 2, y + h, max(a, b)), "rune")


def build_A(state_rects=UV):
    """A Bogentor: Spitzbogen aus Werkstein auf dem Sockel, im Bogen eine Platte (Schleier) - ruhend dunkles Eisen, aktiv kaltes Licht."""
    body = Part("body", (0, 0, 0), UV)
    dais(body)
    hv = 0.17
    yc = lambda x: 1.9 + (0.6 - x) * (0.8 / 0.6)
    for s in (-1, 1):
        body.box((s * 0.6 - 0.15, 0.15, -0.15), (s * 0.6 + 0.15, 0.5, 0.15), "werk")       # Fuss des Pfeilers
        body.box((s * 0.6 - 0.11, 0.15, -0.11), (s * 0.6 + 0.11, 1.9, 0.11), "werk")         # Pfeiler
        xs = [0.71, 0.71, 0.0, 0.0]
        ys = [yc(0.71) + hv, yc(0.71) - hv, yc(0.0) - hv, yc(0.0) + hv]
        body.prism([(s * x, y, -0.11) for x, y in zip(xs, ys)], (0, 0, 0.22), "werk")        # Bogenschenkel
    pyramid_top(body, 0, 0, 2.87, 0.11, 3.3, "werk")                                          # Spitze (Fiale) am Scheitel
    rune_slits(body, (-0.6, 0.6), (0.75, 1.15, 1.55), 0.11)
    glow = Part("glow", (0, 1.4, 0), UV)
    glow.prism([(-0.52, 0.15, -0.03), (0.52, 0.15, -0.03), (0.52, 2.0, -0.03), (0, 2.6, -0.03), (-0.52, 2.0, -0.03)],
               (0, 0, 0.06), "glow")
    return [body, glow]


def build_B(state_rects=UV):
    """B Saeulenkreis: vier Pfeiler mit Spitzen um einen Bodenring; ruhend dunkel, aktiv leuchten Ring und Spitzen."""
    body = Part("body", (0, 0, 0), UV)
    dais(body)
    glow = Part("glow", (0, 0.2, 0), UV)
    for sx in (-1, 1):
        for sz in (-1, 1):
            cx, cz = sx * 0.5, sz * 0.5
            body.box((cx - 0.15, 0.1, cz - 0.15), (cx + 0.15, 0.4, cz + 0.15), "werk")
            body.box((cx - 0.11, 0.1, cz - 0.11), (cx + 0.11, 1.9, cz + 0.11), "werk")
            pyramid_top(glow, cx, cz, 1.9, 0.11, 2.45, "glow")
            # Runenschlitz auf der Flaeche zur Mitte (x) und zur Mitte (z)
            for y in (0.7, 1.15, 1.6):
                body.box((cx - sx * 0.105, y, cz - 0.025), (cx - sx * 0.13, y + 0.24, cz + 0.025), "rune")
                body.box((cx - 0.025, y, cz - sz * 0.105), (cx + 0.025, y + 0.24, cz - sz * 0.13), "rune")
    glow.prism(oct_pts(0.56, 0.19), (0, 0.022, 0), "glow")        # Bodenring (aussen)
    body.prism(oct_pts(0.38, 0.19), (0, 0.033, 0), "dark")        # dunkle Scheibe verdeckt die Mitte: es bleibt ein Ring
    body.prism(oct_pts(0.14, 0.19), (0, 0.044, 0), "rune")        # Mitte
    return [body, glow]


def build_C(state_rects=UV):
    """C Hornkristall: zwei nach innen geneigte Horner halten einen schwebenden Kristall ueber dem Sockel."""
    body = Part("body", (0, 0, 0), UV)
    dais(body)
    for s in (-1, 1):
        a, b = V(s * 0.58, 0.15, 0), V(s * 0.30, 2.15, 0)
        d = (b - a).normalized()
        n = V(d.y, -d.x, 0) * s * s                       # senkrecht in der xy-Ebene
        w = 0.11
        pts = [a + n * w, a - n * w, b - n * w, b + n * w]
        body.prism([(p.x, p.y, -0.11) for p in pts], (0, 0, 0.22), "werk")
        body.spike(b - d * 0.02, b + d * 0.5, 0.11 * math.sqrt(2), "werk")
        for y0 in (0.6, 1.0, 1.4):
            x = s * (0.58 - 0.28 * (y0 + 0.12 - 0.15) / 2.0)
            for z in (1, -1):
                lo, hi = z * 0.1, z * 0.13
                body.box((x - 0.025, y0, min(lo, hi)), (x + 0.025, y0 + 0.24, max(lo, hi)), "rune")
    body.prism(oct_pts(0.3, 0.19), (0, 0.024, 0), "rune")         # Zeichen unter dem Kristall
    glow = Part("glow", (0, 1.3, 0), UV)
    octa(glow, (0, 1.3, 0), 0.21, 0.4, "glow")
    return [body, glow]


BUILD = {"A": build_A, "B": build_B, "C": build_C}


def build_figure():
    p = Part("figure", (0, 0, 0), UV)
    for s in (-1, 1):
        p.box((s * 0.02 + (0 if s > 0 else -0.18), 0.0, -0.1), (s * 0.02 + (0.18 if s > 0 else 0), 0.85, 0.1), "cloth")
        p.box((s * 0.25 + (0 if s > 0 else -0.11), 0.85, -0.08), (s * 0.25 + (0.11 if s > 0 else 0), 1.45, 0.08), "cloth")
    p.box((-0.25, 0.85, -0.14), (0.25, 1.5, 0.14), "cloth")
    p.box((-0.11, 1.52, -0.11), (0.11, 1.8, 0.11), "skin")
    return [p]


def put_in_scene(parts, mat, tag):
    coll = bpy.data.collections.new(tag)
    bpy.context.scene.collection.children.link(coll)
    objs = [p.build(coll, mat, smooth=False) for p in parts]
    for o in objs:
        HOME[o.name] = o.matrix_world.copy()
    return objs


HOME = {}


def place(objs, pos=(0, 0, 0)):
    for o in objs:
        o.matrix_world = Matrix.Translation(G(pos)) @ HOME[o.name]
    bpy.context.view_layer.update()
    for o in objs:
        write_light(o)


def tri_count(objs):
    return [len(o.data.polygons) for o in objs]


def extent(objs):
    lo, hi = np.full(3, 1e9), np.full(3, -1e9)
    for o in objs:
        for c in o.bound_box:
            g = to_game(o.matrix_world @ V(*c))
            lo, hi = np.minimum(lo, (g.x, g.y, g.z)), np.maximum(hi, (g.x, g.y, g.z))
    return lo, hi


# --- Blatt-Werkzeuge -------------------------------------------------------------------------------------------------

def text(img, s, x, y, size=30, color=(235, 235, 235), bold=False):
    f = blf.load("C:/Windows/Fonts/" + ("arialbd.ttf" if bold else "arial.ttf"))
    blf.size(f, size)
    w, h = blf.dimensions(f, s)
    W, H = int(w) + 8, int(size * 1.6)
    ib = imbuf.new((W, H))
    blf.color(f, 1, 1, 1, 1)
    blf.position(f, 4, int(size * 0.4), 0)
    with blf.bind_imbuf(f, ib):
        blf.draw_buffer(f, s)
    tmp = os.path.join(tempfile.gettempdir(), "teleporter_txt.png")
    ib.filepath = tmp
    imbuf.write(ib)
    im = bpy.data.images.load(tmp)
    im.colorspace_settings.name = "Non-Color"
    a = np.array(im.pixels[:], np.float32).reshape(H, W, 4)[::-1, :, 3]
    bpy.data.images.remove(im)
    hh, ww = img.shape[:2]
    ye, xe = min(hh, y + H), min(ww, x + W)
    m = (a[:ye - y, :xe - x] > 0.5)[..., None]
    img[y:ye, x:xe] = np.where(m, np.array(color, float), img[y:ye, x:xe])


def render(visible, cam, loc, target, fov, size, mode):
    setup_render(*size)
    cam.data.angle = math.radians(fov)
    aim(cam, loc, target)
    return compose(render_passes("tp", visible), mode)[::-1]


def blit(sheet, tile, x, y):
    sheet[y:y + tile.shape[0], x:x + tile.shape[1]] = tile


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    os.makedirs(os.path.join(HERE, "modelle"), exist_ok=True)
    os.makedirs(os.path.join(HERE, "texturen"), exist_ok=True)
    atlas = {s: make_atlas(s) for s in STATES}
    images = {s: save_image("atlas_" + s, atlas[s], os.path.join(HERE, "texturen", f"atlas_{s}.png")) for s in STATES}

    # 1. Modelle als glb (mit Textur wie im Spiel); Dreiecke zaehlen
    stats = {}
    for v in VARIANTS:
        for s in STATES:
            mat = export_material(f"tp_{v}_{s}", images[s])
            objs = put_in_scene(BUILD[v](), mat, f"exp_{v}_{s}")
            export_glb(objs, os.path.join(HERE, "modelle", f"teleporter_{v}_{s}.glb"))
            if s == "aktiv":
                lo, hi = extent(objs)
                stats[v] = (tri_count(objs), lo, hi)
            for o in objs:
                bpy.data.objects.remove(o, do_unlink=True)
    with open(os.path.join(HERE, "masse.txt"), "w", encoding="utf-8", newline="\n") as f:
        for v in VARIANTS:
            t, lo, hi = stats[v]
            f.write(f"{v} {NAMES[v]}: Dreiecke {t} (Summe {sum(t)}), x {lo[0]:.2f}..{hi[0]:.2f}, y {lo[1]:.2f}..{hi[1]:.2f}, "
                    f"z {lo[2]:.2f}..{hi[2]:.2f}\n")

    # 2. Szenen zum Rendern (Durchgangs-Material je Zustand)
    pm = {s: pass_material("pm_" + s, images[s]) for s in STATES}
    sets = {(v, s): put_in_scene(BUILD[v](), pm[s], f"r_{v}_{s}") for v in VARIANTS for s in STATES}
    for objs in sets.values():
        place(objs)
    cam = camera()
    stone = stone_textures(tempfile.mkdtemp(prefix="tp_stein_"))
    room = build_room(stone)

    def centre(v):
        lo, hi = stats[v][1], stats[v][2]
        return V(0, (lo[1] + hi[1]) / 2, 0)

    # 2a. Ansichten je Entwurf (aktiv): vorn, Seite, hinten, schraeg oben
    dirs = (("vorn", V(0, 0.08, 1)), ("Seite", V(1, 0.08, 0)), ("hinten", V(0, 0.08, -1)), ("schräg oben", V(0.6, 0.9, 0.7)))
    obl = V(0.7, 0.25, 0.7)
    tiles34 = {}
    for v in VARIANTS:
        c = centre(v)
        sh = np.zeros((40 + 440, 4 * 440, 3)) + FOG_COLOR
        text(sh, f"{v}  {NAMES[v]} - aktiv: vorn, Seite, hinten, schräg oben (ohne Nebel)", 8, 4, 26, bold=True)
        for k, (nm, d) in enumerate(dirs):
            t = render(sets[(v, "aktiv")], cam, G(c + d.normalized() * 6.5), G(c), 30, (440, 440), "ansicht")
            blit(sh, t, k * 440, 40)
            text(sh, nm, k * 440 + 6, 44, 26)
        save_png(sh[::-1], os.path.join(HERE, f"ansichten-{v}.png"))
        for s in STATES:
            tiles34[(v, s)] = render(sets[(v, s)], cam, G(c + obl.normalized() * 6.5), G(c), 30, (396, 440), "ansicht")

    # 2b. Spielbilder 854 x 480, 70 Grad, Augen 1,6 m, Nebel und Koernung
    game = {}
    for v in VARIANTS:
        for s, dists in (("ruhend", (4,)), ("aktiv", (2, 4, 10))):
            for dist in dists:
                img = render(list(sets[(v, s)]) + room, cam, G(V(0, 1.6, dist)), G(V(0, 1.6, 0)), 70, (854, 480), "spiel")
                game[(v, s, dist)] = img
                save_png(img[::-1], os.path.join(HERE, f"spiel-{v}-{s}-{dist}m.png"))

    # 2c. Massstab: Tuer, Spieler und die drei Entwuerfe (aktiv) nebeneinander
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS, "door_normal.glb"))
    door = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in door:
        image = next(n.image for n in o.active_material.node_tree.nodes if n.type == "TEX_IMAGE")
        o.data.materials.clear()
        o.data.materials.append(pass_material("door_" + o.name, image))
        o["smooth"] = False
        HOME[o.name] = o.matrix_world.copy()
    figure = put_in_scene(build_figure(), pm["aktiv"], "figur")
    floor = put_in_scene([_floor()], pm["aktiv"], "boden")
    xs = {"door": -4.3, "fig": -2.7, "A": -0.9, "B": 1.5, "C": 3.9}
    place(door, (xs["door"], 0, 0))
    place(figure, (xs["fig"], 0, 0))
    place(floor)
    scale_objs = door + figure + floor
    for v in VARIANTS:
        o2 = put_in_scene(BUILD[v](), pm["aktiv"], f"m_{v}")
        place(o2, (xs[v], 0, 0))
        scale_objs += o2
    SW, SH, cx, fov, dist, cy = 2460, 1000, -0.2, 33, 7.1, 1.7
    strip = render(scale_objs, cam, G(V(cx, cy, dist)), G(V(cx, cy, 0)), fov, (SW, SH), "ansicht")
    hw = 2 * dist * math.tan(math.radians(fov / 2))
    ww = hw * SW / SH
    px = lambda x: int((x - cx) / ww * SW + SW / 2)
    py = lambda y: int(SH / 2 - (y - cy) / hw * SH)
    for yy, lab in ((1.0, "1 m"), (2.0, "2 m"), (3.0, "3 m"), (3.5, "3,5 m Zellenhöhe")):
        row = py(yy)
        mask = np.all(np.abs(strip[row] - FOG_COLOR) < 3, axis=1)
        mask &= (np.arange(SW) // 14) % 2 == 0
        strip[row - 1:row + 1, mask] = (110, 96, 120)
        text(strip, lab, SW - 330 if yy == 3.5 else SW - 120, row - 34, 26, (160, 146, 170))
    cap = {"door": "Tür 2 × 3,5 m", "fig": "Spieler 1,8 m", "A": "A", "B": "B", "C": "C"}
    for k, label in cap.items():
        text(strip, label, px(xs[k]) - (60 if len(label) > 2 else 12), py(0) + 6, 40 if len(label) <= 2 else 28, bold=True)

    # 3. Blatt
    CW, GAP, M = 800, 30, 30
    W = M * 2 + 3 * CW + 2 * GAP
    head, row1, grow = 150, 44 + 440, 44 + 450
    colh = head + row1 + 2 * grow + 150
    recoh = 330
    Hh = 100 + colh + 44 + SH + recoh
    sheet = np.zeros((Hh, W, 3)) + FOG_COLOR
    text(sheet, "SILVER & ASH - Teleporter, Entwurf zu Issue 436: drei Varianten, je ruhend und aktiv", M, 18, 54, bold=True)
    facts = {}
    for v in VARIANTS:
        t, lo, hi = stats[v]
        facts[v] = (f"{hi[0] - lo[0]:.1f} × {hi[2] - lo[2]:.1f} × {hi[1]:.1f} m (B × T × H), {sum(t)} Dreiecke")
    notes = {
        "A": ("Tor zum Durchtreten, kräftigste Silhouette", "Sperrt: zwei Pfeiler (Kollision)", "Aktiv: heller Schleier im Bogen"),
        "B": ("Offener Kreis, von allen Seiten gleich", "Sperrt: nur vier Pfeiler", "Aktiv: Bodenring und vier Spitzen"),
        "C": ("Schwebender Kristall zwischen zwei Hörnern", "Sperrt: zwei Hörner", "Aktiv: Kristall und Runen hell"),
    }
    for k, v in enumerate(VARIANTS):
        x0, y0 = M + k * (CW + GAP), 100
        text(sheet, f"{v}", x0, y0 - 6, 90, GOLD, True)
        text(sheet, NAMES[v], x0 + 95, y0 + 14, 54, bold=True)
        y = y0 + head - 40
        for j, s in enumerate(STATES):
            blit(sheet, tiles34[(v, s)], x0 + j * 404, y + 44)
            text(sheet, s.upper(), x0 + j * 404 + 6, y + 2, 34, GOLD if s == "aktiv" else (235, 235, 235), True)
        y += row1
        for dist in (4, 10):
            blit(sheet, game[(v, "aktiv", dist)][15:465, 27:827], x0, y + 44)
            text(sheet, f"AKTIV, im Spiel aus {dist} m (Nebel, Körnung)", x0 + 6, y + 2, 32, bold=True)
            y += grow
        for line in (facts[v],) + notes[v]:
            text(sheet, line, x0, y, 31)
            y += 40
    y = 100 + colh
    text(sheet, "MASSSTAB: Tür, Spieler und alle drei Entwürfe (aktiv), Linien bei 1, 2, 3 und 3,5 m", M, y, 38, bold=True)
    blit(sheet, strip, (W - SW) // 2, y + 44)
    y += 44 + SH + 14
    reco = [
        ("EMPFEHLUNG: A  Bogentor", GOLD, True, 50),
        ("Auch aus 10 m liest sich A als helles Tor vor dem Nebel; B und C sind dort dünner und gehen eher unter.", (235,) * 3, False, 33),
        ("A trägt die Spielgeste 'hindurchtreten' schon in der Form; der Sockel (0,2 m) ist begehbar, nur die Pfeiler sperren.", (235,) * 3, False, 33),
        ("Alle drei: nur Palettenfarben (Stein, Werkstein, Eisen, Stoff); aktiv kaltes Blau-Weiß, kein Rot, kein Gold.", (235,) * 3, False, 33),
        ("Vorläufige Annahmen und offene Fragen an den Menschen: siehe notizen.md.", (235,) * 3, False, 33),
    ]
    for line, col, bold, size in reco:
        text(sheet, line, M, y, size, col, bold)
        y += size + 24
    save_png(sheet[::-1], os.path.join(HERE, "blatt.png"))


def _floor():
    p = Part("floor", (0, 0, 0), UV)
    p.box((-6.2, -0.08, -1.5), (6.2, 0.0, 1.5), "dark")
    return p


main()
