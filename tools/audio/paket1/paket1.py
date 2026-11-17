"""Geraeuschpaket 1 (Praefix p1-) aus der neuen Palette - alles in SoundCraft, alles Samples (Uebergabe des Audiodesigners, Issue #290; Paket 1.1 fuer Issue #291).

BRAUCHT SOUNDCRAFT: soundcraft-cli.exe 0.20.1 unter "C:/Program Files/ArtCraft/SoundCraft/" (Pfad: CLI in bauen.py), dazu ffmpeg und
Python 3.12 (nur Standardbibliothek). Laeuft NICHT in der CI und wird dort nie aufgerufen - die fertigen Dateien sind eingecheckt.

Aufruf (aus dem Repository-Wurzelverzeichnis):
    python tools/audio/paket1/paket1.py               alle Geraeusche rechnen und nach assets/audio/ kopieren
    python tools/audio/paket1/paket1.py --nur schuss  nur Dateien, deren Name das enthaelt (ohne Kopieren)
    python tools/models/checksums.py                  danach immer: Pruefsummen in assets/audio/audio.sha256

Hilfen: bauen.py und bauen9.py hier daneben (SoundCraft-Laeufe, Quell-Samples), aus tools/audio ps1_klang.py und synth.py.
Die SoundCraft-Skripte jedes Laufs landen in skripte/, die Messwerte in messwerte-alle.json; Lautheit je Kategorie: uebersicht.md.

Ablauf: fuenf Quell-Samples (Schlag = Rauschschlag, Hauch, Pfeife, Glocke, Stimme) werden je einmal in SoundCraft gebaut, auf 22 050 Hz
in VAG gewandelt (wie im Video) und von einem Sampler je Geraeusch in der Tonhoehe verschoben gespielt. Jedes Geraeusch ist ein SoundCraft-Lauf
(zweimal gerechnet, bitgleich geprueft), danach: mono, Anfang abschneiden, Laenge begrenzen, Spitze nach Kategorie, 16-Bit-WAV, 44,1 kHz.
"""
import argparse
import json
import math
import shutil
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))  # bauen.py, bauen9.py
sys.path.insert(0, str(HERE.parent))  # tools/audio: synth.py, ps1_klang.py, ...

import bauen as B  # noqa: E402
import bauen9 as N  # noqa: E402
from bauen import j  # noqa: E402
from synth import SR  # noqa: E402

DATEIEN = B.TMP / "paket1-dateien"  # Zwischenstand ausserhalb des Repositorys
UEBERGABE = HERE.parents[2] / "assets" / "audio"  # dort laedt das Spiel sie
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


# Erweiterte Stimme (Paket 1.1): drei Formanten (600, 1050, 2400 Hz), zweite Schwingung (Dreieck, 12 Cent verstimmt) fuer leichtes Schweben
STIMME2 = dict(STIMME, osc2_wave=3, osc2_semitones=0.0, osc2_detune=12.0, osc_mix=35.0, cutoff=3500.0)
FORMANTEN2 = [("eq_1band", {"type": 0, "freq": 600.0, "gain": 14.0, "q": 3.0}),
              ("eq_1band", {"type": 0, "freq": 1050.0, "gain": 11.0, "q": 3.0}),
              ("eq_1band", {"type": 0, "freq": 2400.0, "gain": 6.0, "q": 3.0}),
              ("eq_1band", {"type": 4, "freq": 2800.0, "q": 0.7})]


def q_stimme2():
    x, h = N.quelle_synth("q-stimme2", STIMME2, 52, 3.4, inserts=FORMANTEN2)
    return N.vag(x, "stimme2", schleife=(1.0, 1.5, 0.4)), h


STIMMEN = ("stimme", "stimme2")
TONAL = ("pfeife", "glocke") + STIMMEN  # Quellen, die bei E3 (52) aufgenommen sind; Schlag und Hauch bei C4 (60)

QUELLEN = {"schlag": N.instrument_rausch, "hauch": q_hauch, "pfeife": N.instrument_pfeife, "glocke": N.instrument_glocke,
           "stimme": q_stimme, "stimme2": q_stimme2}

# --- die Geraeusche ---------------------------------------------------------------------------------------------------
# Spitzenpegel je Kategorie (dBFS). Die alten Dateien des Spiels: Schuss -1,5, Klinge -4,9, Schritt -12,1, Ghul-Ankuendigung -3,0 (Mittel -18,3).
PEGEL = {
    "ankuendigung": -1.5, "schuss": -1.5, "schmerz": -5.0, "ghul": -6.0, "treffer": -4.5, "nachladen": -8.0, "schwung": -9.0,
    "bewegung": -11.0, "tuer_kiste": -7.0, "aufheben": -8.0, "schritt": -12.0, "menue": -12.0, "marke": -10.0,
}

SCHLAG = dict(play_mode=1, loop_mode=0, attack=0.1, sustain=100.0, release=100.0)


