"""SoundCraft-Versuch: baut Hoerprobe 8 (a = bisheriger Weg, b = SoundCraft) und misst.

Aufruf:  python bauen.py [--nur schuss|klinge|aufheben|skizze]
Nur Python-Standardbibliothek, ffmpeg und soundcraft-cli.exe (nie soundcraft.exe, kein MCP, kein Netz).

Ablauf je Stueck:
  1. Skriptdatei skripte/<name>.txt (Zeilen "ID JSON") wird aus den Parametern unten geschrieben.
     Clip-Nummern vergibt SoundCraft selbst; sie werden mit einem Probelauf ohne Ausgabe ermittelt.
  2. soundcraft-cli script ... zweimal -> die beiden WAV-Dateien muessen bitgleich sein.
  3. b = SoundCraft-Ausgabe -> Mono (Sounds) -> PS1-Kette aus uebergabe/tools/audio (dieselbe wie bei a).
  4. a und b gleich laut (Energie ueber gleiche Laenge), Spitze <= -1 dBFS; Dateien in hoerprobe-8-soundcraft/.
"""
import argparse
import hashlib
import json
import math
import re
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
TOOLS = HERE.parent  # tools/audio (synth.py, ps1_klang.py, ...)
sys.path.insert(0, str(TOOLS))

import ableiten as A  # noqa: E402
import geraeusche_hp3 as G  # noqa: E402
import musik_klang as K  # noqa: E402
import ps1_klang as P  # noqa: E402
from synth import SR, finish, n_of, write_wav  # noqa: E402

CLI = Path(r"C:\Program Files\ArtCraft\SoundCraft\soundcraft-cli.exe")
CLI_SHA256 = None  # wird beim ersten Lauf in HERKUNFT.md festgehalten (siehe pruefe_programm)
OUT = HERE / "hoerprobe-8-soundcraft"
SKR = HERE / "skripte"
TMP = Path(tempfile.gettempdir()) / "sc-versuch"
PS1_RATE = 22050
TICKS_PRO_SCHLAG = 960  # gemessen: 1920 Ticks = 1 s bei 120/min, also 960 je Viertel

# --- gemeinsamer Raum: auf jeder Spur dieselbe Hall-Einstellung (Hall ist linear, gleichwertig zu einem Bus) ------------
RAUM_SOUND = {"mix": 22, "pre_delay": 4, "decay": 0.5, "size": 25, "damping": 4500, "diffusion": 70, "low_cut": 150}
RAUM_SKIZZE = {"mix": 35, "pre_delay": 20, "decay": 2.8, "size": 80, "damping": 2500, "diffusion": 85, "low_cut": 150}


def j(**kw):
    return json.dumps(kw, separators=(",", ":"))


PLATZHALTER = "{TMP}"  # steht in skripte/*.txt fuer den Zwischenordner TMP (dort liegt der Benutzername)


def mit_platzhalter(text):
    """Ersetzt den absoluten Zwischenordner durch {TMP}, damit eingecheckte Skripte keinen lokalen Pfad enthalten."""
    return text.replace(TMP.as_posix(), PLATZHALTER)


def aufgeloest(text):
    """Gegenstueck zu mit_platzhalter: setzt den Zwischenordner dieses Rechners ein, bevor SoundCraft das Skript liest."""
    return text.replace(PLATZHALTER, TMP.as_posix())


def kopf(name, bpm=120):
    return [f"# {name} - SoundCraft-Skript (soundcraft-cli script). Erzeugt von bauen.py, nicht von Hand aendern.",
            "session.new " + j(name=name, sample_rate=44100, bit_depth=16),
            f"event.tempo " + j(bpm=bpm)]


