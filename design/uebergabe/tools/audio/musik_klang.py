"""Hoerprobe 6 – gemeinsame Bausteine fuer die Stimmungsskizzen (Konzept Klangwelt).

Alles reine Synthese mit der Python-Standardbibliothek: Klangerzeuger (Orgel, Streicher,
Chor, Glocke, Trommel), EIN gemeinsamer Hall fuer alles, EINE gemeinsame PS1-Kette,
MIDI-Datei (Standard MIDI File, Format 1) und Einzelspuren als WAV fuer FL Studio.

Eine Skizze ist eine Liste von Spuren; jede Spur hat einen Klangerzeuger, eine
Notenliste (Beginn in Schlaegen, Dauer in Schlaegen, MIDI-Note, Anschlagstaerke) und
einen Mischpegel. Alle Spuren gehen mit demselben Anteil in denselben Hall.
"""
import math
import random
import struct
import sys
import wave
from pathlib import Path


import ps1_klang as P  # noqa: E402
from synth import (SR, add_into, fade_edges, finish, hp1, lin, n_of, normalize, reverb, smooth_noise,  # noqa: E402
                   svf, white, write_wav, zeros)

# --- gemeinsame Festlegungen (fuer BEIDE Skizzen gleich) ---------------------------
TAIL = 2.0            # Sekunden Ausklang nach dem letzten Schlag
REVERB = dict(rt60=2.8, size=1.5, damp_hz=2500.0, seed=6)   # der eine Hall
SEND = 0.5            # jede Spur geht mit diesem Anteil in den Hall
WET = 0.7             # Hallanteil im Gesamtsignal
PEAK_DB = -3.0        # Spitze der Skizze vor der PS1-Kette
TARGET_MEAN_DB = -21.0  # gleiche mittlere Lautstaerke fuer beide Skizzen (RMS, dBFS)
PS1_RATE = 22050


def hz(m):
    return 440.0 * 2.0 ** ((m - 69) / 12.0)


def cents(f, c):
    return f * 2.0 ** (c / 1200.0)


def adsr(n, dur_s, attack, release):
    """Weiche Huellkurve: Anstieg (Kosinus), Halten bis dur_s, Abfall (Kosinus) ueber `release`."""
    env = [0.0] * n
    a, d, r = max(1, int(attack * SR)), int(dur_s * SR), max(1, int(release * SR))
    for i in range(n):
        if i < a:
            e = 0.5 - 0.5 * math.cos(math.pi * i / a)
        else:
            e = 1.0
        if i >= d:
            e *= 0.5 + 0.5 * math.cos(math.pi * min(1.0, (i - d) / r))
        env[i] = e
    return env


# --- Klangerzeuger ----------------------------------------------------------------

def organ(m, dur, vel=1.0, regs=((1, 1.0), (2, 0.5), (3, 0.22), (4, 0.14)), attack=0.08, release=0.5, seed=0):
    """Orgel: Register als Obertoene (8' = 1, 4' = 2, 2 2/3' = 3, 2' = 4), zwei leicht verstimmte
    Stimmen (+-1,2 Cent), langsames Tremulant, ein kurzer Anblas-Hauch am Anfang."""
    rng = random.Random(seed * 131 + m)
    f = hz(m)
    n = n_of(dur + release)
    out = [0.0] * n
    for det in (-1.2, 1.2):
        fv = cents(f, det)
        for k, w in regs:
            if fv * k > 0.45 * SR:
                continue
            wk = 2.0 * math.pi * fv * k / SR
            p0 = rng.uniform(0, 2 * math.pi)
            amp = 0.5 * w
            for i, v in enumerate([amp * math.sin(wk * i + p0) for i in range(n)]):
                out[i] += v
    wt = 2.0 * math.pi * 5.2 / SR
    env = adsr(n, dur, attack, release)
    out = [v * e * (1.0 + 0.04 * math.sin(wt * i)) for i, (v, e) in enumerate(zip(out, env))]
    cn = n_of(0.03)
    chiff = svf(white(cn, rng), min(f * 2.0, 4000.0), 4.0, "bp")
    for i in range(cn):
        out[i] += 0.10 * chiff[i] * math.exp(-i / (0.008 * SR))
    return [v * vel for v in out]


