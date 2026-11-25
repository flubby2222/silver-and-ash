"""Probestueck Hunter's Axe (Issue #201), Runde 2: Entwurf C (Pfahlaxt) mit neuer Textur des Axtkopfes.

Urteil des Menschen zu Runde 1: "C, die Textur des Axtkopfes sollte einmal ueberarbeitet werden. Mir passt der gelbe Part
nicht." Die Form von C bleibt (Silberpfahl oben, Haken hinten). Das mandelfoermige gelbe Auge samt hellem Ring fliegt raus;
auf dem Axtkopf gibt es kein Gelb und kein Gold mehr. Dazu: Stiel gekuerzt, Haken kraeftiger und auf die Rueckseite gesetzt
(in Runde 1 sass er durch einen Vorzeichenfehler auf der Schneidenseite), alle Teile weiter rundum geschlossen.

Erzeugt aus diesem einen Skript (nichts heruntergeladen, nur Blender-Python und NumPy; Geometrie-Hilfen aus runde-1/axt.py):
    axt-c1-geschmiedet.glb     C1 schlichtes geschmiedetes Eisen, helle Schneidenfase, dunkle Hammerschlag-Flecken
    axt-c2-rune.glb            C2 helles Eisen mit eingeritzter Silberlinie und Silberrune
    axt-c3-schartig.glb        C3 alt und schartig: Kerben in der Schneide, dunkle Patina, Kratzer
    axt-c1-eisenzwingen.glb    C1 mit Eisen statt Gold an Zwingen, Schaftfedern und Knauf (Vergleich ohne Gold)
    blatt.png                  Vergleichsblatt (C aus Runde 1, je Fassung Seite / Dreiviertel / Kopf nah / Ich-Sicht / Groesse)
    ich-<fassung>-854.png      Ich-Sicht in Spielaufloesung 854 x 480
    textur-<fassung>.png       die 128 x 128 Textur

Aufruf (aus dem Repository-Wurzelverzeichnis, TEMP/TMP auf einen Zwischenordner setzen):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/hunters-axe-201/runde-2/axt2.py
"""

import json
import math
import os
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
R1 = os.path.join(os.path.dirname(HERE), "runde-1")

# Geometrie- und Blatthilfen der ersten Runde laden (ohne deren main) und danach gezielt ueberschreiben.
_src = open(os.path.join(R1, "axt.py"), encoding="utf-8").read().split('if __name__ == "__main__":')[0]
__file__ = os.path.join(R1, "axt.py")          # damit HERE/ROOT der Runde 1 stimmen
exec(compile(_src, __file__, "exec"), globals())
OUT = os.path.join(os.path.dirname(R1), "runde-2")
assert os.path.isdir(OUT), OUT

# --- Palette: je Fassung hoechstens 16 Farben in Summe -------------------------------------------------------------------

EIS = _ROWS["Eisen"]                    # Stufen 0..7, dunkel nach hell
HOLZ = _ROWS["Holz"][1:6]               # 5 Stufen: Stiel und Leder
GOLD3 = _ROWS["Messing/Gold"][3:6]      # 3 Stufen, nur Zwingen, Schaftfedern, Knauf (Stufe Royal)

# Atlas 128 x 128: Platte bekommt ein fast quadratisches Feld (Texel ca. 1,4 x 1,6 mm), damit Rune und Linien nicht gestreckt sind.
RECTS = {
    "iron": (0, 0, 80, 80),
    "dark": (80, 0, 128, 24),
    "silver": (80, 24, 128, 56),
    "gold": (80, 56, 128, 80),
    "grip": (0, 80, 64, 104),
    "wood": (0, 104, 128, 128),
}
UV = rects_from_pixels(RECTS, TEX)

SH = 0.115                               # Stiel gekuerzt: alles ueber dem Griff liegt um SH tiefer (Spielmass der Datei, vor Skalierung 1,8)


def pick(ramp_idx, field):
    """Feld 0..1 in harten Stufen auf eine Liste von Eisenstufen legen: nur Palettenfarben."""
    ramp = np.array([EIS[i] for i in ramp_idx], float)
    return step(ramp, field)


