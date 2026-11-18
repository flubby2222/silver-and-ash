"""Hoerprobe Skillpunkt, Runde 2 (Issue 372): drei Richtungen per Synthese, nur Standardbibliothek.

Aufruf (aus dem Repository-Stamm):  python design/proben/audio/skillpunkt/runde2/bauen.py
Schreibt 1-turmglocke.wav, 2-klinge.wav, 3-schlag-chor.wav neben das Skript und druckt die Messwerte.
Mono, 44 100 Hz, 16 Bit, Spitze -3 dBFS, Zufall nur mit festem Seed (zwei Laeufe = gleiche Dateien).
"""
import math
import os
import random
import struct
import wave

SR = 44100
ZIEL_SPITZE_DB = -3.0
HIER = os.path.dirname(os.path.abspath(__file__))
TAU = 2 * math.pi


def sek(n):
    return int(n * SR)


def huelle(i, n, attack, release):
    a, r = sek(attack), sek(release)
    g = 1.0
    if i < a:
        g = i / a
    if i > n - r:
        g *= max(0.0, (n - i) / r)
    return g


def hall(samples, nachlauf=0.35, anteil=0.28):
    """Kurzer, dunkler Hall: vier Kammfilter mit Tiefpass in der Rueckkopplung, Rest leise auslaufend."""
    n = len(samples) + sek(nachlauf)
    x = samples + [0.0] * (n - len(samples))
    summe = [0.0] * n
    for ms, fb in ((29.7, 0.55), (37.1, 0.5), (41.1, 0.5), (53.3, 0.45)):
        d = sek(ms / 1000)
        y = [0.0] * n
        tp = 0.0
        for i in range(n):
            rueck = y[i - d] if i >= d else 0.0
            tp += 0.35 * (rueck - tp)  # dunkler werdende Wiederholungen
            y[i] = x[i] + fb * tp
        for i in range(n):
            summe[i] += y[i]
    return [x[i] + anteil * summe[i] / 4 for i in range(n)]


def turmglocke():
    """Tiefe Glocke: Schlagton 110 Hz, inharmonische Teiltoene, kurzer dumpfer Anschlag, kurzer Hall."""
    n = sek(0.55)
    teile = [(55.0, 0.5, 9.0), (110.0, 1.0, 9.0), (131.0, 0.55, 11.0), (165.0, 0.45, 13.0), (220.0, 0.3, 16.0), (330.0, 0.15, 22.0)]
    out = []
    for i in range(n):
        t = i / SR
        s = sum(a * math.exp(-k * t) * math.sin(TAU * f * t) for f, a, k in teile)
        # dumpfer Anschlag: 70-Hz-Schlag, sehr kurz
        s += 0.9 * math.exp(-45 * t) * math.sin(TAU * 70 * t)
        out.append(s * huelle(i, n, 0.002, 0.1))
    return hall(out)


def klinge():
    """Silbernes Klingen: wenige unharmonische Teiltoene (600-1700 Hz), schneller Abfall, etwas gefiltertes Rauschen."""
    rng = random.Random(372)
    n = sek(0.45)
    teile = [(620.0, 1.0, 13.0), (1010.0, 0.8, 17.0), (1670.0, 0.45, 24.0)]
    a = 1 - math.exp(-TAU * 2500 / SR)  # Rauschen bei hoechstens ~2,5 kHz
    tp = 0.0
    out = []
    for i in range(n):
        t = i / SR
        s = sum(am * math.exp(-k * t) * math.sin(TAU * f * t) for f, am, k in teile)
        tp += a * (rng.uniform(-1, 1) - tp)
        s += 0.5 * math.exp(-60 * t) * tp  # Anschlag
        out.append(s * huelle(i, n, 0.0008, 0.08))
    return hall(out, 0.2, 0.15)


