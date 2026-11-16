"""Hoerprobe 2 – Katakomben-Ambiente mit tieferen Tropfen.

Grundlage ist Variante B aus Hoerprobe 1 (ambiente-b-tropfen). Alles bleibt
gleich – Grundflaeche, Zeitpunkte, Panorama, Pegel und Echos der sieben
Tropfen, Zufallszahlen –, nur die Tonhoehe der Tropfen wird gesenkt:
  ambiente-b1-quinte.wav / .ogg             Faktor 2/3     (eine Quinte tiefer)
  ambiente-b2-oktave.wav / .ogg             Faktor 1/2     (eine Oktave tiefer)
  ambiente-b3-anderthalb-oktaven.wav / .ogg Faktor 2^-1,5  (anderthalb Oktaven tiefer)

Aufruf: python ambiente.py   (schreibt in den Ordner dieses Skripts)
"""
import math
import subprocess

from gemeinsam import HERE, HP1  # setzt den Pfad zu synth.py
import importlib.util

from synth import SR, add_into, lp1, n_of, write_wav, zeros

spec = importlib.util.spec_from_file_location("ambiente_hp1", HP1 / "ambiente.py")
hp1 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(hp1)


def make_drip(factor):
    """Wie drip() in Hoerprobe 1, gleiche Zufallszahlen, Tonhoehe mal factor."""
    def drip(rng):
        ln = n_of(0.03)
        f_a = rng.uniform(3000.0, 4200.0) * factor
        f_b = f_a * rng.uniform(1.15, 1.3)
        ph = 0.0
        out = [0.0] * ln
        for i in range(ln):
            ph += (f_a + (f_b - f_a) * (i / ln) ** 0.5) / SR
            out[i] = math.sin(2.0 * math.pi * ph) * math.exp(-i / (0.006 * SR)) * min(1.0, i / 20.0)
        full = zeros(n_of(0.35))
        add_into(full, out, 0, 1.0)
        add_into(full, lp1(out, 3500.0), n_of(0.09), 0.25)
        add_into(full, lp1(out, 2500.0), n_of(0.17), 0.12)
        return full
    return drip


VARIANTS = (("ambiente-b1-quinte", 2.0 / 3.0),
            ("ambiente-b2-oktave", 0.5),
            ("ambiente-b3-anderthalb-oktaven", 2.0 ** -1.5))


def main():
    for name, factor in VARIANTS:
        hp1.drip = make_drip(factor)
        left, right = hp1.variant("b")
        wav = HERE / f"{name}.wav"
        write_wav(wav, [left, right])
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav),
                        "-c:a", "libvorbis", "-q:a", "5", str(HERE / f"{name}.ogg")], check=True)
        print(name)
    # Vorlage: ../hoerprobe-1/ambiente-b-tropfen.wav (nicht kopiert, 5 MB)


if __name__ == "__main__":
    main()
