"""Hand im Handschuh und Aermel des Jaegers fuer die Waffen in der Ich-Sicht (Silver & Ash).

Gemeinsamer Baustein der Probestuecke Pistole und Klinge. Gebaut im Kameraraum (x rechts, y oben, z vorn) relativ
zum Griffpunkt der Waffe; s = +1 rechte Hand, -1 linke Hand (Punkte gespiegelt, Flaechen neu ausgerichtet).
"""

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
