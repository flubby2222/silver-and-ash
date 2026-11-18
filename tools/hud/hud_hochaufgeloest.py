"""HUD und Menue, hoeher aufgeloest und dreidimensional (Issue #393): zwei Probestuecke, ausserhalb des Spiels.

Aufruf (Blender dient als Python mit numpy und als Renderer; nichts wird heruntergeladen):
  "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/hud/hud_hochaufgeloest.py [-- <Ausgabeordner>]

Ohne Ausgabeordner schreibt das Skript nach design/proben/grafik/hud-hochaufgeloest.

Zwei Richtungen mit demselben Aufbau (Layout in Einheiten des 854 x 480 grossen Entwurfs, Faktor s je Ausgabegroesse):
  A  harte Pixel: Raster = Ausgabebild (1708 x 960 = doppelt fein, 1920 x 1080), keine Glaettung, Silber in 8 festen
     Stufen, Bayer-Raster in den Verlaeufen, Schrift "Kapitell" mit EPX-Kantenglaettung auf halbem Pixel (bleibt hart).
  B  geglaettet: Kantenglaettung, weiche Schatten, stufenlose Verlaeufe, Schrift als geglaettete Umrisse derselben Vorlage.
Die Metall-Rahmen sind echte Blender-Renderings (Cycles, CPU, fester Seed): ein Profil (Fase, Plateau, Stufe, Fuss) als
Netz um die Fassungsform, vier Nieten, Licht von oben links, Sonne und schwaches Gegenlicht, Metall mit Rauheit.
Je Rahmen ein Render mit doppelter Pixelzahl; A tastet daraus hart ab, B mittelt. Ohne Zufall ausserhalb des Renderers
(Cycles mit festem Seed ist auf der CPU reproduzierbar): zwei Laeufe liefern bitgleiche Bilder.

Auftragsurteile, die schon gelten: keine kleinen grauen Innenrahmen; Lebensleiste ohne senkrechten roten Balken; Erfahrung
golden; gleiche Abstaende (ein Mass G fuer Rand und Zwischenraeume); grosse Munitionszahl (geladen gross, Vorrat klein)
mittig ueber den Waffenfeldern; "[X] to level up" gelb; Leben und Ausdauer ohne Zahl; Menue-Tafeln leicht durchsichtig;
kein Rot ausser der Lebensfuellung.
"""
import bpy, bmesh, sys, os, math, tempfile, hashlib
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
GRAFIK = os.path.join(REPO, "design", "proben", "grafik")
OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1 \
    else os.path.join(GRAFIK, "hud-hochaufgeloest")
os.makedirs(OUT, exist_ok=True)

sys.path.insert(0, os.path.join(GRAFIK, "schrift"))
import schrift as SCHRIFT  # noqa: E402  (Kapitell: Bitmuster, reines Python)
sys.path.insert(0, os.path.join(GRAFIK, "hud-vorlagen"))
import hud_blaetter as HB  # noqa: E402  (Abstandsfunktionen und Zeichen der abgenommenen Vorlage)

FONT = SCHRIFT.font_a()
TMP = tempfile.mkdtemp(prefix="hudhoch")

# ---------------------------------------------------------------- Farben (sRGB 0..255 wie die Palette)
BONE = (216, 210, 192); BONE2 = (195, 192, 164); BONE3 = (140, 148, 115)
FOG = (16, 11, 20); INK = (14, 11, 20); PANEL = (22, 19, 30)
RED2 = (105, 20, 34); RED5 = (200, 30, 40); RED6 = (225, 58, 48)
BRASS = (214, 170, 64); BRASS_M = (188, 135, 45); BRASS_L = (227, 202, 111)
YELLOW = (238, 204, 74)
SILVER_RAMP = np.array([26, 46, 72, 100, 132, 166, 200, 230], np.float32) / 255.0  # neutrales Silber, 8 harte Stufen
G = 8.0  # ein Abstandsmass fuer Rand und Zwischenraeume (Einheiten)


def c01(c):
    return np.array(c, np.float32) / 255.0


# ---------------------------------------------------------------- Hilfen
def blur1(a, sigma, axis):
    if sigma <= 0.01:
        return a
    r = int(math.ceil(sigma * 3))
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2).astype(np.float32); k /= k.sum()
    pad = [(0, 0)] * a.ndim; pad[axis] = (r, r)
    p = np.pad(a, pad, mode="edge")
    out = np.zeros_like(a)
    n = a.shape[axis]
    for i, w in enumerate(k):
        sl = [slice(None)] * a.ndim; sl[axis] = slice(i, i + n)
        out += w * p[tuple(sl)]
    return out


