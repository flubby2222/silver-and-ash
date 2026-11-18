"""Blatt der 13 HUD-Zeichen (Issue #381): Waffen (Klinge, Pistole, Gewehr, Donnerbuechse, Keule) und Gegenstaende (Eisenflasche, Jaegerauge,
Stiefel, Rosenkranz, Holster, Parade, Egel, Aschenherz), jeweils in ihrer Fassung, wie das Spiel sie aus assets/hud zeichnet.
Zeile 1: HUD-Groesse (1x, 854 x 480) hintereinander; weitere Zeilen: ganzzahlig 4-fach vergroessert, harte Pixel.

Aufruf:
  "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/hud-zeichen/blatt.py [-- <Ausgabeordner>]
Der Ausgabeordner muss der erste Wert nach "--" sein. Liest assets/hud (oder den Ordner nach "--ASSET").
"""
import sys, os, json, zlib, struct
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
sys.path.insert(0, os.path.join(REPO, "design", "proben", "grafik", "schrift"))
import schrift as S  # noqa: E402

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1 else HERE
ASSET = os.path.join(REPO, "assets", "hud")
FONT = S.font_a()


def read_png(path):
    d = open(path, "rb").read(); p = 8; idat = b""
    while p < len(d):
        n, t = struct.unpack(">I4s", d[p:p + 8]); body = d[p + 8:p + 8 + n]
        if t == b"IHDR":
            w, h = struct.unpack(">II", body[:8])
        elif t == b"IDAT":
            idat += body
        p += 12 + n
    raw = zlib.decompress(idat)
    return np.stack([np.frombuffer(raw[r * (w * 4 + 1) + 1:(r + 1) * (w * 4 + 1)], np.uint8) for r in range(h)]).reshape(h, w, 4)


def write_png(path, rgb):
    h, w, _ = rgb.shape
    raw = b"".join(b"\x00" + rgb[r].tobytes() for r in range(h))

    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    open(path, "wb").write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
                           + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


ATLAS = read_png(os.path.join(ASSET, "hud_parts.png"))
T = {p["name"]: p for p in json.load(open(os.path.join(ASSET, "hud_parts.json"), encoding="utf-8"))["parts"]}


def put(cv, name, ox, oy):
    p = T[name]
    im = ATLAS[p["y"]:p["y"] + p["h"], p["x"]:p["x"] + p["w"]].astype(np.float32)
    x, y = p["layoutX"] - ox, p["layoutY"] - oy
    a = im[..., 3:4] / 255
    reg = cv[y:y + p["h"], x:x + p["w"]]
    reg[:] = reg * (1 - a) + im[..., :3] * a


def tile(frame, icon):
    """Fassung mit Zeichen, 3 px Rand, auf dunklem Grund (Nebelton)."""
    f = T[frame]
    ox, oy = f["layoutX"] - 3, f["layoutY"] - 3
    cv = np.zeros((f["h"] + 6, f["w"] + 6, 3), np.float32); cv[:] = (16, 11, 20)
    put(cv, frame, ox, oy); put(cv, icon, ox, oy)
    return np.rint(cv).astype(np.uint8)


def text(cv, s, x, y, col, k=2):
    for ch in s:
        px, w = FONT.glyphs.get(ch, (set(), S.SPACE))
        for (gx, gy) in px:
            cv[y + gy * k:y + (gy + 1) * k, x + gx * k:x + (gx + 1) * k] = col
        x += (w + 1) * k


def textw(s, k=2):
    return sum((FONT.glyphs.get(c, (None, S.SPACE))[1] + 1) * k for c in s)


WEAPONS = [("Blade", "frame_weapon_0", "icon_blade"), ("Pistol", "frame_weapon_1", "icon_pistol"),
           ("Rifle", "frame_weapon_2", "icon_rifle"), ("Blunderbuss", "frame_weapon_3", "icon_blunder"),
           ("Club", "frame_weapon_0", "icon_club")]
ITEMS = [("Iron Flask", "icon_flask"), ("Hunter's Eye", "icon_eye"), ("Hunter's Boots", "icon_boots"), ("Silver Rosary", "icon_rosary"),
         ("Quickdraw Holster", "icon_holster"), ("Blade Parry", "icon_parry"), ("Leech", "icon_leech"), ("Ashen Heart", "icon_ashheart")]
NEW = {"Club", "Hunter's Boots", "Silver Rosary", "Quickdraw Holster", "Blade Parry", "Leech", "Ashen Heart"}


def weapons(active):
    s = "_active" if active else ""
    return [(n, tile(fr + s, ic + s)) for n, fr, ic in WEAPONS]


def items():
    # Das Auge liegt in Fassung 1, alle anderen in Fassung 0 (dort haben ihre Zeichen ihre Lage)
    return [(n, tile("frame_item_1" if ic == "icon_eye" else "frame_item_0", ic)) for n, ic in ITEMS]


def scaled(im, k):
    return np.repeat(np.repeat(im, k, 0), k, 1)


def row(entries, k, label, gap, w_total):
    cells = [(n, scaled(im, k)) for n, im in entries]
    h = max(c.shape[0] for _, c in cells) + 14 + 8 + 8 + (18 if k >= 4 else 0)
    cv = np.zeros((h, w_total, 3), np.uint8); cv[:] = (30, 26, 34)
    text(cv, label, 8, 4, (163, 175, 187), 2)
    x = 8
    for n, c in cells:
        y = 4 + 14 + 8
        cv[y:y + c.shape[0], x:x + c.shape[1]] = c
        if k >= 4:
            text(cv, n, x, y + c.shape[0] + 4, (226, 228, 230) if n in NEW else (126, 144, 165), 2)
        x += max(c.shape[1], textw(n, 2) if k >= 4 else 0) + gap
    return cv


def main():
    ws, wa, it = weapons(False), weapons(True), items()
    W = 1280
    rows = [row(ws + it, 1, "HUD-GROESSE 854 X 480 (1X)", 6, W),
            row(ws, 4, "WAFFEN RUHIG, 4X (HELL: NEU)", 14, W), row(wa, 4, "WAFFEN GEWAEHLT, 4X", 14, W),
            row(it[:4], 4, "GEGENSTAENDE 1, 4X", 14, W), row(it[4:], 4, "GEGENSTAENDE 2, 4X", 14, W)]
    sheet = np.concatenate(rows, 0)
    os.makedirs(OUT, exist_ok=True)
    write_png(os.path.join(OUT, "blatt-hud-zeichen.png"), sheet)
    print("Blatt", sheet.shape[1], "x", sheet.shape[0])


main()
