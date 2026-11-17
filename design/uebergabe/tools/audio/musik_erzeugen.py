"""Uebergabe Musik – erzeugt musik-hauptmenue.ogg und musik-katakomben.ogg (e-Moll, Klangwelt "Glocken und Chor").

Aufruf:
  python musik_erzeugen.py --aus <ordner> [--nur menue|katakomben]

Palette (vorlaeufiger Massstab, wird ueberarbeitet): tiefe Glocke (unharmonische Teiltoene), Chor-Pad
(Stimmquelle durch Formantfilter, Vokal u -> o -> u), Herzschlag-Trommel (Sinus mit fallender Tonhoehe);
e-Moll (E Fis G A H C D), 60 Schlaege/min; EIN Hall (Schroeder, RT60 2,8 s, Daempfung 2,5 kHz, 50 % Send je Spur,
70 % Hallanteil; links und rechts mit verschiedener Zufallszahl, damit er breit wird); EINE PS1-Kette (22 050 Hz,
ADPCM, Gauss-Interpolation, kein Hall der Kette). Rein synthetisch, keine Aufnahmen.

Schleife: Alle Noten und der Hallschwanz laufen kreisfoermig (was ueber das Ende ragt, klingt am Anfang weiter);
die PS1-Kette bekommt vorne das Ende und hinten den Anfang angehaengt. Zwei Laeufe ergeben byte-gleiche Dateien.
"""
import argparse
import math
import os
import pickle
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import ambiente_hp3 as AMB  # noqa: E402
import musik_klang as K  # noqa: E402
from synth import SR, add_wrapped, hp1, lin, n_of, normalize, reverb, svf, write_wav, zeros  # noqa: E402

SCALE = {4, 6, 7, 9, 11, 0, 2}   # E Fis G A H C D
TAIL = 4.0                      # Sekunden Hallschwanz, der auf den Anfang gefaltet wird


# --- Notenlisten (Beginn in Schlaegen, Dauer, MIDI-Note, Anschlag) ----------------------------------

MENU_CHORDS = [(0, 8, [52, 55, 59, 64]), (8, 8, [52, 55, 60, 64]), (16, 4, [52, 57, 60, 64]), (20, 4, [52, 55, 59, 64]),     # Teil A (Skizze b)
               (24, 8, [50, 55, 59, 67]), (32, 8, [54, 57, 62, 66]), (40, 8, [52, 55, 60, 67]),                                # Teil B
               (48, 12, [52, 57, 60, 64]), (60, 12, [50, 54, 59, 66])]                                                          # Teil C, fuehrt zurueck nach Em
MENU_BELL = [(0, 8, 52, 1.0), (8, 8, 52, 0.9), (16, 8, 47, 1.0), (24, 8, 52, 1.0), (32, 8, 50, 0.9), (40, 8, 48, 0.9),
             (48, 8, 45, 1.0), (56, 8, 47, 0.9), (64, 8, 47, 1.0)]
MENU_HEART = ([(t + (0.38 if dub else 0.0), 0.5, 41 if dub else 40, 0.6 if dub else 1.0) for t in range(12, 22) for dub in (0, 1)]
              + [(t + (0.38 if dub else 0.0), 0.5, 41 if dub else 40, 0.6 if dub else 1.0) for t in range(28, 46) for dub in (0, 1)]
              + [(t + (0.38 if dub else 0.0), 0.5, 41 if dub else 40, (1.0 - 0.075 * (t - 48)) * (0.6 if dub else 1.0)) for t in range(48, 58) for dub in (0, 1)])

KATA_CHORDS = [(0, 16, [40, 47, 52, 55]), (16, 16, [40, 48, 52, 55]), (32, 16, [40, 48, 52, 57]), (48, 16, [42, 47, 50, 54])]
KATA_BELL = [(0, 8, 40, 0.6), (32, 8, 45, 0.5)]
KATA_HEART = [(t + (0.38 if dub else 0.0), 0.5, 41 if dub else 40, (0.3 if dub else 0.5)) for t in (22, 46, 58) for dub in (0, 1)]

PIECES = {
    "menue": dict(name="musik-hauptmenue", beats=72, chords=MENU_CHORDS, bell=MENU_BELL, heart=MENU_HEART,
                  mix=dict(bell=-9.0, choir=-13.0, heart=-7.0), formant=(1.0, 0.5, 0.2), low=0.6, target_mean=-21.0),
    "katakomben": dict(name="musik-katakomben", beats=64, chords=KATA_CHORDS, bell=KATA_BELL, heart=KATA_HEART,
                       mix=dict(bell=-12.0, choir=0.0, heart=-14.0), formant=(0.3, 0.15, 0.06), low=0.5, target_mean=-34.0),
}


# --- Klangerzeuger mit Schleifen-Umbruch ------------------------------------------------------------

def render_loop(notes, voice, n):
    buf = zeros(n)
    for k, (s, d, m, v) in enumerate(notes):
        add_wrapped(buf, voice(m, d, v, k), n_of(s), 1.0)
    return buf


