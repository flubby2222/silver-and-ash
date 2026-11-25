"""Probestueck Whip (Issue #202), Runde 1: drei Entwuerfe der Jaegerpeitsche, je in Ruhe und im Schlag, und das Vergleichsblatt.

Erzeugt aus diesem einen Skript (nichts heruntergeladen, nur Blender-Python und NumPy):
    peitsche-a-kette-ruhe.glb / -schlag.glb      A  Kettenpeitsche: Eisenglieder, Silbergewicht am Ende
    peitsche-b-leder-ruhe.glb / -schlag.glb      B  geflochtene Lederpeitsche, die duenner wird, Silberspitze
    peitsche-c-klingen-ruhe.glb / -schlag.glb    C  Klingenpeitsche: Folge von Silberblaettern, je um 90 Grad verdreht
    blatt.png        Vergleichsblatt (Ruhe, Ich-Sicht Ruhe und Schlag, Streckung neben einer 1,8-m-Figur, Silhouetten)
    ansichten.png    je Entwurf in Ruhe vorn, Seite, hinten, schraeg oben, dazu die gestreckte Peitsche im Nahbild
    ich-<entwurf>-<ruhe|schlag>.png   die Ich-Sicht in Spielaufloesung (854 x 480)

Aufbau: ein Knoten "handle" (Griff, Drehpunkt = Ursprung der Hand) und je Glied ein eigener Knoten "seg01" ... mit Drehpunkt am
Gliedanfang, jedes Glied haengt am vorigen (Eltern-Teil im Objekt-Attribut "parent"). Alle Glieder sind starr. Die Ruhe-Datei
hat dieselben Glieder in gleicher Zahl, aber kuerzer und zu einer Schlaufe gelegt (so haengt sie in der Hand); die Schlag-Datei
hat sie gerade entlang +z. Massstab 1,8 eingebacken wie bei allen Waffen, Textur 128 x 128 ohne Filterung, nur Hauptpalette,
flach schattiert. Der Aermel (Hand) wird NICHT modelliert; fuer die Ich-Sicht kommt der vorhandene club_hand.glb dazu.

Aufruf (aus dem Repository-Wurzelverzeichnis, TEMP/TMP auf einen Zwischenordner setzen):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/whip-202/runde-1/peitsche.py
Mit der Umgebungsvariable PEITSCHE_AUS laesst sich ein anderer Zielordner waehlen (Bit-Vergleich zweier Laeufe).
"""

import json
import math
import os
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE)))))
OUT = os.environ.get("PEITSCHE_AUS") or HERE
os.makedirs(OUT, exist_ok=True)
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import blf  # noqa: E402
import bmesh  # noqa: E402,F401
import bpy  # noqa: E402
import imbuf  # noqa: E402
from bpy_extras.object_utils import world_to_camera_view  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from psx import (V, G, FOG_COLOR, Part, tube, rects_from_pixels, vnoise, save_image, export_material,  # noqa: E402
                 pose, bake_scale, export_glb, setup_render, camera, render_passes, compose, coverage, save_png,
                 pass_material, write_light, stone_textures, build_room, MATS, raise_fwd, to_game)

MODELS = os.path.join(ROOT, "assets", "models")
TEX = 128
SCALE = 1.8                      # wie alle Waffen: Dateimass = 1,8 x Spielmass
HANDLE_TOP = 0.22                # Griffende = Anfang der Gliederkette (Spielmass)

# --- Palette (nur Hauptpalette, assets/palette/hauptpalette.json; hoechstens 16 Farben im Atlas) ----------------------------

_PAL = json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))
_ROWS = {r["name"]: np.array(r["farben"], float) for r in _PAL["akzent"]}
WOOD = _ROWS["Holz"][1:5]              # 4 Stufen
LEATHER = _ROWS["Holz"][1:5]
STEEL = _ROWS["Eisen"][1:5]            # geschwaerzter Stahl, 4 Stufen
DARKSTEEL = _ROWS["Eisen"][0:3]        # eine Stufe neu (20,18,28)
SILVER = _ROWS["Eisen"][5:8]           # Silber: helle Eisen-Stufen (Art Bible: Silber gehoert dem Jaeger)
GOLD = _ROWS["Messing/Gold"][3:6]      # nur kleine Beschlaege (drei Stufen)

RECTS = {
    "iron": (0, 0, 96, 48),
    "dark": (96, 0, 128, 48),
    "silver": (0, 48, 64, 72),
    "gold": (64, 48, 128, 72),
    "grip": (0, 72, 64, 96),
    "wood": (0, 96, 128, 128),
}
UV = rects_from_pixels(RECTS, TEX)


