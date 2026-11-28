"""Throwing Knives als Beute (Issue #819), Runde 3: a2 frei schwebend mit pulsierendem Schimmer in der Stufenfarbe.

Urteil zu Runde 2: Form a2 ist gewaehlt; Knoten und Band entfallen; die Stufe soll ein pulsierender Schimmer tragen,
als System fuer alle Beute. Dieses Skript legt drei Bauarten als Film vor (Spielbild 854 x 480, 20 Bilder je Sekunde,
5,25 s, nahtlos) und misst sie:

  A  Aufleuchten   das Stueck selbst: Oberflaeche zur Stufenfarbe gemischt (Uniform flash), am Gipfel ohne Abdunkeln
                   durch die Laterne (Uniform glow). Ein Stueck, das schon die Stufenfarbe hat (Item-Wuerfel), pulsiert
                   stattdessen in der Helligkeit.
  B  Bodenschein   Licht auf dem Boden darunter: runde Scheibe aus Punkten in der Stufenfarbe (Punktmuster 4 x 4 wie
                   pattern in level.fs, an Bildpunkte geheftet), drei Ringe, der Anteil der Punkte pulsiert.
                   Unbeleuchtet, im Nebel. Zwischen den Punkten bleibt der Boden sichtbar.
  C  Funken        kleine unbeleuchtete Wuerfel in der Stufenfarbe steigen um das Stueck auf (wie ShadedBox.DrawGlowing).
  heute            Item-Wuerfel mit Leuchtplatte wie PickupView.DrawEquipment (nur gelesen), zum Vergleich.

Je Bauart ein Film mit a2 und einer mit dem Item-Wuerfel ohne Platte: vorn 3 m, hinten 8 m, von links Worn, Forged,
Royal, Vampiric (Farben aus ChestView.Colours; Vampiric in der Mitte seines eigenen Pulses).

Bewegung wie der Item-Wuerfel im Spiel, auf die Filmlaenge gerundet: eine Drehung in 5,25 s (Spiel 5,24 s), Auf und
Ab +-0,04 m in 2,625 s (Spiel 2,51 s).

Bild: Durchgaenge Grundfarbe, Licht, Entfernung aus Blender (psx), zusammengesetzt mit den Formeln und Werten von
assets/shaders/level.fs und LookSettings (Laterne mindestens 0,35, Nebel 3,25 bis 18,2 m, 15 Bit mit 4 x 4-Muster).
Der Bodenschein ist als Kreis auf der Bodenebene je Bildpunkt gerechnet, nicht als Netz.

Gemessen wird je Stueck: Bildpunkte, die sich um mindestens 16 (zwei Stufen von 32 je Kanal) vom selben Bild ohne
Stufenfarbe unterscheiden - bei a2 vom a2 ohne Schimmer, beim Wuerfel vom leeren Gang.

Schreibt schimmer-*.gif, schimmer-819-runde-3.png und schimmer-zahlen.txt. ffmpeg muss im Pfad liegen.
Aufruf: "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python schimmer.py
"""

import math
import os
import shutil
import subprocess
import sys

import numpy as np

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "runde-2"))
import beute2 as R2  # noqa: E402
import psx  # noqa: E402

S, R5, B, V, Matrix, bpy = R2.S, R2.R5, R2.B, R2.V, R2.Matrix, R2.bpy

