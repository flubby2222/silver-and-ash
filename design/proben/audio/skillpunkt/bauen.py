"""Hoerprobe Skillpunkt (Issue 372): drei Varianten per Synthese, nur Standardbibliothek.

Aufruf (aus dem Repository-Stamm):  python design/proben/audio/skillpunkt/bauen.py
Schreibt a-glocke.wav, b-aufstieg.wav, c-zupfton.wav neben das Skript und druckt die Messwerte.
Mono, 44 100 Hz, 16 Bit, Spitze auf -3 dBFS, Zufall nur mit festem Seed (zwei Laeufe = gleiche Dateien).
"""
import math
import os
import random
import struct
import wave

SR = 44100
ZIEL_SPITZE_DB = -3.0
HIER = os.path.dirname(os.path.abspath(__file__))


def sek(n):
    return int(n * SR)


def huelle(i, n, attack, release):
    """Lineares Ein- und Ausblenden, damit Anfang und Ende bei Stille liegen."""
    a, r = sek(attack), sek(release)
    g = 1.0
    if i < a:
        g = i / a
    if i > n - r:
        g *= max(0.0, (n - i) / r)
    return g


def glocke():
    """Weicher Glockenton: Grundton 330 Hz plus zwei leise Obertoene, langer exponentieller Ausklang."""
    n = sek(0.9)
    teile = [(330.0, 1.0, 5.0), (495.0, 0.35, 8.0), (660.0, 0.25, 10.0)]  # Frequenz, Anteil, Abklingrate
    out = []
    for i in range(n):
        t = i / SR
        s = sum(a * math.exp(-k * t) * math.sin(2 * math.pi * f * t) for f, a, k in teile)
        out.append(s * huelle(i, n, 0.004, 0.08))
    return out


def aufstieg():
    """Zwei weiche Toene, Quinte aufwaerts (220 Hz, dann 330 Hz), jeder mit leichtem Oberton."""
    noten = [(220.0, 0.0), (330.0, 0.2)]
    n = sek(0.65)
    out = [0.0] * n
    for f, start in noten:
        s0 = sek(start)
        ln = n - s0
        for i in range(ln):
            t = i / SR
            w = math.sin(2 * math.pi * f * t) + 0.3 * math.sin(2 * math.pi * 2 * f * t)
            w *= math.exp(-6.0 * t) * min(1.0, i / sek(0.008))
            out[s0 + i] += w
    return [s * huelle(i, n, 0.002, 0.09) for i, s in enumerate(out)]


def zupfton():
    """Gezupfte Saite (Karplus-Strong) bei etwa 196 Hz, stark gedaempft, also dumpf und kurz."""
    rng = random.Random(372)
    f = 196.0
    n = sek(0.7)
    laenge = int(round(SR / f))
    puffer = [rng.uniform(-1, 1) for _ in range(laenge)]
    # Anfangsrauschen glaetten, damit der Anschlag weich statt knisternd ist
    for _ in range(25):
        puffer = [(puffer[i] + puffer[i - 1]) / 2 for i in range(laenge)]
    out = []
    for i in range(n):
        j = i % laenge
        nxt = (j + 1) % laenge
        neu = 0.994 * 0.5 * (puffer[j] + puffer[nxt])
        out.append(puffer[j])
        puffer[j] = neu
    # Tiefpass (Einpol, ca. 600 Hz) nimmt dem Anschlag die Helligkeit
    a = 1 - math.exp(-2 * math.pi * 600 / SR)
    y = 0.0
    glatt = []
    for x in out:
        y += a * (x - y)
        glatt.append(y)
    return [s * huelle(i, n, 0.001, 0.1) for i, s in enumerate(glatt)]


def normieren(samples):
    spitze = max(abs(s) for s in samples)
    g = 10 ** (ZIEL_SPITZE_DB / 20) / spitze
    return [s * g for s in samples]


def schreiben(pfad, samples):
    with wave.open(pfad, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(round(max(-1, min(1, s)) * 32767))) for s in samples))


def goertzel(samples, f):
    k = 2 * math.cos(2 * math.pi * f / SR)
    s1 = s2 = 0.0
    for x in samples:
        s1, s2 = x + k * s1 - s2, s1
    return s2 * s2 + s1 * s1 - k * s1 * s2


def hauptfrequenz(samples):
    """Staerkste Frequenz (1-Hz-Raster, 80-2000 Hz) im Anfangsstueck bis 0,3 s."""
    teil = samples[: sek(0.3)]
    teil = [x * (0.5 - 0.5 * math.cos(2 * math.pi * i / len(teil))) for i, x in enumerate(teil)]
    return max(range(80, 2001), key=lambda f: goertzel(teil, f))


def anteil_ueber(samples, grenze=2000.0):
    """Anteil der Energie ueber der Grenze (Raster 20 Hz bis 12 kHz), in Prozent."""
    teil = samples[: sek(0.4)]
    gesamt = hoch = 0.0
    for f in range(100, 12001, 20):
        e = goertzel(teil, f)
        gesamt += e
        if f > grenze:
            hoch += e
    return 100.0 * hoch / gesamt


def main():
    varianten = [("a-glocke.wav", glocke), ("b-aufstieg.wav", aufstieg), ("c-zupfton.wav", zupfton)]
    print("| Datei | Dauer (s) | Spitze (dBFS) | Hauptfrequenz (Hz) | Energie >2 kHz (%) | erstes/letztes Sample |")
    print("|---|---|---|---|---|---|")
    for name, fn in varianten:
        s = normieren(fn())
        schreiben(os.path.join(HIER, name), s)
        spitze = 20 * math.log10(max(abs(x) for x in s))
        print(
            f"| {name} | {len(s) / SR:.2f} | {spitze:.1f} | {hauptfrequenz(s)} | "
            f"{anteil_ueber(s):.2f} | {s[0]:.5f} / {s[-1]:.5f} |"
        )


if __name__ == "__main__":
    main()
