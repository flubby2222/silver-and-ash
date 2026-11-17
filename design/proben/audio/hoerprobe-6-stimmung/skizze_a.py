"""Hoerprobe 6 – Skizze a "Gruft-Orgel" (D-phrygisch, 50 Schlaege/min, 5 Takte zu 4/4 = 24 s + 2 s Ausklang).

Eigene Komposition, keine Melodie und keine Akkordfolge aus einem bestehenden Werk: Pedalton, vier Streicherstimmen
in enger Stimmfuehrung (Dm - Es - Gm - B - Dm, d. h. i - bII - iv - bVI - i; allgemeine modale Wendungen,
keine Kopie) und ein seltener einstimmiger Orgelgang (F - Es - D) in den letzten Takten.
Aufruf: python skizze_a.py   (schreibt in den Ordner dieses Skripts)
"""
import math
from pathlib import Path

import musik as M
from synth import SR, svf

BPM = 50
BEATS = 20
SCALE = {2, 3, 5, 7, 9, 10, 0}   # D Es F G A B C
TONIC = 2

PEDAL = [(0, 8, 26, 1.0), (8, 4, 31, 1.0), (12, 4, 34, 1.0), (16, 4.5, 26, 1.0)]          # D1 D1 G1 B1 D1
CHORDS = [[50, 57, 62, 65],      # Dm   D3 A3 D4 F4
          [51, 55, 58, 63],      # Es   Es3 G3 B3 Es4
          [50, 58, 62, 67],      # Gm   D3 B3 D4 G4
          [50, 58, 62, 65],      # B    D3 B3 D4 F4
          [50, 57, 62, 65]]      # Dm
STRINGS = [(4 * bar, 4.3, m, 0.9) for bar, ch in enumerate(CHORDS) for m in ch]
ORGAN_LINE = [(12, 2, 77, 0.8), (14, 1, 75, 0.8), (15, 5, 74, 0.8)]                           # F5 Es5 D5

TRACKS = [("Pedal-Orgel", PEDAL, -3.0), ("Streicher", STRINGS, -11.0), ("Orgel-Gang", ORGAN_LINE, -15.0)]
PROGRAMS = {"Pedal-Orgel": (19, 0), "Streicher": (48, 1), "Orgel-Gang": (19, 2)}


def pedal_voice(m, dur, vel, k):
    return M.organ(m, dur, vel, regs=((1, 1.0), (2, 0.6), (3, 0.2)), attack=0.25, release=0.8, seed=k)


def line_voice(m, dur, vel, k):
    return M.organ(m, dur, vel, regs=((1, 1.0), (2, 0.4), (3, 0.15)), attack=0.15, release=1.2, seed=k + 7)


def strings_voice(m, dur, vel, k):
    return M.strings_raw(m, dur, vel, attack=1.0, release=1.2, seed=k)


def strings_post(x):
    """Filterbewegung: zwei Tiefpaesse in Reihe, Grenzfrequenz wandert langsam zwischen 420 und 800 Hz."""
    n = len(x)
    cut = [420.0 + 380.0 * (0.5 + 0.5 * math.sin(2.0 * math.pi * (i / SR) / 9.6 - 1.2)) for i in range(n)]
    return svf(svf(x, cut, 0.9, "lp"), cut, 0.9, "lp")


def build():
    voices = {"Pedal-Orgel": pedal_voice, "Streicher": strings_voice, "Orgel-Gang": line_voice}
    dry, total = M.mix_sketch(TRACKS, BPM, BEATS, voices, {"Streicher": strings_post})
    final, summ, wet = M.master(dry, TRACKS, total, target_mean_db=-19.4)  # gleiche Lautheit (LUFS) wie Skizze b
    return dry, wet, final


def main():
    here = Path(__file__).resolve().parent
    dry, wet, final = build()
    M.ausgeben(here, "a-gruft-orgel", "Skizze a Gruft-Orgel", BPM, TRACKS, dry, wet, final, PROGRAMS,
               ["Tonart: D-phrygisch (D Es F G A B C). Pedal-Orgel liegt auf D1 - G1 - B1 - D1, Streicher in vier Stimmen,",
                "Orgel-Gang (einstimmig, 8' mit 4' und 2 2/3') nur in Takt 4-5."])


if __name__ == "__main__":
    main()
