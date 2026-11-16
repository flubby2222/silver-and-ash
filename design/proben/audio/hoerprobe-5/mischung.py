"""Hoerprobe 5 – Pruefung "klingt wie gemischt?" (Regel 1 der Audio Bible).

Urteil des Menschen zu Hoerprobe 4: "Klingt jetzt, als haette man mehrere Sounds
gemischt." Schichten, die man einzeln hoert, sollten sich im Messbild verraten.
Vier Kennzahlen, an Hoerprobe 3 (besser beurteilt) und 4 ("gemischt") geprueft:

  Einsaetze          Regel-2-Messung (50-ms-Fenster, Anstieg > 8 dB). Mehr als 1 = getrennte Ereignisse.
  Einsatzversatz     Das Signal wird in vier Baender geteilt (< 400, 400-1 500, 1 500-5 000, > 5 000 Hz).
                     Je Band: Zeitpunkt, an dem es 6 dB unter seiner Spitze erreicht. Groesster minus
                     kleinster Wert ueber alle Baender, die hoechstens 20 dB unter dem staerksten liegen.
  Ausklang-Umkehr    Je Band: letzter Zeitpunkt ueber 30 dB unter seiner Spitze. Natuerlich klingen hohe
                     Baender frueher aus als tiefe. Klingt ein hoeheres Band LAENGER als das naechsttiefere,
                     ist es vermutlich ein eigener Anteil (z. B. ein Metallklang neben einem dumpfen Schlag).
                     Kennzahl: groesster solcher Ueberhang in ms.
  Knick              Zeit von der Spitze bis -20 dB (a) und von -20 bis -40 dB (b), Gesamtsignal.
                     Ein einziges Abklingen hat b etwa gleich a. Ist b viel groesser (Verhaeltnis > 3),
                     folgt auf einen kurzen Anteil ein zweiter, langsamer (z. B. trockener Tropfen + Hall).

Grenzen fuer "wirkt gemischt" (Schluss aus den Werten von Hoerprobe 3 und 4, nicht belegt):
Einsaetze > 1, Einsatzversatz > 10 ms, Ausklang-Umkehr > 40 ms, Knick-Verhaeltnis > 3.
"""
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "hoerprobe-1"))
from synth import svf  # noqa: E402

SR = 44100
BANDS = ((None, 400.0), (400.0, 1500.0), (1500.0, 5000.0), (5000.0, None))
GRENZEN = {"einsaetze": 1, "versatz": 10.0, "umkehr": 40.0, "knick": 3.0}


def _band(x, lo, hi):
    y = x
    if lo:
        y = svf(svf(y, lo, 0.707, "hp"), lo, 0.707, "hp")
    if hi:
        y = svf(svf(y, hi, 0.707, "lp"), hi, 0.707, "lp")
    return y


def _env(y, win=0.005):
    w = int(win * SR)
    out = []
    for s in range(0, len(y) - w, w):
        r = math.sqrt(sum(v * v for v in y[s:s + w]) / w)
        out.append(-200.0 if r <= 0 else 20 * math.log10(r))
    return out


def _onsets(x):
    w, h = int(0.05 * SR), int(0.01 * SR)
    env = []
    for s in range(0, max(1, len(x) - w), h):
        r = math.sqrt(sum(v * v for v in x[s:s + w]) / w)
        env.append(-200.0 if r <= 0 else 20 * math.log10(r))
    peak = max(env)
    count, last = 0, -100
    for k, v in enumerate(env):
        if v < peak - 40:
            continue
        prev = env[k - 5] if k >= 5 else peak - 60
        if v - max(prev, peak - 60) > 8.0:
            if k - last > 6:
                count += 1
            last = k
    return count


def analyse(x, win=0.005):
    """Gibt ein dict mit einsaetze, versatz (ms), umkehr (ms), knick (a, b in ms), verdacht (Liste)."""
    data = []
    for lo, hi in BANDS:
        e = _env(_band(x, lo, hi), win)
        pk = max(e)
        k_on = next(k for k, v in enumerate(e) if v >= pk - 6)
        k_end = max(k for k, v in enumerate(e) if v >= pk - 30)
        data.append((pk, k_on * win * 1000, k_end * win * 1000))
    top = max(d[0] for d in data)
    sig = [d for d in data if d[0] >= top - 20]
    versatz = max(d[1] for d in sig) - min(d[1] for d in sig)
    umkehr = max([0.0] + [sig[i + 1][2] - sig[i][2] for i in range(len(sig) - 1)])
    e = _env(x, win)
    k0 = max(range(len(e)), key=lambda k: e[k])

    def t(th):
        return next((k for k in range(k0, len(e)) if all(v < e[k0] - th for v in e[k:])), len(e))
    a = (t(20) - k0) * win * 1000
    b = (t(40) - t(20)) * win * 1000
    res = {"einsaetze": _onsets(x), "versatz": versatz, "umkehr": umkehr, "knick": (a, b)}
    verdacht = []
    if res["einsaetze"] > GRENZEN["einsaetze"]:
        verdacht.append("Einsätze")
    if versatz > GRENZEN["versatz"]:
        verdacht.append("Versatz")
    if umkehr > GRENZEN["umkehr"]:
        verdacht.append("Umkehr")
    if a > 0 and b / a > GRENZEN["knick"]:
        verdacht.append("Knick")
    res["verdacht"] = verdacht
    return res


def zeile(name, x):
    r = analyse(x)
    a, b = r["knick"]
    ratio = "–" if a == 0 else f"{b / a:.1f}"
    v = ", ".join(r["verdacht"]) if r["verdacht"] else "–"
    return (f"| {name} | {r['einsaetze']} | {r['versatz']:.0f} ms | {r['umkehr']:.0f} ms | "
            f"{a:.0f} / {b:.0f} ms ({ratio}) | {v} |")


KOPF = ("| Datei | Einsätze | Einsatzversatz | Ausklang-Umkehr | Knick: 0→−20 / −20→−40 dB (Verhältnis) | wirkt gemischt? |\n"
        "|---|---|---|---|---|---|")


def first_event(x, length=0.7):
    """Schneidet aus einer Folge (mehrere Ereignisse) das erste Ereignis heraus."""
    w = int(0.005 * SR)
    peak = max(abs(v) for v in x) or 1.0
    start = next((s for s in range(0, len(x) - w, w)
                  if math.sqrt(sum(v * v for v in x[s:s + w]) / w) > peak * 0.01), 0)
    s0 = max(0, start - int(0.005 * SR))
    return x[s0:s0 + int(length * SR)]
