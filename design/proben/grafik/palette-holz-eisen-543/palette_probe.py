"""Probestueck zu Issue #543: neutrales Eisen und gedaempftes Holz als Ergaenzung der Hauptpalette (Vorschlag).

Nur ausserhalb des Spiels: nichts unter assets/ wird geaendert, die Palette wird nur gelesen. Alles aus diesem einen Skript:
die Werte der drei Reihen (unten als Konstanten), die umgerechneten Tuer-Atlanten, alle Bilder, das Uebersichtsblatt und
werte.txt. Gebaut auf der Pipeline tools/models/psx.py und dem Fallgitter-Probestueck aus Issue #540 (Branch
feat/fallgitter-probe-540; fallgitter_quelle.py ist dessen Skript, byte-gleich, wird hier nur geladen und ohne eigenen Lauf
benutzt: Gitter, Wand, Rahmen, Kamera, Beschriftung).

Aufruf (aus dem Wurzelverzeichnis, TEMP/TMP vorher auf einen eigenen Ordner setzen):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/palette-holz-eisen-543/palette_probe.py
Umgebungsvariable P543_OUT: anderer Ausgabeordner (fuer den Zwei-Laeufe-Vergleich). P543_QUICK=1: nur eine Tuer, kein Blatt.
Kein Zufall: zwei Laeufe liefern bitgleiche Dateien.
"""
import colorsys
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
OUT = os.environ.get("P543_OUT", HERE)
QUICK = os.environ.get("P543_QUICK") == "1"
os.makedirs(OUT, exist_ok=True)

import numpy as np  # noqa: E402

# --- Fallgitter-Skript laden (ohne den Aufruf main() am Ende) ----------------------------------------------------------
with open(os.path.join(HERE, "fallgitter_quelle.py"), encoding="utf-8") as f:
    _src = f.read().rstrip()
assert _src.endswith("\nmain()"), "fallgitter_quelle.py endet nicht auf main()"
NS = {"__file__": os.path.join(HERE, "palette_probe.py"), "__name__": "fallgitter_quelle"}
exec(compile(_src[:-len("main()")], "fallgitter_quelle.py", "exec"), NS)
assert os.path.normpath(NS["ROOT"]) == os.path.normpath(ROOT)

import bpy  # noqa: E402
from mathutils import Matrix  # noqa: E402

sys.path.insert(0, os.path.join(ROOT, "tools", "palette"))
import snap as palette_snap  # noqa: E402

V, G, save_image, pass_material = NS["V"], NS["G"], NS["save_image"], NS["pass_material"]
text, titled, hcat, vcat, png, half = NS["text"], NS["titled"], NS["hcat"], NS["vcat"], NS["png"], NS["half"]
MODELS, TMP, BG, EYE = NS["MODELS"], NS["TMP"], NS["BG"], NS["EYE"]

# --- Die Vorschlaege (je vier Stufen, dunkel nach hell) -----------------------------------------------------------------
# Eisen, neutral: Helligkeit 30, 50, 74, 104 (Abstaende 20, 24, 30, wie die Stufen 1 bis 4 des Eisens von heute: 20 bis 26),
# R = G, B um 3 Prozent hoeher (so sah es der Mensch im Vergleichsbild von #540: "Neutrales Grau").
EISEN_NEU = [(30, 30, 31), (50, 50, 52), (74, 74, 76), (104, 104, 107)]
# Holz A, graubraun: Farbton 28 Grad, Saettigung 0,19 bis 0,20, Helligkeit 34, 58, 84, 112 (Abstaende 24, 26, 28).
HOLZ_A = [(37, 33, 30), (63, 57, 51), (92, 82, 74), (123, 109, 99)]
# Holz B, etwas waermer: Farbton 26 Grad, Saettigung 0,30 bis 0,31, gleiche Helligkeiten.
HOLZ_B = [(39, 33, 27), (67, 56, 47), (97, 80, 68), (131, 106, 91)]

LUM = lambda c: 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]  # noqa: E731


def sat(c):
    return (max(c) - min(c)) / max(max(c), 1)


def hexa(c):
    return "#%02X%02X%02X" % tuple(int(x) for x in c)


