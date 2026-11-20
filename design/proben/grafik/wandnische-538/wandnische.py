"""Probestueck Wandnische (Issue #538, Baustein aus der Formenlehre Gotik #281, Frage #247).

Eine spitzbogige Nische in der Wand, leer (Props kommen spaeter mit #214), reine Zier, sperrt nichts. Alles entsteht aus diesem
einen Skript: die Modelle (.glb je Wandvariante a, b, c und Fassung gang / raum), der Atlas, alle Probebilder. Die Vorlagen
(Wandpfeiler #536, Deckenbogen #537) werden nur gelesen und ausgefuehrt, nicht veraendert: gleiche Welt-UV (64 px je Meter),
gleiche um LIFT Stufen aufgehellte Wandkachel fuer den Gewaendestein, nur Farben der Hauptpalette, Reihe Stein.

Spiel-Achsen (x seitlich, y oben, z vorn; Meter). Ursprung: Mitte der Nische auf dem Boden, Wandflaeche z = 0.

TIEFE (ehrlich): Die Wand im Spiel ist eine duenne Flaeche am Zellraster, sie laesst sich nicht aushoehlen. Die Nische ist ein
vorgesetzter, gestufter Gewaendekranz mit Sohlbank. Die Oeffnung zeigt eine dunkle Rueckwandplatte (1/64 vor der Wand, nur damit
Wand und Platte nicht um dieselbe Tiefe streiten). Echtes Relief sind nur: Laibung (Gang 0,11 m, Raum 0,23 m zwischen Platte und
Gewaendevorderkante) und die Sohlbank. Der Rest der Tiefe ist gemalt: die Platte ist die Wandkachel um 5 Stufen abgedunkelt,
am Rand in harten Stufen (ohne Dithering) noch dunkler; die Laibung traegt im Atlas eine dunklere Stufe.

Aufruf (aus dem Repository-Wurzelverzeichnis; TEMP/TMP auf einen eigenen Ordner setzen):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/wandnische-538/wandnische.py
Umgebungsvariable W538_ONLY (Komma-Liste, z. B. "modelle,auge") begrenzt die Schritte fuer Versuche. Kein Zufall: zwei Laeufe
liefern gleiche Dateien.
"""

import math
import os
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
PILLAR_PY = os.path.join(ROOT, "design", "proben", "grafik", "wandpfeiler-536", "wandpfeiler.py")

# --- Pfeiler-Skript laden (ohne sein main) ---------------------------------------------------------------------------
_src = open(PILLAR_PY, encoding="utf-8").read().rstrip()
assert _src.endswith("\nmain()"), "wandpfeiler.py endet nicht mehr mit main()"
W = {"__file__": PILLAR_PY, "__name__": "wandpfeiler_ns"}
exec(compile(_src[:-len("main()")], PILLAR_PY, "exec"), W)

bpy = W["bpy"]
bmesh = __import__("bmesh")
Matrix = W["Matrix"]
V, G, Part = W["V"], W["G"], W["Part"]
rects_from_pixels = W["rects_from_pixels"]
OUT = HERE
TMP = os.path.join(tempfile.gettempdir(), "wandnische538_tmp")
os.makedirs(TMP, exist_ok=True)
W["TMP"] = TMP
WALL_FILES = W["WALL_FILES"]
LIFT = W["LIFT"]
S = 0.0625

# --- Masse (Setzungen sind in LIESMICH.md begruendet) ------------------------------------------------------------------
Y_BOT = 0.4375            # Unterkante von Gewaendepfosten und Platte: 1/16 in der Sohlbank (kein Spalt, keine Deckungsgleichheit)
Y_SILL0, Y_SILL1 = 0.25, 0.5   # Sohlbank: eine Schicht, Oberseite = Sohle der Nische
BACKZ = -0.125            # Rueckseite 1/8 m in der Wand (Zahn), wie beim Pfeiler
PLATEZ = 1.0 / 64.0       # Vorderflaeche der Rueckwandplatte
PLATE_BACK = -0.0625
PLATE_D = 1.0 / 32.0      # Platte ragt 1/32 hinter die Laibung: Rand liegt im Gewaendekoerper
WI, WO = 0.125, 0.125     # Breite des inneren und aeusseren Bandes
SILL_HALF = 0.5 + WI + WO  # Sohlbank so breit wie das Gewaende aussen
FORMS = {   # R: Innenradius, CXA: Mittelpunkt neben der Achse, YK: Kaempferlinie, steps: Fugenwinkel (Grad) ab Kaempfer
    "spitz": dict(R=0.8125, CXA=0.3125, YK=1.25, steps=[0.0, 16.5, 33.0, 49.5]),
    "rund": dict(R=0.5, CXA=0.0, YK=1.5, steps=[0.0, 30.0, 60.0]),
}
FASSUNGEN = {  # z_in: Vorderkante der Laibung, z_out: Vorderkante des aeusseren Bandes, zs: Sohlbank vorn; limit: Vorsprung-Grenze
    "gang": dict(z_in=0.125, z_out=0.0625, zs=0.125, limit=0.125, blk="G", sill="SG"),
    "raum": dict(z_in=0.25, z_out=0.125, zs=0.25, limit=0.3125, blk="H", sill="SR"),
}
PLAYER_H = 1.8

