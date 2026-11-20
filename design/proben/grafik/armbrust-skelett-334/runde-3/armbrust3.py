"""Probestueck Skelett mit Armbrust (Issue #334), Runde 3: nur der Bogen der Armbrust von A2 ist neu.

Urteil des Menschen zu Runde 2: "A2, aber der Bogen von der Armbrust muss nochmal ueberarbeitet werden (soll auch ein Bogen
sein)". Die Figur A2 bleibt Punkt fuer Punkt wie in Runde 2 (dieses Skript fuehrt armbrust2.py aus und ersetzt nur die Funktion
`crossbow` sowie `build`, damit die Sehne ein eigenes Teil wird). Drei Fassungen des Bogens:
  B1 schlichter Holzbogen, gleichmaessig gebogen, Mitte dick, Enden duenn
  B2 Stahlbogen, flacher und breiter, mit Wicklung in der Mitte
  B3 Recurve (Hornbogen): Enden schwingen wieder nach vorn und hoch, am auffaelligsten
Die Schenkel sind weit nach hinten zum Schuetzen gebogen (Sehne zwischen den Spitzen, in der Ankuendigung zur Nuss gezogen),
die Spitzen heben sich zusaetzlich nach oben, damit die Kurve auch von vorn lesbar bleibt.

Aufruf (aus dem Repository-Wurzelverzeichnis, TEMP/TMP auf einen Ordner ausserhalb von /tmp setzen):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/armbrust-skelett-334/runde-3/armbrust3.py
Umgebungsvariable ARMBRUST3_OUT leitet die Ausgabe in einen anderen Ordner (fuer die Pruefung zweier Laeufe).
Ausgabe: blatt.png, modelle/armbrust_skelett_<B1|B2|B3>_<ruhe|ank>.glb, texturen/atlas_*.png, masse.txt, spiel-*.png.
"""

import os
import sys

_HERE3 = os.path.dirname(os.path.abspath(__file__))
_R2 = os.path.join(os.path.dirname(_HERE3), "runde-2", "armbrust2.py")
_src = open(_R2, encoding="utf-8").read().rsplit("\nmain()", 1)[0]
__file__ = _R2                                   # HERE und ROOT der Runde 2 (Wurzel des Repositorys) richtig ableiten
exec(compile(_src, _R2, "exec"), globals())      # noqa: S102
OUT = os.environ.get("ARMBRUST3_OUT") or _HERE3
HERE = OUT

_old_crossbow = crossbow  # noqa: F821
CUR = {"ver": "A2", "pose": "ruhe", "sehne": None}
VERS = ("A2", "B1", "B2", "B3")

# Bogen: a = 0..1 vom Mittelpunkt zur Spitze. Zahlenpaare = (Ruhe, Ankuendigung).
BOWS = {
    "B1": dict(mat="wood", tx=(0.52, 0.49), back=(0.20, 0.33), lift=(0.12, 0.15), y0=0.012,
               prof=lambda a: a ** 2, yprof=lambda a: a ** 2, a=(0, 0.2, 0.4, 0.6, 0.8, 1.0),
               w=(0.036, 0.013), v=(0.036, 0.013), sides=6, off=0.0),
    "B2": dict(mat="iron", tx=(0.56, 0.53), back=(0.14, 0.26), lift=(0.09, 0.12), y0=0.012,
               prof=lambda a: a ** 2.4, yprof=lambda a: a ** 2, a=(0, 0.25, 0.5, 0.75, 0.9, 1.0),
               w=(0.058, 0.022), v=(0.024, 0.011), sides=4, off=0.0),
    "B3": dict(mat="bone_dark", tx=(0.52, 0.49), back=(0.18, 0.28), lift=(0.16, 0.20), y0=0.012,
               prof=lambda a: (2.0 * a * a - 1.3 * a ** 4) / 0.7, yprof=lambda a: a ** 3,
               a=(0, 0.2, 0.4, 0.6, 0.75, 0.88, 1.0), w=(0.034, 0.014), v=(0.034, 0.014), sides=4, off=0.055),
}


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
    """Rohrstueck des Schenkels zwischen den Parametern u0 und u1 (etwas dicker: Wicklung, Ring, Kappe), Enden laufen spitz zu."""
    d = (u1 - u0) / 6
    us = [u0 + d, u1 - d]
    pts = [X.p(limb_point(cfg, k, u, zb)) for u in us]
    shapes = [(lerp(cfg["w"], abs(u)) + grow, lerp(cfg["v"], abs(u)) + grow, lerp(cfg["v"], abs(u)) + grow) for u in us]
    P.loft(tube(pts, shapes, cfg["sides"], ref_up=X.d((0, 1, 0))), mat)


