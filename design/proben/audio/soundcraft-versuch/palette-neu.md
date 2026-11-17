# Neue Klangpalette (Hörprobe 9) – von Grund auf, nach der Methode aus dem Video

Stand 2026-11-17. Auftrag des Menschen: „Die Soundpaletten soll er komplett neu machen.“ **Übernommen wird kein Klangprofil** aus den Hörproben 1–8 (kein Glockenprofil aus neun Teiltönen, kein Chor-Pad aus verstimmten Sägen, keine Synthese-Rezepte der Geräusche, keine Drum-Synth-Schläge). Bestehen bleibt nur der Rahmen, den der Mensch bestätigt hat: düstere, sparsame Gothic-Stimmung, PS1-nah, Musik und tonale Geräusche in **einer** Klangwelt, **ein** kurzer einheitlicher Raum, Ghul menschlich und einfach (kommt in Hörprobe 9 nicht vor). Methode: [video.md](video.md). Alles ungehört.

## Leitgedanke aus dem Video

Ein PS1-Klang ist **kein fertig gerechneter Ton mit Filter am Ende**, sondern ein **kurzes Sample**, das in der Tonhöhe verschoben gespielt wird (Video 09:09–09:45); die Textur kommt aus niedriger Rate und ADPCM (02:12–02:30) und wird beim **Tiefspielen** deutlich (09:31). Also gilt für die ganze Palette: **jeder Klang ist ein einziges Sample, alle Noten und Geräusche sind dieselben Samples bei anderer Tonhöhe.** Das macht Musik und Geräusche von selbst kohärent: Sie teilen sich dieselben drei Quellen.

## Die drei Instrumente (je ein Sample, alle bei 22 050 Hz, mono, 16 Bit, VAG)

| Instrument | Quelle in SoundCraft (neu gebaut, nicht aus früheren Hörproben) | Form des Samples | Rolle |
|---|---|---|---|
| **Pfeife** | Subtractive Synth: Rechteck plus Dreieck eine Oktave höher, kein Verstimmen, Tiefpass 900 Hz, aufgenommen bei E3 | 2,74 s (60 452 Frames), **Schleife** ab 0,8 s bis Ende, rund 1,9 s lang (Anfang, Ende und Länge Vielfache von 28; Länge so gewählt, dass das Ende phasenrichtig zum Anfang passt; 0,5 s linear überblendet; Video 02:41–06:31) | Dauerton: Akkorde der Musik, Untergrund; hohl und „orgelig“, Gegenstück zu den Streicher-Pads der PS1-Zeit |
| **Metallglocke** | Subtractive Synth (Dreieck plus Sinus eine Oktave höher) durch **Ringmodulator** (395 Hz, Verhältnis 2,4 zum Grundton E3 164,8 Hz → unharmonische Nebentöne; 60 % Anteil, damit der Grundton hörbar bleibt), bei E3 aufgenommen, langer Abfall | 6,2 s (136 696 Frames), ohne Schleife, Einzelschlag (Video: Einzelschläge brauchen keine Schleife, 02:32) | Töne mit Gewicht: Glockenschläge der Musik, tonale Geräusche (Aufheben); tief gespielt dunkel und lang |
| **Rauschschlag** | Signalgenerator **weißes Rauschen**, Tiefpass fällt von 8 kHz auf 1,5 kHz in 60 ms, Pegel −50 dB nach 0,35 s (Automation). Weiß statt rosa, weil rosa bei +1 Oktave zu dumpf für die Klinge war (gemessen: Anteil über 3 kHz −19 statt −6 dB) | 0,5 s, ohne Schleife | alle geräuschhaften Treffer: Schuss (tief gespielt), Klingentreffer (hoch gespielt) |

Warum nur drei: Das Video zeigt einen Weg für **ein** Sample nach dem anderen; jede weitere Quelle wäre ein weiterer Stil. Drei decken Dauerton, Ton mit Anschlag und Geräusch ab; das reicht für die vier Stücke.

## Gemeinsame Kette (für jedes Instrument und jedes Stück gleich)

