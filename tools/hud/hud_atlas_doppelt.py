"""HUD- und Menue-Atlas in doppelter Aufloesung (Issue #402, Teil 1 von #397): Richtung A "harte Pixel" (Issue #393).

Aufruf (Blender dient als Python mit numpy und als Renderer fuer das Metall; nichts wird heruntergeladen):
  "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/hud/hud_atlas_doppelt.py [-- <Ausgabeordner>]

Ohne Ausgabeordner schreibt das Skript nach assets/hud/hud_parts_x2.(png|json) und assets/menu/menu_parts_x2.(png|json);
mit Ausgabeordner nach <Ordner>/hud/ und <Ordner>/menu/. Die heutigen Atlanten (hud_parts, menu_parts) bleiben unberuehrt.

Aufbau: Die Teileliste (Namen, Lage, Groesse) kommt aus den heutigen Massetabellen assets/hud/hud_parts.json und
assets/menu/menu_parts.json. Jedes heutige Teil entsteht hier mit demselben Namen an doppelter Lage (layoutX/Y, x, y, w, h und
body mal 2) im 1708 x 960 grossen Entwurf. Gezeichnet wird mit der Zeichenflaeche aus tools/hud/hud_hochaufgeloest.py
(Metall-Rahmen als Cycles-Rendering, 8 feste Silberstufen, Bayer-Raster in den Verlaeufen, keine Glaettung), hier mit Alpha-Kanal.
Abweichungen (nur Zeichen von Waffen und Gegenstaenden: groesser und heller; Rahmen ohne Giebel und Fialen) stehen in
design/proben/grafik/hud-atlas-402/abweichungen.md. Ohne Schrift: Zahlen und Texte zeichnet das Spiel selbst.
Ohne Zufall ausser dem Renderer (Cycles, fester Seed, CPU); zwei Laeufe liefern bitgleiche Dateien (tools/hud/hud_atlas_pruefen.py).
"""
import sys, os, json, zlib, struct, math, importlib.util
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_ROOT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1 else None
OUT_HUD = os.path.join(OUT_ROOT, "hud") if OUT_ROOT else os.path.join(REPO, "assets", "hud")
OUT_MENU = os.path.join(OUT_ROOT, "menu") if OUT_ROOT else os.path.join(REPO, "assets", "menu")

# Die Vorlage hud_hochaufgeloest.py (Probestueck aus #393) wird als Modul geladen; ihr Hauptteil laeuft nur bei Direktaufruf.
_spec = importlib.util.spec_from_file_location("hud_hoch", os.path.join(HERE, "hud_hochaufgeloest.py"))
HHM = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(HHM)
HB = HHM.HB
c01 = HHM.c01

S = 2  # Massstab gegenueber dem heutigen Atlas (854 x 480 -> 1708 x 960)
DW, DH = 854 * S, 480 * S

# ---------------------------------------------------------------- Farben (heller als im Probestueck, Issue #402)
BONE, BONE3 = HHM.BONE, HHM.BONE3
BRASS, BRASS_M, BRASS_L = HHM.BRASS, HHM.BRASS_M, HHM.BRASS_L
INK, PANEL, FOG = HHM.INK, HHM.PANEL, HHM.FOG
RED2, RED5, RED6 = HHM.RED2, HHM.RED5, HHM.RED6
GOLD_W = (238, 228, 160)
RIM = (88, 90, 96)  # Rand der Leisten: neutrales Silber
BONE_B, BONE_B2 = (240, 234, 216), (178, 186, 150)          # Zeichen aus Bein: heller als BONE/BONE3
SIL_B, SIL_B2 = (222, 228, 234), (160, 170, 180)            # Zeichen aus Silber
WEAPON = ((228, 224, 202), (162, 168, 140))                 # Waffe in Ruhe
WEAPON_ACT = ((246, 242, 224), (198, 202, 168))             # gewaehlte Waffe
ICON_PAD = 3    # Zeichen: 3 px Rand je Seite mehr als doppelt (Schatten und Kantenstufen)
K_ICON = 1.15                                               # Zeichen von Waffen und Gegenstaenden: 15 % groesser als heute

