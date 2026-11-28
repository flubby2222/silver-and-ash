"""Throwing Knives als Beute (Issue #819), Runde 2: Entwurf a "Dreistich" frei schwebend, ohne Leuchtplatte.

Urteil zu Runde 1: a gefaellt, aber ohne Unterlage, frei schwebend. Dieses Blatt zeigt:

  Block 1 (Stufe Vampiric, drei Drehwinkel, 3 m und 8 m), jeweils wie im Spiel bzw. schwebend:
    Quader   heutiger Platzhalter der Fernwaffe mit Leuchtplatte (PickupView.DrawEquipment, nur gelesen)
    Wuerfel  heutiger schwebender Item-Wuerfel mit Leuchtplatte (ebenda)
    a1       Dreistich wie Runde 1 (Spitzen 85 mm auseinander im Kreis), angehoben
    a2       Dreistich mit den Spitzen unten zusammen, gleiche Neigung (24 Grad nach aussen)
  Block 2 (a2, vier Stufenfarben, ein Winkel): Stufenfarbe ohne Platte
    ohne     nichts: das Band bleibt blutrot wie in der Datei
    Band     das Band in der Stufenfarbe (Texturbereich des Bandes umgefaerbt)
    Knoten   ein Wuerfel 0,09 m in der Stufenfarbe, wo die Spitzen zusammenlaufen

Schweben wie der Item-Wuerfel: Unterkante 0,12 m ueber dem Boden (Wuerfelmitte 0,2 m, Kante 0,16 m), Auf und Ab
+-0,04 m, Drehung um die Hochachse. Die drei Drehwinkel zeigen je eine andere Hoehe (-0,04 / 0 / +0,04 m).
Nebel und Laterne rechnet psx.compose("spiel") mit den Formeln und Werten von assets/shaders/level.fs und
LookSettings (Nebel 3,25 bis 18,2 m, Laterne mindestens 0,35, 15 Bit mit 4 x 4-Muster).

Jedes Messer hat hier ein eigenes Netz, damit jedes sein eigenes Licht bekommt (in Runde 1 teilten sich die Kopien
ein Netz und damit das Licht des zuletzt gestellten Messers; die Zaehlungen dort betrifft das nicht).

Schreibt beute-819-runde-2.png, ansichten/*.png und beute2-zahlen.txt.
Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python beute2.py
"""

import math
import os
import sys

import numpy as np

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
PROBEN = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.join(PROBEN, "throwing-knives-204", "runde-5"))
import staffel as S  # noqa: E402
import runde5 as R5  # noqa: E402
from psx import pass_material, save_image  # noqa: E402

B, G, V, Matrix, Vector, SCALE, bpy = S.B, S.G, S.V, S.Matrix, S.Vector, S.SCALE, S.bpy

U = 2.5                                          # Beute-Massstab: Meter in der Welt je Meter Skriptmass
K = U / SCALE
BLADE = 0.117                                    # Klingenansatz bis Spitze (Skriptmass)
LEAN = 24.0
ANGLES = [math.degrees(1.3 * i) for i in range(3)]
GLOW = (0.10, 0.35, 0.60)
BOB = (-0.04, 0.0, 0.04)
VAMPIRIC = ((150, 20, 30), (175, 25, 35), (200, 30, 40))
TIERS = [("Worn", (110, 76, 46)), ("Forged", (92, 112, 140)), ("Royal", (214, 170, 64)), ("Vampiric", (175, 25, 35))]
UNDER = 0.12                                     # Unterkante des schwebenden Stuecks (Wuerfel: 0,2 - 0,16 / 2)
XS = (1.65, 0.55, -0.55, -1.65)                  # +x liegt links im Bild
XS3 = (1.1, 0.0, -1.1)
DISTS = (3.0, 8.0)
BAND = (0, 72, 64, 128)                          # Texturbereich des Bandes (x0, y0, x1, y1; y von unten), aus messer3.py
KNOT = 0.09


def T(x, y, z):
    return Matrix.Translation(Vector((x * U, y * U, z * U)))


def stich(radius):
    return [B.rot(120 * i, "Z") @ T(radius, 0, 0) @ B.rot(LEAN, "Y") @ T(0, 0, BLADE) @ B.rot(90, "X") for i in range(3)]


class Set:
    pass


def own(group, tag):
    """Kopie eines Messers mit eigenem Netz (eigenes Licht, eigenes Material moeglich)."""
    out = []
    for o in group:
        c = o.copy()
        c.data = o.data.copy()
        c.name = f"{tag}_{o.name}"
        bpy.context.scene.collection.objects.link(c)
        B.HOME[c.name] = B.HOME[o.name].copy()
        out.append(c)
    return out


