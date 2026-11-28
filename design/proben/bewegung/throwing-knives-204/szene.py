"""Throwing Knives (Issue #204), Bewegungsprobe: Szene, Haltung und Messungen in Blender.

Baut auf den Skripten der Grafikprobe auf (unveraendert kopiert: blatt.py, messer_gemeinsam.py, runde-4/versatz.py,
runde-5/staffel.py) und stellt die abgenommene Ruhehaltung s12 mit dem Handmodell messer-e-hand-s12.glb ein.

pose():     Haltung aus bewegung.py auf Manschette, Messer und Unterarm legen. Manschette und Messer drehen gemeinsam
            um den Haltepunkt und werden verschoben; der Unterarm dreht um seinen Drehpunkt nur so weit, dass er weiter
            zur Schulter zeigt (wie WeaponPose.ForearmTurn im Spiel). Gibt den Knick Manschette/Unterarm zurueck.
measure():  Pruefung an der Geometrie in der aktuellen Haltung (ohne Rendern): Regel 30 (Dreiecke der Messer gegen
            Aermel ausser Deckel), sichtbare Punkte hinter dem Deckel (der Schnitt des Handmodells), Messer gegen
            Messer, Abstand zur Silver Blade, Naehe zum Auge, Reichweite, offenes Ende des Unterarms im Bild.
picture():  Ueberdeckung im Bild (Masken je Messer wie staffel.overlap) und Bildpunkte im Zielfenster.
"""

import math
import os
import sys

import numpy as np

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "runde-5"))
import staffel as S  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402
from psx import write_light  # noqa: E402

B, R, G, V, SCALE = S.B, S.R, S.G, S.V, S.SCALE
DEPTH, PIVOT, DX = 0.012, 0.062, 0.008           # Haltung s12 (Skriptmass)
STEP = (0.0, 0.006, 0.012)                       # Staffel entlang der Dicke: rechts, Mitte, links
SHOULDER = V(-0.40, -0.65, -0.20)                # WeaponPose.PistolShoulder, x gespiegelt (hier liegt +x links im Bild)
SHRINK = 0.5                                     # WeaponPose.ShrinkTowardsEye
REACH, NEAR = 0.30, 0.05                         # WeaponPose.Reach; nichts naeher als 5 cm am Auge
AIM = (120, 120)                                 # Zielfenster um das Fadenkreuz (Bildpunkte, 854 x 480)
SPIN = 40.0                                      # Drehung des Geschosses je Tick auf dem Blatt (nur zur Anschauung)


def setup():
    s = S.setup(knife=("messer-e-hand-s12.glb", os.path.join(HERE, "runde-5")))
    s.shot = B.load("messer-e-geschoss.glb", "geschoss", os.path.join(HERE, "runde-5"))
    B.place(s.shot, B.AWAY)
    s.cuff = [o for o in s.sleeve if o.name.startswith("arm_cuff")]
    s.forearm = [o for o in s.sleeve if o not in s.cuff]
    assert s.cuff and s.forearm, [o.name for o in s.sleeve]
    s.lids = [lid for _tree, lid in s.walls]
    s.rest = {}
    s.fan = None
    s.turn = Matrix()
    fan(s, 17.0)
    for o in s.sleeve:
        s.rest[o.name] = o.matrix_world.copy()
    s.pivot = s.forearm[0].matrix_world.translation.copy()
    s.shoulder = G(B.EYE + SHOULDER)
    return s


def fan(s, angle):
    """Ruhehaltung s12 mit dem Faecherwinkel `angle` einstellen und merken."""
    if s.fan == angle:
        return
    S.put(s, DEPTH, PIVOT, DX, fan=angle, thick=STEP)
    for group, wrap in zip(s.knives, s.wraps):
        for o in group + wrap:
            s.rest[o.name] = o.matrix_world.copy()
    s.fan = angle


def turn_of(p):
    """Drehung und Verschiebung der Haltung in Blender-Achsen (um den Haltepunkt)."""
    rot = (Matrix.Rotation(math.radians(p["yaw"]), 4, "Z") @ Matrix.Rotation(math.radians(-p["roll"]), 4, "Y")
           @ Matrix.Rotation(math.radians(-p["pitch"]), 4, "X"))
    return rot, Matrix.Translation(G(V(*p["shift"])))


def shortest(a, b):
    axis = a.cross(b)
    if axis.length < 1e-9:
        return Matrix()
    return Matrix.Rotation(a.angle(b), 4, axis.normalized())


def pose(s, p):
    """Haltung p einstellen. Rueckgabe: Knick zwischen Manschette und Unterarm in Grad."""
    fan(s, round(p["fan"], 3))
    h = G(s.hand)
    rot, move = turn_of(p)
    m = move @ Matrix.Translation(h) @ rot @ Matrix.Translation(-h)
    s.turn = m
    moved = s.cuff + [o for g, w in zip(s.knives, s.wraps) for o in g + w]
    for o in moved:
        o.matrix_world = m @ s.rest[o.name]
    pivot = m @ s.pivot
    was = (m.to_3x3() @ (s.shoulder - s.pivot)).normalized()
    now = (s.shoulder - pivot).normalized()
    bend = Matrix.Translation(pivot) @ shortest(was, now) @ Matrix.Translation(-pivot)
    for o in s.forearm:
        o.matrix_world = bend @ m @ s.rest[o.name]
    B.bpy.context.view_layer.update()
    for o in moved + s.forearm:
        write_light(o)
    return math.degrees(was.angle(now))


