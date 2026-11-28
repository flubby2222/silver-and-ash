"""Wurfmesser des Jaegers (Throwing Knives), Entwurf e "Schwarzruecken" aus dem Probestueck zu Issue #204

Erzeuger fuer das Spiel (Issue #821). Ein Messer, zwei Dateien:

  throwing_knife.glb        das ganze Messer (196 Dreiecke): im Flug und im Ziel. Das Spiel zeichnet es mit 1/1,8.
  throwing_knife_hand.glb   Handmodell (100 Dreiecke): dasselbe Messer, 47 mm hinter dem Klingenansatz abgeschnitten
                            (35 mm hinter dem Haltepunkt bei Tiefe 12 mm), der Schnitt verschlossen. Nur fuer die Hand:
                            der Schnitt liegt hinter dem Deckel der Manschette. Das Handmodell wird nie aus der
                            Manschette gezogen; beim Loslassen ersetzt das ganze Messer es.

Aufbau: ein Knoten "knife", Ursprung am Klingenansatz, im Skript Spitze +z, Klinge breit in y, duenn in x (in der Datei
nach dem Export: Spitze -Y in Blender-Achsen). Massstab 1,8 eingebacken wie bei allen Waffen in der Hand, Textur
128 x 128 ohne Filterung (13 Farben der Hauptpalette), flach schattiert, rundum geschlossen. Kein Aermel: das Spiel
nimmt den Aermel der Pistole (pistol_hand.glb).

Das Skript fasst die drei Skripte der abgenommenen Probe zusammen (Zweig chore/throwing-knives-grafikprobe-204, Commit
af3eb20c: runde-2/messer3.py mit messer_gemeinsam.py, die kurzen Bandenden aus runde-4/messer3b.py, der Schnitt aus
runde-5/handmodell.py). Es prueft vor dem Ablegen, dass beide Dateien Byte fuer Byte den abgenommenen Dateien
runde-5/messer-e-geschoss.glb und runde-5/messer-e-hand-s12.glb entsprechen (SHA-256 unten); weicht eine ab, bricht
es ab und schreibt nichts nach assets/. Bild und Material heissen deshalb wie in der Probe "messer_e".

Aufruf (aus dem Repository-Wurzelverzeichnis, TEMP und TMP auf einen Zwischenordner setzen):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/models/throwing_knives.py

Schreibt assets/models/throwing_knife.glb, assets/models/throwing_knife_hand.glb und assets/models/throwing_knife.png
(das gemeinsame Bild; in den glb steckt es eingebettet). Danach: python tools/models/checksums.py.
Herkunft: alles per Skript, nichts heruntergeladen.
"""

import hashlib
import json
import math
import os
import shutil
import sys
import tempfile

import numpy as np

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT_DIR = os.path.join(ROOT, "assets", "models")
sys.path.insert(0, HERE)
import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from psx import (V, G, Part, frames, rects_from_pixels, vnoise, save_image, export_material,  # noqa: E402
                 pose, bake_scale, export_glb)

TEX = 128
SCALE = 1.8
WHOLE, HAND, IMAGE = "throwing_knife.glb", "throwing_knife_hand.glb", "throwing_knife.png"

# Die abgenommenen Dateien der Probe (runde-5/messer-e-geschoss.glb, runde-5/messer-e-hand-s12.glb).
ACCEPTED = {
    WHOLE: "0befdbc59302f5629cdffd27f8ddf2ae5f4f67c11e107c7b5de9e03f4f95bcc0",
    HAND: "dff918ce0b8a704658ea11547fe83ea0658853b87e42a18e55eae06548ad1513",
}

KNOT = -0.084                       # hier beginnen die Bandenden
LONG, SHORT = -0.097, -0.092        # kurze Bandenden (Runde 4b; Runde 2: -0.110 und -0.098)
DEPTH = 0.012                       # Tiefe der Haltung s12
BEHIND = 0.035                      # Schnitt so weit hinter dem Haltepunkt
CUT = DEPTH + BEHIND                # = 47 mm hinter dem Klingenansatz (Skriptmass)

_PAL = json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json"), encoding="utf-8"))
_ROWS = {r["name"]: np.array(r["farben"], float) for r in _PAL["akzent"] + _PAL["grund"]}
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


# --- Textur -----------------------------------------------------------------------------------------------------------

def step(ramp, field):
    """Feld 0..1 in harten Stufen auf die Rampe legen: nur Palettenfarben, kein Mischen, kein Dithering."""
    i = np.clip((np.asarray(field) * len(ramp)).astype(int), 0, len(ramp) - 1)
    return ramp[i]


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


