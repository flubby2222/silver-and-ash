"""Hand im Handschuh und Aermel des Jaegers fuer die Waffen in der Ich-Sicht (Silver & Ash).

Gemeinsamer Baustein der Probestuecke Pistole und Klinge. Gebaut im Kameraraum (x rechts, y oben, z vorn) relativ
zum Griffpunkt der Waffe; s = +1 rechte Hand, -1 linke Hand (Punkte gespiegelt, Flaechen neu ausgerichtet).
"""

import math

import numpy as np

from psx import V, tube, vnoise, blob, mix

GLOVE = np.array((44, 40, 44), float)       # dunkles, kuehles Leder (hebt sich vom Nussbaum ab)
SLEEVE = np.array((46, 44, 56), float)      # dunkler Mantelstoff, leicht violett-kalt
STITCH = np.array((120, 104, 82), float)
CUFF_SILVER = np.array((190, 194, 206), float)


def C(xr, yu, zf):
    """Kameraraum -> Spiel-Achsen relativ zur Kamera mit Blick nach +z (rechts = -x, ohne Spiegelmatrix)."""
    return V(-xr, yu, zf)


def paint_glove(U, V_):
    h, w = U.shape
    col = np.broadcast_to(GLOVE, (h, w, 3)).copy()
    col *= (0.8 + 0.35 * vnoise(h, w, 10, 10, 61))[..., None]
    top = 1 - np.abs(np.cos(np.pi * U))
    col *= (0.82 + 0.3 * top)[..., None]
    col = mix(col, GLOVE * 1.7, np.clip((vnoise(h, w, 6, 6, 62) - 0.62) * 3, 0, 0.6))     # abgewetzte Stellen
    seam = (np.abs(U - 0.5) < 0.012) | (np.abs(V_ - 0.8) < 0.015)
    col = mix(col, STITCH, (seam & (((V_ + U) * 40) % 1.0 < 0.5)).astype(float) * 0.8)   # Naehte
    return col


def paint_sleeve(U, V_):
    h, w = U.shape
    col = np.broadcast_to(SLEEVE, (h, w, 3)).copy()
    col *= (0.85 + 0.25 * vnoise(h, w, 30, 6, 71))[..., None]                       # Gewebe
    folds = 0.85 + 0.15 * np.cos(2 * np.pi * (V_ * 5 + 0.3 * vnoise(h, w, 4, 4, 72)))
    col *= folds[..., None]
    col = mix(col, SLEEVE * 0.55, (V_ < 0.08).astype(float))                          # Saum am Handgelenk
    for cu in (0.35, 0.65):                                                          # zwei Silberknoepfe
        col = mix(col, CUFF_SILVER, (blob(U, V_, cu, 0.16, 0.03, 0.03) > 0.3).astype(float))
    return col


GLOVE2 = np.array((74, 66, 66), float)      # Handschuh der zweiten Fassung: heller, kuehl-grau, abgewetzt
CUFF = np.array((56, 50, 52), float)        # Stulpe: dunkleres, dickeres Leder


def paint_glove2(U, V_):
    """Finger und Handruecken: u rundherum (0,5 oben = Ruecken/Knoechel), v entlang zur Spitze."""
    h, w = U.shape
    col = np.broadcast_to(GLOVE2, (h, w, 3)).copy()
    col *= (0.82 + 0.3 * vnoise(h, w, 12, 12, 63))[..., None]
    top = 1 - np.abs(np.cos(np.pi * U))
    col *= (0.75 + 0.4 * top)[..., None]                                             # oben heller: Knoechel lesen sich
    col = mix(col, GLOVE2 * 1.45, np.clip((vnoise(h, w, 5, 5, 64) - 0.6) * 3, 0, 0.6) * top)   # abgewetzt
    side_seam = (np.abs(U - 0.25) < 0.02) | (np.abs(U - 0.75) < 0.02)
    col = mix(col, STITCH * 0.8, (side_seam & (((V_ * 24) % 1.0) < 0.5)).astype(float) * 0.4)  # Seitennaehte, zurueckhaltend
    col = mix(col, GLOVE2 * 0.55, (((V_ * 3) % 1.0) < 0.06).astype(float) * 0.7)            # Gelenkfalten
    return col


def paint_cuff(U, V_):
    """Stulpe: v vom Handgelenk zum Rand."""
    h, w = U.shape
    col = np.broadcast_to(CUFF, (h, w, 3)).copy()
    col *= (0.8 + 0.35 * vnoise(h, w, 8, 16, 65))[..., None]
    top = 1 - np.abs(np.cos(np.pi * U))
    col *= (0.8 + 0.3 * top)[..., None]
    rim = V_ > 0.82
    col = mix(col, CUFF * 0.6, rim.astype(float))                                     # umgeschlagener Rand
    stitch = (np.abs(V_ - 0.78) < 0.03) & (((U * 48) % 1.0) < 0.5)
    col = mix(col, STITCH, stitch.astype(float))                                      # Naht entlang des Randes
    seam = (np.abs(U - 0.5) < 0.012) & (((V_ * 20) % 1.0) < 0.5)
    col = mix(col, STITCH, seam.astype(float) * 0.8)                                  # Naht oben laengs
    for cu in (0.4, 0.6):                                                             # zwei Silbernieten
        col = mix(col, CUFF_SILVER, (blob(U, V_, cu, 0.5, 0.025, 0.06) > 0.3).astype(float))
    return col