ROWS = palette_snap.rows()                                   # Hauptpalette, nur gelesen
STEIN, MESSING = ROWS["Stein"], ROWS["Messing/Gold"]
EISEN_ALT, HOLZ_ALT = ROWS["Eisen"], ROWS["Holz"]
NEBEL = STEIN[0]                                             # Stufe 0 des Steins: die dunkelste Fuge

# --- Tuer-Atlanten umrechnen -------------------------------------------------------------------------------------------
# Atlas 128 x 128, Feld [y, x], Zeile 0 unten. Rechtecke wie tools/models/door.py (RECTS); nur Holz und Eisen werden umgerechnet.
RECTS = {"planks": (0, 0, 40, 128), "plate": (40, 0, 72, 32), "iron": (72, 0, 104, 32), "edge": (104, 0, 128, 32)}
# Stoff der Bretter und Kanten je Tuer (Eisentuer: dunkles Eisen statt Holz; Boss: sehr dunkles Holz); Platte und Band: Eisen
BOARDS = {"normal": "holz", "locked": "eisen", "boss": "holz"}
KINDS = ("normal", "locked", "boss")


def load_atlas(kind):
    im = bpy.data.images.load(os.path.join(MODELS, "door_%s.png" % kind))
    w, h = im.size
    a = np.round(np.array(im.pixels[:]).reshape(h, w, 4)[..., :3] * 255).astype(int)
    bpy.data.images.remove(im)
    return a


def used_mask(shape):
    m = np.zeros(shape[:2], bool)
    for x0, y0, x1, y1 in RECTS.values():
        m[y0:y1, x0:x1] = True
    return m


def recolour(kind, atlas, holz, eisen):
    """Planken und Kanten auf die Reihe des Stoffs, Platte und Band auf Eisen; Schloss, Glas und Dunkelfeld bleiben (nicht
    Teil dieser Frage). Erlaubt je Rechteck: die Reihe plus Stein Stufe 0 (die dunkle Fuge)."""
    out = atlas.copy()
    stuff = {"holz": np.array(holz), "eisen": np.array(eisen)}
    for name, (x0, y0, x1, y1) in RECTS.items():
        row = stuff[BOARDS[kind]] if name in ("planks", "edge") else stuff["eisen"]
        allowed = np.concatenate([row, NEBEL[None]])
        out[y0:y1, x0:x1] = palette_snap.snap(atlas[y0:y1, x0:x1], allowed).astype(int)
    return out


def check_layout(kind, a):
    """Rechtecke wie erwartet belegt: Bretter, Beschlagfelder, Schloss/Glas/Dunkel unten; der Rest schwarz."""
    black = (a == 0).all(axis=2)
    assert not black[0:128, 0:40].any() and not black[0:32, 40:128].any() and not black[32:64, 40:128].any(), kind
    assert black[64:128, 40:128].all(), kind


def deviation(a, b):
    m = used_mask(a.shape)
    d = np.abs(a - b)[m]
    return d.mean(axis=0), (b[m] - a[m]).mean(axis=0)


def variants():
    """{Name: {Tuerart: Atlas}} fuer heute, A und B (jeder Atlas als Feld 128 x 128 x 3)."""
    base = {k: load_atlas(k) for k in KINDS}
    for k in KINDS:
        check_layout(k, base[k])
    return {"heute": base,
            "A": {k: recolour(k, base[k], HOLZ_A, EISEN_NEU) for k in KINDS},
            "B": {k: recolour(k, base[k], HOLZ_B, EISEN_NEU) for k in KINDS},
            "alt": {k: recolour(k, base[k], HOLZ_ALT, EISEN_ALT) for k in KINDS}}


def check_palette_rows(var):
    """Jede umgerechnete Farbe der Rechtecke liegt in den Vorschlagsreihen oder ist Stein Stufe 0."""
    m = used_mask((128, 128))
    for name, rows in (("A", HOLZ_A), ("B", HOLZ_B)):
        ok = {tuple(c) for c in rows} | {tuple(c) for c in EISEN_NEU} | {tuple(NEBEL)}
        for k in KINDS:
            got = {tuple(int(x) for x in p) for p in var[name][k][m]}
            # Schloss, Glas, Dunkelfeld liegen ausserhalb der vier Rechtecke und zaehlen hier nicht
            got = {tuple(int(x) for x in p) for k2 in [k] for p in _inside(var[name][k2])}
            assert got <= ok, (name, k, sorted(got - ok))


