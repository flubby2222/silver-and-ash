"""HUD-Bildteile fuer das Spiel (Issue #368): erzeugt aus den Regeln der abgenommenen Vorlage (A3, Verzierung V3, Runde 6)
ein Bild mit Massetabelle: assets/hud/hud_parts.png und assets/hud/hud_parts.json.

Aufruf (Blender dient nur als Python mit numpy; nichts wird heruntergeladen):
  "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python tools/hud/hud_teile.py [-- <Ausgabeordner>]

Ohne Ausgabeordner schreibt das Skript nach assets/hud. Der Ordner muss der erste Wert nach "--" sein, sonst legt hud_blaetter.py
einen Ordner mit dem Namen des Werts an.

Die Zeichenfunktionen (cell, trough, segs, glyph, ...) kommen unveraendert aus
design/proben/grafik/hud-vorlagen/hud_runde4.py; hier wird nur jedes Teil einzeln an seinem Platz im 854 x 480 grossen
Bild gezeichnet und zugeschnitten. Die Koernung haengt von der Bildposition ab, deshalb entsteht jedes Teil an seiner Layout-Position.
Ohne Schrift: Zahlen und Texte zeichnet das Spiel selbst.

Alle Teile sind bei 854 x 480 gemeint und werden 1:1 (ohne Skalierung) gezeichnet. PNG: RGBA, nicht vormultipliziert, ungerastert
(die 15-Bit-Rasterung mit Bayer-Muster ist Sache des Spielbilds, ADR 0010). Ohne Zufall; zwei Laeufe liefern bitgleiche Dateien.
"""
import sys, os, json, zlib, struct

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
VORL = os.path.join(REPO, "design", "proben", "grafik", "hud-vorlagen")
sys.path.insert(0, VORL)
import numpy as np  # noqa: E402
import hud_runde4 as H  # noqa: E402

OUT_DIR = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1 \
    else os.path.join(REPO, "assets", "hud")
ATLAS_W = 512

# Stand der Endfassung (main6 der Vorlage)
H.NEU, H.NUMS, H.ORN, H.GW = True, False, 3, 10.5

GY = 477  # gemeinsame Grundlinie (Art-Bible)
WEAPONS = ("blade", "pistol", "rifle", "blunder")
AMMO_HAS = (False, True, True, True)  # Klinge ohne Munition: Zeichen mittig, sonst links (Platz fuer die Zahl)


# ---------------------------------------------------------------- Rendern und Zuschneiden


def render(draw):
    L = H.Layer(854, 480, True)
    draw(L)
    return L


def to_rgba8(L):
    """Vormultipliziert (Float) -> gerade (nicht vormultiplizierte) RGBA-Bytes; Zeilen von oben nach unten."""
    a = L.a
    safe = np.where(a > 0, a, 1.0)[..., None]
    rgb = np.clip(L.rgb / safe, 0, 1)
    a8 = np.rint(a * 255).astype(np.uint8)
    out = np.zeros(a.shape + (4,), np.uint8)
    out[..., :3] = np.rint(rgb * 255).astype(np.uint8)
    out[..., 3] = a8
    out[a8 == 0] = 0
    return out, int(np.count_nonzero((a > 0) & (a8 == 0)))


def box_of(imgs):
    """Gemeinsamer Zuschnitt: kleinstes Rechteck um alle sichtbaren Pixel (Alpha > 0) der Bilder."""
    x0 = y0 = 10 ** 6; x1 = y1 = -1
    for im in imgs:
        ys, xs = np.nonzero(im[..., 3] > 0)
        if len(xs):
            x0, x1 = min(x0, xs.min()), max(x1, xs.max() + 1)
            y0, y1 = min(y0, ys.min()), max(y1, ys.max() + 1)
    return int(x0), int(y0), int(x1), int(y1)


PARTS = []  # (Name, Bild h x w x 4, layoutX, layoutY, Gruppe)
LOST = [0]


