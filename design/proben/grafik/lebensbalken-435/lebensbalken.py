"""Varianten-Blatt zum Lebensbalken (Issue #435, Teil 1: nur Probe, kein Einbau).

Aufruf (Blender nur als Python mit numpy):
  "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/lebensbalken-435/lebensbalken.py [-- <Ausgabeordner>]

Liest nur Fertiges: assets/hud/hud_parts.png + .json (echter HUD-Atlas), assets/balance.json (Start-Leben, Segen),
basis-854.png (Foto hud-calm ohne HUD, `--photo hud-calm`), die Pixelschrift A "Kapitell" aus ../schrift/schrift.py.
Rahmen und Balken werden aus Atlas-Teilen gestreckt (linke und rechte Kappe, Mittelstueck gekachelt), keine neuen Farben.
Schreibt blatt-lebensbalken.png und Einzelbilder (je Variante der Hoechststand in 854 x 480).
Die Lage der Teile ist von Hand gerechnet (Spacing 6), nicht aus HudLayout (Core) - das ist der Einbau (Teil 2).
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
ATLAS = read_png(os.path.join(REPO, "assets", "hud", "hud_parts.png"))
INFO = json.load(open(os.path.join(REPO, "assets", "hud", "hud_parts.json"), encoding="utf-8"))
T = {p["name"]: p for p in INFO["parts"]}
BASE = read_png(os.path.join(HERE, "basis-854.png"))[..., :3].astype(np.float32)
assert BASE.shape == (480, 854, 3), BASE.shape
FONT = schrift.font_a()

bal = open(os.path.join(REPO, "assets", "balance.json"), encoding="utf-8").read()
bal = json.loads(re.sub(r"//[^\n]*", "", bal))
HP0 = bal["player"]["maxHealth"]          # Start-Leben
STEP = bal["boons"]["health"]             # Lebenspunkte je Upgrade
PXHP = 1                                  # Pixel je Lebenspunkt (alle Varianten)
UP_MID = 10
HP_MID = HP0 + UP_MID * STEP
BOSS_X = 296                              # linke Kante des Boss-Rahmens (HUD-Atlas layoutX)
RIGHT_LIMIT = BOSS_X - 6                  # Rahmen enden hoechstens hier (Abstand 6 zum Boss-Rahmen)
CAP_A = (RIGHT_LIMIT - 36 - 10) // PXHP   # Rahmen 36..: Balken + 2 x 5 px Rand
CAP_C = (RIGHT_LIMIT - 36 - 16) // PXHP   # gemeinsame Fassung: 2 x 8 px Rand
HP_MAX = (min(CAP_A, CAP_C) // STEP) * STEP   # ganze Upgrades, fuer alle Varianten gleich: 235
DARK = np.array([20, 14, 24], np.float32)
BONE, BONE2, GOLD, PAPER, PANEL = (216, 210, 192), (195, 192, 164), (214, 170, 64), (20, 16, 26), (30, 25, 40)
SHADOW = (16, 11, 20)


def part(name):
    p = T[name]
    return ATLAS[p["y"]:p["y"] + p["h"], p["x"]:p["x"] + p["w"]].copy()


def blit(cv, im, x, y, clip_x=None):
    h, w = im.shape[:2]
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(cv.shape[1], x + w), min(cv.shape[0], y + h)
    if x1 <= x0 or y1 <= y0:
        return
    s = im[y0 - y:y1 - y, x0 - x:x1 - x].astype(np.float32)
    a = s[..., 3:4] / 255
    if clip_x is not None:
        cols = (np.arange(x0, x1) + 0.5) < clip_x
        a = a * cols[None, :, None]
    cv[y0:y1, x0:x1] = cv[y0:y1, x0:x1] * (1 - a) + s[..., :3] * a


def put_part(cv, name, at=None, clip_x=None):
    p = T[name]
    x, y = at if at else (p["layoutX"], p["layoutY"])
    blit(cv, part(name), x, y, clip_x)


# ------------------------------------------------------------------ Strecken
def hstretch(im, new_w, left, right, tile_at, tile_w=8, giebel=None):
    """Mitte aus gekachelter Spalte (tile_at), linke/rechte Kappe, optional Giebel (Spaltenbereich, Zeilen) in die Mitte."""
    h, w = im.shape[:2]
    out = np.zeros((h, new_w, 4), np.uint8)
    out[:, :left] = im[:, :left]
    out[:, new_w - right:] = im[:, w - right:]
    strip = im[:, tile_at:tile_at + tile_w]
    for x in range(left, new_w - right):
        out[:, x] = strip[:, (x - left) % tile_w]
    if giebel:
        c0, c1, rows = giebel
        g = im[:rows, c0:c1]
        gx = (new_w - (c1 - c0)) // 2
        mask = g[..., 3] > 0
        out[:rows, gx:gx + (c1 - c0)][mask] = g[mask]
    return out


def vcut(im, r0, r1):
    return np.concatenate([im[:r0], im[r1:]], 0)


def vadd(im, row, n):
    return np.concatenate([im[:row + 1]] + [im[row:row + 1]] * n + [im[row + 1:]], 0)


def giebel_cols(im, lo=20):
    cols = [c for c in range(lo, im.shape[1] - lo) if (im[:4, c, 3] > 0).any()]
    return min(cols) - 1, max(cols) + 2


XP_FRAME = part("frame_xp")                      # 66 x 21
XP_G = giebel_cols(XP_FRAME, 12)


def life_frame(w_bar, endcap=False):
    """Schmale Fassung fuer den Lebensbalken: frame_xp, 4 Zeilen hoeher, Breite = Balken + 10."""
    fr = vadd(XP_FRAME, 8, 4)                    # 25 hoch, Körper 18
    new_w = w_bar + 10
    g = None if endcap else (XP_G[0], XP_G[1], 4)
    fr = hstretch(fr, new_w, 10, 10, 12, 8, g)
    if endcap:                                   # Endkappe: Giebel sitzt ueber dem rechten Ende und wandert mit
        gp = XP_FRAME[:4, XP_G[0]:XP_G[1]]
        gx = new_w - 10 - (XP_G[1] - XP_G[0]) // 2 + 1
        m = gp[..., 3] > 0
        fr[:4, gx:gx + gp.shape[1]][m] = gp[m]
    return fr


def vitals_frame(w_inner, slim):
    fv = part("frame_vitals")                    # 366 x 38, Körper 30
    g = giebel_cols(fv, 20)
    if slim:
        fv = vcut(fv, 12, 26)                    # 24 hoch: nur Ausdauer
    return hstretch(fv, w_inner + 6, 16, 16, 24, 8, (g[0], g[1], 4))


def bar(name, w):
    im = part(name)
    out = np.zeros((im.shape[0], w, 4), np.uint8)
    out[:, :8] = im[:, :8]
    out[:, w - 8:] = im[:, -8:]
    out[:, 8:w - 8] = im[:, 20:21]
    return out


def draw_life(cv, x, y, hp, frac, style):
    """Lebensbalken (14 hoch) mit Breite hp * PXHP bei (x, y)."""
    w = hp * PXHP
    emp, ful = bar("life_empty", w), bar("life_full", w)
    blit(cv, emp, x, y)
    blit(cv, ful, x, y, clip_x=x + 8 + (w - 16) * frac if frac < 1 else None)
    if style == "B":                             # Teilstrich je Upgrade (alle STEP*PXHP px), jeder fuenfte lang
        pitch = STEP * PXHP
        for i, tx in enumerate(range(x + 8 + pitch, x + w - 8, pitch), 1):
            long = (i % 5 == 0)
            y0 = y + (3 if long else 8)
            cv[y0:y + 11, tx] = DARK + 0
    if style == "C":                             # Glieder: je Upgrade eine Zelle, 1 px Lücke bis auf den Grund
        pitch = STEP * PXHP
        for tx in range(x + 8 + pitch - 1, x + w - 8, pitch):
            cv[y + 2:y + 12, tx] = DARK


# ------------------------------------------------------------------ Schrift
def text_px(s, x, base, scale=1, anchor="l"):
    """Pixel (x, y) einer Zeile in Kapitell; Umlaute: Grundbuchstabe plus zwei Punkte."""
    s = s.replace("ß", "ss")
    w = FONT.width(s.replace("ä", "a").replace("ö", "o").replace("ü", "u").replace("Ä", "A").replace("Ö", "O").replace("Ü", "U"), 1) * scale
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


# ------------------------------------------------------------------ HUD zusammensetzen
def common(cv, boss=False):
    """Alle Teile, die in jeder Variante gleich bleiben (Ausschnitt wie das Spiel: Gegenstaende, Waffen, Silber, Schluessel)."""
    for i in range(2):
        put_part(cv, f"frame_item_{i}")
    put_part(cv, "icon_flask"); put_part(cv, "icon_eye")
    for i, g in enumerate(("blade", "pistol", "rifle", "blunder")):
        act = i == 1
        put_part(cv, f"frame_weapon_{i}" + ("_active" if act else ""))
        put_part(cv, f"icon_{g}" + ("_active" if act else ""))
    put_part(cv, "frame_currency"); put_part(cv, "icon_coin"); put_part(cv, "icon_key")
    text(cv, "85", 745 + 12 + 3, 14 + 6 + 3, BONE)
    text(cv, "1", 812 + 12 + 3, 14 + 6 + 3, BONE)
    if boss:
        put_part(cv, "frame_boss"); put_part(cv, "boss_empty"); put_part(cv, "boss_full", clip_x=314 + 226 * 0.7)
    put_part(cv, "crosshair")


def stamina(cv, x0, y0, full=1.0):
    """Fuenf Segmente wie im Spiel (Lage relativ zur linken Kante des Segmentstreifens)."""
    for i in range(5):
        p = T[f"stamina_{i}_empty"]
        ax = x0 + (p["layoutX"] - 256)
        put_part(cv, f"stamina_{i}_empty", (ax, y0))
        put_part(cv, f"stamina_{i}_full", (ax, y0))


def level_frame(cv, x, y):
    blit(cv, part("frame_level"), x, y)
    text(cv, "0", x + 3 + 13, y + 5 + 14 + 3, BONE, 1, "c")


def skills(cv, x, y):
    for i, g in enumerate(("flame", "shield")):
        fx = x + i * 28
        blit(cv, part("frame_skill_%d" % i), fx, y)
        blit(cv, part("icon_" + g), fx + 10, y + 13)


def heute(cv, hp, frac):
    common(cv)
    level_frame(cv, 5, 0)
    put_part(cv, "frame_xp"); put_part(cv, "xp_empty")
    skills(cv, 103, 0)
    put_part(cv, "frame_vitals")
    blit(cv, part("life_empty"), 255, 451)
    blit(cv, part("life_full"), 255, 451, clip_x=255 + 344 * frac)
    stamina(cv, 256, 466)


def neu(cv, style, hp, frac, boss=False):
    common(cv, boss)
    w = hp * PXHP
    if style in ("A", "B"):
        fr = life_frame(w, endcap=(style == "B"))
        blit(cv, fr, 36, 1)                       # Körper y 6..24
        draw_life(cv, 41, 8, hp, frac, style)     # Balken 14 hoch, 2 px Luft im Körper
        put_part(cv, "frame_xp", (36, 25)); put_part(cv, "xp_empty", (41, 32))   # Körper y 30..44
        level_frame(cv, 5, 6)                     # Körper auf der Mitte des Stapels
        skills(cv, 5, 47)
    else:                                         # C: eine gemeinsame Fassung, Leben oben, Erfahrung darunter
        fr = vitals_frame(w + 10, slim=False)
        blit(cv, fr, 36, 1)
        draw_life(cv, 36 + 8, 1 + 9, hp, frac, "C")
        put_part(cv, "xp_empty", (36 + 8, 1 + 24))
        level_frame(cv, 5, 2)
        skills(cv, 5, 41)
    # Ausdauer bleibt unten Mitte, die Fassung ist ohne Leben nur noch 24 hoch
    sf = vitals_frame(344 + 10, slim=True)
    blit(cv, sf, 244, 480 - sf.shape[0])
    stamina(cv, 256, 466)


def render(kind, style=None, hp=HP0, frac=1.0, boss=False):
    cv = BASE.copy()
    if kind == "heute":
        heute(cv, hp, frac)
    else:
        neu(cv, style, hp, frac, boss)
    return cv


# ------------------------------------------------------------------ Blatt
def crop(cv, x0, y0, x1, y1, k=2):
    return np.repeat(np.repeat(cv[y0:y1, x0:x1], k, 0), k, 1)


def paste(sheet, im, x, y):
    sheet[y:y + im.shape[0], x:x + im.shape[1]] = im


def frame_box(sheet, x, y, w, h, col=(90, 86, 100)):
    sheet[y - 1:y + h + 1, x - 1] = col; sheet[y - 1:y + h + 1, x + w] = col
    sheet[y - 1, x - 1:x + w + 1] = col; sheet[y + h, x - 1:x + w + 1] = col


ROWS = [
    ("heute", None, "HEUTE", "Aktuelles Foto aus dem Spiel (hud-calm): Leben 344 px unten in der Mitte, kuerzt nie, waechst nie.",
     ["Leben: 100 Punkte = 344 px, fest, unten in der Mitte (Fassung mit Ausdauer).",
      "Erfahrung: 56 px, oben links neben der Stufe. Faehigkeiten rechts daneben.",
      "Keine Verschiebung noetig - aber das Leben waechst nicht mit Upgrades."]),
    ("neu", "A", "A  SCHLICHT", "Eigene schmale Fassung ueber der Erfahrung, waechst nach rechts.",
     ["Pixel je Lebenspunkt: 1 (je Upgrade +5 px). Start 100 px, Rahmen 110 px.",
      "Obergrenze bei 854 x 480: %d Lebenspunkte (Rahmen endet bei x %d, 6 px vor dem Boss-Rahmen)." % (CAP_A, 36 + CAP_A + 10),
      "Ausdauer bleibt unten in der Mitte, die Fassung wird 24 px statt 38 px hoch.",
      "Faehigkeiten ruecken unter Stufe und Erfahrung (zweite Zeile), Hinweis darunter."]),
    ("neu", "B", "B  MIT ENDKAPPE UND TEILSTRICHEN", "Wie A, Giebel wandert als Endkappe mit; je Upgrade ein Teilstrich, jeder fuenfte lang.",
     ["Pixel je Lebenspunkt: 1; ein Teilstrich alle 5 px = ein Upgrade, alle 25 px ein langer.",
      "Obergrenze wie A: %d Lebenspunkte (%d Upgrades). Die Teilstriche zeigen die Zahl der Upgrades ablesbar." % (CAP_A, (CAP_A - HP0) // STEP),
      "Ausdauer und uebrige Teile wie A (Ausdauer unten in der Mitte, Faehigkeiten zweite Zeile).",
      "Kein Giebel mehr in der Balkenmitte: der Giebel sitzt nur noch am Ende."]),
    ("neu", "C", "C  GLIEDER IN EINER FASSUNG", "Leben und Erfahrung teilen eine Fassung (wie frueher Leben und Ausdauer); das Leben ist aus Gliedern wie die Ausdauer.",
     ["Pixel je Lebenspunkt: 1; ein Glied = ein Upgrade (5 px, 1 px Luecke), wie die fuenf Ausdauer-Segmente.",
      "Obergrenze bei 854 x 480: %d Lebenspunkte (Fassung endet bei x %d)." % (CAP_C, 36 + CAP_C + 16),
      "Erfahrung bleibt 56 px; rechts davon bleibt dunkler Grund der Fassung leer.",
      "Ausdauer unten in der Mitte wie A; Faehigkeiten zweite Zeile unter der Stufe."]),
]
RECO = "B"


def main():
    W = 2200
    rowh = 640
    sheet = np.tile(np.array(PAPER, np.float32), (60 + rowh * len(ROWS) + 20, W, 1))
    text(sheet, "LEBENSBALKEN UEBER DEM XP-BALKEN - VARIANTEN (ISSUE #435, PROBE OHNE EINBAU)".replace("UEBER", "ÜBER"), 20, 34, BONE, 2)
    text(sheet, "Echter HUD-Atlas, Foto hud-calm 854 x 480, Pixel hart. Start %d Leben, +%d je Upgrade (assets/balance.json); gezeigt: Start %d, Mittel %d (+%d Upgrades), Höchst %d (Obergrenze)." % (HP0, STEP, HP0, HP_MID, UP_MID, HP_MAX), 20, 52, BONE2, 1)
    for ri, (kind, style, title, sub, notes) in enumerate(ROWS):
        y0 = 60 + ri * rowh
        reco = (style == RECO)
        if reco:
            sheet[y0 - 4:y0 + rowh - 14, 8:W - 8] = np.array((40, 34, 24), np.float32)
        text(sheet, title, 20, y0 + 18, GOLD if reco else BONE, 2)
        if reco:
            text(sheet, "EMPFEHLUNG", W - 30, y0 + 18, GOLD, 2, "r")
        text(sheet, sub, 20, y0 + 36, BONE2, 1)
        # linke Seite: ganzer Bildschirm im Höchststand
        full = render(kind, style, HP_MAX if kind == "neu" else HP0, 0.7 if kind == "heute" else 1.0, boss=True)
        paste(sheet, full, 20, y0 + 50)
        frame_box(sheet, 20, y0 + 50, 854, 480)
        text(sheet, ("Höchststand %d Leben mit Boss-Leiste (Abstand zur Obergrenze prüfen), 1x" % HP_MAX) if kind == "neu" else "So sieht es heute aus (Leben 70 %), 1x", 20, y0 + 50 + 480 + 18, BONE2, 1)
        # rechts: 2x2 Ausschnitte oben links
        gx, gy = 900, y0 + 50
        states = ([("Start (voll)", HP0, 1.0), ("Mittel +10 Upgrades (voll)", HP_MID, 1.0), ("Höchststand (voll)", HP_MAX, 1.0), ("Mittel, halb leer", HP_MID, 0.5)]
                  if kind == "neu" else [("Start (voll), Leben 100", HP0, 1.0), ("Halb leer", HP0, 0.5)])
        for i, (lab, hp, fr) in enumerate(states):
            cvv = render(kind, style, hp, fr)
            cx, cy = gx + (i % 2) * 640, gy + (i // 2) * 228
            text(sheet, lab, cx, cy + 10, BONE, 1)
            paste(sheet, crop(cvv, 0, 0, 316, 92, 2)[:184, :632], cx, cy + 16)
            frame_box(sheet, cx, cy + 16, 632, 184)
        # Ausdauer unten Mitte
        cvv = render(kind, style, HP_MID, 0.6)
        by = gy + (2 if kind == "neu" else 1) * 228 + 6
        text(sheet, "Unten in der Mitte (Ausdauer):", gx, by + 10, BONE, 1)
        paste(sheet, crop(cvv, 236, 440, 620, 480, 2), gx, by + 16)
        frame_box(sheet, gx, by + 16, 768, 80)
        for k, line in enumerate(notes):
            text(sheet, line, gx + 790 if False else 20, y0 + 50 + 480 + 40 + k * 14, BONE if k == 0 else BONE2, 1)
        # Kurzwerte rechts der Ausdauerzeile
    write_png(os.path.join(OUT, "blatt-lebensbalken.png"), sheet)
    for style in "ABC":
        write_png(os.path.join(OUT, "variante-%s-hoechst-854.png" % style.lower()), render("neu", style, HP_MAX, 1.0, True))
    write_png(os.path.join(OUT, "heute-854.png"), render("heute", None, HP0, 0.7))
    print("HP_MAX", HP_MAX, "CAP_A", CAP_A, "CAP_C", CAP_C, "HP_MID", HP_MID)


main()