# ---------------------------------------------------------------- zusaetzliche Zeichen (Formen wie in hud_teile.py, Issue #381)
sd_box, sd_circle, sd_poly, sd_seg, u_ = HB.sd_box, HB.sd_circle, HB.sd_poly, HB.sd_seg, HB.u_
HB.ICON["club"] = (
    u_(sd_poly([(-0.95, -0.08), (0.0, -0.12), (0.3, -0.3), (0.55, -0.36), (0.85, -0.3), (1.0, 0.0), (0.85, 0.3), (0.55, 0.36),
                (0.3, 0.3), (0.0, 0.12), (-0.95, 0.08)]),
       sd_circle(-0.93, 0, 0.14),
       sd_poly([(0.3, -0.3), (0.42, -0.6), (0.56, -0.34)]), sd_poly([(0.62, -0.34), (0.76, -0.6), (0.86, -0.28)]),
       sd_poly([(0.3, 0.3), (0.42, 0.6), (0.56, 0.34)]), sd_poly([(0.62, 0.34), (0.76, 0.6), (0.86, 0.28)])),
    None)
HB.ICON["boots"] = (
    sd_poly([(-0.5, -1.0), (0.25, -1.0), (0.25, 0.05), (0.7, 0.2), (1.0, 0.45), (1.0, 0.95), (-0.5, 0.95)]),
    u_(sd_box(-0.5, -0.62, 0.25, -0.5), sd_box(-0.5, 0.62, 1.0, 0.74)))
_beads = [sd_circle(0.55 * bx, -0.45 + 0.55 * by, 0.2)
          for bx, by in ((0.0, -1.0), (0.78, -0.62), (1.0, 0.1), (0.62, 0.78), (-0.62, 0.78), (-1.0, 0.1), (-0.78, -0.62))]
HB.ICON["rosary"] = (
    u_(*_beads, sd_circle(0.0, 0.32, 0.14), sd_box(-0.09, 0.4, 0.09, 1.0), sd_box(-0.3, 0.58, 0.3, 0.74)), None)
HB.ICON["holster"] = (
    u_(sd_poly([(-0.55, -0.05), (0.6, -0.05), (0.42, 1.0), (-0.3, 1.0)]),
       sd_poly([(-0.5, -0.05), (0.0, -0.05), (0.35, -0.85), (-0.15, -0.95)]),
       sd_box(-0.15, -0.95, 0.9, -0.68)),
    u_(sd_poly([(-0.58, 0.28), (0.57, 0.28), (0.55, 0.4), (-0.56, 0.4)]), sd_box(0.0, 0.2, 0.28, 0.48)))
HB.ICON["parry"] = (
    u_(sd_seg((-0.85, -0.85), (0.85, 0.85), 0.3), sd_seg((0.85, -0.85), (-0.85, 0.85), 0.3),
       sd_seg((-0.78, 0.32), (-0.32, 0.78), 0.22), sd_seg((0.78, 0.32), (0.32, 0.78), 0.22)),
    sd_circle(0.0, 0.0, 0.1))
_leech, _rings = [], []
for _k in range(11):
    _t = _k / 10.0
    _a = (0.1 + 1.45 * _t) * math.pi
    _cx, _cy = 0.1 + 0.7 * math.cos(_a), 0.15 - 0.72 * math.sin(_a)
    _leech.append(sd_circle(_cx, _cy, 0.42 - 0.26 * _t))
    if _k in (2, 4, 6, 8):
        _rings.append(sd_seg((_cx - 0.3 * math.cos(_a), _cy + 0.3 * math.sin(_a)), (_cx + 0.3 * math.cos(_a), _cy - 0.3 * math.sin(_a)), 0.1))
HB.ICON["leech"] = (u_(*_leech), u_(*_rings))
HB.ICON["ashheart"] = (
    u_(sd_circle(-0.42, -0.35, 0.52), sd_circle(0.42, -0.35, 0.52), sd_poly([(-0.92, -0.1), (0.92, -0.1), (0.0, 1.0)])),
    u_(sd_seg((0.05, -0.75), (-0.12, -0.3), 0.14), sd_seg((-0.12, -0.3), (0.14, 0.1), 0.14), sd_seg((0.14, 0.1), (-0.02, 0.55), 0.14)))