def schlag_chor(laenge=0.6, chor_abfall=4.5, release=0.12, nachlauf=0.25):
    """Tiefer Schlag (Sinus 90 -> 45 Hz) und dunkler Stimmen-Akkord a-Moll (110/131/165/220 Hz) mit 'o'-Klangfarbe."""
    n = sek(laenge)
    akkord = [(110.0, 1.0), (131.0, 0.8), (165.0, 0.7), (220.0, 0.5)]
    # 'o'-Formant: starke Teiltoene um 400-700 Hz, hoehere schnell schwaecher
    out = []
    phase = 0.0
    for i in range(n):
        t = i / SR
        f_schlag = 45 + 45 * math.exp(-25 * t)
        phase += TAU * f_schlag / SR
        s = 1.2 * math.exp(-14 * t) * math.sin(phase)
        chor = 0.0
        for f, am in akkord:
            vib = 1 + 0.004 * math.sin(TAU * 5.5 * t + f)
            for h in range(1, 9):
                fh = f * h * vib
                formant = math.exp(-((fh - 500) / 300) ** 2)
                chor += am * formant / h * math.sin(TAU * fh * t + f * 0.1)
        ein = min(1.0, t / 0.04)  # Chor setzt weich knapp nach dem Schlag ein
        s += 0.55 * chor * ein * math.exp(-chor_abfall * t)
        out.append(s * huelle(i, n, 0.002, release))
    return hall(out, nachlauf, 0.2)


def schlag_chor_final():
    """Variante 3 mit laengerem Ausklang: Schlag und Einsatz unveraendert, Akkord faellt langsamer ab (3,2 statt 4,5 je s)."""
    return schlag_chor(laenge=1.0, chor_abfall=3.2, release=0.3, nachlauf=0.4)


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
    k = 2 * math.cos(TAU * f / SR)
    s1 = s2 = 0.0
    for x in samples:
        s1, s2 = x + k * s1 - s2, s1
    return s2 * s2 + s1 * s1 - k * s1 * s2


def hauptfrequenz(samples):
    teil = samples[: sek(0.3)]
    teil = [x * (0.5 - 0.5 * math.cos(TAU * i / len(teil))) for i, x in enumerate(teil)]
    return max(range(40, 2001), key=lambda f: goertzel(teil, f))


def anteil_ueber(samples, grenze):
    teil = samples[: sek(0.4)]
    gesamt = hoch = 0.0
    for f in range(40, 12001, 20):
        e = goertzel(teil, f)
        gesamt += e
        if f > grenze:
            hoch += e
    return 100.0 * hoch / gesamt


def dauer_bis(samples, db):
    """Letzter Zeitpunkt (s), an dem der RMS-Pegel (10-ms-Fenster) noch hoeher als Spitzenfenster minus db liegt."""
    w = sek(0.01)
    pegel = []
    for i in range(0, len(samples) - w, w):
        pegel.append(math.sqrt(sum(x * x for x in samples[i : i + w]) / w))
    m = max(pegel)
    letzte = max(j for j, p in enumerate(pegel) if p > m * 10 ** (-db / 20))
    return (letzte + 1) * 0.01


def main():
    varianten = [("1-turmglocke.wav", turmglocke), ("2-klinge.wav", klinge), ("3-schlag-chor.wav", schlag_chor), ("3-schlag-chor-final.wav", schlag_chor_final)]
    print("| Datei | Gesamt (s) | Hauptteil, 20 dB (s) | Nachklang, 40 dB (s) | Spitze (dBFS) | Hauptfrequenz (Hz) | Energie >2 kHz (%) | Energie >4 kHz (%) | erstes/letztes Sample |")
    print("|---|---|---|---|---|---|---|---|---|")
    for name, fn in varianten:
        s = normieren(fn())
        schreiben(os.path.join(HIER, name), s)
        spitze = 20 * math.log10(max(abs(x) for x in s))
        print(
            f"| {name} | {len(s) / SR:.2f} | {dauer_bis(s, 20):.2f} | {dauer_bis(s, 40):.2f} | {spitze:.1f} | {hauptfrequenz(s)} | "
            f"{anteil_ueber(s, 2000):.2f} | {anteil_ueber(s, 4000):.2f} | {s[0]:.5f} / {s[-1]:.5f} |"
        )


if __name__ == "__main__":
    main()
