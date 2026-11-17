"""Runde 2: drei Vorschlaege fuer die Hauptpalette (Issue #279), mit den Korrekturen (Auswahl ohne Haut-Stufen 4 und 5, kuehlere Schatten bei 1, Stein bei 3 roetlich): 8 Reihen x 8 Stufen = 64 Farben, je Reihe eine Rampe von dunkel nach hell
mit Farbton-Verschiebung. Reines Python (kein Blender noetig). Aufruf: python paletten.py   -> schreibt paletten/*.json, *.gpl

Aufbau einer Rampe: Ankerpunkte (Stufe, Farbton in Grad (fortlaufend, nicht umgebrochen), Saettigung, Helligkeit L in HLS);
dazwischen wird linear gemischt. Die Stufenfarben der Beute (TierColours/ChestView) und die Nebelfarbe sind feste Ankerpunkte und
kommen genau so (RGB) in jeder Palette vor.

Gelesen vor dem Entwurf (nur Nachschlagewerke, nichts uebernommen; keine fremde Palette kopiert):
- gamedev.net "Color Palettes", Slynyrd "Pixelblog 1: Color Palettes": Rampen mit Farbton-Verschiebung (Schatten kuehler/violett,
  Lichter waermer), gleichmaessige Abstaende der Helligkeit in der Rampe, wenige Rampen reichen.
- Wikipedia "PlayStation technical specifications", psx-spx: Texturen der PS1 hatten 4 Bit (16), 8 Bit (256) oder 15 Bit
  Farben je Textur (eine Farbtabelle je Textur); die Hauptpalette hier ist deshalb nur die Auswahl, aus der jede Textur ihre 24 bis 32
  Farben nimmt.
"""
import colorsys
import json
import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "paletten")

FOG = (16, 11, 20)
WOOD = (110, 76, 46)          # Worn / Beggar (ChestView.Colours)
STEEL = (92, 112, 140)        # Forged / Guard
GOLD = (214, 170, 64)         # Royal / King
BLOOD_DARK = (150, 20, 30)    # Vampiric, Mitte des Pulses 150..200
BLOOD_LIGHT = (200, 30, 40)

ROWS = ["Stein kalt", "Stein warm", "Holz", "Eisen", "Messing/Gold", "Stoff", "Haut/Knochen", "Blutrot"]


def hls_of(rgb):
    h, l, s = colorsys.rgb_to_hls(*(c / 255 for c in rgb))
    return h * 360, s, l


def pin(step, rgb):
    h, s, l = hls_of(rgb)
    return (step, h, s, l, rgb)


def anchor(step, h, s, l):
    return (step, h, s, l, None)


def ramp(anchors):
    """8 Farben aus Ankerpunkten; feste Anker liefern ihre RGB-Farbe genau."""
    anchors = sorted(anchors, key=lambda a: a[0])
    fixed = [anchors[0]]
    for a in anchors[1:]:
        fixed.append((a[0], u(a[1], fixed[-1][1]), a[2], a[3], a[4]))
    anchors = fixed
    out = []
    for i in range(8):
        for a, b in zip(anchors, anchors[1:]):
            if a[0] <= i <= b[0]:
                t = 0 if b[0] == a[0] else (i - a[0]) / (b[0] - a[0])
                break
        if i == a[0] and a[4]:
            out.append(a[4])
            continue
        if i == b[0] and b[4]:
            out.append(b[4])
            continue
        h = a[1] + (b[1] - a[1]) * t
        s = a[2] + (b[2] - a[2]) * t
        l = a[3] + (b[3] - a[3]) * t
        r, g, bl = colorsys.hls_to_rgb((h % 360) / 360, min(max(l, 0), 1), min(max(s, 0), 1))
        out.append((round(r * 255), round(g * 255), round(bl * 255)))
    return out


def fog_anchor():
    return (0, 275, 0.29, 0.05, FOG)


def hue_of(rgb):
    return hls_of(rgb)[0]


def u(h, ref):
    """Farbton fortlaufend halten: h so umbrechen, dass er nahe an ref liegt (sonst Sprung ueber 360)."""
    while h - ref > 180:
        h -= 360
    while h - ref < -180:
        h += 360
    return h