1. **Sample-Aufbereitung nach dem Video:** mono → 16 Bit → 22 050 Hz → (Schleife auf 28er-Raster, überblendet) → VAG (ADPCM) → Sampler. Nur **Einblendung am Anfang** (2 ms), kein Ausblenden (01:58). Vor dem Speichern 16 Bit prüfen (02:07).
2. **Sampler** in SoundCraft: Wurzelton = Aufnahmeton; die Tonhöhe verschiebt der Sampler. **Keine PS1-Kette danach** (die Textur steckt im Sample).
3. **Ein Raum** für alles: `room_reverb`, Mix 28 %, Abklingzeit 1,1 s, Größe 40 %, Dämpfung 3 500 Hz, Vorverzögerung 8 ms, unter 150 Hz abgeschnitten – auf jeder Spur dieselbe Einstellung (SoundCraft erlaubt keine Effekte auf Bussen). Kurz, damit Schuss und Aufheben nicht „in der Halle“ stehen und die Skizze nicht verschwimmt; der Raum ist nicht Teil der PS1-Textur, sondern Zugabe (der Mensch lobte das Menü-Thema mit Hall; ob 1,1 s reicht, ist offen).
4. **Pegel:** gleiche Lautheit zwischen a und b, Spitze höchstens −1 dBFS.

## Tonart und Regeln für die Geräusche

- **Tonart: e-Moll** (E Fis G A H C D), weil das Menü-Thema im Spiel bleibt und alles im selben Tonraum liegen soll. Grundton E; Bass-Töne E1/E2, Akkorde E3–E4.
- **Tonale Geräusche** (Aufheben, später Münzen, Türklang) sind **immer die Metallglocke auf einem Ton der Tonart** (E, G, H, D), nie auf einem fremden Ton. Aufheben = E4 (eine Oktave über der Aufnahme E3).
- **Geräuschhafte Geräusche** (Schuss, Klinge, später Schritte, Aufprall) sind **immer der Rauschschlag**, nur durch Tonhöhe, Länge (Sampler-Abklingzeit) und Wurzel verändert: **tiefer = schwerer** (Schuss: eine Oktave tiefer), **höher = schärfer** (Klinge: eine Oktave höher).
- **Eine Geste je Geräusch:** eine Note, ein Sample, keine Schichten.
- **Transposition begrenzen:** höchstens eine Oktave nach oben, bis zu eineinhalb Oktaven nach unten; weiter nach unten macht das Sample dumpf und lang (das ist gewollt, aber prüfen), nach oben körnig.
- **Rate bleibt 22 050 Hz**, nichts über 11 kHz; alle Dateien einzeln, mono (Geräusche) bzw. stereo (Musik), 16 Bit, 44,1 kHz im Ergebnis.

## Was die Palette nicht kann (bewusst offen)

- **Keine Gauß-Interpolation und kein echter VAG-Kodierer:** SoundCraft interpoliert beim Transponieren auf eigene Weise; die VAG-Wandlung ist meine Annäherung aus `ps1_klang.py`. Das Video nennt gerade die Gauß-Glättung bei tiefen Tönen als Kern der Textur. Ob b trotzdem „PS1“ klingt, hört nur der Mensch.
- **Keine Tonhöhenhüllkurve im Sampler:** ein fallender Schlag (Trommel, Körperton der Klinge) ist nicht Teil der Palette. (Für Stimmen geht ein Umweg, siehe unten.)

## Erweiterung für Geräuschpaket 1 (2026-11-17, nach dem Urteil zu Hörprobe 9)

Urteil des Menschen: Schuss = Rauschschlag tief (1b); die Klinge braucht **zwei** Geräusche: Treffer = Rauschschlag hoch (2b), Schwung ohne Treffer = luftig, ohne Anschlag (Charakter von 2a, aber in der neuen Palette); Aufheben **je Verbrauchsgut** eigen; Skizze: nur als Probe gedacht, keine weitere Musik vorerst. Dafür kommen **zwei Quellen** dazu (jetzt fünf, die obere Grenze von drei bis fünf):

