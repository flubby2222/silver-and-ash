#!/usr/bin/env python3
"""Messwerte der Hoerprobe grosses Tor (#549). Gemessen, nicht gehoert.

Aufruf: python messen.py [Ordner [Anschlag-Tick]]   (Standard: dieser Ordner, Tick 78)
Schreibt messwerte.md, je Datei ein Spektrogramm (.png) und wav.sha256.
Braucht nur Python-Standardbibliothek und ffmpeg.
"""
import cmath
import hashlib
import math
import struct
import subprocess
import sys
import wave
from pathlib import Path

SR = 44100
TICK = SR / 60.0
ANSCHLAG_TICK = 78        # Schliessen, Bewegungsentwurf "Anlauf und harter Anschlag"
RUECKPRALLER = (92, 100)  # Ruecksprung-Kontakte
OEFFNEN_PLATEAU = (30, 92)  # Ticks: Anlauf 30, Auslauf ab 132-40


def lade(pfad):
    with wave.open(str(pfad), "rb") as w:
        assert w.getnchannels() == 1 and w.getframerate() == SR and w.getsampwidth() == 2
        d = w.readframes(w.getnframes())
    return [s / 32768.0 for s in struct.unpack("<%dh" % (len(d) // 2), d)]


def db(x):
    return 20 * math.log10(max(x, 1e-9))


def rms_db(x):
    return 10 * math.log10(sum(s * s for s in x) / max(1, len(x)) + 1e-20)


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


def spektrum(x, N=4096):
    """Mittleres Leistungsspektrum (Hann, Schritt N/2) ueber alle Fenster."""
    han = [0.5 - 0.5 * math.cos(2 * math.pi * i / N) for i in range(N)]
    acc = [0.0] * (N // 2)
    for s in range(0, max(1, len(x) - N), N // 2):
        seg = x[s:s + N]
        if len(seg) < N:
            break
        f = fft([seg[i] * han[i] for i in range(N)])
        for k in range(N // 2):
            acc[k] += abs(f[k]) ** 2
    return acc


def kennzahlen(x):
    N = 4096
    p = spektrum(x, N)
    fr = [k * SR / N for k in range(N // 2)]
    tot = sum(p) + 1e-30
    schwerpunkt = sum(f * v for f, v in zip(fr, p)) / tot
    ueber = lambda f0: 10 * math.log10(sum(v for f, v in zip(fr, p) if f >= f0) / tot + 1e-20)
    acc, roll = 0.0, 0.0
    for f, v in zip(fr, p):
        acc += v
        if acc >= 0.95 * tot:
            roll = f
            break
    return schwerpunkt, ueber(1000.0), ueber(3000.0), roll


def einsatz(x, ab, bis, block=32):
    """Einsatz im Fenster [ab, bis): der Block mit dem staerksten Anstieg seines Pegels gegen die
    8 ms davor. Rueckgabe: (Sample, Anstieg dB)."""
    vorn = int(0.008 * SR)
    best, pos = -1e9, ab
    for i in range(max(vorn, ab), min(len(x) - block, bis), block):
        r = rms_db(x[i:i + block]) - rms_db(x[i - vorn:i])
        if r > best:
            best, pos = r, i
    return pos, best


def kurve100(x):
    n = int(0.1 * SR)
    return [rms_db(x[i:i + n]) for i in range(0, len(x) - n + 1, n)]


def ffmpeg_png(wav, png):
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav), "-lavfi",
                    "showspectrumpic=s=900x400:legend=1:scale=log:fscale=log", str(png)], check=True)


def main():
    global ANSCHLAG_TICK
    ordner = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent
    if len(sys.argv) > 2:
        ANSCHLAG_TICK = int(sys.argv[2])
    dateien = sorted(ordner.glob("tor-*.wav"))
    z = []
    z.append("# Messwerte grosses Tor der Bossraum-Tuer (#549)\n")
    z.append("Gemessen, nicht gehoert. Erzeugt von `messen.py`. Zeitbasis: Bewegungsentwurf (60 Ticks/s, 1 Tick = 735 Samples = 16,67 ms).\n")
    z.append("## Pegel und Spektrum\n")
    z.append("| Datei | Dauer s | Spitze dBFS | Mittel (RMS) dBFS | Schwerpunkt Hz | 95 % der Energie unter Hz | Energie > 1 kHz dB | Energie > 3 kHz dB | Letzte 50 ms dBFS |")
    z.append("|---|---|---|---|---|---|---|---|---|")
    kurven = {}
    ereignis = []
    for f in dateien:
        x = lade(f)
        sp, e1, e3, roll = kennzahlen(x)
        spitze = db(max(abs(s) for s in x))
        z.append(f"| {f.name} | {len(x) / SR:.2f} | {spitze:.1f} | {rms_db(x):.1f} | {sp:.0f} | {roll:.0f} | {e1:.1f} | {e3:.1f} | {rms_db(x[-int(0.05 * SR):]):.1f} |")
        kurven[f.name] = kurve100(x)
        imax = max(range(len(x)), key=lambda i: abs(x[i]))
        k = kurven[f.name]
        ereignis.append((f.name, x, imax, k.index(max(k))))
        ffmpeg_png(f, f.with_suffix(".png"))
        if "schliessen" in f.name:
            xm = int(900 * ANSCHLAG_TICK / 60.0 / (len(x) / SR))
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(f), "-lavfi",
                            f"showwavespic=s=900x200:colors=orange,drawbox=x={xm}:y=0:w=2:h=200:color=red@0.9:t=fill,format=rgb24",
                            str(f.with_name(f.stem + "-welle.png"))], check=True)
    z.append("\nSchwerpunkt und Energieanteile aus dem mittleren Spektrum (Hann 4096). Energie > x kHz als dB unter der Gesamtenergie.\n")
    z.append(f"## Schliessen: sitzt der Anschlag bei Tick {ANSCHLAG_TICK} ({ANSCHLAG_TICK / 60:.2f} s)?\n")
    soll = ANSCHLAG_TICK * TICK
    z.append(f"Sollzeit {ANSCHLAG_TICK} Ticks = {soll:.0f} Samples = {soll / SR * 1000:.1f} ms. Einsatz = 32-Sample-Block mit dem staerksten Pegelanstieg gegen die 8 ms davor, im Fenster +-60 ms.\n")
    z.append("| Datei | lautestes Sample bei s | Einsatz bei ms | Abweichung ms | Anstieg dB |")
    z.append("|---|---|---|---|---|")
    for name, x, imax, _ in ereignis:
        if "schliessen" not in name:
            continue
        pos, anst = einsatz(x, int(soll - 0.06 * SR), int(soll + 0.06 * SR))
        z.append(f"| {name} | {imax / SR:.3f} | {pos / SR * 1000:.1f} | {(pos - soll) / SR * 1000:+.1f} | {anst:.1f} |")
    z.append("\nDie zwei kleinen Rueckpraller (Anschlag +14 und +22 Ticks, Pegel 0,20 und 0,06 des Schlags) sind im Nachklang nicht verlaesslich messbar und werden hier nicht belegt; sie sind im Skript als eigene Schlaege gesetzt (`kern_schlag`).\n")
    z.append("## Oeffnen: Verlauf folgt der Bewegung\n")
    z.append("Bewegung: 132 Ticks (2,20 s), Anlauf 30 Ticks (0,50 s), Auslauf ab Tick 92 (1,53 s). Es gibt keinen Anschlag. Gemessen wird der Pegel je 100 ms.\n")
    z.append("| Datei | lautestes 100-ms-Fenster bei s | Pegel Anlauf (0 bis 0,5 s) dB | Pegel Plateau (0,5 bis 1,53 s) dB | Pegel Auslauf (1,53 bis 2,2 s) dB | lautestes Sample bei s |")
    z.append("|---|---|---|---|---|---|")
    for name, x, imax, i100 in ereignis:
        if "oeffnen" not in name:
            continue
        a = rms_db(x[:int(0.5 * SR)])
        b = rms_db(x[int(0.5 * SR):int(OEFFNEN_PLATEAU[1] * TICK)])
        c = rms_db(x[int(OEFFNEN_PLATEAU[1] * TICK):int(132 * TICK)])
        z.append(f"| {name} | {i100 / 10:.1f} | {a:.1f} | {b:.1f} | {c:.1f} | {imax / SR:.3f} |")
    z.append("\n## Pegel je 100 ms (RMS dBFS)\n")
    for name, k in kurven.items():
        z.append(f"- {name}: " + " ".join(f"{v:.0f}" for v in k))
    z.append("\n## Spektrogramme\n")
    for f in dateien:
        z.append(f"- `{f.with_suffix('.png').name}` (Frequenz logarithmisch, 20 Hz bis 22 kHz)")
    (ordner / "messwerte.md").write_text("\n".join(z) + "\n", encoding="utf-8")
    with open(ordner / "wav.sha256", "w", newline="\n") as h:
        for f in dateien:
            h.write(f"{hashlib.sha256(f.read_bytes()).hexdigest()}  {f.name}\n")


if __name__ == "__main__":
    main()
