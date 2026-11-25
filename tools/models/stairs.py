"""Treppe zur naechsten Ebene (Issue #300): die Wendeltreppe aus Stein, je Wandvariante a, b, c.

Erzeuger fuer das Spiel. Uebergabe aus dem Probestueck treppe (Branch claude/design-probe-ghul-45a8c4, Commit 6ea274a,
design/proben/grafik/treppe/treppe.py), Entwurf 1 "Wendeltreppe", den der Mensch am 2026-11-17 gewaehlt hat. Bereinigt: nur
dieser Entwurf, Kacheln aus assets/textures/ (die das Spiel zeichnet), Werkstein aus der Reihe der Hauptpalette. Erzeugt nur
Modelle und Texturen nach assets/models/, keine Probebilder, dazu die Liste der gelesenen Kacheln
(tools/models/stairs_sources.sha256).

Aufruf (aus dem Repository-Wurzelverzeichnis), danach python tools/models/checksums.py:
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/stairs.py

Alles per Skript, nichts heruntergeladen. Spiel-Achsen (x seitlich, y oben, z vorn), Meter. Ursprung: Mitte am Boden.
Die Treppe steht im Kasten von Stairs (+-1,2 m, so hoch wie der Raum: 6 m, Issue #723; vorher 4 m). Seit Issue #765 (Entwurf b des
Auftraggebers, Probe design/proben/grafik/wendeltreppe-neu/): Sockel mit Fase, Spindel mit vier Ringwuelsten und Kapitell aus
Werkstein (5,5 bis 5,75) als ein Drehkoerper aus Achtecken; 20 schlanke Stufenplatten (0,15 m dick, 45 Grad, Anstieg 0,25 m, vorn
mit Trittnase, unten gefast, innen im Schaft verborgen); ein Eisengelaender (Vierkantstaebe 0,045 m an jeder Stufe, eckiger
Handlauf, Antrittspfosten mit Spitze) ab Stufe 4 bis Stufe 18 bei r = 0,93 m (die drei untersten Stufen bleiben frei, die zwei obersten
auch: das Gelaender endet bei 5,655 m, unter der Lampe bei 5,75; ein Test verlangt, dass der Stein dort endet, wo die Lampe beginnt); die Lampe (5,75 bis 6). Die Hoehe steht hier als HEIGHT und muss
RoomTemplate.RoomCeilingHeight entsprechen (ein Test prueft es).

Je Wandvariante v eine Datei stairs_spiral_<v>.glb mit zwei Knoten, beide fest und ohne Eltern:
  stairs   der Stein: Wandkachel der Variante auf den Seiten, Bodenkachel derselben Variante auf allen nach oben zeigenden
           Flaechen, Werkstein am Kapitell.
  glow     die Lampe oben (Messing/Gold, Verlauf von dunkel nach hell); das Spiel laesst nur sie pulsieren.
  Alle Koerper sind rundum geschlossen: jede Kante gehoert zu genau zwei Dreiecken (tools/models/check_closed.py); gestapelte
  Koerper teilen keine Ecken, sondern sind ein Ringstapel (tube).
Atlas 256 x 512 (Issue #416, Ausnahme der art-bible): Wandkachel 2 x 6 mal nebeneinander, Bodenkachel und Werkstein-Kachel je 2 x 2
mal (jede Kachel 64 x 64 = 1 m), dazu der Messing-Verlauf und das Eisen je 2 x 2 mal. Das Eisen ist die Reihe Eisen der Hauptpalette
(nicht das K2 der Fallgitter), dunkel gehalten: Stufe 1 zu 80 %, Stufe 2 zu 13 %, Stufe 0 zu 7 % der Pixel, mit fester Saat. Keine
neuen Farben: nur die der beiden Kacheln, die das Spiel ohnehin zeichnet, dazu Werkstein, Messing und Eisen. Weil die Kacheln im Spiel je um 24 Farben
haben, sind das zusammen gut 50 (das Probestueck hatte Kacheln mit 6 bis 8 Farben und kam auf 25); die Grenze von 32 Farben
der art-bible gilt hier deshalb nicht fuer den ganzen Atlas.

Steinflaechen bekommen ihre Textur-Koordinaten aus der Position wie SurfaceTexturing (eine 64-px-Kachel je Meter, u am
Wandverlauf, v mit der Hoehe). Sie werden nie geteilt (Issue #416): laeuft eine Flaeche ueber eine Metergrenze, laeuft die
Koordinate in die Kopie der Kachel im Atlas weiter. So hat die Treppe keine T-Stellen, jede Kante gehoert zu genau zwei
Dreiecken. Die Treppe steht auf einer Zellmitte (ungerade Meter) und
wird nicht gedreht, also laufen Fugen und Platten im Raster weiter. Aendert sich eine Kachel, passt die Treppe nicht mehr; ein
Test vergleicht stairs_sources.sha256 mit assets/textures/textures.sha256 und schlaegt fehl, bis dieses Skript neu gelaufen ist.
"""
import hashlib
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from psx import V, G, Part, save_image, export_material, export_glb, report_parts  # noqa: E402

