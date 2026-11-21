"""Wandpfeiler (Issue #536, Baustein aus der Formenlehre Gotik #281): Pfeiler vor der Wand fuer Gang (3,5 m) und Raum (6 m).

Erzeuger fuer das Spiel. Uebergabe aus dem Probestueck design/proben/grafik/wandpfeiler-536/wandpfeiler.py (Branch
feat/wandpfeiler-probe-536): gleiche Geometrie, nur die Fassungen "raum" (hier room) und "gang" (corridor). Anders als das
Probestueck liest der Erzeuger die auf die Hauptpalette umgerechneten Waende (#542); darum hebt er die Wandstufen nur um
LIFT = 2 statt 5 und prueft, dass die Fugen sichtbar bleiben (check_joints). Die
tragende Fassung "gang_traeger" baut ceiling_arch.py (#537); Profil, Texturen und Welt-UV teilen sich beide in
pillar_kit.py. Erzeugt nur Modelle und Texturen nach assets/models/, keine
Probebilder, dazu die Liste der gelesenen Wand-Texturen (tools/models/wall_pillar_sources.sha256).

Aufruf (aus dem Repository-Wurzelverzeichnis), danach python tools/models/checksums.py:
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/wall_pillar.py

Alles per Skript, nichts heruntergeladen. Spiel-Achsen (x seitlich, y oben, z vorn), Meter. Ursprung: Mitte des Pfeilers
auf dem Boden, Wandflaeche z = 0, der Pfeiler steht nach +z vor; die Rueckseite liegt 1/8 m in der Wand (BACK).

Dateien: wall_pillar_<fassung>_<v>.glb und .png, Fassung room oder corridor, v die Wandvariante a, b, c. Ein Knoten
"pillar", ein Netz, rundum geschlossen. Atlas 128 x 64: links die echte Wand-Textur der Variante (bitgleich, nur gelesen),
rechts dieselbe Kachel um LIFT Stufen der Reihe Stein heller, oben gekappt (pillar_tile); der Pfeiler nutzt nur das
rechte Feld.

  room      Sockel 0 bis 0,5, Schaft bis 5,5 (Tiefe 0,25), Kapitell 5,5 bis 6,0; Sockel und Kapitell Tiefe 0,3125
  corridor  Schaft Tiefe 0,0625, Sockel und Kapitell 0,125; Kapitell 3,0 bis 3,5
Schaft 0,5 breit, Sockel und Kapitell 0,625 breit.

Textur-Koordinaten aus der Weltposition wie SurfaceTexturing (eine 64-px-Kachel je Meter, an den Metergrenzen geteilt): steht
die Pfeilermitte auf einer ganzen Meterzahl entlang der Wand (im Spiel jede Zellmitte), laufen die Fugen im Raster der Wand
weiter. Aendert sich eine Wand-Textur oder die Palette, passt der Pfeiler nicht mehr; ein Test vergleicht
wall_pillar_sources.sha256 mit assets/textures/textures.sha256 und schlaegt fehl, bis dieses Skript neu gelaufen ist.
Zwei Laeufe liefern gleiche Dateien (kein Zufall).
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from psx import rects_from_pixels, save_image, export_material, export_glb, report_parts  # noqa: E402
from pillar_kit import (OUT_DIR, TOOLS, WALL_FILES, build_pillar, colours_of, edge_report, stein_row,  # noqa: E402
                        wall_and_pillar, write_sources)

SOURCES = os.path.join(TOOLS, "wall_pillar_sources.sha256")
FRAME_SIZE = (128, 64)
FRAME_RECTS = {"stone": (0, 0, 64, 64), "pillar": (64, 0, 128, 64)}
FRAME_UV = rects_from_pixels(FRAME_RECTS, FRAME_SIZE)

# Fassung: (Schafttiefe, Kapitell-Unterkante); Oberkante = Kapitell-Unterkante + 0,5 = Deckenhoehe
SIZES = {
    "room": (0.25, 5.5),
    "corridor": (0.0625, 3.0),
}


def atlas(variant, stein):
    tile, lifted = wall_and_pillar(variant, stein)
    w, h = FRAME_SIZE
    out = np.zeros((h, w, 3))
    for name, t in (("stone", tile), ("pillar", lifted)):
        x0, y0, x1, y1 = FRAME_RECTS[name]
        out[y0:y1, x0:x1] = t[::-1]                                 # Feld: Zeile 0 unten
    return out


# --- Ausgabe ------------------------------------------------------------------------------------------------------

def write(size, variant, stein):
    name = f"wall_pillar_{size}_{variant}"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Pillar")
    bpy.context.scene.collection.children.link(coll)
    tex = atlas(variant, stein)
    assert colours_of(tex) <= 40, f"{name}: {colours_of(tex)} Farben (hoechstens 40)"
    image = save_image(name, tex, os.path.join(OUT_DIR, name + ".png"))
    part = build_pillar(*SIZES[size], FRAME_UV)
    open_edges, multi = edge_report(part)
    assert open_edges == 0 and multi == 0, f"{name}: {open_edges} offene Kanten, {multi} mehrfach"
    obj = part.build(coll, export_material(name, image), smooth=False)
    export_glb([obj], os.path.join(OUT_DIR, name + ".glb"))
    report_parts(name, [obj])
    print(f"TEXTUR {name} {FRAME_SIZE[0]}x{FRAME_SIZE[1]} Farben {colours_of(tex)}")


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    stein = stein_row()
    for size in SIZES:
        for variant in WALL_FILES:
            write(size, variant, stein)
    write_sources(SOURCES, "tools/models/wall_pillar.py", "wall pillars")


main()