def seg_dist(E, Z, a, b):
    ab = np.array(b) - np.array(a)
    t = np.clip(((E - a[0]) * ab[0] + (Z - a[1]) * ab[1]) / float(ab @ ab), 0, 1)
    return np.hypot(E - (a[0] + t * ab[0]), Z - (a[1] + t * ab[1]))


def poly_dist(E, Z, pts, idx):
    """Abstand zur Schneide: Segmente i -> i+1 fuer i in idx."""
    d = np.full(E.shape, 9.0)
    for i in idx:
        d = np.minimum(d, seg_dist(E, Z, pts[i], pts[(i + 1) % len(pts)]))
    return d


def path_dist(E, Z, path):
    d = np.full(E.shape, 9.0)
    for a, b in zip(path[:-1], path[1:]):
        d = np.minimum(d, seg_dist(E, Z, a, b))
    return d


def plate_painter(variant, bounds, pts, edge_idx):
    e0, e1, z0, z1 = bounds
    rc = (0.044, 0.250 - SH)            # Mitte der Plattenflaeche

    def paint(U, V_):
        h, w = U.shape
        E, Z = e0 + U * (e1 - e0), z0 + V_ * (z1 - z0)
        de = poly_dist(E, Z, pts, edge_idx)
        if variant == "c1":
            f = 0.15 + 0.6 * vnoise(h, w, 3, 4, 301)
            col = pick((2, 3, 4), f)
            dent = vnoise(h, w, 14, 14, 302) < 0.20                    # Hammerschlag: dunkle Mulden
            col = np.where(dent[..., None], pick((1,), f), col)
            fleck = vnoise(h, w, 9, 9, 303) > 0.88                     # blanke Schlagstellen
            col = np.where(fleck[..., None], pick((5,), f), col)
            col = np.where(((de < 0.016) & (de >= 0.011))[..., None], pick((4,), f), col)
            col = np.where(((de < 0.011) & (de >= 0.0055))[..., None], pick((6,), f), col)
            col = np.where((de < 0.0055)[..., None], pick((7,), f), col)  # helle Schneidenfase
            return col
        if variant == "c2":
            f = 0.2 + 0.6 * vnoise(h, w, 2, 30, 311)                   # gebuerstetes, helles Eisen
            col = pick((4, 5), f)
            col = np.where((vnoise(h, w, 12, 12, 312) < 0.12)[..., None], pick((3,), f), col)
            groove = (de > 0.0165) & (de < 0.0205)                    # umlaufende Ritzlinie nach innen versetzt
            col = np.where(groove[..., None], pick((1,), f), col)
            lit = (de >= 0.0205) & (de < 0.0235)
            col = np.where(lit[..., None], pick((7,), f), col)
            col = np.where((de < 0.0045)[..., None], pick((7,), f), col)
            # Rune: Stab mit drei Ästen, selbst erfunden (kein Schriftzeichen aus fremden Werken)
            cx, cz = rc
            strokes = [[(cx, cz - 0.030), (cx, cz + 0.030)],
                       [(cx, cz + 0.030), (cx + 0.016, cz + 0.014)],
                       [(cx, cz + 0.008), (cx + 0.016, cz - 0.008)],
                       [(cx, cz - 0.004), (cx - 0.014, cz + 0.012)]]
            line = np.zeros(E.shape, bool)
            shadow = np.zeros(E.shape, bool)
            for s in strokes:
                line |= path_dist(E, Z, s) < 0.0030
                shadow |= path_dist(E + 0.0022, Z + 0.0022, s) < 0.0030
            col = np.where(shadow[..., None], pick((1,), f), col)
            col = np.where(line[..., None], pick((7,), f), col)
            return col
        # c3: alt, schartig, dunkle Patina
        f = 0.1 + 0.8 * vnoise(h, w, 4, 5, 321)
        col = pick((3, 4), f)
        col = np.where((vnoise(h, w, 5, 5, 322) < 0.36)[..., None], pick((2,), f), col)          # tiefe Patina
        rng = np.random.default_rng(325)                                                           # Kratzer: einzelne Striche
        scratch = np.zeros(E.shape, bool)
        for _ in range(7):
            a0 = (e0 + (e1 - e0) * rng.uniform(0.1, 0.8), z0 + (z1 - z0) * rng.uniform(0.15, 0.85))
            ang = rng.uniform(0.4, 1.2)
            ln = rng.uniform(0.014, 0.034)
            scratch |= path_dist(E, Z, [a0, (a0[0] + ln * math.cos(ang), a0[1] + ln * math.sin(ang))]) < 0.0013
        col = np.where(scratch[..., None], pick((6,), f), col)                                     # Kratzer
        worn = (de < 0.0065) & (vnoise(h, w, 10, 10, 324) > 0.30)
        col = np.where(worn[..., None], pick((6,), f), col)                                        # blank gewetzte Schneide
        col = np.where(((de < 0.0125) & (de >= 0.0065))[..., None], pick((4, 5), f), col)
        return col
    return paint


