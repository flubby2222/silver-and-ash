"""Hand im Handschuh und Aermel des Jaegers fuer die Waffen in der Ich-Sicht (Silver & Ash).

Gemeinsamer Baustein der Probestuecke Pistole und Klinge. Gebaut im Kameraraum (x rechts, y oben, z vorn) relativ
zum Griffpunkt der Waffe; s = +1 rechte Hand, -1 linke Hand (Punkte gespiegelt, Flaechen neu ausgerichtet).
"""

import math

import numpy as np

from psx import V, tube, vnoise, blob, mix, rects_from_pixels

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


def build_sleeve4(part, s, path, radii, mouth_cap=True, lining=False):
    """Mantelaermel: path[0] ist die Oeffnung (Mitte, auf der Waffenachse), danach Punkte bis zum Ellbogen;
    radii dazu. Die Oeffnung ist mit einer dunklen Scheibe geschlossen, die Waffe steckt durch sie hindurch.
    Reihenfolge der Ringe: Ellbogen zuerst, damit die Flaechen nach aussen zeigen (an Foto geprueft)."""
    m = lambda p: C(s * p[0], p[1], p[2])                                            # noqa: E731
    pts = [m(p) for p in path][::-1]
    rad = [(r,) * 3 for r in radii][::-1]
    part.loft(tube(pts, rad, 12, ref_up=V(1, 0, 0)), "sleeve", cap_start=True, cap_end=mouth_cap)
    if lining:
        # Futter (Runde 3): dieselbe Roehre etwas enger mit Flaechen nach innen. Schneidet die Nahebene den Aermel
        # an, sieht man Stoff statt des Griffs dahinter.
        inner = [(r * 0.93,) * 3 for r in radii]
        part.loft(tube([m(p) for p in path], inner, 12, ref_up=V(1, 0, 0)), "sleeve", cap_start=False, cap_end=False)



# --- Aermel 2: drei Entwuerfe (Issue #265) -------------------------------------------------------------------------
# a: enger schwarzer Lederaermel, Armschiene (Stahl) am Handgelenk, Lederhandschuh mit Fingern
# b: Mantelaermel aus grauer Wolle, breite umgeschlagene Stulpe in Ocker mit Silberknoepfen, ohne Hand
# c: dunkelblauer Aermel mit heller Ruschenmanschette (Spitze), ohne Hand
import os

DESIGN = os.environ.get("AERMEL", "e")
CUFF_V = 0.17        # Anteil von v (vom Mund aus), der Stulpe/Ruesche ist; die Pistole setzt 0,29 (Ringflaeche zusaetzlich)
STEEL_B = np.array((96, 100, 114), float)
LEATHER_B = np.array((38, 36, 42), float)
WOOL_B = np.array((54, 56, 66), float)
OCHRE = np.array((132, 100, 64), float)
NAVY_C = np.array((34, 38, 56), float)
LACE = np.array((214, 206, 194), float)


def paint_a_cuff(U, V_):
    """Armschiene: Stahlblech mit Laengsrippen, Nieten an beiden Enden, dunkler Rand."""
    h, w = U.shape
    col = np.broadcast_to(STEEL_B, (h, w, 3)).copy()
    col *= (0.82 + 0.3 * vnoise(h, w, 6, 10, 501))[..., None]
    rib = (((U * 6) % 1.0) < 0.12)
    col = mix(col, STEEL_B * 0.5, rib.astype(float) * 0.8)
    col = mix(col, STEEL_B * 1.35 + 14, ((((U * 6) % 1.0) > 0.14) & (((U * 6) % 1.0) < 0.26)).astype(float) * 0.6)
    for cu in (0.1, 0.35, 0.6, 0.85):
        for cv in (0.15, 0.85):
            col = mix(col, STEEL_B * 1.8 + 30, (blob(U, V_, cu, cv, 0.04, 0.07) > 0.3).astype(float))
    col = mix(col, STEEL_B * 0.35, ((V_ < 0.06) | (V_ > 0.94)).astype(float) * 0.85)
    return col


