"""Hoerprobe 3 – misst alle WAV-Dateien und schreibt messwerte.md.

Wie Hoerprobe 2 (Funktionen aus ../hoerprobe-1/messen.py), dazu:
- je Paar sauber/PS1 der Pegel ueber 8 kHz und zwischen 2 und 8 kHz (zeigt die Faerbung),
- Ghul: Einsaetze nach Regel 2 der Audio Bible,
- Ambiente: Naht (WAV und OGG), Regel 6 an der Flaeche OHNE Tropfen (aus ambiente.flaeche()).

Aufruf: python messen.py
"""
import importlib.util
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
HP1 = HERE.parent / "hoerprobe-1"
sys.path.insert(0, str(HP1))

spec = importlib.util.spec_from_file_location("messen_hp1", HP1 / "messen.py")
m1 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m1)

SR = 44100
SPEC = HERE / "spektrogramme"
HIT = 0.50
LIMITS = {"pistole": 0.6, "klinge": 0.6, "ghul": 1.5, "schritt": 0.3}
MID = "highpass=f=300,highpass=f=300,lowpass=f=3000,lowpass=f=3000"

BESCHREIBUNG = {
    "ghul": "Grundlage `ghul-d-anschwellen` aus Hörprobe 2. **Geste:** Das Maul öffnet sich in 0,5 s und schließt sich beim Schlag – eine Steuerkurve treibt Lautstärke und einen Tiefpass (280 Hz → 2,5 kHz) zugleich. Zwei Schichten: raue Stimme (58 Hz, Formanten 450/1030/2380 Hz) und ein tiefer Sinus auf dem Grundton; dazu eine leise gesättigte Kopie der Stimme.",
    "pistole": "Grundlage `pistole-d-hell-kurz` aus Hörprobe 2. Anschlag: Druckpuls und Knall-Rauschen (Hochpass 1,5 kHz → Bandpass 3,2 kHz, 4 ms). Körper: eine Rauschquelle in zwei Bändern (Band um 550 Hz, Tiefpass 250 Hz, etwa 120 ms) und ein Sub-Stoß 90→45 Hz, zusammen weich gesättigt. Ausklang wie in Hörprobe 2: kalte frühe Reflexionen und kurzer Hall.",
    "klinge": "Grundlage `klinge-d-kurz-dumpf` aus Hörprobe 2. Rauschen durch einen resonanten Tiefpass, dessen Grenze in etwa 50 ms von 1,8 kHz auf 180 Hz fällt, darunter ein Ton 110→55 Hz und ein sehr leiser Kanten-Klick. Drei Varianten mit ±15 % Filter, ±8 % Tonhöhe und bis −1,5 dB Pegel.",
    "schritt": "Grundlage `schritt-d-klar` aus Hörprobe 2. Eine durchgehende Bodenreaktions-Kurve (Ferse, Abrollen, weicher Ballen nach 45–65 ms) trägt gefiltertes Rauschen (Tiefpass um 1,3 kHz, Hochpass 200 Hz), dazu tiefer Stoß, Klick und zwei kalte Reflexionen. Vier Schritte, verschieden in Tempo, Filter und Pegel.",
    "ambiente": "Grundfläche wie Hörprobe 1. Sieben Tropfen anderthalb Oktaven tiefer (Grundton etwa 1,1–1,9 kHz) an denselben Stellen wie in Hörprobe 2, neu mit exponentiell steigender Tonhöhe, weichem Einsatz, 20 ms Abklingzeit und kleinem Höhlenhall (vier Verzögerungen im goldenen Schnitt).",
}
PS1_SATZ = ("Die `-ps1`-Datei ist dieselbe Datei durch `ps1_klang.py`: auf 22 050 Hz umgerechnet, ADPCM kodiert und dekodiert, "
            "mit Gauß-Interpolation zurück auf 44,1 kHz, ohne Hall.")
FOLGE = "Hörhilfe, kein Spiel-Asset: die vier Schritte zweimal im Abstand von 0,5 s."


def kind_of(stem):
    return stem.split("-")[0]


def env_db(ch, win, hop):
    w, h = int(win * SR), int(hop * SR)
    return [(s / SR + win / 2, m1.db(m1.rms(ch[s:s + w]))) for s in range(0, len(ch) - w, h)]