# --- Atlas -----------------------------------------------------------------------------------------------------------
ATLAS_SIZE = (256, 232)
BW, BH = 18, 44           # Steinfeld: 18 px breit (Fugenabstand), 44 Zeilen = 0,6875 m Profilabwicklung
SW_, SH_ = 96, 40         # Sohlbankfeld
NB = 7                    # Steine je Haelfte
PIX = {"stone": (0, 0, 64, 64), "pillar": (64, 0, 128, 64), "plate": (128, 0, 192, 96),
       "SG": (0, 64, 96, 104), "SR": (0, 104, 96, 144)}
for _j in range(NB):
    PIX["G%d" % _j] = (BW * _j, 144, BW * _j + BW, 144 + BH)
    PIX["H%d" % _j] = (BW * _j, 188, BW * _j + BW, 188 + BH)
UV = rects_from_pixels(PIX, ATLAS_SIZE)
W["FRAME_UV"] = UV


def profile(f):
    """Querschnitt des Gewaendes (d, z): d = Abstand von der Oeffnungskante nach aussen, z = Hoehe vor der Wand."""
    zi, zo = f["z_in"], f["z_out"]
    return [(0.0, BACKZ), (0.0, zi - S), (S, zi), (WI, zi), (WI, zo), (WI + WO - S, zo), (WI + WO, zo - S), (WI + WO, BACKZ)]


def profile_s(f):
    """Abwicklung s je Profilpunkt (Meter), s = 0 an der Laibungskante P1; P0 negativ (verdeckt, nur zum Strecken der UV)."""
    p = profile(f)
    s = [0.0] * len(p)
    for i in range(1, 7):
        s[i] = s[i - 1] + math.hypot(p[i][0] - p[i - 1][0], p[i][1] - p[i - 1][1])
    s[0] = -(p[1][1] - p[0][1])
    s[7] = s[6]
    return s


def s_start(f):
    """s am unteren Rand des Steinfelds: 2 Zeilen vor dem sichtbaren Beginn der Laibung (Plattenvorderkante)."""
    return -(f["z_in"] - S - PLATEZ) - 2.0 / 64.0


def stein():
    return W["stein_row"]()


def tile_idx(rgb, st):
    flat = rgb.reshape(-1, 3)
    lut = {c: int(((st - np.array(c)) ** 2).sum(1).argmin()) for c in {tuple(int(x) for x in p) for p in flat}}
    return np.array([lut[tuple(int(x) for x in p)] for p in flat]).reshape(rgb.shape[:2])


def clean_stone(lift_i, mask):
    """Gewaendestein ohne die Fugen der Wandkachel: Fugenpixel (Maske) durch den naechsten Steinpixel rechts daneben ersetzt."""
    out, m = lift_i.copy(), mask.copy()
    for k in range(1, 14):
        take = m & ~np.roll(mask, -k, 1)
        out[take] = np.roll(lift_i, -k, 1)[take]
        m &= ~take
    out[m] = int(np.median(lift_i))
    return out


def shade_rows(rows_s, f, kind):
    """Dunkelstufen je Zeile des Steinfelds / Sohlbankfelds (nach der Abwicklung s): gemalte Schatten in harten Stufen."""
    s = profile_s(f)
    out = np.zeros(len(rows_s), int)
    if kind == "block":
        edges = [(-1e9, s[1], -2), (s[1], s[2], -1), (s[2], s[3], 0), (s[3], s[4], -1), (s[4], s[5], 0), (s[5], s[6], 0)]
    else:   # Sohlbank: Oberseite, obere Fase, Front, untere Fase
        cs = sill_s(f)
        edges = [(-1e9, cs["E"], 0), (cs["E"], cs["D"], 0), (cs["D"], cs["C"], -1), (cs["C"], cs["B"], -1)]
    for lo, hi, dl in edges:
        out[(rows_s >= lo) & (rows_s < hi)] = dl
    return out


