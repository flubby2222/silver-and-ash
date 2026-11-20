"""Probestueck Wandpfeiler (Issue #536, Baustein aus der Formenlehre Gotik #281, Frage #247).

Alles entsteht aus diesem einen Skript: die Modelle (.glb, je Fassung und Wandvariante a, b, c), die Atlas-Bilder, die
Probebilder und die Probenblaetter. Gebaut auf tools/models/psx.py, die Stein- und Texturregeln folgen tools/models/door.py
(Rahmen): Welt-UV (eine 64-px-Kachel je Meter, an den Metergrenzen geteilt), Stein aus der Hauptpalette, Kachel der echten
Wand-Textur. Nichts wird ins Spiel uebernommen.

Fassungen (Spiel-Achsen: x seitlich, y oben, z vorn; Meter; Ursprung: Mitte des Pfeilers auf dem Boden, Wandflaeche z = 0):
  raum         Raum 6 m: Sockel 0 bis 0,5, Schaft 0,5 bis 5,5 (Tiefe 0,25), Kapitell 5,5 bis 6,0; Sockel/Kapitell Tiefe 0,3125
  gang         Gang 3,5 m: Schaft Tiefe 0,0625, Sockel/Kapitell 0,125; Kapitell 3,0 bis 3,5 (reicht bis zum Abschluss)
  gang_traeger Gang, traegt einen Gurtbogen: Schaft 0,5 bis 1,5, Kapitell 1,5 bis 2,0 (Oberkante = Kaempfer)
Schaft 0,5 breit, Sockel und Kapitell 0,625 breit (1/16 je Seite). Ein einziges Netz je Fassung: Ringe (Rechtecke) entlang
des Profils, rundum geschlossen, auch die Rueckseite zur Wand (liegt 1/8 m in der Wand: der "Zahn" als durchgehende Leiste).

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/wandpfeiler-536/wandpfeiler.py
Zwei Laeufe liefern gleiche Dateien (kein Zufall).
"""

import json
import math
import os
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
import blf  # noqa: E402
import bpy  # noqa: E402
import imbuf  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix  # noqa: E402
from psx import (V, G, FOG_COLOR, Part, rects_from_pixels, save_image, stone_textures, pass_material,  # noqa: E402
                 write_light, setup_render, camera, aim, render_passes, compose, save_png, export_material, export_glb,
                 MATS)

OUT = HERE
TMP = os.path.join(tempfile.gettempdir(), "wandpfeiler536_tmp")
os.makedirs(TMP, exist_ok=True)
TEXTURES = os.path.join(ROOT, "assets", "textures")
MODELS = os.path.join(ROOT, "assets", "models")
WALL_FILES = {"a": "catacomb_wall.png", "b": "catacomb_wall_b.png", "c": "catacomb_wall_c.png"}
LIFT = int(os.environ.get("W536_LIFT", "5"))   # Stein-Stufen heller als die Wand (Umgebungsvariable nur fuer Versuche)
FRAME_SIZE = (128, 64)
FRAME_RECTS = {"stone": (0, 0, 64, 64), "pillar": (64, 0, 128, 64)}
FRAME_UV = rects_from_pixels(FRAME_RECTS, FRAME_SIZE)
BG = np.array((30, 26, 34), float)
S = 0.0625                                         # 1/16

# --- Fassungen -------------------------------------------------------------------------------------------------------
# name: (Schafttiefe, Kapitell-Unterkante)
FASSUNGEN = {
    "raum": (0.25, 5.5),
    "gang": (0.0625, 3.0),
    "gang_traeger": (0.0625, 1.5),
}
BACK = -0.125                                      # Rueckseite 1/8 m in der Wand


# --- Palette und Texturen --------------------------------------------------------------------------------------------

def stein_row():
    with open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8") as f:
        data = json.load(f)
    rows = {r["name"]: np.array(r["farben"], int) for r in data["grund"] + data["akzent"]}
    return rows["Stein"]