def paint_a_sleeve(U, V_):
    """Schwarzes Leder: Laengsnaht, helle Faltenbahnen, Abnutzung an den Kanten."""
    h, w = U.shape
    col = np.broadcast_to(LEATHER_B, (h, w, 3)).copy()
    col *= (0.85 + 0.25 * vnoise(h, w, 20, 6, 511))[..., None]
    fold = 0.5 + 0.5 * np.cos(2 * np.pi * (U * 4 + 0.08 * vnoise(h, w, 6, 3, 512)))
    col = mix(col, LEATHER_B * 2.3 + 10, fold ** 2.5 * 0.7)
    col = mix(col, np.array((150.0, 120, 90)), (((U * 40) % 1.0 < 0.5) & (np.abs(U - 0.5) < 0.012)).astype(float) * 0.7)
    col = mix(col, LEATHER_B * 2.6 + 12, np.clip((vnoise(h, w, 6, 5, 513) - 0.66) * 3, 0, 0.6))
    return col


def paint_b_sleeve(U, V_):
    """Grauer Wollmantelaermel; die erste Strecke (v < 0,17) ist die umgeschlagene Stulpe in Ocker mit Naht und Knoepfen."""
    V_ = 1 - V_                                                  # die Roehre ist vom Ellbogen zur Oeffnung gebaut
    h, w = U.shape
    col = np.broadcast_to(WOOL_B, (h, w, 3)).copy()
    col *= (0.88 + 0.2 * vnoise(h, w, 24, 8, 521))[..., None]
    fold = 0.5 + 0.5 * np.cos(2 * np.pi * (U * 3 + 0.1 * vnoise(h, w, 6, 3, 522) + 0.1 * V_))
    col = mix(col * 0.75, WOOL_B * 1.9 + 10, fold ** 1.6 * 0.85)
    cuff = V_ < CUFF_V
    c = np.broadcast_to(OCHRE, (h, w, 3)).copy() * (0.85 + 0.3 * vnoise(h, w, 8, 16, 523))[..., None]
    c = mix(c, OCHRE * 0.6, (np.abs(V_ - CUFF_V * 0.9) < 0.012).astype(float))                    # Kante zur Wolle
    c = mix(c, np.array((220.0, 214, 196)), ((np.abs(V_ - CUFF_V * 0.8) < 0.01) & (((U * 56) % 1.0) < 0.5)).astype(float))  # Naht
    for cu in (0.17, 0.5, 0.83):
        c = mix(c, np.array((214.0, 218, 230)), (blob(U, V_, cu, CUFF_V * 0.45, 0.05, 0.03) > 0.3).astype(float))    # Silberknoepfe
        c = mix(c, DARK_B, ((blob(U, V_, cu, CUFF_V * 0.45, 0.07, 0.04) > 0.1) & (blob(U, V_, cu, CUFF_V * 0.45, 0.05, 0.03) <= 0.3)).astype(float) * 0.7)
    return mix(col, c, cuff.astype(float))


def paint_c_sleeve(U, V_):
    """Dunkelblauer Aermel; v < 0,03 dunkel (Innenseite), v bis 0,17 helle Ruschenmanschette mit Spitzenmuster."""
    V_ = 1 - V_                                                  # die Roehre ist vom Ellbogen zur Oeffnung gebaut
    h, w = U.shape
    col = np.broadcast_to(NAVY_C, (h, w, 3)).copy()
    col *= (0.88 + 0.2 * vnoise(h, w, 24, 8, 531))[..., None]
    fold = 0.5 + 0.5 * np.cos(2 * np.pi * (U * 3 + 0.1 * vnoise(h, w, 6, 3, 532) + 0.1 * V_))
    col = mix(col * 0.75, NAVY_C * 2.4 + 12, fold ** 1.6 * 0.8)
    lace = np.broadcast_to(LACE, (h, w, 3)).copy() * (0.9 + 0.15 * vnoise(h, w, 8, 16, 533))[..., None]
    holes = ((((U * 24) % 1.0) - 0.5) ** 2 + (((V_ * 40 * 0.17 / CUFF_V) % 1.0) - 0.5) ** 2) < 0.06
    lace = mix(lace, NAVY_C * 1.4, holes.astype(float) * 0.8)                              # Spitzenloecher: dunkel durch
    lace = mix(lace, LACE * 0.6, (np.abs(U * 12 - np.floor(U * 12) - 0.5) > 0.42).astype(float) * 0.4)   # Falten der Ruesche
    lace = mix(lace, LACE * 0.55, (np.abs(V_ - CUFF_V * 0.94) < 0.012).astype(float))
    return mix(col, lace, (V_ < CUFF_V).astype(float))


