"""HUD-Zeichen der Jagdaxt als kleines Rendering ihres 3D-Modells (Issue #776, Regel aus #393 in der Art Bible).

Aufruf (Blender rendert das Modell; nichts wird heruntergeladen):
  "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/hud/hud_axt.py [-- <Ausgabeordner>]

Reihenfolge: erst tools/hud/hud_teile.py, dann tools/hud/hud_atlas_doppelt.py, dann tools/hud/hud_lebensbalken.py, dann dieses
Skript. Es liest die fertigen Atlanten assets/hud/hud_parts_x2.(png|json) und assets/hud/hud_parts.(png|json), nimmt seine eigenen
Teile (Feld "source") heraus und haengt sie neu unten an beide Atlanten an. Ohne Ausgabeordner schreibt es nach assets/hud/, sonst
nach <Ordner>/hud/. Zwei Laeufe hintereinander liefern bitgleiche Dateien (Cycles mit festem Seed auf der CPU).

Teile: icon_axe (Waffe in Ruhe, abgedunkelt) und icon_axe_active (gewaehlte Waffe). Sie entstehen mit der Zeichenflaeche und
render_model aus tools/hud/hud_hochaufgeloest.py: feste Kamera und gleiches Licht wie fuer alle Modelle, auf harte Pixel gerechnet
(8 Stufen mit Bayer-Raster, Deckung ab halber Abdeckung, Schatten unten rechts). Groesse und Mitte wie icon_club, damit das Zeichen
im Waffenrahmen genauso sitzt; der einfache Atlas bekommt ein eigenes Rendering in einfacher Aufloesung.
"""
import sys, os, json, importlib.util
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(REPO, "assets", "hud")
OUT_ROOT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1 else None
OUT = os.path.join(OUT_ROOT, "hud") if OUT_ROOT else SRC
MARK = "tools/hud/hud_axt.py"
MODEL = "hunters_axe"
SAME_AS = "icon_club"        # Groesse und Mitte wie das bisherige Zeichen der Axt
DIM_REST, DIM_ACTIVE = 0.78, 1.0  # Waffe in Ruhe dunkler als die gewaehlte (wie WEAPON und WEAPON_ACT der Zeichen)


def _load(name, file):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, file))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


# hud_hochaufgeloest.py legt den ersten Wert nach "--" als Ordner an; ohne Ausgabeordner zeigt es auf seinen Probenordner.
HHM = _load("hud_hoch", "hud_hochaufgeloest.py")
LB = _load("hud_lebensbalken", "hud_lebensbalken.py")  # PNG lesen und schreiben, Packen


class Canvas(HHM.Canvas):
    """Zeichenflaeche mit Alpha (vormultipliziert) im Massstab s, so gross wie der Entwurf 854 x 480 mal s."""

    def __init__(self, s):
        self.s = float(s)
        self.H, self.W = 480 * s, 854 * s
        self.img = np.zeros((self.H, self.W, 4), np.float32)
        self.hard = True

    def blend(self, x0, y0, cov, col, alpha=1.0):
        h, w = cov.shape
        c = cov[..., None] * alpha
        rgb = col if isinstance(col, np.ndarray) and col.ndim == 3 else np.broadcast_to(HHM.c01(col), (h, w, 3))
        col4 = np.concatenate([rgb, np.ones((h, w, 1), np.float32)], axis=2)
        reg = self.img[y0:y0 + h, x0:x0 + w]
        self.img[y0:y0 + h, x0:x0 + w] = reg * (1 - c) + col4 * c


def to_rgba8(img):
    a = img[..., 3]
    safe = np.where(a > 0, a, 1.0)[..., None]
    out = np.zeros(a.shape + (4,), np.uint8)
    out[..., :3] = np.rint(np.clip(img[..., :3] / safe, 0, 1) * 255).astype(np.uint8)
    out[..., 3] = np.rint(a * 255).astype(np.uint8)
    out[out[..., 3] == 0] = 0
    return out


def icon(part, s, dim):
    """Das Modell klein gerendert, genau auf das Rechteck von <part> (Lage im Entwurf mal s)."""
    cv = Canvas(s)
    uw, uh = part["w"] / s, part["h"] / s
    cv.model(MODEL, part["layoutX"] / s + uw / 2.0, part["layoutY"] / s + uh / 2.0, uw, uh, dim)
    x, y = part["layoutX"], part["layoutY"]
    return to_rgba8(cv.img[y:y + part["h"], x:x + part["w"]])


def append(name, s):
    """Nimmt die eigenen Teile aus dem Atlas <name> heraus und haengt die Zeichen der Axt unten an."""
    table = json.load(open(os.path.join(SRC, name + ".json"), encoding="utf-8"))
    atlas = LB.read_png(os.path.join(SRC, name + ".png"))
    keep = [p for p in table["parts"] if p.get("source") != MARK]
    ref = next(p for p in keep if p["name"] == SAME_AS)
    parts = [("icon_axe", icon(ref, s, DIM_REST)), ("icon_axe_active", icon(ref, s, DIM_ACTIVE))]
    base_h = max(p["y"] + p["h"] for p in keep)
    pos, h = LB.pack([(n, im, 0, 0, None) for n, im in parts], table["width"])
    top = base_h + 1
    grown = np.zeros((top + h, table["width"], 4), np.uint8)
    grown[:base_h] = atlas[:base_h]
    for k, (pname, im) in enumerate(parts):
        x, y = pos[k]
        grown[top + y:top + y + im.shape[0], x:x + im.shape[1]] = im
        keep.append({"name": pname, "x": x, "y": top + y, "w": im.shape[1], "h": im.shape[0],
                     "layoutX": ref["layoutX"], "layoutY": ref["layoutY"], "source": MARK})
    table["parts"] = keep
    table["height"] = int(grown.shape[0])
    os.makedirs(OUT, exist_ok=True)
    LB.write_png(os.path.join(OUT, name + ".png"), grown)
    with open(os.path.join(OUT, name + ".json"), "w", newline="\n", encoding="utf-8") as f:
        f.write(json.dumps(table, indent=1, ensure_ascii=False) + "\n")
    print(name, "Teile", len(keep), "Atlas", table["width"], "x", table["height"], flush=True)


def main():
    append("hud_parts_x2", 2)
    append("hud_parts", 1)


if __name__ == "__main__":
    main()
