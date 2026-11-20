"""Probestueck Skelett mit Armbrust (Issue #334), Runde 4: Holzbogen B1, aber flach, nur die Sehne spannt sich.

Urteil des Menschen zu Runde 3, woertlich: "B1, das Model soll sich aber nicht nach oben biegen. Es reicht, wenn sich die
Sehne spannt." Daraus:
  - Nur noch der Holzbogen (B1). Die Spitzen heben sich nicht: der Bogen liegt in einer waagerechten Ebene, die Schenkel sind
    nach hinten zum Schuetzen gebogen.
  - Der Bogen hat in Ruhe und in der Ankuendigung genau dieselbe Form (dieselben Punkte im Armbrust-Rahmen, das prueft das
    Skript). Es aendert sich nur die Sehne (zur Nuss gezogen) und die Lage des Bolzens (liegt auf der Nuss).
  - Figur, Breite, Helligkeit, Textur unveraendert (Runde 2 und 3).
Zwei Stufen der Grundkruemmung, beide flach: F (flach, Spitzen 0,18 m hinter der Mitte) und D (deutlich, 0,26 m).
Zum Vergleich steht B1 aus Runde 3 (Spitzen gehoben, in der Ankuendigung staerker gebogen) in der obersten Zeile.

Aufruf (aus dem Repository-Wurzelverzeichnis, TEMP/TMP auf einen Ordner ausserhalb von /tmp setzen):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/armbrust-skelett-334/runde-4/armbrust4.py
Umgebungsvariable ARMBRUST4_OUT leitet die Ausgabe in einen anderen Ordner (fuer die Pruefung zweier Laeufe).
Ausgabe: blatt.png, modelle/armbrust_skelett_<F|D>_<ruhe|ank>.glb (B1 der Runde 3 als B1_*), texturen/atlas_*.png, masse.txt, spiel-*.png.
"""

import os
import sys

_HERE4 = os.path.dirname(os.path.abspath(__file__))
_R2 = os.path.join(os.path.dirname(_HERE4), "runde-2", "armbrust2.py")
_src = open(_R2, encoding="utf-8").read().rsplit("\nmain()", 1)[0]
__file__ = _R2                                   # HERE und ROOT der Runde 2 (Wurzel des Repositorys) richtig ableiten
exec(compile(_src, _R2, "exec"), globals())      # noqa: S102
OUT = os.environ.get("ARMBRUST4_OUT") or _HERE4
HERE = OUT

CUR = {"ver": "F", "pose": "ruhe", "sehne": None}
VERS = ("B1", "F", "D")

# Bogen: a = 0..1 vom Mittelpunkt zur Spitze. tx, back, lift = (Ruhe, Ankuendigung); bei F und D in beiden Haltungen gleich,
# lift ist 0 (kein Anstieg der Spitzen). B1 ist die Fassung der Runde 3, nur zum Vergleich.
_FLAT = dict(mat="wood", lift=(0.0, 0.0), y0=0.030, prof=lambda a: a ** 2, yprof=lambda a: a ** 2,
             a=(0, 0.2, 0.4, 0.6, 0.8, 1.0), w=(0.036, 0.013), v=(0.036, 0.013), sides=6, off=0.0)
BOWS = {
    "B1": dict(mat="wood", tx=(0.52, 0.49), back=(0.20, 0.33), lift=(0.12, 0.15), y0=0.012,
               prof=lambda a: a ** 2, yprof=lambda a: a ** 2, a=(0, 0.2, 0.4, 0.6, 0.8, 1.0),
               w=(0.036, 0.013), v=(0.036, 0.013), sides=6, off=0.0),
    "F": dict(_FLAT, tx=(0.52, 0.52), back=(0.18, 0.18)),
    "D": dict(_FLAT, tx=(0.52, 0.52), back=(0.26, 0.26)),
}
SEHNE_Y = 0.03        # Hoehe der Sehne in der Mitte = Hoehe der Bolzenachse
NUSS_Z = 0.34         # dorthin zieht die Ankuendigung die Sehne


