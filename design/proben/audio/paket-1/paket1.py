"""Geraeuschpaket 1 aus der neuen Palette (palette-neu.md, erweitert um Hauch und Stimme) - alles in SoundCraft, alles Samples.

Aufruf:  python paket1.py [--nur <teil des dateinamens>]
Braucht: Python-Standardbibliothek, ffmpeg, soundcraft-cli.exe (nur diese), die Hilfen aus ../soundcraft-versuch (bauen.py, bauen9.py)
und aus ../../uebergabe/tools/audio (ps1_klang.py, synth.py).

Ablauf: fuenf Quell-Samples (Schlag = Rauschschlag, Hauch, Pfeife, Glocke, Stimme) werden je einmal in SoundCraft gebaut, auf 22 050 Hz
in VAG gewandelt (wie im Video) und von einem Sampler je Geraeusch in der Tonhoehe verschoben gespielt. Jedes Geraeusch ist ein SoundCraft-Lauf
(zweimal gerechnet, bitgleich geprueft), danach: mono, Anfang abschneiden, Laenge begrenzen, Spitze nach Kategorie, 16-Bit-WAV, 44,1 kHz.
"""
import argparse
import hashlib
import json
import math
import shutil
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
SC = HERE.parent / "soundcraft-versuch"
sys.path.insert(0, str(SC))
sys.path.insert(0, str(HERE.parents[2] / "uebergabe" / "tools" / "audio"))

import bauen as B  # noqa: E402
import bauen9 as N  # noqa: E402
from bauen import j  # noqa: E402
from synth import SR  # noqa: E402

DATEIEN = HERE / "dateien"
UEBERGABE = HERE.parents[2] / "uebergabe" / "assets" / "audio"
RAUM_P1 = dict(N.RAUM9, decay=0.8, size=30, mix=22)  # kuerzer als in Hoerprobe 9: bei winzigen Geraeuschen wuchs der Hall nach dem trockenen Ton zu einem zweiten Einsatz
TPS = 1920  # Ticks je Sekunde bei 120/min und 960 je Viertel

# --- Quellen (je ein Sample) -------------------------------------------------------------------------------------

STIMME = {"osc1_wave": 0, "osc2_wave": 0, "osc2_semitones": 0.0, "osc2_detune": 0.0, "osc_mix": 0.0, "cutoff": 4000.0,
          "resonance": 0.0, "filter_env_amount": 0.0, "amp_attack": 20.0, "amp_decay": 500.0, "amp_sustain": 100.0,
          "amp_release": 200.0, "level": -6.0}
FORMANTEN = [("eq_1band", {"type": 0, "freq": 700.0, "gain": 14.0, "q": 3.0}),
             ("eq_1band", {"type": 0, "freq": 1150.0, "gain": 12.0, "q": 3.0}),
             ("eq_1band", {"type": 4, "freq": 3000.0, "q": 0.7})]


def q_stimme():
    x, h = N.quelle_synth("q-stimme", STIMME, 52, 3.4, inserts=FORMANTEN)
    return N.vag(x, "stimme", schleife=(1.0, 1.5, 0.4)), h


def q_hauch():
    """Weisses Rauschen, Bandpass faehrt von 900 auf 3 800 Hz (0,35 s), Pegel steigt sanft (kein Anschlag) und faellt wieder."""
    n = int(0.5 * 44100)
    z = B.kopf("q-hauch")
    z += ['track.new ' + j(kind="audio", format="mono", name="N"),
          'audiosuite.process ' + j(process="signal_generator", params={"waveform": 4, "freq": 1000.0, "level": -6.0},
                                    tracks=["N"], start=0, end=n),
          'mix.insert ' + j(track="N", slot=0, plugin="eq_1band", params={"type": 6, "freq": 900.0, "q": 0.8}),
          'automation.set_point ' + j(track="N", param="plugin:0:freq", at=0, value=900.0),
          'automation.set_point ' + j(track="N", param="plugin:0:freq", at=int(0.35 * 44100), value=3800.0),
          'automation.set_point ' + j(track="N", param="volume", at=0, value=-45.0),
          'automation.set_point ' + j(track="N", param="volume", at=int(0.15 * 44100), value=0.0),
          'automation.set_point ' + j(track="N", param="volume", at=n - 1, value=-45.0)]
    wav, h = B.lauf("q-hauch", z, 0.5)
    return N.vag(B.b_aus_wav(wav, True)[0], "hauch"), h


