"""Bewegungsentwurf Fallgitter (Issue #540), Probestueck ausserhalb des Spiels.

Zwei Fassungen fuers Fallen (a: schwer und schnell mit Nachfedern, b: in zwei Rucken), eine fuers Heben.
Alles aus diesem einen Skript: Zeitverlaeufe (Formeln, Tabelle, CSV), Bilder aus dem Spiel-Licht, GIFs, Streifenbilder.
Es laedt die Szene des Grafik-Probestuecks (design/proben/grafik/fallgitter-540/fallgitter.py, nur gelesen, ohne dessen
main()) und rendert damit das Gitter in der Tueroeffnung. GIFs setzt ffmpeg (auf dem Rechner installiert) zusammen.

Aufruf (aus dem Wurzelverzeichnis; TEMP vorher auf einen Ordner ausserhalb des Repos setzen):
    "D:\\Program Files\\Blender Foundation\\Blender 5.3\\blender.exe" --background --python design/proben/animation/fallgitter-540/fallgitter_bewegung.py
Umgebungsvariablen: FGB_OUT (Ausgabeordner, Vorgabe: dieser Ordner), FGB_TABLE_ONLY=1 (nur Zeitverlaeufe, keine Bilder).
Kein Zufall: zwei Laeufe liefern gleiche Tabellen; die Bilder sind aus denselben Formeln und Szenen gleich.
"""

import csv
import math
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
GRAFIK = os.path.join(ROOT, "design", "proben", "grafik", "fallgitter-540")
OUT = os.environ.get("FGB_OUT", HERE)
TABLE_ONLY = os.environ.get("FGB_TABLE_ONLY") == "1"

TICK = 1.0 / 60.0

# ---------------------------------------------------------------------------------------------------------------------
# Zeitverlaeufe. Hoehe = Unterkante des Gitters (Spitzen) ueber dem Boden in Metern, Zeit in Ticks seit dem Ausloeser.
# ---------------------------------------------------------------------------------------------------------------------

OPEN = 3.40            # offen: Unterkante hinter dem Sturz (Scheitel des Bogens 3,32), nicht gezeichnet sichtbar
PASS = 1.85            # Schwelle fuer die Sperre: Spieler 1,80 m + 5 cm Luft unter den Spitzen
SPEED_DROP = 42.5      # m/s^2 des freien Falls im Bild (rund 4 g: das Gewicht des Eisens, kein Zeitlupen-Fall)


def bounce(t, t0, length, peak):
    """Ein Sprung nach dem Aufschlag: Parabel von t0 bis t0 + length mit Hoehe peak (0 ausserhalb)."""
    if t < t0 or t > t0 + length:
        return 0.0
    u = (t - t0) / length
    return peak * 4.0 * u * (1.0 - u)


def fall_a(t):
    """a: schwer und schnell. 24 Ticks freier Fall (Hoehe ~ 1 - (t/24)^2), Aufschlag bei 24, zwei Spruenge, Ruhe ab 40."""
    if t <= 0:
        return OPEN
    if t < 24:
        return OPEN * (1.0 - (t / 24.0) ** 2)
    return bounce(t, 24, 11, 0.06) + bounce(t, 35, 5, 0.009)


def fall_b(t):
    """b: zwei Rucke. 0..12 erster Fall um 0,85 m, Fangen der Kette (Nachschwingen bis 24), Halt bis 26,
    zweiter Fall 26..47 (21 Ticks), Aufschlag bei 47, zwei Spruenge, Ruhe ab 63."""
    if t <= 0:
        return OPEN
    if t < 12:
        return OPEN - 0.85 * (t / 12.0) ** 2
    hang = OPEN - 0.85
    if t < 26:
        s = t - 12.0
        return hang - 0.05 * math.exp(-s / 4.0) * math.sin(2.0 * math.pi * s / 8.0) if s < 12 else hang
    if t < 47:
        u = t - 26.0
        return hang * (1.0 - (u / 21.0) ** 2)
    return bounce(t, 47, 11, 0.05) + bounce(t, 58, 5, 0.009)


LIFT_WAIT = 6          # Ticks: die Kette strafft sich, das Gitter steht noch
LIFT_RAMP = 12         # Ticks Anlauf (Geschwindigkeit waechst gleichmaessig)
LIFT_END = 126         # Tick, bei dem die Unterkante OPEN erreicht
LIFT_V = OPEN / (LIFT_END - LIFT_WAIT - LIFT_RAMP / 2.0)      # m je Tick danach (gleichmaessig, 1,79 m/s)


def lift(t):
    """Heben: 6 Ticks Kette strafft sich (Stillstand), 12 Ticks Anlauf, dann gleichmaessig bis Tick 126."""
    s = t - LIFT_WAIT
    if s <= 0:
        return 0.0
    if s < LIFT_RAMP:
        return LIFT_V * s * s / (2.0 * LIFT_RAMP)
    return min(OPEN, LIFT_V * (s - LIFT_RAMP / 2.0))