def choir_post(x, formant, low_w, pad):
    """Vokalformung u -> o -> u (periodisch ueber die Schleife): drei Formantfilter, dazu der tiefe Anteil unter 160 Hz.
    x ist mit `pad` Sekunden Vorlauf aus dem Ende der Schleife versehen; der Vorlauf wird danach abgeschnitten."""
    n = len(x)
    p = n_of(pad)
    L = n - p
    ramp = [0.5 - 0.5 * math.cos(2.0 * math.pi * (i - p) / L) for i in range(n)]
    out = [0.0] * n
    for (a, b), q, g in (((310.0, 450.0), 5.0, formant[0]), ((870.0, 800.0), 6.0, formant[1]), ((2250.0, 2830.0), 8.0, formant[2])):
        y = svf(x, [a + (b - a) * r for r in ramp], q, "bp")
        for i, v in enumerate(y):
            out[i] += g * v
    low = svf(x, 160.0, 1.0, "lp")
    return [o + low_w * lo for o, lo in zip(out, low)][p:]


def fold(wet, n):
    """Hallschwanz (ueber n hinaus) auf den Anfang addieren."""
    w = list(wet[:n])
    for i, v in enumerate(wet[n:]):
        if i < n:
            w[i] += v
    return w


def pan(x, p):
    a = (p + 1.0) * 0.25 * math.pi
    return [v * math.cos(a) for v in x], [v * math.sin(a) for v in x]


def build_piece(spec, cache=None):
    n = n_of(spec["beats"])
    cached = None
    if cache and Path(cache).exists():
        cached = pickle.loads(Path(cache).read_bytes())
    if cached:
        bell, choir_raw, heart = cached
    else:
        bell = render_loop(spec["bell"], lambda m, d, v, k: K.bell(m, max(d, 7.0), v, seed=k), n)
        choir_notes = [(s, d + 0.3, m, 0.9) for s, d, ch in spec["chords"] for m in ch]
        choir_raw = render_loop(choir_notes, lambda m, d, v, k: K.choir_raw(m, d, v, attack=1.6, release=1.8, seed=k), n)
        heart = render_loop(spec["heart"], lambda m, d, v, k: K.drum(120.0 if m == 41 else 100.0, 61.7 if m == 41 else 41.2,
                                                                      0.6, v, seed=k), n)
        if cache:
            Path(cache).write_bytes(pickle.dumps((bell, choir_raw, heart)))
    pad = 4.0
    choir = choir_post(choir_raw[-n_of(pad):] + choir_raw, spec["formant"], spec["low"], pad)
    mix = spec["mix"]
    tracks = {"bell": (normalize(bell, 1.0), mix["bell"], -0.25), "choir": (normalize(choir, 1.0), mix["choir"], 0.0),
              "heart": (normalize(heart, 1.0), mix["heart"], 0.0)}
    left, right = zeros(n), zeros(n)
    delay = n_of(0.013)    # Haas-Versatz fuer den Chor: breiter, ohne zweiten Klangerzeuger
    for name, (x, db, p) in tracks.items():
        g = lin(db)
        l, r = pan([v * g for v in x], p)
        if name == "choir":
            r = [r[(i - delay) % n] for i in range(n)]
        for i in range(n):
            left[i] += l[i]
            right[i] += r[i]
    rev = dict(K.REVERB)
    rev.pop("seed")
    wet_l = fold(reverb([v * K.SEND for v in left], tail=TAIL, seed=6, **rev), n)
    wet_r = fold(reverb([v * K.SEND for v in right], tail=TAIL, seed=7, **rev), n)
    full = [[a + K.WET * b for a, b in zip(left, wet_l)], [a + K.WET * b for a, b in zip(right, wet_r)]]
    pad8 = n_of(8.0)    # Gleichanteil (die Stimmquelle ist einseitig) entfernen, kreisfoermig: Vorlauf aus dem Ende, danach abschneiden
    full = [hp1(c[-pad8:] + c, 20.0)[pad8:] for c in full]
    peak = max(max(abs(v) for v in c) for c in full)
    full = [[v * lin(K.PEAK_DB) / peak for v in c] for c in full]
    out = AMB.ps1_loop(full)                           # PS1-Kette, kreisfoermig
    mean_db = 10.0 * math.log10(sum(v * v for c in out for v in c) / (2 * n))
    g = min(1.0, lin(spec["target_mean"] - mean_db))
    return [[v * g for v in c] for c in out]


def write_ogg(chans, path):
    wav = Path(str(path) + ".tmp.wav")
    write_wav(wav, chans)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav), "-c:a", "libvorbis", "-q:a", "5",
                    "-map_metadata", "-1", "-fflags", "+bitexact", "-flags:a", "+bitexact", str(path)], check=True)
    wav.unlink()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--aus", required=True)
    ap.add_argument("--nur", choices=sorted(PIECES))
    a = ap.parse_args()
    out = Path(a.aus)
    out.mkdir(parents=True, exist_ok=True)
    cache = os.environ.get("SILVERASH_MUSIK_CACHE")     # nur zum Entwickeln: rohe Spuren zwischenspeichern
    for key, spec in PIECES.items():
        if a.nur and a.nur != key:
            continue
        c = Path(cache) / f"{key}.pkl" if cache else None
        write_ogg(build_piece(spec, c), out / f"{spec['name']}.ogg")
        print(spec["name"])


if __name__ == "__main__":
    main()
