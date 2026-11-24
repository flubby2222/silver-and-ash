#!/usr/bin/env python3
"""Hoerprobe grosses Tor der Bossraum-Tuer (#549), ausserhalb des Spiels.

Ein 6 m hohes, zweifluegliges Holztor mit schweren Eisenbaendern in einem
Steinportal. Es klingt nach seinem Material: Holz auf Stein fuehrt, Eisen ist
nur Beiwerk (kein Kette, keine Kurbel, kein Zahnrad, kein Quietschen).

Zeitbasis: die vorlaeufig empfohlenen Bewegungen aus dem Probestueck
`design/proben/bewegung/bossraum-tuer-549/` (60 Ticks je Sekunde):
  Schliessen "Anlauf und harter Anschlag": Anschlag bei Tick 78 (1,30 s),
      Rueckpraller bei Tick 92 und 100, Ruhe ab Tick 100.
  Oeffnen "gleichmaessig schwer": 132 Ticks, 30 Ticks Anlauf, 40 Ticks Auslauf.
Alle Zeiten sind Parameter. Waehlt der Mensch eine andere Bewegung, wird nur
neu gerechnet:  python bauen.py --anschlag 80 --oeffnen 124 [--ziel ORDNER]

Nur Python-Standardbibliothek. Mono, 44100 Hz, 16 Bit. Zufall mit festem Seed,
zwei Laeufe sind bitgleich.

Dateien (je Bewegung drei deutlich verschiedene Varianten):
  tor-schliessen-1-dumpfer-schlag.wav    trocken, kurzer Raum
  tor-schliessen-2-mit-nachhall.wav      gleicher Schlag, langer dunkler Raumhall
  tor-schliessen-3-mit-bodengrollen.wav  Schlag plus langes Beben im Stein
  tor-oeffnen-1-kurzes-knarren.wav       Knarren nur im Anlauf, danach Rollen
  tor-oeffnen-2-langes-knarren.wav       Knarren durchgehend, grosser Raum
  tor-oeffnen-3-mit-steinschleifen.wav   Knarren plus Schleifen der Flügel auf der Schwelle
"""
import argparse
import math
import random
import struct
import wave
from pathlib import Path

SR = 44100
TAU = 2.0 * math.pi
TICK = SR / 60.0  # Samples je Tick (735)

BEW = 0.12                          # Pegel der Bewegung vor dem Anschlag (Schlag = 1)
PEGEL_SCHLIESSEN_SPITZE_DB = -2.5   # wie Gorm-Aufschlag im Spiel (-2,5 dBFS)
PEGEL_OEFFNEN_MITTEL_DB = -24.0     # wie Fallgitter-Heben (Mittel -24 dBFS)
PEGEL_OEFFNEN_SPITZE_MAX_DB = -3.0


def tp(t):
    """Tick -> Sample."""
    return int(round(t * TICK))


def n(sek):
    return int(round(sek * SR))


# ---------------------------------------------------------------- Bausteine

def rauschen(L, rng):
    return [rng.uniform(-1.0, 1.0) for _ in range(L)]


def biquad(x, typ, f, q=0.7071):
    w0 = TAU * f / SR
    c, s = math.cos(w0), math.sin(w0)
    a = s / (2.0 * q)
    if typ == "tief":
        b0, b1, b2 = (1 - c) / 2, 1 - c, (1 - c) / 2
    elif typ == "hoch":
        b0, b1, b2 = (1 + c) / 2, -(1 + c), (1 + c) / 2
    else:  # band, konstante Spitze 0 dB
        b0, b1, b2 = a, 0.0, -a
    a0, a1, a2 = 1 + a, -2 * c, 1 - a
    b0, b1, b2, a1, a2 = b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0
    y = [0.0] * len(x)
    x1 = x2 = y1 = y2 = 0.0
    for i, v in enumerate(x):
        o = b0 * v + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, v, y1, o
        y[i] = o
    return y


def tief2(x, f):
    return biquad(biquad(x, "tief", f), "tief", f)


