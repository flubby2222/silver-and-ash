"""Throwing Knives (Issue #204), Runde 2: Entwurf e "Schwarzruecken" - das eigene Stueck.

Nicht aus dem Bestand abgeleitet, sondern fuer das gebaut, was ein Wurfmesser im Spiel tun muss:
    - aus einem Stueck geschmiedet: einschneidiges Blatt mit geradem Ruecken und bauchiger Schneide. Der Ruecken behaelt die
      schwarze Schmiedehaut, nur die Schneidenhaelfte ist blank geschliffen. Zweigeteilt statt umrandet: auch als sechs
      Pixel im Ziel bleibt ein heller und ein dunkler Streifen nebeneinander.
    - kein Griff aus Holz, keine Parierstange (nichts, was sich beim Wurf verfaengt): die flache Angel ist mit dunkler
      Lederschnur umwickelt.
    - Ringknauf: ein Loch im Umriss. Daran liest man die drei Messer im Faecher und das Messer im Ziel.
    - durch den Ring ein kurzes blutrotes Band: die Stufe Vampiric, und im Flug das Einzige, was man von hinten sieht.
Farben nur aus assets/palette/hauptpalette.json, harte Stufen. Aufbau wie c und d: ein Knoten "knife", Ursprung am
Klingenansatz, Spitze +z, Klinge breit in y, Massstab 1,8 eingebacken, Textur 128 x 128, flach schattiert, geschlossen.
Schreibt messer-e-schwarzruecken.glb und textur3.png.

Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python messer3.py
"""

import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from messer_gemeinsam import (PROF_DIAMOND, SCALE, TEX, Matrix, Part, _ROWS, bake_scale, bpy, circle, closed_report,  # noqa: E402
                              export_glb, export_material, pose, rects_from_pixels, ring_yz, save_image, step, sweep,
                              vnoise, V)

EIS = _ROWS["Eisen"]
HOLZ = _ROWS["Holz"]
BLUT = _ROWS["Blutrot"]

RECTS = {
    "blade": (0, 0, 64, 72),
    "scale": (64, 0, 128, 40),      # Schmiedehaut: Angel und Ring
    "cord": (64, 40, 128, 88),
    "band": (0, 72, 64, 128),
    "rest": (64, 88, 128, 128),
}
UV = rects_from_pixels(RECTS, TEX)


def blade_painter(U, V_):
    """u = 0,25 ist der Ruecken, u = 0,75 die Schneide, dazwischen die beiden Flanken."""
    h, w = U.shape
    a = np.abs(U - 0.25)
    t = np.minimum(a, 1 - a) * 2                                         # 0 Ruecken ... 1 Schneide
    line = 0.40 + 0.10 * (vnoise(h, w, 2, 7, 601) - 0.5) + 0.05 * (V_ - 0.5)   # Schleifkante, leicht wellig
    # Schmiedehaut: fast schwarz, Hammerdellen eine Stufe heller, am Ruecken abgegriffen
    skin = step(EIS[1:4], 0.10 + 0.75 * vnoise(h, w, 5, 9, 602))
    skin = np.where((vnoise(h, w, 9, 14, 603) > 0.84)[..., None], EIS[4], skin)
    # Schliff: Riefen quer zur Klinge (vom Stein), zur Schneide hin heller
    grind = 0.15 + 0.55 * (t - line) / (1 - line) + 0.35 * (vnoise(h, w, 1, 30, 604) - 0.5)
    ground = step(EIS[4:8], np.clip(grind, 0, 0.99))
    col = np.where((t < line)[..., None], skin, ground)
    col = np.where((np.abs(t - line) < 0.035)[..., None], EIS[3], col)   # Uebergang: eine dunkle Stufe, kein Lichtsaum
    edge = (t > 0.88) & (vnoise(h, w, 16, 16, 605) > 0.18)               # blanke Schneide mit Scharten
    return np.where(edge[..., None], EIS[7], col)


def scale_painter(U, V_):
    h, w = U.shape
    f = 0.10 + 0.70 * vnoise(h, w, 4, 6, 611)
    f = np.where(vnoise(h, w, 8, 8, 612) > 0.86, 0.99, f)                # blank gescheuerte Stellen
    return step(EIS[1:5], f)


def cord_painter(U, V_):
    """Lederschnur, eng gewickelt: dunkle Fugen, auf jeder Windung ein helleres Licht."""
    h, w = U.shape
    turn = (V_ * 9 + U) % 1.0
    f = np.where(turn < 0.20, 0.0, 0.30 + 0.55 * np.sin((turn - 0.20) / 0.80 * np.pi) + 0.2 * (vnoise(h, w, 6, 6, 621) - 0.5))
    return step(np.stack([HOLZ[0], HOLZ[1], HOLZ[1], HOLZ[2]]), np.clip(f, 0, 0.99))