def _inside(a):
    out = []
    for x0, y0, x1, y1 in RECTS.values():
        out += [tuple(p) for p in a[y0:y1, x0:x1].reshape(-1, 3)]
    return out


# --- Szenen ------------------------------------------------------------------------------------------------------------
STATE = {"leaf": None, "name": ""}
_add_frame = NS["add_door"]


def add_door_and_leaf(coll, frame):
    """Rahmen wie im Fallgitter-Skript, dazu der Tuerfluegel aus assets/models/door_<art>.glb (nur gelesen) mit dem
    umgerechneten Atlas; gleiche Verschiebung wie der Rahmen (Tuerebene bei z = -1)."""
    objs = _add_frame(coll, frame)
    leaf = STATE["leaf"]
    if leaf is None:
        return objs
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS, "door_%s.glb" % frame))
    new = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    image = save_image("leaf_%s_%s" % (frame, STATE["name"]), leaf[frame].astype(float),
                       os.path.join(TMP, "leaf_%s_%s.png" % (frame, STATE["name"])))
    shift = Matrix.Translation(G(V(0, 0, -1)))
    for o in new:
        mw = o.matrix_world.copy()
        o.data.materials.clear()
        o.data.materials.append(pass_material("l_" + o.name, image))
        o.parent = None
        o.matrix_world = shift @ mw
        coll.objects.link(o)
        for c in list(o.users_collection):
            if c is not coll:
                c.objects.unlink(o)
    return objs + new


NS["add_door"] = add_door_and_leaf


def iron_rows_neu(which):
    if which == "eisen":
        return EISEN_ALT
    pad = np.zeros((2, 3), int)                              # Gitter liest die Stufen 2 bis 5 der Reihe
    return np.concatenate([pad, np.array(EISEN_NEU)])


NS["iron_rows"] = iron_rows_neu


def door_items(kind):
    items = NS["world"](frame=kind, gitter=False)
    return [it for it in items if it[0] != "fuehrung"]        # Sturzring gehoert zum Gitter, nicht zur Tuer


def door_passes(kind, leaf, name, view, size):
    STATE["leaf"], STATE["name"] = leaf, name
    if view == "frontal":
        loc, fov = (0.0, 1.75, 2.7), 60
    else:
        loc, fov = (1.4, 1.6, 2.1), 60
    return NS["render"](door_items(kind), loc, (0, 1.75 if view == "frontal" else 1.6, -1), fov, size)


def gitter_image(which, view, dist, mode, size=NS["SPIEL"]):
    STATE["leaf"] = None
    items = NS["world"](lift=0.0, iron=which)
    return NS["shot"](items, NS["cam_pos"](view, dist), (0, EYE, -1), 70, size, mode)


def gitter_single(which, size=(360, 640)):
    STATE["leaf"] = None
    out = []
    cy, scale = NS["HEIGHT"] / 2, 4.0
    items = [("floor",), ("gitter", "breit", 0.0, which)]
    shots = {"vorn": ((0, cy, 8), (0, cy, -1), 0, scale), "seite": ((8, cy, -1), (0, cy, -1), 0, scale),
             "hinten": ((0, cy, -10), (0, cy, -1), 0, scale), "oben": ((2.6, 4.8, 2.4), (0, 1.6, -1), 38, None)}
    for lbl, (loc, tgt, fov, orth) in shots.items():
        out.append(titled(NS["shot"](items, loc, tgt, fov, size, "hell", ortho=orth), lbl))
    return hcat(out)


# --- Blatt-Bausteine ---------------------------------------------------------------------------------------------------

def fill(im, x, y, w, h, colour):
    im[y:y + h, x:x + w] = np.array(colour, float)


def swatches(im, x, y, colours, w, h, labels=True, size=18, gap=6, lum=False):
    for i, c in enumerate(colours):
        fill(im, x + i * (w + gap), y, w, h, c)
        if labels:
            text(im, hexa(c), x + i * (w + gap), y + h + 2, size)
            if lum:
                text(im, "H %d  S %.2f" % (round(LUM(c)), sat(c)), x + i * (w + gap), y + h + 2 + int(size * 1.5), size - 4, (190, 185, 200))