# ---------------------------------------------------------------- Zeichenflaeche mit Alpha (vormultipliziert)
class ACanvas(HHM.Canvas):
    def __init__(self):
        self.img = np.zeros((DH, DW, 4), np.float32)
        self.H, self.W = DH, DW
        self.hard = True
        self.s = float(S)

    def blend(self, x0, y0, cov, col, alpha=1.0):
        h, w = cov.shape
        xa, ya = max(x0, 0), max(y0, 0)
        xb, yb = min(x0 + w, self.W), min(y0 + h, self.H)
        if xb <= xa or yb <= ya:
            return
        c = cov[ya - y0:yb - y0, xa - x0:xb - x0, None] * alpha
        if isinstance(col, np.ndarray) and col.ndim == 3:
            rgb = col[ya - y0:yb - y0, xa - x0:xb - x0]
        else:
            rgb = np.broadcast_to(c01(col), (yb - ya, xb - xa, 3))
        col4 = np.concatenate([rgb, np.ones((yb - ya, xb - xa, 1), np.float32)], axis=2)
        reg = self.img[ya:yb, xa:xb]
        self.img[ya:yb, xa:xb] = reg * (1 - c) + col4 * c


def to_rgba8(img):
    a = img[..., 3]
    safe = np.where(a > 0, a, 1.0)[..., None]
    rgb = np.clip(img[..., :3] / safe, 0, 1)
    a8 = np.rint(a * 255).astype(np.uint8)
    out = np.zeros(a.shape + (4,), np.uint8)
    out[..., :3] = np.rint(rgb * 255).astype(np.uint8)
    out[..., 3] = a8
    out[a8 == 0] = 0
    return out


# ---------------------------------------------------------------- Bausteine (Koordinaten in Einheiten des 854 x 480 Entwurfs)
def bar_empty(cv, r, inset):
    x, y, w, h = r
    cv.fill_rect(x, y, x + w, y + h, RIM, 0.95)
    cv.fill_rect(x + inset, y + inset, x + w - inset, y + h - inset, INK, 0.96)


def bar_full(cv, r, inset, c_top, c_bot, shine):
    x, y, w, h = r
    x0, y0, x1, y1 = x + inset, y + inset, x + w - inset, y + h - inset
    s = cv.s
    px0, py0, px1, py1 = round(x0 * s), round(y0 * s), round(x1 * s), round(y1 * s)
    cov, bx, by = cv.rect_cov(px0, py0, px1, py1, 0.0)
    cv.blend(bx, by, cov, cv.grad(bx, by, cov.shape[0], cov.shape[1], c_top, c_bot, 5))
    hi = max(1, round(0.8 * s))
    cov2, bx2, by2 = cv.rect_cov(px0, py0, px1, py0 + hi, 0.0)
    cv.blend(bx2, by2, cov2, shine, 0.8)


def diamond(cv, cx, cy, r, c1, c2):
    """Raute mit dunklem Rand (Knopf, Zeichen am offenen Reiter)."""
    s = cv.s
    for rr, col in ((r + 0.9, INK), (r, c1)):
        pts = [(cx * s, (cy - rr) * s), ((cx + rr) * s, cy * s), (cx * s, (cy + rr) * s), ((cx - rr) * s, cy * s)]
        x0, y0, x1, y1 = int(pts[3][0]) - 2, int(pts[0][1]) - 2, int(pts[1][0]) + 3, int(pts[2][1]) + 3
        cov = cv.cov_of(sd_poly(pts), x0, y0, x1, y1)
        cv.blend(x0, y0, cov, col)
    # untere Haelfte dunkler: harte Fase
    pts = [((cx - r) * s, cy * s), ((cx + r) * s, cy * s), (cx * s, (cy + r) * s)]
    x0, y0, x1, y1 = int(pts[0][0]) - 2, int(cy * s) - 2, int(pts[1][0]) + 3, int(pts[2][1]) + 3
    cv.blend(x0, y0, cv.cov_of(sd_poly(pts), x0, y0, x1, y1), c2, 0.9)


def crosshair(cv):
    for rr, col, al in ((2.2, (16, 11, 20), 0.85), (1.4, BONE, 1.0)):
        cx, cy = 427 * S, 240 * S
        x0, y0, x1, y1 = cx - 8, cy - 8, cx + 8, cy + 8
        cv.blend(x0, y0, cv.cov_of(sd_circle(cx, cy, rr * S), x0, y0, x1, y1), col, al)