def spur(name, instrument, params, notes, ende_s, raum_par, inserts=(), gain_db=0.0):
    """Instrumentenspur: Parameter, eigene Effekte (inserts), dann der gemeinsame Raum (gleiche Einstellung auf jeder Spur,
    weil sich auf Bussen in der Kommandozeile keine Effekte einsetzen lassen), ein MIDI-Clip. notes: (pitch, start_ticks, laenge_ticks, velocity)."""
    z = ['track.new ' + j(kind="instrument", format="stereo", name=name, instrument=instrument)]
    z += ['mix.instrument ' + j(track=name, plugin=instrument, params=params)] if params else []
    for slot, (plugin, par) in enumerate(list(inserts) + [("room_reverb", raum_par)]):
        z += ['mix.insert ' + j(track=name, slot=slot, plugin=plugin, params=par)]
    if gain_db:
        z += ['mix.volume ' + j(tracks=[name], db=gain_db)]
    z += ['midi.new_clip ' + j(track=name, start=0, end=int(ende_s * 44100))]
    z += ["@NOTEN " + name + " " + json.dumps(notes)]
    return z


def aufloesen(zeilen, name):
    """Ermittelt die Clip-Nummern durch einen Probelauf (ohne Noten) und setzt die Notenzeilen ein."""
    vor = [z for z in zeilen if not z.startswith("@NOTEN")]
    p = TMP / f"{name}-probe.txt"
    p.write_text("\n".join(vor) + "\n", encoding="utf-8")
    r = subprocess.run([str(CLI), "script", str(p)], capture_output=True, text=True, check=True)
    ids = [int(m) for m in re.findall(r"midi\.new_clip: \{\"clip\":(\d+)", r.stdout)]
    out, k = [], 0
    for z in zeilen:
        if z.startswith("@NOTEN"):
            _, spurname, rest = z.split(" ", 2)
            for pitch, st, ln, vel in json.loads(rest):
                out.append("midi.note_add " + j(clip=ids[k], pitch=pitch, start_ticks=st, length_ticks=ln, velocity=vel))
            k += 1
        else:
            out.append(z)
    return out


def lauf(name, zeilen, ende_s):
    """Schreibt skripte/<name>.txt, laesst SoundCraft zweimal rendern, prueft Bitgleichheit, gibt (Pfad, sha256) zurueck."""
    zeilen = aufloesen(zeilen, name)
    pfad_wav = TMP / f"{name}.wav"
    zeilen.append("file.bounce_mix " + j(path=str(pfad_wav).replace("\\", "/"), format="wav", bit_depth=16,
                                         start=0, end=int(ende_s * 44100), dither=False))
    text = "\n".join(zeilen) + "\n"
    SKR.mkdir(exist_ok=True)
    # Eingecheckt wird die Fassung mit Platzhalter (kein Benutzername im oeffentlichen Repository, #295);
    # SoundCraft bekommt die aufgeloeste Fassung aus TMP, die byte-gleich zur frueheren eingecheckten Datei ist.
    (SKR / f"{name}.txt").write_text(mit_platzhalter(text), encoding="utf-8", newline="\n")
    skript = TMP / f"{name}.txt"
    skript.write_text(text, encoding="utf-8", newline="\n")
    hashes = []
    for i in range(2):
        if pfad_wav.exists():
            pfad_wav.unlink()
        r = subprocess.run([str(CLI), "script", str(skript)], capture_output=True, text=True)
        if r.returncode != 0:
            raise SystemExit(f"{name}: soundcraft-cli Fehler\n{r.stdout}\n{r.stderr}")
        hashes.append(hashlib.sha256(pfad_wav.read_bytes()).hexdigest())
    print(f"{name}: zwei Laeufe {'BITGLEICH' if hashes[0] == hashes[1] else 'VERSCHIEDEN'}  sha256 {hashes[0][:16]}")
    return pfad_wav, hashes


# --- Messung ---------------------------------------------------------------------------------------------------

def lies(pfad):
    ch, _ = P.read_wav(str(pfad))
    return [[v / 32768.0 for v in c] for c in ch]


def db(x):
    return -200.0 if x <= 0 else 20 * math.log10(x)


