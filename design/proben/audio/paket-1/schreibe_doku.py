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
ordnung = ["ankuendigung", "schuss", "schmerz", "ghul", "treffer", "nachladen", "schwung", "bewegung", "tuer_kiste", "aufheben", "schritt", "menue", "marke", "ankuendigung_b", "boss", "boss_schritt"]
kat_zeilen = []
for k in ordnung:
    ms = KAT[k]
    pk = sorted({m["peak_db"] for m in ms})
    rms = [m["rms_db"] for m in ms]
    kat_zeilen.append(f"| {k} | {len(ms)} | {pk[0]:.1f} | {min(rms):.1f} … {max(rms):.1f} |")
kat = "| Kategorie | Dateien | Spitze dBFS | Mittel (RMS) dBFS |\n|---|---|---|---|\n" + "\n".join(kat_zeilen)

uebersicht = f"""# Geräuschpaket 1 – Übersicht

Alle {len(d)} Dateien aus der neuen Palette ([palette-neu.md](../soundcraft-versuch/palette-neu.md)), nach der Methode aus dem Video ([video.md](../soundcraft-versuch/video.md)): sechs Quell-Samples (Schlag, Hauch, Pfeife, Glocke, Stimme, seit Paket 1.1 dazu die erweiterte Stimme), je einmal in SoundCraft gebaut, auf 22 050 Hz in VAG gewandelt und je Geräusch vom Sampler in der Tonhöhe verschoben gespielt; ein gemeinsamer kurzer Raum (Hall 0,8 s, 22 %). **Ungehört**; alle Zahlen sind gemessen. Erzeugung: `python paket1.py` (zweimal gerechnet, bitgleich); Dateien in `dateien/` und als Übergabe in [design/uebergabe/assets/audio/](../../uebergabe/assets/audio/) (Präfix `p1-`, Prüfsummen in `audio-paket-1.sha256`); Zuordnung zu den Spielereignissen und fehlende Ereignisse: [README-audio-paket-1.md](../../uebergabe/README-audio-paket-1.md).

## Je Geräusch eine Zeile

{tabelle}
„gespielt“: Ton und Halbtöne gegen die Aufnahme des Samples (Schlag und Hauch sind bei C4 aufgenommen und tonlos; Pfeife, Glocke und Stimme bei E3). Tonale Geräusche (Aufheben) liegen auf Tönen von e-Moll.

## Lautheit je Kategorie

Die Spitze richtet sich nach den bisherigen Dateien im Spiel (Schuss −1,5, Klinge −4,9, Schritt −12,1, Ghul-Ankündigung −3,0 dBFS) und ist je Kategorie fest. Die Mittelwerte (RMS) liegen bei Schuss, Schritt und Ankündigung 1 bis 2,5 dB unter den alten Dateien (alt: Schuss −18,2, Schritt −30,8, Ankündigung −18,3 dB), beim Treffer etwa 3 bis 4 dB darunter (alt −23,7). Alle Dateien spielt das Spiel mit Lautstärke 1; die Lautheit steckt in den Dateien.

{kat}

Reihenfolge von laut nach leise: Ankündigung, Schuss, Boss (Gorm: Aufschlag, Kette, Brüllen, Tod), Schmerz, Ghul, Treffer, Nachladen, Schwung, Bewegung, Tür/Kiste, Aufheben, Schritte, Menü. Die **Ankündigung** (`p1-ghul-ankuendigung`) hat mit dem Schuss die höchste Spitze (−1,5 dBFS). **Paket 1.1:** sie ist deutlich tiefer (D3 statt E4, Anstieg +3 statt +7 Halbtöne, Tiefpass 1 800 Hz, Hochpass 200 Hz) und liegt damit nicht mehr so isoliert im Mittenband wie vorher (damals unter 300 Hz 29 dB tiefer): 300 bis 3 000 Hz tragen noch die meiste Energie (Mitte −1,9 dB, tief unter 300 Hz −4,6 dB unter der Gesamtenergie), der Abstand ist knapp. Nach der 30-ms-Messung hat sie genau einen Einsatz (Regel 2 der Audio Bible); das 10-ms-Fenster zählt bei tiefen Tönen zwei, weil es einzelne Schwingungen sieht. Die zurückhaltendere Fassung `p1-ghul-ankuendigung-leise` (H2, Spitze −4,5) liegt **unter 300 Hz** (tief −1,8, Mitte −4,8): sie kommt nur über den Einsatz durch, nicht über das Band.

## Wo ich am unsichersten bin

1. **Stimme (Ghul, Schmerz) – Stand Paket 1.1:** Der Mensch fand die Sprachlaute „gut, aber ausbaufähig“. Ghul entdecken und sterben gibt es jetzt in je zwei Varianten; Variante 1 ist unverändert, Variante 2 nutzt eine **erweiterte Quelle `stimme2`** (drei Formanten 600/1 050/2 400 Hz, zweite Schwingung als Dreieck 12 Cent verstimmt für leichtes Schweben, Tonhöhenfahrt mit kleinen Schwankungen, ein leiser Hauch darunter als Rauschanteil). Ob das näher an „menschlich“ liegt oder nur anders, kann ich nicht hören. Der Schmerzlaut ist tiefer (H2 statt G3, Fall −3 statt −4 Halbtöne, 0,4 s statt 0,6 s, kürzeres Ausklingen) und liegt nicht höher als die Ghul-Laute. Zur ursprünglichen Quelle: Die Stimme ist eine Säge mit zwei Formant-Spitzen (700 und 1 150 Hz), als Sample bei E3; Tonhöhenfahrten macht der SoundCraft-`pitch_shifter` per Automation (Anstieg der Ankündigung, Fall beim Sterben und beim Schmerz). Ob das „menschlich“ oder wie ein Synthesizer klingt und ob der Tonhöhen-Effekt Artefakte hat, kann ich nicht hören. Der Mensch wollte Ghul „menschlich und sehr einfach“; dafür ist eine **vierte und fünfte Quelle** (Hauch, Stimme) nötig geworden, weil Schlag, Pfeife und Glocke keine Stimme und keinen Luftklang liefern.
2. **Klingenschwung (Hauch):** Charakter von 2a (luftig, kein Anschlag) in der neuen Palette gebaut: weißes Rauschen, Bandpass fährt von 900 auf 3 800 Hz (0,35 s), Pegel steigt sanft und fällt (45 dB unter, 0,15 s hoch, 0,5 s Ende), **kein** Tiefpass-Sturz wie beim Rauschschlag. Gemessen sitzt er hell (Schwerpunkt etwa 2,8 kHz) und hat anders als der Treffer keinen Anschlag. Ob er wie ein Schwung klingt und nicht wie ein Zischen, ist offen; ebenso, ob er zu 2b (Treffer) „gleichzeitig“ abgespielt noch trennt.
3. **Gleiche Quelle für Schuss, Treffer, Schritte, Nachladen, Landung, Kiste, Menü-Klick:** Alle sind derselbe Rauschschlag bei anderer Tonhöhe und Länge. Das macht sie zu einer Familie, kann aber heißen, dass man sie bei schnellem Wechsel schwer auseinanderhält (Schritt, Landung und Kiste sind alle dumpfe Schläge, 12 bis 18 Halbtöne unter der Aufnahme). Unterscheidung nur über Tonhöhe und Länge.
4. **Sprung, Dash, Tür:** Dasselbe Hauch-Sample in drei Tonlagen (hoch/kurz, mittel, sehr tief/lang). Als „Tür“ ist ein tief gespieltes Rauschen vielleicht nicht schwer genug; die Messung zeigt bei Dash und Tür mehrere Pegelanstiege (Rauschen mit Bandpassfahrt), was als Zucken zu hören sein kann.
5. **Aufheben je Verbrauchsgut:** Unterscheidung nur durch Zahl, Richtung und Lage der Töne (Silber ein Ton E4; Phiole zwei aufsteigend G3→H3; Weihmunition E4 und H3 **zugleich** als ein längerer Klang, 1,4 s; Schlüssel drei E3→H3→E4; Ausrüstung ein tiefer langer Ton E2). Ob man Phiole (Folge) und Weihmunition (Zusammenklang) mit Hören auseinanderhält, ist ungeprüft; ob sie bei vielen Pickups hintereinander nerven, auch.
7. **Vorschlag geweihte Munition (nicht bestellt, getrennt zu bewerten):** `p1-schuss-geweiht-1`, `-2` sind der Rauschschlag des Schusses mit einem kurzen Glockenton (E4) als zweiter Spur (zwei Schichten, eine Geste); `p1-weihe-ende` ist ein leiser tiefer Glockenton (E3) als Marke fürs Ende der Wirkung – es gibt im Core **kein** Ereignis dafür. Da geweihte Munition die Feuerrate verdoppelt, überlappen die Schüsse bei Dauerfeuer; ob der Glockenanteil dann klingelt oder nervt, ist offen.
8. **Fledermaus (Issue #297):** Alles aus den vorhandenen Quellen, nicht schrill: Ankündigung des Spuckens ist ein einzelner Glockenton H4 (0,7 s), das Kreischen vor dem Sturz ein Pfeifton E4 mit fallender Tonfahrt (0,55 s, Spitze −1,5, Mittel −8,3 dBFS: **der lauteste Mittelwert des Pakets**), das Flattern zwei kurze Hauchschläge. Unterscheidung vom Ghul: andere Quelle (Glocke und Pfeife statt Stimme) und höhere Lage. Ob die Glocke H4 mit den Aufheb-Glocken verwechselt wird (gleiche Quelle und Lage wie `p1-aufheben-silber`, E4), ist offen.
9. **Gorm (Issue #298):** Alle Gorm-Laute liegen tief (Schwerpunkt meist 100 bis 200 Hz) und haben **wenig Mittenband** (Mitte −8 bis −20 dB unter Gesamt); nur die Hammer-Ankündigung hat ein angehobenes Band um 500 Hz (Mitte −8,9 dB). Die drei Ankündigungen trennen sich durch die **Klangart**: Hammer = tiefer Pfeifton (H2, schwillt an), Kettenschwung = wirbelndes Rauschen (Hauch, Mitte dominant; der Anstieg über 0,33 s wird vom Einsatzzähler als vier Anstiege gezählt, der Verlauf steigt aber nur und fällt dann), Wurf = ein tiefer Glockenschlag (E2). Ob man sie im Kampf trennt, ist ungehört. Brüllen und Tod sind sehr tiefe Stimme mit Tonfahrt; der Einsatzzähler gibt dort 12 bzw. 19 an, weil die Perioden unter 10 ms liegen (30-ms-Fenster: 2).
6. **Fehlende Ereignisse im Spiel:** `p1-ghul-entdecken`, Sprung, Dash, Landung, Menü-Klick und das Aufheben von Ausrüstung haben noch kein Ereignis (Liste in der Übergabe); die Dateien sind gebaut, aber nicht auslösbar.
"""
(HERE / "uebersicht.md").write_text(uebersicht, encoding="utf-8")
print("uebersicht.md geschrieben")
