"""Bewegungsentwurf Bossraum-Tuer (Issue #549), Probestueck ausserhalb des Spiels.

Je Fluegelart (zweifluegelig am Entwurf a, einfluegelig am Entwurf b) drei Fassungen fuers Schliessen und drei fuers
Oeffnen. Alles aus diesem einen Skript: Zeitverlaeufe (reine Funktionen Winkel(ticks)), Tabellen, Pruefungen gegen den
Stein des Portals, Bilder im Spiel-Licht, GIFs, Kurvenblatt, Phasenblaetter.
Es laedt die Szene des Grafik-Probestuecks (design/proben/grafik/bossraum-tuer-549/bossraum_tuer.py, nur gelesen, ohne
dessen Hauptteil). GIFs setzt ffmpeg (auf dem Rechner installiert) zusammen.

Aufruf (aus dem Wurzelverzeichnis; TEMP vorher auf einen Ordner ausserhalb des Repos setzen):
    blender --background --python design/proben/bewegung/bossraum-tuer-549/bossraum_tuer_bewegung.py
Umgebungsvariablen:
    BTB_OUT    Ausgabeordner (Vorgabe: dieser Ordner)
    BTB_MODE   "tabellen" (nur Zahlen und Kurvenblatt), "probe" (Standbilder einiger Winkel), "alles" (Vorgabe)
    BTB_ONLY   "zweifluegel" oder "einfluegel": nur diese Fluegelart rendern (zwei Laeufe nebeneinander moeglich)
    BTB_NISCHE "0": Bossraum-Seite mit dem Portal, wie es das Modell heute hat (Vorgabe "1": glatte Nische, siehe unten)
Kein Zufall: zwei Laeufe liefern gleiche Tabellen; die Bilder entstehen aus denselben Formeln und Szenen.
"""

import csv
import math
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
GRAFIK = os.path.join(ROOT, "design", "proben", "grafik", "bossraum-tuer-549")
OUT = os.environ.get("BTB_OUT", HERE)
MODE = os.environ.get("BTB_MODE", "alles")
ONLY = os.environ.get("BTB_ONLY", "")
NISCHE = os.environ.get("BTB_NISCHE", "1") != "0"

TICK = 1.0 / 60.0
PLAYER_DIAMETER = 0.70        # Spieler-Radius 0,35 m (BodySizes)
AIR = 0.10                    # Luft neben dem Spieler
PASS_WIDTH = PLAYER_DIAMETER + AIR


# ---------------------------------------------------------------------------------------------------------------------
# Zeitverlaeufe. Winkel in Grad (0 = geschlossen, open_deg = offen), Zeit in Ticks seit dem Ausloeser.
# Alle Funktionen sind rein: gleiche Eingabe, gleiche Ausgabe; sie gelten auch fuer gebrochene Ticks.
# ---------------------------------------------------------------------------------------------------------------------

def smooth(u):
    """Weicher Uebergang 0..1 (smoothstep), ausserhalb geklemmt."""
    u = min(max(u, 0.0), 1.0)
    return u * u * (3.0 - 2.0 * u)


def trapez(t, total, ramp_up, ramp_down):
    """Weganteil 0..1 bei Tempo als Trapez: ramp_up Ticks anfahren, gleichmaessig laufen, ramp_down Ticks abbremsen."""
    if t <= 0:
        return 0.0
    if t >= total:
        return 1.0
    v = 1.0 / (total - (ramp_up + ramp_down) / 2.0)
    if t < ramp_up:
        return v * t * t / (2.0 * ramp_up)
    if t <= total - ramp_down:
        return v * (t - ramp_up / 2.0)
    return 1.0 - v * (total - t) ** 2 / (2.0 * ramp_down)


def bounce(t, t0, length, peak):
    """Ein Rueckprall nach dem Anschlag: Parabel von t0 bis t0 + length mit Hoehe peak (0 ausserhalb)."""
    if t < t0 or t > t0 + length:
        return 0.0
    u = (t - t0) / length
    return peak * 4.0 * u * (1.0 - u)


# --- Schliessen ---

def close_even(t, open_deg):
    """Gleichmaessig schwer: 30 Ticks anfahren, gleichmaessig, 20 Ticks abbremsen, liegt bei Tick 120 an. Kein Prall."""
    return open_deg * (1.0 - trapez(t, 120.0, 30.0, 20.0))


