"""Gemeinsame Bausteine der Grafik-Probestuecke im PS1-nahen Stil (Silver & Ash).

Geometrie mit UVs aus der Bauweise, Texturen per numpy gemalt, ein texturierter Steingang, Render-Durchgaenge mit
Cycles (Textur, Licht je Ecke, Entfernung) und das Zusammensetzen wie im Spiel: heutiger Nebel oder Sichtgrenze
per Dithering mit 15-Bit-Farbe. Wird von den Probestueck-Skripten importiert; selbst kein Probestueck.

Koordinaten: Punkte in Spiel-Achsen (x seitlich, y oben, z vorn, Meter). G() rechnet in Blender-Achsen um.
"""

import math
import os

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

# --- Werte aus dem Spiel (LookSettings.cs, level.fs) --------------------------------------------------------------

FOG_COLOR = np.array((16, 11, 20), float)
FOG_START, FOG_END, LANTERN_MINIMUM = 3.25, 18.2, 0.35
FLOOR_B, CEILING_B, WALL_PX, WALL_MX, WALL_PZ, WALL_MZ = 0.9, 0.55, 1.0, 0.62, 0.82, 0.72
WORLD_WALL, WORLD_FLOOR, WORLD_CEILING = (122, 104, 112), (84, 72, 78), (64, 56, 66)
DITHER_START = 9.0     # Sichtgrenze per Dithering: Pixel fallen ab hier weg, ab FOG_END ist alles weg


def G(p):
    """Spiel-Achsen (x, oben, vorn) -> Blender-Achsen (x, -vorn, oben)."""
    return Vector((p[0], -p[2], p[1]))


def V(x, y, z):
    return Vector((x, y, z))


# --- Geometrie ------------------------------------------------------------------------------------------------------

