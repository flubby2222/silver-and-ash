"""HUD-Vorlagen Runde 2 (Issue #341): drei Ausarbeitungsgrade von Blatt 1A (Kathedrale, 854x480 mitgekoernt).

Aufruf:
  "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python hud_runde2.py

Nutzt die Zeichenwerkzeuge aus hud_blaetter.py. Die Tafeln sind mit echten Spiel-Texturen gefuellt
(assets/models/chest_guard.png, door_locked.png, assets/textures/catacomb_wall.png), 1 Texel = 1 Bildpunkt des 854x480-Bildes.
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from hud_blaetter import *  # noqa
import hud_blaetter as hb

REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
OUT2 = os.path.join(HERE, "runde2")
os.makedirs(OUT2, exist_ok=True)

IRONPL = load_png(os.path.join(REPO, "assets", "models", "door_locked.png"))      # dunkle Eisenbohlen x0..38
WOOD = load_png(os.path.join(REPO, "assets", "models", "chest_guard.png"))        # Holzbohlen oben
STONE_T = load_png(os.path.join(REPO, "assets", "textures", "catacomb_wall.png"))  # Stein 64x64


def texf(img, x0, y0, w, h, ox=0.0, oy=0.0, top=1.0, bot=1.0, y0g=None, y1g=None):
    """Spiel-Textur als Farbfunktion; 1 Texel je Einheit, mit senkrechtem Helligkeitsverlauf."""
    def f(px, py):
        u = (np.floor(px - ox).astype(int) % w) + x0
        v = (np.floor(py - oy).astype(int) % h) + y0
        c = img[v, u]
        if y0g is not None:
            t = np.clip((py - y0g) / (y1g - y0g), 0, 1)[..., None]
            c = c * (top * (1 - t) + bot * t)
        else:
            c = c * top
        return np.clip(c, 0, 1)
    return f


def iron(y0, y1, top=1.3, bot=0.8):
    return texf(IRONPL, 0, 0, 39, 128, 0, 0, top, bot, y0, y1)


def stone(y0, y1, top=0.62, bot=0.38, ox=0):
    return texf(STONE_T, 0, 0, 64, 64, ox, 0, top, bot, y0, y1)


def wood(y0, y1, top=0.85, bot=0.5):
    return texf(WOOD, 0, 0, 128, 60, 0, 0, top, bot, y0, y1)


def bbox(poly, m=3):
    return (min(p[0] for p in poly) - m, min(p[1] for p in poly) - m, max(p[0] for p in poly) + m, max(p[1] for p in poly) + m)


def gable(x0, y0, x1, y1, rise=0.28, c=3):
    cx = (x0 + x1) / 2; sh = y0 + (y1 - y0) * rise
    return [(x0, y1 - c), (x0, sh), (cx, y0), (x1, sh), (x1, y1 - c), (x1 - c, y1), (x0 + c, y1)]


def crowned(x0, y0, x1, y1, ch, cw, c):
    """Tafel mit erhoehtem Mittelstueck (Giebel) ueber der Breite 2*cw."""
    cx = (x0 + x1) / 2
    return [(x0 + c, y0 + ch), (cx - cw - ch, y0 + ch), (cx - cw, y0), (cx + cw, y0), (cx + cw + ch, y0 + ch), (x1 - c, y0 + ch),
            (x1, y0 + ch + c), (x1, y1 - c), (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + ch + c)]


def body(L, poly, texture, w=2.0, shine=True, inner=True, lo=BRASS_D, hi=BRASS_L, mid=None):
    """Gefuellte, texturierte Tafel mit abgesetztem Messingrahmen (oben hell, unten dunkel) und dunkler Innenkante."""
    f = sd_poly(poly); bb = bbox(poly)
    L.shape(bb, f, texture)
    if inner:
        L.shape(bb, lambda px, py: f(px, py) + w + 0.5, IRON0, stroke=1.0, alpha=0.9)
    L.shape(bb, lambda px, py: f(px, py) + w + 1.5, vg(bb[1], bb[3], IRON5, IRON1), stroke=1.0, alpha=0.55)
    L.shape(bb, lambda px, py: f(px, py) + w / 2, vg(bb[1], bb[3], hi, lo), stroke=w)
    L.shape(bb, lambda px, py: f(px, py) + 0.5, IRON0, stroke=1.0, alpha=0.9)
    return f


def rivets(L, pts, c=BRASS_L):
    for (x, y) in pts:
        L.shape((x - 3, y - 3, x + 3, y + 3), sd_circle(x, y, 1.5), c)
        L.shape((x - 3, y - 3, x + 3, y + 3), sd_circle(x + 0.6, y + 0.6, 0.7), BRASS_D, alpha=0.9)


def corner_rivets(L, x0, y0, x1, y1, d=5):
    rivets(L, [(x0 + d, y0 + d), (x1 - d, y0 + d), (x0 + d, y1 - d), (x1 - d, y1 - d)])


def diamond(L, x, y, r, c=BRASS_L):
    L.shape((x - r - 2, y - r - 2, x + r + 2, y + r + 2), sd_poly([(x, y - r), (x + r, y), (x, y + r), (x - r, y)]), vg(y - r, y + r, c, BRASS_M))
    L.shape((x - r - 2, y - r - 2, x + r + 2, y + r + 2), sd_poly([(x, y - r), (x + r, y), (x, y + r), (x - r, y)]), IRON0, stroke=1, alpha=0.8)


def quatrefoil(L, x, y, r, c=BRASS_L):
    for dx, dy in ((r * .55, 0), (-r * .55, 0), (0, r * .55), (0, -r * .55)):
        L.shape((x - r - 2, y - r - 2, x + r + 2, y + r + 2), sd_circle(x + dx, y + dy, r * .5), c)
    L.shape((x - 2, y - 2, x + 2, y + 2), sd_circle(x, y, r * .28), BRASS_D)


def spire(L, x, y, h, wd=4.5):
    """Kleine Fiale (Spitze) die von Kante y nach oben ragt."""
    pts = [(x - wd, y), (x - wd * .6, y - h * .55), (x, y - h), (x + wd * .6, y - h * .55), (x + wd, y)]
    f = sd_poly(pts)
    bb = bbox(pts, 2)
    L.shape(bb, f, vg(y - h, y, BRASS_L, BRASS_M))
    L.shape(bb, f, IRON0, stroke=1, alpha=0.8)


def design_r2(L, V):
    F = F_CINZEL
    sh = (0.8, 0.8, FOG)
    gothic = V >= 2
    rich = V >= 3
    p_ = 7 if V == 2 else 9

    def panel_poly(x0, y0, x1, y1, small=False):
        if not gothic:
            return chamfer(x0, y0, x1, y1, 4)
        return pointed(x0, y0, x1, y1, 5 if small else p_)

    def panel(x0, y0, x1, y1, small=False, tex=None, poly=None, riv=True):
        poly = poly or panel_poly(x0, y0, x1, y1, small)
        texture = tex or (wood(y0, y1, 1.15, 0.6) if rich else iron(y0, y1, 1.35, 0.8))
        if rich:  # doppelter Rahmen
            f0 = sd_poly(poly); bb = bbox(poly, 4)
            L.shape(bb, lambda px, py: f0(px, py) - 2.2, BRASS_D, stroke=1.0, alpha=0.9)
        body(L, poly, texture)
        if riv and not gothic:
            corner_rivets(L, x0, y0, x1, y1, 6)
        if rich and poly is not None and len(poly) == 6:
            for xx in (x0, x1):
                diamond(L, xx, (y0 + y1) / 2, 2.6)
        return poly

    def cell(x0, y0, x1, y1, active=False, tex=None, flat=False):
        poly = chamfer(x0, y0, x1, y1, 3) if (V == 1 or flat) else gable(x0, y0, x1, y1, 0.22 if (x1 - x0) > 40 else 0.28, 3)
        if tex is None and rich:
            tex = iron(y0, y1, 1.0 if active else 0.8, 0.55 if active else 0.4)
        if tex is None:
            tex = stone(y0, y1, 0.68 if active else 0.55, 0.42 if active else 0.3, ox=int(x0))
        f = sd_poly(poly); bb = bbox(poly)
        L.shape(bb, f, tex)
        if rich:  # Nische: Schatten oben, Lichtkante unten
            L.shape(bb, f, (10, 7, 14), alpha=0.55, clipf=lambda px, py: np.maximum(py - (y0 + (y1 - y0) * 0.45), 0 * px - 1))
        L.shape(bb, lambda px, py: f(px, py) + 1.4, IRON0, stroke=1.0, alpha=0.9)
        L.shape(bb, lambda px, py: f(px, py) + 0.8, vg(y0, y1, BRASS_L if active else BRASS_M, BRASS_D), stroke=1.6)
        if rich and active:
            L.shape(bb, lambda px, py: f(px, py) + 3.2, BRASS_L, stroke=1.0, alpha=0.6)
        return poly

    # ---------------------------------------------------------------- oben links
    panel(6, 6, 166, 46)
    cell(13, 10, 40, 42)
    L.text(F, 17, str(LEVEL), 26.5, 35, BRASS_L, "c", shadow=sh)
    L.text(F, 10, "LV", 47, 21, BONE, shadow=sh)
    bar = sd_box(47, 27, 142, 34)
    L.shape((47, 27, 142, 34), bar, IRON0, alpha=0.95)
    L.shape((47, 27, 142, 34), bar, vg(27, 34, BRASS_L, BRASS_D), clipf=clipx(47 + 95 * XP))
    L.shape((47, 27, 142, 34), bar, BRASS_M, stroke=1)
    glyph(L, "star", 154, 30, 6.5, BRASS_L, BRASS)
    panel(6, 50, 80, 92, small=True)
    for i, g in enumerate(("flame", "shield")):
        x = 12 + i * 34
        cell(x, 56, x + 28, 86)
        glyph(L, g, x + 14, 72, 8.5, BONE, BONE3)
    if rich:
        for xx in (30, 150):
            spire(L, xx, 6, 6)
        spire(L, 43, 50, 5)

    # ---------------------------------------------------------------- oben rechts
    panel(736, 6, 850, 52, small=True)
    for row, (g, val) in enumerate((("coin", SILVER), ("key", KEYS))):
        y = 27 + row * 18
        w = L.text(F, 14, str(val), 836, y, BONE, "r", shadow=sh)
        if g == "coin":
            glyph(L, "coin", 836 - w - 12, y - 5, 6.2, IRON7, IRON4, detail=IRON3)
        else:
            glyph(L, "key", 836 - w - 16, y - 5, 7.5, BRASS_L, BRASS_M)
    if rich:
        spire(L, 793, 6, 6)

    # ---------------------------------------------------------------- oben Mitte: Boss
    if V == 1:
        panel(255, 6, 599, 42)
        by0, by1, ny = 24, 35, 18
    else:
        panel(255, 3, 599, 48, poly=crowned(255, 3, 599, 48, 15, 58, 6), riv=False)
        by0, by1, ny = 32, 43, 17.5
    L.text(F, 12, "GORM", 427, ny, BONE, "c", shadow=sh)
    if gothic:
        for sgn in (-1, 1):
            diamond(L, 427 + sgn * 44, ny - 4, 2.6)
    if rich:
        # Zierkopf: Spitze ueber dem Namen und Kugeln an den Balkenenden
        for sgn in (-1, 1):
            xx = 255 + 8 if sgn < 0 else 599 - 8
            L.shape((xx - 6, 32, xx + 6, 44), sd_circle(xx, 37.5, 4.2), vg(33, 42, BRASS_L, BRASS_D))
            L.shape((xx - 6, 32, xx + 6, 44), sd_circle(xx, 37.5, 4.2), IRON0, stroke=1, alpha=0.8)
    bx0, bx1 = (267, 587) if not rich else (274, 580)
    f = sd_poly(pointed(bx0, by0, bx1, by1, 5))
    L.shape((bx0 - 3, by0 - 3, bx1 + 3, by1 + 3), f, IRON0, alpha=0.95)
    L.shape((bx0 - 3, by0 - 3, bx1 + 3, by1 + 3), f, vg(by0, by1, BONE, BONE3), clipf=clipx(bx0 + (bx1 - bx0) * BOSS))
    L.shape((bx0 - 3, by0 - 3, bx1 + 3, by1 + 3), f, vg(by0, by1, BRASS_L, BRASS_D), stroke=1.6)

    # ---------------------------------------------------------------- Bildmitte (frei)
    L.shape((420, 233, 434, 247), sd_circle(427, 240, 2.2), (16, 11, 20), alpha=0.85)
    L.shape((420, 233, 434, 247), sd_circle(427, 240, 1.4), BONE)
    capx = 427 - (15 + 5 + textw(L, F, 12, "to open")) / 2
    cell(capx, 255, capx + 15, 271, active=True, flat=True)
    L.text(F, 11, "F", capx + 7.5, 267, BRASS_L, "c")
    L.text(F, 12, "to open", capx + 20, 267, BONE, shadow=sh)

    # ---------------------------------------------------------------- unten Mitte: Leben und Ausdauer in einer Fassung
    panel(298, 426, 556, 474, poly=pointed(298, 426, 556, 474, 10) if gothic else chamfer(298, 426, 556, 474, 5), riv=False)
    if not gothic:
        corner_rivets(L, 298, 426, 556, 474, 7)
    f = sd_poly(pointed(314, 433, 540, 448, 7))
    L.shape((312, 431, 542, 450), f, RED0, alpha=0.97)
    fx = 314 + 226 * HP
    L.shape((312, 431, 542, 450), f, vg(433, 448, RED5, RED2), clipf=clipx(fx))
    L.shape((312, 431, 542, 450), f, RED6, alpha=0.8, clipf=lambda px, py: np.maximum(px - fx, py - 438))
    L.shape((fx - 2, 432, fx + 2, 449), sd_seg((fx, 435), (fx, 446), 1), RED6, clipf=f)
    L.shape((312, 431, 542, 450), f, vg(433, 448, BRASS_L, BRASS_D), stroke=1.6)
    segs = 5; sw = 41; gap = 4; x0 = 427 - (segs * sw + (segs - 1) * gap) / 2
    for i in range(segs):
        sx0 = x0 + i * (sw + gap)
        b = sd_box(sx0, 453, sx0 + sw, 459, 1)
        L.shape((sx0 - 2, 451, sx0 + sw + 2, 461), b, IRON0, alpha=0.97)
        fill = np.clip(STAM * segs - i, 0, 1)
        if fill > 0:
            L.shape((sx0 - 2, 451, sx0 + sw + 2, 461), b, vg(453, 459, IRON7, IRON4), clipf=clipx(sx0 + sw * fill))
        L.shape((sx0 - 2, 451, sx0 + sw + 2, 461), b, BRASS_D, stroke=1)
    if gothic:
        L.shape((312, 462, 542, 466), sd_seg((322, 466), (532, 466), 1), BRASS_D, alpha=0.9)
    if rich:
        spire(L, 427, 426, 7, 5.5)
        for xx in (314, 540):
            quatrefoil(L, xx, 467, 3.4)

    # ---------------------------------------------------------------- unten links
    panel(6, 436, 80, 478, small=True)
    for i, g in enumerate(("flask", "eye")):
        x = 12 + i * 34
        cell(x, 442, x + 28, 472)
        glyph(L, g, x + 14, 457, 8, BONE if g == "flask" else BRASS_L, BONE3 if g == "flask" else BRASS, detail=(40, 20, 10))
    panel(6, 414, 172, 432, small=True, riv=False)
    glyph(L, "eye", 17, 423, 4.4, ROYAL, BRASS_M, detail=(40, 20, 10))
    L.text(F, 11, "Picked up Hunter's Eye", 26, 427.5, ROYAL, shadow=sh)

    # ---------------------------------------------------------------- unten rechts
    panel(618, 420, 850, 474, small=False)
    for i, (g, am) in enumerate(zip(("blade", "pistol", "rifle", "blunder"), AMMO)):
        x = 630 + i * 53
        act = i == ACTIVE
        cell(x, 426, x + 49, 468, active=act)
        glyph(L, g, x + 24.5, 440, 18, BONE if act else BONE2, BONE3 if act else IRON6)
        if am is not None:
            L.text(F, 12, str(am), x + 24.5, 463, BRASS_L if act else BONE, "c", shadow=sh)
        if act:
            veil(L, x + 2, 428, x + 47, 449, RELOAD)
    if rich:
        spire(L, 734, 420, 6)


def compose_native(scene, V):
    L = Layer(854, 480, True)
    design_r2(L, V)
    out = scene.copy()
    m = L.a > 0
    comp = L.rgb + scene * (1 - L.a[..., None])
    yy, xx = np.nonzero(m)
    thr = (BAYER[yy % 4, xx % 4] + 0.5) / 16.0
    q = np.floor(np.clip(comp[yy, xx], 0, 1) * 31 + thr[:, None]) / 31
    out[yy, xx] = np.clip(q, 0, 1)
    return out


def sheet(nat):
    gap, top = 14, 44
    W = 3 * 854 + 4 * gap; H = top + 480 + 2 * gap
    cv = np.tile(np.array((20, 16, 26), np.float32) / 255, (H, W, 1))
    for i, im in enumerate(nat):
        cv[top + gap:top + gap + 480, gap + i * (854 + gap):gap + i * (854 + gap) + 854] = im
    L = Layer(W, H, False)
    L.sx = L.sy = L.s = 1.0
    names = ["V1  Tafeln", "V2  Gotik", "V3  Reliquiar"]
    for i, n in enumerate(names):
        L.text(F_CINZEL, 20, n, gap + i * (854 + gap) + 427, 32, BRASS_L, "c")
    return np.clip(cv * (1 - L.a[..., None]) + L.rgb, 0, 1)


def main2():
    scene = load_png(os.path.join(HERE, "basis-854x480.png"))
    nat = []
    for V, nm in ((1, "tafeln"), (2, "gotik"), (3, "reliquiar")):
        im = compose_native(scene, V)
        nat.append(im)
        save_png(os.path.join(OUT2, f"variante-{V}-{nm}.png"), nearest(im, FINAL_H, FINAL_W))
        save_png(os.path.join(OUT2, f"variante-{V}-{nm}-854.png"), im)
        print("ok", V, flush=True)
    save_png(os.path.join(OUT2, "blatt-runde2.png"), sheet(nat))


if __name__ == "__main__":
    main2()