def blur(a, sigma):
    return blur1(blur1(a, sigma, 0), sigma, 1)


BAYER = (np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], np.float32) + 0.5) / 16.0


def bayer_at(x0, y0, h, w):
    ys = (np.arange(h) + y0) % 4; xs = (np.arange(w) + x0) % 4
    return BAYER[ys][:, xs]


def epx(p):
    """EPX (Scale2x) fuer Bitmuster: Schraegen bekommen halbe Stufen, Kanten bleiben hart."""
    H, W = p.shape
    q = np.pad(p, 1)
    P = q[1:-1, 1:-1]; A = q[:-2, 1:-1]; B = q[1:-1, 2:]; C = q[1:-1, :-2]; D = q[2:, 1:-1]
    o = np.zeros((2 * H, 2 * W), bool)
    o[0::2, 0::2] = np.where((C == A) & (C != D) & (A != B), A, P)
    o[0::2, 1::2] = np.where((A == B) & (A != C) & (B != D), B, P)
    o[1::2, 0::2] = np.where((D == C) & (D != B) & (C != A), C, P)
    o[1::2, 1::2] = np.where((B == D) & (B != A) & (D != C), D, P)
    return o


# ---------------------------------------------------------------- Metall aus Blender
def offset_poly(P, d):
    """Konvexes Vieleck nach innen um d versetzen (gleiche Eckenzahl)."""
    n = len(P)
    area = sum(P[i][0] * P[(i + 1) % n][1] - P[(i + 1) % n][0] * P[i][1] for i in range(n)) / 2
    sg = 1.0 if area > 0 else -1.0
    lines = []
    for i in range(n):
        ax, ay = P[i]; bx, by = P[(i + 1) % n]
        ex, ey = bx - ax, by - ay
        L = math.hypot(ex, ey)
        nx, ny = -ey / L * sg, ex / L * sg  # nach innen
        lines.append((ax + nx * d, ay + ny * d, ex, ey))
    out = []
    for i in range(n):
        p = lines[i - 1]; q = lines[i]
        den = p[2] * q[3] - p[3] * q[2]
        t = ((q[0] - p[0]) * q[3] - (q[1] - p[1]) * q[2]) / den
        out.append((p[0] + p[2] * t, p[1] + p[3] * t))
    return out


def shape_poly(kind, W, H, cham):
    """Aussenform in Pixeln, y nach unten, (0,0) links oben."""
    if kind == "rect":
        c = cham
        return [(c, 0), (W - c, 0), (W, c), (W, H - c), (W - c, H), (c, H), (0, H - c), (0, c)]
    if kind == "hex":  # spitze Enden links und rechts (Cursor)
        t = cham
        return [(t, 0), (W - t, 0), (W, H / 2), (W - t, H), (t, H), (0, H / 2)]
    if kind == "tri":  # Giebel
        return [(0, H), (W / 2, 0), (W, H)]
    raise ValueError(kind)


PROFILE = [(0.0, 0.0), (0.18, 0.7), (0.34, 1.0), (0.5, 1.0), (0.64, 0.45), (0.74, 0.45), (1.0, 0.0)]


def _setup_scene():
    sc = bpy.context.scene
    for o in list(bpy.data.objects):  # Standardszene (Wuerfel, Kamera, Lampe) entfernen
        bpy.data.objects.remove(o)
    sc.render.engine = "CYCLES"
    cy = sc.cycles
    cy.device = "CPU"; cy.samples = 48; cy.use_denoising = False; cy.seed = 7; cy.use_animated_seed = False
    cy.use_adaptive_sampling = False; cy.use_light_tree = False; cy.use_denoising = False
    cy.max_bounces = 4
    sc.render.film_transparent = True
    sc.render.dither_intensity = 0.0  # kein Zittern beim Speichern
    sc.render.filter_size = 1.0
    sc.view_settings.view_transform = "Standard"; sc.view_settings.look = "None"
    sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
    sc.render.image_settings.color_depth = "16"
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.5, 0.5, 0.52, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.30
    sc.world = w
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"; sc.camera = cam
    for nm, d, e in (("key", (-0.5, 0.6, 0.62), 4.2), ("fill", (0.6, -0.5, 0.5), 0.9)):
        ld = bpy.data.lights.new(nm, "SUN"); ld.energy = e; ld.angle = math.radians(22 if nm == "key" else 40)
        ob = bpy.data.objects.new(nm, ld); sc.collection.objects.link(ob)
        # Sonne leuchtet entlang -Z der Lampe; sie soll aus Richtung d kommen
        ob.rotation_euler = (-Vector(d)).to_track_quat("-Z", "Y").to_euler()
    mat = bpy.data.materials.new("silver"); mat.use_nodes = True
    b = mat.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (0.80, 0.80, 0.82, 1)
    b.inputs["Metallic"].default_value = 1.0
    b.inputs["Roughness"].default_value = 0.34
    return sc, cam, mat