# ---------------------------------------------------------------- Teile des HUD
# Zeichen: Name -> (Form, Mitte x, Mitte y, Radius, Farbe 1, Farbe 2, Zeichen-Klasse "wi" = Waffe/Gegenstand (groesser) oder "z" (1x))
def _icons():
    d = {}
    d["icon_flask"] = ("flask", 27, 462, 8, BONE_B, BONE_B2, "wi")
    d["icon_eye"] = ("eye", 61, 462, 8, SIL_B, SIL_B2, "wi")
    for i, g in enumerate(("blade", "pistol", "rifle", "blunder")):
        cx = (690 + i * 41 + 3) + 32 * (0.36 if i else 0.5)
        d[f"icon_{g}"] = (g, cx, 462, 10.5, WEAPON[0], WEAPON[1], "wi")
        d[f"icon_{g}_active"] = (g, cx, 462, 10.5, WEAPON_ACT[0], WEAPON_ACT[1], "wi")
    d["icon_club"] = ("club", 709, 462, 10.5, WEAPON[0], WEAPON[1], "wi")
    d["icon_club_active"] = ("club", 709, 462, 10.5, WEAPON_ACT[0], WEAPON_ACT[1], "wi")
    for n, g, c1, c2 in (("boots", "boots", BONE_B, BONE_B2), ("rosary", "rosary", SIL_B, SIL_B2),
                         ("holster", "holster", BONE_B, BONE_B2), ("parry", "parry", SIL_B, SIL_B2),
                         ("leech", "leech", BONE_B, BONE_B2), ("ashheart", "ashheart", (206, 212, 218), (146, 154, 164))):
        d[f"icon_{n}"] = (g, 27, 462, 8, c1, c2, "wi")
    d["icon_flame"] = ("flame", 118, 19, 7.2, BONE, BONE3, "z")
    d["icon_shield"] = ("shield", 146, 19, 7.2, BONE, BONE3, "z")
    d["icon_skillpoint"] = ("star", 172, 19, 6.5, (226, 228, 230), (163, 175, 187), "z")
    d["icon_coin"] = ("coin", 751, 20, 5.6, (190, 198, 206), (120, 130, 142), "z")
    d["icon_key"] = ("key", 818, 20, 6.2, (190, 198, 206), (120, 130, 142), "z")
    d["icon_toast_eye"] = ("eye", 18, 432, 3.2, BRASS, BRASS_M, "z")
    return d


ICONS = _icons()

# Rahmen: Name -> (Ring, Fase, Nieten); aktiv = gewaehlte Waffe (warmer Stich), Rest gleich
FRAME_STYLE = {
    "frame_item": (2.8, 2.6, True), "frame_weapon": (2.8, 2.6, True), "frame_vitals": (3.0, 2.8, True),
    "frame_toast": (2.4, 2.2, False), "frame_level": (2.8, 2.6, True), "frame_xp": (2.2, 2.0, False),
    "frame_skill": (2.6, 2.4, True), "frame_currency": (2.8, 2.6, True), "frame_boss": (3.0, 2.8, True),
    "frame_prompt": (2.0, 1.8, False),
}

# Leisten: Name -> (Einsatz = Randstaerke, Farbe oben, unten, Glanzkante)
BARS = {
    "life": (2, RED5, RED2, RED6), "boss": (2, BONE, BONE3, (246, 242, 226)), "xp": (2, BRASS_L, BRASS_M, (246, 232, 160)),
    "stamina": (1, (206, 210, 214), (150, 156, 164), (240, 242, 244)),
}


def draw_hud_part(cv, p):
    n, lx, ly, w, h = p["name"], p["layoutX"], p["layoutY"], p["w"], p["h"]
    if n in ICONS:
        g, cx, cy, r, c1, c2, kind = ICONS[n]
        k = K_ICON if kind == "wi" else 1.0
        det = (40, 20, 10)
        cv.icon(g, cx, cy, r * k, c1, c2, detail=det)
    elif n == "crosshair":
        crosshair(cv)
    elif n.startswith("frame_"):
        key = "_".join(n.split("_")[:2])
        ring, cham, studs = FRAME_STYLE[key]
        bx, by, bw, bh = p["body"]
        cv.frame(lx + bx, ly + by, bw, bh, "rect", ring=ring, cham=cham, studs=studs, tint=BRASS if n.endswith("_active") else None)
    else:  # Leisten: NAME_empty / NAME_full
        base, state = n.rsplit("_", 1)
        base = "stamina" if base.startswith("stamina") else base
        inset, c0, c1, sh = BARS[base]
        if state == "empty":
            bar_empty(cv, (lx, ly, w, h), inset)
        else:
            bar_full(cv, (lx, ly, w, h), inset, c0, c1, sh)


