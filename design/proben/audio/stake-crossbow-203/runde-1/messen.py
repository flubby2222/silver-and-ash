#!/usr/bin/env python3
"""Misst die WAV-Dateien der Hörprobe Stake Crossbow (#203) und schreibt
messwerte.md plus je ein Spektrogramm (PNG, Ordner spektrogramme/).
Braucht ffmpeg im PATH. Aufruf: python messen.py"""
import math
import re
import struct
import subprocess
import wave
from pathlib import Path

HIER = Path(__file__).resolve().parent


def ffmpeg(*args):
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", *args], capture_output=True, text=True)
    return r.stderr


def vol(datei, filt=None):
    t = ffmpeg("-i", str(datei), "-af", (filt + "," if filt else "") + "volumedetect", "-f", "null", "-")
    mean = float(re.search(r"mean_volume: (-?[\d.]+)", t).group(1))
    peak = float(re.search(r"max_volume: (-?[\d.]+)", t).group(1))
    return mean, peak


def lies(datei):
    with wave.open(str(datei)) as w:
        k = w.getnframes()
        d = struct.unpack("<%dh" % k, w.readframes(k))
    return [v / 32768.0 for v in d]


def pegelreihe(x, ms):
    f = int(44100 * ms / 1000)
    return [10 * math.log10(sum(v * v for v in x[i:i + f]) / f + 1e-12) for i in range(0, len(x) - f, f)]


def einsaetze(x, fenster_ms):
    """Stellen, an denen der RMS-Pegel innerhalb von 50 ms um mehr als 8 dB steigt."""
    pegel = pegelreihe(x, fenster_ms)
    schritte = max(1, round(50 / fenster_ms))
    zahl, im = 0, False
    for i in range(len(pegel)):
        j = max(0, i - schritte)
        steigt = pegel[i] - min(pegel[j:i + 1]) > 8 and pegel[i] > -50
        if steigt and not im:
            zahl += 1
        im = steigt
    return zahl


def abklang(x):
    """Zeit (s) bis der 10-ms-Pegel dauerhaft 40 dB unter der Spitze bleibt."""
    pegel = pegelreihe(x, 10)
    top = max(pegel)
    letzte = max(i for i, p in enumerate(pegel) if p > top - 40)
    return (letzte + 1) * 0.01


def ende(x):
    k = x[-2205:]
    return 20 * math.log10(max(abs(v) for v in k) + 1e-9)


def main():
    (HIER / "spektrogramme").mkdir(exist_ok=True)
    zeilen = [
        "# Messwerte Stake-Crossbow-Hörprobe, Runde 1 (#203)\n",
        "Erzeugt von `messen.py`. Alles gemessen, nichts gehört. Spitze = höchster Abtastwert; "
        "Mittel = ffmpeg `volumedetect` über die ganze Datei (Stille eingerechnet); über 3 kHz = Pegel nach "
        "Hochpass 3 kHz minus Gesamtpegel (je kleiner, desto weniger Höhen); Einsätze = Pegelsprünge über 8 dB "
        "in 50 ms (10-ms- und 30-ms-Fenster, siehe Hinweis in der LIESMICH); Abklang = Zeit bis dauerhaft 40 dB "
        "unter der Spitze; Ende = Spitze der letzten 50 ms (Soll höchstens -50 dBFS).\n",
        "| Datei | Länge s | Spitze dBFS | Mittel dB | über 3 kHz dB | Einsätze 10/30 ms | Abklang s | Ende dBFS | Spektrogramm |",
        "|---|---|---|---|---|---|---|---|---|",
    ]
    for p in sorted(HIER.glob("*.wav")):
        x = lies(p)
        mean, peak = vol(p)
        hm, _ = vol(p, "highpass=f=3000")
        png = Path("spektrogramme") / (p.stem + ".png")
        ffmpeg("-y", "-i", str(p), "-lavfi", "showspectrumpic=s=640x320:legend=1:scale=log", str(HIER / png))
        zeilen.append(
            "| %s | %.2f | %.1f | %.1f | %.1f | %d / %d | %.2f | %.1f | [Bild](%s) |"
            % (p.name, len(x) / 44100, peak, mean, hm - mean, einsaetze(x, 10), einsaetze(x, 30),
               abklang(x), ende(x), png.as_posix())
        )
    (HIER / "messwerte.md").write_text("\n".join(zeilen) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