def step(ramp, field):
    """Feld 0..1 in harten Stufen auf die Rampe legen: nur Palettenfarben, kein Mischen."""
    i = np.clip((np.asarray(field) * len(ramp)).astype(int), 0, len(ramp) - 1)
    return ramp[i]


def paint_wood(U, V_):
    h, w = U.shape
    f = 0.15 + 0.7 * vnoise(h, w, 2, 24, 201)
    f = np.where(vnoise(h, w, 9, 30, 202) < 0.14, 0.0, f)
    f = np.where(vnoise(h, w, 4, 6, 203) > 0.86, 0.95, f)
    return step(WOOD, f)


def paint_wrap(U, V_):
    """Schraege Lederwicklung (Griff)."""
    h, w = U.shape
    wrap = (U * 2.0 + V_ * 5) % 1.0
    f = np.where(wrap > 0.2, 0.15 + 0.3 * vnoise(h, w, 3, 8, 204), 0.95)
    return step(LEATHER, f)


def paint_braid(U, V_):
    """Geflecht: zwei gegenlaeufige Schraegen ergeben Rauten, dunkle Fugen dazwischen."""
    a = (U * 6 + V_ * 3) % 1.0
    b = (U * 6 - V_ * 3) % 1.0
    f = np.where((a < 0.5) ^ (b < 0.5), 0.78, 0.40)
    f = np.where((a < 0.10) | (b < 0.10), 0.05, f)
    return step(LEATHER, f)


def paint_silver(U, V_):
    h, w = U.shape
    f = 0.35 + 0.55 * vnoise(h, w, 4, 16, 205)
    f = np.where(np.abs(V_ - 0.5) < 0.08, 1.0, f)
    return step(SILVER, f)


def paint_gold(U, V_):
    h, w = U.shape
    return step(GOLD, 0.25 + 0.7 * vnoise(h, w, 4, 10, 206))


def paint_dark(U, V_):
    h, w = U.shape
    return step(DARKSTEEL, 0.2 + 0.7 * vnoise(h, w, 5, 5, 207))


def paint_iron(U, V_):
    h, w = U.shape
    f = 0.34 + 0.30 * vnoise(h, w, 3, 4, 208)
    f = np.where(vnoise(h, w, 7, 7, 209) < 0.08, 0.05, f)           # vereinzelte Pocken
    f = np.where(vnoise(h, w, 5, 9, 210) > 0.93, 0.98, f)           # blanke Stellen
    return step(STEEL, f)


def make_atlas(painters):
    atlas = np.zeros((TEX, TEX, 3)) + DARKSTEEL[0]
    for name, (x0, y0, x1, y1) in RECTS.items():
        pw, ph = x1 - x0, y1 - y0
        U, V_ = np.meshgrid((np.arange(pw) + 0.5) / pw, (np.arange(ph) + 0.5) / ph)
        atlas[y0:y1, x0:x1] = painters[name](U, V_)
    return atlas


# --- Geometrie ------------------------------------------------------------------------------------------------------------

class Fr:
    """Rahmen eines Gliedes: Ursprung o, Achse a (Richtung der Kette), oben u, Seite s. pt(x, y, z) -> Spielkoordinate."""

    def __init__(self, o, a, ref, roll=0.0):
        a = a.normalized()
        u = (ref - a * ref.dot(a)).normalized()
        r = math.radians(roll)
        u = (u * math.cos(r) + a.cross(u) * math.sin(r)).normalized()
        self.o, self.a, self.u = o, a, u
        self.s = u.cross(a)

    def pt(self, x, y, z):
        return self.o + self.s * x + self.u * y + self.a * z

    def line(self, zs):
        return [self.pt(0, 0, z) for z in zs]


def rod(part, fr, zs, radii, rect, sides=6):
    """Rotationskoerper um die Achse des Rahmens; radii je Ring (Zahl oder (breit, unten, oben))."""
    shapes = [r if isinstance(r, tuple) else (r, r, r) for r in radii]
    part.loft(tube(fr.line(zs), shapes, sides, ref_up=fr.u), rect)


def flat_plate(part, fr, pts, y0, y1, rect):
    """Platte: konvexes Vieleck pts (x, z) in der Ebene des Rahmens, in y von y0 bis y1 ausgezogen. Geschlossen."""
    a = [fr.pt(x, y0, z) for x, z in pts]
    b = [fr.pt(x, y1, z) for x, z in pts]
    n = len(a)
    polys = [[a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]] for k in range(n)] + [list(a), list(reversed(b))]
    part.convex(polys, rect)