def close_slam(t, open_deg):
    """Anlauf und harter Anschlag: wird bis zum Anschlag bei Tick 78 immer schneller, prallt 2,5 Grad (14 Ticks) und
    0,6 Grad (8 Ticks) zurueck, Ruhe ab Tick 100. Der Prall bleibt auf der offenen Seite (Winkel nie unter 0)."""
    if t <= 0:
        return open_deg
    if t < 78:
        return open_deg * (1.0 - (t / 78.0) ** 2.2)
    return bounce(t, 78, 14, 2.5) + bounce(t, 92, 8, 0.6)


def close_two(t, open_deg):
    """Zwei Etappen: 0..36 weich bis auf 24 Grad, 36..52 faengt sich der Fluegel (schwingt 1,5 Grad zurueck),
    52..80 zieht er beschleunigt zu, Anschlag bei 80, Prall 1,5 Grad (10 Ticks) und 0,4 Grad (6 Ticks), Ruhe ab 96."""
    if t <= 0:
        return open_deg
    if t < 36:
        return open_deg + (24.0 - open_deg) * smooth(t / 36.0)
    if t < 52:
        return 24.0 + bounce(t, 36, 16, 1.5)
    if t < 80:
        return 24.0 * (1.0 - ((t - 52.0) / 28.0) ** 2)
    return bounce(t, 80, 10, 1.5) + bounce(t, 90, 6, 0.4)


# --- Oeffnen ---

def open_even(t, open_deg):
    """Gleichmaessig schwer: 30 Ticks anfahren, gleichmaessig, 40 Ticks abbremsen, offen bei Tick 132
    (132 Ticks = heutige bossOpenTime). Kein Prall."""
    return open_deg * trapez(t, 132.0, 30.0, 40.0)


def open_slam(t, open_deg):
    """Anlauf und Anschlag: wird bis zum Anschlag bei Tick 84 immer schneller, prallt 4 Grad (16 Ticks) und 1 Grad
    (9 Ticks) zurueck, Ruhe ab Tick 109. Der Prall bleibt innerhalb des Oeffnungswinkels."""
    if t <= 0:
        return 0.0
    if t < 84:
        return open_deg * (t / 84.0) ** 2
    return open_deg - bounce(t, 84, 16, 4.0) - bounce(t, 100, 9, 1.0)


def open_two(t, open_deg):
    """Zwei Etappen: 0..14 springt der Fluegel einen Spalt auf (9 Grad, abgebremst), 14..26 sinkt er auf 7 Grad
    zurueck, Halt bis 34, 34..124 schwingt er weich ganz auf."""
    if t <= 0:
        return 0.0
    if t < 14:
        u = t / 14.0
        return 9.0 * (1.0 - (1.0 - u) ** 2)
    if t < 26:
        return 9.0 - 2.0 * smooth((t - 14.0) / 12.0)
    if t < 34:
        return 7.0
    return 7.0 + (open_deg - 7.0) * smooth((t - 34.0) / 90.0)


def today_open(t, open_deg):
    """Vergleich: heutiges Bosstor (DoorPose, weich ueber bossOpenTime 2,2 s = 132 Ticks)."""
    return open_deg * smooth(t / 132.0)


MOTIONS = {
    # Schluessel: (Titel, Funktion, letzter Tick, Richtung)
    "gleichmaessig-schliessen": ("Schließen 1: gleichmäßig schwer", close_even, 120, "zu"),
    "anschlag-schliessen": ("Schließen 2: Anlauf und harter Anschlag", close_slam, 100, "zu"),
    "zwei-etappen-schliessen": ("Schließen 3: zwei Etappen", close_two, 96, "zu"),
    "gleichmaessig-oeffnen": ("Öffnen 1: gleichmäßig schwer", open_even, 132, "auf"),
    "anschlag-oeffnen": ("Öffnen 2: Anlauf und Anschlag", open_slam, 109, "auf"),
    "zwei-etappen-oeffnen": ("Öffnen 3: zwei Etappen", open_two, 124, "auf"),
}

# ---------------------------------------------------------------------------------------------------------------------
# Geometrie der Entwuerfe (aus bossraum_tuer.py abgelesen; Achsen: x seitlich, y oben, z nach vorn, Tuerflaeche z = 0).
# Das Portal ist nach vorn und hinten gleich gebaut, die Rechnung gilt fuer beide Schwenkrichtungen.
# ---------------------------------------------------------------------------------------------------------------------

