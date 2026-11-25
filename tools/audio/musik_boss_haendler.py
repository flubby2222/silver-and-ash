"""Musik fuer Bosskampf und Haendler (Issue #552) - erzeugt musik-boss.ogg und musik-haendler.ogg.

Aufruf:
  python musik_boss_haendler.py --aus <ordner> [--nur boss|haendler]

Die zwei Fassungen, die der Mensch in der Hoerprobe an #548 gewaehlt hat (21.11.2026): Bosskampf "b - Basspuls",
Haendler "b - Chor-Atem". Noten, Klangerzeuger und Mischung sind unveraendert aus dem Erzeuger der Hoerprobe
(design/proben/audio/musik-boss-haendler-548/erzeugen.py auf dem Branch feat/musik-boss-haendler-probe-548, Commit
d9103b5) uebernommen; weggelassen sind nur die zwei nicht gewaehlten Stuecke (a) und ihre Klangerzeuger (Trommel, Amboss).

Klangwelt wie musik-hauptmenue.ogg und musik-katakomben.ogg (musik_erzeugen.py, musik_klang.py): tiefe Glocke, Chor-Pad,
derselbe Hall, dieselbe PS1-Kette, e-Moll.

Boss     "Basspuls":  96 Schlaege/min, 16 Takte = 40 s. Gleichmaessiger Achtelpuls auf dem Grundton (tiefer Sinus mit
         Obertoenen bis etwa 330 Hz), Chor-Akkorde, drei Glockenschlaege, keine Trommel.
Haendler "Chor-Atem": 40 s, nur Chor-Stimmen, die versetzt ein- und ausatmen; ein tiefer Glockenschlag am Anfang und
         in der Mitte.

Pegel: Mittel -24 dBFS wie musik-katakomben.ogg (Massstab aus Issue #427), Spitze hoechstens -1,5 dBFS.

Bitgenau: Vor dem Umwandeln nach OGG prueft das Skript die gemischte WAV-Fassung gegen die SHA-256 der abgehoerten
Datei aus der Hoerprobe (wav.sha256 der Probe). Weicht sie ab, bricht es ab. Zwei Laeufe ergeben byte-gleiche OGG-Dateien.
Ausgabe: OGG Vorbis (Qualitaet 5) wie die vorhandenen Stuecke, 44,1 kHz, stereo. Rein synthetisch, nur Standardbibliothek
und ffmpeg.
"""
import argparse
import hashlib
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
from musik_erzeugen import TAIL, choir_post, fold, pan, render_loop  # noqa: E402
from synth import SR, hp1, lin, n_of, normalize, reverb, write_wav, zeros  # noqa: E402

PEAK_LIMIT_DB = -1.5      # Spitze der Datei darf nicht hoeher liegen (Rahmen: hoechstens -1 dBFS)
TARGET_MEAN_DB = -24.0    # gleiche mittlere Lautstaerke wie musik-katakomben.ogg (Massstab aus Issue #427)


# --- Klangerzeuger -----------------------------------------------------------------------------------

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


def boss():
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
    return dict(n=n_of(bars * bar), choir=chords_to_notes(chords), bell=bell, pulse=pulses,
                mix=dict(bell=-9.0, choir=0.0, pulse=-5.0),
                pan=dict(bell=-0.25, choir=0.0, pulse=0.0),
                formant=(0.3, 0.15, 0.06), low=0.5)


def haendler():
    # jede Stimme atmet fuer sich: Beginn versetzt, 13 s lang, so ueberlappen sich die Akkordwechsel weich
    voices = [(0.0, 13.0, 40), (0.0, 13.0, 47), (3.0, 13.0, 52), (6.0, 13.0, 55),
              (10.0, 13.0, 40), (12.0, 13.0, 48), (14.0, 13.0, 52), (17.0, 13.0, 57),
              (20.0, 13.0, 38), (22.0, 13.0, 45), (25.0, 13.0, 50), (28.0, 13.0, 54),
              (30.0, 13.0, 40), (32.0, 13.0, 47), (35.0, 13.0, 52), (37.0, 13.0, 55)]
    notes = [(s, d + 0.3, m, 0.9) for s, d, m in voices]
    bell = [(0.0, 8.0, 40, 0.7), (20.0, 8.0, 45, 0.55)]
    return dict(n=n_of(40.0), choir=notes, bell=bell, pulse=[],
                mix=dict(bell=-8.0, choir=0.0, pulse=0.0),
                pan=dict(bell=-0.25, choir=0.0, pulse=0.0),
                formant=(1.0, 0.5, 0.2), low=0.6)


# Schluessel, Dateiname im Spiel, Noten, SHA-256 der abgehoerten WAV-Fassung aus der Hoerprobe (#548).
STUECKE = {
    "boss": ("musik-boss", boss, "27199530c868c4b7810b7a6ac19495ea92b2358c55bc48c8de0c8a8c121e99b3"),
    "haendler": ("musik-haendler", haendler, "ce7800b3ba8036d86295617ec302624e0562723eb920cc8fc8b4f348005bb3ac"),
}
VOICES = {"bell": bell_voice, "pulse": pulse}


# --- Mischung (wie in der Hoerprobe; Reihenfolge der Spuren bleibt, sonst aendern sich die Rundungen) ---

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
    for name in ("bell", "choir", "pulse"):
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


def write_ogg(chans, path, expected_wav_sha256):
    """WAV schreiben, gegen die abgehoerte Fassung pruefen, nach OGG umwandeln (Einstellungen wie musik_erzeugen.py)."""
    wav = Path(str(path) + ".tmp.wav")
    write_wav(wav, chans)
    got = hashlib.sha256(wav.read_bytes()).hexdigest()
    if got != expected_wav_sha256:
        wav.unlink()
        sys.exit(f"{path.name}: die WAV-Fassung weicht von der abgehoerten Hoerprobe ab (SHA-256 {got}, erwartet {expected_wav_sha256})")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav), "-c:a", "libvorbis", "-q:a", "5",
                    "-map_metadata", "-1", "-fflags", "+bitexact", "-flags:a", "+bitexact", str(path)], check=True)
    wav.unlink()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--aus", required=True)
    ap.add_argument("--nur", choices=sorted(STUECKE))
    a = ap.parse_args()
    out = Path(a.aus)
    out.mkdir(parents=True, exist_ok=True)
    cache = os.environ.get("SILVERASH_MUSIK_CACHE")      # nur zum Entwickeln: rohe Spuren zwischenspeichern
    for key, (name, fn, wav_sha) in STUECKE.items():
        if a.nur and a.nur != key:
            continue
        spec = fn()
        c = Path(cache) / f"{key}.pkl" if cache else None
        write_ogg(mischen(spec, rohspuren(spec, c)), out / f"{name}.ogg", wav_sha)
        print(name, flush=True)


if __name__ == "__main__":
    main()
