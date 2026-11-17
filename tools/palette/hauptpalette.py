"""Hauptpalette von Silver & Ash (Issue #279): Fassung A aus Runde 3 des Grafikdesigners.

Erzeugt assets/palette/hauptpalette.json und assets/palette/hauptpalette.gpl (GIMP-Palette, auch von Krita, Aseprite
und anderen lesbar). Reines Python, kein Blender, kein NumPy; zwei Läufe liefern byte-gleiche Dateien.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    python tools/palette/hauptpalette.py             schreibt die beiden Dateien
    python tools/palette/hauptpalette.py --ableiten  rechnet die Stein-Reihe aus den Level-Texturen nach und
                                                     vergleicht sie mit den festen Werten (schreibt nichts)

Aufbau (Entscheidung des Menschen, 2026-11-17, siehe docs/design/art-bible.md, Abschnitt Farben):
- Die Reihe Stein hat 15 Stufen und einen Farbton. Sie ist aus dem Stein von heute gebildet: Stufe 0 ist die Nebelfarbe,
  die Stufen 1 bis 14 liegen gleichmäßig in der Helligkeit (Lab L) zwischen dem 1. und 99,5. Perzentil aller Pixel der
  neun Katakomben-Texturen; die Farbe einer Stufe ist das Mittel der Pixel, deren Helligkeit am nächsten liegt.
  Die Werte stehen unten fest (STEIN), damit die Palette nicht mit den Texturen wandert; --ableiten belegt die Herkunft.
- Reihen "grund" dürfen Flächen füllen (Stein; Haut/Knochen und Stoff für Figuren). Reihen "akzent" sind nur für kleine
  Flächen mit Zweck (Beute, Türen, Gefahr, später Props und Licht).
- Die übrigen Reihen (Haut/Knochen, Stoff, Holz, Eisen, Messing/Gold, Blutrot) sind Rampen aus Ankerpunkten (Farbton,
  Sättigung, Helligkeit in HLS) wie in Runde 2, Palette 1 "Violettgrau". Die Stufenfarben der Beute sind feste Ankerpunkte
  und stehen genau so im Spiel (ChestView.Colours); die Nebelfarbe ist Stufe 0 des Steins (LookSettings.FogColor).
- Die Reihe Werkstein (Akzent, 4 Stufen, Issue #303; Entscheidung des Menschen am Bild, 2026-11-17, Fassung "blau") ist
  aus den vier hellsten Stein-Stufen gerechnet: zu 55 % zum Grau gemischt und mal 1,45 (WERKSTEIN unten). Gleicher
  Farbton wie der Stein, heller und neutraler; nur für besondere Portale (heute: das Bosstor).

Herkunft der Methode: Rampen mit Farbton-Verschiebung nach Slynyrd "Pixelblog 1: Color Palettes" und gamedev.net
"Color Palettes" (nur Nachschlagewerke, keine fremde Palette übernommen). Alles per Skript, nichts heruntergeladen.
"""

import colorsys
import glob
import json
import math
import os
import struct
import sys
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "assets", "palette")
TEXDIR = os.path.join(ROOT, "assets", "textures")

FOG = (16, 11, 20)            # LookSettings.FogColor
WOOD = (110, 76, 46)          # Worn / Beggar (ChestView.Colours, Body)
STEEL = (92, 112, 140)        # Forged / Guard
GOLD = (214, 170, 64)         # Royal / King
BLOOD_DARK = (150, 20, 30)    # Vampiric / Vampire, dunkler Rand des Pulses
BLOOD_LIGHT = (200, 30, 40)   # heller Rand des Pulses

# Stein, 15 Stufen: aus den neun Katakomben-Texturen von 2026-11-17 abgeleitet (siehe --ableiten), dann fest.
STEIN = [FOG, (38, 34, 40), (45, 40, 47), (52, 46, 54), (61, 52, 56), (71, 61, 66), (79, 67, 73), (86, 74, 80),
         (95, 81, 88), (105, 91, 97), (113, 97, 104), (123, 105, 113), (132, 113, 121), (140, 120, 129), (151, 127, 137)]


# --- Rampen aus Ankerpunkten ------------------------------------------------------------------------------------------

def hls_of(rgb):
    h, l, s = colorsys.rgb_to_hls(*(c / 255 for c in rgb))
    return h * 360, s, l