# ---------------------------------------------------------------- Teile des Menues
def draw_menu_part(cv, p, sh):
    n, lx, ly, w, h = p["name"], p["layoutX"], p["layoutY"], p["w"], p["h"]
    if n == "panel":
        cv.frame(40, 62, 774, 390, "rect", ring=3.2, cham=3.2, studs=True, fill=PANEL, fill_alpha=0.78)
    elif n == "gable":
        cv.frame(412, 186, 30, 19, "tri", ring=2.6, fill_alpha=0, studs=False)
        cv.icon("eye", 427, 197, 3.2, BRASS_L, BRASS_M, detail=(40, 20, 10))
    elif n in ("cursor", "cursor_small"):
        x0, y0, x1, y1 = sh["cursor"] if n == "cursor" else sh["cursorSmall"]
        cv.frame(x0, y0, x1 - x0, y1 - y0, "hex", ring=2.4, cham=14.0 if n == "cursor" else 12.0, studs=False, fill=(8, 7, 13),
                 fill_alpha=0.55, tint=BRASS)
    elif n.startswith("slider_"):
        r = (lx, ly, w, h)
        if n.endswith("empty"):
            bar_empty(cv, r, 2)
        else:
            bar_full(cv, r, 2, BRASS_L, BRASS_M, GOLD_W)
    elif n == "knob":
        diamond(cv, sh["knobCentre"][0], sh["knobCentre"][1], 5.2, BRASS_L, BRASS_M)
    elif n.startswith("tab_"):
        i = 0 if n.startswith("tab_map") else 1
        x0, y0, x1, y1 = sh["tabs"][i]
        active = n.endswith("_active")
        top = y0 if active else y0 + 8
        cv.frame(x0, top, x1 - x0, y1 - top, "rect", ring=2.4, cham=2.2, studs=False)
        if active:
            diamond(cv, (x0 + x1) / 2, y0 - 4, 4.0, BRASS_L, BRASS_M)
    else:
        raise SystemExit("Menue-Teil ohne Zeichnung: " + n)


# ---------------------------------------------------------------- Atlas packen und schreiben (wie in den heutigen Skripten)
def pack(parts, atlas_w):
    order = sorted(range(len(parts)), key=lambda k: (-parts[k][1].shape[0], parts[k][0]))
    x = y = rowh = 0
    pos = {}
    for k in order:
        h, w = parts[k][1].shape[:2]
        if x + w > atlas_w:
            x, y, rowh = 0, y + rowh + 1, 0
        pos[k] = (x, y)
        x += w + 1
        rowh = max(rowh, h)
    return pos, y + rowh


def write_png(path, rgba):
    h, w, _ = rgba.shape
    raw = b"".join(b"\x00" + rgba[r].tobytes() for r in range(h))

    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)) \
        + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(data)


def new_rect(p, is_icon, is_wi):
    """Rechteck des Teils im doppelten Entwurf: heutiges Rechteck mal 2; Zeichen von Waffen und Gegenstaenden um K_ICON groesser
    um dieselbe Mitte."""
    lx, ly, w, h = p["layoutX"], p["layoutY"], p["w"], p["h"]
    if not is_icon:
        return S * lx, S * ly, S * w, S * h
    k = K_ICON if is_wi else 1.0
    hw, hh = math.ceil(w * k) + ICON_PAD, math.ceil(h * k) + ICON_PAD  # Rand fuer Schatten und Kantenstufen
    cx, cy = S * lx + S * w / 2.0, S * ly + S * h / 2.0
    return int(round(cx - hw)), int(round(cy - hh)), 2 * hw, 2 * hh