def load_tile(name):
    """Wand-Kachel 64 x 64, Zeile 0 oben, ganze Zahlen 0..255."""
    im = bpy.data.images.load(os.path.join(TEXTURES, name))
    w, h = im.size
    a = np.array(im.pixels[:], dtype=float).reshape(h, w, 4)[..., :3] * 255.0
    bpy.data.images.remove(im)
    return np.round(a[::-1]).astype(int)


def pillar_tile(tile, stein):
    """Dieselbe Kachel um LIFT Stein-Stufen heller. Alle Stufen der Wand ruecken hinauf; weil die Wand schon bis zur obersten
    Stufe reicht, werden die Stufen oben zusammengedraengt (lineare Abbildung), so bleiben Fugen und Quader unterscheidbar."""
    flat = tile.reshape(-1, 3)
    colours = sorted({tuple(int(c) for c in p) for p in flat})
    idx = {c: int(((stein - np.array(c)) ** 2).sum(1).argmin()) for c in colours}
    lo, hi, top = min(idx.values()), max(idx.values()), len(stein) - 1
    new_lo = min(max(lo + LIFT, 0), top)
    span = max(hi - lo, 1)
    if LIFT >= 0:
        mapped = {c: int(round(new_lo + (i - lo) * (top - new_lo) / span)) for c, i in idx.items()}
    else:
        mapped = {c: max(i + LIFT, 0) for c, i in idx.items()}
    return np.array([stein[mapped[tuple(int(c) for c in p)]] for p in flat]).reshape(tile.shape)


def atlas(variant):
    stein = stein_row()
    tile = load_tile(WALL_FILES[variant])
    lifted = pillar_tile(tile, stein)
    w, h = FRAME_SIZE
    out = np.zeros((h, w, 3))
    for name, t in (("stone", tile), ("pillar", lifted)):
        x0, y0, x1, y1 = FRAME_RECTS[name]
        out[y0:y1, x0:x1] = t[::-1]
    return out


def colours_of(tex):
    return len({tuple(p) for p in np.round(tex).reshape(-1, 3).astype(int)})


# --- Flaechen mit Welt-UV (wie door.py: eine Kachel je Meter, an den Metergrenzen geteilt) -----------------------------

def _clip(poly, axis, bound, keep_ge):
    out = []
    for i, cur in enumerate(poly):
        prev = poly[i - 1]
        c_in = cur[axis] >= bound - 1e-9 if keep_ge else cur[axis] <= bound + 1e-9
        p_in = prev[axis] >= bound - 1e-9 if keep_ge else prev[axis] <= bound + 1e-9
        if c_in != p_in:
            t = (bound - prev[axis]) / (cur[axis] - prev[axis])
            out.append((prev[0] + t * (cur[0] - prev[0]), prev[1] + t * (cur[1] - prev[1]), prev[2] + (cur[2] - prev[2]) * t))
        if c_in:
            out.append(cur)
    return out


