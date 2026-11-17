"""Prueft die erzeugten Dateien gegen den technischen Rahmen der Audio Bible und gibt eine Tabelle aus.
Aufruf: python pruefen.py <ordner mit den Dateien>   (nur Standardbibliothek und ffmpeg)"""
import math
import re
import struct
import subprocess
import sys
import tempfile
import wave
from pathlib import Path

LIMIT = {"pistole": 0.6, "klinge": 0.6, "schritt": 0.3, "ghul": 1.5}


def read(path):
    with wave.open(str(path), "rb") as w:
        ch, n = w.getnchannels(), w.getnframes()
        d = struct.unpack("<%dh" % (n * ch), w.readframes(n))
        return [[v / 32768 for v in d[c::ch]] for c in range(ch)], w.getframerate()


def vol(path):
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(path), "-af", "volumedetect", "-f", "null", "-"],
                       capture_output=True, text=True, check=True)
    return float(re.search(r"max_volume:\s*(-?[\d.]+)", r.stderr).group(1)), float(re.search(r"mean_volume:\s*(-?[\d.]+)", r.stderr).group(1))


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    out = Path(sys.argv[1])
    print("| Datei | Format | Länge | Spitze | Mittel | Ränder / Naht | Ergebnis |\n|---|---|---|---|---|---|---|")
    for p in sorted(out.iterdir()):
        if p.suffix == ".ogg":
            with tempfile.TemporaryDirectory() as td:
                dec = Path(td) / "d.wav"
                subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(p), "-c:a", "pcm_s16le", str(dec)], check=True)
                chs, sr = read(dec)
                mx, mean = vol(dec)
            seam = []
            ok = True
            for c in chs:
                d = sorted(abs(c[i] - c[i - 1]) for i in range(1, len(c)))
                p999, jump = d[int(0.999 * (len(d) - 1))], abs(c[0] - c[-1])
                ok &= jump <= p999
                seam.append(f"{jump:.5f} ≤ {p999:.5f}")
            lo_s, hi_s = (60, 90) if p.name.startswith("musik") else (20, 40)   # Musik 60-90 s, Ambiente 20-40 s
            ok &= lo_s <= len(chs[0]) / sr <= hi_s and mx <= -1.0
            print(f"| `{p.name}` | OGG, {sr} Hz, stereo | {len(chs[0]) / sr:.1f} s | {mx:.1f} dBFS | {mean:.1f} dBFS | Naht L/R: {'; '.join(seam)} | {'bestanden' if ok else 'NICHT'} |")
            continue
        chs, sr = read(p)
        x = chs[0]
        n = len(x)
        mx, mean = vol(p)
        w = int(0.02 * sr)
        peak = max(abs(v) for v in x)
        tail = 20 * math.log10(max(1e-9, math.sqrt(sum(v * v for v in x[-2 * w:-w]) / w) / peak))
        lim = LIMIT[p.stem.split("-")[0]]
        ok = x[0] == 0 and x[-1] == 0 and tail < -40 and mx <= -1.0 and n / sr < lim and sr == 44100 and len(chs) == 1
        print(f"| `{p.name}` | WAV, {sr} Hz, mono | {n / sr:.3f} s (< {lim}) | {mx:.1f} dBFS | {mean:.1f} dBFS | 0 / 0, Rest {tail:.0f} dB | {'bestanden' if ok else 'NICHT'} |")


main()
