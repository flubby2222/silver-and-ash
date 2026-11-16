"""Hoerprobe 1 – misst alle WAV-Dateien und schreibt messwerte.md.

Je Datei: Laenge, Spitzenpegel und mittlere Lautstaerke (ffmpeg volumedetect),
Raender (erstes/letztes Sample, Restpegel vor der Ausblende) und ein
Spektrogramm (ffmpeg showspectrumpic) nach spektrogramme/.
Beim Ghul zusaetzlich, wann es am lautesten ist; beim Ambiente die Nahtstelle
(WAV und decodierte OGG) und der Pegel im mittleren Band 300-3000 Hz.

Aufruf: python messen.py   (nach den fuenf Erzeuger-Skripten)
"""
import math
import re
import struct
import subprocess
import sys
import tempfile
import wave
from pathlib import Path

HERE = Path(__file__).resolve().parent
SPEC = HERE / "spektrogramme"
SR = 44100

LIMITS = {"pistole": (0.0, 0.6), "klinge": (0.0, 0.6), "ghul": (0.0, 1.5),
          "schritt": (0.0, 0.3), "ambiente": (20.0, 40.0)}

BESCHREIBUNG = {
    "pistole-a-trocken": "Heller Rausch-Knall (Hochpass 900 Hz, 6 ms), Druck-Rauschen (Tiefpass 1,6 kHz, 25 ms) und ein von 110 auf 45 Hz fallender Sinus-Stoss, dazu nur zwei frühe Reflexionen (7 und 13 ms); die kürzeste und trockenste der drei.",
    "pistole-b-gewoelbe": "Derselbe Kern mit etwas mehr Tiefe, dazu ein dunkler Schroeder-Hall (RT60 0,6 s, Dämpfung 1,8 kHz), der bis 0,58 s ausklingt; unterscheidet sich durch den langen, dumpfen Raum.",
    "pistole-c-metallisch": "18 ms vor dem Schuss ein Hahn-Klick (Bandpass 3,2 kHz), danach ein kürzerer Knall mit weniger Tiefe und unharmonischen Teiltönen (2,35/3,71/5,18/6,89 kHz, verstimmtes Paar für Schwebung) in einem kurzen, hellen Raum (RT60 0,35 s); die hellste der drei.",
    "klinge-a-dumpf": "Nur der Aufprall: Tiefpass-Rauschen (420 Hz), ein von 95 auf 58 Hz fallender Körperton und ein unregelmäßig moduliertes Band-Rauschen um 700 Hz als feuchtes Quetschen; kein heller Anteil.",
    "klinge-b-schnitt": "Vorweg 70 ms Band-Rauschen, das von 7 auf 2,5 kHz gleitet (der Schnitt), dann derselbe dumpfe Aufprall mit mehr Quetschen; unterscheidet sich durch den hellen Anlauf vor dem Treffer.",
    "klinge-c-klirren": "Dumpfer Aufprall mit hellem Anschlag-Klick, darüber eine bis 0,55 s nachklingende Silberklinge aus sechs unharmonischen Teiltönen (1,87–7,55 kHz, mit Schwebung); unterscheidet sich durch das metallische Nachklingen.",
    "ghul-a-knurren-steigend": "Kehliger Impulszug (55→95 Hz, mit Tonhöhen-Zittern und jeder zweiten Periode leiser) durch tiefe Formant-Filter, 0,5 s lang um 14 dB lauter und heller werdend, bei 0,50 s ein Sprung auf 135 Hz mit Rausch-Ausstoß, dann Abfall; Zeitgeber ist das stetige Ansteigen bis zum Gipfel.",
    "ghul-b-atem-schrei": "Feuchtes Einatmen (Band-Rauschen 300→1300 Hz, gurgelnd moduliert, mit Blasen), das bei 0,48 s abreißt; nach 20 ms Stille setzt bei 0,50 s ein rauer Schrei ein (150→85 Hz, viel Hauch); Zeitgeber ist der Einsatz nach der Lücke.",
    "ghul-c-gurgeln-zaehlen": "Drei kurze, nasse Gurgel-Stöße bei 0 / 0,25 / 0,40 s (Abstände werden kürzer, Tonhöhe und Pegel steigen), der vierte und lauteste bei 0,50 s; Zeitgeber ist der Rhythmus wie beim Anzählen.",
    "schritt-a-weich": "Weiche Sohle: kurzes Tiefpass-Rauschen (um 1,1 kHz, 12 ms) mit leisem tiefem Stoß, kein Raum; die vier Schritte unterscheiden sich in Filter, Abklingzeit, Stoß-Tonhöhe (je ±10–20 %) und Pegel (bis −1,5 dB).",
    "schritt-b-stiefel": "Harte Ferse (um 2,8 kHz) mit kräftigem tiefem Stoß, 35–55 ms später die leisere Spitze, dazu verstreutes Sandknirschen über 2,5 kHz; die vier Schritte unterscheiden sich zusätzlich im Abstand Ferse–Spitze.",
    "schritt-c-hall": "Heller, kurzer Aufsatz (um 3,5 kHz, 7 ms) mit kurzem Raumhall (RT60 0,35 s), der bis 0,28 s ausklingt; die vier Schritte unterscheiden sich in Filter, Abklingzeit, Pegel und Hall-Muster.",
    "ambiente-a-grollen": "Nur die Grundfläche: zweifach tiefpassgefiltertes Rauschen (unter etwa 120 Hz), das langsam atmet (1 und 3 Perioden je Schleife), und sehr leise Luft zwischen 6,5 und 9 kHz; keine Ereignisse.",
    "ambiente-b-tropfen": "Dieselbe Grundfläche plus sieben seltene, leise Wassertropfen (3–5,5 kHz, aufsteigend, 2,5–6 s Abstand, im Stereobild verteilt) mit zwei dunkleren Echos bei 90 und 170 ms.",
    "ambiente-c-ferne-ereignisse": "Dieselbe Grundfläche plus fünf ferne Ereignisse mit langem dunklem Hall, überwiegend unter 400 Hz (die Stein-Anschläge reichen schwach bis etwa 2 kHz): zweimal Steinpoltern, ein Schaben, zweimal tiefes Wehen; das Wehen bei 27,5 s reicht über die Naht und klingt am Anfang aus.",
}

