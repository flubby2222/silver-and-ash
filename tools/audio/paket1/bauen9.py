"""Hoerprobe 9 (zweite Fassung, neue Palette, siehe palette-neu.md): dieselben vier Stuecke nach der Methode des Videos "How to make PS1 samples for FREE!" (SoundFontGuy), in SoundCraft.

Methode (siehe video.md): kurze Quelle -> mono, 16 Bit -> 22,05 kHz -> (bei Schleifen: Anfang, Ende und Laenge je Vielfaches von 28,
Ueberblendung der Schleife) -> VAG (ADPCM) -> als Sample in einen Sampler laden und mit Tonhoehenverschiebung (Transposition) spielen.
Neu gegenueber Hoerprobe 8: kein fertig synthetisierter Dauerton mit nachgeschalteter PS1-Kette, sondern Sample-Instrumente.

Aufruf:  python bauen9.py [--nur schuss|klinge|aufheben|skizze]
Nur Standardbibliothek, ffmpeg, soundcraft-cli.exe. a = Fassung b aus Hoerprobe 8 (hoerprobe-8-soundcraft/<nr>b-*.wav).
"""
import argparse
import json
import math
import shutil
import sys
from pathlib import Path

import bauen as B
import ps1_klang as P
from bauen import RAUM_SKIZZE, RAUM_SOUND, TMP, j
from synth import SR

OUT9 = B.HERE / "hoerprobe-9-methode"
OUT8 = B.OUT
RATE = 22050
BLOCK = 28


# --- Quellen in SoundCraft rendern ------------------------------------------------------------------------------

def quelle_rauschen(name, laenge_s, filter_freq, filter_zeit_s, abfall_db, abfall_zeit_s, filter_typ=4, q=0.7, welle=4):
    """Weisses Rauschen (Signalgenerator auf einer Audiospur) mit fallendem Tiefpass und fallender Lautstaerke (Automation)."""
    n = int(laenge_s * 44100)
    z = B.kopf(name)
    z += ['track.new ' + j(kind="audio", format="mono", name="N"),
          'audiosuite.process ' + j(process="signal_generator", params={"waveform": welle, "freq": 1000.0, "level": -6.0},
                                    tracks=["N"], start=0, end=n),
          'mix.insert ' + j(track="N", slot=0, plugin="eq_1band", params={"type": filter_typ, "freq": filter_freq[0], "q": q}),
          'automation.set_point ' + j(track="N", param="plugin:0:freq", at=0, value=filter_freq[0]),
          'automation.set_point ' + j(track="N", param="plugin:0:freq", at=int(filter_zeit_s * 44100), value=filter_freq[1]),
          'automation.set_point ' + j(track="N", param="volume", at=0, value=0.0),
          'automation.set_point ' + j(track="N", param="volume", at=int(abfall_zeit_s * 44100), value=abfall_db)]
    wav, h = B.lauf(name, z, laenge_s)
    return B.b_aus_wav(wav, True)[0], h


def quelle_synth(name, params, pitch, laenge_s, vel=100, inserts=()):
    """Eine gehaltene Note des Subtractive Synth, trocken (ohne Hall)."""
    z = B.kopf(name)
    z += ['track.new ' + j(kind="instrument", format="stereo", name="Q", instrument="subtractive_synth"),
          'mix.instrument ' + j(track="Q", plugin="subtractive_synth", params=params)]
    z += ['mix.insert ' + j(track="Q", slot=k, plugin=pl, params=pa) for k, (pl, pa) in enumerate(inserts)]
    z += ['midi.new_clip ' + j(track="Q", start=0, end=int(laenge_s * 44100)),
          "@NOTEN Q " + json.dumps([(pitch, 0, int((laenge_s - 0.2) * 1920), vel)])]
    wav, h = B.lauf(name, z, laenge_s)
    return B.b_aus_wav(wav, True)[0], h


# --- Sample-Aufbereitung wie im Video ------------------------------------------------------------------------------