def main_fr():
    return Fr(V(0, 0, 0), V(0, 0, 1), V(0, 1, 0))


# --- Griffe (gerade entlang +z, Drehpunkt = Ursprung) -------------------------------------------------------------------------

def handle_a(part):
    fr = main_fr()
    rod(part, fr, [-0.052, -0.040, 0.0, 0.10, 0.17], [0.011, 0.013, 0.0125, 0.0125, 0.0130], "wood", 8)
    rod(part, fr, [-0.040, -0.030, 0.0, 0.07, 0.11], [0.0145, 0.0150, 0.0150, 0.0150, 0.0145], "grip", 8)
    rod(part, fr, [-0.074, -0.066, -0.058, -0.048], [0.003, 0.0185, 0.0175, 0.010], "gold", 8)         # Knauf (klein, Gold)
    rod(part, fr, [0.150, 0.170, 0.190, 0.212, 0.222], [0.0140, 0.0175, 0.0165, 0.0200, 0.0120], "dark", 8)   # Kopf mit Wulst


def handle_b(part):
    fr = main_fr()
    rod(part, fr, [-0.052, -0.040, 0.0, 0.12, 0.17], [0.011, 0.012, 0.0115, 0.0115, 0.0120], "wood", 8)
    rod(part, fr, [-0.044, -0.030, 0.0, 0.08, 0.125], [0.0160, 0.0150, 0.0140, 0.0135, 0.0140], "grip", 8)
    rod(part, fr, [-0.078, -0.070, -0.056, -0.044], [0.0030, 0.0120, 0.0240, 0.0150], "grip", 8)      # ausgestellter Lederknauf
    rod(part, fr, [0.126, 0.134, 0.152, 0.170], [0.0150, 0.0165, 0.0165, 0.0140], "silver", 8)       # Silberzwinge
    rod(part, fr, [0.168, 0.190, 0.210, 0.222], [0.0120, 0.0150, 0.0130, 0.0100], "grip", 8)          # Lederkopf


def handle_c(part):
    fr = main_fr()
    rod(part, fr, [-0.052, -0.040, 0.0, 0.10, 0.17], [0.010, 0.0125, 0.0120, 0.0120, 0.0125], "wood", 8)
    rod(part, fr, [-0.040, -0.030, 0.0, 0.06, 0.10], [0.0140, 0.0145, 0.0145, 0.0145, 0.0140], "grip", 8)
    rod(part, fr, [-0.082, -0.070, -0.054, -0.046], [0.0020, 0.0100, 0.0160, 0.0100], "silver", 8)     # Silberknauf, spitz
    flat_plate(part, fr, [(-0.052, 0.112), (0.052, 0.112), (0.052, 0.126), (-0.052, 0.126)], -0.0085, 0.0085, "dark")  # Parierstange
    rod(part, fr, [0.126, 0.132, 0.140, 0.146], [0.0150, 0.0175, 0.0175, 0.0150], "gold", 8)            # goldener Ring
    rod(part, fr, [0.150, 0.190, 0.222], [0.0150, 0.0170, 0.0110], "dark", 8)


# --- Glieder (Rahmen: Glied von z = 0 bis L entlang der Achse) ---------------------------------------------------------------

def seg_a(part, fr, i, n, L, rest):
    """Kettenglied: Hundeknochen mit flachen Enden; jedes zweite um 90 Grad gedreht und dunkler. Letztes Glied: Silbergewicht."""
    k = 0.6 if rest else 1.4
    kz = min(0.024, 0.3 * L)
    if i == n - 1:
        rod(part, fr, [-0.012, 0.10 * L, 0.40 * L, 0.80 * L, L + 0.02], [0.012 * k, 0.052 * k, 0.042 * k, 0.020 * k, 0.0006], "silver", 6)
        return
    rect = "iron" if i % 2 == 0 else "dark"
    rod(part, fr, [-0.012, kz, 0.5 * L, L - kz, L + 0.012],
        [(0.016 * k, 0.010 * k, 0.010 * k), (0.040 * k, 0.014 * k, 0.014 * k), (0.017 * k, 0.011 * k, 0.011 * k),
         (0.040 * k, 0.014 * k, 0.014 * k), (0.016 * k, 0.010 * k, 0.010 * k)], rect, 6)


