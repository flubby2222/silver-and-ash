"""Probestueck Gruftwaechter, Runde 6 (Issue #335): H1 mit versetztem Auge.

Urteil des Menschen zu Runde 5: "H1, aber das Auge soll versetzt sein. Als haette er 2."
Alles ist H1 aus Runde 5 (schwarzes Gesicht, Auge in Ruhe matt, rot nur in der Ankuendigung); das Skript fuehrt
gruftwaechter_r4.py unveraendert aus und ersetzt nur Kopf, Gesichtsfeld und Auge wie Runde 5. Neu ist allein die Lage des Auges:
es sitzt dort, wo bei zwei Augen eines sitzen wuerde (Augenhoehe der Haubenmitte, 5,0 cm seitlich der Mitte). Die Stelle
des zweiten Auges bleibt leer und schwarz.

  J1: Auge auf seiner rechten Seite (vom Betrachter aus links, Modell -x).
  J2: Auge auf seiner linken Seite (vom Betrachter aus rechts, Modell +x).

Aufruf (Repository-Wurzel):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/gruftwaechter-335/runde-6/gruftwaechter_r6.py
Umgebungsvariable (nur Entwicklung): GW6_OUT = Ausgabeordner.
"""
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
HERE_R6 = HERE
R4_PATH = os.path.join(os.path.dirname(HERE), "runde-4", "gruftwaechter_r4.py")
OUT = os.environ.get("GW6_OUT", HERE)

os.environ["GW_NOMAIN"] = "1"
ns = {"__file__": R4_PATH, "__name__": "gruftwaechter_r4"}
with open(R4_PATH, encoding="utf-8") as _f:
    exec(compile(_f.read(), R4_PATH, "exec"), ns)
globals().update({k: v for k, v in ns.items() if not k.startswith("__") and k not in ("main", "POSE")})

OUT = os.environ.get("GW6_OUT", HERE_R6)           # nach dem Uebernehmen der r4-Namen wieder setzen
assert "runde-4" not in OUT and "runde-5" not in OUT
VARS5 = ("J1", "J2")
NAMES5 = {"J1": "Auge auf seiner rechten Seite (links im Bild)", "J2": "Auge auf seiner linken Seite (rechts im Bild)"}
EYE_DX = {"J1": -0.050, "J2": 0.050}               # Modell-x; seine rechte Seite = -x
ERLAUBT5 = [("arm_r/cuff", "pole/shaft")]
EYE = {s: (0.036, 0.024) for s in ("ruhend", "ankuendigung")}       # Halbachsen des Auges (m), wie H1 in Runde 5
MODE = {"H": "J1", "state": "ruhend"}


def stone_row():
    import json
    pal = json.load(open(os.path.join(ROOT, "assets", "palette", "hauptpalette.json")))
    return np.array([r for r in pal["grund"] if r["name"] == "Stein"][0]["farben"], float)


STEIN = stone_row()
DARKEST = STEIN[0]
EYE_COLS = {"ruhend": (STEIN[2], STEIN[3], STEIN[4]), "ankuendigung": (BR[3], BR[5], BR[6])}


def eye_field(H, state):
    """Auge als Scheibe: Rand, Mitte, Kern in harten Stufen (3 Farben), Zeile 0 = unten."""
    rim_c, mid_c, core_c = EYE_COLS[state]
    d = np.sqrt(((XS - 15.5) / 16.0) ** 2 + ((YS - 15.5) / 16.0) ** 2)
    out = np.zeros((32, 32, 3)) + rim_c
    out[d < 0.66] = mid_c
    out[d < 0.30] = core_c
    return out


def make_atlas5(H, state):
    a = ns["make_atlas"](state)
    x0, y0 = FIELDS["face"]
    a[y0:y0 + 32, x0:x0 + 32] = DARKEST
    x0, y0 = FIELDS["eye"]
    a[y0:y0 + 32, x0:x0 + 32] = eye_field(H, state)
    return a


