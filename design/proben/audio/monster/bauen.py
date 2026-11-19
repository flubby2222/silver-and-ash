"""Hoerprobe Monsterklaenge (Issue 392): Ghul, Fledermaus, Gorm in je zwei neuen Fassungen, nur Standardbibliothek.

Aufruf (aus dem Repository-Stamm):
    python design/proben/audio/monster/bauen.py             # WAV + messwerte.md + Spektrogramme (ffmpeg)
    python design/proben/audio/monster/bauen.py --nur-runde2 # nur Kreischen c/d bauen, Messwerte und Pruefsummen neu
    python design/proben/audio/monster/bauen.py --pruefen   # Temp-Ordner neu bauen, SHA-256 gegen wav.sha256 vergleichen

Aufbau: Stimme = gehuepfter Impulszug (Hann-Impuls) mit Tonhoehen-Zittern (jitter), Pegel-Zittern (shimmer) und
optionalem Knarren (jede zweite Periode schwaecher), durch drei parallele Formantfilter geschickt; dazu Atem
(Rauschen durch dieselben Formanten). Reine Rauschklaenge: Rauschen durch Bandpass/Tiefpass mit gleitender Mitte.
Hüllkurven stückweise linear, mit langsamer Zufallsschwankung. Jeder Klang hat einen festen Seed.
Die Lautheit (RMS) jedes Klangs wird an die alte Datei in assets/audio angeglichen (nur gelesen), die Spitze
bleibt bei hoechstens -1,5 dBFS.
"""
import hashlib
import math
import os
import random
import struct
import subprocess
import sys
import tempfile
import wave

SR = 44100
TAU = 2 * math.pi
HIER = os.path.dirname(os.path.abspath(__file__))
WURZEL = os.path.abspath(os.path.join(HIER, "..", "..", "..", ".."))
ALT = os.path.join(WURZEL, "assets", "audio")
SPITZE_MAX_DB = -1.5


def sek(s):
    return int(round(s * SR))


# ---------------------------------------------------------------- Bausteine
def _coef(kind, f, q):
    f = min(max(f, 20.0), SR * 0.45)
    w = TAU * f / SR
    c = math.cos(w)
    a = math.sin(w) / (2 * q)
    if kind == "bp":
        b0, b1, b2 = a, 0.0, -a
    elif kind == "lp":
        b0 = (1 - c) / 2
        b1 = 1 - c
        b2 = b0
    else:  # hp
        b0 = (1 + c) / 2
        b1 = -(1 + c)
        b2 = b0
    a0 = 1 + a
    return b0 / a0, b1 / a0, b2 / a0, -2 * c / a0, (1 - a) / a0


def filt(x, kind, f, q=0.707, block=32):
    """Biquad; f darf eine Zahl oder eine Funktion der Zeit (s) sein (Koeffizienten je 32 Samples)."""
    n = len(x)
    out = [0.0] * n
    x1 = x2 = y1 = y2 = 0.0
    zeitabh = callable(f)
    if not zeitabh:
        b0, b1, b2, a1, a2 = _coef(kind, f, q)
    for start in range(0, n, block):
        if zeitabh:
            b0, b1, b2, a1, a2 = _coef(kind, f(start / SR), q)
        for i in range(start, min(start + block, n)):
            xi = x[i]
            y = b0 * xi + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
            x2, x1 = x1, xi
            y2, y1 = y1, y
            out[i] = y
    return out


def rauschen(n, rng):
    return [rng.uniform(-1.0, 1.0) for _ in range(n)]


def rms(x):
    return math.sqrt(sum(v * v for v in x) / max(1, len(x)))


def norm(x, ziel=1.0):
    r = rms(x)
    return [v * ziel / r for v in x] if r > 0 else x


def huelle(n, pts, form=1.0):
    """Stueckweise linear durch (Zeit s, Wert); form > 1 kruemmt jedes Stueck (langsamer Start)."""
    out = [0.0] * n
    for i in range(n):
        t = i / SR
        if t <= pts[0][0]:
            out[i] = pts[0][1]
            continue
        v = pts[-1][1]
        for (t0, v0), (t1, v1) in zip(pts, pts[1:]):
            if t <= t1:
                fr = (t - t0) / (t1 - t0) if t1 > t0 else 1.0
                v = v0 + (v1 - v0) * (fr ** form)
                break
        out[i] = v
    return out