MOTIONS = {
    "fallen-a": dict(fn=fall_a, last=40, title="a  schwer und schnell, Nachfedern"),
    "fallen-b": dict(fn=fall_b, last=63, title="b  zwei Rucke, Kette gibt nach"),
    "heben": dict(fn=lift, last=LIFT_END, title="Heben, gleichmaessig mit Anlauf"),
}


def first_tick(fn, last, test):
    for t in range(0, last + 1):
        if test(fn(t)):
            return t
    return None


def key_ticks():
    """Wann das Bild 'zu' liest und wann die Sperre (vorgeschlagen) beginnt bzw. endet."""
    res = {}
    for name in ("fallen-a", "fallen-b"):
        fn, last = MOTIONS[name]["fn"], MOTIONS[name]["last"]
        # Aufschlag: erster Tick mit Unterkante 0 nach dem Fall (letzter Tick mit fn == 0 vor dem ersten Sprung)
        impact = first_tick(fn, last, lambda h: h <= 1e-9)
        blocks = first_tick(fn, last, lambda h: h < PASS)
        rest = max(t for t in range(0, last + 1) if fn(t) > 1e-9) + 1
        res[name] = dict(sperre_ab=blocks, aufschlag=impact, ruhe=rest)
    fn = lift
    res["heben"] = dict(frei_ab=first_tick(fn, LIFT_END, lambda h: h >= PASS),
                        offen=first_tick(fn, LIFT_END, lambda h: h >= OPEN - 1e-9),
                        sichtbar_oben=first_tick(fn, LIFT_END, lambda h: h >= 3.32))
    return res


def write_tables():
    os.makedirs(OUT, exist_ok=True)
    keys = key_ticks()
    with open(os.path.join(OUT, "zeitverlauf.csv"), "w", newline="") as f:
        w = csv.writer(f, delimiter=";")
        w.writerow(["bewegung", "tick", "sekunden", "unterkante_m"])
        for name, m in MOTIONS.items():
            for t in range(0, m["last"] + 1):
                w.writerow([name, t, "%.3f" % (t * TICK), "%.3f" % m["fn"](t)])
    lines = []
    for name, m in MOTIONS.items():
        lines.append("%s  (%s), %d Ticks = %.2f s" % (name, m["title"], m["last"], m["last"] * TICK))
        lines.append("  Schluesselticks: %s" % keys.get(name))
        row = []
        step = 1 if m["last"] <= 63 else 6
        for t in range(0, m["last"] + 1, step):
            row.append("%3d:%.3f" % (t, m["fn"](t)))
        for i in range(0, len(row), 8):
            lines.append("  " + "  ".join(row[i:i + 8]))
        lines.append("")
    with open(os.path.join(OUT, "zeitverlauf.txt"), "w", newline="") as f:
        f.write("\n".join(lines))
    print("\n".join(lines))
    return keys


def checks():
    """Pruefung jeder Phase: nie unter dem Boden, nie ueber OPEN, Fall nie schneller als erlaubt, Heben nie rueckwaerts."""
    for name, m in MOTIONS.items():
        prev = None
        for t in range(0, m["last"] * 10 + 1):
            tt = t / 10.0
            h = m["fn"](tt)
            assert -1e-9 <= h <= OPEN + 1e-9, (name, tt, h)
            if name == "heben" and prev is not None:
                assert h >= prev - 1e-12, (name, tt, h, prev)
            prev = h
        # Schritt je Tick (Strobing: ab 0,35 m je Tick faellt das Bild in Spruengen, hier gemessen)
        big = max(abs(m["fn"](t + 1) - m["fn"](t)) for t in range(0, m["last"]))
        print("CHECK %s: groesster Schritt je Tick %.3f m, Ende %.3f m" % (name, big, m["fn"](m["last"])))
    assert abs(lift(LIFT_END) - OPEN) < 1e-9
    assert abs(fall_a(40)) < 1e-9 and abs(fall_b(63)) < 1e-9


# ---------------------------------------------------------------------------------------------------------------------
# Bilder
# ---------------------------------------------------------------------------------------------------------------------

def load_grafik():
    """Die Szene des Grafik-Probestuecks laden, ohne ihr main() auszufuehren; ihre Ausgabe geht nach FGB_OUT/ungenutzt."""
    src = open(os.path.join(GRAFIK, "fallgitter.py"), encoding="utf-8").read()
    assert src.rstrip().endswith("main()")
    src = src.rstrip()[:-len("main()")]
    sys.path.insert(0, os.path.join(ROOT, "tools", "models"))
    ns = {"__file__": os.path.join(GRAFIK, "fallgitter.py"), "__name__": "fallgitter_szene"}
    os.environ["FG_OUT"] = os.path.join(os.environ.get("TEMP", HERE), "fgb_ungenutzt")
    exec(compile(src, "fallgitter.py", "exec"), ns)
    return ns