def crossbow3(P, X, lv, long=0.80, span=0.40, tip=0.15):
    ver, pose = CUR["ver"], CUR["pose"]
    if ver == "A2":
        return _old_crossbow(P, X, lv, long, span, tip)
    cfg = BOWS[ver]
    k = 1 if pose == "ank" else 0
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
    # Wicklungen und Beschlaege je Fassung
    if ver == "B1":
        limb_tube(P, X, cfg, k, zb, -0.14, 0.14, 0.009, "leather")
        for s in (1, -1):
            limb_tube(P, X, cfg, k, zb, s * 0.50, s * 0.56, 0.006, "leather")
    elif ver == "B2":
        limb_tube(P, X, cfg, k, zb, -0.22, 0.22, 0.008, "leather")                # Wicklung in der Mitte
        for s in (1, -1):
            limb_tube(P, X, cfg, k, zb, s * 0.22, s * 0.255, 0.012, "iron")         # Eisenring neben der Wicklung
            limb_tube(P, X, cfg, k, zb, s * 0.90, s * 1.0, 0.011, "rust")           # Endkappe
    else:
        limb_tube(P, X, cfg, k, zb, -0.10, 0.10, 0.010, "iron")                    # Beschlag in der Mitte
        for s in (1, -1):
            limb_tube(P, X, cfg, k, zb, s * 0.30, s * 0.46, 0.008, "leather")
    # Sehne: eigenes Teil, von Spitze zu Spitze, Ruhe gerade im Grundriss, Ankuendigung zur Nuss gezogen
    tipL, tipR = limb_point(cfg, k, -1.0, zb), limb_point(cfg, k, 1.0, zb)
    endL, endR = (tipL[0], tipL[1], tipL[2] - cfg["off"]), (tipR[0], tipR[1], tipR[2] - cfg["off"])
    zs = endL[2] if pose == "ruhe" else 0.34
    centre = (0.0, 0.03, zs)
    S = CUR["sehne"]
    for a, b in ((endL, centre), (centre, endR)):
        a2, b2 = inset(a, b)
        bone(S, [a2, b2], (0.012, 0.012), "bone", sides=4, X=X)
    if cfg["off"] > 0:                                                              # Hornspitzen
        for s, t, e in ((-1, tipL, endL), (1, tipR, endR)):
            xspike(P, X, t, e, 0.016, "iron")
            tail = (t[0] + s * 0.05, t[1] + 0.05, t[2] + 0.01)
            xspike(P, X, t, tail, 0.018, "iron")
    # Steigbuegel
    xb((-0.05, -0.06, long - 0.03), (0.05, 0.01, long + 0.035), "iron")
    # Nuss und Abzug
    xb((-0.012, 0.0, 0.32), (0.012, 0.03, 0.35), "bone")
    xb((-0.008, -0.07, 0.30), (0.008, -0.034, 0.325), "iron")
    # Bolzen mit Spitze (Ruhe: hinten an der Sehne, Ankuendigung: auf der Nuss)
    z0 = 0.35 if pose == "ank" else zs + 0.02
    bone(P, [(0, 0.03, z0), (0, 0.03, long + 0.12)], (0.014, 0.014), "wood", sides=4, X=X)
    xspike(P, X, (0, 0.03, long + 0.12), (0, 0.03, long + 0.22), 0.026, "iron")


crossbow = crossbow3


