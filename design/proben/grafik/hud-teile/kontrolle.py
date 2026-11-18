"""Kontrolle der HUD-Bildteile (Issue #368): setzt assets/hud/hud_parts.png nach hud_parts.json zum Endbild zusammen
(ohne Schrift) und vergleicht mit design/proben/grafik/hud-vorlagen/runde6/r6-V3-final-854.png.

Aufruf:
  "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/grafik/hud-teile/kontrolle.py

Schreibt neben dieses Skript: kontrollbild-854.png, vergleich.png, bericht.txt.
Es liest nur die fertigen Asset-Dateien, so wie das Spiel sie liest (Teile ausschneiden, an layoutX/layoutY setzen, ueberblenden).
Nur die Platzierung der Teile, die von Texten abhaengen (Zeichen neben Zahlen, Taste), kommt aus der Vorlage.
"""
import sys, os, json, zlib, struct

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
VORL = os.path.join(REPO, "design", "proben", "grafik", "hud-vorlagen")
sys.path.insert(0, VORL)
import numpy as np  # noqa: E402
import hud_runde4 as H  # noqa: E402
import hud_runde3 as r3  # noqa: E402

ASSET = os.path.join(REPO, "assets", "hud")
H.NEU, H.NUMS, H.ORN, H.GW = True, False, 3, 10.5
GY = 477


def read_png(path):
    d = open(path, "rb").read()
    p = 8; idat = b""
    while p < len(d):
        n, t = struct.unpack(">I4s", d[p:p + 8])
        body = d[p + 8:p + 8 + n]
        if t == b"IHDR":
            w, h = struct.unpack(">II", body[:8])
        elif t == b"IDAT":
            idat += body
        p += 12 + n
    raw = zlib.decompress(idat)
    rows = [np.frombuffer(raw[r * (w * 4 + 1) + 1:(r + 1) * (w * 4 + 1)], np.uint8) for r in range(h)]
    assert all(raw[r * (w * 4 + 1)] == 0 for r in range(h))
    return np.stack(rows).reshape(h, w, 4)


ATLAS = read_png(os.path.join(ASSET, "hud_parts.png"))
INFO = json.load(open(os.path.join(ASSET, "hud_parts.json"), encoding="utf-8"))
T = {p["name"]: p for p in INFO["parts"]}


def part(name):
    p = T[name]
    return ATLAS[p["y"]:p["y"] + p["h"], p["x"]:p["x"] + p["w"]], p["layoutX"], p["layoutY"]


def put(L, name, at=None, clip_x=None):
    """Teil zeichnen (wie das Spiel); clip_x: nur Spalten, deren Mitte links von dieser Entwurfs-x-Lage liegt."""
    im, lx, ly = part(name)
    if at:
        lx, ly = at
    h, w = im.shape[:2]
    a = im[..., 3].astype(np.float32) / 255
    if clip_x is not None:
        cols = (lx + np.arange(w) + 0.5 + 1e-3) < clip_x
        a = a * cols[None, :]
    col = im[..., :3].astype(np.float32) / 255
    L._blend(ly, ly + h, lx, lx + w, a, col, 1.0)


def meter(L, name, frac, x0, x1):
    put(L, name + "_empty")
    put(L, name + "_full", clip_x=x0 + (x1 - x0) * frac)


def zusammen(L):
    """Reihenfolge wie build() der Vorlage (V = 3); Beispielzustand der Vorlage."""
    for i in range(2):
        put(L, f"frame_item_{i}")
    put(L, "icon_flask"); put(L, "icon_eye")
    for i, g in enumerate(("blade", "pistol", "rifle", "blunder")):
        act = i == H.ACTIVE
        put(L, f"frame_weapon_{i}" + ("_active" if act else ""))
        put(L, f"icon_{g}" + ("_active" if act else ""))
        if act:  # Nachladeschleier zeichnet das Spiel (Werte in hud_parts.json, "game")
            x = 690 + i * 41
            H.veil(L, x + 3, GY - 27, x + 35, GY - 3, H.RELOAD)
    put(L, "frame_vitals")
    meter(L, "life", H.HP, 257, 597)
    for i in range(5):
        fill = float(np.clip(H.STAM * 5 - i, 0, 1))
        put(L, f"stamina_{i}_empty")
        if fill > 0:
            sx = 257 + i * (64.8 + 4)
            put(L, f"stamina_{i}_full", clip_x=sx + 64.8 * fill)
    put(L, "frame_toast"); put(L, "icon_toast_eye")
    put(L, "frame_level")
    put(L, "frame_xp"); meter(L, "xp", H.XP, 43, 95)
    for i, g in enumerate(("flame", "shield")):
        put(L, f"frame_skill_{i}"); put(L, f"icon_{g}")
    put(L, "frame_currency"); put(L, "icon_coin"); put(L, "icon_key")
    put(L, "frame_boss"); meter(L, "boss", H.BOSS, 316, 538)
    put(L, "crosshair")
    put(L, "frame_prompt")


def kontrollbild(scene):
    return r3.compose(scene, zusammen)


def vorlage_ohne_schrift(scene):
    """Die Vorlage selbst, aber ohne Schrift (Textbreiten werden weiter gemessen, damit die Lage gleich bleibt)."""
    orig = H.Layer.text
    H.Layer.text = lambda self, fpath, size, s, x, y, color, anchor="l", shadow=None, alpha=1.0: H.textw(self, fpath, size, s)
    try:
        return r3.compose(scene, H.design(3))
    finally:
        H.Layer.text = orig