def seg_b(part, fr, i, n, L, rest):
    """Geflechtglied: Schaft mit Wulst an beiden Enden, nach hinten duenner. Letztes Glied: Lederschaft und Silberspitze."""
    kz = min(0.022, 0.3 * L)
    f = 1.0 if rest else 1.45
    r0 = (0.0215 - 0.0150 * i / n) * f
    r1 = (0.0215 - 0.0150 * (i + 1) / n) * f
    if i == n - 1:
        rod(part, fr, [-0.012, min(kz, 0.15 * L), 0.30 * L], [0.85 * r0, 1.25 * r0, 0.9 * r0], "grip", 6)
        rod(part, fr, [0.28 * L, 0.34 * L, 0.55 * L, L + 0.03], [0.0085, 0.0165, 0.0125, 0.0006], "silver", 4)
        return
    rod(part, fr, [-0.012, kz, 0.5 * L, L - kz, L + 0.012],
        [0.85 * r0, 1.25 * r0, 0.5 * (r0 + r1), 1.25 * r1, 0.85 * r1], "grip", 6)


C_W0, C_W1 = 0.074, 0.036


def seg_c(part, fr, i, n, L, rest):
    """Klingenglied: kleiner dunkler Gelenkzapfen, daran ein silbernes Blatt; jedes zweite um 90 Grad verdreht."""
    k = 0.55 if rest else 1.3
    w = (C_W0 + (C_W1 - C_W0) * i / (n - 1)) * k
    rod(part, fr, [-0.012, 0.010, 0.036, 0.058], [0.011 * k + 0.003, 0.0170 * k + 0.002, 0.0150 * k + 0.002, 0.0110 * k + 0.002], "dark", 6)
    z0, z1, th = 0.036 * min(1.0, L / 0.2), L, 0.0085 * (0.7 if rest else 1.0)
    z0 = min(z0, 0.4 * L)
    h = z1 - z0
    if i == n - 1:
        flat_plate(part, fr, [(0, z0), (w * 0.95, z0 + 0.14 * h), (0, z1 + (0.02 if not rest else 0.0)), (-w * 0.95, z0 + 0.14 * h)], -th, th, "silver")
    else:
        flat_plate(part, fr, [(0, z0), (w, z0 + 0.22 * h), (w, z0 + 0.62 * h), (0, z1), (-w, z0 + 0.62 * h), (-w, z0 + 0.22 * h)], -th, th, "silver")


class Design:
    def __init__(self, key, label, slug, handle, seg, lens, rest_len, turn, roll, painters, drift=0.16):
        self.key, self.label, self.slug, self.handle, self.seg = key, label, slug, handle, seg
        self.lens, self.rest_len, self.turn, self.roll, self.painters, self.drift = lens, rest_len, turn, roll, painters, drift


def _p(grip, iron=paint_iron):
    return {"wood": paint_wood, "grip": grip, "silver": paint_silver, "gold": paint_gold, "dark": paint_dark, "iron": iron}


DESIGNS = [
    Design("a", "A Kettenpeitsche", "a-kette", handle_a, seg_a, [0.375] * 11 + [0.40], 0.062, 400, lambda i: 90.0 * (i % 2), _p(paint_wrap)),
    Design("b", "B Lederpeitsche", "b-leder", handle_b, seg_b, [0.32] * 13 + [0.34], 0.050, 420, lambda i: 0.0, _p(paint_braid)),
    Design("c", "C Klingenpeitsche", "c-klingen", handle_c, seg_c, [0.44] * 9 + [0.54], 0.078, 380, lambda i: 90.0 * (i % 2), _p(paint_wrap)),
]


def rest_dirs(n, turn, drift, start=38.0):
    """Richtungen der Glieder in der Ruhe: eine Schlaufe, die in der Hand nach unten haengt. Gerechnet im Griffsystem
    (Spielachsen, +z = Griffrichtung) aus der Lotrechten der Weltachse; so haengt sie in jeder Ansicht senkrecht."""
    R = held_matrix().to_3x3().inverted()
    down = to_game(R @ Vector((0.0, 0.0, -1.0)))
    f = V(0, 0, 1)
    d = (down - f * down.dot(f)).normalized()
    s = f.cross(d)
    out = []
    for i in range(n):
        th = math.radians(start + turn * i / n)
        out.append((f * math.cos(th) + d * math.sin(th) + s * drift).normalized())
    return out, s