def pin(step, rgb):
    h, s, l = hls_of(rgb)
    return (step, h, s, l, rgb)


def anchor(step, h, s, l):
    return (step, h, s, l, None)


def unwrap(h, ref):
    """Farbton fortlaufend halten (nahe an ref), damit die Mischung nicht über 360 springt."""
    while h - ref > 180:
        h -= 360
    while h - ref < -180:
        h += 360
    return h


def ramp(anchors):
    """8 Farben aus Ankerpunkten; feste Anker liefern ihre RGB-Farbe genau."""
    anchors = sorted(anchors, key=lambda a: a[0])
    chain = [anchors[0]]
    for a in anchors[1:]:
        chain.append((a[0], unwrap(a[1], chain[-1][1]), a[2], a[3], a[4]))
    out = []
    for i in range(8):
        for a, b in zip(chain, chain[1:]):
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


# Reihen außer Stein (Reihenfolge wie im JSON); (Name, Anker)
GRUND_ROWS = [
    ("Haut/Knochen", [anchor(0, 150, .22, .08), anchor(3, 85, .10, .42), anchor(7, 44, .24, .80)]),
    ("Stoff", [anchor(0, 245, .35, .10), anchor(3, 227, .35, .22), anchor(7, 215, .22, .72)]),
]
AKZENT_ROWS = [
    ("Holz", [anchor(0, 320, .28, .08), pin(3, WOOD), anchor(7, 400, .42, .62)]),
    ("Eisen", [anchor(0, 255, .22, .09), pin(4, STEEL), anchor(7, 205, .12, .80)]),
    ("Messing/Gold", [anchor(0, 20, .50, .10), pin(5, GOLD), anchor(7, 52, .70, .78)]),
    ("Blutrot", [anchor(0, 340, .50, .07), pin(3, BLOOD_DARK), pin(5, BLOOD_LIGHT), anchor(7, 370, .75, .62)]),
]


def werkstein(c):
    """Eine Stein-Stufe als Werkstein: zu 55 % zum Grau (Mittel der drei Kanäle) gemischt, dann mal 1,45."""
    grey = sum(c) / 3
    return tuple(min(255, max(0, round((0.45 * v + 0.55 * grey) * 1.45))) for v in c)


# Werkstein, 4 Stufen: aus den Stein-Stufen 11 bis 14 (Issue #303). Die Rahmen der Tueren (tools/models/door.py) bilden
# jede Wandkachel auf diese Stufen ab: die hellste Farbe der Kachel auf Stufe 3.
WERKSTEIN = [werkstein(c) for c in STEIN[11:15]]

# Wofür eine Akzent-Reihe da ist (kommt in die Datei, damit niemand sie als Flächenfarbe nimmt)
ZWECK = {
    "Holz": "Türen, Kisten der Stufe Worn; später Balken und Möbel",
    "Eisen": "Beschläge, Gitter, Kisten der Stufe Forged; später Ketten und Werkzeug",
    "Messing/Gold": "Beute der Stufe Royal, Schlüssel, Zierrat; später Kerzenleuchter",
    "Blutrot": "Blut, Treffer, Gefahr, Beute der Stufe Vampiric; nie als bloße Zierde",
    "Werkstein": "besondere Portale: Bogenläufe, Kämpfer und Sockel des Bosstors; nie als Wandfläche",
}
VERWENDUNG = {
    "Stein": "Wände, Böden, Decken (Fläche)",
    "Haut/Knochen": "Untote, Knochen (Figuren; unverändert bis der Mensch anderes entscheidet)",
    "Stoff": "Ärmel, Mäntel, Lumpen (Figuren; unverändert bis der Mensch anderes entscheidet)",
}

# Katakomben-Auswahl: die Farben, aus denen die Texturen der Ebene bestehen dürfen (Reihe, Stufe)
KATAKOMBEN = ([("Stein", i) for i in range(15)] + [("Holz", i) for i in (1, 2, 3, 4)]
              + [("Eisen", i) for i in (1, 2, 3)] + [("Haut/Knochen", i) for i in (6, 7)])