def trio(src, tag):
    return [own(src, f"{tag}_{i}") for i in range(3)]


def stand(groups, build, p, yaw):
    for group, m in zip(groups, build):
        B.place(group, B.world(p) @ B.rot(yaw, "Z") @ m @ Matrix.Scale(K, 4))


def window(x, dist):
    z = B.EYE.z + dist
    cx, top = B.pixel((x, 0.85, z))
    _x, bottom = B.pixel((x, -0.15, z - 0.2))
    half = int(0.5 / dist * B.FOCAL)
    return slice(max(top, 0), min(bottom, B.GAME[1])), slice(max(cx - half, 0), min(cx + half, B.GAME[0]))


def tile(frame, x, dist):
    c = B.pixel((x, 0.30, B.EYE.z + dist))
    w, h, f = (120, 110, 3) if dist == 3.0 else (45, 41, 8)
    return B.up_n(B.crop(frame, c, w, h), f)


def tile3(frame, x, dist):
    c = B.pixel((x, 0.30, B.EYE.z + dist))
    w, h, f = (90, 110, 3) if dist == 3.0 else (34, 41, 8)
    return B.up_n(B.crop(frame, c, w, h), f)


def tinted(src, tier, name):
    """Material mit der Textur des Messers, das Band in der Stufenfarbe (Helligkeit des Bandes bleibt)."""
    image = next(n.image for n in src.active_material.node_tree.nodes if n.type == "TEX_IMAGE")
    w, h = image.size
    arr = np.array(image.pixels[:]).reshape(h, w, 4)[..., :3] * 255
    x0, y0, x1, y1 = BAND
    part = arr[y0:y1, x0:x1]
    print("BEUTE2 Band-Bereich der Textur, mittlere Farbe", [int(v) for v in part.reshape(-1, 3).mean(0)])
    anchor = np.median(part[..., 0])
    arr[y0:y1, x0:x1] = np.clip(np.array(tier, float) * (part[..., :1] / anchor), 0, 255)
    colours = len({tuple(c) for c in np.round(arr).astype(int).reshape(-1, 3)})
    return pass_material(name, save_image(name, arr, os.path.join(bpy.app.tempdir, name + ".png"))), colours