def build_hand(part, s, top, bottom, outer, radius, finger_ts, wrist, elbow, thumb, index=None):
    """Handschuh mit Fingern, Knoecheln, Daumen, Stulpe und Aermel (zweite Fassung).
    top, bottom: Griffachse (Kameraraum, rechte Hand); outer: Richtung des Handrueckens; radius: Abstand der
    Fingermitten von der Griffachse; finger_ts: Lage der umgreifenden Finger entlang der Achse (Meter ab top);
    wrist, elbow: Handgelenk und Ellbogen; thumb: drei Punkte; index: drei Punkte oder None (dann umgreift er)."""
    m = lambda p: C(s * p[0], p[1], p[2])                                            # noqa: E731
    top_v, bottom_v = V(*top), V(*bottom)
    a = (bottom_v - top_v).normalized()
    o = V(*outer)
    o = (o - a * o.dot(a)).normalized()
    f = a.cross(o).normalized()
    if f.dot(V(0, 0, 1)) < 0 and abs(a.z) < 0.7:                                     # vorn = Richtung des Laufs
        f = -f
    # Umgreifende Finger: Bogen um die Griffachse vom Knoechel (aussen) ueber vorn nach innen.
    knuckles = []
    for i, t in enumerate(finger_ts):
        c = top_v + a * t
        r = 0.0055 - 0.0006 * i
        pts = []
        for k, deg in enumerate((-30, 20, 75, 130, 185)):
            th = math.radians(deg)
            rad = radius * (1.25 if k == 0 else 1.0)
            pts.append(c + (o * math.cos(th) + f * math.sin(th)) * rad)
        knuckles.append(pts[0])
        shapes = [(r * 1.15, r * 1.15, r * 1.15), (r, r, r), (r * 0.95, r * 0.95, r * 0.95), (r * 0.9,) * 3,
                  (r * 0.7,) * 3]
        part.loft(tube([m(tuple(p)) for p in pts], shapes, 5, ref_up=o), "glove")
    if index:
        part.loft(tube([m(p) for p in index], [(0.006, 0.006, 0.006), (0.0055,) * 3, (0.0045,) * 3], 5, ref_up=o),
                  "glove")
        knuckles.insert(0, V(*index[0]))
    part.loft(tube([m(p) for p in thumb], [(0.0068, 0.0068, 0.0068), (0.006,) * 3, (0.0048,) * 3], 5, ref_up=o),
              "glove")
    # Handruecken: vom Handgelenk zur Knoechelreihe, flach, Knoechel als Wulst.
    kc = sum(knuckles, V(0, 0, 0)) / len(knuckles)
    span = (knuckles[0] - knuckles[-1]).length / 2 + 0.006
    wr = V(*wrist)
    part.loft(tube([m(tuple(wr)), m(tuple(wr + (kc - wr) * 0.55)), m(tuple(kc))],
                   [(0.018, 0.008, 0.009), (span * 0.95, 0.008, 0.01), (span, 0.008, 0.011)], 8, ref_up=o),
              "glove")
    # Stulpe: weitet sich vom Handgelenk zum Unterarm, mit umgeschlagenem Rand; dann der Aermel.
    el = V(*elbow)
    d = (el - wr).normalized()
    cuff = [wr - d * 0.004, wr + d * 0.02, wr + d * 0.045, wr + d * 0.05]
    part.loft(tube([m(tuple(p)) for p in cuff], [(0.019,) * 3, (0.026,) * 3, (0.033,) * 3, (0.035,) * 3], 10,
                   ref_up=o), "cuff", cap_end=False)
    part.loft(tube([m(tuple(wr + d * 0.04)), m(tuple(wr + (el - wr) * 0.5)), m(elbow)],
                   [(0.03,) * 3, (0.036,) * 3, (0.04,) * 3], 8, ref_up=o), "sleeve")


def build_arm(part, s, grip, wrist, elbow, thumb, index, fist=(0.021, 0.024, 0.02), fist_ref=V(0, 0, 1)):
    """Faust um den Griff, Daumen, Zeigefinger, Unterarm im Aermel.
    grip: Punkte (Kameraraum) entlang des Griffs, den die Faust umschliesst; wrist, elbow: Handgelenk und
    Ellbogen (Ellbogen ausserhalb des Bildes); thumb, index: je (Ansatz, Spitze)."""
    m = lambda p: C(s * p[0], p[1], p[2])                                            # noqa: E731
    w, b, t = fist
    part.loft(tube([m(p) for p in grip], [(w, b, t)] * len(grip), 8, ref_up=fist_ref), "glove")
    for base, tip in (thumb, index):
        mid = (V(*base) + V(*tip)) / 2
        part.loft(tube([m(base), m(tuple(mid)), m(tip)], [(0.0065, 0.0065, 0.0065), (0.006, 0.006, 0.006),
                                                          (0.005, 0.005, 0.005)], 6), "glove")
    # Handruecken bis zum Handgelenk, dann Stulpe und Aermel bis zum Ellbogen.
    w0 = V(*grip[len(grip) // 2])
    part.loft(tube([m(tuple(w0)), m(tuple((w0 + V(*wrist)) / 2)), m(wrist)],
                   [(0.022, 0.02, 0.02), (0.02, 0.019, 0.019), (0.019, 0.018, 0.018)], 8), "glove")
    cuff = V(*wrist) + (V(*elbow) - V(*wrist)) * 0.08
    part.loft(tube([m(wrist), m(tuple(cuff)), m(tuple(V(*wrist) + (V(*elbow) - V(*wrist)) * 0.5)), m(elbow)],
                   [(0.03, 0.03, 0.03), (0.033, 0.033, 0.033), (0.036, 0.036, 0.036), (0.04, 0.04, 0.04)], 8),
              "sleeve")
