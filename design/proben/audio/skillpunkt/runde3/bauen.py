"""Hoerprobe Skillpunkt, Runde 3 (Issue 377): Schlag mit Chor ohne Wummern, nur Standardbibliothek.

Aufruf (aus dem Repository-Stamm):  python design/proben/audio/skillpunkt/runde3/bauen.py
Schreibt a-ohne-vibrato.wav, b-offener-akkord.wav, c-sanftes-vibrato.wav neben das Skript und druckt die
Messwerte, dazu zum Vergleich die Endfassung aus runde2 (wird nicht veraendert, nur gelesen/neu gerechnet).

Ursache des Wummerns in runde2/bauen.py (gemessen, siehe README): Das Vibrato steckt als Faktor in der
Frequenz *innerhalb* von sin(TAU * f * vib(t) * t). Dadurch waechst die Verstimmung mit t (Phase = f*t*(1+d*sin(w*t)),
Frequenzabweichung ~ f*d*w*t): nach 1 s schwankt jede Stimme um mehrere Hz statt um 0,4 %. Die Stimmen
schweben gegeneinander, im langen Ausklang frei hoerbar. Hier wird die Phase aufsummiert (Frequenz zeitabhaengig).
"""
import importlib.util
import math
import os

HIER = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("runde2_bauen", os.path.join(HIER, "..", "runde2", "bauen.py"))
r2 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r2)
SR, TAU, sek = r2.SR, r2.TAU, r2.sek


def chor(akkord, vib_tiefe, vib_rate, laenge=1.0, chor_abfall=3.2, release=0.3, nachlauf=0.4):
    """Wie runde2.schlag_chor, aber mit aufsummierter Phase. Schlag, Einsatz, Abfall und Laenge unveraendert."""
    n = sek(laenge)
    stimmen = [(f * h, am * math.exp(-(((f * h) - 500) / 300) ** 2) / h, f * 0.1) for f, am in akkord for h in range(1, 9)]
    phasen = [p0 for _, _, p0 in stimmen]
    sch_phase = 0.0
    out = []
    for i in range(n):
        t = i / SR
        sch_phase += TAU * (45 + 45 * math.exp(-25 * t)) / SR
        s = 1.2 * math.exp(-14 * t) * math.sin(sch_phase)
        vib = 1 + vib_tiefe * math.sin(TAU * vib_rate * t)
        summe = 0.0
        for k, (fh, g, _) in enumerate(stimmen):
            phasen[k] += TAU * fh * vib / SR
            summe += g * math.sin(phasen[k])
        s += 0.55 * summe * min(1.0, t / 0.04) * math.exp(-chor_abfall * t)
        out.append(s * r2.huelle(i, n, 0.002, release))
    return r2.hall(out, nachlauf, 0.2)


A_MOLL = [(110.0, 1.0), (131.0, 0.8), (165.0, 0.7), (220.0, 0.5)]  # wie runde2
OFFEN = [(110.0, 1.0), (165.0, 0.8), (220.0, 0.6)]  # Grundton, Quinte, Oktave: keine nahen Toene
WEIT = [(110.0, 1.0), (165.0, 0.8), (220.0, 0.6), (262.0, 0.4)]  # Moll-Terz erst eine Oktave hoeher


def a_ohne_vibrato():
    """Gleicher a-Moll-Akkord wie die Endfassung, aber ohne Vibrato."""
    return chor(A_MOLL, 0.0, 0.0)


def b_offener_akkord():
    """Offener Akkord (Grundton, Quinte, Oktave, 110/165/220 Hz), kein Vibrato: kein naher Ton, der schweben kann."""
    return chor(OFFEN, 0.0, 0.0)


def c_sanftes_vibrato():
    """Weiter Moll-Akkord (Terz oben) mit korrekt gerechnetem, sehr sanftem Vibrato (0,15 %, 4,5 Hz)."""
    return chor(WEIT, 0.0015, 4.5)


def _huellkurve(samples, von, bis):
    """Pegel (dB) in 18-ms-Fenstern, Schritt 4 ms, minus gleitender Mittelwert ueber 100 ms (nimmt den normalen
    Abfall heraus, uebrig bleibt nur die schnelle Schwankung)."""
    w, h = sek(0.018), sek(0.004)
    pegel = [10 * math.log10(sum(x * x for x in samples[i : i + w]) / w + 1e-14) for i in range(sek(von), sek(bis) - w, h)]
    k = 12  # +-12 Schritte = 100 ms
    return [pegel[j] - sum(pegel[j - k : j + k + 1]) / (2 * k + 1) for j in range(k, len(pegel) - k)]


