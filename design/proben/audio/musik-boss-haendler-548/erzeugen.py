"""Hoerprobe Musik Bosskampf und Haendler (Issue #548) - vier Skizzen, als Schleife gedacht.

Aufruf:
  python erzeugen.py --aus <ordner> [--nur boss-a-schwerer-marsch ...]

Klangwelt und Klangerzeuger sind die von musik-hauptmenue.ogg und musik-katakomben.ogg (tools/audio/musik_erzeugen.py,
musik_klang.py): tiefe Glocke, Chor-Pad (Stimmquelle durch Formantfilter, Vokal u -> o -> u), derselbe Hall, dieselbe
PS1-Kette, e-Moll. Neu sind nur drei kleine Klangerzeuger fuer das Schlagwerk (Trommel, Amboss, Basspuls), alle
aus Sinus und Rauschen. Kein Herzschlag (zweiteiliger Schlag), keine Toene ueber etwa 700 Hz in den Stimmen.

Boss a  "schwerer Marsch":  80 Schlaege/min, 14 Takte = 42 s. Tiefe Trommel auf 1 und 3, Amboss (Metall) auf 4,
         tiefe Glocke alle zwei Takte, Chor-Akkorde darunter. Die Mitte (300-3 000 Hz) bleibt fast leer.
Boss b  "Basspuls":         96 Schlaege/min, 16 Takte = 40 s. Gleichmaessiger Achtelpuls auf dem Grundton (tiefer Sinus
         mit Obertoenen bis etwa 330 Hz), Chor-Akkorde, nur wenige Glockenschlaege, keine Trommel.
Haendler a "Glocken-Tropfen": 40 s, ruhiger Chor, einzelne Glockentoene (E G A H D) weit auseinander.
Haendler b "Chor-Atem":      40 s, nur Chor-Stimmen, die versetzt ein- und ausatmen; ein tiefer Glockenschlag am Anfang und in der Mitte.

Schleife: alle Noten und der Hallschwanz laufen kreisfoermig (was ueber das Ende ragt, klingt am Anfang weiter), die
PS1-Kette bekommt vorne das Ende und hinten den Anfang angehaengt. Zwei Laeufe ergeben byte-gleiche Dateien.
Ausgabe: 16 Bit, 44,1 kHz, stereo, WAV. Alles rein synthetisch, nur Standardbibliothek.
"""
import argparse
import math
import os
import pickle
import random
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
TOOLS = HERE.parents[3] / "tools" / "audio"
sys.path.insert(0, str(TOOLS))

import ambiente_hp3 as AMB  # noqa: E402
import musik_klang as K  # noqa: E402
from musik_erzeugen import TAIL, choir_post, fold, pan, render_loop  # noqa: E402
from synth import SR, hp1, lin, n_of, normalize, reverb, svf, white, write_wav, zeros  # noqa: E402

PEAK_LIMIT_DB = -1.5      # Spitze der Datei darf nicht hoeher liegen (Rahmen: hoechstens -1 dBFS)
TARGET_MEAN_DB = -24.0    # gleiche mittlere Lautstaerke wie musik-katakomben.ogg (Massstab aus Issue #427)


# --- neue Klangerzeuger fuer das Schlagwerk ----------------------------------------------------------

def drum(m, d, v, k):
    """Tiefe Trommel, einteiliger Schlag: Sinus faellt von 90 auf 38 Hz, dazu ein kurzer Tiefpass-Rauschschlag."""
    return K.drum(90.0, 38.0, 0.8, v, seed=k)


AMBOSS = ((1.0, 1.0, 0.55), (1.47, 0.7, 0.40), (2.09, 0.5, 0.30), (2.78, 0.35, 0.20), (3.6, 0.2, 0.14))


def anvil(m, d, v, k):
    """Amboss (Metall auf Stein): unharmonische Teiltoene ab 164 Hz (hoechster etwa 590 Hz), kurze Abklingzeiten,
    ein Rauschschlag unter 1,2 kHz."""
    rng = random.Random(k * 53 + m)
    f = K.hz(m)
    n = n_of(1.4)
    out = [0.0] * n
    for ratio, amp, tau in AMBOSS:
        w = 2.0 * math.pi * f * ratio / SR
        p0 = rng.uniform(0, 6.28)
        dec = 1.0 / (tau * SR)
        for i in range(n):
            out[i] += amp * math.sin(w * i + p0) * math.exp(-i * dec)
    cn = n_of(0.03)
    tick = svf(white(cn, rng), 1200.0, 0.8, "lp")
    for i in range(cn):
        out[i] += 0.6 * tick[i] * math.exp(-i / (0.007 * SR))
    for i in range(int(0.002 * SR)):
        out[i] *= i / (0.002 * SR)
    return [x * 0.2 * v for x in out]


