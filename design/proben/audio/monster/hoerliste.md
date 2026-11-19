# Hörliste Monsterklänge (Issue #392)

Urteil des Menschen nach dem Spieltest: „Fledermaus-Angriffssound passt nicht – sollte eher ein Spucken oder Zischen sein“ und „Die Monstersounds müssen definitiv überarbeitet werden – passen nicht zum Vibe – bisschen mehr Realismus“.

Alle Dateien sind gebaut und gemessen, **nicht gehört**. Beide Fassungen eines Klangs haben dieselbe Lautheit (RMS), damit nur der Klang entscheidet. Frage je Zeile: **a, b oder keine? Was stört?** Nichts davon ist im Spiel; die alten Dateien liegen unverändert in `assets/audio/`.

| Alt (Datei, Auslöser) | Fassung a | Fassung b | Unterschied |
|---|---|---|---|
| `p1-ghul-ankuendigung.wav` (Ghul holt aus) | `ghul-ankuendigung-a.wav` | `ghul-ankuendigung-b.wav` | a stimmhaftes Knurren, b stimmloser rasselnder Atem |
| `p1-ghul-entdecken.wav` (Ghul bemerkt Spieler) | `ghul-entdecken-a.wav` | `ghul-entdecken-b.wav` | a Stöhnen mit Vokalwechsel, b nasses Gurgeln |
| `p1-ghul-sterben.wav` (Ghul stirbt) | `ghul-sterben-a.wav` | `ghul-sterben-b.wav` | a fallende Stimme mit Atem, b reines rasselndes Ausatmen |
| `p1-fledermaus-spuck-ankuendigung.wav` (glüht vor dem Spucken) | `fledermaus-spuck-ankuendigung-a.wav` | `fledermaus-spuck-ankuendigung-b.wav` | a ansteigendes Zischen, b anschwellendes Gurgeln |
| `p1-fledermaus-spucken.wav` (spuckt Blutbolzen; **der Angriff**) | `fledermaus-spucken-a.wav` | `fledermaus-spucken-b.wav` | a Fauchen mit feuchtem Klicken, b dumpfes Ausspucken „ptuh“ |
| `p1-fledermaus-kreischen.wav` (kündigt Sturzflug an) | `fledermaus-kreischen-a.wav` | `fledermaus-kreischen-b.wav` | a Fauchen mit tiefer rauer Stimme darunter, b nur heiseres Krächzen |
| `p1-fledermaus-flattern-1.wav` (Flügelschlag) | `fledermaus-flattern-a.wav` | `fledermaus-flattern-b.wav` | a zwei weiche Wellen, b ein dumpfes Wuschen |
| `p1-fledermaus-biss.wav` (Biss im Sturzflug) | `fledermaus-biss-a.wav` | `fledermaus-biss-b.wav` | a feuchtes Schnappen mit Schlag, b Schnappen mit Knurrlaut |
| `p1-fledermaus-treffer.wav` (Fledermaus getroffen) | `fledermaus-treffer-a.wav` | `fledermaus-treffer-b.wav` | a kurzes Fauchen, b kurzer tiefer Rauhlaut |
| `p1-fledermaus-sterben.wav` (Fledermaus stirbt) | `fledermaus-sterben-a.wav` | `fledermaus-sterben-b.wav` | a schwacher fallender Stimmlaut, b ausatmendes Zischen |
| `p1-fledermaus-fehlsturz.wav` (Sturzflug ins Leere) | `fledermaus-fehlsturz-a.wav` | `fledermaus-fehlsturz-b.wav` | a hellerer Luftzug, b dumpferer Luftzug |
| `p1-gorm-bruellen.wav` (Gorm brüllt bei halber Gesundheit) | `gorm-bruellen-a.wav` | `gorm-bruellen-b.wav` | a offenes Brüllen, b knarrendes Knurren, tiefer |
| `p1-gorm-treffer-1.wav` (Gorm getroffen) | `gorm-treffer-a.wav` | `gorm-treffer-b.wav` | a Grunzen mit Stimme, b Ausstoßen von Luft |
| `p1-gorm-sterben.wav` (Gorm stirbt) | `gorm-sterben-a.wav` | `gorm-sterben-b.wav` | a absackendes Stöhnen, b nasses Rasseln |
| `p1-gorm-ankuendigung-hammer.wav` (Gorm hebt Keule) | `gorm-ankuendigung-hammer-a.wav` | `gorm-ankuendigung-hammer-b.wav` | a tiefes Einatmen, b anschwellendes Knurren |