def sill_pts(f):
    zs = f["zs"]
    return {"A": (BACKZ, Y_SILL0), "B": (zs - S, Y_SILL0), "C": (zs, Y_SILL0 + S), "D": (zs, Y_SILL1 - S), "E": (zs - S, Y_SILL1),
            "F": (BACKZ, Y_SILL1)}


def sill_s(f):
    """Abwicklung der Sohlbank: s = 0 an der Vorderkante der Oberseite (E), nach vorn und unten positiv."""
    p = sill_pts(f)
    d = lambda a, b: math.hypot(p[a][0] - p[b][0], p[a][1] - p[b][1])   # noqa: E731
    sE = 0.0
    sD = d("E", "D")
    sC = sD + d("D", "C")
    sB = sC + d("C", "B")
    return {"F": sE - (p["E"][0] - p["F"][0]), "E": sE, "D": sD, "C": sC, "B": sB, "A": sB}


def sill_start(f):
    return -(f["zs"] - S - PLATEZ) - 2.0 / 64.0


def edge_dist_stage(X, Y, form):
    F = FORMS[form]
    cx = F["CXA"] if X <= 0 else -F["CXA"]
    inside = F["R"] ** 2 - (abs(X) - F["CXA"]) ** 2 if F["CXA"] else F["R"] ** 2 - X ** 2
    top = F["YK"] + math.sqrt(max(inside, 0.0))
    d = min(0.5 - abs(X), (top - Y) * 0.9)
    return 3 if d < 1 / 16 else 2 if d < 1 / 8 else 1 if d < 3 / 16 else 0


def my_atlas(variant):
    st = stein()
    tile = W["load_tile"](WALL_FILES[variant])
    lifted = W["pillar_tile"](tile, st)
    t_i, l_i = tile_idx(tile, st), tile_idx(lifted, st)
    lo = int(t_i.min())
    mask = t_i <= lo + 2
    clean = clean_stone(l_i, mask)
    mortar = min(lo + LIFT - 2, len(st) - 1)
    h, w = ATLAS_SIZE[1], ATLAS_SIZE[0]
    idx = np.full((h, w), mortar, int)
    rgb_over = []

    # Rueckwandplatte: Wandkachel in Weltphase, 5 Stufen dunkler, am Rand harte Zusatzstufen (gemalte Tiefe)
    x0, y0, x1, y1 = PIX["plate"]
    tb = t_i[::-1]
    plate = np.zeros((y1 - y0, x1 - x0), int)
    for py in range(y1 - y0):
        for px in range(x1 - x0):
            X, Y = -0.5 + (px + 0.5) / 64.0, 0.5 + (py + 0.5) / 64.0
            base = tb[int(math.floor(Y * 64)) % 64, int(math.floor(X * 64)) % 64]
            plate[py, px] = min(max(base - 5 - edge_dist_stage(X, Y, "spitz"), 1), len(st) - 1)
    idx[y0:y1, x0:x1] = plate

    for name, f in FASSUNGEN.items():
        # Gewaendesteine
        sv = s_start(f) + (np.arange(BH) + 0.5) / 64.0
        dl = shade_rows(sv, f, "block")
        for j in range(NB):
            oy, ox = (11 * j + 5) % 64, (17 * j + 9) % 64
            p = np.roll(clean, (-oy, -ox), (0, 1))[:BH, :BW].copy()
            if j % 2 == 1:
                p = p - 1                                  # jeder zweite Stein eine Stufe dunkler: Steine lesen sich einzeln
            p = np.clip(p + dl[:, None], 1, len(st) - 1)
            p[:, :2] = mortar                               # Fuge 3 px (2 + 1 vom Nachbarn)
            if j < NB - 1:
                p[:, -1] = mortar
            x0, y0, x1, y1 = PIX["%s%d" % (f["blk"], j)]
            idx[y0:y1, x0:x1] = p
        # Sohlbank
        sv = sill_start(f) + (np.arange(SH_) + 0.5) / 64.0
        dl = shade_rows(sv, f, "sill")
        p = np.roll(clean, (-23, -7), (0, 1))
        p = np.concatenate([p, p], 1)[:SH_, :SW_].copy()
        p = np.clip(p + dl[:, None], 1, len(st) - 1)
        x0, y0, x1, y1 = PIX[f["sill"]]
        idx[y0:y1, x0:x1] = p

    out = st[idx].astype(float)
    out[0:64, 0:64] = tile[::-1]
    out[0:64, 64:128] = lifted[::-1]
    return out