DARK_B = np.array((18, 14, 14), float)


# --- Entwurf d (nach dem Video, Issue #265): Handschuh in grossen Formen, Stulpe mit Riemen, Aermel mit langen Faltenbahnen
GLOVE_D = np.array((66, 68, 80), float)
STRAP_D = np.array((138, 108, 70), float)
SLEEVE_D = np.array((32, 48, 96), float)
CUFF_D = np.array((44, 46, 58), float)


def paint_d_glove(U, V_):
    """Handschuh: dunkles Leder, Knoechel und Fingerruecken hell abgesetzt, damit die Faust als Form steht."""
    h, w = U.shape
    col = np.broadcast_to(GLOVE_D, (h, w, 3)).copy()
    col *= (0.85 + 0.25 * vnoise(h, w, 10, 10, 601))[..., None]
    top = 1 - np.abs(np.cos(np.pi * U))
    col = mix(col, GLOVE_D * 1.9 + 14, np.clip(top, 0, 1) ** 2 * 0.65)
    col = mix(col, GLOVE_D * 0.45, (((V_ * 3) % 1.0) < 0.07).astype(float) * 0.8)          # Gelenkfalten
    return col


def paint_d_cuff(U, V_):
    """Stulpe: dunkles dickes Leder mit zwei hellen Riemen (Schnallen) quer, Silberniete."""
    h, w = U.shape
    col = np.broadcast_to(CUFF_D, (h, w, 3)).copy()
    col *= (0.85 + 0.25 * vnoise(h, w, 8, 16, 611))[..., None]
    for v0 in (0.3, 0.68):
        strap = np.abs(V_ - v0) < 0.11
        col = mix(col, STRAP_D * (0.9 + 0.2 * vnoise(h, w, 4, 12, 612))[..., None], strap.astype(float))
        col = mix(col, STRAP_D * 0.5, ((np.abs(V_ - v0) > 0.09) & strap).astype(float) * 0.8)
        col = mix(col, np.array((214.0, 214, 224)), (blob(U, V_, 0.5, v0, 0.04, 0.06) > 0.3).astype(float))   # Schnalle
    return col


def paint_d_sleeve(U, V_):
    """Mantelaermel: dunkles Nachtblau mit drei langen hellen Faltenbahnen entlang der Achse, Naht, Abnutzung."""
    h, w = U.shape
    col = np.broadcast_to(SLEEVE_D, (h, w, 3)).copy()
    col *= (0.88 + 0.2 * vnoise(h, w, 24, 8, 621))[..., None]
    fold = 0.5 + 0.5 * np.cos(2 * np.pi * (U * 3 + 0.08 * vnoise(h, w, 6, 3, 622) + 0.1 * V_))
    col = mix(col * 0.7, SLEEVE_D * 2.6 + 12, fold ** 2.0 * 0.85)
    col = mix(col, np.array((190.0, 190, 204)), ((np.abs(U - 0.5) < 0.008) & (((V_ * 40) % 1.0) < 0.5)).astype(float) * 0.5)
    col = mix(col, SLEEVE_D * 2.4 + 12, np.clip((vnoise(h, w, 5, 6, 623) - 0.64) * 3, 0, 0.55))
    return col


def design_painters(design):
    if design == "d":
        return {"glove": paint_d_glove, "cuff": paint_d_cuff, "sleeve": paint_d_sleeve}
    if design == "a":
        return {"glove": paint_glove2, "cuff": paint_a_cuff, "sleeve": paint_a_sleeve}
    f = paint_b_sleeve if design == "b" else paint_c_sleeve
    return {"sleeve": f, "glove": f, "cuff": f}                      # glove/cuff sind hier ungenutzt


