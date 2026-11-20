"""Probestueck Gruftwaechter, Runde 7 (Issue #335): J1 aus Runde 6, die gekreuzten Ketten schliessen mit dem Guertel ab.

Wunsch des Menschen: "Die Ueberkreuzten ketten des Gruftwaechters sollten mit dem Guertel abschliessen".
Alles ist J1 aus Runde 6 (Koerper, Ausruestung, Haltung, Maasse, schwarzes Gesicht, Auge auf seiner rechten Seite, Ruhe matt,
Ankuendigung rot). Das Skript fuehrt gruftwaechter_r4.py aus, nachdem die zwei Zeilen der gekreuzten Ketten im Quelltext durch
chain_links_ext ersetzt wurden (assert, dass die Ersetzung greift). chain_links_ext baut dieselbe Kette wie chain_links und
verlaengert sie bis an den Guertel: das Ende liegt knapp ueber der Guertelkante und wird von einer Messingklammer
(Raute, liegt wie die Schnalle auf dem Guertel; der alte Endpunkt der Kette entfaellt) uebergriffen. Keine frei endende Kette, kein Spalt.

  J1: Runde 6 unveraendert (Vergleich).
  K1: Ketten biegen nach aussen und enden an den Flanken des Guertels (x +-0,425 m, Klammer Raute).
  K2: Ketten biegen kurz vor dem Guertel senkrecht ab und enden vorn neben der Schnalle (x +-0,335 m, Klammer Raute).

Aufruf (Repository-Wurzel):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/gruftwaechter-335/runde-7/gruftwaechter_r7.py
Umgebungsvariable (nur Entwicklung): GW7_OUT = Ausgabeordner.
"""

import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
HERE_R7 = HERE
R4_PATH = os.path.join(os.path.dirname(HERE), "runde-4", "gruftwaechter_r4.py")
OUT = os.environ.get("GW7_OUT", HERE)

os.environ["GW_NOMAIN"] = "1"
ns = {"__file__": R4_PATH, "__name__": "gruftwaechter_r4"}
L1 = '        chain_links(g, "kette1", parts, T, (-.40, 1.74), (.30, 1.28), .034, 10, .030)\n'
L2 = '        chain_links(g, "kette2", parts, T, (.40, 1.72), (-.30, 1.28), .034, 10, .115, zmode=True)\n'
N1 = '        chain_links_ext(g, "kette1", parts, T, (-.40, 1.74), (.30, 1.28), .034, 10, .030, sign=1)\n'
N2 = '        chain_links_ext(g, "kette2", parts, T, (.40, 1.72), (-.30, 1.28), .034, 10, .115, zmode=True, sign=-1)\n'
with open(R4_PATH, encoding="utf-8") as _f:
    _src = _f.read()
assert _src.count(L1) == 1 and _src.count(L2) == 1, "Kettenzeilen in r4 nicht gefunden"
_src = _src.replace(L1, N1).replace(L2, N2)
EXT = {"mode": None}
EXT_OFF = 0.046                                    # Abstand der verlaengerten Kette von der Rumpfflaeche (Breite des Bandes wirft sich nach aussen)
# Verlaengerung je Fassung: Stuetzpunkte (x, y) fuer das Ende bei +x (Ruhe-Koordinaten), Klammer (x, y, Radius, Ecken)
EXT_PATHS = {
    "J1": ([], None),
    "K1": ([(.37, 1.222), (.425, 1.162)], (.425, 1.140, .044, 4)),
    "K2": ([(.335, 1.215), (.335, 1.150)], (.335, 1.135, .044, 4)),
}


def chain_links_ext(rec, grp, parts, prefixes, a, b, w, steps, off, zmode=False, sign=1):
    """Wie chain_links (r4), aber die Kette laeuft ueber ihr bisheriges Ende hinaus bis an den Guertel (EXT["mode"]).
    Ohne Verlaengerung ist das Ergebnis dasselbe Netz wie chain_links."""
    pr_, pd_, ray_, tube_, V_ = ns["pr"], ns["pd"], ns["ray"], ns["tube"], ns["V"]
    pts, ws = [], []
    ext, clasp = EXT_PATHS[EXT["mode"]]
    n_orig = steps + 1 if not ext else steps          # mit Verlaengerung entfaellt der alte Endpunkt (spart Dreiecke)
    for j in range(n_orig):
        x = a[0] + (b[0] - a[0]) * j / steps
        y = a[1] + (b[1] - a[1]) * j / steps
        d = pd_((0, 0, -1), y)
        p, n = ray_(parts, prefixes, pr_((x, y, 3.0)), d, 0.0 if zmode else off)
        pts.append(p - d.normalized() * off if zmode else p)
        ws.append(w if j % 2 == 0 else w * 0.62)
    for i, (x, y) in enumerate(ext):
        x *= sign
        d = pd_((0, 0, -1), y)
        p, n = ray_(parts, prefixes, pr_((x, y, 3.0)), d, 0.0)
        t_ = (i + 1) / len(ext)
        pts.append(p - d.normalized() * (off * (1 - t_)) + n * (EXT_OFF * t_) if zmode else p + n * EXT_OFF)
        ws.append(w if (n_orig + i) % 2 == 0 else w * 0.62)
    rec.body(grp, tube_(pts, [(q, 0.009, 0.009) for q in ws], 6, V_(0, 0, 1)), "chain")
    if clasp:
        cx, cy, cr, cn = clasp
        ns["hexplate"](rec, grp + "_klammer", parts, prefixes, pr_((cx * sign, cy, 3.0)), pd_((0, 0, -1), cy), cr, .014,
                       "brass", gap=.075, n=cn)