def strings_raw(m, dur, vel=1.0, attack=1.0, release=1.2, seed=0):
    """Streicher-Rohstimme: vier leicht verstimmte Saegezaehne (-9, -3, +4, +10 Cent), sehr langsames
    Vibrato mit eigener Phase je Saegezahn, langsamer Einsatz. Gefiltert wird spaeter die ganze Spur."""
    rng = random.Random(seed * 977 + m)
    f = hz(m)
    n = n_of(dur + release)
    out = [0.0] * n
    for det in (-9.0, -3.0, 4.0, 10.0):
        fv = cents(f, det)
        vf, vd = rng.uniform(4.6, 5.4), 0.0016
        ph0, vp = rng.random(), rng.uniform(0, 6.28)
        k = fv * vd / (2.0 * math.pi * vf)
        w = 2.0 * math.pi * vf / SR
        for i in range(n):
            p = fv * i / SR + ph0 + k * (math.cos(vp) - math.cos(w * i + vp))
            out[i] += 2.0 * (p % 1.0) - 1.0
    env = adsr(n, dur, attack, release)
    return [v * e * 0.25 * vel for v, e in zip(out, env)]


def choir_raw(m, dur, vel=1.0, attack=1.6, release=1.8, seed=0):
    """Chor-Rohstimme: drei leicht verstimmte Stimmquellen (Impulszug mit erhoehtem Kosinus, wie die Stimme
    des Ghuls), langsames Vibrato. Die Vokalformung (u -> o) folgt spaeter fuer die ganze Spur."""
    rng = random.Random(seed * 523 + m)
    f = hz(m)
    n = n_of(dur + release)
    out = [0.0] * n
    for det in (-7.0, 0.0, 7.0):
        fv = cents(f, det)
        vf = rng.uniform(4.4, 5.0)
        vp, ph = rng.uniform(0, 6.28), rng.random()
        k = fv * 0.004 / (2.0 * math.pi * vf)
        w = 2.0 * math.pi * vf / SR
        for i in range(n):
            p = (fv * i / SR + ph + k * (math.cos(vp) - math.cos(w * i + vp))) % 1.0
            if p < 0.55:
                out[i] += math.sin(math.pi * p / 0.55) ** 2
    env = adsr(n, dur, attack, release)
    return [v * e * 0.3 * vel for v, e in zip(out, env)]


BELL_PARTIALS = ((0.5, 1.0, 6.0), (1.0, 0.9, 5.0), (1.183, 0.55, 3.2), (1.506, 0.45, 2.6), (2.0, 0.5, 2.2),
                 (2.514, 0.3, 1.3), (2.662, 0.28, 1.2), (3.011, 0.2, 0.9), (4.166, 0.12, 0.5))  # Verhaeltnis, Pegel, Abklingzeit s


def bell(m, dur, vel=1.0, seed=0):
    """Tiefe Glocke: unharmonische Teiltoene eines Glockenprofils (Unterton, Prim, kleine Terz, Quinte, Oktave ...),
    jeder mit eigener Abklingzeit; Unterton und Prim leicht verdoppelt (langsames Schweben); kurzer Schlag-Hauch."""
    rng = random.Random(seed * 71 + m)
    f = hz(m)
    n = n_of(dur)
    out = [0.0] * n
    for ratio, amp, tau in BELL_PARTIALS:
        for off in ((0.0, 0.6) if ratio <= 1.0 else (0.0,)):
            fr = f * ratio + off
            if fr > 0.45 * SR:
                continue
            w = 2.0 * math.pi * fr / SR
            p0 = rng.uniform(0, 6.28)
            a = amp * (0.7 if off else 1.0)
            dec = 1.0 / (tau * SR)
            for i, v in enumerate([a * math.sin(w * i + p0) * math.exp(-i * dec) for i in range(n)]):
                out[i] += v
    cn = n_of(0.02)
    tick = svf(white(cn, rng), f * 2.0, 3.0, "bp")
    for i in range(cn):
        out[i] += 0.4 * tick[i] * math.exp(-i / (0.005 * SR))
    for i in range(min(n, int(0.003 * SR))):
        out[i] *= i / (0.003 * SR)
    return [v * 0.12 * vel for v in out]