def add(names_draws, clip=None):
    """Ein oder mehrere Teile mit gemeinsamem Zuschnitt (Paare wie leer/voll muessen gleich gross sein).
    clip = (x0, x1): nur Spalten dieses Bereichs (fuer die fuenf Segmente, die in einer Zeichnung liegen)."""
    imgs = []
    for _, draw in names_draws:
        im, lost = to_rgba8(render(draw)); LOST[0] += lost
        if clip:
            im = im.copy(); im[:, :clip[0]] = 0; im[:, clip[1]:] = 0
        imgs.append(im)
    # _full darf nur das Innere der Leiste enthalten: der halbdurchsichtige dunkle Rand liegt schon in _empty und wuerde
    # beim Daruebersetzen doppelt abdunkeln. Das Innere ist bei _empty deckend (Grund hat Alpha 1).
    for k in range(1, len(imgs)):
        if names_draws[k][0].endswith("_full"):
            imgs[k] = imgs[k].copy(); imgs[k][imgs[0][..., 3] != 255] = 0
    x0, y0, x1, y1 = box_of(imgs[:1])  # Zuschnitt nach dem ersten Bild (_empty umfasst alle weiteren)
    for (name, _), im in zip(names_draws, imgs):
        PARTS.append((name, im[y0:y1, x0:x1].copy(), x0, y0))


def frame(name, x0, y0, x1, y1, active=False, seed=2):
    add([(name, lambda L: H.cell(L, x0, y0, x1, y1, "socket", active, seed))])


def bar(name, x0, y0, x1, y1, c0, c1, back, hi, p):
    """Leiste in drei Zustaenden einer Zeichnung: _empty (Rahmen, Grund), _full (voll gefuellt, ganzer Balken mit Rahmen oben drauf).
    Das Spiel zeichnet _empty und darueber _full, links vom Fuellstand beschnitten; das entspricht der Vorlage (Fuellung links
    vom Stand, Rahmen ueber allem)."""
    add([(name + "_empty", lambda L: H.trough(L, x0, y0, x1, y1, 0.0, c0, c1, "recess", back=back, hi=None, p=p)),
         (name + "_full", lambda L: H.trough(L, x0, y0, x1, y1, 1.0, c0, c1, "recess", back=back, hi=hi, p=p))])


# ---------------------------------------------------------------- Teile, in der Reihenfolge der Vorlage (build, V = 3)

# unten links: zwei Gegenstands-Fassungen, 30 x 30, Abstand 4, ab x = 12
for i in range(2):
    frame(f"frame_item_{i}", 12 + i * 34, GY - 30, 12 + i * 34 + 30, GY, False, 8 + i)
add([("icon_flask", lambda L: H.gl(L, "flask", 12 + 15, 462, 8, H.BONE, H.BONE3, "socket", detail=(40, 20, 10)))])
add([("icon_eye", lambda L: H.gl(L, "eye", 46 + 15, 462, 8, H.IRON7, H.IRON5, "socket", detail=(40, 20, 10)))])

# unten rechts: vier Waffen-Fassungen (ruhig und aktiv), je 38 breit, Abstand 3, ab x = 690, dazu die Zeichen
for i, g in enumerate(WEAPONS):
    x = 690 + i * 41
    add([(f"frame_weapon_{i}", lambda L, x=x, i=i: H.cell(L, x, GY - 30, x + 38, GY, "socket", False, 4 + i)),
         (f"frame_weapon_{i}_active", lambda L, x=x, i=i: H.cell(L, x, GY - 30, x + 38, GY, "socket", True, 4 + i))])
    cx = (x + 3) + 32 * (0.36 if AMMO_HAS[i] else 0.5)
    add([(f"icon_{g}", lambda L, cx=cx, g=g: H.gl(L, g, cx, 462, 10.5, H.BONE2, H.IRON6, "socket")),
         (f"icon_{g}_active", lambda L, cx=cx, g=g: H.gl(L, g, cx, 462, 10.5, H.BONE, H.BONE3, "socket"))])