W["atlas"] = my_atlas


def palette_check(tex):
    st = {tuple(int(c) for c in p) for p in stein()}
    cols = {tuple(int(c) for c in p) for p in np.round(tex).reshape(-1, 3)}
    return len(cols), len(cols - st)


# --- Geometrie -------------------------------------------------------------------------------------------------------

def stations(form):
    F = FORMS[form]
    st = [("post", Y_BOT)]
    y = 0.75
    while y < F["YK"] - 1e-9:
        st.append(("post", y))
        y += 0.25
    st.append(("post", F["YK"]))
    for a in F["steps"][1:]:
        st.append(("arc", math.radians(a)))
    st.append(("apex", None))
    assert len(st) == NB + 1
    return st


def pos(F, s, d, z):
    """Punkt der linken Haelfte (x <= 0) an Station s, Abstand d von der Oeffnungskante, Hoehe z vor der Wand."""
    kind, val = s
    if kind == "post":
        return (-(F["R"] - F["CXA"] + d), val, z)
    r = F["R"] + d
    if kind == "arc":
        return (F["CXA"] - r * math.cos(val), F["YK"] + r * math.sin(val), z)
    phi = math.acos(F["CXA"] / r)
    return (0.0, F["YK"] + r * math.sin(phi), z)


def uvp(rect, u, v):
    u0, v0, u1, v1 = UV[rect]
    return (u0 + (u1 - u0) * u, v0 + (v1 - v0) * v)


class Welder:
    def __init__(self, part):
        self.part, self.verts = part, {}

    def __call__(self, p):
        key = tuple(int(round(c * 1e5)) for c in p)
        if key not in self.verts:
            self.verts[key] = self.part.bm.verts.new(G(V(*p)))
        return self.verts[key]


def build_gewaende(form, fass):
    F, f = FORMS[form], FASSUNGEN[fass]
    part = Part("gewaende", (0, 0, 0), UV)
    weld = Welder(part)
    prof, s = profile(f), profile_s(f)
    st = stations(form)
    ls = s_start(f)
    span = BH / 64.0
    vv = [(si - ls) / span for si in s]
    ev = [(vv[i], vv[i + 1]) for i in range(6)] + [(vv[6], vv[6]), (vv[6], vv[6])]
    rings = [[pos(F, q, d, z) for d, z in prof] for q in st]
    for sgn in (-1, 1):
        for k in range(NB):
            rect = "%s%d" % (f["blk"], k)
            a, b = rings[k], rings[k + 1]
            for e in range(8):
                g = (e + 1) % 8
                vs = [weld((p[0] if sgn < 0 else -p[0], p[1], p[2])) for p in (a[e], a[g], b[g], b[e])]
                v0, v1 = ev[e]
                uvs = [uvp(rect, 0.0, v0), uvp(rect, 0.0, v1), uvp(rect, 1.0, v1), uvp(rect, 1.0, v0)]
                part._face(vs, uvs)
        cap = rings[0]
        part._face([weld((p[0] if sgn < 0 else -p[0], p[1], p[2])) for p in cap],
                   [uvp("%s0" % f["blk"], 0.5, 0.5)] * 8)
    bmesh.ops.recalc_face_normals(part.bm, faces=part.bm.faces[:])
    return part


def build_platte(form):
    F = FORMS[form]
    part = Part("platte", (0, 0, 0), UV)
    st = stations(form)
    left = [pos(F, q, PLATE_D, 0.0)[:2] for q in st]
    outline = left + [(-x, y) for x, y in reversed(left[:-1])]
    front = [part.bm.verts.new(G(V(x, y, PLATEZ))) for x, y in outline]
    back = [part.bm.verts.new(G(V(x, y, PLATE_BACK))) for x, y in outline]
    uv = [uvp("plate", min(max(x + 0.5, 0.0), 1.0), min(max((y - 0.5) / 1.5, 0.0), 1.0)) for x, y in outline]
    part._face(front, uv)
    mid = uvp("plate", 0.5, 0.5)
    part._face(list(reversed(back)), [mid] * len(back))
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        part._face([front[i], front[j], back[j], back[i]], [mid] * 4)
    bmesh.ops.recalc_face_normals(part.bm, faces=part.bm.faces[:])
    return part