def build_whip(d, rest):
    n = len(d.lens)
    parts = []
    h = Part("handle", (0, 0, 0), UV)
    d.handle(h)
    parts.append(h)
    prev = "handle"
    o = V(0, 0, HANDLE_TOP)
    if rest:
        dirs, side = rest_dirs(n, d.turn, d.drift)
    for i in range(n):
        if rest:
            fr = Fr(o, dirs[i], side, d.roll(i))
            ln = d.rest_len if i < n - 1 else d.rest_len * 1.25
        else:
            fr = Fr(o, V(0, 0, 1), V(0, 1, 0), d.roll(i))
            ln = d.lens[i]
        part = Part(f"seg{i + 1:02d}", fr.o, UV, parent=prev)
        d.seg(part, fr, i, n, ln, rest)
        parts.append(part)
        prev = part.name
        o = o + fr.a * ln
    return parts


def closed_report(part):
    bm = part.bm
    open_e = sum(1 for e in bm.edges if len(e.link_faces) == 1)
    multi = sum(1 for e in bm.edges if len(e.link_faces) > 2)
    loose = sum(1 for e in bm.edges if len(e.link_faces) == 0)
    return len(bm.edges), open_e, multi, loose


def export_design(d, rest):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    parts = build_whip(d, rest)
    tag = "ruhe" if rest else "schlag"
    e = o_ = m = l_ = 0
    for p in parts:
        a, b, c, dd = closed_report(p)
        e, o_, m, l_ = e + a, o_ + b, m + c, l_ + dd
    print(f"PRUEFUNG {d.label} {tag}: {len(parts)} Teile, Kanten {e}, offene Kanten {o_}, Kanten mit mehr als zwei Flaechen {m}, lose Kanten {l_}")
    tex = make_atlas(d.painters)
    colors = len({tuple(c) for c in tex.reshape(-1, 3)})
    name = f"peitsche_{d.key}_{tag}"
    image = save_image(name, tex, os.path.join(tempfile.gettempdir(), name + ".png"))
    coll = bpy.data.collections.new("Peitsche")
    bpy.context.scene.collection.children.link(coll)
    mat = export_material(name, image)
    objs = [p.build(coll, mat, smooth=False) for p in parts]
    tris = sum(len(o.data.polygons) for o in objs)
    pose(objs, {}, root=Matrix.Scale(SCALE, 4))
    bake_scale(objs)
    export_glb(objs, os.path.join(OUT, f"peitsche-{d.slug}-{tag}.glb"))
    print(f"MODELL {d.label} {tag}: {tris} Dreiecke, {len(objs)} Teile, {colors} Farben, Textur {TEX}x{TEX}")
    return tris


# --- Blatt ------------------------------------------------------------------------------------------------------------------

EYE = V(0, 1.6, -6.0)
HAND = (0.11, -0.095, 0.19)           # Spielachsen wie bei der Keule (+x = links im Bild)
TILE = 320
HOME = {}


def load(name, tag, flat=True):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS if name.startswith(("club", "blade")) else OUT, name))
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in objs:
        image = next(n.image for n in o.active_material.node_tree.nodes if n.type == "TEX_IMAGE")
        o.data.materials.clear()
        o.data.materials.append(pass_material(f"{tag}_{o.name}", image))
        o["smooth"] = not (flat or o.name.startswith(("club", "sword")))
        o["tag"] = tag
        HOME[o.name] = o.matrix_world.copy()
    return objs


def place(objs, matrix):
    for o in objs:
        o.matrix_world = matrix @ HOME[o.name]
    bpy.context.view_layer.update()
    for o in objs:
        write_light(o)


def hand_matrix():
    return Matrix.Translation(G(EYE + V(*HAND))) @ Matrix.Rotation(math.radians(-20), 4, "Y")


def held_matrix():
    """Ruhehaltung: der Griff knickt 35 Grad nach oben und 10 Grad nach aussen ab (Art Bible Regel 23); der Aermel bleibt stehen."""
    return hand_matrix() @ Matrix.Rotation(math.radians(10), 4, "Z") @ raise_fwd(35)


def strike_matrix():
    """Schlag: der Griff liegt flacher (8 Grad hoch) und zeigt etwas zur Bildmitte; die Peitsche streckt sich nach vorn."""
    return hand_matrix() @ Matrix.Rotation(math.radians(-16), 4, "Z") @ raise_fwd(8)


def aim_up(cam, loc, target, up):
    fwd = (target - loc).normalized()
    zc = -fwd
    xc = up.cross(zc).normalized()
    yc = zc.cross(xc)
    m = Matrix(((xc.x, yc.x, zc.x), (xc.y, yc.y, zc.y), (xc.z, yc.z, zc.z)))
    cam.location = loc
    cam.rotation_euler = m.to_euler()