ns["chain_links_ext"] = chain_links_ext
exec(compile(_src, R4_PATH, "exec"), ns)
globals().update({k: v for k, v in ns.items() if not k.startswith("__") and k not in ("main", "POSE")})

OUT = os.environ.get("GW7_OUT", HERE_R7)           # nach dem Uebernehmen der r4-Namen wieder setzen
assert "runde-4" not in OUT and "runde-5" not in OUT and "runde-6" not in OUT
VARS5 = ("J1", "K1", "K2")
NAMES5 = {"J1": "Runde 6 unveraendert (Vergleich)", "K1": "Ketten enden an den Flanken des Guertels",
          "K2": "Ketten enden vorn neben der Schnalle"}
EYE_DX = {"J1": -0.050, "K1": -0.050, "K2": -0.050}   # Modell-x; seine rechte Seite = -x (J1 aus Runde 6)
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
    EXT["mode"] = H
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
    bc = V(0.0, 1.22, 0.40)
    for H in VARS5:
        r_ = sets[(H, "ruhend")]
        tiles[(H, "vorn")] = render(r_, cam, G(V(0, 1.25, 5.4)), G(V(0, 1.25, 0)), 30, (T, T), "ansicht")
        tiles[(H, "hinten")] = render(r_, cam, G(V(0, 1.25, -5.4)), G(V(0, 1.25, 0)), 30, (T, T), "ansicht")
        tiles[(H, "Seite")] = render(r_, cam, G(V(5.4, 1.33, 0)), G(V(0, 1.25, 0)), 30, (T, T), "ansicht")
        tiles[(H, "Bauch und Gürtel")] = render(r_, cam, G(bc + V(0.0, 0.22, 1.55)), G(bc), 46, (T, T), "ansicht")
        for s in states:
            img = render(list(sets[(H, s)]) + room, cam, G(V(0, 1.6, 4)), G(V(0, 1.6, 0)), 70, (854, 480), "spiel")
            game[(H, s)] = img
            save_png(img[::-1], os.path.join(OUT, f"spiel-{H}-{'ankuendigung-' if s == 'ankuendigung' else ''}4m.png"))
    return tiles, game, stats, T


def sheet_out(tiles, game, stats, T):
    GOLD, WH, GR = MG[5], (235, 235, 235), (190, 180, 200)
    M, GAP = 12, 20
    W = 2 * M + 6 * T + 5 * GAP
    sheet = np.zeros((2 * (T + 150) + 400, W, 3)) + FOG_COLOR
    text(sheet, "Gruftwächter, Runde 7 (Issue 335): die gekreuzten Ketten schließen mit dem Gürtel ab", M, 14, 44, WH, True)
    text(sheet, "Alles ist J1 aus Runde 6 (Körper, Ausrüstung, Haltung, schwarzes Gesicht, Auge rechts). Neu nur die Enden der "
         "beiden Ketten: Sie laufen bis an die Gürtelkante und gehen dort unter eine Messingklammer.", M, 70, 22, GR)
    text(sheet, "Hinten gibt es keine Ketten (sie liegen nur vorn auf dem Rumpf); K1 reicht mit den Klammern bis an die Flanken, "
         "K2 bleibt vorn neben der Schnalle.", M, 100, 22, GR)
    y = 150
    heads = {"K1": "K1: Ketten enden an den Flanken des Gürtels, Messingklammer",
             "K2": "K2: Ketten biegen ab und enden vorn neben der Schnalle, Messingklammer"}
    cols = ("Vergleich Runde 6", "vorn", "hinten", "Seite", "Bauch und Gürtel", "Spiel 4 m")
    for H in ("K1", "K2"):
        t, lo, hi = stats[H]
        text(sheet, H, M, y - 4, 60, GOLD, True)
        text(sheet, heads[H], M + 95, y + 8, 34, WH, True)
        y += 62
        text(sheet, f"{sum(t.values())} Dreiecke (Grenze 3000), 0 offene Kanten", M, y, 24, GR)
        y += 36
        for k, nm in enumerate(cols):
            x = M + k * (T + GAP)
            if k == 0:
                tile = tiles[("J1", "vorn" if H == "K1" else "Bauch und Gürtel")]
                nm = "J1 Runde 6 " + ("vorn" if H == "K1" else "Bauch")
            elif k < 5:
                tile = tiles[(H, nm)]
            else:
                tile = game[(H, "ruhend")][30:450, 217:637].copy()
                gi = game[(H, "ruhend")]
                hy, hx = BELLY_PX
                inset = up(gi[hy - 40:hy + 40, hx - 40:hx + 40], 2)
                tile[T - 168:T - 8, T - 168:T - 8] = inset
                tile[T - 169, T - 169:T - 7] = GOLD
                tile[T - 169:T - 7, T - 169] = GOLD
            blit(sheet, tile, x, y)
            text(sheet, nm, x + 6, y + 2, 22, WH)
        y += T + 34
    text(sheet, "Unten rechts im Spielbild: Bauch und Gürtel, 2-fach vergrößert. Nebel und Körnung wie Runde 4 (854 x 480, "
         "70 Grad, Augenhöhe 1,6 m, Abstand 4 m).", M, y, 22, GR)
    y += 40
    save_png(sheet[:y + 10][::-1], os.path.join(OUT, "blatt.png"))


BELLY_PX = (300, 427)

if __name__ == "__main__" or True:
    import tempfile  # noqa: E402
    res = main5()
    sheet_out(*res)