def main():
    scene = H.load_png(os.path.join(VORL, "basis-854x480.png"))
    r6 = H.load_png(os.path.join(VORL, "runde6", "r6-V3-final-854.png"))
    ctrl = kontrollbild(scene)
    ref = vorlage_ohne_schrift(scene)
    H.save_png(os.path.join(HERE, "kontrollbild-854.png"), ctrl)

    r8 = lambda a: np.rint(a * 255).astype(np.int32)   # Vergleich in Byte-Werten, wie in den PNG-Dateien
    q = lambda a, b: np.max(np.abs(r8(a) - r8(b)), axis=2)
    STUFE = 255 / 31                                    # eine Stufe der 15-Bit-Rasterung
    d_ref_any = q(ctrl, ref) > 0
    d_ref = q(ctrl, ref) > STUFE * 1.2                  # mehr als eine Stufe
    text = q(r6, ref) > 0                               # Schriftflaechen: alles, was die Vorlage mit Schrift anders zeichnet
    d_r6_any = (q(ctrl, r6) > 0) & ~text
    d_r6 = (q(ctrl, r6) > STUFE * 1.2) & ~text

    def region(m):
        ys, xs = np.nonzero(m)
        return "-" if not len(xs) else f"x {xs.min()}..{xs.max()}, y {ys.min()}..{ys.max()}"

    lines = [
        "Kontrolle der HUD-Bildteile (Issue #368)",
        f"Teile in der Tabelle: {len(INFO['parts'])}, Atlas {INFO['width']} x {INFO['height']}",
        f"Vergleich in Byte-Werten; eine Stufe der 15-Bit-Rasterung = {STUFE:.1f}. Bild: 854 x 480 = {854 * 480} Pixel.",
        f"Schriftflaechen (Endbild gegen Vorlage ohne Schrift): {int(text.sum())} Pixel",
        f"Kontrollbild gegen Vorlage ohne Schrift: {int(d_ref_any.sum())} Pixel verschieden, davon {int(d_ref.sum())} um mehr als eine Stufe ({region(d_ref)})",
        f"Kontrollbild gegen Endbild r6-V3-final-854.png ausserhalb der Schriftflaechen: {int(d_r6_any.sum())} Pixel verschieden, davon {int(d_r6.sum())} um mehr als eine Stufe ({region(d_r6)})",
    ]
    for nm, m in (("Mehr als eine Stufe, gegen die Vorlage ohne Schrift", d_ref), ("Mehr als eine Stufe, gegen das Endbild ausserhalb der Schrift", d_r6)):
        if m.any():
            lines.append(nm + " (x, y): " + ", ".join(f"({x},{y})" for y, x in list(zip(*np.nonzero(m)))[:60]))
    text_out = chr(10).join(lines) + chr(10)
    with open(os.path.join(HERE, "bericht.txt"), "w", encoding="utf-8", newline=chr(10)) as f:
        f.write(text_out)
    print(text_out)

    # Vergleichsblatt: Kontrollbild | Endbild | Unterschied (rot = Abweichung ausserhalb der Schrift, gelb = Schriftflaeche)
    diff = scene * 0.35
    diff = np.where(text[..., None], np.array([0.85, 0.75, 0.2], np.float32), diff)
    diff = np.where(d_r6[..., None], np.array([1.0, 0.1, 0.1], np.float32), diff)
    gap = 14
    cv = np.tile(np.array((20, 16, 26), np.float32) / 255, (480 + 40 + 40, 3 * 854 + 4 * gap, 1))
    for i, im in enumerate((ctrl, r6, diff)):
        cv[40:520, gap + i * (854 + gap):gap + i * (854 + gap) + 854] = im
    L = H.Layer(cv.shape[1], cv.shape[0], False); L.sx = L.sy = L.s = 1.0
    for i, t in enumerate(("Kontrollbild aus den Teilen (ohne Schrift)", "Endbild der Vorlage (mit Schrift)",
                           "Unterschied: gelb = Schriftflaeche, rot = mehr als eine Stufe")):
        L.text(H.F_BELL, 17, t, gap + i * (854 + gap) + 427, 28, H.BONE, "c")
    L.text(H.F_BELL, 16, f"Ausserhalb der Schriftflaechen: {int(d_r6_any.sum())} Pixel um bis zu eine Stufe verschieden (8-Bit-Rundung), {int(d_r6.sum())} um mehr.", gap + 4, 548, H.BONE2)
    H.save_png(os.path.join(HERE, "vergleich.png"), np.clip(cv * (1 - L.a[..., None]) + L.rgb, 0, 1))

    # Uebersicht der Teile: Atlas 2-fach auf dunklem Karogrund
    k = 2
    ah, aw = ATLAS.shape[:2]
    bg = np.zeros((ah, aw, 3), np.float32)
    yy, xx = np.mgrid[0:ah, 0:aw]
    chk = (((yy // 4) + (xx // 4)) % 2)[..., None]
    bg[:] = np.where(chk, np.array([0.18, 0.2, 0.3], np.float32), np.array([0.12, 0.14, 0.22], np.float32))
    a = ATLAS[..., 3:4].astype(np.float32) / 255
    sheet = bg * (1 - a) + ATLAS[..., :3].astype(np.float32) / 255 * a
    sheet = np.repeat(np.repeat(sheet, k, 0), k, 1)
    H.save_png(os.path.join(HERE, "teile-uebersicht.png"), sheet)


main()
