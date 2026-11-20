"""Teile fuer den wachsenden Lebensbalken oben links und die flache Ausdauer-Fassung (Issue #435, Layout B).

Aufruf (Blender dient nur als Python mit numpy; nichts wird heruntergeladen):
  "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/hud/hud_lebensbalken.py [-- <Ausgabeordner>]

Reihenfolge: erst tools/hud/hud_teile.py, dann tools/hud/hud_atlas_doppelt.py, dann dieses Skript. Es liest die fertigen Atlanten
assets/hud/hud_parts_x2.(png|json) und assets/hud/hud_parts.(png|json), nimmt seine eigenen Teile (Feld "source") heraus und haengt
sie neu unten an beide Atlanten an. Zwei Laeufe hintereinander liefern bitgleiche Dateien. Ohne Ausgabeordner schreibt es nach
assets/hud/, sonst nach <Ordner>/hud/.

Die Teile entstehen wie in der abgenommenen Probe design/proben/grafik/lebensbalken-435/lebensbalken_runde2.py aus Teilen des doppelt
feinen Atlas, nur gestreckt und geschnitten, ohne neue Farbe:
  frame_life, life_frame_end      Fassung aus frame_xp (8 Zeilen hoeher), lang gestreckt; das Spiel schneidet sie von rechts auf die
                                  Laenge des Balkens und setzt den Abschluss life_frame_end dahinter.
  life_bar_empty/_full,           Lebensleiste aus life_empty/life_full, lang gestreckt, ebenso geschnitten; life_cap_empty/_full ist
  life_cap_empty/_full            ihr rechtes Ende.
  life_tick(_long)_full/_empty    Teilstrich je Upgrade, jeder fuenfte lang; dunkel ueber dem vollen, grau ueber dem leeren Teil.
  frame_stamina                   frame_vitals ohne die Zeile des Lebens (30 Zeilen kuerzer), fuer die Ausdauer allein.
Der einfache Atlas bekommt dieselben Teile mit jedem zweiten Pixel (genau halbe Lage und Groesse).
"""
import sys, os, re, json, zlib, struct
import numpy as np
import importlib.util

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(REPO, "assets", "hud")
OUT = os.path.join(sys.argv[sys.argv.index("--") + 1], "hud") if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1 else SRC
MARK = "tools/hud/hud_lebensbalken.py"
# Nieten als Relief (Issue #412): am Lebensbalken je eine auf dem unteren Rand unter jedem langen Strich (jedes fuenfte Upgrade)
_nspec = importlib.util.spec_from_file_location("hud_nieten", os.path.join(HERE, "hud_nieten.py"))
NIETEN = importlib.util.module_from_spec(_nspec)
_nspec.loader.exec_module(NIETEN)
STEP = int(re.search(r'"health"\s*:\s*(\d+)', open(os.path.join(REPO, "assets", "balance.json"), encoding="utf-8").read()).group(1))  # Lebenspunkte je Upgrade
PXHP = 2                     # Pixel je Lebenspunkt im doppelten Raster
LONG_EVERY = 5               # jeder fuenfte Strich ist lang (wie LifeBar.LongTickEvery)

MAX_BAR = 640               # laengste Lebensleiste im doppelten Raster (320 px bei 854 x 480, also bis 320 Lebenspunkte)
FRAME_CAP, BAR_CAP = 14, 16  # rechte Abschluesse im doppelten Raster (wie in der Probe)
DARK, DIM = (20, 14, 24), (100, 100, 100)
LIFE_X, LIFE_Y = 72, 2       # Lage im Entwurf 1708 x 960 wie in der Probe (nur fuer layoutX/Y)


