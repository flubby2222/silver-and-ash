"""Probefotos Runde 2: nur die Umgebung (Wand, Boden, Decke, Tueren, rissige Wand) wird je Palette getauscht, alles andere bleibt
wie in main. Szenen: palette-hall 4 m und 10 m (lokal, ../palette/patch_lokal4.py), door-level-seed1 (Gang mit Holztuer),
door-level-seed2-boss, cracked-wall-2m. Aufruf (aus dem Repository-Wurzelverzeichnis): python design/proben/grafik/palette2/fotos5.py <Ordner> [Palette]
Danach wird src und assets aus Git wiederhergestellt."""
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
OUT = os.path.join(HERE, sys.argv[1] if len(sys.argv) > 1 else "fotos")
ONLY = sys.argv[2] if len(sys.argv) > 2 else None
SCENES = [("saal_4m", "palette-hall", {"SA_PAL_DIST": "4"}), ("saal_10m", "palette-hall", {"SA_PAL_DIST": "10"}),
          ("gang_tuer", "door-level-seed1", {}), ("bosstor", "door-level-seed2-boss", {}), ("wand_2m", "cracked-wall-2m", {})]


def run(*args, **kw):
    return subprocess.run(list(args), cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, **kw)


def install(key):
    if key == "0_heute":
        return
    base = os.path.join(HERE, "neu", key)
    for sub in ("textures", "models"):
        for f in os.listdir(os.path.join(base, sub)):
            shutil.copy(os.path.join(base, sub, f), os.path.join(ROOT, "assets", sub, f))
    run(sys.executable, os.path.join(ROOT, "tools", "models", "checksums.py"))


def main():
    os.makedirs(OUT, exist_ok=True)
    run(sys.executable, os.path.join(os.path.dirname(HERE), "palette", "patch_lokal4.py"), "apply")
    keys = ["0_heute"] + sorted(d for d in os.listdir(os.path.join(HERE, "neu")))
    if ONLY:
        keys = [k for k in keys if k.startswith(ONLY)]
    try:
        for key in keys:
            run("git", "checkout", "assets")
            install(key)
            for name, scene, env in SCENES:
                tmp = os.path.join(OUT, "_tmp")
                run("dotnet", "run", "--project", "src/SilverAndAsh.Game", "--", "--photo", scene, "--out", tmp, env=dict(os.environ, **env))
                src = os.path.join(tmp, scene + ".png")
                if os.path.exists(src):
                    shutil.move(src, os.path.join(OUT, f"{key}_{name}.png"))
                else:
                    print("FEHLER", key, name)
                shutil.rmtree(tmp, ignore_errors=True)
    finally:
        run("git", "checkout", "src", "assets")


main()
