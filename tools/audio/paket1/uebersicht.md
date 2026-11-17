# Geräuschpaket 1 – Übersicht

Alle 36 Dateien aus der neuen Palette ([palette-neu.md](../soundcraft-versuch/palette-neu.md)), nach der Methode aus dem Video ([video.md](../soundcraft-versuch/video.md)): sechs Quell-Samples (Schlag, Hauch, Pfeife, Glocke, Stimme, seit Paket 1.1 dazu die erweiterte Stimme `stimme2`), je einmal in SoundCraft gebaut, auf 22 050 Hz in VAG gewandelt und je Geräusch vom Sampler in der Tonhöhe verschoben gespielt; ein gemeinsamer kurzer Raum (Hall 0,8 s, 22 %). **Ungehört**; alle Zahlen sind gemessen. Erzeugung: `python paket1.py` (zweimal gerechnet, bitgleich); Dateien in `dateien/` und als Übergabe in [design/uebergabe/assets/audio/](../../uebergabe/assets/audio/) (Präfix `p1-`, Prüfsummen in `audio-paket-1.sha256`); Zuordnung zu den Spielereignissen und fehlende Ereignisse: [README-audio-paket-1.md](../../uebergabe/README-audio-paket-1.md).

## Je Geräusch eine Zeile

| Datei | Quelle | gespielt (Halbtöne gegen Aufnahme) | Dauer | Spitze dBFS | Mittel (RMS) dBFS | Kategorie |
|---|---|---|---|---|---|---|
| `p1-schuss-1.wav` | schlag | C3 (-12) | 0.60 s | -1.5 | -19.7 | schuss |
| `p1-schuss-2.wav` | schlag | H2 (-13) | 0.60 s | -1.5 | -19.4 | schuss |
| `p1-schuss-3.wav` | schlag | Cis3 (-11) | 0.60 s | -1.5 | -19.9 | schuss |
| `p1-nachladen-start.wav` | schlag | D4 (+2) | 0.30 s | -8.0 | -31.5 | nachladen |
| `p1-nachladen-bereit.wav` | schlag | G4 (+7) | 0.25 s | -8.0 | -31.9 | nachladen |
| `p1-schwung-1.wav` | hauch | E4 (+4) | 0.50 s | -9.0 | -29.5 | schwung |
| `p1-schwung-2.wav` | hauch | Dis4 (+3) | 0.50 s | -9.0 | -29.4 | schwung |
| `p1-treffer-1.wav` | schlag | C5 (+12) | 0.45 s | -4.5 | -27.6 | treffer |
| `p1-treffer-2.wav` | schlag | H4 (+11) | 0.45 s | -4.5 | -26.5 | treffer |
| `p1-treffer-3.wav` | schlag | Cis5 (+13) | 0.45 s | -4.5 | -27.6 | treffer |
| `p1-schritt-l1.wav` | schlag | A2 (-15) | 0.28 s | -12.0 | -33.0 | schritt |
| `p1-schritt-l2.wav` | schlag | Gis2 (-16) | 0.28 s | -12.0 | -32.8 | schritt |
| `p1-schritt-l3.wav` | schlag | G2 (-17) | 0.28 s | -12.0 | -32.6 | schritt |
| `p1-schritt-r1.wav` | schlag | H2 (-13) | 0.28 s | -12.0 | -33.3 | schritt |
| `p1-schritt-r2.wav` | schlag | B2 (-14) | 0.28 s | -12.0 | -33.1 | schritt |
| `p1-schritt-r3.wav` | schlag | C3 (-12) | 0.28 s | -12.0 | -33.4 | schritt |
| `p1-sprung.wav` | hauch | C5 (+12) | 0.30 s | -11.0 | -30.5 | bewegung |
| `p1-dash.wav` | hauch | G3 (-5) | 0.60 s | -11.0 | -30.1 | bewegung |
| `p1-landung.wav` | schlag | Fis2 (-18) | 0.35 s | -11.0 | -30.6 | bewegung |
| `p1-schmerz.wav` | stimme | H2 (-5) | 0.40 s | -5.0 | -18.9 | schmerz |
| `p1-ghul-entdecken.wav` | stimme | E3 (+0) | 0.90 s | -6.0 | -19.8 | ghul |
| `p1-ghul-entdecken-2.wav` | stimme2 | D3 (-2) | 0.85 s | -6.0 | -19.6 | ghul |
| `p1-ghul-ankuendigung.wav` | stimme | D3 (-2) | 1.00 s | -1.5 | -20.2 | ankuendigung |
| `p1-ghul-sterben.wav` | stimme | A2 (-7) | 1.30 s | -6.0 | -21.4 | ghul |
| `p1-ghul-sterben-2.wav` | stimme2 | G2 (-9) | 1.30 s | -6.0 | -20.5 | ghul |
| `p1-tuer-oeffnen.wav` | hauch | G2 (-17) | 1.30 s | -7.0 | -26.6 | tuer_kiste |
| `p1-kiste-oeffnen.wav` | schlag | B2 (-14) | 0.50 s | -7.0 | -28.6 | tuer_kiste |
| `p1-menue-klick.wav` | schlag | C5 (+12) | 0.12 s | -12.0 | -34.5 | menue |
| `p1-aufheben-silber.wav` | glocke | E4 (+12) | 0.60 s | -8.0 | -30.0 | aufheben |
| `p1-aufheben-phiole.wav` | glocke | G3 (+3), H3 (+7) | 0.80 s | -8.0 | -28.3 | aufheben |
| `p1-aufheben-weihmunition.wav` | glocke | E4 (+12), H3 (+7) | 1.40 s | -8.0 | -31.4 | aufheben |
| `p1-aufheben-schluessel.wav` | glocke | E3 (+0), H3 (+7), E4 (+12) | 0.90 s | -8.0 | -27.1 | aufheben |
| `p1-aufheben-ausruestung.wav` | glocke | E2 (-12) | 1.30 s | -8.0 | -28.9 | aufheben |
| `p1-schuss-geweiht-1.wav` | schlag | C3 (-12) | 0.60 s | -1.5 | -20.4 | schuss |
| `p1-schuss-geweiht-2.wav` | schlag | H2 (-13) | 0.60 s | -1.5 | -19.7 | schuss |
| `p1-weihe-ende.wav` | glocke | E3 (+0) | 0.70 s | -10.0 | -31.2 | marke |
| `p1-fledermaus-flattern-1.wav` | hauch | Fis4 (+6) | 0.22 s | -11.0 | -31.0 | bewegung |
| `p1-fledermaus-flattern-2.wav` | hauch | F4 (+5) | 0.22 s | -11.0 | -31.1 | bewegung |
| `p1-fledermaus-spuck-ankuendigung.wav` | glocke | H4 (+19) | 0.70 s | -1.5 | -23.5 | ankuendigung |
| `p1-fledermaus-spucken.wav` | hauch | E5 (+16) | 0.30 s | -9.0 | -28.9 | schwung |
| `p1-fledermaus-kreischen.wav` | pfeife | E4 (+12) | 0.55 s | -1.5 | -8.3 | ankuendigung |
| `p1-fledermaus-biss.wav` | schlag | Gis4 (+8) | 0.25 s | -4.5 | -28.4 | treffer |
| `p1-fledermaus-fehlsturz.wav` | hauch | B3 (-2) | 0.45 s | -11.0 | -28.0 | bewegung |
| `p1-fledermaus-treffer.wav` | schlag | B4 (+10) | 0.30 s | -4.5 | -28.9 | treffer |
| `p1-fledermaus-sterben.wav` | pfeife | D4 (+10) | 0.70 s | -6.0 | -15.9 | ghul |
| `p1-gorm-ankuendigung-hammer.wav` | pfeife | H2 (-5) | 1.00 s | -1.5 | -13.0 | ankuendigung |
| `p1-gorm-ankuendigung-schwung.wav` | hauch | B2 (-14) | 1.00 s | -1.5 | -20.3 | ankuendigung |
| `p1-gorm-ankuendigung-wurf.wav` | glocke | A2 (-7) | 0.80 s | -1.5 | -21.8 | ankuendigung |
| `p1-gorm-aufschlag.wav` | schlag | Fis1 (-30) | 1.00 s | -2.5 | -23.1 | boss |
| `p1-gorm-kette.wav` | glocke | E2 (-12) | 0.60 s | -2.5 | -17.6 | boss |
| `p1-gorm-haken-wurf.wav` | hauch | E3 (-8) | 0.40 s | -9.0 | -29.0 | schwung |
| `p1-gorm-haken-treffer.wav` | schlag | A1 (-27) | 0.60 s | -2.5 | -23.5 | boss |
| `p1-gorm-treffer-1.wav` | schlag | E3 (-8) | 0.35 s | -4.5 | -26.0 | treffer |
| `p1-gorm-treffer-2.wav` | schlag | D3 (-10) | 0.35 s | -4.5 | -25.5 | treffer |
| `p1-gorm-treffer-kopf.wav` | schlag | B3 (-2) | 0.40 s | -4.5 | -26.1 | treffer |
| `p1-gorm-bruellen.wav` | stimme | G1 (-21) | 1.50 s | -2.5 | -16.4 | boss |
| `p1-gorm-sterben.wav` | stimme | E1 (-24) | 1.50 s | -2.5 | -17.4 | boss |
| `p1-gorm-schritt-1.wav` | schlag | Fis1 (-30) | 0.45 s | -8.0 | -27.0 | boss_schritt |
| `p1-gorm-schritt-2.wav` | schlag | F1 (-31) | 0.45 s | -8.0 | -26.9 | boss_schritt |