def main():
    out = os.path.join(HERE, "ansichten")
    os.makedirs(out, exist_ok=True)
    s = S.setup()
    B.place([o for g in s.knives + s.wraps for o in g] + s.sleeve + s.blade, B.AWAY)
    src = s.knives[0]
    tris = sum(len(S.R.tris(o)[1]) for o in src)
    room = list(s.room)

    # --- Anordnung in der Luft: kleinster Kreis, bei dem sich die Messer nicht durchdringen -------------------------
    probe = trio(src, "probe")
    scan = []
    for mm in range(0, 36, 2):
        stand(probe, stich(mm / 1000), (0, UNDER, B.EYE.z + 3.0), 0.0)
        q = Set()
        q.knives = probe
        scan.append((mm, sum(S.crossing(q))))
    for g in probe:
        B.place(g, B.AWAY)
    tight = next(mm for mm, n in scan if n == 0 and all(m == 0 for r, m in scan if r >= mm))
    print("BEUTE2 Radius mm: Dreieckspaare", scan, "gewaehlt", tight)
    builds = {"a1": stich(0.034), "a2": stich(tight / 1000)}

    # --- Block 1: Vampiric, drei Winkel -------------------------------------------------------------------------------
    rows = [("quader", "heute: Quader mit Platte"), ("wuerfel", "heute: Item-Wuerfel mit Platte"),
            ("a1", "a1: wie Runde 1, schwebend"), ("a2", "a2: Spitzen unten zusammen")]
    sets = {(key, dist): trio(src, f"{key}_{dist}") for key in builds for dist in DISTS}
    knives = [o for gs in sets.values() for g in gs for o in g]
    frames, tiles, numbers, views, cross = [], {}, {}, {}, {}
    for j, (yaw, glow, bob, tier) in enumerate(zip(ANGLES, GLOW, BOB, VAMPIRIC)):
        colour = tuple(int(d + (t - d) * glow) for d, t in zip(R5.DARK, tier))
        plates, solids = [], {}
        for (key, _name), x in zip(rows, XS):
            for dist in DISTS:
                p = (x, 0.0, B.EYE.z + dist)
                if key == "quader":
                    o = R5.flat(f"platte_q_{dist}_{j}", colour, (0.75, 0.01, 0.32))
                    B.place([o], B.world((x, 0.005, p[2])) @ B.rot(yaw, "Z"))
                    plates.append(o)
                    o = R5.flat(f"quader_{dist}_{j}", tier, (0.55, 0.09, 0.12))
                    B.place([o], B.world((x, 0.055, p[2])) @ B.rot(yaw, "Z"))
                    solids[(key, dist)] = o
                elif key == "wuerfel":
                    o = R5.flat(f"platte_w_{dist}_{j}", colour, (0.45, 0.01, 0.45))
                    B.place([o], B.world((x, 0.005, p[2])) @ B.rot(yaw, "Z"))
                    plates.append(o)
                    o = R5.flat(f"wuerfel_{dist}_{j}", tier, (0.16, 0.16, 0.16))
                    B.place([o], B.world((x, 0.2 + bob, p[2])) @ B.rot(yaw, "Z"))
                    solids[(key, dist)] = o
                else:
                    stand(sets[(key, dist)], builds[key], (x, UNDER + bob, p[2]), yaw)
        frame = np.array(B.player(room + plates + list(solids.values()) + knives, s.cam))
        S.save_png(frame[::-1], os.path.join(out, f"spielbild-winkel-{j}-854x480.png"))
        frames.append(frame)
        base = np.array(B.player(room + plates, s.cam))
        masks = [S.diff(np.array(B.player(room + plates + [o for gs in sets.values() for o in gs[i]], s.cam)), base)
                 for i in range(3)]
        solid = S.diff(np.array(B.player(room + plates + list(solids.values()), s.cam)), base)
        for (key, _name), x in zip(rows, XS):
            for dist in DISTS:
                ys, xs = window(x, dist)
                if key in builds:
                    ms = [m[ys, xs] for m in masks]
                    shared = (ms[0] & ms[1]) | (ms[1] & ms[2]) | (ms[0] & ms[2])
                    union = ms[0] | ms[1] | ms[2]
                    numbers[(key, dist, j)] = ([int(m.sum()) for m in ms], int(shared.sum()),
                                               int(union.any(1).sum()), int(union.any(0).sum()))
                else:
                    m = solid[ys, xs]
                    numbers[(key, dist, j)] = ([int(m.sum())], 0, int(m.any(1).sum()), int(m.any(0).sum()))
                tiles.setdefault(key, {})[(dist, j)] = tile(frame, x, dist)
        if j == 1:
            for key in builds:
                q = Set()
                q.knives = sets[(key, 3.0)]
                cross[key] = sum(S.crossing(q))
                p = V(XS[2 if key == "a1" else 3], UNDER, B.EYE.z + 3.0)
                mine = [o for g in sets[(key, 3.0)] for o in g]
                for name, d in (("vorn", V(0, 0.35, -1.9)), ("seite", V(1.9, 0.35, 0)), ("hinten", V(0, 0.35, 1.9)),
                                ("schraeg-oben", V(-1.1, 1.5, -1.1))):
                    img = np.array(B.shot(mine, s.cam, G(p + d), G(p + V(0, 0.22, 0)), 24, (340, 340), "ansicht"))
                    S.save_png(img[::-1], os.path.join(out, f"{key}-{name}.png"))
                    if name == "schraeg-oben":
                        views[key] = img
        B.place(plates + list(solids.values()), B.AWAY)
    for o in knives:
        B.place([o], B.AWAY)

    # --- Block 2: Stufenfarbe ohne Platte, a2, vier Stufen, Winkel 74 Grad ------------------------------------------
    yaw = ANGLES[1]
    kinds = [("ohne", "ohne: Band blutrot wie in der Datei"), ("band", "Band in der Stufenfarbe"),
             ("knoten", f"Knoten: Wuerfel {KNOT:.2f} m in der Stufenfarbe")]
    sets3 = {(key, dist): trio(src, f"{key}3_{dist}") for key, _n in kinds for dist in DISTS}
    for (key, _n), x in zip(kinds, XS3):
        for dist in DISTS:
            stand(sets3[(key, dist)], builds["a2"], (x, UNDER, B.EYE.z + dist), yaw)
    knives3 = [o for gs in sets3.values() for g in gs for o in g]
    band_objs = [o for dist in DISTS for g in sets3[("band", dist)] for o in g]
    plain = {o.name: o.data.materials[0] for o in band_objs}
    ref = np.array(B.player(room + knives3, s.cam))
    tiles3, tier_px, band_colours = {}, {}, {}
    for t, (tname, tier) in enumerate(TIERS):
        mats = {}
        for o in band_objs:
            key = o.name.split("_", 3)[-1]
            if key not in mats:
                mats[key], band_colours[tname] = tinted(src[0] if len(src) == 1 else next(a for a in src if o.name.endswith(a.name)),
                                                        tier, f"band_{tname}_{key}")
            o.data.materials[0] = mats[key]
        knots = []
        for dist in DISTS:
            o = R5.flat(f"knoten_{tname}_{dist}", tier, (KNOT, KNOT, KNOT))
            B.place([o], B.world((XS3[2], UNDER + KNOT / 2, B.EYE.z + dist)) @ B.rot(yaw, "Z"))
            knots.append(o)
        frame = np.array(B.player(room + knives3 + knots, s.cam))
        S.save_png(frame[::-1], os.path.join(out, f"stufe-{tname.lower()}-854x480.png"))
        changed = S.diff(frame, ref)
        for (key, _n), x in zip(kinds, XS3):
            for dist in DISTS:
                ys, xs = window(x, dist)
                tier_px[(key, tname, dist)] = int(changed[ys, xs].sum())
                tiles3.setdefault(key, {})[(dist, t)] = tile3(frame, x, dist)
        if tname == "Forged":
            for key, x in (("band", XS3[1]), ("knoten", XS3[2])):
                p = V(x, UNDER, B.EYE.z + 3.0)
                mine = [o for g in sets3[(key, 3.0)] for o in g] + (knots[:1] if key == "knoten" else [])
                img = np.array(B.shot(mine, s.cam, G(p + V(-1.1, 1.5, -1.1)), G(p + V(0, 0.22, 0)), 24, (340, 340), "ansicht"))
                S.save_png(img[::-1], os.path.join(out, f"a2-{key}-forged-schraeg-oben.png"))
                views[key] = img
        B.place(knots, B.AWAY)
        for o in band_objs:
            o.data.materials[0] = plain[o.name]

    # --- Zahlen -------------------------------------------------------------------------------------------------------
    lines = [f"Throwing Knives als Beute (#819), Runde 2: Bildpunkte im Spielbild 854 x 480, Auge 1,6 m. Messer 2,5-fach, {tris} Dreiecke je Messer.",
             f"Schweben: Unterkante {UNDER:.2f} m, je Winkel {BOB[0]:+.2f} / {BOB[1]:+.2f} / {BOB[2]:+.2f} m. Neigung {LEAN:.0f} Grad nach aussen.",
             "Radius des Kreises der Spitzen (mm Skriptmass): Dreieckspaare Messer gegen Messer: " + ", ".join(f"{mm}: {n}" for mm, n in scan),
             f"a1 Radius 34 mm (in der Welt {34 * U:.0f} mm), a2 Radius {tight} mm (in der Welt {tight * U:.0f} mm)", "",
             "Block 1, Stufe Vampiric. Je Drehwinkel (0 / 74 / 149 Grad): Bildpunkte je Messer [1, 2, 3] | von mehr als einem Messer belegt | Hoehe x Breite des Umrisses"]
    notes = {}
    for key, name in rows:
        notes[key] = [name]
        if key in builds:
            notes[key] += [f"3 x {tris} = {3 * tris} Dreiecke, keine Platte", "Textur des Messers 128 x 128, 13 Farben",
                           f"Messer durchdringen sich: {cross[key]} Dreieckspaare"]
        else:
            notes[key] += ["12 Dreiecke + Platte 12, eine Farbe (Stufe)"]
        for dist in DISTS:
            per = [numbers[(key, dist, j)] for j in range(3)]
            lines.append(f"{name}, {dist:.0f} m: " + " ; ".join(f"{a} | {b} | {h} x {w}" for a, b, h, w in per))
            hs, ws = [h for _a, _b, h, _w in per], [w for _a, _b, _h, w in per]
            if key in builds:
                notes[key].append(f"{dist:.0f} m: je Messer mind. {min(min(a) for a, _b, _h, _w in per)} px, geteilt hoechstens {max(b for _a, b, _h, _w in per)} px")
            else:
                notes[key].append(f"{dist:.0f} m: {min(sum(a) for a, _b, _h, _w in per)} bis {max(sum(a) for a, _b, _h, _w in per)} px")
            notes[key].append(f"{dist:.0f} m: Umriss Hoehe {min(hs)} bis {max(hs)}, Breite {min(ws)} bis {max(ws)} px")
    lines += ["", "Block 2, a2, Winkel 74 Grad: Bildpunkte, die sich vom Stueck 'ohne' unterscheiden (3 m / 8 m)"]
    notes3 = {}
    for key, name in kinds:
        notes3[key] = [name]
        if key == "band":
            notes3[key] += ["je Stufe eine Textur 128 x 128 (oder im Spiel", "das Band als eigenes, gefaerbtes Material)",
                            "Farben der Textur: " + ", ".join(f"{n} {c}" for n, c in band_colours.items())]
        if key == "knoten":
            notes3[key] += ["12 Dreiecke mehr, eine Farbe"]
        for tname, _tier in TIERS:
            a, b = tier_px[(key, tname, 3.0)], tier_px[(key, tname, 8.0)]
            lines.append(f"{name}, {tname}: {a} / {b}")
            if key != "ohne":
                notes3[key].append(f"{tname}: {a} px in 3 m, {b} px in 8 m")
    with open(os.path.join(HERE, "beute2-zahlen.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join("BEUTE2 " + x for x in lines))

    # --- Blatt ----------------------------------------------------------------------------------------------------------
    gap, lab, note_w = 8, 22, 420
    tw, th = 360, 330
    tw3 = 272
    width = note_w + 8 * (tw3 + gap)
    y1 = 34 + lab + 480 + 14
    y2 = y1 + lab + len(rows) * (th + gap) + 14
    sheet = np.zeros((y2 + 2 * lab + len(kinds) * (th + gap) + 8, width, 3)) + S.FOG_COLOR
    B.text(sheet, "Silver & Ash - Throwing Knives als Beute (#819), Runde 2: Dreistich frei schwebend, ohne Leuchtplatte. Spielbild 854 x 480, "
           "Auge 1,6 m, Nebel und Laterne wie level.fs.", 6, 6, 16)
    B.text(sheet, "Spielbild, Winkel 74 Grad: vorn 3 m, hinten 8 m; von links: Quader, Item-Wuerfel, a1, a2", 6, 34, 14)
    B.paste(sheet, frames[1], 0, 34 + lab)
    for i, (key, name) in enumerate((("a1", "a1, schraeg von oben"), ("a2", "a2, schraeg von oben"),
                                     ("band", "a2 mit Band in Stufenfarbe (Forged)"), ("knoten", "a2 mit Knoten (Forged)"))):
        x = 854 + gap + i * (340 + gap)
        B.text(sheet, name, x + 2, 34, 13)
        B.paste(sheet, views[key], x, 34 + lab)
    for c in range(6):
        dist, j = DISTS[c // 3], c % 3
        B.text(sheet, f"{dist:.0f} m, Winkel {ANGLES[j]:.0f} Grad, Hoehe {BOB[j]:+.2f} m ({3 if dist == 3.0 else 8}x)", note_w + c * (tw + gap) + 2, y1, 13)
    for r, (key, _name) in enumerate(rows):
        y = y1 + lab + r * (th + gap)
        for k, note in enumerate(notes[key]):
            B.text(sheet, note, 6, y + 6 + k * 20, 14 if k == 0 else 12)
        for c in range(6):
            B.paste(sheet, tiles[key][(DISTS[c // 3], c % 3)], note_w + c * (tw + gap), y)
    B.text(sheet, "Stufenfarbe ohne Platte: a2 in den vier Stufen, Winkel 74 Grad. Zahl = Bildpunkte, die sich vom Stueck 'ohne' unterscheiden.", 6, y2, 14)
    for c in range(8):
        dist, t = DISTS[c // 4], c % 4
        B.text(sheet, f"{dist:.0f} m, {TIERS[t][0]} ({3 if dist == 3.0 else 8}x)", note_w + c * (tw3 + gap) + 2, y2 + lab, 13)
    for r, (key, _name) in enumerate(kinds):
        y = y2 + 2 * lab + r * (th + gap)
        for k, note in enumerate(notes3[key]):
            B.text(sheet, note, 6, y + 6 + k * 20, 14 if k == 0 else 12)
        for c in range(8):
            B.paste(sheet, tiles3[key][(DISTS[c // 4], c % 4)], note_w + c * (tw3 + gap), y)
    S.save_png(sheet[::-1], os.path.join(HERE, "beute-819-runde-2.png"))
    print("BEUTE2 Blatt geschrieben:", sheet.shape[1], "x", sheet.shape[0])


if __name__ == "__main__":
    main()