# --- Geometrie --------------------------------------------------------------------------------------------------------

def circle(n, phase=0.0):
    return [(math.cos(phase + 2 * math.pi * k / n), math.sin(phase + 2 * math.pi * k / n)) for k in range(n)]


PROF_RECT = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
PROF_DIAMOND = [(-1, 0), (0, -1), (1, 0), (0, 1)]          # Grat links, Schneide unten, Grat rechts, Schneide oben


def sweep(part, centres, sizes, rect, profile=None, up=V(0, 1, 0)):
    """Geschlossener Koerper um eine Mittellinie: je Mittelpunkt ein Ring (Vieleck), Kappen als ebene Vielecke.
    sizes[i] = (halbe Breite, halbe Hoehe); profile: Liste (a, b) im Einheitsquadrat (Standard: Rechteck)."""
    profile = profile or PROF_RECT
    n = len(profile)
    u0, v0, u1, v1 = part.rects[rect]
    rings = []
    for (axis, side, upv), c, (w, h) in zip(frames([V(*p) for p in centres], up), [V(*p) for p in centres], sizes):
        rings.append([part.bm.verts.new(G(c + side * (w * a) + upv * (h * b) - part.pivot)) for a, b in profile])
    count = len(rings)
    faces = []
    for i in range(count - 1):
        for k in range(n):
            q = (rings[i][k], rings[i][(k + 1) % n], rings[i + 1][(k + 1) % n], rings[i + 1][k])
            uvs = ((u0 + (u1 - u0) * k / n, v0 + (v1 - v0) * i / (count - 1)),
                   (u0 + (u1 - u0) * (k + 1) / n, v0 + (v1 - v0) * i / (count - 1)),
                   (u0 + (u1 - u0) * (k + 1) / n, v0 + (v1 - v0) * (i + 1) / (count - 1)),
                   (u0 + (u1 - u0) * k / n, v0 + (v1 - v0) * (i + 1) / (count - 1)))
            faces.append(part._face(q, uvs))
    for ring in (rings[0], list(reversed(rings[-1]))):
        uvs = [(u0 + (u1 - u0) * (0.5 + 0.4 * a), v0 + (v1 - v0) * (0.5 + 0.4 * b)) for a, b in profile]
        faces.append(part._face(ring, uvs if ring is rings[0] else list(reversed(uvs))))
    bmesh.ops.recalc_face_normals(part.bm, faces=faces)
    return faces


def band_end(part, path, sizes):
    """Ein Bandende hinter dem Knoten, laengs gestaucht auf die kurzen Enden der Runde 4b (Form und Texturfeld bleiben)."""
    f = ((LONG if path[-1][2] < -0.100 else SHORT) - KNOT) / (path[-1][2] - KNOT)
    return sweep(part, [(x, y, KNOT + (z - KNOT) * f) for x, y, z in path], sizes, "band")


def ring_yz(part, c, radius, thick, rect, segments=10):
    """Geschlossener Ring (Torus mit Rautenquerschnitt) in der Klingenebene y-z um den Mittelpunkt c."""
    u0, v0, u1, v1 = part.rects[rect]
    c = V(*c)
    verts = []
    for i in range(segments):
        th = 2 * math.pi * i / segments
        radial = V(0, math.sin(th), math.cos(th))
        verts.append([part.bm.verts.new(G(c + radial * (radius + thick * a) + V(1, 0, 0) * (thick * b) - part.pivot))
                      for a, b in PROF_DIAMOND])
    faces = []
    for i in range(segments):
        a, b = verts[i], verts[(i + 1) % segments]
        for k in range(4):
            uvs = ((u0 + (u1 - u0) * k / 4, v0 + (v1 - v0) * i / segments),
                   (u0 + (u1 - u0) * (k + 1) / 4, v0 + (v1 - v0) * i / segments),
                   (u0 + (u1 - u0) * (k + 1) / 4, v0 + (v1 - v0) * (i + 1) / segments),
                   (u0 + (u1 - u0) * k / 4, v0 + (v1 - v0) * (i + 1) / segments))
            faces.append(part._face((a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k]), uvs))
    bmesh.ops.recalc_face_normals(part.bm, faces=faces)
    return faces


def closed_report(parts):
    """Vor dem Bauen: jede Kante gehoert zu genau zwei Flaechen? (offen, mehr als zwei, lose, Kanten)"""
    out = [0, 0, 0, 0]
    for p in parts:
        bm = p.bm
        out[0] += sum(1 for e in bm.edges if len(e.link_faces) == 1)
        out[1] += sum(1 for e in bm.edges if len(e.link_faces) > 2)
        out[2] += sum(1 for e in bm.edges if len(e.link_faces) == 0)
        out[3] += len(bm.edges)
    return out


