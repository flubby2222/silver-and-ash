"""Hoerprobe 6 – prueft die Skizzen nach Regeln und schreibt messwerte.md.

Geprueft wird (alles ohne Gehoer):
  Rahmen      Laenge, Spitze (<= -1 dBFS), erstes/letztes Sample 0, Ausklang, Lautheit (ffmpeg ebur128)
  Tonleiter   Notenliste: alle Noten in der Tonleiter, Tonumfang; Audio: Energie je Tonklasse (Goertzel auf der
              fertigen Datei, 11 025 Hz, C1-H6), Anteil ausserhalb der Tonleiter, staerkste Tonklasse = Grundton?
  Stimmfuehrung  Streicher/Chor: groesster Sprung je Stimme, Stimmkreuzungen, parallele Quinten/Oktaven
  Spektrum    Pegel je Band (< 120, 120-300, 300-3000, > 3000 Hz) und der Abstand der Mitte zum Gesamtpegel
              (Vorschlag aus dem Konzept: mindestens 15 dB darunter)
  Dichte      Noten je Takt je Spur
  Tempo       aus dem Skript
Aufruf: python messen.py   (nach skizze_a.py und skizze_b.py)
"""
import math
import re
import struct
import subprocess
import sys
import wave
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parent / "hoerprobe-1"))

import skizze_a as A  # noqa: E402
import skizze_b as B  # noqa: E402
from synth import svf  # noqa: E402

SR = 44100
PC = ["C", "Cis", "D", "Es", "E", "F", "Fis", "G", "As", "A", "B", "H"]


def read(path):
    with wave.open(str(path), "rb") as w:
        n = w.getnframes()
        d = struct.unpack("<%dh" % n, w.readframes(n))
    return [v / 32768 for v in d]


def vol(path, extra=None):
    af = "volumedetect" if not extra else extra + ",volumedetect"
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(path), "-af", af, "-f", "null", "-"],
                       capture_output=True, text=True, check=True)
    return float(re.search(r"max_volume:\s*(-?[\d.]+)", r.stderr).group(1)), float(re.search(r"mean_volume:\s*(-?[\d.]+)", r.stderr).group(1))


def lufs(path):
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(path), "-af", "ebur128", "-f", "null", "-"],
                       capture_output=True, text=True, check=True)
    return float(re.findall(r"I:\s*(-?[\d.]+) LUFS", r.stderr)[-1])


def decimate(x, f):
    """Mittelwert ueber f Samples nach einem Tiefpass (grob genug fuer Tonklassen bis H6)."""
    y = svf(x, 0.4 * SR / f, 0.707, "lp")
    return [sum(y[i:i + f]) / f for i in range(0, len(y) - f + 1, f)]


def goertzel(x, freq, sr):
    k = 2.0 * math.cos(2.0 * math.pi * freq / sr)
    s1 = s2 = 0.0
    for v in x:
        s0 = v + k * s1 - s2
        s2, s1 = s1, s0
    return s1 * s1 + s2 * s2 - k * s1 * s2


def chroma(x):
    """Energie je Tonklasse aus den Toenen C1-H6 (Obertoene mitgezaehlt, deshalb nie null ausserhalb)."""
    f = 4
    d = decimate(x, f)
    sr = SR // f
    e = [0.0] * 12
    for m in range(24, 96):
        e[m % 12] += goertzel(d, 440.0 * 2 ** ((m - 69) / 12), sr)
    t = sum(e)
    return [v / t for v in e]


def band_db(x, lo=None, hi=None):
    y = x
    if lo:
        y = svf(svf(y, lo, 0.707, "hp"), lo, 0.707, "hp")
    if hi:
        y = svf(svf(y, hi, 0.707, "lp"), hi, 0.707, "lp")
    return 10 * math.log10(max(1e-20, sum(v * v for v in y) / len(y)))


def voices_of(track_notes):
    """Gruppiert gleichzeitig beginnende Noten zu Akkorden (nach Beginn), je Akkord aufsteigend sortiert."""
    chords = {}
    for s, d, m, v in track_notes:
        chords.setdefault(s, []).append(m)
    return [sorted(chords[k]) for k in sorted(chords)]