def mess(chans, rate=SR):
    n = len(chans[0])
    peak = max(max(abs(v) for v in c) for c in chans)
    rms = math.sqrt(sum(v * v for c in chans for v in c) / (n * len(chans)))
    mono = [sum(c[i] for c in chans) / len(chans) for i in range(n)]
    # Spektrum-Schwerpunkt aus dem Betragsspektrum (Goertzel-Gitter in Oktavdritteln wuerde reichen; hier einfach DFT auf 1 s Ausschnitt)
    m = min(n, 16384)
    w = mono[:m]
    cen, bands = schwerpunkt(w, rate)
    return dict(peak_db=round(db(peak), 1), rms_db=round(db(rms), 1), centroid_hz=round(cen), seconds=round(n / rate, 2),
                low_db=bands[0], mid_db=bands[1], high_db=bands[2])


def schwerpunkt(w, rate):
    m = len(w)
    nb = 120
    fs = [40 * (16000 / 40) ** (k / (nb - 1)) for k in range(nb)]
    num = den = 0.0
    band = [0.0, 0.0, 0.0]
    for f in fs:
        wk = 2 * math.pi * f / rate
        c = 2 * math.cos(wk)
        s1 = s2 = 0.0
        for v in w:
            s0 = v + c * s1 - s2
            s2, s1 = s1, s0
        p = s1 * s1 + s2 * s2 - c * s1 * s2
        num += f * p
        den += p
        band[0 if f < 300 else 1 if f < 3000 else 2] += p
    return (num / den if den else 0.0), [round(10 * math.log10(b / den), 1) if b > 0 and den else -99.0 for b in band]


def ebur(pfad):
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(pfad), "-af", "ebur128=peak=true", "-f", "null", "-"],
                       capture_output=True, text=True)
    m = re.search(r"I:\s+(-?[\d.]+) LUFS", r.stderr[r.stderr.rfind("Summary"):])
    return float(m.group(1)) if m else None


def gleich_laut(a, b, laenge_s, ziel_peak_db=-1.0):
    """a, b: Kanallisten. Auf gleiche Laenge, gleiche Energie (RMS ueber alles); gemeinsamer Pegel = lauterer Wert, der beide unter -1 dBFS laesst."""
    n = n_of(laenge_s)

    def fix(chans):
        return [(c + [0.0] * n)[:n] for c in chans]
    a, b = fix(a), fix(b)
    ma, mb = mess(a), mess(b)
    # Ziel-RMS: das kleinere der beiden erreichbaren (so bleibt keine Datei ueber der Spitze)
    erreichbar_a = ma["rms_db"] + (ziel_peak_db - ma["peak_db"])
    erreichbar_b = mb["rms_db"] + (ziel_peak_db - mb["peak_db"])
    ziel = min(erreichbar_a, erreichbar_b)
    ga, gb = 10 ** ((ziel - ma["rms_db"]) / 20), 10 ** ((ziel - mb["rms_db"]) / 20)
    return [[v * ga for v in c] for c in a], [[v * gb for v in c] for c in b], ziel


def kanten(chans, ein_ms=1.0, aus_ms=30.0):
    ne, na = int(ein_ms * SR / 1000), int(aus_ms * SR / 1000)
    out = []
    for c in chans:
        c = list(c)
        for i in range(min(ne, len(c))):
            c[i] *= i / ne
        for i in range(min(na, len(c))):
            c[-1 - i] *= i / na
        out.append(c)
    return out


# --- die drei Geraeusche -------------------------------------------------------------------------------------------

def sc_schuss():
    """Ein Schlag: Drum Synth (Snare, tief gestimmt, kurz, scharf), leicht gesaettigt. Ein Ereignis, ein Ton."""
    z = kopf("schuss")
    z += spur("Schuss", "drum_synth", {"level": -3.0, "snare_tune": -9.0, "snare_snappy": 100.0, "snare_decay": 90.0},
              [(38, 0, 120, 127)], 1.0, RAUM_SOUND,
              inserts=[("saturator", {"drive": 14.0, "tone": 5000.0, "mix": 100.0, "output": -6.0})])
    return z