def stein_rows_assumed():
    """Annahme fuer die spaeteren Ebenen: der Stein in kuehlem Blaugrau und in warmem Gelbgrau, gleiche Helligkeit und Saettigung
    wie die Katakomben, nur der Farbton verschoben. Nur zum Ansehen, keine Palette."""
    out = {}
    for name, hue in (("kuehl", 215), ("warm", 42)):
        rows = []
        for c in STEIN:
            h, l, s = colorsys.rgb_to_hls(*[v / 255 for v in c])
            rows.append(tuple(round(v * 255) for v in colorsys.hls_to_rgb(hue / 360, l, max(s, 0.10))))
        out[name] = rows
    return out


def swatch_section(W, H0):
    """Farbfelder: links die drei Vorschlagsreihen gross, rechts zum Vergleich Stein, Eisen und Holz von heute."""
    h = 880
    im = np.tile(BG, (h, W, 1))
    text(im, "Farbfelder: dunkel nach hell, Stufe 0 bis 3 (Hex, H = Helligkeit 0 bis 255, S = Saettigung)", 30, 10, 26)
    x0 = 30
    rows = (("Eisen NEU, neutral (fuer Umgebungsteile)", EISEN_NEU), ("Holz A, graubraun (naeher am Stein)", HOLZ_A),
            ("Holz B, etwas waermer", HOLZ_B))
    for i, (lbl, cols) in enumerate(rows):
        y = 70 + i * 230
        text(im, lbl, x0, y, 24)
        swatches(im, x0, y + 40, cols, 150, 100, size=20, lum=True)
    # rechts: Vergleichsreihen
    xr = 900
    text(im, "Zum Vergleich: die Hauptpalette heute", xr, 70, 24)
    text(im, "Stein Katakomben (15 Stufen)", xr, 110, 20)
    swatches(im, xr, 138, [tuple(c) for c in STEIN], 66, 56, labels=False, gap=4)
    text(im, "Eisen heute (bläulich; bleibt für Waffen und HUD)", xr, 210, 20)
    swatches(im, xr, 238, [tuple(c) for c in EISEN_ALT], 116, 56, labels=False, gap=4)
    text(im, "Holz heute (satt)", xr, 310, 20)
    swatches(im, xr, 338, [tuple(c) for c in HOLZ_ALT], 116, 56, labels=False, gap=4)
    text(im, "Eisen NEU neben dem Stein dreier Ebenen (Stufen 3, 5, 7, 9, 11, 13 des Steins, dann die vier Eisen-Stufen)", xr, 420, 20)
    text(im, "Katakomben (heute) | Kerker (Annahme: kühl) | Schlosshalle (Annahme: warm). Nur zum Ansehen, keine Palette.", xr, 448, 20,
         (190, 185, 200))
    assumed = stein_rows_assumed()
    for j, (lbl, rows_) in enumerate((("Katakomben", [tuple(c) for c in STEIN]), ("Kerker", assumed["kuehl"]), ("Halle", assumed["warm"]))):
        y = 490 + j * 90
        sel = [rows_[k] for k in (3, 5, 7, 9, 11, 13)]
        text(im, lbl, xr, y + 14, 20)
        swatches(im, xr + 150, y, sel, 70, 70, labels=False, gap=4)
        swatches(im, xr + 150 + 6 * 74 + 30, y, EISEN_NEU, 70, 70, labels=False, gap=4)
    text(im, "Holz A und B neben dem Stein (Stufen 5 und 9 des Steins, dann vier Holz-Stufen)", xr, 770, 20)
    for j, (lbl, cols) in enumerate((("A", HOLZ_A), ("B", HOLZ_B))):
        y = 800 + j * 0
        sel = [tuple(STEIN[k]) for k in (5, 9)]
        xx = xr + j * 500
        text(im, lbl, xx, y, 20)
        swatches(im, xx + 30, y + 4, sel + list(cols), 70, 56, labels=False, gap=4)
    return im


# --- Hauptlauf ---------------------------------------------------------------------------------------------------------