SCHRITT_FOLGE = "Hörhilfe, kein Spiel-Asset: die vier Schritte zweimal im Abstand von 0,5 s; für sie gilt die 0,3-s-Grenze nicht."


def read_wav(path):
    with wave.open(str(path), "rb") as w:
        ch, sw, sr, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        raw = w.readframes(n)
    assert sw == 2 and sr == SR, (path, sw, sr)
    data = struct.unpack("<%dh" % (n * ch), raw)
    return [[v / 32768.0 for v in data[c::ch]] for c in range(ch)], sr


def volumedetect(path, extra=None):
    af = "volumedetect" if not extra else extra + ",volumedetect"
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(path), "-af", af, "-f", "null", "-"],
                       capture_output=True, text=True, check=True)
    mx = float(re.search(r"max_volume:\s*(-?[\d.]+|-inf)", r.stderr).group(1))
    mean = float(re.search(r"mean_volume:\s*(-?[\d.]+|-inf)", r.stderr).group(1))
    return mx, mean


def spectrogram(path, png, stereo):
    size = "1200x360" if stereo else "720x320"
    mode = ":mode=separate" if stereo else ""
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(path), "-lavfi",
                    f"showspectrumpic=s={size}:legend=1:fscale=log{mode}", str(png)], check=True)


def db(x):
    return -200.0 if x <= 0 else 20.0 * math.log10(x)


def rms(xs):
    return math.sqrt(sum(v * v for v in xs) / max(1, len(xs)))


def edge_check(ch):
    """Erstes/letztes Sample, Restpegel 20-40 ms vor dem Ende relativ zur Spitze."""
    peak = max(abs(v) for v in ch)
    pre = ch[-int(0.04 * SR):-int(0.02 * SR)]
    return ch[0], ch[-1], db(rms(pre)) - db(peak)