def build_sleeve5(part, s, path, radii, frill=False, cuff_v=0.17):
    """Wie build_sleeve4, aber die ersten beiden Ringe (Oeffnung) koennen zackig sein (Ruesche)."""
    m = lambda p: C(s * p[0], p[1], p[2])                                            # noqa: E731
    pts = [m(p) for p in path][::-1]
    rad = [(r,) * 3 for r in radii][::-1]
    n = len(pts)
    global CUFF_V
    CUFF_V = cuff_v
    mods = (lambda i, k: (1.0 if k % 2 == 0 else 0.8) if i >= n - 3 else 1.0) if frill else None
    part.loft(tube(pts, rad, 12, ref_up=V(1, 0, 0), mods=mods), "sleeve", cap_start=True, cap_end=True)


def build_design(part, weapon):
    if DESIGN == "e":
        return build_design_e(part, weapon)
    """Baut den Arm des Entwurfs DESIGN fuer 'pistol' (rechts, s = +1) oder 'blade' (links, s = -1)."""
    if DESIGN in ("a", "d"):
        elb_p, elb_b = ((0.10, -0.17, -0.26), (0.11, -0.16, -0.27)) if DESIGN == "a" else ((0.20, -0.13, -0.15), (0.17, -0.12, -0.17))
        if weapon == "pistol":
            build_hand(part, +1, top=(0, -0.006, 0.006), bottom=(0, -0.052, -0.015), outer=(1, 0, 0), radius=0.0185,
                       finger_ts=(0.014, 0.026, 0.037), wrist=(0.016, -0.046, -0.034), elbow=elb_p,
                       thumb=((0.004, -0.014, -0.010), (-0.011, -0.004, 0.0), (-0.0125, 0.004, 0.016)),
                       index=((0.017, -0.006, 0.002), (0.012, -0.011, 0.016), (0.003, -0.015, 0.018)))
        else:
            build_hand(part, -1, top=(0, 0, -0.008), bottom=(0, 0, -0.058), outer=(0.8, 0.6, 0), radius=0.0165,
                       finger_ts=(0.006, 0.017, 0.028, 0.039), wrist=(0.012, -0.004, -0.082), elbow=elb_b,
                       thumb=((-0.002, 0.016, -0.05), (-0.012, 0.013, -0.03), (-0.011, 0.009, -0.012)))
        return
    if weapon == "pistol":
        # Von oben gesehen: erst eine kleine Innenkante um den Griff, dann sofort der volle Radius (Ringflaeche = Stulpenrand
        # sichtbar), dann die Wand der Stulpe, dann der Aermel.
        path = [(0, -0.0100, 0.0030), (0, -0.0104, 0.0029), (0, -0.021, -0.001), (0.0, -0.030, -0.007),
                (0.03, -0.036, -0.016), (0.08, -0.040, -0.030), (0.16, -0.048, -0.052), (0.30, -0.065, -0.090)]
        r = [0.0165, 0.036, 0.036, 0.029, 0.029, 0.031, 0.036, 0.044]
        build_sleeve5(part, +1, path, r, frill=(DESIGN == "c"), cuff_v=0.29)
    else:
        path = [(0, 0, -0.003), (0, 0, -0.016), (0.0, -0.004, -0.04), (0.035, -0.02, -0.07), (0.09, -0.06, -0.09),
                (0.18, -0.11, -0.12), (0.30, -0.17, -0.16)]
        r = [0.040, 0.031, 0.029, 0.031, 0.036, 0.040, 0.046]
        build_sleeve5(part, -1, path, r, frill=(DESIGN == "c"))



# --- Entwurf e (Issue #265, zweite Fassung nach dem Video): geschlossene Faust, Stulpe, Mantelaermel ---------------
# Arm und Waffe haben getrennte Atlanten: pistol_hand.glb / blade_hand.glb tragen ihre eigene 64 x 64-Textur mit eigener
# Palette (32 Farben), so ziehen Holz und Silber der Waffe die Arm-Farben nicht mehr zusammen.
ARM_RECTS = {"glove": (0, 0, 32, 32), "cuff": (32, 0, 64, 32), "sleeve": (0, 32, 64, 64)}
UV_ARM = rects_from_pixels(ARM_RECTS, 64)

