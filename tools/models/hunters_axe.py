"""Jagdaxt des Jaegers (Hunter's Axe, Stufe Royal), Fassung C3 "Schartig" aus dem Probestueck zu Issue #201

Erzeuger fuer das Spiel (Issue #776): ein Knoten "axe" mit Drehpunkt am Griff, Stiel entlang +z, Schneide nach -x,
Massstab 1,8 eingebacken, Textur 128 x 128 ohne Filterung, flach schattiert. Kein Aermel: das Spiel nimmt den Aermel der
Keule (club_hand.glb), wie die Probe ihn fuer die Ich-Sicht dazugestellt hat; es entsteht keine neue Hand.

Das Skript kopiert die Formen nicht, es laedt sie aus dem abgenommenen Probestueck
design/proben/grafik/hunters-axe-201/runde-2/axt2.py (das seinerseits runde-1/axt.py nutzt) und baut nur C3. Bevor es
etwas schreibt, baut es C3 einmal genau so wie die Probe und vergleicht das Ergebnis Byte fuer Byte mit der abgenommenen
Datei runde-2/axt-c3-schartig.glb; weicht es ab, bricht es ab. Erst danach entsteht die Spieldatei. Sie unterscheidet sich
von der Probe im Namen des Bildes und des Materials ("hunters_axe" statt "axe_c3") und nur um das im Aermel verborgene
Griffende: Der Knauf faellt weg (Entscheidung der Leitung zu #776, vorlaeufig), der Stiel endet dann wie bei der Keule
etwa 0,10 m hinter dem Griff, mit seiner geschlossenen Stirnflaeche. Sonst ragte der Knauf links unten aus dem Aermel.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/hunters_axe.py

Schreibt assets/models/hunters_axe.glb und assets/models/hunters_axe.png. Danach: python tools/models/checksums.py.
Herkunft: alles per Skript, nichts heruntergeladen.
"""

import hashlib
import os
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
PROBE = os.path.join(ROOT, "design", "proben", "grafik", "hunters-axe-201", "runde-2")
OUT_DIR = os.path.join(ROOT, "assets", "models")
NAME = "hunters_axe"

sys.path.insert(0, HERE)


def load_probe():
    """Die Bausteine der Probe (axt2.py ohne ihren main-Teil) in einem eigenen Namensraum."""
    path = os.path.join(PROBE, "axt2.py")
    with open(path, encoding="utf-8") as f:
        # rpartition: der Text "if __name__ ..." steht in axt2.py auch schon in Zeile 33 (dort fuer runde-1/axt.py)
        source = f.read().rpartition('if __name__ == "__main__":')[0]
    space = {"__file__": path, "__name__": "hunters_axe_probe"}
    exec(compile(source, path, "exec"), space)
    return space


def export(p, name, image_path, glb_path):
    """Baut C3 wie export_variant der Probe (gleiche Pruefungen) und schreibt das Modell nach glb_path."""
    p["bpy"].ops.wm.read_factory_settings(use_empty=True)
    part, painters = p["build_variant"]("c3", True)
    edges, open_e, multi, loose = p["closed_report"](part)
    print(f"PRUEFUNG {name}: Kanten {edges}, offene Kanten {open_e}, mehr als zwei Flaechen {multi}, lose Kanten {loose}")
    assert open_e == 0 and multi == 0 and loose == 0, name
    tex = p["make_atlas2"](painters)
    colors = {tuple(int(v) for v in c) for c in tex.reshape(-1, 3)}
    palette = {tuple(int(v) for v in c) for row in p["_PAL"]["akzent"] for c in row["farben"]}
    assert len(colors) <= 16 and colors <= palette, (name, len(colors))
    image = p["save_image"](name, tex, image_path)
    coll = p["bpy"].data.collections.new("Axt")
    p["bpy"].context.scene.collection.children.link(coll)
    obj = part.build(coll, p["export_material"](name, image), smooth=False)
    p["pose"]([obj], {}, root=p["Matrix"].Scale(p["SCALE"], 4))
    p["bake_scale"]([obj])
    p["export_glb"]([obj], glb_path)
    print(f"MODELL {name}: {len(obj.data.polygons)} Dreiecke, {len(colors)} Farben")


def sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def main():
    p = load_probe()
    with tempfile.TemporaryDirectory() as folder:
        check = os.path.join(folder, "axt-c3-schartig.glb")
        export(p, "axe_c3", os.path.join(tempfile.gettempdir(), "axe2_c3.png"), check)
        approved = os.path.join(PROBE, "axt-c3-schartig.glb")
        if sha256(check) != sha256(approved):
            raise SystemExit("FEHLER: C3 weicht von der abgenommenen Probe runde-2/axt-c3-schartig.glb ab")
        print("C3 Byte-gleich zur abgenommenen Probe:", sha256(approved))
    os.makedirs(OUT_DIR, exist_ok=True)
    # Griffende kuerzen: ohne Knauf endet die Axt am Stiel (z = -0,060 vor dem Massstab 1,8, also etwa -0,108 m) statt
    # an der Knaufspitze (-0,155 m). Der Stiel bleibt wie in der Probe, ist rundum geschlossen und liegt im Aermel.
    p["pommel"] = lambda part, zc, kind="cone": None
    export(p, NAME, os.path.join(OUT_DIR, NAME + ".png"), os.path.join(OUT_DIR, NAME + ".glb"))


if __name__ == "__main__":
    main()
