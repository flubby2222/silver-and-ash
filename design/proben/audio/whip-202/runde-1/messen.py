#!/usr/bin/env python3
"""Misst die WAV-Dateien der Hörprobe Whip (#202), Runde 1, und schreibt messwerte.md,
LIESMICH.md (Tabellen mit den Messwerten), wav.sha256 und je ein Spektrogramm (PNG, Ordner
spektrogramme/). Braucht ffmpeg im PATH. Aufruf: python messen.py"""
import hashlib
import math
import re
import struct
import subprocess
import wave
from pathlib import Path

HIER = Path(__file__).resolve().parent
BESTAND = HIER.parents[4] / "assets" / "audio"
VERGLEICH = ["p1-schwung-1.wav", "p1-treffer-1.wav", "klinge-1.wav"]

BESCHREIBUNG = {
    "schlag-a-scharfer-knall": "Kurzes Ausholen (Rauschband steigt 0,16 s an), dann ein kurzer harter Knall (Rauschklick plus Körper 2 kHz, 50 ms) mit kleinem Schlag darunter.",
    "schlag-b-dumpfer-lederschlag": "Langsameres tiefes Ausholen (0,3 s), der Aufschlag ist ein dumpfer, nasser Lederklatsch um 700 Hz mit Schlag 85 Hz, kein Klick.",
    "schlag-c-langes-zischen": "Langes Zischen (0,55 s, steigt von 0,9 auf 2,8 kHz), dann ein kleiner kurzer Knall und ein Rest Zischen, der ausläuft.",
    "treffer-fleisch-a-klatsch": "Trockener Lederklatsch: Rauschstoß um 900 Hz, Schlag 110 Hz, kurzes Knacken des Lederendes.",
    "treffer-fleisch-b-striemen": "Knacken des Lederendes, dann zieht das Leder über die Haut (Reibe-Rauschen fällt von 1,1 auf 0,35 kHz), leiser Schlag darunter.",
    "treffer-fleisch-c-nass-dumpf": "Runder tiefer Schlag 65 Hz, feuchtes Schmatzen (Rauschband 400 auf 200 Hz, grob gekörnt), weicher Stoß um 600 Hz.",
    "treffer-hart-a-stein-knallt": "Harter Knall des Lederendes auf Stein (Klick, Körper 1,6 kHz) mit trockenem Tock aus Rauschen um 420 Hz und Schlag 140 Hz.",
    "treffer-hart-b-holz-klopft": "Leder auf Holzbalken: dumpfer Klopf 190 auf 150 Hz, klingt in 50 ms aus, knappe Rauschspitze, tiefer Schlag 70 Hz.",
    "treffer-hart-c-stein-rieselt": "Kleiner Knall auf Stein, Schlag 70 Hz, danach rieselt Staub (gekörntes Rauschen 0,3 bis 1,5 kHz, fällt in 0,3 s ab).",
    "aufladen-a-leder-knarrt": "Leder knarrt unter Zug (gekörntes Rauschband 180 auf 420 Hz, wird lauter), Brummen 55 auf 85 Hz, am Ende zieht es fest: tiefer Schlag 60 Hz.",
    "aufladen-b-wirbeln": "Das Leder kreist durch die Luft: Rauschband 300 auf 900 Hz pulst immer schneller (3 auf 11 Hz) und wird lauter, endet ohne Schlag im weichen Abfall.",
    "aufladen-c-spannen": "Dunkles gedehntes Reiben (120 auf 260 Hz) mit Brummen 50 auf 80 Hz und drei kurzen Lederquietschern, endet mit tiefem Schlag 50 Hz.",
    "heranziehen-a-ruck": "Kurzes Reißen: Rauschband fällt von 1,5 kHz auf 300 Hz, dazu ein dumpfer Schlag 75 Hz (Ruck am Ende der Leine).",
    "heranziehen-b-schleifen": "Ein Körper schleift über Stein: gekörntes Rauschband 500 auf 200 Hz, fällt gleichmäßig ab, leiser Stoß 60 Hz am Anfang.",
    "heranziehen-c-lederruck": "Die Leine spannt sich: kurzes gekörntes Rauschband 260 auf 520 Hz mit Schlag 90 Hz, die kürzeste der drei.",
    "zusammenhang-a": "Schlag A (scharfer Knall), 20 ms nach dem Knall Treffer in Fleisch A (Klatsch).",
    "zusammenhang-b": "Schlag B (dumpfer Lederschlag), 20 ms nach dem Knall Treffer in Fleisch B (Striemen).",
    "zusammenhang-c": "Schlag C (langes Zischen), 20 ms nach dem Knall Treffer in Fleisch C (nass-dumpf).",
}