def pulse(m, d, v, k):
    """Basspuls: Sinus mit zweitem und drittem Teilton (bis etwa 330 Hz), schnelle Abklingzeit, leicht gesaettigt."""
    f = K.hz(m)
    n = n_of(0.5)
    out = [0.0] * n
    ph = 0.0
    for i in range(n):
        t = i / SR
        ph += 2.0 * math.pi * f / SR
        s = math.sin(ph) + 0.5 * math.sin(2.0 * ph) + 0.22 * math.sin(3.0 * ph)
        a = min(1.0, t / 0.004) * math.exp(-t / 0.11)
        out[i] = math.tanh(1.4 * s * a)
    return [x * v for x in out]


def bell_voice(m, d, v, k):
    return K.bell(m, max(d, 7.0), v, seed=k)


# --- Notenlisten (Beginn in Sekunden, Dauer in Sekunden, MIDI-Note, Anschlag) ------------------------

def chords_to_notes(chords):
    return [(s, d + 0.3, m, 0.9) for s, d, ch in chords for m in ch]


def boss_a():
    bpm, bars = 80, 14
    beat, bar = 60.0 / bpm, 240.0 / bpm
    em, c, dd = [40, 47, 52, 55], [36, 43, 48, 52], [38, 45, 50, 54]
    plan = [(4, em), (2, c), (2, dd), (2, em), (2, c), (2, dd)]
    chords, bell, root, t = [], [], [], 0
    for nb, ch in plan:
        chords.append((t * bar, nb * bar, ch))
        t += nb
    for b in range(0, bars, 2):
        # Grundton des gerade klingenden Akkords: der Akkord, dessen Beginn dem Takt am naechsten davor liegt
        chord = [ch for (s, d, ch) in chords if s <= b * bar + 0.01 < s + d][0]
        bell.append((b * bar, 8.0, chord[0], 0.9))
    drums = [(b * bar + i * beat, 0.8, 40, 1.0 if i == 0 else 0.75) for b in range(bars) for i in (0, 2)]
    anvils = [(b * bar + 3 * beat, 1.4, 52 if (b // 2) % 2 == 0 else 50, 0.55) for b in range(2, bars)]
    return dict(n=n_of(bars * bar), choir=chords_to_notes(chords), bell=bell, drum=drums, anvil=anvils, pulse=[],
                mix=dict(bell=-9.0, choir=0.0, drum=-5.0, anvil=-13.0, pulse=0.0),
                pan=dict(bell=-0.25, choir=0.0, drum=0.0, anvil=0.2, pulse=0.0),
                formant=(0.3, 0.15, 0.06), low=0.5)


def boss_b():
    bpm, bars = 96, 16
    beat, bar = 60.0 / bpm, 240.0 / bpm
    em, c, dd, am = [52, 55, 59, 64], [48, 55, 52, 60], [50, 54, 57, 62], [45, 52, 57, 60]
    plan = [(4, em, 40), (2, c, 36), (2, dd, 38), (4, em, 40), (2, am, 45), (2, dd, 38)]
    chords, pulses, bell, t = [], [], [], 0
    acc = [1.0, 0.5, 0.7, 0.5, 0.9, 0.5, 0.7, 0.5]
    for nb, ch, root in plan:
        chords.append((t * bar, nb * bar, ch + [root]))
        for b in range(t, t + nb):
            for i, a in enumerate(acc):
                pulses.append((b * bar + i * beat / 2.0, 0.5, root, a))
        t += nb
    for b, m in ((0, 40), (8, 40), (12, 45)):
        bell.append((b * bar, 8.0, m, 0.8))
    return dict(n=n_of(bars * bar), choir=chords_to_notes(chords), bell=bell, drum=[], anvil=[], pulse=pulses,
                mix=dict(bell=-9.0, choir=0.0, drum=0.0, anvil=0.0, pulse=-5.0),
                pan=dict(bell=-0.25, choir=0.0, drum=0.0, anvil=0.0, pulse=0.0),
                formant=(0.3, 0.15, 0.06), low=0.5)


def haendler_a():
    chords = [(0.0, 10.0, [40, 47, 52, 59]), (10.0, 10.0, [36, 43, 52, 59]), (20.0, 10.0, [38, 45, 50, 54]), (30.0, 10.0, [40, 47, 52, 55])]
    bell = [(2.0, 8.0, 59, 0.6), (6.5, 8.0, 55, 0.5), (12.0, 8.0, 57, 0.6), (16.0, 8.0, 52, 0.5),
            (22.0, 8.0, 59, 0.6), (26.5, 8.0, 62, 0.45), (32.0, 8.0, 55, 0.6), (36.0, 8.0, 52, 0.5)]
    return dict(n=n_of(40.0), choir=chords_to_notes(chords), bell=bell, drum=[], anvil=[], pulse=[],
                mix=dict(bell=-4.0, choir=0.0, drum=0.0, anvil=0.0, pulse=0.0),
                pan=dict(bell=-0.2, choir=0.0, drum=0.0, anvil=0.0, pulse=0.0),
                formant=(0.6, 0.3, 0.1), low=0.5)


def haendler_b():
    # jede Stimme atmet fuer sich: Beginn versetzt, 13 s lang, so ueberlappen sich die Akkordwechsel weich
    voices = [(0.0, 13.0, 40), (0.0, 13.0, 47), (3.0, 13.0, 52), (6.0, 13.0, 55),
              (10.0, 13.0, 40), (12.0, 13.0, 48), (14.0, 13.0, 52), (17.0, 13.0, 57),
              (20.0, 13.0, 38), (22.0, 13.0, 45), (25.0, 13.0, 50), (28.0, 13.0, 54),
              (30.0, 13.0, 40), (32.0, 13.0, 47), (35.0, 13.0, 52), (37.0, 13.0, 55)]
    notes = [(s, d + 0.3, m, 0.9) for s, d, m in voices]
    bell = [(0.0, 8.0, 40, 0.7), (20.0, 8.0, 45, 0.55)]
    return dict(n=n_of(40.0), choir=notes, bell=bell, drum=[], anvil=[], pulse=[],
                mix=dict(bell=-8.0, choir=0.0, drum=0.0, anvil=0.0, pulse=0.0),
                pan=dict(bell=-0.25, choir=0.0, drum=0.0, anvil=0.0, pulse=0.0),
                formant=(1.0, 0.5, 0.2), low=0.6)


STUECKE = {
    "boss-a-schwerer-marsch": boss_a,
    "boss-b-basspuls": boss_b,
    "haendler-a-glocken-tropfen": haendler_a,
    "haendler-b-chor-atem": haendler_b,
}
VOICES = {"bell": bell_voice, "drum": drum, "anvil": anvil, "pulse": pulse}


# --- Mischung ---------------------------------------------------------------------------------------

def rohspuren(spec, cache):
    if cache and Path(cache).exists():
        return pickle.loads(Path(cache).read_bytes())
    n = spec["n"]
    raw = {}
    for name, voice in VOICES.items():
        if spec[name]:
            raw[name] = render_loop(spec[name], voice, n)
    raw["choir"] = render_loop(spec["choir"], lambda m, d, v, k: K.choir_raw(m, d, v, attack=1.6, release=1.8, seed=k), n)
    if cache:
        Path(cache).write_bytes(pickle.dumps(raw))
    return raw


def mischen(spec, raw):
    n = spec["n"]
    pad = 4.0
    choir = choir_post(raw["choir"][-n_of(pad):] + raw["choir"], spec["formant"], spec["low"], pad)
    left, right = zeros(n), zeros(n)
    delay = n_of(0.013)      # Haas-Versatz fuer den Chor wie in musik_erzeugen.py
    for name in ("bell", "choir", "drum", "anvil", "pulse"):
        x = choir if name == "choir" else raw.get(name)
        if x is None:
            continue
        g = lin(spec["mix"][name])
        l, r = pan([v * g for v in normalize(x, 1.0)], spec["pan"][name])
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
    pad8 = n_of(8.0)
    full = [hp1(c[-pad8:] + c, 20.0)[pad8:] for c in full]
    peak = max(max(abs(v) for v in c) for c in full)
    full = [[v * lin(K.PEAK_DB) / peak for v in c] for c in full]
    out = AMB.ps1_loop(full)
    mean_db = 10.0 * math.log10(sum(v * v for c in out for v in c) / (2 * n))
    pk = max(max(abs(v) for v in c) for c in out)
    g = min(lin(TARGET_MEAN_DB - mean_db), lin(PEAK_LIMIT_DB) / pk)
    return [[v * g for v in c] for c in out]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--aus", required=True)
    ap.add_argument("--nur", nargs="*", choices=sorted(STUECKE))
    a = ap.parse_args()
    out = Path(a.aus)
    out.mkdir(parents=True, exist_ok=True)
    cache = os.environ.get("SILVERASH_MUSIK_CACHE")      # nur zum Entwickeln: rohe Spuren zwischenspeichern
    for name, fn in STUECKE.items():
        if a.nur and name not in a.nur:
            continue
        spec = fn()
        c = Path(cache) / f"{name}.pkl" if cache else None
        write_wav(out / f"{name}.wav", mischen(spec, rohspuren(spec, c)))
        print(name, flush=True)


if __name__ == "__main__":
    main()
