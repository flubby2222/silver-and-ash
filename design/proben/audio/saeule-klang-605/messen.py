#!/usr/bin/env python3
"""Misst die WAV-Dateien der Saeulen-Probe mit ffmpeg und schreibt messwerte.md."""
import math
import re
import sys
import struct
import subprocess
import wave
from pathlib import Path

HERE = Path(__file__).resolve().parent
SR = 44100


def ff(args):
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", *args], capture_output=True, text=True)
    return r.stderr


def vol(pfad, filt=""):
    af = (filt + "," if filt else "") + "volumedetect"
    t = ff(["-i", str(pfad), "-af", af, "-f", "null", "-"])
    mean = float(re.search(r"mean_volume: (-?[\d.]+)", t).group(1))
    mx = float(re.search(r"max_volume: (-?[\d.]+)", t).group(1))
    return mean, mx


def lufs(pfad):
    t = ff(["-i", str(pfad), "-af", "ebur128=peak=true", "-f", "null", "-"])
    t = t.split("Summary:")[-1]
    m = re.search(r"I:\s+(-?[\d.]+) LUFS", t)
    return float(m.group(1)) if m else float("nan")


def samples(pfad):
    with wave.open(str(pfad)) as w:
        raw = w.readframes(w.getnframes())
    return [v / 32768 for v in struct.unpack("<%dh" % (len(raw) // 2), raw)]


def staerkster_anstieg(x, fenster=0.010):
    """Zeit des lautesten 10-ms-Fensters (Hauptschlag), in s, und sein Pegel dBFS."""
    n = int(fenster * SR)
    best, wo = -1e9, 0
    for i in range(0, len(x) - n, n // 2):
        a = 20 * math.log10(math.sqrt(sum(v * v for v in x[i:i + n]) / n) + 1e-9)
        if a > best:
            best, wo = a, i
    return wo / SR, best


def pegel_je_100ms(x):
    n = SR // 10
    return [20 * math.log10(math.sqrt(sum(v * v for v in x[i:i + n]) / n) + 1e-9)
            for i in range(0, len(x) - n + 1, n)]


def main():
    zeilen = ["# Messwerte Saeulen-Klang (#605)", "",
              "Gemessen mit ffmpeg und Python (`messen.py`). Gemessen heisst nicht gehoert: ob es gut klingt, entscheidet der Mensch.", "",
              "| Datei | Laenge s | Spitze dBFS | Mittel dBFS (RMS) | Lautheit LUFS | Anteil ueber 4 kHz dB | letzte 50 ms dBFS | lautestes 10-ms-Fenster bei s |",
              "|---|---|---|---|---|---|---|---|"]
    verlaeufe = []
    for p in sorted(HERE.glob("saeule-*.wav")):
        x = samples(p)
        mean, mx = vol(p)
        hm, _ = vol(p, "highpass=f=4000,highpass=f=4000")
        _, ende = vol(p, "areverse,atrim=0:0.05")
        l = lufs(p)
        t, d = staerkster_anstieg(x)
        zeilen.append(f"| {p.name} | {len(x) / SR:.2f} | {mx:.1f} | {mean:.1f} | {l:.1f} | {hm - mean:.1f} | {ende:.1f} | {t:.3f} ({d:.0f} dBFS) |")
        verlaeufe.append((p.name, pegel_je_100ms(x)))
        out = HERE / (p.stem + ".png")
        ff(["-y", "-i", str(p), "-lavfi", "showspectrumpic=s=640x300:legend=0:scale=log", str(out)])
    zeilen += ["", "Anteil ueber 4 kHz: Pegel des doppelt (24 dB/Okt.) bei 4 kHz hochgepassten Signals minus Gesamtmittel; je kleiner, desto weniger Hoehen.", "",
               "## Pegel je 100 ms (dBFS)", ""]
    for n, v in verlaeufe:
        zeilen.append(f"- {n}: " + " ".join(f"{u:.0f}" for u in v))
    (HERE / "messwerte.md").write_text("\n".join(zeilen) + "\n", encoding="utf-8")
    print("\n".join(zeilen))


if __name__ == "__main__":
    # Pflicht-Ordner: nie stillschweigend die Dateien einer anderen Runde neu messen und ueberschreiben
    if len(sys.argv) != 2:
        sys.exit("Aufruf: python messen.py <Ordner mit saeule-*.wav>")
    HERE = Path(sys.argv[1]).resolve()
    main()
