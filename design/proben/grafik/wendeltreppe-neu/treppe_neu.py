"""Probestueck zu Issue #765: drei Entwuerfe einer detaillierteren Wendeltreppe (nur Probe, nichts im Spiel).

  a  schlanke Stufenplatten mit Trittkante, profilierter Sockel/Spindel/Kapitell, kein Gelaender
  b  wie a, dazu ein Eisengelaender: Staebe an jeder Stufe und ein Handlauf, beginnt erst ab der vierten Stufe (freier Zugang)
  c  wie a, dazu eine steinerne Wange mit niedriger Bruestung (0,36 m ueber der Stufe), ebenfalls ab der vierten Stufe

Aufruf (aus dem Repository-Wurzelverzeichnis), schreibt entwurf_a/b/c.glb und .png neben dieses Skript:
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/wendeltreppe-neu/treppe_neu.py

Die Atlas- und Flaechenfunktionen (Kacheln mehrfach im Atlas, nichts wird geschnitten) kommen aus tools/models/stairs.py in der Fassung
des Branchs fix/wendeltreppe-geschlossen-416 (PR #763): der Kopf der Datei bis `def spiral` wird hier ausgefuehrt. Grundmasse wie das
heutige Modell: Ursprung Mitte am Boden, Sockel +-1,0971 m, 6 m hoch, Lampe (glow) unveraendert. Keine neuen Farben: Stein-Kacheln
der Variante a, Werkstein, Messing, dazu die Reihe Eisen der Hauptpalette (Stufen 3 bis 5) fuers Gelaender.
"""

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
SRC = os.path.join(ROOT, "tools", "models", "stairs.py")
text = open(SRC, encoding="utf-8").read()
text = text[:text.index("\ndef spiral")].replace("__file__", repr(SRC))
exec(compile(text, SRC, "exec"))                                                    # noqa: S102 (eigenes Skript im Repository)

import json  # noqa: E402

REGIONS["iron"] = (128, 256, 256, 384)                                              # Eisen: 2 x 2 Kacheln
TILES["iron"] = (64, 64)
PLATE = 0.15                                                                        # Dicke der Stufenplatten (Anstieg 0,25)


def iron_row():
    with open(PALETTE, encoding="utf-8") as f:
        data = json.load(f)
    return {r["name"]: r["farben"] for r in data["grund"] + data["akzent"]}["Eisen"]


def atlas_neu(variant, werkstein, messing):
    a, _ = atlas(variant, werkstein, messing)
    eisen = iron_row()
    x0, y0, x1, y1 = REGIONS["iron"]
    rng = np.random.RandomState(7)
    for yy in range(y0, y1):
        for xx in range(x0, x1):
            r = rng.rand()
            a[yy, xx] = eisen[3] if r < 0.80 else (eisen[2] if r < 0.93 else eisen[4])
    used = {tuple(p) for p in a.reshape(-1, 3).astype(int)}
    return a, len(used)


# --- allgemeine Koerper ----------------------------------------------------------------------------------------------------------

def P3(r, a_deg, y):
    return (r * math.cos(math.radians(a_deg)), y, r * math.sin(math.radians(a_deg)))


def centroid(pts):
    return tuple(sum(p[i] for p in pts) / len(pts) for i in range(3))


def area_xz(pts):
    return 0.5 * sum(a[0] * b[2] - b[0] * a[2] for a, b in zip(pts, pts[1:] + pts[:1]))


def face(part, pts, hint, field, fit=False):
    """Vier Punkte: eben als ein Viereck, sonst als zwei Dreiecke (gleiche Diagonale auf beiden Seiten der Kante)."""
    A, B, C, D = pts
    n = (V(*B) - V(*A)).cross(V(*C) - V(*A))
    if n.length < 1e-12 or abs(n.normalized().dot(V(*D) - V(*A))) < 1e-5:
        emit(part, pts, hint, field, fit)
    else:
        emit(part, [A, B, C], hint, field)
        emit(part, [A, C, D], hint, field)


