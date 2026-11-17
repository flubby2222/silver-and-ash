"""Prueft musik-*.ogg gegen den technischen Rahmen und die Regeln der Klangwelt (nur Standardbibliothek und ffmpeg).

Aufruf: python musik_pruefen.py <ordner mit den OGG-Dateien>
Je Datei: Laenge (60-90 s), Spitze (<= -1 dBFS), Mittel, Lautheit (LUFS), Naht der dekodierten Datei (Sprung vom letzten zum ersten
Sample gegenueber dem 99,9-%-Abstand zweier Nachbar-Samples, je Kanal), Pegel je Band und Abstand der Mitte (300-3 000 Hz) zum
Gesamtpegel (Ziel Katakomben: mindestens 15 dB), Energie je Tonklasse (Anteil ausserhalb von e-Moll).
"""
import math
import re
import struct
import subprocess
import sys
import tempfile
import wave
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from synth import svf  # noqa: E402

SR = 44100
SCALE = {4, 6, 7, 9, 11, 0, 2}
PC = ["C", "Cis", "D", "Es", "E", "F", "Fis", "G", "As", "A", "B", "H"]


def read(path):
    with wave.open(str(path), "rb") as w:
        ch, n = w.getnchannels(), w.getnframes()
        d = struct.unpack("<%dh" % (n * ch), w.readframes(n))
    return [[v / 32768 for v in d[c::ch]] for c in range(ch)]


def ff(path, af):
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(path), "-af", af, "-f", "null", "-"],
                       capture_output=True, text=True, check=True)
    return r.stderr


def band(x, lo=None, hi=None):
    y = x
    if lo:
        y = svf(svf(y, lo, 0.707, "hp"), lo, 0.707, "hp")
    if hi:
        y = svf(svf(y, hi, 0.707, "lp"), hi, 0.707, "lp")
    return 10 * math.log10(max(1e-20, sum(v * v for v in y) / len(y)))


def goertzel(x, freq, sr):
    k = 2.0 * math.cos(2.0 * math.pi * freq / sr)
    s1 = s2 = 0.0
    for v in x:
        s0 = v + k * s1 - s2
        s2, s1 = s1, s0
    return s1 * s1 + s2 * s2 - k * s1 * s2


def chroma(x):
    f = 4
    y = svf(x, 0.4 * SR / f, 0.707, "lp")
    d = [sum(y[i:i + f]) / f for i in range(0, len(y) - f + 1, f)]
    e = [0.0] * 12
    for m in range(24, 96):
        e[m % 12] += goertzel(d, 440.0 * 2 ** ((m - 69) / 12), SR // f)
    t = sum(e)
    return [v / t for v in e]


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    print("| Datei | Länge | Spitze | Mittel | Lautheit | Naht L / R (Sprung ≤ 99,9 %) | < 300 Hz | 300–3 000 Hz | > 3 000 Hz | gesamt | Abstand Mitte | außerhalb e-Moll | stärkste Tonklasse | Ergebnis |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for p in sorted(Path(sys.argv[1]).glob("musik-*.ogg")):
        with tempfile.TemporaryDirectory() as td:
            dec = Path(td) / "d.wav"
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(p), "-c:a", "pcm_s16le", str(dec)], check=True)
            chs = read(dec)
            e = ff(dec, "volumedetect")
            mx = float(re.search(r"max_volume:\s*(-?[\d.]+)", e).group(1))
            mean = float(re.search(r"mean_volume:\s*(-?[\d.]+)", e).group(1))
            lufs = float(re.findall(r"I:\s*(-?[\d.]+) LUFS", ff(dec, "ebur128"))[-1])
        n = len(chs[0])
        seam, ok = [], True
        for c in chs:
            d = sorted(abs(c[i] - c[i - 1]) for i in range(1, n))
            p999, jump = d[int(0.999 * (len(d) - 1))], abs(c[0] - c[-1])
            ok &= jump <= p999
            seam.append(f"{jump:.5f} ≤ {p999:.5f}")
        mono = [(a + b) / 2 for a, b in zip(*chs)]
        lo, mid, hi = band(mono, None, 300), band(mono, 300, 3000), band(mono, 3000)
        tot = 10 * math.log10(sum(v * v for v in mono) / n)
        ch = chroma(mono)
        out = sum(ch[i] for i in range(12) if i not in SCALE)
        top = max(range(12), key=lambda i: ch[i])
        ok &= 60 <= n / SR <= 90 and mx <= -1.0
        print(f"| `{p.name}` | {n / SR:.1f} s | {mx:.1f} dBFS | {mean:.1f} dBFS | {lufs:.1f} LUFS | {'; '.join(seam)} | {lo:.0f} | {mid:.0f} | {hi:.0f} | {tot:.0f} | "
              f"{mid - tot:.1f} dB | {out * 100:.1f} % | {PC[top]} ({ch[top] * 100:.0f} %) | {'bestanden' if ok else 'NICHT bestanden'} |")


if __name__ == "__main__":
    main()
