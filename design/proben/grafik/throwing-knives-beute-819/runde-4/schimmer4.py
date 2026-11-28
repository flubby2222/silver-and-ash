"""Throwing Knives als Beute (Issue #819), Runde 4: Aufleuchten, Bodenschein und Funken zusammen an einem Stueck.

Urteil zu Runde 3: alle drei Bauarten zusammen. A mit fluessigerem Uebergang, B bei Royal schwaecher, C kleiner,
zurueckhaltender, ein paar mehr. Der Schimmer ersetzt die eckige Leuchtplatte fuer alle Beute mit Stufe.

  A  Aufleuchten   wie Runde 3, aber 8 statt 4 Stufen im Puls; der groesste Sprung zwischen zwei Stufen wird fuer
                   4 bis 16 Stufen gemessen. Ein zweiter Film zeigt 16 Stufen.
  B  Bodenschein   wie Runde 3, aber je Stufe ein eigener Faktor auf den Punktanteil, so dass die gemessene Staerke
                   (Summe des gewichteten Farbabstands zum Boden) bei allen vier Stufen gleich ist.
  C  Funken        zwei Stufen der Zurueckhaltung: c1 = 9 Funken mit 3 cm (oben 2 cm), c2 = 12 Funken mit 2 cm.
                   Radius und Winkel je Funke verschieden. Die Funken sind als Quadrate ins Bild gerechnet.

Filme (Spielbild 854 x 480, 50 Bilder je Sekunde, 5,2 s, nahtlos): kombi-a2-c1, kombi-a2-c2, kombi-wuerfel-c1,
kombi-wuerfel-c2, heute (Wuerfel mit Platte), versetzt (a2, c1, jedes Stueck mit eigener Phase), dicht (a2, c1:
drei Stuecke 0,6 m nebeneinander, Vampiric mit eigenem Puls, Forged neben einer Frostscheibe; vorn 4 m, hinten 8 m).

Bild: Durchgaenge aus Blender (psx), zusammengesetzt mit den Formeln von assets/shaders/level.fs (nur gelesen).
Schreibt schimmer4-*.gif, schimmer-819-runde-4.png und schimmer4-zahlen.txt. ffmpeg muss im Pfad liegen.
Aufruf: blender --background --python schimmer4.py
"""

import math
import os
import shutil
import subprocess
import sys
from collections import deque

import numpy as np

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "runde-2"))
import beute2 as R2  # noqa: E402
import psx  # noqa: E402

S, R5, B, V, Matrix, bpy = R2.S, R2.R5, R2.B, R2.V, R2.Matrix, R2.bpy

FPS, FRAMES = 50, 260
LOOP = FRAMES / FPS
STEP = int(os.environ.get("SCHIMMER_STEP", "1"))
TIERS, XS = R2.TIERS, R2.XS
W, H = B.GAME
F = B.FOCAL
EYE_Y, EYE_Z = B.EYE.y, B.EYE.z
DARK = np.array(R5.DARK, float)
P_A, P_B, P_C, P_BOB, P_VAMP = LOOP / 3, LOOP / 2, LOOP / 3, LOOP / 2, LOOP / 2
FLASH, DIM = (0.20, 0.70), (0.60, 1.00)
CANDIDATES = (4, 6, 8, 10, 12, 16)                        # A: Stufenzahlen, deren groesster Sprung gemessen wird
N_A, N_FINE = 8, 16                                       # A: gewaehlte Stufenzahl; feinere Fassung als zweiter Film
SHARE = (4, 12)
RINGS = ((0.16, 1.0), (0.27, 0.6), (0.36, 0.3))
LIFT = 0.015
C_SPEC = {"c1": (9, 0.03, 0.02, 0.75), "c2": (12, 0.02, 0.02, 0.70)}   # Zahl, Kante unten, Kante oben, sichtbarer Teil
C_OLD = (6, 0.05, 0.03, 0.80)
SEEN = 16.0
LUMA = np.array((0.30, 0.59, 0.11))
VAMP = ((150, 20, 30), (200, 30, 40))                     # ChestView: Vampiric pulsiert zwischen diesen beiden
FROST = {"x": -1.7, "r": 0.62, "rim": 0.06, "cold": (126, 145, 178), "dark": (86, 107, 154), "rime": (168, 181, 199)}
FILMS = [("heute", "heute: Item-Wuerfel mit Leuchtplatte"), ("kombi-a2-c1", "A+B+C1, a2"), ("kombi-a2-c2", "A+B+C2, a2"),
         ("kombi-wuerfel-c1", "A+B+C1, Item-Wuerfel"), ("kombi-wuerfel-c2", "A+B+C2, Item-Wuerfel"),
         ("kombi-a2-c1-a16", "A+B+C1, a2, A mit 16 Stufen"), ("versetzt", "A+B+C1, a2, Phase je Stueck"), ("dicht", "A+B+C1, a2 dicht, Frostscheibe, Vampiric-Puls")]
