#!/usr/bin/env python3
"""Misst die WAV-Dateien der Hörprobe Stake Crossbow (#203), Runde 2, schreibt
messwerte.md und je ein Spektrogramm (spektrogramme/). Braucht ffmpeg im PATH.
Aufruf: python messen.py [Ordner mit Runde-1-Dateien der Crossbow zum Vergleich]
Spektralschwerpunkt = Mittel der Frequenzen, gewichtet mit der Energie (FFT über
die ganze Datei); je kleiner, desto dunkler."""
import cmath
import math
import re
import struct
import subprocess
import sys
import wave
from pathlib import Path

HIER = Path(__file__).resolve().parent
BESTAND = HIER.parents[4] / "assets" / "audio"
VERGLEICH = ["pistole.wav", "p1-schuss-1.wav", "p1-schuss-geweiht-1.wav", "p1-nachladen-start.wav",
             "p1-nachladen-bereit.wav", "p1-treffer-1.wav", "axt-treffer-fleisch.wav", "axt-treffer-hart.wav"]
R1 = Path(sys.argv[1]) if len(sys.argv) > 1 else None
SR = 44100


def ffmpeg(*args):
    return subprocess.run(["ffmpeg", "-hide_banner", "-nostats", *args], capture_output=True, text=True).stderr


def vol(datei, filt=None):
    t = ffmpeg("-i", str(datei), "-af", (filt + "," if filt else "") + "volumedetect", "-f", "null", "-")
    return float(re.search(r"mean_volume: (-?[\d.]+)", t).group(1)), float(re.search(r"max_volume: (-?[\d.]+)", t).group(1))


def lies(datei):
    with wave.open(str(datei)) as w:
        k = w.getnframes()
        d = struct.unpack("<%dh" % k, w.readframes(k))
    return [v / 32768.0 for v in d]


def fft(a):
    n = len(a)
    if n == 1:
        return a
    g = fft(a[0::2])
    u = fft(a[1::2])
    out = [0j] * n
    for k in range(n // 2):
        t = cmath.exp(-2j * math.pi * k / n) * u[k]
        out[k] = g[k] + t
        out[k + n // 2] = g[k] - t
    return out


def schwerpunkt(x):
    m = 1
    while m < len(x):
        m *= 2
    f = fft([complex(v, 0) for v in x] + [0j] * (m - len(x)))
    z = sum(abs(f[k]) ** 2 for k in range(1, m // 2))
    return sum(k * SR / m * abs(f[k]) ** 2 for k in range(1, m // 2)) / z


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


def abklang(x):
    pegel = pegelreihe(x, 10)
    top = max(pegel)
    return (max(i for i, p in enumerate(pegel) if p > top - 40) + 1) * 0.01


def ende(x):
    return 20 * math.log10(max(abs(v) for v in x[-2205:]) + 1e-9)


def zeile(p, x, bild_name):
    mean, peak = vol(p)
    hm, _ = vol(p, "highpass=f=3000")
    return ("| %s | %.2f | %.1f | %.1f | %.1f | %d | %d / %d | %.2f | %.1f | %s |"
            % (p.name, len(x) / SR, peak, mean, hm - mean, round(schwerpunkt(x)), einsaetze(x, 10), einsaetze(x, 30),
               abklang(x), ende(x), "[Bild](%s)" % bild_name if bild_name else ""))


def main():
    (HIER / "spektrogramme").mkdir(exist_ok=True)
    kopf = ["| Datei | Länge s | Spitze dBFS | Mittel dB | über 3 kHz dB | Schwerpunkt Hz | Einsätze 10/30 ms | Abklang s | Ende dBFS | Spektrogramm |",
            "|---|---|---|---|---|---|---|---|---|---|"]
    z = ["# Messwerte Hörprobe Stake Crossbow, Runde 2 (#203)\n",
         "Erzeugt von `messen.py`. Alles gemessen, nichts gehört. Spitze = höchster Abtastwert (ffmpeg `volumedetect`); "
         "Mittel = `volumedetect` über die ganze Datei; über 3 kHz = Pegel nach Hochpass 3 kHz minus Gesamtpegel "
         "(je kleiner, desto weniger Höhen); Schwerpunkt = energiegewichtete Mitte des Spektrums; Einsätze = Pegelsprünge "
         "über 8 dB in 50 ms (10- und 30-ms-Fenster; der Beginn zählt nicht mit); Abklang = Zeit bis dauerhaft 40 dB unter "
         "der Spitze; Ende = Spitze der letzten 50 ms (Soll höchstens -50 dBFS).\n"]
    gruppen = [("Schuss", "schuss-*.wav"), ("Treffer Fleisch", "treffer-fleisch-*.wav"), ("Treffer hart", "treffer-hart-*.wav"),
               ("Nachladen (T = 2,5 s)", "nachladen-*.wav"), ("Dehnung des Nachladens (T = 1,5 s und 3,5 s)", "dehnung/nachladen-*.wav"),
               ("Dreierfolgen Fleisch (Abstand 40 ms, je dreimal dieselbe Fassung)", "dreierfolge-fleisch-*.wav")]
    for titel, muster in gruppen:
        z += ["## " + titel + "\n"] + kopf
        for p in sorted(HIER.glob(muster)):
            x = lies(p)
            bild = "spektrogramme/" + p.stem + ".png"
            ffmpeg("-y", "-i", str(p), "-lavfi", "showspectrumpic=s=640x320:legend=1:scale=log", str(HIER / bild))
            z.append(zeile(p, x, bild))
        z.append("")
    z += ["## Vergleich: Bestand im Spiel\n"] + kopf
    for v in VERGLEICH:
        p = BESTAND / v
        z.append(zeile(p, lies(p), ""))
    z.append("")
    if R1 and R1.exists():
        z += ["## Vergleich: Crossbow Runde 1 (Branch `feat/stake-crossbow-hoerprobe-203`, ungehört)\n"] + kopf
        for p in sorted(R1.glob("*.wav")):
            if p.name.startswith(("hoerprobe", "zusammenhang")):
                continue
            z.append(zeile(p, lies(p), ""))
        z.append("")
    (HIER / "messwerte.md").write_text("\n".join(z) + "\n", encoding="utf-8")

    # Dreierfolge über alle 27 Kombinationen (Spitze), aus den Einzeldateien
    teile = [lies(p) for p in sorted(HIER.glob("treffer-fleisch-*.wav"))]
    top = 0.0
    for a in teile:
        for b in teile:
            for c in teile:
                m = [0.0] * (len(c) + 2 * int(SR * 0.04) + 5000)
                for off, s in ((0, a), (int(SR * 0.04), b), (2 * int(SR * 0.04), c)):
                    for i, v in enumerate(s):
                        m[off + i] += v
                top = max(top, max(abs(v) for v in m))
    with open(HIER / "messwerte.md", "a", encoding="utf-8") as f:
        f.write("## Dreierfolge Fleisch, alle 27 Kombinationen der Fassungen\n\nHöchste Spitze: %.2f dBFS (Grenze -1,0 dBFS; Ziel der Skripts -1,5).\n"
                % (20 * math.log10(top)))


if __name__ == "__main__":
    main()