def build_sohlbank(fass):
    f = FASSUNGEN[fass]
    part = Part("sohlbank", (0, 0, 0), UV)
    pts, cs = sill_pts(f), sill_s(f)
    order = ["A", "B", "C", "D", "E", "F"]
    ls = sill_start(f)
    span = SH_ / 64.0
    vv = {k: (cs[k] - ls) / span for k in order}
    rect = f["sill"]
    cols = {}
    for x in (-SILL_HALF, SILL_HALF):
        cols[x] = [part.bm.verts.new(G(V(x, pts[k][1], pts[k][0]))) for k in order]
    xl, xr = -SILL_HALF, SILL_HALF
    for i, k in enumerate(order):
        j = (i + 1) % 6
        k2 = order[j]
        va, vb = vv[k], vv[k2]
        if k == "A":
            va = vb                                   # Unterseite: eine Zeile (nicht sichtbar)
        if k == "F":
            vb = va                                   # Rueckseite in der Wand
        part._face([cols[xl][i], cols[xr][i], cols[xr][j], cols[xl][j]],
                   [uvp(rect, 0, va), uvp(rect, 1, va), uvp(rect, 1, vb), uvp(rect, 0, vb)])
    for x, u in ((xl, 0.0), (xr, 1.0)):
        part._face(list(cols[x]), [uvp(rect, u, vv[k]) for k in order])
    bmesh.ops.recalc_face_normals(part.bm, faces=part.bm.faces[:])
    return part


def niche_set(form, fass):
    return [build_gewaende(form, fass), build_platte(form), build_sohlbank(fass)]


def place(part, side, zpos, x0):
    """side 0: Wand in der Ebene z = 0 (Raum); side -1 / +1: Gangwand bei x = side, Nische an der Stelle zpos (Spiel-z)."""
    if side == 0:
        part.bm.transform(Matrix.Translation((x0, 0.0, 0.0)))
        return
    rot = Matrix.Rotation(math.radians(90 if side < 0 else -90), 4, "Z")
    part.bm.transform(Matrix.Translation((side * 1.0, -zpos, 0.0)) @ rot @ Matrix.Translation((x0, 0.0, 0.0)))


def game_bbox(parts):
    lo, hi = [9e9] * 3, [-9e9] * 3
    for p in parts:
        for v in p.bm.verts:
            g = (v.co.x, v.co.z, -v.co.y)
            for i in range(3):
                lo[i], hi[i] = min(lo[i], g[i]), max(hi[i], g[i])
    return lo, hi


# --- Szene -----------------------------------------------------------------------------------------------------------

def add_niche(coll, form, fass, v, x0=0.0, side=0, zpos=0.0):
    out = []
    for p in niche_set(form, fass):
        place(p, side, zpos, x0)
        name = "n_%s_%s_%s_%s_%g_%g_%g" % (form, fass, v, p.name, side, zpos, x0)
        out.append(p.build(coll, W["pass_material"](name, W["atlas_img"](v)), smooth=False))
    return out


def add_gang(coll, v, z0, z1, walls="LR", ceiling=True):
    part = Part("gang", (0, 0, 0), UV)
    x = 1.0
    emit = W["emit"]
    if "L" in walls:
        emit(part, [(-x, 0, z0), (-x, 0, z1), (-x, 3.5, z1), (-x, 3.5, z0)], (1, 0, 0), "stone")
    if "R" in walls:
        emit(part, [(x, 0, z0), (x, 0, z1), (x, 3.5, z1), (x, 3.5, z0)], (-1, 0, 0), "stone")
    if ceiling:
        emit(part, [(-x, 3.5, z0), (x, 3.5, z0), (x, 3.5, z1), (-x, 3.5, z1)], (0, -1, 0), "stone")
    return [part.build(coll, W["pass_material"]("g_%s_%g_%g" % (v, z0, z1), W["atlas_img"](v)), smooth=False)]


