"""Engine der drei Pixelschriften (Issue #355). Reines Python, ohne numpy, ohne Schriftdatei.

Eine Schrift ist ein Raster aus Bitmustern (glyphen.py). Hier entstehen daraus drei Schriften:
  A "Kapitell" (kleine Serifen, Kleinbuchstaben als Kapitaelchen)
  B "Textura"  (gebrochen: Kopf- und Fussspitzen, dicke Senkrechte, duenne Waagerechte)
  C "Schmal"   (hoch und schmal, 1-Pixel-Striche)

Eine Schrift liefert fuer einen Text eine Liste von Pixeln (x, y) mit y relativ zur Grundlinie (0 = unterste Zeile
der Versalien, nach oben negativ) und die Breite. Gezeichnet wird in schrift_blatt.py.
"""
import glyphen as G

SPACE = 3  # Breite des Leerzeichens in Pixeln
GAP = 1    # Abstand zwischen Zeichen


def parse(spec):
    """'<top>:<zeilen>' oder '<zeilen>' -> (top, Menge von (x, y), Breite). Zeilen mit '/' getrennt."""
    top = 0
    if ":" in spec:
        t, spec = spec.split(":", 1)
        top = int(t)
    rows = spec.split("/")
    w = len(rows[0])
    for r in rows:
        assert len(r) == w, "Zeilen ungleich lang: %r" % spec
    px = set()
    for y, r in enumerate(rows):
        for x, c in enumerate(r):
            if c == "#":
                px.add((x, y + top))
    return px


def norm(px):
    """Auf x >= 0 verschieben; liefert (Pixelmenge, Breite)."""
    if not px:
        return set(), 1
    mx = min(x for x, _ in px)
    px = {(x - mx, y) for x, y in px}
    return px, max(x for x, _ in px) + 1


def shift(px, dy):
    return {(x, y + dy) for x, y in px}


class Font:
    def __init__(self, name, base, cap, glyphs, desc):
        self.name = name
        self.base = base      # Zeile der Grundlinie im Raster
        self.cap = cap        # Versalhoehe
        self.glyphs = glyphs  # Zeichen -> (Pixel, Breite)
        self.desc = desc

    def has(self, ch):
        return ch == " " or ch in self.glyphs

    def width(self, s, scale=1):
        w = 0
        for i, ch in enumerate(s):
            w += SPACE if ch == " " else self.glyphs[ch][1]
            if i < len(s) - 1:
                w += GAP
        return w * scale

    def layout(self, s):
        """-> Liste (x, y) relativ zu (linke Kante, Grundlinie)."""
        out = []
        x = 0
        for ch in s:
            if ch == " ":
                x += SPACE + GAP
                continue
            px, w = self.glyphs[ch]
            for gx, gy in px:
                out.append((x + gx, gy - self.base))
            x += w + GAP
        return out

    def ascii(self, s):
        pts = self.layout(s)
        if not pts:
            return ""
        y0 = min(y for _, y in pts)
        y1 = max(y for _, y in pts)
        w = self.width(s)
        rows = [["."] * w for _ in range(y1 - y0 + 1)]
        for x, y in pts:
            rows[y - y0][x] = "#"
        return "\n".join("".join(r) for r in rows)


# ------------------------------------------------------------------ Bausteine
def runs(px):
    """Senkrechte Laeufe der Laenge >= 2: Liste (x, y_oben, y_unten)."""
    out = []
    for x in sorted({x for x, _ in px}):
        ys = sorted(y for xx, y in px if xx == x)
        start = prev = ys[0]
        for y in ys[1:] + [None]:
            if y is not None and y == prev + 1:
                prev = y
                continue
            if prev - start >= 1:
                out.append((x, start, prev))
            if y is not None:
                start = prev = y
    return out


def serifs(px, top, bottom):
    """A: kleine Serifen an Ober- und Unterkante senkrechter Staemme (nur nach aussen)."""
    add = set()
    for r, d in ((top, 1), (bottom, -1)):
        cols = sorted(x for x, y in px if y == r)
        if not cols:
            continue
        for c in cols:
            if not all((c, r + d * k) in px for k in range(3)):
                continue
            if c == cols[0] and (c + 1, r) not in px:
                add.add((c - 1, r))
            if c == cols[-1] and (c - 1, r) not in px:
                add.add((c + 1, r))
    return px | add


