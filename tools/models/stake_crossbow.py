"""Pflockarmbrust (Stake Crossbow), Entwurf A "Eisenwinde" aus dem Probestueck zu Issue #203

Erzeuger fuer das Spiel (Issue #793): zwei Haltungen derselben Armbrust mit denselben drei Knoten, alle ohne Eltern, Drehpunkt
am Griff (Ursprung), Schussrichtung +z, oben +y:
    "crossbow"  Schaft, Bogen, Sehne, Beschlaege
    "crank"     die Kurbel der Winde (Drehpunkt auf der Achse der Walze, dreht sich beim Spannen)
    "stake"     der Pflock (eigener Knoten: nach dem Schuss liegt er nicht mehr auf, beim Nachladen wird er eingelegt)
stake_crossbow_loaded.glb ist die gespannte Waffe (Bogenspitzen nach hinten gebogen, Sehne im Pflockende), die Form der Probe
armbrust-a-eisen.glb. stake_crossbow_relaxed.glb ist die entspannte: Bogen fast gerade, Sehne gerade zwischen den Spitzen vor dem
Schaft. Die Knoten sind in beiden Dateien dieselben und gleich aufgebaut (die Teile des Spiels stehen in StakeCrossbowPose.Parts).
Rundum geschlossen (jede Kante gehoert zu genau zwei Flaechen, vor dem Schreiben geprueft), keine Hand: das Spiel nimmt den
Aermel der Pistole (pistol_hand.glb), wie die Probe ihn fuer die Ich-Sicht dazugestellt hat.

Masstab: die Probe backt 1,8 x 0,72 = 1,3 ein; das ist fuer eine so breite Waffe zu gross (Regel fuer breite Waffen in der Art Bible,
Abschnitt "Breite Waffen in der Ich-Sicht"). Die Spieldateien backen 1,8 x SHRINK (0,42), siehe SHRINK unten. Formen, Farben und Textur sind
die der Probe (dieselben Bausteine aus design/proben/grafik/stake-crossbow-203/runde-1/armbrust_gemeinsam.py und armbrust_a.py).

Das Skript laedt die Bausteine der Probe, baut zuerst Entwurf A genau wie die Probe und vergleicht das Ergebnis Byte fuer Byte mit
der abgenommenen armbrust-a-eisen.glb; weicht es ab, bricht es ab und schreibt nichts.

Aufruf (aus dem Repository-Wurzelverzeichnis, TEMP und TMP auf einen Zwischenordner setzen):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/stake_crossbow.py

Schreibt assets/models/stake_crossbow_loaded.glb, stake_crossbow_relaxed.glb und stake_crossbow.png. Danach: python tools/models/checksums.py.
Herkunft: alles per Skript, nichts heruntergeladen.
"""

import hashlib
import importlib.util
import os
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
PROBE = os.path.join(ROOT, "design", "proben", "grafik", "stake-crossbow-203", "runde-1")
OUT_DIR = os.path.join(ROOT, "assets", "models")
NAME = "stake_crossbow"
FILES = {True: "stake_crossbow_loaded.glb", False: "stake_crossbow_relaxed.glb"}

SHRINK = 0.42                # statt 0,72 der Probe: Dateimass = 1,8 x SHRINK (Spannweite des Bogens 0,35 m)
CRANK_PIVOT = (0.0, 0.035, -0.060)   # Achse der Walze (Skriptmass)

sys.path.insert(0, PROBE)