def add_door_x(coll, v, x, z=0.0):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(W["MODELS"], "door_frame_normal_%s.glb" % v))
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in objs:
        img = None
        for m in o.data.materials:
            for n in m.node_tree.nodes:
                if n.type == "TEX_IMAGE":
                    img = n.image
        o.data.materials.clear()
        o.data.materials.append(W["pass_material"]("d_" + o.name, img))
        o.parent = None
        o.matrix_world = Matrix.Translation(G(V(x, 0, z - 1)))
        coll.objects.link(o)
        for c in list(o.users_collection):
            if c is not coll:
                c.objects.unlink(o)
    return objs


def add_figure(coll, x, z, h, width, rgb):
    part = Part("figur", (0, 0, 0), {"all": (0.0, 0.0, 1.0, 1.0)})
    part.box((x - width / 2, 0, z - width / 2), (x + width / 2, h, z + width / 2), "all")
    tag = "fig_%g_%g_%d" % (x, h, rgb[0])
    img = W["save_image"](tag, np.tile(np.array(rgb, float), (4, 4, 1)), os.path.join(TMP, tag + ".png"))
    return [part.build(coll, W["pass_material"](tag, img), smooth=False)]


def build_scene(items):
    coll = W["new_scene"]()
    objs = []
    for it in items:
        k = it[0]
        if k == "niche":
            objs += add_niche(coll, *it[1:])
        elif k == "wall":
            objs += W["add_wall"](coll, *it[1:])
        elif k == "floor":
            objs += W["add_floor"](coll, *it[1:])
        elif k == "gang":
            objs += add_gang(coll, *it[1:])
        elif k == "pillar":
            objs += W["add_pillar"](coll, *it[1:])
        elif k == "door":
            objs += add_door_x(coll, *it[1:])
        elif k == "fig":
            objs += add_figure(coll, *it[1:])
    W["finish"](objs)
    return objs


W["build_scene"] = build_scene
shot, text, png, hcat, titled = W["shot"], W["text"], W["png"], W["hcat"], W["titled"]


# --- Modelle ---------------------------------------------------------------------------------------------------------

def export_models():
    rows = []
    d = os.path.join(OUT, "modelle")
    os.makedirs(d, exist_ok=True)
    jobs = [(form, fass, v) for form in ("spitz",) for fass in FASSUNGEN for v in WALL_FILES]
    jobs += [("rund", fass, "a") for fass in FASSUNGEN]
    for form, fass, v in jobs:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        coll = bpy.data.collections.new("Wandnische")
        bpy.context.scene.collection.children.link(coll)
        tex = my_atlas(v)
        ncol, bad = palette_check(tex)
        assert bad == 0, "Farben ausserhalb der Reihe Stein: %d" % bad
        tag = "wandnische_%s_%s_%s" % (form, fass, v)
        image = W["save_image"](tag, tex, os.path.join(d, tag + ".png"))
        mat = W["export_material"]("m", image)
        objs, info = [], []
        parts = niche_set(form, fass)
        lo, hi = game_bbox(parts)
        assert hi[2] <= FASSUNGEN[fass]["limit"] + 1e-9, "Vorsprung zu gross"
        for part in parts:
            o_edges, multi = W["edge_report"](part)
            info.append((part.name, W["triangles"](part), o_edges, multi))
            objs.append(part.build(coll, mat, smooth=False))
        W["export_glb"](objs, os.path.join(d, tag + ".glb"))
        rows.append((tag, info, ncol, lo, hi))
    for tag, info, ncol, lo, hi in rows:
        print("MODELL %s: %s, Farben %d, gesamt %d Dreiecke, Hoehe bis %.4f, Breite %.4f, Vorsprung %.4f" % (
            tag, "; ".join("%s %d Dreiecke, offen %d, mehrfach %d" % i for i in info), ncol, sum(i[1] for i in info),
            hi[1], hi[0] - lo[0], hi[2]))
    return rows


# --- Ansichten -------------------------------------------------------------------------------------------------------

def gang_items(v, form="spitz", z0=-9.0, z1=9.0, ceiling=True):
    return [("floor", -1, 1, z0, z1), ("gang", v, z0, z1, "LR", ceiling), ("niche", form, "gang", v, 0.0, -1, 0.0)]


def raum_items(v, form="spitz"):
    return [("floor", -8, 8, -1, 14), ("wall", v, -6, 6, 6.0), ("niche", form, "raum", v, 0.0, 0, 0.0)]


def items_for(fass, v, form="spitz"):
    return gang_items(v, form) if fass == "gang" else raum_items(v, form)