def emit(part, pts, hint, field):
    P = [V(*p) for p in pts]
    n = None
    for i in range(1, len(P) - 1):
        c = (P[i] - P[0]).cross(P[i + 1] - P[0])
        if c.length > 1e-9:
            n = c
            break
    if n is None:
        return
    if n.dot(V(*hint)) < 0:
        P.reverse()
        n = -n
    n = n.normalized()
    ax = max(range(3), key=lambda i: abs(round(n[i], 6)))
    if ax == 2:
        pu, pv = (lambda p: p.x if n.z > 0 else -p.x), (lambda p: p.y)
    elif ax == 0:
        pu, pv = (lambda p: -p.z if n.x > 0 else p.z), (lambda p: p.y)
    else:
        pu, pv = (lambda p: p.x if n.y > 0 else -p.x), (lambda p: p.z)
    poly = [(pu(p), pv(p), p) for p in P]
    i0, i1 = math.floor(min(q[0] for q in poly) + 1e-9), math.ceil(max(q[0] for q in poly) - 1e-9)
    j0, j1 = math.floor(min(q[1] for q in poly) + 1e-9), math.ceil(max(q[1] for q in poly) - 1e-9)
    u0, v0, u1, v1 = part.rects[field]
    for i in range(i0, max(i1, i0 + 1)):
        for j in range(j0, max(j1, j0 + 1)):
            cell = _clip(_clip(_clip(_clip(poly, 0, i, True), 0, i + 1, False), 1, j, True), 1, j + 1, False)
            cell = [q for k, q in enumerate(cell) if k == 0 or abs(q[0] - cell[k - 1][0]) + abs(q[1] - cell[k - 1][1]) > 1e-7]
            if len(cell) >= 2 and abs(cell[0][0] - cell[-1][0]) + abs(cell[0][1] - cell[-1][1]) < 1e-7:
                cell = cell[:-1]
            if len(cell) < 3:
                continue
            area = sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(cell, cell[1:] + cell[:1])) / 2
            if abs(area) < 1e-6:
                continue
            verts = [part.bm.verts.new(G(q[2] - part.pivot)) for q in cell]
            uvs = [(u0 + (u1 - u0) * min(max(q[0] - i, 0), 1), v0 + (v1 - v0) * min(max(q[1] - j, 0), 1)) for q in cell]
            part._face(verts, uvs)


def G_inv(p):
    """Blender-Achsen -> Spiel-Achsen."""
    return V(p[0], p[2], -p[1])


def raw(part, pts, hint, field):
    """Verdeckte Flaeche (Rueckseite, Boden) in einem Stueck, ohne Teilung an den Metergrenzen. Liegen auf einer Kante
    Ecken der geteilten Nachbarflaechen, werden sie eingefuegt (sonst entstuende an der Teilung eine offene Kante)."""
    P = [V(*p) for p in pts]
    flip = (P[1] - P[0]).cross(P[2] - P[0]).dot(V(*hint)) < 0
    others = [G_inv(v.co) for v in part.bm.verts]
    withs = []
    for i, a in enumerate(P):
        b = P[(i + 1) % len(P)]
        withs.append(a)
        ab = b - a
        L2 = ab.dot(ab)
        if L2 < 1e-12:
            continue
        hits = []
        for q in others:
            t = (q - a).dot(ab) / L2
            if 1e-6 < t < 1 - 1e-6 and (a + ab * t - q).length < 1e-6:
                hits.append((t, q))
        for t, q in sorted(hits, key=lambda h: h[0]):
            if not withs or (withs[-1] - q).length > 1e-6:
                withs.append(q)
    P = withs
    if flip:
        P.reverse()
    u0, v0, u1, v1 = part.rects[field]
    verts = [part.bm.verts.new(G(p - part.pivot)) for p in P]
    part._face(verts, [(u0 + (u1 - u0) * 0.5, v0 + (v1 - v0) * 0.5)] * len(verts))


# --- Pfeiler ---------------------------------------------------------------------------------------------------------

def profile(cb):
    """(y, o) von unten nach oben; o = Vorsprung vor dem Schaft je Seite und nach vorn (0 oder 1/16)."""
    pts = [(0.0, S), (0.4375, S), (0.5, 0.0),                       # Sockel 0,5 hoch, oben Fase 1/16
           (cb, 0.0),                                               # Schaft
           (cb + S, S), (cb + 2 * S, S),                            # Platte unten: Fase 1/16, senkrecht 1/16
           (cb + 2 * S, 0.0), (cb + 3 * S, 0.0),                    # Kehle 1/16
           (cb + 4 * S, S), (cb + 5 * S, 0.0),                      # Wulst (Raute) 1/8
           (cb + 6 * S, S), (cb + 8 * S, S)]                        # Platte oben: Fase 1/16, senkrecht 1/8
    out = []
    for p in pts:
        if not out or out[-1] != p:
            out.append(p)
    return out