def adsr(decay, release=40.0, sustain=0.0, attack=0.1):
    return dict(play_mode=0, loop_mode=0, attack=attack, decay=decay, sustain=sustain, release=release)


def G(datei, quelle, kategorie, noten, laenge, params, auto=(), inserts=(), max_s=None, zusatz=()):
    """noten: (Tonhoehe, Start s, Laenge s, Anschlag). laenge: Rendern in s. max_s: Datei wird darauf gekuerzt.
    zusatz: weitere Spuren (Quelle, Noten, Sampler-Parameter), z. B. ein kurzer Glockenanteil im Schuss."""
    return dict(datei=datei, quelle=quelle, kat=kategorie, noten=noten, laenge=laenge, params=params, auto=list(auto),
                inserts=list(inserts), max_s=max_s or laenge, zusatz=list(zusatz))


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
# (links l3 liegt bei -2 statt +1: dort war es byte-gleich zu r2, Befund der Review)
for foot, base, abw in (("l", 45, (0, -1, -2)), ("r", 47, (0, -1, 1))):
    for i, d in enumerate(abw, 1):
        GERAEUSCHE.append(G(f"p1-schritt-{foot}{i}", "schlag", "schritt", [(base + d, 0, 0.05, 110)], 0.5, adsr(70.0, 40.0), max_s=0.28))
# Bewegung
GERAEUSCHE.append(G("p1-sprung", "hauch", "bewegung", [(72, 0, 0.06, 100)], 0.6, SCHLAG, max_s=0.3))
GERAEUSCHE.append(G("p1-dash", "hauch", "bewegung", [(55, 0, 0.06, 110)], 1.0, SCHLAG, max_s=0.6))
GERAEUSCHE.append(G("p1-landung", "schlag", "bewegung", [(42, 0, 0.05, 115)], 0.6, adsr(160.0, 60.0), max_s=0.35))
# Stimme: Schmerz (faellt), Ghul entdecken (steigt), Ankuendigung (Anschwellen und Steigen, endet am Gipfel), sterben (faellt lang)
PS = ("pitch_shifter", {"semitones": 0.0, "mix": 100.0})
# Paket 1.1: Schmerz tiefer (H2 statt G3, nicht hoeher als die Ghul-Laute E3/A2), kuerzer und trockener (kurzes Ausklingen)
GERAEUSCHE.append(G("p1-schmerz", "stimme", "schmerz", [(47, 0, 0.25, 110)], 0.7, adsr(200.0, 60.0, 100.0, 5.0),
                    auto=glide("V", 0, "semitones", [(0.0, 0.0), (0.25, -3.0)]), inserts=[PS], max_s=0.4))
GERAEUSCHE.append(G("p1-ghul-entdecken", "stimme", "ghul", [(52, 0, 0.55, 105)], 1.2, adsr(400.0, 120.0, 100.0, 5.0),
                    auto=glide("V", 0, "semitones", [(0.0, 0.0), (0.4, 3.0)]), inserts=[PS], max_s=0.9))
# Variante 2: erweiterte Stimme (drei Formanten, schwebend), leichte Tonhoehenbewegung, dazu ein leiser Hauch als Rauschanteil
GERAEUSCHE.append(G("p1-ghul-entdecken-2", "stimme2", "ghul", [(50, 0, 0.5, 105)], 1.2, adsr(400.0, 120.0, 100.0, 5.0),
                    auto=glide("V", 0, "semitones", [(0.0, 0.0), (0.1, 0.5), (0.2, -0.3), (0.3, 1.5), (0.45, 4.0)]), inserts=[PS], max_s=0.85,
                    zusatz=[("hauch", [(52, 0.0, 0.3, 90)], dict(SCHLAG, level=-14.0))]))
# Ankuendigung (Paket 1.1): tiefer und sanfter, kein schriller Anstieg; durchkommen ueber Einsatz, Pegel und Mittenband
TIEF = ("eq_1band", {"type": 4, "freq": 1800.0, "q": 0.7})
HOCH_PASS = ("eq_1band", {"type": 3, "freq": 200.0, "q": 0.7})  # nimmt den Bauch unter 200 Hz weg, damit die Warnung im Mittenband liegt
GERAEUSCHE.append(G("p1-ghul-ankuendigung", "stimme", "ankuendigung", [(50, 0, 0.9, 127)], 1.4, adsr(400.0, 25.0, 100.0, 5.0),
                    auto=glide("V", 0, "semitones", [(0.0, 0.0), (0.9, 3.0)]) + lautstaerke("V", [(0.0, -18.0), (0.9, 0.0)]),
                    inserts=[PS, HOCH_PASS, TIEF], max_s=1.0))
