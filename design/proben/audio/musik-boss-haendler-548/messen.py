"""Misst die vier Skizzen und schreibt messwerte.md, uebersicht.md und spektrogramme/*.png.

Aufruf (aus dem Repository-Stamm oder von ueberall):
  python messen.py <ordner mit den WAV-Dateien> <ausgabeordner>

Gemessen wird nur: Laenge, Spitze, Mittel (volumedetect), Lautheit (ebur128), Pegel je Sekunde, Naht der Schleife
(Sprung letztes zu erstem Sample gegen den 99,9-%-Abstand zweier Nachbar-Samples, je Kanal; dazu der Pegel der letzten
und ersten 100 ms), Pegel je Band, Abstand der Mitte 300-3 000 Hz zum Gesamtpegel, Frequenzschwerpunkt und Anteil
ausserhalb von e-Moll. Als Massstab laufen assets/audio/musik-katakomben.ogg und musik-hauptmenue.ogg mit.
Ob etwas gut klingt, misst das nicht; das entscheidet der Mensch.
"""
import math
import re
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
sys.path.insert(0, str(ROOT / "tools" / "audio"))
from musik_pruefen import band, chroma, ff, read, PC, SCALE  # noqa: E402

SR = 44100
REF = [ROOT / "assets" / "audio" / "musik-katakomben.ogg", ROOT / "assets" / "audio" / "musik-hauptmenue.ogg"]

# Fenster fuer den Vergleich von Ende und Anfang: ein Takt bei den Boss-Stuecken (80 bzw. 96 Schlaege/min), sonst 4 s.
FENSTER = {"boss-a-schwerer-marsch": 3.0, "boss-b-basspuls": 2.5}

SAETZE = {
    "boss-a-schwerer-marsch": "Schlagwerk-Stück: eine tiefe Trommel auf 1 und 3 und ein Ambossschlag (Metall auf Stein) auf 4 tragen es bei 80 Schlägen pro Minute, darunter liegen langsame Chor-Akkorde und eine tiefe Glocke alle zwei Takte.",
    "boss-b-basspuls": "Puls-Stück: ein gleichmäßiger Achtelpuls auf dem Grundton (tiefer Ton mit Obertönen bis etwa 330 Hz) treibt bei 96 Schlägen pro Minute, darüber stehen Chor-Akkorde und nur drei Glockenschläge; keine Trommel, kein Amboss.",
    "haendler-a-glocken-tropfen": "Melodisch: ein leiser Chor liegt unter acht einzelnen Glockentönen (E, G, A, H, D) mit großen Abständen, die wie langsame Tropfen fallen; der Akkord wechselt alle 10 s.",
    "haendler-b-chor-atem": "Ohne Melodie: nur Chor-Stimmen, die versetzt einsetzen und wieder ausatmen (der Akkord wandert fließend), dazu zwei tiefe Glockenschläge bei 0 s und 20 s; die Vokalfärbung ist voller als bei den Boss-Stücken.",
}
REIHENFOLGE = list(SAETZE)


