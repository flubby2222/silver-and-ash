"""Blatt der drei Pixelschriften (Issue #355). Aufruf (Blender nur als Python mit numpy und PNG-Ausgabe):

  "<Blender>\\blender.exe" --background --python schrift_blatt.py

Voraussetzung: der Ordner ../hud-vorlagen (HUD Runde 6, Variante V3 "Reich") liegt daneben.
Dessen Skripte zeichnen den HUD-Ausschnitt; hier wird nur die Schrift ausgetauscht: Layer.text und textw
zeichnen statt Cinzel das Pixelraster aus schrift.py. Kein Aufbau, keine Farbe, keine Lage wird geaendert.
Die Beschriftung des Blatts steht in der jeweiligen Pixelschrift selbst (kein Systemschrift-Text).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
HUD = os.path.join(HERE, "..", "hud-vorlagen")
sys.path.insert(0, HERE)
sys.path.insert(0, HUD)

import numpy as np  # noqa: E402
import schrift  # noqa: E402
import hud_blaetter as hb  # noqa: E402
import hud_runde2 as r2  # noqa: E402
import hud_runde3 as r3  # noqa: E402
import hud_runde4 as r4  # noqa: E402

BONE, BONE2, ROYAL = hb.BONE, hb.BONE2, hb.ROYAL
IRON1, IRON3, IRON4, IRON6, IRON7, FOG = hb.IRON1, hb.IRON3, hb.IRON4, hb.IRON6, hb.IRON7, hb.FOG
PAPER = (20, 16, 26)
PANEL = (26, 21, 34)


# ------------------------------------------------------------------ Zeichnen auf einer Zahlenflaeche
def put(cv, font, s, x, base, color, scale=1, anchor="l", shadow=None):
    """Text auf Flaeche cv (H,W,3 in 0..1). base = Zeile der Grundlinie. Gibt die Breite in Pixeln zurueck."""
    w = font.width(s) * scale
    if anchor == "c":
        x = x - w // 2
    elif anchor == "r":
        x = x - w
    pts = font.layout(s)
    H, W, _ = cv.shape

    def stamp(ox, oy, col):
        c = np.array(col, np.float32) / 255.0
        for px, py in pts:
            X = int(ox + px * scale)
            Y = int(oy + py * scale)
            if X < 0 or Y < 0 or X + scale > W or Y + scale > H:
                continue
            cv[Y:Y + scale, X:X + scale] = c

    if shadow:
        stamp(x + scale, base + scale, shadow)
    stamp(x, base, color)
    return w


def rect(cv, x0, y0, x1, y1, col, fill=None):
    c = np.array(col, np.float32) / 255.0
    if fill is not None:
        cv[y0:y1, x0:x1] = np.array(fill, np.float32) / 255.0
    cv[y0:y0 + 1, x0:x1] = c
    cv[y1 - 1:y1, x0:x1] = c
    cv[y0:y1, x0:x0 + 1] = c
    cv[y0:y1, x1 - 1:x1] = c


def flat(w, h, col):
    return np.tile(np.array(col, np.float32) / 255.0, (h, w, 1))


# ------------------------------------------------------------------ HUD mit ausgetauschter Schrift
def install(font):
    """Layer.text und textw der HUD-Skripte auf die Pixelschrift umbiegen."""
    def text(self, fpath, size, s, x, y, color, anchor="l", shadow=None, alpha=1.0):
        w = font.width(s)
        # Lage: Mitte der Versalhoehe von Cinzel (0,35 x Groesse ueber der Grundlinie) bleibt die Mitte
        base = int(round(y - 0.35 * size + font.cap / 2.0 - 1))
        left = x - (w / 2.0 if anchor == "c" else w if anchor == "r" else 0)
        left = int(round(left))
        pts = font.layout(s)
        one = np.ones((1, 1), np.float32)
        passes = []
        if shadow and len(shadow) == 3:
            passes.append((1, 1, shadow[2], 0.9))  # shadow = (dx, dy, Farbe); ein Pixel nach unten rechts
        passes.append((0, 0, color, 1.0))
        for dx, dy, col, aa in passes:
            c = np.array(col, np.float32) / 255.0
            for px, py in pts:
                i = left + px + dx
                j = base + py + dy
                if 0 <= i < self.W and 0 <= j < self.H:
                    self._blend(j, j + 1, i, i + 1, one, np.broadcast_to(c, (1, 1, 3)), alpha * aa)
        return float(w)

    def textw(layer, fpath, size, s):
        return font.width(s) / layer.sx

    hb.Layer.text = text
    for m in (hb, r2, r3, r4):
        m.textw = textw


def hud_frame(font, scene):
    install(font)
    r4.NEU, r4.NUMS, r4.ORN = True, False, 3
    r4.GW = 10.5
    return r3.compose(scene, r4.design(3))


# ------------------------------------------------------------------ Menues
def menu(font, kind):
    cv = flat(427, 240, FOG)
    rect(cv, 6, 6, 421, 234, IRON1)
    if kind == "title":
        head, items = "Silver & Ash", ["Start", "Settings", "Quit"]
        y0, step = 128, 36
    else:
        head, items = "Paused", ["Resume", "Settings", "Back to title", "Quit"]
        y0, step = 112, 32
    put(cv, font, head, 213, 62, BONE, 3, "c", shadow=(0, 0, 0))
    cv[78:79, 130:297] = np.array(IRON3, np.float32) / 255.0
    for i, it in enumerate(items):
        base = y0 + i * step
        w = font.width(it) * 2
        x = 213
        if i == 0:
            rect(cv, x - w // 2 - 10, base - font.cap * 2 - 7, x + w // 2 + 10, base + 8, IRON4, fill=IRON1)
            put(cv, font, it, x, base, ROYAL, 2, "c", shadow=(0, 0, 0))
        else:
            put(cv, font, it, x, base, BONE2, 2, "c", shadow=(0, 0, 0))
    put(cv, font, "[Up/Down] Select   [Enter] OK", 213, 224, IRON6, 1, "c")
    return cv


def charset(font):
    cv = flat(854, 232, PANEL)
    rect(cv, 0, 0, 854, 232, IRON1)
    lines = [
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
        "abcdefghijklmnopqrstuvwxyz",
        "0123456789  .,:;!?'\"-+=/\\",
        "()[]<>%&*#_|@~^",
        "The quick brown fox jumps over the lazy dog",
        "[F] Buy  [E] Open  x2  7/10  +25%  128",
    ]
    for i, s in enumerate(lines):
        put(cv, font, s, 14, 30 + i * 36, BONE if i < 5 else ROYAL, 2)
    return cv


def crop(img, x0, y0, x1, y1, k):
    c = img[y0:y1, x0:x1]
    return np.repeat(np.repeat(c, k, 0), k, 1)


def paste(cv, img, x, y):
    h, w = img.shape[:2]
    cv[y:y + h, x:x + w] = img


CROPS = [
    ("Toast", (4, 418, 176, 444)),
    ("Counters", (722, 4, 854, 40)),
    ("Hint", (382, 248, 478, 276)),
    ("Boss", (340, 2, 516, 32)),
    ("Level", (0, 0, 62, 34)),
    ("Ammo", (686, 446, 854, 480)),
]

W_BLOCK = 12 + 854 + 12 + 854 + 12
ROW_CROPS = [[0, 1, 2], [3, 4, 5]]


def block(font, title, sub, scene):
    frame = hud_frame(font, scene)
    rows_h = []
    for row in ROW_CROPS:
        rows_h.append(14 + max((CROPS[i][1][3] - CROPS[i][1][1]) * 4 for i in row))
    H = 12 + 40 + 480 + 12 + sum(rows_h) + 12 * len(rows_h) + 4
    cv = flat(W_BLOCK, H, PAPER)
    put(cv, font, title, 12, 12 + 2 + 15, ROYAL, 3 if font.cap < 8 else 2)
    put(cv, font, sub, 12 + 854 + 12, 12 + 2 + 15, BONE2, 1)
    top = 12 + 40
    paste(cv, frame, 12, top)
    xr = 12 + 854 + 12
    paste(cv, menu(font, "title"), xr, top)
    paste(cv, menu(font, "pause"), xr + 427, top)
    paste(cv, charset(font), xr, top + 240 + 8)
    y = top + 480 + 12
    for row, rh in zip(ROW_CROPS, rows_h):
        x = 12
        for i in row:
            name, (x0, y0, x1, y1) = CROPS[i]
            put(cv, font, name + " (4x)", x, y + 9, IRON6, 1)
            im = crop(frame, x0, y0, x1, y1, 4)
            paste(cv, im, x, y + 14)
            x += im.shape[1] + 16
        y += rh + 12
    return cv, frame


def main():
    scene = hb.load_png(os.path.join(HUD, "basis-854x480.png"))
    fonts = schrift.all_fonts()
    meta = [
        ("A  Kapitell", "Small caps with small serifs. Lowercase = small capitals.", "a-kapitell"),
        ("B  Textura", "Broken: thick stems, thin crossbars, pointed heads and feet.", "b-textura"),
        ("C  Schmal", "Narrow and tall: 1 px strokes, 8 px caps, 6 px x-height.", "c-schmal"),
    ]
    blocks = []
    for f, (t, sub, key) in zip(fonts, meta):
        cv, frame = block(f, t, sub, scene)
        hb.save_png(os.path.join(HERE, "schrift-%s.png" % key), cv)
        hb.save_png(os.path.join(HERE, "hud-%s-854.png" % key), frame)
        blocks.append(cv)
        print("ok", f.name, flush=True)
    H = sum(b.shape[0] for b in blocks) + 6 * (len(blocks) - 1)
    sheet = flat(W_BLOCK, H, PAPER)
    y = 0
    for b in blocks:
        paste(sheet, b, 0, y)
        y += b.shape[0] + 6
    hb.save_png(os.path.join(HERE, "blatt-schrift.png"), sheet)
    print("Blatt", sheet.shape, flush=True)


main()
