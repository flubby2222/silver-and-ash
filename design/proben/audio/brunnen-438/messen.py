#!/usr/bin/env python3
"""Misst die WAV-Dateien der Brunnen-Hoerprobe (#438) und schreibt messwerte.md
und je ein Spektrogramm (PNG). Braucht ffmpeg im PATH. Aufruf: python messen.py"""
import math
import re
import struct
import subprocess
import wave
from pathlib import Path

HIER = Path(__file__).resolve().parent
SR = 44100


def ffmpeg(*args):
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", *args], capture_output=True, text=True)
    return r.stderr


def vol(datei, filt=None):
    a = ["-i", str(datei), "-af", (filt + ",volumedetect") if filt else "volumedetect"]
    t = ffmpeg(*a, "-f", "null", "-")
    mean = float(re.search(r"mean_volume: (-?[\d.]+)", t).group(1))
    peak = float(re.search(r"max_volume: (-?[\d.]+)", t).group(1))
    return mean, peak


def lies(datei):
    with wave.open(str(datei)) as w:
        k = w.getnframes()
        d = struct.unpack("<%dh" % k, w.readframes(k))
    return [v / 32768.0 for v in d]


def einsaetze(x, fenster_ms):
    """Stellen, an denen der RMS-Pegel innerhalb von 50 ms um mehr als 8 dB steigt."""
    f = int(SR * fenster_ms / 1000)
    pegel = [10 * math.log10(sum(v * v for v in x[i:i + f]) / f + 1e-12) for i in range(0, len(x) - f, f)]
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
    f = 441
    pegel = [10 * math.log10(sum(v * v for v in x[i:i + f]) / f + 1e-12) for i in range(0, len(x) - f, f)]
    top = max(pegel)
    letzte = 0
    for i, p in enumerate(pegel):
        if p > top - 40:
            letzte = i
    return (letzte + 1) * 0.01


def ende(x):
    """Spitze (dBFS) der letzten 50 ms (Regel 7: hoechstens -50 dBFS)."""
    return 20 * math.log10(max(abs(v) for v in x[-2205:]) + 1e-9)


def naht(x):
    """Schleifennaht: Sprung letztes -> erstes Sample im Verhaeltnis zum groessten
    Nachbarsprung innerhalb der Datei (je kleiner, desto weniger hoerbar)."""
    sprung = abs(x[0] - x[-1])
    groesst = max(abs(x[i + 1] - x[i]) for i in range(len(x) - 1))
    return sprung, groesst


def main():
    zeilen = [
        "# Messwerte Brunnen-Hoerprobe (#438)\n",
        "Erzeugt von `messen.py`. Alles gemessen, nichts gehoert. Spitze = hoechster Abtastwert; "
        "Mittel = ffmpeg `volumedetect`; ueber 3 kHz = Pegel nach Hochpass 3 kHz minus Gesamtpegel; "
        "Mitte 300-3000 = Pegel im Band 300 bis 3000 Hz minus Gesamtpegel; Einsaetze = Pegelspruenge "
        "ueber 8 dB in 50 ms (10-/30-ms-Fenster); Abklang = Zeit bis 40 dB unter Spitze; "
        "Ende = Spitze der letzten 50 ms (Regel 7, Soll hoechstens -50 dBFS; bei Schleifen entfaellt sie).\n",
        "## Einzelklaenge\n",
        "| Datei | Laenge s | Spitze dBFS | Mittel dB | ueber 3 kHz dB | Einsaetze 10/30 ms | Abklang s | Ende dBFS | Spektrogramm |",
        "|---|---|---|---|---|---|---|---|---|",
    ]
    schleifen = [
        "\n## Schleifen\n",
        "Naht = Betrag des Sprungs letztes -> erstes Sample, daneben der groesste Sprung zwischen zwei "
        "Nachbarwerten in der Datei (Naht soll nicht darueber liegen). Die Fassung `-ohne-tropfen` ist das "
        "Rauschbett von B allein (Regel 6 misst die Flaeche ohne Einzelereignisse).\n",
        "| Datei | Laenge s | Spitze dBFS | Mittel dB | ueber 3 kHz dB | Mitte 300-3000 dB | Naht / groesster Nachbarsprung | Spektrogramm |",
        "|---|---|---|---|---|---|---|---|",
    ]
    for p in sorted(HIER.glob("*.wav")):
        x = lies(p)
        mean, peak = vol(p)
        hm, _ = vol(p, "highpass=f=3000")
        png = p.with_suffix(".png")
        ffmpeg("-y", "-i", str(p), "-lavfi", "showspectrumpic=s=640x320:legend=1:scale=log", str(png))
        laenge = len(x) / SR
        if p.name.startswith("wasser"):
            mm, _ = vol(p, "highpass=f=300,lowpass=f=3000")
            s, g = naht(x)
            schleifen.append("| %s | %.2f | %.1f | %.1f | %.1f | %.1f | %.5f / %.5f | ![](%s) |"
                             % (p.name, laenge, peak, mean, hm - mean, mm - mean, s, g, png.name))
        else:
            zeilen.append("| %s | %.2f | %.1f | %.1f | %.1f | %d / %d | %.2f | %.1f | ![](%s) |"
                          % (p.name, laenge, peak, mean, hm - mean, einsaetze(x, 10), einsaetze(x, 30),
                             abklang(x), ende(x), png.name))
    (HIER / "messwerte.md").write_text("\n".join(zeilen + schleifen) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
