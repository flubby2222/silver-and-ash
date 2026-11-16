"""Ghul - Probestueck 1 fuer Silver & Ash.

Erzeugt das Modell vollstaendig per Skript (nichts von Hand, nichts heruntergeladen), exportiert es als
ghul.glb und rendert die Bilder: vier Ansichten, Spielbilder beider Lumpen-Farben aus 4 m und 10 m, die
Ausholen-Haltung und den Vergleich der Helligkeitsregeln (zweite Fassung).

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/ghul/ghul.py

Koordinaten: Das Skript beschreibt jeden Punkt in Spiel-Achsen (x seitlich, y oben, z vorn, Meter, Ursprung
zwischen den Fuessen auf dem Boden) und rechnet erst beim Anlegen in Blender-Achsen um (z oben, Blick nach -y).
Der glTF-Export mit "+Y oben" bringt das Modell wieder genau in die Spiel-Achsen.
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

OUT_DIR = os.path.dirname(os.path.abspath(__file__))

# --- Farben (sRGB 0..255). Hoechstens vier bis fuenf pro Modell, eine davon Akzent. ------------------------------

SKIN = (124, 132, 112)    # fahle, gruenliche Haut: Arme und Beine (wie der Platzhalter)
SKULL = (170, 172, 144)   # fahler Schaedel: Kopf, Kiefer, Krallen, Wirbel - hellste Farbe, damit der Kopf heraussticht
MAW = (30, 20, 26)        # Augenhoehlen und Schlund: sehr dunkel, macht den Kopf von vorn zum Gesicht (kein Rot)

# Lumpen (Rumpf, Fetzen): zwei Vorschlaege mit eigenem Farbton, weit weg vom rosagrauen Stein.
# Der erste steht im Modell (ghul.glb) und in den vier Ansichten; der zweite nur in den Spielbildern zum Vergleich.
RAGS_VARIANTS = {
    "blaugrau": (84, 100, 118),   # kaltes, schmutziges Blaugrau
    "leinen": (148, 130, 100),    # fahles Leinen, Knochenbeige
}
RAGS = RAGS_VARIANTS["blaugrau"]

# --- Werte aus dem Spiel (LookSettings.cs, level.fs) --------------------------------------------------------------

FOG_COLOR = (16, 11, 20)
FOG_START = 3.25
FOG_END = 18.2
LANTERN_MINIMUM = 0.35
FLOOR_BRIGHTNESS = 0.9
CEILING_BRIGHTNESS = 0.55
WALL_PLUS_X = 1.0
WALL_MINUS_X = 0.62
WALL_PLUS_Z = 0.82
WALL_MINUS_Z = 0.72

WORLD_WALL = (122, 104, 112)
WORLD_FLOOR = (84, 72, 78)
WORLD_CEILING = (64, 56, 66)


def P(x, up, fwd):
    """Punkt in Spiel-Achsen (x, oben, vorn) -> Blender-Achsen (x, -vorn, oben)."""
    return Vector((x, -fwd, up))


# --- Bausteine -----------------------------------------------------------------------------------------------------

class Part:
    """Ein Teil des Modells, das sich im Spiel als Ganzes bewegt. Ursprung = Drehpunkt."""

    def __init__(self, name, pivot):
        self.name = name
        self.pivot = pivot          # in Blender-Achsen
        self.bm = bmesh.new()
        self.layer = self.bm.faces.layers.int.new("color")   # Index in self.palette, uebersteht das Triangulieren
        self.palette = []

    def _piece(self, faces, color):
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        for f in faces:
            f[self.layer] = self._index(color)

    def ring(self, centre, axis, half_w, half_h, sides, phase=0.0):
        """Vieleck um eine Achse; half_w seitlich, half_h quer dazu (oben)."""
        axis = axis.normalized()
        up = Vector((0, 0, 1))
        side = axis.cross(up)
        if side.length < 1e-4:
            side = Vector((1, 0, 0))
        side.normalize()
        up2 = side.cross(axis).normalized()
        verts = []
        for k in range(sides):
            a = phase + 2 * math.pi * k / sides
            p = centre + side * (half_w * math.cos(a)) + up2 * (half_h * math.sin(a)) - self.pivot
            verts.append(self.bm.verts.new(p))
        return verts

    def loft(self, sections, sides, color, phase=0.0):
        """Gliedmasse aus Querschnitten: sections = [(mitte, halbe Breite, halbe Hoehe), ...]."""
        rings = []
        for i, (c, w, h) in enumerate(sections):
            prev_c = sections[max(i - 1, 0)][0]
            next_c = sections[min(i + 1, len(sections) - 1)][0]
            rings.append(self.ring(c, next_c - prev_c, w, h, sides, phase))
        faces = []
        for a, b in zip(rings, rings[1:]):
            for k in range(sides):
                faces.append(self.bm.faces.new((a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k])))
        faces.append(self.bm.faces.new(rings[0]))
        faces.append(self.bm.faces.new(list(reversed(rings[-1]))))
        self._piece(faces, color)
        return faces

    def face_panel(self, centre, tilt, xs, ys, depth, holes, hole_depth, color, hole_color,
                   taper=1.0, slant=0.0, back_scale=1.0):
        """Gesichtsplatte: ein Klotz, dessen Vorderseite ein Raster ist (xs von links nach rechts, ys von oben
        nach unten). holes = [(zeile, erste spalte, letzte spalte)] werden eingesenkt (Wand und Boden dunkel).
        centre in Spiel-Achsen, tilt in Grad: die Vorderseite schaut so weit nach oben.
        taper: Breite unten im Verhaeltnis zu oben (Kinn). slant: die Augenzeilen sinken zur Mitte hin um so
        viel (Meter) ab - boeser Blick. back_scale: Rueckseite kleiner, damit die Seiten schraeg werden."""
        top, bottom, half = ys[0], ys[-1], max(abs(x) for x in xs)

        def shaped(x, y, row):
            if row in (1, 2):
                y -= slant * (1 - abs(x) / half)
            return x * (1 - (1 - taper) * (top - y) / (top - bottom)), y

        t = math.radians(tilt)
        up = (0.0, math.cos(t), -math.sin(t))
        fwd = (0.0, math.sin(t), math.cos(t))

        def vert(u, v, w):
            g = [centre[i] + v * up[i] + w * fwd[i] for i in range(3)]
            return self.bm.verts.new(P(g[0] + u, g[1], g[2]) - self.pivot)

        grid = [[vert(*shaped(x, y, r), 0.0) for x in xs] for r, y in enumerate(ys)]
        hole_cells = {(r, c) for r, c0, c1 in holes for c in range(c0, c1 + 1)}
        skull, dark = [], []
        rows, cols = len(ys) - 1, len(xs) - 1
        for r in range(rows):
            for c in range(cols):
                if (r, c) not in hole_cells:
                    skull.append(self.bm.faces.new((grid[r][c], grid[r + 1][c], grid[r + 1][c + 1], grid[r][c + 1])))
        for r, c0, c1 in holes:
            rim = ([grid[r][c] for c in range(c0, c1 + 2)] + [grid[r + 1][c] for c in range(c1 + 1, c0 - 1, -1)])
            back = [self.bm.verts.new(v.co - Vector(P(*fwd)) * hole_depth) for v in rim]
            n = len(rim)
            dark += [self.bm.faces.new((rim[k], rim[(k + 1) % n], back[(k + 1) % n], back[k])) for k in range(n)]
            dark.append(self.bm.faces.new(back))
        corners = [vert(*(back_scale * c for c in shaped(x, y, -1)), -depth)
                   for x, y in ((xs[0], top), (xs[-1], top), (xs[-1], bottom), (xs[0], bottom))]
        tl, tr, br, bl = corners
        skull.append(self.bm.faces.new(list(grid[0]) + [tr, tl]))
        skull.append(self.bm.faces.new([row[-1] for row in grid] + [br, tr]))
        skull.append(self.bm.faces.new(list(reversed(grid[-1])) + [bl, br]))
        skull.append(self.bm.faces.new([row[0] for row in reversed(grid)] + [tl, bl]))
        skull.append(self.bm.faces.new(corners))
        self._piece(skull + dark, color)
        for f in dark:
            f[self.layer] = self._index(hole_color)

    def _index(self, color):
        if color not in self.palette:
            self.palette.append(color)
        return self.palette.index(color)

    def spike(self, base, tip, half_w, half_h, sides, color, phase=0.0):
        """Pyramide: Kralle, Wirbel, Fetzen."""
        ring = self.ring(base, tip - base, half_w, half_h, sides, phase)
        t = self.bm.verts.new(tip - self.pivot)
        faces = [self.bm.faces.new((ring[k], ring[(k + 1) % sides], t)) for k in range(sides)]
        faces.append(self.bm.faces.new(list(reversed(ring))))
        self._piece(faces, color)

    def build(self, collection):
        bmesh.ops.triangulate(self.bm, faces=list(self.bm.faces))
        mesh = bpy.data.meshes.new(self.name)
        colors = [self.palette[f[self.layer]] for f in self.bm.faces]
        self.bm.to_mesh(mesh)
        self.bm.free()
        for poly in mesh.polygons:
            poly.use_smooth = False
        mesh["base_colors"] = [c for col in colors for c in col]
        write_face_colors(mesh, "Col", colors)
        obj = bpy.data.objects.new(self.name, mesh)
        obj.location = self.pivot
        collection.objects.link(obj)
        return obj


def write_face_colors(mesh, name, face_colors):
    """Jede Flaeche bekommt genau eine Farbe (Ecken-Farbattribut, 8 Bit sRGB)."""
    attr = mesh.color_attributes.get(name) or mesh.color_attributes.new(name, "BYTE_COLOR", "CORNER")
    for poly, col in zip(mesh.polygons, face_colors):
        rgba = (col[0] / 255, col[1] / 255, col[2] / 255, 1.0)
        for li in poly.loop_indices:
            attr.data[li].color_srgb = rgba
    mesh.color_attributes.active_color = attr
    mesh.color_attributes.render_color_index = mesh.color_attributes.find(name)


def base_colors(mesh):
    flat = list(mesh["base_colors"])
    return [tuple(flat[i:i + 3]) for i in range(0, len(flat), 3)]


# --- Das Modell ----------------------------------------------------------------------------------------------------

SHOULDER = (0.27, 0.66, 0.14)     # Drehpunkt der Arme (x mit Seite multipliziert)
ARM_REST_TILT = 30                # so weit (Grad) zeigen die Arme in Ruhe von senkrecht unten nach vorn
NECK = (0.0, 0.74, 0.24)          # Drehpunkt des Kopfes
JAW_HINGE = (0.0, 0.77, 0.30)     # Drehpunkt des Kiefers


def build_body():
    part = Part("body", P(0, 0, 0))
    # Rumpf: Becken hinten, Buckel, breite Schultern, duenner Hals - von hinten nach vorn ansteigend.
    part.loft([
        (P(0, 0.40, -0.28), 0.17, 0.12),
        (P(0, 0.62, -0.08), 0.22, 0.17),
        (P(0, 0.67, 0.14), 0.27, 0.11),
        (P(0, 0.74, 0.25), 0.06, 0.05),
    ], 6, RAGS, phase=math.pi / 2)
    for s in (-1, 1):
        # Beine: Froschhaltung, Knie seitlich nach vorn, Fuesse unter dem Becken.
        part.loft([
            (P(s * 0.12, 0.40, -0.24), 0.075, 0.075),
            (P(s * 0.27, 0.30, 0.00), 0.06, 0.06),
            (P(s * 0.23, 0.06, -0.20), 0.045, 0.045),
            (P(s * 0.23, 0.02, -0.04), 0.05, 0.018),
        ], 4, SKIN, phase=math.pi / 4)
        # Fetzen am Becken, nach hinten unten haengend.
        part.spike(P(s * 0.08, 0.36, -0.36), P(s * 0.12, 0.12, -0.46), 0.06, 0.012, 3, RAGS)
    # Wirbel auf dem Buckel: zerklueftete Ruecken-Silhouette von der Seite.
    for up, fwd, tip_up, tip_fwd in ((0.80, -0.14, 0.88, -0.20), (0.79, -0.01, 0.87, -0.05), (0.74, 0.11, 0.81, 0.08)):
        part.spike(P(0, up - 0.04, fwd), P(0, tip_up, tip_fwd), 0.03, 0.03, 3, SKULL)
    return part


def build_head():
    part = Part("head", P(*NECK))
    # Schaedel: lang, vorgeschoben, vorn zur Schnauze verjuengt.
    part.loft([
        (P(0, 0.85, 0.21), 0.09, 0.08),
        (P(0, 0.88, 0.31), 0.13, 0.10),
        (P(0, 0.85, 0.42), 0.10, 0.075),
    ], 6, SKULL, phase=math.pi / 2)
    # Gesicht: flache Platte vorn, leicht nach oben gekippt (der Spieler schaut von oben). Zwei Augenhoehlen
    # (4,5 x 4 cm) und ein breites Maul (14 x 3,5 cm), durch Stege aus Schaedelfarbe getrennt.
    # Aus 4 m: Augen etwa 4 x 3 Pixel, Maul etwa 12 x 3 Pixel.
    part.face_panel(
        centre=(0.0, 0.84, 0.48), tilt=12,
        xs=(-0.10, -0.07, -0.025, 0.025, 0.07, 0.10),
        ys=(0.08, 0.05, 0.01, -0.025, -0.06, -0.08),
        depth=0.07, holes=[(1, 1, 1), (1, 3, 3), (3, 1, 3)], hole_depth=0.025,
        color=SKULL, hole_color=MAW, taper=0.7, slant=0.015, back_scale=0.6)
    return part


def build_jaw():
    part = Part("jaw", P(*JAW_HINGE))
    faces = part.loft([
        (P(0, 0.76, 0.27), 0.08, 0.025),
        (P(0, 0.71, 0.46), 0.05, 0.02),
    ], 4, SKULL, phase=math.pi / 4)
    # Oberseite des Unterkiefers = Boden des Mauls: dunkel, sichtbar sobald der Kiefer aufgeht.
    top = max(faces[:4], key=lambda f: f.normal.z)
    top[part.layer] = part._index(MAW)
    return part


def build_arm(s):
    part = Part("arm_px" if s > 0 else "arm_nx", P(s * SHOULDER[0], SHOULDER[1], SHOULDER[2]))
    # Oberarm zur Seite, Ellbogen aussen, Unterarm greift nach vorn auf den Boden.
    part.loft([
        (P(s * 0.27, 0.66, 0.14), 0.075, 0.075),
        (P(s * 0.41, 0.38, 0.12), 0.06, 0.06),
        (P(s * 0.36, 0.08, 0.42), 0.045, 0.045),
        (P(s * 0.37, 0.03, 0.50), 0.07, 0.022),
    ], 4, SKIN, phase=math.pi / 4)
    # Drei lange Krallen, gespreizt.
    for off in (-0.045, 0.0, 0.045):
        part.spike(P(s * 0.37 + off, 0.03, 0.50), P(s * 0.37 + off * 1.6, 0.0, 0.64), 0.018, 0.012, 3, SKULL)
    return part


def build_model():
    coll = bpy.data.collections.new("Ghul")
    bpy.context.scene.collection.children.link(coll)
    parts = [build_body(), build_head(), build_jaw(), build_arm(1), build_arm(-1)]
    return [p.build(coll) for p in parts]


# --- Darstellung wie im Spiel ---------------------------------------------------------------------------------------

def to_game(v):
    """Blender-Richtung -> Spiel-Richtung (x, y oben, z vorn)."""
    return Vector((v.x, v.z, -v.y))


def brightness(normal_world, smooth=False):
    """FlatShading.Brightness aus dem Spiel (sechs Stufen) - oder stufenlos zum Vergleich."""
    n = to_game(normal_world)
    if smooth:
        # Jede Richtung traegt mit dem Quadrat ihres Anteils bei (die Quadrate ergeben zusammen 1).
        # Auf den sechs Hauptrichtungen gleich wie die Spiel-Regel, dazwischen weich gemischt.
        return (n.x * n.x * (WALL_PLUS_X if n.x > 0 else WALL_MINUS_X)
                + n.y * n.y * (FLOOR_BRIGHTNESS if n.y > 0 else CEILING_BRIGHTNESS)
                + n.z * n.z * (WALL_PLUS_Z if n.z > 0 else WALL_MINUS_Z))
    flat = max(abs(n.x), abs(n.z))
    if abs(n.y) > flat:
        return FLOOR_BRIGHTNESS if n.y > 0 else CEILING_BRIGHTNESS
    if abs(n.x) > abs(n.z):
        return WALL_PLUS_X if n.x > 0 else WALL_MINUS_X
    return WALL_PLUS_Z if n.z > 0 else WALL_MINUS_Z


def smoothstep(e0, e1, x):
    t = min(max((x - e0) / (e1 - e0), 0.0), 1.0)
    return t * t * (3 - 2 * t)


def shade(color, normal_world, centre_world, camera_pos, smooth=False):
    """Flat Shading, dazu (wenn camera_pos) Laterne und Nebel wie in level.fs - pro Flaeche."""
    b = brightness(normal_world, smooth)
    lit = [c * b for c in color]
    if camera_pos is None:
        return tuple(min(255, round(c)) for c in lit)
    d = (centre_world - camera_pos).length
    lantern = 1.0 - (1.0 - LANTERN_MINIMUM) * min(d / FOG_END, 1.0)
    fog = smoothstep(FOG_START, FOG_END, d)
    return tuple(min(255, round(c * lantern * (1 - fog) + f * fog)) for c, f in zip(lit, FOG_COLOR))


def apply_shading(objects, camera_pos, rags=RAGS, smooth=False):
    """rags ersetzt die Lumpenfarbe nur fuers Bild (Vergleich der Varianten)."""
    for obj in objects:
        mesh = obj.data
        rot = obj.matrix_world.to_3x3()
        colors = [rags if col == RAGS else col for col in base_colors(mesh)]
        shaded = [shade(col, (rot @ poly.normal).normalized(), obj.matrix_world @ poly.center, camera_pos, smooth)
                  for poly, col in zip(mesh.polygons, colors)]
        write_face_colors(mesh, "Shaded", shaded)


def build_room():
    """Gang aus Boden, zwei Waenden und Decke in Spielfarben, nur fuer die Spielbilder (nicht Teil des Modells)."""
    coll = bpy.data.collections.new("Raum")
    bpy.context.scene.collection.children.link(coll)
    objs = []
    step = 0.25

    def grid(name, color, corner, du, dv, nu, nv):
        bm = bmesh.new()
        verts = [[bm.verts.new(corner + du * (i * step) + dv * (j * step)) for j in range(nv + 1)] for i in range(nu + 1)]
        for i in range(nu):
            for j in range(nv):
                bm.faces.new((verts[i][j], verts[i + 1][j], verts[i + 1][j + 1], verts[i][j + 1]))
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        mesh["base_colors"] = list(color) * len(mesh.polygons)
        write_face_colors(mesh, "Col", [color] * len(mesh.polygons))
        obj = bpy.data.objects.new(name, mesh)
        coll.objects.link(obj)
        objs.append(obj)

    half, height, back, front = 2.5, 3.5, -12.0, 24.0
    length = int((front - back) / step)
    x, y, z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
    grid("floor", WORLD_FLOOR, Vector((-half, back, 0)), x, y, int(2 * half / step), length)
    grid("ceiling", WORLD_CEILING, Vector((-half, back, height)), y, x, length, int(2 * half / step))
    grid("wall_left", WORLD_WALL, Vector((-half, back, 0)), y, z, length, int(height / step))
    grid("wall_right", WORLD_WALL, Vector((half, back, 0)), z, y, int(height / step), length)
    # Normalen nach innen (zum Gang) zeigen lassen.
    for obj in objs:
        centre = Vector((0, 0, height / 2))
        poly = obj.data.polygons[0]
        if (poly.normal).dot(centre - poly.center) < 0:
            obj.data.flip_normals()
    return objs


# --- Rendern -------------------------------------------------------------------------------------------------------

def srgb_to_linear(c):
    c = c / 255
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def setup_scene():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    shading = scene.display.shading
    shading.light = "FLAT"
    shading.color_type = "VERTEX"
    shading.show_object_outline = False
    shading.show_cavity = False
    shading.show_shadows = False
    shading.show_xray = False
    shading.show_backface_culling = True
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("Nebel")
    world.color = [srgb_to_linear(c) for c in FOG_COLOR]
    scene.world = world
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.resolution_percentage = 100
    cam_data = bpy.data.cameras.new("Kamera")
    cam = bpy.data.objects.new("Kamera", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return scene, cam


def aim(cam, location, target):
    cam.location = location
    cam.rotation_euler = (target - location).to_track_quat("-Z", "Y").to_euler()


def render(scene, path):
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("Bild:", path)


def pose(model, wind_up):
    """Haltung wie im Platzhalter (GhoulViews.cs): wind_up 0 = Ruhe, 1 = voll ausgeholt.

    Rumpf richtet sich um 22 Grad auf (um die Fuesse), die Arme heben sich von 25 auf 140 Grad (das Modell
    steht in Ruhe bei 30 Grad), der Kiefer geht 30 Grad auf. Positive Winkel im Spiel = nach vorn/oben drehen;
    in Blender ist das eine negative Drehung um x.
    """
    objs = {o.name: o for o in model}
    body = Matrix.Rotation(math.radians(-22 * wind_up), 4, "X")
    head = body @ Matrix.Translation(objs["head"].location.copy())
    objs["body"].matrix_world = body
    objs["head"].matrix_world = head
    jaw_local = objs["jaw"].location - objs["head"].location
    objs["jaw"].matrix_world = head @ Matrix.Translation(jaw_local) @ Matrix.Rotation(math.radians(30 * wind_up), 4, "X")
    for name in ("arm_px", "arm_nx"):
        arm_tilt = 25 + 115 * wind_up
        objs[name].matrix_world = (body @ Matrix.Translation(objs[name].location.copy())
                                   @ Matrix.Rotation(math.radians(-(arm_tilt - ARM_REST_TILT)), 4, "X"))
    bpy.context.view_layer.update()


def side_by_side(left, right, path):
    """Zwei gleich grosse Bilder nebeneinander, mit 4 Pixel Trennstrich in Nebelfarbe."""
    import numpy as np
    imgs = [bpy.data.images.load(p) for p in (left, right)]
    w, h = imgs[0].size
    a = [np.array(i.pixels[:]).reshape(h, w, 4) for i in imgs]
    gap = np.zeros((h, 4, 4))
    gap[:, :, 3] = 1
    out = np.concatenate([a[0], gap, a[1]], axis=1)
    img = bpy.data.images.new("vergleich", 2 * w + 4, h)
    img.pixels = out.ravel()
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    print("Bild:", path)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    model = build_model()

    tris = sum(len(o.data.polygons) for o in model)
    print("Dreiecke:", tris, {o.name: len(o.data.polygons) for o in model})

    # Export: glTF binaer, +Y oben (= Spiel-Achsen), Vertex-Farben aus dem Attribut "Col", keine Materialien.
    bpy.ops.object.select_all(action="DESELECT")
    for o in model:
        o.select_set(True)
    bpy.ops.export_scene.gltf(
        filepath=os.path.join(OUT_DIR, "ghul.glb"),
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_normals=True,
        export_vertex_color="ACTIVE",
        export_materials="NONE",
        export_texcoords=False,
    )

    scene, cam = setup_scene()

    # Vier Ansichten: flach schattiert wie im Spiel, ohne Nebel, vor der Nebelfarbe.
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.display.render_aa = "8"
    cam.data.type = "PERSP"
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle_y = math.radians(30)
    apply_shading(model, None)
    target = P(0, 0.48, 0.12)
    views = {
        "ansicht_vorn": P(0, 0.55, 3.4),
        "ansicht_seite": P(3.4, 0.55, 0.12),
        "ansicht_hinten": P(0, 0.55, -3.2),
        "ansicht_schraeg_oben": P(-2.0, 2.5, 2.2),
    }
    for name, location in views.items():
        aim(cam, location, target)
        render(scene, os.path.join(OUT_DIR, name + ".png"))

    # Bilder unter Spielbedingungen: 854 x 480, 70 Grad vertikal, Augen auf 1,6 m, harte Pixel.
    room = build_room()
    scene.render.resolution_x = 854
    scene.render.resolution_y = 480
    scene.display.render_aa = "OFF"
    cam.data.angle_y = math.radians(70)
    cam.data.clip_end = 100

    def game_shot(d, name, rags=RAGS, smooth=False):
        eye = P(0, 1.6, d)
        aim(cam, eye, P(0, 0.5, 0))
        apply_shading(model + room, eye, rags, smooth)
        path = os.path.join(OUT_DIR, name + ".png")
        render(scene, path)
        return path

    # 1. Beide Lumpen-Farben aus 4 m und 10 m.
    for variant, rags in RAGS_VARIANTS.items():
        for d in (4, 10):
            game_shot(d, f"spiel_{d}m_{variant}", rags)
    # 2. Helligkeit: Sechs-Stufen-Regel des Spiels (oben) gegen stufenlos.
    smooth = game_shot(4, "spiel_4m_stufenlos", smooth=True)
    side_by_side(os.path.join(OUT_DIR, "spiel_4m_blaugrau.png"), smooth,
                 os.path.join(OUT_DIR, "vergleich_helligkeit_4m.png"))
    # 3. Ankuendigung: Ruhe und Ausholen aus 4 m nebeneinander.
    rest = os.path.join(OUT_DIR, "spiel_4m_blaugrau.png")
    pose(model, 1.0)
    wind_up = game_shot(4, "spiel_4m_ausholen")
    side_by_side(rest, wind_up, os.path.join(OUT_DIR, "vergleich_ruhe_ausholen_4m.png"))
    pose(model, 0.0)


main()