**Nicht neu gebaut** (keine Stimmen, sondern Schläge, Metall, Schritte): Gorms Schritte, Aufschlag, Kette, Haken, Schwung- und Wurf-Ankündigung, Kopftreffer und zweiter Treffer. Bei Bedarf als zweite Runde.

Dateien liegen alle im selben Ordner. Messwerte und Spektrogramme: `messwerte.md`, `bilder/`. Erzeugen: `python design/proben/audio/monster/bauen.py`; Prüfung auf bitgleiche Läufe: `… --pruefen`.

## Grenzen (gemessen, ungehört)

- Lautheit: Pegel (RMS) je Paar gleich, gleich der alten Datei, soweit die Spitze (höchstens −1,5 dBFS) es zulässt. Bei `fledermaus-kreischen`, `fledermaus-sterben`, `gorm-sterben` und `gorm-ankuendigung-hammer` liegt die neue Fassung 1 bis 5 dB unter der alten, bei `fledermaus-kreischen` 10 dB (die alte war mit −8,3 dB Mittel sehr laut und schrill); im Spiel wäre das ein Pegel-Wert, kein Klangurteil.
- Ankündigungen (Regel 2 der Audio-Bible): mit 30-ms-Fenster höchstens ein Einsatz bei allen acht Fassungen; 0 bei `fledermaus-spuck-ankuendigung-a` und `fledermaus-kreischen-b` heißt: sanfter Beginn ohne Sprung, kein zweiter Einsatz. Mit 10-ms-Fenster zählen tiefe Stimmen (unter 70 Hz, Periode über 14 ms) und das Flattern mehrere Einsätze: Messartefakt der tiefen Periode, nicht gehört.
- Fledermaus-Zischen liegt um 1 bis 3 kHz (Anteil über 3 kHz bei `fledermaus-spucken-a` nur −3,6 dB, weil Zischen Rauschen ist); Stimmen der Fledermaus bleiben unter etwa 400 Hz Grundton.

## Runde 2 (Urteil vom 2026-11-19: gewählt sind Kreischen c, Spuck-Ankündigung a, Sterben b)

Die Fassungen c, a und b sind im Spiel (`tools/audio/monsterklaenge.py`); es ist keine Hörprobe mehr offen. Die Tabelle zeigt, was zur Wahl stand.

| Spielklang | Fassung c | Fassung d | Unterschied |
|---|---|---|---|
| `p1-fledermaus-kreischen.wav` (kündigt den Sturzflug an; a und b abgelehnt) | `fledermaus-kreischen-c.wav` | `fledermaus-kreischen-d.wav` | c raues Zirpen-Schnarren, Grundton 180 bis 230 Hz, Pegel flattert immer schneller (8 bis 28 Hz), Atem dabei; d Flügelschwirren: weiche Rauschschläge zwischen 200 und 1200 Hz, ohne Stimme, Schlagrate steigt von 6 auf 22 Hz. Beide schwellen an, 0,55 s, ein Einsatz mit 30-ms-Fenster, keine Energie über 3 kHz von Belang (c −15,8 dB, d −18,0 dB unter Gesamt) |
| `p1-fledermaus-spuck-ankuendigung.wav` (offen aus Runde 1) | `fledermaus-spuck-ankuendigung-a.wav` | `fledermaus-spuck-ankuendigung-b.wav` | a ansteigendes Zischen, b anschwellendes Gurgeln |
| `p1-fledermaus-sterben.wav` (offen aus Runde 1) | `fledermaus-sterben-a.wav` | `fledermaus-sterben-b.wav` | a schwacher fallender Stimmlaut, b ausatmendes Zischen |

Die Pegel von c und d liegen bei −19,0 dB Mittel (alt: −8,3 dB), weil die Spitze höchstens −1,5 dBFS erreichen darf; die alte Datei war sehr laut.
