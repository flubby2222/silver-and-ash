"""Ghul - Probestueck 1 fuer Silver & Ash.

Erzeugt das Modell vollstaendig per Skript (nichts von Hand, nichts heruntergeladen), exportiert es als
ghul.glb und rendert sechs Bilder: vier Ansichten und zwei Bilder unter Spielbedingungen.

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
RAGS = (92, 76, 80)       # Lumpen: Rumpf und Fetzen - etwas heller als beim Platzhalter, damit sie im Nebel bleiben
SKULL = (170, 172, 144)   # fahler Schaedel: Kopf, Kiefer, Krallen, Wirbel - hellste Farbe, damit der Kopf heraussticht
EYES = (200, 226, 150)    # Akzent: kaltes, krankes Leuchten der Augen

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
        if color not in self.palette:
            self.palette.append(color)
        for f in faces:
            f[self.layer] = self.palette.index(color)

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
        (P(0, 0.84, 0.21), 0.09, 0.08),
        (P(0, 0.87, 0.31), 0.13, 0.10),
        (P(0, 0.85, 0.41), 0.11, 0.08),
        (P(0, 0.80, 0.50), 0.06, 0.04),
    ], 6, SKULL, phase=math.pi / 2)
    for s in (-1, 1):
        part.spike(P(s * 0.055, 0.86, 0.43), P(s * 0.07, 0.86, 0.47), 0.022, 0.016, 3, EYES)
    return part


def build_jaw():
    part = Part("jaw", P(*JAW_HINGE))
    part.loft([
        (P(0, 0.76, 0.27), 0.08, 0.025),
        (P(0, 0.71, 0.46), 0.045, 0.02),
    ], 4, SKULL, phase=math.pi / 4)
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


def brightness(normal_world):
    """FlatShading.Brightness aus dem Spiel."""
    n = to_game(normal_world)
    flat = max(abs(n.x), abs(n.z))
    if abs(n.y) > flat:
        return FLOOR_BRIGHTNESS if n.y > 0 else CEILING_BRIGHTNESS
    if abs(n.x) > abs(n.z):
        return WALL_PLUS_X if n.x > 0 else WALL_MINUS_X
    return WALL_PLUS_Z if n.z > 0 else WALL_MINUS_Z


def smoothstep(e0, e1, x):
    t = min(max((x - e0) / (e1 - e0), 0.0), 1.0)
    return t * t * (3 - 2 * t)


def shade(color, normal_world, centre_world, camera_pos):
    """Flat Shading, dazu (wenn camera_pos) Laterne und Nebel wie in level.fs - pro Flaeche."""
    b = brightness(normal_world)
    lit = [c * b for c in color]
    if camera_pos is None:
        return tuple(min(255, round(c)) for c in lit)
    d = (centre_world - camera_pos).length
    lantern = 1.0 - (1.0 - LANTERN_MINIMUM) * min(d / FOG_END, 1.0)
    fog = smoothstep(FOG_START, FOG_END, d)
    return tuple(min(255, round(c * lantern * (1 - fog) + f * fog)) for c, f in zip(lit, FOG_COLOR))


def apply_shading(objects, camera_pos):
    for obj in objects:
        mesh = obj.data
        rot = obj.matrix_world.to_3x3()
        colors = base_colors(mesh)
        shaded = [shade(col, (rot @ poly.normal).normalized(), obj.matrix_world @ poly.center, camera_pos)
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

    # Zwei Bilder unter Spielbedingungen: 854 x 480, 70 Grad vertikal, Augen auf 1,6 m, harte Pixel.
    room = build_room()
    scene.render.resolution_x = 854
    scene.render.resolution_y = 480
    scene.display.render_aa = "OFF"
    cam.data.angle_y = math.radians(70)
    cam.data.clip_end = 100
    for d in (4, 10):
        eye = P(0, 1.6, d)
        aim(cam, eye, P(0, 0.5, 0))
        apply_shading(model + room, eye)
        render(scene, os.path.join(OUT_DIR, f"spiel_{d}m.png"))


main()