TOOLS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(TOOLS))
OUT_DIR = os.path.join(ROOT, "assets", "models")
TEXTURES = os.path.join(ROOT, "assets", "textures")
PALETTE = os.path.join(ROOT, "assets", "palette", "hauptpalette.json")
SOURCES = os.path.join(TOOLS, "stairs_sources.sha256")

W, H = 256, 512
# Atlasfelder in Pixeln (y von unten); jedes Feld ist die Kachel mehrfach nebeneinander, damit keine Flaeche an einer Metergrenze
# geteilt werden muss (emit): Wand 2 x 6 Kacheln (die Spindel ist 5,25 m hoch), Boden und Werkstein 2 x 2, Lampe 2 x 2 Verlaeufe.
REGIONS = {"stone": (0, 128, 128, 512), "glow": (0, 0, 128, 64), "floor": (128, 0, 256, 128), "werk": (128, 128, 256, 256),
           "iron": (128, 256, 256, 384)}
TILES = {"stone": (64, 64), "floor": (64, 64), "werk": (64, 64), "glow": (64, 32), "iron": (64, 64)}                # Pixel je Kachel (= je Meter)
UV = {}                                                                             # Part verlangt die Rechtecke, emit nimmt REGIONS
HEIGHT = 6.0                                                                        # = RoomTemplate.RoomCeilingHeight = Stairs.Height
PLATE = 0.15                                                                        # Dicke der Stufenplatten (Anstieg 0,25: 0,1 m Luft zwischen zwei Platten)
IRON_MIX = ((1, 0.80), (2, 0.93), (0, 1.0))                                         # Reihe Eisen: Stufe 1 (dunkel) zu 80 %, Stufe 2 zu 13 %, Stufe 0 zu 7 % der Pixel
STEPS = 20                                                                          # 0,5 + 0,25 * 19 = 5,25: die letzte Stufe liegt 0,25 unter dem Kapitell, wie vorher
WALL = {"a": "catacomb_wall.png", "b": "catacomb_wall_b.png", "c": "catacomb_wall_c.png"}
FLOOR = {"a": "catacomb_floor.png", "b": "catacomb_floor_b.png", "c": "catacomb_floor_c.png"}


def palette_rows():
    """Werkstein (4 Stufen), Messing/Gold und Eisen aus assets/palette/hauptpalette.json."""
    with open(PALETTE, encoding="utf-8") as f:
        data = json.load(f)
    rows = {r["name"]: r["farben"] for r in data["grund"] + data["akzent"]}
    return rows["Werkstein"], rows["Messing/Gold"], rows["Eisen"]