FRAME_CACHE = {}


def frame(ns, h, caption=None):
    """Ein Spielbild (854 x 480, Spiel-Licht): Gitter mit Unterkante h in der Tueroeffnung, Kamera 3 m davor auf 1,6 m."""
    key = round(h, 4)
    if key not in FRAME_CACHE:
        items = ns["world"](lift=h)
        FRAME_CACHE[key] = ns["shot"](items, ns["cam_pos"]("frontal", 3.0), (0, ns["EYE"], -1), 70, ns["SPIEL"], "spiel")
    return FRAME_CACHE[key].copy()


def captioned(ns, im, lines):
    import numpy as np
    strip = np.tile(np.array((16, 11, 20), float), (46, im.shape[1], 1))
    for i, s in enumerate(lines):
        ns["text"](strip, s, 6, 2 + i * 22, 15)
    return np.concatenate([im, strip], axis=0)


def status(name, t, keys):
    if name == "heben":
        frei = keys["heben"]["frei_ab"]
        return "frei" if t >= frei else "gesperrt"
    return "gesperrt" if t >= keys[name]["sperre_ab"] else "frei"


def sample_times(last, hold_start=8, hold_end=14):
    """Zeitpunkte in Ticks im Abstand 1,2 (= 50 Bilder je Sekunde, GIF-Takt 2/100 s) mit Halt vorn und hinten."""
    ts = [-1.2 * i for i in range(hold_start, 0, -1)]
    t = 0.0
    while t < last:
        ts.append(t)
        t += 1.2
    ts.append(float(last))
    ts += [last + 1.2 * i for i in range(1, hold_end + 1)]
    return ts


def render_motion(ns, name, keys):
    import numpy as np
    m = MOTIONS[name]
    d = os.path.join(os.environ.get("TEMP", HERE), "fgb_frames", name)
    shutil.rmtree(d, ignore_errors=True)
    os.makedirs(d)
    ts = sample_times(m["last"])
    for i, t in enumerate(ts):
        tc = min(max(t, 0.0), float(m["last"]))
        h = m["fn"](tc)
        st = status(name, tc, keys)
        im = captioned(ns, frame(ns, h), [
            "%s" % m["title"],
            "Tick %5.1f   %.2f s   Unterkante %.2f m   Sperre (Vorschlag): %s" % (tc, tc * TICK, h, st)])
        ns["png"](im, os.path.join(d, "f%04d.png" % i))
    return d, ts


def gif(frames_dir, out_path, centis):
    """ffmpeg: Bilderfolge -> GIF mit eigener Palette, ohne Dithering (das Spielbild ist schon gekoernt)."""
    pal = os.path.join(frames_dir, "pal.png")
    src = os.path.join(frames_dir, "f%04d.png")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", "50", "-i", src,
                    "-vf", "palettegen=max_colors=96:stats_mode=full", pal], check=True)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", str(100.0 / centis), "-i", src, "-i", pal,
                    "-lavfi", "paletteuse=dither=none", "-loop", "0", out_path], check=True)


def strip_image(ns, name, ticks, keys):
    m = MOTIONS[name]
    tiles = []
    for t in ticks:
        h = m["fn"](t)
        im = ns["half"](frame(ns, h))
        im = ns["titled"](im, "Tick %d  %.2f m" % (t, h), 15)
        tiles.append(im)
    return ns["hcat"](tiles)


def main():
    keys = write_tables()
    checks()
    if TABLE_ONLY:
        return
    ns = load_grafik()
    for name in MOTIONS:
        d, ts = render_motion(ns, name, keys)
        gif(d, os.path.join(OUT, "%s-echtzeit.gif" % name), 2)
        gif(d, os.path.join(OUT, "%s-halbe-geschwindigkeit.gif" % name), 4)
    ka, kb = keys["fallen-a"], keys["fallen-b"]
    strips = {
        "fallen-a": [0, 12, ka["sperre_ab"], ka["aufschlag"], 29, ka["ruhe"]],
        "fallen-b": [0, 11, 25, kb["sperre_ab"], kb["aufschlag"], kb["ruhe"]],
        "heben": [0, 30, keys["heben"]["frei_ab"], 90, 112, LIFT_END],
    }
    for name, ticks in strips.items():
        im = strip_image(ns, name, ticks, keys)
        import numpy as np
        head = np.tile(np.array((16, 11, 20), float), (30, im.shape[1], 1))
        ns["text"](head, MOTIONS[name]["title"] + "   (Ticks " + ", ".join(str(t) for t in ticks) + ")", 6, 3, 16)
        ns["png"](np.concatenate([head, im], axis=0), os.path.join(OUT, "streifen-%s.png" % name))


main()
