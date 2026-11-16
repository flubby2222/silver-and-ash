"""Hoerprobe 2 – misst alle WAV-Dateien und schreibt messwerte.md.

Nutzt die Messfunktionen aus Hoerprobe 1 (ffmpeg volumedetect und
showspectrumpic, Randpruefung, Nahtstelle). Neu:
- Ghul: Zeitpunkt des Gipfels, Pegel nach dem Schlag und die Zahl der Einsaetze
  vor dem Schlag (Mass fuer "mehrere Ereignisse"), zum Vergleich auch fuer Hoerprobe 1.
- Ambiente: Vergleich mit der Vorlage aus Hoerprobe 1 (nur die Tropfen duerfen
  sich unterscheiden) und integrierte Lautheit in LUFS (ffmpeg ebur128).

Aufruf: python messen.py   (nach den Erzeuger-Skripten)
"""
import importlib.util
import math
import re
import subprocess
import sys

from gemeinsam import HERE, HP1

spec = importlib.util.spec_from_file_location("messen_hp1", HP1 / "messen.py")
m1 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m1)

SR = 44100
SPEC = HERE / "spektrogramme"
HIT = 0.50

LIMITS = {"pistole": 0.6, "klinge": 0.6, "ghul": 1.5, "schritt": 0.3}

BESCHREIBUNG = {
    "ambiente-b1-quinte": "Vorlage ambiente-b-tropfen aus Hörprobe 1, Tropfen eine Quinte tiefer (Faktor 2/3): Grundton etwa 2,0–3,6 kHz statt 3,0–5,5 kHz; sonst unverändert.",
    "ambiente-b2-oktave": "Wie b1, Tropfen eine Oktave tiefer (Faktor 1/2): etwa 1,5–2,7 kHz.",
    "ambiente-b3-anderthalb-oktaven": "Wie b1, Tropfen anderthalb Oktaven tiefer (Faktor 0,354): etwa 1,1–1,9 kHz – nach der Minnaert-Formel entspräche das einer Luftblase von etwa 2–3 mm Radius.",
    "ghul-d-anschwellen": "**Geste:** Eine tiefe, raue Stimme auf gleichbleibendem Ton (58 Hz, jede zweite Periode leiser) schwillt in 0,5 s gleichmäßig um 30 dB an und bricht beim Schlag in 40 ms ab. Zwei Schichten: Stimme und leiser Atem durch denselben festen Rachen (Formanten 380/700 Hz).",
    "ghul-e-fauchen": "**Geste:** Ein einziges Fauchen (Rauschen durch zwei Rachen-Formanten, die von 450 auf 900 Hz wandern) schwillt in 0,5 s um 30 dB an, wird dabei heller und bricht beim Schlag in 40 ms ab. Eine Schicht, keine Stimme.",
    "ghul-f-aufsteigen": "**Geste:** Eine raue Stimme gleitet in 0,5 s gleichmäßig von 45 auf 110 Hz und erreicht beim Schlag den höchsten Ton, danach klingt sie in etwa 0,1 s ab. Eine Schicht, dunkler Vokal (Formanten 320/620 Hz), nur 12 dB Anstieg – hier trägt die Tonhöhe die Zeit, nicht die Lautstärke.",
    "pistole-d-hell-kurz": "Drei Schichten: Anschlag als Druckpuls (1,2 ms, Friedlander-Form) mit 3 ms Hochpass-Rauschen; Körper aus Tiefpass-Rauschen 1,4 kHz (22 ms) und Stoß 100→45 Hz; Ausklang aus drei kalten frühen Reflexionen und kurzem Hall (RT60 0,35 s). Heller und kürzer als e.",
    "pistole-d-hell-kurz-22k": "Zusatz zur Frage nach dem Klang der Ära: genau Variante d, aber mit `ffmpeg` auf 22 050 Hz herunter- und wieder hochgerechnet, sodass oberhalb von etwa 11 kHz nichts mehr ist – wie bei einer mit 22 kHz abgelegten Datei. Keine Nachbildung der ADPCM-Körnung.",
    "pistole-e-dunkel-schwer": "Drei Schichten: weicherer Druckpuls (2,5 ms) ohne Rausch-Klick; breiter Körper aus Tiefpass-Rauschen 800 Hz (40 ms) und Stoß 80→38 Hz; dunkler Gewölbe-Hall (RT60 0,5 s, Dämpfung 1,5 kHz). Tiefer, weicher und länger als d.",
    "vergleich-pistole-a-trocken": "Unverändert aus Hörprobe 1. Kommt den neuen Regeln am nächsten: ein Ereignis, Knall + Druck + Stoß, nur zwei frühe Reflexionen.",
    "klinge-d-kurz-dumpf": "Zwei Schichten, kein Ausklang: leiser Kanten-Klick (1,5 ms) und dumpfer Schlag aus Tiefpass-Rauschen 350 Hz (25 ms) mit Körperton 110→60 Hz. Die kürzeste Variante (0,24 s).",
    "klinge-e-schwer-nass": "Drei Schichten: kaum hörbarer Klick, tieferer Schlag (Tiefpass 250 Hz, 40 ms, Körperton 80→45 Hz) und als Ausklang ein einziges kurzes, nasses Nachgeben (Bandrauschen 500 Hz, 70 ms, ohne Modulation).",
    "vergleich-klinge-a-dumpf": "Unverändert aus Hörprobe 1. Kommt den Regeln am nächsten: nur der Aufprall; das Quetschen dort ist allerdings unregelmäßig moduliert.",
    "schritt-d-klar": "Drei Schichten: Klick (Hochpass 3 kHz, 1 ms), Sohle (Tiefpass-Rauschen um 1,3 kHz, 9 ms) mit Stoß um 80 Hz, zwei kalte Reflexionen (4 und 9 ms). Vier Schritte, verschieden in Filter, Abklingzeit, Stoß-Tonhöhe und Pegel.",
    "schritt-e-schwer": "Drei Schichten: kaum Klick, dunkle Sohle (Tiefpass um 700 Hz, 14 ms) mit kräftigem Stoß um 60 Hz, zwei dunklere Reflexionen (6 und 13 ms). Vier Schritte wie bei d variiert.",
    "vergleich-schritt-a-weich": "Unverändert aus Hörprobe 1. Kommt den Regeln am nächsten: ein einzelner Aufsatz ohne Ferse/Spitze-Folge und ohne Knirschen.",
}
FOLGE = "Hörhilfe, kein Spiel-Asset: die vier Schritte zweimal im Abstand von 0,5 s; für sie gilt die 0,3-s-Grenze nicht."