FPS, FRAMES = 20, 105
LOOP = FRAMES / FPS
STEP = int(os.environ.get("SCHIMMER_STEP", "1"))          # nur zum Probieren: jedes n-te Bild
TIERS, XS, DISTS = R2.TIERS, R2.XS, R2.DISTS
W, H = B.GAME
DARK = np.array(R5.DARK, float)
P_A, P_B, P_C, P_TODAY, P_BOB = LOOP / 3, LOOP / 2, LOOP / 3, LOOP / 2, LOOP / 2
FLASH = (0.20, 0.70)                                      # A: Anteil der Stufenfarbe auf der Oberflaeche
DIM = (0.60, 1.00)                                        # A am Wuerfel: Helligkeit der Stufenfarbe
SHARE = (4, 12)                                           # B: Punkte von 16 im innersten Ring
RINGS = ((0.16, 1.0), (0.27, 0.6), (0.36, 0.3))           # B: Radius in m, Anteil des Rings am innersten
LIFT = 0.015                                              # B: so hoch ueber dem Boden (wie die Reifscheibe)
MOTES, MOTE_LIFE, MOTE_R = 6, 0.8, 0.25                   # C: Funken je Stueck, sichtbarer Teil des Aufstiegs, Radius
MOTE_SIZE = (0.05, 0.03)                                  # C: Kante unten, Kante ab halber Hoehe
SEEN = 16.0                                               # ab diesem Farbabstand zaehlt ein Bildpunkt
FILMS = [("heute", "heute: Item-Wuerfel mit Leuchtplatte"), ("ohne", "a2 ohne Schimmer"),
         ("a-a2", "A Aufleuchten, a2"), ("a-wuerfel", "A Aufleuchten, Item-Wuerfel"),
         ("b-a2", "B Bodenschein, a2"), ("b-wuerfel", "B Bodenschein, Item-Wuerfel"),
         ("c-a2", "C Funken, a2"), ("c-wuerfel", "C Funken, Item-Wuerfel")]
RESERVED = [("Reif hell", (168, 181, 199)), ("Kaelte", (126, 145, 178)), ("Reif dunkel", (86, 107, 154)),
            ("Betaeubung Arme", (163, 175, 187)), ("kritischer Treffer", (230, 40, 50)), ("Warnung Gorm", (200, 30, 30)),
            ("Glut Gorm", (255, 120, 60)), ("Muenze", (150, 156, 170)), ("Schluessel", (255, 214, 120))]


def wave(t, period):
    return 0.5 - 0.5 * math.cos(2 * math.pi * t / period)


def step_a(t):
    """A: vier harte Stufen 0, 1/3, 2/3, 1."""
    return round(wave(t, P_A) * 3) / 3


def share_b(t):
    return (SHARE[0] + round(wave(t, P_B) * (SHARE[1] - SHARE[0]))) / 16


def glow_today(t):
    return 0.35 + 0.25 * math.sin(2 * math.pi * t / P_TODAY)


def motes(t):
    """C: (Hoehe, Winkel in Grad, Kante) je sichtbarem Funken."""
    out = []
    for i in range(MOTES):
        u = (t / P_C + i / MOTES) % 1.0
        if u < MOTE_LIFE:
            out.append((0.06 + 0.9 * u, 150.0 * i + 150.0 * u, MOTE_SIZE[0] if u < MOTE_LIFE / 2 else MOTE_SIZE[1]))
    return out


def grab(tag, visible, which=("base", "light", "dist")):
    """Durchgaenge als Felder, Zeile 0 oben: Farbe 0..255, Licht, Entfernung, Deckung."""
    scene = bpy.context.scene
    show = set(visible)
    for o in scene.objects:
        if o.type == "MESH":
            o.hide_render = o not in show
    out = {}
    for name in which:
        psx.set_pass(name)
        path = os.path.join(psx.TMP, f"{tag}_{name}.exr")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(path, check_existing=False)
        buf = np.empty(W * H * 4, np.float32)
        img.pixels.foreach_get(buf)
        bpy.data.images.remove(img)
        out[name] = buf.reshape(H, W, 4)[::-1].astype(float)
    res = {"cov": out["dist"][..., 3] > 0.5, "d": out["dist"][..., 0]}
    if "base" in out:
        res["base"] = out["base"][..., :3] * 255
    if "light" in out:
        res["light"] = out["light"][..., :1]
    return res


def lantern(d, glow=0.0):
    return np.maximum(1 - (1 - psx.LANTERN_MINIMUM) * np.clip(d / psx.FOG_END, 0, 1), glow)[..., None]