LEAVES = {
    # Laenge ab Angel (Angel bei x = 1), halbe Dicke mit Beschlag, Oberkante als Funktion des Abstands von der Angel
    "a": {"art": "zweifluegel", "s0": 0.01, "s1": 1.00, "half": 0.12 + 0.03, "spring": 3.2, "apex": 5.18, "open": 90.0},
    "c": {"art": "zweifluegel", "s0": 0.01, "s1": 1.00, "half": 0.11 + 0.03, "spring": 3.2, "apex": 4.91, "open": 90.0},
    "b": {"art": "einfluegel", "s0": 0.01, "s1": 1.99, "half": 0.15 + 0.035, "top": 4.98, "open": 78.0},
}
ARCH = {
    # Spitzbogen-Portale: Mittelpunkt der Boegen bei x = xc (gespiegelt), Kaempfer 3,2; Stufen (Tiefe von, bis, Radius)
    "a": {"xc": -1.5, "spring": 3.2, "steps": ((0.12, 0.35, 2.5), (0.35, 0.60, 2.7), (0.60, 0.85, 2.9), (0.85, 1.0, 3.1))},
    "c": {"xc": -1.0, "spring": 3.2, "steps": ((0.12, 0.50, 2.0), (0.50, 0.80, 2.2), (0.80, 1.0, 2.45))},
}
RECT_B = ((0.12, 0.50, 1.0, 5.0), (0.50, 0.80, 1.22, 5.22), (0.80, 1.0, 1.44, 5.44))   # Tiefe von, bis, Halbbreite, Sturz
NICHE_HW, NICHE_TOP = 1.2, 5.3   # Vorschlag: glatte Nische auf der Schwenkseite (Halbbreite, Hoehe), Tiefe 0,12 bis 1,0


def leaf_top(key, s):
    """Oberkante des Fluegels im Abstand s von der Angel."""
    lf = LEAVES[key]
    if "top" in lf:
        return lf["top"]
    hw, rise = 0.99, lf["apex"] - lf["spring"]
    R = (rise * rise + hw * hw) / (2 * hw)
    cx = hw - R
    x = min(max(1.0 - s, 0.0), hw)                    # Lage im geschlossenen Fluegel
    return lf["spring"] + math.sqrt(max(R * R - (x - cx) ** 2, 0.0))


def stone_depth(key, x, y, z, niche=False):
    """Wie tief der Punkt im Stein des Portals steckt (0 = frei) und in welchem Bauteil."""
    az, ax = abs(z), abs(x)
    if az < 0.12:                                     # Tuerfuge: Mauer neben der Oeffnung
        return (ax - 1.0, "Falz") if ax > 1.0 else (0.0, "")
    if niche:
        if az >= 1.0:
            return 0.0, ""
        d = max(ax - NICHE_HW, y - NICHE_TOP)
        return (d, "Nische") if d > 0 else (0.0, "")
    if key == "b":
        if az >= 1.0:
            if ax > 1.0 and y < 0.5 and az < 1.125:
                return min(ax - 1.0, 1.125 - az), "Sockel"
            if 1.0 < ax < 1.44 and 4.9 < y < 5.1 and az < 1.22:
                return min(ax - 1.0, y - 4.9), "Konsole"
            return 0.0, ""
        for z0, z1, hw, top in RECT_B:
            if z0 <= az < z1:
                d = max(ax - hw, y - top)
                return (d, "Leibung" if ax - hw >= y - top else "Sturz") if d > 0 else (0.0, "")
        return 0.0, ""
    a = ARCH[key]
    if az >= 1.0:
        return 0.0, ""
    for z0, z1, R in a["steps"]:
        if z0 <= az < z1:
            if y <= a["spring"]:
                d = ax - (R + a["xc"])
                return (d, "Leibung") if d > 0 else (0.0, "")
            d = math.hypot(ax - a["xc"], y - a["spring"]) - R
            return (d, "Bogen") if d > 0 else (0.0, "")
    return 0.0, ""


def leaf_point(s, w, deg):
    """Punkt des Fluegels (Abstand s von der Angel, w quer zur Flaeche) beim Winkel deg; Angel bei x = 1, z = 0."""
    c, sn = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return 1.0 - s * c + w * sn, s * sn + w * c


def clip(key, deg, niche=False):
    """Groesste Eintauchtiefe je Bauteil ueber alle Probepunkte der Fluegel-Oberflaeche."""
    lf = LEAVES[key]
    worst = {}
    n = 60
    for i in range(n + 1):
        s = lf["s0"] + (lf["s1"] - lf["s0"]) * i / n
        top = leaf_top(key, s)
        for w in (-lf["half"], 0.0, lf["half"]):
            x, z = leaf_point(s, w, deg)
            for j in range(41):
                y = top * j / 40.0
                d, what = stone_depth(key, x, y, z, niche)
                if d > worst.get(what, 0.0):
                    worst[what] = d
    worst.pop("", None)
    return worst