SC, CAM, MAT = _setup_scene()
_metal_cache = {}
LIGHTS = [o for o in SC.collection.objects if o.type == "LIGHT"]
_model_cache = {}
MODEL_DIR = os.path.join(REPO, "assets", "models")
MODEL_VIEW = dict(cy=-0.09, cz=0.0, scale=0.54)  # feste Kamera fuer alle Modelle (Einheiten der Modelldatei)


def render_model(name, W, H, ss=2):
    """Modell aus assets/models in fester Seitenansicht (Kamera schraeg von links oben, Waffe zeigt nach rechts), gleiches Licht fuer alle.
    Rueckgabe RGBA (H*ss, W*ss, 4) in sRGB-Naeherung, Zeilen von oben."""
    key = (name, W, H, ss)
    if key in _model_cache:
        return _model_cache[key]
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODEL_DIR, name + ".glb"))
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.name == "flash" or o.type != "MESH":  # Muendungsfeuer gehoert nicht ins Symbol
            o.hide_render = True
    old = [(l, l.rotation_euler.copy()) for l in LIGHTS]
    for l, d in zip(LIGHTS, ((-0.7, 0.45, 0.8), (-0.4, -0.5, 0.3))):
        l.rotation_euler = (-Vector(d)).to_track_quat("-Z", "Y").to_euler()
    old_cam = (CAM.location.copy(), CAM.rotation_euler.copy(), CAM.data.ortho_scale)
    CAM.location = (-3.0, MODEL_VIEW["cy"], 3.0 + MODEL_VIEW["cz"])
    CAM.rotation_euler = Vector((1, 0, -1)).to_track_quat("-Z", "Y").to_euler()
    CAM.data.ortho_scale = MODEL_VIEW["scale"]  # gilt fuer die laengere Seite (hier die Breite)
    CAM.data.clip_start = 0.1; CAM.data.clip_end = 20.0
    # Bildausschnitt: Breite fest MODEL_VIEW["scale"], Hoehe nach Seitenverhaeltnis
    SC.render.resolution_x = W * ss; SC.render.resolution_y = H * ss; SC.render.resolution_percentage = 100
    path = os.path.join(TMP, "m.png")
    SC.render.filepath = path
    bpy.ops.render.render(write_still=True)
    for o in new:
        bpy.data.objects.remove(o)
    for l, r in old:
        l.rotation_euler = r
    CAM.location, CAM.rotation_euler, CAM.data.ortho_scale = old_cam
    im = bpy.data.images.load(path)
    im.colorspace_settings.name = "Non-Color"; im.alpha_mode = "CHANNEL_PACKED"
    w, hh = im.size
    a = np.empty(w * hh * 4, np.float32); im.pixels.foreach_get(a)
    bpy.data.images.remove(im)
    a = a.reshape(hh, w, 4)[::-1].copy()
    _model_cache[key] = a
    return a


