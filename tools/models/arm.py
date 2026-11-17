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


# --- Fassung 3 (Issue #256): kein Handschuh, kein Finger - langer Aermel mit Stulpe ueber der Waffe --------------

def build_sleeve(part, s, rear, front, radius, elbow, flare=1.12, thickness=0.036):
    """Aermel eines schweren Jaegermantels, der den Griff (Pistole) bzw. Griff und Heft (Klinge) ganz umschliesst.
    rear, front: hinteres und vorderes Ende der Stulpe auf der Waffenachse (Kameraraum, rechte Seite; s = -1
    spiegelt); radius: Innenradius der Stulpe; elbow: Ellbogen (ausserhalb des Bildes). Rueckgabe: nichts.
    Teil 'cuff': Stulpe mit umgeschlagenem Rand vorn (v = 1 am Rand); Teil 'sleeve': Aermel bis zum Ellbogen."""
    m = lambda p: C(s * p[0], p[1], p[2])                                            # noqa: E731
    r, f, e = V(*rear), V(*front), V(*elbow)
    mid = (r + f) / 2
    # Stulpe von hinten (v = 0) nach vorn (v = 1): leicht weiter nach vorn, dann ein kurzer dicker Rand.
    pts = [r, r + (mid - r) * 0.6, mid + (f - mid) * 0.5, f - (f - r).normalized() * 0.003, f]
    rad = [radius * 1.02, radius * 1.0, radius * 1.05, radius * flare, radius * flare * 1.03]
    # Reihenfolge vorn -> hinten (v = 1 vorn bleibt im Bild: siehe paint_cuff3), sonst zeigen die Flaechen nach innen.
    part.loft(tube([m(tuple(p)) for p in pts[::-1]], [(x,) * 3 for x in rad[::-1]], 10, ref_up=V(1, 0, 0)), "cuff")
    # Aermel: aus der Stulpe nach hinten-aussen-unten, weitet sich zum Ellbogen; ein weicher Knick unterwegs.
    d = (e - r)
    b1 = r + d * 0.18 + (r - f).normalized() * 0.01
    b2 = r + d * 0.55
    part.loft(tube([m(tuple(r + (f - r).normalized() * 0.004)), m(tuple(b1)), m(tuple(b2)), m(tuple(e))],
                   [(radius * 1.12,) * 3, (thickness * 0.85,) * 3, (thickness,) * 3, (thickness * 1.1,) * 3], 10,
                   ref_up=V(1, 0, 0)), "sleeve")


SLEEVE3 = np.array((54, 60, 84), float)     # schwerer Mantelstoff: kaltes Nachtblau, klar blauer als Holz und Leder
CUFF3 = np.array((96, 72, 54), float)       # Stulpe: helleres, dickes Leder (Nussbraun), hebt sich vom Stoff ab


def paint_sleeve3(U, V_):
    """Mantelstoff: u rund um den Aermel, v entlang. Koeper in laengs laufenden Rippen, weiche Falten, Abnutzung."""
    h, w = U.shape
    col = np.broadcast_to(SLEEVE3, (h, w, 3)).copy()
    col *= (0.86 + 0.22 * vnoise(h, w, 30, 6, 71))[..., None]
    col *= (0.93 + 0.1 * np.cos(2 * np.pi * U * 14))[..., None]                         # Rippen (Koeper)
    folds = 0.88 + 0.12 * np.cos(2 * np.pi * (V_ * 4 + 0.35 * vnoise(h, w, 4, 4, 72)))
    col *= folds[..., None]
    col = mix(col, SLEEVE3 * 1.35, np.clip((vnoise(h, w, 5, 5, 73) - 0.68) * 3, 0, 0.5))   # abgewetzt
    return col


