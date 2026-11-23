"""Probestueck Ash Altar (Issue #615): drei Entwuerfe a, b, c, rein als Probe ausserhalb des Spiels.

Alles aus einem Skript: Geometrie, Textur (nur Farben der Hauptpalette), drei .glb, Ansichten und das Probenblatt.
Nichts heruntergeladen, nichts in assets/ geaendert.

Aufruf (aus dem Repository-Wurzelverzeichnis, ohne Argumente):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/altar-615/altar.py

Jedes Modell hat zwei Teile: "body" (Stein, Silber, Asche, Wachs) und "glut" (Glut, Flammen, Fenster). Beim Rendern ist
"glut" selbstleuchtend (Licht = 1); das Spiel soll es ohne Laterne zeichnen, wie den Kristall des Teleporters.
Koordinaten: Spiel-Achsen (x seitlich, y oben, z vorn = Blickseite), Meter, Ursprung Mitte des Fusses am Boden.
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
                 setup_render, camera, aim, render_passes, compose, save_png, stone_textures,
                 build_room, export_glb, to_game, MATS)

OUT = HERE
DESIGNS = ("a", "b", "c")
TITLE = {"a": "a  Aschenblock", "b": "b  Aschenkelch", "c": "c  Gruftschrein"}

PAL = {r["name"]: np.array(r["farben"], float)
       for k in ("grund", "akzent")
       for r in json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json")))[k]}
ST, EI, SF, HK, MG = PAL["Stein"], PAL["Eisen"], PAL["Stoff"], PAL["Haut/Knochen"], PAL["Messing/Gold"]

# --- Textur-Atlas 64 x 64, 16 x 16 je Feld, nur Palettenfarben -------------------------------------------------------

FIELDS = {"stone": (0, 0), "dark": (16, 0), "iron": (32, 0), "silver": (48, 0),
          "ash": (0, 16), "ember": (16, 16), "flame": (32, 16), "wax": (48, 16),
          "gold": (0, 32), "cloth": (16, 32)}
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


def speckle(base, tones, step=3):
    out = np.zeros((16, 16, 3)) + base
    for y in range(16):
        for x in range(16):
            k = (x * 7 + y * 5 + (x * y) % 3) % step
            if k:
                out[y, x] = tones[k - 1]
    return out


def make_atlas():
    a = np.zeros((64, 64, 3)) + ST[1]

    def put(name, arr):
        x, y = FIELDS[name]
        a[y:y + 16, x:x + 16] = arr

    put("stone", blocks([ST[4], ST[5], ST[6], ST[7]], ST[2]))
    dark = np.zeros((16, 16, 3)) + ST[1]
    dark[(np.indices((16, 16)).sum(0) % 5) == 0] = ST[2]
    put("dark", dark)
    iron = np.zeros((16, 16, 3)) + EI[2]
    iron[(np.arange(16) % 4 < 2)] = EI[3]
    iron[:, 0] = EI[4]
    put("iron", iron)
    silver = np.zeros((16, 16, 3)) + EI[5]
    silver[(np.arange(16) % 5 == 0)] = EI[6]
    silver[:, 0] = EI[7]
    put("silver", silver)
    put("ash", speckle(ST[9], (ST[8], ST[10])))
    put("ember", speckle(MG[4], (MG[3], MG[5]), 3))
    flame = np.zeros((16, 16, 3)) + MG[5]
    flame[:8] = MG[6]
    flame[12:] = MG[4]
    put("flame", flame)
    put("wax", np.zeros((16, 16, 3)) + HK[min(5, len(HK) - 1)])
    gold = np.zeros((16, 16, 3)) + MG[5]
    gold[:, 7:9] = MG[7]
    gold[:3] = MG[6]
    put("gold", gold)
    put("cloth", np.zeros((16, 16, 3)) + SF[3])
    return a


# --- Bausteine -----------------------------------------------------------------------------------------------------

def oct_pts(r, y, rot=math.radians(22.5)):
    return [(r * math.cos(rot + k * math.pi / 4), y, r * math.sin(rot + k * math.pi / 4)) for k in range(8)]


def octa(p, c, r, h, rect):
    cx, cy, cz = c
    e = [V(cx + r, cy, cz), V(cx, cy, cz + r), V(cx - r, cy, cz), V(cx, cy, cz - r)]
    top, bot = V(cx, cy + h, cz), V(cx, cy - h, cz)
    polys = []
    for k in range(4):
        polys += [[e[k], e[(k + 1) % 4], top], [e[k], e[(k + 1) % 4], bot]]
    p.convex(polys, rect)


def candle(body, glut, x, z, y0, h):
    """Wachskerze (Quader 7 cm) mit kleiner Flamme (Messing, wie die Kerzenfarbe der Art Bible)."""
    body.box((x - 0.035, y0 - 0.02, z - 0.035), (x + 0.035, y0 + h, z + 0.035), "wax")
    glut.spike((x, y0 + h - 0.02, z), (x, y0 + h + 0.17, z), 0.06, "flame")


def bowl(body, cx, cz, y, r):
    """Flache achteckige Silberschale: Fuss, Wand, Aschefuellung (Oberflaeche leicht unter dem Rand)."""
    body.prism([(cx + p[0] * 0.55, y, cz + p[2] * 0.55) for p in oct_pts(r, 0)], (0, 0.05, 0), "silver")
    body.prism([(cx + p[0], y + 0.04, cz + p[2]) for p in oct_pts(r, 0)], (0, 0.09, 0), "silver")
    body.prism([(cx + p[0] * 0.82, y + 0.12, cz + p[2] * 0.82) for p in oct_pts(r, 0)], (0, 0.02, 0), "ash")


def design_a():
    """Aschenblock: huefthoher Steinblock (wie im Entwurf), Silberschale mit Asche und Glut, zwei Kerzen."""
    body, glut = Part("body", (0, 0, 0), UV), Part("glut", (0, 0, 0), UV)
    body.box((-0.55, 0, -0.40), (0.55, 0.12, 0.40), "stone")
    body.box((-0.46, 0.12, -0.31), (0.46, 0.92, 0.31), "stone")
    body.box((-0.28, 0.32, 0.30), (0.28, 0.72, 0.325), "dark")                 # dunkle Nische vorn
    body.box((-0.55, 0.92, -0.40), (0.55, 1.04, 0.40), "stone")
    bowl(body, 0, 0, 1.04, 0.36)
    octa(glut, (0, 1.22, 0), 0.11, 0.11, "ember")
    octa(glut, (-0.17, 1.19, 0.07), 0.07, 0.06, "ember")
    octa(glut, (0.16, 1.19, -0.05), 0.07, 0.06, "ember")
    candle(body, glut, -0.43, -0.12, 1.04, 0.40)
    candle(body, glut, 0.43, -0.12, 1.04, 0.40)
    return [body, glut]


def design_b():
    """Aschenkelch: achteckiger Sockel, breite Silberschale mit Aschehuegel, vier Eisenspitzen, zwei Flammen vorn."""
    body, glut = Part("body", (0, 0, 0), UV), Part("glut", (0, 0, 0), UV)
    body.prism(oct_pts(0.80, 0.0), (0, 0.12, 0), "stone")
    body.prism(oct_pts(0.50, 0.10), (0, 0.10, 0), "stone")
    body.prism(oct_pts(0.30, 0.18), (0, 0.66, 0), "stone")
    body.prism(oct_pts(0.40, 0.84), (0, 0.08, 0), "stone")
    body.prism(oct_pts(0.40, 0.90), (0, 0.04, 0), "iron")
    body.prism(oct_pts(0.66, 1.00), (0, 0.10, 0), "silver")
    body.prism(oct_pts(0.74, 1.08), (0, 0.06, 0), "silver")
    body.spike((0, 1.10, 0), (0, 1.34, 0), 0.60, "ash", sides=8)
    for sx, sz in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        x, z = sx * 0.44, sz * 0.44
        body.box((x - 0.035, 1.05, z - 0.035), (x + 0.035, 1.48, z + 0.035), "iron")
        body.spike((x, 1.46, z), (x, 1.70, z), 0.05, "iron")
    for x in (-0.24, 0.24):
        octa(glut, (x, 1.24, 0.18), 0.07, 0.07, "ember")
    octa(glut, (0, 1.30, -0.05), 0.07, 0.08, "ember")
    for sx in (-1, 1):
        glut.spike((sx * 0.44, 1.42, 0.44), (sx * 0.44, 1.60, 0.44), 0.075, "flame")
    return [body, glut]


def design_c():
    """Gruftschrein: breiter Steinschrein mit Spitzgiebel dahinter, goldenes Glutfenster, kleine Schale, zwei Kerzen."""
    body, glut = Part("body", (0, 0, 0), UV), Part("glut", (0, 0, 0), UV)
    body.box((-0.85, 0, -0.40), (0.85, 0.14, 0.40), "stone")
    body.box((-0.75, 0.14, -0.32), (0.75, 0.86, 0.32), "stone")
    for x in (-0.40, 0.0, 0.40):
        body.box((x - 0.10, 0.30, 0.31), (x + 0.10, 0.68, 0.335), "dark")
    body.box((-0.82, 0.86, -0.38), (0.82, 0.96, 0.38), "stone")
    gable = [(-0.70, 0.94, -0.38), (0.70, 0.94, -0.38), (0.70, 1.72, -0.38), (0.0, 2.15, -0.38), (-0.70, 1.72, -0.38)]
    body.prism(gable, (0, 0, 0.14), "stone")
    for sx in (-1, 1):
        body.box((sx * 0.70 - 0.07, 1.70, -0.38), (sx * 0.70 + 0.07, 1.90, -0.24), "stone")
        body.spike((sx * 0.70, 1.88, -0.31), (sx * 0.70, 2.20, -0.31), 0.10, "stone")
    window = [(-0.20, 1.10, -0.245), (0.20, 1.10, -0.245), (0.20, 1.56, -0.245), (0.0, 1.88, -0.245), (-0.20, 1.56, -0.245)]
    glut.prism(window, (0, 0, 0.03), "gold")
    bowl(body, 0, 0.02, 0.96, 0.24)
    octa(glut, (0, 1.14, 0.02), 0.06, 0.07, "ember")
    candle(body, glut, -0.52, 0.05, 0.96, 0.26)
    candle(body, glut, 0.52, 0.05, 0.96, 0.26)
    return [body, glut]


BUILD = {"a": design_a, "b": design_b, "c": design_c}


def figure():
    f = Part("figur", (0, 0, 0), UV)
    f.box((-0.30, 0, -0.20), (0.30, 1.8, 0.20), "cloth")
    return [f]


HOME = {}


def put_in_scene(parts, mat, tag):
    coll = bpy.data.collections.new(tag)
    bpy.context.scene.collection.children.link(coll)
    objs = [p.build(coll, mat, smooth=False) for p in parts]
    for o in objs:
        HOME[o.name] = o.matrix_world.copy()
    return objs


def write_light(o):
    """Licht je Ecke: Koerper nach Normale (wie im Spiel), Glut selbstleuchtend."""
    import psx
    if o.name.startswith("glut"):
        mesh = o.data
        attr = mesh.color_attributes.get("Light") or mesh.color_attributes.new("Light", "FLOAT_COLOR", "CORNER")
        for li in range(len(mesh.loops)):
            attr.data[li].color = (1, 1, 1, 1)
    else:
        psx.write_light(o)


def place(objs, pos=(0, 0, 0)):
    for o in objs:
        o.matrix_world = Matrix.Translation(G(pos)) @ HOME[o.name]
    bpy.context.view_layer.update()
    for o in objs:
        write_light(o)


def tri_count(objs):
    return {o.name.split(".")[0]: len(o.data.polygons) for o in objs}


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
    tmp = os.path.join(tempfile.gettempdir(), "altar_txt.png")
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
    return compose(render_passes("al", visible), mode)[::-1]


def blit(sheet, tile, x, y):
    sheet[y:y + tile.shape[0], x:x + tile.shape[1]] = tile


def count_colors(arr):
    return len({tuple(int(v) for v in px) for px in arr.reshape(-1, 3)})


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    atlas = make_atlas()
    print("Farben im Atlas:", count_colors(atlas), "Groesse", atlas.shape[:2])
    images = {s: save_image("altar_" + s, atlas, os.path.join(OUT, f"altar_{s}.png")) for s in DESIGNS}
    for s in DESIGNS:
        objs = put_in_scene(BUILD[s](), export_material("al_" + s, images[s]), "exp_" + s)
        export_glb(objs, os.path.join(OUT, f"altar_{s}.glb"))
        lo, hi = extent(objs)
        t = tri_count(objs)
        print(s, "Dreiecke", t, "Summe", sum(t.values()),
              "x %.2f..%.2f y %.2f..%.2f z %.2f..%.2f" % (lo[0], hi[0], lo[1], hi[1], lo[2], hi[2]))
        for o in objs:
            bpy.data.objects.remove(o, do_unlink=True)

    pm = pass_material("pm_altar", images["a"])
    sets = {s: put_in_scene(BUILD[s](), pm, "r_" + s) for s in DESIGNS}
    fig = put_in_scene(figure(), pm, "r_fig")
    for objs in sets.values():
        place(objs)
    cam = camera()
    room = build_room(stone_textures(tempfile.mkdtemp(prefix="al_stein_")))

    # Probenblatt: je Entwurf eine Zeile, links aus 3 m, rechts aus 8 m (Spielbild 854 x 480, Ausschnitt)
    cw, ch = 480, 300
    gap, head, rowh = 12, 56, ch + 40
    sheet = np.zeros((head + 3 * rowh + 30, 2 * cw + gap + 20, 3)) + FOG_COLOR
    text(sheet, "Ash Altar, Probestück (Issue 615): Spielbild aus 3 m und 8 m, Quader = 1,8 m", 10, 8, 26, bold=True)
    for r, s in enumerate(DESIGNS):
        y0 = head + r * rowh
        text(sheet, TITLE[s] + f"   ({sum(tri_count(sets[s]).values())} Dreiecke)", 10, y0, 24, bold=True)
        for k, d in enumerate((3, 8)):
            place(fig, (-1.4, 0, 0))
            img = render(sets[s] + fig + room, cam, G(V(0, 1.6, d)), G(V(0, 1.6, 0)), 70, (854, 480), "spiel")
            save_png(img[::-1], os.path.join(OUT, f"spiel-{s}-{d}m.png"))
            if d == 3:
                tile = img[110:110 + ch, 187:187 + cw]
            else:
                c = img[165:165 + ch // 2, 307:307 + cw // 2]
                tile = np.repeat(np.repeat(c, 2, axis=0), 2, axis=1)
            x = 10 + k * (cw + gap)
            blit(sheet, tile, x, y0 + 38)
            text(sheet, f"{d} m" + (" (Ausschnitt)" if d == 3 else " (Ausschnitt, 2-fach vergrößert)"), x + 4, y0 + 38, 18)
    save_png(sheet[::-1], os.path.join(OUT, "blatt.png"))

    # Ansichten: vorn, Seite, hinten, schraeg oben (ohne Nebel), mit Figur als Massstab
    tile = 360
    views = (("vorn", V(0, 0.1, 1)), ("Seite", V(1, 0.1, 0)), ("hinten", V(0, 0.1, -1)), ("schräg oben", V(0.6, 0.9, 0.7)))
    sheet2 = np.zeros((50 + 3 * (tile + 40), 4 * tile, 3)) + FOG_COLOR
    text(sheet2, "Ash Altar - vorn, Seite, hinten, schräg oben (ohne Nebel); blauer Quader daneben: 1,8 m", 10, 8, 24, bold=True)
    c = V(0, 1.0, 0)
    for r, s in enumerate(DESIGNS):
        y0 = 50 + r * (tile + 40)
        text(sheet2, TITLE[s], 10, y0, 22, bold=True)
        for k, (nm, d) in enumerate(views):
            place(fig, (-1.5, 0, 0) if nm != "Seite" else (0, 0, -1.5))
            t = render(sets[s] + fig, cam, G(c + d.normalized() * 6.0), G(c), 30, (tile, tile), "ansicht")
            blit(sheet2, t, k * tile, y0 + 40)
            save_png(t[::-1], os.path.join(OUT, f"ansicht-{s}-{k + 1}.png"))
    save_png(sheet2[::-1], os.path.join(OUT, "ansichten.png"))


main()