def build_pillar(name, variant, x0=0.0):
    depth, cb = FASSUNGEN[name]
    top = cb + 0.5
    prof = profile(cb)
    part = Part("pfeiler_" + name, (0, 0, 0), FRAME_UV)
    rings = [(y, 0.25 + o, depth + o) for y, o in prof]            # y, halbe Breite, Tiefe vorn
    xs = lambda hx, s: x0 + s * hx                                  # noqa: E731
    for (y0, h0, f0), (y1, h1, f1) in zip(rings, rings[1:]):
        dx, dy, dz = h1 - h0, y1 - y0, f1 - f0
        for s in (1, -1):                                           # Seiten
            emit(part, [(xs(h0, s), y0, BACK), (xs(h0, s), y0, f0), (xs(h1, s), y1, f1), (xs(h1, s), y1, BACK)],
                 (s * dy, -dx, 0), "pillar")
        emit(part, [(xs(h0, -1), y0, f0), (xs(h0, 1), y0, f0), (xs(h1, 1), y1, f1), (xs(h1, -1), y1, f1)],
             (0, -dz, dy), "pillar")
    y, h, f = rings[-1]
    emit(part, [(xs(h, -1), y, BACK), (xs(h, 1), y, BACK), (xs(h, 1), y, f), (xs(h, -1), y, f)], (0, 1, 0), "pillar")
    right = [(xs(h, 1), y, BACK) for y, h, f in rings]
    left = [(xs(h, -1), y, BACK) for y, h, f in reversed(rings)]
    raw(part, right + left, (0, 0, -1), "pillar")                   # Rueckseite: ein Vieleck in der Wand
    y, h, f = rings[0]
    raw(part, [(xs(h, -1), y, BACK), (xs(h, 1), y, BACK), (xs(h, 1), y, f), (xs(h, -1), y, f)], (0, -1, 0), "pillar")
    return part, top


def edge_report(part):
    """Kanten je Netz (Ecken gleicher Lage zusammengelegt): offene Kanten und Kanten mit mehr als zwei Flaechen."""
    keys, edges = {}, {}
    for f in part.bm.faces:
        ids = [keys.setdefault(tuple(round(c * 1e5) for c in v.co), len(keys)) for v in f.verts]
        for a, b in zip(ids, ids[1:] + ids[:1]):
            k = (min(a, b), max(a, b))
            edges[k] = edges.get(k, 0) + 1
    return sum(1 for n in edges.values() if n == 1), sum(1 for n in edges.values() if n > 2)


def triangles(part):
    return sum(len(f.verts) - 2 for f in part.bm.faces)


def export_models():
    """Je Fassung und Variante eine .glb mit dem Atlas (128 x 64) nach OUT/modelle/."""
    rows = []
    for name in FASSUNGEN:
        for v in WALL_FILES:
            bpy.ops.wm.read_factory_settings(use_empty=True)
            coll = bpy.data.collections.new("Pfeiler")
            bpy.context.scene.collection.children.link(coll)
            tex = atlas(v)
            assert colours_of(tex) <= 40, colours_of(tex)
            d = os.path.join(OUT, "modelle")
            os.makedirs(d, exist_ok=True)
            image = save_image("wandpfeiler_%s_%s" % (name, v), tex, os.path.join(d, "wandpfeiler_%s_%s.png" % (name, v)))
            part, top = build_pillar(name, v)
            o_edges, multi = edge_report(part)
            tris = triangles(part)
            obj = part.build(coll, export_material("m", image), smooth=False)
            export_glb([obj], os.path.join(d, "wandpfeiler_%s_%s.glb" % (name, v)))
            rows.append((name, v, tris, o_edges, multi, colours_of(tex), top))
    for r in rows:
        print("MODELL %s %s: %d Dreiecke, offene Kanten %d, mehrfach %d, Farben %d, Hoehe %.4f" % r)
    return rows


# --- Szenen ----------------------------------------------------------------------------------------------------------

ATLAS_IMG = {}


def new_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    ATLAS_IMG.clear()
    coll = bpy.data.collections.new("Szene")
    bpy.context.scene.collection.children.link(coll)
    return coll


