"""Setzt je Szene die Spielfotos a | b | c zu einem Blatt zusammen (Blender-Python). Nur Probebilder."""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE)))), "tools", "models"))
from psx import load_png, save_png  # noqa: E402

for scene in ("room-corridor", "room-combat", "room-treasure", "ghoul-10m", "chests-4m"):
    imgs = [np.asarray(load_png(os.path.join(HERE, "fotos", v, scene + ".png")), float) for v in "abc"]
    # (load_png/save_png laufen von unten nach oben: umgekehrt anhaengen, damit a oben liegt)
    # untereinander: dreimal 854x480 passt besser auf den Bildschirm als nebeneinander
    save_png(np.concatenate(imgs[::-1], axis=0), os.path.join(HERE, f"blatt_{scene}.png"))
    print(scene, imgs[0].shape, [round(float(i.mean()), 1) for i in imgs])

# Sichtbarkeit des Ghuls aus 10 m: Fensterausschnitt um die Figur (Bildmitte, y 218..262, x 408..448); Mass: mittlere
# Abweichung der Pixel von der Helligkeit des Randstreifens (Boden) - groesser = hebt sich staerker ab.
for v in "abc":
    im = np.asarray(load_png(os.path.join(HERE, "fotos", v, "ghoul-10m.png")), float).mean(axis=2)
    for top in (480 - 262, ):
        crop = im[top:top + 44, 408:448]
        ring = np.concatenate([crop[:3].ravel(), crop[-3:].ravel(), crop[:, :3].ravel(), crop[:, -3:].ravel()])
        inner = crop[6:-6, 6:-6]
        print("Ghul 10 m", v, "Boden", round(float(np.median(ring)), 1), "Figur dunkler/heller um", round(float(np.abs(inner - np.median(ring)).mean()), 2),
              "Rauschen im Rand", round(float(ring.std()), 2))
