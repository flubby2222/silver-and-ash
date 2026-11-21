"""Probestueck zu Issue #543 (Runde 2): kontrastreichere neutrale Eisen-Reihen fuer Umgebungsteile.

Ausgangslage: Mit der Reihe der ersten Probe (Helligkeit 30/50/74/104) verschwinden Nieten und Beschlaege auf dem Holz der
Tuer, die Eisentuer wirkt flach. Hier drei Reihen mit mehr Abstand (K1 = 34/58/88/124 als Ausgangspunkt, K2, K3), alle neutral
(R = G, B hoechstens 2 Prozent hoeher). Das Holz der Tuerfluegel bleibt Pixel fuer Pixel, wie es heute im Spiel liegt
(assets/models/door_*.png wird nur gelesen). Nichts unter assets/ wird geaendert, die Palette wird nur gelesen.

Zwei Schritte (Umgebungsvariable EK_STEP):
    blatt   (Vorgabe)  Reihen pruefen, Tuer-Atlanten umrechnen, Blatt und Einzelbilder, werte.txt
    gitter             das Fallgitter-Probestueck (Modelle und Bilder aus dem Skript von #540) in der EMPFEHLUNG neu erzeugen
Aufruf (aus dem Wurzelverzeichnis, TEMP/TMP vorher auf einen eigenen Ordner setzen):
    blender --background --python design/proben/grafik/eisen-kontrast-543/eisen_kontrast.py
EK_OUT: anderer Ausgabeordner (Zwei-Laeufe-Vergleich). EK_QUICK=1: nur ein paar Bilder, kein Blatt.
Gebaut auf palette_probe.py (#543, Runde 1) und fallgitter_quelle.py (= Skript aus #540); beide werden nur geladen.
Kein Zufall: zwei Laeufe liefern bitgleiche Dateien.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
OUT = os.environ.get("EK_OUT", HERE)
STEP = os.environ.get("EK_STEP", "blatt")
QUICK = os.environ.get("EK_QUICK") == "1"
PROBE1 = os.path.join(ROOT, "design", "proben", "grafik", "palette-holz-eisen-543")
os.makedirs(OUT, exist_ok=True)
os.environ["P543_OUT"] = os.path.join(OUT, "_ungenutzt")   # palette_probe legt seinen Ausgabeordner an, schreibt hier nichts

# --- Vorarbeit laden (ohne deren main()) --------------------------------------------------------------------------------
with open(os.path.join(PROBE1, "palette_probe.py"), encoding="utf-8") as f:
    _src = f.read().rstrip()
assert _src.endswith("\nmain()")
PP = {"__file__": os.path.join(PROBE1, "palette_probe.py"), "__name__": "palette_probe"}
exec(compile(_src[:-len("main()")], "palette_probe.py", "exec"), PP)
os.rmdir(os.environ["P543_OUT"])
NS = PP["NS"]
np = PP["np"]
text, titled, hcat, vcat, png, half = NS["text"], NS["titled"], NS["hcat"], NS["vcat"], NS["png"], NS["half"]
BG, EYE, SPIEL = NS["BG"], NS["EYE"], NS["SPIEL"]
KINDS, RECTS, load_atlas, door_passes, gitter_image = PP["KINDS"], PP["RECTS"], PP["load_atlas"], PP["door_passes"], PP["gitter_image"]
hexa, LUM = PP["hexa"], PP["LUM"]
EISEN_ALT = PP["EISEN_ALT"]
assert os.path.normpath(NS["ROOT"]) == os.path.normpath(ROOT)

# --- Die Reihen ---------------------------------------------------------------------------------------------------------


def neutral(lums):
    """Neutrales Grau: R = G, B 2 Prozent hoeher (hoechstens ein Hauch, Saettigung unter 0,03)."""
    return [(v, v, int(round(v * 1.02))) for v in lums]


REIHEN = {
    "P1": [(30, 30, 31), (50, 50, 52), (74, 74, 76), (104, 104, 107)],   # erste Probe, zum Vergleich
    "K1": neutral([34, 58, 88, 124]),
    "K2": neutral([32, 62, 104, 148]),
    "K3": neutral([30, 64, 116, 176]),
}
NAMEN = {"P1": "Probe 1", "K1": "K1", "K2": "K2", "K3": "K3"}
EMPFEHLUNG = "K2"

# Mindestwerte (Helligkeit 0 bis 255)
MIN_ABSTAND = 20            # zwischen benachbarten Stufen
MIN_NIET = 30               # Niet gegen Beschlag
MIN_HOLZ = 20               # Beschlag gegen das Holz der Tuer (damit der Beschlag auf dem Holz lesbar ist)
SAT_MAX = 0.04              # Farbstich hoechstens minimal


def sat(c):
    return (max(c) - min(c)) / max(max(c), 1)


def check_reihe(name, rows):
    """Vier Stufen, aufsteigend, Mindestabstand, neutral."""
    assert len(rows) == 4, name
    lums = [LUM(c) for c in rows]
    for a, b in zip(lums, lums[1:]):
        assert b - a >= MIN_ABSTAND, (name, lums)
    for c in rows:
        assert abs(c[0] - c[1]) == 0 and 0 <= c[2] - c[0] <= max(2, round(0.03 * c[0])) and sat(c) <= SAT_MAX, (name, c)
    return lums


# --- Tuer-Atlanten: Eisenfarben nach Rang auf die Reihe, Holz bleibt -----------------------------------------------------
# Stufe 0 bis 3 der Reihe je alter Eisenfarbe (Rang nach Helligkeit). Beschlag = Stufe 2, Niet = Stufe 3.
STUFE = {
    "normal": {(88, 91, 102): 0, (108, 111, 124): 1, (136, 141, 157): 2, (197, 203, 226): 3},
    "locked": {(25, 21, 27): 0, (37, 34, 39): 0, (44, 45, 52): 0, (48, 49, 58): 0, (52, 54, 64): 1, (59, 61, 71): 1,
               (73, 73, 81): 1, (89, 92, 104): 1, (125, 130, 146): 2, (185, 191, 213): 3},
    "boss": {(93, 88, 100): 1, (134, 129, 150): 2, (198, 191, 220): 3},
}
# Rechtecke, in denen Eisen umgerechnet wird; bei normal und boss sind Bretter und Kante Holz und bleiben
EISENFELDER = {"normal": ("plate", "iron"), "boss": ("plate", "iron"), "locked": ("planks", "plate", "iron", "edge")}
# Farben in diesen Feldern, die bleiben (Holz-Reste, Fugen, das Messing des Schlosses)
BLEIBT = {
    "normal": {(36, 30, 32), (65, 54, 45), (84, 70, 58), (93, 77, 64), (101, 84, 69), (110, 92, 76)},
    "locked": {(71, 57, 30)},
    "boss": {(41, 35, 43), (46, 39, 48), (71, 56, 31)},
}
NIET, BESCHLAG = 3, 2
HOLZFELDER = {"normal": "planks", "boss": "planks"}


def umrechnen(kind, atlas, rows):
    out = atlas.copy()
    rows = np.array(rows)
    for name in EISENFELDER[kind]:
        x0, y0, x1, y1 = RECTS[name]
        patch = atlas[y0:y1, x0:x1]
        res = patch.copy()
        seen = np.zeros(patch.shape[:2], bool)
        for col, st in STUFE[kind].items():
            m = (patch == np.array(col)).all(axis=2)
            res[m] = rows[st]
            seen |= m
        for col in BLEIBT[kind]:
            seen |= (patch == np.array(col)).all(axis=2)
        assert seen.all(), (kind, name, sorted({tuple(int(v) for v in p) for p in patch[~seen]}))
        out[y0:y1, x0:x1] = res
    return out


def holz_hell(kind, atlas):
    x0, y0, x1, y1 = RECTS[HOLZFELDER[kind]]
    return float(np.mean([LUM(p) for p in atlas[y0:y1, x0:x1].reshape(-1, 3)]))


def masse(rows, holz_normal):
    lums = [LUM(c) for c in rows]
    return {"niet": lums[NIET] - lums[BESCHLAG], "holz": lums[BESCHLAG] - holz_normal}


def ok(m):
    return m["niet"] >= MIN_NIET and abs(m["holz"]) >= MIN_HOLZ


# --- Bilder -------------------------------------------------------------------------------------------------------------
GROESSE = 300
KOPF = ("P1", "K1", "K2", "K3")
STATE = PP["STATE"]


def iron_rows_alle(which):
    if which == "eisen":
        return EISEN_ALT
    pad = np.zeros((2, 3), int)
    return np.concatenate([pad, np.array(REIHEN[which])])


NS["iron_rows"] = iron_rows_alle


def tuer(kind, var, name, mode, size=(GROESSE, GROESSE), view="frontal"):
    p = door_passes(kind, var[name], name + str(size[0]), view, size)
    return NS["to_image"](p, mode)


def nah(kind, var, name):
    """Beschlaege aus etwa 1,6 m, Spielbild (854 x 480, 70 Grad, Augenhoehe 1,6 m): was der Spieler vor der Tuer sieht."""
    STATE["leaf"], STATE["name"] = var[name], name + "nah"
    p = NS["render"](PP["door_items"](kind), (0.45, 1.6, 0.6), (0.3, 1.45, -1.0), 70, SPIEL)
    return NS["to_image"](p, "spiel")


def fill(im, x, y, w, h, colour):
    im[y:y + h, x:x + w] = np.array(colour, float)


def kopfzeile(w, name, rows):
    im = np.tile(BG, (56, w, 1))
    lums = [round(LUM(c)) for c in rows]
    text(im, "%s: Helligkeit %s" % (NAMEN[name], "/".join(str(v) for v in lums)), 6, 14, 24)
    for i, c in enumerate(rows):
        x0 = 700 + i * 270
        fill(im, x0, 8, 40, 40, c)
        text(im, hexa(c), x0 + 48, 4, 18)
        text(im, "H %d  S %.2f" % (lums[i], sat(c)), x0 + 48, 26, 14, (190, 185, 200))
    return im


def blatt(var):
    W = 1800
    head = np.tile(BG, (150, W, 1))
    text(head, "Silver & Ash, Issue #543: neutrales Eisen, kontrastreicher (Runde 2)", 10, 6, 34)
    text(head, "Holz der Türflügel bleibt wie heute. Je Reihe links die Normaltür (Holz mit Beschlägen), die Eisentür und das Fallgitter, jeweils hell und im Spiel-Licht.", 10, 56, 20)
    text(head, "Probe 1 ist die erste Probe (30/50/74/104), K1 bis K3 sind neu. Darunter eine Nahaufnahme der Beschläge (Spielbild) und die Messwerte.", 10, 86, 20)
    text(head, "Niet = Stufe 3, Beschlag = Stufe 2 (Normaltür und Eisentür). Die Fugen und Bretter der Eisentür liegen auf Stufe 0 und 1.", 10, 116, 20)
    parts = [head]
    for name in KOPF:
        rows = REIHEN[name]
        tiles = []
        for kind in ("normal", "locked"):
            for mode, lbl in (("hell", "hell"), ("spiel", "Spiel-Licht")):
                tiles.append(titled(tuer(kind, var, name, mode), "%s, %s, %s" % (kind, NAMEN[name], lbl), 16))
        for mode, lbl in (("hell", "hell"), ("spiel", "Spiel-Licht")):
            tiles.append(titled(gitter_image(name, "schraeg", 3.0, mode, (GROESSE, GROESSE)), "Gitter, %s, %s" % (NAMEN[name], lbl), 16))
        parts += [kopfzeile(W, name, rows), hcat(tiles, gap=0)]
    # Nahaufnahme: heute und die vier Reihen
    nt = np.tile(BG, (40, W, 1))
    text(nt, "Nahaufnahme der Normaltür im Spielbild (854 x 480, Ausschnitt 360 x 300 in Originalgröße): heute, Probe 1, K1, K2, K3", 10, 6, 22)
    tiles = []
    for name in ("heute",) + KOPF:
        im = nah("normal", var, name)
        crop = im[120:420, 150:510]                                         # 360 x 300 Pixel, Ausschnitt in Spielaufloesung
        tiles.append(titled(crop, "heute" if name == "heute" else NAMEN[name], 18))
    parts += [nt, hcat(tiles, gap=0)]
    return parts


def main_blatt():
    # Reihen und Messwerte
    lines = ["Reihen zu Issue #543 Runde 2 (neutrales Eisen, kontrastreicher), erzeugt von eisen_kontrast.py", ""]
    for name, rows in REIHEN.items():
        lums = check_reihe(name, rows)
        lines.append("%s: %s  Helligkeit %s  Abstaende %s" % (
            name, "  ".join("%s=%s" % (hexa(c), tuple(c)) for c in rows), [round(v) for v in lums],
            [round(b - a) for a, b in zip(lums, lums[1:])]))
        lines.append("   Saettigung %s" % [round(sat(c), 3) for c in rows])

    base = {k: load_atlas(k) for k in KINDS}
    for k in KINDS:
        PP["check_layout"](k, base[k])
    var = {"heute": base}
    for name, rows in REIHEN.items():
        var[name] = {k: umrechnen(k, base[k], rows) for k in KINDS}
    # Holz und alles ausserhalb der Eisenfelder bleibt Pixel fuer Pixel
    for name in REIHEN:
        for k in KINDS:
            keep = np.ones(base[k].shape[:2], bool)
            for f in EISENFELDER[k]:
                x0, y0, x1, y1 = RECTS[f]
                keep[y0:y1, x0:x1] = False
            assert (var[name][k][keep] == base[k][keep]).all(), (name, k)
            # nur Farben der Reihe, des heutigen Holzes und der bleibenden Farben in den Eisenfeldern
            for f in EISENFELDER[k]:
                x0, y0, x1, y1 = RECTS[f]
                got = {tuple(int(v) for v in p) for p in var[name][k][y0:y1, x0:x1].reshape(-1, 3)}
                assert got <= {tuple(c) for c in REIHEN[name]} | BLEIBT[k], (name, k, f)

    # Messwerte: Niet gegen Beschlag, Beschlag gegen Holz
    hn = holz_hell("normal", base["normal"])
    lines += ["", "Holz der Normaltuer (Bretter, unveraendert): mittlere Helligkeit %.1f; Boss-Planken %.1f" % (hn, holz_hell("boss", base["boss"])),
              "Messung je Reihe (Helligkeit 0 bis 255): Niet - Beschlag (Soll >= %d), Beschlag - Holz der Normaltuer (Betrag, Soll >= %d)" % (MIN_NIET, MIN_HOLZ)]
    for name, rows in REIHEN.items():
        m = masse(rows, hn)
        lines.append("  %-3s Niet-Beschlag %+5.1f   Beschlag-Holz %+5.1f   %s" % (name, m["niet"], m["holz"], "ERFUELLT" if ok(m) else "zu schwach"))
    lines.append("Im Spielbild schrumpfen Unterschiede mit der Laterne: bei 2 m mal %.2f, bei 4 m mal %.2f, bei 8 m mal %.2f (nur Laternenanteil, ohne Nebel)" % (
        1 - 0.65 * 2 / 18.2, 1 - 0.65 * 4 / 18.2, 1 - 0.65 * 8 / 18.2))

    # Gegenprobe der Pruefung: Probe 1 muss durchfallen, die Empfehlung muss bestehen
    assert not ok(masse(REIHEN["P1"], hn)), "Pruefung greift nicht: Probe 1 muesste durchfallen"
    assert ok(masse(REIHEN[EMPFEHLUNG], hn)), EMPFEHLUNG
    for name in ("K2", "K3"):
        assert ok(masse(REIHEN[name], hn)), name
    assert not ok(masse(REIHEN["K1"], hn))   # K1 (Ausgangspunkt) reicht fuer die Beschlaege auf dem Holz nicht
    # Niet gegen Beschlag je Tuer (Atlas): hellster Beschlagton gegen Niet im umgerechneten Feld
    for name in ("K1", "K2", "K3"):
        for k in ("normal", "locked", "boss"):
            iron = var[name][k]
            x0, y0, x1, y1 = RECTS["iron"]
            cols = {tuple(int(v) for v in p) for p in iron[y0:y1, x0:x1].reshape(-1, 3)}
            ls = sorted(LUM(c) for c in cols if c in {tuple(c2) for c2 in REIHEN[name]})
            assert ls[-1] - ls[-2] >= MIN_NIET, (name, k, ls)

    with open(os.path.join(OUT, "werte.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join(lines))
    import json
    with open(os.path.join(OUT, "vorschlag.json"), "w", encoding="utf-8") as f:
        json.dump({"empfehlung": EMPFEHLUNG,
                   "eisen": {n: [{"hex": hexa(c), "rgb": list(c), "helligkeit": round(LUM(c))} for c in r] for n, r in REIHEN.items()},
                   "holz": "unveraendert (heutiges Holz der Tuerfluegel)"}, f, indent=1)
        f.write("\n")

    if QUICK:
        png(titled(tuer("normal", var, "K2", "spiel"), "K2 normal"), os.path.join(OUT, "schnell.png"))
        return

    # Blatt
    parts = blatt(var)
    W = 1800
    # Messwerte als Text unten
    tab = np.tile(BG, (40 + 30 * (len(REIHEN) + 2), W, 1))
    text(tab, "Messwerte (Helligkeit 0 bis 255): Niet gegen Beschlag, Beschlag gegen das heutige Holz der Normaltür (mittlere Helligkeit %.0f)" % hn, 10, 6, 22)
    text(tab, "Soll: Niet gegen Beschlag mindestens %d, Beschlag gegen Holz mindestens %d, benachbarte Stufen mindestens %d auseinander." % (MIN_NIET, MIN_HOLZ, MIN_ABSTAND), 10, 36, 20, (190, 185, 200))
    for i, (name, rows) in enumerate(REIHEN.items()):
        m = masse(rows, hn)
        lums = [LUM(c) for c in rows]
        text(tab, "%-8s Niet - Beschlag %+4.0f     Beschlag - Holz %+4.0f     Abstände %s     %s" % (
            NAMEN[name], m["niet"], m["holz"], [round(b - a) for a, b in zip(lums, lums[1:])], "erfüllt" if ok(m) else "zu schwach"), 10, 70 + i * 30, 22)
    text(tab, "Empfehlung: %s. Das Holz bleibt unverändert; nur das Eisen von Beschlägen, Nieten, Eisentür und Fallgitter ist neu." % EMPFEHLUNG, 10, 70 + len(REIHEN) * 30, 22)
    parts.append(tab)
    parts = [p if p.shape[1] == W else np.concatenate([p, np.tile(BG, (p.shape[0], W - p.shape[1], 1))], 1) for p in parts]
    sheet = np.concatenate(parts, axis=0)
    assert sheet.shape[0] <= 2400 and sheet.shape[1] <= 1800, sheet.shape
    png(sheet, os.path.join(OUT, "blatt-eisen-543.png"))

    # Einzelbilder
    for name in KOPF:
        for kind in ("normal", "locked"):
            png(hcat([titled(tuer(kind, var, name, "hell", (480, 480)), "Tür %s, %s, hell" % (kind, NAMEN[name]), 20),
                      titled(tuer(kind, var, name, "spiel", (480, 480)), "Tür %s, %s, Spiel-Licht" % (kind, NAMEN[name]), 20)]),
                os.path.join(OUT, "tuer-%s-%s.png" % (kind, name.lower())))
        png(vcat([titled(gitter_image(name, "schraeg", 3.0, "hell"), "Fallgitter, %s, schräg 3 m, hell" % NAMEN[name], 20),
                  titled(gitter_image(name, "schraeg", 3.0, "spiel"), "Fallgitter, %s, schräg 3 m, Spiel-Licht" % NAMEN[name], 20)]),
            os.path.join(OUT, "gitter-%s-schraeg-3m.png" % name.lower()))
    for name in ("heute",) + KOPF:
        png(titled(nah("normal", var, name), "Normaltür aus 1,6 m, Spielbild, " + ("heute" if name == "heute" else NAMEN[name]), 20),
            os.path.join(OUT, "beschlaege-nah-%s.png" % name.lower()))


def main_gitter():
    """Fallgitter-Probestueck (Skript aus #540) in der Empfehlung neu erzeugen: Modelle, Zustaende, Vergleiche."""
    rows = np.array(REIHEN[EMPFEHLUNG])
    orig_row = NS["row"]

    def row_neu(name):
        return rows if name == "Eisen" else orig_row(name)

    def iron_rows_g(which):
        if which == "blau":
            return EISEN_ALT
        return np.concatenate([np.zeros((2, 3), int), rows])   # "eisen" und alles andere: die Empfehlung

    NS["row"] = row_neu
    NS["iron_rows"] = iron_rows_g
    NS["OUT"] = os.path.join(OUT, "fallgitter")
    os.makedirs(NS["OUT"], exist_ok=True)

    def view_iron_neu():
        out = []
        for mode in ("spiel", "hell"):
            pair = []
            for which, lbl in (("blau", "Eisen der Hauptpalette heute (bläulich)"), ("eisen", "neutrales Eisen %s" % EMPFEHLUNG)):
                pair.append(titled(NS["shot"](NS["world"](iron=which), NS["cam_pos"]("schraeg", 3.0), (0, EYE, -1), 70, SPIEL, mode),
                                   lbl + (", Spielbild" if mode == "spiel" else ", hell")))
            out.append(hcat(pair))
        return vcat(out)

    NS["view_iron"] = view_iron_neu
    # main() des Fallgitter-Skripts: Modelle, Zustaende, Uebersichten, Einzelbilder, Messung
    with open(os.path.join(PROBE1, "fallgitter_quelle.py"), encoding="utf-8") as f:
        src = f.read().rstrip()
    NS["MODELS_ONLY"] = os.environ.get("EK_MODELS_ONLY") == "1"
    exec(compile("main()", "fallgitter_main", "exec"), NS)
    assert src.endswith("main()")


if STEP == "gitter":
    main_gitter()
else:
    main_blatt()
