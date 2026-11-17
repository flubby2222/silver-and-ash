"""Probefotos der Tuerrahmen im Spiel (Debug-Build, Szene frame-view aus patch_lokal6.py).

Schnell: Der Debug-Build wird einmal gebaut (dotnet build, mit angewendetem patch_lokal6.py), dann laeuft die Exe je Bild
in etwa 2 Sekunden direkt aus bin/Debug; Modelle und Texturen werden nur im Ausgabeordner bin/... getauscht (nie in assets/),
samt Pruefsummen-Liste dort. Die Wandkacheln sind die der Palette A (palette3/neu/A/textures) in beiden Spalten.

Spalten: "heute" = Modelle aus main (assets/models), "neu" = modelle/ dieses Ordners.
Aufruf (Wurzelverzeichnis): python design/proben/grafik/tuerrahmen/fotos7.py [Namenspraefix]
"""
import hashlib
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
BIN = os.path.join(ROOT, "src", "SilverAndAsh.Game", "bin", "Debug", "net10.0")
OUT = os.path.join(HERE, "fotos")
ONLY = sys.argv[1] if len(sys.argv) > 1 else None
TEX_A = os.path.join(ROOT, "design", "proben", "grafik", "palette3", "neu", "A", "textures")
KINDS = {"Normal": "normal", "Locked": "locked", "Boss": "boss"}


def sha_list(directory, list_name, endings):
    lines = []
    for name in sorted(os.listdir(directory)):
        if name.endswith(endings):
            data = open(os.path.join(directory, name), "rb").read()
            lines.append(f"{hashlib.sha256(data).hexdigest()} {len(data)} {name}")
    with open(os.path.join(directory, list_name), "w", encoding="ascii", newline="\n") as f:
        f.write("# sha256 size name - local probe\n" + "\n".join(lines) + "\n")


def reset_models():
    d = os.path.join(BIN, "assets", "models")
    shutil.rmtree(d, ignore_errors=True)
    shutil.copytree(os.path.join(ROOT, "assets", "models"), d)
    return d


def install_textures():
    d = os.path.join(BIN, "assets", "textures")
    for f in os.listdir(TEX_A):
        shutil.copy(os.path.join(TEX_A, f), os.path.join(d, f))
    sha_list(d, "textures.sha256", (".png",))


def set_door(column, kind, variant, hoch=False, hell=False):
    d = reset_models()
    if column == "neu":
        src = os.path.join(HERE, "modelle", f"door_{KINDS[kind]}{'hoch' if hoch else ''}{'hell' if hell else ''}_{variant}.glb")
        shutil.copy(src, os.path.join(d, f"door_{KINDS[kind]}.glb"))
    sha_list(d, "models.sha256", (".glb", ".png"))


