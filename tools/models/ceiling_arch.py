"""Deckenbogen (Issue #537, Baustein aus der Formenlehre Gotik #281): Gurtbogen quer ueber den Gang auf zwei Pfeilern.

Erzeuger fuer das Spiel. Uebergabe aus dem Probestueck deckenbogen.py auf dem Branch feat/deckenbogen-probe-537 (Stand 774ce69): gleiche
Geometrie (gedrueckter Spitzbogen, sechs Keilsteine je Haelfte, Zaehne an Fuss und Schlussstein) und dieselben tragenden
Pfeiler (Fassung "gang_traeger"). Anders als das Probestueck liest der Erzeuger die auf die Hauptpalette umgerechneten
Waende (#542) und teilt Pfeiler-Profil, Wand-Kachel, aufgehellte Kachel und Welt-UV mit wall_pillar.py (pillar_kit.py).
Erzeugt nur Modelle und Texturen nach assets/models/, keine Probebilder, dazu die Liste der gelesenen Wand-Texturen
(tools/models/ceiling_arch_sources.sha256).

Aufruf (aus dem Repository-Wurzelverzeichnis), danach python tools/models/checksums.py:
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/ceiling_arch.py

Alles per Skript, nichts heruntergeladen. Spiel-Achsen (x seitlich, y oben, z vorn), Meter. Ursprung: Mitte des Gangs auf
dem Boden, in der Ebene des Bogens; der Gang verlaeuft entlang z, die Waende stehen bei x = -1 und x = +1 (ein 2 m breiter
Gang, eine Zelle), die Decke liegt bei y = 3,5.

Dateien: ceiling_arch_<v>.glb und .png, v die Wandvariante a, b, c. Drei Knoten, je ein Netz, jedes rundum geschlossen:
  arch          Ring 1/8 dick und 0,5 tief, Kaempfer 2,0, innen R 1,5 (lichte Weite 1,75, Scheitel 3,364), aussen R 1,625;
                Mittelpunkte 0,625 neben der Achse. Sechs Keilsteine je Haelfte (radiale Fugen alle 11 Grad), der
                Schlussstein durchgehend ueber den Scheitel. Fuss und Schlussstein aussen auf R 1,75 aufgeweitet: die Zaehne
                (1/8 m) stecken in Wand und Decke.
  pillar_left   Pfeiler "gang_traeger" an der Wand x = -1, Blick nach +x: Schaft 0,0625 tief, Kapitell 1,5 bis 2,0.
  pillar_right  derselbe an der Wand x = +1, Blick nach -x.
Atlas 128 x 128: unten links die echte Wand-Kachel (bitgleich, nur gelesen), daneben die um LIFT Stufen hellere Pfeiler-
Kachel (wie wall_pillar.py), darueber je Keilstein ein Stueck der Pfeiler-Kachel fuer Laibung (I) und Stirn (S), jeder
zweite Stein eine Stufe dunkler, mit Fugen in der dunkelsten Stufe der Wand.

Die Pfeiler tragen Textur-Koordinaten aus der Weltposition (eine 64-px-Kachel je Meter): steht die Bogenebene auf einer
ganzen Meterzahl entlang des Gangs (im Spiel jede Zellmitte), laufen ihre Fugen im Raster der Wand. Ein Test vergleicht
ceiling_arch_sources.sha256 mit assets/textures/textures.sha256. Zwei Laeufe liefern gleiche Dateien (kein Zufall).
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix  # noqa: E402
from psx import V, G, Part, rects_from_pixels, save_image, export_material, export_glb, report_parts  # noqa: E402
from pillar_kit import (LIFT, MIN_JOINT, MIN_STEPS, OUT_DIR, TOOLS, WALL_FILES, build_pillar, colours_of,  # noqa: E402
                        edge_report, palette_rows, steps_of, stein_row, wall_and_pillar, write_sources)

SOURCES = os.path.join(TOOLS, "ceiling_arch_sources.sha256")

# --- Masse -----------------------------------------------------------------------------------------------------------
CX = 0.625              # Mittelpunkt der Bogenhaelfte neben der Achse
Y0 = 2.0                # Kaempferlinie = Oberkante des Kapitells
R_IN = 1.5
R_OUT = 1.625
R_TOOTH = 1.75          # aufgeweiteter Aussenradius an Fuss und Schlussstein
HALF_DEPTH = 0.25       # Bogen 0,5 tief
CHAMFER = 0.0625
STEP_DEG = 11.0
STONES = 6              # Keilsteine je Haelfte
CORRIDOR_WIDTH, CORRIDOR_HEIGHT = 2.0, 3.5
PILLAR = (0.0625, 1.5)  # Fassung gang_traeger: Schafttiefe, Kapitell-Unterkante (Oberkante = Kaempfer)
MIN_CLEAR = 3.25        # lichte Hoehe in der Gangmitte mindestens

# Profil des Rings (r, z): Laibung mit Fase 1/16, Seitenflaechen, Ruecken
PROFILE = [(R_IN, HALF_DEPTH - CHAMFER), (R_IN + CHAMFER, HALF_DEPTH), (R_OUT, HALF_DEPTH),
           (R_OUT, -HALF_DEPTH), (R_IN + CHAMFER, -HALF_DEPTH), (R_IN, -(HALF_DEPTH - CHAMFER))]
OUTER_IDX = (2, 3)
EDGE_KIND = ["I", "S", "I", "S", "I", "I"]      # I: Lauf- und Fasenflaeche, S: Seitenflaeche (Stirn des Rings)
TOOTH_AT = {0, STONES}                          # Ringe mit aufgeweitetem Aussenradius

# --- Atlas -----------------------------------------------------------------------------------------------------------
ATLAS_SIZE = (128, 128)
PIX = {"stone": (0, 0, 64, 64), "pillar": (64, 0, 128, 64)}
SW, SH_I, SH_S = 20, 32, 16
for _j in range(STONES):
    PIX["I%d" % _j] = (SW * _j, 64, SW * _j + SW, 64 + SH_I)
    PIX["S%d" % _j] = (SW * _j, 96, SW * _j + SW, 96 + SH_S)
UV = rects_from_pixels(PIX, ATLAS_SIZE)
PARTS = ("arch", "pillar_left", "pillar_right")


def stone_patches(lifted, stein, mortar):
    """Je Keilstein ein Stueck der Pfeiler-Kachel (I: 20 x 32, S: 20 x 16), Zeile 0 oben, mit Fugen an den Stoessen."""
    patches = {}
    for j in range(STONES):
        oy, ox = (11 * j + 5) % 64, (17 * j + 9) % 64
        rolled = np.roll(lifted, (-oy, -ox), (0, 1))
        for kind, rows, y_off in (("I", SH_I, 0), ("S", SH_S, SH_I)):
            patch = rolled[y_off:y_off + rows, :SW].copy()
            if j % 2 == 1:                               # jeder zweite Stein eine Stufe dunkler: Steine lesen sich einzeln
                idx = steps_of(patch, stein).reshape(patch.shape[:2])
                patch = stein[np.maximum(idx - 1, 0)]
            patch[:, :2] = mortar                        # Fuge 3 px (2 + 1 vom Nachbarn), wie die Fugen der Wand
            if j < STONES - 1:                           # der Schlussstein hat keine Fuge am Scheitel
                patch[:, -1] = mortar
            patches["%s%d" % (kind, j)] = patch
    return patches


def check_stones(name, patches, stein, mortar_step):
    """Fugen der Keilsteine sichtbar: zusammen mindestens MIN_STEPS Stufen, und die Fuge liegt mindestens MIN_JOINT Stufen
    unter der Flaeche (Median) jedes Steins."""
    steps = set()
    for key, patch in patches.items():
        idx = steps_of(patch, stein)
        steps |= set(idx.tolist())
        face = np.percentile(idx, 50, method="lower")
        assert face - mortar_step >= MIN_JOINT, \
            f"{name} {key}: Fuge {mortar_step} zu nah an der Flaeche {face} (mindestens {MIN_JOINT})"
    print(f"STEINE {name}: Stufen {len(steps)}, Fuge {mortar_step}")
    assert len(steps) >= MIN_STEPS, f"{name}: Keilsteine haben nur {len(steps)} Stein-Stufen (mindestens {MIN_STEPS})"


def check_palette(name, tex, stein):
    """Nur Farben der Hauptpalette; alles, was Bogen und Pfeiler zeigen, aus der Reihe Stein."""
    palette = {tuple(int(c) for c in p) for row in palette_rows().values() for p in row}
    row = {tuple(int(c) for c in p) for p in stein}
    cols = {tuple(int(c) for c in p) for p in np.round(tex).reshape(-1, 3)}
    assert cols <= palette, f"{name}: {len(cols - palette)} Farben ausserhalb der Hauptpalette"
    for field, (x0, y0, x1, y1) in PIX.items():
        if field == "stone":
            continue
        used = {tuple(int(c) for c in p) for p in np.round(tex[y0:y1, x0:x1]).reshape(-1, 3)}
        assert used <= row, f"{name}: Feld {field} hat {len(used - row)} Farben ausserhalb der Reihe Stein"


def atlas(variant, stein):
    tile, lifted = wall_and_pillar(variant, stein)
    w, h = ATLAS_SIZE
    out = np.tile(stein[0].astype(float), (h, w, 1))
    out[0:64, 0:64] = tile[::-1]                                    # Feld: Zeile 0 unten
    out[0:64, 64:128] = lifted[::-1]
    mortar_step = min(steps_of(tile, stein).tolist())               # die dunkelste Stufe der Wand: ihre Fugen
    mortar = stein[min(mortar_step + LIFT - 2, len(stein) - 1)]
    patches = stone_patches(lifted, stein, mortar)
    check_stones(variant, patches, stein, mortar_step + LIFT - 2)
    for key, patch in patches.items():
        x0, y0, x1, y1 = PIX[key]
        out[y0:y1, x0:x1] = patch[::-1]
    return out


# --- Bogen -----------------------------------------------------------------------------------------------------------

def ring_points(k):
    """Ecken des Profils an Ring k der linken Haelfte: (x, y, z, r). Ring STONES liegt auf der Mittelebene x = 0."""
    pts = []
    for i, (r0, z) in enumerate(PROFILE):
        r = R_TOOTH if (i in OUTER_IDX and k in TOOTH_AT) else r0
        if k < STONES:
            phi = math.radians(STEP_DEG * k)
            x = CX - r * math.cos(phi)
        else:
            phi = math.acos(CX / r)
            x = 0.0
        pts.append((x, Y0 + r * math.sin(phi), z, r))
    return pts


def build_arch():
    """Ein geschlossenes Netz: zwei gespiegelte Haelften aus Keilsteinen, am Scheitel ohne Naht verbunden."""
    part = Part("arch", (0, 0, 0), UV)
    verts = {}

    def vert(p):
        key = tuple(int(round(c * 1e5)) for c in p)
        if key not in verts:
            verts[key] = part.bm.verts.new(G(V(*p)))
        return verts[key]

    def uvp(rect, u, v):
        u0, v0, u1, v1 = UV[rect]
        return (u0 + (u1 - u0) * u, v0 + (v1 - v0) * v)

    def vcoord(kind, r, z):
        return (z + HALF_DEPTH) / (2 * HALF_DEPTH) if kind == "I" else (r - R_IN) / (R_TOOTH - R_IN)

    for sgn in (-1, 1):
        def pos(p):
            return vert((p[0] * (-sgn), p[1], p[2]))

        for k in range(STONES):
            a, b = ring_points(k), ring_points(k + 1)
            for e in range(6):
                f = (e + 1) % 6
                kind = EDGE_KIND[e]
                rect = "%s%d" % (kind, k)
                corners = [(a[e], 0.0), (a[f], 0.0), (b[f], 1.0), (b[e], 1.0)]
                vs = [pos(c) for c, _ in corners]
                uvs = [uvp(rect, u, vcoord(kind, c[3], c[2])) for c, u in corners]
                part._face(vs, uvs)
        cap = ring_points(0)                                           # Fuss: Flaeche auf dem Kapitell
        mid = uvp("I0", 0.5, 0.5)
        part._face([pos(c) for c in cap], [mid] * 6)
    bmesh.ops.recalc_face_normals(part.bm, faces=part.bm.faces[:])
    return part


def build_side_pillar(side):
    """Pfeiler gang_traeger, gedreht an die Gangwand (side -1 links, +1 rechts), Wandflaeche bei x = side * 1. Die
    Textur-Koordinaten entstehen vor dem Drehen (Pfeilermitte bei 0), darum liegen die Metergrenzen auf der Bogenebene."""
    part = build_pillar(*PILLAR, UV, name="pillar_left" if side < 0 else "pillar_right")
    rot = Matrix.Rotation(math.radians(90 if side < 0 else -90), 4, "Z")
    part.bm.transform(Matrix.Translation((side * CORRIDOR_WIDTH / 2, 0.0, 0.0)) @ rot)
    return part


def clear_height(x):
    """Lichte Hoehe unter dem Bogenring an der Stelle x (Laibung, Radius 1,5)."""
    cx = CX if x <= 0 else -CX                          # Mittelpunkt der gegenueberliegenden Haelfte
    return Y0 + math.sqrt(R_IN ** 2 - (x - cx) ** 2)


def check_fit():
    """Masse gegen den Gang: der Fuss sitzt buendig auf dem Kapitell, die Zaehne stecken in Wand und Decke, der Bogen
    laesst den Gang frei."""
    foot = ring_points(0)
    crown = ring_points(STONES)
    half = CORRIDOR_WIDTH / 2
    capital_front = half - (PILLAR[0] + 0.0625)
    assert abs(PILLAR[1] + 0.5 - Y0) < 1e-9, "Kapitell-Oberkante nicht auf der Kaempferlinie"
    assert abs(-foot[0][0] - capital_front) < 1e-9, f"Laibung {foot[0][0]:.4f} nicht buendig mit dem Kapitell"
    assert min(p[0] for p in foot) < -half, "Fuss-Zahn steckt nicht in der Wand"
    assert max(p[1] for p in crown) > CORRIDOR_HEIGHT, "Schlussstein-Zahn steckt nicht in der Decke"
    assert clear_height(0.0) >= MIN_CLEAR, f"lichte Hoehe {clear_height(0.0):.3f} unter {MIN_CLEAR}"
    print("LICHT Mitte %.3f, 0,35 neben der Wand %.3f, Kaempfer-Weite %.3f" % (
        clear_height(0.0), clear_height(-(half - 0.35)), 2 * (R_IN - CX)))


# --- Ausgabe ------------------------------------------------------------------------------------------------------

def write(variant, stein):
    name = f"ceiling_arch_{variant}"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Arch")
    bpy.context.scene.collection.children.link(coll)
    tex = atlas(variant, stein)
    check_palette(name, tex, stein)
    assert colours_of(tex) <= 40, f"{name}: {colours_of(tex)} Farben (hoechstens 40)"
    image = save_image(name, tex, os.path.join(OUT_DIR, name + ".png"))
    material = export_material(name, image)
    objs = []
    for part in [build_arch(), build_side_pillar(-1), build_side_pillar(1)]:
        open_edges, multi = edge_report(part)
        assert open_edges == 0 and multi == 0, f"{name} {part.name}: {open_edges} offene Kanten, {multi} mehrfach"
        objs.append(part.build(coll, material, smooth=False))
    assert tuple(o.name for o in objs) == PARTS
    export_glb(objs, os.path.join(OUT_DIR, name + ".glb"))
    report_parts(name, objs)
    print(f"TEXTUR {name} {ATLAS_SIZE[0]}x{ATLAS_SIZE[1]} Farben {colours_of(tex)}")


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    check_fit()
    stein = stein_row()
    for variant in WALL_FILES:
        write(variant, stein)
    write_sources(SOURCES, "tools/models/ceiling_arch.py", "ceiling arches")


main()
