"""Throwing Knives (Issue #204), Runde 5: gestaffelte Messer und tiefer im Aermel. Gemeinsame Teile.

Urteil des Auftraggebers zu Runde 4b (2026-11-28): "Messer Staffeln, ist wichtig für das Drop modell. außerdem könnten
sie noch 10cm weiter in den Ärmel ragen. Muss dann natürlich schauen, dass kein sichtbares clipping vorhanden ist."

Dieses Modul stellt die Szene (Gang, Aermel, Silver Blade, drei Messer mit kurzem Band) und die Messungen:

- put():       Haltung mit Tiefe, Drehpunkt, Versatz, Faecherwinkel und Staffelung (je Messer ein Versatz entlang der
               eigenen Dicke und entlang der eigenen Achse).
- pierce():    Regel 30, jedes Dreieck der Messer gegen jedes Dreieck des Aermels ausser dem Deckel der Manschette.
- crossing():  Dreieckspaare, in denen sich zwei Messer gegenseitig durchdringen.
- seen():      "sichtbares clipping": Punkte der Messer hinter der Ebene des Deckels, die das Auge trotzdem sieht
               (Strahl vom Auge, der vor dem Punkt keine Aermelflaeche trifft) und die im Bild 854 x 480 liegen.
- overlap():   Ueberdeckung im Bild der Ich-Sicht wie in ../runde-4/wicklung_abstand.py (Masken je Messer), dazu je
               Wicklung die Bildpunkte, die frei sind, und die, die sich mit einem anderen Messer ueberdecken.

Masse: Skriptmass (Datei und Ich-Sicht 1,8-fach). Tiefe = Abstand Klingenansatz bis Haltepunkt des Aermels entlang der
Messerachse; kleiner heisst tiefer im Aermel, negativ heisst: auch der Klingenansatz steckt im Aermel.
"""

import os
import sys
import tempfile

import numpy as np

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
PROBE = os.path.dirname(HERE)
sys.path.insert(0, PROBE)
sys.path.insert(0, os.path.join(PROBE, "runde-4"))
import blatt as B  # noqa: E402
import versatz as R  # noqa: E402
from messer_gemeinsam import MODELS, SCALE, G, Matrix, V, Vector, bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402
from psx import FOG_COLOR, MATS, save_png  # noqa: E402,F401

KNIFE = os.path.join(PROBE, "runde-4")           # messer-e-kurzband.glb (196 Dreiecke), unveraendert
PITCH, ROLL = 44.0, 6.0                          # Neigung und Kippen: seit Runde 2 unveraendert
BLADE = 0.117                                    # Klingenlaenge ab Ansatz
WRAP = (-0.054, -0.012)                          # Wicklung, laengs ab Klingenansatz (zur Spitze positiv)
NAMES = ("rechts", "Mitte", "links")             # Reihenfolge der Messer im Bild


class Scene:
    pass


