"""Menue-Bildteile fuer das Spiel (Issue #371): erzeugt aus der abgenommenen Vorlage "C2 Schlank" (Runde 2, Issue #351)
ein Bild mit Massetabelle: assets/menu/menu_parts.png und assets/menu/menu_parts.json.

Aufruf (Blender dient nur als Python mit numpy; nichts wird heruntergeladen):
  "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/menu/menue_teile.py [-- <Ausgabeordner>]

Ohne Ausgabeordner schreibt das Skript nach assets/menu.

Die Zeichenfunktionen (tafel, mark, slider, tab, gable_stack) kommen unveraendert aus
design/proben/grafik/menue/runde2/menue_r2.py, mit den Massen P_SCHLANK. Die Vorlage ruft am Ende main() auf; hier wird ihr
Quelltext ohne diesen Aufruf ausgefuehrt, damit nur die Funktionen geladen werden. Jedes Teil entsteht an seinem Platz im
854 x 480 grossen Entwurf (die Koernung haengt von der Bildposition ab) und wird zugeschnitten.

Tafel und Cursor gibt es je einmal in grosser Fassung; das Spiel zieht sie auf jede Groesse (neun bzw. drei Stuecke: Ecken und
Spitzen bleiben 1:1, die Kanten und die Mitte werden gestreckt). Giebel, Regler, Knopf und Reiter werden 1:1 gezeichnet.
Ohne Schrift: Texte zeichnet das Spiel selbst. Ohne Zufall; zwei Laeufe liefern bitgleiche Dateien.
"""
import sys, os, json, zlib, struct

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
R2 = os.path.join(REPO, "design", "proben", "grafik", "menue", "runde2", "menue_r2.py")

OUT_DIR = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1 \
    else os.path.join(REPO, "assets", "menu")
ATLAS_W = 1024

# Vorlage laden: Quelltext ohne den Aufruf von main() in der letzten Zeile.
_src = open(R2, encoding="utf-8").read()
_cut = _src.rstrip().rfind("\nmain()")
assert _cut > 0, "menue_r2.py endet nicht mehr mit main()"
V = {"__file__": R2, "__name__": "menue_r2_teile"}
exec(compile(_src[:_cut], R2, "exec"), V)  # noqa: S102 - eigene Vorlage aus dem Repository
np = V["np"]
r4 = V["r4"]
P = V["P_SCHLANK"]
M = V["M"]
CX = V["CX"]

# ---------------------------------------------------------------- Rendern und Zuschneiden (wie tools/hud/hud_teile.py)


def render(draw):
    L = r4.Layer(854, 480, True)
    draw(L)
    return L


def to_rgba8(L):
    """Vormultipliziert (Float) -> gerade (nicht vormultiplizierte) RGBA-Bytes; Zeilen von oben nach unten."""
    a = L.a
    safe = np.where(a > 0, a, 1.0)[..., None]
    rgb = np.clip(L.rgb / safe, 0, 1)
    a8 = np.rint(a * 255).astype(np.uint8)
    out = np.zeros(a.shape + (4,), np.uint8)
    out[..., :3] = np.rint(rgb * 255).astype(np.uint8)
    out[..., 3] = a8
    out[a8 == 0] = 0
    return out


def box_of(im):
    ys, xs = np.nonzero(im[..., 3] > 0)
    return int(xs.min()), int(ys.min()), int(xs.max() + 1), int(ys.max() + 1)


PARTS = []  # (Name, Bild h x w x 4, layoutX, layoutY)


def add(names_draws):
    """Ein oder mehrere Teile mit gemeinsamem Zuschnitt nach dem ersten Bild (Paare wie leer/voll sind gleich gross)."""
    imgs = [to_rgba8(render(draw)) for _, draw in names_draws]
    for k in range(1, len(imgs)):
        if names_draws[k][0].endswith("_full"):
            # _full nur im deckenden Inneren von _empty: der halbdurchsichtige Rand liegt schon in _empty.
            imgs[k] = imgs[k].copy(); imgs[k][imgs[0][..., 3] != 255] = 0
    x0, y0, x1, y1 = box_of(imgs[0])
    for (name, _), im in zip(names_draws, imgs):
        PARTS.append((name, im[y0:y1, x0:x1].copy(), x0, y0))


# ---------------------------------------------------------------- Teile

# Tafel ohne Giebel in der Groesse der Seite mit Reitern (Vorlage: 40..814 x 62..452); Quelle fuer alle Tafeln.
PANEL = (40, 62, 814, 452)
add([("panel", lambda L: V["tafel"](L, P, *PANEL, gable=False, seed=11))])

# Giebel mit Auge ueber der Mitte der Titeltafel (Vorlage: Tafel ab y = 204, Mitte x = 427).
GABLE_ANCHOR = (CX, 204)
add([("gable", lambda L: V["gable_stack"](L, P, CX, 204))])

# Cursor: spitze Silberfassung, gross (Eintraege, 36 hoch) und klein (Wahl in der Zeile "Display", 30 hoch).
CURSOR = (CX - 104, 222, CX + 104, 258)
add([("cursor", lambda L: V["mark"](L, P, *CURSOR, seed=20))])
CURSOR_SMALL = (CX - 68, 300, CX + 68, 330)
add([("cursor_small", lambda L: V["mark"](L, P, *CURSOR_SMALL, small=True, seed=36))])

