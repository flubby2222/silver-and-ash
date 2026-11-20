# Hörprobe Teleporter (#436, Issue #476)

Probestücke außerhalb des Spiels. Weder Leitung noch Audiodesigner können hören: Alles unten ist Aufbau und Messung, kein Klangurteil. Das Urteil gibt der Mensch.

## Urteil des Menschen (20.11.2026) und Nachbesserung

- **Freischalten: A Horn + Glimmen gewählt**; B (Kristallton) und C (Puls und Funken) **verworfen** (Dateien bleiben liegen). Rückmeldung: Der Klang hackt am Ende ab, er soll sanft auslaufen.
- **Reise: B Kristall-Sog gewählt** (Abreise und Ankunft); A (Horn-Gleiten) und C (Dröhnen) **verworfen** (Dateien bleiben liegen).
- **Nachbesserung `freischalten-a-horn-glimmen.wav`:** Ursache gemessen: Horn und Körnchen endeten bei 1,3 s auf etwa -30 dB (50-ms-RMS), der Hall fiel danach in 50 ms um etwa 20 dB. Jetzt Horn 2,0 s, Körnchen blenden in 0,6 s aus, die Mischung läuft in 1,2 s per Kosinus auf null, Hall-Nachlauf 0,15 s. Datei 1,60 s auf 2,15 s. Letzte 50 ms: Spitze -91 dBFS (vorher -84 dBFS, aber erst nach dem steilen Abfall). Klang bis 0,8 s unverändert (Pegelverlauf gleich).
- **Prüfung Reise B:** Ankunft endet sauber (letzte 50 ms -91 dBFS), unverändert. Die Abreise endete mitten im Anschwellen (letzte 50 ms Spitze -6,2 dBFS) und ist jetzt 1,3 s lang, die letzten 0,7 s blenden weich aus (letzte 50 ms Spitze -52,5 dBFS). Der Anstieg ist dadurch etwas gestreckt (Schwellen über 1,3 statt 0,8 s).
- Regel (Audio-Bible, Regel 7): Spitze der letzten 50 ms höchstens -50 dBFS; `messen.py` führt die Spalte „Ende dBFS“. Die verworfene `reise-c-droehnen-abreise.wav` (-11,3 dBFS) erfüllt sie nicht, wurde aber nicht angefasst.

Ereignisse: **Freischalten** = `TeleporterUnlocked` (der Kristall erwacht), **Reise** = `PlayerTeleported`, als Paar Abreise + Ankunft (je Variante zwei Dateien; wer nur einen Klang will, nimmt die Ankunft oder die Abreise allein).

Rahmen: dunkles Horn, Kristall erwacht mit Glimmen; keine hohen, schrillen Töne. Gemessen: Grundtöne 49 bis 220 Hz, Teiltöne und Körnchen höchstens bis etwa 1,4 kHz (Horn-Teiltöne vor dem Tiefpass; Körnchen bis 700 Hz; Kristall-Teiltöne bis 2,4 kHz begrenzt), Tiefpässe bei 0,5 bis 2,2 kHz, Anteil über 3 kHz mindestens 36 dB unter dem Gesamtpegel. Mono, 16 Bit, 44 100 Hz, Spitze -3 dBFS (wie `skillpunkt.wav`; die Geräusche aus Paket 1 liegen bei -7 bis -8 dBFS). Tabelle mit Spektrogrammen: [messwerte.md](messwerte.md).

## Freischalten

| Variante | Datei | Länge | Aufbau |
|---|---|---|---|
| A Horn mit Glimmen | `freischalten-a-horn-glimmen.wav` | 1,60 s | Ein dunkler Hornstoß (98 Hz, Sägezahn-Teiltöne, Tiefpass 900 Hz, wächst in 0,18 s an), darüber ein leises Glimmen aus kurzen Sinus-Körnchen (293 bis 698 Hz, Moll-Pentatonik, damit nichts schwebt), das erst beim Ausklingen aufgeht. Kurzer Hall. |
| B Kristallton | `freischalten-b-kristallton.wav` | 1,55 s | Langsames Erwachen (0,28 s Anstieg) eines glasig-dunklen Tons (220 Hz, unharmonische Teiltöne 1 : 2,76 : 5,4), darunter ein leiser Hornton (73 Hz). Kein Glimmen, der Ton selbst ist der Kristall. |
| C Puls und Funken | `freischalten-c-puls-funken.wav` | 1,37 s | Tiefer Schlag (90 auf 55 Hz) wie ein Herzschlag, kurz danach ein Funkeln aus dichten Körnchen, das gegen Ende dichter wird. Kein Horn. |