def kind_of(stem):
    return stem.replace("vergleich-", "").split("-")[0]


def env_db(ch, win=0.04, hop=0.01):
    w, h = int(win * SR), int(hop * SR)
    out = []
    for s in range(0, len(ch) - w, h):
        out.append((s / SR + win / 2, m1.db(m1.rms(ch[s:s + w]))))
    return out


def onsets(ch, until=HIT + 0.02):
    """Zahl der Einsaetze bis kurz nach dem Schlag: Stellen, an denen der Pegel
    (50-ms-Fenster, alle 10 ms) gegenueber 50 ms vorher um mehr als 8 dB steigt.
    Vor dem Laut gilt der Pegel als Stille (60 dB unter der Spitze), der Beginn
    zaehlt also mit; Steigungen weniger als 60 ms auseinander zaehlen einmal.
    Eine einzige Geste ergibt 1. (Mit 30-50-ms-Fenstern und 6-8 dB geprueft:
    gleiche Ergebnisse.)"""
    env = env_db(ch, win=0.05, hop=0.01)
    peak = max(v for _, v in env)
    count, last_t = 0, -1.0
    for k, (t, v) in enumerate(env):
        if t > until or v < peak - 40:
            continue
        prev = env[k - 5][1] if k >= 5 else peak - 60
        if v - max(prev, peak - 60) > 8.0:
            if t - last_t > 0.06:
                count += 1
            last_t = t
    return count


def lufs(path):
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(path), "-af", "ebur128", "-f", "null", "-"],
                       capture_output=True, text=True, check=True)
    return float(re.findall(r"I:\s*(-?[\d.]+) LUFS", r.stderr)[-1])