# Regler: Rinne leer und voll (gleich gross), Rautenknopf. Vorlage: x 420..580, Zeilenmitte 138, Hoehe 10.
SL = (420, 580, 138)


def _trough(frac, hi):
    sh = P["sh"]
    return lambda L: r4.trough(L, SL[0], SL[2] - sh / 2, SL[1], SL[2] + sh / 2, frac, V["BRASS_L"], V["BRASS_M"], "recess",
                               back=V["IRON0"], hi=hi, p=sh / 2.2)


add([("slider_empty", _trough(0.0, None)), ("slider_full", _trough(1.0, V["GOLD_W"]))])
KNOB = (500.5, 138)
add([("knob", lambda L: r4.diamond4(L, KNOB[0], KNOB[1], P["kr"]))])

# Reiter: Karte (120 breit) und Ausruestung (190 breit), je ruhig und offen (offen 8 hoeher, mit Raute obenauf).
TABS = P["tabs"]
for i, nm in enumerate(("map", "equipment")):
    add([(f"tab_{nm}", lambda L, i=i: V["tab"](L, P, TABS[i], False, 41 + i))])
    add([(f"tab_{nm}_active", lambda L, i=i: V["tab"](L, P, TABS[i], True, 41 + i))])


# ---------------------------------------------------------------- Atlas packen


def pack(parts):
    order = sorted(range(len(parts)), key=lambda k: (-parts[k][1].shape[0], parts[k][0]))
    x = y = rowh = 0
    pos = {}
    for k in order:
        h, w = parts[k][1].shape[:2]
        if x + w > ATLAS_W:
            x, y, rowh = 0, y + rowh + 1, 0
        pos[k] = (x, y)
        x += w + 1; rowh = max(rowh, h)
    return pos, y + rowh


def write_png(path, rgba):
    h, w, _ = rgba.shape
    raw = b"".join(b"\x00" + rgba[r].tobytes() for r in range(h))

    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)) \
        + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(data)


def rgb(c):
    return [int(v) for v in c]


def main():
    names = [p[0] for p in PARTS]
    assert len(names) == len(set(names)), "doppelte Teilnamen"
    pos, atlas_h = pack(PARTS)
    atlas = np.zeros((atlas_h, ATLAS_W, 4), np.uint8)
    table = []
    for k, (name, im, lx, ly) in enumerate(PARTS):
        x, y = pos[k]
        h, w = im.shape[:2]
        atlas[y:y + h, x:x + w] = im
        table.append({"name": name, "x": x, "y": y, "w": w, "h": h, "layoutX": lx, "layoutY": ly})
    info = {
        "image": "menu_parts.png",
        "width": ATLAS_W, "height": int(atlas_h),
        "designWidth": 854, "designHeight": 480,
        "notes": [
            "x, y, w, h: Rechteck des Teils im Bild; layoutX, layoutY: linke obere Ecke im 854 x 480 grossen Entwurf.",
            "panel: Tafel ohne Giebel, gezeichnet fuer das Rechteck shapes.panel; das Spiel zieht sie in neun Stuecken auf jede Groesse (Ecken slice.panel px bleiben 1:1).",
            "cursor und cursor_small: gezeichnet fuer shapes.cursor bzw. shapes.cursorSmall; drei Stuecke (Spitzen slice.cursor bzw. slice.cursorSmall px bleiben 1:1).",
            "gable: ueber der Mitte der oberen Kante einer Tafel; gableAnchor ist der Punkt (Mitte, Oberkante) der Tafel, zu dem layoutX/Y gehoeren.",
            "slider_empty und slider_full sind gleich gross: erst _empty, darueber _full links vom Stand beschnitten; knob mittig auf dem Stand (shapes.slider: x0, x1, Mitte y; knobCentre).",
            "tab_*: Rechtecke shapes.tabs (Vorlage); _active ist 8 px hoeher und traegt eine Raute.",
        ],
        "shapes": {
            "panel": list(PANEL),
            "cursor": list(CURSOR),
            "cursorSmall": list(CURSOR_SMALL),
            "slider": list(SL),
            "knobCentre": list(KNOB),
            "tabs": [list(t) for t in TABS],
            "gableAnchor": list(GABLE_ANCHOR),
        },
        "slice": {"panel": 8, "cursor": 24, "cursorSmall": 20},
        "game": {
            "text": {"main": rgb(V["BONE"]), "second": rgb(V["BONE2"]), "selected": rgb(V["ROYAL"]),
                     "quiet": rgb(V["IRON6"]), "shadow": rgb(M.FOG)},
            "sliderEdge": rgb(V["GOLD_W"]),
        },
        "parts": table,
    }
    os.makedirs(OUT_DIR, exist_ok=True)
    write_png(os.path.join(OUT_DIR, "menu_parts.png"), atlas)
    with open(os.path.join(OUT_DIR, "menu_parts.json"), "w", newline="\n", encoding="utf-8") as f:
        f.write(json.dumps(info, indent=1, ensure_ascii=False) + "\n")
    print("TEILE", len(table), "Atlas", ATLAS_W, "x", atlas_h)


main()