def _ring_loft(bm, P, z0, prof, w, h):
    """Netz aus versetzten Ringen: prof = [(Anteil der Breite, Anteil der Hoehe)]."""
    rings = []
    for (fd, fz) in prof:
        Q = offset_poly(P, fd * w)
        rings.append([bm.verts.new((x, -y, z0 + fz * h)) for x, y in Q])
    n = len(P)
    for a, b in zip(rings[:-1], rings[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
    return rings


def render_metal(kind, W, H, ring, cham, studs, smooth, gem):
    """Gerendertes Metall (RGBA, doppelte Pixelzahl, Zeilen von oben). Rand pad = 3 px."""
    key = (kind, W, H, round(ring, 2), round(cham, 2), studs, smooth, gem)
    if key in _metal_cache:
        return _metal_cache[key]
    pad = 3
    Wc, Hc = W + 2 * pad, H + 2 * pad
    P = [(x + pad, y + pad) for x, y in shape_poly(kind, W, H, cham)]
    bm = bmesh.new()
    h = ring * 0.62
    if kind == "tri":
        rings = _ring_loft(bm, P, 0.0, [(0.0, 0.0), (0.22, 0.8), (0.4, 1.0)], ring * 1.5, h * 1.3)
        bm.faces.new(list(reversed(rings[-1])))
    else:
        _ring_loft(bm, P, 0.0, PROFILE, ring, h)
    if studs and kind == "rect":
        n = len(P)
        q = offset_poly(P, ring * 0.5)
        for i in (1, 3, 5, 7):  # Mitten der vier Fasen
            qa, qb = q[i], q[(i + 1) % n]
            cx, cy = (qa[0] + qb[0]) / 2, (qa[1] + qb[1]) / 2
            r = ring * 0.46
            g = bmesh.ops.create_uvsphere(bm, u_segments=16 if smooth else 8, v_segments=10 if smooth else 5, radius=r)
            low = [v for v in g["verts"] if v.co.z < -1e-5]  # untere Halbkugel weg (sonst liegen zwei Schalen aufeinander)
            bmesh.ops.delete(bm, geom=low, context="VERTS")
            for v in g["verts"]:
                if v.is_valid:
                    v.co = (cx + v.co.x, -cy + v.co.y, h * 0.95 + v.co.z * 0.9)
    if gem:
        gx, gy = Wc / 2, pad + ring * 0.5
        r = ring * 0.62
        top = bm.verts.new((gx, -gy, h * 0.95 + r * 0.9))
        pts = [bm.verts.new((gx + dx, -gy - dy, h * 0.95)) for dx, dy in ((r * 1.2, 0), (0, r), (-r * 1.2, 0), (0, -r))]
        for i in range(4):
            bm.faces.new((pts[i], pts[(i + 1) % 4], top))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("m%d" % len(_metal_cache)); bm.to_mesh(me); bm.free()
    for p in me.polygons:
        p.use_smooth = bool(smooth)
    me.materials.append(MAT)
    ob = bpy.data.objects.new("o%d" % len(_metal_cache), me); SC.collection.objects.link(ob)
    CAM.location = (Wc / 2, -Hc / 2, 60.0); CAM.rotation_euler = (0, 0, 0)
    CAM.data.ortho_scale = float(max(Wc, Hc)); CAM.data.clip_start = 1.0; CAM.data.clip_end = 200.0
    SC.render.resolution_x = Wc * 2; SC.render.resolution_y = Hc * 2; SC.render.resolution_percentage = 100
    path = os.path.join(TMP, "r.png")
    SC.render.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(ob); bpy.data.meshes.remove(me)
    im = bpy.data.images.load(path)
    im.colorspace_settings.name = "Non-Color"; im.alpha_mode = "CHANNEL_PACKED"
    w, hh = im.size
    a = np.empty(w * hh * 4, np.float32); im.pixels.foreach_get(a)
    bpy.data.images.remove(im)
    a = a.reshape(hh, w, 4)[::-1].copy()
    _metal_cache[key] = (a, pad)
    return a, pad


# ---------------------------------------------------------------- Zeichenflaeche
class Canvas:
    def __init__(self, base, hard, s):
        self.img = base.astype(np.float32).copy()
        self.H, self.W = self.img.shape[:2]
        self.hard = hard
        self.s = s

    def blend(self, x0, y0, cov, col, alpha=1.0):
        h, w = cov.shape
        xa, ya = max(x0, 0), max(y0, 0); xb, yb = min(x0 + w, self.W), min(y0 + h, self.H)
        if xb <= xa or yb <= ya:
            return
        c = cov[ya - y0:yb - y0, xa - x0:xb - x0, None] * alpha
        if isinstance(col, np.ndarray) and col.ndim == 3:
            col = col[ya - y0:yb - y0, xa - x0:xb - x0]
        else:
            col = c01(col)
        reg = self.img[ya:yb, xa:xb]
        self.img[ya:yb, xa:xb] = reg * (1 - c) + col * c

    def cov_of(self, sdf, x0, y0, x1, y1):
        """Abdeckung einer Abstandsfunktion (Pixelkoordinaten); hart: Pixelmitte, weich: 1 px Kante."""
        px, py = np.meshgrid(np.arange(x0, x1, dtype=np.float32) + 0.5, np.arange(y0, y1, dtype=np.float32) + 0.5)
        d = sdf(px, py)
        return (d <= 0).astype(np.float32) if self.hard else np.clip(0.5 - d, 0, 1).astype(np.float32)

    def grad(self, x0, y0, h, w, c0, c1, levels=5):
        """Senkrechter Verlauf; hart: Bayer-Raster mit wenigen Stufen, weich: stufenlos."""
        t = np.linspace(0, 1, h, dtype=np.float32)[:, None] * np.ones((1, w), np.float32)
        if self.hard:
            t = np.clip(np.floor(t * (levels - 1) + bayer_at(x0, y0, h, w)) / (levels - 1), 0, 1)
        return c01(c0) * (1 - t[..., None]) + c01(c1) * t[..., None]

    def rect_cov(self, x0, y0, x1, y1, r=0.0):
        sdf = HB.sd_box(x0, y0, x1, y1, r)
        bx, by = int(math.floor(x0)) - 1, int(math.floor(y0)) - 1
        return self.cov_of(sdf, bx, by, int(math.ceil(x1)) + 1, int(math.ceil(y1)) + 1), bx, by

    def fill_rect(self, ux0, uy0, ux1, uy1, col, alpha=1.0, r=0.0):
        s = self.s
        cov, bx, by = self.rect_cov(round(ux0 * s), round(uy0 * s), round(ux1 * s), round(uy1 * s), 0 if self.hard else r * s)
        self.blend(bx, by, cov, col, alpha)

    # ------------------------------------------------------------ Rahmen mit gerendertem Metall
    def frame(self, ux, uy, uw, uh, kind="rect", ring=2.8, cham=2.6, studs=True, gem=False, fill=PANEL, fill_alpha=0.9,
              tint=None):
        s = self.s
        X0, Y0 = round(ux * s), round(uy * s)
        W, H = round(uw * s), round(uh * s)
        rp = ring * s
        ch = cham * s
        a, pad = render_metal(kind, W, H, rp, ch, studs, not self.hard, gem)
        hh, ww = a.shape[0] // 2, a.shape[1] // 2
        v = a.reshape(hh, 2, ww, 2, 4)
        alpha_ss = v[..., 3].mean(axis=(1, 3))
        rgb_ss = (v[..., :3] * v[..., 3:4]).sum(axis=(1, 3)) / np.maximum(v[..., 3].sum(axis=(1, 3)), 1e-6)[..., None]
        L = rgb_ss.mean(axis=2)
        lo, hi = 0.16, 0.92
        t = np.clip((L - lo) / (hi - lo), 0, 1)
        if self.hard:
            idx = np.minimum((t * 8).astype(int), 7)
            col = np.repeat(SILVER_RAMP[idx][..., None], 3, axis=2)
            cov = (alpha_ss >= 0.5).astype(np.float32)
        else:
            pos = t * 7
            i0 = np.minimum(pos.astype(int), 6); f = pos - i0
            v1 = SILVER_RAMP[i0] * (1 - f) + SILVER_RAMP[i0 + 1] * f
            col = np.repeat(v1[..., None], 3, axis=2)
            cov = alpha_ss.astype(np.float32)
        if tint is not None:  # aktives Feld: Silber mit warmem Stich
            col = col * 0.8 + c01(tint)[None, None, :] * col.mean(axis=2, keepdims=True) * 0.2
        bx, by = X0 - pad, Y0 - pad
        Pout = [(x + pad, y + pad) for x, y in shape_poly(kind, W, H, ch)]
        if kind != "tri" and fill_alpha > 0:
            Pin = offset_poly(Pout, rp * 0.75)
            inner = self.cov_of(HB.sd_poly(Pin), 0, 0, cov.shape[1], cov.shape[0])
            self.blend(bx, by, inner, fill, fill_alpha)
        sh = np.zeros_like(cov)
        if self.hard:
            d = max(1, round(0.9 * s))
            sh[d:, d:] = cov[:-d, :-d]
            self.blend(bx, by, sh * (1 - cov), INK, 0.55)
        else:
            d = int(round(1.0 * s))
            sh[d:, d:] = cov[:-d, :-d]
            self.blend(bx, by, blur(sh, 0.8 * s), INK, 0.62)
        self.blend(bx, by, cov, col)

    # ------------------------------------------------------------ Zeichen
    def icon(self, name, ucx, ucy, us, c1, c2, detail=(40, 20, 10)):
        s = self.s
        f, det = HB.ICON[name]
        cx, cy, r = ucx * s, ucy * s, us * s
        g = HB.xform(f, cx, cy, r)
        x0, y0, x1, y1 = int(cx - r * 1.5) - 2, int(cy - r * 1.5) - 2, int(cx + r * 1.5) + 3, int(cy + r * 1.5) + 3
        sh = max(1, round(0.9 * s))
        covs = self.cov_of(g, x0 - sh, y0 - sh, x1 - sh, y1 - sh)
        if not self.hard:
            covs = blur(covs, 0.5 * s)
        self.blend(x0, y0, covs, INK, 0.75)
        cov = self.cov_of(g, x0, y0, x1, y1)
        yy = (np.arange(y0, y1, dtype=np.float32) + 0.5 - (cy - r)) / (2 * r)
        t = np.clip(yy, 0, 1)[:, None, None] * np.ones((1, x1 - x0, 1), np.float32)
        if self.hard:
            t = np.clip(np.floor(t * 3 + bayer_at(x0, y0, y1 - y0, x1 - x0)[..., None]) / 3, 0, 1)
        col = c01(c1) * (1 - t) + c01(c2) * t
        self.blend(x0, y0, cov, col)
        if det is not None:
            dcov = self.cov_of(HB.xform(det, cx, cy, r), x0, y0, x1, y1) * cov
            self.blend(x0, y0, dcov, detail, 0.85)

    # ------------------------------------------------------------ Modell aus dem Spiel, klein gerendert
    def model(self, name, ucx, ucy, uw, uh, dim=1.0):
        s = self.s
        W, H = round(uw * s), round(uh * s)
        a = render_model(name, W, H)
        v = a.reshape(H, 2, W, 2, 4)
        al = v[..., 3].mean(axis=(1, 3))
        rgb = (v[..., :3] * v[..., 3:4]).sum(axis=(1, 3)) / np.maximum(v[..., 3].sum(axis=(1, 3)), 1e-6)[..., None]
        rgb = np.clip(rgb * dim, 0, 1)
        x0, y0 = int(round(ucx * s - W / 2.0)), int(round(ucy * s - H / 2.0))
        if self.hard:
            cov = (al >= 0.5).astype(np.float32)
            lv = 8.0
            rgb = np.clip(np.floor(rgb * (lv - 1) + bayer_at(x0, y0, H, W)[..., None]) / (lv - 1), 0, 1)
        else:
            cov = al.astype(np.float32)
        sh = max(1, round(0.9 * s))
        sm = np.zeros_like(cov)
        sm[sh:, sh:] = cov[:-sh, :-sh]
        if not self.hard:
            sm = blur(sm, 0.5 * s)
        self.blend(x0, y0, sm * (1 - cov), INK, 0.7)
        self.blend(x0, y0, cov, rgb)

    # ------------------------------------------------------------ Schrift Kapitell
    def text(self, txt, ux, uy, cap, color, anchor="l", shadow=True):
        """Grundlinie uy, Versalhoehe cap (Einheiten). anchor l, c, r."""
        pts = FONT.layout(txt)
        bw = FONT.width(txt)
        ROWS, BASE = 12, 8  # Zeilen -8 .. +3, die Grundlinie ist Zeile 8
        bm = np.zeros((ROWS, bw), bool)
        for x, y in pts:
            if 0 <= y + BASE < ROWS and 0 <= x < bw:
                bm[y + BASE, x] = True
        e = epx(bm)
        s = self.s
        if self.hard:
            f = max(1, int(round(cap * s / 14.0)))
            mask = np.kron(e.astype(np.float32), np.ones((f, f), np.float32))
            per = 2.0 * f
        else:
            per = cap * s / 7.0
            Wt, Ht = int(round(bw * per)), int(round(ROWS * per))
            up = HB.bilinear(e.astype(np.float32), Ht, Wt)
            sg = 0.30 * per / 2.0
            mask = np.clip((blur(up, sg) - 0.5) * max(2.5 * sg, 3.0) + 0.5, 0, 1)
        wpx = mask.shape[1]
        x = ux * s
        if anchor == "c":
            x -= wpx / 2.0
        elif anchor == "r":
            x -= wpx
        x0 = int(round(x))
        y0 = int(round(uy * s - (BASE + 1) * per))
        if shadow:
            d = max(1, int(round(0.55 * s)))
            sm = mask if self.hard else blur(mask, 0.35 * s)
            self.blend(x0 + d, y0 + d, sm, FOG, 0.9 if self.hard else 0.8)
        self.blend(x0, y0, mask, color)
        return wpx / s


def textw(txt, cap):
    """Breite in Einheiten (Naeherung, passend zu beiden Richtungen)."""
    return FONT.width(txt) * cap / 7.0


# ---------------------------------------------------------------- Bildschirme
def bar(cv, x0, y0, x1, y1, frac, c_top, c_bot, back=INK, shine=None):
    """Leiste ohne eigenen Rahmen: dunkler Grund, Fuellung, helle Kante oben."""
    s = cv.s
    cv.fill_rect(x0, y0, x1, y1, back, 0.96, r=0.8)
    if frac > 0:
        xe = x0 + (x1 - x0) * frac
        px0, py0, px1, py1 = round(x0 * s), round(y0 * s), round(xe * s), round(y1 * s)
        cov, bx, by = cv.rect_cov(px0, py0, px1, py1, 0.0)
        col = cv.grad(bx, by, cov.shape[0], cov.shape[1], c_top, c_bot, 5)
        cv.blend(bx, by, cov, col)
        if shine is not None:
            hi = max(1, round(0.8 * s))
            cov2, bx2, by2 = cv.rect_cov(px0, py0, px1, py0 + hi, 0.0)
            cv.blend(bx2, by2, cov2, shine, 0.8)


def draw_hud(cv, ammo_loaded=5, ammo_reserve=24, models=False):
    # unten links: Gegenstaende
    for i, nm in enumerate(("flask", "eye")):
        x = G + i * (30 + G)
        cv.frame(x, 480 - G - 30, 30, 30, "rect", ring=2.8, cham=2.6)
        cv.icon(nm, x + 15, 480 - G - 15, 8.0, BONE, BONE3 if nm == "flask" else (160, 170, 178))
    # unten Mitte: Leben und Ausdauer, keine Zahlen
    vx, vw = 427 - 170, 340
    vy = 480 - G - 30
    cv.frame(vx, vy, vw, 30, "rect", ring=3.0, cham=2.8)
    bar(cv, vx + 10, vy + 5, vx + vw - 10, vy + 15, 0.68, RED5, RED2, shine=RED6)
    n, gap = 5, 5.0
    seg = ((vw - 20) - gap * (n - 1)) / n
    for i in range(n):
        xa = vx + 10 + i * (seg + gap)
        fr = min(1.0, max(0.0, 3.4 - i))
        bar(cv, xa, vy + 19, xa + seg, vy + 25, fr, (206, 210, 214), (150, 156, 164), shine=(240, 242, 244))
    # unten rechts: Waffenfelder, aktive Waffe mit warmem Stich
    wx0 = 854 - G - (4 * 38 + 3 * G)
    for i, nm in enumerate(("blade", "pistol", "rifle", "blunder")):
        x = wx0 + i * (38 + G)
        act = (i == 1)
        cv.frame(x, 480 - G - 30, 38, 30, "rect", ring=2.8, cham=2.6, tint=BRASS if act else None)
        if models and nm in ("blade", "pistol"):
            cv.model(nm, x + 19, 480 - G - 15, 32.0, 24.0, 1.0 if act else 0.8)
        else:  # kein Modell im Repository: Platzhalter bleibt das heutige Zeichen
            cv.icon(nm, x + 19, 480 - G - 15, 10.5, BONE if act else BONE2, BONE3 if act else (130, 136, 126))
    # grosse Munitionszahl mittig ueber den Waffenfeldern: geladen gross, Vorrat klein
    cx = wx0 + (4 * 38 + 3 * G) / 2.0
    base = 480 - G - 30 - G - 3
    big, small = str(ammo_loaded), "/" + str(ammo_reserve)
    wb, ws = textw(big, 21.0), textw(small, 10.5)
    xl = cx - (wb + 3 + ws) / 2.0
    cv.text(big, xl, base, 21.0, BONE)
    cv.text(small, xl + wb + 3, base, 10.5, BONE2)
    # oben links: Stufe, Erfahrung (golden), Hinweis
    cv.frame(G, G, 28, 28, "rect", ring=2.8, cham=2.6)
    cv.text("7", G + 14, G + 14 + 7.0, 14.0, BONE, "c")
    xx0 = G + 28 + G
    cv.frame(xx0, G + 7, 100, 14, "rect", ring=2.2, cham=2.0, studs=False)
    bar(cv, xx0 + 4.5, G + 11, xx0 + 100 - 4.5, G + 17, 0.45, BRASS_L, BRASS_M, shine=(246, 232, 160))
    cv.text("[X] to level up", G, G + 28 + G + 7, 7.0, YELLOW)


def draw_menu(cv):
    px, pw = 427 - 120, 240
    py, ph = 164, 250
    cv.frame(px, py, pw, ph, "rect", ring=3.2, cham=3.2, studs=True, fill=PANEL, fill_alpha=0.78)
    cv.frame(427 - 19, py - 21, 38, 23, "tri", ring=2.6, fill_alpha=0, studs=False)
    cv.text("PAUSED", 427, 126, 21.0, BONE, "c")
    for i, t in enumerate(("Resume", "Settings", "Back to title", "Quit")):
        cyy = py + 44 + i * 54
        if i == 0:
            cv.frame(427 - 106, cyy - 18, 212, 36, "hex", ring=2.4, cham=14.0, studs=False, fill=(8, 7, 13), fill_alpha=0.55,
                     tint=BRASS)
            cv.text(t, 427, cyy + 7, 14.0, BRASS_L, "c")
        else:
            cv.text(t, 427, cyy + 7, 14.0, BONE, "c")
    cv.text("Esc: back to the game", 427, py + ph + 15, 7.0, BONE2, "c")


def base_photo():
    return HB.load_png(os.path.join(GRAFIK, "hud-vorlagen", "basis-854x480.png"))


def make(hard, W, H, s):
    base = base_photo()
    out = {}
    cv = Canvas(HB.nearest(base, H, W), hard, s)
    draw_hud(cv)
    out["hud"] = cv.img
    cv3 = Canvas(HB.nearest(base, H, W), hard, s)
    draw_hud(cv3, models=True)
    out["hudm"] = cv3.img
    cv2 = Canvas(HB.nearest(base * 0.42, H, W), hard, s)
    draw_hud(cv2)
    draw_menu(cv2)
    out["menu"] = cv2.img
    return out


def zoom(img, x0, y0, x1, y1, f):
    return np.repeat(np.repeat(img[y0:y1, x0:x1, :3], f, 0), f, 1)


def blatt(name, hard, imgs):
    """Ein Blatt je Richtung, gleicher Aufbau fuer A und B: Beschriftung, heute, HUD, HUD mit Modellen, Menue, Ausschnitte."""
    W = 1920
    heute = HB.load_png(os.path.join(GRAFIK, "hud-im-spiel", "ruhe-854.png"))[..., :3]
    heute_strip = HB.nearest(heute, 1080, 1920)[..., :3][1080 - 175:]
    parts = []  # (Beschriftung, Bild)
    parts.append(("TODAY: 854 x 480 SHOWN AT 1920 (BOTTOM STRIP)", heute_strip))
    parts.append(("HUD AT 1920 x 1080", imgs["1920"]["hud"][..., :3]))
    parts.append(("HUD WITH RENDERED WEAPON MODELS, BOTTOM STRIP 1920", imgs["1920"]["hudm"][1080 - 175:, :, :3]))
    parts.append(("MENU AT 1920 x 1080", imgs["1920"]["menu"][..., :3]))
    parts.append(("DETAIL 4X: WEAPON SLOTS WITH MODELS, AMMO", zoom(imgs["1920"]["hudm"], 1440, 1080 - 165, 1920, 1080, 4)))
    parts.append(("DETAIL 4X: LEVEL, XP, HINT", zoom(imgs["1920"]["hud"], 0, 0, 480, 150, 4)[:480]))
    parts.append(("DETAIL 3X: MENU TITLE, PANEL TOP AND CURSOR", zoom(imgs["1920"]["menu"], 640, 240, 1280, 500, 3)))
    parts.append(("854 LAYOUT AT 1708 x 960 (BOTTOM STRIP)", np.pad(imgs["854"]["hudm"][960 - 160:, :, :3], ((0, 0), (106, 106), (0, 0)))))
    gap, lab = 28, 44
    H = sum(p[1].shape[0] + lab + gap for p in parts) + 90
    sheet = np.zeros((H, W, 3), np.float32); sheet[:] = c01((34, 31, 42))
    cv = Canvas(sheet, True, 2.25)
    cv.text(("A  HARD PIXELS" if hard else "B  SMOOTHED") + "  /  ISSUE 393", 20 / 2.25, 62 / 2.25, 14.0, BONE)
    y = 90
    for t, im in parts:
        cv.text(t, 20 / 2.25, (y + 28) / 2.25, 7.0, BONE2)
        y += lab
        h, w = im.shape[:2]
        cv.img[y:y + h, (W - w) // 2:(W - w) // 2 + w] = im
        y += h + gap
    HB.save_png(os.path.join(OUT, f"blatt-{name}.png"), cv.img)


if __name__ == "__main__":
    import time
    t0 = time.time()
    for name, hard in (("a", True), ("b", False)):
        imgs = {}
        for tag, (W, H, s) in (("854", (1708, 960, 2.0)), ("1920", (1920, 1080, 2.25))):
            r = make(hard, W, H, s)
            imgs[tag] = r
            for k, im in r.items():
                p = os.path.join(OUT, f"{name}-{k}-{tag}.png")
                HB.save_png(p, im)
            print(name, tag, round(time.time() - t0, 1), "s", flush=True)
        blatt(name, hard, imgs)
