"""Hoerprobe 6 – Skizze b "Glocken und Chor" (e-Moll, 60 Schlaege/min, 6 Takte zu 4/4 = 24 s + 2 s Ausklang).

Eigene Komposition, keine Melodie und keine Akkordfolge aus einem bestehenden Werk: eine tiefe Glocke (E - E - H),
ein Chor-Pad in vier Stimmen (Em - C/E - Am/E - Em; allgemeine modale Wendungen, keine Kopie), dessen Vokal sich langsam
von "u" nach "o" oeffnet, und eine Herzschlag-Trommel auf dem Grundton nur in den letzten zehn Schlaegen.
Aufruf: python skizze_b.py   (schreibt in den Ordner dieses Skripts)
"""
from pathlib import Path

import musik as M
from synth import svf

BPM = 60
BEATS = 24
SCALE = {4, 6, 7, 9, 11, 0, 2}   # E Fis G A H C D
TONIC = 4

BELL = [(0, 8, 52, 1.0), (8, 8, 52, 0.9), (16, 8, 47, 1.0)]                                   # E3 E3 H2
CHOIR_CHORDS = [(0, 8, [52, 59, 64, 67]), (8, 8, [52, 60, 64, 67]), (16, 4, [52, 57, 64, 69]), (20, 4, [52, 59, 64, 67])]
CHOIR = [(s, d + 0.3, m, 0.9) for s, d, ch in CHOIR_CHORDS for m in ch]
# lub-dub je Schlag in den Schlaegen 12-22; MIDI-Taste 40 als Platzhalter (die Tonhoehe liegt im Klangerzeuger)
HEART = [(t + (0.38 if dub else 0.0), 0.5, 40, 0.6 if dub else 1.0) for t in range(12, 22) for dub in (0, 1)]

TRACKS = [("Glocke", BELL, -9.0), ("Chor", CHOIR, -13.0), ("Herzschlag", HEART, -7.0)]
PROGRAMS = {"Glocke": (14, 0), "Chor": (52, 1), "Herzschlag": (0, 9)}


def bell_voice(m, dur, vel, k):
    return M.bell(m, max(dur, 7.0), vel, seed=k)


def choir_voice(m, dur, vel, k):
    return M.choir_raw(m, dur, vel, attack=1.6, release=1.8, seed=k)


def heart_voice(m, dur, vel, k):
    dub = vel < 0.9
    return M.drum(120.0 if dub else 100.0, 61.7 if dub else 41.2, 0.6, vel, seed=k)


def choir_post(x):
    """Vokalformung u -> o ueber die ganze Skizze: drei Formantfilter, deren Mittelfrequenzen wandern
    (u 310/870/2250 Hz -> o 450/800/2830 Hz), dazu der tiefe Anteil unter 160 Hz."""
    n = len(x)
    ramp = [min(1.0, i / n) for i in range(n)]
    out = [0.0] * n
    for (a, b), q, g in (((310.0, 450.0), 5.0, 1.0), ((870.0, 800.0), 6.0, 0.5), ((2250.0, 2830.0), 8.0, 0.2)):
        y = svf(x, [a + (b - a) * r for r in ramp], q, "bp")
        for i, v in enumerate(y):
            out[i] += g * v
    low = svf(x, 160.0, 1.0, "lp")
    return [o + 0.6 * lo for o, lo in zip(out, low)]


def build():
    voices = {"Glocke": bell_voice, "Chor": choir_voice, "Herzschlag": heart_voice}
    dry, total = M.mix_sketch(TRACKS, BPM, BEATS, voices, {"Chor": choir_post})
    final, summ, wet = M.master(dry, TRACKS, total)
    return dry, wet, final


def main():
    here = Path(__file__).resolve().parent
    dry, wet, final = build()
    M.ausgeben(here, "b-glocken-chor", "Skizze b Glocken und Chor", BPM, TRACKS, dry, wet, final, PROGRAMS,
               ["Tonart: e-Moll (E Fis G A H C D). Glocke E3 - E3 - H2 (laeuft je 8 Schlaege aus), Chor in vier Stimmen,",
                "Herzschlag-Trommel nur in den Schlaegen 12-22, Tonhoehe faellt auf E1 (Grundton) bzw. H1 (die zweite Note).",
                "In der MIDI-Datei liegt die Trommel auf Kanal 10 (Taste 40); sie ist dort nur ein Platzhalter."])


if __name__ == "__main__":
    main()
