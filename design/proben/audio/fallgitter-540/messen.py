#!/usr/bin/env python3
"""Misst die Fallgitter-Proben (#540) und zeichnet Wellenform-Bilder mit Ticks.

Aufruf:  python messen.py [Ordner]   (Standard: neben dem Skript)
Benutzt Python-Standardbibliothek und ffmpeg (volumedetect, showwavespic,
drawbox, drawtext). Schreibt messwerte.md und je Datei ein PNG.
"""
import math
import re
import subprocess
import sys
import wave
from pathlib import Path

SR = 44100
TICK = 735
HIER = Path(__file__).resolve().parent
ORDNER = Path(sys.argv[1]) if len(sys.argv) > 1 else HIER
FONT = "C\\:/Windows/Fonts/arial.ttf"

# Ereignisse je Bewegung: (Tick, Beschriftung, Farbe)
EREIGNISSE = {
    "fallen-a": [(0, "0 loesen", "gray"), (24, "24 Aufschlag", "red"), (35, "35", "orange"), (40, "40", "orange")],
    "fallen-b": [(0, "0 loesen", "gray"), (12, "12 Ruck 1", "red"), (26, "26 Ruck 2", "red"),
                 (47, "47 Aufschlag", "red"), (58, "58", "orange"), (63, "63", "orange")],
    "heben": [(0, "0", "gray"), (6, "6 Anlauf", "orange"), (18, "18", "gray"), (75, "75 frei", "gray"),
              (126, "126 Einrasten", "red")],
}
# Ereignisse, deren Einsatz auf den Tick genau gemessen wird
MESSPUNKTE = {"fallen-a": [24, 35], "fallen-b": [12, 26, 47, 58], "heben": [126]}


def lies(pfad):
    with wave.open(str(pfad), "rb") as w:
        roh = w.readframes(w.getnframes())
    return [int.from_bytes(roh[i:i + 2], "little", signed=True) / 32768.0 for i in range(0, len(roh), 2)]


def db(x):
    return 20 * math.log10(max(x, 1e-9))


def ffmpeg_pegel(pfad):
    p = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(pfad), "-af", "volumedetect", "-f", "null", "-"],
                       capture_output=True, text=True)
    mean = float(re.search(r"mean_volume: (-?[\d.]+)", p.stderr).group(1))
    peak = float(re.search(r"max_volume: (-?[\d.]+)", p.stderr).group(1))
    return mean, peak


def ende_dbfs(pfad):
    p = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(pfad), "-af",
                        "areverse,atrim=0:0.05,volumedetect", "-f", "null", "-"], capture_output=True, text=True)
    return float(re.search(r"max_volume: (-?[\d.]+)", p.stderr).group(1))


def hochanteil(pfad):
    """Energie ueber 3 kHz relativ zur Gesamtenergie, in dB (zweimal Hochpass)."""
    def rms(af):
        p = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(pfad), "-af", af + ",astats=metadata=0",
                            "-f", "null", "-"], capture_output=True, text=True)
        w = re.findall(r"RMS level dB: (-?[\d.inf]+)", p.stderr)
        return float(w[-1]) if w else -99.0
    return rms("highpass=f=3000,highpass=f=3000") - rms("anull")


def einsatz(x, tick):
    """Einsatz = Block (32 Samples, 0,7 ms) mit dem staerksten Anstieg gegen den
    Mittelwert der 4 Bloecke davor, im Fenster +-40 ms um den Tick. Meldet die
    Abweichung des Blockanfangs vom Tick-Sample und den Anstieg in dB."""
    soll = tick * TICK
    B = 32
    a = max(4 * B, soll - 1764)
    b = min(len(x) - B, soll + 1764)
    bestes, ab, lok = -1.0, None, 0.0
    for i in range(a, b, 8):
        e = max(abs(s) for s in x[i:i + B])
        vor = sum(max(abs(s) for s in x[i - (k + 1) * B:i - k * B]) for k in range(4)) / 4.0
        r = e / (vor + 1e-4)
        if r > bestes:
            bestes, ab, lok = r, i - soll, e
    return ab, bestes


def rms_verlauf(x, fenster_ms=100):
    f = int(SR * fenster_ms / 1000)
    return [db(math.sqrt(sum(s * s for s in x[i:i + f]) / f)) for i in range(0, len(x) - f + 1, f)]


def bild(wav, png, name, laenge_s):
    breite, hoehe = 1400, 360
    filt = [f"[0:a]showwavespic=s={breite}x{hoehe}:colors=0x7aa2c8:split_channels=0[w]"]
    ketten = "[w]"
    z = 0
    for tick, text, farbe in EREIGNISSE[name]:
        x = int(round(tick / 60.0 / laenge_s * breite))
        z += 1
        ketten_neu = f"[v{z}]"
        filt.append(f"{ketten}drawbox=x={x}:y=0:w=2:h={hoehe}:color={farbe}@0.9:t=fill,"
                    f"drawtext=fontfile='{FONT}':text='{text}':x={x + 4}:y={4 + 14 * (z % 2)}:fontsize=14:fontcolor={farbe}"
                    f"{ketten_neu}")
        ketten = ketten_neu
    subprocess.run(["ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-i", str(wav), "-filter_complex",
                    ";".join(filt), "-map", ketten, "-frames:v", "1", str(png)], check=True)


def main():
    namen = [("fallen-a", 1), ("fallen-a", 2), ("fallen-b", 1), ("fallen-b", 2), ("heben", 1), ("heben", 2)]
    zeilen = ["# Messwerte Fallgitter-Hoerprobe (#540)", "",
              "Gemessen mit Python (Einsatz, Verlauf) und `ffmpeg` (volumedetect, astats). Gemessen, nicht gehoert.", "",
              "| Datei | Dauer s | Spitze dBFS | Mittel dBFS | Letzte 50 ms dBFS | Energie > 3 kHz dB |",
              "|---|---|---|---|---|---|"]
    einsaetze = ["", "## Einsatz auf dem Tick", "",
                 "Block (32 Samples) mit dem staerksten Pegelanstieg im Fenster +-40 ms um den Tick; Abweichung seines Anfangs vom "
                 "Tick-Sample (Tick x 735). Negativ = vor dem Tick. Anstieg = Blockspitze gegen Mittel der 4 Bloecke davor.", "",
                 "| Datei | Tick | Abweichung Samples | Abweichung ms | Anstieg dB |", "|---|---|---|---|---|"]
    verlauf = ["", "## Pegel je 100 ms (RMS dBFS)", ""]
    for name, var in namen:
        datei = f"{name}-variante-{var}"
        pfad = ORDNER / f"{datei}.wav"
        x = lies(pfad)
        dauer = len(x) / SR
        mean, peak = ffmpeg_pegel(pfad)
        zeilen.append(f"| {datei}.wav | {dauer:.2f} | {peak:.1f} | {mean:.1f} | {ende_dbfs(pfad):.1f} | {hochanteil(pfad):.1f} |")
        for tick in MESSPUNKTE[name]:
            ab, lok = einsatz(x, tick)
            einsaetze.append(f"| {datei} | {tick} | {ab} | {ab / SR * 1000:.2f} | {db(lok):.1f} |")
        verlauf.append(f"- {datei}: " + " ".join(f"{v:.0f}" for v in rms_verlauf(x)))
        bild(pfad, ORDNER / f"{datei}.png", name, dauer)
    (ORDNER / "messwerte.md").write_text("\n".join(zeilen + einsaetze + verlauf) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