def load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(PROBE, name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def build(g, a, loaded):
    """Wie a.build_a der Probe, aber mit Kurbel als eigenem Knoten und (entspannt) geradem Bogen mit gerader Sehne."""
    V = g.V
    body, stake = g.new_parts()
    crank = g.Part("crank", CRANK_PIVOT, g.UV)
    y_stake = a.Y_STAKE
    g.sweep(body, [(0, 0.005, -0.078), (0, 0.006, -0.04), (0, 0.005, 0.06), (0, 0.004, 0.14)],
            [(0.019, 0.022), (0.025, 0.026), (0.027, 0.024), (0.021, 0.020)], "wood")
    g.sweep(body, [(0, 0.006, 0.004), (0, -0.04, -0.002), (0, -0.082, -0.014)], [(0.014, 0.013), (0.0165, 0.0150), (0.015, 0.014)], "grip")
    for z in (-0.050, 0.020, 0.100):
        g.beam(body, (0, 0.004, z - 0.007), (0, 0.004, z + 0.007), 0.0300, 0.0235, "iron")
    for s in (-1, 1):
        g.beam(body, (s * 0.0125, y_stake + 0.001, -0.040), (s * 0.0125, y_stake + 0.001, 0.145), 0.0040, 0.0105, "iron")
    xs = [-0.215, -0.16, -0.08, 0.0, 0.08, 0.16, 0.215]
    bend = -0.045 if loaded else -0.008          # gespannt kippen die Spitzen nach hinten, entspannt fast gerade
    zs = [0.140 + bend * (x / 0.215) ** 2 for x in xs]
    depth = [0.0075, 0.0105, 0.0150, 0.0170, 0.0150, 0.0105, 0.0075]
    hgt = [0.0070, 0.0095, 0.0120, 0.0130, 0.0120, 0.0095, 0.0070]
    g.sweep(body, [(x, 0.020, z) for x, z in zip(xs, zs)], list(zip(depth, hgt)), "iron")
    g.beam(body, (0, 0.004, 0.118), (0, 0.004, 0.165), 0.0300, 0.0245, "dark", 0.0260, 0.0200)
    for s in (-1, 1):
        g.beam(body, (s * 0.205, 0.020, zs[0] + 0.004), (s * 0.228, 0.020, zs[0] + 0.008 - 0.0025), 0.0072, 0.0068, "silver")
    g.disc_x(body, (0, 0.035, -0.060), 0.0150, 0.030, "dark")
    for s in (-1, 1):
        g.disc_x(body, (s * 0.032, 0.035, -0.060), 0.0185, 0.004, "gold")
    g.rod(crank, (-0.040, 0.035, -0.060), (-0.040, 0.078, -0.072), 0.0045, "iron", sides=4, up=V(0, 0, 1))
    g.disc_x(crank, (-0.040, 0.082, -0.073), 0.0080, 0.010, "grip")
    tips = [(-0.222, 0.020, zs[0] + 0.006), (0.222, 0.020, zs[-1] + 0.006)]
    if loaded:
        g.build_string(body, tips, (0, y_stake, -0.026))
    else:
        g.rod(body, tips[0], tips[1], 0.0024, "cord", sides=4)
    g.build_stake(stake, y_stake, -0.026, 0.268, 0.0095, 0.062, "silver", collar=(0.200, 0.0112, "iron"))
    return [body, crank, stake]


def export(g, parts, glb_path, image_path, shrink):
    bpy = g.bpy
    for part in parts:
        report = g.closed_report([part])
        assert report[0] == 0 and report[1] == 0 and report[2] == 0, (part.name, report)
    tex = g.make_atlas(g.base_painters())
    colors = len({tuple(c) for c in tex.reshape(-1, 3)})
    assert colors <= 32, colors
    image = g.save_image(NAME, tex, image_path)
    coll = bpy.data.collections.new("Armbrust")
    bpy.context.scene.collection.children.link(coll)
    mat = g.export_material(NAME, image)
    objs = [part.build(coll, mat, smooth=False) for part in parts]
    tris = sum(len(o.data.polygons) for o in objs)
    g.pose(objs, {}, root=g.Matrix.Scale(g.SCALE * shrink, 4))
    g.bake_scale(objs)
    g.export_glb(objs, glb_path)
    xs = [v.co.x for o in objs for v in o.data.vertices]
    print(f"MODELL {os.path.basename(glb_path)}: {len(objs)} Teile, {tris} Dreiecke, {colors} Farben, alle Teile geschlossen, "
          f"Spannweite {max(xs) - min(xs):.3f} m (Dateimass)")


def main():
    g = load("armbrust_gemeinsam")
    a = load("armbrust_a")
    with tempfile.TemporaryDirectory() as folder:
        probe_glb = os.path.join(folder, "armbrust-a-eisen.glb")
        g.bpy.ops.wm.read_factory_settings(use_empty=True)
        body, stake = a.build_a()
        tex = g.make_atlas(g.base_painters())
        image = g.save_image("armbrust_a", tex, os.path.join(folder, "armbrust_a.png"))
        coll = g.bpy.data.collections.new("Armbrust")
        g.bpy.context.scene.collection.children.link(coll)
        mat = g.export_material("armbrust_a", image)
        objs = [body.build(coll, mat, smooth=False), stake.build(coll, mat, smooth=False)]
        g.pose(objs, {}, root=g.Matrix.Scale(g.SCALE * g.SHRINK, 4))
        g.bake_scale(objs)
        g.export_glb(objs, probe_glb)
        if sha256(probe_glb) != sha256(os.path.join(PROBE, "armbrust-a-eisen.glb")):
            raise SystemExit("FEHLER: A weicht von der abgenommenen Probe runde-1/armbrust-a-eisen.glb ab")
        print("A Byte-gleich zur abgenommenen Probe:", sha256(probe_glb))
    os.makedirs(OUT_DIR, exist_ok=True)
    for loaded in (True, False):
        g.bpy.ops.wm.read_factory_settings(use_empty=True)
        export(g, build(g, a, loaded), os.path.join(OUT_DIR, FILES[loaded]), os.path.join(OUT_DIR, NAME + ".png"), SHRINK)


if __name__ == "__main__":
    main()