def setup(extra=()):
    """Leere Szene, Gang, Aermel, Silver Blade, drei Messer; extra = weitere Modelle aus assets/models (nur gelesen)."""
    s = Scene()
    stone = os.path.join(tempfile.gettempdir(), "messer_stein")
    os.makedirs(stone, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    B.HOME.clear()
    B.POSES = {"e": (PITCH, ROLL, (0.0, 0.0, 0.0))}
    s.knives = B.copies(B.load("messer-e-kurzband.glb", "ek", KNIFE), 3)
    s.wraps = [wrap_only(g) for g in s.knives]
    s.sleeve = B.load("pistol_hand.glb", "pistolh", MODELS, smooth=True)
    s.blade = B.load("blade.glb", "blade", MODELS, smooth=True) + B.load("blade_hand.glb", "bladeh", MODELS, smooth=True)
    s.extra = {name: B.load(name, name.split(".")[0], MODELS, smooth=True) for name in extra}
    s.cam = B.camera()
    s.room = B.build_room(B.stone_textures(stone))
    B.place(s.sleeve, B.at(B.HAND))
    B.place(s.blade, B.at(B.LEFT) @ B.rot(-20, "Y"))
    for objs in s.extra.values():
        B.place(objs, B.AWAY)
    s.hand = B.EYE + V(*B.HAND)
    s.walls, s.all_walls, lid_points = [], [], []
    for o in s.sleeve:
        lid = R.lid_of(o, G(s.hand)) if o.name.startswith("arm_cuff") else set()
        vs, ts = R.tris(o)
        lid_points += [vs[i] for t in lid for i in ts[t]]
        s.walls.append((BVHTree.FromPolygons(vs, ts), lid))
        s.all_walls.append(BVHTree.FromPolygons(vs, ts))
    s.lid = sum(lid_points, Vector()) / len(lid_points)
    s.empty = np.array(B.player(list(s.room), s.cam))
    s.arm = diff(np.array(B.player(list(s.room) + s.sleeve, s.cam)), s.empty)
    return s


def diff(img, empty):
    return np.abs(img - empty).sum(2) > 1e-4


def wrap_only(group):
    """Je Messer ein zweites Netz, das nur die Flaechen der Wicklung enthaelt (fuer die Maske im Bild)."""
    out = []
    for o in group:
        mesh = o.data.copy()
        bm = bmesh.new()
        bm.from_mesh(mesh)
        gone = [f for f in bm.faces if not WRAP[0] <= -f.calc_center_median().y / SCALE <= WRAP[1]]
        bmesh.ops.delete(bm, geom=gone, context="FACES")
        bm.to_mesh(mesh)
        bm.free()
        c = bpy.data.objects.new(o.name + "_wicklung", mesh)
        c["smooth"] = o["smooth"]
        bpy.context.scene.collection.objects.link(c)
        B.HOME[c.name] = B.HOME[o.name].copy()
        out.append(c)
    return out


def cut_mesh(mesh, keep_from):
    """Kopie des Netzes ohne alles, was laengs hinter keep_from liegt (m Skriptmass ab Klingenansatz); Schnitt verschlossen."""
    out = mesh.copy()
    bm = bmesh.new()
    bm.from_mesh(out)
    geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
    res = bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=Vector((0, -keep_from * SCALE, 0)),
                                 plane_no=Vector((0, 1, 0)), clear_outer=True)
    edges = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
    if edges:
        bmesh.ops.triangle_fill(bm, edges=edges, use_beauty=True)
    bm.to_mesh(out)
    bm.free()
    return out


def use_cut(s, keep_from=None):
    """Handmodell: alle drei Messer hinter keep_from abgeschnitten (None = ganzes Messer). Gibt die Dreieckszahl zurueck."""
    if not hasattr(s, "full"):
        s.full = [o.data for o in s.knives[0]]
        s.full_wrap = [o.data for o in s.wraps[0]]
        s.cuts = {}
    if keep_from not in s.cuts:
        if keep_from is None:
            s.cuts[None] = (s.full, s.full_wrap)
        else:
            s.cuts[keep_from] = ([cut_mesh(m, keep_from) for m in s.full], [cut_mesh(m, keep_from) for m in s.full_wrap])
    whole, wrap = s.cuts[keep_from]
    for group, wgroup in zip(s.knives, s.wraps):
        for o, m in zip(group, whole):
            o.data = m
        for o, m in zip(wgroup, wrap):
            o.data = m
    count = 0
    for m in whole:
        m.calc_loop_triangles()
        count += len(m.loop_triangles)
    return count


def put(s, depth, pivot, dx, fan=17.0, thick=(0.0, 0.0, 0.0), along=(0.0, 0.0, 0.0)):
    """Haltung: Tiefe, Drehpunkt hinter dem Klingenansatz, Versatz nach links, Faecher +-fan Grad; je Messer (rechts,
    Mitte, links) ein Versatz entlang der eigenen Dicke (thick) und entlang der eigenen Achse zur Spitze (along)."""
    B.GRIP = depth * SCALE
    B.FAN_PIVOT = pivot * SCALE
    for group, wrap, angle, t, a in zip(s.knives, s.wraps, (-fan, 0.0, fan), thick, along):
        m = (Matrix.Translation(G(V(dx * SCALE, 0, 0))) @ B.held("e", angle)
             @ Matrix.Translation(Vector((t * SCALE, -a * SCALE, 0))))
        B.place(group + wrap, m)