def gap(key, deg):
    """Lichte Weite fuer eine Figur beim Winkel deg (Meter)."""
    lf = LEAVES[key]
    pts = []
    for i in range(81):
        s = lf["s0"] + (lf["s1"] - lf["s0"]) * i / 80.0
        for w in (-lf["half"], lf["half"]):
            pts.append(leaf_point(s, w, deg))
    if lf["art"] == "zweifluegel":                    # Spalt zwischen den beiden Fluegeln (spiegelgleich)
        return max(0.0, 2.0 * min(x for x, _ in pts))
    # einfluegelig: Abstand des Fluegels zum Gewaende der Gegenseite (Stufenprofil des Sturzportals)
    prof = [(-1.0, 0.0), (-1.0, 0.5), (-1.22, 0.5), (-1.22, 0.8), (-1.44, 0.8), (-1.44, 1.0), (-4.0, 1.0)]
    best = 9.0
    for (ax, az), (bx, bz) in zip(prof, prof[1:]):
        for px, pz in pts:
            dx, dz = bx - ax, bz - az
            u = min(max(((px - ax) * dx + (pz - az) * dz) / (dx * dx + dz * dz), 0.0), 1.0)
            best = min(best, math.hypot(px - ax - u * dx, pz - az - u * dz))
            if px < -1.0 and pz < 0.5:                # Fluegel steht noch in der Oeffnung
                best = 0.0
    return best


def block_angle(key):
    """Kleinster ganzer Winkel, bei dem eine Figur (0,70 m + 0,10 m Luft) durchpasst."""
    for deg in range(0, 91):
        if gap(key, deg) >= PASS_WIDTH:
            return float(deg)
    raise AssertionError(key)


BLOCK = {}


def setup_block():
    for key in LEAVES:
        BLOCK[key] = block_angle(key)


def keys_for(name, key):
    """Kennzahlen eines Verlaufs: ab wann die Tuer sperrt bzw. frei ist, Anschlag, Ruhe, groesster Schritt je Tick."""
    title, fn, last, way = MOTIONS[name]
    op, blk = LEAVES[key]["open"], BLOCK[key]
    vals = [fn(t, op) for t in range(0, last + 1)]
    if way == "zu":
        hit = next(t for t in range(0, last + 1) if vals[t] <= 1e-9)
        switch = next(t for t in range(0, last + 1) if all(v < blk for v in vals[t:]))
    else:
        hit = next(t for t in range(0, last + 1) if vals[t] >= op - 1e-9)
        switch = next(t for t in range(0, last + 1) if all(v >= blk for v in vals[t:]))
    step = max(abs(vals[t + 1] - vals[t]) for t in range(last))
    return {"wechsel": switch, "anschlag": hit, "ruhe": last, "schritt": step}