def onsets(ch, until=HIT + 0.02):
    """Regel 2 der Audio Bible: Stellen, an denen der Pegel (50-ms-Fenster) gegenueber 50 ms vorher um mehr
    als 8 dB steigt; vor dem Laut gilt Stille (60 dB unter der Spitze). Ein einziger Einsatz = 1."""
    env = env_db(ch, 0.05, 0.01)
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


def band_mean(path, af):
    return m1.volumedetect(path, af)[1]


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    SPEC.mkdir(exist_ok=True)
    order = ["ghul", "pistole", "klinge", "schritt", "ambiente"]

    wavs = sorted(HERE.glob("*.wav"), key=lambda p: (order.index(kind_of(p.stem)),
                                                       p.stem.replace("sauber", "").replace("ps1", ""),
                                                       "ps1" in p.stem))
    checks, details, ghul_rows, color_rows, amb_rows = [], [], [], [], []
    current = None
    titles = {"pistole": "Pistolenschuss", "klinge": "Klingentreffer", "ghul": "Ghul-Laut",
              "schritt": "Schritt auf Stein", "ambiente": "Katakomben-Ambiente"}
    head = "| Datei | Länge | Spitze | Mittel | Spektrogramm |\n|---|---|---|---|---|"
    for p in wavs:
        stem, kind = p.stem, kind_of(p.stem)
        chs, sr = m1.read_wav(p)
        length = len(chs[0]) / sr
        mx, mean = m1.volumedetect(p)
        m1.spectrogram(p, SPEC / f"{stem}.png", len(chs) == 2)
        folge = stem.endswith("-folge")
        if kind != current:
            details.append(f"\n## {titles[kind]}\n\n{BESCHREIBUNG[kind]}\n\n{PS1_SATZ}\n")
            if kind == "schritt":
                details.append(FOLGE + "\n")
            details.append(head)
            current = kind
        details.append(f"| `{p.name}` | {length:.3f} s | {mx:.1f} dBFS | {mean:.1f} dBFS | ![{stem}](spektrogramme/{stem}.png) |")

        hi = band_mean(p, "highpass=f=8000,highpass=f=8000")
        mid8 = band_mean(p, "highpass=f=2000,highpass=f=2000,lowpass=f=8000,lowpass=f=8000")
        color_rows.append(f"| `{p.name}` | {mean:.1f} dBFS | {mid8:.1f} dBFS | {hi:.1f} dBFS |")

        peak_ok = mx <= -1.0
        if kind == "ambiente":
            len_ok, len_txt = 20.0 <= length <= 40.0, "20–40 s"
            with tempfile.TemporaryDirectory() as td:
                dec = Path(td) / "dec.wav"
                subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(p.with_suffix(".ogg")),
                                "-c:a", "pcm_s16le", str(dec)], check=True)
                ochs, _ = m1.read_wav(dec)
            parts, edge_ok = [], True
            for src, cc in (("WAV", chs), ("OGG", ochs)):
                for r in m1.seam(cc):
                    good = r["sprung"] <= r["p999"]
                    edge_ok &= good
                    parts.append(f"{src} {r['kanal']}: {r['sprung']:.5f} {'≤' if good else '>'} {r['p999']:.5f}")
            edge_txt = "Naht: " + "; ".join(parts)
        else:
            len_ok, len_txt = (True, "Hörhilfe") if folge else (length < LIMITS[kind], f"< {LIMITS[kind]} s")
            first, last, tail = m1.edge_check(chs[0])
            edge_ok = first == 0.0 and last == 0.0 and tail < -40.0
            tail_txt = "digitale Stille" if tail < -120 else f"{tail:.0f} dB"
            edge_txt = (f"erstes/letztes Sample 0/0, Rest vor Blende {tail_txt}" if first == 0.0 and last == 0.0
                        else f"erstes {first:.5f}, letztes {last:.5f}, Rest {tail_txt}")
        ok = len_ok and peak_ok and edge_ok
        checks.append(f"| `{p.name}` | {length:.3f} s ({len_txt}) {'✔' if len_ok else '✘'} | {mx:.1f} dBFS "
                      f"{'✔' if peak_ok else '✘'} | {edge_txt} {'✔' if edge_ok else '✘'} | {'bestanden' if ok else 'NICHT bestanden'} |")

        if kind == "ghul":
            ch = chs[0]
            ghul_rows.append(f"| `{p.name}` | {m1.loudest_window(ch):.3f} s | {onsets(ch)} |")
        if kind == "ambiente":
            amb_rows.append(f"| `{p.name}` (mit Tropfen) | {band_mean(p, MID):.1f} dBFS | {mean:.1f} dBFS | "
                            f"{band_mean(p, MID) - mean:.1f} dB | – |")

    # Regel 6: Flaeche ohne Tropfen
    spec_a = importlib.util.spec_from_file_location("ambiente_hp3", HERE / "ambiente.py")
    amb = importlib.util.module_from_spec(spec_a)
    spec_a.loader.exec_module(amb)
    with tempfile.TemporaryDirectory() as td:
        f = Path(td) / "flaeche.wav"
        amb.write_wav(f, list(amb.flaeche()))
        fm = m1.volumedetect(f)[1]
        fmid = band_mean(f, MID)
    gap = fmid - fm
    amb_rows.insert(0, f"| **Fläche ohne Tropfen** (nur für die Messung erzeugt, sauber) | {fmid:.1f} dBFS | {fm:.1f} dBFS | "
                       f"{gap:.1f} dB | {'✔ mindestens 25 dB darunter' if gap <= -25 else '✘ weniger als 25 dB'} |")

    out = ["# Messwerte – Hörprobe 3\n",
           "Erzeugt mit `python messen.py`. Spitze und Mittel aus `ffmpeg -af volumedetect`, Spektrogramme aus "
           "`ffmpeg -lavfi showspectrumpic` (Zeit nach rechts, Frequenz logarithmisch nach oben). WAV, 16 Bit, "
           "44 100 Hz; Geräusche mono, Ambiente stereo und zusätzlich als OGG. Jede Datei gibt es `-sauber` und `-ps1`; "
           "wie die PS1-Kette arbeitet und was an ihr geprüft ist, steht in [ps1_pruefung.md](ps1_pruefung.md).\n",
           "Diese Datei beschreibt nur Aufbau und Messwerte. Ob etwas gut klingt, entscheidet die Hörprobe.\n",
           "## Prüfung gegen den technischen Rahmen der Audio Bible\n",
           "| Datei | Länge | Spitze | Ränder | Ergebnis |\n|---|---|---|---|---|"] + checks
    out += ["\n## Ghul: Regel 2 (ein Einsatz)\n",
            "Der Schlag kommt bei 0,50 s. *Gipfel*: Mitte des lautesten 10-ms-Fensters. *Einsätze* nach der Messvorschrift "
            "von Regel 2 (50-ms-Fenster, Anstieg über 8 dB, der Beginn zählt mit). Ziel: 1.\n",
            "| Datei | Gipfel | Einsätze |\n|---|---|---|"] + ghul_rows
    out += ["\n## Ambiente: Regel 6 (Mitte frei)\n",
            "Gemessen wird nach Regel 6 die Fläche **ohne** Tropfen: `ambiente.py` erzeugt sie mit denselben Zufallszahlen, "
            "nur mit stummen Tropfen. Zum Vergleich die fertigen Dateien mit Tropfen (die tiefen Tropfen liegen selbst im "
            "Band; das ist laut Regel 6 gewollt).\n",
            "| Signal | Mitte 300–3000 Hz | gesamt | Abstand | Regel 6 |\n|---|---|---|---|---|"] + amb_rows
    out += ["\n## Färbung: sauber gegen PS1\n",
            "Mittlerer Pegel gesamt, zwischen 2 und 8 kHz und über 8 kHz (je zwei Hoch-/Tiefpässe von `ffmpeg`). "
            "Der Unterschied zwischen `-sauber` und `-ps1` zeigt, wie viel Höhen die PS1-Kette wegnimmt.\n",
            "| Datei | gesamt | 2–8 kHz | über 8 kHz |\n|---|---|---|---|"] + color_rows
    out += ["\n## Dateien im Einzelnen"] + details
    (HERE / "messwerte.md").write_text("\n".join(out) + "\n", encoding="utf-8")
    print("\n".join(checks + ghul_rows + amb_rows))


if __name__ == "__main__":
    main()
