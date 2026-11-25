#!/usr/bin/env python3
"""Messwerte und Bilder fuer Runde 2 (nutzt ../messen.py)."""
import sys
from pathlib import Path

HIER = Path(__file__).resolve().parent
sys.path.insert(0, str(HIER.parent))
sys.argv = [sys.argv[0], str(HIER)]
import messen as M  # noqa: E402

M.EREIGNISSE["fallen-b"] = [(0, "0", "gray"), (12, "12 Stocken", "red"), (26, "26 Stocken", "red"),
                            (47, "47 Aufschlag", "red"), (58, "58", "orange"), (63, "63", "orange")]
M.EREIGNISSE["heben"] = [(0, "0", "gray"), (6, "6", "gray"), (18, "18", "gray"), (75, "75 frei", "gray"),
                         (126, "126 Anschlag", "red")]


def main():
    zeilen = ["# Messwerte Runde 2 (Fallgitter #540)", "",
              "Gemessen, nicht gehoert. Einsatz = staerkster Pegelanstieg je 32-Sample-Block im Fenster +-40 ms.", "",
              "| Datei | Dauer s | Spitze dBFS | Mittel dBFS | Letzte 50 ms dBFS | Energie > 3 kHz dB |",
              "|---|---|---|---|---|---|"]
    ein = ["", "## Einsatz auf dem Tick", "", "| Datei | Tick | Abweichung Samples | ms | Anstieg dB |", "|---|---|---|---|---|"]
    verl = ["", "## Pegel je 100 ms (RMS dBFS)", ""]
    for name, kuerzel in (("fallen-b", "fallen-b-rutschen"), ("heben", "heben-rutschen")):
        for var in (1, 2):
            datei = f"{kuerzel}-variante-{var}"
            pfad = HIER / f"{datei}.wav"
            x = M.lies(pfad)
            dauer = len(x) / M.SR
            mean, peak = M.ffmpeg_pegel(pfad)
            zeilen.append(f"| {datei}.wav | {dauer:.2f} | {peak:.1f} | {mean:.1f} | {M.ende_dbfs(pfad):.1f} | {M.hochanteil(pfad):.1f} |")
            for tick in {"fallen-b": [12, 26, 47], "heben": [126]}[name]:
                ab, r = M.einsatz(x, tick)
                ein.append(f"| {datei} | {tick} | {ab} | {ab / M.SR * 1000:.2f} | {M.db(r):.1f} |")
            verl.append(f"- {datei}: " + " ".join(f"{v:.0f}" for v in M.rms_verlauf(x)))
            M.bild(pfad, HIER / f"{datei}.png", name, dauer)
    (HIER / "messwerte.md").write_text("\n".join(zeilen + ein + verl) + "\n", encoding="utf-8")


main()