GERAEUSCHE.append(G("p1-ghul-sterben", "stimme", "ghul", [(45, 0, 0.9, 105)], 1.6, adsr(400.0, 400.0, 100.0, 5.0),
                    auto=glide("V", 0, "semitones", [(0.0, 0.0), (1.0, -9.0)]) + lautstaerke("V", [(0.5, 0.0), (1.2, -35.0)]),
                    inserts=[("pitch_shifter", {"semitones": 0.0, "mix": 100.0})], max_s=1.3))
GERAEUSCHE.append(G("p1-ghul-sterben-2", "stimme2", "ghul", [(43, 0, 0.8, 105)], 1.6, adsr(400.0, 400.0, 100.0, 5.0),
                    auto=glide("V", 0, "semitones", [(0.0, 0.0), (0.2, -0.4), (0.5, 0.2), (1.0, -7.0)]) + lautstaerke("V", [(0.5, 0.0), (1.2, -35.0)]),
                    inserts=[PS], max_s=1.3, zusatz=[("hauch", [(48, 0.0, 0.5, 90)], dict(SCHLAG, level=-12.0))]))
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
    glocke("p1-aufheben-weihmunition", [(64, 0.0), (59, 0.0)], 1.4, decay=600.0),    # E4 und H3 zugleich: ein Klang (Quinte), laenger, hell
    glocke("p1-aufheben-schluessel", [(52, 0.0), (59, 0.11), (64, 0.22)], 0.9),      # E3 -> H3 -> E4, drei Toene
    glocke("p1-aufheben-ausruestung", [(40, 0.0)], 1.3, decay=700.0),                # ein tiefer, langer Ton (E2)
]
# VORSCHLAG (getrennt gekennzeichnet, kein Wunsch aus dem Urteil): geweihte Munition
# - Schuss mit Glockenanteil: derselbe Rauschschlag wie p1-schuss-*, dazu ein kurzer heller Glockenton (E4) als zweite Spur
# - Marke fuer das Ende der Wirkung: ein leiser, tiefer Glockenton (E3); im Core gibt es dafuer noch kein Ereignis
for i, p in enumerate((48, 47), 1):
    GERAEUSCHE.append(G(f"p1-schuss-geweiht-{i}", "schlag", "schuss", [(p, 0, 0.0625, 127)], 1.0, dict(SCHLAG, decay=400.0), max_s=0.6,
                        zusatz=[("glocke", [(64, 0.0, 0.3, 100)], dict(adsr(300.0, 120.0), level=6.0))]))
GERAEUSCHE.append(G("p1-weihe-ende", "glocke", "marke", [(52, 0.0, 0.3, 90)], 0.9, adsr(350.0, 150.0), max_s=0.7))


def stueck(g, quellen):
    inst = quellen[g["quelle"]][0]
    pfad, n, a, b = inst
    p = dict(g["params"], root_note=52 if g["quelle"] in TONAL else 60)
    if g["quelle"] in STIMMEN:
        p.update(loop_mode=1, loop_start=a, loop_end=b)
    noten = [(pi, int(t * TPS), max(1, int(l * TPS)), v) for pi, t, l, v in g["noten"]]
    name = "V" if g["quelle"] in STIMMEN else "S"
    z = B.kopf(g["datei"]) + N.sampler_spur(name, pfad, p, noten, g["laenge"], RAUM_P1, inserts=g["inserts"])
    for k, (q2, noten2, params2) in enumerate(g["zusatz"], 1):
        pfad2 = quellen[q2][0][0]
        p2 = dict(params2, root_note=52 if q2 in TONAL else 60)
        n2 = [(pi, int(t * TPS), max(1, int(l * TPS)), v) for pi, t, l, v in noten2]
        z += N.sampler_spur(f"Z{k}", pfad2, p2, n2, g["laenge"], RAUM_P1)
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


def onsets(x, fenster=0.01):
    """Zaehlt Einsaetze: Pegel steigt innerhalb von 50 ms um mehr als 8 dB (Hop 5 ms, Fenster 10 ms); der Beginn zaehlt mit."""
    hop, win = int(0.005 * SR), int(fenster * SR)
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
        m["einsaetze_30ms"] = onsets(x, 0.03)  # tiefe Toene (Periode > 6 ms): das 10-ms-Fenster sieht einzelne Schwingungen als Einsaetze
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
    """Kopiert die Dateien nach assets/audio/. Die Pruefsummen schreibt danach tools/models/checksums.py (eine Liste fuer alle Klaenge)."""
    UEBERGABE.mkdir(parents=True, exist_ok=True)
    for g in GERAEUSCHE:
        shutil.copyfile(DATEIEN / f"{g['datei']}.wav", UEBERGABE / f"{g['datei']}.wav")
    print("Jetzt die Pruefsummen neu schreiben: python tools/models/checksums.py")


def tabellenzeilen(bericht):
    z = []
    for g in GERAEUSCHE:
        m = bericht["dateien"].get(g["datei"])
        if not m:
            continue
        root = 52 if g["quelle"] in TONAL else 60
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
