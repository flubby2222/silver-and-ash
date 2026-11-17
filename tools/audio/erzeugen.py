"""Uebergabe Audio – erzeugt alle Spiel-Dateien fuer assets/audio/ (Stand nach Hoerprobe 5 kurz, Urteil "b").

Aufruf:
  python erzeugen.py --aus <ordner> [--quellen <ordner>]
    --aus      Zielordner (z. B. assets/audio)
    --quellen  Ordner mit den Kenney-Originalen ausserhalb des Repositorys
               (Standard %LOCALAPPDATA%\\SilverAndAsh\\klangquellen), erwartet
               <quellen>/kenney/impact-sounds/inhalt/impactTin_medium_004.ogg

Dateien (alle durch die PS1-Kette aus ps1_klang.py, 16 Bit, 44 100 Hz):
  pistole.wav               mono   synthetisch (Hoerprobe 3, PS1-Fassung)
  klinge-1..3.wav           mono   synthetisch, heller (Hoerprobe 3 + hellere Klangfarbe), 3 leichte Varianten
  schritt-1..4.wav          mono   synthetisch (Hoerprobe 3, PS1-Fassung)
  ghul-ankuendigung.wav     mono   synthetisch (Hoerprobe 3, PS1-Fassung) – VORLAEUFIG
  ambiente-katakomben.ogg   stereo 30 s nahtlos; Tropfen aus der Kenney-Aufnahme impactTin_medium_004.ogg

Ein Lauf erzeugt dieselben Bytes wie der vorige (feste Zufallszahlen; ffmpeg mit bitexact).
Nur Python-Standardbibliothek und ffmpeg.
"""
import argparse
import hashlib
import math
import random
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import ableiten as A  # noqa: E402
import ambiente_hp3 as AMB  # noqa: E402
import geraeusche_hp3 as G  # noqa: E402
import quelle as Q  # noqa: E402
from synth import SR, add_into, curve, env_ad, finish, mul, n_of, normalize, osc_sine, svf, white, write_wav  # noqa: E402

# Pruefsumme des Originals (Impact Sounds 1.0, kenney.nl, abgerufen 2026-11-16)
TIN_SHA256 = "fd53e24efec3d5c1cee5a46fee70f97efd4bd01e53ac5d9b5f3b6719dfe9f5aa"


def klinge(take):
    """Hoerprobe 3 (Tiefpass mit fallender Grenzfrequenz + Koerperton), heller NUR ueber die Klangfarbe
    desselben Ereignisses: Tiefpass startet bei 4,5 kHz statt 1,8 kHz, Anschlag 0,2 ms statt 1 ms,
    Koerperton 140 -> 70 Hz statt 110 -> 55 Hz. Der leise Klick aus Hoerprobe 3 entfaellt.
    Drei Varianten: Tiefpass-Start +-10 %, Tonhoehe +-8 %, Pegel bis -1,5 dB."""
    rng = random.Random(14100 + take)
    j = rng.uniform
    n = n_of(0.24)
    fc0 = 4500.0 * j(0.9, 1.1)
    cut = [180.0 + (fc0 - 180.0) * math.exp(-i / (0.010 * SR)) for i in range(n)]
    body = mul(svf(white(n, rng), cut, 1.4, "lp"), env_ad(n, 0.0002, 0.03))
    p = j(0.92, 1.08)
    tone = mul(osc_sine(curve(n, [(0.0, 140.0 * p), (0.08, 70.0 * p)])), env_ad(n, 0.0005, 0.045))
    x = add_into(normalize(body, 1.0), tone, 0, 0.6)
    return finish(x, -4.0 + j(-1.5, 0.0), length=0.24, fade_out_ms=40)


def tropfen(root):
    """Tropfen aus impactTin_medium_004.ogg (Kenney, CC0): geschnitten, 7 Halbtoene tiefer (+-2 je Tropfen),
    Tiefpass 2,5 kHz, ohne eigenen Raum. Jede Funktion verbraucht genau zwei Zufallszahlen je Tropfen wie in
    Hoerprobe 1-5, damit die sieben Zeitpunkte der Flaeche erhalten bleiben."""
    path = Q.find(root, "impact-sounds", "impactTin_medium_004.ogg")
    digest = Q.sha256(path)
    if digest != TIN_SHA256:
        raise SystemExit(f"{path}: SHA-256 {digest} stimmt nicht mit {TIN_SHA256} ueberein (anderes Original?)")
    cache = svf(A.clean(A.cut_gesture(Q.load(path), 0.15)), 2500.0, 0.707, "lp")

    def drip(rng):
        st = -7 + rng.uniform(-2.0, 2.0)
        g = rng.uniform(0.8, 1.0)
        y = normalize(A.varispeed(cache, st), 1.0)
        return [v * 0.35 * g for v in list(y) + [0.0] * n_of(0.05)]
    return drip


def ambiente(root, out):
    AMB.hp1.drip = tropfen(root)
    left, right = AMB.hp1.variant("b")
    chans = AMB.ps1_loop([left, right])
    wav = out / "_ambiente.wav"
    write_wav(wav, chans)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav), "-c:a", "libvorbis", "-q:a", "5", "-map_metadata", "-1",
                    "-fflags", "+bitexact", "-flags:a", "+bitexact",
                    str(out / "ambiente-katakomben.ogg")], check=True)
    wav.unlink()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--aus", required=True)
    ap.add_argument("--quellen", default=str(Q.default_root()))
    a = ap.parse_args()
    out = Path(a.aus)
    out.mkdir(parents=True, exist_ok=True)

    write_wav(out / "pistole.wav", [G.ps1(G.pistole())])
    for t in range(1, 4):
        write_wav(out / f"klinge-{t}.wav", [A.to_ps1(klinge(t))])
    for t in range(1, 5):
        write_wav(out / f"schritt-{t}.wav", [G.ps1(G.schritt(t))])
    write_wav(out / "ghul-ankuendigung.wav", [G.ps1(G.ghul())])
    ambiente(a.quellen, out)
    for p in sorted(out.iterdir()):
        print(p.name, hashlib.sha256(p.read_bytes()).hexdigest()[:16])


if __name__ == "__main__":
    main()