| Instrument | Quelle in SoundCraft | Form | Rolle und Begründung |
|---|---|---|---|
| **Hauch** | Signalgenerator weißes Rauschen; Bandpass (`eq_1band`, Typ Bandpass) fährt per Automation von 900 auf 3 800 Hz in 0,35 s; Pegel steigt von −45 dB sanft auf 0 dB nach 0,15 s und fällt bis zum Ende (0,5 s) wieder auf −45 dB; 11 004 Frames, ohne Schleife | Einzelschlag, kein Anschlag | **Klingenschwung** (kein Treffer), Sprung, Dash, Tür. Warum nicht der Rauschschlag: Der hat einen Anschlag und einen Tiefpass-Sturz (dumpf); der Schwung soll luftig sein. Wie 2a wirkt (hell, kein Anschlag), wird hier durch **ansteigenden** Pegel und **steigenden** Bandpass erzeugt, nicht durch ein altes Rezept (kein Drum-Synth, kein Hi-Hat) |
| **Stimme** | Subtractive Synth, **eine** Säge ohne Verstimmung, Tiefpass 4 kHz, durch zwei Formant-Spitzen (`eq_1band` Peak, 700 Hz +14 dB Q 3 und 1 150 Hz +12 dB Q 3, Vokal „a“) und einen Tiefpass 3 kHz; bei E3 aufgenommen (2,45 s), Schleife ab 1,0 s, 0,5 s überblendet, Länge phasenrichtig | Dauerton, Schleife | **Ghul** (menschlich, sehr einfach) und **Schmerz** der Spielfigur. Keine der drei alten Quellen kann eine Stimme. Bewusst die einfachste Stimme: ein Vokal, eine Quelle |

**Tonhöhenfahrten der Stimme:** Der Sampler kann innerhalb einer Note die Tonhöhe nicht ändern. Umweg: `pitch_shifter` als Effekt auf der Spur, sein Parameter `semitones` per Automation (`plugin:0:semitones`) – gemessen 180 → 244 Hz bei +7 Halbtönen. Das ersetzt die fehlende Tonhöhenhüllkurve **nur für die Stimme** (Ghul, Schmerz); der Effekt kann Artefakte erzeugen (ungehört).

**Regeln aus Paket 1 (zusätzlich zu den obigen):**
- **Schlag:** Schuss (eine Oktave tiefer), Treffer (eine Oktave höher), Schritte (zwei Oktaven tiefer, kurz: Abklingzeit 70 ms), Nachladen, Landung, Kiste, Menü-Klick: dasselbe Sample, nur Tonhöhe und Abklingzeit ändern sich.
- **Hauch:** Klingenschwung, Sprung (+12, kurz), Dash (−5), Tür (−17, lang). Nie mit Anschlag.
- **Stimme:** Ghul-Entdecken (steigt +3), Ankündigung (**eine Oktave über der Aufnahme**, steigt +7 und schwillt von −18 dB auf 0 dB an, endet am Gipfel: eine Uhr nach Regel 2; liegt dadurch ganz im Band 300–3 000 Hz und damit über der Musik und den anderen Geräuschen), Sterben (fällt −9, 0,9 s), Schmerz (fällt −4, kurz).
- **Aufheben (Glocke, Töne von e-Moll):** je Verbrauchsgut **Zahl, Richtung und Lage** der Töne, als Familie ein Klingen derselben Glocke: Silber ein Ton E4; Phiole zwei aufsteigend G3→H3; Weihmunition E4 und H3 **zugleich** (ein heller, längerer Klang, 1,4 s; „geweiht“ = Zusammenklang statt Folge); geweihter Schuss (Vorschlag) = Rauschschlag plus kurzer Glockenton E4 als zweite Spur; Schlüssel drei E3→H3→E4; Ausrüstung ein tiefer langer Ton E2.
- **Ein Raum für alles, kürzer als in Hörprobe 9:** Hall 0,8 s, Größe 30 %, Mix 22 % (vorher 1,1 s, 40 %, 28 %), weil bei winzigen Geräuschen (Schritt, Klick) der Hall nach dem trockenen Ton zu einem hörbaren zweiten Einsatz wuchs (gemessen: Pegel stieg nach dem Abfall wieder um 9 dB).
- **Lautheit je Kategorie fest** (Spitze dBFS): Ankündigung −1,5; Schuss −1,5; Schmerz −5; Ghul −6; Treffer −4,5; Nachladen −8; Schwung −9; Bewegung −11; Tür/Kiste −7; Aufheben −8; Schritte −12; Menü −12 (Tabelle in `paket-1/uebersicht.md`).