def atlas_img(v):
    if v not in ATLAS_IMG:
        ATLAS_IMG[v] = save_image("atlas_" + v, atlas(v), os.path.join(TMP, "atlas_%s.png" % v))
    return ATLAS_IMG[v]


def finish(objs):
    bpy.context.view_layer.update()
    for o in objs:
        o["smooth"] = False
        write_light(o)


def add_pillar(coll, name, v, x):
    part, _ = build_pillar(name, v, x)
    o = part.build(coll, pass_material("p_%s_%s_%g" % (name, v, x), atlas_img(v)), smooth=False)
    return [o]


def add_wall(coll, v, x0, x1, h):
    part = Part("wand", (0, 0, 0), FRAME_UV)
    emit(part, [(x0, 0, 0), (x1, 0, 0), (x1, h, 0), (x0, h, 0)], (0, 0, 1), "stone")
    return [part.build(coll, pass_material("w_%s_%g" % (v, x0), atlas_img(v)), smooth=False)]


def add_floor(coll, x0=-8, x1=8, z0=-1, z1=14):
    img = stone_textures(TMP)["floor"]
    part = Part("boden", (0, 0, 0), {"all": (0.0, 0.0, 1.0, 1.0)})
    verts = [part.bm.verts.new(G(V(x, 0, z))) for x, z in ((x0, z0), (x1, z0), (x1, z1), (x0, z1))]
    part._face(verts, [(x0, z0), (x1, z0), (x1, z1), (x0, z1)])
    return [part.build(coll, pass_material("boden", img), smooth=False)]