def leading(chords):
    """Groesster Sprung je Stimme, Kreuzungen (Stimme i tiefer als i+1 im naechsten Akkord), parallele 5/8."""
    leaps, par, cross = [], 0, 0
    for a, b in zip(chords, chords[1:]):
        if len(a) != len(b):
            continue
        leaps.append(max(abs(y - x) for x, y in zip(a, b)))
        cross += sum(1 for i in range(len(b) - 1) if b[i] >= b[i + 1])
        for i in range(len(a)):
            for j in range(i + 1, len(a)):
                ia, ib = (a[j] - a[i]) % 12, (b[j] - b[i]) % 12
                if ia == ib and ia in (0, 7) and a[i] != b[i] and (b[i] - a[i]) * (b[j] - a[j]) > 0:
                    par += 1
    return (max(leaps) if leaps else 0), cross, par


def analyse(key, mod):
    path = HERE / f"{key}.wav"
    x = read(path)
    n = len(x)
    mx, mean = vol(path)
    l = lufs(path)
    w = int(0.02 * SR)
    peak = max(abs(v) for v in x)
    rest = 20 * math.log10(max(1e-9, math.sqrt(sum(v * v for v in x[-2 * w:-w]) / w) / peak))
    bands = [band_db(x, None, 120), band_db(x, 120, 300), band_db(x, 300, 3000), band_db(x, 3000)]
    total = 10 * math.log10(sum(v * v for v in x) / n)
    ch = chroma(x)
    out_energy = sum(ch[i] for i in range(12) if i not in mod.SCALE)
    top = max(range(12), key=lambda i: ch[i])
    notes_ok, rng = [], []
    for name, notes, _ in mod.TRACKS:
        if name == "Herzschlag":
            continue
        bad = [m for _, _, m, _ in notes if m % 12 not in mod.SCALE]
        notes_ok.append((name, len(notes), len(bad)))
        rng.append((name, min(m for *_, m, _ in [(0, 0, m, 0) for _, _, m, _ in notes]), max(m for _, _, m, _ in notes)))
    pad = next(n for n in ("Streicher", "Chor") if any(t[0] == n for t in mod.TRACKS))
    pad_notes = next(t[1] for t in mod.TRACKS if t[0] == pad)
    leap, cross, par = leading(voices_of(pad_notes))
    beats = mod.BEATS
    dens = [(name, len(notes) / (beats / 4)) for name, notes, _ in mod.TRACKS]
    return dict(key=key, n=n, mx=mx, mean=mean, lufs=l, first=x[0], last=x[-1], rest=rest, bands=bands, total=total,
                ch=ch, out_energy=out_energy, top=top, notes_ok=notes_ok, rng=rng, pad=pad, leap=leap, cross=cross,
                par=par, dens=dens, mod=mod)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    res = [analyse("a-gruft-orgel", A), analyse("b-glocken-chor", B)]
    out = ["# Messwerte – Hörprobe 6 (Stimmungsskizzen)\n",
           "Erzeugt mit `python messen.py`. Geprüft nach Regeln, **nichts gehört**. Spitze und Mittel aus `ffmpeg volumedetect`, "
           "Lautheit aus `ffmpeg ebur128`, Tonklassen aus einer eigenen Goertzel-Analyse der fertigen Datei (C1–H6).\n",
           "## Rahmen\n",
           "| Datei | Länge | Spitze | Mittel | Lautheit | erstes / letztes Sample | Rest vor Ende | Ergebnis |\n|---|---|---|---|---|---|---|---|"]
    for r in res:
        ok = r["mx"] <= -1.0 and r["first"] == 0.0 and r["last"] == 0.0 and r["rest"] < -40
        out.append(f"| `{r['key']}.wav` | {r['n'] / SR:.1f} s | {r['mx']:.1f} dBFS | {r['mean']:.1f} dBFS | {r['lufs']:.1f} LUFS | "
                   f"{r['first']:g} / {r['last']:g} | {r['rest']:.0f} dB | {'bestanden' if ok else 'NICHT bestanden'} |")
    out += ["\nSkizzen sind Hörproben, keine Schleifen; sie enden in Stille (kein Nahtkriterium). 22 050 Hz PS1-Kette, mono.\n",
            "## Tonleiter und Grundton\n"]
    for r in res:
        m = r["mod"]
        out.append(f"**{r['key']}** – Tonleiter {', '.join(PC[i] for i in sorted(m.SCALE, key=lambda i: (i - m.TONIC) % 12))}, Grundton {PC[m.TONIC]}.\n")
        out.append("- Notenliste: " + "; ".join(f"{n}: {c} Noten, {b} außerhalb" for n, c, b in r["notes_ok"]) + ".")
        out.append("- Tonumfang (MIDI): " + "; ".join(f"{n} {lo}–{hi}" for n, lo, hi in r["rng"]) + ".")
        out.append(f"- Audio: Energie außerhalb der Tonleiter **{r['out_energy'] * 100:.1f} %** (Obertöne der Säge- und Impulsklänge zählen mit, "
                   f"null ist darum nicht zu erwarten); stärkste Tonklasse **{PC[r['top']]}** ({r['ch'][r['top']] * 100:.0f} %) – "
                   f"{'= Grundton' if r['top'] == m.TONIC else 'NICHT der Grundton'}.")
        out.append("- Tonklassen (Anteil in %): " + ", ".join(f"{PC[i]} {r['ch'][i] * 100:.0f}" for i in range(12)) + "\n")
    out += ["## Stimmführung (vierstimmiges Streicher- bzw. Chor-Pad)\n",
            "| Skizze | Spur | größter Sprung einer Stimme | Stimmkreuzungen | parallele Quinten/Oktaven |\n|---|---|---|---|---|"]
    for r in res:
        out.append(f"| {r['key']} | {r['pad']} | {r['leap']} Halbtöne | {r['cross']} | {r['par']} |")
    out += ["\n## Spektrum und Pegel\n",
            "Mittlerer Pegel je Band (dBFS, je zwei Filter 2. Ordnung) und Abstand der Mitte (300–3 000 Hz) zum Gesamtpegel. "
            "Vorschlag aus dem Konzept: Mitte mindestens 15 dB unter dem Gesamtpegel (damit Ankündigungen frei bleiben).\n",
            "| Skizze | < 120 Hz | 120–300 | 300–3 000 | > 3 000 | gesamt | Abstand Mitte | Vorschlag erfüllt? |\n|---|---|---|---|---|---|---|---|"]
    for r in res:
        gap = r["bands"][2] - r["total"]
        out.append(f"| {r['key']} | {r['bands'][0]:.0f} | {r['bands'][1]:.0f} | {r['bands'][2]:.0f} | {r['bands'][3]:.0f} | "
                   f"{r['total']:.0f} | {gap:.1f} dB | {'ja' if gap <= -15 else 'NEIN'} |")
    gaps = ", ".join(f"{r['key'][0]}: {r['bands'][2] - r['total']:.1f} dB" for r in res)
    out += [f"\n**Der Vorschlag wird verfehlt** ({gaps}). Die Mittelspuren (Streicher, Orgel-Gang bzw. Chor, Glocke) habe ich schon abgesenkt; "
            "weiter absenken hieße, beide Skizzen auf Bass und Hall zu reduzieren. Ob 15 dB das richtige Maß ist, zeigt erst der Test im Spiel mit "
            "den Geräuschen (wird eine Ankündigung verdeckt?); das ist Sache seines Ohres. Beide Skizzen sind auf gleiche Lautheit "
            "(LUFS, siehe oben) eingestellt, damit der a/b-Vergleich nicht an der Lautstärke hängt.",
            "\n## Dichte und Tempo\n",
            "| Skizze | Tempo | Takte | Noten je Takt |\n|---|---|---|---|"]
    for r in res:
        out.append(f"| {r['key']} | {r['mod'].BPM} Schläge/min | {r['mod'].BEATS // 4} | " + ", ".join(f"{n} {d:.1f}" for n, d in r["dens"]) + " |")
    out += ["\n## Was ich nicht prüfen konnte\n",
            "- Ob es **düster** klingt statt nur dunkel, ob die Akkordfolge trägt, ob die Stimmung zu den Katakomben passt.",
            "- Ob die Orgel nach Orgel klingt und die Streicher nicht nach Sägezahn; ob Glocke und Chor glaubhaft sind. Ich habe dafür nur "
            "Regeln angewandt (mehrere leicht verstimmte Stimmen, Register in Oktaven und Quinten, langsame Einschwingzeiten, "
            "wandernde Filter, unharmonische Glocken-Teiltöne, Vokalformanten) und nichts davon gehört.",
            "- Die Mischung (Pegel der Spuren zueinander und zum Hall) und ob der Hall zu lang oder zu kurz ist.",
            "- Ob die Skizze als Spielmusik funktioniert (Länge der Schleife, Wiederholung, Zusammenspiel mit Geräuschen); sie ist keine Schleife."]
    (HERE / "messwerte.md").write_text("\n".join(out) + "\n", encoding="utf-8")
    print("\n".join(out))


if __name__ == "__main__":
    main()
