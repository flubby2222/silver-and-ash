# Audio Bible – wie Silver & Ash klingt

Dieses Dokument liest der Audiodesigner vor jeder Aufgabe. Es hat zwei Teile: den **gewünschten Klang** und die **gelernten Regeln** aus den Hörproben des Menschen (siehe [ADR 0009](../entscheidungen/0009-leitung-designer-auto-merge.md)).

**Stand:** 2026-11-16, nach Hörprobe 1 und der Recherche des Audiodesigners ([Grundlagen Sounddesign](grundlagen-audio.md)). Weder die Leitung noch der Audiodesigner können hören. Was hier über Klang steht, gilt erst, wenn der Mensch es in einer Hörprobe bestätigt hat; bestätigt ist bisher nur, was unter „Gelernte Regeln“ steht. Hörprobe 2 liegt vor, sein Urteil dazu steht noch aus.

## Leitbild

*Dieses Leitbild ist eine Setzung der Leitung und gilt als Vermutung, bis eine Hörprobe es bestätigt.*

Unter der Erde ist es still. Man hört den eigenen Schritt auf Stein, das Tropfen von Wasser, weit weg etwas, das sich bewegt. Geräusche sind **trocken, nah und körperlich**; der Raum antwortet mit kurzem, kaltem Hall. Wenn etwas laut wird, ist es gefährlich.

Passend zum PS1-nahen Bild darf der Klang **etwas rau und einfach** sein. Er soll nicht nach Filmorchester klingen.

**Keine komponierte Musik in 1.0.** Stattdessen ein Ambiente je Ebene und eine dichtere, bedrohlichere Schicht für den Kampf gegen Gorm.

## Wozu Geräusche im Spiel dienen

Klang ist hier zuerst **Information**, dann Stimmung. In dieser Reihenfolge:

1. **Ankündigung:** Jeder gegnerische Angriff, der im Bild eine Ankündigung hat, bekommt auch eine hörbare – und sie sagt nicht nur *dass*, sondern *wann* der Treffer kommt (Lehre aus Retro Woche 10). Das Kreischen der Fledermaus vor dem Sturzflug, das Ausholen des Ghuls, Gorms Kette.
2. **Rückmeldung:** Der Spieler hört, ob sein Schlag oder Schuss getroffen hat, und ob es ein Kopftreffer war.
3. **Orientierung:** Ein Gegner, den man noch nicht sieht, ist zu hören und zu orten.
4. **Stimmung:** Ambiente, Schritte, Türen, Kisten.

Daraus folgt: **Ankündigungen dürfen von nichts überdeckt werden.** Ambiente ist leise und lässt in der Mitte des Hörbereichs Platz.

## Klangfamilien

Jede Familie soll man an ihrem Charakter erkennen, auch mit geschlossenen Augen:

| Familie | Charakter (Hypothese) | Erste Stücke |
|---|---|---|
| Jäger: Fernwaffen | hart, kurz, metallisch, mit kurzem Nachhall im Stein | Pistolenschuss; Rifle und Blunderbuss folgen als Varianten derselben Familie, dazu Nachladen |
| Jäger: Nahkampf | scharf und hell beim Schwung, dumpf beim Treffer | Klingentreffer; Club und Aufladen später |
| Jäger: Körper | trocken, unaufdringlich | Schritte auf Stein; Sprung, Landung, Dodge und Dash später |
| Untote | tief, kehlig, rau – **einfach** (siehe Regel 2 unten) | Ghul-Laut |
| Fliegendes | hoch, dünn, schneidend | *(später)* |
| Boss | schwer, tief, Metall auf Stein | *(später)* |
| Beute und Bedienung | klein, hell, kurz | *(später: Kisten, Türen, Aufheben, Menü)* |
| Ambiente | leise Fläche, weit, mit einzelnen fernen Ereignissen | Katakomben |

## Regeln für jedes Geräusch

Aus dem Urteil des Menschen zu Hörprobe 1 und aus der Recherche. Die Regeln 1 und 2 folgen direkt aus seinem Urteil, die übrigen sind Vorschläge des Audiodesigners, die die Leitung übernommen hat und die eine Hörprobe noch bestätigen muss:

1. **Ein Geräusch ist eine Geste.** Jede Datei ist *ein* hörbares Ereignis aus höchstens drei Schichten (Anschlag, Körper, Ausklang), die zu einem Ereignis verschmelzen. Kein Vorlauf, keine Nachschläge.
2. **Eine Ankündigung ist eine Uhr.** Der Laut vor einem Angriff besteht aus einer gleichförmigen Bewegung (Anschwellen, Tonhöhenfahrt oder schneller werdender Puls), die beim Schlag ihr Ende oder ihren Gipfel erreicht. Höchstens zwei Schichten, keine Pausen oder Stöße mittendrin. Messbar: genau ein Einsatz (eine Stelle, an der der Pegel innerhalb von 50 ms um mehr als 8 dB steigt; der Beginn des Lauts zählt mit, es gibt also keinen zweiten).
3. **Varianten unterscheiden sich im Charakter** einer Geste (Tonlage, Härte, Länge, Raum), nicht durch zusätzliche Ereignisse.
4. **Bedrohung durch Tiefe und Rauheit, nicht durch Menge.** Nur eine Modulation gleichzeitig.
5. **Kurze Ausklänge.** Hall in der Datei nur so lang, wie das Geräusch es braucht.
6. **Ambiente: Mitte frei, messbar.** Die Fläche liegt im Band 300 bis 3 000 Hz mindestens 25 dB unter ihrem Gesamtpegel. Gemessen wird die Fläche **ohne** Einzelereignisse: Solange Tropfen fest in der Schleife liegen, erzeugt das Skript zum Messen zusätzlich eine Fassung ohne sie. Tiefere Tropfen liegen selbst in diesem Band; das ist gewollt, sie sind ein Ereignis und keine Fläche. (Die Recherche nennt an einer Stelle 5 kHz als Obergrenze; es gilt 3 000 Hz.)

Noch nicht entschieden, Vorschlag aus der Recherche: erkennbare Einzelereignisse (Tropfen, Poltern) nicht fest in die Ambiente-Schleife einbauen, sondern vom Spiel einzeln abspielen lassen. Das ist eine Code-Aufgabe und gehört zu v0.15.

## Technischer Rahmen

Vorläufig, bis die erste Hörprobe und die Einbindung zeigen, was trägt:

| Was | Wert |
|---|---|
| Format | WAV (16 Bit) für kurze Geräusche; Ambiente als OGG, aus der WAV-Datei mit `ffmpeg` umgewandelt |
| Abtastrate | 44 100 Hz |
| Kanäle | mono für alles, was im Raum einen Ort hat; Ambiente darf stereo sein. Lautstärke und Richtung berechnet das Spiel aus der Position ([#225](https://github.com/flubby2222/silver-and-ash/issues/225)) – keine Stereo-Effekte in die Datei einbauen |
| Länge | Schuss und Treffer unter 0,6 s, Gegnerlaut unter 1,5 s, Schritt unter 0,3 s, Ambiente 20–40 s als nahtlose Schleife |
| Spitzenpegel | höchstens −1 dBFS, nie übersteuert |
| Anfang und Ende | bei Stille, ohne Knacken |
| Varianten | Schritte und Treffer in mindestens drei Varianten, damit Wiederholung nicht auffällt |

Erzeugt wird **nur per Synthese-Skript**. Keine Aufnahmen, keine heruntergeladenen Dateien, keine KI-Generatoren.

### Erlaubte Werkzeuge

Genau diese zwei, beide sind auf dem Rechner vorhanden:

- **Python 3.12, nur die Standardbibliothek** (`wave`, `math`, `random`, `struct`, `array`) für die Synthese. Kein `pip install`, also auch kein NumPy.
- **`ffmpeg`** für alles Weitere: Umwandeln nach OGG (`-c:a libvorbis`), Spitzenpegel und mittlere Lautstärke (`-af volumedetect`), Spektrogramm als Bild (`-lavfi showspectrumpic`).

Reicht das für ein Geräusch nicht, ist das ein Fall für die Leitung, nicht für ein weiteres Werkzeug. Die CI braucht keines von beiden: Im Repo liegen die erzeugten Dateien und die Skripte.

## Was eine Hörprobe enthält

Pro Geräusch **zwei bis drei deutlich verschiedene Varianten**, dazu `messwerte.md` mit Länge, Spitzenpegel, mittlerer Lautstärke und einem Spektrogramm je Variante (alles mit `ffmpeg`, siehe „Erlaubte Werkzeuge“). Die Leitung prüft die Messwerte gegen den technischen Rahmen und stellt dem Menschen die Probe mit einer kurzen Frage je Geräusch vor („Welche Variante? Was stört?“).

## Gelernte Regeln

Hier trägt die Leitung nach jeder Hörprobe ein, was der Mensch gesagt hat – möglichst in seinen Worten – und was daraus als Regel folgt.

| Nr. | Datum | Geräusch | Urteil des Menschen | Regel |
|---|---|---|---|---|
| 1 | 2026-11-16 | Hörprobe 1 insgesamt | „Die Hörproben klingen ganz interessant.“ | Die Richtung (Synthese per Skript, trocken, kurz) wird weiterverfolgt |
| 2 | 2026-11-16 | Katakomben-Ambiente | „Ambience klingt ganz gut. Das Tropfen könnte etwas lower pitch sein.“ | Ambiente mit Tropfen bleibt die Grundlage; Tropfen tiefer. Wie viel tiefer, entscheidet Hörprobe 2 (Quinte, Oktave, anderthalb Oktaven) |
| 3 | 2026-11-16 | Ghul-Laut | „Der Ghoul gefällt mir nicht. Da ist zu viel los. Das kann ruhig etwas simpler sein.“ | Regeln 1 und 2 oben: eine Geste, ein Einsatz, höchstens zwei Schichten |
| 4 | 2026-11-18 | Skillpunkt-Klang, Runde 1 (Glocke 330 Hz, Aufstieg, Zupfton; alle drei abgelehnt) | „Passt nicht zur Stimmung“ und „Zu lang oder zu träge“ (angekreuzt). Gewünschte Richtungen für Runde 2 (sinngemäß): Turmglocke mit Hall, Metall/Klinge, dumpfer Schlag mit Chor | *Deutung des Audiodesigners:* Meldeklänge sollen düster und schwer statt freundlich und hell sein, kurz mit klarem Einsatz (Hauptteil unter etwa 0,5 s, Nachklang darf leise auslaufen); nichts Helles oder Süßliches. Die Deutung bestätigt erst Runde 2 |
| 5 | 2026-11-18 | Skillpunkt-Klang, Runde 2: Turmglocke, Klinge, Schlag mit Chor | „3. Soll aber noch etwas länger ausklingen“ – gewählt ist **Variante 3, Schlag mit Chor**; Variante 1 (Turmglocke) und 2 (Klinge) abgelehnt, ohne genannten Grund | *Deutung des Audiodesigners:* tiefer Schlag plus kurzer dunkler Stimmen-Akkord trifft die Stimmung; der Ausklang darf länger sein (Nachklang bis etwa 0,9 s statt 0,56 s), Einsatz und Schlag bleiben knapp. Endfassung: `design/proben/audio/skillpunkt/runde2/3-schlag-chor-final.wav`; Einbau ist ein eigenes Issue |
| 6 | 2026-11-18 | Skillpunkt-Klang, Endfassung Schlag mit Chor (länger ausklingend) | „Die neue version ist besser. allerdings höre ich da wie so ein wummern oder eine interferenz heraus. Es soll smooth klingen“; danach: „Ist besonders im neuen teil zu hören“ | *Deutung des Audiodesigners, gemessen:* Chorstimmen dürfen nicht gegeneinander schweben. Ursache war ein Rechenfehler im Vibrato (Frequenz mal Zeit statt aufsummierter Phase: 13,8 Hz statt 0,4 Hz Abweichung nach 1 s); außerdem schweben 110 und 131 Hz im a-Moll-Akkord. Vibrato nur mit aufsummierter Phase, Akkordtöne nicht dicht nebeneinander. Das Urteil zu Runde 3 (`design/proben/audio/skillpunkt/runde3/`) steht aus, nichts davon ist abgenommen |
| 7 | 2026-11-18 | Skillpunkt-Klang, Runde 3 (a ohne Vibrato, b offener Akkord, c sanftes Vibrato) | „a“ (ohne genannten Grund); gewählt ist `design/proben/audio/skillpunkt/runde3/a-ohne-vibrato.wav`, b und c nicht gewählt | *Deutung des Audiodesigners:* Das Wummern kam vom Vibrato-Rechenfehler (siehe Regel 6); ohne Vibrato ist der Ausklang für ihn glatt genug. Regel: kein Vibrato im Ausklang von Chor-Klängen; falls je wieder Vibrato, nur mit aufsummierter Phase |
| – | 2026-11-16 | Pistole, Klinge, Schritte | kein Urteil | Schweigen ist keine Zustimmung; Hörprobe 2 fragt ausdrücklich danach |