def add_door(coll, v):
    """Der Tuerrahmen aus assets/models (nur gelesen), so verschoben, dass seine Wandflaeche (z = 1) auf z = 0 liegt."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS, "door_frame_normal_%s.glb" % v))
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in objs:
        img = None
        for m in o.data.materials:
            for n in m.node_tree.nodes:
                if n.type == "TEX_IMAGE":
                    img = n.image
        o.data.materials.clear()
        o.data.materials.append(pass_material("d_" + o.name, img))
        o.parent = None
        o.matrix_world = Matrix.Translation(G(V(0, 0, -1)))
        coll.objects.link(o)
        for c in list(o.users_collection):
            if c is not coll:
                c.objects.unlink(o)
    return objs


def build_scene(items):
    coll = new_scene()
    objs = []
    for it in items:
        k = it[0]
        if k == "pillar":
            objs += add_pillar(coll, it[1], it[2], it[3])
        elif k == "wall":
            objs += add_wall(coll, it[1], it[2], it[3], it[4])
        elif k == "floor":
            objs += add_floor(coll)
        elif k == "door":
            objs += add_door(coll, it[1])
    finish(objs)
    return objs


def shot(items, loc, tgt, fov, size, mode="hell", ortho=None):
    build_scene(items)
    cam = camera()
    setup_render(*size)
    if ortho:
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = ortho
    else:
        cam.data.angle = math.radians(fov)
    aim(cam, G(V(*loc)), G(V(*tgt)))
    p = render_passes("w536")
    if mode == "hell":
        base, light = p["base"][..., :3] * 255, p["light"][..., :1]
        cov = p["base"][..., 3:4] > 0.5
        img = np.where(cov, np.clip(base * light * 1.3, 0, 255), BG)
    else:
        img = compose(p, "spiel")
    return np.round(img[::-1])                                      # Zeile 0 oben


# --- Beschriftung und Bilder -----------------------------------------------------------------------------------------

def text(img, s, x, y, size=18, color=(235, 230, 240)):
    f = blf.load("C:/Windows/Fonts/arial.ttf")
    blf.size(f, size)
    w, h = blf.dimensions(f, s)
    wd, hd = int(w) + 8, int(size * 1.6)
    ib = imbuf.new((wd, hd))
    blf.color(f, 1, 1, 1, 1)
    blf.position(f, 4, int(size * 0.4), 0)
    with blf.bind_imbuf(f, ib):
        blf.draw_buffer(f, s)
    assert ib.size == (wd, hd), (ib.size, wd, hd)
    tmp = os.path.join(TMP, "txt.png")
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


def png(img, path):
    d = os.path.dirname(path)
    os.makedirs(d, exist_ok=True)
    save_png(np.asarray(img, float)[::-1], path)


def hcat(images, gap=6):
    h = max(a.shape[0] for a in images)
    pads = [a if a.shape[0] == h else np.concatenate([a, np.tile(BG, (h - a.shape[0], a.shape[1], 1))], 0) for a in images]
    out = [pads[0]]
    for a in pads[1:]:
        out += [np.tile(BG * 0.5, (h, gap, 1)), a]
    return np.concatenate(out, axis=1)


def titled(img, s):
    img = img.copy()
    text(img, s, 6, 4, 18)
    return img


# --- Ansichten ---------------------------------------------------------------------------------------------------------

HEIGHT = {"raum": 6.0, "gang": 3.5, "gang_traeger": 2.0}
WALLH = {"raum": 6.0, "gang": 3.5, "gang_traeger": 3.5}


def view_front(name, v="a", size=(480, 720)):
    h = WALLH[name]
    items = [("wall", v, -3, 3, h), ("pillar", name, v, 0.0)]
    scale = h + 0.4
    return shot(items, (0, h / 2, 6), (0, h / 2, 0), 0, size, "hell", ortho=scale)


def view_oblique(name, v="a", size=(640, 720)):
    h = WALLH[name]
    items = [("floor",), ("wall", v, -4, 4, h), ("pillar", name, v, 0.0)]
    d = 2.2 + h * 0.75
    return shot(items, (d * 0.62, h * 0.45, d * 0.8), (0, h * 0.45, 0), 45, size, "hell")


def view_eye(name, v="a", size=(854, 480), mode="spiel", tilt=None):
    h = WALLH[name]
    items = [("floor",), ("wall", v, -5, 5, h), ("pillar", name, v, 0.0)]
    tgt = (0, 1.65, 0) if tilt is None else (0, tilt, 0)
    return shot(items, (0.5, 1.65, 4.0), tgt, 70, size, mode)


def view_single(name, which, v="a", size=(360, 640)):
    """Pfeiler allein auf dem Boden, ohne Wand: vorn, Seite, hinten, schraeg von oben."""
    top = HEIGHT[name]
    items = [("floor",), ("pillar", name, v, 0.0)]
    cy = top / 2
    scale = top + 0.5
    if which == "vorn":
        return shot(items, (0, cy, 8), (0, cy, 0), 0, size, "hell", ortho=scale)
    if which == "seite":
        return shot(items, (8, cy, 0), (0, cy, 0), 0, size, "hell", ortho=scale)
    if which == "hinten":
        return shot(items, (0, cy, -8), (0, cy, 0), 0, size, "hell", ortho=scale)
    d = max(top * 1.15, 3.0)
    return shot(items, (d * 0.7, top + d * 0.45, d * 0.8), (0, top * 0.45, 0), 38, size, "hell")


def view_detail(name, v="a", size=(420, 420)):
    """Sockel und Kapitell aus der Naehe: schraeg von vorn-oben-rechts und im Profil (von der Seite)."""
    depth, cb = FASSUNGEN[name]
    items = [("floor",), ("wall", v, -2, 2, HEIGHT[name] + 1), ("pillar", name, v, 0.0)]
    out = []
    for cy, lbl in ((0.4, "Sockel"), (cb + 0.25, "Kapitell")):
        a = shot(items, (1.5, cy + 0.35, 1.6), (0, cy, 0), 40, size, "hell")
        b = shot(items, (6, cy, 0), (0, cy, 0), 0, size, "hell", ortho=1.1)
        out += [titled(a, lbl + " schraeg"), titled(b, lbl + " Profil (Seite)")]
    return hcat(out)


def sheet_all(v="a"):
    items = [("wall", v, -6, 6, 6.4), ("pillar", "gang_traeger", v, -3.0), ("pillar", "gang", v, 0.0),
             ("pillar", "raum", v, 3.0)]
    img = shot(items, (0, 3.2, 12), (0, 3.2, 0), 0, (1200, 660), "hell", ortho=6.6)
    for x, s in ((-3, "Gang tragend (Kaempfer 2,0)"), (0, "Gang (3,5)"), (3, "Raum (6,0)")):
        text(img, s, int(600 + x * 100 - 90), 6, 18)
    return img


def sheet_oblique(v="a"):
    items = [("floor",), ("wall", v, -6, 6, 6.4), ("pillar", "gang_traeger", v, -3.0), ("pillar", "gang", v, 0.0),
             ("pillar", "raum", v, 3.0)]
    return shot(items, (5.5, 2.6, 10.5), (0, 2.8, 0), 52, (1200, 660), "hell")


def view_door(v="a", size=(1200, 600)):
    items = [("floor",), ("wall", v, -4, -1, 3.5), ("wall", v, 1, 4, 3.5), ("door", v),
             ("pillar", "gang", v, -2.0), ("pillar", "gang_traeger", v, 2.0)]
    return shot(items, (0, 1.75, 11), (0, 1.75, 0), 0, size, "hell", ortho=4.2)


def view_door_oblique(v="a", size=(1200, 600)):
    items = [("floor",), ("wall", v, -4, -1, 3.5), ("wall", v, 1, 4, 3.5), ("door", v),
             ("pillar", "gang", v, -2.0), ("pillar", "gang_traeger", v, 2.0)]
    return shot(items, (3.5, 1.7, 5.5), (0, 1.7, 0), 55, size, "hell")


def view_variants(name="gang", zone="Sockel"):
    depth, cb = FASSUNGEN[name]
    cy = 0.75 if zone == "Sockel" else cb + 0.1
    out = []
    for v in WALL_FILES:
        items = [("wall", v, -3, 3, 6.2), ("pillar", name, v, 0.0)]
        out.append(titled(shot(items, (0, cy, 8), (0, cy, 0), 0, (420, 420), "hell", ortho=1.6),
                          "Wand %s, %s" % (v, zone)))
    return hcat(out)


def view_variants_full(name="gang"):
    out = []
    for v in WALL_FILES:
        items = [("wall", v, -3, 3, 3.5), ("pillar", name, v, 0.0)]
        out.append(titled(shot(items, (0, 1.75, 8), (0, 1.75, 0), 0, (360, 480), "hell", ortho=3.8), "Wand " + v))
    return hcat(out)


def main():
    rows = export_models()
    for name in FASSUNGEN:
        d = os.path.join(OUT, name)
        png(view_front(name), os.path.join(d, "vorn.png"))
        png(view_oblique(name), os.path.join(d, "schraeg.png"))
        png(view_eye(name, mode="spiel"), os.path.join(d, "spielaufloesung.png"))
        png(view_eye(name, mode="hell"), os.path.join(d, "auge-hell.png"))
        png(hcat([titled(view_single(name, w), w) for w in ("vorn", "seite", "hinten", "oben")]), os.path.join(d, "einzel.png"))
        png(view_detail(name), os.path.join(d, "detail.png"))
    png(view_eye("raum", mode="hell", tilt=3.2), os.path.join(OUT, "raum", "auge-hoch.png"))
    png(sheet_all(), os.path.join(OUT, "blatt-vorn.png"))
    png(sheet_oblique(), os.path.join(OUT, "blatt-schraeg.png"))
    png(view_door(), os.path.join(OUT, "vergleich-tuer.png"))
    png(view_door_oblique(), os.path.join(OUT, "vergleich-tuer-schraeg.png"))
    png(view_variants("gang", "Sockel"), os.path.join(OUT, "wandvarianten-sockel.png"))
    png(view_variants("gang", "Kapitell"), os.path.join(OUT, "wandvarianten-kapitell.png"))
    png(view_variants_full("gang"), os.path.join(OUT, "wandvarianten-ganz.png"))
    png(view_variants("raum", "Sockel"), os.path.join(OUT, "wandvarianten-raum-sockel.png"))


main()