def view_eye(fass, v, mode, kind, size=(854, 480)):
    """Augenhoehe 1,6 m. gang: Nische an der linken Wand bei z = 0 (Blick quer / schraeg beim Gehen); raum: Wand z = 0."""
    it = items_for(fass, v)
    if fass == "gang":
        loc, tgt = {"frontal": ((0.8, 1.6, 0.0), (-1.0, 1.6, 0.0)), "schraeg": ((0.45, 1.6, 3.2), (-1.0, 1.55, 0.0))}[kind]
    else:
        loc, tgt = {"frontal": ((0.0, 1.6, 3.0), (0.0, 1.6, 0.0)), "schraeg": ((2.6, 1.6, 3.4), (0.0, 1.5, 0.0))}[kind]
    return shot(it, loc, tgt, 70, size, mode)


def view_grazing(fass, v, size=(854, 480)):
    """Streiflicht-Blick entlang der Wand (Blick fast parallel zur Wandebene, etwa 6 Grad)."""
    it = items_for(fass, v)
    if fass == "gang":
        return shot(it, (-0.7, 1.6, 3.2), (-1.0, 1.6, 0.0), 70, size, "hell")
    return shot(it, (3.6, 1.6, 0.36), (0.0, 1.6, 0.0), 70, size, "hell")


def view_grazing_far(fass, v, size=(854, 480)):
    it = items_for(fass, v)
    if fass == "gang":
        return shot(it, (-0.75, 1.6, 6.0), (-1.0, 1.6, 0.0), 70, size, "spiel")
    return shot(it, (5.0, 1.6, 0.5), (0.0, 1.6, 0.0), 70, size, "spiel")


def wall_items(fass, v, form="spitz", extra=()):
    h = 6.0 if fass == "raum" else 3.5
    return [("floor", -8, 8, -1, 14), ("wall", v, -6, 6, h), ("niche", form, fass, v, 0.0, 0, 0.0)] + list(extra)


def view_detail(fass, v, size=(420, 420)):
    it = wall_items(fass, v)
    a = shot(it, (0.75, 1.5, 1.1), (-0.45, 1.25, 0.0), 40, size, "hell")
    b = shot(it, (0.45, 1.3, 1.3), (0.0, 2.05, 0.0), 40, size, "hell")
    c = shot(it, (0.95, 0.95, 1.1), (-0.3, 0.5, 0.05), 40, size, "hell")
    d = shot(it, (7.0, 1.2, 0.0), (0.0, 1.2, 0.0), 0, size, "hell", ortho=2.6)
    return hcat([titled(a, "Gewaende links (Kaempfer)"), titled(b, "Scheitel von unten"), titled(c, "Sohlbank und Fuss"),
                 titled(d, "Seite (Vorsprung)")])


def view_front_ortho(fass, v, form="spitz", size=(380, 600), label=None):
    it = wall_items(fass, v, form)
    img = shot(it, (0, 1.4, 8), (0, 1.4, 0), 0, size, "hell", ortho=3.0)
    return titled(img, label or ("Wand " + v))


def view_single(fass, v, which, size=(360, 640)):
    it = [("floor", -3, 3, -3, 3), ("niche", "spitz", fass, v, 0.0, 0, 0.0)]
    cy = 1.2
    if which == "vorn":
        return shot(it, (0, cy, 8), (0, cy, 0), 0, size, "hell", ortho=3.2)
    if which == "seite":
        return shot(it, (8, cy, 0), (0, cy, 0), 0, size, "hell", ortho=3.2)
    if which == "hinten":
        return shot(it, (0, cy, -8), (0, cy, 0), 0, size, "hell", ortho=3.2)
    return shot(it, (2.2, 3.6, 3.0), (0, 1.1, 0), 38, size, "hell")


def view_compare(fass, v, oblique=False):
    red = (200, 90, 70)
    if fass == "raum":
        it = [("floor", -8, 8, -1, 14), ("wall", v, -6, -4.2, 6.0), ("wall", v, -2.2, 6, 6.0), ("door", v, -3.2), ("niche", "spitz", "raum", v, 0.0, 0, 0.0),
              ("pillar", "raum", v, 3.2), ("fig", 1.5, 0.3, PLAYER_H, 0.45, red)]
        if oblique:
            return shot(it, (4.5, 1.7, 7.5), (0, 1.6, 0), 60, (1000, 600), "hell")
        return shot(it, (0, 3.1, 12), (0, 3.1, 0), 0, (1000, 740), "hell", ortho=10.2)
    it = [("floor", -8, 8, -1, 14), ("wall", v, -5, -3.6, 3.5), ("wall", v, -1.6, 5, 3.5), ("door", v, -2.6), ("niche", "spitz", "gang", v, 0.0, 0, 0.0),
          ("pillar", "gang", v, 2.4), ("fig", 1.15, 0.3, PLAYER_H, 0.45, red)]
    if oblique:
        return shot(it, (3.5, 1.7, 5.5), (0, 1.6, 0), 60, (1000, 600), "hell")
    return shot(it, (0, 1.75, 12), (0, 1.75, 0), 0, (1000, 560), "hell", ortho=8.0)