def schwanken(n, rng, rate, tiefe):
    """Langsame Zufallsschwankung um 1 (Kosinus-Interpolation zwischen Zufallswerten)."""
    schritt = max(1, int(SR / rate))
    knoten = [rng.uniform(-1.0, 1.0) for _ in range(n // schritt + 3)]
    out = []
    for i in range(n):
        k, fr = divmod(i, schritt)
        fr /= schritt
        m = (1 - math.cos(math.pi * fr)) / 2
        out.append(1 + tiefe * (knoten[k] * (1 - m) + knoten[k + 1] * m))
    return out


def mal(*listen):
    return [math.prod(v) for v in zip(*listen)]


def lin(t, a, b, dauer):
    return a + (b - a) * min(1.0, max(0.0, t / dauer))


def stimme(n, rng, f0, vokal, q=(5.0, 7.0, 9.0), atem=0.4, jitter=0.03, shimmer=0.15, knarren=0.0):
    """Stimme + Atem durch drei Formantfilter. f0(t) in Hz, vokal(t) -> (f1, f2, f3)."""
    quelle = []
    ph = 0.0
    periode = 0
    jit = 1.0
    amp = 1.0
    for i in range(n):
        t = i / SR
        ph += f0(t) * jit / SR
        if ph >= 1.0:
            ph -= 1.0
            periode += 1
            jit = 1 + jitter * rng.gauss(0, 1)
            amp = max(0.2, 1 + shimmer * rng.gauss(0, 1))
            if knarren and periode % 2:
                amp *= 1 - knarren
        quelle.append(amp * (math.sin(math.pi * ph / 0.6) ** 2 if ph < 0.6 else 0.0))
    quelle = [quelle[i] - quelle[i - 1] for i in range(n)]  # Abstrahlung: Ableitung
    luft = rauschen(n, rng)
    gewichte = (1.0, 0.7, 0.4)
    stim = [0.0] * n
    hauch = [0.0] * n
    for k in range(3):
        fk = (lambda t, k=k: vokal(t)[k])
        a = filt(quelle, "bp", fk, q[k])
        b = filt(luft, "bp", fk, q[k])
        for i in range(n):
            stim[i] += gewichte[k] * a[i]
            hauch[i] += gewichte[k] * b[i]
    stim = norm(stim)
    hauch = norm(hauch)
    return [s + atem * h for s, h in zip(stim, hauch)]


def saettigen(x, grad):
    return [math.tanh(grad * v) / math.tanh(grad) for v in x]


def fertig(x):
    """Hochpass 25 Hz gegen Gleichanteil, 2 ms Einblenden, 6 ms Ausblenden (Stille an Anfang und Ende)."""
    x = filt(x, "hp", 25.0, 0.707)
    n = len(x)
    e, a = sek(0.002), sek(0.006)
    for i in range(min(e, n)):
        x[i] *= i / e
    for i in range(min(a, n)):
        x[n - 1 - i] *= i / a
    x[0] = x[-1] = 0.0
    return x


# RMS der alten Dateien (Stand origin/main vor Issue 392). Fest eingetragen, weil assets/audio nach dem Einbau
# die neuen Dateien enthaelt und ein zweiter Lauf sonst eine andere Ziellautheit sieht.
ALT_RMS = {
    "p1-fledermaus-biss.wav": 0.03811547133285714,
    "p1-fledermaus-fehlsturz.wav": 0.04001653299070042,
    "p1-fledermaus-flattern-1.wav": 0.028153790644584915,
    "p1-fledermaus-kreischen.wav": 0.38449668251857105,
    "p1-fledermaus-spuck-ankuendigung.wav": 0.06648546060006182,
    "p1-fledermaus-spucken.wav": 0.03579009022936983,
    "p1-fledermaus-sterben.wav": 0.15944708096164378,
    "p1-fledermaus-treffer.wav": 0.03583096274719661,
    "p1-ghul-ankuendigung.wav": 0.09738755900480069,
    "p1-ghul-entdecken.wav": 0.101857419127374,
    "p1-ghul-sterben.wav": 0.08482610551690303,
    "p1-gorm-ankuendigung-hammer.wav": 0.22281638552651406,
    "p1-gorm-bruellen.wav": 0.1511242467137007,
    "p1-gorm-sterben.wav": 0.13514257221088827,
    "p1-gorm-treffer-1.wav": 0.05033925386962173,
}


def lautheit_alt(name):
    if name in ALT_RMS:
        return ALT_RMS[name]
    with wave.open(os.path.join(ALT, name), "rb") as w:
        roh = w.readframes(w.getnframes())
    werte = struct.unpack("<%dh" % (len(roh) // 2), roh)
    return math.sqrt(sum(v * v for v in werte) / len(werte)) / 32768.0


def anpassen_paar(xa, xb, alt):
    """Beide Fassungen auf dieselbe RMS-Lautheit: die der alten Datei, oder die kleinere, die beide ohne Ueberschreiten
    der Spitzengrenze erreichen."""
    grenze = 10 ** (SPITZE_MAX_DB / 20)
    ziel = lautheit_alt(alt)
    for x in (xa, xb):
        ziel = min(ziel, rms(x) * grenze / max(abs(v) for v in x))
    return [[v * ziel / rms(x) for v in x] for x in (xa, xb)]


def schreibe(pfad, x):
    daten = struct.pack("<%dh" % len(x), *[max(-32767, min(32767, int(round(v * 32767)))) for v in x])
    with wave.open(pfad, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(daten)


def lies(pfad):
    with wave.open(pfad, "rb") as w:
        roh = w.readframes(w.getnframes())
    return [v / 32768.0 for v in struct.unpack("<%dh" % (len(roh) // 2), roh)]


# ---------------------------------------------------------------- Ghul
def ghul_ankuendigung_a(seed=3921):
    """Knurren: knarrende Stimme 60 -> 70 Hz, Vokal u -> o, Atem, ueber 1 s anschwellend, am Gipfel Ende."""
    rng = random.Random(seed)
    n = sek(1.0)
    v = stimme(n, rng, lambda t: 60 + 10 * t, lambda t: (350 + 150 * t, 700 + 300 * t, 2400),
               q=(6, 8, 10), atem=0.5, jitter=0.04, shimmer=0.2, knarren=0.5)
    h = mal(huelle(n, [(0, 0.03), (0.99, 1.0), (1.0, 0)], 2.2), schwanken(n, rng, 6, 0.2))
    return mal(v, h)


def ghul_ankuendigung_b(seed=3922):
    """Atem: stimmloses, rasselndes Einatmen; Rauschen mit steigender Mitte 350 -> 1100 Hz, anschwellend."""
    rng = random.Random(seed)
    n = sek(1.0)
    r = rauschen(n, rng)
    a = filt(r, "bp", lambda t: 350 + 750 * t, 2.5)
    b = filt(r, "bp", 2200, 3.0)
    x = [p + 0.35 * q for p, q in zip(norm(a), norm(b))]
    h = mal(huelle(n, [(0, 0.03), (0.99, 1.0), (1.0, 0)], 2.0), schwanken(n, rng, 14, 0.3))
    return mal(x, h)


def ghul_entdecken_a(seed=3923):
    """Stoehnen: Vokal u -> a -> o, Tonhoehe wie ein Bogen 75 -> 105 -> 70 Hz, knarrend."""
    rng = random.Random(seed)
    n = sek(0.9)

    def f0(t):
        return 75 + 30 * math.sin(math.pi * min(1.0, t / 0.8))

    def vok(t):
        a = math.sin(math.pi * min(1.0, t / 0.8))
        return (330 + 420 * a, 800 + 400 * a, 2400)

    v = stimme(n, rng, f0, vok, q=(6, 8, 10), atem=0.35, jitter=0.04, shimmer=0.2, knarren=0.4)
    h = mal(huelle(n, [(0, 0), (0.08, 0.7), (0.3, 1), (0.6, 0.8), (0.85, 0.15), (0.9, 0)]), schwanken(n, rng, 5, 0.3))
    return mal(v, h)


def ghul_entdecken_b(seed=3924):
    """Gurgeln: Tiefpass-Rauschen mit unregelmaessigem Nass-Flattern (~28 Hz), darunter leise Stimme 55 Hz."""
    rng = random.Random(seed)
    n = sek(0.9)
    luft = filt(rauschen(n, rng), "lp", 900, 0.8)
    lfo = filt(rauschen(n, rng), "lp", 28, 0.7)
    lfo = [0.5 + 0.5 * max(-1.0, min(1.0, 2.2 * v / rms(lfo))) for v in lfo]
    gurgel = norm(mal(luft, lfo))
    v = stimme(n, rng, lambda t: 55, lambda t: (400, 800, 2300), q=(5, 7, 9), atem=0.2, jitter=0.05, knarren=0.5)
    x = [g + 0.3 * s for g, s in zip(gurgel, norm(v))]
    h = huelle(n, [(0, 0), (0.1, 0.8), (0.35, 1), (0.7, 0.5), (0.9, 0)])
    return mal(x, h)


def ghul_sterben_a(seed=3925):
    """Letzter Atemzug mit Stimme: Tonhoehe faellt 100 -> 40 Hz, Vokal a -> o, Atem nimmt zu."""
    rng = random.Random(seed)
    n = sek(1.3)
    v = stimme(n, rng, lambda t: 100 * math.exp(-0.7 * t / 1.3 * 1.6), lambda t: (700 - 300 * min(1, t / 1.2), 1100 - 300 * min(1, t / 1.2), 2400),
               q=(5, 7, 9), atem=0.6, jitter=0.05, shimmer=0.25, knarren=0.4)
    h = mal(huelle(n, [(0, 0), (0.05, 1), (0.25, 0.8), (0.5, 0.45), (0.9, 0.15), (1.3, 0)]), schwanken(n, rng, 7, 0.4))
    return mal(v, h)


def ghul_sterben_b(seed=3926):
    """Rasselndes Ausatmen: Rauschen mit fallender Mitte 1100 -> 250 Hz, rauhe Schwankung (~22 Hz)."""
    rng = random.Random(seed)
    n = sek(1.3)
    x = norm(filt(rauschen(n, rng), "bp", lambda t: 1100 * math.exp(-1.48 * t / 1.3), 1.2))
    h = mal(huelle(n, [(0, 0), (0.06, 1), (0.4, 0.7), (1.3, 0)]), schwanken(n, rng, 22, 0.6))
    return mal(x, h)


# ---------------------------------------------------------------- Fledermaus
def fl_spuck_ank_a(seed=3931):
    """Anschwellendes Zischen: Rauschen, Mitte 1000 -> 2400 Hz, Pegel steigt bis zum Ende."""
    rng = random.Random(seed)
    n = sek(0.7)
    x = norm(filt(filt(rauschen(n, rng), "hp", 800, 0.7), "bp", lambda t: 1000 + 2000 * t, 1.5))
    return mal(x, huelle(n, [(0, 0.03), (0.69, 1.0), (0.7, 0)], 2.5), schwanken(n, rng, 9, 0.2))


def fl_spuck_ank_b(seed=3932):
    """Gurgeln: Tiefpass-Rauschen mit Nass-Flattern (~20 Hz, flach), anschwellend, dazu leises Band um 600 Hz."""
    rng = random.Random(seed)
    n = sek(0.7)
    luft = rauschen(n, rng)
    lfo = filt(rauschen(n, rng), "lp", 20, 0.7)
    lfo = [0.7 + 0.3 * max(-1.0, min(1.0, 2.0 * v / rms(lfo))) for v in lfo]
    g = norm(mal(filt(luft, "lp", 1200, 0.8), lfo))
    b = norm(filt(luft, "bp", 600, 4.0))
    x = [p + 0.3 * q for p, q in zip(g, b)]
    return mal(x, huelle(n, [(0, 0.03), (0.69, 1.0), (0.7, 0)], 2.0))


def fl_spucken_a(seed=3933):
    """Fauchen: kurzer Rauschstoss (Band um 2200 Hz) plus ein feuchtes Klicken am Anfang."""
    rng = random.Random(seed)
    n = sek(0.3)
    r = rauschen(n, rng)
    z = norm(filt(filt(r, "hp", 1000, 0.7), "bp", 2200, 0.9))
    k = norm(filt(r, "bp", 900, 3.0))
    hz = [math.exp(-t / 0.07) * min(1.0, t / 0.004) for t in (i / SR for i in range(n))]
    hk = [math.exp(-(i / SR) / 0.012) for i in range(n)]
    return [a * p + 0.8 * b * q for a, p, b, q in zip(hz, z, hk, k)]


def fl_spucken_b(seed=3934):
    """Ausspucken 'ptuh': kurzer dumpfer Schlag (Tiefpass-Rauschen plus fallende Sinuswelle 220 -> 90 Hz), dann leises Zischen."""
    rng = random.Random(seed)
    n = sek(0.2)
    r = rauschen(n, rng)
    d = norm(filt(r, "lp", 1500, 0.8))
    ph = 0.0
    s = []
    for i in range(n):
        t = i / SR
        ph += TAU * (90 + 130 * math.exp(-t / 0.02)) / SR
        s.append(math.sin(ph))
    z = norm(filt(r, "bp", 2200, 1.0))
    hd = [math.exp(-(i / SR) / 0.025) for i in range(n)]
    hz = [0.25 * math.exp(-(i / SR) / 0.08) * min(1.0, (i / SR) / 0.015) for i in range(n)]
    return [a * (p + 0.8 * q) + b * w for a, p, q, b, w in zip(hd, d, s, hz, z)]


def fl_kreischen_a(seed=3935):
    """Fauchen vor dem Sturzflug: Rauschband 1800 -> 2600 Hz plus raue tiefe Stimme (160 -> 240 Hz) darunter."""
    rng = random.Random(seed)
    n = sek(0.55)
    z = norm(filt(rauschen(n, rng), "bp", lambda t: 1800 + 1450 * t, 1.2))
    v = stimme(n, rng, lambda t: 160 + 145 * t, lambda t: (600, 1500, 2600), q=(4, 6, 8), atem=0.8, jitter=0.06,
               shimmer=0.25, knarren=0.3)
    x = [a + 0.35 * b for a, b in zip(z, norm(v))]
    return mal(x, huelle(n, [(0, 0.03), (0.54, 1.0), (0.55, 0)], 2.0))


def fl_kreischen_b(seed=3936):
    """Heiseres Kraechzen: nur Stimme 140 -> 210 Hz, starkes Zittern, Atem, anschwellend; keine hohe Lage."""
    rng = random.Random(seed)
    n = sek(0.55)
    v = stimme(n, rng, lambda t: 140 + 130 * t, lambda t: (500, 1300, 2500), q=(5, 7, 9), atem=0.5, jitter=0.08,
               shimmer=0.3)
    return mal(v, huelle(n, [(0, 0.03), (0.54, 1.0), (0.55, 0)], 1.8), schwanken(n, rng, 14, 0.3))


def fl_flattern_a(seed=3937):
    """Flügelschlag, zwei weiche Wellen aus Rauschen 250 -> 1400 Hz."""
    rng = random.Random(seed)
    n = sek(0.22)
    x = norm(filt(filt(rauschen(n, rng), "hp", 250, 0.7), "lp", 1400, 0.7))
    return mal(x, huelle(n, [(0, 0), (0.03, 0.7), (0.07, 0.25), (0.11, 1), (0.18, 0.2), (0.22, 0)]))


def fl_flattern_b(seed=3938):
    """Flügelschlag, ein einziges dumpfes Wuschen: Rauschen um 350 Hz."""
    rng = random.Random(seed)
    n = sek(0.22)
    x = norm(filt(rauschen(n, rng), "bp", 350, 1.0))
    return mal(x, huelle(n, [(0, 0), (0.08, 1), (0.22, 0)]))


def fl_biss_a(seed=3939):
    """Feuchtes Schnappen: kurzes Rauschklicken um 1400 Hz plus dumpfer Schlag 140 Hz."""
    rng = random.Random(seed)
    n = sek(0.25)
    k = norm(filt(rauschen(n, rng), "bp", 1400, 2.0))
    d = [math.sin(TAU * 140 * i / SR) for i in range(n)]
    hk = [math.exp(-(i / SR) / 0.008) for i in range(n)]
    hd = [math.exp(-(i / SR) / 0.04) for i in range(n)]
    return [a * p + 0.6 * b * q for a, p, b, q in zip(hk, k, hd, d)]


def fl_biss_b(seed=3940):
    """Schnappen mit Knurrlaut: Tiefpass-Rauschstoss (60 ms) plus kurzer Stimmlaut 180 Hz mit Atem."""
    rng = random.Random(seed)
    n = sek(0.25)
    s = norm(filt(rauschen(n, rng), "lp", 2500, 0.8))
    v = norm(stimme(n, rng, lambda t: 180, lambda t: (600, 1200, 2500), atem=0.8, jitter=0.06))
    hs = [math.exp(-(i / SR) / 0.02) * min(1.0, (i / SR) / 0.002) for i in range(n)]
    hv = huelle(n, [(0, 0), (0.02, 0.7), (0.12, 0.4), (0.25, 0)])
    return [a * p + 0.5 * b * q for a, p, b, q in zip(hs, s, hv, v)]


def fl_treffer_a(seed=3941):
    """Kurzes Fauchen 'hsst': Rauschband 2400 Hz, darunter schwaches raues Stimmchen (300 -> 220 Hz)."""
    rng = random.Random(seed)
    n = sek(0.3)
    z = norm(filt(rauschen(n, rng), "bp", 2400, 1.5))
    v = norm(stimme(n, rng, lambda t: 300 - 80 * t / 0.3, lambda t: (650, 1600, 2600), atem=0.7, jitter=0.06))
    x = [a + 0.3 * b for a, b in zip(z, v)]
    return mal(x, huelle(n, [(0, 0), (0.01, 1), (0.1, 0.5), (0.3, 0)]))


def fl_treffer_b(seed=3942):
    """Kurzer tiefer Rauhlaut: Stimme 200 -> 150 Hz mit viel Atem."""
    rng = random.Random(seed)
    n = sek(0.3)
    v = stimme(n, rng, lambda t: 200 - 50 * t / 0.3, lambda t: (550, 1300, 2500), atem=0.7, jitter=0.07, shimmer=0.25)
    return mal(v, huelle(n, [(0, 0), (0.015, 1), (0.12, 0.5), (0.3, 0)]))


def fl_sterben_a(seed=3943):
    """Schwacher Stimmlaut: Tonhoehe faellt 380 -> 160 Hz, viel Atem, unregelmaessig."""
    rng = random.Random(seed)
    n = sek(0.7)
    v = stimme(n, rng, lambda t: 380 * math.exp(-1.2 * t / 0.7 * 0.8), lambda t: (700, 1700, 2700), q=(4, 6, 8),
               atem=0.9, jitter=0.05, shimmer=0.25)
    return mal(v, huelle(n, [(0, 0), (0.04, 1), (0.3, 0.6), (0.7, 0)]), schwanken(n, rng, 9, 0.35))


def fl_sterben_b(seed=3944):
    """Ausatmendes Zischen: Rauschband faellt 3000 -> 900 Hz, darunter leise raue Stimme 150 Hz."""
    rng = random.Random(seed)
    n = sek(0.7)
    z = norm(filt(rauschen(n, rng), "bp", lambda t: 3000 * math.exp(-1.7 * t / 0.7), 1.2))
    v = norm(stimme(n, rng, lambda t: 150, lambda t: (550, 1200, 2500), atem=0.6, jitter=0.07))
    x = [a + 0.3 * b for a, b in zip(z, v)]
    return mal(x, huelle(n, [(0, 0), (0.04, 1), (0.35, 0.5), (0.7, 0)]))


def fl_fehlsturz_a(seed=3945):
    """Luftzug: Rauschen, Mitte faellt 1600 -> 500 Hz, ein Bogen."""
    rng = random.Random(seed)
    n = sek(0.45)
    x = norm(filt(rauschen(n, rng), "bp", lambda t: 1600 * math.exp(-1.16 * t / 0.45), 1.2))
    return mal(x, huelle(n, [(0, 0), (0.18, 1), (0.45, 0)]))


def fl_fehlsturz_b(seed=3946):
    """Dumpfer Luftzug: Tiefpass-Rauschen, Grenze 1100 -> 300 Hz, breiter Bogen mit leichter Schwankung."""
    rng = random.Random(seed)
    n = sek(0.45)
    x = norm(filt(rauschen(n, rng), "lp", lambda t: 1100 * math.exp(-1.3 * t / 0.45), 0.8))
    return mal(x, huelle(n, [(0, 0), (0.15, 0.8), (0.28, 1), (0.45, 0)]), schwanken(n, rng, 10, 0.2))


# ---------------------------------------------------------------- Gorm
def gorm_bruellen_a(seed=3951):
    """Bruellen: Stimme 50 -> 68 -> 48 Hz, weite Formanten (a), Atem, leicht saettigend."""
    rng = random.Random(seed)
    n = sek(1.5)

    def f0(t):
        return 50 + 18 * math.sin(math.pi * min(1.0, t / 1.4)) ** 1.5

    v = stimme(n, rng, f0, lambda t: (600 + 100 * min(1, t / 0.5), 1000 + 100 * min(1, t / 0.5), 2400),
               q=(3, 4, 6), atem=0.45, jitter=0.05, shimmer=0.3)
    h = mal(huelle(n, [(0, 0), (0.12, 1), (0.9, 0.85), (1.3, 0.35), (1.5, 0)]), schwanken(n, rng, 4, 0.25))
    return saettigen(mal(v, h), 2.0)


def gorm_bruellen_b(seed=3952):
    """Knurren: Stimme 38 -> 45 -> 36 Hz, Knarren, Vokal o, rasselnde Pegelschwankung ~22 Hz, saettigend."""
    rng = random.Random(seed)
    n = sek(1.5)
    v = stimme(n, rng, lambda t: 36 + 9 * math.sin(math.pi * min(1.0, t / 1.4)), lambda t: (450, 800, 2300),
               q=(3, 4, 6), atem=0.3, jitter=0.05, shimmer=0.3, knarren=0.55)
    am = [1 - 0.5 * (0.5 + 0.5 * math.sin(TAU * 22 * i / SR)) for i in range(n)]
    h = mal(huelle(n, [(0, 0), (0.15, 1), (0.9, 0.85), (1.3, 0.35), (1.5, 0)]), schwanken(n, rng, 4, 0.25))
    return saettigen(mal(v, h, am), 2.0)


def gorm_treffer_a(seed=3953):
    """Grunzen 'hnnf': kurze Stimme 70 -> 50 Hz, Vokal o, Atem."""
    rng = random.Random(seed)
    n = sek(0.35)
    v = stimme(n, rng, lambda t: 70 - 20 * t / 0.35, lambda t: (500, 900, 2300), q=(3, 4, 6), atem=0.5, jitter=0.05)
    return mal(v, huelle(n, [(0, 0), (0.03, 1), (0.15, 0.5), (0.35, 0)]))


def gorm_treffer_b(seed=3954):
    """Ausstossen 'haah': Tiefpass-Rauschen (Grenze 1000 -> 300 Hz) plus leise Stimme 55 Hz."""
    rng = random.Random(seed)
    n = sek(0.35)
    a = norm(filt(rauschen(n, rng), "lp", lambda t: 1000 * math.exp(-3.5 * t / 0.35 * 0.35), 0.8))
    v = norm(stimme(n, rng, lambda t: 55, lambda t: (500, 900, 2300), q=(3, 4, 6), atem=0.3, jitter=0.05))
    x = [p + 0.4 * q for p, q in zip(a, v)]
    return mal(x, huelle(n, [(0, 0), (0.03, 1), (0.15, 0.5), (0.35, 0)]))


def gorm_sterben_a(seed=3955):
    """Absackendes Stoehnen: Stimme faellt 60 -> 28 Hz, Vokal a -> o, Atem nimmt zu."""
    rng = random.Random(seed)
    n = sek(1.5)
    v = stimme(n, rng, lambda t: 60 * math.exp(-0.76 * t / 1.5 * 1.4), lambda t: (650 - 250 * min(1, t / 1.4), 1000 - 200 * min(1, t / 1.4), 2400),
               q=(3, 4, 6), atem=0.6, jitter=0.05, shimmer=0.3)
    h = mal(huelle(n, [(0, 0), (0.1, 1), (0.6, 0.7), (1.2, 0.2), (1.5, 0)]), schwanken(n, rng, 5, 0.35))
    return saettigen(mal(v, h), 1.6)


def gorm_sterben_b(seed=3956):
    """Nasses Rasseln: Tiefpass-Rauschen sinkt 800 -> 150 Hz, Schwankung ~18 Hz, leise tiefe Stimme 35 Hz."""
    rng = random.Random(seed)
    n = sek(1.5)
    a = norm(filt(rauschen(n, rng), "lp", lambda t: 800 * math.exp(-1.67 * t / 1.5), 0.8))
    am = [1 - 0.7 * (0.5 + 0.5 * math.sin(TAU * 18 * i / SR)) for i in range(n)]
    v = norm(stimme(n, rng, lambda t: 35, lambda t: (450, 800, 2300), q=(3, 4, 6), atem=0.3, jitter=0.06, knarren=0.5))
    x = [p * m + 0.3 * q for p, m, q in zip(a, am, v)]
    return mal(x, huelle(n, [(0, 0), (0.08, 1), (0.6, 0.7), (1.5, 0)]), schwanken(n, rng, 5, 0.3))


def gorm_hammer_a(seed=3957):
    """Tiefes Einatmen: Rauschband 400 -> 1000 Hz, ueber 1 s anschwellend."""
    rng = random.Random(seed)
    n = sek(1.0)
    x = norm(filt(filt(rauschen(n, rng), "bp", lambda t: 400 + 600 * t, 1.0), "lp", 1500, 0.7))
    return mal(x, huelle(n, [(0, 0.03), (0.99, 1.0), (1.0, 0)], 2.0))


def gorm_hammer_b(seed=3958):
    """Anschwellendes Knurren: Stimme 42 -> 62 Hz, Vokal o -> a, Atem, Knarren."""
    rng = random.Random(seed)
    n = sek(1.0)
    v = stimme(n, rng, lambda t: 42 + 20 * t, lambda t: (450 + 200 * t, 800 + 250 * t, 2300), q=(3, 4, 6), atem=0.4,
               jitter=0.05, shimmer=0.25, knarren=0.4)
    return saettigen(mal(v, huelle(n, [(0, 0.03), (0.99, 1.0), (1.0, 0)], 2.2)), 1.6)


# (Name, alte Datei, Ausloeser im Spiel, ist Ankuendigung, Funktion a, Funktion b, Unterschied a/b)
KLAENGE = [
    ("ghul-ankuendigung", "p1-ghul-ankuendigung.wav", "Ghul holt zum Schlag aus (GhoulWindUp)", True,
     ghul_ankuendigung_a, ghul_ankuendigung_b, "a stimmhaftes Knurren, b stimmloser rasselnder Atem"),
    ("ghul-entdecken", "p1-ghul-entdecken.wav", "Ghul bemerkt den Spieler (GhoulNoticed)", False,
     ghul_entdecken_a, ghul_entdecken_b, "a Stoehnen mit Vokalwechsel, b nasses Gurgeln"),
    ("ghul-sterben", "p1-ghul-sterben.wav", "Ghul stirbt (GhoulDied)", False,
     ghul_sterben_a, ghul_sterben_b, "a fallende Stimme mit Atem, b reines rasselndes Ausatmen"),
    ("fledermaus-spuck-ankuendigung", "p1-fledermaus-spuck-ankuendigung.wav", "Fledermaus gluet vor dem Spucken (BatSpitWindUp)", True,
     fl_spuck_ank_a, fl_spuck_ank_b, "a ansteigendes Zischen, b anschwellendes Gurgeln"),
    ("fledermaus-spucken", "p1-fledermaus-spucken.wav", "Fledermaus spuckt den Blutbolzen (BatSpat)", False,
     fl_spucken_a, fl_spucken_b, "a Fauchen mit feuchtem Klicken, b dumpfes Ausspucken 'ptuh'"),
    ("fledermaus-kreischen", "p1-fledermaus-kreischen.wav", "Fledermaus kuendigt den Sturzflug an (BatScreech)", True,
     fl_kreischen_a, fl_kreischen_b, "a Fauchen mit tiefer rauer Stimme darunter, b nur heiseres Kraechzen"),
    ("fledermaus-flattern", "p1-fledermaus-flattern-1.wav", "Fluegelschlag in Hoerweite (BatFlap)", False,
     fl_flattern_a, fl_flattern_b, "a zwei weiche Wellen, b ein dumpfes Wuschen"),
    ("fledermaus-biss", "p1-fledermaus-biss.wav", "Fledermaus beisst im Sturzflug (BatBite)", False,
     fl_biss_a, fl_biss_b, "a feuchtes Schnappen mit Schlag, b Schnappen mit Knurrlaut"),
    ("fledermaus-treffer", "p1-fledermaus-treffer.wav", "Fledermaus wird getroffen (BatHurt)", False,
     fl_treffer_a, fl_treffer_b, "a kurzes Fauchen, b kurzer tiefer Rauhlaut"),
    ("fledermaus-sterben", "p1-fledermaus-sterben.wav", "Fledermaus stirbt (BatDied)", False,
     fl_sterben_a, fl_sterben_b, "a schwacher fallender Stimmlaut, b ausatmendes Zischen"),
    ("fledermaus-fehlsturz", "p1-fledermaus-fehlsturz.wav", "Sturzflug geht ins Leere (BatDiveMissed)", False,
     fl_fehlsturz_a, fl_fehlsturz_b, "a hellerer Luftzug, b dumpferer Luftzug"),
    ("gorm-bruellen", "p1-gorm-bruellen.wav", "Gorm bruellt bei halber Gesundheit (GormRoar)", False,
     gorm_bruellen_a, gorm_bruellen_b, "a offenes Bruellen (Vokal a), b knarrendes Knurren (Vokal o, tiefer)"),
    ("gorm-treffer", "p1-gorm-treffer-1.wav", "Gorm wird am Koerper getroffen (GormHurt)", False,
     gorm_treffer_a, gorm_treffer_b, "a Grunzen mit Stimme, b Ausstossen von Luft"),
    ("gorm-sterben", "p1-gorm-sterben.wav", "Gorm stirbt (GormDied)", False,
     gorm_sterben_a, gorm_sterben_b, "a absackendes Stoehnen, b nasses Rasseln"),
    ("gorm-ankuendigung-hammer", "p1-gorm-ankuendigung-hammer.wav", "Gorm hebt die Keule (GormHammerWindUp)", True,
     gorm_hammer_a, gorm_hammer_b, "a tiefes Einatmen, b anschwellendes Knurren"),
]


def fl_kreischen_c(seed=3961):
    """Zirpen-Schnarren: raue Stimme 180 -> 230 Hz, Pegel mit steigender Rate (8 -> 28 Hz) flatternd, Atem, anschwellend."""
    rng = random.Random(seed)
    n = sek(0.55)
    v = stimme(n, rng, lambda t: 180 + 50 * t / 0.55, lambda t: (600, 1500, 2600), q=(4, 6, 8), atem=0.6,
               jitter=0.06, shimmer=0.25)
    ph = 0.0
    am = []
    for i in range(n):
        t = i / SR
        ph += (8 + 20 * t / 0.55) / SR
        am.append(1 - 0.4 * (0.5 + 0.5 * math.sin(TAU * ph)))
    return mal(v, am, huelle(n, [(0, 0.05), (0.54, 1.0), (0.55, 0)], 1.6))


def fl_kreischen_d(seed=3962):
    """Fluegelschwirren: weiche Rauschschlaege (Band 200 bis 1200 Hz), Schlagrate steigt 6 -> 22 Hz, anschwellend; keine Stimme."""
    rng = random.Random(seed)
    n = sek(0.55)
    x = norm(filt(filt(rauschen(n, rng), "hp", 200, 0.7), "lp", 1200, 0.7))
    ph = 0.0
    am = []
    for i in range(n):
        t = i / SR
        ph += (6 + 16 * t / 0.55) / SR
        am.append(1 - 0.5 * (0.5 + 0.5 * math.sin(TAU * ph)))
    return mal(x, am, huelle(n, [(0, 0.05), (0.54, 1.0), (0.55, 0)], 1.8))


RUNDE2 = [
    ("fledermaus-kreischen", "p1-fledermaus-kreischen.wav", "Fledermaus kuendigt den Sturzflug an (BatScreech)", True,
     fl_kreischen_c, fl_kreischen_d, "c raues Zirpen-Schnarren mit steigender Flatterrate, d Fluegelschwirren ohne Stimme"),
]


def alle(nur_runde2=False):
    """(Eintrag, Endungen) fuer Runde 1 (a, b) und Runde 2 (c, d)."""
    liste = [] if nur_runde2 else [(k, ("a", "b")) for k in KLAENGE]
    return liste + [(k, ("c", "d")) for k in RUNDE2]


def bauen(ordner, nur_runde2=False):
    os.makedirs(ordner, exist_ok=True)
    dateien = []
    for (name, alt, _, _, fa, fb, _), endungen in alle(nur_runde2):
        paar = anpassen_paar(fertig(fa()), fertig(fb()), alt)
        for suffix, x in zip(endungen, paar):
            p = os.path.join(ordner, "%s-%s.wav" % (name, suffix))
            schreibe(p, x)
            dateien.append(p)
    return dateien


def sha(p):
    with open(p, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


# ---------------------------------------------------------------- Messen
def db(v):
    return 20 * math.log10(max(v, 1e-9))


def baender(x):
    """Anteil der Energie (dB unter Gesamtenergie) in <300, 300-3000, >3000 Hz."""
    ges = sum(v * v for v in x) or 1e-12
    tief = filt(filt(x, "lp", 300, 0.707), "lp", 300, 0.707)
    hoch = filt(filt(x, "hp", 3000, 0.707), "hp", 3000, 0.707)
    mitte = filt(filt(x, "hp", 300, 0.707), "lp", 3000, 0.707)
    return [10 * math.log10(max(sum(v * v for v in b) / ges, 1e-9)) for b in (tief, mitte, hoch)]


def einsaetze(x, fenster_ms, schwelle=8.0):
    """Anzahl Stellen, an denen der Pegel innerhalb von 50 ms um mehr als 8 dB steigt (Beginn zaehlt mit)."""
    h = int(SR * fenster_ms / 1000)
    pegel = [db(rms(x[i:i + h])) for i in range(0, len(x) - h, h)]
    schritt = max(1, 50 // fenster_ms)
    zaehler, aktiv = 0, False
    for i in range(len(pegel)):
        vorher = pegel[i - schritt] if i >= schritt else -80.0
        if pegel[i] - vorher > schwelle and pegel[i] > -50:
            if not aktiv:
                zaehler += 1
            aktiv = True
        else:
            aktiv = False
    return zaehler


def messwerte(ordner, mit_bildern):
    zeilen = ["# Messwerte Monsterklaenge (Issue 392)", "",
              "Gemessen, **nicht gehoert**. Mono, 44 100 Hz, 16 Bit. Lautheit (RMS) je Klang an die alte Datei angeglichen; "
              "Spitze hoechstens -1,5 dBFS. Baender = Energie in dB unter Gesamtenergie. Einsaetze = Stellen mit "
              "Pegelanstieg > 8 dB in 50 ms (Beginn zaehlt mit), Fenster 10 ms / 30 ms. Spektrogramme: `bilder/`.", "",
              "| Datei | Laenge (s) | Spitze (dBFS) | Mittel (dB, ffmpeg) | alt: Mittel (dB) | <300 Hz | 300-3k Hz | >3k Hz | Einsaetze 10/30 ms |",
              "|---|---|---|---|---|---|---|---|---|"]
    if mit_bildern:
        os.makedirs(os.path.join(ordner, "bilder"), exist_ok=True)
    for (name, alt, _, _, _, _, _), endungen in alle():
        for suffix in endungen:
            p = os.path.join(ordner, "%s-%s.wav" % (name, suffix))
            x = lies(p)
            ffm = subprocess.run(["ffmpeg", "-hide_banner", "-i", p, "-af", "volumedetect", "-f", "null", "-"],
                                 capture_output=True, text=True).stderr
            mittel = [l.split("mean_volume:")[1].split()[0] for l in ffm.splitlines() if "mean_volume" in l][0]
            ffa = subprocess.run(["ffmpeg", "-hide_banner", "-i", os.path.join(ALT, alt), "-af", "volumedetect", "-f", "null", "-"],
                                 capture_output=True, text=True).stderr
            mitte_alt = [l.split("mean_volume:")[1].split()[0] for l in ffa.splitlines() if "mean_volume" in l][0]
            b = baender(x)
            zeilen.append("| %s-%s.wav | %.2f | %.1f | %s | %s | %.1f | %.1f | %.1f | %d / %d |" % (
                name, suffix, len(x) / SR, db(max(abs(v) for v in x)), mittel, mitte_alt, b[0], b[1], b[2],
                einsaetze(x, 10), einsaetze(x, 30)))
            if mit_bildern:
                subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", p, "-lavfi",
                                "showspectrumpic=s=640x256:legend=0", os.path.join(ordner, "bilder", "%s-%s.png" % (name, suffix))],
                               check=True)
    zeilen += ["", "Ankuendigungen (Regel 2 der Audio-Bible: genau ein Einsatz): " +
               ", ".join(n for n, _, _, a, _, _, _ in KLAENGE if a) + ".", ""]
    with open(os.path.join(ordner, "messwerte.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(zeilen))


def main():
    if "--pruefen" in sys.argv:
        # Zweiter Lauf: in einen Temp-Ordner bauen und gegen die eingecheckte wav.sha256 (erster Lauf) vergleichen.
        with open(os.path.join(HIER, "wav.sha256"), encoding="utf-8") as f:
            soll = dict((z.split()[1], z.split()[0]) for z in f if z.strip())
        with tempfile.TemporaryDirectory() as t:
            ist = dict((os.path.basename(p), sha(p)) for p in bauen(t, "--nur-runde2" in sys.argv))
        gleich = all(soll.get(k) == v for k, v in ist.items()) and bool(ist)
        print("zweiter Lauf bitgleich zu wav.sha256:", gleich, "(%d Dateien)" % len(ist))
        return 0 if gleich else 1
    bauen(HIER, "--nur-runde2" in sys.argv)
    messwerte(HIER, True)
    with open(os.path.join(HIER, "wav.sha256"), "w", newline="\n") as f:
        for (name, *_), endungen in alle():
            for s_ in endungen:
                p = os.path.join(HIER, "%s-%s.wav" % (name, s_))
                f.write("%s  %s-%s.wav\n" % (sha(p), name, s_))
    return 0


if __name__ == "__main__":
    sys.exit(main())
