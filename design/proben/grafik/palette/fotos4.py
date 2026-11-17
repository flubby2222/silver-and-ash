"""Probefotos der Paletten im Spiel: je Palette (und heutiger Stand "0_heute") die Szenen palette-hall (4 m, 10 m),
door-level-seed2-boss, room-boss (12 m). Die umgerechneten Texturen und Modelle kommen vorlaeufig nach assets/, danach wird alles
aus Git wiederhergestellt. Aufruf (aus dem Repository-Wurzelverzeichnis): python design/proben/grafik/palette/fotos4.py <Ordner> [Palette]
"""
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
OUT = os.path.join(HERE, sys.argv[1] if len(sys.argv) > 1 else "fotos")
ONLY = sys.argv[2] if len(sys.argv) > 2 else None
SCENES = [("saal_4m", "palette-hall", {"SA_PAL_DIST": "4"}), ("saal_10m", "palette-hall", {"SA_PAL_DIST": "10"}),
          ("bosstor", "door-level-seed2-boss", {}), ("gorm_12m", "room-boss", {})]


def run(*args, **kw):
    return subprocess.run(list(args), cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, **kw)


def install(key):
    """Kopiert die umgerechneten Dateien nach assets/ (bei 0_heute nichts) und schreibt die Pruefsummen neu."""
    if key == "0_heute":
        return
    base = os.path.join(HERE, "umgerechnet", key)
    for sub in ("textures", "models"):
        for f in os.listdir(os.path.join(base, sub)):
            shutil.copy(os.path.join(base, sub, f), os.path.join(ROOT, "assets", sub, f))
    run(sys.executable, os.path.join(ROOT, "tools", "models", "checksums.py"))
    run(sys.executable, os.path.join(ROOT, "tools", "textures", "checksums.py")) if os.path.exists(os.path.join(ROOT, "tools", "textures", "checksums.py")) else None


def main():
    os.makedirs(OUT, exist_ok=True)
    run(sys.executable, os.path.join(HERE, "patch_lokal4.py"), "apply")
    keys = ["0_heute"] + sorted(d for d in os.listdir(os.path.join(HERE, "umgerechnet")))
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
