"""Menue-Design (Issue #351): drei Varianten im Stil des abgenommenen HUD (V3 "Reich", Runde 6), Schrift A "Kapitell".

Aufruf (Blender dient nur als Python mit numpy und PNG-Ausgabe, es wird nichts gerendert):

  "<Blender>\\blender.exe" --background --python menue.py

Importiert (nichts kopiert): die Formen und Silberfassungen aus ../hud-vorlagen (hud_runde4: cell, sframe, trough, ornament,
lit_ring ...) und die Pixelschrift aus ../schrift (schrift.py). Bild-Ein/Ausgabe und 15-Bit-Koernung wie im HUD (hud_runde3.compose).

Gezeigt wird je Variante dasselbe: Titelmenue, Pausenmenue, Einstellungen, Beenden-Frage, Reiterleiste des Tab-Menues.
Es aendert sich nur die Gestaltung von Tafel, Markierung, Regler und Reitern; Texte, Lage und Hintergrund sind gleich.

  A Schlicht  nur Rahmenlinie und Markierung (keine Tafel-Fuellung)
  B Gefasst   Tafel mit Silberfassung wie die HUD-Felder, Markierung als erhabene Silberplatte
  C Reich     wie B plus Giebel, Eckrauten, Doppelrahmen, Fialen; spitze Markierung, Regler mit Raute, Giebel-Reiter
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "schrift"))
sys.path.insert(0, os.path.join(HERE, "..", "hud-vorlagen"))

import numpy as np  # noqa: E402
import schrift  # noqa: E402
import hud_blaetter as hb  # noqa: E402
import hud_runde3 as r3  # noqa: E402
import hud_runde4 as r4  # noqa: E402

FONT = schrift.font_a()
r4.NEU = True  # neutrales Silber, harte Kanten (Endfassung des HUD)

BONE, BONE2, ROYAL = hb.BONE, hb.BONE2, hb.ROYAL
IRON0, IRON1, IRON2, IRON5, IRON6, IRON7 = hb.IRON0, hb.IRON1, hb.IRON2, hb.IRON5, hb.IRON6, hb.IRON7
BRASS_M, BRASS_L, GOLD_W, FOG = hb.BRASS_M, hb.BRASS_L, hb.GOLD_W, hb.FOG
PAPER = (20, 16, 26)
NL = [tuple(float(v) * 255 for v in c) for c in r4.N_LEVELS]  # neutrale Silberstufen dunkel -> hell (0..255)

W, H = 854, 480
CX = 427
VARIANTS = ["A", "B", "C"]
NAMES = {"A": "A  Schlicht", "B": "B  Gefasst", "C": "C  Reich"}
SUBS = {
    "A": "Only a frame line and a marker. No panel fill. Cursor: gold text between two silver diamonds.",
    "B": "Panel in a silver setting like the HUD cells. Cursor: raised silver plate. Slider: HUD bar.",
    "C": "As B plus gables, corner studs, double frame, finials. Pointed cursor, diamond slider, gabled tabs.",
}


# ------------------------------------------------------------------ Schrift auf einer Ebene
def ptext(L, s, x, base, color, scale=1, anchor="l", shadow=True):
    """Text der Pixelschrift auf der Ebene L. base = Zeile der Grundlinie."""
    w = FONT.width(s) * scale
    left = int(x - w // 2) if anchor == "c" else int(x - w) if anchor == "r" else int(x)
    pts = FONT.layout(s)
    xs = [left + px * scale for px, _ in pts]
    ys = [base + py * scale for _, py in pts]
    i0, j0 = min(xs), min(ys)
    i1, j1 = max(xs) + scale + scale, max(ys) + scale + scale
    cov = np.zeros((j1 - j0, i1 - i0), np.float32)
    sh = np.zeros_like(cov)
    for X, Y in zip(xs, ys):
        cov[Y - j0:Y - j0 + scale, X - i0:X - i0 + scale] = 1
        sh[Y - j0 + scale:Y - j0 + 2 * scale, X - i0 + scale:X - i0 + 2 * scale] = 1
    for m, c, a in ((sh, FOG, 0.9), (cov, color, 1.0)):
        if m is sh and not shadow:
            continue
        col = np.broadcast_to(np.array(c, np.float32) / 255.0, m.shape + (3,))
        L._blend(j0, j1, i0, i1, m, col, a)
    return w


def base_for(cy, scale):
    """Grundlinie, damit die Versalhoehe (7 px) in der Zeilenmitte cy liegt."""
    return int(round(cy + 3.5 * scale))


def fill(L, poly, y0, y1, c0, c1, alpha=0.97):
    f = r4.sd_poly(poly)
    L.shape(r4.bbox(poly), f, r4.vg(y0, y1, c0, c1), alpha=alpha)


def line(L, x0, y0, x1, y1, col, alpha=1.0):
    L.shape((x0 - 1, y0 - 1, x1 + 1, y1 + 1), r4.sd_box(x0, y0, x1, y1), col, alpha=alpha)


# ------------------------------------------------------------------ Bausteine je Variante
def tafel(L, v, x0, y0, x1, y1, ornate=None, seed=10):
    """Tafel um eine Gruppe von Eintraegen."""
    if v == "A":
        r4.sframe(L, r4.chamfer(x0, y0, x1, y1, 3), y0, y1, 1.6, seed)
        return
    old = r4.ORN
    r4.ORN = (3 if v == "C" else 0) if ornate is None else ornate
    r4.cell(L, x0, y0, x1, y1, "socket", False, seed=seed)
    r4.ORN = old


def mark(L, v, x0, y0, x1, y1, small=False, seed=20):
    """Markierter Eintrag (Cursor) oder gewaehlter Wert."""
    cy = (y0 + y1) / 2
    if v == "A":
        line(L, x0, y1 - 1, x1, y1, NL[3])
        if not small:
            r4.diamond4(L, x0 - 9, cy, 4.2)
            r4.diamond4(L, x1 + 9, cy, 4.2)
    elif v == "B":
        poly = r4.chamfer(x0, y0, x1, y1, 2.5)
        fill(L, poly, y0, y1, IRON2, IRON1)
        r4.sframe(L, poly, y0, y1, 2.4, seed)
    else:
        h = y1 - y0
        poly = r4.pointed(x0, y0, x1, y1, h / 2.2)
        inner = r4.pointed(x0 + 2.6, y0 + 2.6, x1 - 2.6, y1 - 2.6, (h - 5.2) / 2.2)
        fill(L, inner, y0, y1, IRON2, IRON0)
        r4.sframe(L, poly, y0, y1, 2.6, seed)
        if not small:
            r4.diamond4(L, x0 - 8, cy, 3.6)
            r4.diamond4(L, x1 + 8, cy, 3.6)


def slider(L, v, x0, x1, cy, frac, active=False, seed=30):
    c0, c1 = BRASS_L, BRASS_M
    if v == "A":
        y0, y1 = cy - 3, cy + 3
        b = r4.sd_box(x0, y0, x1, y1)
        bb = (x0 - 3, y0 - 3, x1 + 3, y1 + 3)
        L.shape(bb, b, IRON0, alpha=0.9)
        fx = x0 + (x1 - x0) * frac
        L.shape(bb, b, r4.vg(y0, y1, c0, c1), clipf=r4.clipx(fx))
        L.shape(bb, lambda px, py: b(px, py) + 0.6, NL[4] if active else NL[2], stroke=1.0)
        line(L, fx - 1, cy - 8, fx + 1, cy + 8, NL[5] if active else NL[3])
    else:
        y0, y1 = cy - 6, cy + 6
        r4.trough(L, x0, y0, x1, y1, frac, c0, c1, "recess", back=IRON0, hi=GOLD_W, p=5 if v == "B" else 6)
        if v == "C":
            fx = x0 + (x1 - x0) * frac
            for i in range(11):
                tx_ = round(x0 + (x1 - x0) * i / 10)
                line(L, tx_, cy + 9, tx_ + 1, cy + 12 if i % 5 else cy + 14, NL[2])
            r4.diamond4(L, round(fx) + 0.5, cy, 7.0)


def tab(L, v, x0, y0, x1, y1, active, seed=40):
    if v == "A":
        if active:
            line(L, x0, y1 - 3, x1, y1, BRASS_M)
        return
    if v == "B":
        old = r4.ORN
        r4.ORN = 0
        r4.cell(L, x0, y0, x1, y1, "socket" if active else "recess", active, seed=seed)
        r4.ORN = old
        return
    top = y0 if active else y0 + 7
    poly = r4.gable(x0, top, x1, y1, 0.42, 3)
    q = r4.gable(x0 + 3, top + 3, x1 - 3, y1, 0.42, 1)
    fill(L, q, top, y1, IRON2 if active else IRON1, IRON0)
    if active:
        r4.sframe(L, poly, top, y1, 3.0, seed)
        r4.diamond4(L, (x0 + x1) / 2, top - 4, 3.0)
    else:
        r4.sframe(L, poly, top, y1, 2.0, seed + 1)


# ------------------------------------------------------------------ Bildschirme
def heading(L, s, base, scale):
    ptext(L, s, CX, base, BONE, scale, "c")


def entries(L, v, items, centres, sel, wide=248):
    for i, (it, cy) in enumerate(zip(items, centres)):
        if i == sel:
            mark(L, v, CX - wide / 2, cy - 16, CX + wide / 2, cy + 16, seed=20 + i)
        ptext(L, it, CX, base_for(cy, 2), ROYAL if i == sel else BONE2, 2, "c")


def screen_title(L, v):
    heading(L, "Silver & Ash", 132, 4)
    cs = [240, 294, 348]
    tafel(L, v, CX - 150, 200, CX + 150, 388)
    entries(L, v, ["Start", "Settings", "Quit"], cs, 0)


def screen_pause(L, v):
    heading(L, "Paused", 104, 3)
    cs = [196, 250, 304, 358]
    tafel(L, v, CX - 150, 160, CX + 150, 394)
    entries(L, v, ["Resume", "Settings", "Back to title", "Quit"], cs, 0)


def screen_settings(L, v):
    heading(L, "Settings", 66, 3)
    tafel(L, v, 150, 90, 704, 392)
    rows = [("Mouse sensitivity", 0.5), ("Master volume", 0.8), ("Music volume", 0.6), ("Effects volume", 0.9)]
    cs = [136, 186, 236, 286, 346]
    for i, (lab, fr) in enumerate(rows):
        cy = cs[i]
        if i == 0:
            mark(L, v, 172, cy - 18, 682, cy + 18, seed=21)
        ptext(L, lab, 192, base_for(cy, 2), ROYAL if i == 0 else BONE2, 2)
        slider(L, v, 400, 600, cy, fr, active=(i == 0), seed=31 + i)
        ptext(L, "%d%%" % round(fr * 100), 664, base_for(cy, 2), ROYAL if i == 0 else BONE2, 2, "r")
    cy = cs[4]
    ptext(L, "Display", 192, base_for(cy, 2), BONE2, 2)
    ww = FONT.width("Window") * 2
    wf = FONT.width("Full screen") * 2
    xr = 664
    xf0 = xr - wf
    xw1 = xf0 - 30
    xw0 = xw1 - ww
    mark(L, v, xw0 - 12, cy - 14, xw1 + 12, cy + 14, small=True, seed=36)
    ptext(L, "Window", xw0, base_for(cy, 2), ROYAL, 2)
    ptext(L, "Full screen", xf0, base_for(cy, 2), BONE2, 2)
    ptext(L, "[Esc] Back", CX, 424, IRON6, 1, "c")


def screen_quit(L, v):
    tafel(L, v, CX - 130, 170, CX + 130, 316)
    heading(L, "Quit?", 222, 3)
    for cx, lab, on in ((CX - 66, "Yes", False), (CX + 66, "No", True)):
        if on:
            mark(L, v, cx - 44, 258, cx + 44, 290, small=True, seed=24)
        ptext(L, lab, cx, base_for(274, 2), ROYAL if on else BONE2, 2, "c")


TAB_BOXES = [(40, 16, 150, 52), (158, 16, 328, 52)]


def screen_tabs(L, v):
    if v == "A":
        r4.sframe(L, r4.chamfer(40, 56, 814, 452, 3), 56, 452, 1.6, 11)
    else:
        old = r4.ORN
        r4.ORN = 1 if v == "C" else 0  # kein Giebel an der Seite: der gehoert den Reitern
        r4.cell(L, 40, 56, 814, 452, "socket", False, seed=11)
        r4.ORN = old
    for i, (lab, box) in enumerate(zip(["Map", "Equipment"], TAB_BOXES)):
        x0, y0, x1, y1 = box
        tab(L, v, x0, y0, x1, y1, i == 0, seed=41 + i)
        ptext(L, lab, (x0 + x1) / 2, base_for((y0 + y1) / 2 + (0 if v != "C" or i == 0 else 3), 2), ROYAL if i == 0 else IRON6, 2, "c")


SCREENS = [("Title", screen_title, 0.34), ("Pause", screen_pause, 0.34), ("Settings", screen_settings, 0.3),
           ("Quit", screen_quit, 0.34), ("Tabs", screen_tabs, 0.3)]


def dim(scene, k):
    """Hintergrund abgedunkelt und in 15 Bit mit 4x4-Koernung wie das Spielbild."""
    yy, xx = np.mgrid[0:H, 0:W]
    thr = ((hb.BAYER[yy % 4, xx % 4] + 0.5) / 16.0)[..., None]
    return np.floor(np.clip(scene * k, 0, 1) * 31 + thr) / 31


def render(scene, v, name):
    for nm, fn, k in SCREENS:
        if nm == name:
            return r3.compose(dim(scene, k), lambda L, fn=fn: fn(L, v))
    raise KeyError(name)


# ------------------------------------------------------------------ Blatt
def flat(w, h, col):
    return np.tile(np.array(col, np.float32) / 255.0, (h, w, 1))


def paste(cv, img, x, y):
    h, w = img.shape[:2]
    cv[y:y + h, x:x + w] = img


def zoom(img, x0, y0, x1, y1, k=4):
    return np.repeat(np.repeat(img[y0:y1, x0:x1], k, 0), k, 1)


def label(cv, s, x, base, col=IRON6, scale=1):
    L = r4.Layer(cv.shape[1], cv.shape[0], True)
    L.sx = L.sy = L.s = 1.0
    ptext(L, s, x, base, col, scale, shadow=False)
    cv[...] = L.rgb + cv * (1 - L.a[..., None])


# Ausschnitte (Name, Bildschirm, x0, y0, x1, y1): Markierung, Regler, Reiter, je 4-fach
CROPS = [("Cursor, title (4x)", "Title", 296, 218, 452, 262), ("Cursor, settings row (4x)", "Settings", 160, 114, 316, 158),
         ("Slider, active row (4x)", "Settings", 470, 114, 626, 158), ("Tabs (4x)", "Tabs", 30, 4, 186, 56)]


def block(scene, v, shots):
    gap = 12
    colw = W
    zooms = [zoom(shots[s], x0, y0, x1, y1) for (_, s, x0, y0, x1, y1) in CROPS]
    zh = max(z.shape[0] for z in zooms) + 14
    Wb = 3 * colw + 4 * gap
    Hb = gap + 44 + H + gap + H + gap + zh + gap
    cv = flat(Wb, Hb, PAPER)
    label(cv, NAMES[v], gap, 14 + 22, ROYAL, 3)
    label(cv, SUBS[v], gap + 500, 14 + 17, BONE2, 1)
    y1 = gap + 44
    y2 = y1 + H + gap
    y3 = y2 + H + gap
    place = [("Title", 0, y1), ("Pause", 1, y1), ("Settings", 2, y1), ("Quit", 0, y2), ("Tabs", 1, y2)]
    for nm, col, y in place:
        paste(cv, shots[nm], gap + col * (colw + gap), y)
    x = gap
    for (nm, *_), z in zip(CROPS, zooms):
        label(cv, nm, x, y3 + 8, IRON6, 1)
        paste(cv, z, x, y3 + 12)
        x += z.shape[1] + 16
    return cv


def main():
    scene = hb.load_png(os.path.join(HERE, "..", "hud-vorlagen", "basis-854x480.png"))
    blocks = []
    for v in VARIANTS:
        shots = {nm: render(scene, v, nm) for nm, _, _ in SCREENS}
        for nm, im in shots.items():
            hb.save_png(os.path.join(HERE, "einzeln", "%s-%s.png" % (v.lower(), nm.lower())), im)
        b = block(scene, v, shots)
        hb.save_png(os.path.join(HERE, "menue-%s.png" % v.lower()), b)
        blocks.append(b)
        print("ok", v, flush=True)
    Hs = sum(b.shape[0] for b in blocks) + 8 * (len(blocks) - 1)
    sheet = flat(blocks[0].shape[1], Hs, PAPER)
    y = 0
    for b in blocks:
        paste(sheet, b, 0, y)
        y += b.shape[0] + 8
    hb.save_png(os.path.join(HERE, "blatt-menue.png"), sheet)
    print("Blatt", sheet.shape, flush=True)


if __name__ == "__main__":
    main()
