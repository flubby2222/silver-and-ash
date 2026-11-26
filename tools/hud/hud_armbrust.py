"""HUD-Zeichen der Pflockarmbrust als kleines Rendering ihres 3D-Modells (Issue #793, Regel aus #393 in der Art Bible).

Aufruf (Blender rendert das Modell; nichts wird heruntergeladen):
  "D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python tools/hud/hud_armbrust.py [-- <Ausgabeordner>]

Reihenfolge: wie tools/hud/hud_peitsche.py und danach (erst hud_teile.py, hud_atlas_doppelt.py, hud_lebensbalken.py, hud_axt.py,
hud_peitsche.py). Es liest die fertigen Atlanten assets/hud/hud_parts_x2.(png|json) und assets/hud/hud_parts.(png|json), nimmt seine
eigenen Teile (Feld "source") heraus und haengt sie neu unten an beide Atlanten an. Ohne Ausgabeordner schreibt es nach assets/hud/,
sonst nach <Ordner>/hud/. Zwei Laeufe hintereinander liefern bitgleiche Dateien (Cycles mit festem Seed auf der CPU).

Teile: icon_crossbow (Waffe in Ruhe, abgedunkelt) und icon_crossbow_active (gewaehlte Waffe), gerendert aus
models/stake_crossbow_loaded.glb (gespannt, mit Pflock) mit render_model aus tools/hud/hud_hochaufgeloest.py: gleiche
Kamerarichtung und gleiches Licht wie fuer alle Modelle. Der Ausschnitt ist um SCALE weiter und auf die Mitte der Armbrust
gerueckt (MITTE): der Bogen ist breiter als jede andere Waffe. Groesse und Lage des Zeichens wie icon_club.
"""
import os, json, importlib.util

HERE = os.path.dirname(os.path.abspath(__file__))


def _load(name, file):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, file))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


# Das Skript der Axt bringt Zeichenflaeche, Rendering und Anhaengen an die Atlanten mit; hier nur, was fuer die Armbrust anders ist.
AXT = _load("hud_axt", "hud_axt.py")

SCALE = 1.7
MITTE = (0.0, 0.1)  # Kamera auf die Mitte der Armbrust (Einheiten der Modelldatei, wie cy und cz)
AXT.MARK = "tools/hud/hud_armbrust.py"
AXT.MODEL = "stake_crossbow_loaded"
AXT.HHM.MODEL_VIEW["scale"] = AXT.HHM.MODEL_VIEW["scale"] / SCALE
AXT.HHM.MODEL_VIEW["cy"], AXT.HHM.MODEL_VIEW["cz"] = MITTE


def append(name, s):
    """Wie hud_axt.append mit dem Modell der Armbrust; hud_axt benennt seine Teile fest, sie werden danach umbenannt."""
    AXT.append(name, s)
    rename(name, {"icon_axe": "icon_crossbow", "icon_axe_active": "icon_crossbow_active"})


def rename(name, names):
    path = os.path.join(AXT.OUT, name + ".json")
    table = json.load(open(path, encoding="utf-8"))
    for part in table["parts"]:
        if part.get("source") == AXT.MARK and part["name"] in names:
            part["name"] = names[part["name"]]
    with open(path, "w", newline="\n", encoding="utf-8") as f:
        f.write(json.dumps(table, indent=1, ensure_ascii=False) + "\n")


def main():
    append("hud_parts_x2", 2)
    append("hud_parts", 1)


if __name__ == "__main__":
    main()