# Die Stufenfarben der Beute: feste Ankerpunkte, so wie sie im Spiel stehen (ChestView.Colours).
# "rolle" Body = Farbe des Rahmens, der Namen und der Kisten; Band = dunkler Streifen der Kistenplatzhalter.
STUFENFARBEN = [
    {"stufe": "Worn", "kiste": "Beggar", "reihe": "Holz", "body": WOOD, "band": (56, 44, 36)},
    {"stufe": "Forged", "kiste": "Guard", "reihe": "Eisen", "body": STEEL, "band": (52, 58, 70)},
    {"stufe": "Royal", "kiste": "King", "reihe": "Messing/Gold", "body": GOLD, "band": (140, 100, 36)},
    {"stufe": "Vampiric", "kiste": "Vampire", "reihe": "Blutrot", "body": BLOOD_DARK, "body_hell": BLOOD_LIGHT,
     "band": (40, 10, 14), "hinweis": "pulsiert zwischen body und body_hell"},
]


def snap5(v):
    return round(round(v * 31 / 255) * 255 / 31)


# Eine Farbe aus Runde 2 wurde um vier Stufen aufgehellt, weil sie nach der 5-Bit-Rasterung des Spiels (ADR 0010) mit einer
# Farbe der damaligen Stein-Reihen zusammenfiel (aus der Rampe: 26, 15, 22). Der Wert bleibt, wie der Mensch ihn gewählt hat.
ANPASSUNG = {("Holz", 0): (30, 19, 26)}


def build_rows():
    """Alle Reihen: Stein fest, die übrigen aus ihren Ankerpunkten, dazu die Anpassung oben."""
    grund = [("Stein", [tuple(c) for c in STEIN])]
    akzent = []
    for target, rows in ((grund, GRUND_ROWS), (akzent, AKZENT_ROWS)):
        for name, anchors in rows:
            colours = ramp(anchors)
            for (row, step), c in ANPASSUNG.items():
                if row == name:
                    colours[step] = c
            target.append((name, colours))
    akzent.append(("Werkstein", list(WERKSTEIN)))
    return grund, akzent


def zusammenfallende_5bit(rows):
    """Paare von Farben, die nach der 5-Bit-Rasterung gleich aussehen (zur Kontrolle, Ausgabe im Lauf)."""
    seen, pairs = {}, []
    for name, cs in rows:
        for i, c in enumerate(cs):
            k = tuple(snap5(v) for v in c)
            if k in seen:
                pairs.append((seen[k], (name, i)))
            seen.setdefault(k, (name, i))
    return pairs


# --- Ableitung der Stein-Reihe (nur --ableiten) -----------------------------------------------------------------------