def drum(f_start, f_end, dur, vel=1.0, seed=0):
    """Dumpfer Trommelschlag: Sinus, dessen Tonhoehe in 60 ms von f_start auf f_end faellt, dazu ein kurzer
    Tiefpass-Rauschschlag."""
    rng = random.Random(seed + int(f_end))
    n = n_of(dur)
    out = [0.0] * n
    ph = 0.0
    for i in range(n):
        t = i / SR
        f = f_end + (f_start - f_end) * math.exp(-t / 0.02)
        ph += f / SR
        out[i] = math.sin(2.0 * math.pi * ph) * math.exp(-t / 0.14)
    cn = n_of(0.02)
    tick = svf(white(cn, rng), 300.0, 0.7, "lp")
    for i in range(cn):
        out[i] += 0.5 * tick[i] * math.exp(-i / (0.006 * SR))
    for i in range(min(n, int(0.004 * SR))):
        out[i] *= i / (0.004 * SR)
    return [v * vel for v in out]


# --- Spuren -------------------------------------------------------------------------

def beat_s(bpm):
    return 60.0 / bpm


def render_track(notes, bpm, voice, total_s, post=None):
    """Rendert eine Spur (mono, trocken). notes: (start_schlag, dauer_schlaege, midi, anschlag)."""
    buf = zeros(n_of(total_s))
    b = beat_s(bpm)
    for k, (s, d, m, v) in enumerate(notes):
        add_into(buf, voice(m, d * b, v, k), n_of(s * b), 1.0)
    return post(buf) if post else buf


def common_reverb(x):
    """Der eine Hall: Schroeder-Hall mit Daempfung; gibt nur den Hallanteil zurueck, auf die Laenge von x gekuerzt."""
    wet = reverb(x, tail=1.0, **REVERB)
    return wet[:len(x)]


def mix_sketch(tracks, bpm, bars_beats, voice_fns, posts):
    """tracks: Liste (name, notes, mix_db). Gibt (trockene Spuren, Hallbus, Gesamtsignal) zurueck, alles ungefaehr -6 dBFS-normalisiert
    nur fuer die Stems; das Gesamtsignal ist die Summe nach Mischpegeln."""
    total = bars_beats * beat_s(bpm) + TAIL
    dry = {}
    for name, notes, _ in tracks:
        dry[name] = render_track(notes, bpm, voice_fns[name], total, posts.get(name))
    return dry, total


def master(dry, tracks, total, target_mean_db=None):
    """Mischt die Spuren mit ihren Pegeln, schickt SEND jeder Spur in den einen Hall, summiert, formt Anfang/Ende,
    setzt die Spitze und schickt alles durch die PS1-Kette (Rate PS1_RATE, kein Hall der Kette)."""
    n = n_of(total)
    summ = zeros(n)
    for name, _, mix_db in tracks:
        g = lin(mix_db)
        for i, v in enumerate(normalize(dry[name], 1.0)):   # jede Spur erst auf Spitze 1, dann Mischpegel
            summ[i] += v * g
    wet = common_reverb([v * SEND for v in summ])
    full = [d + WET * w for d, w in zip(summ, wet)]
    full = finish(full, PEAK_DB, length=total, fade_in_ms=30.0, fade_out_ms=600.0)
    ints = [int(round(v * 32767)) for v in full]
    out = [v / 32767.0 for v in P.process([ints], rate=PS1_RATE)[0]]
    # beide Skizzen auf gleiche mittlere Lautstaerke (fuer den a/b-Vergleich); nur absenken, nie anheben
    mean_db = 10.0 * math.log10(max(1e-20, sum(v * v for v in out) / len(out)))
    g = min(1.0, lin((TARGET_MEAN_DB if target_mean_db is None else target_mean_db) - mean_db))
    return [v * g for v in out], summ, wet


# --- Ausgabe fuer FL Studio ---------------------------------------------------------

def write_wav16(path, x):
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", max(-32768, min(32767, int(round(v * 32767))))) for v in x))


