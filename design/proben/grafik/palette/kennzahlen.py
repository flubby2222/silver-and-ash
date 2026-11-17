"""Kennzahlen der Umrechnung (Blender-Python): mittlere Lab-Farbe von Ghul-Haut-Flaeche und Boden/Wand je Palette, Abstand
(Regel 1 der Art Bible: Figur hebt sich vom Boden ab) und Zahl der Farben je Textur vorher/nachher."""
import os
import numpy as np
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))


def load(path):
    im = bpy.data.images.load(path)
    w, h = im.size
    a = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[..., :3] * 255
    return a.reshape(-1, 3)


def lab(rgb):
    c = np.asarray(rgb, float) / 255.0
    lin = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = lin @ m.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], axis=-1)


def skin(px):
    """Haut des Ghuls: die helleren zwei Drittel der Pixel, ohne die dunklen Hoehlen (L > 25)."""
    L = lab(px)
    return L[L[:, 0] > 25].mean(0)


for key in ("0_heute", "1_violettgrau", "2_kalt_blau", "3_warm_ocker"):
    base = os.path.join(ROOT, "assets") if key == "0_heute" else os.path.join(HERE, "umgerechnet", key)
    tex = lambda n: os.path.join(base, "textures", n)   # noqa: E731
    mod = lambda n: os.path.join(base, "models", n)     # noqa: E731
    g = skin(load(mod("ghoul.png")))
    fl = lab(load(tex("catacomb_floor.png"))).mean(0)
    wl = lab(load(tex("catacomb_wall.png"))).mean(0)
    de = lambda a, b: float(np.linalg.norm(a - b))       # noqa: E731
    print(f"KENNZAHL {key}: Ghul L {g[0]:.0f} a {g[1]:.0f} b {g[2]:.0f} | Boden L {fl[0]:.0f} | Abstand Ghul-Boden dE {de(g, fl):.1f} (dL {g[0]-fl[0]:.0f}) | Ghul-Wand dE {de(g, wl):.1f}")