def build3(stage, pose, ver):
    """Wie build() der Runde 2, aber mit eigenem Teil "sehne" (ausser bei A2: dort steckt die Sehne in der Armbrust)."""
    CUR.update(ver=ver, pose=pose)
    sehne = Part("sehne", (0, 0, 0), UV)             # noqa: F821
    sehne.flat = True
    CUR["sehne"] = sehne
    parts = build(stage, pose)                        # noqa: F821
    return parts if ver == "A2" else parts + [sehne]


def main3():
    global SCRATCH
    SCRATCH = os.environ.get("TEMP", tempfile.gettempdir())
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
            objs = put_in_scene(build3("A2", pose, ver), mat, f"exp_{ver}_{pose}")
            if ver != "A2":
                export_glb(objs, os.path.join(OUT, "modelle", f"armbrust_skelett_{ver}_{pose}.glb"))
            lo, hi = extent(objs)
            bl, bh = extent([o for o in objs if o.name in ("armbrust", "sehne")])
            stats[(ver, pose)] = (tri_count(objs), lo, hi, bl, bh)
            for o in objs:
                bpy.data.objects.remove(o, do_unlink=True)
    assert sum(stats[("A2", "ruhe")][0].values()) == 1518, stats[("A2", "ruhe")][0]

    # 2. Szenen
    pm = {k: pass_material("pm_" + k, images[k]) for k in images}
    sets = {(v, pose): put_in_scene(build3("A2", pose, v), pm[atlas_of[pose]], f"r_{v}_{pose}") for v in VERS for pose in POSES}
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

    # 2b. Nahaufnahmen des Bogens: schraeg, von oben, von vorn; Ruhe und Ankuendigung
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
    xs = {"G": -1.9, "A2": -0.95, "B1": 0.0, "B2": 0.95, "B3": 1.9}
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

    # 2d. Ankuendigung aus 4 m: A2, B1, B2, B3
    park_all()
    ann = []
    for v, x in zip(VERS, (-2.1, -0.7, 0.7, 2.1)):
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
    text(sheet, "Skelett mit Armbrust, Probestück zu Issue 334, Runde 3: der Bogen der Armbrust", M, 12, 46, bold=True)
    text(sheet, "Oben A2 aus Runde 2 zum Vergleich (gerader Stab). Darunter B1, B2, B3: dieselbe Figur, nur der Bogen ist neu.",
         M, 76, 28, (200, 190, 205))
    text(sheet, "Je Zeile links die Figur in Ruhe aus vier Blickwinkeln, rechts der Bogen: obere Reihe in Ruhe, untere Reihe "
                "gespannt in der Ankündigung (schräg, von oben, von vorn).", M, 112, 28, (200, 190, 205))
    text(sheet, "Rot nur in den Pupillen der Ankündigung. Die Sehne ist ein eigenes Teil von Spitze zu Spitze.", M, 148, 28,
         (200, 190, 205))
    labels = {"A2": "A2 Runde 2 (Vergleich): gerader Stab", "B1": "B1 Holzbogen: gleichmäßig gebogen",
              "B2": "B2 Stahlbogen: flacher und breiter, Wicklung", "B3": "B3 Recurve: Enden schwingen nach vorn und hoch"}
    for i, v in enumerate(VERS):
        ry = head + i * ROWH
        t, lo, hi, bl, bh = stats[(v, "ruhe")]
        n = sum(t.values())
        na = t["armbrust"] + t.get("sehne", 0)
        text(sheet, f"{labels[v]}: {n} Dreiecke, Armbrust samt Sehne {na}, Bogen {bh[0] - bl[0]:.2f} m breit", M, ry, 32,
             GO[5] if v != "A2" else (200, 200, 210), True)                       # noqa: F821
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
    text(sheet, "Ankündigung aus 4 m im Spiel: A2, B1, B2, B3", M, y, 34, bold=True)
    blit(sheet, up(img4[200:390, 177:677], 2), M, y + 56)                          # noqa: F821
    for i, v in enumerate(VERS):
        text(sheet, v, M + int((427 - 177 + (-2.1 + 1.4 * i) * 85.6) * 2) - 20, y + 60, 30, GO[5], True)    # noqa: F821
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
    panels = (("Aus 10 m im Spiel, Ruhe (fünffach vergrößert): Ghul, A2, B1, B2, B3", crop(img10["ruhe"])),
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


main3()