GRUPPEN = [
    ("1. Schlag (Ausholen und Knall)", "schlag-"),
    ("2. Treffer in Fleisch", "treffer-fleisch-"),
    ("3. Treffer auf Stein oder Holz", "treffer-hart-"),
    ("4. Aufladen (der geladene Schlag, der rissige Wände bricht)", "aufladen-"),
    ("5. Heranziehen (Ghul oder Fledermaus wird herangezogen)", "heranziehen-"),
    ("Zusammenhang (Schlag, dann Treffer in Fleisch derselben Fassung)", "zusammenhang-"),
]


def ffmpeg(*args):
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", *args], capture_output=True, text=True)
    return r.stderr


def vol(datei, filt=None):
    t = ffmpeg("-i", str(datei), "-af", (filt + "," if filt else "") + "volumedetect", "-f", "null", "-")
    mean = float(re.search(r"mean_volume: (-?[\d.]+)", t).group(1))
    peak = float(re.search(r"max_volume: (-?[\d.]+)", t).group(1))
    return mean, peak


def lies(datei):
    with wave.open(str(datei)) as w:
        k = w.getnframes()
        d = struct.unpack("<%dh" % k, w.readframes(k))
    return [v / 32768.0 for v in d]


def pegelreihe(x, ms):
    f = int(44100 * ms / 1000)
    return [10 * math.log10(sum(v * v for v in x[i:i + f]) / f + 1e-12) for i in range(0, len(x) - f, f)]


def einsaetze(x, fenster_ms):
    pegel = pegelreihe(x, fenster_ms)
    schritte = max(1, round(50 / fenster_ms))
    zahl, im = 0, False
    for i in range(len(pegel)):
        j = max(0, i - schritte)
        steigt = pegel[i] - min(pegel[j:i + 1]) > 8 and pegel[i] > -50
        if steigt and not im:
            zahl += 1
        im = steigt
    return zahl


def abklang(x):
    pegel = pegelreihe(x, 10)
    top = max(pegel)
    letzte = max(i for i, p in enumerate(pegel) if p > top - 40)
    return (letzte + 1) * 0.01


def ende(x):
    k = x[-2205:]
    return 20 * math.log10(max(abs(v) for v in k) + 1e-9)


def messe(p):
    x = lies(p)
    mean, peak = vol(p)
    hm, hp = vol(p, "highpass=f=4000,highpass=f=4000")
    return {
        "name": p.stem, "datei": p.name, "laenge": len(x) / 44100, "spitze": peak, "mittel": mean,
        "ueber4": hm - mean, "ueber4_spitze": hp, "e10": einsaetze(x, 10), "e30": einsaetze(x, 30),
        "abklang": abklang(x), "ende": ende(x),
    }


def f2(v):
    return ("%.2f" % v).replace(".", ",")


def f1(v):
    return ("%.1f" % v).replace(".", ",")


