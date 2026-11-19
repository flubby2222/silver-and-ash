"""Runde 2 zum Lebensbalken (Issue #435) und Schmuck der Rahmen (Issue #412, Teil 1): nur Probe, kein Einbau.

Aufruf (Blender nur als Python mit numpy):
  "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/lebensbalken-435/lebensbalken_runde2.py [-- <Ausgabeordner>]

Liest nur Fertiges: assets/hud/hud_parts_x2.(png|json) und assets/menu/menu_parts_x2.(png|json) (das neue HUD-Design, wie das Spiel es heute
zeichnet), assets/balance.json, basis-854.png (Foto hud-calm ohne HUD, 2fach vergroessert als Hintergrund), Schrift A "Kapitell".
Entwurfsraster ist 1708 x 960 (2 Pixel je Pixel des 854er Rasters). Die Bilder in 854 x 480 nehmen jeden zweiten Pixel, die in 1920 x 1080
den naechsten Pixel (Faktor 1,124), beides ohne Glaettung. Schmuck (S1 bis S3) entsteht hier Pixel fuer Pixel mit den sechs Silberstufen,
die im Rahmen schon vorkommen; keine neue Farbe. Schreibt blatt-lebensbalken-runde2.png, blatt-schmuck-runde2.png und einige Einzelbilder.
"""
import sys, os, re, json, zlib, struct
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
sys.path.insert(0, os.path.join(HERE, "..", "schrift"))
import schrift  # noqa: E402

OUT = HERE
if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1:
    OUT = sys.argv[sys.argv.index("--") + 1]
os.makedirs(OUT, exist_ok=True)


# ------------------------------------------------------------------ PNG
def read_png(path):
    d = open(path, "rb").read()
    p = 8; idat = b""
    while p < len(d):
        n, t = struct.unpack(">I4s", d[p:p + 8])
        body = d[p + 8:p + 8 + n]
        if t == b"IHDR":
            w, h, bd, ct = struct.unpack(">IIBB", body[:10])
        elif t == b"IDAT":
            idat += body
        p += 12 + n
    raw = zlib.decompress(idat)
    ch = {6: 4, 2: 3}[ct]
    st = w * ch
    rows = []
    prev = np.zeros(st, np.int32)
    for r in range(h):
        f = raw[r * (st + 1)]
        line = np.frombuffer(raw[r * (st + 1) + 1:(r + 1) * (st + 1)], np.uint8).astype(np.int32)
        if f == 0:
            cur = line
        else:
            cur = np.zeros(st, np.int32)
            for i in range(st):
                a = cur[i - ch] if i >= ch else 0
                b = prev[i]
                c = prev[i - ch] if i >= ch else 0
                if f == 1: v = line[i] + a
                elif f == 2: v = line[i] + b
                elif f == 3: v = line[i] + ((a + b) >> 1)
                else:
                    pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                    pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                    v = line[i] + pr
                cur[i] = v & 255
        rows.append(cur.astype(np.uint8)); prev = cur
    return np.stack(rows).reshape(h, w, ch)


def write_png(path, a):
    a = np.ascontiguousarray(np.clip(np.rint(a), 0, 255).astype(np.uint8))
    h, w, c = a.shape
    raw = b"".join(b"\x00" + a[r].tobytes() for r in range(h))
    def chunk(t, b):
        return struct.pack(">I", len(b)) + t + b + struct.pack(">I", zlib.crc32(t + b) & 0xffffffff)
    ct = {3: 2, 4: 6}[c]
    open(path, "wb").write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, ct, 0, 0, 0))
                           + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


# ------------------------------------------------------------------ Eingaben
HUD = read_png(os.path.join(REPO, "assets", "hud", "hud_parts_x2.png"))
HT = {p["name"]: p for p in json.load(open(os.path.join(REPO, "assets", "hud", "hud_parts_x2.json"), encoding="utf-8"))["parts"]}
MEN = read_png(os.path.join(REPO, "assets", "menu", "menu_parts_x2.png"))
MT = {p["name"]: p for p in json.load(open(os.path.join(REPO, "assets", "menu", "menu_parts_x2.json"), encoding="utf-8"))["parts"]}
B854 = read_png(os.path.join(HERE, "basis-854.png"))[..., :3].astype(np.float32)
assert B854.shape == (480, 854, 3), B854.shape
BASE = np.repeat(np.repeat(B854, 2, 0), 2, 1)          # 1708 x 960
FONT = schrift.font_a()
DW, DH = 1708, 960