def vlq(n):
    out = [n & 0x7F]
    n >>= 7
    while n:
        out.append((n & 0x7F) | 0x80)
        n >>= 7
    return bytes(reversed(out))


def write_midi(path, bpm, tracks, programs):
    """Standard MIDI File (Format 1, 480 Ticks je Viertel). tracks: (name, notes); programs: name -> (programm, kanal)."""
    ppq = 480

    def chunk(tag, data):
        return tag + struct.pack(">I", len(data)) + data
    out = [chunk(b"MThd", struct.pack(">HHH", 1, len(tracks) + 1, ppq))]
    tempo = chunk(b"MTrk", vlq(0) + b"\xff\x51\x03" + struct.pack(">I", int(60_000_000 / bpm))[1:]
                  + vlq(0) + b"\xff\x58\x04\x04\x02\x18\x08" + vlq(0) + b"\xff\x2f\x00")
    out.append(tempo)
    for name, notes in tracks:
        prog, ch = programs[name]
        data = vlq(0) + b"\xff\x03" + bytes([len(name)]) + name.encode("ascii")
        if ch != 9:
            data += vlq(0) + bytes([0xC0 | ch, prog])
        ev = []
        for s, d, m, v in notes:
            ev.append((int(round(s * ppq)), 1, m, v))
            ev.append((int(round((s + d) * ppq)), 0, m, 0))
        ev.sort(key=lambda e: (e[0], e[1]))
        t = 0
        for tick, on, m, v in ev:
            data += vlq(tick - t) + bytes([(0x90 if on else 0x80) | ch, m, max(1, min(127, int(v * 100))) if on else 0])
            t = tick
        data += vlq(0) + b"\xff\x2f\x00"
        out.append(chunk(b"MTrk", data))
    Path(path).write_bytes(b"".join(out))


def slug(name):
    return name.lower().replace(" ", "-").replace("ä", "ae").replace("ö", "oe").replace("ü", "ue")


def ausgeben(outdir, key, title, bpm, tracks, dry, wet, final, programs, extra_notes):
    """Schreibt <key>.wav (PS1-Fassung), fl-studio/<key>/ mit MIDI, Einzelspuren (16 Bit, mono, 44,1 kHz,
    gleich lang, ab Sample 0, je -6 dBFS) und mischung.txt (Pegel je Spur, Hall)."""
    outdir = Path(outdir)
    write_wav(outdir / f"{key}.wav", [final])
    fl = outdir / "fl-studio" / key
    fl.mkdir(parents=True, exist_ok=True)
    write_midi(fl / f"{key}.mid", bpm, [(n, notes) for n, notes, _ in tracks], programs)
    lines = [f"{title} - Mischung (Spuren je auf -6 dBFS Spitze normalisiert)", "",
             f"Tempo: {bpm} Schlaege/min, Taktart 4/4. Alle Dateien: mono, 44 100 Hz, 16 Bit, gleich lang, beginnen bei Sample 0.", "",
             "Meine Mischung (Regler je Spur; 0 dB = Datei wie geliefert):"]
    for i, (name, _, mix_db) in enumerate(tracks, 1):
        fname = f"{key}_{i}_{slug(name)}.wav"
        write_wav16(fl / fname, normalize(dry[name], lin(-6.0)))
        lines.append(f"  {fname}: {mix_db + 6.0:+.1f} dB")
    write_wav16(fl / f"{key}_9_hall-bus.wav", normalize(wet, lin(-6.0)))
    lines += [f"  {key}_9_hall-bus.wav: nur der Hall (mein Schroeder-Hall, RT60 {REVERB['rt60']} s, aus der Summe aller Spuren); "
              f"+0 dB; ersetzbar durch einen eigenen Hall-Bus mit {SEND:.0%} Send je Spur und {WET:.0%} Hallanteil", "",
              "Danach habe ich alles durch meine PS1-Kette geschickt (22 050 Hz, ADPCM, Gauss-Interpolation); "
              f"das Ergebnis ist {key}.wav im Ordner darueber.", ""] + list(extra_notes)
    (fl / "mischung.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
