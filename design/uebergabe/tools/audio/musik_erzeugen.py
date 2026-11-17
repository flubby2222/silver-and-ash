"""Uebergabe Musik – erzeugt die Musikdateien (Klangwelt "Glocken und Chor", e-Moll bzw. nah verwandt).

Aufruf:
  python musik_erzeugen.py --aus <ordner> [--nur menue|katakomben|boss|boss2|haendler]
Die Anfaenge zum Vorhoeren (erste 28 s als WAV) macht musik_vorhoeren.py aus den fertigen OGG-Dateien.

Stuecke: musik-hauptmenue (60/min, e-Moll), musik-katakomben (60/min, e-Moll), musik-boss und musik-boss-phase2 (100/min,
e-phrygisch), musik-haendler (72/min, G-Dur, die Paralleltonart von e-Moll). Jedes ist eine eigene Komposition.

Palette (vorlaeufiger Massstab, wird ueberarbeitet): tiefe Glocke (unharmonische Teiltoene), Chor-Pad (Stimmquelle durch
Formantfilter), Trommel (Sinus mit fallender Tonhoehe), kurzer Glockenschlag als "Kettenakzent"; EIN Hall (Schroeder,
RT60 2,8 s, Daempfung 2,5 kHz, 50 % Send je Spur, 70 % Hallanteil; links und rechts mit verschiedener Zufallszahl, damit er
breit wird); EINE PS1-Kette (22 050 Hz, ADPCM, Gauss-Interpolation, kein Hall der Kette). Rein synthetisch, keine Aufnahmen.

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

TAIL = 4.0                      # Sekunden Hallschwanz, der auf den Anfang gefaltet wird
U_O = ((310.0, 450.0), (870.0, 800.0), (2250.0, 2830.0))     # Vokal u -> o -> u
A_O = ((700.0, 620.0), (1220.0, 1050.0), (2600.0, 2450.0))   # Vokal a -> o-aehnlich -> a (heller)


# --- Hauptmenue (60/min, e-Moll, 72 Schlaege); Herzschlag nur noch als Pegel-Bezug (siehe ref) --------------------

MENU_CHORDS = [(0, 8, [52, 55, 59, 64]), (8, 8, [52, 55, 60, 64]), (16, 4, [52, 57, 60, 64]), (20, 4, [52, 55, 59, 64]),     # Teil A (Skizze b)
               (24, 8, [50, 55, 59, 67]), (32, 8, [54, 57, 62, 66]), (40, 8, [52, 55, 60, 67]),                                # Teil B
               (48, 12, [52, 57, 60, 64]), (60, 12, [50, 54, 59, 66])]                                                          # Teil C, fuehrt zurueck nach Em
MENU_BELL = [(0, 8, 52, 1.0), (8, 8, 52, 0.9), (16, 8, 47, 1.0), (24, 8, 52, 1.0), (32, 8, 50, 0.9), (40, 8, 48, 0.9),
             (48, 8, 45, 1.0), (56, 8, 47, 0.9), (64, 8, 47, 1.0)]
MENU_HEART_REF = ([(t + (0.38 if dub else 0.0), 0.5, 41 if dub else 40, 0.6 if dub else 1.0) for t in range(12, 22) for dub in (0, 1)]
                  + [(t + (0.38 if dub else 0.0), 0.5, 41 if dub else 40, 0.6 if dub else 1.0) for t in range(28, 46) for dub in (0, 1)]
                  + [(t + (0.38 if dub else 0.0), 0.5, 41 if dub else 40, (1.0 - 0.075 * (t - 48)) * (0.6 if dub else 1.0)) for t in range(48, 58) for dub in (0, 1)])

# --- Katakomben (60/min, e-Moll, 70 Schlaege = 70 s): tiefer Chor und seltene Glocken, sonst nichts ---------------------

KATA_CHORDS = [(0, 14, [47, 52, 55, 59]), (14, 14, [47, 50, 55, 59]), (28, 14, [45, 50, 54, 62]), (42, 14, [43, 52, 55, 60]),
               (56, 14, [47, 50, 54, 59])]     # Em, G, D/A, C/G, Bm; Bm fuehrt nach Em zurueck
KATA_BELL = [(3.0, 8, 52, 0.35), (20.5, 8, 59, 0.30), (33.5, 8, 55, 0.30), (47.0, 8, 62, 0.25), (61.5, 8, 52, 0.30)]

# --- Boss (100/min, e-phrygisch, 112 Schlaege = 67,2 s): Trommelpuls, Kettenakzent, tiefer Chor ---------------------

BOSS_CHORDS = [(0, 16, [43, 47, 52, 55]), (16, 16, [41, 48, 53, 57]), (32, 16, [43, 47, 52, 55]), (48, 16, [41, 45, 50, 57]),
               (64, 16, [43, 47, 52, 55]), (80, 16, [41, 48, 53, 57]), (96, 16, [43, 47, 52, 55])]   # Em F Em Dm Em F Em
BOSS_DRUM = []
for bar in range(28):
    b0 = 4 * bar
    if bar < 8 or bar >= 24:
        pat = [(0.0, 40, 1.0), (2.5, 41, 0.6)]
    elif bar < 16:
        pat = [(0.0, 40, 1.0), (1.5, 41, 0.6), (2.0, 40, 0.8), (3.0, 41, 0.5)]
    else:
        pat = [(0.0, 40, 1.0), (1.0, 41, 0.6), (2.0, 40, 0.9), (3.0, 41, 0.6), (3.5, 41, 0.4)]
    BOSS_DRUM += [(b0 + t, 0.5, note, v) for t, note, v in pat]
BOSS_ACCENT = [(4 * bar + t, 0.5, 69 if k % 2 == 0 else 76, 0.45) for k, (bar, t) in
               enumerate((bar, t) for bar in range(8, 24) for t in (1.5, 3.5))]
# Phase 2: dieselben Spuren plus dichtere Schicht (Achtel-Akzente und Geisterschlaege), gleich lang
BOSS2_ACCENT = BOSS_ACCENT + [(4 * bar + t, 0.5, 69 if k % 2 == 0 else 76, 0.35)
                              for k, (bar, t) in enumerate((bar, t) for bar in range(8, 28) for t in (0.5, 2.5))]
BOSS2_DRUM = BOSS_DRUM + [(4 * bar + t, 0.5, 41, 0.3) for bar in range(16, 28) for t in (0.5, 1.5, 2.5, 3.5)]

# --- Haendler (72/min, G-Dur, 56 Schlaege = 46,7 s): helles Chor-Pad und leise Glocken ----------------------------------

HAEND_CHORDS = [(0, 14, [55, 62, 67, 71]), (14, 14, [57, 62, 66, 74]), (28, 14, [55, 64, 67, 71]), (42, 14, [55, 64, 67, 72])]   # G, D/A, Em, C/G
HAEND_BELL = [(1.0, 8, 67, 0.5), (8.0, 8, 74, 0.4), (15.0, 8, 66, 0.5), (22.0, 8, 69, 0.4),
              (29.0, 8, 64, 0.5), (36.0, 8, 71, 0.4), (43.0, 8, 72, 0.5), (50.0, 8, 67, 0.4)]


def piece(name, bpm, beats, chords, mix, target, vowel=U_O, formant=(1.0, 0.5, 0.2), low=0.6, choir_env=(1.6, 1.8),
          bell=(), accent=(), drum=(), ref=None, send_hp=None):
    return dict(name=name, bpm=bpm, beats=beats, chords=chords, bell=list(bell), accent=list(accent), drum=list(drum), mix=mix,
                target_mean=target, vowel=vowel, formant=formant, low=low, choir_env=choir_env, ref=ref, send_hp=send_hp)


MENU = piece("musik-hauptmenue", 60, 72, MENU_CHORDS, dict(bell=-9.0, choir=-13.0, drum=-7.0), -21.0, bell=MENU_BELL)
MENU_REF = dict(MENU, drum=MENU_HEART_REF)          # Bezug: die fruehere Mischung MIT Herzschlag (Skalierung bleibt gleich)
MENU["ref"] = MENU_REF
KATA = piece("musik-katakomben", 60, 70, KATA_CHORDS, dict(bell=-24.0, choir=0.0), -34.0, formant=(0.22, 0.11, 0.045), low=0.5,
             choir_env=(2.5, 2.5), bell=KATA_BELL, send_hp=150.0)
BOSS = piece("musik-boss", 100, 112, BOSS_CHORDS, dict(choir=-8.0, drum=0.0, accent=-10.0), -26.0, formant=(0.5, 0.25, 0.1),
             low=0.6, choir_env=(2.0, 2.0), accent=BOSS_ACCENT, drum=BOSS_DRUM, send_hp=150.0)
BOSS2 = piece("musik-boss-phase2", 100, 112, BOSS_CHORDS, dict(choir=-8.0, drum=0.0, accent=-10.0), -26.0, formant=(0.5, 0.25, 0.1),
              low=0.6, choir_env=(2.0, 2.0), accent=BOSS2_ACCENT, drum=BOSS2_DRUM, ref=BOSS, send_hp=150.0)
HAEND = piece("musik-haendler", 72, 56, HAEND_CHORDS, dict(bell=-10.0, choir=-3.0), -30.0, vowel=A_O, formant=(1.0, 0.6, 0.3), low=0.2,
              choir_env=(1.8, 2.0), bell=HAEND_BELL, send_hp=150.0)

PIECES = {"menue": MENU, "katakomben": KATA, "boss": BOSS, "boss2": BOSS2, "haendler": HAEND}


# --- Klangerzeuger mit Schleifen-Umbruch ------------------------------------------------------------

def render_loop(notes, voice, n, beat):
    buf = zeros(n)
    for k, (s, d, m, v) in enumerate(notes):
        add_wrapped(buf, voice(m, d * beat, v, k), n_of(s * beat), 1.0)
    return buf


def accent_voice(m, d, v, k):
    """Kettenakzent: kurzer Glockenschlag (0,5 s), die letzten 0,15 s ausgeblendet. Die Akzente liegen auf A4 und E5, weil Quinte und kleine Terz
    des Glockenprofils dann auf Tonleitertoene fallen (auf H wuerde die Quinte Fis klingen, das gegen das F der phrygischen Akkorde steht)."""
    x = K.bell(m, 0.5, v, seed=k)
    f = n_of(0.15)
    for j in range(f):
        x[len(x) - 1 - j] *= j / f
    return x


def choir_post(x, vowel, formant, low_w, pad):
    """Vokalformung (periodisch ueber die Schleife, a -> b -> a): drei Formantfilter, dazu der tiefe Anteil unter 160 Hz.
    x ist mit `pad` Sekunden Vorlauf aus dem Ende der Schleife versehen; der Vorlauf wird danach abgeschnitten."""
    n = len(x)
    p = n_of(pad)
    L = n - p
    ramp = [0.5 - 0.5 * math.cos(2.0 * math.pi * (i - p) / L) for i in range(n)]
    out = [0.0] * n
    for (a, b), q, g in zip(vowel, (5.0, 6.0, 8.0), formant):
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


def render_raw(spec, cache=None, reuse=None):
    """Rohe Spuren eines Stuecks (Glocke, Chor, Trommel, Akzent); optional im Entwicklungs-Zwischenspeicher."""
    beat = 60.0 / spec["bpm"]
    n = n_of(spec["beats"] * beat)
    if cache and Path(cache).exists():
        return pickle.loads(Path(cache).read_bytes())
    att, rel = spec["choir_env"]
    raw = {}
    if reuse is not None:      # Glocke und Chor stimmen mit der Bezugsmischung ueberein: nicht noch einmal rendern
        raw["bell"], raw["choir"] = reuse["bell"], reuse["choir"]
    else:
        raw["bell"] = render_loop(spec["bell"], lambda m, d, v, k: K.bell(m, max(d, 7.0), v, seed=k), n, beat) if spec["bell"] else None
        notes = [(s, d + 0.3 / beat, m, 0.9) for s, d, ch in spec["chords"] for m in ch]
        raw["choir"] = render_loop(notes, lambda m, d, v, k: K.choir_raw(m, d, v, attack=att, release=rel, seed=k), n, beat)
    raw["accent"] = render_loop(spec["accent"], accent_voice, n, beat) if spec["accent"] else None
    raw["drum"] = render_loop(spec["drum"], lambda m, d, v, k: K.drum(120.0 if m == 41 else 100.0, 61.7 if m == 41 else 41.2,
                                                                       0.6, v, seed=k), n, beat) if spec["drum"] else None
    if cache:
        Path(cache).write_bytes(pickle.dumps(raw))
    return raw


def chain(spec, raw, peak_ref=None, g_ref=None, limit_db=-1.5):
    """Mischung, Hall, Gleichanteil, PS1-Kette. Mit peak_ref/g_ref (aus einer Bezugsmischung) bleiben Skalierung und Pegel gleich,
    damit gemeinsame Spuren nicht lauter oder leiser werden; ueberschreitet das die Grenze limit_db, wird abgesenkt."""
    n = len(raw["choir"])
    pad = 4.0
    choir = choir_post(raw["choir"][-n_of(pad):] + raw["choir"], spec["vowel"], spec["formant"], spec["low"], pad)
    mix = spec["mix"]
    tracks = {}
    if raw["bell"] is not None:
        tracks["bell"] = (normalize(raw["bell"], 1.0), mix["bell"], -0.25)
    tracks["choir"] = (normalize(choir, 1.0), mix["choir"], 0.0)
    if raw["drum"] is not None:
        tracks["drum"] = (normalize(raw["drum"], 1.0), mix["drum"], 0.0)
    if raw["accent"] is not None:
        tracks["accent"] = (normalize(raw["accent"], 1.0), mix["accent"], 0.3)
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
    def send(x):
        """Sendsignal fuer den Hall; mit send_hp ohne Tiefen (zwei Hochpaesse, kreisfoermig): Die Kammfilter des Halls
        schwingen sonst bei etwa 45, 55, 90 und 110 Hz mit und faerben die Baesse (siehe README)."""
        y = [v * K.SEND for v in x]
        if spec.get("send_hp"):
            z = n_of(8.0)
            f = spec["send_hp"]
            y = svf(svf(y[-z:] + y, f, 0.707, "hp"), f, 0.707, "hp")[z:]
        return y
    wet_l = fold(reverb(send(left), tail=TAIL, seed=6, **rev), n)
    wet_r = fold(reverb(send(right), tail=TAIL, seed=7, **rev), n)
    full = [[a + K.WET * b for a, b in zip(left, wet_l)], [a + K.WET * b for a, b in zip(right, wet_r)]]
    pad8 = n_of(8.0)    # Gleichanteil (die Stimmquelle ist einseitig) entfernen, kreisfoermig: Vorlauf aus dem Ende, danach abschneiden
    full = [hp1(c[-pad8:] + c, 20.0)[pad8:] for c in full]
    peak = peak_ref if peak_ref is not None else max(max(abs(v) for v in c) for c in full)
    full = [[v * lin(K.PEAK_DB) / peak for v in c] for c in full]
    out = AMB.ps1_loop(full)                           # PS1-Kette, kreisfoermig
    if g_ref is None:
        mean_db = 10.0 * math.log10(sum(v * v for c in out for v in c) / (2 * n))
        g_ref = min(1.0, lin(spec["target_mean"] - mean_db))
    else:
        out_peak = max(max(abs(v) for v in c) for c in out)
        if out_peak * g_ref > lin(limit_db):
            g_ref = lin(limit_db) / out_peak
    return [[v * g_ref for v in c] for c in out], peak, g_ref


def build_piece(spec, cache=None, keep=None):
    raw = render_raw(spec, (Path(cache) / f"{spec['name']}.pkl") if cache else None)
    if spec["ref"] is not None:
        ref = spec["ref"]
        share = ref["chords"] == spec["chords"] and ref["bell"] == spec["bell"] and ref["choir_env"] == spec["choir_env"]
        ref_raw = render_raw(ref, None, reuse=raw if share else None)
        _, peak_ref, g_ref = chain(ref, ref_raw)
        if keep is not None:
            keep["ref"] = (peak_ref, g_ref)
        return chain(spec, raw, peak_ref, g_ref)[0]
    out, peak, g = chain(spec, raw)
    return out


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
    if cache:
        Path(cache).mkdir(parents=True, exist_ok=True)
    for key, spec in PIECES.items():
        if a.nur and a.nur != key:
            continue
        chans = build_piece(spec, cache)
        write_ogg(chans, out / f"{spec['name']}.ogg")
        print(spec["name"])


if __name__ == "__main__":
    main()