def conform_fan(rec, grp, parts, prefixes, origin, direction, pts2d, thick, rect, gap, up_hint):
    """Wie conform (r4), aber Ober- und Unterseite sind Faecher um einen Mittelpunkt, der ebenfalls per Strahl auf die Haube
    gesetzt wird. Die Haube hat eine Kante genau in der Mitte (Ring bei y 2,0, Eckpunkt vorn); liegt das Auge dort, liegen
    alle Dreiecke des Faechers auf je einer Flaeche der Haube und die Unterseite steckt nirgends in ihr."""
    g = rec.name + "/" + grp
    d = V(*direction).normalized()
    p0, n0 = ray(parts, prefixes, origin, d, 0.0)
    ex, ey, ez = frame_from(n0, up_hint)
    inner, outer = [], []
    for x, y in [(0.0, 0.0)] + list(pts2d):
        q, nq = ray(parts, prefixes, p0 + ex * x + ey * y + ez * 1.0, -ez, gap)
        inner.append(q)
        outer.append(q + nq * thick)
    n = len(pts2d)
    vi = [rec.bm.verts.new(G(q - rec.pivot)) for q in inner]
    vo = [rec.bm.verts.new(G(q - rec.pivot)) for q in outer]
    u0, v0, u1, v1 = rec.rects[rect]
    xs, ys = [p[0] for p in pts2d], [p[1] for p in pts2d]
    sx, sy = max(max(xs) - min(xs), 1e-4), max(max(ys) - min(ys), 1e-4)
    uvp = [(u0 + (u1 - u0) * (x - min(xs)) / sx, v0 + (v1 - v0) * (y - min(ys)) / sy) for x, y in [(0.0, 0.0)] + list(pts2d)]
    cu = ((u0 + u1) / 2, (v0 + v1) / 2)
    cin, cout = inner[0], outer[0]
    faces = []
    for i in range(1, n + 1):
        j = i % n + 1
        top = rec._face((vo[0], vo[i], vo[j]), (uvp[0], uvp[i], uvp[j]))
        rec._orient(top, cout - cin)
        bot = rec._face((vi[0], vi[j], vi[i]), (uvp[0], uvp[j], uvp[i]))
        rec._orient(bot, cin - cout)
        side = rec._face((vi[i], vi[j], vo[j], vo[i]), (cu, cu, cu, cu))
        rec._orient(side, (inner[i] + inner[j] + outer[i] + outer[j]) / 4 - (cin + cout) / 2)
        faces += [top, bot, side]
    rec._note(g, faces)
    return g


def make_head5(parts):
    pr_, pd_ = ns["pr"], ns["pd"]
    rings = [ns["hring"](y, 0, HEAD_CZ, head_r(y), head_r(y), head_r(y) * 0.95, 10) for y in HEAD_Y]
    h = rec_part("head", (0, 1.9, HEAD_CZ))
    body_q(h, "head", rings, lambda i, k: "face" if i in (2, 3) and k in (4, 5) else "coif", "flat", "dome")
    parts.append(h)
    e = rec_part("eyes", (0, 2.0, HEAD_CZ))
    y = 2.0
    hc = pr_((0, y, HEAD_CZ))
    face, right = pd_((0, 0, 1), y), pd_((1, 0, 0), y)
    rx, ry = EYE[MODE["state"]]
    conform_fan(e, "auge", parts, ["head/head"], hc + right * EYE_DX[MODE["H"]] + face * 1.0, -face, ngon(rx, ry, 8), 0.010, "eye",
                0.003, pd_((0, 1, 0), y))
    parts.append(e)


def build5(H, state):
    MODE["H"], MODE["state"] = H, state
    return ns["build"]("G2", state)


ns["make_head"] = make_head5
from psx import FOG_COLOR  # noqa: E402,F811


def head_cam(state):
    """Kamera fuer 'Kopf nah': leicht seitlich vor dem Gesicht, in der Haltung des Zustands."""
    ns["POSE"] = ns["Pose"](-10.0, 16.0) if state == "ankuendigung" else ns["Pose"]()
    hc = ns["pr"]((0, 2.0, HEAD_CZ))
    face, right, upv = ns["pd"]((0, 0, 1), 2.0), ns["pd"]((1, 0, 0), 2.0), ns["pd"]((0, 1, 0), 2.0)
    return hc + face * 1.0 + right * 0.22 + upv * 0.03, hc + upv * 0.0


