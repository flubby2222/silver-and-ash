"""Throwing Knives (Issue #204), Runde 5: die zwei Dateien fuer Teil 2.

Abgenommen ist die Haltung s12 (Tiefe 12 mm). Dort passt das ganze Messer nicht in den Aermel (tiefe-zahlen.txt), also
gibt es zwei Modelle desselben Messers e "Schwarzruecken" mit kurzem Band:

  messer-e-geschoss.glb   das ganze Messer (196 Dreiecke): Wurfgeschoss und Beute. Inhalt wie
                          ../runde-4/messer-e-kurzband.glb; das Skript prueft, dass beide Dateien Byte fuer Byte gleich sind.
  messer-e-hand-s12.glb   Handmodell: dasselbe Messer, 47 mm hinter dem Klingenansatz abgeschnitten (35 mm hinter dem
                          Haltepunkt bei Tiefe 12 mm), Schnitt verschlossen. Nur fuer die Hand im Aermel.

Bedingung fuer Teil 2: Das Handmodell wird nie aus der Manschette gezogen. Beim Loslassen ersetzt das Geschoss es,
beim Nachladen erscheint es im Aermel.

Beide Dateien entstehen aus ../runde-2/messer3.py mit den kurzen Bandenden aus ../runde-4/messer3b.py; fuer das
Handmodell wird das fertige Netz vor dem Schreiben geschnitten. Die Textur ist in beiden dieselbe (textur3b.png).
Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python handmodell.py
"""

import os
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
PROBE = os.path.dirname(HERE)
sys.path.insert(0, PROBE)
sys.path.insert(0, os.path.join(PROBE, "runde-2"))
sys.path.insert(0, os.path.join(PROBE, "runde-4"))
import bmesh  # noqa: E402
import messer3 as M  # noqa: E402
import messer3b as K  # noqa: E402
from mathutils import Vector  # noqa: E402

DEPTH = 0.012                       # Tiefe der Haltung s12
BEHIND = 0.035                      # Schnitt so weit hinter dem Haltepunkt
CUT = DEPTH + BEHIND                # = 47 mm hinter dem Klingenansatz (Skriptmass)

_export_glb = M.export_glb


def cut(obj):
    """Alles hinter dem Schnitt entfernen (Spitze liegt in -Y, die Datei ist 1,8-fach), Schnitt verschliessen."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
    res = bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=Vector((0, CUT * M.SCALE, 0)),
                                 plane_no=Vector((0, 1, 0)), clear_outer=True)
    y = CUT * M.SCALE
    verts = [v for v in res["geom_cut"] if isinstance(v, bmesh.types.BMVert)]
    bmesh.ops.remove_doubles(bm, verts=verts, dist=1e-5)       # doppelte Ecken der Texturnaehte am Schnitt verbinden
    edges = [e for e in bm.edges if all(abs(v.co.y - y) < 1e-5 for v in e.verts)]
    assert edges, "Schnitt trifft das Messer nicht"
    print("HANDMODELL Schnittkanten", len(edges), "Flaechen je Kante", sorted(len(e.link_faces) for e in edges))
    bmesh.ops.holes_fill(bm, edges=[e for e in edges if len(e.link_faces) == 1], sides=0)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    count = {}                      # Kanten nach Lage zaehlen: das Netz hat an Texturnaehten doppelte Ecken
    for f in bm.faces:
        p = [tuple(round(c, 5) for c in v.co) for v in f.verts]
        for a, b in zip(p, p[1:] + p[:1]):
            count[tuple(sorted((a, b)))] = count.get(tuple(sorted((a, b))), 0) + 1
    odd = sum(1 for n in count.values() if n % 2)
    print(f"HANDMODELL Kanten {len(count)}, davon mit ungerader Flaechenzahl (offen) {odd}")
    assert odd == 0, "Handmodell nicht geschlossen"
    bm.to_mesh(obj.data)
    bm.free()
    return len(obj.data.polygons)


def write(name, hand):
    def export_glb(objs, path):
        if hand:
            print(f"HANDMODELL {name}: {cut(objs[0])} Dreiecke, Schnitt {CUT * 1000:.0f} mm hinter dem Klingenansatz")
        _export_glb(objs, path)

    M.sweep = K.sweep
    M.HERE = HERE
    M.export_glb = export_glb
    M.export()
    os.replace(os.path.join(HERE, "messer-e-schwarzruecken.glb"), os.path.join(HERE, name))
    os.remove(os.path.join(HERE, "textur3.png"))        # gleicher Inhalt wie ../runde-4/textur3b.png
    with open(os.path.join(HERE, name), "rb") as f:
        return f.read()


def main():
    whole = write("messer-e-geschoss.glb", hand=False)
    with open(os.path.join(PROBE, "runde-4", "messer-e-kurzband.glb"), "rb") as f:
        assert f.read() == whole, "Geschoss weicht von runde-4/messer-e-kurzband.glb ab"
    hand = write("messer-e-hand-s12.glb", hand=True)
    assert write("messer-e-hand-s12.glb", hand=True) == hand, "zwei Laeufe, zwei verschiedene Dateien"
    print(f"HANDMODELL geschrieben: Geschoss {len(whole)} Byte (gleich runde-4/messer-e-kurzband.glb), "
          f"Hand {len(hand)} Byte, zweimal erzeugt und Byte fuer Byte gleich")


if __name__ == "__main__":
    main()