def over(pic, lit, d, cov):
    m = cov & (d < pic[1])
    return np.where(m[..., None], lit, pic[0]), np.where(m, d, pic[1])


def finish(pic, thr):
    t = np.clip((pic[1] - psx.FOG_START) / (psx.FOG_END - psx.FOG_START), 0, 1)
    fog = (t * t * (3 - 2 * t))[..., None]
    col = pic[0] * (1 - fog) + psx.FOG_COLOR * fog
    return np.clip(np.floor((col + (thr[..., None] - 0.5) * 8) / 8) * 8 + 4, 0, 255)


def main():
    os.makedirs(psx.TMP, exist_ok=True)
    s = S.setup()
    B.place([o for g in s.knives + s.wraps for o in g] + s.sleeve + s.blade, B.AWAY)
    src = s.knives[0]
    build = R2.stich(0.0)
    scene = bpy.context.scene

    # --- Stuecke ------------------------------------------------------------------------------------------------------
    spots = [(i, dist) for i in range(4) for dist in DISTS]
    trios = {k: R2.trio(src, f"s{k[0]}_{k[1]}") for k in spots}
    knives = [o for gs in trios.values() for g in gs for o in g]
    cubes = {k: R5.flat(f"wuerfel_{k[0]}_{k[1]}", (255, 255, 255), (0.16, 0.16, 0.16)) for k in spots}
    plates = []
    for i, dist in spots:
        o = R5.flat(f"platte_{i}_{dist}", (255, 255, 255), (0.45, 0.01, 0.45))
        B.place([o], B.world((XS[i], 0.005, B.EYE.z + dist)))
        plates.append(o)
    mote = R5.flat("funke", (255, 255, 255), (1.0, 1.0, 1.0))
    sparks = {k: [] for k in spots}
    for k in spots:
        for j in range(MOTES):
            c = mote.copy()
            c.name = f"funke_{k[0]}_{k[1]}_{j}"
            scene.collection.objects.link(c)
            sparks[k].append(c)
    B.place([mote], B.AWAY)
    all_sparks = [o for v in sparks.values() for o in v]

    # --- Fester Teil: Gang, Platten, Fenster, Bodenebene ------------------------------------------------------------
    B.player(list(s.room), s.cam)                        # stellt Kamera und Bildgroesse ein
    room = grab("gang", list(s.room))
    plate = grab("platte", plates)
    scene.render.use_border, scene.render.use_crop_to_border = True, False
    scene.render.border_min_x, scene.render.border_max_x = 0.0, 1.0
    scene.render.border_min_y, scene.render.border_max_y = 1 - 460 / H, 1 - 236 / H
    ys, xs = np.mgrid[0:H, 0:W]
    thr = psx.BAYER4[ys % 4, xs % 4]
    wins, tidx = {}, -np.ones((H, W), int)
    for i, dist in spots:
        wins[(i, dist)] = R2.window(XS[i], dist)
        tidx[wins[(i, dist)]] = i
    tc = np.zeros((H, W, 3))
    for i, (_n, colour) in enumerate(TIERS):
        tc[tidx == i] = colour
    slope = (H / 2 - (ys + 0.5)) / B.FOCAL
    dz = np.where(slope < -1e-6, (LIFT - B.EYE.y) / np.minimum(slope, -1e-6), 1e6)
    fx, fz = (W / 2 - (xs + 0.5)) / B.FOCAL * dz, B.EYE.z + dz
    fd = np.sqrt(fx * fx + (B.EYE.y - LIFT) ** 2 + dz * dz)
    ring = np.zeros((H, W))
    for i, dist in spots:
        r = np.hypot(fx - XS[i], fz - (B.EYE.z + dist))
        for radius, part in reversed(RINGS):
            ring = np.where(r < radius, part, ring)
    empty_pic = (room["base"] * room["light"] * lantern(room["d"]), np.where(room["cov"], room["d"], 100.0))
    empty = finish(empty_pic, thr)

    def measure(img, ref):
        dd = np.sqrt(((img - ref) ** 2).sum(2))
        out = np.zeros((4, 2, 5))
        for (i, dist), win in wins.items():
            m = dd[win] >= SEEN
            if m.any():
                out[i, DISTS.index(dist)] = (m.sum(), dd[win][m].mean(), *img[win][m].mean(0))
        return out

    # --- Bilder -------------------------------------------------------------------------------------------------------
    frames = list(range(0, FRAMES, STEP))
    raw = {key: open(os.path.join(psx.TMP, f"schimmer_{key}.rgb"), "wb") for key, _n in FILMS}
    stat = {key: np.zeros((len(frames), 4, 2, 5)) for key, _n in FILMS}
    colours = {key: set() for key, _n in FILMS}
    for n, f in enumerate(frames):
        t = f / FPS
        yaw, bob = 360.0 * t / LOOP, 0.04 * math.sin(2 * math.pi * t / P_BOB)
        for (i, dist), gs in trios.items():
            R2.stand(gs, build, (XS[i], R2.UNDER + bob, B.EYE.z + dist), yaw)
        for (i, dist), o in cubes.items():
            B.place([o], B.world((XS[i], 0.2 + bob, B.EYE.z + dist)) @ B.rot(yaw, "Z"))
        seen = motes(t)
        for (i, dist), group in sparks.items():
            for j, o in enumerate(group):
                if j < len(seen):
                    y, angle, size = seen[j]
                    a = math.radians(angle)
                    p = (XS[i] + MOTE_R * math.cos(a), y, B.EYE.z + dist + MOTE_R * math.sin(a))
                    o.matrix_world = B.world(p) @ Matrix.Scale(size, 4)
                else:
                    o.matrix_world = B.AWAY
        bpy.context.view_layer.update()
        k = grab("messer", knives)
        q = grab("wuerfel", list(cubes.values()))
        m = grab("funken", all_sparks, ("base", "dist"))

        step = step_a(t)
        flash = FLASH[0] + (FLASH[1] - FLASH[0]) * step
        dim = DIM[0] + (DIM[1] - DIM[0]) * step
        knife_plain = (k["base"] * k["light"] * lantern(k["d"]), k["d"], k["cov"])
        knife_a = ((k["base"] * (1 - flash) + tc * flash) * k["light"] * lantern(k["d"], step), k["d"], k["cov"])
        cube_plain = (tc * q["light"] * lantern(q["d"]), q["d"], q["cov"])
        cube_a = (tc * dim * q["light"] * lantern(q["d"], step), q["d"], q["cov"])
        disc = (tc, fd, (thr < ring * share_b(t)) & (tidx >= 0))
        spark = (tc, m["d"], m["cov"])
        slab = ((DARK + (tc - DARK) * glow_today(t)) * plate["light"] * lantern(plate["d"]), plate["d"], plate["cov"])
        layers = {"heute": [slab, cube_plain], "ohne": [knife_plain], "a-a2": [knife_a], "a-wuerfel": [cube_a],
                  "b-a2": [disc, knife_plain], "b-wuerfel": [disc, cube_plain],
                  "c-a2": [spark, knife_plain], "c-wuerfel": [spark, cube_plain]}
        done = {}
        for key, _name in FILMS:
            pic = empty_pic
            for layer in layers[key]:
                pic = over(pic, *layer)
            done[key] = finish(pic, thr)
            bytes_ = done[key].astype(np.uint8)
            raw[key].write(bytes_.tobytes())
            packed = (bytes_[..., 0].astype(np.int64) << 16) | (bytes_[..., 1].astype(np.int64) << 8) | bytes_[..., 2]
            colours[key].add(len(np.unique(packed)))      # Farben je Bild: ein GIF-Bild fasst 256
        for key, _name in FILMS:
            stat[key][n] = measure(done[key], done["ohne"] if key.endswith("a2") else empty)
        print("SCHIMMER Bild", f, flush=True)
    for fh in raw.values():
        fh.close()

    # --- Zahlen -------------------------------------------------------------------------------------------------------
    lines = [f"Throwing Knives als Beute (#819), Runde 3: Schimmer in der Stufenfarbe. Spielbild 854 x 480, Auge 1,6 m, {len(frames)} Bilder, {FPS / STEP:.0f} je Sekunde.",
             f"A Aufleuchten: Puls {P_A:.2f} s in vier Stufen; a2 Stufenfarbe {FLASH[0]:.2f} bis {FLASH[1]:.2f} der Oberflaeche, Wuerfel Helligkeit {DIM[0]:.2f} bis {DIM[1]:.2f}; Laterne am Gipfel aus (glow 0 bis 1).",
             f"B Bodenschein: Puls {P_B:.3f} s; Punkte {SHARE[0]} bis {SHARE[1]} von 16 im innersten Ring; Ringe " + ", ".join(f"bis {r:.2f} m x {p:.1f}" for r, p in RINGS) + ".",
             f"C Funken: {MOTES} je Stueck, Aufstieg {P_C:.2f} s von 0,06 auf {0.06 + 0.9 * MOTE_LIFE:.2f} m, Kreis {MOTE_R:.2f} m, Kante {MOTE_SIZE[0]:.2f} m, ab halber Hoehe {MOTE_SIZE[1]:.2f} m.",
             f"heute: Platte 0,45 x 0,45 m, Mischung Dunkel -> Stufe 0,10 bis 0,60, Puls hier {P_TODAY:.3f} s (Spiel 2,09 s).",
             f"Gezaehlt: Bildpunkte mit Farbabstand >= {SEEN:.0f} zum Bild ohne Stufenfarbe (a2: a2 ohne Schimmer; Wuerfel und heute: leerer Gang).",
             "Dunkelstes und hellstes Bild: kleinste und groesste Summe (Bildpunkte x mittlerer Farbabstand) der vier Stufen in 8 m.", ""]
    summary = {}
    for key, name in FILMS:
        if key == "ohne":
            continue
        st = stat[key]
        energy = (st[:, :, 1, 0] * st[:, :, 1, 1]).sum(1)
        lo, hi = int(energy.argmin()), int(energy.argmax())
        summary[key] = {"lo": lo, "hi": hi, "px": {}, "gap": {}}
        lines.append(f"{name} (dunkelstes Bild {frames[lo]}, hellstes Bild {frames[hi]}; hoechstens {max(colours[key])} Farben je Bild)")
        for d, dist in enumerate(DISTS):
            row = []
            for i, (tname, _c) in enumerate(TIERS):
                a, b = st[lo, i, d], st[hi, i, d]
                row.append(f"{tname} {a[0]:.0f} px (Abstand {a[1]:.0f}) / {b[0]:.0f} px (Abstand {b[1]:.0f})")
                summary[key]["px"][(i, dist)] = (int(a[0]), int(b[0]), int(st[:, i, d, 0].min()), int(st[:, i, d, 0].max()))
            lines.append(f"  {dist:.0f} m, dunkel / hell: " + "; ".join(row))
            lines.append(f"  {dist:.0f} m, wenigste / meiste Bildpunkte im ganzen Puls: " + "; ".join(
                f"{TIERS[i][0]} {summary[key]['px'][(i, dist)][2]} / {summary[key]['px'][(i, dist)][3]}" for i in range(4)))
            for which, n in (("dunkel", lo), ("hell", hi)):
                cols = [st[n, i, d, 2:5] for i in range(4) if st[n, i, d, 0] > 0]
                names = [TIERS[i][0] for i in range(4) if st[n, i, d, 0] > 0]
                pairs = sorted((float(np.linalg.norm(cols[a] - cols[b])), names[a], names[b])
                               for a in range(len(cols)) for b in range(a + 1, len(cols)))
                if pairs:
                    summary[key]["gap"][(dist, which)] = pairs[0]
                    lines.append(f"  {dist:.0f} m, {which}: mittlere Farbe je Stufe " + ", ".join(
                        f"{nm} ({c[0]:.0f}, {c[1]:.0f}, {c[2]:.0f})" for nm, c in zip(names, cols))
                        + f"; kleinster Abstand zweier Stufen {pairs[0][0]:.0f} ({pairs[0][1]} / {pairs[0][2]})")
                else:
                    lines.append(f"  {dist:.0f} m, {which}: keine Stufe sichtbar")
        lines.append("")
    lines.append("Stufenfarben gegen vergebene Effektfarben (ohne Nebel), naechste Farbe und Abstand:")
    for tname, c in TIERS:
        near = sorted((float(np.linalg.norm(np.array(c, float) - np.array(r, float))), rn) for rn, r in RESERVED)
        lines.append(f"  {tname} {c}: " + ", ".join(f"{rn} {dd:.0f}" for dd, rn in near[:3]))
    with open(os.path.join(HERE, "schimmer-zahlen.txt"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines) + "\n")
    print("\n".join("SCHIMMER " + x for x in lines))

    # --- Filme --------------------------------------------------------------------------------------------------------
    ffmpeg = shutil.which("ffmpeg")
    for key, _name in FILMS:
        if ffmpeg is None:
            print("SCHIMMER ffmpeg fehlt im Pfad, kein Film")
            break
        # Eine Palette fuer den ganzen Film (256 Farben, ohne Rasterung), gespeichert wird je Bild nur das geaenderte
        # Rechteck. Eine Palette je Bild waere farbgenau, macht aber 20 MB je Film statt rund 1 MB.
        graph = ("split[a][b];[a]palettegen=max_colors=256:stats_mode=full:reserve_transparent=0[p];"
                 "[b][p]paletteuse=dither=none:diff_mode=rectangle")
        res = subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}",
                              "-r", str(FPS / STEP), "-i", os.path.join(psx.TMP, f"schimmer_{key}.rgb"),
                              "-filter_complex", graph, "-loop", "0", os.path.join(HERE, f"schimmer-{key}.gif")],
                             capture_output=True, text=True)
        print("SCHIMMER Film", key, res.returncode, res.stderr.strip()[:300],
              os.path.getsize(os.path.join(HERE, f"schimmer-{key}.gif")) if res.returncode == 0 else "")

    # --- Blatt --------------------------------------------------------------------------------------------------------
    film = {key: np.memmap(os.path.join(psx.TMP, f"schimmer_{key}.rgb"), np.uint8, "r", shape=(len(frames), H, W, 3))
            for key, _n in FILMS}
    gap, lab, note_w = 8, 22, 520
    tw, th, big = 320, 328, 330
    show = 1                                              # Streifen zeigen Forged: die Stufe, die dem Stahl am naechsten ist
    width = max(4 * (W + gap), note_w + 8 * (tw + gap) + 2 * (big + gap))
    y1 = 34 + 2 * (lab + H + gap)
    strips = [k for k, _n in FILMS if k != "ohne"]
    sheet = np.zeros((y1 + lab + len(strips) * (th + gap) + 8, width, 3)) + psx.FOG_COLOR
    B.text(sheet, "Silver & Ash - Throwing Knives als Beute (#819), Runde 3: pulsierender Schimmer in der Stufenfarbe. Spielbild 854 x 480, Auge 1,6 m, "
           "Nebel und Laterne wie level.fs. Vorn 3 m, hinten 8 m; von links Worn, Forged, Royal, Vampiric. Bewegung: siehe Filme.", 6, 6, 16)
    top = [("heute", "ohne"), ("a-a2", "a-wuerfel"), ("b-a2", "b-wuerfel"), ("c-a2", "c-wuerfel")]
    names = dict(FILMS)
    for c, pair in enumerate(top):
        for r, key in enumerate(pair):
            n = summary[key]["hi"] if key in summary else 0
            x, y = c * (W + gap), 34 + r * (lab + H + gap)
            B.text(sheet, f"{names[key]} - Bild {frames[n]}" + (" (hellstes)" if key in summary else ""), x + 2, y, 14)
            B.paste(sheet, film[key][n].astype(float), x, y + lab)
    value = {"heute": lambda t: (glow_today(t) - 0.10) / 0.50, "a": step_a, "b": share_b,
             "c": lambda t: len(motes(t)) / MOTES}
    period = {"heute": P_TODAY, "a": P_A, "b": P_B, "c": P_C}
    need = {"heute": ["PickupView.DrawEquipment, wie im Spiel"],
            "a": ["Einbau: SetFlash und SetGlow je Stueck in PickupView,", "beide Uniformen gibt es (Treffer, Reif); keine neue Geometrie"],
            "b": ["Einbau: Scheibe als Netz (3 Ringe) mit SetPattern; neu im", "Shader: Bildpunkte ausserhalb des Musters verwerfen (2 Zeilen)"],
            "c": ["Einbau: 4 bis 5 ShadedBox.DrawGlowing je Stueck,", "wie die Motes in StatusView; nichts Neues im Shader"]}
    B.text(sheet, f"Ein Puls in acht Bildern, {TIERS[show][0]} in 8 m (8x); Balken = Staerke im Bild. Rechts: dunkelstes und hellstes Bild in 3 m (3x).", 6, y1, 14)
    for r, key in enumerate(strips):
        y = y1 + lab + r * (th + gap)
        kind = key.split("-")[0]
        sm = summary[key]
        notes = [names[key], f"Puls {period[kind]:.2f} s"] + need[kind]
        for dist in DISTS:
            notes.append(f"{dist:.0f} m, px dunkel / hell: " + ", ".join(
                f"{TIERS[i][0][0]} {sm['px'][(i, dist)][0]}/{sm['px'][(i, dist)][1]}" for i in range(4)))
        notes.append("8 m, wenigste px im Puls: " + ", ".join(f"{TIERS[i][0][0]} {sm['px'][(i, 8.0)][2]}" for i in range(4)))
        for which in ("dunkel", "hell"):
            g = sm["gap"].get((8.0, which))
            notes.append(f"8 m {which}: kleinster Farbabstand zweier Stufen " + (f"{g[0]:.0f} ({g[1][0]}/{g[2][0]})" if g else "-"))
        for j, note in enumerate(notes):
            B.text(sheet, note, 6, y + 6 + j * 20, 14 if j == 0 else 12)
        for j in range(8):
            t = j * period[kind] / 8
            n = min(range(len(frames)), key=lambda a: abs(frames[a] / FPS - t))
            c = B.pixel((XS[show], 0.30, B.EYE.z + 8.0))
            x = note_w + j * (tw + gap)
            B.paste(sheet, B.up_n(B.crop(film[key][n].astype(float), c, 40, 41), 8), x, y)
            sheet[y + th - 8:y + th, x:x + max(2, int(tw * value[kind](frames[n] / FPS)))] = TIERS[show][1]
        for j, n in enumerate((sm["lo"], sm["hi"])):
            c = B.pixel((XS[show], 0.30, B.EYE.z + 3.0))
            B.paste(sheet, B.up_n(B.crop(film[key][n].astype(float), c, 110, 109), 3), note_w + 8 * (tw + gap) + j * (big + gap), y)
    S.save_png(sheet[::-1], os.path.join(HERE, "schimmer-819-runde-3.png"))
    print("SCHIMMER Blatt geschrieben:", sheet.shape[1], "x", sheet.shape[0])


if __name__ == "__main__":
    main()