class Part:
    """Ein starres Teil; Ursprung = Drehpunkt. parent: Teil, an dem es haengt. rects: Name -> UV-Rechteck (0..1)."""

    def __init__(self, name, pivot, rects, parent=None):
        self.name, self.pivot, self.parent, self.rects = name, V(*pivot), parent, rects
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.verify()

    def _face(self, verts, uvs):
        f = self.bm.faces.new(verts)
        for loop, uv in zip(f.loops, uvs):
            loop[self.uv].uv = uv
        return f

    def loft(self, rings, rect, cap_start=True, cap_end=True, closed=False):
        """Ringe gleicher Eckenzahl verbinden. UV: u rundherum (Ecke 0 = Naht), v entlang. Kappen als Faecher."""
        u0, v0, u1, v1 = self.rects[rect]
        n = len(rings[0])
        count = len(rings) + (1 if closed else 0)
        verts = [[self.bm.verts.new(G(p - self.pivot)) for p in ring] for ring in rings]
        faces = []

        def uv(k, i):
            return (u0 + (u1 - u0) * k / n, v0 + (v1 - v0) * i / (count - 1))

        for i in range(count - 1):
            a, b = verts[i], verts[(i + 1) % len(verts)]
            for k in range(n):
                faces.append(self._face((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]),
                                        (uv(k, i), uv(k + 1, i), uv(k + 1, i + 1), uv(k, i + 1))))
        if not closed:
            for idx, cap in ((0, cap_start), (count - 1, cap_end)):
                if not cap:
                    continue
                ring = verts[0] if idx == 0 else verts[-1]
                inner = rings[1] if idx == 0 else rings[-2]
                outer = rings[0] if idx == 0 else rings[-1]
                c_out = sum(outer, V(0, 0, 0)) / n
                c_in = sum(inner, V(0, 0, 0)) / n
                pole_game = c_out + (c_out - c_in) * 0.25
                pole = self.bm.verts.new(G(pole_game - self.pivot))
                for k in range(n):
                    faces.append(self._face((ring[k], ring[(k + 1) % n], pole),
                                            (uv(k, idx), uv(k + 1, idx), (uv(k, idx)[0] + (u1 - u0) / (2 * n), uv(k, idx)[1]))))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        return faces

    def prism(self, points, offset, rect, cap=True):
        """Konvexes Vieleck (Spiel-Achsen) entlang offset ausgezogen. rect: Rechteckname oder Funktion
        (Normale aussen -> Rechteckname), z. B. damit die Unterseite eines Deckels anders aussieht."""
        a = [V(*p) for p in points]
        b = [p + V(*offset) for p in a]
        n = len(a)
        quads = [[a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]] for k in range(n)]
        polys = quads + ([list(a), list(reversed(b))] if cap else [])
        return self.convex(polys, rect)

    def convex(self, polys, rect):
        """Flaechen eines konvexen Koerpers anlegen. UV planar: Projektion entlang der Hauptachse der Normale,
        normiert auf die Ausdehnung aller Flaechen mit demselben Rechteck. Normalen zeigen nach aussen."""
        centre = sum((p for poly in polys for p in poly), V(0, 0, 0)) / sum(len(poly) for poly in polys)
        info = []
        for poly in polys:
            normal = (poly[1] - poly[0]).cross(poly[2] - poly[0]).normalized()
            mid = sum(poly, V(0, 0, 0)) / len(poly)
            if normal.dot(mid - centre) < 0:
                normal = -normal
            name = rect(normal) if callable(rect) else rect
            info.append((poly, normal, name))
        bounds = {}
        for poly, _, name in info:
            lo, hi = bounds.get(name, (V(9, 9, 9), V(-9, -9, -9)))
            for p in poly:
                lo = V(min(lo.x, p.x), min(lo.y, p.y), min(lo.z, p.z))
                hi = V(max(hi.x, p.x), max(hi.y, p.y), max(hi.z, p.z))
            bounds[name] = (lo, hi)
        faces = []
        for poly, normal, name in info:
            u0, v0, u1, v1 = self.rects[name]
            lo, hi = bounds[name]
            span = V(max(hi.x - lo.x, 1e-4), max(hi.y - lo.y, 1e-4), max(hi.z - lo.z, 1e-4))
            ax = max(range(3), key=lambda i: abs(normal[i]))
            ua, va = {0: (2, 1), 1: (0, 2), 2: (0, 1)}[ax]
            verts = [self.bm.verts.new(G(p - self.pivot)) for p in poly]
            uvs = [(u0 + (u1 - u0) * (p[ua] - lo[ua]) / span[ua], v0 + (v1 - v0) * (p[va] - lo[va]) / span[va])
                   for p in poly]
            faces.append(self._face(verts, uvs))
        bmesh.ops.remove_doubles(self.bm, verts=[v for f in faces for v in f.verts], dist=1e-6)
        faces = [f for f in faces if f.is_valid]
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        return faces

    def box(self, lo, hi, rect):
        x0, y0, z0 = lo
        x1, y1, z1 = hi
        return self.prism([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], (0, y1 - y0, 0), rect)

    def spike(self, base, tip, half, rect, sides=4):
        axis = (V(*tip) - V(*base)).normalized()
        ref = V(0, 1, 0) if abs(axis.y) < 0.9 else V(0, 0, 1)
        side = axis.cross(ref).normalized()
        up = side.cross(axis).normalized()
        ring = [V(*base) + side * half * math.cos(math.pi / 4 + 2 * math.pi * k / sides)
                + up * half * math.sin(math.pi / 4 + 2 * math.pi * k / sides) for k in range(sides)]
        t = V(*tip)
        polys = [[ring[k], ring[(k + 1) % sides], t] for k in range(sides)] + [list(reversed(ring))]
        return self.convex(polys, rect)

    def build(self, collection, material, smooth=True):
        bmesh.ops.triangulate(self.bm, faces=list(self.bm.faces))
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        if smooth:
            mesh.shade_smooth()
        else:
            mesh.shade_flat()
        mesh.materials.append(material)
        obj = bpy.data.objects.new(self.name, mesh)
        obj.matrix_world = Matrix.Translation(G(self.pivot))
        collection.objects.link(obj)
        obj["pivot"] = list(self.pivot)
        obj["parent"] = self.parent or ""
        obj["smooth"] = smooth
        return obj


