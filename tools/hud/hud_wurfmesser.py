"""HUD-Zeichen der Wurfmesser als kleines Rendering ihres 3D-Modells (Issue #821, Regel aus #393 in der Art Bible).

Aufruf (Blender rendert das Modell; nichts wird heruntergeladen):
  "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/hud/hud_wurfmesser.py [-- <Ausgabeordner>]

Reihenfolge: wie tools/hud/hud_armbrust.py und danach (erst hud_teile.py, hud_atlas_doppelt.py, hud_lebensbalken.py, hud_axt.py,
hud_peitsche.py, hud_armbrust.py). Es liest die fertigen Atlanten assets/hud/hud_parts_x2.(png|json) und
assets/hud/hud_parts.(png|json), nimmt seine eigenen Teile (Feld "source") heraus und haengt sie neu unten an beide Atlanten an.
Ohne Ausgabeordner schreibt es nach assets/hud/, sonst nach <Ordner>/hud/. Zwei Laeufe hintereinander liefern bitgleiche Dateien
(Cycles mit festem Seed auf der CPU).

Teile: icon_knives (Waffe in Ruhe, abgedunkelt) und icon_knives_active (gewaehlte Waffe), gerendert aus
models/throwing_knife.glb (das ganze Messer, wie es fliegt) mit render_model aus tools/hud/hud_hochaufgeloest.py: gleiche
Kamerarichtung und gleiches Licht wie fuer alle Modelle. Der Ausschnitt ist um SCALE enger und auf die Mitte des Messers
gerueckt (MITTE): das Messer ist kuerzer als die anderen Waffen. Groesse und Lage des Zeichens wie icon_club.
"""
import os, importlib.util

HERE = os.path.dirname(os.path.abspath(__file__))


def _load(name, file):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, file))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


# Das Skript der Armbrust bringt das Umbenennen mit, das der Axt Zeichenflaeche, Rendering und Anhaengen an die Atlanten.
ARMBRUST = _load("hud_armbrust", "hud_armbrust.py")
AXT = ARMBRUST.AXT

SCALE = 1.25
MITTE = (-0.018, 0.005)  # Kamera auf die Mitte des Messers (Einheiten der Modelldatei, wie cy und cz)
AXT.MARK = "tools/hud/hud_wurfmesser.py"
AXT.MODEL = "throwing_knife"
# hud_armbrust.py hat den Ausschnitt beim Laden schon fuer die Armbrust verstellt: hier vom festen Ausgangswert aus.
AXT.HHM.MODEL_VIEW["scale"] = AXT.HHM.MODEL_VIEW["scale"] * ARMBRUST.SCALE / SCALE
AXT.HHM.MODEL_VIEW["cy"], AXT.HHM.MODEL_VIEW["cz"] = MITTE


def append(name, s):
    """Wie hud_axt.append mit dem Modell des Wurfmessers; hud_axt benennt seine Teile fest, sie werden danach umbenannt."""
    AXT.append(name, s)
    ARMBRUST.rename(name, {"icon_axe": "icon_knives", "icon_axe_active": "icon_knives_active"})


def main():
    append("hud_parts_x2", 2)
    append("hud_parts", 1)


if __name__ == "__main__":
    main()