„gespielt“: Ton und Halbtöne gegen die Aufnahme des Samples (Schlag und Hauch sind bei C4 aufgenommen und tonlos; Pfeife, Glocke und Stimme bei E3). Tonale Geräusche (Aufheben) liegen auf Tönen von e-Moll.

## Lautheit je Kategorie

Die Spitze richtet sich nach den bisherigen Dateien im Spiel (Schuss −1,5, Klinge −4,9, Schritt −12,1, Ghul-Ankündigung −3,0 dBFS) und ist je Kategorie fest. Die Mittelwerte (RMS) liegen bei Schuss, Schritt und Ankündigung 1 bis 2,5 dB unter den alten Dateien (alt: Schuss −18,2, Schritt −30,8, Ankündigung −18,3 dB), beim Treffer etwa 3 bis 4 dB darunter (alt −23,7). Alle Dateien spielt das Spiel mit Lautstärke 1; die Lautheit steckt in den Dateien.

| Kategorie | Dateien | Spitze dBFS | Mittel (RMS) dBFS |
|---|---|---|---|
| ankuendigung | 1 | -1.5 | -20.2 … -20.2 |
| schuss | 5 | -1.5 | -20.4 … -19.4 |
| schmerz | 1 | -5.0 | -18.9 … -18.9 |
| ghul | 4 | -6.0 | -21.4 … -19.6 |
| treffer | 3 | -4.5 | -27.6 … -26.5 |
| nachladen | 2 | -8.0 | -31.9 … -31.5 |
| schwung | 2 | -9.0 | -29.5 … -29.4 |
| bewegung | 3 | -11.0 | -30.6 … -30.1 |
| tuer_kiste | 2 | -7.0 | -28.6 … -26.6 |
| aufheben | 5 | -8.0 | -31.4 … -27.1 |
| schritt | 6 | -12.0 | -33.4 … -32.6 |
| menue | 1 | -12.0 | -34.5 … -34.5 |
| marke | 1 | -10.0 | -31.2 … -31.2 |

