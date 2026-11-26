"""Peitsche des Jaegers (Whip), Entwurf B "Leder" aus dem Probestueck zu Issue #202

Erzeuger fuer das Spiel (Issue #786): zwei Haltungen derselben Peitsche, je ein Knoten "handle" (Griff, Drehpunkt am
Ursprung der Hand, Griff entlang +z) und 14 starre Glieder "seg01" ... "seg14" mit Drehpunkt am Gliedanfang, jedes am
vorigen haengend. whip_rest.glb ist die Ruhe (Glieder kurz, zur Schlaufe gelegt), whip_strike.glb der Schlag (Glieder
lang, gerade entlang +z). Massstab 1,8 eingebacken, Textur 128 x 128 ohne Filterung, flach schattiert. Kein Aermel: das
Spiel nimmt den Aermel der Keule (club_hand.glb), wie die Probe ihn fuer die Ich-Sicht dazugestellt hat.

Das Skript kopiert die Formen nicht, es laedt sie aus dem abgenommenen Probestueck
design/proben/grafik/whip-202/runde-1/peitsche.py und baut nur Entwurf B. Bevor es etwas schreibt, baut es B in Ruhe und
im Schlag genau so wie die Probe und vergleicht beide Ergebnisse Byte fuer Byte mit den abgenommenen Dateien
peitsche-b-leder-ruhe.glb und peitsche-b-leder-schlag.glb; weicht eines ab, bricht es ab. Erst danach entstehen die
Spieldateien. Sie unterscheiden sich von der Probe nur im Namen des Bildes und des Materials ("whip" statt
"peitsche_b_ruhe" / "peitsche_b_schlag"); Formen, Masse und Farben sind dieselben.

Aufruf (aus dem Repository-Wurzelverzeichnis, TEMP und TMP auf einen Zwischenordner setzen):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/whip.py

Schreibt assets/models/whip_rest.glb, assets/models/whip_strike.glb und assets/models/whip.png (das gemeinsame Bild
beider Haltungen; in den glb steckt es eingebettet). Danach: python tools/models/checksums.py.
Herkunft: alles per Skript, nichts heruntergeladen.
"""

import hashlib
import os
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
PROBE = os.path.join(ROOT, "design", "proben", "grafik", "whip-202", "runde-1")
OUT_DIR = os.path.join(ROOT, "assets", "models")
NAME = "whip"
FILES = {True: "whip_rest.glb", False: "whip_strike.glb"}

sys.path.insert(0, HERE)


def load_probe():
    """Die Bausteine der Probe (peitsche.py ohne ihren main-Teil) in einem eigenen Namensraum."""
    path = os.path.join(PROBE, "peitsche.py")
    with open(path, encoding="utf-8") as f:
        source = f.read().rpartition('if __name__ == "__main__":')[0]
    space = {"__file__": path, "__name__": "whip_probe"}
    exec(compile(source, path, "exec"), space)
    return space


def design_b(p):
    return next(d for d in p["DESIGNS"] if d.key == "b")


def export_game(p, rest, image_path, glb_path):
    """Baut B wie export_design der Probe (gleiche Teile, gleiche Pruefung) und schreibt das Modell nach glb_path."""
    d = design_b(p)
    p["bpy"].ops.wm.read_factory_settings(use_empty=True)
    parts = p["build_whip"](d, rest)
    for part in parts:
        edges, open_e, multi, loose = p["closed_report"](part)
        assert open_e == 0 and multi == 0 and loose == 0, (part.name, open_e, multi, loose)
    tex = p["make_atlas"](d.painters)
    colors = {tuple(int(v) for v in c) for c in tex.reshape(-1, 3)}
    palette = {tuple(int(v) for v in c) for row in p["_PAL"]["akzent"] for c in row["farben"]}
    assert len(colors) <= 16 and colors <= palette, (len(colors), colors - palette)
    image = p["save_image"](NAME, tex, image_path)
    coll = p["bpy"].data.collections.new("Peitsche")
    p["bpy"].context.scene.collection.children.link(coll)
    mat = p["export_material"](NAME, image)
    objs = [part.build(coll, mat, smooth=False) for part in parts]
    tris = sum(len(o.data.polygons) for o in objs)
    p["pose"](objs, {}, root=p["Matrix"].Scale(p["SCALE"], 4))
    p["bake_scale"](objs)
    p["export_glb"](objs, glb_path)
    print(f"MODELL {os.path.basename(glb_path)}: {len(objs)} Teile, {tris} Dreiecke, {len(colors)} Farben, alle Teile geschlossen")


def sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def main():
    p = load_probe()
    d = design_b(p)
    with tempfile.TemporaryDirectory() as folder:
        p["OUT"] = folder
        for rest in (True, False):
            p["export_design"](d, rest)
            tag = "ruhe" if rest else "schlag"
            name = f"peitsche-{d.slug}-{tag}.glb"
            if sha256(os.path.join(folder, name)) != sha256(os.path.join(PROBE, name)):
                raise SystemExit(f"FEHLER: B {tag} weicht von der abgenommenen Probe runde-1/{name} ab")
            print(f"B {tag} Byte-gleich zur abgenommenen Probe:", sha256(os.path.join(PROBE, name)))
    os.makedirs(OUT_DIR, exist_ok=True)
    for rest in (True, False):
        export_game(p, rest, os.path.join(OUT_DIR, NAME + ".png"), os.path.join(OUT_DIR, FILES[rest]))


if __name__ == "__main__":
    main()