def project(w):
    """Blender-Punkt -> (x, y, Tiefe vor dem Auge) im Spielbild."""
    d = V(w.x, w.z, -w.y) - B.EYE
    if d.z < 0.011:
        return None
    return B.GAME[0] / 2 - d.x / d.z * B.FOCAL, B.GAME[1] / 2 - d.y / d.z * B.FOCAL, d.z


def in_frame(q, margin=0):
    return q is not None and -margin <= q[0] < B.GAME[0] + margin and -margin <= q[1] < B.GAME[1] + margin


def seen(s, groups, walls):
    """Wie staffel.seen, aber in der aktuellen Haltung: Punkte der Messer hinter der Ebene des Deckels, die das Auge
    sieht und die im Bild liegen."""
    fwd = (s.turn.to_3x3() @ G(V(0, 0, 1))).normalized()
    lid = s.turn @ s.lid
    eye = G(B.EYE)
    count = 0
    for group in groups:
        for o in group:
            vs, ts = R.tris(o)
            for p in vs + [sum((vs[i] for i in t), Vector()) / 3 for t in ts]:
                if (p - lid).dot(fwd) > -0.001 or not in_frame(project(p)):
                    continue
                d = p - eye
                hit = min((x[3] for x in (w.ray_cast(eye, d.normalized()) for w in walls) if x[0] is not None),
                          default=None)
                if hit is None or hit > d.length - 1e-4:
                    count += 1
    return count


def measure(s, shown=(0, 1, 2)):
    """Geometrie-Pruefung der aktuellen Haltung fuer die Messer `shown` (0 rechts, 1 Mitte, 2 links)."""
    groups = [s.knives[i] for i in shown]
    trees = [BVHTree.FromPolygons(*R.tris(o)) for o in s.sleeve]
    walls = list(zip(trees, s.lids))
    out = {"pierce": sum(n for n, _parts in R.pierce(groups, walls)), "seen": seen(s, groups, trees)}
    cross = S.crossing(s)
    pairs = {(0, 1): cross[0], (1, 2): cross[1], (0, 2): cross[2]}
    out["cross"] = sum(n for (a, b), n in pairs.items() if a in shown and b in shown)
    blade = [BVHTree.FromPolygons(*R.tris(o)) for o in s.blade]
    eye = G(B.EYE)
    gap, near, far, inside = 9.0, 9.0, 0.0, 0
    for o in s.cuff + [x for g in groups for x in g]:
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            gap = min(gap, min(t.find_nearest(w)[3] for t in blade))
            near = min(near, (w - eye).length * SHRINK)
            far = max(far, -(w - eye).y * SHRINK)
            inside += in_frame(project(w))
    out["blade_mm"], out["near"], out["far"], out["in_frame"] = gap * 1000, near, far, inside
    # Offenes Ende des Unterarms: die Ecken, die am weitesten zur Schulter liegen, duerfen nicht im Bild stehen.
    end = 0
    for o in s.forearm:
        ws = [o.matrix_world @ v.co for v in o.data.vertices]
        axis = (s.shoulder - (o.matrix_world.translation)).normalized()
        along = [(w - o.matrix_world.translation).dot(axis) for w in ws]
        limit = max(along) - 0.01
        end += sum(1 for w, a in zip(ws, along) if a >= limit and in_frame(project(w)))
    out["open_end"] = end
    return out


def box(s, shown=(0, 1, 2)):
    """Bildrahmen von Manschette und Messern (Bildpunkte); None, wenn nichts davon vor dem Auge liegt."""
    pts = [project(o.matrix_world @ v.co) for o in s.cuff + [x for i in shown for x in s.knives[i]] for v in o.data.vertices]
    pts = [q for q in pts if q is not None]
    if not pts:
        return None
    a = np.array(pts)[:, :2]
    return tuple(int(x) for x in (*a.min(0), *a.max(0)))


def picture(s):
    """Ueberdeckung im Bild mit allen drei Messern (strengster Fall) und Bildpunkte von Aermel und Messern im
    Zielfenster. Rendert sieben Bilder."""
    s.arm = S.diff(np.array(B.player(list(s.room) + s.sleeve, s.cam)), s.empty)
    r = S.overlap(s)
    whole, _wrap = S.masks(s)
    mask = s.arm.copy()
    for m in whole:
        mask |= m
    cy, cx = B.GAME[1] // 2, B.GAME[0] // 2
    r["aim"] = int(mask[cy - AIM[1] // 2:cy + AIM[1] // 2, cx - AIM[0] // 2:cx + AIM[0] // 2].sum())
    r["pixels"] = int(mask.sum())
    return r


def frame(s, shown, flight=None, blade=True):
    """Spielbild 854 x 480 der aktuellen Haltung mit den Messern `shown`; flight = Ticks seit dem Loslassen (das
    Geschoss liegt dann 0,5 m je Tick vor dem Auge, wie im Spielkern)."""
    objs = list(s.room) + s.sleeve + [o for i in shown for o in s.knives[i]] + (s.blade if blade else [])
    if flight:
        p = B.EYE + V(0, 0, 0.5 * flight)
        B.place(s.shot, B.thrown((p.x, p.y, p.z), spin=SPIN * flight))
        objs += s.shot
    return np.array(B.player(objs, s.cam))