def make_painters(variant, bounds, pts, edge_idx, gold):
    def gold_paint(U, V_):
        h, w = U.shape
        f = 0.2 + 0.7 * vnoise(h, w, 4, 10, 206)
        if gold:
            return step(GOLD3, f)
        return pick((2, 3, 4), f)       # Eisen statt Gold

    def silver_paint(U, V_):
        h, w = U.shape
        f = 0.35 + 0.55 * vnoise(h, w, 4, 16, 205)
        f = np.where(np.abs(V_ - 0.5) < 0.08, 1.0, f)
        return pick((5, 6, 7), f)

    def dark_paint(U, V_):
        h, w = U.shape
        return pick((0, 1, 2), 0.2 + 0.7 * vnoise(h, w, 5, 5, 207))

    return {"wood": paint_wood_r2, "grip": paint_grip_r2, "silver": silver_paint, "gold": gold_paint, "dark": dark_paint,
            "iron": plate_painter(variant, bounds, pts, edge_idx)}


def paint_wood_r2(U, V_):
    h, w = U.shape
    f = 0.15 + 0.7 * vnoise(h, w, 2, 24, 201)
    f = np.where(vnoise(h, w, 9, 30, 202) < 0.14, 0.0, f)
    f = np.where(vnoise(h, w, 4, 6, 203) > 0.86, 0.95, f)
    return step(HOLZ, f)


def paint_grip_r2(U, V_):
    h, w = U.shape
    wrap = (U * 2.0 + V_ * 5) % 1.0
    f = np.where(wrap > 0.2, 0.15 + 0.3 * vnoise(h, w, 3, 8, 204), 0.95)
    return step(HOLZ[:3], f)


def make_atlas2(painters):
    atlas = np.zeros((TEX, TEX, 3)) + EIS[0]
    for name, (x0, y0, x1, y1) in RECTS.items():
        pw, ph = x1 - x0, y1 - y0
        U, V_ = np.meshgrid((np.arange(pw) + 0.5) / pw, (np.arange(ph) + 0.5) / ph)
        atlas[y0:y1, x0:x1] = painters[name](U, V_)
    return atlas


# --- Geometrie: C, gekuerzt, Haken kraeftiger und hinten ---------------------------------------------------------------------

def sh(pts):
    return [(e, z - SH) for e, z in pts]


C_BASE = sh(C_PTS)
C_ED = list(C_EDGE)
C_DD = list(C_D)


def notched():
    """C3: V-Kerben in die Schneide (echte Geometrie): drei Stellen auf den Schneidensegmenten 3, 4 und 5."""
    pts, edge, ds = [], [], []
    notches = {3: (0.55, 0.0075, 0.0058), 4: (0.40, 0.0090, 0.0070), 5: (0.62, 0.0070, 0.0055)}
    n = len(C_BASE)
    for i in range(n):
        a, b = np.array(C_BASE[i]), np.array(C_BASE[(i + 1) % n])
        pts.append(tuple(a))
        ds.append(C_D[i])
        edge.append(C_EDGE[i])
        if i in notches:
            s, hw, depth = notches[i]
            t = (b - a) / np.linalg.norm(b - a)
            nrm = np.array((t[1], -t[0]))                 # nach aussen (Umriss gegen den Uhrzeigersinn)
            p = a + (b - a) * s
            for q, d in ((p - t * hw, 0.0075), (p - nrm * depth, 0.0060), (p + t * hw, 0.0075)):
                pts.append(tuple(q))
                ds.append(d)
                edge.append(True)
    return pts, edge, ds


