"""Prueft, ob ps1_klang.py tut, was es soll, und schreibt ps1_pruefung.md.

1. ADPCM-Dekoder: Stille bleibt Stille; Kodierer und Dekoder rechnen gleich;
   Bloecke haben das richtige Format.
2. Gauss-Tabelle: Abweichung zu 18 Stuetzstellen aus psx-spx; Summe je Phase.
3. Frequenzgang: Sinustoene durch die Kette (ohne Hall), Pegel vorher/nachher;
   einmal nur Umrechnen + Interpolation, einmal mit ADPCM; bei 22 050 und 11 025 Hz.
4. Rauschabstand der ADPCM-Stufe: Sinus 1 kHz und ein Schuss aus Hoerprobe 2.
5. Hall: Abklingzeit der Voreinstellungen (Impulsantwort), Stabilitaet.

Aufruf: python ps1_pruefung.py
"""
import math
import sys
from pathlib import Path

import ps1_klang as P

HERE = Path(__file__).resolve().parent
SR = P.SR


def db(x):
    return -200.0 if x <= 0 else 20 * math.log10(x)


def rms(x):
    return math.sqrt(sum(v * v for v in x) / max(1, len(x)))


def sine(f, sec=0.4, amp=16384):
    return [int(round(amp * math.sin(2 * math.pi * f * i / SR))) for i in range(int(sec * SR))]