def sc_klinge():
    """Ein Streich: Drum Synth (offene Hi-Hat, nur das Rauschen) kurz, durch Tiefpass gedaempft - geraeuschhaft, kein Ton."""
    z = kopf("klinge")
    z += spur("Klinge", "drum_synth", {"level": 6.0, "open_hat_decay": 100.0}, [(46, 0, 120, 127)], 1.0, RAUM_SOUND,
              inserts=[("eq_1band", {"type": 4, "freq": 5500.0, "q": 0.7})])
    return z


def sc_aufheben():
    """Ein heller Ton in der Tonart (H4 = Quinte von e-Moll): zwei Sinus im Abstand 15 Halbtoene (unharmonisch, glockig), schnell abklingend."""
    z = kopf("aufheben")
    z += spur("Aufheben", "subtractive_synth",
              {"osc1_wave": 2, "osc2_wave": 2, "osc2_semitones": 15.0, "osc2_detune": 0.0, "osc_mix": 30.0, "cutoff": 6000.0,
               "resonance": 0.0, "filter_env_amount": 0.0, "amp_attack": 2.0, "amp_decay": 450.0, "amp_sustain": 0.0,
               "amp_release": 200.0, "level": -3.0}, [(71, 0, 960, 100)], 1.2, RAUM_SOUND)
    return z


# --- Skizze: dieselben Noten wie Teil A des Menue-Themas (musik_erzeugen.py) ---------------------------------------

def sc_skizze():
    """20 s, 60/min (1 Schlag = 960 Ticks = 1 s). Glocke (zwei Sinus, unharmonisch) + Chor (zwei verstimmte Saegen, tiefer Tiefpass,
    langsamer Einsatz), gemeinsamer Raum. Kein Herzschlag."""
    t = TICKS_PRO_SCHLAG  # SoundCraft rechnet mit 960 Ticks je Viertel (nicht 480)
    akkorde = [(0, 8, [52, 55, 59, 64]), (8, 8, [52, 55, 60, 64]), (16, 4, [52, 57, 60, 64])]
    bell = [(0, 8, 52, 1.0), (8, 8, 52, 0.9), (16, 8, 47, 1.0)]
    chor = [(m, s * t, int((d + 0.3) * t), 90) for s, d, ch in akkorde for m in ch]
    glocke = [(m, s * t, int(min(d, 20 - s) * t), int(100 * v)) for s, d, m, v in bell]
    z = kopf("skizze", bpm=60)
    z += spur("Glocke", "subtractive_synth",
              {"osc1_wave": 2, "osc2_wave": 2, "osc2_semitones": 15.0, "osc2_detune": 0.0, "osc_mix": 30.0, "cutoff": 1800.0,
               "resonance": 0.0, "filter_env_amount": 0.0, "amp_attack": 3.0, "amp_decay": 5000.0, "amp_sustain": 0.0,
               "amp_release": 3000.0, "level": -9.0}, glocke, 20.0, RAUM_SKIZZE)
    z += spur("Chor", "subtractive_synth",
              {"osc1_wave": 0, "osc2_wave": 0, "osc2_semitones": 0.0, "osc2_detune": 14.0, "osc_mix": 50.0, "cutoff": 520.0,
               "resonance": 12.0, "filter_env_amount": 0.0, "amp_attack": 1600.0, "amp_decay": 1000.0, "amp_sustain": 100.0,
               "amp_release": 1800.0, "level": -22.0}, chor, 20.0, RAUM_SKIZZE)
    return z


# --- a-Seiten (bisheriger Weg, Python) ---------------------------------------------------------------------------

def a_schuss():
    return [G.ps1(G.pistole())]


def a_klinge():
    # wie uebergabe/tools/audio/erzeugen.py: klinge(1) durch to_ps1
    sys.path.insert(0, str(TOOLS))
    import erzeugen as E  # noqa: E402
    return [A.to_ps1(E.klinge(1))]