def vag(x, name, schleife=None, ein_ms=2.0):
    """x: Float-Signal (44,1 kHz, mono). Gibt (Pfad, Frames, Schleifenstart in %, Schleifenende in %) zurueck.
    Schritte: auf -3 dBFS normalisieren, Anfang entkloppen (nur Einblendung), auf 22,05 kHz, optional Schleife
    (schleife = (start_s, laenge_s, ueberblendung_s), alles auf Vielfache von 28), ADPCM-Hin-und-Zurueck (VAG), 16-Bit-WAV mit 22 050 Hz."""
    pk = max(abs(v) for v in x) or 1.0
    x = [v * (10 ** (-3 / 20) / pk) for v in x]
    ne = int(ein_ms * SR / 1000)
    for i in range(min(ne, len(x))):
        x[i] *= i / ne
    ints = [[int(round(v * 32767)) for v in x]]
    low = P.resample(ints, SR, RATE)[0]
    low = [int(round(v)) for v in low]
    l0 = l1 = None
    if schleife:
        s_s, len_s, xf_s = schleife
        l0 = (int(s_s * RATE) // BLOCK) * BLOCK
        xf = min(int(xf_s * RATE), l0)
        mitte = (int(len_s * RATE) // BLOCK) * BLOCK
        best = None
        for ln in range(mitte - 12 * BLOCK * 10, mitte + 1, BLOCK):  # bis 0,2 s kuerzer: Ende passt in der Phase zum Anfang
            if l0 + ln > len(low) or ln <= xf:
                continue
            e = sum((low[l0 + ln - xf + i] - low[l0 - xf + i]) ** 2 for i in range(0, xf, 3))
            if best is None or e < best[0]:
                best = (e, ln)
        ln = best[1]
        l1 = l0 + ln
        low = low[:l1]
        assert len(low) == l1 and l0 % BLOCK == 0 and l1 % BLOCK == 0 and (l1 - l0) % BLOCK == 0
        y = list(low)
        for i in range(xf):  # lineare Ueberblendung: Ende der Schleife mit dem Stueck vor dem Schleifenanfang
            w = (i + 1) / xf
            y[l1 - xf + i] = int(round((1 - w) * low[l1 - xf + i] + w * low[l0 - xf + i]))
        low = y
    else:
        low = low[: (len(low) // BLOCK) * BLOCK]
    d = [int(round(v)) for v in P.adpcm_roundtrip(low)]
    pfad = TMP / f"{name}-vag.wav"
    P.write_wav(str(pfad), [d], rate=RATE)
    n = len(d)
    return pfad, n, (100.0 * l0 / n if l0 is not None else 0.0), (100.0 if l0 is not None else 100.0)


def sampler_spur(name, pfad, lade, notes, ende_s, raum_par, inserts=(), gain_db=0.0):
    z = ['track.new ' + j(kind="instrument", format="stereo", name=name, instrument="sampler"),
         'mix.sampler_load ' + j(track=name, path=str(pfad).replace("\\", "/"), params=lade)]
    for slot, (plugin, par) in enumerate(list(inserts) + [("room_reverb", raum_par)]):
        z += ['mix.insert ' + j(track=name, slot=slot, plugin=plugin, params=par)]
    if gain_db:
        z += ['mix.volume ' + j(tracks=[name], db=gain_db)]
    z += ['midi.new_clip ' + j(track=name, start=0, end=int(ende_s * 44100)),
          "@NOTEN " + name + " " + json.dumps(notes)]
    return z


# --- neue Palette (palette-neu.md): drei Instrumente, je ein Sample ------------------------------------------------

RAUM9 = {"mix": 28, "pre_delay": 8, "decay": 1.1, "size": 40, "damping": 3500, "diffusion": 75, "low_cut": 150}
SAMPLER_EINSCHLAG = {"play_mode": 1, "loop_mode": 0, "attack": 0.1, "sustain": 100.0, "release": 150.0}

PFEIFE = {"osc1_wave": 1, "osc2_wave": 3, "osc2_semitones": 12.0, "osc2_detune": 0.0, "osc_mix": 35.0, "cutoff": 900.0,
          "resonance": 5.0, "filter_env_amount": 0.0, "amp_attack": 30.0, "amp_decay": 500.0, "amp_sustain": 100.0,
          "amp_release": 300.0, "level": -6.0}
GLOCKE = {"osc1_wave": 3, "osc2_wave": 2, "osc2_semitones": 12.0, "osc2_detune": 0.0, "osc_mix": 30.0, "cutoff": 3000.0,
          "resonance": 0.0, "filter_env_amount": 0.0, "amp_attack": 3.0, "amp_decay": 5000.0, "amp_sustain": 0.0,
          "amp_release": 3000.0, "level": -3.0}
RINGMOD = ("ring_mod", {"effect": 0, "frequency": 395.0, "resonance": 50.0, "mod_depth": 0.0, "mix": 60.0})


def instrument_pfeife():
    x, h = quelle_synth("q-pfeife", PFEIFE, 52, 3.4)
    return vag(x, "pfeife", schleife=(0.8, 2.0, 0.5)), h


def instrument_glocke():
    x, h = quelle_synth("q-glocke", GLOCKE, 52, 6.2, inserts=[RINGMOD])
    return vag(x, "glocke"), h


def instrument_rausch():
    x, h = quelle_rauschen("q-rausch", 0.5, (8000.0, 1500.0), 0.06, -50.0, 0.35, welle=4)
    return vag(x, "rausch"), h


def _lade(inst, root, **extra):
    d = dict(SAMPLER_EINSCHLAG, root_note=root)
    d.update(extra)
    return inst[0], d


def neu_schuss():
    """Rauschschlag (rosa Rauschen, fallender Tiefpass) eine Oktave tiefer gespielt: schwerer, laenger, dumpfer."""
    (pfad, n, a, b), h = instrument_rausch()
    z = B.kopf("schuss9") + sampler_spur("Schuss", *_lade((pfad,), 60, decay=400.0), [(48, 0, 120, 127)], 1.0, RAUM9)
    return z, 0.8, {"quelle_sha256": h[0][:12], "sample_frames": n}


def neu_klinge():
    """Derselbe Rauschschlag eine Oktave hoeher gespielt: kuerzer und schaerfer."""
    (pfad, n, a, b), h = instrument_rausch()
    z = B.kopf("klinge9") + sampler_spur("Klinge", *_lade((pfad,), 60, decay=300.0), [(72, 0, 120, 127)], 1.0, RAUM9)
    return z, 0.8, {"quelle_sha256": h[0][:12], "sample_frames": n}


def neu_aufheben():
    """Metallglocke (Ringmodulator) bei E3 aufgenommen, bei E4 gespielt (eine Oktave hoeher, Grundton der Tonart)."""
    (pfad, n, a, b), h = instrument_glocke()
    z = B.kopf("aufheben9") + sampler_spur("Aufheben", *_lade((pfad,), 52, decay=4000.0), [(64, 0, 960, 100)], 1.2, RAUM9)
    return z, 1.2, {"quelle_sha256": h[0][:12], "sample_frames": n}


GLOCKE_PEGEL = 4.0
PFEIFE_PEGEL = -13.0


def neu_skizze():
    """Pfeife (bei E3, Schleife 2 s) auf den Akkordtoenen von Teil A des Menue-Themas, Metallglocke zwei Oktaven bis eineinhalb Oktaven tiefer als
    aufgenommen auf E2, E2, H1 (Schlaege bei 0, 8, 16 s). Dieselben Akkorde wie in Hoerprobe 8."""
    t = B.TICKS_PRO_SCHLAG
    akkorde = [(0, 8, [52, 55, 59, 64]), (8, 8, [52, 55, 60, 64]), (16, 4, [52, 57, 60, 64])]
    bell = [(0, 8, 40, 1.0), (8, 8, 40, 0.9), (16, 8, 35, 1.0)]
    (pg, ng, _, _), h1 = instrument_glocke()
    (pc, nc, a, b), h2 = instrument_pfeife()
    pfeife = [(m, s * t, int((d + 0.3) * t), 90) for s, d, ch in akkorde for m in ch]
    glocke = [(m, s * t, int(min(d, 20 - s) * t), int(100 * v)) for s, d, m, v in bell]
    z = B.kopf("skizze9", bpm=60)
    z += sampler_spur("Glocke", pg, dict(SAMPLER_EINSCHLAG, root_note=52, decay=5000.0, release=1500.0, level=GLOCKE_PEGEL),
                      glocke, 20.0, RAUM9)
    z += sampler_spur("Pfeife", pc, {"root_note": 52, "play_mode": 0, "loop_mode": 1, "loop_start": a, "loop_end": b,
                                      "attack": 1600.0, "decay": 1000.0, "sustain": 100.0, "release": 1800.0,
                                      "level": PFEIFE_PEGEL}, pfeife, 20.0, RAUM9)
    return z, 20.0, {"quelle_sha256": h1[0][:12] + "/" + h2[0][:12], "sample_frames": f"{ng}/{nc}", "schleife_pct": [round(a, 2), b]}


STUECKE = [("1", "schuss", neu_schuss, True), ("2", "klinge", neu_klinge, True), ("3", "aufheben", neu_aufheben, True),
           ("4", "skizze", neu_skizze, False)]


def pegel_je_sekunde(chans):
    n = len(chans[0])
    return [round(B.db(math.sqrt(sum(v * v for c in chans for v in c[i * SR:(i + 1) * SR]) / max(1, SR * len(chans)))))
            for i in range(n // SR)]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--nur", choices=["schuss", "klinge", "aufheben", "skizze"])
    args = ap.parse_args()
    TMP.mkdir(exist_ok=True)
    OUT9.mkdir(exist_ok=True)
    bericht = {}
    for nr, name, f, mono in STUECKE:
        if args.nur and args.nur != name:
            continue
        z, laenge, info = f()
        wav, h = B.lauf(name + "9", z, laenge)
        b = B.b_aus_wav(wav, mono)
        a = B.lies(OUT8 / f"{nr}b-{name}.wav")
        a, b, ziel = B.gleich_laut(a, b, laenge)
        if name == "skizze":
            la = B.ebur(_schreibe(OUT9 / f"{nr}a-{name}.wav", a))
            lb = B.ebur(_schreibe(OUT9 / f"{nr}b-{name}.wav", b))
            g = 10 ** ((la - lb) / 20)
            b = [[v * g for v in c] for c in b]
            a, b = B.kanten(a, 20, 500), B.kanten(b, 20, 500)
        pa, pb = _schreibe(OUT9 / f"{nr}a-{name}.wav", a), _schreibe(OUT9 / f"{nr}b-{name}.wav", b)
        bericht[name] = {"bitgleich": h[0] == h[1], "sha256_roh": h[0], "info": info, "roh_soundcraft": B.mess(B.lies(wav)),
                         "a": B.mess(a), "b": B.mess(b), "lufs_a": B.ebur(pa), "lufs_b": B.ebur(pb)}
        if name == "skizze":
            bericht[name]["pegel_je_sekunde_b"] = pegel_je_sekunde(b)
            bericht[name]["pegel_je_sekunde_a"] = pegel_je_sekunde(a)
        print(name, json.dumps(bericht[name], indent=1))
    (B.HERE / ("messwerte9-" + (args.nur or "alle") + ".json")).write_text(json.dumps(bericht, indent=1), encoding="utf-8")


def _schreibe(pfad, chans):
    B.schreibe(pfad, chans)
    return pfad


if __name__ == "__main__":
    main()