def loudest_window(ch, win=0.01):
    w = int(win * SR)
    best, best_t = -1.0, 0.0
    for s in range(0, len(ch) - w, w // 2):
        r = rms(ch[s:s + w])
        if r > best:
            best, best_t = r, s / SR
    return best_t + win / 2


def window_db(ch, t0, t1):
    return db(rms(ch[int(t0 * SR):int(t1 * SR)]))


def seam(chs):
    """Sprung an der Nahtstelle im Vergleich zu den normalen Sample-Abstaenden."""
    rows = []
    for c, ch in enumerate(chs):
        diffs = sorted(abs(ch[i] - ch[i - 1]) for i in range(1, len(ch)))
        p999 = diffs[int(0.999 * (len(diffs) - 1))]
        jump = abs(ch[0] - ch[-1])
        half = int(0.5 * SR)
        rows.append({"kanal": "L" if c == 0 else "R", "ende": ch[-1], "anfang": ch[0], "sprung": jump,
                     "p999": p999, "max": diffs[-1],
                     "rms_ende": db(rms(ch[-half:])), "rms_anfang": db(rms(ch[:half]))})
    return rows


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    SPEC.mkdir(exist_ok=True)
    wavs = sorted(HERE.glob("*.wav"))
    lines = []
    checks = []
    seam_lines = []
    ghul_lines = []
    order = ["pistole", "klinge", "ghul", "schritt", "ambiente"]
    wavs.sort(key=lambda p: (order.index(p.stem.split("-")[0]), p.stem))
    current = None
    for p in wavs:
        stem = p.stem
        kind = stem.split("-")[0]
        chs, sr = read_wav(p)
        length = len(chs[0]) / sr
        mx, mean = volumedetect(p)
        png = SPEC / f"{stem}.png"
        spectrogram(p, png, len(chs) == 2)
        folge = stem.endswith("-folge")
        group = stem.rsplit("-", 1)[0] if kind == "schritt" else stem
        if kind != current:
            titles = {"pistole": "Pistolenschuss", "klinge": "Klingentreffer", "ghul": "Ghul-Laut",
                      "schritt": "Schritt auf Stein", "ambiente": "Katakomben-Ambiente"}
            lines.append(f"\n## {titles[kind]}\n")
            current = kind
        if kind == "schritt" and (stem.endswith("-1") or folge):
            if stem.endswith("-1"):
                lines.append(f"\n### {group}\n\n{BESCHREIBUNG[group]}\n")
                lines.append("| Datei | Länge | Spitze | Mittel | Spektrogramm |\n|---|---|---|---|---|")
        elif kind != "schritt":
            lines.append(f"\n### {stem}\n\n{BESCHREIBUNG[stem]}\n")
            lines.append("| Datei | Länge | Spitze | Mittel | Spektrogramm |\n|---|---|---|---|---|")
        if folge:
            lines.append(f"\n{SCHRITT_FOLGE}\n")
            lines.append("| Datei | Länge | Spitze | Mittel | Spektrogramm |\n|---|---|---|---|---|")
        lines.append(f"| `{p.name}` | {length:.3f} s | {mx:.1f} dBFS | {mean:.1f} dBFS | "
                     f"![{stem}](spektrogramme/{stem}.png) |")

        # Pruefung gegen den technischen Rahmen
        lo, hi = LIMITS[kind]
        if folge:
            len_ok, len_txt = True, "Hörhilfe, keine Grenze"
        elif kind == "ambiente":
            len_ok, len_txt = lo <= length <= hi, f"{lo:.0f}–{hi:.0f} s"
        else:
            len_ok, len_txt = length < hi, f"< {hi} s"
        peak_ok = mx <= -1.0
        if kind == "ambiente":
            first, last, tail = chs[0][0], chs[0][-1], None
            edge_ok, edge_txt = True, "Schleife, siehe Naht"
        else:
            first, last, tail = edge_check(chs[0])
            edge_ok = first == 0.0 and last == 0.0 and tail < -40.0
            tail_txt = "digitale Stille" if tail < -120 else f"{tail:.0f} dB"
            edge_txt = f"erstes/letztes Sample 0/0, Rest vor Blende {tail_txt}" if first == 0.0 and last == 0.0 \
                else f"erstes {first:.5f}, letztes {last:.5f}, Rest {tail:.0f} dB"
        ok = len_ok and peak_ok and edge_ok
        checks.append(f"| `{p.name}` | {length:.3f} s ({len_txt}) {'✔' if len_ok else '✘'} | "
                      f"{mx:.1f} dBFS {'✔' if peak_ok else '✘'} | {edge_txt} {'✔' if edge_ok else '✘'} | "
                      f"{'bestanden' if ok else 'NICHT bestanden'} |")

        if kind == "ghul":
            ch = chs[0]
            t_max = loudest_window(ch)
            ghul_lines.append(f"| `{p.name}` | {t_max:.3f} s | {window_db(ch, 0.40, 0.48):.1f} dBFS | "
                              f"{window_db(ch, 0.50, 0.56):.1f} dBFS |")

        if kind == "ambiente":
            mid_max, mid_mean = volumedetect(p, "highpass=f=300,highpass=f=300,lowpass=f=3000,lowpass=f=3000")
            low_max, low_mean = volumedetect(p, "lowpass=f=300,lowpass=f=300")
            ogg = p.with_suffix(".ogg")
            with tempfile.TemporaryDirectory() as td:
                dec = Path(td) / "dec.wav"
                subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(ogg), "-c:a", "pcm_s16le", str(dec)],
                               check=True)
                ochs, _ = read_wav(dec)
            for src, cc in (("WAV", chs), ("OGG", ochs)):
                for r in seam(cc):
                    seam_lines.append(
                        f"| `{p.stem}` {src} | {r['kanal']} | {len(cc[0]) / SR:.3f} s | {r['ende']:+.5f} → {r['anfang']:+.5f} | "
                        f"{r['sprung']:.5f} | {r['p999']:.5f} | {r['max']:.5f} | "
                        f"{r['rms_ende']:.1f} / {r['rms_anfang']:.1f} dBFS |")
            seam_lines.append(f"| `{p.stem}` Bänder | – | – | Mitte 300–3000 Hz: Mittel {mid_mean:.1f} dBFS | "
                              f"Tiefen < 300 Hz: Mittel {low_mean:.1f} dBFS | gesamt {mean:.1f} dBFS | – | – |")

    out = []
    out.append("# Messwerte – Hörprobe 1\n")
    out.append("Erzeugt mit `python messen.py` (Python-Standardbibliothek und `ffmpeg`). "
               "Spitze und Mittel stammen aus `ffmpeg -af volumedetect`, die Spektrogramme aus "
               "`ffmpeg -lavfi showspectrumpic` (Zeit nach rechts, Frequenz logarithmisch nach oben). "
               "Alle Dateien: WAV, 16 Bit, 44 100 Hz; Geräusche mono, Ambiente stereo und zusätzlich als OGG.\n")
    out.append("Diese Datei beschreibt nur Aufbau und Messwerte. Ob etwas gut klingt, entscheidet die Hörprobe.\n")
    out.append("## Prüfung gegen den technischen Rahmen der Audio Bible\n")
    out.append("Spitzenpegel höchstens −1 dBFS; Anfang und Ende: erstes und letztes Sample exakt 0, "
               "und 20–40 ms vor dem Ende liegt der Pegel mindestens 40 dB unter der Spitze "
               "(die Ausblende schneidet also nichts Lautes ab).\n")
    out.append("| Datei | Länge | Spitze | Ränder | Ergebnis |\n|---|---|---|---|---|")
    out += checks
    out.append("\n### Ghul: Wann ist der Laut am lautesten?\n")
    out.append("Der Schlag kommt bei 0,50 s. Gemessen: Mitte des lautesten 10-ms-Fensters, "
               "mittlerer Pegel kurz davor (0,40–0,48 s) und direkt danach (0,50–0,56 s).\n")
    out.append("| Datei | lautestes Fenster | 0,40–0,48 s | 0,50–0,56 s |\n|---|---|---|---|")
    out += ghul_lines
    out.append("\n### Ambiente: Nahtstelle und Frequenzbänder\n")
    out.append("Beim Wiederholen folgt auf das letzte Sample wieder das erste. *Sprung* ist der Abstand "
               "dieser beiden Werte; zum Vergleich der Abstand zweier Nachbar-Samples, den 99,9 % "
               "aller Stellen im Stück nicht überschreiten, und der größte überhaupt. Ist der Sprung "
               "kleiner als diese Werte, unterscheidet sich die Naht nicht von einer beliebigen Stelle "
               "im Stück. Dazu die mittlere Lautstärke der letzten und ersten 0,5 s. Die OGG-Datei wurde "
               "dafür mit `ffmpeg` zurück in WAV decodiert. Die Band-Zeile misst mit je zwei Hoch- und "
               "Tiefpässen von `ffmpeg`, wie viel Pegel in der Mitte des Hörbereichs liegt.\n")
    out.append("| Datei | Kanal | Länge | letztes → erstes Sample | Sprung | 99,9 % | größter | Mittel Ende / Anfang |\n"
               "|---|---|---|---|---|---|---|---|")
    out += seam_lines
    out.append("\n## Dateien im Einzelnen")
    out += lines
    (HERE / "messwerte.md").write_text("\n".join(out) + "\n", encoding="utf-8")
    print("\n".join(checks))
    print("\n".join(ghul_lines))
    print("\n".join(seam_lines))


if __name__ == "__main__":
    main()