def paint_cuff3(U, V_):
    """Stulpe: v von hinten (0) nach vorn (1). Vorn umgeschlagener, dunklerer Rand mit Naht und drei Silbernieten."""
    h, w = U.shape
    V_ = 1 - V_                                       # die Stulpe ist von vorn nach hinten gebaut
    col = np.broadcast_to(CUFF3, (h, w, 3)).copy()
    col *= (0.8 + 0.35 * vnoise(h, w, 8, 16, 65))[..., None]
    rim = V_ > 0.8
    col = mix(col, CUFF3 * 0.62, rim.astype(float))
    stitch = (np.abs(V_ - 0.74) < 0.03) & (((U * 48) % 1.0) < 0.5)
    col = mix(col, STITCH, stitch.astype(float))
    for cu in (0.2, 0.5, 0.8):
        col = mix(col, CUFF_SILVER, (blob(U, V_, cu, 0.9, 0.03, 0.06) > 0.3).astype(float))
    return col


# --- Fassung 4 (Leitung: Stoff statt Leder, Unterarm im Bild): ein weiter Mantelaermel ohne eigene Stulpe -----------

SLEEVE4 = np.array((50, 58, 84), float)          # dunkler Mantelstoff, kaltes Nachtblau
SLEEVE4_LIGHT = np.array((92, 104, 140), float)  # Lichtbahnen der Falten (muessen bei 854 x 480 lesbar sein)


def paint_sleeve4(U, V_):
    """u rund um den Aermel, v von der Oeffnung (0) zum Ellbogen (1). Faltenbahnen laengs, Saum mit Naht an der
    Oeffnung, Abnutzung am Saum. Kontrast bewusst hoch: im Foto muessen helle und dunkle Bahnen zu sehen sein."""
    h, w = U.shape
    col = np.broadcast_to(SLEEVE4, (h, w, 3)).copy()
    warp = 0.07 * vnoise(h, w, 6, 3, 91)
    fold = 0.5 + 0.5 * np.cos(2 * np.pi * (U * 3 + warp + 0.12 * V_))          # drei breite Bahnen rund herum
    fold2 = 0.5 + 0.5 * np.cos(2 * np.pi * (U * 7 + 2 * warp))
    col = mix(col * 0.72, SLEEVE4_LIGHT, np.clip(0.75 * fold + 0.25 * fold2, 0, 1) ** 1.5 * 0.9)
    col *= (0.9 + 0.2 * vnoise(h, w, 24, 8, 92))[..., None]                       # Gewebe
    hem = V_ < 0.09
    col = mix(col, SLEEVE4 * 0.55, hem.astype(float))                                  # Saum, dunkler
    stitch = (np.abs(V_ - 0.065) < 0.012) & (((U * 56) % 1.0) < 0.55)
    col = mix(col, CUFF_SILVER * 0.8, stitch.astype(float))                            # Naht aus hellem Faden
    col = mix(col, SLEEVE4 * 1.6, (np.abs(V_ - 0.11) < 0.012).astype(float) * 0.7)     # Borte kurz ueber dem Saum
    wear = np.clip((vnoise(h, w, 5, 6, 93) - 0.62) * 3, 0, 0.55) * (V_ < 0.3)
    col = mix(col, SLEEVE4_LIGHT * 1.05, wear)
    return col


def build_sleeve4(part, s, path, radii, mouth_cap=True):
    """Mantelaermel: path[0] ist die Oeffnung (Mitte, auf der Waffenachse), danach Punkte bis zum Ellbogen;
    radii dazu. Die Oeffnung ist mit einer dunklen Scheibe geschlossen, die Waffe steckt durch sie hindurch.
    Reihenfolge der Ringe: Ellbogen zuerst, damit die Flaechen nach aussen zeigen (an Foto geprueft)."""
    m = lambda p: C(s * p[0], p[1], p[2])                                            # noqa: E731
    pts = [m(p) for p in path][::-1]
    rad = [(r,) * 3 for r in radii][::-1]
    part.loft(tube(pts, rad, 12, ref_up=V(1, 0, 0)), "sleeve", cap_start=True, cap_end=mouth_cap)
