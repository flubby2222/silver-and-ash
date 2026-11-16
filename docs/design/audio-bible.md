# Audio Bible – wie Silver & Ash klingt

Dieses Dokument liest der Audiodesigner vor jeder Aufgabe. Es hat zwei Teile: den **gewünschten Klang** und die **gelernten Regeln** aus den Hörproben des Menschen (siehe [ADR 0009](../entscheidungen/0009-leitung-designer-auto-merge.md)).

**Stand:** erste Fassung der Leitung vom 2026-11-16. Sie ist eine Arbeitshypothese: Weder die Leitung noch der Audiodesigner können hören. Was hier über Klang steht, gilt erst, wenn der Mensch es in einer Hörprobe bestätigt hat.

## Leitbild

*Dieses Leitbild ist eine Setzung der Leitung und gilt als Vermutung bis zur ersten Hörprobe.*

Unter der Erde ist es still. Man hört den eigenen Schritt auf Stein, das Tropfen von Wasser, weit weg etwas, das sich bewegt. Geräusche sind **trocken, nah und körperlich**; der Raum antwortet mit kurzem, kaltem Hall. Wenn etwas laut wird, ist es gefährlich.

Passend zum Bild im PS2-Stil darf der Klang **etwas rau und einfach** sein. Er soll nicht nach Filmorchester klingen.

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
| Untote | tief, kehlig, feucht, unregelmäßig | Ghul-Laut |
| Fliegendes | hoch, dünn, schneidend | *(später)* |
| Boss | schwer, tief, Metall auf Stein | *(später)* |
| Beute und Bedienung | klein, hell, kurz | *(später: Kisten, Türen, Aufheben, Menü)* |
| Ambiente | leise Fläche, weit, mit einzelnen fernen Ereignissen | Katakomben |

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
| – | – | – | – | *noch keine* |
