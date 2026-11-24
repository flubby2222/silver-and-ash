# Hörprobe „großes Tor“ der Bossraum-Tür (#549)

Probestück außerhalb des Spiels, kein Spielcode. Weder die Leitung noch der Audiodesigner können hören: Was unten steht, ist **gemessen**. Ob etwas schwer, feierlich und nach Holz klingt, entscheidet allein der Mensch.

## Was klingt

Ein 6 m hohes, zweiflügliges Holztor mit schweren Eisenbändern im Steinportal. Es schließt sich hinter den Spielern, wenn der Kampf beginnt, und öffnet sich danach. Niemand bedient es sichtbar. Der Klang folgt den geltenden Urteilen: **Material statt Mechanik** (keine Kette, kein Kurbeln, kein Zahnrad), **nichts Hohes oder Schrilles**, Holz auf Stein führt, Eisen ist leises Beiwerk.

Zeitbasis sind die vorläufig empfohlenen Bewegungen aus `design/proben/bewegung/bossraum-tuer-549/` (60 Ticks je Sekunde):

- **Schließen „Anlauf und harter Anschlag“:** Anschlag bei Tick 78 (1,30 s), zwei kleine Rückpraller danach, Ruhe ab Tick 100.
- **Öffnen „gleichmäßig schwer“:** 132 Ticks (2,20 s), 30 Ticks Anlauf, 40 Ticks Auslauf, kein Anschlag.

## Aufbau (gemeinsam)

Alles per Synthese-Skript `bauen.py` (nur Python-Standardbibliothek, fester Seed, zwei Läufe bitgleich, geprüft mit `wav.sha256`). Mono, 44 100 Hz, 16 Bit. Ein Tiefpass bei 2,2 kHz (vierpolig) hält jedes Geräusch dunkel.

Die Bewegung besteht aus vier Schichten, alle vom Tempo der Bewegung geführt:

1. **Holzstimme:** ein einzelner tiefer Grundton (52 bis 112 Hz, Phase aufsummiert, kein Schweben) mit Obertönen bis etwa 1 kHz und Betonung um 180 und 420 Hz. Das Ächzen von Holz und Angeln.
2. **Haft-Gleit-Körner:** kurze gedämpfte Resonanzen zwischen 110 und 330 Hz, dichter bei höherem Tempo (das Knarren des Holzes).
3. **Eisen als Beiwerk:** wenige leise Körner bei 260 bis 520 Hz (Bänder und Angeln), rund 14 dB unter dem Holz.
4. **Rollen:** Rauschen unter 260 Hz, Pegel steigt mit dem Tempo.

Der **Anschlag** (Schließen) ist ein Ereignis aus vier Teilen: Kern (Sinus, fällt von 80 auf 40 Hz), Holzkörper (Rauschen unter 450 Hz), Eisenbeiwerk (vier gedämpfte Eigenschwingungen 211 bis 838 Hz, leise) und Nachbeben im Stein (unter 130 Hz). Davor wird die Bewegung 25 ms lang ausgeblendet, damit der Schlag allein steht. Die zwei Rückpraller sind eigene kleine Schläge bei Anschlag +14 und +22 Ticks (Pegel 0,20 und 0,06).

Beim **Öffnen** löst sich das Tor bei Tick 0 mit einem leisen dumpfen Stoß aus dem Stein; danach tragen die vier Schichten die Bewegung.

## Die Varianten

### Schließen (alle drei: gleicher Schlag auf Tick 78)

| Datei | Was man hören soll |
|---|---|
| `tor-schliessen-1-dumpfer-schlag.wav` | trocken: Anlauf aus Ächzen und Rollen, ein harter Schlag, sehr kurzer Raum (Nachhall 0,45 s), Rückpraller gut erkennbar, Ende nach 2,7 s |
| `tor-schliessen-2-mit-nachhall.wav` | derselbe Schlag in einem großen dunklen Steinraum (Nachhall 2,6 s, stark ausgesendet); der Schlag hängt als langer Raumklang nach, Ende nach 4,6 s |
| `tor-schliessen-3-mit-bodengrollen.wav` | Schlag plus 2 s Beben im Stein (Rauschen 25 bis 110 Hz, Ton 52 Hz mit Oktave), mittlerer Raum (1,4 s), Ende nach 4,0 s |