def limb_point(cfg, k, u, zb):
    a = abs(u)
    s = 1 if u >= 0 else -1
    return (s * cfg["tx"][k] * a, cfg["y0"] + cfg["lift"][k] * cfg["yprof"](a), zb - cfg["back"][k] * cfg["prof"](a))


def lerp(pair, a):
    return pair[0] + (pair[1] - pair[0]) * a ** 0.9


def inset(a, b):
    """Die Kappen eines Rohrs mit zwei Ringen ragen um ein Viertel der Laenge hinaus (Pol von psx.Part.loft). Rueckt man die
    Ringe je um ein Sechstel nach innen, enden die Spitzen genau auf a und b."""
    return tuple(np.array(a, float) + (np.array(b, float) - np.array(a, float)) / 6), \
        tuple(np.array(b, float) + (np.array(a, float) - np.array(b, float)) / 6)


def limb_tube(P, X, cfg, k, zb, u0, u1, grow, mat):
    """Rohrstueck des Schenkels zwischen den Parametern u0 und u1 (etwas dicker: Wicklung, Band), Enden laufen spitz zu."""
    d = (u1 - u0) / 6
    us = [u0 + d, u1 - d]
    pts = [X.p(limb_point(cfg, k, u, zb)) for u in us]
    shapes = [(lerp(cfg["w"], abs(u)) + grow, lerp(cfg["v"], abs(u)) + grow, lerp(cfg["v"], abs(u)) + grow) for u in us]
    P.loft(tube(pts, shapes, cfg["sides"], ref_up=X.d((0, 1, 0))), mat)


def bow_k(ver, pose):
    """Welcher Satz (Ruhe/Ankuendigung) der Bogenmasse gilt. Bei F und D immer derselbe: der Bogen aendert sich nicht."""
    return 1 if (pose == "ank" and ver == "B1") else 0


def crossbow4(P, X, lv, long=0.80, span=0.40, tip=0.15):
    ver, pose = CUR["ver"], CUR["pose"]
    cfg = BOWS[ver]
    k = bow_k(ver, pose)
    xb = lambda lo, hi, r: xbox(P, X, lo, hi, r)       # noqa: E731
    xb((-0.028, -0.034, 0.0), (0.028, 0.02, long), "wood")                       # Schaft
    xb((-0.04, -0.075, -0.005), (0.04, 0.034, 0.09), "wood")                     # Kolben
    xb((-0.022, -0.09, 0.16), (0.022, -0.034, 0.24), "wood")                     # Griff
    zb = long - 0.10
    # Bogen: ein durchgehendes Rohr von Spitze zu Spitze, Mitte dick, Enden duenn
    half = list(cfg["a"])
    us = [-a for a in reversed(half[1:])] + half
    pts = [X.p(limb_point(cfg, k, u, zb)) for u in us]
    shapes = [(lerp(cfg["w"], abs(u)), lerp(cfg["v"], abs(u)), lerp(cfg["v"], abs(u))) for u in us]
    P.loft(tube(pts, shapes, cfg["sides"], ref_up=X.d((0, 1, 0))), cfg["mat"])
    # Lederwicklung in der Mitte und zwei Baender an den Enden
    limb_tube(P, X, cfg, k, zb, -0.14, 0.14, 0.009, "leather")
    for s in (1, -1):
        limb_tube(P, X, cfg, k, zb, s * 0.50, s * 0.56, 0.006, "leather")
    # Sehne: eigenes Teil, von Spitze zu Spitze; Ruhe gerade, Ankuendigung zur Nuss gezogen (nur sie bewegt sich)
    tipL, tipR = limb_point(cfg, k, -1.0, zb), limb_point(cfg, k, 1.0, zb)
    zs = tipL[2] if pose == "ruhe" else NUSS_Z
    centre = (0.0, SEHNE_Y, zs)
    S = CUR["sehne"]
    for a, b in ((tipL, centre), (centre, tipR)):
        a2, b2 = inset(a, b)
        bone(S, [a2, b2], (0.012, 0.012), "bone", sides=4, X=X)
    # Steigbuegel
    xb((-0.05, -0.06, long - 0.03), (0.05, 0.01, long + 0.035), "iron")
    # Nuss und Abzug
    xb((-0.012, 0.0, 0.32), (0.012, 0.03, 0.35), "bone")
    xb((-0.008, -0.07, 0.30), (0.008, -0.034, 0.325), "iron")
    # Bolzen mit Spitze (Ruhe: hinten an der Sehne, Ankuendigung: auf der Nuss)
    z0 = 0.35 if pose == "ank" else zs + 0.02
    bone(P, [(0, 0.03, z0), (0, 0.03, long + 0.12)], (0.014, 0.014), "wood", sides=4, X=X)
    xspike(P, X, (0, 0.03, long + 0.12), (0, 0.03, long + 0.22), 0.026, "iron")


