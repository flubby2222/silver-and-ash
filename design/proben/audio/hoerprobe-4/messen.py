"""Hoerprobe 4 – misst alle WAV-Dateien und schreibt messwerte.md.

- technischer Rahmen (Laenge, Spitze, Raender; Ambiente: Naht in WAV und OGG),
- Tropfen: Abklingzeit des Tons (einzelner Tropfen ohne Flaeche) und des ganzen Ereignisses,
  zum Vergleich der Tropfen aus Hoerprobe 3; Echo-Muster des alten Hoehlenhalls,
- Klinge: Pegel in den Baendern 2-8 kHz und ueber 8 kHz – sauber, PS1 ohne und mit Ausgleich,
  zum Vergleich Hoerprobe 3.

Aufruf: python messen.py
"""
import importlib.util
import math
import random
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
HP1 = HERE.parent / "hoerprobe-1"
HP3 = HERE.parent / "hoerprobe-3"
sys.path.insert(0, str(HP1))
sys.path.insert(0, str(HP3))
sys.path.insert(0, str(HERE))

spec = importlib.util.spec_from_file_location("messen_hp1", HP1 / "messen.py")
m1 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m1)

import klinge as K  # noqa: E402
import tropfen as T  # noqa: E402
from synth import svf  # noqa: E402

SR = 44100

TEXT = {
    "tropfen": "Tropfen allein, ohne Fläche: vier Tropfen im Abstand von 0,8 s (Hörhilfe, kein Spiel-Asset).",
    "ambiente": "Grundfläche und sieben Tropfen wie Hörprobe 3; nur der Tropfen ist neu gebaut (siehe `tropfen.py`). Durch die PS1-Kette (22 050 Hz, ohne Hall), kreisförmig verarbeitet.",
    "klinge": "Drei Fassungen zu je drei Treffern, durch die PS1-Kette mit Höhen-Ausgleich davor (siehe `klinge.py`).",
}


def band_db(x, lo=None, hi=None):
    y = x
    if lo:
        y = svf(svf(y, lo, 0.707, "hp"), lo, 0.707, "hp")
    if hi:
        y = svf(svf(y, hi, 0.707, "lp"), hi, 0.707, "lp")
    return 10 * math.log10(max(1e-20, sum(v * v for v in y) / len(y)))


