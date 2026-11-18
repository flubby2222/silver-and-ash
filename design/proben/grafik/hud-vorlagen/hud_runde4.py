"""HUD-Vorlagen Runde 4 (Issue #341): Layout A aus Runde 3 in Silber, drei Bauweisen.

Aufruf:
  "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python hud_runde4.py

  A1  schmale, geschlossene Silberleiste
  A2  offener Silberrahmen ohne Fuellung
  A3  keine Leiste, nur Silberfassungen auf gemeinsamer Grundlinie
Silber = Palettenreihe IRON (hauptpalette.json), vier harte Stufen mit Koernung, ohne weichen Glanz.
Gemessen wird der verdeckte Bildanteil aus dem fertigen Bild (Unterschied zum Foto ohne HUD).
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from hud_runde2 import *  # noqa
import hud_runde3 as r3

OUT4 = os.path.join(HERE, "runde4")
os.makedirs(OUT4, exist_ok=True)
SH = (0.8, 0.8, FOG)
SILV_TOP, SILV_BOT = np.array(IRON6, np.float32) / 255, np.array(IRON4, np.float32) / 255


NUMS = True   # Zahlen bei Leben und Ausdauer (Runde 5: False)
GW = 9.5      # Zeichengröße der Waffen (Runde 6: 10.5)
ORN = 0       # Verzierung der Silberfassungen: 0 keine, 1 zurückhaltend, 2 mittel, 3 reich
NEU = False  # False = Runde 4 (bläuliches Silber), True = Endfassung (neutral, harte Kanten)


def _neutral(c, a=0.2):
    c = np.array(c, np.float32)
    lum = 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]
    return (lum + (c - lum) * a) / 255


N_LEVELS = [_neutral(c) for c in (IRON3, IRON4, IRON5, IRON6, IRON7, (226, 228, 230))]  # dunkel -> hell


def lit_ring(f, seed=0):
    """Harte Lichtkante oben/links, Schattenkante unten/rechts in wenigen Palettenstufen plus Körnung."""
    lv = np.stack(N_LEVELS)

    def g(px, py):
        nx = f(px + 1, py) - f(px - 1, py); ny = f(px, py + 1) - f(px, py - 1)
        m = np.maximum(np.hypot(nx, ny), 1e-6)
        d = -(ny / m) * 0.85 - (nx / m) * 0.5
        idx = np.select([d > 0.7, d > 0.25, d > -0.3, d > -0.7], [5, 4, 3, 2], 1)
        gx = np.floor(px).astype(np.int64); gy = np.floor(py).astype(np.int64)
        h = ((gx * 73856093) ^ (gy * 19349663) ^ (seed * 83492791)) & 0xFFFF
        n = h / 65535.0 - 0.5
        return np.clip(lv[idx] * (1 + 0.12 * n[..., None]), 0, 1)
    return g


def silver(y0, y1, seed=0, k=1.0):
    """Silber: vier harte Helligkeitsstufen (kein weicher Verlauf), Punktkoernung und feine Buerstenzeilen."""
    def f(px, py):
        gx = np.floor(px).astype(np.int64); gy = np.floor(py).astype(np.int64)
        h = ((gx * 73856093) ^ (gy * 19349663) ^ (seed * 83492791)) & 0xFFFF
        n = h / 65535.0 - 0.5
        hr = (((gy * 2654435761) ^ (seed * 97)) & 0xFFFF) / 65535.0 - 0.5
        t = np.clip((py - y0) / max(y1 - y0, 1), 0, 1)
        t = np.floor(t * 4 - 1e-6) / 3.0
        top, bot = (N_LEVELS[4], N_LEVELS[2]) if NEU else (SILV_TOP, SILV_BOT)
        base = top * (1 - t[..., None]) + bot * t[..., None]
        return np.clip(base * k * (1 + 0.13 * n[..., None] + 0.07 * hr[..., None]), 0, 1)
    return f


def sbody(L, poly, y0, y1, w=2.0, seed=0):
    """Geschlossene Silberplatte: Koernung, helle Kante oben, dunkle Kante unten, graviertes Innenlinienpaar."""
    f = sd_poly(poly); bb = bbox(poly)
    L.shape(bb, f, silver(y0, y1, seed))
    ym = (y0 + y1) / 2
    L.shape(bb, lambda px, py: f(px, py) + 1.5, IRON7, stroke=1.0, alpha=0.85, clipf=lambda px, py: py - ym)
    L.shape(bb, lambda px, py: f(px, py) + 2.5, IRON2, stroke=1.0, alpha=0.8, clipf=lambda px, py: ym - py)
    L.shape(bb, lambda px, py: f(px, py) + 0.5, IRON0, stroke=1.0, alpha=0.95)
    return f


def sframe(L, poly, y0, y1, w=2.6, seed=1):
    """Offener Silberrahmen: nur ein Streifen entlang der Form."""
    f = sd_poly(poly); bb = bbox(poly, 4)
    col = lit_ring(f, seed) if NEU else silver(y0, y1, seed)
    L.shape(bb, lambda px, py: f(px, py) + w / 2, col, stroke=w)
    L.shape(bb, lambda px, py: f(px, py) - 0.5, IRON0, stroke=1.0, alpha=0.9)
    L.shape(bb, lambda px, py: f(px, py) + w + 0.5, IRON0, stroke=1.0, alpha=0.8)
    return f


def rect(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def tx(L, size, s, x, y, color=BONE, anchor="l"):
    return L.text(F_CINZEL, size, s, x, y, color, anchor, shadow=SH)


def gl(L, name, cx, cy, s, c1, c2, mode, **kw):
    if mode != "solid":  # ohne Fuellung: dunkler Schatten damit das Zeichen auf dem Bild lesbar bleibt
        glyph(L, name, cx + 1, cy + 1, s, IRON0, IRON0, detail=IRON0)
    glyph(L, name, cx, cy, s, c1, c2, **kw)


def _lit_poly(L, pts, seed=0):
    """Kleines Silberteil: dunkler Rand, Flächen nach derselben Lichtregel wie die Fassung (Licht von oben links)."""
    f = sd_poly(pts); bb = bbox(pts, 3)
    L.shape(bb, lambda px, py: f(px, py) - 1.0, IRON0, alpha=0.92)
    L.shape(bb, f, lit_ring(f, seed))


def diamond4(L, cx, cy, r):
    _lit_poly(L, [(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)])


def ornament(L, x0, y0, x1, y1):
    """Ein Motiv an allen Fassungen: Rautenniete an den Ecken (1), dazu Spitzgiebel oben (2), dazu Doppelrahmen, Giebelauge und Fialen an langen Fassungen (3).
    Runde 6: alle Mittelpunkte liegen auf dem Pixelraster (Giebelbreite gerade/ungerade passend zur Fassung), Fialen sitzen mittig auf der Fassungskante."""
    if ORN <= 0:
        return
    tall = (y1 - y0) >= 20
    r = 3.0 if ORN < 3 else (3.4 if tall else 2.6)
    for (cx, cy) in ((x0 + 1.5, y0 + 1.5), (x1 - 1.5, y0 + 1.5), (x0 + 1.5, y1 - 1.5), (x1 - 1.5, y1 - 1.5)):
        diamond4(L, cx, cy, r)
    if ORN >= 2:
        mx = (x0 + x1) / 2
        hw = 6.0 if abs(mx - round(mx)) < 0.01 else 6.5
        _lit_poly(L, [(mx - hw, y0 + 1.5), (mx, y0 - 4.0), (mx + hw, y0 + 1.5)])
        if ORN >= 3:
            L.shape((mx - 3, y0 - 2, mx + 3, y0 + 3), sd_circle(mx, y0 - 0.5, 1.1), IRON0)
    if ORN >= 3:
        bb = (x0, y0, x1, y1)
        q = sd_poly(chamfer(x0 + 5.5, y0 + 5.5, x1 - 5.5, y1 - 5.5, 1.0))
        L.shape(bb, lambda px, py: q(px, py) + 0.5, tuple(N_LEVELS[0] * 255), stroke=1.0, alpha=0.9)
        if x1 - x0 > 100:  # lange Fassungen: Fialen mittig an den Enden
            cy = (y0 + y1) / 2
            for fx in (x0, x1):
                _lit_poly(L, [(fx, cy - 8), (fx + 2.6, cy), (fx, cy + 8), (fx - 2.6, cy)])


def cell(L, x0, y0, x1, y1, style, active=False, seed=2):
    """recess: dunkle Nische, duenne Silberlinie | frame: Silberlinie, Bild scheint durch | socket: Silberfassung mit dunkler Nische."""
    poly = chamfer(x0, y0, x1, y1, 2.5)
    f = sd_poly(poly); bb = bbox(poly, 4)
    rimc = IRON7 if active else IRON5
    if style == "recess":
        L.shape(bb, f, vg(y0, y1, IRON1, IRON0), alpha=0.97)
        L.shape(bb, lambda px, py: f(px, py) + 0.7, rimc, stroke=1.2)
        return x0 + 2, y0 + 2, x1 - 2, y1 - 2
    if style == "frame":
        L.shape(bb, lambda px, py: f(px, py) - 1.1, IRON0, stroke=1.0, alpha=0.75)
        L.shape(bb, lambda px, py: f(px, py) + 0.8, silver(y0, y1, seed, 1.12 if active else 1.0), stroke=1.8)
        return x0 + 2, y0 + 2, x1 - 2, y1 - 2
    sframe(L, poly, y0, y1, 3.0, seed)
    q = chamfer(x0 + 3, y0 + 3, x1 - 3, y1 - 3, 1.5)
    L.shape(bb, sd_poly(q), vg(y0, y1, IRON1, IRON0), alpha=0.97)
    if active:
        L.shape(bb, lambda px, py: sd_poly(q)(px, py) + 0.7, IRON7, stroke=1.0, alpha=0.9)
    if x1 - x0 >= 24:
        ornament(L, x0, y0, x1, y1)
    return x0 + 3, y0 + 3, x1 - 3, y1 - 3


def trough(L, x0, y0, x1, y1, frac, c0, c1, style, back=IRON0, hi=None, p=None):
    h = y1 - y0
    f = sd_poly(pointed(x0, y0, x1, y1, p or h / 2.5))
    bb = (x0 - 4, y0 - 4, x1 + 4, y1 + 4)
    fx = x0 + (x1 - x0) * frac
    if style == "frame":  # ohne Fuellung: leerer Teil bleibt durchsichtig, nur dunkler Rand
        L.shape(bb, lambda px, py: f(px, py) - 1.4, IRON0, stroke=1.0, alpha=0.8)
    else:
        L.shape(bb, lambda px, py: f(px, py) - 1.8, IRON0, alpha=0.95)
        L.shape(bb, f, back)
    L.shape(bb, f, vg(y0, y1, c0, c1), clipf=clipx(fx))
    if hi:
        L.shape(bb, f, hi, alpha=0.8, clipf=lambda px, py: np.maximum(px - fx, py - (y0 + h * 0.32)))
        L.shape(bb, sd_seg((fx, y0 + 1), (fx, y1 - 1), 1.0), hi, clipf=f)
    L.shape(bb, lambda px, py: f(px, py) + 0.9, silver(y0, y1, 3), stroke=1.8)


HPV, STV = 64, 62  # wie Runde 3 A: 50 % von 128, Ausdauer in Prozent


def nums(L, xl, xr, yc):
    tx(L, 15, str(HPV), xl, yc + 5.5, BONE, "c")
    tx(L, 13, str(STV), xr, yc + 4.5, BONE, "c")


def segs(L, x0, x1, y0, y1, style, n=5, gap=3):
    sw = (x1 - x0 - (n - 1) * gap) / n
    for i in range(n):
        sx = x0 + i * (sw + gap)
        b = sd_box(sx, y0, sx + sw, y1, 0.8)
        bb = (sx - 3, y0 - 3, sx + sw + 3, y1 + 3)
        if style == "frame":
            L.shape(bb, lambda px, py: b(px, py) - 1.2, IRON0, stroke=1.0, alpha=0.75)
        else:
            L.shape(bb, lambda px, py: b(px, py) - 1.4, IRON0, alpha=0.97)
            L.shape(bb, b, IRON1)
        fill = np.clip(STAM * n - i, 0, 1)
        if fill > 0:
            L.shape(bb, b, vg(y0, y1, tuple(N_LEVELS[5] * 255) if NEU else IRON7, tuple(N_LEVELS[3] * 255) if NEU else IRON5), clipf=clipx(sx + sw * fill))
        L.shape(bb, lambda px, py: b(px, py) + 0.7, tuple(N_LEVELS[2] * 255) if NEU else IRON4, stroke=1.0)


def weapon_cells(L, x0, y0, y1, w, gap, style, gs, nsize):
    for i, (g, am) in enumerate(zip(("blade", "pistol", "rifle", "blunder"), AMMO)):
        x = x0 + i * (w + gap); act = i == ACTIVE
        a, b, c, d = cell(L, x, y0, x + w, y1, style, act, seed=4 + i)
        cy = (b + d) / 2
        gl(L, g, a + (c - a) * (0.36 if am is not None else 0.5), cy, gs, BONE if act else BONE2, BONE3 if act else IRON6, style)
        if am is not None:
            tx(L, nsize, str(am), a + (c - a) * 0.78, cy + nsize * 0.36, BONE if act else BONE2, "c")
        if act:
            veil(L, a, b, c, d - (d - b) * 0.0, RELOAD)


def item_cells(L, x0, y0, y1, w, gap, style, gs):
    for i, g in enumerate(("flask", "eye")):
        x = x0 + i * (w + gap)
        a, b, c, d = cell(L, x, y0, x + w, y1, style, False, seed=8 + i)
        gl(L, g, (a + c) / 2, (b + d) / 2, gs, BONE if g == "flask" else IRON7, BONE3 if g == "flask" else IRON5, style, detail=(40, 20, 10))


def toast(L, x0, y0, x1, y1, style):
    if style == "recess":
        sbody(L, chamfer(x0, y0, x1, y1, 2.5), y0, y1, seed=9)
    elif style == "frame":
        sframe(L, chamfer(x0, y0, x1, y1, 2.5), y0, y1, 1.8, 9)
    else:
        sframe(L, chamfer(x0, y0, x1, y1, 2.5), y0, y1, 2.6, 9)
        L.shape((x0, y0, x1, y1), sd_box(x0 + 2.6, y0 + 2.6, x1 - 2.6, y1 - 2.6), IRON0, alpha=0.93)
    cyy = (y0 + y1) / 2
    glyph(L, "eye", x0 + 10, cyy, 4.2, ROYAL, BRASS_M, detail=(40, 20, 10))
    tx(L, 10.5, "Picked up Hunter's Eye", x0 + 19, cyy + 3.8, ROYAL)


def centre(L):
    L.shape((420, 233, 434, 247), sd_circle(427, 240, 2.2), (16, 11, 20), alpha=0.85)
    L.shape((420, 233, 434, 247), sd_circle(427, 240, 1.4), BONE)
    capx = 427 - (17 + 5 + textw(L, F_CINZEL, 12, "to open")) / 2
    cell(L, capx, 253, capx + 17, 271, "socket", True)
    tx(L, 11, "F", capx + 8.5, 267, IRON7, "c")
    tx(L, 12, "to open", capx + 22, 267)


def top_text(L, xr, y_a, y_b):
    for (g, val, y) in (("coin", SILVER, y_a), ("key", KEYS, y_b)):
        w = tx(L, 13, str(val), xr, y, BONE, "r")
        glyph(L, g, xr - w - 11, y - 4.6, 5.6, IRON7, IRON4, detail=IRON3)


# ================================================================ Varianten

def build(L, V):
    style = {1: "recess", 2: "frame", 3: "socket"}[V]
    sty = style
    # ---------------------------------------------------------------- unten (Layout A: Gegenstaende | Leben+Ausdauer | Waffen)
    if V == 1:
        BY = 448   # Leiste y 448..480 = 32 Pixel
        sbody(L, rect(-6, BY, 860, 486), BY, 480)
        for x in (170, 684):
            L.shape((x - 4, BY, x + 4, 482), sd_box(x - 1, BY + 1, x + 1, 480), IRON0, alpha=0.9)
            L.shape((x - 4, BY, x + 4, 482), sd_box(x + 1, BY + 1, x + 2, 480), IRON7, alpha=0.85)
        cy0, cy1 = BY + 4, 477
        item_cells(L, 14, cy0, cy1, 30, 4, sty, 8)
        weapon_cells(L, 692, cy0, cy1, 38, 3, sty, GW, 10)
        nums(L, 206, 648, BY + 14)
        trough(L, 224, BY + 6, 630, BY + 17, HP, RED5, RED2, sty, back=RED0, hi=RED6, p=5)
        segs(L, 224, 630, BY + 21, BY + 26, sty, 5, 4)
        toast(L, 8, 424, 172, 440, sty)
    elif V == 2:
        BY = 446
        # offener Rahmen: Schiene oben, zwei Pfeiler, nichts dazwischen
        for (x0, x1) in ((-6, 170), (684, 860)):
            pass
        f = sd_box(-6, BY, 860, BY + 3)
        L.shape((0, BY - 3, 854, BY + 6), f, silver(BY, BY + 3, 5))
        L.shape((0, BY - 3, 854, BY + 6), lambda px, py: f(px, py) - 1.0, IRON0, stroke=1.0, alpha=0.8)
        for x in (170, 684):
            fp = sd_box(x - 1.3, BY, x + 1.3, 482)
            L.shape((x - 4, BY - 3, x + 4, 484), fp, silver(BY, 480, 6))
            L.shape((x - 4, BY - 3, x + 4, 484), lambda px, py, fp=fp: fp(px, py) - 1.0, IRON0, stroke=1.0, alpha=0.8)
        item_cells(L, 14, BY + 6, 477, 32, 4, sty, 8.5)
        weapon_cells(L, 690, BY + 6, 477, 38, 3, sty, GW, 10)
        nums(L, 206, 648, BY + 17)
        trough(L, 224, BY + 8, 630, BY + 20, HP, RED5, RED2, sty, hi=RED6, p=5)
        segs(L, 224, 630, BY + 25, BY + 30, sty, 5, 4)
        toast(L, 8, 420, 172, 436, sty)
    else:
        GY = 477   # gemeinsame Grundlinie: alle Fassungen enden hier
        item_cells(L, 12, GY - 30, GY, 30, 4, sty, 8)
        weapon_cells(L, 690, GY - 30, GY, 38, 3, sty, GW, 10)
        cell(L, 247, GY - 30, 607, GY, "socket", False, seed=10)
        ta, tb = (283, 571) if NUMS else (257, 597)
        if NUMS:
            nums(L, 265, 589, GY - 15)
        trough(L, ta, GY - 24, tb, GY - 14, HP, RED5, RED2, "recess", back=RED0, hi=RED6, p=4)
        segs(L, ta, tb, GY - 10, GY - 5, "recess", 5, 4)
        toast(L, 8, 424, 172, 440, sty)

    # ---------------------------------------------------------------- oben: dieselben Anzeigen, Huelle je Variante
    cx = 427
    if V == 1:
        sbody(L, [(-6, -6), (160, -6), (160, 30), (152, 38), (-6, 38)], -6, 38, seed=11)
        sbody(L, [(716, -6), (860, -6), (860, 38), (716, 38)][::1], -6, 38, seed=12)
        sbody(L, [(262, -6), (592, -6), (592, 26), (584, 34), (270, 34), (262, 26)], -6, 34, seed=13)
    elif V == 2:
        sframe(L, [(-6, -6), (160, -6), (160, 30), (152, 38), (-6, 38)], -6, 38, 2.6, 11)
        sframe(L, [(716, -6), (860, -6), (860, 38), (716, 38)], -6, 38, 2.6, 12)
        sframe(L, [(262, -6), (592, -6), (592, 26), (584, 34), (270, 34), (262, 26)], -6, 34, 2.6, 13)
    ty0, ty1 = 5, 33
    # TL: Stufe, Erfahrung, zwei Faehigkeiten
    a, b, c, d = cell(L, 8, ty0, 34, ty1, sty, True, 14)
    tx(L, 16, str(LEVEL), (a + c) / 2, 25, IRON7, "c")
    xbx0, xbx1 = 40, 98
    if V == 3:
        cell(L, 38, 12, 100, 26, "socket", False, 15)
        trough(L, 43, 16, 95, 22, XP, IRON7, IRON5, "recess", back=IRON0, p=3)
    else:
        trough(L, xbx0, 16, xbx1, 23, XP, IRON7, IRON5, sty, back=IRON1, p=3)
    for i, g in enumerate(("flame", "shield")):
        x = 106 + i * 28
        a, b, c, d = cell(L, x, ty0, x + 24, ty1, sty, False, 16 + i)
        gl(L, g, (a + c) / 2, (b + d) / 2, 7.2, BONE, BONE3, sty)
    # TR: Silber und Schluessel in einer Zeile
    if V == 3:
        cell(L, 724, ty0, 850, ty1, "socket", False, 18)
    w1 = tx(L, 13, str(SILVER), 784, 24, BONE, "r")
    glyph(L, "coin", 784 - w1 - 10, 20, 5.6, IRON7, IRON4, detail=IRON3)
    w2 = tx(L, 13, str(KEYS), 840, 24, BONE, "r")
    glyph(L, "key", 840 - w2 - 13, 20, 6.2, IRON7, IRON4)
    # Boss: Name und Balken
    if V == 3:
        cell(L, 300, 4, 554, 33, "socket", False, 19)
        trough(L, 316, 21, 538, 28, BOSS, BONE, BONE3, "recess", p=3)
        tx(L, 11, "GORM", cx, 17, BONE, "c")
    else:
        tx(L, 11, "GORM", cx, 15, BONE, "c")
        trough(L, 282, 19, 572, 27, BOSS, BONE, BONE3, sty, p=3)
    centre(L)


def design(V):
    return lambda L: build(L, V)


def measure(out, scene):
    return float(np.mean(np.max(np.abs(out - scene), axis=2) > 0.03) * 100.0)


TITLES = ["A1  Schmale Silberleiste", "A2  Offener Silberrahmen", "A3  Silberfassungen auf einer Linie"]


def sheet(nat, pct, p3):
    gap, top, bottom = 14, 46, 100
    W = 3 * 854 + 4 * gap; H = top + 480 + bottom + gap
    cv = np.tile(np.array((20, 16, 26), np.float32) / 255, (H, W, 1))
    for i, im in enumerate(nat):
        cv[top:top + 480, gap + i * (854 + gap):gap + i * (854 + gap) + 854] = im
    L = Layer(W, H, False)
    L.sx = L.sy = L.s = 1.0
    notes = [
        ["Geschlossene Leiste, 32 px hoch (Runde 3: 60 px); oben schmale Platten.",
         "Die Fläche bleibt Silber mit Körnung, Anzeigen liegen in dunklen Nischen; Leben und Ausdauer mit Zahlen."],
        ["Nur Schiene, Pfeiler und Zellenränder aus Silber, nichts gefüllt:",
         "das Spiel scheint durch; Zeichen haben einen dunklen Schatten."],
        ["Keine Leiste und keine Pfeiler; jede Anzeige hat ihre eigene Fassung,",
         "alle enden auf derselben Grundlinie (y 477)."],
    ]
    for i, n in enumerate(TITLES):
        x = gap + i * (854 + gap)
        L.text(F_CINZEL, 20, n, x + 427, 33, IRON7, "c")
        ys = top + 480 + 24
        L.text(F_BELL, 16, f"Verdeckt: {pct[i]:.1f} % des Bildes (Runde 3 A: {p3:.1f} %)", x + 4, ys, BONE)
        for k, line in enumerate(notes[i]):
            L.text(F_BELL, 15, line, x + 4, ys + 24 + k * 21, BONE2)
    return np.clip(cv * (1 - L.a[..., None]) + L.rgb, 0, 1)


def main_final():
    """Endfassung A3 mit neutralem Silber; Vergleich alt/neu in 854 x 480."""
    global NEU
    scene = load_png(os.path.join(HERE, "basis-854x480.png"))
    ims = []
    for neu in (False, True):
        NEU = neu
        im = r3.compose(scene, design(3))
        ims.append(im)
        print("A3", "neu" if neu else "alt", round(measure(im, scene), 2), flush=True)
    save_png(os.path.join(OUT4, "A3-final-854.png"), ims[1])
    save_png(os.path.join(OUT4, "A3-final.png"), nearest(ims[1], FINAL_H, FINAL_W))
    gap = 14
    cv = np.tile(np.array((20, 16, 26), np.float32) / 255, (480 + 40, 2 * 854 + 3 * gap, 1))
    cv[40:, gap:gap + 854] = ims[0]
    cv[40:, 2 * gap + 854:2 * gap + 1708] = ims[1]
    L = Layer(cv.shape[1], cv.shape[0], False); L.sx = L.sy = L.s = 1.0
    L.text(F_CINZEL, 18, "A3 alt (Runde 4)", gap + 427, 28, IRON7, "c")
    L.text(F_CINZEL, 18, "A3 neu (neutrales Silber)", 2 * gap + 854 + 427, 28, IRON7, "c")
    save_png(os.path.join(OUT4, "vergleich-A3-alt-neu.png"), np.clip(cv * (1 - L.a[..., None]) + L.rgb, 0, 1))


def main4():
    scene = load_png(os.path.join(HERE, "basis-854x480.png"))
    p3 = measure(r3.compose(scene, r3.design_a), scene)
    nat, pct = [], []
    for V, nm in ((1, "geschlossen"), (2, "offen"), (3, "fassungen")):
        im = r3.compose(scene, design(V))
        nat.append(im); pct.append(measure(im, scene))
        save_png(os.path.join(OUT4, f"variante-A{V}-{nm}.png"), nearest(im, FINAL_H, FINAL_W))
        save_png(os.path.join(OUT4, f"variante-A{V}-{nm}-854.png"), im)
        print("ok", V, round(pct[-1], 2), flush=True)
    print("Runde 3 A", round(p3, 2))
    save_png(os.path.join(OUT4, "blatt-runde4.png"), sheet(nat, pct, p3))


def sheet5(nat, pct, base):
    gap, top, bottom = 14, 46, 100
    W = 3 * 854 + 4 * gap; H = top + 480 + bottom + gap
    cv = np.tile(np.array((20, 16, 26), np.float32) / 255, (H, W, 1))
    for i, im in enumerate(nat):
        cv[top:top + 480, gap + i * (854 + gap):gap + i * (854 + gap) + 854] = im
    L = Layer(W, H, False); L.sx = L.sy = L.s = 1.0
    titles = ["V1  Zurückhaltend", "V2  Mittel", "V3  Reich"]
    notes = [["Rautenniete in den vier Ecken jeder Fassung.", "Sonst nichts; das Silber der Fassung selbst bleibt unverändert."],
             ["Ecknieten und ein Spitzgiebel oben in der Mitte jeder Fassung.", "Dasselbe Motiv an allen Fassungen, auch oben und am Boss."],
             ["Dazu Giebelauge, Doppelrahmen innen und Fialen an den langen Fassungen.", "Mehr Gotik, aber alles im selben Silber."]]
    for i, n in enumerate(titles):
        x = gap + i * (854 + gap)
        L.text(F_CINZEL, 20, n, x + 427, 33, IRON7, "c")
        ys = top + 480 + 24
        L.text(F_BELL, 16, f"Verdeckt: {pct[i]:.1f} % des Bildes (A3 ohne Verzierung, ohne Zahlen: {base:.1f} %)", x + 4, ys, BONE)
        for k, line in enumerate(notes[i]):
            L.text(F_BELL, 15, line, x + 4, ys + 24 + k * 21, BONE2)
    return np.clip(cv * (1 - L.a[..., None]) + L.rgb, 0, 1)


def main5():
    """Runde 5: A3 ohne Zahlen bei Leben/Ausdauer, drei Stufen Verzierung."""
    global NEU, NUMS, ORN
    NEU, NUMS = True, False
    scene = load_png(os.path.join(HERE, "basis-854x480.png"))
    ORN = 0
    base = measure(r3.compose(scene, design(3)), scene)
    nat, pct = [], []
    for o, nm in ((1, "zurueckhaltend"), (2, "mittel"), (3, "reich")):
        ORN = o
        im = r3.compose(scene, design(3))
        nat.append(im); pct.append(measure(im, scene))
        save_png(os.path.join(OUT4, f"r5-V{o}-{nm}-854.png"), im)
        save_png(os.path.join(OUT4, f"r5-V{o}-{nm}.png"), nearest(im, FINAL_H, FINAL_W))
        print("R5", o, round(pct[-1], 2), "Basis", round(base, 2), flush=True)
    save_png(os.path.join(OUT4, "blatt-runde5.png"), sheet5(nat, pct, base))

def sheet6(old, new, pct_old, pct_new):
    """Blatt Runde 6: V3 alt | V3 neu, darunter 4x-Ausschnitte der Fassungen (alt oben, neu unten), unten das Endbild."""
    gap = 14
    crops = [("Unten links", 0, 416, 240, 480), ("Unten Mitte", 230, 440, 470, 480), ("Unten rechts", 600, 440, 854, 480),
             ("Oben links", 0, 0, 240, 44), ("Boss", 290, 0, 560, 44), ("Oben rechts", 700, 0, 854, 44)]
    k = 3
    rows_h = [(c[4] - c[2]) * k for c in crops]
    W = 2 * 854 + 3 * gap
    H = 586 + sum(22 + h + 12 for h in rows_h) + 20
    cv = np.tile(np.array((20, 16, 26), np.float32) / 255, (H, W, 1))
    cv[46:526, gap:gap + 854] = old
    cv[46:526, 2 * gap + 854:2 * gap + 1708] = new
    L = Layer(W, H, False); L.sx = L.sy = L.s = 1.0
    L.text(F_CINZEL, 20, "V3 aus Runde 5", gap + 427, 33, IRON7, "c")
    L.text(F_CINZEL, 20, "V3 überarbeitet (Runde 6)", 2 * gap + 854 + 427, 33, IRON7, "c")
    L.text(F_BELL, 16, f"Verdeckt: {pct_old:.1f} % des Bildes", gap + 4, 550, BONE)
    L.text(F_BELL, 16, f"Verdeckt: {pct_new:.1f} % des Bildes (Grenze 10 %)", 2 * gap + 854 + 4, 550, BONE)
    y = 586
    # Raster: 3 Zeilen x 2 Spalten (links alt, rechts neu) je Ausschnitt-Paar
    yy = y
    for j, (name, x0, y0, x1, y1) in enumerate(crops):
        h = (y1 - y0) * k
        L.text(F_BELL, 15, f"{name} (3-fach): links alt, rechts neu", gap + 4, yy + 14, BONE2)
        yy += 22
        for side, im in enumerate((old, new)):
            c = np.repeat(np.repeat(im[y0:y1, x0:x1], k, 0), k, 1)
            cw = c.shape[1]
            xx = gap + side * (854 + gap)
            cv[yy:yy + h, xx:xx + cw] = c
        yy += h + 12
    out = np.clip(cv * (1 - L.a[..., None]) + L.rgb, 0, 1)
    return out[:yy + 10]


def main6():
    """Runde 6: V3 überarbeitet. Alt = Stand Runde 5 (GW 9.5), neu = Feinschliff."""
    global NEU, NUMS, ORN, GW
    NEU, NUMS, ORN = True, False, 3
    scene = load_png(os.path.join(HERE, "basis-854x480.png"))
    GW = 10.5
    new = r3.compose(scene, design(3))
    pn = measure(new, scene)
    old = load_png(os.path.join(HERE, "runde5", "r5-V3-reich-854.png"))
    po = measure(old, scene)
    out = os.path.join(HERE, "runde6"); os.makedirs(out, exist_ok=True)
    save_png(os.path.join(out, "r6-V3-final-854.png"), new)
    save_png(os.path.join(out, "r6-V3-final.png"), nearest(new, FINAL_H, FINAL_W))
    save_png(os.path.join(out, "blatt-runde6.png"), sheet6(old, new, po, pn))
    print("R6 alt", round(po, 2), "neu", round(pn, 2), flush=True)



if __name__ == "__main__":
    import sys as _s
    if "--runde6" in _s.argv:
        main6()
    elif "--runde5" in _s.argv:
        main5()
    else:
        main4()
        main_final()