# ---------------------------------------------------------------- PNG lesen und schreiben (RGBA, 8 Bit)
def read_png(path):
    d = open(path, "rb").read()
    p, idat = 8, b""
    while p < len(d):
        n, t = struct.unpack(">I4s", d[p:p + 8])
        body = d[p + 8:p + 8 + n]
        if t == b"IHDR":
            w, h, _, ct = struct.unpack(">IIBB", body[:10])
        elif t == b"IDAT":
            idat += body
        p += 12 + n
    assert ct == 6, "nur RGBA"
    raw = zlib.decompress(idat)
    st, ch = w * 4, 4
    out = np.zeros((h, st), np.uint8)
    prev = np.zeros(st, np.int32)
    for r in range(h):
        f = raw[r * (st + 1)]
        line = np.frombuffer(raw[r * (st + 1) + 1:(r + 1) * (st + 1)], np.uint8).astype(np.int32)
        if f == 0:
            cur = line
        elif f == 2:
            cur = (line + prev) & 255
        else:
            cur = np.zeros(st, np.int32)
            for i in range(st):
                a = cur[i - ch] if i >= ch else 0
                b = prev[i]
                c = prev[i - ch] if i >= ch else 0
                if f == 1:
                    v = line[i] + a
                elif f == 3:
                    v = line[i] + ((a + b) >> 1)
                else:
                    pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                    v = line[i] + (a if (pa <= pb and pa <= pc) else (b if pb <= pc else c))
                cur[i] = v & 255
        out[r] = cur
        prev = cur
    return out.reshape(h, w, 4)


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


# ---------------------------------------------------------------- Strecken und Schneiden (wie in der Probe)
def hstretch(im, new_w, left, right, tile_at, tile_w=2):
    h, w = im.shape[:2]
    out = np.zeros((h, new_w, 4), np.uint8)
    out[:, :left] = im[:, :left]
    out[:, new_w - right:] = im[:, w - right:]
    strip = im[:, tile_at:tile_at + tile_w]
    for x in range(left, new_w - right):
        out[:, x] = strip[:, (x - left) % tile_w]
    return out


def vadd(im, row, n):
    return np.concatenate([im[:row + 1]] + [im[row:row + 1]] * n + [im[row + 1:]], 0)


def vcut(im, r0, r1):
    return np.concatenate([im[:r0], im[r1:]], 0)


def tick(h, col):
    t = np.zeros((h, 2, 4), np.uint8)
    t[..., :3] = col
    t[..., 3] = 255
    return t


# ---------------------------------------------------------------- Teile im doppelten Raster: (Name, Bild, layoutX, layoutY, body)
def make_parts(atlas, table):
    def part(name):
        p = table[name]
        return atlas[p["y"]:p["y"] + p["h"], p["x"]:p["x"] + p["w"]].copy()

    xp = table["frame_xp"]
    frame = hstretch(vadd(part("frame_xp"), 24, 8), MAX_BAR + 20, FRAME_CAP, FRAME_CAP, 50)
    fw = frame.shape[1] - FRAME_CAP
    body = [xp["body"][0], xp["body"][1], fw + FRAME_CAP - 2 * xp["body"][0], xp["body"][3] + 8]
    bx, by = LIFE_X + 10, LIFE_Y + 14
    out = [("frame_life", frame[:, :fw], LIFE_X, LIFE_Y, body),
           ("life_frame_end", frame[:, fw:], LIFE_X + fw, LIFE_Y, None)]
    bw = MAX_BAR - BAR_CAP
    for state in ("empty", "full"):
        bar = hstretch(part("life_" + state), MAX_BAR, BAR_CAP, BAR_CAP, 40)
        out.append(("life_bar_" + state, bar[:, :bw], bx, by, None))
        out.append(("life_cap_" + state, bar[:, bw:], bx + bw, by, None))
    for state, col in (("full", DARK), ("empty", DIM)):
        out.append(("life_tick_" + state, tick(8, col), bx + BAR_CAP + 10, by + 16, None))
        out.append(("life_tick_long_" + state, tick(18, col), bx + BAR_CAP + 50, by + 6, None))
    fv = table["frame_vitals"]
    b = fv["body"]
    out.append(("frame_stamina", vcut(part("frame_vitals"), 20, 50), fv["layoutX"], fv["layoutY"] + 30, [b[0], b[1], b[2], b[3] - 30]))
    for name, im, lx, ly, bd in out:
        assert im.shape[0] % 2 == 0 and im.shape[1] % 2 == 0 and lx % 2 == 0 and ly % 2 == 0, name
        assert bd is None or all(v % 2 == 0 for v in bd), name
    return out


