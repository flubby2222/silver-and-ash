#!/usr/bin/env python3
"""Misst die WAV-Dateien der Hörprobe Hunter's Axe, Runde 2 (#201), und schreibt
messwerte.md plus je ein Spektrogramm (PNG, Ordner spektrogramme/).
Braucht ffmpeg im PATH. Aufruf: python messen.py

Neu gegenüber Runde 1: Spektrum der letzten 300 ms (Hann-Fenster, FFT 16384, reine Python-
Standardbibliothek). Gemeldet werden
  - "Spitze > 400 Hz": höchster Bin über 400 Hz, in dB unter dem höchsten Bin des ganzen Fensters;
  - "Hervortreten": derselbe Bin minus der Median seiner Nachbarn im Abstand 40 bis 150 Hz
    (je größer, desto tonaler steht ein Teilton aus dem Rauschboden heraus; um 0 bis 10 dB ist Rauschen);
  - "Energie > 400 Hz": Anteil der Energie über 400 Hz an der Energie des Fensters, in dB.
"""
import cmath
import math
import re
import struct
import subprocess
import wave
from pathlib import Path

HIER = Path(__file__).resolve().parent
RUNDE1 = HIER.parent / "runde-1"
VERGLEICH = ["aufladen-a-knarzen.wav", "aufladen-b-brummen.wav", "aufladen-c-ratsche.wav"]
SR = 44100
FFT_N = 16384


def ffmpeg(*args):
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", *args], capture_output=True, text=True)
    return r.stderr


def vol(datei):
    t = ffmpeg("-i", str(datei), "-af", "volumedetect", "-f", "null", "-")
    return float(re.search(r"mean_volume: (-?[\d.]+)", t).group(1)), float(re.search(r"max_volume: (-?[\d.]+)", t).group(1))


def lies(datei):
    with wave.open(str(datei)) as w:
        k = w.getnframes()
        d = struct.unpack("<%dh" % k, w.readframes(k))
    return [v / 32768.0 for v in d]