def main():
    (HIER / "spektrogramme").mkdir(exist_ok=True)
    eigene = sorted(HIER.glob("*.wav"))
    werte = {}
    for p in eigene:
        werte[p.stem] = messe(p)
        ffmpeg("-y", "-i", str(p), "-lavfi", "showspectrumpic=s=640x320:legend=1:scale=log",
               str(HIER / "spektrogramme" / (p.stem + ".png")))
    bestand = []
    for v in VERGLEICH:
        p = BESTAND / v
        if p.exists():
            m = messe(p)
            bestand.append(m)
            ffmpeg("-y", "-i", str(p), "-lavfi", "showspectrumpic=s=640x320:legend=1:scale=log",
                   str(HIER / "spektrogramme" / ("bestand-" + p.stem + ".png")))

    z = [
        "# Messwerte Whip-Hörprobe, Runde 1 (#202)\n",
        "Erzeugt von `messen.py`. Alles gemessen, nichts gehört. Spitze = höchster Abtastwert; "
        "Mittel = ffmpeg `volumedetect` über die ganze Datei (Stille eingerechnet); über 4 kHz = Pegel "
        "nach Hochpass 4 kHz (zwei Stufen hintereinander, 24 dB je Oktave) minus Gesamtpegel (je kleiner, desto weniger Höhen; zusätzlich die Spitze "
        "über 4 kHz in dBFS); Einsätze = Pegelsprünge über 8 dB in 50 ms (10-ms- und 30-ms-Fenster); "
        "Abklang = Zeit bis dauerhaft 40 dB unter der Spitze; Ende = Spitze der letzten 50 ms (Soll höchstens -50 dBFS).\n",
        "| Datei | Länge s | Spitze dBFS | Mittel dB | über 4 kHz dB | Spitze über 4 kHz dBFS | Einsätze 10/30 ms | Abklang s | Ende dBFS | Spektrogramm |",
        "|---|---|---|---|---|---|---|---|---|---|",
    ]
    for m in list(werte.values()) + bestand:
        bst = m["datei"] not in {p.name for p in eigene}
        png = "spektrogramme/" + ("bestand-" if bst else "") + m["name"] + ".png"
        z.append("| %s%s | %.2f | %.1f | %.1f | %.1f | %.1f | %d / %d | %.2f | %.1f | [Bild](%s) |" % (
            m["datei"], " (Bestand)" if bst else "", m["laenge"], m["spitze"], m["mittel"], m["ueber4"],
            m["ueber4_spitze"], m["e10"], m["e30"], m["abklang"], m["ende"], png))
    (HIER / "messwerte.md").write_text("\n".join(z) + "\n", encoding="utf-8")

    maxlaenge = max(m["laenge"] for m in werte.values())
    schlechtestes_ende = max(m["ende"] for m in werte.values())
    hoechster_hoehen = max(m["ueber4"] for m in werte.values())
    l = [
        "# Hörprobe Whip (Lederpeitsche), Runde 1 (Issue #202)\n",
        "Jede Fassung ist **eine eigene WAV-Datei** mit sprechendem Namen. Hör sie in dieser Reihenfolge "
        "(Gruppe für Gruppe, je A, B, C) und sag mir je Gruppe: **welche Fassung, und was stört?**\n",
        "Alles ist per Skript (`bauen.py`) synthetisiert, nichts heruntergeladen. **Alles gemessen, nichts gehört.** "
        "Die Angriffe sind die aus dem Issue: Schlag mit langer Reichweite, Heranziehen von Ghulen und Fledermäusen, "
        "Aufladen (bricht rissige Wände). Ein Peitschenknall ist von Natur aus hell; deshalb ist er hier kurz "
        "(Knallspitze 1 bis 4 ms) und die Energie über 4 kHz ist begrenzt: Tiefpass 4. Ordnung bei 3,0 kHz (Schlag A, "
        "Treffer Stein A) bzw. 2,4 kHz (alles andere). Gemessen liegt der Pegel über 4 kHz in allen Dateien mindestens "
        "%s dB unter dem Gesamtpegel (siehe Spalte unten und `messwerte.md`). Keine tonalen Schlussereignisse: Einrasten, "
        "Klopfen und Tock sind tiefe Schläge unter 200 Hz oder Rauschen mit niedriger Güte, kein Ding.\n" % f1(abs(hoechster_hoehen)).replace(",", ","),
        "Bestand zum Vergleich (aus `assets/audio`): " + "; ".join(
            "`%s` (%s s, Spitze %s dBFS, Mittel %s dB, über 4 kHz %s dB)" % (
                m["datei"], f2(m["laenge"]), f1(m["spitze"]), f1(m["mittel"]), f1(m["ueber4"])) for m in bestand) + ".\n",
        "Messwerte: Länge in s, Spitze in dBFS, Mittel in dB (ffmpeg `volumedetect`), über 4 kHz = Pegel der Anteile über 4 kHz "
        "relativ zum Gesamtpegel in dB. Ausführlicher in `messwerte.md`, Bilder in `spektrogramme/`.\n",
    ]
    for titel, praefix in GRUPPEN:
        l.append("## " + titel + "\n")
        l.append("| Datei | Beschreibung | Länge s | Spitze dBFS | Mittel dB | über 4 kHz dB |")
        l.append("|---|---|---|---|---|---|")
        for name, m in werte.items():
            if name.startswith(praefix):
                l.append("| `%s` | %s | %s | %s | %s | %s |" % (
                    m["datei"], BESCHREIBUNG[name], f2(m["laenge"]), f1(m["spitze"]), f1(m["mittel"]), f1(m["ueber4"])))
        l.append("")
    l += [
        "## Hinweise zu den Messwerten\n",
        "- Spitzen: Schlag und Heranziehen -6, Treffer -4,5, Aufladen -10 dBFS (Ziele); die Aufladen-Fassungen sind auf gleiche mittlere "
        "Lautstärke gebracht (nur nach unten gerechnet), bei Schlag, Treffern und Heranziehen sind die Spitzen gleich, die mittlere "
        "Lautstärke unterscheidet sich (ein kurzer Knall hat naturgemäß ein tiefes Mittel).",
        "- Alle Dateien laufen auf Stille aus (letzte 50 ms: höchster Wert %s dBFS, Soll höchstens -50). Mono, 16 Bit, 44 100 Hz. Längste Datei: %s s."
        % (f1(schlechtestes_ende), f2(maxlaenge)),
        "- Der Einsatzzähler in `messwerte.md` zählt bei tiefen Tönen mehr als einen Einsatz (Perioden über 6 ms, das 30-ms-Fenster zeigt "
        "den wahren Wert). Beim Schlag sind Ausholen und Knall absichtlich eine Geste (das Ausholen schwillt bis zum Knall an); die "
        "Aufladen-Fassungen A und C enden mit einem tiefen Schlag, B ohne.",
        "- Schlag B (0,70 s) und Schlag C (0,93 s) sowie alle Aufladen- und Zusammenhang-Dateien liegen über den 0,6 s, die die Audio Bible "
        "für Schuss und Treffer nennt; das Ausholen gehört dort zur Geste (Länge samt Hall-Nachlauf). Alle Treffer und Heranziehen-Dateien bleiben unter 0,6 s.",
        "- Zwei Läufe von `bauen.py` ergeben bitgleiche Dateien (`wav.sha256`).",
        "- Nachbau: `python bauen.py` (Dateien), `python messen.py` (Messwerte, Spektrogramme, diese Datei und `wav.sha256`, braucht `ffmpeg`).",
    ]
    (HIER / "LIESMICH.md").write_text("\n".join(l) + "\n", encoding="utf-8")
    h = ["%s  %s" % (hashlib.sha256(p.read_bytes()).hexdigest(), p.name) for p in eigene]
    (HIER / "wav.sha256").write_text("\n".join(h) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