def main5():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    for sub in ("modelle", "texturen"):
        os.makedirs(os.path.join(OUT, sub), exist_ok=True)
    states = ("ruhend", "ankuendigung")
    pal_all = {tuple(c) for k in PAL for c in PAL[k]}
    atlas, images = {}, {}
    for H in VARS5:
        for s in states:
            a = make_atlas5(H, s)
            cols = {tuple(c) for c in a.reshape(-1, 3)}
            assert cols <= pal_all, "Farbe ausserhalb der Palette"
            assert len(cols) <= 32, f"{H} {s}: {len(cols)} Farben"
            red = any(c in {tuple(x) for x in BR} for c in cols)
            assert red == (s == "ankuendigung"), f"{H} {s}: Rot-Regel"
            fx, fy = FIELDS["face"]
            assert (a[fy:fy + 32, fx:fx + 32] == DARKEST).all(), "Gesichtsfeld nicht ganz schwarz"
            atlas[(H, s)] = a
            images[(H, s)] = save_image(f"atlas_{H}_{s}", a, os.path.join(OUT, "texturen", f"atlas_{H}_{s}.png"))

    # 1. Modelle und Pruefungen
    lines, clip_lines, stats, built = [], [], {}, {}
    for H in VARS5:
        for s in states:
            built[(H, s)] = build5(H, s)
        mat = export_material(f"gw_{H}", images[(H, "ruhend")])
        objs = put_in_scene(built[(H, "ruhend")], mat, f"exp_{H}")
        export_glb(objs, os.path.join(OUT, "modelle", f"gruftwaechter_{H}.glb"))
        lo, hi = extent(objs)
        stats[H] = (tri_count(objs), lo, hi)
        rep = closed_report(objs)
        total = sum(r[0] for r in rep.values())
        lines.append(f"{H} {NAMES5[H]}: Teile {len(objs)}, Dreiecke {total}, offene Kanten "
                     f"{sum(r[1] for r in rep.values())}, Kanten mit mehr als zwei Flaechen {sum(r[2] for r in rep.values())}, "
                     f"kleinster Rauminhalt {min(r[3] for r in rep.values()):.6f} m3 (muss > 0 sein)")
        for name, r in rep.items():
            lines.append(f"    {name}: {r[0]} Dreiecke, offen {r[1]}, mehrfach {r[2]}, Raum {r[3]:.4f}")
        assert total <= 3000, f"{H}: {total} Dreiecke"
        assert hi[1] <= 2.6, f"{H}: {hi[1]:.2f} m hoch"
        assert sum(r[1] for r in rep.values()) == 0, "offene Kanten"
        for o in objs:
            bpy.data.objects.remove(o, do_unlink=True)
    for H in VARS5:
        for s in states:
            found, bad, selfx = clip_check(built[(H, s)], ERLAUBT5)
            nb = sum(len(p.bodies) for p in built[(H, s)])
            clip_lines.append(f"{H} {s}: {nb} Koerper, Paare mit Durchdringung {len(found)} (davon erlaubt "
                              f"{len(found) - len(bad)}), NICHT erlaubt {len(bad)}, Selbstdurchdringung {len(selfx)}")
            for a_, b_, n_, c_ in found:
                tag = "erlaubt" if (a_, b_, n_, c_) not in bad else "FEHLER"
                clip_lines.append(f"    {tag}: {a_} x {b_} ({n_} Dreieckspaare, erstes bei x {c_[0]} y {c_[1]} z {c_[2]})")
            for gname, n_ in selfx:
                clip_lines.append(f"    FEHLER Selbstdurchdringung: {gname} ({n_} Dreieckspaare)")
    with open(os.path.join(OUT, "pruefung.txt"), "w", encoding="utf-8-sig", newline="\n") as f:
        f.write("Pruefung 1: je Teil im Skript (Kante mit genau einer Flaeche = offen):\n" + "\n".join(lines) + "\n\n"
                "Pruefung 2: Durchdringung aller Koerper paarweise (BVH-Dreieckstest), Ruhe und Ankuendigung.\n"
                "Erlaubt ist nur der Waffenschaft in der Manschette (keine Hand modelliert).\n" + "\n".join(clip_lines) + "\n")
    with open(os.path.join(OUT, "masse.txt"), "w", encoding="utf-8-sig", newline="\n") as f:
        for H in VARS5:
            t, lo, hi = stats[H]
            f.write(f"{H} {NAMES5[H]}: Dreiecke {sum(t.values())} {dict(t)}, x {lo[0]:.2f}..{hi[0]:.2f}, "
                    f"y {lo[1]:.2f}..{hi[1]:.2f}, z {lo[2]:.2f}..{hi[2]:.2f}\n")
    print("\n".join(l for l in lines if not l.startswith("    ")))
    print("\n".join(clip_lines))
    print("FEHLER GESAMT:", sum(1 for l in clip_lines if "FEHLER" in l))

    # 2. Szenen
    pm = {k: pass_material("pm_%s_%s" % k, images[k]) for k in atlas}
    sets = {}
    for H in VARS5:
        for s in states:
            sets[(H, s)] = put_in_scene(build5(H, s), pm[(H, s)], f"r_{H}_{s}")
    for objs in sets.values():
        place(objs)
    cam = camera()
    stone = stone_textures(tempfile.mkdtemp(prefix="gw_stein_"))
    room = build_room(stone)
    T = 420
    fc = V(*FOCUS)

    tiles, game = {}, {}
    for H in VARS5:
        r_ = sets[(H, "ruhend")]
        a_ = sets[(H, "ankuendigung")]
        tiles[(H, "vorn")] = render(r_, cam, G(V(0, 1.25, 5.4)), G(V(0, 1.25, 0)), 30, (T, T), "ansicht")
        for s, objs, nm in (("ruhend", r_, "Kopf Ruhe"), ("ankuendigung", a_, "Kopf Ankündigung")):
            loc, tgt = head_cam(s)
            tiles[(H, nm)] = render(objs, cam, G(loc), G(tgt), 24, (T, T), "ansicht")
        for s in states:
            img = render(list(sets[(H, s)]) + room, cam, G(V(0, 1.6, 4)), G(V(0, 1.6, 0)), 70, (854, 480), "spiel")
            game[(H, s)] = img
            save_png(img[::-1], os.path.join(OUT, f"spiel-{H}-{'ankuendigung-' if s == 'ankuendigung' else ''}4m.png"))
    return tiles, game, stats, T