def band_painter(U, V_):
    """Blutrotes Band: Blutrot 3 (Anker der Stufe), Falten Blutrot 2, ein Licht in Blutrot 5 laengs der Mitte."""
    h, w = U.shape
    f = 0.45 + 0.5 * (vnoise(h, w, 3, 5, 631) - 0.5)
    col = step(np.stack([BLUT[2], BLUT[3], BLUT[3]]), np.clip(f, 0, 0.99))
    light = (np.abs((U * 2) % 1.0 - 0.5) < 0.10) & (vnoise(h, w, 2, 9, 632) > 0.45)
    return np.where(light[..., None], BLUT[5], col)


PAINTERS = {"blade": blade_painter, "scale": scale_painter, "cord": cord_painter, "band": band_painter,
            "rest": scale_painter}


def make_atlas():
    atlas = np.zeros((TEX, TEX, 3)) + EIS[0]
    for name, (x0, y0, x1, y1) in RECTS.items():
        pw, ph = x1 - x0, y1 - y0
        U, V_ = np.meshgrid((np.arange(pw) + 0.5) / pw, (np.arange(ph) + 0.5) / ph)
        atlas[y0:y1, x0:x1] = PAINTERS[name](U, V_)
    return atlas


def build_e():
    k = Part("knife", (0, 0, 0), UV)
    # Blatt: Ruecken (-y) fast gerade, Schneide (+y) bauchig, Spitze zum Ruecken hin gezogen
    sweep(k, [(0, 0.0000, -0.004), (0, 0.0020, 0.014), (0, 0.0048, 0.052), (0, 0.0024, 0.092), (0, -0.0022, 0.117)],
          [(0.0032, 0.0075), (0.0034, 0.0095), (0.0032, 0.0123), (0.0022, 0.0086), (0.0004, 0.0006)], "blade", PROF_DIAMOND)
    # Angel: flach, aus demselben Stueck, verjuengt sich zum Ring
    sweep(k, [(0, 0, -0.064), (0, 0, -0.003)], [(0.0026, 0.0048), (0.0030, 0.0070)], "scale")
    # Schnurwicklung ueber der Mitte der Angel, flach-oval
    sweep(k, [(0, 0, -0.054), (0, 0, -0.034), (0, 0, -0.012)], [(0.0044, 0.0064), (0.0050, 0.0074), (0.0046, 0.0078)], "cord",
          circle(6))
    # Ringknauf
    ring_yz(k, (0, 0, -0.0725), 0.0088, 0.0027, "scale", segments=8)
    # Band: durch den Ring geknotet (Knoten), zwei ungleich lange Enden haengen nach hinten
    sweep(k, [(0, 0, -0.0845), (0, 0, -0.0790)], [(0.0030, 0.0034)] * 2, "band", circle(6))
    sweep(k, [(0.0012, 0.0008, -0.084), (0.0016, 0.0034, -0.097), (0.0010, 0.0026, -0.110)],
          [(0.0006, 0.0030), (0.0006, 0.0032), (0.0006, 0.0022)], "band")
    sweep(k, [(-0.0012, -0.0008, -0.084), (-0.0014, -0.0036, -0.098)],
          [(0.0006, 0.0028), (0.0006, 0.0020)], "band")
    return k


def export():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    knife = build_e()
    open_e, multi, loose, edges = closed_report([knife])
    print(f"PRUEFUNG e: Kanten {edges}, offene Kanten {open_e}, Kanten mit mehr als zwei Flaechen {multi}, lose Kanten {loose}")
    assert open_e == 0 and multi == 0 and loose == 0, "Modell nicht geschlossen"
    tex = make_atlas()
    colors = {tuple(int(v) for v in c) for c in tex.reshape(-1, 3)}
    palette = {tuple(int(v) for v in c) for row in _ROWS.values() for c in row}
    assert len(colors) <= 32 and colors <= palette, (len(colors), colors - palette)
    image = save_image("messer_e", tex, os.path.join(HERE, "textur3.png"))
    coll = bpy.data.collections.new("Messer")
    bpy.context.scene.collection.children.link(coll)
    obj = knife.build(coll, export_material("messer_e", image), smooth=False)
    tris = len(obj.data.polygons)
    assert tris <= 500, tris                                     # drei Messer zusammen bleiben unter 1500
    pose([obj], {}, root=Matrix.Scale(SCALE, 4))
    bake_scale([obj])
    export_glb([obj], os.path.join(HERE, "messer-e-schwarzruecken.glb"))
    co = [v.co for v in obj.data.vertices]
    print(f"MODELL e Schwarzruecken: {tris} Dreiecke je Messer, {len(colors)} Farben, Textur {TEX}x{TEX}, "
          f"Laenge {(max(c.y for c in co) - min(c.y for c in co)) / SCALE:.3f} m, "
          f"Breite {(max(c.z for c in co) - min(c.z for c in co)) / SCALE:.3f} m (Spielmass, Datei 1,8-fach)")


if __name__ == "__main__":
    export()