def view_gang_tall(v):
    """Nische im 2 m breiten Gang gegen Spieler (1,8 m), Blick quer ueber den Gang."""
    red = (200, 90, 70)
    it = gang_items(v) + [("fig", 0.4, 0.0, PLAYER_H, 0.45, red)]
    return shot(it, (0.95, 1.6, 2.2), (-1.0, 1.4, 0.0), 70, (854, 480), "hell")


def view_round_vs_spitz(fass="gang", v="a"):
    h = 6.0 if fass == "raum" else 3.5
    it = [("floor", -8, 8, -1, 14), ("wall", v, -3, 3, h), ("niche", "spitz", fass, v, -1.2, 0, 0.0),
          ("niche", "rund", fass, v, 1.2, 0, 0.0)]
    a = shot(it, (0, 1.4, 8), (0, 1.4, 0), 0, (900, 520), "hell", ortho=5.2)
    t = titled(a, "links Spitzbogen (Issue, Setzung), rechts Rundbogen (Formenlehre-Zeile)")
    return t


def view_texture(v):
    tex = my_atlas(v)
    big = np.kron(tex, np.ones((3, 3, 1)))[::-1]
    return big


STEPS = os.environ.get("W538_ONLY")
STEPS = set(STEPS.split(",")) if STEPS else None


def want(name):
    return STEPS is None or name in STEPS


def main():
    if want("modelle"):
        export_models()
    for fass in FASSUNGEN:
        d = os.path.join(OUT, fass)
        if want("auge"):
            png(view_eye(fass, "a", "hell", "frontal"), os.path.join(d, "auge-frontal-hell.png"))
            png(view_eye(fass, "a", "hell", "schraeg"), os.path.join(d, "auge-schraeg-hell.png"))
            png(view_eye(fass, "a", "spiel", "frontal"), os.path.join(d, "spiel-frontal.png"))
            png(view_eye(fass, "a", "spiel", "schraeg"), os.path.join(d, "spiel-schraeg.png"))
        if want("streif"):
            png(view_grazing(fass, "a"), os.path.join(d, "streiflicht.png"))
            png(view_grazing_far(fass, "a"), os.path.join(d, "streiflicht-spiel.png"))
        if want("detail"):
            png(view_detail(fass, "a"), os.path.join(d, "detail.png"))
        if want("einzel"):
            png(hcat([titled(view_single(fass, "a", w), w) for w in ("vorn", "seite", "hinten", "oben")]),
                os.path.join(d, "einzel.png"))
        if want("vergleich"):
            png(view_compare(fass, "a"), os.path.join(d, "vergleich-tuer-pfeiler.png"))
            png(view_compare(fass, "a", True), os.path.join(d, "vergleich-tuer-pfeiler-schraeg.png"))
        if want("varianten"):
            png(hcat([view_front_ortho(fass, x) for x in WALL_FILES]), os.path.join(d, "wandvarianten.png"))
            for x in ("b", "c"):
                png(view_eye(fass, x, "spiel", "schraeg"), os.path.join(d, "wandvarianten", "spiel-schraeg-%s.png" % x))
                png(view_eye(fass, x, "hell", "frontal"), os.path.join(d, "wandvarianten", "auge-frontal-%s.png" % x))
        if want("rund"):
            png(view_round_vs_spitz(fass), os.path.join(d, "rundbogen-vergleich.png"))
    if want("auge"):
        png(view_eye("gang", "a", "spiel", "schraeg"), os.path.join(OUT, "spielaufloesung.png"))
        png(view_gang_tall("a"), os.path.join(OUT, "gang-mit-spieler.png"))
    if want("tex"):
        png(view_texture("a"), os.path.join(OUT, "textur-a.png"))


main()
