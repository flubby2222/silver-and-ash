"""Hoerprobe 5 – Vorauswahl-Messungen und messwerte.md (Konzept Klangbibliothek, Abschnitt 4).

Je Datei: technischer Rahmen (Laenge, Spitze, Raender; Ambiente: Naht der OGG),
Einsaetze (Regel 2), Abklingzeit bis -60 dB, Pegel in vier Baendern und der
Unterschied zur Vergleichsdatei derselben Kategorie (bisher beste synthetische Fassung).

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
sys.path.insert(0, str(HERE))
spec = importlib.util.spec_from_file_location("messen_hp1", HP1 / "messen.py")
m1 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m1)
import quelle as Q  # noqa: E402
import mischung as MI  # noqa: E402

LIMIT = {"pistole": 0.6, "klinge": 0.6, "schritt": 0.3, "tropfen": None, "einstellung": None}
REF = {"pistole": "vergleich-pistole-hp3.wav", "klinge": "vergleich-klinge-hp3-heller.wav",
       "schritt": "vergleich-schritt-hp3-folge.wav", "tropfen": "vergleich-tropfen-hp3-gekuerzt.wav"}

HERKUNFT = {
    "pistole-1-schlag-rein": "**Rein aus einer Aufnahme.** `impactPunch_heavy_001.ogg` (Impact Sounds): auf den Schlag geschnitten, Brummen unter 40 Hz entfernt, 2 Halbtöne tiefer (Varispeed), stark weich gesättigt (tanh), kurzer synthetischer Raum wie Hörprobe 3. Kenney hat keinen Schuss; der Faustschlag ersetzt ihn.",
    "pistole-2-metall-plus-koerper": "**Mischung, verschmolzen.** Anschlag: erste 25 ms von `impactMetal_heavy_002.ogg` (Impact Sounds) + synthetischer Körper und Sub wie Hörprobe 3; beide tragen eine gemeinsame Hüllkurve, werden gemeinsam gesättigt und liegen im selben Raum.",
    "pistole-3-platte-plus-koerper": "**Mischung, verschmolzen.** Anschlag: erste 20 ms von `impactPlate_heavy_001.ogg` (Impact Sounds), 3 Halbtöne tiefer, + synthetischer Körper; gemeinsame Hüllkurve, gemeinsame Sättigung, selber Raum.",
    "vergleich-pistole-hp3": "Vergleich: `pistole-ps1.wav` aus Hörprobe 3, rein synthetisch, unverändert.",
    "klinge-1-hieb-rein": "**Rein aus einer Aufnahme.** `chop.ogg` (RPG Audio): geschnitten, 2 Halbtöne tiefer.",
    "klinge-2-schnitt-rein": "**Rein aus einer Aufnahme.** `knifeSlice.ogg` (RPG Audio): geschnitten, 3 Halbtöne tiefer. Die Aufnahme ist ein Schnitt, der durch die Frequenzen wandert; die Messung zeigt deshalb Einsatzversatz.",
    "klinge-3-schneide-plus-koerper": "**Mischung, verschmolzen.** Erste 40 ms von `knifeSlice.ogg` (RPG Audio) und synthetisches Rauschen laufen gemeinsam durch einen Tiefpass, der von 7 kHz auf 180 Hz fällt, mit gemeinsamer Hüllkurve; darunter der Körperton 110→55 Hz. Ersetzt die Fassung „zwei Aufnahmen“ aus dem ersten Stand dieser Hörprobe.",
    "vergleich-klinge-hp3-heller": "Vergleich, rein synthetisch: die Klinge aus **Hörprobe 3**, heller nur über die Klangfarbe desselben Ereignisses (Tiefpass startet bei 4,5 statt 1,8 kHz, Anschlag 0,2 statt 1 ms, Körperton 140→70 statt 110→55 Hz); der leise Klick wurde weggenommen, nichts hinzugefügt.",
    "schritt-1-rpg-rein": "**Rein aus Aufnahmen.** `footstep00/02/06/07.ogg` (RPG Audio), je auf einen Schritt geschnitten. Diese vier ohne Übersteuerung ausgewählt (03 und 08 haben übersteuerte Samples).",
    "schritt-2-beton-rein": "**Rein aus Aufnahmen.** `footstep_concrete_000–003.ogg` (Impact Sounds), 2 Halbtöne tiefer (schwerer). Die Originale sind sehr knapp geschnitten (0,1 s).",
    "schritt-3-anschlag-plus-koerper": "**Mischung, verschmolzen.** Erste 20 ms der RPG-Schritte und synthetisches Rauschen werden gemeinsam von der Bodenreaktions-Kurve aus Hörprobe 3 getragen.",
    "vergleich-schritt-hp3": "Vergleich: `schritt-ps1-folge.wav` aus Hörprobe 3, rein synthetisch, unverändert.",
    "tropfen-1-glas": "**Rein aus einer Aufnahme.** `impactGlass_light_000.ogg` (Impact Sounds), eine Oktave tiefer (±2 Halbtöne je Tropfen), kein eigener Raum. Kenney hat kein Wasser; leichtes Glas ersetzt es.",
    "tropfen-2-blech": "**Rein aus einer Aufnahme.** `impactTin_medium_004.ogg` (Impact Sounds), 7 Halbtöne tiefer, Tiefpass 2,5 kHz, kein eigener Raum.",
    "tropfen-3-klopfen-plus-ton": "**Mischung, verschmolzen.** Erste 8 ms von `impactGeneric_light_000.ogg` (Impact Sounds) + synthetischer Ton (8 ms Abklingzeit) unter einer gemeinsamen Hüllkurve, kein eigener Raum.",
    "vergleich-tropfen-hp3-gekuerzt": "Vergleich, rein synthetisch: der Tropfen aus **Hörprobe 3**, Klingeln durch Wegnehmen behoben – derselbe Ton, Abklingzeit 8 statt 20 ms, Höhlenhall ganz weggenommen (schon bei 5 % Pegel lieferte er eigene Einsätze). Nichts hinzugefügt.",
}


def kind_of(stem):
    s = stem.replace("vergleich-", "").replace("ambiente-", "")
    return s.split("-")[0]


def group_of(stem):
    for k in HERKUNFT:
        if stem.startswith(k):
            return k
    return None


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    (HERE / "spektrogramme").mkdir(exist_ok=True)
    refs = {k: Q.bands(m1.read_wav(HERE / v)[0][0]) for k, v in REF.items()}
    rows, checks, settings, mix = [], [], [], []
    for p in sorted(HERE.glob("*.wav")):
        stem = p.stem
        kind = kind_of(stem)
        chs, _ = m1.read_wav(p)
        x = chs[0]
        length = len(x) / Q.SR
        mx, mean = m1.volumedetect(p)
        m1.spectrogram(p, HERE / "spektrogramme" / f"{stem}.png", False)
        first, last, tail = m1.edge_check(x)
        lim = LIMIT.get(kind)
        hilfe = stem.endswith("folge") or "einzeln" in stem or kind in ("einstellung", "tropfen")
        len_ok = True if (lim is None or hilfe) else length < lim
        edge_ok = first == 0.0 and last == 0.0 and tail < -40.0
        ok = len_ok and mx <= -1.0 and edge_ok
        checks.append(f"| `{p.name}` | {length:.3f} s {'✔' if len_ok else '✘'} | {mx:.1f} dBFS {'✔' if mx <= -1 else '✘'} | "
                      f"{'✔' if edge_ok else '✘'} | {'bestanden' if ok else 'NICHT bestanden'} |")
        b = Q.bands(x)
        d = Q.decay(x)
        on = Q.onsets(x) if not hilfe else "–"
        ref = refs.get(kind)
        delta = " / ".join(f"{b[i] - ref[i]:+.0f}" for i in range(1, 5)) if ref else "–"
        line = (f"| `{p.name}` | {on} | {'–' if d is None or hilfe else f'{d * 1000:.0f} ms'} | "
                + " | ".join(f"{v:.0f}" for v in b[1:]) + f" | {delta} | ![{stem}](spektrogramme/{stem}.png) |")
        (settings if kind == "einstellung" else rows).append(line)
        if kind != "einstellung" and not stem.endswith("folge") and not (kind == "schritt" and not stem.endswith("-1")):
            mix.append(MI.zeile(f"`{p.name}`", MI.first_event(x) if kind == "tropfen" else x))
    amb = []
    for p in sorted(HERE.glob("ambiente-*.ogg")):
        with tempfile.TemporaryDirectory() as td:
            dec = Path(td) / "d.wav"
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(p), "-c:a", "pcm_s16le", str(dec)], check=True)
            chs, _ = m1.read_wav(dec)
            mx, mean = m1.volumedetect(dec)
        seam = m1.seam(chs)
        good = all(r["sprung"] <= r["p999"] for r in seam)
        amb.append(f"| `{p.name}` | {len(chs[0]) / 44100:.3f} s | {mx:.1f} dBFS | {mean:.1f} dBFS | "
                   + "; ".join(f"{r['kanal']}: {r['sprung']:.5f} ≤ {r['p999']:.5f}" if r["sprung"] <= r["p999"]
                               else f"{r['kanal']}: {r['sprung']:.5f} > {r['p999']:.5f}" for r in seam)
                   + f" {'✔' if good else '✘'} |")
    head = ("| Datei | Einsätze | bis −60 dB | < 300 Hz | 300–2 000 | 2–8 kHz | > 8 kHz | Δ zum Vergleich (dB, 4 Bänder) | Spektrogramm |\n"
            "|---|---|---|---|---|---|---|---|---|")
    out = ["# Messwerte – Hörprobe 5\n",
           "Erzeugt mit `python messen.py`. Alle Dateien sind durch die PS1-Kette gegangen (Einstellung „a-leicht“: "
           "22 050 Hz, Höhenausgleich, kein Hall), außer den Dateien `einstellung-*-b-stark` (11 025 Hz, kein Ausgleich, "
           "Hall „Room“). Herkunft der Originale: [klangquellen.md](../../../grundlagen/klangquellen.md). Bandpegel sind "
           "mittlere Pegel in dBFS (je zwei Filter 2. Ordnung); „Δ zum Vergleich“ ist der Unterschied zur bisher besten "
           "synthetischen Fassung derselben Kategorie in den Bändern < 300 Hz / 300–2 000 Hz / 2–8 kHz / > 8 kHz.\n",
           "Diese Datei beschreibt Aufbau und Messwerte. Ob etwas gut klingt, entscheidet die Hörprobe.\n",
           "## Herkunft und Bearbeitung je Kandidat\n"]
    out += [f"- `{k}`: {v}" for k, v in HERKUNFT.items()]
    out += ["\n## Vorauswahl-Messungen\n", head] + rows
    out += ["\n## PS1-Einstellung je Kategorie\n", head] + settings
    out += ["\n## Ambiente mit den Tropfen-Kandidaten (OGG, Naht nach dem Dekodieren)\n",
            "| Datei | Länge | Spitze | Mittel | Naht (Sprung ≤ 99,9 % der Nachbar-Abstände) |\n|---|---|---|---|---|"] + amb
    out += ["\n## Prüfung gegen den technischen Rahmen\n",
            "| Datei | Länge | Spitze | Ränder (0/0, Rest < −40 dB) | Ergebnis |\n|---|---|---|---|---|"] + checks
    (HERE / "messwerte.md").write_text("\n".join(out) + "\n", encoding="utf-8")
    print("\n".join(rows + settings + amb))
    print(sum("NICHT" in c for c in checks), "nicht bestanden")
    for c in checks:
        if "NICHT" in c:
            print(c)


if __name__ == "__main__":
    main()