def build_knife():
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
    band_end(k, [(0.0012, 0.0008, -0.084), (0.0016, 0.0034, -0.097), (0.0010, 0.0026, -0.110)],
             [(0.0006, 0.0030), (0.0006, 0.0032), (0.0006, 0.0022)])
    band_end(k, [(-0.0012, -0.0008, -0.084), (-0.0014, -0.0036, -0.098)],
             [(0.0006, 0.0028), (0.0006, 0.0020)])
    return k


def cut(obj):
    """Alles hinter dem Schnitt entfernen (Spitze liegt in -Y, die Datei ist 1,8-fach), Schnitt verschliessen."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
    res = bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=Vector((0, CUT * SCALE, 0)),
                                 plane_no=Vector((0, 1, 0)), clear_outer=True)
    y = CUT * SCALE
    verts = [v for v in res["geom_cut"] if isinstance(v, bmesh.types.BMVert)]
    bmesh.ops.remove_doubles(bm, verts=verts, dist=1e-5)       # doppelte Ecken der Texturnaehte am Schnitt verbinden
    edges = [e for e in bm.edges if all(abs(v.co.y - y) < 1e-5 for v in e.verts)]
    assert edges, "Schnitt trifft das Messer nicht"
    bmesh.ops.holes_fill(bm, edges=[e for e in edges if len(e.link_faces) == 1], sides=0)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    count = {}                      # Kanten nach Lage zaehlen: das Netz hat an Texturnaehten doppelte Ecken
    for f in bm.faces:
        p = [tuple(round(c, 5) for c in v.co) for v in f.verts]
        for a, b in zip(p, p[1:] + p[:1]):
            count[tuple(sorted((a, b)))] = count.get(tuple(sorted((a, b))), 0) + 1
    odd = sum(1 for n in count.values() if n % 2)
    assert odd == 0, "Handmodell nicht geschlossen"
    bm.to_mesh(obj.data)
    bm.free()
    return len(obj.data.polygons)


def export(glb_path, image_path, hand):
    """Ein Messer bauen und schreiben; hand: hinter dem Deckel der Manschette abgeschnitten."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    knife = build_knife()
    open_e, multi, loose, _edges = closed_report([knife])
    assert open_e == 0 and multi == 0 and loose == 0, "Modell nicht geschlossen"
    tex = make_atlas()
    colors = {tuple(int(v) for v in c) for c in tex.reshape(-1, 3)}
    palette = {tuple(int(v) for v in c) for row in _ROWS.values() for c in row}
    assert len(colors) <= 32 and colors <= palette, (len(colors), colors - palette)
    image = save_image("messer_e", tex, image_path)
    coll = bpy.data.collections.new("Messer")
    bpy.context.scene.collection.children.link(coll)
    obj = knife.build(coll, export_material("messer_e", image), smooth=False)
    tris = len(obj.data.polygons)
    assert tris <= 500, tris                                     # drei Messer zusammen bleiben unter 1500
    pose([obj], {}, root=Matrix.Scale(SCALE, 4))
    bake_scale([obj])
    if hand:
        tris = cut(obj)
    export_glb([obj], glb_path)
    print(f"MODELL {os.path.basename(glb_path)}: {tris} Dreiecke, {len(colors)} Farben, Textur {TEX}x{TEX}, geschlossen")


def sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def main():
    with tempfile.TemporaryDirectory() as folder:
        # Der Dateiname des Bildes steht in der glb: beim Bauen heisst es wie in der Probe, erst die Kopie heisst IMAGE.
        image = os.path.join(folder, "textur3.png")
        for name, hand in ((WHOLE, False), (HAND, True)):
            export(os.path.join(folder, name), image, hand)
            if sha256(os.path.join(folder, name)) != ACCEPTED[name]:
                raise SystemExit(f"FEHLER: {name} weicht von der abgenommenen Probe ab ({sha256(os.path.join(folder, name))})")
            print(f"{name} Byte-gleich zur abgenommenen Probe:", ACCEPTED[name])
        os.makedirs(OUT_DIR, exist_ok=True)
        for name in (WHOLE, HAND):
            shutil.copyfile(os.path.join(folder, name), os.path.join(OUT_DIR, name))
        shutil.copyfile(image, os.path.join(OUT_DIR, IMAGE))


if __name__ == "__main__":
    main()
