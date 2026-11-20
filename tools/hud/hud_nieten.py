"""Nieten als Relief fuer die Rahmen und Reiter des doppelt feinen Atlas (Issue #412, Teil 2; Fassung "S1 Nieten").

Reines numpy, kein Blender. Wird von tools/hud/hud_atlas_doppelt.py (Rahmen und Reiter) und tools/hud/hud_lebensbalken.py (Striche des
Lebensbalkens) geladen.

Eine Niete ist eine Halbkugel: Aus der Hoehe jedes Pixels folgt die Neigung, aus der Neigung mit Licht von oben links die Helligkeit,
und die Helligkeit wird auf die sechs Silberstufen des Rahmens gerundet (SIL = Stufen 2 bis 7 von SILVER_RAMP in hud_hochaufgeloest.py).
Der aeussere Ring bleibt die dunkelste Stufe, damit die Niete auch auf hellem Rahmen als Rund lesbar ist. Keine Glaettung, keine neue Farbe.
Die Form haengt nur vom Radius ab; es gibt keinen Zufall.
"""
import math
import numpy as np

SIL = (72, 100, 132, 166, 200, 230)   # die sechs Silberstufen des Rahmens (L0 bis L5)
LIGHT = np.array([-0.55, -0.55, 0.63], np.float64)
LIGHT = LIGHT / np.linalg.norm(LIGHT)
_cache = {}


def dome_levels(r):
    """Stufen (0 bis 5) einer Niete mit Radius r als Feld (2r+1) x (2r+1); -1 ausserhalb des Runds."""
    if r in _cache:
        return _cache[r]
    n = 2 * r + 1
    big = r + 0.5
    lv = np.full((n, n), -1, np.int8)
    for j in range(n):
        for i in range(n):
            dx, dy = i - r, j - r
            d2 = dx * dx + dy * dy
            if d2 > r * r + 1:
                continue
            if d2 > (r - 1) * (r - 1) + 1:       # Randring
                lv[j, i] = 0
                continue
            z = math.sqrt(max(0.0, 1.0 - d2 / (big * big)))
            nrm = np.array([dx / big, dy / big, z])
            lit = float(nrm @ LIGHT)             # 1 = Licht trifft senkrecht
            # Neigung zum Licht: dunkel (Schatten unten rechts) bis hell (Spitzlicht oben links)
            lv[j, i] = int(min(5, max(1, math.floor((lit - 0.05) * 6.0))))
    _cache[r] = lv
    return lv


def stamp_dome(img, cx, cy, r):
    """Setzt eine Niete in img (H x W x 4, vormultipliziert, float 0 bis 1) mit Mitte (cx, cy); deckend."""
    lv = dome_levels(r)
    n = lv.shape[0]
    for j in range(n):
        for i in range(n):
            k = int(lv[j, i])
            if k < 0:
                continue
            x, y = cx - r + i, cy - r + j
            if 0 <= y < img.shape[0] and 0 <= x < img.shape[1]:
                v = SIL[k] / 255.0
                img[y, x, 0] = img[y, x, 1] = img[y, x, 2] = v
                img[y, x, 3] = 1.0


def stamp_dome_u8(img, cx, cy, r):
    """Wie stamp_dome, aber fuer ein 8-Bit-RGBA-Bild (H x W x 4, uint8)."""
    lv = dome_levels(r)
    n = lv.shape[0]
    for j in range(n):
        for i in range(n):
            k = int(lv[j, i])
            if k < 0:
                continue
            x, y = cx - r + i, cy - r + j
            if 0 <= y < img.shape[0] and 0 <= x < img.shape[1]:
                img[y, x, :3] = SIL[k]
                img[y, x, 3] = 255


def corner_radius(w, h):
    """Radius der Eckniete: 4 px bei Rahmen ab 40 px Seite, sonst 3 px (schmale Leisten und kleine Rahmen)."""
    return 4 if min(w, h) >= 40 else 3


def rivet_corners(put, x0, y0, w, h):
    """Vier Nieten in den Ecken des Rahmens (aeusseres Rechteck x0, y0, w, h in Pixeln), Mitte je r px von den Aussenkanten."""
    r = corner_radius(w, h)
    for cx in (x0 + r, x0 + w - 1 - r):
        for cy in (y0 + r, y0 + h - 1 - r):
            put(cx, cy, r)


def rivet_edges(put, x0, y0, w, h, rim, pitch=80):
    """Nieten auf der oberen und unteren Kante, gleichmaessig verteilt, etwa alle pitch px (Reiter)."""
    n = max(0, w // pitch - 1)
    for i in range(1, n + 1):
        px = x0 + (w - 1) * i // (n + 1)
        put(px, y0 + rim // 2, 3)
        put(px, y0 + h - 1 - rim // 2, 3)