def write_tables():
    setup_block()
    lines = []
    lines.append("Bossraum-Tuer #549: Zeitverlaeufe und Pruefungen (erzeugt von bossraum_tuer_bewegung.py)")
    lines.append("")
    lines.append("Sperr-Schwelle: lichte Weite %.2f m (Spieler %.2f m + %.2f m Luft)" % (PASS_WIDTH, PLAYER_DIAMETER, AIR))
    for key in ("a", "c", "b"):
        b = BLOCK[key]
        lines.append("  Entwurf %s (%s): frei ab %2.0f Grad (Weite %.2f m; bei %2.0f Grad %.2f m), offen %2.0f Grad (Weite %.2f m)"
                     % (key, LEAVES[key]["art"], b, gap(key, b), b - 1, gap(key, b - 1), LEAVES[key]["open"],
                        gap(key, LEAVES[key]["open"])))
    lines.append("")
    lines.append("Verlauf; Fluegelart; Sperre wechselt bei Tick; Anschlag bei Tick; Ruhe ab Tick; groesster Schritt Grad je Tick")
    allkeys = {}
    for name in MOTIONS:
        for key in ("a", "b"):
            k = keys_for(name, key)
            allkeys[(name, key)] = k
            lines.append("  %-26s %-11s %3d  %3d  %3d  %.2f" % (name, LEAVES[key]["art"], k["wechsel"], k["anschlag"],
                                                               k["ruhe"], k["schritt"]))
    lines.append("")
    lines.append("Durchstiche: groesste Eintauchtiefe des Fluegels in den Stein je Winkel (m), Modell wie abgegeben")
    for key in ("a", "c", "b"):
        first = None
        for deg10 in range(0, int(LEAVES[key]["open"] * 10) + 1):
            w = clip(key, deg10 / 10.0)
            if any(v > 0.01 for k, v in w.items() if k not in ("Falz", "Leibung")) and first is None:
                first = deg10 / 10.0
        lines.append("  Entwurf %s: erster Durchstich ausserhalb von Falz/Leibung (> 1 cm) bei %s Grad"
                     % (key, "keinem" if first is None else "%.1f" % first))
        for deg in (5, 10, 15, 20, 30, 45, 60, 75, 78, 80, 85, 90):
            if deg > 90:
                continue
            w = clip(key, float(deg))
            lines.append("    %2d Grad: %s" % (deg, ", ".join("%s %.3f" % (k, v) for k, v in sorted(w.items())) or "frei"))
        wn = {}
        for deg10 in range(0, 901):
            for k, v in clip(key, deg10 / 10.0, niche=True).items():
                wn[k] = max(wn.get(k, 0.0), v)
        lines.append("    mit glatter Nische (Halbbreite %.2f, Hoehe %.2f) ueber 0..90 Grad: %s"
                     % (NICHE_HW, NICHE_TOP, ", ".join("%s %.3f" % (k, v) for k, v in sorted(wn.items())) or "frei"))
    # Grenzen: nie unter 0, nie ueber offen; Boden und Decke bleiben frei (Fluegel 0..5,18 m, Raum 6 m)
    for name, (title, fn, last, way) in MOTIONS.items():
        for key in ("a", "b"):
            op = LEAVES[key]["open"]
            prev = None
            for t10 in range(-20, last * 10 + 40):
                v = fn(t10 / 10.0, op)
                assert -1e-9 <= v <= op + 1e-9, (name, key, t10, v)
                if prev is not None:
                    assert abs(v - prev) < 0.45, (name, key, t10, v, prev)      # kein Sprung im Verlauf
                prev = v
            assert abs(fn(last, op) - (0.0 if way == "zu" else op)) < 1e-9, (name, key)
            assert abs(fn(0, op) - (op if way == "zu" else 0.0)) < 1e-9, (name, key)
    lines.append("")
    lines.append("Grenzen geprueft: alle Verlaeufe bleiben in 0..offen, ohne Sprung, Anfang und Ende stimmen.")
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, "pruefung.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    with open(os.path.join(OUT, "zeitverlauf.csv"), "w", encoding="utf-8", newline="") as f:
        wr = csv.writer(f, delimiter=";", lineterminator="\n")
        head = ["tick"]
        for key in ("a", "b"):
            head += ["%s-%s" % (LEAVES[key]["art"], n) for n in MOTIONS]
        wr.writerow(head)
        for t in range(0, 133):
            row = [t]
            for key in ("a", "b"):
                row += ["%.2f" % fn(min(t, last), LEAVES[key]["open"]) for (_, fn, last, _) in MOTIONS.values()]
            wr.writerow(row)
    print("\n".join(lines))
    return allkeys


# ---------------------------------------------------------------------------------------------------------------------
# Bilder
# ---------------------------------------------------------------------------------------------------------------------

def load_grafik():
    """Die Szene des Grafik-Probestuecks laden, ohne ihren Hauptteil auszufuehren (anderer __name__)."""
    path = os.path.join(GRAFIK, "bossraum_tuer.py")
    src = open(path, encoding="utf-8").read()
    ns = {"__file__": path, "__name__": "bossraum_tuer_szene"}
    exec(compile(src, path, "exec"), ns)
    os.makedirs(ns["TMP"], exist_ok=True)
    return ns


EYE = 1.6
SIZE = (854, 480)
VIEWS = {
    # Blick aus dem Bossraum (z < 0) bzw. aus dem Vorraum (z > 0), je 5 m vor der Wandebene
    "bossraum": ((0.0, EYE, -6.0), (0.0, EYE, 0.0)),
    "vorraum": ((0.0, EYE, 6.0), (0.0, EYE, 0.0)),
}
SCENE = {}