def pierce(s):
    return R.pierce(s.knives, s.walls)


def crossing(s):
    """Dreieckspaare, in denen zwei Messer einander durchdringen: (rechts-Mitte, Mitte-links, rechts-links)."""
    trees = []
    for group in s.knives:
        vs, ts = [], []
        for o in group:
            v, t = R.tris(o)
            ts += [tuple(i + len(vs) for i in tri) for tri in t]
            vs += v
        trees.append(BVHTree.FromPolygons(vs, ts))
    return tuple(len(trees[a].overlap(trees[b])) for a, b in ((0, 1), (1, 2), (0, 2)))


def seen(s):
    """Punkte der Messer (Ecken und Flaechenmitten) hinter der Deckelebene, die das Auge sieht und die im Bild liegen.
    Rueckgabe: je Messer (Zahl, betroffene Teile)."""
    fwd, eye = G(V(0, 0, 1)), G(B.EYE)
    out = []
    for group in s.knives:
        count, parts = 0, set()
        for o in group:
            vs, ts = R.tris(o)
            inv = o.matrix_world.inverted()
            for p in vs + [sum((vs[i] for i in t), Vector()) / 3 for t in ts]:
                if (p - s.lid).dot(fwd) > -0.001:
                    continue
                x, y = B.pixel((p.x, p.z, -p.y))
                if not (0 <= x < B.GAME[0] and 0 <= y < B.GAME[1]):
                    continue
                d = p - eye
                hit = min((h[3] for h in (w.ray_cast(eye, d.normalized()) for w in s.all_walls) if h[0] is not None),
                          default=None)
                if hit is None or hit > d.length - 1e-4:
                    count += 1
                    parts.add(R.part(-(inv @ p).y / SCALE))
        out.append((count, sorted(parts)))
    return out


def masks(s):
    """Je Messer: ganze Maske und Maske der Wicklung im Spielbild, beide ohne das, was der Aermel verdeckt."""
    whole = [diff(np.array(B.player(list(s.room) + g, s.cam)), s.empty) & ~s.arm for g in s.knives]
    wrap = [diff(np.array(B.player(list(s.room) + w, s.cam)), s.empty) & ~s.arm for w in s.wraps]
    return whole, wrap


def overlap(s):
    """Ueberdeckung im Bild. rows = Bildzeilen, in denen sich benachbarte Messer ueberdecken (Mass der Korrektur zu
    4b), width = groesste Ueberdeckung in Bildpunkten in einer Zeile; je Wicklung: Bildpunkte allein, davon mit einem
    anderen Messer ueberdeckt, und die Zeilen, in denen das geschieht."""
    whole, wrap = masks(s)
    rows, width = [], 0
    for y in range(whole[0].shape[0]):
        for a, b in ((0, 1), (1, 2)):
            both = int((whole[a][y] & whole[b][y]).sum())
            if both:
                rows.append(y)
                width = max(width, both)
    rows = sorted(set(rows))
    per = []
    for i in range(3):
        others = np.zeros_like(whole[0])
        for j in range(3):
            if j != i:
                others |= whole[j]
        hit = wrap[i] & others
        ys = np.flatnonzero(hit.any(1))
        per.append((int(wrap[i].sum()), int(hit.sum()), len(ys)))
    visible = [int(m.sum()) for m in whole]
    return {"rows": rows, "width": width, "wrap": per, "visible": visible}


def line(tag, label, r):
    rows = r["rows"]
    span = f"{rows[0]} bis {rows[-1]} ({len(rows)} Zeilen, bis {r['width']} px breit)" if rows else "keine"
    wraps = "; ".join(f"{n}: {a} px, davon {b} px in {c} Zeilen ueberdeckt" for n, (a, b, c) in zip(NAMES, r["wrap"]))
    return f"{tag} {label} | Messer ueberdecken sich in Zeilen {span} | Wicklung {wraps} | sichtbar je Messer {r['visible']} px"