# unten Mitte: eine Fassung 360 x 30, Leben (Leiste 340 x 10), fuenf Ausdauer-Segmente
frame("frame_vitals", 247, GY - 30, 607, GY, False, 10)
bar("life", 257, GY - 24, 597, GY - 14, H.RED5, H.RED2, H.RED0, H.RED6, 4)
SEG_X = [257 + i * (64.8 + 4) for i in range(5)]


def _segs(stam):
    def d(L):
        H.STAM = stam
        H.segs(L, 257, 597, GY - 10, GY - 5, "recess", 5, 4)
    return d


for i, sx in enumerate(SEG_X):
    c = (int(np.floor(sx)) - 3, int(np.ceil(sx + 64.8)) + 3)
    add([(f"stamina_{i}_empty", _segs(0.0)), (f"stamina_{i}_full", _segs(1.0))], clip=c)

# Aufnahme-Meldung ueber der linken Gruppe: Rahmen 164 x 16 (Breite je nach Text; die einzige Goldflaeche ist das Auge)


def _toast(L):
    H.sframe(L, H.chamfer(8, 424, 172, 440, 2.5), 424, 440, 2.6, 9)
    L.shape((8, 424, 172, 440), H.sd_box(8 + 2.6, 424 + 2.6, 172 - 2.6, 440 - 2.6), H.IRON0, alpha=0.93)


add([("frame_toast", _toast)])
add([("icon_toast_eye", lambda L: H.glyph(L, "eye", 18, 432, 4.2, H.ROYAL, H.BRASS_M, detail=(40, 20, 10)))])

# oben links: Stufe (aktive Fassung 26 x 28), Erfahrung (Fassung 62 x 14, Leiste 52 x 6), zwei Faehigkeiten (24 x 28)
frame("frame_level", 8, 5, 34, 33, True, 14)
frame("frame_xp", 38, 12, 100, 26, False, 15)
add([("xp_empty", lambda L: H.trough(L, 43, 16, 95, 22, 0.0, H.IRON7, H.IRON5, "recess", back=H.IRON0, p=3)),
     ("xp_full", lambda L: H.trough(L, 43, 16, 95, 22, 1.0, H.IRON7, H.IRON5, "recess", back=H.IRON0, p=3))])
for i, g in enumerate(("flame", "shield")):
    x = 106 + i * 28
    frame(f"frame_skill_{i}", x, 5, x + 24, 33, False, 16 + i)
    add([(f"icon_{g}", lambda L, x=x, g=g: H.gl(L, g, x + 12, 19, 7.2, H.BONE, H.BONE3, "socket"))])
# Zeichen fuer einen offenen Skillpunkt (neu, nicht in der Vorlage): Stern aus dem Zeichensatz der Vorlage, helles Silber
add([("icon_skillpoint", lambda L: H.gl(L, "star", 172, 19, 6.5, (226, 228, 230), H.IRON6, "socket"))])

# oben rechts: Fassung 126 x 28 mit Silber und Schluessel (Lage der Zeichen haengt von der Breite der Zahl ab: hier Werte 128 und 2)
frame("frame_currency", 724, 5, 850, 33, False, 18)
add([("icon_coin", lambda L: H.glyph(L, "coin", 751, 20, 5.6, H.IRON7, H.IRON4, detail=H.IRON3))])
add([("icon_key", lambda L: H.glyph(L, "key", 818, 20, 6.2, H.IRON7, H.IRON4))])

# oben Mitte: Boss, Fassung 254 x 29, Leiste 222 x 7
frame("frame_boss", 300, 4, 554, 33, False, 19)
bar("boss", 316, 21, 538, 28, H.BONE, H.BONE3, H.IRON0, None, 3)