def frames(centres, ref_up):
    """Achse, Seite und Oben je Ring - Oben wird mitgefuehrt (keine Verdrehung)."""
    out = []
    up = ref_up.normalized()
    for i in range(len(centres)):
        axis = (centres[min(i + 1, len(centres) - 1)] - centres[max(i - 1, 0)]).normalized()
        up = (up - axis * up.dot(axis)).normalized()
        out.append((axis, axis.cross(up).normalized(), up))
    return out


def tube(centres, shapes, sides, ref_up=V(0, 1, 0), mods=None):
    """Ringe um eine Mittellinie. shapes[i] = (halbe Breite, Tiefe unten, Hoehe oben). Ecke 0 unten, sides/2 oben."""
    rings = []
    for i, ((axis, side, up), c, (w, b, t)) in enumerate(zip(frames(centres, ref_up), centres, shapes)):
        ring = []
        for k in range(sides):
            th = 2 * math.pi * k / sides
            f = mods(i, k) if mods else 1.0
            down = -math.cos(th)
            ring.append(c + side * (w * math.sin(th) * f) + up * (down * (t if down > 0 else b) * f))
        rings.append(ring)
    return rings


def rects_from_pixels(pixel_rects, size):
    """Pixel-Rechtecke (x0, y0, x1, y1; y von unten) -> UV-Rechtecke mit halbem Pixel Rand gegen Ueberbluten."""
    w, h = size if isinstance(size, tuple) else (size, size)
    return {k: ((x0 + 0.5) / w, (y0 + 0.5) / h, (x1 - 0.5) / w, (y1 - 0.5) / h)
            for k, (x0, y0, x1, y1) in pixel_rects.items()}


# --- Textur-Malerei -------------------------------------------------------------------------------------------------