def niche_parts(ns, coll, key):
    """Vorschlag fuer die Schwenkseite (Bossraum): glatte Nische statt der gestuften Laeufe. Nur Kulisse dieser Probe."""
    V, W, Part = ns["V"], ns["W"], ns["Part"]
    A = ns["atlas_images"]()
    part = Part("nische", (0, 0, 0), ns["FRAME_UV"])
    hw, top, zd, zw, TOP, POST = NICHE_HW, NICHE_TOP, -0.12, -1.0, ns["TOP"], ns["POST"]
    for sx in (1, -1):
        W(part, [(sx * hw, 0, zw), (sx * POST, 0, zw), (sx * POST, TOP, zw), (sx * hw, TOP, zw)], (0, 0, -1), "stone")
        W(part, [(sx * hw, 0, zd), (sx * hw, 0, zw), (sx * hw, top, zw), (sx * hw, top, zd)], (-sx, 0, 0), "stone")
        # Rueckwand der Nische (Ebene der Tuerfuge) um die Oeffnung herum
        if key == "b":
            W(part, [(sx * 1.0, 0, zd), (sx * hw, 0, zd), (sx * hw, top, zd), (sx * 1.0, top, zd)], (0, 0, -1), "werk")
        else:
            a = ARCH[key]
            arc = ns["arc_pts"](a["xc"], a["steps"][0][2], a["spring"])
            W(part, [(sx * 1.0, 0, zd), (sx * hw, 0, zd), (sx * hw, a["spring"], zd), (sx * 1.0, a["spring"], zd)],
              (0, 0, -1), "werk")
            ring = [(hw, a["spring"])] + arc + [(0.0, top)]
            for p, q in zip(ring, ring[1:]):
                W(part, [(sx * hw, top, zd), (sx * p[0], p[1], zd), (sx * q[0], q[1], zd)], (0, 0, -1), "werk")
    if key == "b":
        W(part, [(-1.0, 5.0, zd), (1.0, 5.0, zd), (1.0, top, zd), (-1.0, top, zd)], (0, 0, -1), "werk")
    W(part, [(-hw, top, zw), (hw, top, zw), (hw, TOP, zw), (-hw, TOP, zw)], (0, 0, -1), "stone")
    W(part, [(-hw, top, zd), (hw, top, zd), (hw, top, zw), (-hw, top, zw)], (0, -1, 0), "stone")
    return [ns["place"](part, coll, ns["pass_material"]("m_nische", A["frame"]))]


def build_scene(ns, key):
    """Tuer in der Zelle (Tuerflaeche z = 0), Wandebenen z = +-1, Vorraum bei z > 1, Bossraum bei z < -1, beide 6 m hoch."""
    import bpy
    coll = ns["new_scene"]()
    objs = ns["add_design"](coll, key, dz=0.0)
    if NISCHE:
        back = [o for o in objs if o.name == "portal_back"]
        for o in back:
            objs.remove(o)
            bpy.data.objects.remove(o)
        objs += niche_parts(ns, coll, key)
    for z, facing in ((1.0, 1), (-1.0, -1)):
        objs += ns["add_wall"](coll, -9, -2, ns["TOP"], z, facing) + ns["add_wall"](coll, 2, 9, ns["TOP"], z, facing)
    objs += ns["add_floor"](coll, -9, 9, -16, 16, 0.0, "floor")
    objs += ns["add_floor"](coll, -9, 9, 1, 16, ns["TOP"], "ceiling")
    objs += ns["add_floor"](coll, -9.5, 9, -16, -1, ns["TOP"], "ceiling")
    ns["finish"](objs)
    cam = ns["camera"]()
    ns["setup_render"](*SIZE)
    cam.data.angle = math.radians(70)
    leaves = [o for o in objs if o.name in ("leaf_a", "leaf_b", "leaf")]
    assert len(leaves) == (2 if LEAVES[key]["art"] == "zweifluegel" else 1), [o.name for o in objs]
    SCENE.clear()
    SCENE.update({"key": key, "cam": cam, "leaves": leaves, "pivots": {o.name: o.matrix_world.translation.copy() for o in leaves}})


FRAME_CACHE = {}


def frame(ns, key, view, deg):
    """Ein Spielbild (854 x 480, Spiel-Licht): Fluegel um deg Grad in den Bossraum (z < 0) geschwenkt."""
    import bpy
    import numpy as np
    from mathutils import Matrix
    ck = (key, view, round(deg, 2))
    if ck in FRAME_CACHE:
        return FRAME_CACHE[ck].copy()
    if SCENE.get("key") != key:
        build_scene(ns, key)
    for o in SCENE["leaves"]:
        hinge = 1.0 if SCENE["pivots"][o.name].x > 0 else -1.0
        # Blender: Spiel-z nach hinten ist +y; der Fluegel an der +x-Angel dreht dafuer negativ um die Hochachse
        o.matrix_world = Matrix.Translation(SCENE["pivots"][o.name]) @ Matrix.Rotation(math.radians(-hinge * deg), 4, "Z")
    bpy.context.view_layer.update()
    for o in SCENE["leaves"]:
        ns["write_light"](o)
    loc, tgt = VIEWS[view]
    ns["aim"](SCENE["cam"], ns["G"](ns["V"](*loc)), ns["G"](ns["V"](*tgt)))
    p = ns["render_passes"]("btb_" + key)
    img = np.round(ns["compose"](p, "spiel")[::-1])
    FRAME_CACHE[ck] = img
    return img.copy()