# Jede Palette: je Reihe eine Liste von Ankern. Steps der festen Farben: Holz 3, Eisen 4 (Stahlblau), Gold 5, Blutrot 3 und 5.
PALETTES = {
    "1_violettgrau": {
        "name": "Violettgrau (nah am heutigen Stand)",
        "rows": [
            [fog_anchor(), anchor(2, 288, .14, .225), anchor(3, 322, .09, .33), anchor(7, 378, .08, .74)],
            [anchor(0, 295, .22, .09), anchor(3, 355, .13, .34), anchor(7, 400, .20, .78)],
            [anchor(0, 320, .28, .08), pin(3, WOOD), anchor(7, 400, .42, .62)],
            [anchor(0, 255, .22, .09), pin(4, STEEL), anchor(7, 205, .12, .80)],
            [anchor(0, 20, .50, .10), pin(5, GOLD), anchor(7, 52, .70, .78)],
            [anchor(0, 245, .35, .10), anchor(3, 227, .35, .22), anchor(7, 215, .22, .72)],
            [anchor(0, 150, .22, .08), anchor(3, 85, .10, .42), anchor(7, 44, .24, .80)],
            [anchor(0, 340, .50, .07), pin(3, BLOOD_DARK), pin(5, BLOOD_LIGHT), anchor(7, 370, .75, .62)],
        ],
        "katakomben": [(0, i) for i in range(8)] + [(1, 0), (1, 1), (1, 2), (1, 4), (1, 6), (1, 7)] + [(2, i) for i in (1, 2, 3, 4)] +
                      [(3, i) for i in (1, 2, 3, 5)] + [(6, i) for i in (6, 7)],
    },
    "2_kalt_blau": {
        "name": "Kalt und blau",
        "rows": [
            [fog_anchor(), anchor(3, 235, .16, .33), anchor(7, 210, .10, .76)],
            [anchor(0, 270, .20, .09), anchor(3, 290, .07, .34), anchor(7, 395, .10, .76)],
            [anchor(0, 240, .25, .08), pin(3, WOOD), anchor(7, 392, .30, .62)],
            [anchor(0, 235, .30, .09), pin(4, STEEL), anchor(7, 195, .14, .82)],
            [anchor(0, 30, .40, .10), pin(5, GOLD), anchor(7, 58, .50, .80)],
            [anchor(0, 240, .40, .09), anchor(3, 228, .40, .22), anchor(7, 205, .25, .74)],
            [anchor(0, 200, .20, .08), anchor(3, 110, .08, .42), anchor(7, 50, .14, .80)],
            [anchor(0, 330, .45, .07), pin(3, BLOOD_DARK), pin(5, BLOOD_LIGHT), anchor(7, 365, .65, .60)],
        ],
        "katakomben": [(0, i) for i in range(8)] + [(1, 0), (1, 1), (1, 2), (1, 4), (1, 6), (1, 7)] + [(2, i) for i in (1, 2, 3, 4)] +
                      [(3, i) for i in (1, 2, 3, 5)] + [(6, i) for i in (6, 7)],
    },
    "3_warm_ocker": {
        "name": "Warm mit Braun und Ocker",
        "rows": [
            [fog_anchor(), anchor(3, 345, .07, .33), anchor(7, 372, .07, .72)],
            [anchor(0, 300, .20, .09), anchor(3, 385, .20, .33), anchor(7, 408, .34, .76)],
            [anchor(0, 350, .30, .08), pin(3, WOOD), anchor(7, 402, .50, .60)],
            [anchor(0, 280, .15, .09), pin(4, STEEL), anchor(7, 195, .08, .80)],
            [anchor(0, 15, .55, .10), pin(5, GOLD), anchor(7, 48, .75, .78)],
            [anchor(0, 260, .30, .10), anchor(3, 232, .30, .22), anchor(7, 225, .15, .70)],
            [anchor(0, 10, .20, .08), anchor(3, 60, .14, .42), anchor(7, 40, .34, .80)],
            [anchor(0, 350, .50, .07), pin(3, BLOOD_DARK), pin(5, BLOOD_LIGHT), anchor(7, 375, .80, .62)],
        ],
        "katakomben": [(0, i) for i in range(8)] + [(1, 0), (1, 1), (1, 2), (1, 4), (1, 6), (1, 7)] + [(2, i) for i in (1, 2, 3, 4)] +
                      [(3, i) for i in (1, 2, 3, 5)] + [(6, i) for i in (6, 7)],
    },
}


