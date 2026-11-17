"""Hoerprobe 9: dieselben vier Stuecke nach der Methode des Videos "How to make PS1 samples for FREE!" (SoundFontGuy), in SoundCraft.

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

def quelle_rauschen(name, laenge_s, filter_freq, filter_zeit_s, abfall_db, abfall_zeit_s, filter_typ=4, q=0.7):
    """Weisses Rauschen (Signalgenerator auf einer Audiospur) mit fallendem Tiefpass und fallender Lautstaerke (Automation)."""
    n = int(laenge_s * 44100)
    z = B.kopf(name)
    z += ['track.new ' + j(kind="audio", format="mono", name="N"),
          'audiosuite.process ' + j(process="signal_generator", params={"waveform": 4, "freq": 1000.0, "level": -6.0},
                                    tracks=["N"], start=0, end=n),
          'mix.insert ' + j(track="N", slot=0, plugin="eq_1band", params={"type": filter_typ, "freq": filter_freq[0], "q": q}),
          'automation.set_point ' + j(track="N", param="plugin:0:freq", at=0, value=filter_freq[0]),
          'automation.set_point ' + j(track="N", param="plugin:0:freq", at=int(filter_zeit_s * 44100), value=filter_freq[1]),
          'automation.set_point ' + j(track="N", param="volume", at=0, value=0.0),
          'automation.set_point ' + j(track="N", param="volume", at=int(abfall_zeit_s * 44100), value=abfall_db)]
    wav, h = B.lauf(name, z, laenge_s)
    return B.b_aus_wav(wav, True)[0], h


def quelle_synth(name, params, pitch, laenge_s, vel=100):
    """Eine gehaltene Note des Subtractive Synth, trocken (ohne Hall)."""
    z = B.kopf(name)
    z += ['track.new ' + j(kind="instrument", format="stereo", name="Q", instrument="subtractive_synth"),
          'mix.instrument ' + j(track="Q", plugin="subtractive_synth", params=params),
          'midi.new_clip ' + j(track="Q", start=0, end=int(laenge_s * 44100)),
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
        ln = (int(len_s * RATE) // BLOCK) * BLOCK
        l1 = l0 + ln
        xf = min(int(xf_s * RATE), l0)
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


# --- die vier Stuecke --------------------------------------------------------------------------------------------

def neu_schuss():
    """Rauschen (Tiefpass 9 kHz -> 500 Hz in 60 ms, Pegel -45 dB in 0,22 s) -> 22,05 kHz VAG -> Sampler, eine Oktave tiefer gespielt."""
    x, h = quelle_rauschen("q-schuss", 0.5, (9000.0, 500.0), 0.06, -45.0, 0.22)
    pfad, n, a, b = vag(x, "schuss")
    z = B.kopf("schuss9") + sampler_spur("Schuss", pfad, {"root_note": 60, "play_mode": 1, "loop_mode": 0, "attack": 0.1,
                                                         "decay": 400.0, "sustain": 100.0, "release": 100.0},
                                         [(48, 0, 120, 127)], 1.0, RAUM_SOUND)
    return z, 0.8, {"quelle_sha256": h[0][:12], "sample_frames": n}


def neu_klinge():
    """Rauschen mit schnell fallendem Tiefpass (6 kHz -> 300 Hz in 15 ms), 22,05 kHz VAG, eine Quinte hoeher gespielt."""
    x, h = quelle_rauschen("q-klinge", 0.4, (6000.0, 300.0), 0.015, -40.0, 0.24)
    pfad, n, a, b = vag(x, "klinge")
    z = B.kopf("klinge9") + sampler_spur("Klinge", pfad, {"root_note": 60, "play_mode": 1, "loop_mode": 0, "attack": 0.1,
                                                         "decay": 300.0, "sustain": 100.0, "release": 100.0},
                                         [(67, 0, 120, 127)], 1.0, RAUM_SOUND)
    return z, 0.8, {"quelle_sha256": h[0][:12], "sample_frames": n}


GLOCKE = {"osc1_wave": 2, "osc2_wave": 2, "osc2_semitones": 15.0, "osc2_detune": 0.0, "osc_mix": 30.0, "cutoff": 1800.0,
          "resonance": 0.0, "filter_env_amount": 0.0, "amp_attack": 3.0, "amp_decay": 5000.0, "amp_sustain": 0.0,
          "amp_release": 3000.0, "level": -3.0}


def neu_aufheben():
    """Glocken-Sample (zwei Sinus, 15 Halbtoene Abstand) bei H3 aufgenommen, bei H4 gespielt (eine Oktave hoeher, Sample spielt doppelt so schnell)."""
    p = dict(GLOCKE, amp_decay=1800.0, cutoff=6000.0)
    x, h = quelle_synth("q-aufheben", p, 59, 2.0)
    pfad, n, a, b = vag(x, "aufheben")
    z = B.kopf("aufheben9") + sampler_spur("Aufheben", pfad, {"root_note": 59, "play_mode": 1, "loop_mode": 0, "attack": 0.1,
                                                             "decay": 4000.0, "sustain": 100.0, "release": 200.0},
                                           [(71, 0, 960, 100)], 1.2, RAUM_SOUND)
    return z, 1.2, {"quelle_sha256": h[0][:12], "sample_frames": n}


CHOR = {"osc1_wave": 0, "osc2_wave": 0, "osc2_semitones": 0.0, "osc2_detune": 14.0, "osc_mix": 50.0, "cutoff": 520.0,
        "resonance": 12.0, "filter_env_amount": 0.0, "amp_attack": 40.0, "amp_decay": 500.0, "amp_sustain": 100.0,
        "amp_release": 300.0, "level": -6.0}


def neu_skizze():
    """Zwei Sample-Instrumente: Glocke (bei E2 aufgenommen, ausklingend, als Einzelschlag) und Chor (bei E3 aufgenommen, 2,8 s lang,
    Schleife 0,8 s ... 2,8 s auf Vielfache von 28 gelegt, 0,5 s linear ueberblendet); beide durch die Tonhoehenverschiebung des Samplers
    auf die Akkordtoene gebracht (wie auf der PS1: ein Sample je Klang, viele Tonhoehen). Dieselben Noten wie in Hoerprobe 8."""
    t = B.TICKS_PRO_SCHLAG
    akkorde = [(0, 8, [52, 55, 59, 64]), (8, 8, [52, 55, 60, 64]), (16, 4, [52, 57, 60, 64])]
    bell = [(0, 8, 52, 1.0), (8, 8, 52, 0.9), (16, 8, 47, 1.0)]
    xg, h1 = quelle_synth("q-glocke", GLOCKE, 40, 6.0)
    pg, ng, _, _ = vag(xg, "glocke")
    xc, h2 = quelle_synth("q-chor", CHOR, 52, 3.4)
    pc, nc, a, b = vag(xc, "chor", schleife=(0.8, 2.0, 0.5))
    chor = [(m, s * t, int((d + 0.3) * t), 90) for s, d, ch in akkorde for m in ch]
    glocke = [(m, s * t, int(min(d, 20 - s) * t), int(100 * v)) for s, d, m, v in bell]
    z = B.kopf("skizze9", bpm=60)
    z += sampler_spur("Glocke", pg, {"root_note": 40, "play_mode": 1, "loop_mode": 0, "attack": 0.1, "decay": 5000.0,
                                      "sustain": 100.0, "release": 1500.0, "level": -1.0}, glocke, 20.0, RAUM_SKIZZE)
    z += sampler_spur("Chor", pc, {"root_note": 52, "play_mode": 0, "loop_mode": 1, "loop_start": a, "loop_end": b,
                                    "attack": 1600.0, "decay": 1000.0, "sustain": 100.0, "release": 1800.0, "level": -13.0},
                      chor, 20.0, RAUM_SKIZZE)
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
