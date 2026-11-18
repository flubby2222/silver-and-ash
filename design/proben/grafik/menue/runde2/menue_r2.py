"""Menue-Design Runde 2 (Issue #351): drei Auslegungen von "C Reich", die "eleganter" verschieden deuten.

Aufruf (Blender nur als Python mit numpy und PNG-Ausgabe):

  "<Blender>\\blender.exe" --background --python menue_r2.py

Importiert aus Runde 1 (../menue.py: Schrift, Text, Hilfsfunktionen, Hintergrund) und aus ../../hud-vorlagen (Silberfassungen).
Gleich in allen drei: Texte, Hintergrund, Schrift A, Farben, Lage der Ueberschriften. Es aendern sich Proportionen und Zierrat.

  C1 Fein      duenne Fassungen, kleine Rauten, mehr Luft um den Text
  C2 Schlank   hoehere, schmalere Tafeln, steile ruhige Giebel, kaum Doppelungen
  C3 Geordnet  Zierrat nur an Giebel (zweistufig) und Ecken gebuendelt, Flaechen dazwischen ruhig
Gegenueber Runde 1: Giebel groesser (bei 1x sichtbar), Reiter hoeher und breiter.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))

import numpy as np  # noqa: E402
import menue as M  # noqa: E402  (Runde 1; laedt auch Schrift und HUD-Skripte)

r4, r3, hb, FONT = M.r4, M.r3, M.hb, M.FONT
ptext, base_for, fill, line = M.ptext, M.base_for, M.fill, M.line
BONE, BONE2, ROYAL, IRON0, IRON1, IRON2, IRON6 = M.BONE, M.BONE2, M.ROYAL, M.IRON0, M.IRON1, M.IRON2, M.IRON6
BRASS_M, BRASS_L, GOLD_W = M.BRASS_M, M.BRASS_L, M.GOLD_W
NL, W, H, CX = M.NL, M.W, M.H, M.CX

# ------------------------------------------------------------------ Parameter je Auslegung
P_FEIN = dict(key="c1", name="C1  Fein", fw=1.8, cr=2.2, gable=[(24, 9)], eye=False, dbl=False, fin=False,
              tw=150, pitch=60, mh=34, inset=28, tip=2.4, mdia=2.6, sh=8, kr=4.5, ends=False,
              tabs=((40, 14, 176, 58), (184, 14, 372, 58)), tabrise=0.45, trad=2.4, tfw=1.8, brackets=False,
              sw=(190, 664), sl=(416, 596), vx=640)
P_SCHLANK = dict(key="c2", name="C2  Schlank", fw=2.6, cr=2.6, gable=[(15, 20)], eye=True, dbl=False, fin=False,
                 tw=120, pitch=62, mh=36, inset=16, tip=1.7, mdia=0, sh=10, kr=5.5, ends=False,
                 tabs=((40, 8, 160, 58), (168, 8, 358, 58)), tabrise=0.62, trad=3.0, tfw=2.6, brackets=False,
                 sw=(200, 654), sl=(420, 580), vx=622)
P_GEORDNET = dict(key="c3", name="C3  Geordnet", fw=2.6, cr=4.2, gable=[(38, 11), (17, 9)], eye=True, dbl=False, fin=False,
                  tw=150, pitch=54, mh=32, inset=22, tip=2.2, mdia=3.6, sh=12, kr=7.0, ends=True,
                  tabs=((40, 12, 160, 56), (168, 12, 358, 56)), tabrise=0.45, trad=3.4, tfw=2.6, brackets=True,
                  sw=(150, 704), sl=(400, 600), vx=664)
PARAMS = [P_FEIN, P_SCHLANK, P_GEORDNET]
SUBS = {
    "c1": "One reading of 'more elegant': finer. Thinner settings, small studs, more air around the text.",
    "c2": "Another reading: slimmer. Taller, narrower panels, steep calm gables, fewer ticks and doubles.",
    "c3": "Another reading: ornate but ordered. Ornament only at the gable and corners, calm surfaces between.",
}


# ------------------------------------------------------------------ Bausteine
def corner_brackets(L, x0, y0, x1, y1, n=12):
    for (cx, cy, sx, sy) in ((x0, y0, 1, 1), (x1, y0, -1, 1), (x0, y1, 1, -1), (x1, y1, -1, -1)):
        xa, xb = sorted((cx, cx + sx * n))
        ya, yb = sorted((cy, cy + sy * n))
        line(L, xa + (3 if sx < 0 else 0), cy - (1 if sy > 0 else 0) if False else min(cy, cy + sy * 4), xb - (3 if sx > 0 else 0) + 0, max(cy, cy + sy * 4) + 0, M.NL[3], 0.0)


def gable_stack(L, P, mx, y0):
    """Giebel ueber der Tafel; mehrere Stufen uebereinander (C3 zweistufig)."""
    top = y0 + 1.5
    for (hw, h) in P["gable"]:
        r4._lit_poly(L, [(mx - hw, top), (mx, top - h), (mx + hw, top)])
        top -= h - 1
    if P["eye"]:
        hw, h = P["gable"][0]
        L.shape((mx - 3, y0 - 3, mx + 3, y0 + 3), r4.sd_circle(mx, y0 - 0.5 - (0 if len(P["gable"]) > 1 else h * 0.3), 1.2), IRON0)


def tafel(L, P, x0, y0, x1, y1, gable=True, seed=10):
    poly = r4.chamfer(x0, y0, x1, y1, 3)
    fill(L, poly, y0, y1, IRON1, IRON0)
    r4.sframe(L, poly, y0, y1, P["fw"], seed)
    if P["dbl"]:
        q = r4.sd_poly(r4.chamfer(x0 + 5.5, y0 + 5.5, x1 - 5.5, y1 - 5.5, 1.0))
        L.shape((x0, y0, x1, y1), lambda px, py: q(px, py) + 0.5, NL[0], stroke=1.0, alpha=0.9)
    for (cx, cy) in ((x0 + 1.5, y0 + 1.5), (x1 - 1.5, y0 + 1.5), (x0 + 1.5, y1 - 1.5), (x1 - 1.5, y1 - 1.5)):
        r4.diamond4(L, cx, cy, P["cr"])
    if gable:
        mx = (x0 + x1) / 2
        gable_stack(L, P, mx, y0)
    if P["brackets"]:  # C3: kurze Zierleisten innen an den Ecken
        n = 14
        for (cx, cy, sx, sy) in ((x0, y0, 1, 1), (x1, y0, -1, 1), (x0, y1, 1, -1), (x1, y1, -1, -1)):
            xa = cx + sx * 6
            ya = cy + sy * 6
            line(L, min(xa, xa + sx * n), ya, max(xa, xa + sx * n), ya + 1, NL[2], 0.9)
            line(L, xa, min(ya, ya + sy * n), xa + 1, max(ya, ya + sy * n), NL[2], 0.9)


def mark(L, P, x0, y0, x1, y1, small=False, seed=20):
    cy = (y0 + y1) / 2
    h = y1 - y0
    fw = P["fw"]
    poly = r4.pointed(x0, y0, x1, y1, h / P["tip"])
    inner = r4.pointed(x0 + fw, y0 + fw, x1 - fw, y1 - fw, (h - 2 * fw) / P["tip"])
    fill(L, inner, y0, y1, IRON2, IRON0)
    r4.sframe(L, poly, y0, y1, fw, seed)
    if not small and P["mdia"]:
        r4.diamond4(L, x0 - 8, cy, P["mdia"])
        r4.diamond4(L, x1 + 8, cy, P["mdia"])


def slider(L, P, x0, x1, cy, frac, seed=30):
    sh = P["sh"]
    r4.trough(L, x0, cy - sh / 2, x1, cy + sh / 2, frac, BRASS_L, BRASS_M, "recess", back=IRON0, hi=GOLD_W, p=sh / 2.2)
    fx = x0 + (x1 - x0) * frac
    r4.diamond4(L, round(fx) + 0.5, cy, P["kr"])
    if P["ends"]:
        r4.diamond4(L, x0 - 9, cy, 2.8)
        r4.diamond4(L, x1 + 9, cy, 2.8)


def tab(L, P, box, active, seed=40):
    x0, y0, x1, y1 = box
    top = y0 if active else y0 + 8
    poly = r4.gable(x0, top, x1, y1, P["tabrise"], 3)
    q = r4.gable(x0 + P["tfw"], top + P["tfw"], x1 - P["tfw"], y1, P["tabrise"], 1)
    fill(L, q, top, y1, IRON2 if active else IRON1, IRON0)
    r4.sframe(L, poly, top, y1, P["tfw"] if active else 1.8, seed)
    if active:
        mx = (x0 + x1) / 2
        if len(P["gable"]) > 1:
            r4._lit_poly(L, [(mx - 5, top + 1.5), (mx, top - 4), (mx + 5, top + 1.5)])
        else:
            r4.diamond4(L, mx, top - 4, P["trad"])


# ------------------------------------------------------------------ Bildschirme
def heading(L, s, base, scale):
    ptext(L, s, CX, base, BONE, scale, "c")


def entries(L, P, items, c0, sel):
    for i, it in enumerate(items):
        cy = c0 + i * P["pitch"]
        if i == sel:
            mark(L, P, CX - (P["tw"] - P["inset"]), cy - P["mh"] / 2, CX + (P["tw"] - P["inset"]), cy + P["mh"] / 2, seed=20 + i)
        ptext(L, it, CX, base_for(cy, 2), ROYAL if i == sel else BONE2, 2, "c")


def group(L, P, y0, items, head, hbase, hscale):
    pad = P["mh"] / 2 + (18 if P["key"] == "c1" else 14)
    c0 = y0 + pad + 4
    y1 = c0 + (len(items) - 1) * P["pitch"] + pad + 4
    heading(L, head, hbase, hscale)
    tafel(L, P, CX - P["tw"], y0, CX + P["tw"], y1)
    entries(L, P, items, c0, 0)


def screen_title(L, P):
    group(L, P, 204, ["Start", "Settings", "Quit"], "Silver & Ash", 132, 4)


def screen_pause(L, P):
    group(L, P, 160, ["Resume", "Settings", "Back to title", "Quit"], "Paused", 104, 3)


def screen_settings(L, P):
    x0, x1 = P["sw"]
    heading(L, "Settings", 62, 3)
    rp = P["pitch"] - 6
    c0 = 138
    cs = [c0 + i * rp for i in range(4)] + [c0 + 4 * rp + 8]
    tafel(L, P, x0, 88, x1, cs[4] + P["mh"] / 2 + 20)
    rows = [("Mouse sensitivity", 0.5), ("Master volume", 0.8), ("Music volume", 0.6), ("Effects volume", 0.9)]
    lx = x0 + 26
    for i, (lab, fr) in enumerate(rows):
        cy = cs[i]
        if i == 0:
            mark(L, P, x0 + 14, cy - P["mh"] / 2, x1 - 14, cy + P["mh"] / 2, seed=21)
        ptext(L, lab, lx, base_for(cy, 2), ROYAL if i == 0 else BONE2, 2)
        slider(L, P, P["sl"][0], P["sl"][1], cy, fr, 31 + i)
        ptext(L, "%d%%" % round(fr * 100), P["vx"], base_for(cy, 2), ROYAL if i == 0 else BONE2, 2, "r")
    cy = cs[4]
    ptext(L, "Display", lx, base_for(cy, 2), BONE2, 2)
    ww, wf = FONT.width("Window") * 2, FONT.width("Full screen") * 2
    xf0 = P["vx"] - wf
    xw1 = xf0 - 30
    xw0 = xw1 - ww
    mark(L, P, xw0 - 14, cy - 15, xw1 + 14, cy + 15, small=True, seed=36)
    ptext(L, "Window", xw0, base_for(cy, 2), ROYAL, 2)
    ptext(L, "Full screen", xf0, base_for(cy, 2), BONE2, 2)
    ptext(L, "[Esc] Back", CX, int(cs[4] + P["mh"] / 2 + 48), IRON6, 1, "c")


def screen_quit(L, P):
    hw = P["tw"] - 22 if P["key"] != "c2" else 104
    tafel(L, P, CX - hw, 166, CX + hw, 322)
    heading(L, "Quit?", 224, 3)
    off = hw * 0.5
    for cx, lab, on in ((CX - off, "Yes", False), (CX + off, "No", True)):
        if on:
            mark(L, P, cx - 40, 262, cx + 40, 296, small=True, seed=24)
        ptext(L, lab, cx, base_for(279, 2), ROYAL if on else BONE2, 2, "c")


def screen_tabs(L, P):
    poly = r4.chamfer(40, 62, 814, 452, 3)
    fill(L, poly, 62, 452, IRON1, IRON0)
    r4.sframe(L, poly, 62, 452, P["fw"], 11)
    for (cx, cy) in ((41.5, 63.5), (812.5, 63.5), (41.5, 450.5), (812.5, 450.5)):
        r4.diamond4(L, cx, cy, P["cr"])
    for i, (lab, box) in enumerate(zip(["Map", "Equipment"], P["tabs"])):
        tab(L, P, box, i == 0, 41 + i)
        x0, y0, x1, y1 = box
        top = y0 if i == 0 else y0 + 8
        ptext(L, lab, (x0 + x1) / 2, base_for((top + y1) / 2 + 3, 2), ROYAL if i == 0 else IRON6, 2, "c")


SCREENS = [("Title", screen_title, 0.34), ("Pause", screen_pause, 0.34), ("Settings", screen_settings, 0.3),
           ("Quit", screen_quit, 0.34), ("Tabs", screen_tabs, 0.3)]

# Ausschnitte in 4-fach: (Name, Bildschirm, x0, y0, x1, y1) je Auslegung
CROPS = {
    "c1": [("Cursor, title (4x)", "Title", 290, 226, 446, 270), ("Gable (4x)", "Title", 349, 190, 505, 234),
           ("Slider, active row (4x)", "Settings", 470, 118, 626, 162), ("Tabs (4x)", "Tabs", 30, 4, 186, 64)],
    "c2": [("Cursor, title (4x)", "Title", 316, 226, 472, 270), ("Gable (4x)", "Title", 349, 186, 505, 230),
           ("Slider, active row (4x)", "Settings", 470, 118, 626, 162), ("Tabs (4x)", "Tabs", 30, 0, 186, 64)],
    "c3": [("Cursor, title (4x)", "Title", 290, 226, 446, 270), ("Gable (4x)", "Title", 349, 186, 505, 230),
           ("Slider, active row (4x)", "Settings", 470, 118, 626, 162), ("Tabs (4x)", "Tabs", 30, 0, 186, 64)],
}


def render(scene, P, name):
    for nm, fn, k in SCREENS:
        if nm == name:
            return r3.compose(M.dim(scene, k), lambda L, fn=fn: fn(L, P))
    raise KeyError(name)


def block(P, shots):
    gap = 12
    zooms = [M.zoom(shots[s], x0, y0, x1, y1) for (_, s, x0, y0, x1, y1) in CROPS[P["key"]]]
    zh = max(z.shape[0] for z in zooms) + 14
    Wb = 3 * W + 4 * gap
    Hb = gap + 44 + H + gap + H + gap + zh + gap
    cv = M.flat(Wb, Hb, M.PAPER)
    M.label(cv, P["name"], gap, 14 + 22, ROYAL, 3)
    M.label(cv, SUBS[P["key"]], gap + 500, 14 + 17, BONE2, 1)
    y1 = gap + 44
    y2 = y1 + H + gap
    y3 = y2 + H + gap
    for nm, col, y in (("Title", 0, y1), ("Pause", 1, y1), ("Settings", 2, y1), ("Quit", 0, y2), ("Tabs", 1, y2)):
        M.paste(cv, shots[nm], gap + col * (W + gap), y)
    x = gap
    for (nm, *_), z in zip(CROPS[P["key"]], zooms):
        M.label(cv, nm, x, y3 + 8, IRON6, 1)
        M.paste(cv, z, x, y3 + 12)
        x += z.shape[1] + 16
    return cv


def main():
    scene = hb.load_png(os.path.join(HERE, "..", "..", "hud-vorlagen", "basis-854x480.png"))
    blocks = []
    for P in PARAMS:
        shots = {nm: render(scene, P, nm) for nm, _, _ in SCREENS}
        for nm, im in shots.items():
            hb.save_png(os.path.join(HERE, "einzeln", "%s-%s.png" % (P["key"], nm.lower())), im)
        b = block(P, shots)
        hb.save_png(os.path.join(HERE, "menue-%s.png" % P["key"]), b)
        blocks.append(b)
        print("ok", P["key"], flush=True)
    Hs = sum(b.shape[0] for b in blocks) + 8 * (len(blocks) - 1)
    sheet = M.flat(blocks[0].shape[1], Hs, M.PAPER)
    y = 0
    for b in blocks:
        M.paste(sheet, b, 0, y)
        y += b.shape[0] + 8
    hb.save_png(os.path.join(HERE, "blatt-menue-r2.png"), sheet)
    print("Blatt", sheet.shape, flush=True)


main()