## Reise (Abreise / Ankunft)

| Variante | Dateien | Länge | Aufbau |
|---|---|---|---|
| A Horn-Gleiten | `reise-a-horn-gleiten-abreise.wav` / `-ankunft.wav` | 1,00 s / 1,10 s | Abreise: Horn fällt von 196 auf 98 Hz, dazu ein leiser Sog aus Rauschen. Ankunft: kurzer Hornstoß steigt von 98 auf 131 Hz, tiefer Schlag (80 auf 49 Hz) darunter. |
| B Kristall-Sog | `reise-b-kristall-sog-abreise.wav` / `-ankunft.wav` | 0,80 s / 1,20 s | Abreise: Kristallton (165 Hz) schwillt an, Rauschen zieht von 1,5 kHz auf 500 Hz zusammen, Ende abrupt gedämpft. Ankunft: weicher Kristallanschlag (196 Hz) mit dumpfem Schlag (75 auf 52 Hz). |
| C Dröhnen | `reise-c-droehnen-abreise.wav` / `-ankunft.wav` | 0,70 s / 0,90 s | Abreise: nur Druck, ein tiefer Ton (61 Hz) schwillt an, dazu ein ansteigendes Rauschen-Band (300 bis 900 Hz). Ankunft: tiefer Schlag (85 auf 49 Hz) und ein kurzes Funkeln. |

## Messwerte in Kürze

(Volle Tabelle: `messwerte.md`.)

- Spitze überall -3,0 dBFS (nur `reise-b-kristall-sog-abreise` -3,3 wegen des Ausblendens am Ende).
- Mittlere Lautstärke zwischen -11,2 und -20,4 dB. Am lautesten wirkt nach Messung `reise-b-kristall-sog-abreise` (-11,2 dB), am leisesten `reise-b-kristall-sog-ankunft` (-20,4 dB). Die Freischalten-Stücke liegen eng beieinander (-16,0 / -18,4 / -16,9 dB). Zum Vergleich `skillpunkt.wav`: Spitze -3,0, Mittel -23,0 dB. Die Proben sind also im Mittel lauter als der Skillpunkt-Klang; beim Einbau würde ich sie um etwa 4 bis 6 dB zurücknehmen (Abspielfaktor in `SoundCues`).
- Längen 0,7 bis 1,6 s. Die Bible nennt für Meldeklänge keine feste Grenze; das Urteil zum Skillpunkt verlangt Hauptteil unter etwa 0,5 s mit Nachklang. Abklang bis -40 dB: Freischalten 1,0 bis 1,3 s, Reise 0,6 bis 0,9 s. Den Hauptteil habe ich bei der Reise kürzer gehalten (A/B/C), weil sie im Kampf vorkommt.
- Einsätze (Regel 2): im 30-ms-Fenster genau einer je Stück (Ausnahmen: die beiden Ankünfte A und B und C-Ankunft zählen 0, weil ihr Anschlag langsamer als 8 dB in 50 ms steigt oder tief liegt). Im 10-ms-Fenster zählen die tiefen Stücke bis zu drei: bekannter Messfehler bei Tönen unter 70 Hz (Periode länger als das Fenster), kein echter zweiter Einsatz.
- Alle Dateien enden auf Stille (30 ms Ausblenden), Anfang 4 ms eingeblendet.

## Reproduzierbarkeit

`python bauen.py` (nur Standardbibliothek, fester Seed je Stück) erzeugt alle neun Dateien in 2 s. Ein zweiter Lauf in einen anderen Ordner lieferte neun bitgleiche Dateien; die Prüfsummen stehen in `wav.sha256`. `python messen.py` erzeugt `messwerte.md` und die Spektrogramme (braucht `ffmpeg`).

## Empfehlung (Vermutung, ungehört)

- **Freischalten: A.** Es ist die einzige Variante, die beide Vorgaben des Menschen zugleich enthält: das dunkle Horn und das Glimmen. B ist stiller und glasiger (nähere Richtung „Kristall“, weniger „Horn“), C hat kein Horn.
- **Reise: A**, damit Freischalten und Reise dieselbe Klangquelle haben (Horn). Abreise und Ankunft sind getrennt hörbar, weil Abreise fällt und Ankunft steigt. Wer die Reise leiser und unauffälliger will, nimmt C (kürzeste Stücke, geringster Höhenanteil: bis -48 dB über 3 kHz).
- Stärkster Kontrast für die Wahl: A (Horn) gegen B (Kristall) gegen C (nur Tiefe und Puls).

Fragen an den Menschen: Freischalten, welche Variante? Reise, welche Variante, und als Paar oder nur die Ankunft? Was stört?