QUELLEN = {"schlag": N.instrument_rausch, "hauch": q_hauch, "pfeife": N.instrument_pfeife, "glocke": N.instrument_glocke,
           "stimme": q_stimme}

# --- die Geraeusche ---------------------------------------------------------------------------------------------------
# Spitzenpegel je Kategorie (dBFS). Die alten Dateien des Spiels: Schuss -1,5, Klinge -4,9, Schritt -12,1, Ghul-Ankuendigung -3,0 (Mittel -18,3).
PEGEL = {
    "ankuendigung": -1.5, "schuss": -1.5, "schmerz": -5.0, "ghul": -6.0, "treffer": -4.5, "nachladen": -8.0, "schwung": -9.0,
    "bewegung": -11.0, "tuer_kiste": -7.0, "aufheben": -8.0, "schritt": -12.0, "menue": -12.0,
}

SCHLAG = dict(play_mode=1, loop_mode=0, attack=0.1, sustain=100.0, release=100.0)


def adsr(decay, release=40.0, sustain=0.0, attack=0.1):
    return dict(play_mode=0, loop_mode=0, attack=attack, decay=decay, sustain=sustain, release=release)


def G(datei, quelle, kategorie, noten, laenge, params, auto=(), inserts=(), max_s=None):
    """noten: (Tonhoehe, Start s, Laenge s, Anschlag). laenge: Rendern in s. max_s: Datei wird darauf gekuerzt."""
    return dict(datei=datei, quelle=quelle, kat=kategorie, noten=noten, laenge=laenge, params=params, auto=list(auto),
                inserts=list(inserts), max_s=max_s or laenge)


def glide(track, plugin_slot, param, punkte):
    return ['automation.set_point ' + j(track=track, param=f"plugin:{plugin_slot}:{param}", at=int(t * 44100), value=v)
            for t, v in punkte]


def lautstaerke(track, punkte):
    return ['automation.set_point ' + j(track=track, param="volume", at=int(t * 44100), value=v) for t, v in punkte]


def n1(p, l, v=110):
    return [(p, 0.0, l, v)]


GERAEUSCHE = []
# Schuss: Rauschschlag eine Oktave tiefer (Urteil: 1b); drei Varianten +-1 Halbton
for i, p in enumerate((48, 47, 49), 1):
    GERAEUSCHE.append(G(f"p1-schuss-{i}", "schlag", "schuss", [(p, 0, 0.0625, 127)], 1.0, dict(SCHLAG, decay=400.0), max_s=0.6))
# Nachladen: zwei kurze Klacke, tief (Magazin) und scharf (bereit)
GERAEUSCHE.append(G("p1-nachladen-start", "schlag", "nachladen", [(62, 0, 0.06, 110)], 0.5, adsr(70.0, 30.0), max_s=0.3))
GERAEUSCHE.append(G("p1-nachladen-bereit", "schlag", "nachladen", [(67, 0, 0.05, 120)], 0.5, adsr(45.0, 30.0), max_s=0.25))
# Klinge: Schwung (Hauch, kein Anschlag) und Treffer (Rauschschlag eine Oktave hoeher, Urteil 2b)
for i, p in enumerate((64, 63), 1):
    GERAEUSCHE.append(G(f"p1-schwung-{i}", "hauch", "schwung", [(p, 0, 0.06, 110)], 0.8, SCHLAG, max_s=0.5))
