"""Zeichensatz der Pixelschrift A "Kapitell" fuer das Spiel (Issue #367): ein Bild mit allen Zeichen und eine Masstabelle,
assets/fonts/kapitell.png und assets/fonts/kapitell.json.

Aufruf (reines Python 3, ohne numpy, ohne Blender; nichts wird heruntergeladen):
  python tools/schrift/schrift_atlas.py [<Ausgabeordner>]

Ohne Ausgabeordner schreibt das Skript nach assets/fonts.

Die Bitmuster bleiben die einzige Quelle: Das Skript importiert font_a() aus design/proben/grafik/schrift/schrift.py
(Skelette aus glyphen.py, Serifen aus dem Code dort) und kopiert kein Muster. Neu ist hier nur das Ersatzzeichen fuer
Zeichen, die der Zeichensatz nicht hat (ein hohles Kaestchen, "Tofu"), damit ein fehlendes Zeichen sichtbar bleibt.

Bild: RGBA, Zeichen weiss und deckend, sonst durchsichtig; das Spiel faerbt beim Zeichnen ein. Jedes Zeichen steht in
einer Zelle von LINE_HEIGHT Zeilen; Zeile 0 der Zelle ist die Oberkante der Versalien, die Grundlinie liegt in Zeile 6.
Ohne Zufall und ohne Zeitstempel; zwei Laeufe liefern bitgleiche Dateien.
"""
import json
import os
import struct
import sys
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(REPO, "design", "proben", "grafik", "schrift"))
import schrift as S  # noqa: E402

OUT_DIR = sys.argv[1] if len(sys.argv) > 1 else os.path.join(REPO, "assets", "fonts")
ATLAS_W = 128
CHARSET = S.CHARSET_UP + S.CHARSET_LOW + S.CHARSET_DIG + S.CHARSET_PUN

# Ersatzzeichen: hohles Kaestchen in Versalhoehe (neu, kein Zeichen der Schrift)
FALLBACK = S.norm(S.parse("####/#..#/#..#/#..#/#..#/#..#/####"))


def write_png(path, width, height, pixels):
    """pixels: Menge (x, y) deckender weisser Punkte; alles andere durchsichtig."""
    rows = []
    for y in range(height):
        row = bytearray(b"\x00")
        for x in range(width):
            row += b"\xff\xff\xff\xff" if (x, y) in pixels else b"\x00\x00\x00\x00"
        rows.append(bytes(row))

    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)) \
        + chunk(b"IDAT", zlib.compress(b"".join(rows), 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(data)


def main():
    font = S.font_a()
    S.check(font, unique=True)
    glyphs = [(ch, font.glyphs[ch]) for ch in CHARSET]
    assert not any(frozenset(px) == frozenset(FALLBACK[0]) and w == FALLBACK[1] for _, (px, w) in glyphs), \
        "Ersatzzeichen gleicht einem Zeichen der Schrift"

    rows = {y for _, (px, _) in glyphs for _, y in px}
    assert min(rows) >= 0, "Zeichen ragt ueber die Versalhoehe"
    line_height = max(rows) + 1

    entries = glyphs + [(None, FALLBACK)]
    pixels = set()
    table = []
    fallback = None
    x = y = 0
    for ch, (px, w) in entries:
        if x + w > ATLAS_W:
            x = 0
            y += line_height + 1
        for gx, gy in px:
            pixels.add((x + gx, y + gy))
        entry = {"x": x, "y": y, "w": w}
        if ch is None:
            fallback = entry
        else:
            table.append(dict(char=ch, **entry))
        x += w + 1
    atlas_h = y + line_height

    info = {
        "image": "kapitell.png",
        "name": "A Kapitell",
        "width": ATLAS_W, "height": atlas_h,
        "lineHeight": line_height,
        "capHeight": font.cap,
        "baseline": font.base,
        "space": S.SPACE,
        "gap": S.GAP,
        "notes": [
            "Jede Zelle ist lineHeight Zeilen hoch und w Spalten breit; x, y: linke obere Ecke im Bild.",
            "Zeile 0 der Zelle ist die Oberkante der Versalien, die Grundlinie liegt in Zeile baseline.",
            "Zwischen zwei Zeichen gap Spalten Abstand, das Leerzeichen ist space Spalten breit (ohne eigene Zelle).",
            "fallback: Ersatzzeichen fuer jedes Zeichen, das nicht in glyphs steht.",
            "Zeichen weiss und deckend; das Spiel faerbt beim Zeichnen ein und vergroessert nur ganzzahlig (Punktfilter).",
        ],
        "fallback": fallback,
        "glyphs": table,
    }
    os.makedirs(OUT_DIR, exist_ok=True)
    write_png(os.path.join(OUT_DIR, "kapitell.png"), ATLAS_W, atlas_h, pixels)
    with open(os.path.join(OUT_DIR, "kapitell.json"), "w", newline="\n", encoding="utf-8") as f:
        f.write(json.dumps(info, indent=1, ensure_ascii=False) + "\n")
    print("ZEICHEN", len(table), "+ Ersatzzeichen, Bild", ATLAS_W, "x", atlas_h, "Zeilenhoehe", line_height)


main()
