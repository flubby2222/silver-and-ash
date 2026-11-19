#!/usr/bin/env python3
"""Hoerprobe Musikpegel (Issue #427): misst die heutigen Musikdateien und baut Proben "wie im Spiel".

Nur Standardbibliothek und ffmpeg. Erzeugt nichts neu, sondern verarbeitet assets/audio/musik-katakomben.ogg
(Ausschnitt) und assets/audio/p1-schuss-1.wav (Bezug) mit festen Pegelstufen.

Abspielfaktor im Spiel (nur gelesen, Core/Audio/SoundCues.cs, Game/Audio/SoundPlayer.cs), Regler auf Anschlag:
  Katakomben  = Datei * CatacombsMusicVolume 1.0 * Regler 1.0 * MasterVolume 0.7
  Hauptmenue  = Datei * TitleMusicVolume     0.8 * Regler 1.0 * MasterVolume 0.7
  Schuss      = Datei * SoundVolume          1.0 * Regler 1.0 * MasterVolume 0.7
Der Master 0.7 gilt fuer alles gleich; er steckt deshalb in allen Proben.

Aufruf: python bauen.py <repo-wurzel> <ausgabeordner>
"""
import array
import math
import subprocess
import sys
import wave
from pathlib import Path

RATE = 44100
MASTER = 0.7
MUSIK_FAKTOR = {"musik-katakomben.ogg": 1.0, "musik-hauptmenue.ogg": 0.8}
STUFEN = [("heute", 0.0), ("plus6", 6.0), ("plus10", 10.0)]
AUSSCHNITT_START = 20.0   # Sekunden in der Katakomben-Datei
AUSSCHNITT_LAENGE = 20.0
BLENDE = 0.5              # Ein-/Ausblenden der Ausschnitte in s (kein Knacken)
SPITZE_GRENZE_DB = -1.0


def db(x):
    return 20 * math.log10(x) if x > 0 else -999.0


def dekodiere(pfad):
    """Datei -> (Liste von Kanaelen als Float-Listen), 44,1 kHz, ueber ffmpeg."""
    kanaele = int(subprocess.check_output(
        ["ffprobe", "-v", "error", "-select_streams", "a:0", "-show_entries", "stream=channels",
         "-of", "csv=p=0", str(pfad)]).decode().strip())
    roh = subprocess.check_output(
        ["ffmpeg", "-v", "error", "-i", str(pfad), "-f", "s16le", "-ac", str(kanaele), "-ar", str(RATE), "-"])
    a = array.array("h")
    a.frombytes(roh)
    return [[a[i] / 32768.0 for i in range(k, len(a), kanaele)] for k in range(kanaele)]


def schreibe(pfad, kanaele):
    n = len(kanaele[0])
    a = array.array("h", [0] * (n * len(kanaele)))
    for k, ch in enumerate(kanaele):
        for i, v in enumerate(ch):
            a[i * len(kanaele) + k] = max(-32768, min(32767, int(round(v * 32767))))
    with wave.open(str(pfad), "wb") as w:
        w.setnchannels(len(kanaele))
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(a.tobytes())


def messe(pfad):
    """Spitze (Sample, dBFS), RMS (dBFS, ueber alle Kanaele), integrierte Lautheit (LUFS, ebur128), Wahre Spitze."""
    ch = dekodiere(pfad)
    spitze = max(max(abs(v) for v in c) for c in ch)
    quad = sum(v * v for c in ch for v in c) / sum(len(c) for c in ch)
    ausgabe = subprocess.run(
        ["ffmpeg", "-nostats", "-hide_banner", "-i", str(pfad), "-af", "ebur128=peak=true", "-f", "null", "-"],
        capture_output=True, text=True).stderr
    teil = ausgabe.split("Summary:")[-1]
    lufs = tp = None
    for zeile in teil.splitlines():
        z = zeile.strip()
        if z.startswith("I:"):
            lufs = float(z.split()[1])
        elif z.startswith("Peak:"):
            tp = float(z.split()[1])
    return {"laenge": len(ch[0]) / RATE, "spitze": db(spitze), "rms": db(math.sqrt(quad)), "lufs": lufs, "tp": tp}


def ausschnitt(ch, start, laenge):
    a, b = int(start * RATE), int((start + laenge) * RATE)
    out = [c[a:b] for c in ch]
    f = int(BLENDE * RATE)
    for c in out:
        for i in range(f):
            g = 0.5 - 0.5 * math.cos(math.pi * i / f)
            c[i] *= g
            c[-1 - i] *= g
    return out


def gain(ch, faktor):
    return [[v * faktor for v in c] for c in ch]


def stereo(ch):
    return ch if len(ch) == 2 else [ch[0], ch[0]]