LEATHER_E = np.array((34, 25, 22), float)       # Fassung f: Faust noch dunkler (Schaft 84, 56, 38)
CUFF_E = np.array((160, 125, 90), float)        # Stulpe deutlich heller: muss als Band lesen
CLOTH_E = np.array((40, 44, 56), float)         # Mantelstoff: sehr dunkles Blaugrau
THREAD_E = np.array((214, 190, 146), float)


def paint_e_glove(U, V_):
    """Faust: u rund um die Faust (0,5 = Handruecken, aussen), v von den Fingerspitzen (0) zum Handgelenk (1).
    Zwei Flaechen mit Kante: Handruecken (|u - 0,5| < 0,2, glatt, etwas heller) und Fingerruecken (Seiten, drei Fugen);
    die Kante dazwischen ist eine dunkle Linie mit hellem Saum. Knoechelreihe als heller Streifen oben."""
    h, w = U.shape
    col = np.broadcast_to(LEATHER_E, (h, w, 3)).copy()
    col *= (0.85 + 0.25 * vnoise(h, w, 6, 6, 701))[..., None]
    back = np.abs(U - 0.5) < 0.2
    col = mix(col, LEATHER_E * 1.9 + 8, back.astype(float) * 0.6)                                      # Handruecken heller
    for v0 in (0.14, 0.28, 0.42):
        col = mix(col, LEATHER_E * 0.35, ((np.abs(V_ - v0) < 0.012) & ~back).astype(float) * 0.9)   # Fugen nur auf den Fingern
    edge = np.abs(np.abs(U - 0.5) - 0.2) < 0.018
    col = mix(col, LEATHER_E * 0.3, edge.astype(float) * 0.95)                                         # Kante zwischen den Flaechen
    col = mix(col, LEATHER_E * 3.0 + 16, ((np.abs(np.abs(U - 0.5) - 0.235) < 0.012)).astype(float) * 0.5)  # heller Saum
    col = mix(col, LEATHER_E * 3.0 + 16, ((np.abs(V_ - 0.05) < 0.05) & (np.abs(U - 0.5) < 0.32)).astype(float) * 0.5)  # Knoechel
    col = mix(col, LEATHER_E * 0.7, (V_ > 0.7).astype(float))                                          # Handgelenk dunkler
    col = mix(col, LEATHER_E * 2.4 + 10, np.clip((vnoise(h, w, 5, 5, 702) - 0.68) * 3, 0, 0.45))
    return col


def paint_e_cuff(U, V_):
    """Stulpe: helles Leder, deutliche Naht (heller Faden) und ein dunkler Riemen mit Silbernieten; v 0 am Handgelenk."""
    h, w = U.shape
    col = np.broadcast_to(CUFF_E, (h, w, 3)).copy()
    col *= (0.88 + 0.22 * vnoise(h, w, 8, 8, 711))[..., None]
    col = mix(col, CUFF_E * 0.42, ((V_ > 0.42) & (V_ < 0.72)).astype(float))                          # Riemen
    col = mix(col, THREAD_E * 1.1, ((np.abs(V_ - 0.2) < 0.035) & (((U * 24) % 1.0) < 0.55)).astype(float))   # Naht
    for cu in (0.3, 0.7):
        col = mix(col, np.array((226.0, 226, 236)), (blob(U, V_, cu, 0.57, 0.05, 0.09) > 0.3).astype(float))
    col = mix(col, CUFF_E * 0.4, (V_ > 0.9).astype(float))                                             # Rand
    return col


def paint_e_sleeve(U, V_):
    """Mantelstoff: sehr dunkles Blaugrau, drei lange hellere Faltenbahnen laengs (kraeftig), helle Naht, Abnutzung."""
    h, w = U.shape
    col = np.broadcast_to(CLOTH_E, (h, w, 3)).copy()
    col *= (0.9 + 0.2 * vnoise(h, w, 16, 8, 721))[..., None]
    fold = 0.5 + 0.5 * np.cos(2 * np.pi * (U * 3 + 0.08 * vnoise(h, w, 5, 3, 722) + 0.1 * V_))
    col = mix(col * 0.65, CLOTH_E * 2.4 + 12, fold ** 1.8 * 0.9)
    col = mix(col, THREAD_E * 0.7, ((np.abs(U - 0.5) < 0.012) & (((V_ * 30) % 1.0) < 0.5)).astype(float) * 0.5)
    col = mix(col, CLOTH_E * 2.6 + 12, np.clip((vnoise(h, w, 4, 5, 723) - 0.66) * 3, 0, 0.5))
    return col