def load(name):
    """Kachel als ganze Zahlen 0..255, Zeile 0 oben (wie das Bild aussieht)."""
    im = bpy.data.images.load(os.path.join(TEXTURES, name))
    w, h = im.size
    return np.round(np.array(im.pixels[:]).reshape(h, w, 4)[::-1, :, :3] * 255).astype(int)


def werk_of(tile, werkstein):
    """Dieselbe Kachel in den vier Stufen der Reihe Werkstein (nach der Helligkeit der Farben in Viertel geteilt)."""
    uniq = sorted({tuple(c) for c in tile.reshape(-1, 3)}, key=lambda c: np.dot(c, [0.3, 0.59, 0.11]))
    rank = {c: min(3, int(4 * i / len(uniq))) for i, c in enumerate(uniq)}
    return np.array([werkstein[rank[tuple(c)]] for c in tile.reshape(-1, 3)]).reshape(tile.shape)


def atlas(variant, werkstein, messing, eisen):
    wall, floor = load(WALL[variant]), load(FLOOR[variant])
    a = np.zeros((H, W, 3))
    for name, t in (("stone", wall), ("floor", floor), ("werk", werk_of(wall, werkstein))):
        x0, y0, x1, y1 = REGIONS[name]
        a[y0:y1, x0:x1] = np.tile(t[::-1], ((y1 - y0) // 64, (x1 - x0) // 64, 1))   # Zeile 0 unten
    x0, y0, x1, y1 = REGIONS["glow"]
    for row in range(y0, y1):                                                       # Verlauf von dunkel (unten) nach hell (oben), je 32 Zeilen
        a[row, x0:x1] = messing[3 + min(4, ((row - y0) % 32) * 5 // 32)]
    x0, y0, x1, y1 = REGIONS["iron"]
    rng = np.random.RandomState(7)                                                  # feste Saat: zwei Laeufe geben dieselbe Datei
    for yy in range(y0, y1):                                                        # Eisen: Koernung aus drei Stufen der Reihe
        for xx in range(x0, x1):
            r = rng.rand()
            a[yy, xx] = eisen[next(step for step, limit in IRON_MIX if r < limit)]
    used = {tuple(p) for p in a.reshape(-1, 3).astype(int)}
    allowed = {tuple(c) for t in (wall, floor) for c in t.reshape(-1, 3)} | {tuple(c) for c in werkstein} | {tuple(c) for c in messing} | {tuple(c) for c in eisen}
    allowed.add((0, 0, 0))                                                          # die ungenutzten Felder des Atlas
    assert used <= allowed, f"{variant}: Farben ausserhalb von Kacheln, Werkstein, Messing und Eisen: {sorted(used - allowed)}"
    return a, len(used)


# --- Steinflaechen mit Welt-UV (wie die Tuerrahmen in door.py) ---------------------------------------------------------------

def emit(part, pts, hint, field, fit=False):
    """Konvexe ebene Flaeche mit Normale nach `hint`; Waende: u am Wandverlauf, v mit der Hoehe; nach oben zeigende Steinflaechen
    nehmen die Bodenkachel (u = x, v = z). Eine Kachel je Meter. Die Flaeche wird nie geteilt: laeuft sie ueber eine Metergrenze,
    liegt die Kachel im Atlas mehrfach nebeneinander (REGIONS), und die Textur-Koordinate laeuft ueber die Kopie weiter. So
    haengt jede Kante an genau zwei Dreiecken (Regel 21) und die Treppe braucht keine Schnitt-Dreiecke. `fit=True` (verdeckte
    Flaechen: Unterseite auf dem Boden, Kappen der Spindel) legt die Flaeche ohne Raster auf eine Kachel."""
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
    ax = max(range(3), key=lambda i: abs(n[i]))
    if ax == 2:
        pu, pv = (lambda p: p.x if n.z > 0 else -p.x), (lambda p: p.y)
    elif ax == 0:
        pu, pv = (lambda p: -p.z if n.x > 0 else p.z), (lambda p: p.y)
    else:
        pu, pv = (lambda p: p.x if n.y > 0 else -p.x), (lambda p: p.z)
        if n.y > 0 and field == "stone":
            field = "floor"
    x0, y0, x1, y1 = REGIONS[field]
    tw, th = TILES[field]
    us, vs = [pu(p) for p in P], [pv(p) for p in P]
    if fit:
        i0, j0 = min(us), min(vs)
        su, sv = tw / max(max(us) - i0, 1e-6), th / max(max(vs) - j0, 1e-6)
        su = sv = min(su, sv, tw)
    else:
        i0, j0 = math.floor(min(us) + 1e-9), math.floor(min(vs) + 1e-9)
        su, sv = tw, th
        assert max(us) - i0 <= (x1 - x0) / tw + 1e-9 and max(vs) - j0 <= (y1 - y0) / th + 1e-9, f"{field}: Flaeche passt nicht in die Kachelreihe"
    uvs = [((x0 + min(max((u - i0) * su, 0.02), x1 - x0 - 0.02)) / W, (y0 + min(max((v - j0) * sv, 0.02), y1 - y0 - 0.02)) / H)
           for u, v in zip(us, vs)]
    part._face([part.bm.verts.new(G(p - part.pivot)) for p in P], uvs)


def octagon(circ, rot=22.5):
    return [(circ * math.cos(math.radians(rot + 45 * k)), circ * math.sin(math.radians(rot + 45 * k))) for k in range(8)]


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


def rail_iron(part, first=3, last=17):
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
    ca, sa = math.cos(math.radians(a - 22.5)), math.sin(math.radians(a - 22.5))
    rings = []
    for y, k in ((base - 0.03, 1.0), (base + 1.05, 1.0), (base + 1.10, 0.6), (base + 1.16, 0.25)):   # Vierkant, oben zur Spitze verjuengt
        s = 0.045 * k
        rings.append([(x + dx * ca - dz * sa, y, z + dx * sa + dz * ca) for dx, dz in ((-s, -s), (s, -s), (s, s), (-s, s))])
    tube(part, rings, "iron", fit_bottom=True)


def spiral():
    st, gl = Part("stairs", (0, 0, 0), UV), Part("glow", (0, 0, 0), UV)
    spindle(st)
    for i in range(STEPS):
        tread(st, i)
    rail_iron(st)
    tube(gl, [oct_ring(0.3125, HEIGHT - 0.25), oct_ring(0.3125, HEIGHT)], "glow")   # Lampe
    return [st, gl]


def build(variant, werkstein, messing, eisen):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Treppe")
    bpy.context.scene.collection.children.link(coll)
    name = f"stairs_spiral_{variant}"
    tex, colours = atlas(variant, werkstein, messing, eisen)
    image = save_image(name, tex, os.path.join(OUT_DIR, name + ".png"))
    mat = export_material(name, image)
    objs = [p.build(coll, mat, smooth=False) for p in spiral()]
    export_glb(objs, os.path.join(OUT_DIR, name + ".glb"))
    report_parts(name, objs)
    print(f"TEXTUR {name} {W}x{H} Farben {colours}")


def write_sources():
    """SHA-256 und Groesse jeder gelesenen Kachel (wie door_frame_sources.sha256)."""
    lines = ["# sha256 size name - the wall and floor textures tools/models/stairs.py built the stairs from"]
    for name in sorted(list(WALL.values()) + list(FLOOR.values())):
        with open(os.path.join(TEXTURES, name), "rb") as f:
            data = f.read()
        lines.append(f"{hashlib.sha256(data).hexdigest()} {len(data)} {name}")
    with open(SOURCES, "w", encoding="ascii", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("Quellen:", os.path.relpath(SOURCES, ROOT))


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    werkstein, messing, eisen = palette_rows()
    for variant in "abc":
        build(variant, werkstein, messing, eisen)
    write_sources()


main()
