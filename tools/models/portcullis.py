"""Fallgitter (Issue #540, Teil 1): eisernes Gitter fuer den Normal- und den Verschlossen-Rahmen, dazu der steinerne
Fuehrungsring im Bogen der Tuerebene.

Erzeuger fuer das Spiel. Uebergabe aus dem abgenommenen Probestueck design/proben/grafik/fallgitter-540/fallgitter.py
(Branch feat/fallgitter-probe-540, Commit 15c8f06): gleiche Geometrie und derselbe Atlas, nur die breite Form (das schmale
Gitter fuer den Boss-Rahmen entfaellt, am Bossraum gibt es kein Fallgitter) und der Ring "normal". Das Eisen ist die
neutrale Reihe K2 aus #543 statt der blaeulichen Reihe Eisen der Hauptpalette; K2 gilt nur fuer dieses Gitter. Erzeugt nur
Modelle und Texturen nach assets/models/, keine Probebilder, dazu die Liste der gelesenen Wand-Texturen
(tools/models/portcullis_sources.sha256).

Aufruf (aus dem Repository-Wurzelverzeichnis), danach python tools/models/checksums.py:
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/portcullis.py

Alles per Skript, nichts heruntergeladen. Spiel-Achsen (x seitlich, y oben, z vorn), Meter. Ursprung bei beiden Modellen:
Mitte der Tuerebene auf dem Boden, also derselbe Punkt wie beim Tuerrahmen (Zellmitte). Das Gitter wird im Spiel als
Ganzes in y verschoben (y = Unterkante der Spitzen ueber dem Boden); der Ring bleibt stehen.

Dateien:
  portcullis.glb / .png          ein Knoten grate: 9 Vierkantstaebe 0,07 mit Pyramidenspitze, drei Querbaender und der
                                 obere Abschluss; 1,70 breit, 3,50 hoch. Atlas 64 x 64 (Stab links, Band rechts), nur K2.
  portcullis_guide_<v>.glb / .png  ein Knoten guide: Spitzbogenring 1/16 breit hinter dem innersten Lauf des Rahmens (R 1,5 bis
                                 1,5625, seit #714 im Stein hinter der Leibung), so tief wie die Tuerebene; Atlas ist die Wandkachel der Variante v (a, b, c) in Welt-UV.
Jedes Netz rundum geschlossen. Zwei Laeufe liefern gleiche Dateien (die Fleckung nutzt einen festen Seed). Aendert sich
eine Wand-Textur, passt der Ring nicht mehr zur Wand; ein Test vergleicht portcullis_sources.sha256 mit
assets/textures/textures.sha256 und schlaegt fehl, bis dieses Skript neu gelaufen ist.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from psx import V, G, Part, rects_from_pixels, save_image, export_material, export_glb, report_parts  # noqa: E402
from pillar_kit import OUT_DIR, TOOLS, WALL_FILES, colours_of, edge_report, load_tile, stein_row, write_sources  # noqa: E402

SOURCES = os.path.join(TOOLS, "portcullis_sources.sha256")

# --- Eisen K2 (#543): neutral, vier Stufen von dunkel nach hell -----------------------------------------------------
K2 = np.array([(32, 32, 33), (62, 62, 63), (104, 104, 106), (148, 148, 151)], int)

# --- Masse (wie im Probestueck, dort in LIESMICH.md begruendet) -------------------------------------------------------
BAR = 0.035                  # halbe Stabdicke (Stab 0,07 x 0,07, vierkant)
SPIKE = 0.22                 # Laenge der Spitze unten (vierseitige Pyramide)
BAR_TOP = 3.46               # Staebe enden im oberen Querband
BANDS = (0.62, 1.55, 2.48)   # Mitte der Querbaender, je 0,10 hoch
BAND_H = 0.05                # halbe Hoehe
BAND_Z = 0.05                # halbe Tiefe (Stab: 0,035) - das Band umfasst die Staebe
RAIL = (3.38, 3.50)          # oberer Abschluss, liegt im Zustand "zu" hinter dem Bogenscheitel (3,32)
HALF_WIDTH, BARS = 0.85, 9   # Form "breit"

# Ring (Kreismittelpunkt x, R innen, R aussen, Ansatz, halbe Tiefe) nach tools/models/door.py (stufenportal, innerster Lauf).
# Seit Issue #714 schliesst der Rahmen den Bogen selbst: Seine Leibung (innerster Lauf, R 1,4375) reicht von der Wandebene bis
# zur Tuerebene und ueber sie hinweg bis z = +-0,07 und zurueck. Der Ring lag mit seiner Innenflaeche bei demselben R auf
# derselben Stelle (zwei gleich ausgerichtete Flaechen = Z-Fighting). Er sitzt jetzt 1/16 weiter aussen (R innen 1,5) im Stein
# hinter der Leibung: rundum geschlossen, nichts mehr deckungsgleich, im Bild von der Leibung verdeckt.
RING = (1.0 - 1.5625, 1.5, 1.5625, 2.0, 0.07)

# --- Atlas -----------------------------------------------------------------------------------------------------------
G_SIZE = (64, 64)
G_UV = rects_from_pixels({"bar": (0, 0, 32, 64), "band": (32, 0, 64, 64)}, G_SIZE)
F_UV = rects_from_pixels({"stone": (0, 0, 64, 64)}, (64, 64))


def grate_atlas():
    """64 x 64: links der Stab (u quer ueber die Breite: Lichtkante links, dunkel rechts), rechts das Band (v quer: oben hell).
    Die Stufen 2..5 des Probestuecks sind hier K2[0..3]."""
    iron = np.concatenate([np.zeros((2, 3), int), K2]).astype(float)
    rng = np.random.default_rng(540)
    bar = np.tile(iron[4], (64, 32, 1))
    bar[:, 0:4] = iron[5]
    bar[:, 26:30] = iron[3]
    bar[:, 30:32] = iron[2]
    speck = rng.random((64, 32)) < 0.07
    speck[:, :4] = speck[:, 26:] = False
    bar[speck] = iron[3]
    band = np.tile(iron[4], (64, 32, 1))
    band[56:64] = iron[5]          # Zeile 0 unten: oben hell
    band[14:26] = iron[3]
    band[0:14] = iron[2]
    speck = rng.random((64, 32)) < 0.06
    speck[56:] = False
    band[speck] = iron[3]
    out = np.zeros((64, 64, 3))
    out[0:64, 0:32] = bar
    out[0:64, 32:64] = band
    return out


def guide_atlas(variant):
    return load_tile(WALL_FILES[variant])[::-1].astype(float)


def check_palette(name, tex, row, uses_all):
    """Nur Farben der Reihe; bei uses_all muss jede Farbe der Reihe vorkommen."""
    allowed = {tuple(int(c) for c in p) for p in row}
    used = {tuple(int(c) for c in p) for p in np.round(tex).reshape(-1, 3)}
    assert used <= allowed, f"{name}: Farben ausserhalb der Reihe: {sorted(used - allowed)}"
    assert not uses_all or used == allowed, f"{name}: Farben der Reihe ungenutzt: {sorted(allowed - used)}"
    assert len(used) <= 40, f"{name}: {len(used)} Farben (hoechstens 40)"


# --- Gitter ----------------------------------------------------------------------------------------------------------

def bar(part, cx):
    """Ein Stab: vierkantiger Schaft mit Pyramidenspitze unten, als ein einziger konvexer Koerper (14 Dreiecke)."""
    y0, y1 = SPIKE, BAR_TOP
    cs = [(cx - BAR, -BAR), (cx + BAR, -BAR), (cx + BAR, BAR), (cx - BAR, BAR)]
    tip = V(cx, 0.0, 0.0)
    polys = []
    for (xa, za), (xb, zb) in zip(cs, cs[1:] + cs[:1]):
        polys.append([V(xa, y0, za), V(xb, y0, zb), V(xb, y1, zb), V(xa, y1, za)])
        polys.append([V(xa, y0, za), V(xb, y0, zb), tip])
    polys.append([V(x, y1, z) for x, z in cs])
    part.convex(polys, "bar")


def build_grate():
    part = Part("grate", (0.0, 0.0, 0.0), G_UV)
    span = HALF_WIDTH - BAR
    for i in range(BARS):
        bar(part, -span + 2 * span * i / (BARS - 1))
    hw = HALF_WIDTH + 0.005
    for y in BANDS:
        part.box((-hw, y - BAND_H, -BAND_Z), (hw, y + BAND_H, BAND_Z), "band")
    part.box((-hw, RAIL[0], -BAND_Z), (hw, RAIL[1], BAND_Z), "band")
    return part


# --- Fuehrung (Sturzring) --------------------------------------------------------------------------------------------

def arc_pts(xc, r, spring, n=6):
    rise = math.sqrt(r * r - xc * xc)
    a_end = math.atan2(rise, -xc)
    return [(xc + r * math.cos(a_end * j / n), spring + r * math.sin(a_end * j / n)) for j in range(n + 1)]


def face(part, pts, hint):
    """Eine ebene Flaeche mit Welt-UV (eine 64-px-Kachel je Meter), nicht an Metergrenzen geteilt: die Kachel wird auf die
    Zelle der kleinsten Ecke festgelegt und die Ecken darueber hinaus werden auf den Rand gedrueckt (die Flaechen sind kleiner
    als 0,25 m). So entstehen keine T-Knoten und das Netz bleibt geschlossen."""
    P = [V(*p) for p in pts]
    n = (P[1] - P[0]).cross(P[2] - P[0])
    if n.dot(V(*hint)) < 0:
        P.reverse()
        n = -n
    n = n.normalized()
    ax = max(range(3), key=lambda i: abs(n[i]))
    if ax == 2:
        pu, pv = (lambda p: p.x if n.z > 0 else -p.x), (lambda p: p.y)
    elif ax == 0:
        pu, pv = (lambda p: -p.z if n.x > 0 else p.z), (lambda p: p.y)
    else:
        pu, pv = (lambda p: p.x if n.y > 0 else -p.x), (lambda p: p.z)
    us, vs = [pu(p) for p in P], [pv(p) for p in P]
    i0, j0 = math.floor(min(us) + 1e-9), math.floor(min(vs) + 1e-9)
    u0, v0, u1, v1 = part.rects["stone"]
    verts = [part.bm.verts.new(G(p - part.pivot)) for p in P]
    uvs = [(u0 + (u1 - u0) * min(max(u - i0, 0), 1), v0 + (v1 - v0) * min(max(v - j0, 0), 1)) for u, v in zip(us, vs)]
    part._face(verts, uvs)


def build_guide():
    """Bogenring (Spitzbogen aus zwei Kreisen wie der innerste Lauf des Rahmens), 1/8 breit, so tief wie die Tuerebene
    (z = +-hz). Seit #714 ohne Flaeche auf der Leibung des Rahmens: Der Ring liegt hinter ihr im Stein; rundum geschlossen."""
    xc, r_in, r_out, spring, hz = RING
    part = Part("guide", (0.0, 0.0, 0.0), F_UV)
    ri, ro = arc_pts(xc, r_in, spring), arc_pts(xc, r_out, spring)
    seq_i = ri + [(-x, y) for x, y in reversed(ri[:-1])]          # rechter Ansatz -> Scheitel -> linker Ansatz
    seq_o = ro + [(-x, y) for x, y in reversed(ro[:-1])]
    cen = [(xc, spring)] * 6 + [(-xc, spring)] * 6                  # Kreismitte je Segment (rechte, linke Haelfte)
    for k in range(len(seq_i) - 1):
        (xa, ya), (xb, yb), (xc2, yc2), (xd, yd) = seq_i[k], seq_i[k + 1], seq_o[k + 1], seq_o[k]
        mx, my = (xa + xb) / 2, (ya + yb) / 2
        to_c = (cen[k][0] - mx, cen[k][1] - my, 0)                   # zum Kreismittelpunkt = in die Oeffnung
        face(part, [(xa, ya, -hz), (xb, yb, -hz), (xb, yb, hz), (xa, ya, hz)], to_c)                       # innen
        face(part, [(xd, yd, -hz), (xc2, yc2, -hz), (xc2, yc2, hz), (xd, yd, hz)], (-to_c[0], -to_c[1], 0))   # aussen
        face(part, [(xa, ya, hz), (xb, yb, hz), (xc2, yc2, hz), (xd, yd, hz)], (0, 0, 1))                  # vorn
        face(part, [(xa, ya, -hz), (xb, yb, -hz), (xc2, yc2, -hz), (xd, yd, -hz)], (0, 0, -1))              # hinten
    for k in (0, len(seq_i) - 1):                                    # Enden auf der Ansatzlinie
        (xa, ya), (xd, yd) = seq_i[k], seq_o[k]
        face(part, [(xa, ya, -hz), (xd, yd, -hz), (xd, yd, hz), (xa, ya, hz)], (0, -1, 0))
    return part


def triangles(part):
    return sum(len(f.verts) - 2 for f in part.bm.faces)


# --- Ausgabe ------------------------------------------------------------------------------------------------------

def write(name, tex, row, uses_all, make):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Portcullis")
    bpy.context.scene.collection.children.link(coll)
    check_palette(name, tex, row, uses_all)
    image = save_image(name, tex, os.path.join(OUT_DIR, name + ".png"))
    part = make()
    open_edges, multi = edge_report(part)
    assert open_edges == 0 and multi == 0, f"{name}: {open_edges} offene Kanten, {multi} mehrfach"
    tris = triangles(part)
    obj = part.build(coll, export_material(name, image), smooth=False)
    export_glb([obj], os.path.join(OUT_DIR, name + ".glb"))
    report_parts(name, [obj])
    print(f"TEXTUR {name} {tex.shape[1]}x{tex.shape[0]} Farben {colours_of(tex)}, {tris} Dreiecke")


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    write("portcullis", grate_atlas(), K2, True, build_grate)
    stein = stein_row()
    for variant in WALL_FILES:
        write("portcullis_guide_" + variant, guide_atlas(variant), stein, False, build_guide)
    write_sources(SOURCES, "tools/models/portcullis.py", "portcullis guide rings")


main()