def mischen(a, b, ab_sekunde):
    """b (Stereo) zu a (Stereo) addieren, ab ab_sekunde."""
    off = int(ab_sekunde * RATE)
    out = [list(c) for c in a]
    for k in range(2):
        for i, v in enumerate(b[k]):
            if off + i < len(out[k]):
                out[k][off + i] += v
    return out


def begrenze(ch):
    """Sanfte Begrenzung nur falls die Spitze ueber der Grenze liegt (tanh-Knie). Liefert (ch, benutzt)."""
    grenze = 10 ** (SPITZE_GRENZE_DB / 20)
    sp = max(max(abs(v) for v in c) for c in ch)
    if sp <= grenze:
        return ch, False
    knie = 0.5 * grenze
    def f(v):
        s = abs(v)
        if s <= knie:
            return v
        y = knie + (grenze - knie) * math.tanh((s - knie) / (grenze - knie))
        return math.copysign(y, v)
    return [[f(v) for v in c] for c in ch], True


def main():
    wurzel, ziel = Path(sys.argv[1]), Path(sys.argv[2])
    ziel.mkdir(parents=True, exist_ok=True)
    audio = wurzel / "assets" / "audio"
    zeilen = []

    print("== Bestand (Datei allein / wie im Spiel = Datei * Faktor * Master)")
    for name in ["musik-katakomben.ogg", "musik-hauptmenue.ogg", "ambiente-katakomben.ogg",
                 "p1-schuss-1.wav", "p1-schuss-geweiht-1.wav", "p1-treffer-1.wav", "p1-gorm-treffer-1.wav",
                 "p1-schmerz.wav", "p1-schritt-l1.wav", "pistole.wav"]:
        m = messe(audio / name)
        f = MUSIK_FAKTOR.get(name, 1.0) * MASTER
        zeilen.append((name, m, f))
        print(f"{name:28s} {m['laenge']:6.1f}s  Spitze {m['spitze']:7.1f}  RMS {m['rms']:7.1f}  "
              f"LUFS {m['lufs']:7.1f}  | im Spiel: Spitze {m['spitze'] + db(f):7.1f}  LUFS {m['lufs'] + db(f):7.1f}")

    musik = dekodiere(audio / "musik-katakomben.ogg")
    schuss = stereo(dekodiere(audio / "p1-schuss-1.wav"))
    teil = stereo(ausschnitt(musik, AUSSCHNITT_START, AUSSCHNITT_LAENGE))
    schuss_im_spiel = gain(schuss, MASTER)

    print("== Proben (Faktor Katakomben 1.0 * Master 0.7 * Stufe)")
    stufen_audio = {}
    for name, plus in STUFEN:
        faktor = MUSIK_FAKTOR["musik-katakomben.ogg"] * MASTER * 10 ** (plus / 20)
        a = gain(teil, faktor)
        a, begrenzt = begrenze(a)
        stufen_audio[name] = a
        pfad = ziel / f"{name}.wav"
        schreibe(pfad, a)
        m = messe(pfad)
        print(f"{pfad.name:12s} plus {plus:4.1f} dB  Spitze {m['spitze']:6.2f}  wahre Spitze {m['tp']}  "
              f"RMS {m['rms']:6.2f}  LUFS {m['lufs']}  begrenzt={begrenzt}")

    # Folge: je Stufe 10 s Ausschnitt, ein Schuss bei 5 s als Bezug, 1,5 s Pause dazwischen
    folge = [[], []]
    pause = int(1.5 * RATE)
    marken = []
    for name, plus in STUFEN:
        start_s = len(folge[0]) / RATE
        seg = ausschnitt(stufen_audio_roh(musik, plus), AUSSCHNITT_START, 10.0)
        seg = mischen(seg, schuss_im_spiel, 5.0)
        seg, _ = begrenze(seg)
        for k in range(2):
            folge[k].extend(seg[k])
            folge[k].extend([0.0] * pause)
        marken.append((name, start_s))
    pfad = ziel / "folge-heute-plus6-plus10-mit-schuss.wav"
    schreibe(pfad, folge)
    m = messe(pfad)
    print(f"{pfad.name}: {m['laenge']:.1f}s Spitze {m['spitze']:.2f} LUFS {m['lufs']}  Marken {marken}")

    # Schuss allein wie im Spiel (Bezug)
    pfad = ziel / "bezug-schuss.wav"
    schreibe(pfad, schuss_im_spiel)
    m = messe(pfad)
    print(f"{pfad.name}: Spitze {m['spitze']:.2f} RMS {m['rms']:.2f} LUFS {m['lufs']}")


def stufen_audio_roh(musik, plus):
    faktor = MUSIK_FAKTOR["musik-katakomben.ogg"] * MASTER * 10 ** (plus / 20)
    return stereo(gain(musik, faktor))


if __name__ == "__main__":
    main()