def sheet_out(tiles, game, stats, T):
    GOLD, WH, GR = MG[5], (235, 235, 235), (190, 180, 200)
    M, GAP = 12, 20
    W = 2 * M + 5 * T + 4 * GAP
    sheet = np.zeros((2 * (T + 150) + 400, W, 3)) + FOG_COLOR
    text(sheet, "Gruftwächter, Runde 6 (Issue 335): H1 mit versetztem Auge, als hätte er zwei", M, 14, 44, WH, True)
    text(sheet, "Alles ist H1 aus Runde 5 (schwarzes Gesicht, Auge in Ruhe matt, rot nur in der Ankündigung). Neu nur die Lage: "
         "5,0 cm seitlich der Mitte, auf Augenhöhe.", M, 70, 22, GR)
    text(sheet, "Die Stelle des zweiten Auges bleibt leer und schwarz. Das Auge ist 7,2 x 4,8 cm groß wie in Runde 5, ein "
         "gedachtes Paar (Abstand 10 cm) passt mit 2 cm Rand ins 21 cm breite schwarze Feld.", M, 100, 22, GR)
    y = 150
    heads = {"J1": "J1: Auge auf seiner rechten Seite (links im Bild)",
             "J2": "J2: Auge auf seiner linken Seite (rechts im Bild)"}
    cols = ("vorn", "Kopf Ruhe", "Kopf Ankündigung", "Spiel 4 m Ruhe", "Spiel 4 m Ankündigung")
    for H in VARS5:
        t, lo, hi = stats[H]
        text(sheet, H, M, y - 4, 60, GOLD, True)
        text(sheet, heads[H], M + 95, y + 8, 34, WH, True)
        y += 62
        text(sheet, f"{sum(t.values())} Dreiecke (Grenze 3000), 0 offene Kanten", M, y, 24, GR)
        y += 36
        for k, nm in enumerate(cols):
            x = M + k * (T + GAP)
            if k < 3:
                tile = tiles[(H, nm)]
            else:
                s = "ruhend" if k == 3 else "ankuendigung"
                tile = game[(H, s)][30:450, 217:637].copy()
                gi = game[(H, s)]
                hy, hx = HEAD_PX[s]
                crop = gi[hy - 20:hy + 20, hx - 20:hx + 20]
                inset = up(crop, 4)
                tile[T - 168:T - 8, T - 168:T - 8] = inset
                tile[T - 169, T - 169:T - 7] = GOLD
                tile[T - 169:T - 7, T - 169] = GOLD
            blit(sheet, tile, x, y)
            text(sheet, nm, x + 6, y + 2, 22, WH)
        y += T + 34
    text(sheet, "Unten rechts im Spielbild: der Kopf, 4-fach vergrößert. Nebel und Körnung wie Runde 4 (854 x 480, 70 Grad, "
         "Augenhöhe 1,6 m, Abstand 4 m).", M, y, 22, GR)
    y += 40
    save_png(sheet[:y + 10][::-1], os.path.join(OUT, "blatt.png"))


HEAD_PX = {"ruhend": (205, 427), "ankuendigung": (205, 435)}

if __name__ == "__main__" or True:
    import tempfile  # noqa: E402
    res = main5()
    sheet_out(*res)