### Öffnen (alle drei: gleiche Bewegung, 2,2 s)

| Datei | Was man hören soll |
|---|---|
| `tor-oeffnen-1-kurzes-knarren.wav` | Knarren nur in den ersten ~1,3 s (Anlauf), dann blendet es aus; danach nur noch tiefes Rollen bis zum Stillstand, kurzer Raum, Ende nach 3,0 s |
| `tor-oeffnen-2-langes-knarren.wav` | Ächzen und Knarren durchgehend bis zum Stillstand (höchste Körnerdichte, Stimme 60 bis 112 Hz), langer Raumklang (Nachhall 2,4 s), Ende nach 4,6 s |
| `tor-oeffnen-3-mit-steinschleifen.wav` | Knarren wie in 1, dazu ein gleichmäßiger Teppich aus Schleifen (Rauschen 160 bis 900 Hz, körnig) von Holz und Eisenband auf der Schwelle, mittlerer Raum, Ende nach 3,4 s |

Zu jeder Datei gehört ein Spektrogramm (`<Name>.png`), bei Schließen dazu eine Wellenform mit roter Linie bei Tick 78 (`<Name>-welle.png`). Alle Zahlen und die Pegel je 100 ms stehen in `messwerte.md`.

## Messwerte

| Datei | Dauer s | Spitze dBFS | Mittel (RMS) dBFS | Schwerpunkt Hz | 95 % der Energie unter Hz | Energie > 1 kHz dB | Energie > 3 kHz dB | Letzte 50 ms dBFS |
|---|---|---|---|---|---|---|---|---|
| tor-schliessen-1-dumpfer-schlag | 2,73 | -2,5 | -20,0 | 78 | 205 | -45,0 | -82,2 | -200 (Stille) |
| tor-schliessen-2-mit-nachhall | 4,63 | -2,5 | -22,1 | 81 | 205 | -45,1 | -80,2 | -200 |
| tor-schliessen-3-mit-bodengrollen | 4,03 | -2,5 | -23,2 | 81 | 183 | -46,7 | -79,2 | -200 |
| tor-oeffnen-1-kurzes-knarren | 3,00 | -8,8 | -24,0 | 151 | 377 | -45,8 | -78,8 | -200 |
| tor-oeffnen-2-langes-knarren | 4,60 | -10,6 | -24,0 | 165 | 377 | -30,8 | -78,1 | -200 |
| tor-oeffnen-3-mit-steinschleifen | 3,40 | -11,3 | -24,0 | 149 | 355 | -37,6 | -78,6 | -200 |

Energie über 1 und 3 kHz in dB unter der Gesamtenergie, aus dem mittleren Spektrum (Hann 4096).

**Sitzt der Anschlag bei 1,30 s?** Einsatz = 32-Sample-Block mit dem stärksten Pegelanstieg gegen die 8 ms davor, Fenster ±60 ms um Tick 78:

| Datei | Einsatz ms | Abweichung ms | lautestes Sample bei s | Anstieg dB |
|---|---|---|---|---|
| tor-schliessen-1-dumpfer-schlag | 1301,0 | +1,0 | 1,312 | 16,2 |
| tor-schliessen-2-mit-nachhall | 1297,3 | -2,7 | 1,337 | 9,1 |
| tor-schliessen-3-mit-bodengrollen | 1301,0 | +1,0 | 1,337 | 13,6 |

Alle drei liegen innerhalb von 3 ms (unter einem Viertel Tick) auf dem Anschlag; das lauteste Sample jeweils 12 bis 37 ms danach, im Kern des Schlags. Die Rückpraller sind im Nachklang nicht verlässlich messbar und hier nicht belegt (sie sind als eigene kleine Schläge im Skript gesetzt). Der 100-ms-Pegel steigt bis zum Schlag an und springt im Fenster des Schlags um 10 dB (Schließen 1: -18 auf -8 dB; bei 2: -19 auf -9; bei 3: -21 auf -12) über das Fenster davor.