def tube(part, rings, field="stone", fields=None, top_field=None, bottom=True, top=True, fit_bottom=False, fit_top=False):
    """Geschlossener Koerper aus Ringen (gleich viele Punkte, jeder Ring ein konvexes Vieleck, im Plan gegen den Uhrzeigersinn).
    Die Seitenflaechen zwischen zwei Ringen sind Vierecke; liegen beide Ringe gleich hoch, zeigt die Flaeche nach oben, wenn der
    zweite Ring kleiner ist, sonst nach unten. `fields(y_mitte)` waehlt die Kachel je Ringabstand."""
    for k in range(len(rings) - 1):
        r0, r1 = rings[k], rings[k + 1]
        c0, c1 = centroid(r0), centroid(r1)
        mid = centroid([c0, c1])
        f = fields((c0[1] + c1[1]) / 2) if fields else field
        flat = all(abs(a[1] - b[1]) < 1e-9 for a, b in zip(r0, r1)) and abs(c0[1] - c1[1]) < 1e-9
        for i in range(len(r0)):
            j = (i + 1) % len(r0)
            q = [r0[i], r0[j], r1[j], r1[i]]
            if flat:
                hint = (0, 1, 0) if abs(area_xz(r1)) < abs(area_xz(r0)) else (0, -1, 0)
                if abs(abs(area_xz(r1)) - abs(area_xz(r0))) < 1e-9:
                    continue
            else:
                qc = centroid(q)
                hint = (qc[0] - mid[0], qc[1] - mid[1], qc[2] - mid[2])
            if len({tuple(round(c, 6) for c in p) for p in q}) < 3:
                continue
            if len({tuple(round(c, 6) for c in p) for p in q}) == 3:                # Dreieck (zwei Punkte fallen zusammen)
                seen = []
                for p in q:
                    if tuple(round(c, 6) for c in p) not in [tuple(round(c, 6) for c in s) for s in seen]:
                        seen.append(p)
                emit(part, seen, hint, f)
            else:
                face(part, q, hint, f)
    if bottom:
        emit(part, rings[0], (0, -1, 0), field, fit_bottom)
    if top:
        emit(part, rings[-1], (0, 1, 0), top_field or field, fit_top)


def oct_ring(r, y):
    return [(x, y, z) for x, z in octagon(r)]


def box(part, cx, cz, yaw_deg, w, d, y0, y1, field="iron"):
    """Quader um (cx, cz), w quer zum Radius, d entlang des Radius, gedreht um die Hochachse."""
    ca, sa = math.cos(math.radians(yaw_deg)), math.sin(math.radians(yaw_deg))
    pts = []
    for dx, dz in ((-d, -w), (d, -w), (d, w), (-d, w)):                              # gegen den Uhrzeigersinn von oben (x nach z)
        pts.append((cx + dx * ca - dz * sa, cz + dx * sa + dz * ca))
    tube(part, [[(x, y0, z) for x, z in pts], [(x, y1, z) for x, z in pts]], field, fit_bottom=True)


# --- Teile -----------------------------------------------------------------------------------------------------------------------

def spindle(part):
    """Sockel mit Fase, Spindel mit vier Ringwuelsten, Kapitell aus Werkstein: ein Koerper (Drehkoerper aus Achtecken)."""
    prof = [(0.0, 1.1875), (0.09, 1.1875), (0.125, 1.15), (0.125, 1.0625), (0.21, 1.0625), (0.25, 1.02),
            (0.25, 0.70), (0.25, 0.44), (0.31, 0.36), (0.40, 0.3125)]
    for yc in (1.30, 2.55, 3.80, 5.05):
        prof += [(yc - 0.06, 0.3125), (yc, 0.355), (yc + 0.07, 0.355), (yc + 0.13, 0.3125)]
    prof += [(5.50, 0.3125), (5.50, 0.50), (5.62, 0.50), (5.75, 0.42)]
    rings = [oct_ring(r, y) for y, r in prof]
    tube(part, rings, "stone", fields=lambda y: "werk" if y >= 5.5 - 1e-9 else "stone", top_field="werk", fit_bottom=True)


def tread(part, i):
    """Stufenplatte i: 45 Grad breit, vorn (Seite a0) mit Nase, unten gefast; innen im Schaft verborgen."""
    a0, a1 = 45.0 * i, 45.0 * (i + 1)
    t = 0.5 + 0.25 * i
    r_in, r_out, nose = 0.2, 1.0625, 3.0
    full = [P3(r_in, a0 - nose, t), P3(r_out, a0 - nose, t), P3(r_out, a1, t), P3(r_in, a1, t)]
    mid = [P3(r_in, a0 - nose, t - 0.06), P3(r_out, a0 - nose, t - 0.06), P3(r_out, a1, t - 0.06), P3(r_in, a1, t - 0.06)]
    low = [P3(r_in, a0 - nose + 2.5, t - PLATE), P3(r_out - 0.05, a0 - nose + 2.5, t - PLATE),
           P3(r_out - 0.05, a1 - 2.0, t - PLATE), P3(r_in, a1 - 2.0, t - PLATE)]
    tube(part, [low, mid, full], "stone", fit_bottom=True)