def glatt(L, rng, rate_hz):
    """Langsam schwankende Zufallskurve 0..1 (Kosinus-Interpolation)."""
    schritt = max(1, int(SR / rate_hz))
    pts = [rng.random() for _ in range(L // schritt + 3)]
    out = [0.0] * L
    for i in range(L):
        k, fr = divmod(i, schritt)
        u = fr / schritt
        w = 0.5 - 0.5 * math.cos(math.pi * u)
        out[i] = pts[k] * (1 - w) + pts[k + 1] * w
    return out


def addiere(ziel, pos, quelle, faktor=1.0):
    for i, v in enumerate(quelle):
        j = pos + i
        if 0 <= j < len(ziel):
            ziel[j] += v * faktor


def hall(x, rt, daempfung, laenge, pegel=1.0):
    """Dunkler Raumhall (8 Kammfilter mit Daempfung, 2 Allpaesse), ohne Vorverzoegerung.
    rt = Nachhallzeit (60 dB) in s. Das Ergebnis hat die Laenge `laenge`."""
    L = laenge
    inp = x[:L] + [0.0] * max(0, L - len(x))
    verz = [n(d) for d in (0.0113, 0.0151, 0.0197, 0.0253, 0.0297, 0.0371, 0.0437, 0.0583)]
    summe = [0.0] * L
    for d in verz:
        g = 10 ** (-3.0 * d / (rt * SR))
        buf = [0.0] * d
        store = 0.0
        idx = 0
        for i in range(L):
            o = buf[idx]
            store = o * (1.0 - daempfung) + store * daempfung
            buf[idx] = inp[i] + store * g
            idx += 1
            if idx == d:
                idx = 0
            summe[i] += o
    for d, g in ((n(0.0126), 0.5), (n(0.0100), 0.5)):
        buf = [0.0] * d
        idx = 0
        for i in range(L):
            b = buf[idx]
            v = summe[i]
            buf[idx] = v + b * g
            summe[i] = b - v * g
            idx += 1
            if idx == d:
                idx = 0
    k = pegel / len(verz)
    return [s * k for s in summe]


# ------------------------------------------------------- Bewegungsverlaeufe

def tempo_schliessen(L, anschlag_tick):
    """Anlauf und harter Anschlag: Weg ~ (t/T)^2,2, Tempo ~ (t/T)^1,2, 0..1 bis zum Anschlag."""
    v = [0.0] * L
    for i in range(min(L, tp(anschlag_tick))):
        v[i] = (i / (TICK * anschlag_tick)) ** 1.2
    return v


def tempo_oeffnen(L, dauer, an, ab):
    """Trapez: Anlauf `an`, gleichmaessig, Auslauf `ab` (Ticks); Tempo 0..1 (Maximum 1)."""
    v = [0.0] * L
    for i in range(min(L, tp(dauer))):
        t = i / TICK
        if t < an:
            v[i] = t / an
        elif t > dauer - ab:
            v[i] = max(0.0, (dauer - t) / ab)
        else:
            v[i] = 1.0
        # weiche Ecken: Kosinus statt Dreieck
        v[i] = 0.5 - 0.5 * math.cos(math.pi * v[i]) if v[i] < 1.0 else 1.0
    return v


# ------------------------------------------------------------ Materialklang

def holzstimme(v, rng, ab, bis, f_lo, f_hi, pegel, zittern=0.06):
    """Tiefes Ächzen von Holz und Angeln: ein einzelner Grundton (Phase aufsummiert,
    nie Frequenz mal Zeit) mit Obertönen bis etwa 1 kHz, Formantbetonung bei 180 und
    420 Hz, Tonhöhe und Pegel folgen dem Tempo und schwanken unregelmäßig."""
    L = len(v)
    w1 = glatt(L, rng, 3.0)
    w2 = glatt(L, rng, 7.0)
    out = [0.0] * L
    ph = 0.0
    for i in range(ab, min(bis, L)):
        s = v[i]
        if s <= 0.0:
            continue
        f = (f_lo + (f_hi - f_lo) * s ** 0.7) * (1.0 + zittern * (w1[i] - 0.5))
        ph += TAU * f / SR
        amp = (s ** 0.9) * (0.5 + 0.5 * w2[i])
        x = 0.0
        for k in range(1, 12):
            fk = k * f
            if fk > 1100.0:
                break
            g = k ** -1.1 * (1.0 + 1.6 * math.exp(-((fk - 180.0) / 90.0) ** 2)
                             + 1.0 * math.exp(-((fk - 420.0) / 140.0) ** 2))
            x += g * math.sin(k * ph)
        out[i] = x * amp
    # raues Holzrascheln: Rauschen 90..420 Hz, mit dem Tempo
    r = biquad(biquad(rauschen(L, rng), "hoch", 90.0), "tief", 420.0)
    for i in range(ab, min(bis, L)):
        out[i] += r[i] * (v[i] ** 1.1) * (0.4 + 0.6 * w2[i]) * 0.9
    return [o * pegel for o in out]


def knarrkoerner(v, rng, ab, bis, dichte_max, f_lo, f_hi, pegel, dauer_lo=0.03, dauer_hi=0.08, abkling=60.0):
    """Haft-Gleit-Körner (Stick-Slip): kurze gedämpfte Resonanzen im Bereich f_lo..f_hi.
    Dichte steigt mit dem Tempo."""
    L = len(v)
    out = [0.0] * L
    phase = rng.random()
    for i in range(ab, min(bis, L)):
        s = v[i]
        phase += (dichte_max * (0.15 + 0.85 * s)) / SR if s > 0.02 else 0.0
        if phase >= 1.0:
            phase -= 1.0
            f = rng.uniform(f_lo, f_hi)
            d = n(rng.uniform(dauer_lo, dauer_hi))
            g = pegel * (0.3 + 0.7 * s) * rng.uniform(0.4, 1.0)
            for j in range(d):
                if i + j >= L:
                    break
                t = j / SR
                a = min(1.0, j / 60.0)  # 1,4 ms Einschwingen, kein Knacken
                out[i + j] += math.sin(TAU * f * t) * math.exp(-abkling * t) * g * a
    return out


def rollen(v, rng, ab, bis, pegel):
    """Schweres Rollen und Luftverdraengung: Rauschen unter 300 Hz, Pegel ~ Tempo^1,5."""
    L = len(v)
    r = biquad(biquad(rauschen(L, rng), "hoch", 35.0), "tief", 260.0)
    r = biquad(r, "tief", 260.0)
    out = [0.0] * L
    for i in range(ab, min(bis, L)):
        out[i] = r[i] * (v[i] ** 1.5) * pegel
    return out


def schleifen(v, rng, ab, bis, pegel):
    """Holz und Eisenband schleifen auf der Steinschwelle: gebändertes Rauschen 160..900 Hz,
    körnig moduliert (gleichmäßiger Teppich, keine Töne)."""
    L = len(v)
    r = biquad(biquad(rauschen(L, rng), "hoch", 160.0), "tief", 900.0)
    r = biquad(r, "tief", 900.0)
    kornig = glatt(L, rng, 38.0)
    lang = glatt(L, rng, 4.0)
    out = [0.0] * L
    for i in range(ab, min(bis, L)):
        s = v[i]
        out[i] = r[i] * (s ** 0.9) * (0.25 + 0.75 * kornig[i] * (0.5 + 0.5 * lang[i])) * pegel
    return out


def kern_schlag(rng, laut, kurz=False):
    """Ein Flügelpaar schlägt ins Steinportal. Vier Schichten, ein Ereignis:
    Kern (Sinus 80 -> 40 Hz), Holzkörper (Rauschen unter 450 Hz), Eisenbeiwerk
    (gedämpfte Eigenschwingungen 210..840 Hz, leise), Stein-Nachbeben (unter 130 Hz)."""
    ns = n(0.9 if not kurz else 0.45)
    out = [0.0] * ns
    kn = biquad(biquad(rauschen(ns, rng), "tief", 450.0), "tief", 450.0)
    nb = biquad(biquad(rauschen(ns, rng), "tief", 130.0), "tief", 130.0)
    ph = 0.0
    moden = ((211.0, 24.0, 0.10), (347.0, 30.0, 0.07), (593.0, 38.0, 0.05), (838.0, 46.0, 0.03))
    for i in range(ns):
        t = i / SR
        ph += TAU * (40.0 + 40.0 * math.exp(-t * 32.0)) / SR
        x = 1.0 * math.sin(ph + math.pi / 2) * math.exp(-7.0 * t)
        x += 1.7 * kn[i] * math.exp(-20.0 * t)
        for f, d, g in moden:
            x += g * math.sin(TAU * f * t) * math.exp(-d * t)
        x += 3.0 * nb[i] * math.exp(-4.5 * t)
        a = min(1.0, i / 66.0)  # 1,5 ms Anstieg
        out[i] = x * a * laut
    return out


def bodengrollen(rng, laut, dauer):
    """Langes Beben im Stein: Rauschen 25..110 Hz, dazu ein Ton bei 52 Hz und seine Oktave
    (gleiche Phase, kein Schweben), Abklingen über `dauer` s."""
    ns = n(dauer)
    r = biquad(biquad(rauschen(ns, rng), "hoch", 25.0), "tief", 110.0)
    r = biquad(r, "tief", 110.0)
    lang = glatt(ns, rng, 5.0)
    out = [0.0] * ns
    for i in range(ns):
        t = i / SR
        env = math.exp(-2.4 * t) * min(1.0, t / 0.03)
        ph = TAU * 52.0 * t
        x = 4.0 * r[i] * (0.5 + 0.5 * lang[i]) + 0.55 * math.sin(ph) + 0.25 * math.sin(2 * ph)
        out[i] = x * env * laut
    return out


def abschluss(buf, ausblenden_s):
    L = len(buf)
    a = min(L, n(ausblenden_s))
    for i in range(a):
        buf[L - a + i] *= 0.5 + 0.5 * math.cos(math.pi * i / a)
    # Anfang: 3 ms Einblenden gegen Knacken
    e = n(0.003)
    for i in range(min(e, L)):
        buf[i] *= 0.5 - 0.5 * math.cos(math.pi * i / e)


def veredeln(m):
    m = biquad(m, "hoch", 24.0)
    return tief2(tief2(m, 2200.0), 2200.0)  # nichts Helles oder Schrilles


def spitze_db(x):
    return 20 * math.log10(max(abs(s) for s in x) + 1e-12)


def mittel_db(x):
    return 10 * math.log10(sum(s * s for s in x) / len(x) + 1e-20)


# ------------------------------------------------------------ die Geräusche

def schliessen(var, anschlag_tick):
    rng = random.Random(549_100 + var)
    schwanz = {1: 0.9, 2: 2.8, 3: 2.2}[var]
    ende = anschlag_tick + 22 + 10          # letzter Rueckpraller + Nachklang
    L = tp(ende) + n(schwanz)
    dry = [0.0] * L
    send = [0.0] * L
    v = tempo_schliessen(L, anschlag_tick)
    # Bewegung bis zum Anschlag: Holz aechzt, Angeln knarren, Rollen. Steigt bis zum Schlag.
    st = holzstimme(v, rng, 0, tp(anschlag_tick), 58.0, 92.0, BEW)
    kk = knarrkoerner(v, rng, 0, tp(anschlag_tick), 28.0, 120.0, 300.0, BEW * 0.9)
    eis = knarrkoerner(v, rng, 0, tp(anschlag_tick), 14.0, 260.0, 520.0, BEW * 0.2, 0.04, 0.10, 45.0)
    ro = rollen(v, rng, 0, tp(anschlag_tick), BEW * 2.0)
    bew = [st[i] + kk[i] + eis[i] + ro[i] for i in range(L)]
    # Der Pegel steigt bis zum Schlag; kurz davor (20 ms) weich auf null, damit der Schlag allein steht.
    vor = n(0.025)
    p = tp(anschlag_tick)
    for i in range(vor):
        bew[p - vor + i] *= 1.0 - 0.9 * (i / vor)
    addiere(dry, 0, bew, 1.0)
    sendanteil = {1: 0.10, 2: 0.55, 3: 0.30}[var]
    addiere(send, 0, bew, sendanteil)
    # Schlag und zwei kleine Rueckpraller
    for tick, laut, kurz in ((anschlag_tick, 1.0, False),
                             (anschlag_tick + 14, 0.20, True),
                             (anschlag_tick + 22, 0.06, True)):
        s = kern_schlag(rng, laut, kurz)
        addiere(dry, tp(tick), s, 1.0)
        addiere(send, tp(tick), s, sendanteil * 0.9)
    if var == 3:
        g = bodengrollen(rng, 0.9, 2.0)
        addiere(dry, tp(anschlag_tick), g, 1.0)
        addiere(send, tp(anschlag_tick), g, 0.2)
    rt, damp = {1: (0.45, 0.55), 2: (2.6, 0.45), 3: (1.4, 0.5)}[var]
    nach = hall(send, rt, damp, L, 1.0)
    m = [dry[i] + nach[i] for i in range(L)]
    m = veredeln(m)
    abschluss(m, 0.6 if var != 1 else 0.35)
    k = 10 ** ((PEGEL_SCHLIESSEN_SPITZE_DB - spitze_db(m)) / 20.0)
    return [s * k for s in m]


def oeffnen(var, dauer, an, ab):
    rng = random.Random(549_200 + var)
    schwanz = {1: 0.8, 2: 2.4, 3: 1.2}[var]
    L = tp(dauer) + n(schwanz)
    dry = [0.0] * L
    send = [0.0] * L
    v = tempo_oeffnen(L, dauer, an, ab)
    ende_k = {1: int(dauer * 0.58), 2: dauer, 3: dauer}[var]
    ende_s = tp(ende_k)
    if var == 1:
        # Knarren endet nicht abrupt, sondern blendet ueber 0,4 s aus (Tempo-Huellkurve fuer Stimme und Koerner)
        vk = list(v)
        fa = n(0.4)
        for i in range(max(0, ende_s - fa), min(L, ende_s)):
            vk[i] *= 0.5 + 0.5 * math.cos(math.pi * (i - (ende_s - fa)) / fa)
        v_knarr = vk
    else:
        v_knarr = v
    if var == 1:
        stimmen = holzstimme(v_knarr, rng, 0, ende_s, 52.0, 78.0, 0.65)
        kk = knarrkoerner(v_knarr, rng, 0, ende_s, 18.0, 110.0, 260.0, 0.45)
    elif var == 2:
        stimmen = holzstimme(v, rng, 0, ende_s, 60.0, 112.0, 0.70, zittern=0.10)
        kk = knarrkoerner(v, rng, 0, ende_s, 30.0, 110.0, 330.0, 0.55)
    else:
        stimmen = holzstimme(v, rng, 0, ende_s, 56.0, 88.0, 0.50)
        kk = knarrkoerner(v, rng, 0, ende_s, 20.0, 110.0, 280.0, 0.35)
    eis = knarrkoerner(v, rng, 0, ende_s, 10.0, 260.0, 520.0, 0.08, 0.04, 0.10, 45.0)
    ro = rollen(v, rng, 0, tp(dauer), 3.0)
    bew = [stimmen[i] + kk[i] + eis[i] + ro[i] for i in range(L)]
    if var == 3:
        sch = schleifen(v, rng, 0, tp(dauer), 0.85)
        bew = [bew[i] + sch[i] for i in range(L)]
    sendanteil = {1: 0.12, 2: 0.45, 3: 0.20}[var]
    addiere(dry, 0, bew, 1.0)
    addiere(send, 0, bew, sendanteil)
    # Tick 0: das Tor loest sich dumpf aus dem Stein (kein Metallklang)
    ns = n(0.35)
    kn = biquad(biquad(rauschen(ns, rng), "tief", 300.0), "tief", 300.0)
    lo = []
    ph = 0.0
    for i in range(ns):
        t = i / SR
        ph += TAU * (44.0 + 20.0 * math.exp(-t * 25.0)) / SR
        lo.append((math.sin(ph + math.pi / 2) * math.exp(-12.0 * t) + 1.4 * kn[i] * math.exp(-28.0 * t))
                  * min(1.0, i / 110.0))
    addiere(dry, 0, lo, 0.32)
    addiere(send, 0, lo, 0.32 * sendanteil)
    rt, damp = {1: (0.5, 0.55), 2: (2.4, 0.45), 3: (0.9, 0.5)}[var]
    nach = hall(send, rt, damp, L, 1.0)
    m = [dry[i] + nach[i] for i in range(L)]
    m = veredeln(m)
    abschluss(m, 0.5)
    k = 10 ** ((PEGEL_OEFFNEN_MITTEL_DB - mittel_db(m)) / 20.0)
    m = [s * k for s in m]
    if spitze_db(m) > PEGEL_OEFFNEN_SPITZE_MAX_DB:
        k = 10 ** ((PEGEL_OEFFNEN_SPITZE_MAX_DB - spitze_db(m)) / 20.0)
        m = [s * k for s in m]
    return m


def schreibe(pfad, x):
    with wave.open(str(pfad), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", max(-32768, min(32767, int(round(s * 32767.0))))) for s in x))


NAMEN_SCHLIESSEN = {1: "dumpfer-schlag", 2: "mit-nachhall", 3: "mit-bodengrollen"}
NAMEN_OEFFNEN = {1: "kurzes-knarren", 2: "langes-knarren", 3: "mit-steinschleifen"}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--anschlag", type=int, default=78, help="Tick des Anschlags beim Schliessen")
    ap.add_argument("--oeffnen", type=int, default=132, help="Dauer des Oeffnens in Ticks")
    ap.add_argument("--anlauf", type=int, default=30, help="Anlauf beim Oeffnen in Ticks")
    ap.add_argument("--auslauf", type=int, default=40, help="Auslauf beim Oeffnen in Ticks")
    ap.add_argument("--ziel", default=str(Path(__file__).resolve().parent))
    a = ap.parse_args()
    ziel = Path(a.ziel)
    ziel.mkdir(parents=True, exist_ok=True)
    for var in (1, 2, 3):
        schreibe(ziel / f"tor-schliessen-{var}-{NAMEN_SCHLIESSEN[var]}.wav", schliessen(var, a.anschlag))
        schreibe(ziel / f"tor-oeffnen-{var}-{NAMEN_OEFFNEN[var]}.wav", oeffnen(var, a.oeffnen, a.anlauf, a.auslauf))


if __name__ == "__main__":
    main()