HOOK = [(-0.006, 0.256), (-0.034, 0.262), (-0.058, 0.256), (-0.076, 0.240), (-0.084, 0.220), (-0.080, 0.202)]
HOOK_W = [0.0160, 0.0140, 0.0118, 0.0094, 0.0066, 0.0012]
HOOK_T = [0.0130, 0.0112, 0.0092, 0.0070, 0.0048, 0.0012]


def build_variant(variant, gold):
    if variant == "c3":
        pts, edge, ds = notched()
    else:
        pts, edge, ds = C_BASE, C_ED, C_DD
    part = Part("axe", (0, 0, 0), UV)
    b = bounds_of(pts)
    edge_idx = [i for i, e in enumerate(edge) if e]
    haft(part, -0.060, 0.300 - SH, swell=0.0008)
    grip(part, -0.052, 0.052)
    pommel(part, -0.060, "cone")
    plate(part, pts, edge, [0.0035 if not e else 0.0016 for e in edge], 0.0115, ds, b)
    # Haken auf der RUECKSEITE (x = SIDE * e mit e < 0), kraeftig, flache Roehre in der Plattenebene
    part.loft(tube([V(SIDE * e, 0, z - SH) for e, z in HOOK], [(w, t, t) for w, t in zip(HOOK_W, HOOK_T)], 6, ref_up=V(0, 1, 0)),
              "silver")
    collar(part, 0.200 - SH, 0.008, 0.0140)
    collar(part, 0.296 - SH, 0.007, 0.0140)
    langets(part, 0.198 - SH, 0.052)
    rivets(part, [(0.020, 0.250 - SH)], 0.0115)
    # Pfahl am Stielende (Silber), etwas kuerzer als in Runde 1
    z0 = 0.296 - SH
    zs = [z0, z0 + 0.014, z0 + 0.036, z0 + 0.054, z0 + 0.064]
    part.loft(tube([V(0, 0, z) for z in zs], [(r, r, r) for r in (0.0100, 0.0108, 0.0072, 0.0032, 0.0006)], 4), "silver")
    return part, make_painters(variant, b, pts, edge_idx, gold)


FASSUNGEN = [
    # Schluessel, Beschriftung, Datei, Variante, Gold an Zwingen
    ("c1", "C1 Geschmiedet", "axt-c1-geschmiedet.glb", "c1", True),
    ("c2", "C2 Rune", "axt-c2-rune.glb", "c2", True),
    ("c3", "C3 Schartig", "axt-c3-schartig.glb", "c3", True),
    ("c1e", "C1 Eisenzwingen", "axt-c1-eisenzwingen.glb", "c1", False),
]

COLOR_COUNT = {}
TEXS = {}


def export_variant(key, label, fname, variant, gold):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    part, painters = build_variant(variant, gold)
    edges, open_e, multi, loose = closed_report(part)
    print(f"PRUEFUNG {label}: Kanten {edges}, offene Kanten {open_e}, Kanten mit mehr als zwei Flaechen {multi}, lose Kanten {loose}")
    assert open_e == 0 and multi == 0 and loose == 0, label
    tex = make_atlas2(painters)
    cols = {tuple(int(v) for v in c) for c in tex.reshape(-1, 3)}
    COLOR_COUNT[key] = len(cols)
    TEXS[key] = tex.copy()
    assert len(cols) <= 16, (label, len(cols))
    pal = {tuple(int(v) for v in c) for r in _PAL["akzent"] for c in r["farben"]}
    assert cols <= pal, (label, cols - pal)
    # kein Gelb/Gold auf der Plattenflaeche
    x0, y0, x1, y1 = RECTS["iron"]
    face = {tuple(int(v) for v in c) for c in tex[y0:y1, x0:x1].reshape(-1, 3)}
    assert all(not (c[0] > c[2] + 25 and c[1] > c[2] + 15) for c in face), (label, face)
    save_png(tex[::-1], os.path.join(OUT, f"textur-{key}.png"))
    image = save_image("axe_" + key, tex, os.path.join(tempfile.gettempdir(), f"axe2_{key}.png"))
    coll = bpy.data.collections.new("Axt")
    bpy.context.scene.collection.children.link(coll)
    obj = part.build(coll, export_material("axe_" + key, image), smooth=False)
    tris = len(obj.data.polygons)
    pose([obj], {}, root=Matrix.Scale(SCALE, 4))
    bake_scale([obj])
    export_glb([obj], os.path.join(OUT, fname))
    print(f"MODELL {label}: {tris} Dreiecke, {len(cols)} Farben, Textur {TEX}x{TEX}")
    return tris


