"""Prueft die Raumvorlagen der Probe Raumdesign Runde 1 (Issue #215). Reines Python, nichts installiert.

Aufruf:   python pruefen.py            (prueft alle r*.txt neben dem Skript, Rueckgabe 1 bei einem Fehler)
          python pruefen.py datei.txt  (nur diese Datei)

Geprueft wird, was RoomTemplateParser und RoomTemplateTests heute verlangen, und dazu die Regeln aus dem Auftrag:
  - Zeichen: heutiges Format; *.neu.txt darf zusaetzlich die vorgeschlagenen Zeichen (siehe notizen.md) nutzen
  - Rand ist Wand oder Tuerplatz, Tuer nicht in der Ecke, dahinter Boden (auf 0 m), daneben Boden (Raeume)
  - Boden haengt zusammen (wie der Parser: vier Nachbarn, Hoehe egal)
  - Raumart: Kampf 10-18 Zellen je Richtung, genau ein C, mind. 30 Felder >= 2 Zellen (4 m) von jeder Tuer;
    Schatz/Geheim genau ein '+', mind. ein C, keine Gegner
  - Erreichbarkeit mit Hoehen (Flutfuellung, gerichtet): hinauf gehen bis 0,4 m (BodySizes.StepHeight),
    hinauf springen bis 1,0 m (Doku "ca. 1 m"; balance.json erlaubt 1,3 m), hinab immer.
    Fehler: Feld nicht erreichbar oder kein Rueckweg zur Tuer, auch mit Sprung.
    Hinweis: Feld nur mit Sprung erreichbar oder nur mit Sprung verlassbar (Ghule springen vermutlich nicht).
  - Breite: jedes Bodenfeld liegt in einem 2 x 2-Block begehbarer Felder, deren Hoehen um hoechstens 0,4 m
    abweichen. Die Engstelle (R5) ist ausgenommen; dort meldet das Skript den Steg nur als Hinweis.
Runde Saeule 'O' (Vorschlag) zaehlt hier wie Wand: so sind die Wege um sie herum sicher breit genug.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))

# Raumart je Vorlage und ob die Breitenregel gilt
ROOMS = {
    "r1-l-form": ("combat", True),
    "r2-t-form": ("combat", True),
    "r3-nischen": ("combat", True),
    "r4-saeulenhalle": ("combat", True),
    "r5-engstelle": ("combat", False),
    "r6-empore": ("treasure", True),
}

STEP = 0.4
JUMP = 1.0
DOOR_DISTANCE_CELLS = 2.0  # 4 m / 2 m je Zelle

WALL = set("# +")
TODAY = set("# .+P123_GBCT")
NEW = TODAY | set("O45678abcdefgh")


def height_of(ch):
    """Bodenhoehe in m, None fuer Wand und runde Saeule."""
    if ch in WALL or ch == "O":
        return None
    if ch in ".PGBTC":
        return 0.0
    if ch in "12345678":
        return 0.25 * int(ch)
    if ch == "_":
        return -1.0
    if ch in "abcdefgh":
        return -0.25 * ("abcdefgh".index(ch) + 1)
    raise ValueError(ch)


def load(path):
    with open(path, encoding="utf-8") as f:
        lines = [ln.rstrip("\n").rstrip("\r") for ln in f]
    while lines and not lines[-1].strip():
        lines.pop()
    return lines


def check(path):
    name = os.path.basename(path)
    stem = name.split(".")[0]
    kind, width_rule = ROOMS.get(stem, ("combat", True))
    new = name.endswith(".neu.txt")
    allowed = NEW if new else TODAY
    errs, notes = [], []
    rows = load(path)
    w, d = max(len(r) for r in rows), len(rows)
    if any(len(r) != w for r in rows):
        notes.append("Zeilen ungleich lang (Rest gilt als Wand)")
    g = [[(r[x] if x < len(r) else "#") for x in range(w)] for r in rows]

    for z in range(d):
        for x in range(w):
            if g[z][x] not in allowed:
                errs.append(f"unbekanntes Zeichen '{g[z][x]}' in Zeile {z + 1}, Spalte {x + 1}")
    if errs:
        return kind, errs, notes, {}

    h = [[height_of(g[z][x]) for x in range(w)] for z in range(d)]
    # Vorschlag: im neuen Format nimmt ein Kistenplatz die Hoehe seiner Nachbarn an, wenn sie alle gleich sind
    if new:
        for z in range(d):
            for x in range(w):
                if g[z][x] == "C":
                    nb = {h[z + dz][x + dx] for dx, dz in ((1, 0), (-1, 0), (0, 1), (0, -1))
                          if 0 <= x + dx < w and 0 <= z + dz < d and h[z + dz][x + dx] is not None}
                    if len(nb) == 1:
                        h[z][x] = nb.pop()

    def walk(x, z):
        return 0 <= x < w and 0 <= z < d and h[z][x] is not None

    # Rand
    for z in range(d):
        for x in range(w):
            if (x in (0, w - 1) or z in (0, d - 1)) and g[z][x] not in WALL:
                errs.append(f"Boden am Rand in Zeile {z + 1}, Spalte {x + 1}")
    # Tueren
    doors = [(x, z) for z in range(d) for x in range(w) if g[z][x] == "+"]
    starts = []
    for x, z in doors:
        ew, ns = x in (0, w - 1), z in (0, d - 1)
        if ew and ns:
            errs.append(f"Tuer in der Ecke (Zeile {z + 1}, Spalte {x + 1})")
            continue
        if not ew and not ns:
            errs.append(f"Tuer nicht am Rand (Zeile {z + 1}, Spalte {x + 1})")
            continue
        bx, bz = (x, 1) if z == 0 else (x, d - 2) if z == d - 1 else (1, z) if x == 0 else (w - 2, z)
        sx, sz = (1, 0) if ns else (0, 1)
        if not walk(bx, bz):
            errs.append(f"hinter der Tuer Zeile {z + 1}, Spalte {x + 1} ist kein Boden")
            continue
        if not (walk(bx - sx, bz - sz) and walk(bx + sx, bz + sz)):
            errs.append(f"neben dem Feld hinter der Tuer Zeile {z + 1}, Spalte {x + 1} fehlt Boden")
        if h[bz][bx] != 0.0:
            errs.append(f"das Feld hinter der Tuer Zeile {z + 1}, Spalte {x + 1} liegt nicht auf 0 m")
        starts.append((bx, bz))
    if not doors:
        errs.append("kein Tuerplatz '+'")

    floor = [(x, z) for z in range(d) for x in range(w) if walk(x, z)]
    n_c = sum(row.count("C") for row in g)
    n_enemy = sum(row.count("G") + row.count("B") for row in g)

    # Zusammenhang wie der Parser
    if floor:
        seen = {floor[0]}
        todo = [floor[0]]
        while todo:
            x, z = todo.pop()
            for nx, nz in ((x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1)):
                if walk(nx, nz) and (nx, nz) not in seen:
                    seen.add((nx, nz))
                    todo.append((nx, nz))
        if len(seen) != len(floor):
            errs.append(f"Boden haengt nicht zusammen ({len(floor) - len(seen)} Felder abgetrennt)")

    # Raumart
    free = sum(1 for x, z in floor if all(((x - a) ** 2 + (z - b) ** 2) ** 0.5 >= DOOR_DISTANCE_CELLS for a, b in doors))
    if kind == "combat":
        if not (10 <= w <= 18 and 10 <= d <= 18):
            errs.append(f"Kampfraum {w} x {d}, erlaubt 10-18 je Richtung")
        if n_c != 1:
            errs.append(f"Kampfraum braucht genau ein C, hat {n_c}")
        if free < 30:
            errs.append(f"nur {free} freie Felder abseits der Tueren (mind. 30)")
    else:
        if len(doors) != 1:
            errs.append(f"{kind} braucht genau einen Tuerplatz, hat {len(doors)}")
        if n_c < 1:
            errs.append(f"{kind} braucht mindestens ein C")
        if n_enemy:
            errs.append(f"{kind} hat Gegnerplaetze")

    # Erreichbarkeit mit Hoehen
    def flood(src, up, reverse=False):
        seen = set(src)
        todo = list(src)
        while todo:
            x, z = todo.pop()
            for nx, nz in ((x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1)):
                if not walk(nx, nz) or (nx, nz) in seen:
                    continue
                rise = (h[z][x] - h[nz][nx]) if reverse else (h[nz][nx] - h[z][x])
                if rise <= up + 1e-6:
                    seen.add((nx, nz))
                    todo.append((nx, nz))
        return seen

    if starts:
        src = starts[:1]
        there_j, back_j = flood(src, JUMP), flood(src, JUMP, reverse=True)
        there_w, back_w = flood(src, STEP), flood(src, STEP, reverse=True)
        for name_, a, b in (("nicht erreichbar", there_j, None), ("kein Rueckweg zur Tuer", back_j, None)):
            miss = [c for c in floor if c not in a]
            if miss:
                errs.append(f"{len(miss)} Felder {name_}, auch mit Sprung, z. B. Zeile {miss[0][1] + 1}, Spalte {miss[0][0] + 1}")
        only_j = [c for c in floor if c in there_j and c not in there_w]
        trap = [c for c in floor if c in back_j and c not in back_w]
        if only_j:
            notes.append(f"{len(only_j)} Felder nur mit Sprung erreichbar (Ghule kommen nicht hin)")
        if trap:
            notes.append(f"{len(trap)} Felder nur mit Sprung verlassbar (Ghul-Falle?)")
        # alle Tueren untereinander zu Fuss verbunden?
        for t in starts[1:]:
            if t not in there_w or t not in back_w:
                notes.append(f"Tuer bei Zeile {t[1] + 1}, Spalte {t[0] + 1} nur mit Sprung mit der ersten verbunden")

    # Breite
    narrow = []
    for x, z in floor:
        ok = False
        for ox in (0, -1):
            for oz in (0, -1):
                blk = [(x + ox + i, z + oz + j) for i in (0, 1) for j in (0, 1)]
                if all(walk(*c) for c in blk):
                    hs = [h[c[1]][c[0]] for c in blk]
                    if max(hs) - min(hs) <= STEP + 1e-6:
                        ok = True
        if not ok:
            narrow.append((x, z))
    if narrow:
        msg = f"{len(narrow)} Felder schmaler als 2 Zellen, z. B. Zeile {narrow[0][1] + 1}, Spalte {narrow[0][0] + 1}"
        (errs if width_rule else notes).append(msg + ("" if width_rule else " (Engstelle, erlaubt)"))

    # Brunnenplatz (nur Hinweis): Feld mit acht Nachbarn auf gleicher Hoehe
    wells = sum(1 for x, z in floor if all(walk(x + i, z + j) and h[z + j][x + i] == h[z][x]
                                          for i in (-1, 0, 1) for j in (-1, 0, 1)))
    stats = {"size": (w, d), "floor": len(floor), "free": free, "doors": len(doors), "wells": wells,
             "heights": sorted({h[z][x] for x, z in floor})}
    return kind, errs, notes, stats


def main(argv):
    files = argv[1:] or sorted(os.path.join(HERE, f) for f in os.listdir(HERE)
                               if f.startswith("r") and f.endswith(".txt"))
    bad = 0
    for path in files:
        kind, errs, notes, st = check(path)
        status = "FEHLER" if errs else "ok"
        bad += bool(errs)
        info = ""
        if st:
            hs = ", ".join(f"{v:+.2f}" for v in st["heights"])
            info = (f"  {st['size'][0]} x {st['size'][1]}, {st['floor']} Boden, {st['free']} frei, "
                    f"{st['doors']} Tueren, {st['wells']} Brunnenplaetze, Hoehen {hs}")
        print(f"{os.path.basename(path):26s} {kind:9s} {status}{info}")
        for e in errs:
            print(f"    FEHLER  {e}")
        for n in notes:
            print(f"    Hinweis {n}")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