def shoot(name, column, kind, variant, env, hoch=False, hell=False):
    set_door(column, kind, variant, hoch, hell)
    tmp = os.path.join(OUT, "_tmp")
    shutil.rmtree(tmp, ignore_errors=True)
    e = dict(os.environ, SA_DOOR_KIND=kind, SA_FORCE_WALL=variant, SA_NEWPORTAL="1" if column == "neu" else "0", **env)
    subprocess.run([os.path.join(BIN, "SilverAndAsh.Game.exe"), "--photo", "frame-view", "--out", tmp], cwd=BIN, env=e,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    src = os.path.join(tmp, "frame-view.png")
    if os.path.exists(src):
        shutil.move(src, os.path.join(OUT, name + ".png"))
    else:
        print("FEHLER", name)
    shutil.rmtree(tmp, ignore_errors=True)


def cases():
    for column in ("heute", "neu"):
        for kind in KINDS:
            for var in "ab":
                for dist in (4, 10):
                    for state in ("zu", "offen"):
                        yield (f"{column}_{kind.lower()}_{var}_{dist}m_{state}", column, kind, var,
                               {"SA_DOOR_DIST": str(dist), "SA_DOOR_OPEN": "1" if state == "offen" else "0"}, False)
            # Nahaufnahmen der Stelle, an der Rahmen und Wand zusammenstossen (Kaempfer, Bogenansatz; Sockel)
            yield (f"{column}_{kind.lower()}_a_nah_kaempfer", column, kind, "a",
                   {"SA_DOOR_DIST": "4", "SA_CAM": "-0.2,-2.3,1.0,1.9,-1.0"}, False)
            yield (f"{column}_{kind.lower()}_a_nah_sockel", column, kind, "a",
                   {"SA_DOOR_DIST": "4", "SA_CAM": "0.4,-1.9,1.0,0.5,-1.0"}, False)
            yield (f"{column}_{kind.lower()}_c_4m_zu", column, kind, "c", {"SA_DOOR_DIST": "4"}, False)
    # Tuer in einer Ost-West-Wand (um 90 Grad gedreht): laufen die Fugen auch dort im Raster der Wand?
    for column in ("heute", "neu"):
        for kind in ("Normal", "Boss"):
            for var in "ab":
                yield (f"{column}_seitlich_{kind.lower()}_{var}_4m", column, kind, var, {"SA_DOOR_DIST": "4", "SA_SIDEWAYS": "1"}, False)
    # nur zur Ansicht: Boss A mit Werkstein heller als die Rampe
    for var in "ab":
        for dist in (4, 10):
            for state in ("zu", "offen"):
                yield (f"hell_boss_{var}_{dist}m_{state}", "neu", "Boss", var,
                       {"SA_DOOR_DIST": str(dist), "SA_DOOR_OPEN": "1" if state == "offen" else "0"}, False)
    # Boss A im 6-m-Raum, von der Raumseite (die andere Seite der Halle): heute, neu, neu mit hohem Relief
    cam = {"SA_DOOR_DIST": "4", "SA_HALL_H": "6"}
    for dist, pz in ((4, 4.0), (6, 5.2)):
        c = dict(cam, SA_CAM=f"0,{pz},0,1.5,0")
        yield f"raum_boss_a_{dist}m_heute", "heute", "Boss", "a", c, False
        yield f"raum_boss_a_{dist}m_neu", "neu", "Boss", "a", c, False
        yield f"raum_boss_a_{dist}m_hoch", "neu", "Boss", "a", c, True
        c2 = dict(c, SA_CAM=f"0,{pz},0,3.0,0")
        yield f"raum_boss_b_{dist}m_hoch", "neu", "Boss", "b", c2, True


def clips():
    """Oeffnungsbewegung (--clip door-normal-opening / door-boss-opening, tools/clip.ps1 -Sheet): heute und neu, Wand a."""
    clip_root = os.path.join(os.environ["LOCALAPPDATA"], "SilverAndAsh", "clips")
    for column in ("heute", "neu"):
        for kind, scene in (("Normal", "door-normal-opening"), ("Boss", "door-boss-opening")):
            set_door(column, kind, "a")
            folder = os.path.join(clip_root, scene)
            shutil.rmtree(folder, ignore_errors=True)
            subprocess.run([os.path.join(BIN, "SilverAndAsh.Game.exe"), "--clip", scene], cwd=BIN, env=dict(os.environ, SA_FORCE_WALL="a", SA_NEWPORTAL="1" if column == "neu" else "0"),
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            subprocess.run(["powershell", "-ExecutionPolicy", "Bypass", "-File", os.path.join(ROOT, "tools", "clip.ps1"), folder, "-Sheet"],
                           cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            sheet = folder + "-sheet.png"
            if os.path.exists(sheet):
                shutil.copy(sheet, os.path.join(HERE, f"bewegung_{kind.lower()}_{column}.png"))
            else:
                print("FEHLER", column, kind)
    reset_models()


def main():
    os.makedirs(OUT, exist_ok=True)
    install_textures()
    if ONLY == "--clips":
        clips()
        return
    n = 0
    for name, column, kind, var, env, hoch in cases():
        if ONLY and not name.startswith(ONLY):
            continue
        shoot(name, column, kind, var, env, hoch, name.startswith("hell_"))
        n += 1
    reset_models()
    print("Bilder:", n)


main()