# Mitte: Fadenkreuz-Punkt und Fassung der Bedienungstaste (Taste und Text zeichnet das Spiel; Lage haengt von der Textbreite ab)
def _cross(L):
    L.shape((420, 233, 434, 247), H.sd_circle(427, 240, 2.2), (16, 11, 20), alpha=0.85)
    L.shape((420, 233, 434, 247), H.sd_circle(427, 240, 1.4), H.BONE)


add([("crosshair", _cross)])
frame("frame_prompt", 390, 253, 407, 271, True, 2)


# ---------------------------------------------------------------- Atlas packen


def pack(parts):
    order = sorted(range(len(parts)), key=lambda k: (-parts[k][1].shape[0], parts[k][0]))
    x = y = rowh = 0
    pos = {}
    for k in order:
        h, w = parts[k][1].shape[:2]
        if x + w > ATLAS_W:
            x, y, rowh = 0, y + rowh + 1, 0
        pos[k] = (x, y)
        x += w + 1; rowh = max(rowh, h)
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


def main():
    names = [p[0] for p in PARTS]
    assert len(names) == len(set(names)), "doppelte Teilnamen"
    pos, atlas_h = pack(PARTS)
    atlas = np.zeros((atlas_h, ATLAS_W, 4), np.uint8)
    table = []
    for k, (name, im, lx, ly) in enumerate(PARTS):
        x, y = pos[k]
        h, w = im.shape[:2]
        atlas[y:y + h, x:x + w] = im
        table.append({"name": name, "x": x, "y": y, "w": w, "h": h, "layoutX": lx, "layoutY": ly})
    info = {
        "image": "hud_parts.png",
        "width": ATLAS_W, "height": int(atlas_h),
        "designWidth": 854, "designHeight": 480,
        "notes": [
            "x, y, w, h: Rechteck des Teils im Bild; layoutX, layoutY: linke obere Ecke im 854 x 480 grossen Entwurf (Abweichend davon zeichnet das Spiel nach seiner Anordnung).",
            "Teile werden 1:1 gezeichnet (Raster der Entwurfsaufloesung), nicht skaliert.",
            "NAME_empty und NAME_full (life, boss, xp, stamina_N) haben dieselbe Groesse: erst _empty, darueber _full links vom Fuellstand beschnitten.",
            "frame_weapon_N_active und icon_<waffe>_active fuer die gewaehlte Waffe; Schrift, Zahlen, Nachladeschleier und Hervorhebung am Fuellstand zeichnet das Spiel.",
            "icon_skillpoint ist neu (Stern aus dem Zeichensatz der Vorlage), icon_toast_eye ist golden (nur Aufnahme-Meldung).",
            "layoutX/Y von icon_coin, icon_key, frame_prompt und frame_toast gelten nur fuer die Beispielwerte der Vorlage (Silber 128, Schluessel 2, Text 'to open', Meldung 'Picked up Hunter's Eye').",
        ],
        "game": {
            "reloadVeil": {"color": [14, 9, 22], "alpha": 0.72, "edgeColor": list(H.IRON6), "edgeAlpha": 0.9, "edgeWidth": 1.2,
                           "note": "ueber das Waffenbild von links, Anteil = Nachladefortschritt; Rand der Fassung innen"},
            "lifeEdge": {"color": list(H.RED6), "width": 1,
                         "note": "Vorlage: 1 px helle Kante am Fuellstand der Lebensleiste, nicht Teil der Bilder"},
            "text": {"main": list(H.BONE), "second": list(H.BONE2), "shadow": list(H.FOG), "pickup": list(H.ROYAL)},
        },
        "parts": table,
    }
    os.makedirs(OUT_DIR, exist_ok=True)
    write_png(os.path.join(OUT_DIR, "hud_parts.png"), atlas)
    with open(os.path.join(OUT_DIR, "hud_parts.json"), "w", newline="\n", encoding="utf-8") as f:
        f.write(json.dumps(info, indent=1, ensure_ascii=False) + "\n")
    print("TEILE", len(table), "Atlas", ATLAS_W, "x", atlas_h, "Pixel mit Alpha < 1/255 verloren:", LOST[0])


main()
