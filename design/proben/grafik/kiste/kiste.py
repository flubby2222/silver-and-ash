"""Kiste - Probestueck 2 fuer Silver & Ash.

Ein Kistenmodell fuer alle vier Stufen: gemeinsamer Kasten, Deckel (Drehpunkt am Scharnier hinten) und Schloss
vorn. Die Stufe zeigt sich an Farbe UND Form:
    Beggar  - schlicht: zwei Baender, Schloss, Scharnierleiste
    Guard   - dazu Eckbeschlaege an den vier senkrechten Kanten
    King    - Eckbeschlaege und ein Kronen-Aufsatz mitten auf dem Deckel
    Vampire - Eckbeschlaege und vier nach aussen gebogene Dornen an den Deckelecken
Die Farben stammen unveraendert aus ChestView.cs.

Exportiert je Stufe eine Datei (chest_<stufe>.glb) und rendert die Bilder der Probe.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/kiste/kiste.py

Koordinaten wie beim Ghul: Punkte in Spiel-Achsen (x seitlich, y oben, z vorn, Meter, Ursprung Mitte Boden),
P() rechnet in Blender-Achsen um (Vorderseite blickt nach -y). Export mit +Y oben = Spiel-Achsen.
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

OUT_DIR = os.path.dirname(os.path.abspath(__file__))

# --- Farben (sRGB), unveraendert aus ChestView.cs -----------------------------------------------------------------

TIERS = {
    #           Koerper           Baender/Beschlaege
    "beggar": ((110, 76, 46), (56, 44, 36)),
    "guard": ((92, 112, 140), (52, 58, 70)),
    "king": ((214, 170, 64), (140, 100, 36)),
    # Vampire pulsiert im Spiel zwischen 150, 20, 30 und 200, 30, 40; im Modell und in den Bildern die Mitte.
    "vampire": ((175, 25, 35), (40, 10, 14)),
}
BRASS = (176, 140, 72)     # Schloss
INSIDE = (18, 14, 14)      # Innenraum, Deckel-Unterseite, Schluesselloch

# Platzhalter-Rollen: Das Modell wird mit diesen Schluesseln gebaut und je Stufe umgefaerbt.
BODY, BAND = "body", "band"

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

# --- Masse (Chest.cs: 1 m breit, 0,7 m hoch, 0,7 m tief; Kasten 65 % der Hoehe wie im Platzhalter) ---------------

WIDTH, HEIGHT, DEPTH = 1.0, 0.7, 0.7
BASE_H = HEIGHT * 0.65                 # 0,455 m
HW, HD = WIDTH / 2, DEPTH / 2
OVER = 0.015                           # Deckel steht ringsum ueber: die Fuge zwischen Kasten und Deckel wird sichtbar
PROUD = 0.012                          # Baender und Beschlaege stehen so weit vor
HINGE = (0.0, BASE_H, -HD - OVER)      # Drehpunkt des Deckels: hintere Unterkante
LOCK_PIVOT = (0.0, BASE_H + 0.015, HD + 0.04)
BAND_X = 0.3                           # Mitte der Baender, wie im Platzhalter
BAND_HALF = 0.04


def P(x, up, fwd):
    """Punkt in Spiel-Achsen (x, oben, vorn) -> Blender-Achsen (x, -vorn, oben)."""
    return Vector((x, -fwd, up))


class Part:
    """Ein Teil, das sich im Spiel als Ganzes bewegt. Ursprung = Drehpunkt. Farben als Rolle oder RGB."""

    def __init__(self, name, pivot):
        self.name = name
        self.pivot = P(*pivot)
        self.bm = bmesh.new()
        self.layer = self.bm.faces.layers.int.new("color")
        self.palette = []

    def _index(self, color):
        if color not in self.palette:
            self.palette.append(color)
        return self.palette.index(color)

    def _piece(self, faces, color, down_color=None, up_color=None):
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        for f in faces:
            c = color
            if down_color is not None and f.normal.z < -0.9:
                c = down_color
            if up_color is not None and f.normal.z > 0.9:
                c = up_color
            f[self.layer] = self._index(c)

    def _v(self, x, y, z):
        return self.bm.verts.new(P(x, y, z) - self.pivot)

    def prism(self, points, offset, color, down_color=None, up_color=None):
        """Vieleck (Spiel-Achsen) entlang offset (Spiel-Achsen) ausgezogen: Seiten und zwei Deckel."""
        a = [self._v(*p) for p in points]
        b = [self._v(p[0] + offset[0], p[1] + offset[1], p[2] + offset[2]) for p in points]
        n = len(points)
        faces = [self.bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k])) for k in range(n)]
        faces += [self.bm.faces.new(a), self.bm.faces.new(list(reversed(b)))]
        self._piece(faces, color, down_color, up_color)

    def box(self, lo, hi, color, up_color=None):
        x0, y0, z0 = lo
        x1, y1, z1 = hi
        self.prism([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], (0, y1 - y0, 0), color, up_color=up_color)

    def lid_profile_x(self, profile, x0, x1, color, down_color=None):
        """Deckelform: Profil [(vorn, oben)] in der z-y-Ebene, von x0 nach x1 ausgezogen."""
        self.prism([(x0, y, z) for z, y in profile], (x1 - x0, 0, 0), color, down_color)

    def spike(self, base, tip, half, color, sides=4):
        """Pyramide (Spiel-Achsen)."""
        base_v, tip_v = P(*base), P(*tip)
        axis = (tip_v - base_v).normalized()
        side = axis.cross(Vector((0, 0, 1)))
        if side.length < 1e-4:
            side = Vector((1, 0, 0))
        side.normalize()
        up2 = side.cross(axis).normalized()
        ring = []
        for k in range(sides):
            a = math.pi / 4 + 2 * math.pi * k / sides
            ring.append(self.bm.verts.new(base_v + side * half * math.cos(a) + up2 * half * math.sin(a) - self.pivot))
        t = self.bm.verts.new(tip_v - self.pivot)
        faces = [self.bm.faces.new((ring[k], ring[(k + 1) % sides], t)) for k in range(sides)]
        faces.append(self.bm.faces.new(list(reversed(ring))))
        self._piece(faces, color)

    def build(self, collection, colors):
        bmesh.ops.triangulate(self.bm, faces=list(self.bm.faces))
        mesh = bpy.data.meshes.new(self.name)
        face_colors = [colors.get(self.palette[f[self.layer]], self.palette[f[self.layer]]) for f in self.bm.faces]
        self.bm.to_mesh(mesh)
        self.bm.free()
        for poly in mesh.polygons:
            poly.use_smooth = False
        mesh["base_colors"] = [c for col in face_colors for c in col]
        write_face_colors(mesh, "Col", face_colors)
        obj = bpy.data.objects.new(self.name, mesh)
        obj.location = self.pivot
        collection.objects.link(obj)
        return obj


def write_face_colors(mesh, name, face_colors):
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

def lid_profile(grow=0.0):
    """Deckel im Schnitt (vorn, oben) ab Unterkante: senkrechter Rand, abgeschraegte Kanten, flache Oberseite."""
    z, top, edge, flat = HD + OVER + grow, HEIGHT - BASE_H + grow, 0.11 + grow * 0.4, 0.23 + grow * 0.4
    return [(-z, 0.0), (z, 0.0), (z, edge), (flat, top), (-flat, top), (-z, edge)]


def build_base(tier):
    part = Part("base", (0, 0, 0))
    # Kasten; die Oberseite ist der dunkle Innenraum (sichtbar, sobald der Deckel offen ist).
    part.box((-HW, 0, -HD), (HW, BASE_H, HD), BODY, up_color=INSIDE)
    for s in (-1, 1):
        x = s * BAND_X
        part.box((x - BAND_HALF, -0.0, -HD - PROUD), (x + BAND_HALF, BASE_H, HD + PROUD), BAND, up_color=INSIDE)
    # Scharnierleiste hinten: macht die Rueckseite eindeutig (vorn sitzt das Schloss).
    part.box((-0.22, BASE_H - 0.05, -HD - 0.03), (0.22, BASE_H + 0.0, -HD), BAND)
    if tier != "beggar":
        # Eckbeschlaege: dreieckige Leisten auf den vier senkrechten Kanten, aussen zwei Platten, innen verdeckt.
        for sx in (-1, 1):
            for sz in (-1, 1):
                cx, cz, e, w = sx * HW, sz * HD, PROUD, 0.09
                tri = [(cx + sx * e, 0.0, cz + sz * e), (cx - sx * w, 0.0, cz + sz * e), (cx + sx * e, 0.0, cz - sz * w)]
                part.prism(tri, (0, BASE_H, 0), BAND)
    return part


def build_lid(tier):
    part = Part("lid", HINGE)
    lo = BASE_H
    profile = [(z, y + lo) for z, y in lid_profile()]
    # Unterseite in Bandfarbe (Innenseite des Deckels): offen soll sie nicht als schwarzer Block dastehen.
    part.lid_profile_x(profile, -HW - OVER, HW + OVER, BODY, down_color=BAND)
    for s in (-1, 1):
        x = s * BAND_X
        band = [(z, y + lo) for z, y in lid_profile(PROUD)]
        part.lid_profile_x(band, x - BAND_HALF, x + BAND_HALF, BAND)
    top = HEIGHT + PROUD
    if tier == "guard":
        # Tragegriff aus Eisen: zwei Pfosten und eine Stange - eckiger Buckel mit Luecke statt Zacken.
        for x in (-0.19, 0.19):
            part.box((x - 0.025, top - 0.01, -0.025), (x + 0.025, top + 0.1, 0.025), BAND)
        part.box((-0.23, top + 0.1, -0.03), (0.23, top + 0.15, 0.03), BAND)
    if tier == "king":
        # Kronen-Aufsatz: Leiste mit drei Zacken, mitten auf dem Deckel, quer zur Blickrichtung.
        part.box((-0.25, top - 0.01, -0.035), (0.25, top + 0.07, 0.035), BODY)
        for x, h in ((-0.17, 0.13), (0.0, 0.21), (0.17, 0.13)):
            part.spike((x, top + 0.07, 0.0), (x, top + 0.07 + h, 0.0), 0.06, BODY, sides=3)
    if tier == "vampire":
        # Vier Dornen an den Deckelecken, nach aussen geneigt: zackiger Umriss statt Krone.
        for sx in (-1, 1):
            for sz in (-1, 1):
                base = (sx * (HW - 0.06), top - 0.02, sz * 0.17)
                tip = (sx * (HW + 0.04), top + 0.2, sz * 0.24)
                part.spike(base, tip, 0.045, BODY)
    return part


def build_lock():
    part = Part("lock", LOCK_PIVOT)
    # Schlossplatte vorn, ragt ueber die Fuge auf den Deckelrand; dunkles Schluesselloch.
    z0 = HD + OVER
    part.box((-0.08, BASE_H - 0.17, z0), (0.08, BASE_H + 0.03, z0 + 0.04), BRASS)
    part.box((-0.018, BASE_H - 0.13, z0 + 0.04), (0.018, BASE_H - 0.05, z0 + 0.046), INSIDE)
    return part


def build_chest(tier, collection):
    body, band = TIERS[tier]
    colors = {BODY: body, BAND: band}
    parts = [build_base(tier), build_lid(tier), build_lock()]
    objs = [p.build(collection, colors) for p in parts]
    for o in objs:
        o["tier"] = tier
    return objs


def set_open(objs, angle_deg):
    """Deckel nach hinten klappen (positiv = auf). In Blender eine negative Drehung um x."""
    lid = next(o for o in objs if o.name.endswith("lid"))
    lid.rotation_euler = (math.radians(-angle_deg), 0, 0)


def place(objs, x, y=0.0):
    for o in objs:
        o.location = o.location + Vector((x, y, 0))


# --- Darstellung wie im Spiel ---------------------------------------------------------------------------------------

def to_game(v):
    return Vector((v.x, v.z, -v.y))


def brightness(normal_world):
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
    lit = [c * brightness(normal_world) for c in color]
    if camera_pos is None:
        return tuple(min(255, round(c)) for c in lit)
    d = (centre_world - camera_pos).length
    lantern = 1.0 - (1.0 - LANTERN_MINIMUM) * min(d / FOG_END, 1.0)
    fog = smoothstep(FOG_START, FOG_END, d)
    return tuple(min(255, round(c * lantern * (1 - fog) + f * fog)) for c, f in zip(lit, FOG_COLOR))


def apply_shading(objects, camera_pos):
    bpy.context.view_layer.update()
    for obj in objects:
        mesh = obj.data
        rot = obj.matrix_world.to_3x3()
        shaded = [shade(col, (rot @ poly.normal).normalized(), obj.matrix_world @ poly.center, camera_pos)
                  for poly, col in zip(mesh.polygons, base_colors(mesh))]
        write_face_colors(mesh, "Shaded", shaded)


def build_room():
    """Raum aus Boden, Waenden und Decke in Spielfarben, nur fuer die Spielbilder (nicht Teil des Modells)."""
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

    half, height, back, front = 4.5, 3.5, -12.0, 24.0
    length = int((front - back) / step)
    x, y, z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
    grid("floor", WORLD_FLOOR, Vector((-half, back, 0)), x, y, int(2 * half / step), length)
    grid("ceiling", WORLD_CEILING, Vector((-half, back, height)), y, x, length, int(2 * half / step))
    grid("wall_left", WORLD_WALL, Vector((-half, back, 0)), y, z, length, int(height / step))
    grid("wall_right", WORLD_WALL, Vector((half, back, 0)), z, y, int(height / step), length)
    for obj in objs:
        centre = Vector((0, 0, height / 2))
        poly = obj.data.polygons[0]
        if poly.normal.dot(centre - poly.center) < 0:
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
    cam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera"))
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
    return path


def show_only(scene, visible):
    for obj in scene.objects:
        if obj.type == "MESH":
            obj.hide_render = obj not in visible


def side_by_side(left, right, path):
    import numpy as np
    imgs = [bpy.data.images.load(p) for p in (left, right)]
    w, h = imgs[0].size
    a = [np.array(i.pixels[:]).reshape(h, w, 4) for i in imgs]
    gap = np.zeros((h, 4, 4))
    gap[:, :, 3] = 1
    img = bpy.data.images.new("vergleich", 2 * w + 4, h)
    img.pixels = np.concatenate([a[0], gap, a[1]], axis=1).ravel()
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    print("Bild:", path)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Kisten")
    bpy.context.scene.collection.children.link(coll)

    # Je Stufe eine Datei.
    chests = {}
    for tier in TIERS:
        objs = build_chest(tier, coll)
        chests[tier] = objs
        print("Dreiecke", tier, sum(len(o.data.polygons) for o in objs), {o.name: len(o.data.polygons) for o in objs})
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.select_set(True)
        bpy.ops.export_scene.gltf(
            filepath=os.path.join(OUT_DIR, f"chest_{tier}.glb"),
            export_format="GLB", use_selection=True, export_yup=True, export_normals=True,
            export_vertex_color="ACTIVE", export_materials="NONE", export_texcoords=False)
        # Blender braucht eindeutige Namen; im Export heissen die Teile schlicht base, lid, lock.
        for o in objs:
            o.name = f"{tier}_{o.name}"

    scene, cam = setup_scene()
    king = chests["king"]

    # Vier Ansichten der King-Kiste.
    show_only(scene, king)
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.display.render_aa = "8"
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle_y = math.radians(30)
    apply_shading(king, None)
    target = P(0, 0.4, 0)
    for name, location in {
        "ansicht_vorn": P(0, 0.9, 3.4),
        "ansicht_seite": P(3.4, 0.9, 0),
        "ansicht_hinten": P(0, 0.9, -3.4),
        "ansicht_schraeg_oben": P(-2.0, 2.6, 2.2),
    }.items():
        aim(cam, location, target)
        render(scene, os.path.join(OUT_DIR, name + ".png"))

    # Spielbedingungen: 854 x 480, 70 Grad vertikal, Augen auf 1,6 m, harte Pixel.
    room = build_room()
    scene.render.resolution_x = 854
    scene.render.resolution_y = 480
    scene.display.render_aa = "OFF"
    cam.data.angle_y = math.radians(70)
    cam.data.clip_end = 100

    def game_shot(d, name, objs, look_y=0.35):
        show_only(scene, objs + room)
        eye = P(0, 1.6, d)
        aim(cam, eye, P(0, look_y, 0))
        apply_shading(objs + room, eye)
        return render(scene, os.path.join(OUT_DIR, name + ".png"))

    # Alle vier Stufen nebeneinander, Abstand 1,6 m von Mitte zu Mitte.
    row = []
    for tier, x in zip(TIERS, (-2.4, -0.8, 0.8, 2.4)):
        place(chests[tier], x)
        row += chests[tier]
    for d in (4, 10):
        game_shot(d, f"stufen_{d}m", row)
    for tier, x in zip(TIERS, (-2.4, -0.8, 0.8, 2.4)):
        place(chests[tier], -x)

    # King geschlossen und offen (Deckel 110 Grad nach hinten, wie der Platzhalter mit 1,9 rad).
    closed = game_shot(4, "king_geschlossen_4m", king)
    set_open(king, 110)
    opened = game_shot(4, "king_offen_4m", king)
    set_open(king, 0)
    side_by_side(closed, opened, os.path.join(OUT_DIR, "king_geschlossen_offen_4m.png"))
    for p in (closed, opened):
        os.remove(p)


main()
