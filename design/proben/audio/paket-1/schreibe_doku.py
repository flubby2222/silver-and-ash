"""Schreibt uebersicht.md (Paket-1-Ordner) und die Uebergabe-Beschreibung aus messwerte-alle.json und tabelle.md.
Aufruf (nach paket1.py): python schreibe_doku.py
"""
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
UEB = HERE.parents[1] / "uebergabe"
d = json.loads((HERE / "messwerte-alle.json").read_text(encoding="utf-8"))["dateien"]
tabelle = (HERE / "tabelle.md").read_text(encoding="utf-8")

KAT = {}
for name, m in d.items():
    KAT.setdefault(m["kategorie"], []).append(m)
ordnung = ["ankuendigung", "schuss", "schmerz", "ghul", "treffer", "nachladen", "schwung", "bewegung", "tuer_kiste", "aufheben", "schritt", "menue"]
kat_zeilen = []
for k in ordnung:
    ms = KAT[k]
    pk = sorted({m["peak_db"] for m in ms})
    rms = [m["rms_db"] for m in ms]
    kat_zeilen.append(f"| {k} | {len(ms)} | {pk[0]:.1f} | {min(rms):.1f} … {max(rms):.1f} |")
kat = "| Kategorie | Dateien | Spitze dBFS | Mittel (RMS) dBFS |\n|---|---|---|---|\n" + "\n".join(kat_zeilen)

uebersicht = f"""# Geräuschpaket 1 – Übersicht

Alle 31 Dateien aus der neuen Palette ([palette-neu.md](../soundcraft-versuch/palette-neu.md)), nach der Methode aus dem Video ([video.md](../soundcraft-versuch/video.md)): fünf Quell-Samples (Schlag, Hauch, Pfeife, Glocke, Stimme), je einmal in SoundCraft gebaut, auf 22 050 Hz in VAG gewandelt und je Geräusch vom Sampler in der Tonhöhe verschoben gespielt; ein gemeinsamer kurzer Raum (Hall 0,8 s, 22 %). **Ungehört**; alle Zahlen sind gemessen. Erzeugung: `python paket1.py` (zweimal gerechnet, bitgleich); Dateien in `dateien/` und als Übergabe in [design/uebergabe/assets/audio/](../../uebergabe/assets/audio/) (Präfix `p1-`, Prüfsummen in `audio-paket-1.sha256`); Zuordnung zu den Spielereignissen und fehlende Ereignisse: [README-audio-paket-1.md](../../uebergabe/README-audio-paket-1.md).

## Je Geräusch eine Zeile

{tabelle}
„gespielt“: Ton und Halbtöne gegen die Aufnahme des Samples (Schlag und Hauch sind bei C4 aufgenommen und tonlos; Pfeife, Glocke und Stimme bei E3). Tonale Geräusche (Aufheben) liegen auf Tönen von e-Moll.

## Lautheit je Kategorie

Die Spitze richtet sich nach den bisherigen Dateien im Spiel (Schuss −1,5, Klinge −4,9, Schritt −12,1, Ghul-Ankündigung −3,0 dBFS) und ist je Kategorie fest. Die Mittelwerte (RMS) liegen bei Schuss, Schritt und Ankündigung 1 bis 2,5 dB unter den alten Dateien (alt: Schuss −18,2, Schritt −30,8, Ankündigung −18,3 dB), beim Treffer etwa 3 bis 4 dB darunter (alt −23,7). Alle Dateien spielt das Spiel mit Lautstärke 1; die Lautheit steckt in den Dateien.

{kat}

Reihenfolge von laut nach leise: Ankündigung, Schuss, Schmerz, Ghul, Treffer, Nachladen, Schwung, Bewegung, Tür/Kiste, Aufheben, Schritte, Menü. Die **Ankündigung** (`p1-ghul-ankuendigung`) hat mit dem Schuss die höchste Spitze (−1,5 dBFS), liegt aber in einem **eigenen Band**: 300 bis 3 000 Hz tragen praktisch die ganze Energie, unter 300 Hz liegt sie 29 dB tiefer (die Katakomben-Musik liegt in dieser Mitte mindestens 15 dB unter ihrem Gesamtpegel). Sie hat genau einen Einsatz (gemessen nach Regel 2 der Audio Bible).

## Wo ich am unsichersten bin

1. **Stimme (Ghul, Schmerz):** Die Stimme ist eine Säge mit zwei Formant-Spitzen (700 und 1 150 Hz), als Sample bei E3; Tonhöhenfahrten macht der SoundCraft-`pitch_shifter` per Automation (Anstieg der Ankündigung, Fall beim Sterben und beim Schmerz). Ob das „menschlich“ oder wie ein Synthesizer klingt und ob der Tonhöhen-Effekt Artefakte hat, kann ich nicht hören. Der Mensch wollte Ghul „menschlich und sehr einfach“; dafür ist eine **vierte und fünfte Quelle** (Hauch, Stimme) nötig geworden, weil Schlag, Pfeife und Glocke keine Stimme und keinen Luftklang liefern.
2. **Klingenschwung (Hauch):** Charakter von 2a (luftig, kein Anschlag) in der neuen Palette gebaut: weißes Rauschen, Bandpass fährt von 900 auf 3 800 Hz (0,35 s), Pegel steigt sanft und fällt (45 dB unter, 0,15 s hoch, 0,5 s Ende), **kein** Tiefpass-Sturz wie beim Rauschschlag. Gemessen sitzt er hell (Schwerpunkt etwa 2,8 kHz) und hat anders als der Treffer keinen Anschlag. Ob er wie ein Schwung klingt und nicht wie ein Zischen, ist offen; ebenso, ob er zu 2b (Treffer) „gleichzeitig“ abgespielt noch trennt.
3. **Gleiche Quelle für Schuss, Treffer, Schritte, Nachladen, Landung, Kiste, Menü-Klick:** Alle sind derselbe Rauschschlag bei anderer Tonhöhe und Länge. Das macht sie zu einer Familie, kann aber heißen, dass man sie bei schnellem Wechsel schwer auseinanderhält (Schritt, Landung und Kiste sind alle dumpfe Schläge, 12 bis 18 Halbtöne unter der Aufnahme). Unterscheidung nur über Tonhöhe und Länge.
4. **Sprung, Dash, Tür:** Dasselbe Hauch-Sample in drei Tonlagen (hoch/kurz, mittel, sehr tief/lang). Als „Tür“ ist ein tief gespieltes Rauschen vielleicht nicht schwer genug; die Messung zeigt bei Dash und Tür mehrere Pegelanstiege (Rauschen mit Bandpassfahrt), was als Zucken zu hören sein kann.
5. **Aufheben je Verbrauchsgut:** Unterscheidung nur durch Zahl, Richtung und Lage der Töne (Silber ein Ton E4; Phiole zwei aufsteigend G3→H3; Weihmunition zwei fallend E4→H3; Schlüssel drei E3→H3→E4; Ausrüstung ein tiefer langer Ton E2). Ob man Phiole und Weihmunition mit Hören auseinanderhält, ist ungeprüft; ob sie bei vielen Pickups hintereinander nerven, auch.
6. **Fehlende Ereignisse im Spiel:** `p1-ghul-entdecken`, Sprung, Dash, Landung, Menü-Klick und das Aufheben von Ausrüstung haben noch kein Ereignis (Liste in der Übergabe); die Dateien sind gebaut, aber nicht auslösbar.
"""
(HERE / "uebersicht.md").write_text(uebersicht, encoding="utf-8")
print("uebersicht.md geschrieben")