**Ist etwas schrill?** Gemessen: Schwerpunkt unter 170 Hz in allen Dateien, 95 % der Energie unter 400 Hz, Energie über 3 kHz mindestens 78 dB unter der Gesamtenergie, über 1 kHz mindestens 30 dB (die längere Knarr-Variante liegt am höchsten, durch die Obertöne der Holzstimme bis 1,1 kHz).

**Öffnen folgt der Bewegung** (Pegel je 100 ms, Plateau 0,5 bis 1,53 s): Anlauf (0 bis 0,5 s) 1 bis 5 dB unter dem Plateau, Auslauf bei Variante 1 bewusst leiser (Knarren ausgeblendet), bei 2 und 3 mit dem Tempo fallend. Das Öffnen hat keine einzelne Spitze (Spitzen -8,8 bis -11,3 dBFS, 6 bis 9 dB unter dem Schließ-Schlag).

**Pegel nach #427:** Der Schließ-Schlag hat die Spitze des Gorm-Aufschlags (-2,5 dBFS, der lauteste Einzelschlag im Bestand); das Öffnen das Mittel des Fallgitter-Hebens (-24 dBFS). Weil alle Schließ-Dateien auf dieselbe Spitze gepegelt sind, ist das Mittel bei der trockenen Variante höher (-20,0 gegen -22,1 und -23,2). Gewöhnliche Türen im Bestand: Spitze -7,0, Mittel -26,6 (`p1-tuer-oeffnen.wav`).

**Grenzen der Messung:** Die Energie liegt fast ganz unter 200 Hz (Schließen). Auf kleinen Lautsprechern oder Laptop-Boxen ist das kaum zu hören; die Hörprobe sollte an Kopfhörern oder einer Anlage mit Tiefgang oder mit einem Vergleichsgeräusch aus dem Spiel stattfinden. Die Messung sagt nichts darüber, ob es nach Holz oder nach Pappe klingt.

## Empfehlung des Audiodesigners (vorläufig, ungehört)

- **Schließen: Variante 3 „mit Bodengrollen“.** Begründung aus dem Aufbau, nicht aus dem Gehör: Das lange tiefe Beben im Stein trägt das „schwer und feierlich“ am deutlichsten, der mittlere Raum (1,4 s) bleibt unter dem langen Hall von Variante 2 (2,6 s), der gegen die Audio-Bible-Regel „kurze Ausklänge“ läuft. Variante 1 ist die sicherste Wahl, wenn der Raum später eigenen Hall im Spiel bekommt.
- **Öffnen: Variante 1 „kurzes Knarren“.** Sie hält das Knarren am knappsten, das Holzmaterial führt nur im Anlauf, und die Gesamtlänge (3,0 s) liegt am dichtesten an der Bewegung (2,2 s); der Rest ist Rollen. Variante 3 ist die richtige Wahl, wenn „Stein“ hörbar sein soll.

## Fragen an den Menschen

1. **Schließen: Welche Variante?** Empfehlung: 3 „mit Bodengrollen“ (Alternativen: 1 trocken, 2 langer Hall).
2. **Öffnen: Welche Variante?** Empfehlung: 1 „kurzes Knarren“ (Alternativen: 2 langes Knarren, 3 mit Steinschleifen).

Wählt der Mensch eine andere Bewegung, wird nur neu gerechnet: `python bauen.py --anschlag <Tick> --oeffnen <Ticks> --anlauf <Ticks> --auslauf <Ticks>`, danach `python messen.py . <Anschlag-Tick>`.

## Runde 2 (Urteil des Menschen zu Runde 1)

Urteil: Schliessen "Grollen beim oeffnen. Grollen, dann schlag beim schliessen." Oeffnen "Neu machen. Soll sich wie ein schleifen auf geroell anhoeren."

**Gelernte Regel (fuer die Audio-Bible):** Holzknarren passt nicht zum Steinportal. Die Tor-Bewegung klingt nach Stein: Oeffnen ist koerniges, schweres Schleifen auf Schutt mit tiefem Grollen darunter; Schliessen ist Grollen waehrend des Laufs, erst am Anschlag der Schlag (nicht umgekehrt).