crossbow = crossbow4


def bow_points_local(ver, pose):
    """Alle Mittellinien-Punkte des Bogens im Rahmen der Armbrust (ohne Haltung der Figur)."""
    cfg = BOWS[ver]
    k = bow_k(ver, pose)
    zb = 0.80 - 0.10
    half = list(cfg["a"])
    us = [-a for a in reversed(half[1:])] + half
    return [limb_point(cfg, k, u, zb) for u in us]


def build4(stage, pose, ver):
    """Wie build() der Runde 2, aber mit eigenem Teil "sehne"."""
    CUR.update(ver=ver, pose=pose)
    sehne = Part("sehne", (0, 0, 0), UV)             # noqa: F821
    sehne.flat = True
    CUR["sehne"] = sehne
    parts = build(stage, pose)                        # noqa: F821
    return parts + [sehne]


def main4():
    global SCRATCH
    SCRATCH = os.environ.get("TEMP", tempfile.gettempdir())
    # Beweis: F und D haben in Ruhe und Ankuendigung exakt dieselben Bogenpunkte, kein Punkt hebt sich (y gleich)
    for v in ("F", "D"):
        assert bow_points_local(v, "ruhe") == bow_points_local(v, "ank"), v
        assert len({p[1] for p in bow_points_local(v, "ruhe")}) == 1, v
    assert bow_points_local("B1", "ruhe") != bow_points_local("B1", "ank")      # Gegenprobe: B1 aus Runde 3 aendert sich
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    for d in ("modelle", "texturen"):
        os.makedirs(os.path.join(OUT, d), exist_ok=True)
    atlas = {"ruhe": make_atlas(False), "angriff": make_atlas(True)}
    colours = {k: len({tuple(c) for c in v.reshape(-1, 3).round().astype(int)}) for k, v in atlas.items()}
    assert max(colours.values()) <= 32, colours
    images = {k: save_image("atlas_" + k, v, os.path.join(OUT, "texturen", f"atlas_{k}.png")) for k, v in atlas.items()}
    atlas_of = {"ruhe": "ruhe", "ank": "angriff"}

    # 1. glb und Dreiecke
    stats = {}
    for ver in VERS:
        for pose in POSES:
            mat = export_material(f"as_{ver}_{pose}", images[atlas_of[pose]])
            objs = put_in_scene(build4("A2", pose, ver), mat, f"exp_{ver}_{pose}")
            export_glb(objs, os.path.join(OUT, "modelle", f"armbrust_skelett_{ver}_{pose}.glb"))
            lo, hi = extent(objs)
            bl, bh = extent([o for o in objs if o.name in ("armbrust", "sehne")])
            stats[(ver, pose)] = (tri_count(objs), lo, hi, bl, bh)
            for o in objs:
                bpy.data.objects.remove(o, do_unlink=True)
    assert sum(stats[("B1", "ruhe")][0].values()) == 1650, stats[("B1", "ruhe")][0]
    for v in ("F", "D"):
        for pose in POSES:
            assert sum(stats[(v, pose)][0].values()) <= 1650, (v, pose)
            assert stats[(v, pose)][4][0] - stats[(v, pose)][3][0] <= 1.2, (v, pose)

    # 2. Szenen
    pm = {k: pass_material("pm_" + k, images[k]) for k in images}
    sets = {(v, pose): put_in_scene(build4("A2", pose, v), pm[atlas_of[pose]], f"r_{v}_{pose}") for v in VERS for pose in POSES}
    cam = camera()
    stone = stone_textures(tempfile.mkdtemp(prefix="as_stein_", dir=SCRATCH))
    room = build_room(stone)

    def import_glb(path):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=path)
        objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
        for o in objs:
            image = next(n.image for n in o.active_material.node_tree.nodes if n.type == "TEX_IMAGE")
            o.data.materials.clear()
            o.data.materials.append(pass_material("imp_" + o.name, image))
            o["smooth"] = True
            HOME[o.name] = o.matrix_world.copy()
        return objs

    ghoul = import_glb(os.path.join(MODELS, "ghoul.glb"))                       # noqa: F821

    def parked(objs):
        place(objs, (200, 0, 0))                                                 # noqa: F821

    def park_all():
        parked(ghoul)
        for key in sets:
            parked(sets[key])

    park_all()

    # 2a. Ansichten der Figur (Ruhe): vorn, Seite, hinten, schraeg oben
    TW, TH = 300, 520
    dirs = (("vorn", V(0, 0.05, 1)), ("Seite", V(1, 0.05, 0)), ("hinten", V(0, 0.05, -1)), ("schräg oben", V(0.7, 0.5, 0.7)))
    tiles = {}
    for v in VERS:
        place(sets[(v, "ruhe")], (0, 0, 0))
        lo, hi = stats[(v, "ruhe")][1], stats[(v, "ruhe")][2]
        c = V(0.0, 0.98, (lo[2] + hi[2]) / 2)
        for nm, d in dirs:
            tiles[(v, nm)] = render(sets[(v, "ruhe")], cam, G(c + d.normalized() * 6.5), G(c), 20, (TW, TH), "ansicht")
        parked(sets[(v, "ruhe")])

    # 2b. Nahaufnahmen der Armbrust: schraeg, von oben, von vorn; Ruhe und Ankuendigung
    CW, CH = 395, 285
    cdirs = (("schräg", V(0.85, 0.55, 0.55)), ("oben", V(0.0, 1.0, 0.02)), ("vorn", V(0.0, 0.12, 1.0)))
    close = {}
    for v in VERS:
        for pose in POSES:
            place(sets[(v, pose)], (0, 0, 0))
            cfg = POSE[pose]                                                      # noqa: F821
            xc = Xf(cfg["butt"], cfg["pitch"], cfg["yaw"]).p((0.0, 0.06, 0.52))   # noqa: F821
            for nm, d in cdirs:
                close[(v, pose, nm)] = render(sets[(v, pose)], cam, G(xc + d.normalized() * 3.0), G(xc), 24, (CW, CH), "ansicht")
            parked(sets[(v, pose)])

    # 2c. Spiel aus 10 m: Ruhe und Ankuendigung; Messung je Figur allein
    xs = {"G": -1.4, "B1": -0.47, "F": 0.47, "D": 1.4}
    eye, tgt = G(V(0, 1.6, 10)), G(V(0, 1.6, 0))
    img10, meas = {}, {}
    for pose in POSES:
        park_all()
        place(ghoul, (xs["G"], 0, 0))
        vis = list(ghoul)
        for v in VERS:
            place(sets[(v, pose)], (xs[v], 0, 0))
            vis += sets[(v, pose)]
        img10[pose], _ = render(vis + room, cam, eye, tgt, 70, (854, 480), "spiel", True)
        save_png(img10[pose][::-1], os.path.join(OUT, f"spiel-10m-{pose}.png"))
        for k in ("G",) + VERS:
            park_all()
            objs = ghoul if k == "G" else sets[(k, pose)]
            place(objs, (0, 0, 0))
            im, mk = render(objs, cam, eye, tgt, 70, (854, 480), "spiel", True)
            cols, rows = np.where(mk.any(axis=0))[0], np.where(mk.any(axis=1))[0]
            meas[(k, pose)] = (int(mk.sum()), int(cols.max() - cols.min() + 1), int(rows.max() - rows.min() + 1),
                               float(lum(im[mk]).mean()))                         # noqa: F821

    # 2d. Ankuendigung aus 4 m: B1 (Runde 3), F, D
    park_all()
    ann = []
    x4 = (-1.4, 0.0, 1.4)
    for v, x in zip(VERS, x4):
        place(sets[(v, "ank")], (x, 0, 0))
        ann += sets[(v, "ank")]
    img4 = render(ann + room, cam, G(V(0, 1.6, 4)), G(V(0, 1.6, 0)), 70, (854, 480), "spiel")
    save_png(img4[::-1], os.path.join(OUT, "spiel-ankuendigung-4m.png"))
    park_all()

    # masse.txt
    with open(os.path.join(OUT, "masse.txt"), "w", encoding="utf-8", newline="\n") as f:
        for v in VERS:
            for pose in POSES:
                t, lo, hi, bl, bh = stats[(v, pose)]
                f.write(f"{v} {pose}: Dreiecke {t} (Summe {sum(t.values())}), Figur x {lo[0]:.2f}..{hi[0]:.2f}, "
                        f"y {lo[1]:.2f}..{hi[1]:.2f}, z {lo[2]:.2f}..{hi[2]:.2f}, Breite {hi[0] - lo[0]:.2f}, Hoehe {hi[1]:.2f}; "
                        f"Armbrust samt Sehne: x {bl[0]:.2f}..{bh[0]:.2f}, Breite {bh[0] - bl[0]:.2f}, "
                        f"y {bl[1]:.2f}..{bh[1]:.2f}, z {bl[2]:.2f}..{bh[2]:.2f}\n")
        f.write(f"Farben in der Textur: ruhe {colours['ruhe']}, angriff {colours['angriff']} (Grenze 32)\n")
        f.write("Bogenpunkte F und D in Ruhe und Ankuendigung identisch, alle auf einer Hoehe (im Skript geprueft)\n")
        f.write("Silhouette aus 10 m im Spiel (854x480, ohne Raum): Figur, Haltung: Pixelflaeche, Breite px, Hoehe px, Helligkeit 0..255\n")
        for (k, pose), m in meas.items():
            f.write(f"  {k} {pose}: {m[0]} px, {m[1]} x {m[2]}, Helligkeit {m[3]:.1f}\n")

    # 3. Blatt
    M, ROWH = 30, 650
    W = 2480
    head = 190
    y3 = head + len(VERS) * ROWH + 10
    H = y3 + 1900
    sheet = np.zeros((H, W, 3), np.float32) + np.array(FOG_COLOR, np.float32)    # noqa: F821
    text(sheet, "Skelett mit Armbrust, Probestück zu Issue 334, Runde 4: flacher Holzbogen, nur die Sehne spannt", M, 12, 46, bold=True)
    text(sheet, "Urteil: \"B1, das Model soll sich aber nicht nach oben biegen. Es reicht, wenn sich die Sehne spannt.\" "
                "Oben B1 (Runde 3), darunter F flach und D deutlich.",
         M, 76, 28, (200, 190, 205))
    text(sheet, "Je Zeile links die Figur in Ruhe aus vier Blickwinkeln, rechts die Armbrust: obere Reihe in Ruhe, untere Reihe "
                "gespannt in der Ankündigung (schräg, von oben, von vorn).", M, 112, 28, (200, 190, 205))
    text(sheet, "Bei F und D ist der Bogen in beiden Haltungen dieselbe flache Form; es ändern sich nur Sehne und Bolzen. "
                "Rot nur in den Pupillen der Ankündigung.", M, 148, 28, (200, 190, 205))
    labels = {"B1": "B1 aus Runde 3 (Vergleich): Spitzen gehoben, in der Ankündigung stärker gebogen",
              "F": "F flach: Bogen waagerecht, Spitzen 0,18 m hinter der Mitte, in beiden Haltungen gleich",
              "D": "D deutlich: Bogen waagerecht, Spitzen 0,26 m hinter der Mitte, in beiden Haltungen gleich"}
    for i, v in enumerate(VERS):
        ry = head + i * ROWH
        t, lo, hi, bl, bh = stats[(v, "ruhe")]
        n = sum(t.values())
        na = t["armbrust"] + t.get("sehne", 0)
        text(sheet, f"{labels[v]}: {n} Dreiecke, Armbrust samt Sehne {na}, Bogen {bh[0] - bl[0]:.2f} m breit", M, ry, 30,
             GO[5] if v != "B1" else (200, 200, 210), True)                       # noqa: F821
        for j, (nm, _) in enumerate(dirs):
            blit(sheet, tiles[(v, nm)], M + j * TW, ry + 50)                       # noqa: F821
            text(sheet, nm, M + j * TW + 6, ry + 52, 24)
        x0 = M + 1220
        for r, pose in enumerate(POSES):
            for c, (nm, _) in enumerate(cdirs):
                px, py = x0 + c * (CW + 5), ry + 50 + r * (CH + 5)
                blit(sheet, close[(v, pose, nm)], px, py)                          # noqa: F821
                text(sheet, ("Ruhe " if pose == "ruhe" else "gespannt ") + nm, px + 6, py + 2, 26, GO[5], True)    # noqa: F821
    # unten links: Ankuendigung aus 4 m
    y = y3
    text(sheet, "Ankündigung aus 4 m im Spiel: B1 (Runde 3), F, D", M, y, 34, bold=True)
    blit(sheet, up(img4[200:390, 177:677], 2), M, y + 56)                          # noqa: F821
    for v, x in zip(VERS, x4):
        text(sheet, v, M + int((427 - 177 + x * 85.6) * 2) - 20, y + 60, 30, GO[5], True)    # noqa: F821
    ty = y + 56 + 380 + 30
    text(sheet, "Zahlen (aus 10 m: Fläche, Breite x Höhe in px, Helligkeit):", M, ty, 28, bold=True)
    for i, v in enumerate(VERS):
        t = stats[(v, "ruhe")][0]
        m = meas[(v, "ruhe")]
        ma = meas[(v, "ank")]
        text(sheet, f"{v}: gesamt {sum(t.values())} Dreiecke, Armbrust {t['armbrust']}, Sehne {t.get('sehne', 0)}",
             M, ty + 44 + i * 84, 26, GO[5], True)                                   # noqa: F821
        text(sheet, f"      aus 10 m, Ruhe: {m[0]} px, {m[1]}x{m[2]}, hell {m[3]:.0f}; Ankündigung: {ma[0]} px, "
                    f"{ma[1]}x{ma[2]}, hell {ma[3]:.0f}", M, ty + 44 + i * 84 + 38, 24)
    # unten rechts: 10 m
    rx = M + 1230
    z = 5
    crop = lambda im: im[200:304, 340:514]                                          # noqa: E731
    panels = (("Aus 10 m im Spiel, Ruhe (fünffach vergrößert): Ghul, B1, F, D", crop(img10["ruhe"])),
              ("Aus 10 m im Spiel, Ankündigung (fünffach vergrößert)", crop(img10["ank"])))
    py = y
    for title, im in panels:
        text(sheet, title, rx, py, 32, bold=True)
        blit(sheet, up(im, z), rx, py + 50)                                        # noqa: F821
        for k in ("G",) + VERS:
            px = rx + int(435 + xs[k] * 34.3 * z) - 24
            text(sheet, {"G": "Ghul"}.get(k, k), px, py + 52, 28, GO[5], True)      # noqa: F821
        py += 50 + 520 + 60
    lit = up(crop(img10["ank"]), z)                                                  # noqa: F821
    dark = np.clip(np.array(FOG_COLOR) + (lit - np.array(FOG_COLOR)) * 0.45, 0, 255)  # noqa: F821
    text(sheet, "Dasselbe dunkel (Helligkeit der Figuren auf 45 %), Ankündigung", rx, py, 32, bold=True)
    blit(sheet, dark, rx, py + 50)                                                    # noqa: F821
    save_png(sheet[:py + 50 + 520 + 30][::-1], os.path.join(OUT, "blatt.png"))       # noqa: F821
    print("FARBEN", colours)
    print("MESSUNG", meas)
    print("DREIECKE", {k: sum(v[0].values()) for k, v in stats.items()})


main4()