bal = open(os.path.join(REPO, "assets", "balance.json"), encoding="utf-8").read()
bal = json.loads(re.sub(r"//[^\n]*", "", bal))
HP0 = bal["player"]["maxHealth"]
STEP = bal["boons"]["health"]
PXHP = 2                                   # Pixel je Lebenspunkt im 1708er Raster (1 px bei 854 x 480)
UP_MID = 10
HP_MID = HP0 + UP_MID * STEP
BOSS_X = HT["frame_boss"]["layoutX"]       # 592
LIFE_X = 72
CAP = (BOSS_X - 12 - LIFE_X - 20) // PXHP  # Rahmen endet 12 px (6 px bei 854) vor dem Boss-Rahmen: 244
HP_MAX = (CAP // STEP) * STEP              # ganze Upgrades: 240; gezeigt wird 235 wie in Runde 1
HP_MAX = 235
PITCH = STEP * PXHP                        # ein Strich je Upgrade: 10 px

DARK = (20, 14, 24)
BONE, BONE2, GOLD, PAPER = (216, 210, 192), (195, 192, 164), (214, 170, 64), (20, 16, 26)
SHADOW = (16, 11, 20)
SIL = [72, 100, 132, 166, 200, 230]
L0, L1, L2, L3, L4, L5 = [(v, v, v) for v in SIL]


def part(a, t, name):
    p = t[name]
    return a[p["y"]:p["y"] + p["h"], p["x"]:p["x"] + p["w"]].copy()


def blit(cv, im, x, y, clip_x=None):
    h, w = im.shape[:2]
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(cv.shape[1], x + w), min(cv.shape[0], y + h)
    if x1 <= x0 or y1 <= y0:
        return
    s = im[y0 - y:y1 - y, x0 - x:x1 - x].astype(np.float32)
    a = s[..., 3:4] / 255
    if clip_x is not None:
        a = a * ((np.arange(x0, x1) + 0.5) < clip_x)[None, :, None]
    cv[y0:y1, x0:x1] = cv[y0:y1, x0:x1] * (1 - a) + s[..., :3] * a


def put(cv, name, at=None, clip_x=None):
    p = HT[name]
    x, y = at if at else (p["layoutX"], p["layoutY"])
    blit(cv, part(HUD, HT, name), x, y, clip_x)


# ------------------------------------------------------------------ Strecken
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


def life_frame(w_bar):
    """Fassung des Lebensbalkens: frame_xp, 8 Zeilen hoeher (Balken 28 statt 20), Breite = Balken + 20."""
    fr = vadd(part(HUD, HT, "frame_xp"), 24, 8)          # 50 hoch, Koerper 36
    return hstretch(fr, w_bar + 20, 14, 14, 50)


def life_bar(name, w):
    im = part(HUD, HT, name)
    return hstretch(im, w, 16, 16, 40)


def stamina_frame():
    fv = part(HUD, HT, "frame_vitals")                  # 732 x 76; Leben (y 18..46) entfaellt
    return vcut(fv, 18, 48)                             # 46 hoch, Ausdauer bleibt unten buendig


# ------------------------------------------------------------------ Schrift
def text_px(s, x, base, scale=1, anchor="l"):
    s = s.replace("ß", "ss")
    bare = s
    for a, b in (("ä", "a"), ("ö", "o"), ("ü", "u"), ("Ä", "A"), ("Ö", "O"), ("Ü", "U")):
        bare = bare.replace(a, b)
    w = FONT.width(bare, 1) * scale
    if anchor == "c": x -= w // 2
    elif anchor == "r": x -= w
    pts, cx = [], 0
    for ch in s:
        if ch == " ":
            cx += schrift.SPACE + schrift.GAP; continue
        g = {"ä": "a", "ö": "o", "ü": "u", "Ä": "A", "Ö": "O", "Ü": "U"}.get(ch, ch)
        px, gw = FONT.glyphs[g]
        for gx, gy in px:
            pts.append((cx + gx, gy - FONT.base))
        if ch in "äöü": pts += [(cx + 1, -6), (cx + 3, -6)]
        if ch in "ÄÖÜ": pts += [(cx + 1, -8), (cx + 3, -8)]
        cx += gw + schrift.GAP
    out = []
    for px_, py_ in pts:
        for dy in range(scale):
            for dx in range(scale):
                out.append((x + px_ * scale + dx, base + py_ * scale + dy))
    return out, w


def text(cv, s, x, base, color=BONE, scale=1, anchor="l", shadow=True):
    pts, w = text_px(s, x, base, scale, anchor)
    H, W, _ = cv.shape
    if shadow:
        for px, py in pts:
            sx, sy = px + scale, py + scale
            if 0 <= sx < W and 0 <= sy < H: cv[sy, sx] = SHADOW
    for px, py in pts:
        if 0 <= px < W and 0 <= py < H: cv[py, px] = color
    return w


# ------------------------------------------------------------------ Schmuck (Issue #412): nur Silberstufen aus dem Rahmen
PAD = 12


def grey_mask(im):
    r, g, b, a = (im[..., i].astype(int) for i in range(4))
    return (a == 255) & (r == g) & (g == b) & np.isin(r, SIL)


def rim_box(im):
    ys, xs = np.nonzero(grey_mask(im))
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())