def drip_regions(a, b, thresh=1e-3, gap=0.25):
    """Zaehlt zusammenhaengende Bereiche, in denen sich zwei Signale unterscheiden."""
    regions = 0
    last = -10 ** 9
    big = 0.0
    g = int(gap * SR)
    for i, (x, y) in enumerate(zip(a, b)):
        d = abs(x - y)
        if d > thresh:
            if i - last > g:
                regions += 1
            last = i
        big = max(big, d) if d <= thresh else big
    return regions


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    SPEC.mkdir(exist_ok=True)
    order = ["ghul", "pistole", "klinge", "schritt", "ambiente"]
    wavs = sorted(HERE.glob("*.wav"), key=lambda p: (order.index(kind_of(p.stem)), p.stem.startswith("vergleich"), p.stem))
    checks, details, ghul_rows, amb_rows = [], [], [], []
    current = None
    titles = {"pistole": "Pistolenschuss", "klinge": "Klingentreffer", "ghul": "Ghul-Laut",
              "schritt": "Schritt auf Stein", "ambiente": "Katakomben-Ambiente"}
    ref_b, _ = m1.read_wav(HP1 / "ambiente-b-tropfen.wav")
    for p in wavs:
        stem = p.stem
        kind = kind_of(stem)
        chs, sr = m1.read_wav(p)
        length = len(chs[0]) / sr
        mx, mean = m1.volumedetect(p)
        m1.spectrogram(p, SPEC / f"{stem}.png", len(chs) == 2)
        folge = stem.endswith("-folge")
        group = stem.rsplit("-", 1)[0] if kind == "schritt" else stem
        if kind != current:
            details.append(f"\n## {titles[kind]}\n")
            current = kind
        head = "| Datei | Länge | Spitze | Mittel | Spektrogramm |\n|---|---|---|---|---|"
        if kind == "schritt":
            if stem.endswith("-1"):
                details.append(f"\n### {group}\n\n{BESCHREIBUNG[group]}\n\n{head}")
            if folge:
                details.append(f"\n{FOLGE}\n\n{head}")
        else:
            details.append(f"\n### {stem}\n\n{BESCHREIBUNG[stem]}\n\n{head}")
        details.append(f"| `{p.name}` | {length:.3f} s | {mx:.1f} dBFS | {mean:.1f} dBFS | ![{stem}](spektrogramme/{stem}.png) |")

        peak_ok = mx <= -1.0
        if kind == "ambiente":
            len_ok, len_txt = 20.0 <= length <= 40.0, "20–40 s"
            edge_ok, edge_txt = True, "Schleife, siehe Naht"
        else:
            if folge:
                len_ok, len_txt = True, "Hörhilfe, keine Grenze"
            else:
                len_ok, len_txt = length < LIMITS[kind], f"< {LIMITS[kind]} s"
            first, last, tail = m1.edge_check(chs[0])
            edge_ok = first == 0.0 and last == 0.0 and tail < -40.0
            tail_txt = "digitale Stille" if tail < -120 else f"{tail:.0f} dB"
            edge_txt = f"erstes/letztes Sample 0/0, Rest vor Blende {tail_txt}" if first == 0.0 and last == 0.0 \
                else f"erstes {first:.5f}, letztes {last:.5f}, Rest {tail_txt}"
        ok = len_ok and peak_ok and edge_ok
        checks.append(f"| `{p.name}` | {length:.3f} s ({len_txt}) {'✔' if len_ok else '✘'} | {mx:.1f} dBFS {'✔' if peak_ok else '✘'} | "
                      f"{edge_txt} {'✔' if edge_ok else '✘'} | {'bestanden' if ok else 'NICHT bestanden'} |")

        if kind == "ambiente":
            mid_max, mid_mean = m1.volumedetect(p, "highpass=f=300,highpass=f=300,lowpass=f=3000,lowpass=f=3000")
            loud = lufs(p)
            ogg = p.with_suffix(".ogg")
            import tempfile
            from pathlib import Path
            with tempfile.TemporaryDirectory() as td:
                dec = Path(td) / "dec.wav"
                subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(ogg), "-c:a", "pcm_s16le", str(dec)], check=True)
                ochs, _ = m1.read_wav(dec)
            seam_txt = []
            seam_ok = True
            for src, cc in (("WAV", chs), ("OGG", ochs)):
                for r in m1.seam(cc):
                    seam_ok &= r["sprung"] <= r["p999"]
                    seam_txt.append(f"{src} {r['kanal']}: {r['sprung']:.5f} ≤ {r['p999']:.5f}" if r["sprung"] <= r["p999"]
                                    else f"{src} {r['kanal']}: {r['sprung']:.5f} > {r['p999']:.5f}")
            regions = min(drip_regions(chs[0], ref_b[0]), 99), min(drip_regions(chs[1], ref_b[1]), 99)
            amb_rows.append(f"| `{p.stem}` | {'; '.join(seam_txt)} {'✔' if seam_ok else '✘'} | {mid_mean:.1f} dBFS | {mean:.1f} dBFS | "
                            f"{loud:.1f} LUFS | L {regions[0]}, R {regions[1]} |")

        if kind == "ghul":
            ch = chs[0]
            t_max = m1.loudest_window(ch)
            pk = m1.db(max(abs(v) for v in ch))
            after = m1.window_db(ch, 0.56, 0.60) - pk
            after_txt = "Stille" if after < -120 else f"{after:.0f} dB"
            ghul_rows.append(f"| `{p.name}` (Hörprobe 2) | {t_max:.3f} s | {after_txt} | {onsets(ch)} |")

    for name in ("ghul-a-knurren-steigend", "ghul-b-atem-schrei", "ghul-c-gurgeln-zaehlen"):
        ch = m1.read_wav(HP1 / f"{name}.wav")[0][0]
        pk = m1.db(max(abs(v) for v in ch))
        ghul_rows.append(f"| `{name}.wav` (Hörprobe 1) | {m1.loudest_window(ch):.3f} s | "
                         f"{m1.window_db(ch, 0.56, 0.60) - pk:.0f} dB | {onsets(ch)} |")

    out = ["# Messwerte – Hörprobe 2\n",
           "Erzeugt mit `python messen.py`. Spitze und Mittel aus `ffmpeg -af volumedetect`, Spektrogramme aus "
           "`ffmpeg -lavfi showspectrumpic` (Zeit nach rechts, Frequenz logarithmisch nach oben), Lautheit aus "
           "`ffmpeg -af ebur128`. WAV, 16 Bit, 44 100 Hz; Geräusche mono, Ambiente stereo und zusätzlich als OGG. "
           "Dateien mit `vergleich-` sind unverändert aus Hörprobe 1 kopiert.\n",
           "Diese Datei beschreibt nur Aufbau und Messwerte. Ob etwas gut klingt, entscheidet die Hörprobe.\n",
           "## Prüfung gegen den technischen Rahmen der Audio Bible\n",
           "Spitzenpegel höchstens −1 dBFS; erstes und letztes Sample exakt 0; 20–40 ms vor dem Ende mindestens "
           "40 dB unter der Spitze.\n",
           "| Datei | Länge | Spitze | Ränder | Ergebnis |\n|---|---|---|---|---|"] + checks
    out += ["\n## Ghul: eine Geste?\n",
            "Der Schlag kommt bei 0,50 s. *Gipfel*: Mitte des lautesten 10-ms-Fensters. *Nach dem Schlag*: Pegel "
            "0,56–0,60 s relativ zur Spitze (zeigt, wie klar der Laut nach dem Schlag endet). *Einsätze*: wie oft "
            "der Pegel (50-ms-Fenster) bis kurz nach dem Schlag gegenüber 50 ms vorher um mehr als 8 dB steigt; "
            "der Beginn des Lauts zählt mit – ein Maß dafür, ob der Laut aus mehreren Ereignissen besteht. Ziel für "
            "eine einzige Geste: 1. Zum Vergleich die drei Varianten aus Hörprobe 1.\n",
            "| Datei | Gipfel | nach dem Schlag | Einsätze bis zum Schlag |\n|---|---|---|---|"] + ghul_rows
    out += ["\n## Ambiente: Naht, Mitte, Lautheit, Unterschied zur Vorlage\n",
            "*Naht*: Sprung vom letzten zum ersten Sample, verglichen mit dem Abstand zweier Nachbar-Samples, den "
            "99,9 % aller Stellen nicht überschreiten (WAV und zurückdecodierte OGG, je Kanal). *Mitte*: mittlerer "
            "Pegel im Band 300–3000 Hz. *Unterschied zur Vorlage*: Zahl der getrennten Stellen, an denen sich die "
            "Datei von `../hoerprobe-1/ambiente-b-tropfen.wav` um mehr als 0,001 unterscheidet – erwartet sind "
            "genau die sieben Tropfen, sonst nichts.\n",
            "| Datei | Naht | Mitte 300–3000 Hz | gesamt | Lautheit | Unterschied zur Vorlage |\n|---|---|---|---|---|---|"]
    out += amb_rows
    ref_mid = m1.volumedetect(HP1 / "ambiente-b-tropfen.wav", "highpass=f=300,highpass=f=300,lowpass=f=3000,lowpass=f=3000")[1]
    out.append(f"| Vorlage `ambiente-b-tropfen` (Hörprobe 1) | – | {ref_mid:.1f} dBFS | – | {lufs(HP1 / 'ambiente-b-tropfen.wav'):.1f} LUFS | – |")
    out += ["\n## Dateien im Einzelnen"] + details
    (HERE / "messwerte.md").write_text("\n".join(out) + "\n", encoding="utf-8")
    print("\n".join(checks + ghul_rows + amb_rows))


if __name__ == "__main__":
    main()