def render_tile(visible, cam, loc, target, fov, size, mode, up=None):
    setup_render(*size)
    cam.data.type = "PERSP"
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle = math.radians(fov)
    aim_up(cam, loc, target, up if up is not None else G(V(0, 1, 0)))
    return compose(render_passes("blatt", visible), mode)[::-1]


def render_mask(visible, cam, loc, target, fov, size):
    setup_render(*size)
    cam.data.type = "PERSP"
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle = math.radians(fov)
    aim_up(cam, loc, target, G(V(0, 1, 0)))
    return coverage(render_passes("maske", visible))[::-1]


def up2(a, f):
    return np.repeat(np.repeat(a, f, axis=0), f, axis=1)


def text(img, s, x, y, size=16):
    """Beschriftung mit der Blender-Schrift; y von oben. Groesse wird geprueft."""
    assert 8 <= size <= 40 and isinstance(s, str) and 0 <= x < 20000 and 0 <= y < 20000, (s, x, y, size)
    f = blf.load("C:/Windows/Fonts/arial.ttf")
    blf.size(f, size)
    w, h = blf.dimensions(f, s)
    W, H = int(w) + 8, int(size * 1.6)
    ib = imbuf.new((W, H))
    blf.color(f, 1, 1, 1, 1)
    blf.position(f, 4, int(size * 0.4), 0)
    with blf.bind_imbuf(f, ib):
        blf.draw_buffer(f, s)
    tmp = os.path.join(tempfile.gettempdir(), "peitsche_txt.png")
    ib.filepath = tmp
    imbuf.write(ib)
    im = bpy.data.images.load(tmp)
    im.colorspace_settings.name = "Non-Color"
    a = np.array(im.pixels[:], np.float32).reshape(H, W, 4)[::-1, :, 3]
    bpy.data.images.remove(im)
    hh, ww = img.shape[:2]
    ye, xe = min(hh, y + H), min(ww, x + W)
    m = (a[:ye - y, :xe - x] > 0.5)[..., None]
    img[y:ye, x:xe] = np.where(m, 235.0, img[y:ye, x:xe])