def rim_thick(im, box):
    x0, y0, x1, y1 = box
    m = grey_mask(im)
    cx = x0 + (x1 - x0) // 3
    t = 0
    while m[y0 + t, cx]:
        t += 1
    return t


def setpx(im, x, y, col):
    if 0 <= y < im.shape[0] and 0 <= x < im.shape[1]:
        im[y, x, :3] = col; im[y, x, 3] = 255


def dome(im, cx, cy, r):
    """Runde Niete: Rand L0, Flaeche L3, Licht oben links L5, Schatten unten rechts L1."""
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            d2 = dx * dx + dy * dy
            if d2 > r * r + 1:
                continue
            if d2 > (r - 1) * (r - 1) + 1:
                col = L0
            elif dx + dy <= -r + 1:
                col = L5
            elif dx + dy >= r - 1:
                col = L1
            else:
                col = L3
            setpx(im, cx + dx, cy + dy, col)


def plate(im, box, t, sx, sy, arm=26):
    """Eckwinkel: zwei Arme (Dicke t + 3, Laenge arm) mit Kante aus Licht und Schatten und einer Niete im Knick."""
    x0, y0, x1, y1 = box
    p = t + 3
    mask = set()
    for u in range(arm):
        for v in range(p):
            mask.add((u, v))
    for u in range(p):
        for v in range(arm):
            mask.add((u, v))
    for (u, v) in mask:
        X = x0 + u if sx > 0 else x1 - u
        Y = y0 + v if sy > 0 else y1 - v
        col = L2
        if (u + 1, v) not in mask:                    # Kante zum Innenraum in x-Richtung
            col = L0 if sx > 0 else L4
        if (u, v + 1) not in mask:                    # Kante zum Innenraum in y-Richtung
            col = L0 if sy > 0 else L5
        if u == 0: col = L4 if sx > 0 else L1       # aussen: links hell, rechts dunkel
        if v == 0: col = L5 if sy > 0 else L1       # aussen: oben hell, unten dunkel
        setpx(im, X, Y, col)
    cx = x0 + p // 2 + 1 if sx > 0 else x1 - p // 2 - 1
    cy = y0 + p // 2 + 1 if sy > 0 else y1 - p // 2 - 1
    dome(im, cx, cy, 2)


def block(im, x, y, w, h):
    for j in range(h):
        for i in range(w):
            col = L2
            if j == 0: col = L5
            elif i == 0: col = L4
            elif i == w - 1: col = L1
            setpx(im, x + i, y + j, col)