ARM_PAINTERS = {"glove": paint_e_glove, "cuff": paint_e_cuff, "sleeve": paint_e_sleeve}


def build_fist_arm(part, s, top, bottom, outer, wrist, elbow, thumb, index=None, fist=(0.0155, 0.015, 0.0185),
                   reach=0.62, cuff_len=0.016):
    """Faust (Fassung f): schlank (hoechstens anderthalb Griffbreiten), deckt nur den oberen Teil des Griffs (`reach`),
    Handruecken (aussen) und Fingerruecken sind zwei Flaechen mit Kante (Textur), Daumen ein eigenes Stueck, Zeigefinger
    ein Stummel. Danach eine kurze, anliegende Stulpe und der Mantelaermel, der schraeg aus dem Bild laeuft."""
    m = lambda p: C(s * p[0], p[1], p[2])                                            # noqa: E731
    t, b = V(*top), V(*bottom)
    a = (b - t).normalized()
    o = V(*outer)
    o = (o - a * o.dot(a)).normalized()
    ref = m(tuple(o))
    wr, el = V(*wrist), V(*elbow)
    w, bb, tt = fist
    pts = [t - a * 0.004, t + (b - t) * reach * 0.5, t + (b - t) * reach, wr]
    shapes = [(w * 0.92, bb * 0.9, tt * 0.95), (w, bb, tt), (w * 0.95, bb * 0.95, tt * 0.95), (0.0165, 0.0155, 0.017)]
    part.loft(tube([m(tuple(p)) for p in pts], shapes, 8, ref_up=ref), "glove", cap_end=False)
    part.loft(tube([m(p) for p in thumb], [(0.0078,) * 3, (0.0070,) * 3, (0.0058,) * 3], 6, ref_up=ref), "glove")
    if index:
        part.loft(tube([m(p) for p in index[:2]], [(0.0058,) * 3, (0.0048,) * 3], 6, ref_up=ref), "glove")
    d = (el - wr).normalized()
    cuff = [wr - d * 0.004, wr + d * cuff_len * 0.5, wr + d * cuff_len]
    part.loft(tube([m(tuple(p)) for p in cuff], [(0.0185,) * 3, (0.0205,) * 3, (0.0215,) * 3], 10, ref_up=ref),
              "cuff", cap_end=False)
    part.loft(tube([m(tuple(wr + d * (cuff_len - 0.002))), m(tuple(wr + (el - wr) * 0.45)), m(tuple(el))],
                   [(0.0225,) * 3, (0.032,) * 3, (0.040,) * 3], 10, ref_up=ref), "sleeve")


def build_design_e(part, weapon):
    if weapon == "pistol":
        build_fist_arm(part, +1, top=(0, -0.006, 0.006), bottom=(0, -0.052, -0.015), outer=(1, 0, 0),
                       wrist=(0.016, -0.031, -0.018), elbow=(0.26, -0.07, -0.02),
                       thumb=((0.004, -0.014, -0.010), (-0.011, -0.004, 0.0), (-0.0125, 0.004, 0.016)),
                       index=((0.017, -0.006, 0.002), (0.008, -0.012, 0.018)), fist=(0.0150, 0.0145, 0.0180))
    else:
        build_fist_arm(part, -1, top=(0, 0, -0.008), bottom=(0, 0, -0.058), outer=(0.8, 0.6, 0),
                       wrist=(0.026, -0.012, -0.054), elbow=(0.27, -0.08, -0.04),
                       thumb=((-0.002, 0.016, -0.05), (-0.012, 0.013, -0.03), (-0.011, 0.009, -0.012)),
                       fist=(0.0150, 0.0145, 0.0185), reach=0.72)