for i, p in enumerate((72, 71, 73), 1):
    GERAEUSCHE.append(G(f"p1-treffer-{i}", "schlag", "treffer", [(p, 0, 0.06, 127)], 0.8, dict(SCHLAG, decay=300.0), max_s=0.45))
# Schritte: links etwas tiefer als rechts, je drei Varianten, Rauschschlag zwei Oktaven tiefer, kurz
for foot, base in (("l", 45), ("r", 47)):
    for i, d in enumerate((0, -1, 1), 1):
        GERAEUSCHE.append(G(f"p1-schritt-{foot}{i}", "schlag", "schritt", [(base + d, 0, 0.05, 110)], 0.5, adsr(70.0, 40.0), max_s=0.28))
# Bewegung
GERAEUSCHE.append(G("p1-sprung", "hauch", "bewegung", [(72, 0, 0.06, 100)], 0.6, SCHLAG, max_s=0.3))
GERAEUSCHE.append(G("p1-dash", "hauch", "bewegung", [(55, 0, 0.06, 110)], 1.0, SCHLAG, max_s=0.6))
GERAEUSCHE.append(G("p1-landung", "schlag", "bewegung", [(42, 0, 0.05, 115)], 0.6, adsr(160.0, 60.0), max_s=0.35))
# Stimme: Schmerz (faellt), Ghul entdecken (steigt), Ankuendigung (Anschwellen und Steigen, endet am Gipfel), sterben (faellt lang)
GERAEUSCHE.append(G("p1-schmerz", "stimme", "schmerz", [(55, 0, 0.35, 110)], 0.9, adsr(400.0, 120.0, 100.0, 5.0),
                    auto=glide("V", 0, "semitones", [(0.0, 0.0), (0.35, -4.0)]), inserts=[("pitch_shifter", {"semitones": 0.0, "mix": 100.0})],
                    max_s=0.6))
GERAEUSCHE.append(G("p1-ghul-entdecken", "stimme", "ghul", [(52, 0, 0.55, 105)], 1.2, adsr(400.0, 120.0, 100.0, 5.0),
                    auto=glide("V", 0, "semitones", [(0.0, 0.0), (0.4, 3.0)]), inserts=[("pitch_shifter", {"semitones": 0.0, "mix": 100.0})],
                    max_s=0.9))
GERAEUSCHE.append(G("p1-ghul-ankuendigung", "stimme", "ankuendigung", [(64, 0, 0.9, 127)], 1.4, adsr(400.0, 25.0, 100.0, 5.0),
                    auto=glide("V", 0, "semitones", [(0.0, 0.0), (0.9, 7.0)]) + lautstaerke("V", [(0.0, -18.0), (0.9, 0.0)]),
                    inserts=[("pitch_shifter", {"semitones": 0.0, "mix": 100.0})], max_s=1.0))
GERAEUSCHE.append(G("p1-ghul-sterben", "stimme", "ghul", [(45, 0, 0.9, 105)], 1.6, adsr(400.0, 400.0, 100.0, 5.0),
                    auto=glide("V", 0, "semitones", [(0.0, 0.0), (1.0, -9.0)]) + lautstaerke("V", [(0.5, 0.0), (1.2, -35.0)]),
                    inserts=[("pitch_shifter", {"semitones": 0.0, "mix": 100.0})], max_s=1.3))
# Tuer, Kiste, Menue
GERAEUSCHE.append(G("p1-tuer-oeffnen", "hauch", "tuer_kiste", [(43, 0, 0.7, 100)], 1.8, dict(play_mode=0, loop_mode=0, attack=0.1, decay=300.0, sustain=100.0, release=250.0),
                    max_s=1.3))