QUIET = ["heute", "ohne", "nur-a", "nur-b", "nur-c1", "nur-c2", "nur-c-alt", "kombi-a2-c1", "kombi-a2-c2"]


def wave(t, period):
    return 0.5 - 0.5 * math.cos(2 * math.pi * t / period)


def level(t, n):
    return round(wave(t, P_A) * (n - 1)) / (n - 1)


def share_b(t):
    return (SHARE[0] + round(wave(t, P_B) * (SHARE[1] - SHARE[0]))) / 16


def glow_today(t):
    return 0.35 + 0.25 * math.sin(2 * math.pi * t / P_BOB)


def motes(t, spec, ph):
    """(Hoehe, Winkel, Kante, Radius) je sichtbarem Funken."""
    n, lo, hi, life = spec
    out = []
    for i in range(n):
        u = (t / P_C + i / n + ph) % 1.0
        if u < life:
            out.append((0.06 + 0.75 * u, math.radians(137.5 * i + 90.0 * u), lo if u < life / 2 else hi,
                        0.20 + 0.02 * ((i * 7) % 5)))
    return out


def grab(tag, visible, which=("base", "light", "dist")):
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
    return {"cov": out["dist"][..., 3] > 0.5, "d": out["dist"][..., 0], "base": out["base"][..., :3] * 255,
            "light": out["light"][..., :1]}


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


def window(x, dist, half):
    z = EYE_Z + dist
    cx, top = B.pixel((x, 0.85, z))
    _x, bottom = B.pixel((x, -0.15, z - 0.2))
    h = int(half / dist * F)
    return slice(max(top, 0), min(bottom, H)), slice(max(cx - h, 0), min(cx + h, W))


def pieces_main(ids):
    return [{"x": XS[i], "dist": dist, "tier": i, "id": (1 + i * 2 + d) if ids else 0, "vamp": False}
            for i in range(4) for d, dist in enumerate((3.0, 8.0))]


def pieces_dense():
    out = []
    for d, dist in enumerate((4.0, 8.0)):
        for j, (x, tier) in enumerate(((1.5, 0), (0.9, 2), (0.3, 3), (-0.9, 1))):
            out.append({"x": x, "dist": dist, "tier": tier, "id": 1 + d * 4 + j, "vamp": tier == 3})
    return out


