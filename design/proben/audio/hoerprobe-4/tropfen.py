"""Hoerprobe 4 – Tropfen ohne Klingeln, drei Fassungen, alle durch die PS1-Kette.

Ursache des Klingelns in Hoerprobe 3 (gemessen, siehe messwerte.md):
  1. Der Tropfen war ein einzelner reiner Sinus (ein schmaler Ton) mit 20 ms
     Zeitkonstante; er faellt erst nach 0,14 s um 60 dB.
  2. Der Hoehlenhall (vier Verzoegerungen 62/100/162/262 ms, Rueckfuehrung 0,45)
     wirft diesen reinen Ton als einzelne, deutlich getrennte Echos (je etwa -11 dB)
     zurueck und laesst ihn ueber 1 s kreisen. Ein reiner Ton plus wenige,
     duenne Echos ist genau ein Klingeln.

Gegenmittel, je Fassung eines mehr:
  ambiente-a-kurz      Ton kurz (Zeitkonstante 5 ms), KEIN Hoehlenhall, nur ein leises, dumpfes Echo nach 40 ms
  ambiente-b-breit     kurzer Ton (6 ms) plus Aufschlag-Rauschen (Bandpass, breit), dichter, gedaempfter
                       Raum (Schroeder, RT60 0,4 s, Daempfung 1,5 kHz) statt einzelner Echos
  ambiente-c-geraeusch kein reiner Ton mehr: Rauschen durch einen breiten, nach oben gleitenden Bandpass
                       (Guete 3), 8 ms, dazu derselbe dichte, gedaempfte Raum wie b

Alles andere wie in Hoerprobe 3 (Grundflaeche, sieben Zeitpunkte, Tonlage anderthalb
Oktaven tiefer). Zusaetzlich je Fassung eine kurze Datei mit vier einzelnen Tropfen
(tropfen-<v>-einzeln.wav), damit man den Tropfen ohne Flaeche hoert.

Aufruf: python tropfen.py
"""
import importlib.util
import math
import random
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
HP1 = HERE.parent / "hoerprobe-1"
HP3 = HERE.parent / "hoerprobe-3"
sys.path.insert(0, str(HP1))
sys.path.insert(0, str(HP3))

import ps1_klang as P  # noqa: E402
from synth import SR, add_into, env_ad, lp1, mul, n_of, reverb, svf, white, write_wav, zeros  # noqa: E402

spec = importlib.util.spec_from_file_location("ambiente_hp3", HP3 / "ambiente.py")
hp3 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(hp3)

FACTOR = 2.0 ** -1.5


def tone(f_a, f_b, tau, ln):
    out = [0.0] * ln
    ph = 0.0
    for i in range(ln):
        t = i / ln
        ph += f_a * (f_b / f_a) ** (1.0 - math.exp(-5.0 * t)) / SR
        out[i] = math.sin(2.0 * math.pi * ph) * math.exp(-i / (tau * SR)) * min(1.0, i / (0.001 * SR))
    return out


def room(x):
    """Dichter, gedaempfter Raum statt einzelner Echos."""
    wet = reverb(x, rt60=0.4, size=1.0, damp_hz=1500.0, tail=0.5, seed=4)
    return add_into(lp1(wet, 2500.0), x, 0, 1.0 / 0.35)


def drip_a(rng):
    f_a = rng.uniform(3000.0, 4200.0) * FACTOR
    f_b = f_a * rng.uniform(1.15, 1.3)
    t = tone(f_a, f_b, 0.005, n_of(0.05))
    full = zeros(n_of(0.2))
    add_into(full, t, 0, 1.0)
    add_into(full, lp1(t, 1200.0), n_of(0.04), 0.15)
    return full


def drip_b(rng):
    f_a = rng.uniform(3000.0, 4200.0) * FACTOR
    f_b = f_a * rng.uniform(1.15, 1.3)
    n = n_of(0.06)
    t = tone(f_a, f_b, 0.006, n)
    hit = mul(svf(white(n, random.Random(int(f_a))), 2500.0, 1.0, "bp"), env_ad(n, 0.0005, 0.003))
    x = add_into(t, hit, 0, 0.6)
    return [v * 0.35 for v in room(x)]


def drip_c(rng):
    f_a = rng.uniform(3000.0, 4200.0) * FACTOR
    f_b = f_a * rng.uniform(1.15, 1.3)
    n = n_of(0.06)
    sweep = [f_a * (f_b / f_a) ** (1.0 - math.exp(-5.0 * i / n)) for i in range(n)]
    x = mul(svf(white(n, random.Random(int(f_a))), sweep, 3.0, "bp"), env_ad(n, 0.001, 0.008))
    m = max(abs(v) for v in x) or 1.0
    x = [v / m for v in x]
    return [v * 0.35 for v in room(x)]


VARIANTS = {"a-kurz": drip_a, "b-breit": drip_b, "c-geraeusch": drip_c}


def build(fn):
    hp3.hp1.drip = fn
    return hp3.hp1.variant("b")


def singles(fn):
    """Vier Tropfen im Abstand von 0,8 s, ohne Flaeche (Hoerhilfe)."""
    rng = random.Random(77)
    out = zeros(n_of(3.6))
    for k in range(4):
        add_into(out, fn(rng), n_of(0.1 + 0.8 * k), 0.5)
    return out


def main():
    for key, fn in VARIANTS.items():
        left, right = build(fn)
        chans = hp3.ps1_loop([left, right])
        wav = HERE / f"ambiente-{key}-ps1.wav"
        write_wav(wav, chans)
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav), "-c:a", "libvorbis", "-q:a", "5",
                        str(wav.with_suffix(".ogg"))], check=True)
        s = singles(fn)
        ints = [int(round(v * 32767)) for v in s]
        out = P.process([ints], rate=22050)[0]
        write_wav(HERE / f"tropfen-{key}-einzeln-ps1.wav", [[v / 32767.0 for v in out]])
        print(key)


if __name__ == "__main__":
    main()
