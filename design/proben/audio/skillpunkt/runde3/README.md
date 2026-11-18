# Hörprobe Skillpunkt, Runde 3: Schlag mit Chor ohne Wummern (Issue #377)

Urteil des Menschen zur Endfassung (`../runde2/3-schlag-chor-final.wav`): „Die neue version ist besser. allerdings höre ich da wie so ein wummern oder eine interferenz heraus. Es soll smooth klingen“, und: „Ist besonders im neuen teil zu hören“. Die Endfassung bleibt unverändert liegen. Hier drei Fassungen gleicher Länge und gleichen Charakters (gleicher Schlag, gleicher Einsatz, gleiche Abklingzeit), gebaut und gemessen, **nicht gehört**. **Frage an den Menschen: a, b oder c? Klingt es glatt?**

## Ursache (gemessen)

In `runde2/bauen.py` steckt das Vibrato als Faktor in der Frequenz innerhalb des Sinus: `sin(2π · f · vib(t) · t)`. Die Phase ist dann `f·t·(1 + 0,004·sin(2π·5,5·t))`, die Frequenzabweichung wächst mit der Zeit (ungefähr `f · 0,004 · 2π · 5,5 · t`). Ein kleines Vibrato von 0,4 % wird so nach einer Sekunde zu einem Hin- und Herrutschen jeder Stimme um mehrere Hertz. Die Stimmen schweben gegeneinander, und im verlängerten Ausklang liegt das frei. Das ist die Deutung der Leitung (Schwebung), nur anders begründet: nicht verstimmte Stimmen, sondern ein Rechenfehler im Vibrato.

Messung an einer einzelnen 110-Hz-Stimme mit 0,4 % Vibrato bei 5,5 Hz, größte Abweichung von 110 Hz zwischen 0,4 und 1,0 s (Mischung mit 110 Hz, 50 ms gemittelt, Phasenableitung; wegen der Mittelung eine Untergrenze):

| Rechnung | Abweichung |
|---|---|
| alt (Faktor in der Frequenz, wie runde2) | 13,8 Hz |
| neu (Phase aufsummiert) | 0,4 Hz |

Zusätzlich liegen im a-Moll-Akkord 110 und 131 Hz nur 21 Hz auseinander; auch das gibt eine Schwebung, selbst bei sauberer Rechnung.

## Varianten

- **a – ohne Vibrato** (`a-ohne-vibrato.wav`): gleicher a-Moll-Akkord (110/131/165/220 Hz), Vibrato weggelassen. Die Schwebung zwischen 110 und 131 Hz bleibt.
- **b – offener Akkord** (`b-offener-akkord.wav`): nur Grundton, Quinte, Oktave (110/165/220 Hz), kein Vibrato. Keine nahe beieinander liegenden Töne, also nichts, was schweben kann; klingt offener, ohne Terz.
- **c – sanftes Vibrato** (`c-sanftes-vibrato.wav`): Akkord mit der Moll-Terz erst eine Oktave höher (110/165/220/262 Hz), Vibrato nur 0,15 % bei 4,5 Hz, korrekt gerechnet.

## Erzeugen

```bash
python design/proben/audio/skillpunkt/runde3/bauen.py
```

Schreibt die drei WAV-Dateien neben das Skript und druckt die Messwerte (rechnet die Endfassung aus `runde2` dafür neu, ohne sie zu schreiben). Zwei Läufe liefern byte-gleiche Dateien (SHA-256 geprüft); die Dateien in `runde2/` sind unverändert.

## Messwerte (gemessen, ungehört)

Mono, 44 100 Hz, 16 Bit. „Streuung“ = Standardabweichung des Pegels im Ausklang (0,3 bis 1,0 s): RMS in 18-ms-Fenstern, in dB, nach Abzug des gleitenden Mittels über 100 ms (der normale Abfall fällt heraus, übrig bleibt die schnelle Schwankung). 18 ms löschen die Tonhöhen-Welligkeit (110/165/220 Hz) weitgehend aus. „Größte Spanne“ = höchster minus tiefster Wert dieser Schwankung. „Stärkste Schwankung“ = ihre dominante Frequenz.

| Datei | Gesamt (s) | Hauptteil (s) | Nachklang (s) | Spitze (dBFS) | mittlere Lautstärke (dB) | Streuung im Ausklang (dB) | größte Spanne (dB) | stärkste Schwankung (Hz) |
|---|---|---|---|---|---|---|---|---|
| Endfassung (runde2) | 1,40 | 0,49 | 0,89 | −3,0 | −23,9 | 0,90 | 4,66 | 15,0 |
| a-ohne-vibrato.wav | 1,40 | 0,51 | 0,90 | −3,0 | −23,0 | 0,60 | 3,39 | 21,0 |
| b-offener-akkord.wav | 1,40 | 0,51 | 0,88 | −3,0 | −23,4 | 0,29 | 2,10 | 4,5 |
| c-sanftes-vibrato.wav | 1,40 | 0,51 | 0,88 | −3,0 | −23,3 | 0,46 | 3,15 | 29,0 |

Nach den Zahlen ist b die glatteste (Streuung auf ein Drittel), a die ähnlichste zur Endfassung. Ob es für das Ohr glatt klingt, kann nur der Mensch beurteilen. Der Unterschied der Zahlen ist mäßig (Spanne 4,7 → 2,1 dB); gemessen wurde die Hüllkurve, die Frequenzschwankung der Stimmen (13,8 Hz → 0,4 Hz) sagt mehr über die Ursache.