def fft(re_, im_):
    n = len(re_)
    j = 0
    for i in range(1, n):
        bit = n >> 1
        while j & bit:
            j ^= bit
            bit >>= 1
        j ^= bit
        if i < j:
            re_[i], re_[j] = re_[j], re_[i]
            im_[i], im_[j] = im_[j], im_[i]
    ln = 2
    while ln <= n:
        ang = -2.0 * math.pi / ln
        wr, wi = math.cos(ang), math.sin(ang)
        for s in range(0, n, ln):
            cr, ci = 1.0, 0.0
            for k in range(ln // 2):
                a, b = s + k, s + k + ln // 2
                tr = re_[b] * cr - im_[b] * ci
                ti = re_[b] * ci + im_[b] * cr
                re_[b], im_[b] = re_[a] - tr, im_[a] - ti
                re_[a], im_[a] = re_[a] + tr, im_[a] + ti
                cr, ci = cr * wr - ci * wi, cr * wi + ci * wr
        ln <<= 1


def spektrum(x, size=8192):
    """Mittleres Leistungsspektrum (Hann, Schritt = Fensterbreite). Gibt (Frequenzen, Leistung) zurueck."""
    win = [0.5 - 0.5 * math.cos(2 * math.pi * i / size) for i in range(size)]
    acc = [0.0] * (size // 2)
    cnt = 0
    for s in range(0, len(x) - size + 1, size):
        r = [x[s + i] * win[i] for i in range(size)]
        im = [0.0] * size
        fft(r, im)
        for k in range(size // 2):
            acc[k] += r[k] * r[k] + im[k] * im[k]
        cnt += 1
    return [k * SR / size for k in range(size // 2)], [a / cnt for a in acc]


def schwerpunkt(fr, p):
    tot = sum(p)
    cen = sum(f * v for f, v in zip(fr, p)) / tot
    acc, roll = 0.0, 0.0
    for f, v in zip(fr, p):
        acc += v
        if acc >= 0.9 * tot:
            roll = f
            break
    peak_f = fr[max(range(1, len(p)), key=lambda i: p[i])]
    return cen, roll, peak_f


def messe(path, bild_aus=None):
    with tempfile.TemporaryDirectory() as td:
        dec = Path(td) / "d.wav"
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(path), "-c:a", "pcm_s16le", str(dec)], check=True)
        chs = read(dec)
        e = ff(dec, "volumedetect")
        mx = float(re.search(r"max_volume:\s*(-?[\d.]+)", e).group(1))
        mean = float(re.search(r"mean_volume:\s*(-?[\d.]+)", e).group(1))
        lufs = float(re.findall(r"I:\s*(-?[\d.]+) LUFS", ff(dec, "ebur128"))[-1])
        if bild_aus:
            bild_aus.parent.mkdir(parents=True, exist_ok=True)
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(dec), "-lavfi",
                            "pan=mono|c0=0.5*c0+0.5*c1,showspectrumpic=s=1100x420:legend=0:fscale=log:scale=log:stop=8000",
                            "-frames:v", "1", str(bild_aus)], check=True)
    n = len(chs[0])
    r = {"laenge": n / SR, "spitze": mx, "mittel": mean, "lufs": lufs}
    seam, ok = [], True
    for c in chs:
        d = sorted(abs(c[i] - c[i - 1]) for i in range(1, n))
        p999, jump = d[int(0.999 * (len(d) - 1))], abs(c[0] - c[-1])
        ok &= jump <= p999
        seam.append(f"{jump:.5f} ≤ {p999:.5f}")
    r["naht"], r["naht_ok"] = "; ".join(seam), ok
    mono = [(a + b) / 2 for a, b in zip(*chs)]

    def rms100(seg):
        return 10 * math.log10(max(1e-12, sum(v * v for v in seg) / len(seg)))
    k = SR // 10
    w = int(FENSTER.get(Path(path).stem, 4.0) * SR)
    r["rand"] = (rms100(mono[-w:]), rms100(mono[:w]))
    r["rand100"] = (rms100(mono[-k:]), rms100(mono[:k]))
    sek = [rms100(mono[i:i + SR]) for i in range(0, n - SR + 1, SR)]
    r["sek_min"], r["sek_max"] = min(sek), max(sek)
    lo, mid, hi = band(mono, None, 300), band(mono, 300, 3000), band(mono, 3000)
    tot = 10 * math.log10(sum(v * v for v in mono) / n)
    r.update(lo=lo - tot, mid=mid - tot, hi=hi - tot, abstand=tot - mid)
    ch = chroma(mono)
    r["ausserhalb"] = sum(ch[i] for i in range(12) if i not in SCALE) * 100
    top = max(range(12), key=lambda i: ch[i])
    r["klasse"] = f"{PC[top]} ({ch[top] * 100:.0f} %)"
    fr, p = spektrum(mono)
    r["schwerpunkt"], r["rolloff"], r["spitzenfrequenz"] = schwerpunkt(fr, p)
    return r


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ordner, aus = Path(sys.argv[1]), Path(sys.argv[2])
    aus.mkdir(parents=True, exist_ok=True)
    werte = {}
    for name in REIHENFOLGE:
        werte[name] = messe(ordner / f"{name}.wav", aus / "spektrogramme" / f"{name}.png")
        print(name, flush=True)
    refw = {p.name: messe(p) for p in REF if p.exists()}
    kat = refw["musik-katakomben.ogg"]

    kopf = ("| Datei | Länge | Spitze | Mittel | Lautheit | Mittel gegen Katakomben | Spitze gegen Katakomben | Pegel je Sekunde (min / max) | "
            "Frequenzschwerpunkt | 90 % der Energie unter | Abstand Mitte 300–3 000 Hz | außerhalb e-Moll |\n|---|---|---|---|---|---|---|---|---|---|---|---|")

    def zeile(name, r):
        return (f"| `{name}` | {r['laenge']:.1f} s | {r['spitze']:.1f} dBFS | {r['mittel']:.1f} dBFS | {r['lufs']:.1f} LUFS | "
                f"{r['mittel'] - kat['mittel']:+.1f} dB | {r['spitze'] - kat['spitze']:+.1f} dB | {r['sek_min']:.1f} / {r['sek_max']:.1f} dBFS | "
                f"{r['schwerpunkt']:.0f} Hz | {r['rolloff']:.0f} Hz | {r['abstand']:.1f} dB | {r['ausserhalb']:.1f} % |")
    naht = ("| Datei | Sprung letztes→erstes Sample (L; R) gegen 99,9-%-Abstand | Pegel letztes Fenster | Pegel erstes Fenster | Unterschied | letzte / erste 100 ms (zur Info) | Ergebnis |\n"
            "|---|---|---|---|---|---|---|")
    bands = "| Datei | < 300 Hz | 300–3 000 Hz | > 3 000 Hz | stärkste Tonklasse |\n|---|---|---|---|---|"
    for name in REIHENFOLGE:
        r = werte[name]
        a, b = r["rand"]
        ok = r["naht_ok"] and abs(a - b) <= 3.0
        naht += f"\n| `{name}` | {r['naht']} | {a:.1f} dBFS | {b:.1f} dBFS | {abs(a - b):.1f} dB | {r['rand100'][0]:.1f} / {r['rand100'][1]:.1f} dBFS | {'bestanden' if ok else 'NICHT bestanden'} |"
        bands += f"\n| `{name}` | {r['lo']:.1f} dB | {r['mid']:.1f} dB | {r['hi']:.1f} dB | {r['klasse']} |"
    tab = "\n".join([kopf] + [zeile(n, werte[n]) for n in REIHENFOLGE])
    ref = "\n".join([kopf] + [zeile(n, r) for n, r in refw.items()])

    md = f"""# Messwerte – Musik-Skizzen Bosskampf und Händler (Issue #548)

Erzeugt von `messen.py`. Alle Zahlen sind gemessen, nicht gehört. Pegel in dBFS, Bandwerte in dB unter dem Gesamtpegel.

## Die vier Skizzen

{tab}

## Maßstab: vorhandene Stücke im Spiel

{ref}

Maßstab ist `musik-katakomben.ogg` (Mittel nach Issue #427 bei −24 dBFS). Alle vier Skizzen sind auf dasselbe Mittel
gebracht; die Spitze liegt dort, wo die Dynamik der Stücke sie hinsetzt, nie über −1,5 dBFS (Rahmen: höchstens −1 dBFS).

## Naht der Schleife

Anfang und Ende laufen kreisförmig ineinander (Noten, Hall und PS1-Kette). Gemessen wird an der fertigen Datei:
der Sprung vom letzten zum ersten Sample darf nicht größer sein als der 99,9-%-Abstand zweier Nachbar-Samples, und
der Pegel des letzten Fensters (ein Takt bei den Boss-Stücken, sonst 4 s) darf höchstens 3 dB vom Pegel des ersten gleich langen Fensters abweichen. Die ersten 100 ms allein sind kein Maß: dort liegt bei den Boss-Stücken der Trommelschlag auf der Eins.

{naht}

## Frequenzbänder

{bands}

Die Katakomben-Regel „Mitte frei“ (mindestens 15 dB Abstand im Band 300–3 000 Hz, Audio Bible Regel 6) ist für die Boss-Stücke
der Maßstab, weil dort Ankündigungen gegnerischer Schläge durchkommen müssen. Für die Händler-Stücke gilt sie nicht streng
(kein Kampf), die Zahl steht zum Vergleich da.

## Spektrogramme

""" + "\n".join(f"- `spektrogramme/{n}.png`" for n in REIHENFOLGE) + "\n"
    (aus / "messwerte.md").write_text(md, encoding="utf-8")

    ue = ["# Übersicht – Musik-Skizzen Bosskampf und Händler (Issue #548)", "",
          "Hörprobe, nicht im Spiel. Vier Einzeldateien, je als Schleife gedacht (Länge 40 bis 42 s). Alles per Skript (`erzeugen.py`) aus der",
          "Klangwelt von `musik-hauptmenue.ogg` und `musik-katakomben.ogg`: tiefe Glocke, Chor-Pad, derselbe Hall, dieselbe PS1-Kette, e-Moll. Kein Herzschlag.",
          "Weder die Leitung noch der Audiodesigner können hören: Die Zahlen sagen nichts darüber, ob es gut klingt. Das entscheidet der Mensch.", "",
          "Pegel zum Vergleich: `musik-katakomben.ogg` hat Mittel {:.1f} dBFS, Spitze {:.1f} dBFS, Lautheit {:.1f} LUFS. ".format(kat["mittel"], kat["spitze"], kat["lufs"]),
          "Alle vier Skizzen sind auf dasselbe Mittel gebracht, damit nur der Klang verglichen wird. Wer sie im Spiel einstellt, nimmt als Spielfaktor 1,0 wie bei den bestehenden Stücken.", ""]
    for stueck, titel in (("boss", "Bosskampf: schwer, tief, treibend, Platz für Kampfgeräusche"), ("haendler", "Händler: ruhig, eine Atempause, dieselbe düstere Welt")):
        ue += [f"## {titel}", "", "| Datei | Länge | Spitze | Mittel | Lautheit | Mittel gegen Katakomben | Frequenzschwerpunkt | Abstand Mitte 300–3 000 Hz |", "|---|---|---|---|---|---|---|---|"]
        for n in REIHENFOLGE:
            if n.startswith(stueck):
                r = werte[n]
                ue.append(f"| `{n}.wav` | {r['laenge']:.1f} s | {r['spitze']:.1f} dBFS | {r['mittel']:.1f} dBFS | {r['lufs']:.1f} LUFS | "
                          f"{r['mittel'] - kat['mittel']:+.1f} dB | {r['schwerpunkt']:.0f} Hz | {r['abstand']:.1f} dB |")
        ue.append("")
        for n in REIHENFOLGE:
            if n.startswith(stueck):
                ue.append(f"- **{n[len(stueck) + 1]} (`{n}`):** {SAETZE[n]}")
        ue.append("")
    ue += ["Naht, Bänder, Pegel je Sekunde und Spektrogramme stehen in `messwerte.md`. Zwei Läufe von `erzeugen.py` ergeben bitgleiche Dateien (`wav.sha256`).", ""]
    (aus / "uebersicht.md").write_text("\n".join(ue), encoding="utf-8")


if __name__ == "__main__":
    main()