GERAEUSCHE.append(G("p1-kiste-oeffnen", "schlag", "tuer_kiste", [(46, 0, 0.05, 120)], 0.8, adsr(220.0, 80.0), max_s=0.5))
GERAEUSCHE.append(G("p1-menue-klick", "schlag", "menue", [(72, 0, 0.03, 100)], 0.3, adsr(25.0, 15.0), max_s=0.12))
# Aufheben: Metallglocke auf Toenen von e-Moll; Verbrauchsgut unterscheidbar durch Zahl, Richtung und Lage der Toene
G_BELL = adsr(330.0, 120.0)


def glocke(datei, tone, lang=0.8, decay=330.0):
    return G(datei, "glocke", "aufheben", [(p, t, 0.5 if decay < 700 else 1.0, 105) for p, t in tone], lang, adsr(decay, 150.0), max_s=lang)


GERAEUSCHE += [
    glocke("p1-aufheben-silber", [(64, 0.0)], 0.6),                                  # ein heller Ton (E4)
    glocke("p1-aufheben-phiole", [(55, 0.0), (59, 0.12)], 0.8),                      # G3 -> H3, aufsteigende Terz (Heilung)
    glocke("p1-aufheben-weihmunition", [(64, 0.0), (59, 0.12)], 0.8),                # E4 -> H3, fallende Quarte
    glocke("p1-aufheben-schluessel", [(52, 0.0), (59, 0.11), (64, 0.22)], 0.9),      # E3 -> H3 -> E4, drei Toene
    glocke("p1-aufheben-ausruestung", [(40, 0.0)], 1.3, decay=700.0),                # ein tiefer, langer Ton (E2)
]


def stueck(g, quellen):
    inst = quellen[g["quelle"]][0]
    pfad, n, a, b = inst
    p = dict(g["params"], root_note=52 if g["quelle"] in ("pfeife", "glocke", "stimme") else 60)
    if g["quelle"] == "stimme":
        p.update(loop_mode=1, loop_start=a, loop_end=b)
    noten = [(pi, int(t * TPS), max(1, int(l * TPS)), v) for pi, t, l, v in g["noten"]]
    name = "V" if g["quelle"] == "stimme" else "S"
    z = B.kopf(g["datei"]) + N.sampler_spur(name, pfad, p, noten, g["laenge"], RAUM_P1, inserts=g["inserts"])
    z += g["auto"]
    return z


def nachbearbeiten(wav, g):
    """mono, Anfang auf den ersten hoerbaren Sample, Laenge begrenzen, Auslaufen, Spitze nach Kategorie."""
    x = B.b_aus_wav(wav, True)[0]
    pk = max(abs(v) for v in x) or 1.0
    s = next((i for i, v in enumerate(x) if abs(v) > pk * 10 ** (-60 / 20)), 0)
    x = x[max(0, s - int(0.002 * SR)):]
    x = x[: int(g["max_s"] * SR)]
    na = int((0.03 if g["max_s"] > 0.3 else 0.012) * SR)
    for i in range(min(na, len(x))):
        x[-1 - i] *= i / na
    ne = int(0.002 * SR)
    for i in range(min(ne, len(x))):
        x[i] *= i / ne
    pk = max(abs(v) for v in x) or 1.0
    f = 10 ** (PEGEL[g["kat"]] / 20) / pk
    return [v * f for v in x]