def decay_times(x, win=0.005):
    """Ab dem lautesten Fenster: Zeit bis -20, -40, -60 dB (5-ms-Fenster)."""
    w = int(win * SR)
    env = [m1.db(m1.rms(x[s:s + w])) for s in range(0, len(x) - w, w)]
    k0 = max(range(len(env)), key=lambda k: env[k])
    res = []
    for th in (20, 40, 60):
        k = next((k for k in range(k0, len(env)) if all(v < env[k0] - th for v in env[k:])), None)
        res.append("–" if k is None else f"{(k - k0) * win * 1000:.0f} ms")
    return res


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    (HERE / "spektrogramme").mkdir(exist_ok=True)
    checks, details = [], []
    order = ["tropfen", "ambiente", "klinge"]
    wavs = sorted(HERE.glob("*.wav"), key=lambda p: (order.index(p.stem.split("-")[0]), p.stem))
    current = None
    for p in wavs:
        stem, kind = p.stem, p.stem.split("-")[0]
        chs, sr = m1.read_wav(p)
        length = len(chs[0]) / sr
        mx, mean = m1.volumedetect(p)
        m1.spectrogram(p, HERE / "spektrogramme" / f"{stem}.png", len(chs) == 2)
        if kind != current:
            details.append(f"\n## {kind.capitalize()}\n\n{TEXT[kind]}\n\n| Datei | Länge | Spitze | Mittel | Spektrogramm |\n|---|---|---|---|---|")
            current = kind
        details.append(f"| `{p.name}` | {length:.3f} s | {mx:.1f} dBFS | {mean:.1f} dBFS | ![{stem}](spektrogramme/{stem}.png) |")
        peak_ok = mx <= -1.0
        if kind == "ambiente":
            len_ok, len_txt = 20 <= length <= 40, "20–40 s"
            with tempfile.TemporaryDirectory() as td:
                dec = Path(td) / "d.wav"
                subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(p.with_suffix(".ogg")),
                                "-c:a", "pcm_s16le", str(dec)], check=True)
                ochs, _ = m1.read_wav(dec)
            parts, edge_ok = [], True
            for src, cc in (("WAV", chs), ("OGG", ochs)):
                for r in m1.seam(cc):
                    good = r["sprung"] <= r["p999"]
                    edge_ok &= good
                    parts.append(f"{src} {r['kanal']} {r['sprung']:.5f} {'≤' if good else '>'} {r['p999']:.5f}")
            edge_txt = "Naht: " + "; ".join(parts)
        else:
            len_ok, len_txt = (True, "Hörhilfe") if kind == "tropfen" else (length < 0.6, "< 0.6 s")
            first, last, tail = m1.edge_check(chs[0])
            edge_ok = first == 0.0 and last == 0.0 and tail < -40.0
            edge_txt = f"erstes/letztes Sample {first:g}/{last:g}, Rest vor Blende " + \
                ("digitale Stille" if tail < -120 else f"{tail:.0f} dB")
        ok = len_ok and peak_ok and edge_ok
        checks.append(f"| `{p.name}` | {length:.3f} s ({len_txt}) {'✔' if len_ok else '✘'} | {mx:.1f} dBFS "
                      f"{'✔' if peak_ok else '✘'} | {edge_txt} {'✔' if edge_ok else '✘'} | {'bestanden' if ok else 'NICHT bestanden'} |")

    # Tropfen: Abklingzeiten (sauber, ein Tropfen mit fester Zufallsfolge)
    drip_rows = []
    for name, fn in [("Hörprobe 3 (reiner Ton + Höhlenhall)", T.hp3.drip)] + \
                    [(f"{k}", f) for k, f in T.VARIANTS.items()]:
        x = fn(random.Random(5))
        x = x + [0.0] * int(0.05 * SR)
        drip_rows.append(f"| {name} | " + " | ".join(decay_times(x)) + " |")
    imp = [0.0] * 10
    imp[0] = 1.0
    cave = T.hp3.cave(imp, tail=0.5)
    echoes = sorted({round(i / SR * 1000) for i, v in enumerate(cave) if abs(v) > 0.1})

    # Klinge: Baender
    kl_rows = []
    ref = m1.read_wav(HP3 / "klinge-sauber-1.wav")[0][0]
    ref_ps1 = m1.read_wav(HP3 / "klinge-ps1-1.wav")[0][0]
    for label, sig in (("Hörprobe 3, sauber", ref), ("Hörprobe 3, PS1", ref_ps1)):
        s = sig
        kl_rows.append(f"| {label} | {band_db(s):.1f} | {band_db(s, 2000, 8000):.1f} | {band_db(s, 8000):.1f} |")
    for key, fn in K.VARIANTS.items():
        c = fn(1)
        for label, s in (("sauber", c), ("PS1 ohne Ausgleich", K.ps1(c, False)), ("PS1 mit Ausgleich (Datei)", K.ps1(c, True))):
            kl_rows.append(f"| {key}, {label} | {band_db(s):.1f} | {band_db(s, 2000, 8000):.1f} | {band_db(s, 8000):.1f} |")

    out = ["# Messwerte – Hörprobe 4\n",
           "Erzeugt mit `python messen.py`. Spitze und Mittel aus `ffmpeg -af volumedetect`, Spektrogramme aus "
           "`ffmpeg -lavfi showspectrumpic`. Alle Dateien sind durch die PS1-Kette aus Hörprobe 3 gegangen "
           "(`../hoerprobe-3/ps1_klang.py`, 22 050 Hz, ohne Hall). Die Fläche des Ambientes ist unverändert; "
           "Regel 6 ist dafür in Hörprobe 3 gemessen (38,5 dB Abstand).\n",
           "Diese Datei beschreibt nur Aufbau und Messwerte. Ob etwas gut klingt, entscheidet die Hörprobe.\n",
           "## Prüfung gegen den technischen Rahmen\n",
           "| Datei | Länge | Spitze | Ränder | Ergebnis |\n|---|---|---|---|---|"] + checks
    out += ["\n## Tropfen: Ursache des Klingelns und Abklingzeit\n",
            f"Der Höhlenhall aus Hörprobe 3 antwortet auf einen Impuls mit einzelnen, deutlich getrennten Echos "
            f"(über −20 dB) bei {', '.join(str(e) for e in echoes)} ms und kreist über 1 s. Ein reiner Sinus, der so "
            f"zurückgeworfen wird, klingt als Ton weiter.\n",
            "Abklingzeit eines einzelnen sauberen Tropfens (mit Raum), ab dem lautesten 5-ms-Fenster bis der Pegel "
            "dauerhaft 20, 40 und 60 dB darunter liegt:\n",
            "| Tropfen | −20 dB | −40 dB | −60 dB |\n|---|---|---|---|"] + drip_rows
    out += ["\n## Klinge: Höhen und PS1-Ausgleich\n",
            "Mittlerer Pegel (dBFS) gesamt, im Band 2–8 kHz und über 8 kHz, je Treffer 1. Der Ausgleich hebt vor "
            "der PS1-Kette die Höhen über etwa 3 kHz an; über 11 kHz kann er nichts zurückholen, weil die Kette "
            "dort alles abschneidet.\n",
            "| Signal | gesamt | 2–8 kHz | über 8 kHz |\n|---|---|---|---|"] + kl_rows
    out += ["\n## Dateien im Einzelnen"] + details
    (HERE / "messwerte.md").write_text("\n".join(out) + "\n", encoding="utf-8")
    print("\n".join(checks + drip_rows + kl_rows))
    print(echoes)


if __name__ == "__main__":
    main()