def build(old_json, atlas_w, drawer, png_name, out_dir, extra):
    old = json.load(open(old_json, encoding="utf-8"))
    parts, lost_total = [], 0
    for p in old["parts"]:
        cv = ACanvas()
        is_wi = p["name"] in ICONS and ICONS[p["name"]][6] == "wi"
        nx, ny, nw, nh = new_rect(p, p["name"] in ICONS, is_wi)
        drawer(cv, dict(p, layoutX=p["layoutX"], layoutY=p["layoutY"]))
        im = to_rgba8(cv.img)
        crop = im[ny:ny + nh, nx:nx + nw].copy()
        lost = int(np.count_nonzero(im[..., 3] > 0) - np.count_nonzero(crop[..., 3] > 0))
        if lost:
            print("ABGESCHNITTEN:", p["name"], lost, "Pixel ausserhalb", (nx, ny, nw, nh), flush=True)
        lost_total += lost
        parts.append((p["name"], crop, nx, ny, p))
    pos, atlas_h = pack(parts, atlas_w)
    atlas = np.zeros((atlas_h, atlas_w, 4), np.uint8)
    table = []
    for k, (name, im, nx, ny, p) in enumerate(parts):
        x, y = pos[k]
        h, w = im.shape[:2]
        atlas[y:y + h, x:x + w] = im
        e = {"name": name, "x": x, "y": y, "w": w, "h": h, "layoutX": nx, "layoutY": ny}
        if "body" in p:
            e["body"] = [v * S for v in p["body"]]
        table.append(e)
    info = {k: old[k] for k in old if k not in ("parts", "image", "width", "height", "designWidth", "designHeight", "notes")}
    info = dict({"image": png_name, "scale": S, "width": atlas_w, "height": int(atlas_h), "designWidth": DW, "designHeight": DH,
                 "notes": ["Doppelt feiner Atlas (Issue #402): jedes Teil des heutigen Atlas mit gleichem Namen an doppelter Lage und in "
                           "doppelter Groesse; layoutX/Y, x, y, w, h, body, shapes und slice sind im Entwurf 1708 x 960 angegeben.",
                           "Ausnahme: Zeichen von Waffen und Gegenstaenden (icon_*) sind 15 % groesser um dieselbe Mitte, siehe "
                           "design/proben/grafik/hud-atlas-402/abweichungen.md.",
                           "Richtung A (harte Pixel): keine Glaettung, Silber in 8 festen Stufen, Rahmen als Metall-Relief; Teile 1:1 zeichnen."]
                 + [n for n in old["notes"] if "_empty und" in n or "NAME_" in n or "_full" in n]}, **info)
    info.update(extra(old))
    info["parts"] = table
    os.makedirs(out_dir, exist_ok=True)
    write_png(os.path.join(out_dir, png_name), atlas)
    with open(os.path.join(out_dir, png_name.replace(".png", ".json")), "w", newline="\n", encoding="utf-8") as f:
        f.write(json.dumps(info, indent=1, ensure_ascii=False) + "\n")
    print(png_name, "Teile", len(table), "Atlas", atlas_w, "x", atlas_h, "abgeschnitten", lost_total, flush=True)


def _hud_extra(old):
    g = json.loads(json.dumps(old.get("game", {})))
    if "reloadVeil" in g:
        g["reloadVeil"]["edgeWidth"] = g["reloadVeil"].get("edgeWidth", 1.2) * S
    return {"game": g}


def _menu_extra(old):
    d = lambda v: [x * S for x in v]  # noqa: E731
    sh = old["shapes"]
    return {"shapes": {"panel": d(sh["panel"]), "cursor": d(sh["cursor"]), "cursorSmall": d(sh["cursorSmall"]), "slider": d(sh["slider"]),
                       "knobCentre": d(sh["knobCentre"]), "tabs": [d(t) for t in sh["tabs"]], "gableAnchor": d(sh["gableAnchor"])},
            "slice": {k: v * S for k, v in old["slice"].items()}}


def main():
    build(os.path.join(REPO, "assets", "hud", "hud_parts.json"), 1024, lambda cv, p: draw_hud_part(cv, p), "hud_parts_x2.png",
          OUT_HUD, _hud_extra)
    menu_old = json.load(open(os.path.join(REPO, "assets", "menu", "menu_parts.json"), encoding="utf-8"))
    build(os.path.join(REPO, "assets", "menu", "menu_parts.json"), 2048, lambda cv, p: draw_menu_part(cv, p, menu_old["shapes"]),
          "menu_parts_x2.png", OUT_MENU, _menu_extra)


main()
