"""Probefotos der rissigen Wand im Spiel (Debug-Build, --photo wall-view). Kein Spielcode: patch_lokal3.py schreibt Szene und
Zeichnung vorlaeufig in src, die Modelle kommen vorlaeufig nach assets/models; am Ende wird beides aus Git wiederhergestellt.
Aufruf (aus dem Repository-Wurzelverzeichnis): python design/proben/grafik/risswand/fotos3.py <Ordner> [Namenspraefix]
"""
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
OUT = os.path.join(HERE, sys.argv[1] if len(sys.argv) > 1 else "fotos")
MODELS = os.path.join(ROOT, "assets", "models")
ONLY = sys.argv[2] if len(sys.argv) > 2 else None

CASES = []      # (Name, Variante, Staerke, Abstand, Rueckseite)
for var, dists in (("a", (2, 4, 10)), ("b", (2, 4, 10)), ("c", (4,))):
    for st in ("dezent", "deutlich"):
        for d in dists:
            CASES.append((f"{var}_{st}_{d}m", var, st, d, 0))
for var in ("a", "b"):
    for st in ("dezent", "deutlich"):
        CASES.append((f"{var}_{st}_geheimraum", var, st, 2, 1))
if ONLY:
    CASES = [c for c in CASES if c[0].startswith(ONLY)]


def run(*args, **kw):
    return subprocess.run(list(args), cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, **kw)


def main():
    os.makedirs(OUT, exist_ok=True)
    run(sys.executable, os.path.join(HERE, "patch_lokal3.py"), "apply")
    copied = []
    for f in os.listdir(os.path.join(HERE, "modelle")):
        shutil.copy(os.path.join(HERE, "modelle", f), os.path.join(MODELS, f))
        copied.append(f)
    run(sys.executable, os.path.join(ROOT, "tools", "models", "checksums.py"))
    try:
        for name, var, st, dist, back in CASES:
            env = dict(os.environ, SA_WALL_VAR=var, SA_WALL_DIST=str(dist), SA_WALL_BACK=str(back), SA_CRACK=f"{var}_{st}",
                       SA_DOOR_OPEN="0")
            tmp = os.path.join(OUT, "_tmp")
            run("dotnet", "run", "--project", "src/SilverAndAsh.Game", "--", "--photo", "wall-view", "--out", tmp, env=env)
            src = os.path.join(tmp, "wall-view.png")
            if os.path.exists(src):
                shutil.move(src, os.path.join(OUT, f"{name}.png"))
            else:
                print("FEHLER", name)
            shutil.rmtree(tmp, ignore_errors=True)
    finally:
        for f in copied:
            p = os.path.join(MODELS, f)
            if os.path.exists(p) and f.startswith("crack_"):
                os.remove(p)
        run("git", "checkout", "src", "assets")


main()