def captioned(ns, im, lines):
    import numpy as np
    strip = np.tile(np.array((16, 11, 20), float), (46, im.shape[1], 1))
    for i, s in enumerate(lines):
        ns["text"](strip, s, 6, 2 + i * 22, 15)
    return np.concatenate([im, strip], axis=0)


def sample_times(last, hold_start=10, hold_end=24):
    """Zeitpunkte in Ticks im Abstand 1,2 (= 50 Bilder je Sekunde, GIF-Takt 2/100 s) mit Halt vorn und hinten."""
    ts = [-1.2 * i for i in range(hold_start, 0, -1)]
    t = 0.0
    while t < last:
        ts.append(t)
        t += 1.2
    ts.append(float(last))
    ts += [last + 1.2 * i for i in range(1, hold_end + 1)]
    return ts


def view_for(way):
    return "bossraum" if way == "zu" else "vorraum"


def status(way, t, k):
    if way == "zu":
        return "sperrt" if t >= k["wechsel"] else "frei"
    return "frei" if t >= k["wechsel"] else "sperrt"


def render_motion(ns, name, key, k):
    title, fn, last, way = MOTIONS[name]
    op = LEAVES[key]["open"]
    d = os.path.join(os.environ.get("TEMP", HERE), "btb_frames", key + "-" + name)
    shutil.rmtree(d, ignore_errors=True)
    os.makedirs(d)
    view = view_for(way)
    for i, t in enumerate(sample_times(last)):
        tc = min(max(t, 0.0), float(last))
        deg = fn(tc, op)
        im = captioned(ns, frame(ns, key, view, deg), [
            "%s (%s, Entwurf %s), Blick aus dem %s" % (title, LEAVES[key]["art"], key, "Bossraum" if way == "zu" else "Vorraum"),
            "Tick %5.1f   %.2f s   Winkel %5.1f°   Durchgang (Vorschlag): %s" % (tc, tc * TICK, deg, status(way, tc, k))])
        ns["png"](im, os.path.join(d, "f%04d.png" % i))
    return d


def gif(frames_dir, out_path, centis=2):
    """ffmpeg: Bilderfolge -> GIF mit eigener Palette, ohne Dithering (das Spielbild ist schon gekoernt)."""
    pal = os.path.join(frames_dir, "pal.png")
    src = os.path.join(frames_dir, "f%04d.png")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", "50", "-i", src,
                    "-vf", "palettegen=max_colors=96:stats_mode=full", pal], check=True)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", str(100.0 / centis), "-i", src, "-i", pal,
                    "-lavfi", "paletteuse=dither=none", "-loop", "0", out_path], check=True)


def phase_sheet(ns, key, allkeys):
    """Je Verlauf sieben Phasen in gleichem Zeitabstand, halbe Groesse. Nur Vorauswahl; das Urteil faellt am GIF."""
    import numpy as np
    rows = []
    for name, (title, fn, last, way) in MOTIONS.items():
        tiles = []
        for i in range(7):
            t = last * i / 6.0
            deg = fn(t, LEAVES[key]["open"])
            im = frame(ns, key, view_for(way), deg)[::2, ::2].copy()
            ns["text"](im, "Tick %d  %.0f°  %s" % (round(t), deg, status(way, t, allkeys[(name, key)])), 4, 2, 14)
            tiles.append(im)
        row = ns["hcat"](tiles, gap=4)
        head = np.tile(np.array((16, 11, 20), float), (28, row.shape[1], 1))
        ns["text"](head, "%s, %s (Entwurf %s), Phasen im Abstand von %.1f Ticks" % (title, LEAVES[key]["art"], key, last / 6.0),
                   6, 2, 16)
        rows.append(np.concatenate([head, row], axis=0))
    ns["png"](ns["vcat"](rows, gap=6), os.path.join(OUT, "phasen-%s-%s.png" % (LEAVES[key]["art"], key)))


# ---------------------------------------------------------------------------------------------------------------------
# Kurvenblatt
# ---------------------------------------------------------------------------------------------------------------------