Erzeuger: `bauen_r2.py` (nutzt Bausteine aus `bauen.py`; `python bauen_r2.py --anschlag 78 --oeffnen 132 --anlauf 30 --auslauf 40`). Messen: `python messen.py` (neu: Spalte Energie 200-800 Hz). Zeiten und Pegel unveraendert (Schliessen Spitze -2,5 dBFS, Oeffnen Mittel -24 dBFS, Spitze <= -3 dBFS).

Schliessen (Grollen + tiefes Beben + Koerner 200-700 Hz bis zum Anschlag, 25 ms Absenkung davor, dann Schlag mit zwei kleinen Rueckprallern):
1. `r2-tor-schliessen-1-grollen-kurzer-schlag.wav`: mittleres Grollen, kurzer Nachklang (0,45 s Hall).
2. `r2-tor-schliessen-2-starkes-grollen-langer-nachklang.wav`: staerkeres Grollen, langer Nachklang (2,6 s Hall) mit Bodengrollen.
3. `r2-tor-schliessen-3-grollen-mit-schutt-rieseln.wav`: wie 1, danach rieselt feiner Schutt (Koerner 250-800 Hz, abklingend).

Oeffnen (Koerner-Schleifen + Rauschbett + Grollen, kein Holz):
1. `r2-tor-oeffnen-1-geroell-fein.wav`: dichte feine Koerner (5-14 ms), wenig Grollen.
2. `r2-tor-oeffnen-2-geroell-grob.wav`: wenige grosse Brocken (18-50 ms) mit kollernden Stoessen, tiefer.
3. `r2-tor-oeffnen-3-geroell-mit-starkem-grollen.wav`: mittlere Koernung, starkes Grollen, langer Nachklang (2 s Hall).

Messwerte (Auszug, volle Tabelle in `messwerte.md`):

| Datei | Spitze | Mittel | Schwerpunkt Hz | 200-800 Hz dB | > 1 kHz dB | > 3 kHz dB |
|---|---|---|---|---|---|---|
| schliessen-1 | -2,5 | -20,8 | 79 | -12,1 | -36,4 | -74,7 |
| schliessen-2 | -2,5 | -20,7 | 99 | -10,0 | -32,8 | -71,2 |
| schliessen-3 | -2,5 | -22,6 | 81 | -11,4 | -33,2 | -71,4 |
| oeffnen-1 | -8,8 | -24,0 | 345 | -3,5 | -13,1 | -53,5 |
| oeffnen-2 | -8,8 | -24,0 | 184 | -7,5 | -18,3 | -56,9 |
| oeffnen-3 | -7,7 | -24,0 | 153 | -7,2 | -22,6 | -62,2 |

Der Anteil 200-800 Hz liegt in Runde 1 bei etwa -13 dB (Schliessen) und -5 bis -8 dB (Oeffnen); jetzt -10 bis -12 dB beziehungsweise -3,5 bis -7,5 dB. Alle Dateien enden auf digitaler Stille, zwei Laeufe sind bitgleich. Der Anschlag sitzt bei Variante 1 und 3 bei 1,301 s (Soll 1,300 s). Bei Variante 2 findet die Messung wegen des starken Grollens und des Halls keinen eindeutigen Einsatz (staerkster Anstieg bei +47 ms, lautestes Sample bei 1,335 s); der Schlag ist im Skript bei Tick 78 gesetzt. Oeffnen-1 ist am hellsten (Schwerpunkt 345 Hz, -13 dB ueber 1 kHz); falls es zu scharf ist, ist das der Regler.

Empfehlung (nach Bau und Messung, nicht nach Gehoer): Schliessen 1 (klarer Schlag, kurzer Nachklang, passt zur Regel "kurzer Schwanz"); Oeffnen 2 (groebste Koernung, am deutlichsten Schutt, wenig Anteil ueber 1 kHz). Fragen: Schliessen: 1, 2 oder 3? Oeffnen: 1 fein, 2 grob oder 3 mit starkem Grollen?
