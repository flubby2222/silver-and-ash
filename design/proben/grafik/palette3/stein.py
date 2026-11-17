"""Palette Runde 3 (Issue #279): Die Stein-Reihe entsteht aus den Farben der heutigen Wand-, Boden- und Decken-Texturen.
Blender-Python (numpy). Schreibt:
  paletten/palette_stein.json   die Palette (Reihen, Grund/Akzent, Katakomben-Auswahl)
  neu/A/textures/*.png          Fassung A: heutiger Stand, nur in die Stein-Rampe einsortiert (nach Helligkeit)
  neu/B/textures/*.png          Fassung B: dasselbe, ruhiger und homogener (helle Ausreisser und grossflaechige Fleckung zurueckgenommen)
Aufruf: blender --background --python stein.py
Die Texturen in assets/ werden nur gelesen."""
import json
import os

import numpy as np
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
TEXDIR = os.path.join(ROOT, "assets", "textures")
NAMES = [f"catacomb_{k}{v}.png" for v in ("", "_b", "_c") for k in ("wall", "floor", "ceiling")]
FOG = (16, 11, 20)
STEPS = 14                                     # Stufen aus den Texturen, dazu die Nebelfarbe als dunkelste: 15


def lab(rgb):
    c = np.asarray(rgb, float) / 255.0
    lin = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = lin @ m.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], axis=-1)


def read(path):
    im = bpy.data.images.load(path)
    w, h = im.size
    a = np.array(im.pixels[:], dtype=float).reshape(h, w, 4)[..., :3] * 255
    bpy.data.images.remove(im)
    return np.round(a[::-1])                   # Zeile 0 oben


def write(arr, path):
    h, w = arr.shape[:2]
    im = bpy.data.images.new("t", w, h)
    im.pixels = np.concatenate([arr[::-1] / 255.0, np.ones((h, w, 1))], axis=2).ravel().tolist()
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)


def build_ramp(images):
    """STEPS gleichmaessig verteilte Helligkeiten (Lab L) zwischen dem 1. und 99. Perzentil aller Pixel der Texturen; die Farbe einer Stufe
    ist das Mittel der Pixel, deren Helligkeit am naechsten liegt (so bleibt der Farbton der heutigen Texturen, die Abstaende sind gleich)."""
    px = np.concatenate([i.reshape(-1, 3) for i in images])
    L = lab(px)[:, 0]
    lo, hi = np.percentile(L, 1), np.percentile(L, 99.5)
    ramp = []
    for t in np.linspace(lo, hi, STEPS):
        w = 1.0
        sel = np.abs(L - t) < w
        while sel.sum() < 40:
            w += 0.5
            sel = np.abs(L - t) < w
        ramp.append(np.round(px[sel].mean(axis=0)))
    return np.array([FOG] + [tuple(c) for c in ramp], float)


def to_ramp_by_l(L, ramp):
    rl = lab(ramp)[:, 0]
    return ramp[np.abs(L[..., None] - rl[None, ...]).argmin(-1)]


def lowpass(field, sigma):
    h, w = field.shape
    fy, fx = np.fft.fftfreq(h)[:, None], np.fft.fftfreq(w)[None, :]
    g = np.exp(-2 * (np.pi * sigma) ** 2 * (fx ** 2 + fy ** 2))
    return np.real(np.fft.ifft2(np.fft.fft2(field) * g))


def calm(L):
    """Fassung B: helle Ausreisser (Einzelsteine, Flecken) und die grossflaechige Fleckung, die sich jeden Meter wiederholt,
    zuruecknehmen; Fugen (dunkle Pixel) bleiben."""
    mean = L.mean()
    low = lowpass(L, 9.0)                                 # grobe Fleckung (periodisch, damit die Kachel nahtlos bleibt)
    high = L - low
    high = np.where(high > 0, high * 0.7, high)           # helle Ausreisser dampfen, Fugen nicht
    return mean + 0.3 * (low - mean) + high


def main():
    imgs = {n: read(os.path.join(TEXDIR, n)) for n in NAMES}
    ramp = build_ramp(imgs.values())
    os.makedirs(os.path.join(HERE, "paletten"), exist_ok=True)
    pal1 = json.load(open(os.path.join(HERE, "..", "palette2", "paletten", "palette_1_violettgrau.json"), encoding="utf-8"))
    rows = {r["name"]: r["farben"] for r in pal1["reihen"]}
    data = {
        "name": "Stein aus dem heutigen Stand (Runde 3)",
        "regeln": "Eine Flaeche, eine Rampe: Die Reihe Stein (15 Stufen: Nebelfarbe 16/11/20 und 14 Stufen aus den Farben der heutigen Wand-, "
                  "Boden- und Decken-Texturen) fuellt Waende, Boeden, Decken. Abwechslung ueber Helligkeit in der Rampe und ueber die Varianten a/b/c, "
                  "nicht ueber Farbton. Reihen 'grund' duerfen Flaechen fuellen, Reihen 'akzent' nur kleine Flaechen mit Zweck (Beute, Tueren, Gefahr, "
                  "spaeter Props und Licht).",
        "grund": [{"name": "Stein", "farben": [list(map(int, c)) for c in ramp]},
                  {"name": "Haut/Knochen", "farben": rows["Haut/Knochen"]}, {"name": "Stoff", "farben": rows["Stoff"]}],
        "akzent": [{"name": n, "farben": rows[n]} for n in ("Holz", "Eisen", "Messing/Gold", "Blutrot")],
        "katakomben": [["Stein", i] for i in range(15)] + [["Holz", i] for i in (1, 2, 3, 4)] + [["Eisen", i] for i in (1, 2, 3)] + [["Haut/Knochen", i] for i in (6, 7)],
        "hinweis": "Holz, Eisen, Messing/Gold, Blutrot und die Stufenfarben der Beute (110/76/46, 92/112/140, 214/170/64, 150/20/30, 200/30/40) "
                   "stehen wie in palette2/Palette 1.",
    }
    with open(os.path.join(HERE, "paletten", "palette_stein.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)
    with open(os.path.join(HERE, "paletten", "palette_stein.gpl"), "w", encoding="utf-8") as f:
        f.write("GIMP Palette\nName: Silver and Ash Stein (Runde 3)\nColumns: 8\n#\n")
        for grp in ("grund", "akzent"):
            for r in data[grp]:
                for i, c in enumerate(r["farben"]):
                    f.write(f"{c[0]:3d} {c[1]:3d} {c[2]:3d} {grp}:{r['name']} {i}\n")
    print("RAMPE", [tuple(int(v) for v in c) for c in ramp])
    for fass in ("A", "B"):
        out = os.path.join(HERE, "neu", fass, "textures")
        os.makedirs(out, exist_ok=True)
        for n, img in imgs.items():
            L = lab(img)[..., 0]
            if fass == "B":
                L = calm(L)
            res = to_ramp_by_l(L, ramp)
            write(res, os.path.join(out, n))
            print("TEX", fass, n, "Farben", len({tuple(p) for p in res.reshape(-1, 3).astype(int)}), "vorher", len({tuple(p) for p in img.reshape(-1, 3).astype(int)}),
                  "Streuung L vorher", round(float(lab(img)[..., 0].std()), 1), "nachher", round(float(lab(res)[..., 0].std()), 1))


main()
