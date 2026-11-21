"""Wandnische (Issue #538, Baustein aus der Formenlehre Gotik #281): spitzbogige Nische vor der Wand fuer Gang (3,5 m) und
Raum (6 m).

Erzeuger fuer das Spiel. Uebergabe aus dem abgenommenen Probestueck design/proben/grafik/wandnische-538/wandnische.py
(Branch feat/wandnische-probe-538, Commit 07cac5c): gleiche Geometrie und derselbe Atlas, nur die Form "spitz" und die
Fassungen "gang" (hier corridor) und "raum" (room). Die Teile heissen frame (Gewaende), back_plate (Rueckwandplatte) und
sill (Sohlbank). Das Probestueck hellt den Gewaendestein mit der Stauchung des Pfeiler-Probestuecks auf (5 Stufen, linear in
den oberen Rest); die Nische malt ihre Fugen selbst (Fugenfarbe), darum bleibt diese Aufhellung hier unveraendert
(niche_lift) und weicht bewusst von pillar_kit.pillar_tile (LIFT = 2) ab. Erzeugt nur Modelle und Texturen nach
assets/models/, keine Probebilder, dazu die Liste der gelesenen Wand-Texturen (tools/models/wall_niche_sources.sha256).

Aufruf (aus dem Repository-Wurzelverzeichnis), danach python tools/models/checksums.py:
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/wall_niche.py

Alles per Skript, nichts heruntergeladen. Spiel-Achsen (x seitlich, y oben, z vorn), Meter. Ursprung: Mitte der Nische auf
dem Boden, Wandflaeche z = 0, die Nische steht nach +z vor; die Rueckseiten liegen 1/8 m in der Wand (BACKZ).

TIEFE: Die Wand im Spiel ist eine duenne Flaeche am Zellraster und laesst sich nicht aushoehlen. Die Nische ist ein
vorgesetzter, gestufter Gewaendekranz mit Sohlbank; die Oeffnung zeigt eine dunkle Rueckwandplatte 1/64 vor der Wand. Die
Platte ist die Wandkachel in Weltphase um 5 Stufen abgedunkelt, am Rand in harten Stufen noch dunkler (gemalte Tiefe).

Masse: Oeffnung 1,0 breit, Sohle 0,5, gedrueckter Spitzbogen R = 13/16, Mittelpunkte 0,3125 neben der Achse, Kaempfer 1,25,
Scheitel 2,0. Zwei Baender je 1/8, Gesamtbreite 1,5; Sohlbank 0,25 bis 0,5, so breit wie das Gewaende.
  corridor  Laibung vorn 0,125, aeusseres Band 0,0625, Sohlbank 0,125; Vorsprung hoechstens 0,125
  room      Laibung vorn 0,25,  aeusseres Band 0,125,  Sohlbank 0,25;  Vorsprung hoechstens 0,3125

Dateien: wall_niche_<fassung>_<v>.glb und .png, v die Wandvariante a, b, c. Drei Knoten (frame, back_plate, sill), jedes
Netz rundum geschlossen. Atlas 256 x 232 (Felder wie im Probestueck). Die Mitte der Nische steht im Spiel auf der Mitte einer
Zellwand (ganze Meterzahl), dann liegt die Platte in Weltphase der Wand. Aendert sich eine Wand-Textur oder die Palette,
passt die Nische nicht mehr; ein Test vergleicht wall_niche_sources.sha256 mit assets/textures/textures.sha256 und schlaegt
fehl, bis dieses Skript neu gelaufen ist. Zwei Laeufe liefern gleiche Dateien (kein Zufall).
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh  # noqa: E402
import bpy  # noqa: E402
from psx import V, G, Part, rects_from_pixels, save_image, export_material, export_glb, report_parts  # noqa: E402
from pillar_kit import (OUT_DIR, TOOLS, WALL_FILES, colours_of, edge_report, load_tile, stein_row,  # noqa: E402
                        steps_of, write_sources)

SOURCES = os.path.join(TOOLS, "wall_niche_sources.sha256")
PARTS = ("frame", "back_plate", "sill")
NICHE_LIFT = 5            # Stein-Stufen heller als die Wand, wie im Probestueck (lineare Stauchung, niche_lift)
MIN_JOINT = 1             # gemalte Fuge mindestens so viele Stufen dunkler als die hellen Steine (Median); das abgenommene
                          # Probestueck erreicht auf Wand B nicht mehr (bekannte Schwaeche: wenig Kontrast auf Variante b)
S = 0.0625

# --- Masse (wie im Probestueck, dort in LIESMICH.md begruendet) -------------------------------------------------------
Y_BOT = 0.4375            # Unterkante von Gewaendepfosten und Platte: 1/16 in der Sohlbank
Y_SILL0, Y_SILL1 = 0.25, 0.5   # Sohlbank: eine Schicht, Oberseite = Sohle der Nische
BACKZ = -0.125            # Rueckseite 1/8 m in der Wand (Zahn), wie beim Pfeiler
PLATEZ = 1.0 / 64.0       # Vorderflaeche der Rueckwandplatte
PLATE_BACK = -0.0625
PLATE_D = 1.0 / 32.0      # Platte ragt 1/32 hinter die Laibung: Rand liegt im Gewaendekoerper
WI, WO = 0.125, 0.125     # Breite des inneren und aeusseren Bandes
SILL_HALF = 0.5 + WI + WO  # Sohlbank so breit wie das Gewaende aussen
FORM = dict(R=0.8125, CXA=0.3125, YK=1.25, steps=[0.0, 16.5, 33.0, 49.5])   # gedrueckter Spitzbogen
SIZES = {  # z_in: Vorderkante der Laibung, z_out: Vorderkante des aeusseren Bandes, zs: Sohlbank vorn; limit: Vorsprung
    "corridor": dict(z_in=0.125, z_out=0.0625, zs=0.125, limit=0.125, blk="G", sill="SG"),
    "room": dict(z_in=0.25, z_out=0.125, zs=0.25, limit=0.3125, blk="H", sill="SR"),
}

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


def tile_idx(rgb, stein):
    return steps_of(rgb, stein).reshape(rgb.shape[:2])


def niche_lift(tile, stein):
    """Kachel um NICHE_LIFT Stufen heller, die Stufen oben linear zusammengedraengt (wie das Pfeiler-Probestueck)."""
    flat = tile.reshape(-1, 3)
    colours = sorted({tuple(int(c) for c in p) for p in flat})
    idx = {c: int(((stein - np.array(c)) ** 2).sum(1).argmin()) for c in colours}
    lo, hi, top = min(idx.values()), max(idx.values()), len(stein) - 1
    new_lo = min(max(lo + NICHE_LIFT, 0), top)
    span = max(hi - lo, 1)
    mapped = {c: int(round(new_lo + (i - lo) * (top - new_lo) / span)) for c, i in idx.items()}
    return np.array([stein[mapped[tuple(int(c) for c in p)]] for p in flat]).reshape(tile.shape)


def clean_stone(lift_i, mask):
    """Gewaendestein ohne die Fugen der Wandkachel: Fugenpixel (Maske) durch den naechsten Steinpixel rechts daneben ersetzt."""
    out, m = lift_i.copy(), mask.copy()
    for k in range(1, 14):
        take = m & ~np.roll(mask, -k, 1)
        out[take] = np.roll(lift_i, -k, 1)[take]
        m &= ~take
    out[m] = int(np.median(lift_i))
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


def edge_dist_stage(X, Y):
    """Zusatz-Dunkelstufe der Platte nach dem Abstand zum Rand der Oeffnung (0 bis 3)."""
    F = FORM
    inside = F["R"] ** 2 - (abs(X) - F["CXA"]) ** 2
    top = F["YK"] + math.sqrt(max(inside, 0.0))
    d = min(0.5 - abs(X), (top - Y) * 0.9)
    return 3 if d < 1 / 16 else 2 if d < 1 / 8 else 1 if d < 3 / 16 else 0


def atlas(variant, stein):
    """Atlas der Variante und die Stufen (Index in der Reihe Stein) fuer die Pruefungen."""
    tile = load_tile(WALL_FILES[variant])
    lifted = niche_lift(tile, stein)
    t_i, l_i = tile_idx(tile, stein), tile_idx(lifted, stein)
    lo = int(t_i.min())
    mask = t_i <= lo + 2
    clean = clean_stone(l_i, mask)
    mortar = min(lo + NICHE_LIFT - 2, len(stein) - 1)
    h, w = ATLAS_SIZE[1], ATLAS_SIZE[0]
    idx = np.full((h, w), mortar, int)

    # Rueckwandplatte: Wandkachel in Weltphase, 5 Stufen dunkler, am Rand harte Zusatzstufen (gemalte Tiefe)
    x0, y0, x1, y1 = PIX["plate"]
    tb = t_i[::-1]
    plate = np.zeros((y1 - y0, x1 - x0), int)
    for py in range(y1 - y0):
        for px in range(x1 - x0):
            X, Y = -0.5 + (px + 0.5) / 64.0, 0.5 + (py + 0.5) / 64.0
            base = tb[int(math.floor(Y * 64)) % 64, int(math.floor(X * 64)) % 64]
            plate[py, px] = min(max(base - 5 - edge_dist_stage(X, Y), 1), len(stein) - 1)
    idx[y0:y1, x0:x1] = plate

    for f in SIZES.values():
        # Gewaendesteine
        sv = s_start(f) + (np.arange(BH) + 0.5) / 64.0
        dl = shade_rows(sv, f, "block")
        for j in range(NB):
            oy, ox = (11 * j + 5) % 64, (17 * j + 9) % 64
            p = np.roll(clean, (-oy, -ox), (0, 1))[:BH, :BW].copy()
            if j % 2 == 1:
                p = p - 1                                  # jeder zweite Stein eine Stufe dunkler: Steine lesen sich einzeln
            p = np.clip(p + dl[:, None], 1, len(stein) - 1)
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
        p = np.clip(p + dl[:, None], 1, len(stein) - 1)
        x0, y0, x1, y1 = PIX[f["sill"]]
        idx[y0:y1, x0:x1] = p

    out = stein[idx].astype(float)
    out[0:64, 0:64] = tile[::-1]
    out[0:64, 64:128] = lifted[::-1]
    return out, idx, t_i, mortar


def check_atlas(name, tex, idx, t_i, mortar, stein):
    """Nur Farben der Reihe Stein; gemalte Fugen sichtbar dunkler als der Gewaendestein; Platte dunkler als die Wand."""
    row = {tuple(int(c) for c in p) for p in stein}
    cols = {tuple(int(c) for c in p) for p in np.round(tex).reshape(-1, 3)}
    assert not cols - row, f"{name}: {len(cols - row)} Farben ausserhalb der Reihe Stein"
    assert len(cols) <= 40, f"{name}: {len(cols)} Farben (hoechstens 40)"
    faces = {}
    for size, f in SIZES.items():
        blocks = []
        for j in range(NB):
            x0, y0, x1, y1 = PIX["%s%d" % (f["blk"], j)]
            blocks.append(idx[y0:y1, x0 + 2:x1 - 1])              # ohne die gemalten Fugenspalten
        assert min(int(b.min()) for b in blocks) >= mortar, f"{name} {size}: Stein dunkler als die Fuge {mortar}"
        even = int(np.median(np.concatenate([b.ravel() for b in blocks[0::2]])))
        odd = int(np.median(np.concatenate([b.ravel() for b in blocks[1::2]])))
        assert even - mortar >= MIN_JOINT, f"{name} {size}: Fuge {mortar} zu nah am Stein {even} (mindestens {MIN_JOINT})"
        faces[size] = (even, odd)
    x0, y0, x1, y1 = PIX["plate"]
    plate, wall = int(np.median(idx[y0:y1, x0:x1])), int(np.median(t_i))
    assert plate < wall, f"{name}: Platte {plate} nicht dunkler als die Wand {wall}"
    print(f"FUGEN {name}: Fuge {mortar}, Stein hell/dunkel {faces}, Platte {plate}, Wand {wall}")


# --- Geometrie -------------------------------------------------------------------------------------------------------

def stations():
    F = FORM
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


def pos(s, d, z):
    """Punkt der linken Haelfte (x <= 0) an Station s, Abstand d von der Oeffnungskante, Hoehe z vor der Wand."""
    F = FORM
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


def build_frame(size):
    f = SIZES[size]
    part = Part("frame", (0, 0, 0), UV)
    weld = Welder(part)
    prof, s = profile(f), profile_s(f)
    st = stations()
    ls = s_start(f)
    span = BH / 64.0
    vv = [(si - ls) / span for si in s]
    ev = [(vv[i], vv[i + 1]) for i in range(6)] + [(vv[6], vv[6]), (vv[6], vv[6])]
    rings = [[pos(q, d, z) for d, z in prof] for q in st]
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


def build_back_plate():
    part = Part("back_plate", (0, 0, 0), UV)
    st = stations()
    left = [pos(q, PLATE_D, 0.0)[:2] for q in st]
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


def build_sill(size):
    f = SIZES[size]
    part = Part("sill", (0, 0, 0), UV)
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


def forward_extent(parts):
    """Groesster Vorsprung vor der Wand (Spiel-z) ueber alle Teile."""
    return max(-v.co.y for p in parts for v in p.bm.verts)


def triangles(part):
    return sum(len(f.verts) - 2 for f in part.bm.faces)


# --- Ausgabe ------------------------------------------------------------------------------------------------------

def write(size, variant, stein):
    name = f"wall_niche_{size}_{variant}"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Niche")
    bpy.context.scene.collection.children.link(coll)
    tex, idx, t_i, mortar = atlas(variant, stein)
    check_atlas(name, tex, idx, t_i, mortar, stein)
    image = save_image(name, tex, os.path.join(OUT_DIR, name + ".png"))
    material = export_material(name, image)
    parts = [build_frame(size), build_back_plate(), build_sill(size)]
    reach = forward_extent(parts)
    assert reach <= SIZES[size]["limit"] + 1e-9, f"{name}: Vorsprung {reach:.4f} ueber {SIZES[size]['limit']}"
    objs, total = [], 0
    for part in parts:
        open_edges, multi = edge_report(part)
        assert open_edges == 0 and multi == 0, f"{name} {part.name}: {open_edges} offene Kanten, {multi} mehrfach"
        total += triangles(part)
        objs.append(part.build(coll, material, smooth=False))
    assert tuple(o.name for o in objs) == PARTS
    export_glb(objs, os.path.join(OUT_DIR, name + ".glb"))
    report_parts(name, objs)
    print(f"TEXTUR {name} {ATLAS_SIZE[0]}x{ATLAS_SIZE[1]} Farben {colours_of(tex)}, {total} Dreiecke, Vorsprung {reach:.4f}")


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    stein = stein_row()
    for size in SIZES:
        for variant in WALL_FILES:
            write(size, variant, stein)
    write_sources(SOURCES, "tools/models/wall_niche.py", "wall niches")


main()