def schwankung(samples, von=0.3, bis=1.0):
    """Schwankung der Huellkurve im Ausklang: Standardabweichung (dB) und groesste Spanne (dB) der schnellen Anteile."""
    res = _huellkurve(samples, von, bis)
    sd = math.sqrt(sum(r * r for r in res) / len(res))
    return sd, max(res) - min(res)


def schwebungsfrequenz(samples, von=0.3, bis=1.0):
    """Staerkste Schwankungsfrequenz (4-60 Hz, 0,5-Hz-Raster) der Huellkurve, per Goertzel."""
    res = _huellkurve(samples, von, bis)
    rate = SR / sek(0.004)
    return max((f / 2 for f in range(8, 121)), key=lambda f: _g(res, f, rate))


def _g(x, f, rate):
    k = 2 * math.cos(TAU * f / rate)
    s1 = s2 = 0.0
    for v in x:
        s1, s2 = v + k * s1 - s2, s1
    return s2 * s2 + s1 * s1 - k * s1 * s2


def einzelstimme(neu, tiefe=0.004, rate=5.5, f=110.0, laenge=1.1):
    """Eine einzelne 110-Hz-Stimme mit Vibrato: alt = Faktor in der Frequenz innerhalb des Sinus (wie runde2),
    neu = aufsummierte Phase (wie hier)."""
    n = sek(laenge)
    out, ph = [], 0.0
    for i in range(n):
        t = i / SR
        vib = 1 + tiefe * math.sin(TAU * rate * t)
        if neu:
            ph += TAU * f * vib / SR
            out.append(math.sin(ph))
        else:
            out.append(math.sin(TAU * f * vib * t))
    return out


def abweichung(samples, f0=110.0, von=0.4, bis=1.0):
    """Groesste Abweichung (Hz) der Stimme von f0: Mischung mit exp(-j*2*pi*f0*t), Mittelung ueber 50 ms, Phasenableitung.
    Die Mittelung glaettet das Vibrato etwas, der Wert ist also eine Untergrenze."""
    w, h = sek(0.05), sek(0.004)
    z = []
    for i in range(sek(von), sek(bis) - w, h):
        re = im = 0.0
        for j in range(i, i + w):
            a = TAU * f0 * j / SR
            re += samples[j] * math.cos(a)
            im -= samples[j] * math.sin(a)
        z.append(complex(re, im))
    dev = [math.atan2((b * a.conjugate()).imag, (b * a.conjugate()).real) / TAU * SR / h for a, b in zip(z, z[1:])]
    return max(abs(x) for x in dev)


def main():
    varianten = [("a-ohne-vibrato.wav", a_ohne_vibrato), ("b-offener-akkord.wav", b_offener_akkord), ("c-sanftes-vibrato.wav", c_sanftes_vibrato)]
    import sys
    sys.stdout.reconfigure(encoding="utf-8")
    print()
    print("Ursache, einzelne 110-Hz-Stimme mit Vibrato 0,4 % bei 5,5 Hz, groesste Abweichung von 110 Hz zwischen 0,4 und 1,0 s:")
    print(f"- alt (Faktor in der Frequenz, runde2): {abweichung(einzelstimme(False)):.1f} Hz")
    print(f"- neu (aufsummierte Phase): {abweichung(einzelstimme(True)):.1f} Hz")
    print()
    print()
    print("| Datei | Gesamt (s) | Hauptteil (s) | Nachklang (s) | Spitze (dBFS) | Streuung Ausklang (dB) | größte Spanne (dB) | stärkste Schwankung (Hz) | erstes/letztes Sample |")
    print("|---|---|---|---|---|---|---|---|---|")
    ref = r2.normieren(r2.schlag_chor_final())
    zeilen = [("runde2/3-schlag-chor-final.wav (Endfassung)", ref)]
    for name, fn in varianten:
        s = r2.normieren(fn())
        r2.schreiben(os.path.join(HIER, name), s)
        zeilen.append((name, s))
    for name, s in zeilen:
        sd, sp = schwankung(s)
        spitze = 20 * math.log10(max(abs(x) for x in s))
        print(
            f"| {name} | {len(s) / SR:.2f} | {r2.dauer_bis(s, 20):.2f} | {r2.dauer_bis(s, 40):.2f} | {spitze:.1f} | "
            f"{sd:.2f} | {sp:.2f} | {schwebungsfrequenz(s):.1f} | {s[0]:.5f} / {s[-1]:.5f} |"
        )


if __name__ == "__main__":
    main()