def ribbon(part, nodes, section, field, caps=True):
    """Band entlang einer Schraubenlinie. nodes: (Winkel in Grad, Radius, Hoehe der Mitte); section: (radial, hoch) im Ring,
    konvex im Uhrzeigersinn vom Strahl nach aussen gesehen. Die Ringe liegen in der Ebene aus Strahl und Hochachse."""
    rings = []
    for a, r, y in nodes:
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        rings.append([((r + dr) * ca, y + dh, (r + dr) * sa) for dr, dh in section])
    tube(part, rings, field, bottom=caps, top=caps)


def tread_top(i):
    return 0.5 + 0.25 * i


def rail_iron(part, first=3, last=19):
    """Staebe (Vierkant 0,045) und Handlauf aus Eisen, ab Stufe `first` (die untersten Stufen bleiben offen)."""
    nodes = []
    for i in range(first, last + 1):
        a = 45.0 * i + 30.0                                                         # ueber dem hinteren Drittel der Stufe
        t = tread_top(i)
        x, z = 0.93 * math.cos(math.radians(a)), 0.93 * math.sin(math.radians(a))
        box(part, x, z, a, 0.0225, 0.0225, t - 0.05, t + 0.86)
        nodes.append((a, 0.93, t + 0.86))
    # Handlauf: Vierkant 0,05 breit, 0,045 hoch, auf den Staeben; Schnitt gegen den Uhrzeigersinn von aussen gesehen
    section = [(-0.03, -0.0225), (0.03, -0.0225), (0.03, 0.0225), (-0.03, 0.0225)]
    ribbon(part, [(a, r, y + 0.0225) for a, r, y in nodes], section, "iron")
    a, r, y = nodes[0]                                                              # Anfangspfosten, dicker, mit Spitze
    x, z = 0.93 * math.cos(math.radians(a - 22.5)), 0.93 * math.sin(math.radians(a - 22.5))
    base = tread_top(first) - 0.12
    base = tread_top(first) - 0.12
    ca, sa = math.cos(math.radians(a - 22.5)), math.sin(math.radians(a - 22.5))
    rings = []
    for y, k in ((base - 0.03, 1.0), (base + 1.05, 1.0), (base + 1.10, 0.6), (base + 1.16, 0.25)):   # Vierkant, oben zur Spitze verjuengt
        s = 0.045 * k
        rings.append([(x + dx * ca - dz * sa, y, z + dx * sa + dz * ca) for dx, dz in ((-s, -s), (s, -s), (s, s), (-s, s))])
    tube(part, rings, "iron", fit_bottom=True)

def wall_stone(part, first=3, last=19):
    """Steinerne Wange mit niedriger Bruestung: Sechseck-Band entlang der Treppe, Ringe an den Stufengrenzen."""
    section = [(-0.05, -0.30), (0.05, -0.30), (0.05, 0.30), (0.03, 0.37), (-0.03, 0.37), (-0.05, 0.30)]
    nodes = [(45.0 * k, 0.97, 0.5 + 0.25 * (k - 0.5)) for k in range(first, last + 2)]
    ribbon(part, nodes, section, "stone")


def build_part(design):
    st, gl = Part("stairs", (0, 0, 0), UV), Part("glow", (0, 0, 0), UV)
    spindle(st)
    for i in range(STEPS):
        tread(st, i)
    if design == "b":
        rail_iron(st)
    if design == "c":
        wall_stone(st)
    tube(gl, [oct_ring(0.3125, HEIGHT - 0.25), oct_ring(0.3125, HEIGHT)], "glow")   # Lampe wie heute
    return [st, gl]


def main():
    werkstein, messing = palette_rows()
    for d in "abc":
        bpy.ops.wm.read_factory_settings(use_empty=True)
        coll = bpy.data.collections.new("Treppe")
        bpy.context.scene.collection.children.link(coll)
        name = f"entwurf_{d}"
        tex, colours = atlas_neu("a", werkstein, messing)
        image = save_image(name, tex, os.path.join(HERE, name + ".png"))
        mat = export_material(name, image)
        objs = [p.build(coll, mat, smooth=False) for p in build_part(d)]
        export_glb(objs, os.path.join(HERE, name + ".glb"))
        report_parts(name, objs)
        print(f"TEXTUR {name} {W}x{H} Farben {colours}")


main()