def extent(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return (lo + hi) / 2, (hi - lo).length / 2


def around(objs, cam, direction, fov, size, mode="ansicht", pad=1.12):
    """Kamera um die Mitte der Teile, Blickrichtung 'direction' (Spielachsen, von der Mitte zur Kamera)."""
    c, r = extent(objs)
    dist = r * pad / math.tan(math.radians(fov / 2))
    return render_tile(objs, cam, c + G(direction.normalized()) * dist, c, fov, size, mode)


def thumb(mask, f):
    h2, w2 = mask.shape[0] // f, mask.shape[1] // f
    small = mask[:h2 * f, :w2 * f].reshape(h2, f, w2, f).mean(axis=(1, 3))[..., None]
    return np.where(small > 0.25, 235.0, 0.0) + np.zeros((1, 1, 3))


FIG_COLOR = np.array((74, 66, 92), float)
TICK_COLOR = np.array((120, 110, 140), float)


def draw_figure(img, ppm, z_left, y_top):
    """1,8-m-Figur (Spielmass) als graue Silhouette hinter der Peitsche; nur auf leere Pixel (Hintergrund)."""
    h, w = img.shape[:2]
    empty = np.all(img == FOG_COLOR, axis=-1)
    ys, xs = np.mgrid[0:h, 0:w]
    Z = z_left + xs / ppm
    Y = y_top - ys / ppm
    zc = -0.42
    body = (np.abs(Z - zc) < 0.20) & (Y > 0.86) & (Y < 1.52)
    head = (Z - zc) ** 2 + (Y - 1.68) ** 2 < 0.115 ** 2
    legs = ((np.abs(Z - (zc - 0.09)) < 0.075) | (np.abs(Z - (zc + 0.09)) < 0.075)) & (Y > 0.0) & (Y <= 0.86)
    # Arm: vom Schulterpunkt zur Hand (z = 0, y = 1,05)
    t = np.clip(((Z - zc) * (0.0 - zc) + (Y - 1.42) * (1.05 - 1.42)) / ((0.0 - zc) ** 2 + (1.05 - 1.42) ** 2), 0, 1)
    arm = (Z - (zc + t * (0.0 - zc))) ** 2 + (Y - (1.42 + t * (1.05 - 1.42))) ** 2 < 0.045 ** 2
    fig = (body | head | legs | arm) & empty
    img[fig] = FIG_COLOR
    # Boden und Meter-Marken (ab Griffanfang, Spielmass)
    ground = empty & (np.abs(Y - 0.0) < 0.5 / ppm)
    img[ground] = TICK_COLOR
    for m in range(1, 5):
        tick = empty & (np.abs(Z - m) < 0.5 / ppm) & (Y > -0.02) & (Y < 0.07)
        img[tick] = TICK_COLOR
    return img


def strip_view(objs, cam):
    """Gestreckte Peitsche von der Seite (Spielmass, ohne die Skalierung 1,8) neben einer 1,8-m-Figur. Rechts = vorn."""
    zmin, width, size = -0.9, 5.9, (1600, 560)
    ppm = size[0] / width
    ytop = 0.93 + size[1] / ppm / 2
    place(objs, Matrix.Translation(G(V(0, 1.05, 0))) @ Matrix.Scale(1 / SCALE, 4))
    setup_render(*size)
    cam.data.type = "ORTHO"
    cam.data.sensor_fit = "AUTO"
    cam.data.ortho_scale = width
    aim_up(cam, G(V(8.0, 0.93, zmin + width / 2)), G(V(0, 0.93, zmin + width / 2)), G(V(0, 1, 0)))
    bpy.context.view_layer.update()
    flip = world_to_camera_view(bpy.context.scene, cam, G(V(0, 1, 3))).x < world_to_camera_view(bpy.context.scene, cam, G(V(0, 1, 0))).x
    img = compose(render_passes("strip", objs), "ansicht")[::-1]
    if flip:
        img = img[:, ::-1]
    img = np.ascontiguousarray(img)
    draw_figure(img, ppm, zmin, ytop)
    for m in range(1, 5):
        text(img, f"{m} m", int((m - zmin) * ppm) + 4, size[1] - 34, 16)
    text(img, "1,8 m", int((-0.42 - zmin) * ppm) - 22, int((ytop - 1.95) * ppm), 16)
    cam.data.type = "PERSP"
    cam.data.sensor_fit = "VERTICAL"
    return img


CROP_REST = (80, 480, 110, 510)        # Zeilen von oben, Spalten
CROP_STRIKE = (26, 480, 0, 808)


def blatt():
    stone_dir = os.path.join(tempfile.gettempdir(), "peitsche_stein")
    os.makedirs(stone_dir, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    whips = {(d.key, t): load(f"peitsche-{d.slug}-{t}.glb", f"{d.key}{t}") for d in DESIGNS for t in ("ruhe", "schlag")}
    club = load("club.glb", "club", flat=False)
    club_hand = load("club_hand.glb", "clubh", flat=False)
    blade = load("blade.glb", "blade", flat=False)
    blade_hand = load("blade_hand.glb", "bladeh", flat=False)
    cam = camera()
    room = build_room(stone_textures(stone_dir))
    held = held_matrix().to_3x3().to_4x4()
    res = {"seite": {}, "strip": {}, "views": {}, "ego": {}, "mask": {}, "full": {}}
    views = {"vorn": V(0, 0.15, 1), "seite": V(1, 0.15, 0), "hinten": V(0, 0.15, -1), "oben": V(0.45, 1, 0.55)}
    for d in DESIGNS:
        k = d.key
        ruhe, schlag = whips[(k, "ruhe")], whips[(k, "schlag")]
        place(ruhe, held)
        res["seite"][k] = up2(around(ruhe, cam, views["seite"], 30, (200, 200)), 2)
        res["views"][k] = [around(ruhe, cam, views[v], 30, (TILE, TILE)) for v in ("vorn", "seite", "hinten", "oben")]
        place(schlag, Matrix.Identity(4))
        c = G(V(0, 0, 0.75))
        res["views"][k].append(render_tile(schlag, cam, c + G(V(1.0, 0.6, -0.4).normalized()) * 2.0, c, 33, (TILE, TILE), "ansicht"))
        res["strip"][k] = strip_view(schlag, cam)
    shots = [(f"{d.key}-{t}", whips[(d.key, t)], None, t) for d in DESIGNS for t in ("ruhe", "schlag")]
    shots += [("club", club, club_hand, "ruhe"), ("blade", blade, blade_hand, "ruhe")]
    for name, w, h, t in shots:
        keep = list(room) + w + list(h or club_hand)
        if h:
            place(h, hand_matrix())
        else:
            place(club_hand, hand_matrix())
        place(w, (strike_matrix() if t == "schlag" else held_matrix()) if name != "blade" else hand_matrix())
        shot = render_tile(keep, cam, G(EYE), G(EYE + V(0, 0, 1)), 70, (854, 480), "spiel")
        res["full"][name] = shot
        crop = CROP_STRIKE if t == "schlag" else CROP_REST
        res["ego"][name] = shot[crop[0]:crop[1], crop[2]:crop[3]]
        res["mask"][name] = render_mask(list(w), cam, G(EYE), G(EYE + V(0, 0, 1)), 70, (854, 480))[crop[0]:crop[1], crop[2]:crop[3]]
    return res


def main():
    tris = {}
    for d in DESIGNS:
        for rest in (True, False):
            tris[(d.key, rest)] = export_design(d, rest)
    res = blatt()
    gap, label_h, W = 8, 26, 1632
    bg = FOG_COLOR
    row_h = 454
    h_total = label_h + 3 * (row_h + label_h + gap) + 3 * (560 + label_h + gap) + (151 + 44 + gap)
    sheet = np.zeros((h_total, W, 3)) + bg
    text(sheet, "Silver & Ash - Whip (Issue 202), Runde 1: drei Entwuerfe. Je Entwurf: Ruhe (Seite), Ich-Sicht Ruhe, Ich-Sicht Schlag, "
         "Streckung (Spielmass) neben einer 1,8-m-Figur", 6, 2, 16)

    def put(img, x0, y0, title):
        h, w = img.shape[:2]
        sheet[y0 + label_h:y0 + label_h + h, x0:x0 + w] = img
        text(sheet, title, x0 + 4, y0, 16)

    y = label_h
    for d in DESIGNS:
        k = d.key
        pad = np.zeros((row_h, 400, 3)) + bg
        pad[:400, :400] = res["seite"][k]
        put(pad, 0, y, f"{d.label} ({tris[(k, True)]} Dreiecke): Ruhe, von der Seite")
        put(res["ego"][f"{k}-ruhe"], 408, y, f"{d.label}: Ich-Sicht Ruhe (Ausschnitt)")
        put(res["ego"][f"{k}-schlag"], 816, y, f"{d.label}: Ich-Sicht Schlag, Dateimass (Ausschnitt)")
        y += row_h + label_h + gap
    for d in DESIGNS:
        put(res["strip"][d.key], 0, y, f"{d.label} ({tris[(d.key, False)]} Dreiecke): im Schlag gestreckt, Spielmass, Seite; Marken = Meter ab Griffanfang")
        y += 560 + label_h + gap
    # Silhouetten
    names = [("a", "A"), ("b", "B"), ("c", "C"), ("club", "Keule"), ("blade", "Klinge")]
    sil = np.zeros((151 + 18, W, 3)) + bg
    x = 2
    for key, lab in names:
        m = thumb(res["mask"][key if key in ("club", "blade") else key + "-ruhe"], 3)
        sil[18:18 + m.shape[0], x:x + m.shape[1]] = m
        text(sil, lab, x, 0, 14)
        x += m.shape[1] + 6
    x += 20
    for key in ("a", "b", "c"):
        m = thumb(res["mask"][key + "-schlag"], 3)
        sil[18:18 + m.shape[0], x:x + m.shape[1]] = m
        text(sil, key.upper() + " Schlag", x, 0, 14)
        x += m.shape[1] + 6
    put(sil, 0, y, "Silhouetten in der Ich-Sicht, Drittelgroesse: Ruhe (A B C Keule Klinge), dann Schlag (A B C)")
    save_png(sheet[::-1], os.path.join(OUT, "blatt.png"))

    # Ansichten
    cols, rows = 5, 3
    sh = np.zeros((label_h + rows * (TILE + label_h + gap), cols * (TILE + gap), 3)) + bg
    text(sh, "Silver & Ash - Whip (Issue 202), Runde 1: Ruhe vorn / Seite / hinten / schraeg oben, dazu die gestreckte Peitsche (Nahbild)", 6, 2, 16)
    heads = ("Ruhe vorn", "Ruhe Seite", "Ruhe hinten", "Ruhe schraeg oben", "Schlag, Nahbild")
    for r, d in enumerate(DESIGNS):
        for c in range(cols):
            x0, y0 = c * (TILE + gap), label_h + r * (TILE + label_h + gap)
            sh[y0 + label_h:y0 + label_h + TILE, x0:x0 + TILE] = res["views"][d.key][c]
            text(sh, f"{d.label[0]}: {heads[c]}", x0 + 4, y0, 16)
    save_png(sh[::-1], os.path.join(OUT, "ansichten.png"))
    for name, shot in res["full"].items():
        if name in ("club", "blade"):
            continue
        k, t = name.split("-")
        slug = next(d.slug for d in DESIGNS if d.key == k)
        save_png(shot[::-1], os.path.join(OUT, f"ich-{slug}-{t}.png"))


if __name__ == "__main__":
    main()