COLORS = {"gleichmaessig": (120, 200, 255), "anschlag": (255, 150, 90), "zwei-etappen": (170, 230, 120)}


def curve_sheet(ns, allkeys):
    import numpy as np
    pw, ph, ml, mt, mr, mb = 760, 380, 60, 44, 16, 40
    paper, ink, grid, ref = np.array((24, 21, 28), float), (235, 230, 240), np.array((60, 54, 68), float), (130, 124, 140)
    panels = []
    for key in ("a", "b"):
        for way in ("zu", "auf"):
            img = np.tile(paper, (ph, pw, 1))
            op, blk = LEAVES[key]["open"], BLOCK[key]
            x_of = lambda t: ml + (pw - ml - mr) * t / 140.0
            y_of = lambda d: ph - mb - (ph - mt - mb) * d / 95.0
            for d in range(0, 91, 15):
                img[int(y_of(d)), ml:pw - mr] = grid
                ns["text"](img, "%d°" % d, 14, int(y_of(d)) - 12, 13, ref)
            for t in range(0, 141, 20):
                img[mt:ph - mb, int(x_of(t))] = grid
                ns["text"](img, "%d" % t, int(x_of(t)) - 10, ph - mb + 4, 13, ref)
            yb = int(y_of(blk))
            img[yb:yb + 2, ml:pw - mr:6] = np.array((230, 90, 90), float)
            ns["text"](img, "Sperre unter %.0f°" % blk, pw - mr - 150, yb - 22, 13, (230, 90, 90))

            def plot(fn, last, col, w=3):
                c = np.array(col, float)
                for i in range(0, last * 20 + 1):
                    t = i / 20.0
                    x, y = int(x_of(t)), int(y_of(fn(t, op)))
                    img[y - w // 2:y - w // 2 + w, x:x + 2] = c
            if way == "auf":
                plot(today_open, 132, ref, 2)
                ns["text"](img, "grau: heutiges Bosstor (132 Ticks, weich)", ml + 8, mt + 62, 13, ref)
            ly = mt + 2
            for name, (title, fn, last, w) in MOTIONS.items():
                if w != way:
                    continue
                col = COLORS[name.rsplit("-", 1)[0]]
                plot(fn, last, col)
                k = allkeys[(name, key)]
                xs = int(x_of(k["wechsel"]))
                img[int(y_of(blk)) - 7:int(y_of(blk)) + 8, xs:xs + 2] = np.array(col, float)
                lx = ml + 8 if way == "auf" else pw - mr - 400
                ns["text"](img, "%s: %s bei Tick %d, Ruhe ab %d" % (title.split(": ")[1], "sperrt" if way == "zu" else "frei",
                                                                 k["wechsel"], k["ruhe"]), lx, ly, 13, col)
                ly += 19
            ns["text"](img, "%s, %s (Entwurf %s, offen %.0f°)   Winkel über Ticks" % (
                "Schließen" if way == "zu" else "Öffnen", LEAVES[key]["art"], key, op), 8, 4, 16, ink)
            panels.append(img)
    sheet = ns["vcat"]([ns["hcat"](panels[0:2]), ns["hcat"](panels[2:4])])
    ns["png"](sheet, os.path.join(OUT, "kurven-bossraum-tuer-549.png"))


def main():
    allkeys = write_tables()
    ns = load_grafik()
    curve_sheet(ns, allkeys)
    if MODE == "tabellen":
        return
    import time
    for key in ("a", "b"):
        if ONLY and LEAVES[key]["art"] != ONLY:
            continue
        if MODE == "probe":
            t0 = time.time()
            degs = (0.0, 20.0, 45.0, 70.0, LEAVES[key]["open"])
            for view in VIEWS:
                tiles = [frame(ns, key, view, d) for d in degs]
                ns["png"](ns["hcat"](tiles[:3]), os.path.join(OUT, "probe-%s-%s-1.png" % (key, view)))
                ns["png"](ns["hcat"](tiles[3:]), os.path.join(OUT, "probe-%s-%s-2.png" % (key, view)))
            print("ZEIT je Bild: %.2f s" % ((time.time() - t0) / (2 * len(degs))))
            continue
        for name in MOTIONS:
            d = render_motion(ns, name, key, allkeys[(name, key)])
            way = name.rsplit("-", 1)[1]
            gif(d, os.path.join(OUT, "tor-%s-%s-%s-%s.gif" % (LEAVES[key]["art"], key, name.rsplit("-", 1)[0], way)))
        phase_sheet(ns, key, allkeys)


main()