Reihenfolge von laut nach leise: Ankündigung, Schuss, Schmerz, Ghul, Treffer, Nachladen, Schwung, Bewegung, Tür/Kiste, Aufheben, Schritte, Menü. Die **Ankündigung** (`p1-ghul-ankuendigung`) hat mit dem Schuss die höchste Spitze (−1,5 dBFS). **Paket 1.1** (Fassung A): sie ist deutlich tiefer (D3 statt E4, Anstieg +3 statt +7 Halbtöne, Tiefpass 1 800 Hz, Hochpass 200 Hz); 300 bis 3 000 Hz tragen noch die meiste Energie (Mitte −1,9 dB, unter 300 Hz −4,6 dB unter der Gesamtenergie), der Abstand zum tiefen Band ist knapp. Nach der 30-ms-Messung hat sie genau einen Einsatz (Regel 2 der Audio Bible). Die leisere Fassung B (`-leise`) ist nicht im Spiel.

## Wo ich am unsichersten bin

1. **Stimme (Ghul, Schmerz):** Paket 1.1: Ghul entdecken und sterben gibt es in je zwei Varianten; Variante 2 nutzt die erweiterte Quelle `stimme2` (drei Formanten 600/1 050/2 400 Hz, zweite Schwingung leicht verstimmt, kleine Tonhöhenschwankungen, leiser Hauch darunter). Der Schmerzlaut ist tiefer (H2 statt G3, 0,4 s statt 0,6 s) und liegt nicht höher als die Ghul-Laute. Die ursprüngliche Stimme ist eine Säge mit zwei Formant-Spitzen (700 und 1 150 Hz), als Sample bei E3; Tonhöhenfahrten macht der SoundCraft-`pitch_shifter` per Automation (Anstieg der Ankündigung, Fall beim Sterben und beim Schmerz). Ob das „menschlich“ oder wie ein Synthesizer klingt und ob der Tonhöhen-Effekt Artefakte hat, kann ich nicht hören. Der Mensch wollte Ghul „menschlich und sehr einfach“; dafür ist eine **vierte und fünfte Quelle** (Hauch, Stimme) nötig geworden, weil Schlag, Pfeife und Glocke keine Stimme und keinen Luftklang liefern.
2. **Klingenschwung (Hauch):** Charakter von 2a (luftig, kein Anschlag) in der neuen Palette gebaut: weißes Rauschen, Bandpass fährt von 900 auf 3 800 Hz (0,35 s), Pegel steigt sanft und fällt (45 dB unter, 0,15 s hoch, 0,5 s Ende), **kein** Tiefpass-Sturz wie beim Rauschschlag. Gemessen sitzt er hell (Schwerpunkt etwa 2,8 kHz) und hat anders als der Treffer keinen Anschlag. Ob er wie ein Schwung klingt und nicht wie ein Zischen, ist offen; ebenso, ob er zu 2b (Treffer) „gleichzeitig“ abgespielt noch trennt.
3. **Gleiche Quelle für Schuss, Treffer, Schritte, Nachladen, Landung, Kiste, Menü-Klick:** Alle sind derselbe Rauschschlag bei anderer Tonhöhe und Länge. Das macht sie zu einer Familie, kann aber heißen, dass man sie bei schnellem Wechsel schwer auseinanderhält (Schritt, Landung und Kiste sind alle dumpfe Schläge, 12 bis 18 Halbtöne unter der Aufnahme). Unterscheidung nur über Tonhöhe und Länge.
4. **Sprung, Dash, Tür:** Dasselbe Hauch-Sample in drei Tonlagen (hoch/kurz, mittel, sehr tief/lang). Als „Tür“ ist ein tief gespieltes Rauschen vielleicht nicht schwer genug; die Messung zeigt bei Dash und Tür mehrere Pegelanstiege (Rauschen mit Bandpassfahrt), was als Zucken zu hören sein kann.
5. **Aufheben je Verbrauchsgut:** Unterscheidung nur durch Zahl, Richtung und Lage der Töne (Silber ein Ton E4; Phiole zwei aufsteigend G3→H3; Weihmunition E4 und H3 **zugleich** als ein längerer Klang, 1,4 s; Schlüssel drei E3→H3→E4; Ausrüstung ein tiefer langer Ton E2). Ob man Phiole (Folge) und Weihmunition (Zusammenklang) mit Hören auseinanderhält, ist ungeprüft; ob sie bei vielen Pickups hintereinander nerven, auch.
7. **Vorschlag geweihte Munition (nicht bestellt, getrennt zu bewerten):** `p1-schuss-geweiht-1`, `-2` sind der Rauschschlag des Schusses mit einem kurzen Glockenton (E4) als zweiter Spur (zwei Schichten, eine Geste); `p1-weihe-ende` ist ein leiser tiefer Glockenton (E3) als Marke fürs Ende der Wirkung – seit #291 löst ihn das Core-Ereignis `ConsecrationEnded` aus. Da geweihte Munition die Feuerrate verdoppelt, überlappen die Schüsse bei Dauerfeuer; ob der Glockenanteil dann klingelt oder nervt, ist offen.
6. **Fehlende Ereignisse im Spiel:** Seit #291 haben Ghul entdecken, Sprung, Dash (auch Ausweichen), Landung (ab 0,6 m Fallhöhe), Ende der Weihe und Menü-Klick ihr Ereignis. Nur das Aufheben von Ausrüstung bleibt ohne Ereignis, bis es Ausrüstung im Spiel gibt (#194).
8. **Fledermaus (Issue #297):** Alles aus den vorhandenen Quellen, nicht schrill: Ankündigung des Spuckens ist ein einzelner Glockenton H4 (0,7 s), das Kreischen vor dem Sturz ein Pfeifton E4 mit fallender Tonfahrt (0,55 s, Spitze −1,5, Mittel −8,3 dBFS: **der lauteste Mittelwert des Pakets**), das Flattern zwei kurze Hauchschläge. Unterscheidung vom Ghul: andere Quelle (Glocke und Pfeife statt Stimme) und höhere Lage. Ob die Glocke H4 mit den Aufheb-Glocken verwechselt wird (gleiche Quelle und Lage wie `p1-aufheben-silber`, E4), ist offen. Seit #297 hat das Flattern sein Ereignis `BatFlapped` (alle 0,4 s je wache Fledermaus); das Spiel spielt es nur bis 8 m Abstand und höchstens alle 0,25 s.
9. **Gorm (Issue #298):** Alle Gorm-Laute liegen tief (Schwerpunkt meist 100 bis 200 Hz) und haben **wenig Mittenband** (Mitte −8 bis −20 dB unter Gesamt); nur die Hammer-Ankündigung hat ein angehobenes Band um 500 Hz (Mitte −8,9 dB). Die drei Ankündigungen trennen sich durch die **Klangart**: Hammer = tiefer Pfeifton (H2, schwillt an), Kettenschwung = wirbelndes Rauschen (Hauch, Mitte dominant; der Anstieg über 0,33 s wird vom Einsatzzähler als vier Anstiege gezählt, der Verlauf steigt aber nur und fällt dann), Wurf = ein tiefer Glockenschlag (E2). Ob man sie im Kampf trennt, ist ungehört. Brüllen und Tod sind sehr tiefe Stimme mit Tonfahrt; der Einsatzzähler gibt dort 12 bzw. 19 an, weil die Perioden unter 10 ms liegen (30-ms-Fenster: 2).