def fft(a):
    k = len(a)
    if k == 1:
        return a
    g = fft(a[0::2])
    u = fft(a[1::2])
    out = [0j] * k
    for i in range(k // 2):
        t = cmath.exp(-2j * math.pi * i / k) * u[i]
        out[i] = g[i] + t
        out[i + k // 2] = g[i] - t
    return out


def spektrum(x, sek, ab=None):
    """Betragsspektrum der letzten `sek` Sekunden (oder ab Sekunde `ab`) mit Hann-Fenster, Länge FFT_N/2."""
    k = int(SR * sek)
    f = x[-k:] if ab is None else x[int(SR * ab):int(SR * ab) + k]
    w = [f[i] * (0.5 - 0.5 * math.cos(TAU / (k - 1) * i)) for i in range(k)]
    w += [0.0] * (FFT_N - k)
    return [abs(c) for c in fft([complex(v) for v in w])[:FFT_N // 2]]


TAU = 2 * math.pi


def schluss_spektrum(x, sek=0.3, ab=None):
    """Gibt (Hz, Spitze dB unter Fenstermaximum, Hervortreten dB, Energie > 400 Hz dB) zurück.
    Spitze = höchster Bin über 400 Hz; Hervortreten = höchste lokale Spitze (Bin größer als alle
    Bins im Abstand bis 20 Hz) über 400 Hz minus Mittel der Mediane links und rechts davon
    (Abstand 40 bis 150 Hz, in dB), damit ein steiler Abfall nicht als Teilton zählt."""
    sp = spektrum(x, sek, ab)
    bh = SR / FFT_N
    top = max(sp) or 1e-12
    b400 = int(400 / bh) + 1
    bend = int(8000 / bh)
    bmax = max(range(b400, bend), key=lambda i: sp[i])
    pk = 20 * math.log10(sp[bmax] / top + 1e-12)
    k20, k40, k150 = int(20 / bh), int(40 / bh), int(150 / bh)
    beste = -999.0
    for i in range(b400, bend):
        if sp[i] < max(sp[i - k20:i + k20 + 1]) or sp[i] <= 0:
            continue
        li = sorted(sp[i - k150:i - k40 + 1])
        re_ = sorted(sp[i + k40:i + k150 + 1])
        ref = 0.5 * (20 * math.log10(li[len(li) // 2] + 1e-12) + 20 * math.log10(re_[len(re_) // 2] + 1e-12))
        beste = max(beste, 20 * math.log10(sp[i] + 1e-12) - ref)
    ges = sum(v * v for v in sp)
    hoch = sum(v * v for v in sp[b400:])
    return bmax * bh, pk, beste, 10 * math.log10(hoch / ges + 1e-12)


def pegelreihe(x, ms):
    f = int(SR * ms / 1000)
    return [10 * math.log10(sum(v * v for v in x[i:i + f]) / f + 1e-12) for i in range(0, len(x) - f, f)]


def einsaetze(x, fenster_ms):
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


def ende(x):
    return 20 * math.log10(max(abs(v) for v in x[-2205:]) + 1e-9)


def main():
    (HIER / "spektrogramme").mkdir(exist_ok=True)
    zeilen = [
        "# Messwerte Hunter's-Axe-Hörprobe, Runde 2 (#201): Aufladen\n",
        "Erzeugt von `messen.py`. Alles gemessen, nichts gehört. Spitze = höchster Abtastwert; Mittel = ffmpeg "
        "`volumedetect` über die ganze Datei; Einsätze = Pegelsprünge über 8 dB in 50 ms (10-/30-ms-Fenster); "
        "Ende = Spitze der letzten 50 ms (Soll höchstens -50 dBFS). Die letzten drei Spalten messen das Spektrum "
        "der **letzten 300 ms** (Hann, FFT 16384, Auflösung 2,7 Hz): Spitze > 400 Hz = höchster Bin über 400 Hz in dB "
        "unter dem höchsten Bin des Fensters (bei Frequenz in Klammern); Hervortreten = stärkste lokale Spitze über 400 Hz minus Mittel der "
        "Mediane der Bins links und rechts im Abstand 40 bis 150 Hz (um 0 bis 10 dB = Rauschen, über 20 dB = stehender Teilton); bei zusammenhang-* nicht gemessen (Ende ist der Treffer, kein Aufladen); "
        "Energie > 400 Hz = Anteil an der Fensterenergie.\n",
        "| Datei | Länge s | Spitze dBFS | Mittel dB | Einsätze 10/30 ms | Ende dBFS | Spitze > 400 Hz dB (Hz) | Hervortreten dB | Energie > 400 Hz dB | Spektrogramm |",
        "|---|---|---|---|---|---|---|---|---|---|---|",
    ]
    dateien = sorted(HIER.glob("*.wav")) + [RUNDE1 / v for v in VERGLEICH]
    for p in dateien:
        x = lies(p)
        mean, peak = vol(p)
        eigen = p.parent == HIER
        png = Path("spektrogramme") / ((p.stem if eigen else "runde1-" + p.stem) + ".png")
        ffmpeg("-y", "-i", str(p), "-lavfi", "showspectrumpic=s=640x320:legend=1:scale=log", str(HIER / png))
        if p.name.startswith("zusammenhang"):
            sp1 = sp2 = "-"
        else:
            hz, pk, herv, anteil = schluss_spektrum(x)
            sp1 = "%.1f (%.0f) | %.1f | %.1f" % (pk, hz, herv, anteil)
            hz, pk, herv, anteil = schluss_spektrum(x, 0.3, 1.0)
            sp2 = "%.1f (%.0f) / %.1f / %.1f" % (pk, hz, herv, anteil)
        if sp1 == "-":
            sp1 = "- | - | -"
        zeilen.append("| %s%s | %.2f | %.1f | %.1f | %d / %d | %.1f | %s | %s | [Bild](%s) |"
                      % (p.name, "" if eigen else " (Runde 1, zum Vergleich)", len(x) / SR, peak, mean,
                         einsaetze(x, 10), einsaetze(x, 30), ende(x), sp1, sp2, png.as_posix()))
    (HIER / "messwerte.md").write_text("\n".join(zeilen) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