def onsets(x):
    """Zaehlt Einsaetze: Pegel steigt innerhalb von 50 ms um mehr als 8 dB (Hop 5 ms, Fenster 10 ms); der Beginn zaehlt mit."""
    hop, win = int(0.005 * SR), int(0.01 * SR)
    lv = []
    for i in range(0, max(1, len(x) - win), hop):
        r = math.sqrt(sum(v * v for v in x[i:i + win]) / win)
        lv.append(B.db(r) if r > 0 else -120.0)
    n, last = 0, -99
    for i, v in enumerate(lv):
        prev = min(lv[max(0, i - 10):i] or [-120.0])
        if v > -50 and v - prev > 8 and i - last > 10:
            n += 1
            last = i
    return n


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--nur", default="")
    args = ap.parse_args()
    B.TMP.mkdir(exist_ok=True)
    B.SKR = HERE / "skripte"
    DATEIEN.mkdir(exist_ok=True)
    quellen = {k: f() for k, f in QUELLEN.items()}
    bericht = {"quellen": {k: {"frames": v[0][1], "sha256_roh": v[1][0]} for k, v in quellen.items()}, "dateien": {}}
    for g in GERAEUSCHE:
        if args.nur not in g["datei"]:
            continue
        z = stueck(g, quellen)
        wav, h = B.lauf(g["datei"], z, g["laenge"])
        x = nachbearbeiten(wav, g)
        B.schreibe(DATEIEN / f"{g['datei']}.wav", [x])
        m = B.mess([x])
        m["einsaetze"] = onsets(x)
        m["kategorie"] = g["kat"]
        m["bitgleich"] = h[0] == h[1]
        bericht["dateien"][g["datei"]] = m
    out = HERE / ("messwerte-" + (args.nur or "alle") + ".json")
    out.write_text(json.dumps(bericht, indent=1), encoding="utf-8")
    print("fertig:", len(bericht["dateien"]), "Dateien,", sum(1 for v in bericht["dateien"].values() if not v["bitgleich"]), "nicht bitgleich")


if __name__ == "__main__":
    main()


# --- Uebersicht und Uebergabe ----------------------------------------------------------------------------------------

NAMEN = ["C", "Cis", "D", "Dis", "E", "F", "Fis", "G", "Gis", "A", "B", "H"]


def ton(m):
    return f"{NAMEN[m % 12]}{m // 12 - 1}"


def schreibe_uebergabe():
    """Kopiert die Dateien nach design/uebergabe/assets/audio/ und schreibt audio-paket-1.sha256 (Format wie audio.sha256)."""
    UEBERGABE.mkdir(parents=True, exist_ok=True)
    zeilen = ["# sha256 size name - Geraeuschpaket 1 (p1-*), erzeugt von design/proben/audio/paket-1/paket1.py; Format wie audio.sha256"]
    for g in GERAEUSCHE:
        src = DATEIEN / f"{g['datei']}.wav"
        dst = UEBERGABE / f"{g['datei']}.wav"
        shutil.copyfile(src, dst)
        data = dst.read_bytes()
        zeilen.append(f"{hashlib.sha256(data).hexdigest()} {len(data)} {dst.name}")
    (UEBERGABE / "audio-paket-1.sha256").write_text("\n".join(zeilen) + "\n", encoding="utf-8", newline="\n")


def tabellenzeilen(bericht):
    z = []
    for g in GERAEUSCHE:
        m = bericht["dateien"].get(g["datei"])
        if not m:
            continue
        root = 52 if g["quelle"] in ("pfeife", "glocke", "stimme") else 60
        tonlage = ", ".join(f"{ton(p)} ({p - root:+d})" for p, t, l, v in g["noten"])
        z.append(f"| `{g['datei']}.wav` | {g['quelle']} | {tonlage} | {m['seconds']:.2f} s | {m['peak_db']:.1f} | {m['rms_db']:.1f} | {g['kat']} |")
    return "\n".join(z)


if __name__ == "__main__" and not any(a.startswith("--nur") for a in sys.argv[1:]):
    _b = json.loads((HERE / "messwerte-alle.json").read_text(encoding="utf-8"))
    schreibe_uebergabe()
    (HERE / "tabelle.md").write_text(
        "| Datei | Quelle | gespielt (Halbtöne gegen Aufnahme) | Dauer | Spitze dBFS | Mittel (RMS) dBFS | Kategorie |\n|---|---|---|---|---|---|---|\n"
        + tabellenzeilen(_b) + "\n", encoding="utf-8")
    print("Uebergabe und tabelle.md geschrieben")