def gable(im, cx, y0, t, widths=(40, 30, 20), sh=4):
    """Giebel aus drei Stufen ueber dem oberen Rand, auf einem Sockel in der Randdicke; Spitzbogenfenster in der untersten Stufe."""
    block(im, cx - widths[0] // 2 - 2, y0, widths[0] + 4, t)
    for k, w in enumerate(widths):
        block(im, cx - w // 2, y0 - (k + 1) * sh, w, sh)
    for j in range(sh):
        for i in range(2):
            setpx(im, cx - 1 + i, y0 - sh + j, L0)


def ornament(im, style, kind="rahmen", ticks=None):
    """Bild mit PAD Rand ringsum; style None, S1 (Nieten), S2 (Eckwinkel), S3 (Giebel). Ergebnis: neues Bild, Offset PAD."""
    out = np.zeros((im.shape[0] + 2 * PAD, im.shape[1] + 2 * PAD, 4), np.uint8)
    out[PAD:-PAD, PAD:-PAD] = im
    if style is None:
        return out
    box = rim_box(out)
    x0, y0, x1, y1 = box
    t = rim_thick(out, box)
    if style == "S1":
        for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
            dome(out, x0 + 4 if sx > 0 else x1 - 4, y0 + 4 if sy > 0 else y1 - 4, 4)
        if ticks:                                   # Nieten auf dem unteren Rand an den langen Strichen
            for tx in ticks:
                dome(out, PAD + tx, y1 - t // 2, 3)
        else:
            n = max(0, (x1 - x0) // 80 - 1)
            for i in range(1, n + 1):
                px = x0 + (x1 - x0) * i // (n + 1)
                dome(out, px, y0 + t // 2, 3); dome(out, px, y1 - t // 2, 3)
    elif style == "S2":
        for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
            plate(out, box, t, sx, sy)
    elif style == "S3":
        if kind == "leben":
            gable(out, x1 - 30, y0, t)
        elif kind == "tab":
            w = x1 - x0
            for cx in (x0 + w // 7, x1 - w // 7):
                gable(out, cx, y0, t, (30, 20, 10))
        else:
            gable(out, (x0 + x1) // 2, y0, t)
    return out


# ------------------------------------------------------------------ HUD zusammensetzen (Layout B im neuen Design)
def common(cv, boss=False):
    for i in range(2):
        put(cv, f"frame_item_{i}")
    put(cv, "icon_flask"); put(cv, "icon_eye")
    for i, g in enumerate(("blade", "pistol", "rifle", "blunder")):
        act = i == 1
        put(cv, f"frame_weapon_{i}" + ("_active" if act else ""))
        put(cv, f"icon_{g}" + ("_active" if act else ""))
    put(cv, "frame_currency"); put(cv, "icon_coin"); put(cv, "icon_key")
    for nm, s in (("icon_coin", "85"), ("icon_key", "1")):
        p = HT[nm]
        text(cv, s, p["layoutX"] + p["w"] + 10, p["layoutY"] + p["h"] // 2 + 10, BONE, 3)
    if boss:
        put(cv, "frame_boss"); put(cv, "boss_empty"); put(cv, "boss_full", clip_x=HT["boss_full"]["layoutX"] + 452 * 0.7)
    put(cv, "crosshair")


def level(cv, x, y):
    blit(cv, part(HUD, HT, "frame_level"), x, y)
    text(cv, "0", x + 6 + 26, y + 10 + 28 + 10, BONE, 3, "c")


def skills(cv, x, y):
    for i, g in enumerate(("flame", "shield")):
        fx = x + i * 56
        blit(cv, part(HUD, HT, "frame_skill_%d" % i), fx, y)
        p = HT["icon_" + g]
        blit(cv, part(HUD, HT, "icon_" + g), fx + 6 + (48 - p["w"]) // 2, y + 10 + (56 - p["h"]) // 2)


def draw_hud(hp, frac, orn=None, boss=False, bg=None):
    cv = (BASE if bg is None else bg).copy()
    common(cv, boss)
    w = hp * PXHP
    fr = ornament(life_frame(w), orn, "leben", ticks=[10 + 16 + PITCH * i for i in range(5, (w - 32) // PITCH + 1, 5)])
    blit(cv, fr, LIFE_X - PAD, 2 - PAD)                       # Koerper y 12..48
    bx, by = LIFE_X + 10, 2 + 14
    emp, ful = life_bar("life_empty", w), life_bar("life_full", w)
    blit(cv, emp, bx, by)
    edge = bx + 16 + (w - 32) * frac
    blit(cv, ful, bx, by, clip_x=edge if frac < 1 else None)
    for i, tx in enumerate(range(bx + 16 + PITCH, bx + w - 16, PITCH), 1):
        y0 = by + (6 if i % 5 == 0 else 16)
        cv[y0:by + 24, tx:tx + 2] = DARK if tx < edge else (100, 100, 100)
    put(cv, "frame_xp", (LIFE_X, 50)); put(cv, "xp_empty", (LIFE_X + 10, 64))   # Koerper y 60..88
    level(cv, 10, 12)
    skills(cv, 10, 90)
    sf = stamina_frame()
    blit(cv, sf, HT["frame_vitals"]["layoutX"], DH - sf.shape[0])
    for i in range(5):
        put(cv, f"stamina_{i}_empty"); put(cv, f"stamina_{i}_full")
    return cv


def draw_heute(frac=0.7):
    cv = BASE.copy()
    common(cv)
    level(cv, 10, 0)
    put(cv, "frame_xp"); put(cv, "xp_empty")
    skills(cv, 206, 0)
    put(cv, "frame_vitals"); put(cv, "life_empty"); put(cv, "life_full", clip_x=510 + 688 * frac)
    for i in range(5):
        put(cv, f"stamina_{i}_empty"); put(cv, f"stamina_{i}_full")
    return cv


def to1920(cv):
    xi = np.minimum((np.arange(1920) / 1920 * DW).astype(int), DW - 1)
    yi = np.minimum((np.arange(1080) / 1080 * DH).astype(int), DH - 1)
    return cv[yi][:, xi]


def shrink(a, w, h):
    H, W = a.shape[:2]
    xe = np.linspace(0, W, w + 1).astype(int); ye = np.linspace(0, H, h + 1).astype(int)
    s = np.add.reduceat(np.add.reduceat(a.astype(np.float32), ye[:-1], 0), xe[:-1], 1)
    return s / np.outer(np.diff(ye), np.diff(xe))[..., None]


# ------------------------------------------------------------------ Menue (Karte, Ausruestung): Reiter mit Schmuck
def draw_menu(orn):
    cv = np.tile(np.array((14, 10, 20), np.float32), (DH, DW, 1))
    blit(cv, part(MEN, MT, "panel"), MT["panel"]["layoutX"], MT["panel"]["layoutY"])
    for name, label, act in (("tab_map_active", "MAP", True), ("tab_equipment", "EQUIPMENT", False)):
        p = MT[name]
        im = ornament(part(MEN, MT, name), orn, "tab")
        blit(cv, im, p["layoutX"] - PAD, p["layoutY"] - PAD)
        x0, y0, x1, y1 = rim_box(im)
        cy = p["layoutY"] - PAD + (y0 + y1) // 2 + 14 + (8 if act else 0)
        text(cv, label, p["layoutX"] - PAD + (x0 + x1) // 2, cy, BONE if act else BONE2, 4, "c")
    return cv


# ------------------------------------------------------------------ Blatt
def paste(sheet, im, x, y):
    sheet[y:y + im.shape[0], x:x + im.shape[1]] = im


def box(sheet, x, y, w, h, col=(90, 86, 100)):
    sheet[y - 1:y + h + 1, x - 1] = col; sheet[y - 1:y + h + 1, x + w] = col
    sheet[y - 1, x - 1:x + w + 1] = col; sheet[y + h, x - 1:x + w + 1] = col


def zoom(a, k):
    return np.repeat(np.repeat(a, k, 0), k, 1)


def lines(sheet, ls, x, y, scale=2, col=BONE2, dy=22):
    for i, s in enumerate(ls):
        text(sheet, s, x, y + i * dy, col, scale)
    return y + len(ls) * dy


def sheet_leben():
    W = 2400
    sh = np.tile(np.array(PAPER, np.float32), (2120, W, 1))
    text(sh, "LEBENSBALKEN, LAYOUT B IM NEUEN HUD-DESIGN (ISSUE #435, RUNDE 2)", 10, 40, BONE, 4)
    lines(sh, ["Echter HUD-Atlas in doppelter Auflösung (hud_parts_x2), harte Pixel, neutrales Silber. Start %d Leben, +%d je Upgrade; im Entwurf 1708 x 960 sind es %d px je Lebenspunkt," % (HP0, STEP, PXHP),
               "also %d px je Upgrade (ein Strich), jeder fünfte Strich lang. Bei 854 x 480 ist es 1 px je Lebenspunkt, bei 1920 x 1080 etwa %.2f px. Endkappe wandert mit." % (PITCH, PXHP * 1920 / DW)],
          10, 78, 2)
    full = draw_hud(HP_MAX, 1.0, None, True)
    s1920 = to1920(full)
    text(sh, "GANZER BILDSCHIRM, HÖCHSTSTAND %d MIT BOSS-LEISTE" % HP_MAX, 10, 152, GOLD, 3)
    v = shrink(s1920, 1200, 675)
    paste(sh, v, 10, 170); box(sh, 10, 170, 1200, 675)
    text(sh, "1920 x 1080, hier auf 1200 breit verkleinert", 10, 870, BONE2, 2)
    v854 = full[0::2, 0::2]
    paste(sh, v854, 1260, 170); box(sh, 1260, 170, 854, 480)
    text(sh, "854 x 480, jeder zweite Pixel, 1:1", 1260, 676, BONE2, 2)
    lines(sh, ["Obergrenze im Entwurf: %d Lebenspunkte (Rahmen endet 12 px vor" % CAP, "dem Boss-Rahmen, bei 854 x 480 sind es 6 px). Gezeigt: %d." % HP_MAX,
               "Stufe sitzt auf der Mitte des Stapels, Fähigkeiten in zweiter", "Zeile darunter, Erfahrung bleibt 112 px lang."], 1260, 720, 2)
    y = 940
    text(sh, "ZUSTÄNDE IM ENTWURFSRASTER (1708 x 960, 1:1, Ausschnitt oben links)", 10, y + 6, GOLD, 3)
    states = [("START %d" % HP0, HP0, 1.0), ("MITTEL %d (+%d UPGRADES)" % (HP_MID, UP_MID), HP_MID, 1.0),
              ("HÖCHST %d" % HP_MAX, HP_MAX, 1.0), ("MITTEL, HALB LEER", HP_MID, 0.5)]
    for i, (lab, hp, fr) in enumerate(states):
        x = 10 + i * 600
        c = draw_hud(hp, fr)[0:190, 0:580]
        text(sh, lab, x, y + 40, BONE, 2)
        paste(sh, c, x, y + 52); box(sh, x, y + 52, 580, 190)
    y2 = y + 300
    text(sh, "AUSDAUER UNTEN IN DER MITTE UND 1920 x 1080 IM ORIGINALMASS", 10, y2 + 6, GOLD, 3)
    c = full[DH - 80:DH, 440:1270]
    text(sh, "Ausdauer (Fassung nur noch 46 hoch)", 10, y2 + 40, BONE, 2)
    paste(sh, c, 10, y2 + 52); box(sh, 10, y2 + 52, 830, 80)
    for j, (lab, hp) in enumerate((("1920, Start %d" % HP0, HP0), ("1920, Höchst %d" % HP_MAX, HP_MAX))):
        x = 880 + j * 760
        im = to1920(draw_hud(hp, 1.0))[0:210, 0:740]
        text(sh, lab, x, y2 + 40, BONE, 2)
        paste(sh, im, x, y2 + 52); box(sh, x, y2 + 52, 740, 210)
    y3 = y2 + 330
    text(sh, "HEUTE (NEUES DESIGN, LEBEN FEST UNTEN IN DER MITTE) ZUM VERGLEICH", 10, y3, GOLD, 3)
    h = draw_heute()
    paste(sh, shrink(h, 854, 480), 10, y3 + 20); box(sh, 10, y3 + 20, 854, 480)
    lines(sh, ["Heute: Leben 344 px (bei 854 x 480) fest unten, kürzt und wächst nie.", "Neu (B): Leben oben über der Erfahrung, 100 px bei Start,",
               "wächst mit jedem Upgrade um 5 px bis zur Obergrenze;", "Ausdauer bleibt unten in der Mitte, Fassung 46 statt 76 hoch."], 900, y3 + 60, 2)
    return sh[:y3 + 540], full


def sheet_schmuck():
    W = 2800
    sh = np.tile(np.array(PAPER, np.float32), (2830, W, 1))
    text(sh, "SCHMUCK FÜR RAHMEN UND REITER (ISSUE #412, TEIL 1) - EMPFEHLUNG %s" % RECO, 10, 40, BONE, 4)
    lines(sh, ["Neues HUD-Design (hud_parts_x2, menu_parts_x2): harte Pixel, nur die sechs Silberstufen des Rahmens, keine neue Farbe, kein Glühen.",
               "Links oben jeweils der heutige schlichte Rahmen. S1 Nieten, S2 Eckwinkel, S3 Giebel. Gezeigt am Lebensbalken (Layout B) und am Reiter der Karte."], 10, 78, 2)
    names = [(None, "HEUTE, SCHLICHT"), ("S1", "S1 NIETEN"), ("S2", "S2 ECKWINKEL"), ("S3", "S3 GIEBEL")]
    text(sh, "LEBENSBALKEN MIT 2 FACH VERGRÖSSERUNG: MITTEL %d, ERFAHRUNG UND FÄHIGKEITEN DARUNTER" % HP_MID, 10, 150, GOLD, 3)
    for i, (o, lab) in enumerate(names):
        x = 10 + (i % 2) * 880
        y = 175 + (i // 2) * 450
        c = zoom(draw_hud(HP_MID, 0.8, o)[0:200, 0:420], 2)
        text(sh, lab, x, y + 6, GOLD if o == RECO else BONE, 3)
        paste(sh, c, x, y + 20); box(sh, x, y + 20, 840, 400)
    y = 175 + 2 * 450 + 40
    text(sh, "HÖCHSTSTAND %d, 1:1 (DIE GIEBEL- UND NIETENLAGE WANDERT MIT DEM ENDE)" % HP_MAX, 10, y, GOLD, 3)
    for i, (o, lab) in enumerate(names):
        c = draw_hud(HP_MAX, 1.0, o)[0:110, 0:600]
        x = 10 + i * 700
        text(sh, lab, x, y + 36, BONE, 2)
        paste(sh, c, x, y + 46); box(sh, x, y + 46, 600, 110)
    y += 230
    text(sh, "REITER DER KARTE (AKTIV) UND DER AUSRÜSTUNG, MIT 2 FACH VERGRÖSSERUNG", 10, y, GOLD, 3)
    for i, (o, lab) in enumerate(names):
        x = 10 + (i % 2) * 1400
        yy = y + 20 + (i // 2) * 480
        c = zoom(draw_menu(o)[0:210, 60:720], 2)
        text(sh, lab, x, yy + 20, GOLD if o == RECO else BONE, 3)
        paste(sh, c, x, yy + 34); box(sh, x, yy + 34, 1320, 420)
    y = y + 20 + 2 * 480 + 40
    lines(sh, ["S1 Nieten: runde Niete an jeder Ecke, am Lebensbalken zusätzlich an den langen Strichen (jedes fünfte Upgrade) auf dem unteren Rand, an Reitern alle 80 px.",
               "S2 Eckwinkel: vier Winkel (26 px Armlänge) mit Licht links oben und Schatten rechts unten, Niete im Knick; Kanten bleiben schlicht.",
               "S3 Giebel: Giebel aus drei Stufen mit Spitzbogenfenster; am Lebensbalken sitzt er auf dem Ende und wandert mit, an Reitern zwei kleine (Mitte frei für die Marke).",
               "Art Bible: Metallrelief, Licht von links oben, acht Silberstufen (davon sechs im Rahmen), keine Glättung, Gold nur für Erfahrung und Marken, kein neuer Farbton."],
          10, y, 2, BONE2, 24)
    return sh[:y + 120]


RECO = "S1"


def main():
    a, full = sheet_leben()
    write_png(os.path.join(OUT, "blatt-lebensbalken-runde2.png"), a)
    write_png(os.path.join(OUT, "blatt-schmuck-runde2.png"), sheet_schmuck())
    write_png(os.path.join(OUT, "runde2-hoechst-1920.png"), to1920(full))
    write_png(os.path.join(OUT, "runde2-hoechst-854.png"), full[0::2, 0::2])
    print("HP_MAX", HP_MAX, "CAP", CAP, "HP_MID", HP_MID, "PITCH", PITCH)


main()