def a_aufheben():
    x = K.bell(71, 0.8, 1.0, seed=1)
    x = finish(x, -4.0, length=0.8, fade_out_ms=60)
    return [G.ps1(x)]


def a_skizze(quelle):
    """Die ersten 20 s des fertigen Menue-Themas (ohne Herzschlag), aus der OGG-Datei."""
    TMP.mkdir(exist_ok=True)
    w = TMP / "a-skizze.wav"
    subprocess.run(["ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-i", str(quelle), "-t", "20", str(w)], check=True)
    return lies(w)


# --- b-Seiten: SoundCraft -> Mono -> PS1 --------------------------------------------------------------------------

def b_aus_wav(pfad, mono=True):
    ch = lies(pfad)
    if mono:
        ch = [[sum(c[i] for c in ch) / len(ch) for i in range(len(ch[0]))]]
    return ch


def ps1_float(chans):
    ints = [[int(round(max(-1.0, min(1.0, v)) * 32767)) for v in c] for c in chans]
    out = P.process(ints, rate=PS1_RATE)
    return [[v / 32767.0 for v in c] for c in out]


def schreibe(pfad, chans):
    write_wav(pfad, chans)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--nur", choices=["schuss", "klinge", "aufheben", "skizze"])
    ap.add_argument("--menue", default=str(HERE.parents[2] / "assets" / "audio" / "musik-hauptmenue.ogg"))
    args = ap.parse_args()
    TMP.mkdir(exist_ok=True)
    OUT.mkdir(exist_ok=True)
    sha = hashlib.sha256(CLI.read_bytes()).hexdigest()
    ver = subprocess.run([str(CLI), "--version"], capture_output=True, text=True).stdout.strip()
    print("soundcraft-cli", ver, "sha256", sha)
    stuecke = [
        ("1", "schuss", sc_schuss, a_schuss, 0.8, True),
        ("2", "klinge", sc_klinge, a_klinge, 0.8, True),
        ("3", "aufheben", sc_aufheben, a_aufheben, 1.2, True),
        ("4", "skizze", sc_skizze, None, 20.0, False),
    ]
    bericht = {"cli": ver, "cli_sha256": sha, "stuecke": {}}
    for nr, name, sc, a, laenge, mono in stuecke:
        if args.nur and args.nur != name:
            continue
        wav, hashes = lauf(name, sc(), laenge)
        rohmess = mess(lies(wav))
        b = ps1_float(b_aus_wav(wav, mono))
        a_ch = a() if a else a_skizze(args.menue)
        a_ch, b, ziel = gleich_laut(a_ch, b, laenge)
        if name == "skizze":
            a_ch, b = kanten(a_ch, 20, 500), kanten(b, 20, 500)
        schreibe(OUT / f"{nr}a-{name}.wav", a_ch)
        schreibe(OUT / f"{nr}b-{name}.wav", b)
        if name == "skizze":
            # lange Datei: nach Lautheit (LUFS) angleichen statt nach Energie; b wird entsprechend leiser/lauter
            la, lb = ebur(OUT / f"{nr}a-{name}.wav"), ebur(OUT / f"{nr}b-{name}.wav")
            g = 10 ** ((la - lb) / 20)
            b = [[v * g for v in c] for c in b]
            schreibe(OUT / f"{nr}b-{name}.wav", b)
        bericht["stuecke"][name] = {
            "bitgleich": hashes[0] == hashes[1], "sha256_roh": hashes[0], "roh_soundcraft": rohmess,
            "a": mess(a_ch), "b": mess(b), "ziel_rms_db": ziel,
            "lufs_a": ebur(OUT / f"{nr}a-{name}.wav"), "lufs_b": ebur(OUT / f"{nr}b-{name}.wav"),
        }
        print(name, json.dumps(bericht["stuecke"][name], indent=1))
    (HERE / ("messwerte-" + (args.nur or "alle") + ".json")).write_text(json.dumps(bericht, indent=1), encoding="utf-8")


if __name__ == "__main__":
    main()