def snap5(v):
    return round(round(v * 31 / 255) * 255 / 31)


def lab(rgb):
    def lin(c):
        c /= 255
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    r, g, b = (lin(c) for c in rgb)
    x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047
    y = 0.2126 * r + 0.7152 * g + 0.0722 * b
    z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883
    f = lambda t: t ** (1 / 3) if t > 0.008856 else 7.787 * t + 16 / 116   # noqa: E731
    fx, fy, fz = f(x), f(y), f(z)
    return 116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz)


def dist(a, b):
    return math.dist(lab(a), lab(b))


def build(key):
    """Die Rampen; Farben, die nach der 5-Bit-Rasterung des Spiels mit einer fruehen zusammenfielen, werden um ein paar Stufen
    aufgehellt (die festen Farben - Nebel, Beute - bleiben, wie sie sind)."""
    spec = PALETTES[key]
    rows = [ramp(r) for r in spec["rows"]]
    fixed = {FOG, WOOD, STEEL, GOLD, BLOOD_DARK, BLOOD_LIGHT}
    seen = set()
    for ri in range(8):
        for si in range(8):
            c = rows[ri][si]
            while tuple(snap5(v) for v in c) in seen and c not in fixed:
                c = tuple(min(255, v + 4) for v in c)
            rows[ri][si] = c
            seen.add(tuple(snap5(v) for v in c))
    return rows


def main():
    os.makedirs(OUT, exist_ok=True)
    for key, spec in PALETTES.items():
        rows = build(key)
        flat = [(ri, si, rows[ri][si]) for ri in range(8) for si in range(8)]
        keys15 = {}
        for ri, si, c in flat:
            keys15.setdefault(tuple(snap5(v) for v in c), []).append((ri, si))
        collisions = [v for v in keys15.values() if len(v) > 1]
        steps = [dist(rows[ri][si], rows[ri][si + 1]) for ri in range(8) for si in range(7)]
        data = {
            "name": spec["name"],
            "regeln": "8 Reihen x 8 Stufen, je Reihe dunkel -> hell mit Farbton-Verschiebung. Nebelfarbe RGB 16/11/20 ist Stufe 0 der Reihe "
                      "'Stein kalt'. Holz Stufe 3, Eisen Stufe 4 (Stahlblau), Messing/Gold Stufe 5, Blutrot Stufen 3 und 5 sind die Stufenfarben "
                      "der Beute und stehen genau so im Spiel (ChestView.Colours).",
            "reihen": [{"name": ROWS[ri], "farben": [list(c) for c in rows[ri]]} for ri in range(8)],
            "katakomben": [[ROWS[r], s] for r, s in spec["katakomben"]],
            "pruefung": {"farben": 64, "verschieden_nach_5_bit": len(keys15), "zusammenfallende_paare": collisions,
                         "kleinster_abstand_in_rampe_lab": round(min(steps), 1), "mittlerer_abstand_in_rampe_lab": round(sum(steps) / len(steps), 1)},
        }
        with open(os.path.join(OUT, f"palette_{key}.json"), "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=1)
        with open(os.path.join(OUT, f"palette_{key}.gpl"), "w", encoding="utf-8") as f:
            f.write(f"GIMP Palette\nName: Silver and Ash {spec['name']}\nColumns: 8\n#\n")
            for ri in range(8):
                for si in range(8):
                    r, g, b = rows[ri][si]
                    f.write(f"{r:3d} {g:3d} {b:3d} {ROWS[ri]} {si}\n")
        print(key, "verschieden nach 5 Bit:", len(keys15), "Kollisionen:", collisions, "kleinster Abstand", round(min(steps), 1))
        for ri in range(8):
            print("  ", ROWS[ri].ljust(14), " ".join("%3d,%3d,%3d" % c for c in rows[ri]))


main()