def main():
    os.makedirs(psx.TMP, exist_ok=True)
    s = S.setup()
    B.place([o for g in s.knives + s.wraps for o in g] + s.sleeve + s.blade, B.AWAY)
    src = s.knives[0]
    build = R2.stich(0.0)
    scene = bpy.context.scene

    sets = {"main": (pieces_main(False), 0.5), "vers": (pieces_main(True), 0.5), "dicht": (pieces_dense(), 0.30)}
    trios = {k: [R2.trio(src, f"{k}{j}") for j in range(len(ps))] for k, (ps, _h) in sets.items()}
    cubes = [R5.flat(f"wuerfel{j}", (255, 255, 255), (0.16, 0.16, 0.16)) for j in range(8)]
    plates = []
    for j, p in enumerate(sets["main"][0]):
        o = R5.flat(f"platte{j}", (255, 255, 255), (0.45, 0.01, 0.45))
        B.place([o], B.world((p["x"], 0.005, EYE_Z + p["dist"])))
        plates.append(o)
    everything = [o for ts in trios.values() for gs in ts for g in gs for o in g] + cubes

    B.player(list(s.room), s.cam)
    B.place(everything, B.AWAY)
    room = grab("gang", list(s.room))
    plate = grab("platte", plates)
    scene.render.use_border, scene.render.use_crop_to_border = True, False
    scene.render.border_min_x, scene.render.border_max_x = 0.0, 1.0
    scene.render.border_min_y, scene.render.border_max_y = 1 - 460 / H, 1 - 236 / H
    ys, xs = np.mgrid[0:H, 0:W]
    thr = psx.BAYER4[ys % 4, xs % 4]
    slope = (H / 2 - (ys + 0.5)) / F
    dz = np.where(slope < -1e-6, (LIFT - EYE_Y) / np.minimum(slope, -1e-6), 1e6)
    fx, fz = (W / 2 - (xs + 0.5)) / F * dz, EYE_Z + dz
    fd = np.sqrt(fx * fx + (EYE_Y - LIFT) ** 2 + dz * dz)
    empty_pic = (room["base"] * room["light"] * lantern(room["d"]), np.where(room["cov"], room["d"], 100.0))
    empty = finish(empty_pic, thr)

    # Frostscheibe wie StatusView.DrawFrostDisc: innen Punktmuster Kaelte ueber dunklem Reif, heller Rand, unbeleuchtet.
    frost_col, frost_cov = np.zeros((H, W, 3)), np.zeros((H, W), bool)
    for dist in (4.0, 8.0):
        r = np.hypot(fx - FROST["x"], fz - (EYE_Z + dist))
        inner = r < FROST["r"] - FROST["rim"]
        frost_col = np.where((inner & (thr < 0.5))[..., None], FROST["cold"], np.where(inner[..., None], FROST["dark"], frost_col))
        frost_col = np.where(((r < FROST["r"]) & ~inner)[..., None], FROST["rime"], frost_col)
        frost_cov |= r < FROST["r"]
    frost_pic = over(empty_pic, frost_col, fd, frost_cov)
    frost_img = finish(frost_pic, thr)

    def prep(ps, half):
        owner, best, part, zone, wins = -np.ones((H, W), int), np.full((H, W), 1e9), np.zeros((H, W)), -np.ones((H, W), int), []
        for j, p in enumerate(ps):
            r = np.hypot(fx - p["x"], fz - (EYE_Z + p["dist"]))
            mine = (r < RINGS[-1][0]) & (r < best)
            pr = np.zeros((H, W))
            for radius, share in reversed(RINGS):
                pr = np.where(r < radius, share, pr)
            owner, best, part = np.where(mine, j, owner), np.where(mine, r, best), np.where(mine, pr, part)
            win = window(p["x"], p["dist"], half)
            zone[win] = j
            wins.append(win)
        return {"owner": owner, "part": part, "zone": zone, "wins": wins, "ps": ps}

    stat = {k: prep(*v) for k, v in sets.items()}
    K = np.ones(4)

    def state(ps, t, n):
        """Je Stueck: Stufe von A, Punktanteil von B, Farbe; letzter Eintrag = kein Stueck."""
        lv, sh, col = np.zeros(len(ps) + 1), np.zeros(len(ps) + 1), np.zeros((len(ps) + 1, 3))
        for j, p in enumerate(ps):
            ph = (p["id"] / (2 * math.pi)) % 1.0
            lv[j] = level(t + ph * P_A, n)
            sh[j] = share_b(t + ph * P_B) * K[p["tier"]]
            c = np.array(TIERS[p["tier"]][1], float)
            if p["vamp"]:
                w = wave(t + ph * P_VAMP, P_VAMP)
                c = np.array(VAMP[0], float) * (1 - w) + np.array(VAMP[1], float) * w
            col[j] = c
        return lv, sh, col

    def disc_layer(st, sh, col):
        return col[st["owner"]], fd, thr < st["part"] * sh[st["owner"]]

    def spark_layer(st, t, spec, col):
        lc, ld, cov = np.zeros((H, W, 3)), np.full((H, W), 1e9), np.zeros((H, W), bool)
        for j, p in enumerate(st["ps"]):
            ph = (p["id"] / (2 * math.pi)) % 1.0
            for y, a, size, rad in motes(t, spec, ph):
                X, Z = p["x"] + rad * math.cos(a), p["dist"] + rad * math.sin(a)
                cx, cy, h = W / 2 - X * F / Z, H / 2 - (y - EYE_Y) * F / Z, size * F / Z / 2
                x0, x1 = math.ceil(cx - h - 0.5), math.floor(cx + h - 0.5)
                y0, y1 = math.ceil(cy - h - 0.5), math.floor(cy + h - 0.5)
                if x1 < x0 or y1 < y0 or x0 < 0 or y0 < 0 or x1 >= W or y1 >= H:
                    continue
                d = math.sqrt(X * X + (y - EYE_Y) ** 2 + Z * Z)
                sl = (slice(y0, y1 + 1), slice(x0, x1 + 1))
                m = d < ld[sl]
                lc[sl], ld[sl], cov[sl] = np.where(m[..., None], col[j], lc[sl]), np.where(m, d, ld[sl]), cov[sl] | m
        return lc, ld, cov

    def knife_layer(k, st, lv, col, lit=True):
        z = st["zone"]
        if not lit:
            return k["base"] * k["light"] * lantern(k["d"]), k["d"], k["cov"]
        fl = (FLASH[0] + (FLASH[1] - FLASH[0]) * lv[z])[..., None]
        return (k["base"] * (1 - fl) + col[z] * fl) * k["light"] * lantern(k["d"], lv[z]), k["d"], k["cov"]

    def cube_layer(q, st, lv, col, lit=True):
        z = st["zone"]
        dim = (DIM[0] + (DIM[1] - DIM[0]) * lv[z])[..., None] if lit else 1.0
        return col[z] * dim * q["light"] * lantern(q["d"], lv[z] if lit else 0.0), q["d"], q["cov"]

    def stack(base, layers):
        pic = base
        for layer in layers:
            pic = over(pic, *layer)
        return finish(pic, thr)

    def dist_w(a, b):
        return np.sqrt((((a - b) ** 2) * LUMA).sum(2))

    def measure(img, ref, wins):
        dd = np.sqrt(((img - ref) ** 2).sum(2))
        out = np.zeros((len(wins), 5))
        for j, win in enumerate(wins):
            m = dd[win] >= SEEN
            if m.any():
                out[j] = (m.sum(), dd[win][m].mean(), *img[win][m].mean(0))
        return out

    # --- B: Staerke je Stufe messen und angleichen -------------------------------------------------------------------
    mainst = stat["main"]
    tier_col = np.vstack([np.array([TIERS[p["tier"]][1] for p in mainst["ps"]], float), np.zeros((1, 3))])

    def disc_strength():
        sh = np.array([K[p["tier"]] * SHARE[1] / 16 for p in mainst["ps"]] + [0.0])
        img = stack(empty_pic, [disc_layer(mainst, sh, tier_col)])
        wd, dd = dist_w(img, empty), np.sqrt(((img - empty) ** 2).sum(2))
        return (np.array([wd[w].sum() for w in mainst["wins"]]).reshape(4, 2),
                np.array([(dd[w] >= SEEN).sum() for w in mainst["wins"]]).reshape(4, 2), img)

    before_s, before_px, before_img = disc_strength()
    target = np.exp(np.log(before_s[[0, 1, 3]]).mean())   # Royal war zu stark: Ziel ist das Mittel der drei anderen
    for _ in range(6):
        cur = np.exp(np.log(disc_strength()[0]).mean(1))
        K = np.clip(K * target / cur, 0.3, 15 / SHARE[1])
    after_s, after_px, after_img = disc_strength()

    # --- Bilder -------------------------------------------------------------------------------------------------------
    frames = list(range(0, FRAMES, STEP))
    raw = {key: open(os.path.join(psx.TMP, f"schimmer4_{key}.rgb"), "wb") for key, _n in FILMS}
    stats = {key: np.zeros((len(frames), 8, 5)) for key, _n in FILMS}
    stats["nur-b"] = np.zeros((len(frames), 8, 5))
    quiet = {key: [] for key in QUIET}
    hist = {key: deque(maxlen=max(1, round(0.1 * FPS / STEP))) for key in QUIET}
    worn_vamp, frost_gap = [], []
    n_a, jumps, level_tiles = None, {}, []
    for n, f in enumerate(frames):
        t = f / FPS
        for key, (ps, _h) in sets.items():
            for j, p in enumerate(ps):
                ph = (p["id"] / (2 * math.pi)) % 1.0
                yaw, bob = 360.0 * (t / LOOP + ph), 0.04 * math.sin(2 * math.pi * (t / P_BOB + ph))
                R2.stand(trios[key][j], build, (p["x"], R2.UNDER + bob, EYE_Z + p["dist"]), yaw)
                if key == "main":
                    B.place([cubes[j]], B.world((p["x"], 0.2 + bob, EYE_Z + p["dist"])) @ B.rot(yaw, "Z"))
        bpy.context.view_layer.update()
        kn = {key: grab("messer_" + key, [o for gs in trios[key] for g in gs for o in g]) for key in sets}
        q = grab("wuerfel", cubes)

        if n_a is None:                                   # A: Stufenzahl aus dem groessten Sprung bestimmen
            k = kn["main"]
            for cand in CANDIDATES:
                worst = 0.0
                for a in range(cand - 1):
                    lo = np.full(9, a / (cand - 1))
                    hi = np.full(9, (a + 1) / (cand - 1))
                    da = np.abs(knife_layer(k, mainst, hi, tier_col)[0] - knife_layer(k, mainst, lo, tier_col)[0]).max(2)
                    for win in mainst["wins"]:
                        m = k["cov"][win]
                        if m.any():
                            worst = max(worst, float(da[win][m].mean()))
                jumps[cand] = worst
            n_a = N_A
            c3 = B.pixel((XS[1], 0.30, EYE_Z + 3.0))
            for a in range(n_a):
                img = stack(empty_pic, [knife_layer(k, mainst, np.full(9, a / (n_a - 1)), tier_col)])
                level_tiles.append(B.up_n(B.crop(img, c3, 110, 109), 2))

        done = {}
        lv, sh, col = state(mainst["ps"], t, n_a)
        disc = disc_layer(mainst, sh, col)
        sp = {c: spark_layer(mainst, t, spec, col) for c, spec in C_SPEC.items()}
        sp_old = spark_layer(mainst, t, C_OLD, col)
        kp, ka = knife_layer(kn["main"], mainst, lv, col, False), knife_layer(kn["main"], mainst, lv, col)
        qa = cube_layer(q, mainst, lv, col)
        slab = ((DARK + (col[mainst["zone"]] - DARK) * glow_today(t)) * plate["light"] * lantern(plate["d"]), plate["d"], plate["cov"])
        done["heute"] = stack(empty_pic, [slab, cube_layer(q, mainst, lv, col, False)])
        done["ohne"] = stack(empty_pic, [kp])
        done["nur-a"] = stack(empty_pic, [ka])
        done["nur-b"] = stack(empty_pic, [disc, kp])
        done["nur-c1"] = stack(empty_pic, [sp["c1"], kp])
        done["nur-c2"] = stack(empty_pic, [sp["c2"], kp])
        done["nur-c-alt"] = stack(empty_pic, [sp_old, kp])
        for c in C_SPEC:
            done[f"kombi-a2-{c}"] = stack(empty_pic, [disc, sp[c], ka])
            done[f"kombi-wuerfel-{c}"] = stack(empty_pic, [disc, sp[c], qa])
        lv16 = state(mainst["ps"], t, N_FINE)[0]
        done["kombi-a2-c1-a16"] = stack(empty_pic, [disc, sp["c1"], knife_layer(kn["main"], mainst, lv16, col)])
        vst = stat["vers"]
        lv, sh, col = state(vst["ps"], t, n_a)
        done["versetzt"] = stack(empty_pic, [disc_layer(vst, sh, col), spark_layer(vst, t, C_SPEC["c1"], col),
                                             knife_layer(kn["vers"], vst, lv, col)])
        vers_ref = stack(empty_pic, [knife_layer(kn["vers"], vst, lv, col, False)])
        dst = stat["dicht"]
        lv, sh, col = state(dst["ps"], t, n_a)
        ddisc = disc_layer(dst, sh, col)
        done["dicht"] = stack(frost_pic, [ddisc, spark_layer(dst, t, C_SPEC["c1"], col), knife_layer(kn["dicht"], dst, lv, col)])
        dicht_ref = stack(frost_pic, [knife_layer(kn["dicht"], dst, lv, col, False)])

        for key, _name in FILMS:
            raw[key].write(done[key].astype(np.uint8).tobytes())
        for key in ("heute", "kombi-wuerfel-c1", "kombi-wuerfel-c2"):
            stats[key][n] = measure(done[key], empty, mainst["wins"])
        for key in ("kombi-a2-c1", "kombi-a2-c2", "nur-b"):
            stats[key][n] = measure(done[key], done["ohne"], mainst["wins"])
        stats["versetzt"][n] = measure(done["versetzt"], vers_ref, vst["wins"])
        stats["dicht"][n] = measure(done["dicht"], dicht_ref, dst["wins"])
        sd = stats["dicht"][n]
        for row in (0, 4):                                # Worn (0) gegen Vampiric (2) in der dichten Reihe
            if sd[row, 0] > 0 and sd[row + 2, 0] > 0:
                worn_vamp.append((row, float(np.linalg.norm(sd[row, 2:5] - sd[row + 2, 2:5]))))
        for row, j in ((0, 3), (1, 7)):                   # Punkte des Forged-Scheins gegen die Frostscheibe daneben
            dots = ddisc[2] & (dst["owner"] == j)
            if dots.any():
                frost_gap.append((row, float(np.linalg.norm(done["dicht"][dots].mean(0) - frost_img[frost_cov & (np.abs(fz - EYE_Z - (4.0, 8.0)[row]) < 0.7)].mean(0))),
                                  int((dots & frost_cov).sum()), int(dots.sum())))
        for key in QUIET:                                 # Unruhe: Bildpunkte, die sich in 0,1 s aendern
            if len(hist[key]) == hist[key].maxlen:
                dd = np.sqrt(((done[key] - hist[key][0]) ** 2).sum(2)) >= SEEN
                quiet[key].append([dd[w].sum() for w in mainst["wins"]])
            hist[key].append(done[key])
        print("SCHIMMER Bild", f, flush=True)
    for fh in raw.values():
        fh.close()

    # --- Zahlen -------------------------------------------------------------------------------------------------------
    names = dict(FILMS)
    tn = [x[0] for x in TIERS]
    lines = [f"Throwing Knives als Beute (#819), Runde 4: A + B + C zusammen. Spielbild 854 x 480, {len(frames)} Bilder, {FPS / STEP:.0f} je Sekunde, Schleife {LOOP:.1f} s.", "",
             f"A Aufleuchten, Puls {P_A:.2f} s. Groesster Sprung zwischen zwei Stufen (Mittel ueber das Stueck, groesster Kanal, von 255; alle Stufenfarben, 3 m und 8 m):",
             "  " + "; ".join(f"{c} Stufen: {jumps[c]:.1f}" for c in CANDIDATES),
             f"  gewaehlt: {n_a} Stufen (Runde 3: 4). Der Sprung halbiert sich auf {jumps[n_a] / 8:.1f} Schritte der 15-Bit-Farbe (ein Schritt = 8);",
             f"  eine Stufe steht im Mittel {P_A / 2 / (n_a - 1) * 1000:.0f} ms = {P_A / 2 / (n_a - 1) * 60:.1f} Ticks, bleibt also als Stufe lesbar. Bei {N_FINE} Stufen waeren es {P_A / 2 / (N_FINE - 1) * 1000:.0f} ms = {P_A / 2 / (N_FINE - 1) * 60:.1f} Ticks (Film kombi-a2-c1-a16).", "",
             f"B Bodenschein, Puls {P_B:.2f} s, Punkte {SHARE[0]} bis {SHARE[1]} von 16 mal Faktor der Stufe. Staerke = Summe des gewichteten Farbabstands",
             "  (0,30 R, 0,59 G, 0,11 B) aller Bildpunkte zum leeren Boden, hellstes Bild; Ziel = Mittel von Worn, Forged, Vampiric."]
    for i in range(4):
        lines.append(f"  {tn[i]}: Faktor {K[i]:.2f} (Punkte hell {K[i] * SHARE[1]:.1f} von 16); Staerke 3 m {before_s[i, 0]:.0f} -> {after_s[i, 0]:.0f}, 8 m {before_s[i, 1]:.0f} -> {after_s[i, 1]:.0f}; "
                     f"Bildpunkte 3 m {before_px[i, 0]} -> {after_px[i, 0]}, 8 m {before_px[i, 1]} -> {after_px[i, 1]}")
    for d, dn in enumerate(("3 m", "8 m")):
        lines.append(f"  staerkste / schwaechste Stufe {dn}: vorher {before_s[:, d].max() / before_s[:, d].min():.2f}, nachher {after_s[:, d].max() / after_s[:, d].min():.2f}")
    lines += ["", "C Funken, Aufstieg 0,06 bis 0,62 m in " + f"{P_C:.2f} s, Kreis 0,20 bis 0,28 m:"]
    for c, (cnt, lo, hi, life) in list(C_SPEC.items()) + [("Runde 3", C_OLD)]:
        lines.append(f"  {c}: {cnt} Funken, Kante {lo * 100:.0f} cm (oben {hi * 100:.0f} cm), {cnt * life:.1f} gleichzeitig; Kante in 3 m {lo * F / 3:.1f} px, in 8 m {lo * F / 8:.1f} px; Flaeche zusammen {cnt * life * (lo * lo + hi * hi) / 2 * 1e4:.0f} cm2")
    lines.append("")
    summary = {}
    for key in ("heute", "nur-b", "kombi-a2-c1", "kombi-a2-c2", "kombi-wuerfel-c1", "kombi-wuerfel-c2", "versetzt", "dicht"):
        st = stats[key]
        dists = (4.0, 8.0) if key == "dicht" else (3.0, 8.0)
        order = [[0, 3, 1, 2], [4, 7, 5, 6]] if key == "dicht" else [[0, 2, 4, 6], [1, 3, 5, 7]]   # je Entfernung: W, F, R, V
        far = order[1]
        energy = (st[:, far, 0] * st[:, far, 1]).sum(1)
        lo, hi = int(energy.argmin()), int(energy.argmax())
        summary[key] = {"lo": lo, "hi": hi, "rows": []}
        lines.append(f"{names.get(key, 'nur B (Runde 3 mit neuem Faktor), a2')} (dunkelstes Bild {frames[lo]}, hellstes {frames[hi]})")
        for d, dist in enumerate(dists):
            idx = order[d]
            row = "; ".join(f"{tn[i]} {st[lo, idx[i], 0]:.0f} / {st[hi, idx[i], 0]:.0f} (im Puls {st[:, idx[i], 0].min():.0f} bis {st[:, idx[i], 0].max():.0f})" for i in range(4))
            lines.append(f"  {dist:.0f} m, Bildpunkte mit Stufe dunkel / hell: {row}")
            gaps = []
            for nn in (lo, hi):
                cols = [st[nn, j, 2:5] for j in idx if st[nn, j, 0] > 0]
                gaps.append(min((float(np.linalg.norm(a - b)) for x, a in enumerate(cols) for b in cols[x + 1:]), default=0.0))
            lines.append(f"  {dist:.0f} m, kleinster Farbabstand zweier Stufen dunkel / hell: {gaps[0]:.0f} / {gaps[1]:.0f}")
            summary[key]["rows"].append((dist, [(int(st[lo, j, 0]), int(st[hi, j, 0]), int(st[:, j, 0].min())) for j in idx], gaps))
        lines.append("")
    lines.append("Unruhe: Bildpunkte je Stueck, die sich innerhalb von 0,1 s um >= 16 aendern (Mittel ueber Schleife und vier Stufen), 3 m / 8 m:")
    qsum = {}
    for key in QUIET:
        a = np.array(quiet[key], float).reshape(-1, 4, 2).mean((0, 1)) if quiet[key] else np.zeros(2)
        qsum[key] = a
        lines.append(f"  {key}: {a[0]:.0f} / {a[1]:.1f}")
    lines.append("")
    for row, dn in ((0, "4 m"), (4, "8 m")):
        v = [g for r, g in worn_vamp if r == row]
        if v:
            lines.append(f"dicht, {dn}: Farbabstand Worn gegen Vampiric (mit eigenem Puls {P_VAMP:.1f} s) ueber die Schleife {min(v):.0f} bis {max(v):.0f}")
    for row, dn in ((0, "4 m"), (1, "8 m")):
        v = [g for g in frost_gap if g[0] == row]
        if v:
            lines.append(f"dicht, {dn}: Punkte des Forged-Scheins gegen die Frostscheibe daneben: Farbabstand {min(x[1] for x in v):.0f} bis {max(x[1] for x in v):.0f}; "
                         f"hoechstens {max(x[2] for x in v)} von {max(x[3] for x in v)} Punkten liegen auf der Frostscheibe")
    with open(os.path.join(HERE, "schimmer4-zahlen.txt"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines) + "\n")
    print("\n".join("SCHIMMER " + x for x in lines))

    # --- Filme --------------------------------------------------------------------------------------------------------
    ffmpeg = shutil.which("ffmpeg")
    for key, _name in FILMS:
        if ffmpeg is None:
            print("SCHIMMER ffmpeg fehlt im Pfad, kein Film")
            break
        graph = ("split[a][b];[a]palettegen=max_colors=256:stats_mode=full:reserve_transparent=0[p];"
                 "[b][p]paletteuse=dither=none:diff_mode=rectangle")
        out = os.path.join(HERE, f"schimmer4-{key}.gif")
        res = subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}",
                              "-r", str(FPS / STEP), "-i", os.path.join(psx.TMP, f"schimmer4_{key}.rgb"),
                              "-filter_complex", graph, "-loop", "0", out], capture_output=True, text=True)
        print("SCHIMMER Film", key, res.returncode, res.stderr.strip()[:300], os.path.getsize(out) if res.returncode == 0 else "")

    # --- Blatt --------------------------------------------------------------------------------------------------------
    film = {key: np.memmap(os.path.join(psx.TMP, f"schimmer4_{key}.rgb"), np.uint8, "r", shape=(len(frames), H, W, 3))
            for key, _n in FILMS}
    gap, lab, note_w, tw, th, big = 8, 22, 520, 320, 328, 330
    width = note_w + 8 * (tw + gap) + 2 * (big + gap)
    strips = ["heute", "kombi-a2-c1", "kombi-a2-c2", "kombi-wuerfel-c1", "kombi-wuerfel-c2"]
    y1 = 34 + 2 * (lab + H + gap)
    y2 = y1 + lab + len(strips) * (th + gap)
    y3 = y2 + lab + 226 + gap
    y4 = y3 + lab + big + gap
    sheet = np.zeros((y4 + lab + 448 + 8, width, 3)) + psx.FOG_COLOR
    B.text(sheet, "Silver & Ash - Beute (#819), Runde 4: Aufleuchten + Bodenschein + Funken zusammen. Spielbild 854 x 480, Nebel und Laterne wie level.fs. "
           "Von links Worn, Forged, Royal, Vampiric; vorn 3 m, hinten 8 m. Bewegung: siehe Filme (50 Bilder/s).", 6, 6, 16)
    top = [("kombi-a2-c1", "heute"), ("kombi-wuerfel-c1", "kombi-a2-c2"), ("versetzt", "kombi-wuerfel-c2"), ("dicht", None)]
    for c, pair in enumerate(top):
        for r, key in enumerate(pair):
            x, y = c * (W + gap), 34 + r * (lab + H + gap)
            if key is None:
                for j, ln in enumerate(lines[2:6] + lines[7:16] + lines[-len(QUIET) - 4:]):
                    B.text(sheet, ln[:150], x + 2, y + lab + j * 17, 11)
                continue
            nn = summary[key]["hi"]
            B.text(sheet, f"{names[key]} - Bild {frames[nn]} (hellstes)", x + 2, y, 14)
            B.paste(sheet, film[key][nn].astype(float), x, y + lab)
    B.text(sheet, f"Eine Schleife des Bodenscheins ({P_B:.1f} s) in acht Bildern, Forged in 8 m (8x); Balken = Punktanteil von B. Rechts: dunkelstes und hellstes Bild in 3 m (3x).", 6, y1, 14)
    for r, key in enumerate(strips):
        y = y1 + lab + r * (th + gap)
        sm = summary[key]
        notes = [names[key]]
        if key != "heute":
            notes += [f"A {n_a} Stufen in {P_A:.2f} s, B {P_B:.2f} s, C {key[-2:]}"]
        for dist, px, gaps in sm["rows"]:
            notes.append(f"{dist:.0f} m, px dunkel / hell: " + ", ".join(f"{tn[i][0]} {px[i][0]}/{px[i][1]}" for i in range(4)))
            notes.append(f"{dist:.0f} m, wenigste px im Puls: " + ", ".join(f"{tn[i][0]} {px[i][2]}" for i in range(4)))
            notes.append(f"{dist:.0f} m, Farbabstand zweier Stufen mind.: {gaps[0]:.0f} / {gaps[1]:.0f}")
        if key in qsum:
            notes.append(f"Unruhe 3 m / 8 m: {qsum[key][0]:.0f} / {qsum[key][1]:.1f} px je 0,1 s")
        for j, note in enumerate(notes):
            B.text(sheet, note, 6, y + 6 + j * 20, 14 if j == 0 else 12)
        for j in range(8):
            t = j * P_B / 8
            nn = min(range(len(frames)), key=lambda a: abs(frames[a] / FPS - t))
            c = B.pixel((XS[1], 0.30, EYE_Z + 8.0))
            x = note_w + j * (tw + gap)
            B.paste(sheet, B.up_n(B.crop(film[key][nn].astype(float), c, 40, 41), 8), x, y)
            val = (glow_today(t) - 0.10) / 0.50 if key == "heute" else share_b(t) * 16 / SHARE[1]
            sheet[y + th - 8:y + th, x:x + max(2, int(tw * val))] = TIERS[1][1]
        for j, nn in enumerate((sm["lo"], sm["hi"])):
            c = B.pixel((XS[1], 0.30, EYE_Z + 3.0))
            B.paste(sheet, B.up_n(B.crop(film[key][nn].astype(float), c, 110, 109), 3), note_w + 8 * (tw + gap) + j * (big + gap), y)
    B.text(sheet, f"A: die {n_a} Stufen des Aufleuchtens, Forged in 3 m (2x). Groesster Sprung zwischen zwei Stufen {jumps[n_a]:.1f} von 255 (bei 4 Stufen {jumps[4]:.1f}).", 6, y2, 14)
    for a, tile in enumerate(level_tiles):
        B.paste(sheet, tile, 6 + a * (tile.shape[1] + gap), y2 + lab)
    B.text(sheet, "B: Bodenschein allein im hellsten Bild, 3 m (3x). Links vier Stufen vorher (gleicher Punktanteil), rechts nachher (Faktor je Stufe: "
           + ", ".join(f"{tn[i]} {K[i]:.2f}" for i in range(4)) + ").", 6, y3, 14)
    for h, img in enumerate((before_img, after_img)):
        for i in range(4):
            c = B.pixel((XS[i], 0.10, EYE_Z + 3.0))
            B.paste(sheet, B.up_n(B.crop(img, c, 110, 109), 3), 6 + (h * 4 + i) * (big + gap) + h * 40, y3 + lab)
    B.text(sheet, "Dicht beieinander (2x, Bildzeilen 236 bis 460): Worn, Royal, Vampiric mit eigenem Puls im Abstand 0,6 m; Forged neben einer Frostscheibe. Links dunkelstes, rechts hellstes Bild.", 6, y4, 14)
    for j, nn in enumerate((summary["dicht"]["lo"], summary["dicht"]["hi"])):
        B.paste(sheet, B.up_n(film["dicht"][nn][236:460].astype(float), 2), j * (2 * W + gap), y4 + lab)
    S.save_png(sheet[::-1], os.path.join(HERE, "schimmer-819-runde-4.png"))
    print("SCHIMMER Blatt geschrieben:", sheet.shape[1], "x", sheet.shape[0])


if __name__ == "__main__":
    main()