# --- Blatt -------------------------------------------------------------------------------------------------------------------

def load2(path, tag):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in objs:
        image = next(n.image for n in o.active_material.node_tree.nodes if n.type == "TEX_IMAGE")
        o.data.materials.clear()
        o.data.materials.append(pass_material(f"{tag}_{o.name}", image))
        o["smooth"] = not o.name.startswith(("club", "sword", "axe"))
        o["tag"] = tag
        HOME[o.name] = o.matrix_world.copy()
    return objs


def extents(mask):
    rows = np.where(mask.max(axis=1) > 0.3)[0]
    cols = np.where(mask.max(axis=0) > 0.3)[0]
    return (int(rows.min()), int(rows.max()), int(cols.min()), int(cols.max())) if len(rows) else None


HEAD_C = V(0.02, 0.0, 0.31)


def blatt2(tris):
    stone_dir = os.path.join(tempfile.gettempdir(), "axt2_stein")
    os.makedirs(stone_dir, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    axes = {"r1": load2(os.path.join(R1, "axt-c-pfahlaxt.glb"), "r1")}
    for k, _l, f, _v, _g in FASSUNGEN:
        axes[k] = load2(os.path.join(OUT, f), k)
    club = load2(os.path.join(MODELS, "club.glb"), "club")
    club_hand = load2(os.path.join(MODELS, "club_hand.glb"), "clubh")
    cam = camera()
    room = build_room(stone_textures(stone_dir))
    keys = ["r1"] + [f[0] for f in FASSUNGEN]
    tiles, wide, ego, masks, full = {}, {}, {}, {}, {}
    side_loc = G(CENTRE + V(0, 1, 0) * 1.5)
    quarter_loc = G(CENTRE + V(-0.8, 0.40, -0.30).normalized() * 1.5)
    close_loc = G(HEAD_C + V(-0.30, 1.0, -0.12).normalized() * 0.78)
    for k in keys:
        place(axes[k], Matrix())
        tiles[(k, "seite")] = up2(render_tile(axes[k], cam, side_loc, G(CENTRE), 33, (TILE, TILE), "ansicht", UPG), UPSCALE)
        tiles[(k, "schraeg")] = up2(render_tile(axes[k], cam, quarter_loc, G(CENTRE), 33, (TILE, TILE), "ansicht", UPG), UPSCALE)
        tiles[(k, "kopf")] = up2(render_tile(axes[k], cam, close_loc, G(HEAD_C), 33, (TILE, TILE), "ansicht", UPG), UPSCALE)
        place(axes[k], Matrix.Translation(G(V(-0.07, 0, 0))))
        place(club, Matrix.Translation(G(V(0.40, 0, 0))))
        wide[k] = up2(render_tile(axes[k] + club, cam, G(V(0.17, 1.6, 0.30)), G(V(0.17, 0, 0.30)), 42, (TILE, TILE), "ansicht", UPG), UPSCALE)
    crop = (80, 480, 100, 500)
    shots = [(k, axes[k]) for k in keys] + [("club", club)]
    for k, w in shots:
        keep = list(room) + w
        place(w, held_matrix())
        hands = club_hand
        place(hands, hand_matrix())
        keep += hands
        shot = render_tile(keep, cam, G(EYE), G(EYE + V(0, 0, 1)), 70, (854, 480), "spiel")
        full[k] = shot
        ego[k] = shot[crop[0]:crop[1], crop[2]:crop[3]]
        masks[k] = render_mask(list(w), cam, G(EYE), G(EYE + V(0, 0, 1)), 70, (854, 480))[crop[0]:crop[1], crop[2]:crop[3]]
        print("EXTENT", k, extents(masks[k]))
    return tiles, wide, ego, masks, full


def main():
    tris = {}
    for key, label, fname, variant, gold in FASSUNGEN:
        tris[key] = export_variant(key, label, fname, variant, gold)
    tiles, wide, ego, masks, full = blatt2(tris)
    gap, label_h, tw = 8, 26, TILE * UPSCALE
    cols, rows = 5, 6
    sheet = np.zeros((label_h + rows * (tw + label_h + gap), cols * (tw + gap), 3)) + FOG_COLOR
    text(sheet, "Silver & Ash - Hunter's Axe (Issue 201), Runde 2: C ohne gelbes Feld, drei Texturen, kuerzerer Stiel, kraeftiger Haken",
         6, 2, 16)

    def put(r, c, img, title, span=1):
        x0, y0 = c * (tw + gap), label_h + r * (tw + label_h + gap)
        h, w = img.shape[:2]
        sheet[y0 + label_h:y0 + label_h + min(h, tw), x0:x0 + min(w, span * tw + (span - 1) * gap)] = \
            img[:tw, :span * tw + (span - 1) * gap]
        text(sheet, title, x0 + 4, y0, 16)

    def pad_ego(e):
        pad = np.zeros((tw, tw, 3)) + FOG_COLOR
        pad[:min(tw, e.shape[0]), :min(tw, e.shape[1])] = e[:tw, :tw]
        return pad

    names = {"r1": "C Runde 1 (Vergleich)"}
    names.update({f[0]: f[1] for f in FASSUNGEN})
    for r, k in enumerate(["r1"] + [f[0] for f in FASSUNGEN]):
        lab = names[k]
        t = f" ({tris[k]} Dr.)" if k in tris else " (556 Dr.)"
        put(r, 0, tiles[(k, "seite")], f"{lab}{t}: Seite")
        put(r, 1, tiles[(k, "schraeg")], f"{lab}: Dreiviertel")
        put(r, 2, tiles[(k, "kopf")], f"{lab}: Kopf nah")
        put(r, 3, pad_ego(ego[k]), f"{lab}: Ich-Sicht (Ausschnitt)")
        put(r, 4, wide[k], f"{lab} (rechts) neben der Keule")
    # letzte Zeile: Texturen und Silhouetten
    last = 5
    for c, f in enumerate(FASSUNGEN):
        tex = TEXS[f[0]]
        big = np.repeat(np.repeat(tex, 3, axis=0), 3, axis=1)        # 3-fach, harte Texel
        tx = np.zeros((tw, tw, 3)) + FOG_COLOR
        tx[:big.shape[0], :big.shape[1]] = big
        put(last, c, tx, f"Textur {f[1]} ({COLOR_COUNT[f[0]]} Farben, 128x128, 3-fach)")
    sil = np.zeros((tw, tw, 3)) + FOG_COLOR
    for i, (k, lab) in enumerate((("r1", "C alt"), ("c1", "C neu"), ("club", "Keule"))):
        m = masks[k]
        h2, w2 = m.shape[0] // 4, m.shape[1] // 4
        small = m[:h2 * 4, :w2 * 4].reshape(h2, 4, w2, 4).mean(axis=(1, 3))[..., None]
        thumb = np.where(small > 0.3, 235.0, 0.0)
        x0 = 4 + i * (w2 + 6)
        sil[40:40 + h2, x0:x0 + w2] = thumb
        text(sil, lab, x0, 8, 16)
    put(last, 4, sil, "Silhouetten in der Ich-Sicht (Viertel)")
    save_png(sheet[::-1], os.path.join(OUT, "blatt.png"))
    for k in ("c1", "c2", "c3", "c1e"):
        save_png(full[k][::-1], os.path.join(OUT, f"ich-{k}-854.png"))


if __name__ == "__main__":
    main()