def read_png(path):
    """Minimaler Leser für 8-Bit-RGB-PNG (die Level-Texturen); liefert Liste von (r, g, b)."""
    data = open(path, "rb").read()
    pos, idat, w, h = 8, b"", 0, 0
    while pos < len(data):
        n, kind = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + n]
        if kind == b"IHDR":
            w, h, depth, ctype = struct.unpack(">IIBB", body[:10])
            if depth != 8 or ctype != 2:
                raise ValueError(f"{path}: nur 8-Bit-RGB unterstützt")
        elif kind == b"IDAT":
            idat += body
        pos += 12 + n
    raw, stride, rows, prev = zlib.decompress(idat), w * 3, [], bytearray(w * 3)
    for y in range(h):
        f = raw[y * (stride + 1)]
        line = bytearray(raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)])
        for x in range(stride):
            a = line[x - 3] if x >= 3 else 0
            b = prev[x]
            c = prev[x - 3] if x >= 3 else 0
            if f == 1:
                line[x] = (line[x] + a) & 255
            elif f == 2:
                line[x] = (line[x] + b) & 255
            elif f == 3:
                line[x] = (line[x] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(line)
        prev = line
    return [tuple(line[i:i + 3]) for line in rows for i in range(0, stride, 3)]


def lab_l(rgb):
    def lin(c):
        c /= 255
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    r, g, b = (lin(c) for c in rgb)
    y = 0.2126 * r + 0.7152 * g + 0.0722 * b
    f = y ** (1 / 3) if y > 0.008856 else 7.787 * y + 16 / 116
    return 116 * f - 16


def percentile(sorted_values, q):
    """Lineare Interpolation wie numpy.percentile."""
    k = (len(sorted_values) - 1) * q / 100
    lo = math.floor(k)
    hi = min(lo + 1, len(sorted_values) - 1)
    return sorted_values[lo] + (sorted_values[hi] - sorted_values[lo]) * (k - lo)


def derive_stein():
    pixels = []
    for name in ("wall", "floor", "ceiling"):
        for suffix in ("", "_b", "_c"):
            pixels += read_png(os.path.join(TEXDIR, f"catacomb_{name}{suffix}.png"))
    lum = [lab_l(p) for p in pixels]
    ordered = sorted(lum)
    lo, hi = percentile(ordered, 1), percentile(ordered, 99.5)
    ramp14 = []
    for k in range(14):
        t = lo + (hi - lo) * k / 13
        w = 1.0
        sel = [i for i, v in enumerate(lum) if abs(v - t) < w]
        while len(sel) < 40:
            w += 0.5
            sel = [i for i, v in enumerate(lum) if abs(v - t) < w]
        ramp14.append(tuple(int(round(sum(pixels[i][c] for i in sel) / len(sel))) for c in range(3)))
    return [FOG] + ramp14


# --- Ausgabe ----------------------------------------------------------------------------------------------------------

def as_lists(rows):
    return [{"name": n, "farben": [list(c) for c in cs]} for n, cs in rows]


def main():
    if "--ableiten" in sys.argv:
        got = derive_stein()
        same = [tuple(c) for c in got] == [tuple(c) for c in STEIN]
        print("Stein aus den Texturen:", got)
        print("gleich den festen Werten:", same)
        sys.exit(0 if same else 1)

    grund, akzent = build_rows()
    grund_rows = as_lists(grund)
    for r in grund_rows:
        r["verwendung"] = VERWENDUNG[r["name"]]
    akzent_rows = as_lists(akzent)
    for r in akzent_rows:
        r["zweck"] = ZWECK[r["name"]]
    data = {
        "name": "Silver & Ash, Hauptpalette (Fassung A, Runde 3)",
        "stand": "2026-11-17",
        "regeln": [
            "Der heutige Stein ist der Maßstab: Die Reihe Stein (15 Stufen, ein Farbton) füllt Wände, Böden und Decken.",
            "Eine Fläche bleibt in einer Rampe: Abwechslung über Helligkeit und Varianten, nie über eingestreute Farbtöne.",
            "Reihen 'grund' dürfen Flächen füllen; Reihen 'akzent' nur kleine Flächen mit Zweck (Beute, Türen, Gefahr, später Props und Licht).",
            "Die Palette gilt zunächst für die Umgebung; Spielfigur, Waffen, Ärmel, Gegner und Kisten bleiben unverändert.",
            "Jede Ebene bekommt eine eigene Stein-Reihe mit demselben Aufbau; Blutrot und die Stufenfarben sind in allen Ebenen gleich.",
        ],
        "grund": grund_rows,
        "akzent": akzent_rows,
        "katakomben": [[n, i] for n, i in KATAKOMBEN],
        "stufenfarben": [
            {**{k: list(v) if isinstance(v, tuple) else v for k, v in s.items()}} for s in STUFENFARBEN
        ],
        "nebelfarbe": list(FOG),
        "hinweis": "Stufenfarben und Nebelfarbe sind feste Ankerpunkte und stehen genau so im Spiel "
                   "(ChestView.Colours, TierColours, LookSettings.FogColor). Die Bänder der Kistenplatzhalter (band) liegen nicht in den Reihen.",
    }
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, "hauptpalette.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)
        f.write("\n")
    with open(os.path.join(OUT, "hauptpalette.gpl"), "w", encoding="utf-8", newline="\n") as f:
        f.write("GIMP Palette\nName: Silver and Ash Hauptpalette\nColumns: 8\n#\n")
        for kind, rows in (("grund", grund), ("akzent", akzent)):
            for name, cs in rows:
                for i, c in enumerate(cs):
                    f.write(f"{c[0]:3d} {c[1]:3d} {c[2]:3d} {kind}:{name} {i}\n")
    n = sum(len(cs) for _, cs in grund + akzent)
    print("nach 5 Bit zusammenfallende Paare:", zusammenfallende_5bit(grund + akzent) or "keine")
    print(f"{n} Farben in {len(grund) + len(akzent)} Reihen nach {os.path.relpath(OUT, ROOT)}")


main()