def vnoise(h, w, cy, cx, seed):
    """Wertrauschen 0..1: Zufallsgitter cy x cx, bilinear auf h x w."""
    g = np.random.default_rng(seed).random((cy + 1, cx + 1))
    ys, xs = np.linspace(0, cy, h), np.linspace(0, cx, w)
    y0, x0 = np.clip(ys.astype(int), 0, cy - 1), np.clip(xs.astype(int), 0, cx - 1)
    fy, fx = (ys - y0)[:, None], (xs - x0)[None, :]
    a, b, c, d = g[y0][:, x0], g[y0][:, x0 + 1], g[y0 + 1][:, x0], g[y0 + 1][:, x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def blob(U, V_, cu, cv, ru, rv):
    return np.clip(1 - ((U - cu) / ru) ** 2 - ((V_ - cv) / rv) ** 2, 0, 1)


def mix(base, color, amount):
    return base * (1 - amount[..., None]) + np.asarray(color, float) * amount[..., None]


def quantize(img, colors, seed=1):
    """Begrenzte Palette (k-Means), wie eine Textur mit Farbtabelle."""
    px = img.reshape(-1, 3)
    rng = np.random.default_rng(seed)
    centres = px[rng.choice(len(px), colors, replace=False)].copy()
    for _ in range(12):
        idx = ((px[:, None, :] - centres[None, :, :]) ** 2).sum(-1).argmin(1)
        for c in range(colors):
            sel = px[idx == c]
            if len(sel):
                centres[c] = sel.mean(0)
    return np.round(centres[idx]).reshape(img.shape)


def paint_atlas(pixel_rects, painters, size, paint=4, colors=32, seed=1):
    """Jedes Rechteck mit seinem Maler fuellen (U, V je 0..1 im Rechteck), in paint-facher Groesse, dann
    verkleinert und auf colors Farben reduziert. Rueckgabe: Feld [y][x][rgb] 0..255, Zeile 0 unten."""
    w, h = size if isinstance(size, tuple) else (size, size)
    atlas = np.zeros((h * paint, w * paint, 3))
    for name, (x0, y0, x1, y1) in pixel_rects.items():
        ph, pw = (y1 - y0) * paint, (x1 - x0) * paint
        U, V_ = np.meshgrid((np.arange(pw) + 0.5) / pw, (np.arange(ph) + 0.5) / ph)
        atlas[y0 * paint:y1 * paint, x0 * paint:x1 * paint] = painters[name](U, V_)
    small = atlas.reshape(h, paint, w, paint, 3).mean(axis=(1, 3))
    return np.clip(quantize(np.clip(small, 0, 255), colors, seed), 0, 255)


def save_image(name, arr, path):
    """Feld als PNG speichern und als Blender-Bild (ohne Filterung genutzt) zurueckgeben."""
    h, w = arr.shape[:2]
    img = bpy.data.images.new(name, w, h)
    img.pixels = np.concatenate([arr / 255.0, np.ones((h, w, 1))], axis=2).ravel()
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    img.reload()
    return img


def export_material(name, image):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = "Closest"
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


# --- Stein fuer den Gang (64 x 64 je Flaeche, 1 m je Wiederholung) -------------------------------------------------

def _stones(size, block_w, block_h, base, seed, mortar=0.5, jitter=0.12, bevel=0.12, cracks=6):
    rng = np.random.default_rng(seed)
    p = 4
    n = size * p
    ys, xs = np.mgrid[0:n, 0:n] / p
    row = (ys // block_h).astype(int)
    xo = xs + (row % 2) * (block_w / 2)
    col = (xo // block_w).astype(int) % (size // block_w)
    lx, ly = xo % block_w, ys % block_h
    tone = rng.uniform(1 - jitter, 1 + jitter, (size // block_h + 1, size // block_w + 1))
    hue = rng.uniform(-6, 6, (size // block_h + 1, size // block_w + 1))
    img = np.broadcast_to(np.asarray(base, float), (n, n, 3)).copy()
    img *= tone[row, col][..., None]
    img[..., 2] += hue[row, col]
    img *= (0.88 + 0.24 * vnoise(n, n, 16, 16, seed + 1))[..., None]
    img *= (0.94 + 0.12 * vnoise(n, n, 48, 48, seed + 2))[..., None]
    top = (ly > block_h - 1.6).astype(float)
    bottom = (ly < 1.4).astype(float)
    img *= (1 + bevel * top - bevel * 0.8 * bottom)[..., None]
    joint = (lx < 1.2) | (ly < 1.0)
    img[joint] *= mortar
    for _ in range(cracks):
        cx, cy = rng.uniform(0, size), rng.uniform(0, size)
        ang = rng.uniform(0, math.pi)
        for t in np.linspace(0, rng.uniform(3, 9), 40):
            x = int((cx + math.cos(ang) * t + rng.normal(0, 0.3)) * p) % n
            y = int((cy + math.sin(ang) * t + rng.normal(0, 0.3)) * p) % n
            img[y:y + 2, x:x + 2] *= 0.6
    small = img.reshape(size, p, size, p, 3).mean(axis=(1, 3))
    small *= (np.asarray(base, float).mean() / small.mean())     # mittlere Helligkeit = heutige Flaechenfarbe
    return np.clip(quantize(np.clip(small, 0, 255), 16, seed), 0, 255)


def stone_textures(out_dir):
    return {
        "wall": save_image("stein_wand", _stones(64, 32, 16, WORLD_WALL, 101), os.path.join(out_dir, "stein_wand.png")),
        "floor": save_image("stein_boden", _stones(64, 32, 32, WORLD_FLOOR, 202, mortar=0.45, jitter=0.16, cracks=10),
                            os.path.join(out_dir, "stein_boden.png")),
        "ceiling": save_image("stein_decke", _stones(64, 64, 32, WORLD_CEILING, 303, mortar=0.55),
                              os.path.join(out_dir, "stein_decke.png")),
    }


# --- Licht je Ecke --------------------------------------------------------------------------------------------------

def to_game(v):
    return V(v.x, v.z, -v.y)


def brightness(n):
    """Stufenlos (Gouraud): die sechs Helligkeiten des Spiels nach den Quadraten der Normalen gemischt."""
    g = to_game(n)
    return (g.x * g.x * (WALL_PX if g.x > 0 else WALL_MX) + g.y * g.y * (FLOOR_B if g.y > 0 else CEILING_B)
            + g.z * g.z * (WALL_PZ if g.z > 0 else WALL_MZ))


def write_light(obj):
    """Attribut 'Light' (Float, Ecken) aus der Welt-Normale nach der aktuellen Pose."""
    mesh = obj.data
    rot = obj.matrix_world.to_3x3()
    attr = mesh.color_attributes.get("Light") or mesh.color_attributes.new("Light", "FLOAT_COLOR", "CORNER")
    smooth = bool(obj.get("smooth", True))
    normals = mesh.corner_normals
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            b = brightness((rot @ (normals[li].vector if smooth else poly.normal)).normalized())
            attr.data[li].color = (b, b, b, 1.0)


# --- Posen ----------------------------------------------------------------------------------------------------------

def raise_fwd(deg):
    """Drehung um x: positiv hebt das vordere Ende (unten -> vorn -> oben). In Blender negativ um x."""
    return Matrix.Rotation(math.radians(-deg), 4, "X")


def pose(objs, angles, root=Matrix()):
    """angles: Teilname -> Grad. Teile haengen an ihren Eltern; Ursprung = Drehpunkt. root: Lage des Ganzen."""
    by = {o.name: o for o in objs}
    done = {}

    def world(name):
        if name in done:
            return done[name]
        o = by[name]
        pivot = Vector(list(o["pivot"]))
        parent = o["parent"]
        if parent:
            m = world(parent) @ Matrix.Translation(G(pivot) - G(Vector(list(by[parent]["pivot"])))) @ raise_fwd(angles.get(name, 0))
        else:
            m = root @ Matrix.Translation(G(pivot)) @ raise_fwd(angles.get(name, 0))
        done[name] = m
        return m

    for o in objs:
        o.matrix_world = world(o.name)
    bpy.context.view_layer.update()


# --- Raum -----------------------------------------------------------------------------------------------------------

def build_room(stone, half=2.5, height=3.5, back=-12.0, front=24.0):
    """Gang aus Boden, Waenden, Decke mit Steintextur (1 m je Wiederholung). Licht je Flaeche wie im Spiel."""
    coll = bpy.data.collections.new("Raum")
    bpy.context.scene.collection.children.link(coll)
    objs, step = [], 0.25

    def grid(name, tex, corner, du, dv, nu, nv):
        bm = bmesh.new()
        uv = bm.loops.layers.uv.verify()
        verts = [[bm.verts.new(corner + du * (i * step) + dv * (j * step)) for j in range(nv + 1)] for i in range(nu + 1)]
        for i in range(nu):
            for j in range(nv):
                f = bm.faces.new((verts[i][j], verts[i + 1][j], verts[i + 1][j + 1], verts[i][j + 1]))
                for loop, (a, b) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
                    loop[uv].uv = (a * step, b * step)
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new(name, mesh)
        obj["smooth"] = False
        obj["image"] = tex
        coll.objects.link(obj)
        objs.append(obj)

    length = int((front - back) / step)
    x, y, z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
    grid("floor", "floor", Vector((-half, back, 0)), x, y, int(2 * half / step), length)
    grid("ceiling", "ceiling", Vector((-half, back, height)), y, x, length, int(2 * half / step))
    grid("wall_left", "wall", Vector((-half, back, 0)), y, z, length, int(height / step))
    grid("wall_right", "wall", Vector((half, back, 0)), z, y, int(height / step), length)
    for obj in objs:
        poly = obj.data.polygons[0]
        if poly.normal.dot(Vector((0, 0, height / 2)) - poly.center) < 0:
            obj.data.flip_normals()
        obj.data.materials.append(pass_material("raum_" + obj.name, stone[obj["image"]]))
        write_light(obj)
    return objs


# --- Rendern --------------------------------------------------------------------------------------------------------

MATS = {}


def pass_material(name, image):
    """Emissions-Material: je Durchgang wird Textur, Licht oder Entfernung auf die Emission gesteckt."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = "Closest"
    image.colorspace_settings.name = "Non-Color"            # rohe sRGB-Werte durchreichen
    light = nt.nodes.new("ShaderNodeAttribute")
    light.attribute_name = "Light"
    cam = nt.nodes.new("ShaderNodeCameraData")
    MATS[mat.name] = (nt, emit, {"base": tex.outputs["Color"], "light": light.outputs["Color"],
                                 "dist": cam.outputs["View Distance"]})
    return mat


def set_pass(which):
    for nt, emit, outs in MATS.values():
        for link in list(emit.inputs["Color"].links):
            nt.links.remove(link)
        nt.links.new(outs[which], emit.inputs["Color"])


def setup_render(w, h):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 1
    scene.cycles.use_denoising = False
    scene.cycles.pixel_filter_type = "BOX"
    scene.cycles.filter_width = 0.01
    scene.cycles.max_bounces = 0
    scene.render.film_transparent = True
    scene.render.resolution_x, scene.render.resolution_y = w, h
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "OPEN_EXR"
    scene.render.image_settings.color_depth = "32"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("Leer")
    scene.world.color = (0, 0, 0)


def camera():
    cam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera"))
    bpy.context.scene.collection.objects.link(cam)
    bpy.context.scene.camera = cam
    cam.data.sensor_fit = "VERTICAL"
    cam.data.clip_start = 0.01
    cam.data.clip_end = 100
    return cam


def aim(cam, location, target):
    cam.location = location
    cam.rotation_euler = (target - location).to_track_quat("-Z", "Y").to_euler()


TMP = os.path.join(os.environ.get("TEMP", "."), "psx_passes")


def render_passes(tag, visible=None):
    """Drei Durchgaenge als Float-Felder: Grundfarbe (roh sRGB 0..1 + Deckung), Licht, Entfernung (m)."""
    os.makedirs(TMP, exist_ok=True)
    scene = bpy.context.scene
    if visible is not None:
        for o in scene.objects:
            if o.type == "MESH":
                o.hide_render = o not in visible
    result = {}
    for which in ("base", "light", "dist"):
        set_pass(which)
        path = os.path.join(TMP, f"{tag}_{which}.exr")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(path, check_existing=False)
        w, h = img.size
        result[which] = np.array(img.pixels[:]).reshape(h, w, 4)
        bpy.data.images.remove(img)
    return result


BAYER4 = (np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) + 0.5) / 16


def compose(p, mode):
    """'spiel' (entschieden 2026-11-16: Nebel als Sichtgrenze, danach 15 Bit mit 4x4-Bayer-Koernung),
    'ansicht' (ohne Nebel und Laterne), sowie fuer aeltere Probestuecke 'nebel' (ohne Koernung) und
    'dither' (Ausblenden ueber das Punktmuster, verworfen)."""
    if mode == "spiel":
        col = compose(p, "nebel")
        h, w = col.shape[:2]
        ys, xs = np.mgrid[0:h, 0:w]
        thr = BAYER4[ys % 4, xs % 4][..., None]
        return np.clip(np.floor((col + (thr - 0.5) * 8) / 8) * 8 + 4, 0, 255)
    base = p["base"][..., :3] * 255
    light = p["light"][..., :1]
    d = p["dist"][..., :1]
    covered = p["base"][..., 3:4] > 0.5
    h, w = d.shape[:2]
    if mode == "ansicht":
        return np.where(covered, base * light, FOG_COLOR)
    col = base * light * (1 - (1 - LANTERN_MINIMUM) * np.clip(d / FOG_END, 0, 1))
    if mode == "nebel":
        t = np.clip((d - FOG_START) / (FOG_END - FOG_START), 0, 1)
        fog = t * t * (3 - 2 * t)
        return np.where(covered, col * (1 - fog) + FOG_COLOR * fog, FOG_COLOR)
    ys, xs = np.mgrid[0:h, 0:w]
    thr = BAYER4[ys % 4, xs % 4][..., None]
    col = np.floor((col + (thr - 0.5) * 8) / 8) * 8 + 4
    gone = np.clip((d - DITHER_START) / (FOG_END - DITHER_START), 0, 1) > thr
    return np.where(covered & ~gone, np.clip(col, 0, 255), FOG_COLOR)


def save_png(arr, path):
    h, w = arr.shape[:2]
    img = bpy.data.images.new(os.path.basename(path), w, h)
    img.pixels = np.concatenate([np.clip(arr, 0, 255) / 255.0, np.ones((h, w, 1))], axis=2).ravel()
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    print("Bild:", path)


def load_png(path):
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    arr = np.array(img.pixels[:]).reshape(h, w, 4)[..., :3] * 255
    bpy.data.images.remove(img)
    return arr


def zoom(arr, rows, cols, factor):
    """Ausschnitt (Zeilen von unten gezaehlt) ohne Glaettung vergroessern."""
    c = arr[rows[0]:rows[1], cols[0]:cols[1]]
    return np.repeat(np.repeat(c, factor, axis=0), factor, axis=1)


def side_by_side(images, gap=6):
    h = max(a.shape[0] for a in images)
    padded = [np.concatenate([a, np.zeros((h - a.shape[0], a.shape[1], 3))], axis=0) if a.shape[0] < h else a
              for a in images]
    out = [padded[0]]
    for a in padded[1:]:
        out += [np.zeros((h, gap, 3)), a]
    return np.concatenate(out, axis=1)


def coverage(p):
    """Maske der gedeckten Pixel (fuer Messungen, wenn nur die Figur sichtbar war)."""
    return p["base"][..., 3] > 0.5


def runs(mask_row):
    """Laengen der zusammenhaengenden gedeckten Stuecke einer Bildzeile."""
    out, n = [], 0
    for v in mask_row:
        if v:
            n += 1
        elif n:
            out.append(n)
            n = 0
    if n:
        out.append(n)
    return out


# --- Uebergabe ------------------------------------------------------------------------------------------------------

def bake_scale(objs):
    """Die Skalierung der Teile (z. B. aus pose(root=Matrix.Scale(...))) in die Ecken einrechnen. Danach steht in der
    Datei nur noch die Verschiebung je Teil (der Drehpunkt) - das Spiel kennt keine skalierten Knoten (docs/modelle.md)."""
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.context.view_layer.update()


def export_glb(objs, path):
    """Teile als glb: +Y oben (= Spiel-Achsen), Textur eingebettet ohne Filterung, keine Vertex-Farben."""
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_yup=True,
                              export_normals=True, export_vertex_color="NONE", export_materials="EXPORT",
                              export_texcoords=True)
    print("Modell:", path)


def report_parts(label, objs):
    """Je Teil: Drehpunkt in Spiel-Achsen (= Knoten-Verschiebung in der Datei), Eltern-Teil, Dreiecke."""
    for o in objs:
        t = to_game(o.matrix_world.translation)
        print(f"TEIL {label} {o.name} {t.x:.4f} {t.y:.4f} {t.z:.4f} {o['parent'] or '-'} {len(o.data.polygons)}")