def add_rivets(parts):
    """Nieten unter den langen Strichen auf dem unteren Rand der Fassung des Lebensbalkens (Teile werden veraendert).

    Die Striche stehen bei x = Balkenanfang + Abschluss + Schritt * Pixel je Lebenspunkt * i (wie LifeBar.Shape); der Balken beginnt
    10 px (doppeltes Raster) hinter dem Anfang der Fassung. Das Spiel schneidet die Fassung von rechts, die Lage vom linken Ende
    bleibt, also stehen die Nieten immer genau unter den Strichen.
    """
    for name, im, lx, ly, bd in parts:
        if name != "frame_life":
            continue
        pitch = STEP * PXHP
        y = bd[1] + bd[3] - 1 - round(2.2 * 2) // 2   # Mitte des unteren Randes (Randstaerke 4,4 px)
        x = 10 + BAR_CAP + pitch * LONG_EVERY
        while x + 4 < im.shape[1] - 6:
            NIETEN.stamp_dome_u8(im, x, y, 3)
            x += pitch * LONG_EVERY


# ---------------------------------------------------------------- Anhaengen
def pack(parts, atlas_w):
    order = sorted(range(len(parts)), key=lambda k: (-parts[k][1].shape[0], parts[k][0]))
    x = y = rowh = 0
    pos = {}
    for k in order:
        h, w = parts[k][1].shape[:2]
        if x + w > atlas_w:
            x, y, rowh = 0, y + rowh + 1, 0
        pos[k] = (x, y)
        x += w + 1
        rowh = max(rowh, h)
    return pos, y + rowh


def append(name, parts):
    """Nimmt die eigenen Teile aus dem Atlas <name> heraus und haengt <parts> unten an."""
    table = json.load(open(os.path.join(SRC, name + ".json"), encoding="utf-8"))
    atlas = read_png(os.path.join(SRC, name + ".png"))
    keep = [p for p in table["parts"] if p.get("source") != MARK]
    base_h = max(p["y"] + p["h"] for p in keep)
    atlas = atlas[:base_h]
    pos, h = pack(parts, table["width"])
    top = base_h + 1
    grown = np.zeros((top + h, table["width"], 4), np.uint8)
    grown[:base_h] = atlas
    for k, (pname, im, lx, ly, body) in enumerate(parts):
        x, y = pos[k]
        grown[top + y:top + y + im.shape[0], x:x + im.shape[1]] = im
        e = {"name": pname, "x": x, "y": top + y, "w": im.shape[1], "h": im.shape[0], "layoutX": lx, "layoutY": ly}
        if body is not None:
            e["body"] = body
        e["source"] = MARK
        keep.append(e)
    table["parts"] = keep
    table["height"] = int(grown.shape[0])
    os.makedirs(OUT, exist_ok=True)
    write_png(os.path.join(OUT, name + ".png"), grown)
    with open(os.path.join(OUT, name + ".json"), "w", newline="\n", encoding="utf-8") as f:
        f.write(json.dumps(table, indent=1, ensure_ascii=False) + "\n")
    print(name, "Teile", len(keep), "Atlas", table["width"], "x", table["height"], flush=True)


def main():
    x2 = json.load(open(os.path.join(SRC, "hud_parts_x2.json"), encoding="utf-8"))
    fine = make_parts(read_png(os.path.join(SRC, "hud_parts_x2.png")), {p["name"]: p for p in x2["parts"]})
    add_rivets(fine)
    plain = [(n, im[::2, ::2].copy(), lx // 2, ly // 2, None if bd is None else [v // 2 for v in bd]) for n, im, lx, ly, bd in fine]
    append("hud_parts_x2", fine)
    append("hud_parts", plain)


main()
