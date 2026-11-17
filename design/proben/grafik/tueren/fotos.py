"""Probefotos der Tueren im Spiel (Debug-Build, --photo door-view). Kein Spielcode: patch_lokal.py schreibt die Szene und die
Tuer-Ansicht vorlaeufig in src, die Modelle kommen vorlaeufig nach assets/models; am Ende wird beides aus Git
wiederhergestellt. Aufruf (aus dem Repository-Wurzelverzeichnis): python design/proben/grafik/tueren/fotos.py <Ausgabeordner>
"""
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
OUT = os.path.join(HERE, sys.argv[1] if len(sys.argv) > 1 else "fotos")
MODELS = os.path.join(ROOT, "assets", "models")

CASES = []                                                             # (Art, Name, Abstand, Rueckseite, offen)
for kind in ("Normal", "Boss", "Locked"):
    CASES += [(kind, "zu_4m", 4, 0, 0.0), (kind, "halb_4m", 4, 0, 0.5), (kind, "offen_4m", 4, 0, 1.0),
              (kind, "zu_10m", 10, 0, 0.0), (kind, "ruecken_zu_4m", 4, 1, 0.0), (kind, "ruecken_halb_4m", 4, 1, 0.5)]
CASES += [("Cracked", "zu_4m", 4, 0, 0.0), ("Cracked", "zu_10m", 10, 0, 0.0), ("Cracked", "ruecken_zu_4m", 4, 1, 0.0)]

ONLY = sys.argv[2].lower() if len(sys.argv) > 2 else None
if ONLY:
    CASES = [c for c in CASES if c[0].lower() == ONLY]


def run(*args, **kw):
    return subprocess.run(list(args), cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, **kw)


def main():
    os.makedirs(OUT, exist_ok=True)
    run(sys.executable, os.path.join(HERE, "patch_lokal.py"), "apply")
    for f in os.listdir(os.path.join(HERE, "modelle")):
        shutil.copy(os.path.join(HERE, "modelle", f), os.path.join(MODELS, f))
    run(sys.executable, os.path.join(ROOT, "tools", "models", "checksums.py"))
    try:
        for kind, name, dist, back, openf in CASES:
            env = dict(os.environ, SA_DOOR_KIND=kind, SA_DOOR_DIST=str(dist), SA_DOOR_BACK=str(back), SA_DOOR_OPEN=str(openf))
            tmp = os.path.join(OUT, "_tmp")
            run("dotnet", "run", "--project", "src/SilverAndAsh.Game", "--", "--photo", "door-view", "--out", tmp, env=env)
            src = os.path.join(tmp, "door-view.png")
            if os.path.exists(src):
                shutil.move(src, os.path.join(OUT, f"{kind.lower()}_{name}.png"))
            else:
                print("FEHLER", kind, name)
            shutil.rmtree(tmp, ignore_errors=True)
    finally:
        for f in os.listdir(os.path.join(HERE, "modelle")):
            p = os.path.join(MODELS, f)
            if f.startswith("door_") and os.path.exists(p):
                os.remove(p)
        run("git", "checkout", "src", "assets")


main()