def textura(px, top, bottom, thick=True):
    """B: Kopfspitze links oben, Fussspitze rechts unten an jedem Stamm; Staemme dick (rechts +1)."""
    rs = [r for r in runs(px) if r[2] - r[1] >= 2]
    out = set(px)
    for x, y0, y1 in rs:
        if not thick:
            break
        for y in range(y0, y1 + 1):
            if (x + 1, y) not in px and (x + 2, y) not in px:
                out.add((x + 1, y))
    for x, y0, y1 in rs:
        if (x - 1, y0) not in out and (x - 2, y0) not in out and (x - 1, y0 + 1) not in out:
            out.add((x - 1, y0))
        if (x + 1, y1) not in out:
            out.add((x + 1, y1))
        elif (x + 2, y1) not in out and (x + 2, y1 - 1) not in out:
            out.add((x + 2, y1))
    return out


def build(table, mapper):
    g = {}
    for ch, spec in table.items():
        g[ch] = norm(mapper(ch, parse(spec)))
    return g


# ------------------------------------------------------------------ A
def font_a():
    g = {}
    for ch, spec in G.S7.items():
        g[ch] = norm(serifs(parse(spec), 0, 6))
    for ch, spec in G.SC5.items():
        px = parse("2:" + spec)
        if ch == "i":
            px = serifs(px, 2, 6)
        g[ch] = norm(px)
    for ch, spec in G.D7.items():
        g[ch] = norm(parse(spec))
    for ch, spec in G.P7.items():
        g[ch] = norm(parse(spec))
    return Font("A Kapitell", 6, 7, g, "Kleine Serifen, Kleinbuchstaben als Kapitaelchen")


# ------------------------------------------------------------------ B
B_LOW_OVERRIDE = {
    "w": "2:#..#..#/#..#..#/#..#..#/#..#..#/.##.##.",
}
# Runde Kleinbuchstaben von Hand (doppelte Staemme, ohne Spitzen), sonst liest sich o wie a
B_LOW_HAND = {
    "o": "2:.###./##.##/##.##/##.##/.###.",
    "c": "2:.###./##.../##.../##.../.###.",
    "e": "2:.###./##.##/#####/##.../.###.",
    "s": "2:.####/##.../.###./...##/####.",
    "a": "2:.###./...##/.####/##.##/.####",
}


def font_b():
    g = {}
    for ch, spec in G.S7.items():
        # Versalien: nur die geraden Buchstaben bekommen dicke Staemme, die diagonalen bleiben lesbar duenn
        if ch in "BDEFHIKLPRTUJ":
            g[ch] = norm(textura(parse(spec), 0, 6))
        else:
            g[ch] = norm(parse(spec))
    low = dict(G.LB)
    low.update(B_LOW_OVERRIDE)
    for ch, spec in low.items():
        if ch in B_LOW_HAND:
            g[ch] = norm(parse(B_LOW_HAND[ch]))
            continue
        px = parse(spec)
        top = min(y for _, y in px)
        bot = max(y for _, y in px)
        g[ch] = norm(textura(px, top, bot))
    for ch, spec in G.D7.items():
        g[ch] = norm(parse(spec))  # Ziffern bleiben schlicht: Lesbarkeit der Zahlen geht vor
    for ch, spec in G.P7.items():
        g[ch] = norm(parse(spec))
    return Font("B Textura", 6, 7, g, "Gebrochen: Kopf- und Fussspitzen, dicke Senkrechte")


# ------------------------------------------------------------------ C
def font_c():
    g = {}
    for table in (G.C_CAPS, G.C_DIG):
        for ch, spec in table.items():
            g[ch] = norm(parse(spec))
    for ch, spec in G.C_LOW.items():
        g[ch] = norm(parse(spec))
    for ch, spec in G.P7.items():
        g[ch] = norm(shift(parse(spec), 1))
    for ch, spec in G.C_P.items():
        g[ch] = norm(parse(spec))
    return Font("C Schmal", 7, 8, g, "Schmal und hoch, 1-Pixel-Striche")


CHARSET_UP = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
CHARSET_LOW = "abcdefghijklmnopqrstuvwxyz"
CHARSET_DIG = "0123456789"
CHARSET_PUN = ".,:;!?'\"-+=/\\()[]<>%&*#_|@~^"


def check(font):
    for ch in CHARSET_UP + CHARSET_LOW + CHARSET_DIG + CHARSET_PUN:
        assert ch in font.glyphs, "%s fehlt: %r" % (font.name, ch)


def all_fonts():
    fs = [font_a(), font_b(), font_c()]
    for f in fs:
        check(f)
    return fs


if __name__ == "__main__":
    import sys
    text = "Hunter's Eye [F] to open 0123456789 AHMNWw gjpqy Mm"
    for f in all_fonts():
        print(f.name, f.width(text))
        print(f.ascii(text))
        print()
