"""HUD-Vorlagen Runde 3 (Issue #341): drei Muster aus der Recherche (docs/design/grundlagen-hud.md), Kathedrale, 854x480 mitgekoernt.

Aufruf:
  "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python hud_runde3.py

  A  Eine Leiste        (Muster 1-3: ein Koerper aus Weltmaterial, ein Fuenftel der Hoehe wie Quake/Diablo)
  B  Behaelter in Fassung (Muster 4-5: ein Hauptdarsteller, Leben als fuellbares Gefaess)
  C  Ein Zellenmass      (Muster 6: gleiche gerahmte Zelle fuer jeden Wert wie Blood/Castlevania)
Nutzt die Zeichenwerkzeuge aus hud_blaetter.py und hud_runde2.py. Texturen: echte Spieltexturen, 1 Texel = 1 Bildpunkt.
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from hud_runde2 import *  # noqa
import hud_runde2 as r2

OUT3 = os.path.join(HERE, "runde3")
os.makedirs(OUT3, exist_ok=True)
SH = (0.8, 0.8, FOG)
HPV = 64     # 50 % von 128
STV = 62     # Ausdauer in Prozent


def rect(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def tx(L, size, s, x, y, color=BONE, anchor="l"):
    return L.text(F_CINZEL, size, s, x, y, color, anchor, shadow=SH)


def slab(L, poly, y0, y1, top=0.95, bot=0.6, ox=0, w=2.0):
    return body(L, poly, stone(y0, y1, top * 0.8, bot * 0.8, ox=ox), w=w)


def cell3(L, x0, y0, x1, y1, active=False, frame=3.5):
    """Zelle: Steinfassung, darin dunkle Eisen-Nische mit Messingkante. Gibt die Innenmasse zurueck."""
    slab(L, chamfer(x0, y0, x1, y1, 3), y0, y1, 0.95, 0.65, ox=int(x0), w=1.6)
    a, b, c, d = x0 + frame, y0 + frame, x1 - frame, y1 - frame
    poly = chamfer(a, b, c, d, 2)
    f = sd_poly(poly); bb = bbox(poly)
    L.shape(bb, f, iron(b, d, 0.95 if active else 0.62, 0.6 if active else 0.38))
    L.shape(bb, f, (10, 7, 14), alpha=0.6, clipf=lambda px, py: (b + (d - b) * 0.4) - py)
    L.shape(bb, lambda px, py: f(px, py) + 0.8, vg(b, d, BRASS_L if active else BRASS_M, BRASS_D), stroke=1.6)
    return a, b, c, d


def trough(L, x0, y0, x1, y1, frac, c0, c1, back=IRON0, rim=True, p=None, hi=None):
    h = y1 - y0
    f = sd_poly(pointed(x0, y0, x1, y1, p or h / 2.5))
    bb = (x0 - 4, y0 - 4, x1 + 4, y1 + 4)
    L.shape(bb, lambda px, py: f(px, py) - 2.4, IRON0, alpha=0.95)
    L.shape(bb, f, back)
    fx = x0 + (x1 - x0) * frac
    L.shape(bb, f, vg(y0, y1, c0, c1), clipf=clipx(fx))
    if hi:
        L.shape(bb, f, hi, alpha=0.8, clipf=lambda px, py: np.maximum(px - fx, py - (y0 + h * 0.32)))
        L.shape(bb, sd_seg((fx, y0 + 2), (fx, y1 - 2), 1.0), hi, clipf=f)
    if rim:
        L.shape(bb, lambda px, py: f(px, py) + 0.8, vg(y0, y1, BRASS_L, BRASS_D), stroke=1.6)


def toast(L, x0, y0, x1, y1):
    slab(L, chamfer(x0, y0, x1, y1, 3), y0, y1, 0.9, 0.6, ox=int(x0), w=1.8)
    glyph(L, "eye", x0 + 11, (y0 + y1) / 2, 4.6, ROYAL, BRASS_M, detail=(40, 20, 10))
    tx(L, 11, "Picked up Hunter's Eye", x0 + 21, (y0 + y1) / 2 + 4, ROYAL)


def centre(L):
    L.shape((420, 233, 434, 247), sd_circle(427, 240, 2.2), (16, 11, 20), alpha=0.85)
    L.shape((420, 233, 434, 247), sd_circle(427, 240, 1.4), BONE)
    capx = 427 - (17 + 5 + textw(L, F_CINZEL, 12, "to open")) / 2
    cell3(L, capx, 253, capx + 17, 271, True, 2.2)
    tx(L, 11, "F", capx + 8.5, 267, BRASS_L, "c")
    tx(L, 12, "to open", capx + 22, 267)


def weapons(L, x0, y0, y1, w, gap, gs, ny, frame=3.5, nsize=12):
    for i, (g, am) in enumerate(zip(("blade", "pistol", "rifle", "blunder"), AMMO)):
        x = x0 + i * (w + gap); act = i == ACTIVE
        a, b, c, d = cell3(L, x, y0, x + w, y1, act, frame)
        glyph(L, g, (a + c) / 2, b + (d - b) * 0.38, gs, BONE if act else BONE2, BONE3 if act else IRON6)
        if am is not None:
            tx(L, nsize, str(am), (a + c) / 2, ny, BRASS_L if act else BONE, "c")
        if act:
            veil(L, a + 1, b + 1, c - 1, b + (d - b) * 0.72, RELOAD)


def items(L, x0, y0, y1, w, gap, gs, frame=3.5):
    for i, g in enumerate(("flask", "eye")):
        x = x0 + i * (w + gap)
        a, b, c, d = cell3(L, x, y0, x + w, y1, False, frame)
        glyph(L, g, (a + c) / 2, (b + d) / 2, gs, BONE if g == "flask" else BRASS_L, BONE3 if g == "flask" else BRASS, detail=(40, 20, 10))


def stamina_segs(L, x0, x1, y0, y1, n=5, gap=4):
    sw = (x1 - x0 - (n - 1) * gap) / n
    for i in range(n):
        sx = x0 + i * (sw + gap)
        b = sd_box(sx, y0, sx + sw, y1, 1)
        L.shape((sx - 3, y0 - 3, sx + sw + 3, y1 + 3), lambda px, py: b(px, py) - 1.6, IRON0, alpha=0.97)
        L.shape((sx - 3, y0 - 3, sx + sw + 3, y1 + 3), b, IRON1)
        fill = np.clip(STAM * n - i, 0, 1)
        if fill > 0:
            L.shape((sx - 3, y0 - 3, sx + sw + 3, y1 + 3), b, vg(y0, y1, IRON7, IRON4), clipf=clipx(sx + sw * fill))
        L.shape((sx - 3, y0 - 3, sx + sw + 3, y1 + 3), b, BRASS_D, stroke=1)


def top_right_text(L, x_right, y_a, y_b):
    for (g, val, y) in (("coin", SILVER, y_a), ("key", KEYS, y_b)):
        w = tx(L, 14, str(val), x_right, y, BONE, "r")
        if g == "coin":
            glyph(L, "coin", x_right - w - 12, y - 5, 6.2, IRON7, IRON4, detail=IRON3)
        else:
            glyph(L, "key", x_right - w - 16, y - 5, 7.5, BRASS_L, BRASS_M)


# ================================================================ A: eine Leiste

def design_a(L):
    # --- Leiste: ein Koerper ueber die ganze Breite, y 420..480 (ein Achtel der Hoehe) ---
    slab(L, rect(-6, 421, 860, 486), 421, 480, 1.0, 0.55, w=2.4)
    # Rost-Fugen: zwei dunkle Zierlinien, die die Leiste zonen
    L.shape((0, 424, 854, 428), sd_box(-6, 425, 860, 426.2), IRON0, alpha=0.7)
    # Pfeiler (Eisenbohlen) teilen links / Mitte / rechts
    for x in (168, 686):
        body(L, rect(x - 5, 413, x + 5, 486), iron(413, 480, 1.25, 0.8), w=1.6)
        spire(L, x, 413, 9, 5.5)
        for yy in (430, 456):
            diamond(L, x, yy, 2.6)
    # Mitte: Leben als Hauptdarsteller
    a, b, c, d = cell3(L, 190, 427, 234, 455, True, 3)
    tx(L, 17, str(HPV), (a + c) / 2, 449, BONE, "c")
    trough(L, 240, 430, 614, 452, HP, RED5, RED2, back=RED0, p=9, hi=RED6)
    a, b, c, d = cell3(L, 620, 427, 664, 455, False, 3)
    tx(L, 15, str(STV), (a + c) / 2, 448.5, IRON6, "c")
    stamina_segs(L, 240, 614, 458, 468, 5, 5)
    # Ecken: unten links, unten rechts
    items(L, 16, 429, 471, 42, 6, 10.5)
    quatrefoil(L, 134, 450, 7)
    weapons(L, 698, 428, 473, 36, 4, 12.5, 468, 3, 11)
    toast(L, 8, 396, 178, 414)
    # --- oben: buendig am Bildrand, aus demselben Stein ---
    slab(L, [(-6, -6), (178, -6), (178, 44), (168, 54), (96, 54), (96, 92), (86, 102), (-6, 102)], 0, 102, 1.0, 0.6, w=2.2)
    a, b, c, d = cell3(L, 10, 8, 44, 46, True, 3)
    tx(L, 19, str(LEVEL), (a + c) / 2, 36, BRASS_L, "c")
    tx(L, 10, "LV", 54, 22, BONE)
    trough(L, 54, 27, 152, 37, XP, BRASS_L, BRASS_D, back=IRON1, p=4)
    glyph(L, "star", 164, 32, 6.2, BRASS_L, BRASS)
    for i, g in enumerate(("flame", "shield")):
        x = 12 + i * 40
        a, b, c, d = cell3(L, x, 58, x + 34, 94, False, 3)
        glyph(L, g, (a + c) / 2, (b + d) / 2, 8.5, BONE, BONE3)
    slab(L, [(684, -6), (860, -6), (860, 60), (694, 60), (684, 50)], 0, 60, 1.0, 0.6, ox=3, w=2.2)
    top_right_text(L, 840, 25, 47)
    slab(L, [(250, -6), (604, -6), (604, 34), (590, 52), (264, 52), (250, 34)], 0, 52, 1.0, 0.6, ox=5, w=2.2)
    tx(L, 12, "GORM", 427, 19, BONE, "c")
    trough(L, 276, 27, 578, 40, BOSS, BONE, BONE3, p=6)
    centre(L)


# ================================================================ B: Behaelter in Fassung

def design_b(L):
    cx, cy, R = 427, 443, 42
    # --- niedrige Fassung: Fluegel links und rechts, Sockel durchgehend ---
    slab(L, rect(-6, 462, 860, 486), 440, 480, 1.0, 0.55, w=2.2)
    slab(L, [(-6, 438), (262, 438), (290, 462), (290, 486), (-6, 486)], 438, 480, 1.0, 0.6, w=2.4)
    slab(L, [(592, 438), (860, 438), (860, 486), (564, 486), (564, 462)], 438, 480, 1.0, 0.6, ox=7, w=2.4)
    items(L, 14, 446, 476, 50, 6, 10.5)
    quatrefoil(L, 104, 461, 7)
    quatrefoil(L, 236, 461, 6)
    weapons(L, 612, 444, 477, 52, 6, 14, 472, 3.5, 12)
    toast(L, 8, 414, 178, 432)
    # --- Gefaess: Rosette mit Fluessigkeit ---
    bb = (cx - 60, cy - 60, cx + 60, cy + 60)
    L.shape(bb, sd_circle(cx, cy, R), stone(cy - R, cy + R, 1.0, 0.6, ox=3))
    L.shape(bb, lambda px, py: sd_circle(cx, cy, R)(px, py) + 0.8, vg(cy - R, cy + R, BRASS_L, BRASS_D), stroke=1.8)
    L.shape(bb, lambda px, py: sd_circle(cx, cy, R)(px, py) + 3.6, IRON0, stroke=1.0, alpha=0.9)
    for k in range(12):
        ang = math.radians(k * 30 + 15)
        nx, ny = cx + 38.6 * math.cos(ang), cy + 38.6 * math.sin(ang)
        L.shape((nx - 3, ny - 3, nx + 3, ny + 3), sd_circle(nx, ny, 1.7), vg(ny - 2, ny + 2, BRASS_L, BRASS_D))
    g = sd_circle(cx, cy, 33)
    L.shape(bb, lambda px, py: g(px, py) + 1.6, IRON0)
    L.shape(bb, g, RED0)
    lvl = cy + 33 - 66 * HP
    L.shape(bb, g, vg(lvl, cy + 33, RED5, RED2), clipf=lambda px, py: lvl - py)
    L.shape(bb, g, RED6, alpha=0.9, clipf=lambda px, py: np.maximum(py - lvl - 1.3, lvl - py))
    L.shape(bb, lambda px, py: g(px, py) + 0.7, vg(cy - 33, cy + 33, BRASS_L, BRASS_D), stroke=1.4)
    L.shape(bb, sd_ellipse(cx - 13, cy - 17, 6, 3), BONE, alpha=0.22, clipf=g)
    tx(L, 17, str(HPV), cx, cy + 5, BONE, "c")
    spire(L, cx, cy - R - 1, 10, 6)
    # Ausdauer: Segmente als Bogen um das Gefaess
    r_st = 52
    L.shape(bb if False else (cx - 70, cy - 70, cx + 70, cy), sd_arc(cx, cy, r_st, 203, 337, 9), IRON0, alpha=0.95)
    for i in range(5):
        a0 = 207 + i * 26.0; a1 = a0 + 22
        L.shape((cx - 70, cy - 70, cx + 70, cy), sd_arc(cx, cy, r_st, a0, a1, 6), IRON1)
        fill = np.clip(STAM * 5 - i, 0, 1)
        if fill > 0:
            L.shape((cx - 70, cy - 70, cx + 70, cy), sd_arc(cx, cy, r_st, a0, a0 + 22 * fill, 6), IRON6)
        L.shape((cx - 70, cy - 70, cx + 70, cy), sd_arc(cx, cy, r_st, a0, a1, 7.6), BRASS_D, stroke=1)
    # --- oben links: Medaillon mit XP-Ring, Fertigkeiten als Scheiben ---
    slab(L, [(-6, -6), (150, -6), (150, 44), (138, 62), (-6, 62)], 0, 62, 1.0, 0.6, w=2.2)
    mx, my = 34, 33
    bb = (mx - 40, my - 40, mx + 40, my + 40)
    L.shape(bb, sd_circle(mx, my, 29), stone(my - 29, my + 29, 1.0, 0.65, ox=2))
    L.shape(bb, lambda px, py: sd_circle(mx, my, 29)(px, py) + 0.8, vg(my - 29, my + 29, BRASS_L, BRASS_D), stroke=1.8)
    L.shape(bb, sd_circle(mx, my, 24.6), IRON0)
    L.shape(bb, sd_arc(mx, my, 21.4, 0, 359.99, 5.0), IRON1)
    L.shape(bb, sd_arc(mx, my, 21.4, -90, -90 + 360 * XP, 5.0), vg(my - 24, my + 24, BRASS_L, BRASS_D))
    L.shape(bb, sd_circle(mx, my, 17.6), iron(my - 18, my + 18, 0.7, 0.45))
    tx(L, 19, str(LEVEL), mx, my + 6.5, BRASS_L, "c")
    for sx_, gname in ((88, "flame"), (124, "shield")):
        sy_ = 34
        bb2 = (sx_ - 20, sy_ - 20, sx_ + 20, sy_ + 20)
        L.shape(bb2, sd_circle(sx_, sy_, 17), stone(sy_ - 17, sy_ + 17, 0.95, 0.6, ox=int(sx_)))
        L.shape(bb2, lambda px, py, sx_=sx_, sy_=sy_: sd_circle(sx_, sy_, 17)(px, py) + 0.8, vg(sy_ - 17, sy_ + 17, BRASS_L, BRASS_D), stroke=1.6)
        L.shape(bb2, sd_circle(sx_, sy_, 12.6), iron(sy_ - 13, sy_ + 13, 0.6, 0.4))
        glyph(L, gname, sx_, sy_, 7.5, BONE, BONE3)
    # --- oben rechts, Boss ---
    slab(L, [(704, -6), (860, -6), (860, 60), (714, 60), (704, 50)], 0, 60, 1.0, 0.6, ox=3, w=2.2)
    top_right_text(L, 840, 25, 47)
    slab(L, [(292, -6), (562, -6), (562, 34), (427, 58), (292, 34)], 0, 58, 1.0, 0.6, ox=5, w=2.2)
    tx(L, 12, "GORM", 427, 18, BONE, "c")
    trough(L, 312, 25, 542, 36, BOSS, BONE, BONE3, p=5)
    diamond(L, 427, 46, 3)
    centre(L)


# ================================================================ C: ein Zellenmass

def vcell(L, x0, y0, x1, y1, label, value, vcol=BONE, size=17, active=False, frame=4.5):
    a, b, c, d = cell3(L, x0, y0, x1, y1, active, frame)
    cxm = (a + c) / 2
    if label:
        tx(L, 10, label, cxm, b + 11, BRASS_M if not active else BRASS_L, "c")
    tx(L, size, value, cxm, d - 5, vcol, "c")
    return a, b, c, d


def design_c(L):
    H = 48
    # --- oben links: Medaillon + angeschlossener Balken, darunter zwei Zellen ---
    mx, my = 30, 30
    slab(L, chamfer(4, 6, 178, 54, 3), 6, 54, 1.0, 0.65, ox=1, w=1.8)
    bb = (mx - 36, my - 36, mx + 36, my + 36)
    L.shape(bb, sd_circle(mx, my, 26), stone(my - 26, my + 26, 1.0, 0.65, ox=2))
    L.shape(bb, lambda px, py: sd_circle(mx, my, 26)(px, py) + 0.8, vg(my - 26, my + 26, BRASS_L, BRASS_D), stroke=1.8)
    L.shape(bb, sd_circle(mx, my, 20.4), iron(my - 20, my + 20, 0.75, 0.45))
    L.shape(bb, lambda px, py: sd_circle(mx, my, 20.4)(px, py) + 0.8, BRASS_M, stroke=1.2)
    tx(L, 19, str(LEVEL), mx, my + 7, BRASS_L, "c")
    tx(L, 10, "LEVEL", 66, 23, BRASS_M)
    trough(L, 66, 29, 168, 41, XP, BRASS_L, BRASS_D, back=IRON1, p=4)
    for i, g in enumerate(("flame", "shield")):
        x = 4 + i * 52
        a, b, c, d = cell3(L, x, 58, x + 48, 58 + H, False, 4.5)
        glyph(L, g, (a + c) / 2, (b + d) / 2, 9, BONE, BONE3)
    # --- oben rechts: zwei Zellen ---
    vcell(L, 736, 6, 796, 6 + H, "SILVER", str(SILVER), BONE, 16)
    vcell(L, 800, 6, 850, 6 + H, "KEYS", str(KEYS), BRASS_L, 16)
    # --- Boss: Zellenrahmen mit breitem Balken ---
    slab(L, chamfer(255, 4, 599, 54, 3), 4, 54, 1.0, 0.65, ox=5, w=1.8)
    tx(L, 12, "GORM", 427, 21, BONE, "c")
    trough(L, 272, 29, 582, 44, BOSS, BONE, BONE3, p=6)
    # --- unten links: Leben-Zelle, Balken-Zelle, Gegenstaende ---
    y0, y1 = 424, 424 + H
    a, b, c, d = vcell(L, 4, y0, 62, y1, "HP", str(HPV), BONE, 18, True)
    a, b, c, d = cell3(L, 66, y0, 238, y1, False, 4.5)
    trough(L, a + 6, b + 6, c - 6, b + 20, HP, RED5, RED2, back=RED0, p=5, hi=RED6)
    stamina_segs(L, a + 6, c - 6, b + 26, b + 32, 5, 3)
    items(L, 242, y0, y1, 48, 4, 10, 4.5)
    toast(L, 4, 394, 174, 412)
    # --- unten rechts: vier gleiche Zellen ---
    weapons(L, 626, y0, y1, 54, 4, 14, 469, 4.5, 13)
    centre(L)


DESIGNS = [("A", "leiste", design_a), ("B", "gefaess", design_b), ("C", "zellen", design_c)]
TITLES = ["A  Eine Leiste", "B  Gefäß in Fassung", "C  Ein Zellenmaß"]
NOTES = [
    ["Muster: eine durchgehende Leiste aus Weltmaterial (Quake, Diablo): Stein mit Pfeilern, Werte eingelassen,",
     "Leben groß als Hauptdarsteller. Abweichung von #213: unten links, Mitte, rechts sind ein Körper; oben bündig am Rand."],
    ["Muster: Behälter statt Strich (Diablo, Castlevania): Leben als Rosette, die sich füllt, Ausdauer als Bogen darum,",
     "niedrige Steinfassung trägt Waffen und Gegenstände. Abweichung von #213: Ecken unten sind durch die Fassung verbunden."],
    ["Muster: ein Zellenmaß (Blood, Castlevania): jeder Wert in gleich hoher, gleich gerahmter Zelle mit Beschriftung und",
     "großer Zahl, Bildmitte ganz frei. Abweichung von #213: Leben und Ausdauer ziehen von der Mitte nach unten links."],
]


def compose(scene, fn):
    L = Layer(854, 480, True)
    fn(L)
    out = scene.copy()
    m = L.a > 0
    comp = L.rgb + scene * (1 - L.a[..., None])
    yy, xx = np.nonzero(m)
    thr = (BAYER[yy % 4, xx % 4] + 0.5) / 16.0
    q = np.floor(np.clip(comp[yy, xx], 0, 1) * 31 + thr[:, None]) / 31
    out[yy, xx] = np.clip(q, 0, 1)
    return out


def sheet(nat):
    gap, top, bottom = 14, 46, 78
    W = 3 * 854 + 4 * gap; H = top + 480 + bottom + gap
    cv = np.tile(np.array((20, 16, 26), np.float32) / 255, (H, W, 1))
    for i, im in enumerate(nat):
        cv[top:top + 480, gap + i * (854 + gap):gap + i * (854 + gap) + 854] = im
    L = Layer(W, H, False)
    L.sx = L.sy = L.s = 1.0
    for i, n in enumerate(TITLES):
        x = gap + i * (854 + gap)
        L.text(F_CINZEL, 20, n, x + 427, 33, BRASS_L, "c")
        for k, line in enumerate(NOTES[i]):
            L.text(F_BELL, 15.5, line, x + 4, top + 480 + 24 + k * 22, BONE)
    return np.clip(cv * (1 - L.a[..., None]) + L.rgb, 0, 1)


def main3():
    scene = load_png(os.path.join(HERE, "basis-854x480.png"))
    nat = []
    for key, nm, fn in DESIGNS:
        im = compose(scene, fn)
        nat.append(im)
        save_png(os.path.join(OUT3, f"variante-{key}-{nm}.png"), nearest(im, FINAL_H, FINAL_W))
        save_png(os.path.join(OUT3, f"variante-{key}-{nm}-854.png"), im)
        print("ok", key, flush=True)
    save_png(os.path.join(OUT3, "blatt-runde3.png"), sheet(nat))


if __name__ == "__main__":
    main3()