def mid(x):
    n = len(x)
    return x[n // 4: 3 * n // 4]


def chain_level(f, rate, use_adpcm):
    x = sine(f)
    low = P.resample([x], SR, rate)[0]
    d = P.adpcm_roundtrip(low) if use_adpcm else low
    y = P.play(d, rate)[:len(x)]
    return db(rms(mid(y)) / rms(mid(x)))


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    out = ["# Prüfung der PS1-Kette (`ps1_klang.py`)\n",
           "Erzeugt mit `python ps1_pruefung.py`. Alle Zahlen sind gemessen; was sie für den Klang bedeuten, entscheidet die Hörprobe.\n"]

    # 1. ADPCM-Format und Dekoder
    zeros = P.adpcm_roundtrip([0] * 280)
    blocks = P.encode(sine(1000, 0.05))
    ok_format = all(len(n) == 28 and all(0 <= v <= 15 for v in n) and (h & 0x0F) <= 12 and (h >> 4) <= 4 for h, n in blocks)
    # Kodierer und Dekoder rechnen gleich: Der Kodierer schaetzt pro Block den Fehler mit der Dekoder-Arithmetik;
    # hier wird geprueft, dass erneutes Kodieren des dekodierten Signals es exakt wiedergibt (Fixpunkt).
    x = sine(440, 0.1, 12000)
    once = P.adpcm_roundtrip(x)
    twice = P.adpcm_roundtrip(once)
    same = sum(1 for a, b in zip(once, twice) if a == b) / len(once)
    used = {}
    for h, _ in P.encode(P.resample([sine(300, 0.3)], SR, 22050)[0]):
        used[h >> 4] = used.get(h >> 4, 0) + 1
    out += ["## 1. ADPCM\n",
            f"- Stille rein, Stille raus: {'ja' if all(v == 0 for v in zeros) else 'NEIN'}",
            f"- Blockformat (28 Werte zu 4 Bit, Shift 0–12, Filter 0–4): {'ja' if ok_format else 'NEIN'}",
            f"- Zweimal kodiert gleich einmal kodiert: {same * 100:.1f} % der Samples identisch (ein guter Kodierer findet die Blöcke wieder)",
            f"- Benutzte Filter bei einem 300-Hz-Ton (Filter: Zahl der Blöcke): {dict(sorted(used.items()))} – der Kodierer wählt je Block den Filter mit dem kleinsten Fehler\n"]

    # 2. Gauss-Tabelle
    anchors = {0x000: -1, 0x020: 3, 0x040: 39, 0x060: 156, 0x080: 424, 0x0A0: 935, 0x0C0: 1789, 0x0E0: 3079,
               0x0FF: 4807, 0x100: 4871, 0x120: 7170, 0x140: 9901, 0x160: 12905, 0x180: 15948, 0x1A0: 18746,
               0x1C0: 21004, 0x1E0: 22467, 0x1FF: 22963}
    g = P.GAUSS
    devs = {i: g[i] - v for i, v in anchors.items()}
    sums = [g[0xFF - p] + g[0x1FF - p] + g[0x100 + p] + g[p] for p in range(256)]
    out += ["## 2. Gauß-Tabelle (Annäherung)\n",
            "Stützstellen aus psx-spx, berechnete Werte aus der Näherungsformel:\n",
            "| Index | psx-spx | berechnet | Abweichung |\n|---|---|---|---|"]
    out += [f"| {i:03X}h | {v} | {g[i]} | {devs[i]:+d} |" for i, v in anchors.items()]
    out += [f"\nGrößte Abweichung: {max(abs(d) for d in devs.values())} (von 22 963). Summe der vier Gewichte je Phase: "
            f"{min(sums)}–{max(sums)}; psx-spx nennt für die echte Tabelle 32 639–32 641 (7F7Fh–7F81h).\n"]

    # 3. Frequenzgang
    freqs = [100, 500, 1000, 2000, 4000, 6000, 8000, 10000, 12000, 16000]
    out += ["## 3. Frequenzgang\n",
            "Sinuston −6 dBFS, 0,4 s; Pegel der Ausgabe relativ zur Eingabe (mittlere Hälfte gemessen), ohne Hall. "
            "„Interpolation“ = nur Umrechnen und Abspielen mit Gauß-Interpolation; „+ ADPCM“ = ganze Kette.\n",
            "| Frequenz | 22 050 Hz: Interpolation | 22 050 Hz: + ADPCM | 11 025 Hz: Interpolation | 11 025 Hz: + ADPCM |\n|---|---|---|---|---|"]
    for f in freqs:
        row = [chain_level(f, r, a) for r in (22050, 11025) for a in (False, True)]
        out.append(f"| {f} Hz | " + " | ".join("unter −60 dB" if v < -60 else f"{v:+.1f} dB" for v in row) + " |")
        print(f, row)
    out.append("\nErwartet: oberhalb der halben Rate (11 025 bzw. 5 513 Hz) nichts mehr; darunter ein sanfter Abfall der Höhen durch die Gauß-Interpolation, der zur halben Rate hin stärker wird.\n")

    # 4. Rauschabstand ADPCM
    def snr(x, rate):
        low = P.resample([x], SR, rate)[0]
        d = P.adpcm_roundtrip(low)
        err = [a - b for a, b in zip(low, d)]
        return db(rms(low) / max(1e-9, rms(err)))

    shot = P.read_wav(HERE.parent / "hoerprobe-2" / "pistole-d-hell-kurz.wav")[0][0]
    out += ["## 4. Rauschabstand der ADPCM-Stufe\n",
            "Signal geteilt durch Fehler (dekodiert minus Eingang der ADPCM-Stufe), bei gleicher Rate.\n",
            "| Signal | 22 050 Hz | 11 025 Hz |\n|---|---|---|",
            f"| Sinus 1 kHz, −6 dBFS | {snr(sine(1000), 22050):.1f} dB | {snr(sine(1000), 11025):.1f} dB |",
            f"| Sinus 1 kHz, −30 dBFS | {snr(sine(1000, amp=1036), 22050):.1f} dB | {snr(sine(1000, amp=1036), 11025):.1f} dB |",
            f"| `pistole-d-hell-kurz.wav` (Hörprobe 2) | {snr(shot, 22050):.1f} dB | {snr(shot, 11025):.1f} dB |",
            "\nZum Vergleich: 16-Bit-PCM hat theoretisch etwa 98 dB. Weil ADPCM Shift und Filter je Block anpasst, hängt der Abstand kaum vom Pegel ab, wohl aber davon, wie gut sich das Signal vorhersagen lässt (Rauschen schlecht, tiefe Töne gut).\n"]

    # 5. Hall
    out += ["## 5. Hall-Voreinstellungen\n",
            "Impulsantwort (ein Impuls 0,5 auf beide Eingänge), gemessen am linken Ausgang: Pegel in 50-ms-Fenstern; "
            "„Abklingzeit“ = Zeit, bis der Pegel 60 dB unter dem lautesten Fenster liegt (Kurve über 30 dB hochgerechnet, falls nötig).\n",
            "| Voreinstellung | Speicher | lautestes Fenster | −30 dB nach | Abklingzeit (geschätzt) | stabil |\n|---|---|---|---|---|---|"]
    for name in ("Room", "Studio Small", "Studio Medium", "Studio Large", "Hall", "Half Echo", "Space Echo"):
        imp = [0] * int(0.05 * SR)
        imp[10] = 16384
        wl, wr = P.reverb(imp, imp, name, tail_s=4.0)
        w = int(0.05 * SR)
        env = [db(rms(wl[s:s + w]) / 32768) for s in range(0, len(wl) - w, w)]
        k_max = max(range(len(env)), key=lambda k: env[k])
        t30 = next((k for k in range(k_max, len(env)) if env[k] < env[k_max] - 30), None)
        t60 = next((k for k in range(k_max, len(env)) if env[k] < env[k_max] - 60), None)
        if t60 is not None:
            rt = f"{t60 * 0.05:.2f} s"
        elif t30 is not None:
            rt = f"≈ {2 * t30 * 0.05:.2f} s"
        else:
            rt = "über 4 s"
        stable = max(abs(v) for v in wl[-w * 20:]) < 32767
        out.append(f"| {name} | {P.REVERB_PRESETS[name][0]} Byte | {k_max * 0.05:.2f} s | "
                   f"{'–' if t30 is None else f'{t30 * 0.05:.2f} s'} | {rt} | {'ja' if stable else 'NEIN'} |")
        print(name, rt)
    out.append("")
    (HERE / "ps1_pruefung.md").write_text("\n".join(out) + "\n", encoding="utf-8")
    print("ok")


if __name__ == "__main__":
    main()
