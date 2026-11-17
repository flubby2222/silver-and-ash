"""Probefotos der Tueren, Runde 2 (Debug-Build, --photo door-view). Kein Spielcode: patch_lokal2.py schreibt Szene und
Tuer-Ansicht vorlaeufig in src, die Modelle kommen vorlaeufig nach assets/models; am Ende wird beides aus Git
wiederhergestellt. Aufruf (aus dem Repository-Wurzelverzeichnis): python design/proben/grafik/tueren2/fotos2.py <Ordner> [Art]
"""
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
OUT = os.path.join(HERE, sys.argv[1] if len(sys.argv) > 1 else "fotos")
MODELS = os.path.join(ROOT, "assets", "models")
ONLY = sys.argv[2].lower() if len(sys.argv) > 2 else None

ARTEN = [("Normal", "", "normal"), ("Locked", "", "locked"), ("Boss", "a", "boss_a"), ("Boss", "b", "boss_b")]
CASES = []      # (Art, Variante, Name, Abstand, Rueckseite, offen, ostwest)
for kind, var, tag in ARTEN:
    for i, p in enumerate((0.0, 1 / 3, 2 / 3, 1.0)):
        CASES.append((kind, var, f"{tag}_phase{i}", 4, 0, p, 0))
    CASES += [(kind, var, f"{tag}_zu_10m", 10, 0, 0.0, 0), (kind, var, f"{tag}_ruecken_zu_4m", 4, 1, 0.0, 0),
              (kind, var, f"{tag}_ruecken_halb_4m", 4, 1, 2 / 3, 0), (kind, var, f"{tag}_ostwest_zu_4m", 4, 0, 0.0, 1),
              (kind, var, f"{tag}_ostwest_halb_4m", 4, 0, 2 / 3, 1)]
if ONLY:
    CASES = [c for c in CASES if c[2].startswith(ONLY)]


def run(*args, **kw):
    return subprocess.run(list(args), cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, **kw)


def main():
    os.makedirs(OUT, exist_ok=True)
    run(sys.executable, os.path.join(HERE, "patch_lokal2.py"), "apply")
    for f in os.listdir(os.path.join(HERE, "modelle")):
        if f.endswith(".glb") or f.endswith(".png"):
            shutil.copy(os.path.join(HERE, "modelle", f), os.path.join(MODELS, f))
    copied = [f for f in os.listdir(os.path.join(HERE, "modelle"))]
    run(sys.executable, os.path.join(ROOT, "tools", "models", "checksums.py"))
    try:
        for kind, var, name, dist, back, openf, ew in CASES:
            env = dict(os.environ, SA_DOOR_KIND=kind, SA_DOOR_VARIANT=var, SA_DOOR_DIST=str(dist), SA_DOOR_BACK=str(back),
                       SA_DOOR_OPEN=f"{openf:.4f}", SA_DOOR_EW=str(ew))
            tmp = os.path.join(OUT, "_tmp")
            run("dotnet", "run", "--project", "src/SilverAndAsh.Game", "--", "--photo", "door-view", "--out", tmp, env=env)
            src = os.path.join(tmp, "door-view.png")
            if os.path.exists(src):
                shutil.move(src, os.path.join(OUT, f"{name}.png"))
            else:
                print("FEHLER", name)
            shutil.rmtree(tmp, ignore_errors=True)
    finally:
        for f in copied:
            p = os.path.join(MODELS, f)
            if os.path.exists(p) and f.startswith("door_"):
                os.remove(p)
        run("git", "checkout", "src", "assets")


main()
