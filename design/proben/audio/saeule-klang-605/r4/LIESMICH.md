# Säulen-Klang Runde 4 (Issue #605)

Anlass: Urteil zu Runde 3 „besser. etwas dumpfer“ (alle drei Klänge, a und b nicht unterschieden). Gebaut mit `bauen.py` (nur Python-Standardbibliothek, bitgleich), gemessen mit `python messen.py` (`messwerte.md`; `../messen.py` verlangt jetzt den Ordner als Argument). Mono, 44,1 kHz, 16 Bit, Spitze unter −1 dBFS; a, b und c eines Klangs haben gleiche mittlere Lautstärke, Gesamtpegel wieder auf dem Niveau von Runde 2. Gemessen, nicht gehört.

**Gegenüber Runde 3:** Art, Muster und Seeds sind gleich; nur die Klangfarbe ist tiefer: Grenzfrequenz kleiner Brocken und des Griesrauschens etwa 1,25 kHz statt 2,3 kHz, Abschlusstiefpass 2,6 kHz statt 5 kHz, mehr tiefes Masse-Rauschen.
**c** ist dieselbe Struktur wie a mit einem deutlich stärkeren Schritt (Grenzfrequenz etwa 0,75 kHz, Abschlusstiefpass 1,5 kHz, noch mehr Masse).

| Klang | a | b | c |
|---|---|---|---|
| Treffer Stufe 1 | schwerer Riss, kurzes dichtes Geröll | loses, unregelmäßiges Nachrutschen | wie a, deutlich dumpfer |
| Treffer Stufe 2 | schwerer Riss, dichtes kurzes Geröll, Staub | Hauptstoß, längeres Nachstürzen mit Lücken | wie a, deutlich dumpfer |
| Bruch (Aufschlag bei 0,70 s) | Mahlen, Aufschlag, dichtes Geröll, das schnell abnimmt | Mahlen, Aufschlag, längeres unregelmäßiges Nachstürzen | wie a, deutlich dumpfer |

Dateien: `saeule-treffer-stufe1-{a,b,c}.wav`, `saeule-treffer-stufe2-{a,b,c}.wav`, `saeule-bruch-{a,b,c}.wav`.