def main():
    var = variants()
    check_palette_rows(var)
    kinds = ("normal",) if QUICK else KINDS

    lines = ["Werte (Hauptpalette-Vorschlag zu #543), erzeugt von palette_probe.py", ""]
    for name, rows_ in (("Eisen NEU", EISEN_NEU), ("Holz A", HOLZ_A), ("Holz B", HOLZ_B)):
        lines.append("%s: %s" % (name, "  ".join("%s=%s H%d S%.2f" % (hexa(c), tuple(c), round(LUM(c)), sat(c)) for c in rows_)))
        lines.append("  Helligkeit: %s, Abstaende %s" % ([round(LUM(c)) for c in rows_], [round(LUM(b) - LUM(a)) for a, b in zip(rows_, rows_[1:])]))
    lines.append("Zum Vergleich Eisen heute (Stufen 1 bis 7) Abstaende: %s" % [round(LUM(b) - LUM(a)) for a, b in zip(EISEN_ALT[1:], EISEN_ALT[2:])])
    lines.append("Zum Vergleich Holz heute Abstaende: %s, Saettigung Stufen 2 bis 5: %s" % (
        [round(LUM(b) - LUM(a)) for a, b in zip(HOLZ_ALT, HOLZ_ALT[1:])], [float(round(sat(c), 2)) for c in HOLZ_ALT[2:6]]))
    wall = NS["fuehrung_atlas"]("a")
    lines.append("Wand-Kachel A (Stein): mittlere Helligkeit %.1f, Stein-Stufen 4 bis 10: %s" % (
        LUM(wall.reshape(-1, 3).mean(0)), [round(LUM(c)) for c in STEIN[4:11]]))
    lines += ["", "Mittlere Abweichung gegen die heutige Tuer-Textur, je Kanal R/G/B (Betrag, ueber die Felder Bretter, Platte, Band, Kante;",
              "Schloss, Glas und Dunkelfeld bleiben unveraendert und zaehlen nicht), in Klammern die mittlere Verschiebung mit Vorzeichen:"]
    for k in KINDS:
        for v, lbl in (("alt", "heutige Reihen (Referenz)"), ("A", "Holz A + Eisen NEU"), ("B", "Holz B + Eisen NEU")):
            ab, sg = deviation(var["heute"][k], var[v][k])
            lines.append("  door_%-6s %-26s %5.1f %5.1f %5.1f   (%+.1f %+.1f %+.1f)" % ((k, lbl) + tuple(ab) + tuple(sg)))
    lines.append("")
    lines.append("Farben je umgerechnetem Atlas (nur die vier Felder): " + "; ".join(
        "%s/%s %d" % (v, k, len(set(_inside(var[v][k])))) for v in ("A", "B") for k in KINDS))
    with open(os.path.join(OUT, "werte.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join(lines))

    with open(os.path.join(OUT, "vorschlag.json"), "w", encoding="utf-8") as f:
        json.dump({"hinweis": "Vorschlag zu Issue #543, nicht Teil der Hauptpalette; je vier Stufen, dunkel nach hell",
                   "eisen_neutral": [list(c) for c in EISEN_NEU], "holz_a_graubraun": [list(c) for c in HOLZ_A],
                   "holz_b_waermer": [list(c) for c in HOLZ_B]}, f, indent=1)
        f.write("\n")

    # Bildpaare: je Tuer drei Zeilen (hell frontal, Spiel-Licht frontal, Spiel-Licht schraeg), drei Spalten (heute, A, B)
    names = (("heute", "heute"), ("A", "A: Holz graubraun"), ("B", "B: Holz wärmer"))
    cache = {}
    for k in kinds:
        rows_img = []
        for view, mode, lbl in (("frontal", "hell", "hell"), ("frontal", "spiel", "Spiel-Licht"), ("schraeg", "spiel", "Spiel-Licht, schräg")):
            tiles = []
            for v, vl in names:
                p = cache.setdefault((k, v, view), door_passes(k, var[v], v, view, (480, 480)))
                tiles.append(titled(NS["to_image"](p, mode), "Tür %s, %s, %s" % (k, vl, lbl), 18))
            rows_img.append(hcat(tiles))
        png(vcat(rows_img), os.path.join(OUT, "paar-%s.png" % k))

    # Fallgitter in der neuen Eisen-Reihe
    if not QUICK:
        rows_img = []
        for view in ("frontal", "schraeg"):
            for dist in (3.0, 8.0):
                tiles = []
                for mode in ("hell", "spiel"):
                    for which, lbl in (("eisen", "Eisen heute"), ("neu", "Eisen NEU")):
                        tiles.append(titled(half(gitter_image(which, view, dist, mode)), "%s, %s, %d m, %s" % (
                            lbl, view, int(dist), "hell" if mode == "hell" else "Spiel-Licht"), 14))
                rows_img.append(hcat(tiles))
        png(vcat(rows_img), os.path.join(OUT, "fallgitter-vergleich.png"))
        png(gitter_single("neu"), os.path.join(OUT, "fallgitter-einzel-neu.png"))
        # Blatt
        make_sheet(var, cache)


def make_sheet(var, cache):
    W = 2950
    head = np.tile(BG, (150, W, 1))
    text(head, "Silver & Ash, Issue #543: neutrales Eisen und gedämpftes Holz für Umgebungsteile (Vorschlag zur Wahl)", 30, 10, 38)
    text(head, "Entschieden: Eisen an Umgebungsteilen ist neutrales Grau (Fallgitter, Türbeschläge); das bläuliche Eisen bleibt für Waffen und HUD.", 30, 66, 24)
    text(head, "Zu wählen: Holz A (graubraun, näher am Stein) oder Holz B (etwas wärmer). Unten die drei Türflügel: heute, mit A, mit B. Die Palette ist noch nicht geändert.", 30, 98, 24)
    parts = [head, swatch_section(W, 0)]
    # Tueren: je Tuer eine Zeile, links hell (heute, A, B), rechts Spiel-Licht
    tile_w = (W - 6 * 8) // 6
    door_rows = []
    for k in KINDS:
        tiles = []
        for mode, lbl in (("hell", "hell"), ("spiel", "Spiel-Licht")):
            for v, vl in (("heute", "heute"), ("A", "A, graubraun"), ("B", "B, wärmer")):
                p = cache[(k, v, "frontal")]
                im = NS["to_image"](p, mode)
                im = im.reshape(240, 2, 240, 2, 3).mean(axis=(1, 3)) if False else im
                tiles.append(titled(im, "%s, %s, %s" % (k, vl, lbl), 20))
        door_rows.append(hcat(tiles, gap=8))
    doors = vcat(door_rows, gap=10)
    pad = W - doors.shape[1]
    if pad > 0:
        doors = np.concatenate([doors, np.tile(BG, (doors.shape[0], pad, 1))], axis=1)
    title = np.tile(BG, (50, W, 1))
    text(title, "Türflügel auf Wand der Katakomben: Normal (Holz), Locked (Eisen), Boss (dunkles Holz). Links hell, rechts im Spiel-Licht.", 30, 8, 26)
    parts += [title, doors]
    # Fallgitter: schraeg 3 m, heute / neu, hell und Spiel-Licht
    gt = np.tile(BG, (50, W, 1))
    text(gt, "Fallgitter aus #540 (Gitter geschlossen, Rahmen Normal): Eisen heute gegen Eisen NEU, schräg aus 3 m", 30, 8, 26)
    tiles = []
    for mode in ("hell", "spiel"):
        for which, lbl in (("eisen", "Eisen heute"), ("neu", "Eisen NEU")):
            tiles.append(titled(gitter_image(which, "schraeg", 3.0, mode, (586, 330)), "%s, %s" % (lbl, "hell" if mode == "hell" else "Spiel-Licht"), 20))
    gitter_row = hcat(tiles, gap=8)
    pad = W - gitter_row.shape[1]
    if pad > 0:
        gitter_row = np.concatenate([gitter_row, np.tile(BG, (gitter_row.shape[0], pad, 1))], axis=1)
    parts += [gt, gitter_row]
    # Auf gleiche Breite bringen und stapeln
    parts = [p if p.shape[1] == W else np.concatenate([p, np.tile(BG, (p.shape[0], W - p.shape[1], 1))], 1) for p in parts]
    png(np.concatenate(parts, axis=0), os.path.join(OUT, "blatt-543.png"))


main()
