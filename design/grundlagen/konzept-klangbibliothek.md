# Konzept: PS1-Klänge aus einer Bibliothek echter Aufnahmen

**Stand:** 2026-11-16, Audiodesigner. **Status:** Konzept. Bis der Mensch die Änderung an [ADR 0009](../../docs/entscheidungen/0009-leitung-designer-auto-merge.md) freigibt, wird nichts heruntergeladen. Alle Kandidaten unten sind nur gelesen, nicht geladen.

**Auftrag des Menschen** (nach Hörprobe 3): ein Konzept, mit dem aus einer Sound-Library mit realen Aufnahmen die PS1-Version abgeleitet wird; nur seriöse und kostenfreie Quellen; ohne passende Quelle unter Beachtung der Regeln improvisieren. Dazu sein Hinweis: Die PS1-Fassungen „werden in Zukunft auch kuratiert werden müssen“.

**Bedingungen der Leitung:** Das Repository ist öffentlich, das Spiel kann verteilt werden. Erlaubt sind nur Lizenzen, die das Weitergeben bearbeiteter Dateien in einem öffentlichen Repository und in einem Spiel erlauben: an erster Stelle gemeinfrei/CC0, CC BY nur mit vollständiger Namensnennung. Ausgeschlossen sind „nur nicht-kommerziell“, „keine Weitergabe als Datei/Sammlung“, unklare oder fehlende Lizenzen und alles, wofür man sich mit fremden Zugangsdaten anmelden müsste.

**Wie gelesen wurde:** Lizenz- und Nutzungsseiten habe ich am 2026-11-16 selbst abgerufen (über ein Abruf-Werkzeug, das Seiten liest, nicht lädt). Die Zitate unten sind wörtlich und kurz. Drei Seiten ließen sich nicht abrufen; diese Quellen sind deshalb **nicht** bewertet und gelten als ausgeschlossen, bis jemand die Lizenz selbst gelesen hat.

---

## 1. Quellen

### Zulässig

| Quelle | Lizenz | Entscheidender Satz (wörtlich) | Anmeldung | Verlässlichkeit der Herkunft |
|---|---|---|---|---|
| **Kenney** ([Support](https://kenney.nl/support), z. B. [Impact Sounds](https://kenney.nl/assets/impact-sounds), [RPG Audio](https://kenney.nl/assets/rpg-audio)) | CC0 | „all game assets on the asset pages are public domain licensed (CC0). You're free to use them, even in commercial projects.“ – „Attribution is not required.“ | keine gesehen (Spendenabfrage mit „Continue without donating“) | **hoch:** ein einziger Herausgeber, keine Nutzer-Uploads. Aber: wenig Auswahl (130 bzw. 50 Dateien, Dateinamen nicht auf der Seite gelistet). |
| **Freesound**, nur Sounds mit **CC0** ([FAQ](https://freesound.org/help/faq/), [CC0-Text](https://creativecommons.org/publicdomain/zero/1.0/)) | CC0 je Sound | CC0: „You can copy, modify, distribute and perform the work, even for commercial purposes, all without asking permission.“ | **ja:** „To download a sound, first make sure you are logged into your registered account.“ ([FAQ](https://freesound.org/help/faq/)) | **mittel:** Nutzer-Uploads. Freesound: „We do not review the sounds and metadata posted to the site, but the community does“ ([Nutzungsbedingungen](https://freesound.org/help/tos_web/)). Der Hochladende versichert: „You warrant that you legally can post the sound to Freesound.“ |
| **Freesound**, Sounds mit **CC BY 4.0** ([CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)) | CC BY 4.0 | „You must give appropriate credit, provide a link to the license, and indicate if changes were made.“ | ja (wie oben) | wie oben; zusätzlich Pflicht zur Nennung in `HERKUNFT.md` **und** im Spiel (Abspann). |
| **Wikimedia Commons**, nur Dateien mit CC0 oder CC BY ([Commons:Licensing](https://commons.wikimedia.org/wiki/Commons:Licensing)) | je Datei | „Commercial use of the work _must_ be allowed.“ – „Republication and distribution _must_ be allowed.“ | nicht geprüft | **mittel:** Nutzer-Uploads; Commons: „it is the responsibility of reusers to ensure that the use of the media is according to the license“. Wenig Geräusch-Material. CC BY-SA würde verlangen, dass auch unsere bearbeitete Datei unter BY-SA steht – das ist nicht in den Bedingungen der Leitung vorgesehen und bräuchte eine Entscheidung. |

**Zur Anmeldung bei Freesound:** Die Bedingung schließt *fremde* Zugangsdaten aus. Ein Konto, das der Mensch selbst anlegt und mit dem er selbst lädt, wäre seine Entscheidung. Ich lege kein Konto an und gebe keine Zugangsdaten ein. Ohne Konto bleibt Freesound eine Fundstelle: Ich kann suchen und lesen, aber nicht laden.

### Ausgeschlossen

| Quelle | Grund (wörtlich, mit Link) |
|---|---|
| **Sonniss #GameAudioGDC Bundle** | Weitergabe verboten, auch bearbeitet: „Licensee may not distribute, publish, sub-license or otherwise supply the sound effects as sound effects to any other person.“ – „This applies whether the sound effects are unmodified, modified or re-designed.“ ([Lizenz v2.0](https://sonniss.com/gdc-bundle-license/)). In einem öffentlichen Repository liegt jede Datei für jeden abrufbar – das ist Weitergabe. |
| **Pixabay** (ab 2019) | Nicht CC0 („CC0 Content on the Service is any content which lists a "Published date" prior to January 9, 2019“) und „You cannot sell or distribute the Content … on a Standalone basis.“ ([Nutzungsbedingungen](https://pixabay.com/service/terms/)). Ob eine bearbeitete Geräuschdatei im Repository noch „Standalone“ ist, sagt der Text nur an Bild-Beispielen – also unklar. |
| **Freesound CC BY-NC** | Nur nicht-kommerziell: „you can't earn any money with the piece of work you create!“ ([FAQ](https://freesound.org/help/faq/)) |
| **BBC Sound Effects, OpenGameArt, Zapsplat** | Lizenzseiten ließen sich nicht abrufen (BBC: Abruf verweigert; OpenGameArt und Zapsplat: HTTP 403). Nicht selbst gelesen = nicht zulässig. |

### Risiko klein halten (Nutzer-Uploads)

Ein Upload kann selbst fremdes Material sein. Ich nehme nur Aufnahmen, bei denen **alle** Punkte zutreffen:

1. Die Beschreibung belegt eine **eigene Aufnahme**: Rekorder oder Mikrofon, Ort oder Datum (z. B. „Zoom H4n, Mai 2017“).
2. Keine Kennzeichnung als synthetisch, KI-erzeugt oder „aus einem Spiel/Film“; nichts, was nach einem bekannten Werk klingt (Titel, Tags).
3. Der Hochladende hat mehrere eigene Aufnahmen; keine offensichtliche Weitergabe fremder Sammlungen.
4. Die Lizenz steht **auf der Seite des Sounds selbst** (nicht nur im Suchfilter) und wird beim Abruf mit Datum notiert; dazu Titel, Beschreibung und Link als Text in der Herkunftsakte (Abschnitt 5).
5. Abgeleitete Uploads (Bearbeitung eines anderen Sounds) nur, wenn auch das Original die Bedingungen erfüllt.

Bleibt ein Rest an Risiko, ist er dokumentiert und die Datei austauschbar. Rangfolge: Kenney vor Freesound CC0 vor Freesound CC BY.

---

## 2. Auswahl je Geräusch

**Was im Messbild eine brauchbare Aufnahme zeigt** (gilt für alle):

- ein klarer Einsatz, der sich ohne Überlappung herausschneiden lässt (Einsätze zählen wie in Regel 2);
- Rauschgrund vor dem Einsatz mindestens 40 dB unter der Spitze;
- keine Übersteuerung (keine Folgen von Samples am Vollausschlag), kein Gleichanteil;
- wenig Raum in der Aufnahme (Raum geben wir selbst dazu), außer beim Raumton;
- Bandbreite mindestens bis 11 kHz (die PS1-Kette schneidet ohnehin dort ab; mehr ist nicht nötig, weniger ist dumpf).

Alle Kandidaten unten sind CC0 laut der jeweiligen Seite, sofern nicht anders vermerkt. „Länge nicht geprüft“ heißt: nur aus der Suchliste bekannt, Seite nicht geöffnet – dann stammt auch die Lizenz nur aus dem CC0-Suchfilter und ist noch auf der Seite selbst zu bestätigen.

| Geräusch | Was ich suche | Woran ich sie erkenne | Kandidaten (Link · Urheber · Lizenz · Länge) |
|---|---|---|---|
| **Pistolenschuss** | einzelner Schuss, kurze Waffe, Innenraum oder trocken | ein steiler Einsatz, Körper unter 100 ms, kurzer Raum; kein Echo von Bergen | [Pistol Shot](https://freesound.org/people/Kodack/sounds/253736/) · Kodack · CC0 · 2,0 s, 44,1 kHz/16 Bit (eigene Aufnahme, Rekorder genannt) — [Small pistol gunshot indoors](https://freesound.org/people/acidsnowflake/sounds/402789/) · acidsnowflake · CC0 · Länge nicht geprüft (Innenraum, Aufnehmender nicht genannt) — [Pistol Shot](https://freesound.org/people/LeMudCrab/sounds/163456/) · LeMudCrab · CC0 · Länge nicht geprüft (Aufnehmender nicht genannt) |
| **Klingentreffer** | Stich/Schlag in Fleisch (Foley mit Fleisch, Kohl, Melone) | dumpfer Körper unter 500 Hz plus kurzer heller Anteil; ein Einsatz | [stabs.wav](https://freesound.org/people/nicktermer/sounds/259542/) · nicktermer · CC0 · Länge nicht geprüft („nicht die sauberste“ Aufnahme laut Beschreibung) — [Sword hits the body](https://freesound.org/people/vdovitsky/sounds/411122/) · vdovitsky · CC0 · Länge nicht geprüft (Herstellung unklar → nur Vorbehalt) — Kenney [Impact Sounds](https://kenney.nl/assets/impact-sounds) · Kenney · CC0 (Inhalt erst nach Laden sichtbar) |
| **Klingenschwung** | Luftzug („Swoosh“), kurz | breites Rauschen mit auf- und absteigender Bandmitte, unter 0,5 s | [Clean fast Swoosh](https://freesound.org/people/Danjocross/sounds/507466/) · Danjocross · CC0 · 0,9 s, 48 kHz/24 Bit (eigene Aufnahme mit Kabel) |
| **Klinge ziehen** („Shing“) | Metall an Metall, hell | schmale hohe Linien im Spektrogramm, kurz | [Sword.wav](https://freesound.org/people/ShadowFlayer/sounds/401606/) · ShadowFlayer · CC0 · 3,9 s (eigene Aufnahme; Kommentar: „gedämpft“) — [Unsheathing sword](https://freesound.org/people/mht23SD/sounds/707339/) · mht23SD · CC0 · 0,4 s (Herkunft nicht angegeben → Vorbehalt) |
| **Schritte auf Stein** | einzelne Stiefelschritte, trocken, mehrere in einer Datei | Abstände ≥ 200 ms, klarer Fersen-Einsatz, Rauschgrund niedrig | [Hiking Boot Footsteps on Stone](https://freesound.org/people/Fission9/sounds/521590/) · Fission9 · CC0 · 3,5 s, 48 kHz/24 Bit, 6 Schritte (eigene Aufnahme, H4n Pro) — [Footsteps – Stone, Rock, Concrete](https://freesound.org/people/SecureSubset/sounds/813622/) · SecureSubset · CC0 · Länge nicht geprüft (Foley, MKH416) — [Mountain Boots Rock](https://freesound.org/people/Nox_Sound/sounds/558477/) · Nox_Sound · CC0 · Länge nicht geprüft (Rode NTG4+, mono) — Kenney [RPG Audio](https://kenney.nl/assets/rpg-audio) (Schritte laut Schlagworten) |
| **Ghul-Laut** | menschliche Stimme, tiefes Knurren/Stöhnen, wenig Hall | eine einzige Phrase, Grundton tief, Rauheit sichtbar (Untertöne) | [INFECTED ZOMBIE MAN DYING GROWL](https://freesound.org/people/spootyskater/sounds/612699/) · spootyskater · CC0 · 14 s (eigene Stimme, aber mit Hall) — [zombie-growls](https://freesound.org/people/Xfrtrex/sounds/769200/) · Xfrtrex · CC0 · 31 s, MP3 (eigene Aufnahme; MP3 = schon verlustbehaftet) — **besser:** die Stimme des Menschen selbst (Abschnitt 6) |
| **Tropfen** | einzelne Tropfen in Wasser, Höhle oder Eimer | einzelne Einsätze mit Abstand, kurzer Ton, wenig Rauschen | [Water droplets falling in a cave](https://freesound.org/people/radioscoopfi/sounds/607071/) · radioscoopfi · CC0 · 11 s, 48 kHz/16 Bit, mono (Aufnahme-Angaben fehlen → Vorbehalt) — [Water drops](https://freesound.org/people/northern87/sounds/369989/) · northern87 · CC0 · 13,5 s (Eimer; Angaben fehlen) |
| **Raumton** | Höhle/Keller, ruhig, ohne Stimmen und Verkehr | gleichmäßige Fläche, keine Einzelereignisse, tiefe Energie | [Ambience INT water drip cave grotto](https://freesound.org/people/corkob/sounds/392668/) · corkob · CC0 · 19,7 s, FLAC (eigene Aufnahme, H4n, Mai 2017; enthält Tropfen) — *nicht:* „Cave Ambience“ von pointlessperson1 (als synthetisch eingeordnet). Unsere synthetische Fläche hat dem Menschen gefallen; Raumton ist der Fall, in dem Aufnahmen am wenigsten bringen. |
| **Fledermaus** (später) | Rufe und Flügelschlag | kurze hohe Rufe, Flattern als schnelle Impulsfolge | [Bat calls, Meysset Cave](https://freesound.org/people/Sacha.Julien/sounds/725450/) · Sacha.Julien · CC0 · 7:27, 75 MB (eigene Aufnahme in einer Höhle; lang → nur Ausschnitt verwenden) — [Bat wings](https://freesound.org/people/Gammelsmurfen778/sounds/546823/) · Gammelsmurfen778 · Lizenz/Länge nicht geprüft (Foley mit Blättern) |
| **Gorm mit Kette** (später) | schwere Kette, Rasseln und Schleifen auf Stein | breitbandige, dichte Impulsfolge, metallische Linien | [Chain Rattling – 1](https://freesound.org/people/Roxis_Boy/sounds/401622/) · Roxis_Boy · CC0 · 9,7 s, 48 kHz/24 Bit (eigene Aufnahme, Zoom H6) — [Padlock Chain](https://freesound.org/people/leonelmail/sounds/330635/) · leonelmail · Lizenz/Länge nicht geprüft |
| **Türen** (später) | alte Holztür, Knarren, Öffnen/Schließen | langer tonaler Knarrton, getrennter Schlag am Ende | [Big door creak](https://freesound.org/people/jemburyturner/sounds/273466/) · jemburyturner · CC0 · 6,5 s, 48 kHz/24 Bit (MKH416, Sound Devices) — [Old Wooden Door Open and Close](https://freesound.org/people/MorenaLuz/sounds/814857/) · MorenaLuz · CC0 · 12,2 s (Zoom H5) |
| **Kisten** (später) | Holztruhe öffnen, Kiste zerbrechen | Scharnier-Knarren + Holzschlag; beim Zerbrechen dichte Splitter | [Wooden Chest Open](https://freesound.org/people/The_Frisbee_of_Peace/sounds/573654/) · The_Frisbee_of_Peace · CC0 · 2,3 s, 48 kHz/32 Bit (Zoom H1n) — [Opening Antique Trunk](https://freesound.org/people/OverlookHotelRecords/sounds/271378/) · OverlookHotelRecords · Lizenz/Länge nicht geprüft |

Vor jedem Laden wird die Sound-Seite noch einmal geöffnet und Lizenz, Länge und Beschreibung werden mit Datum festgehalten.

---

## 3. Ableitung: von der Aufnahme zur PS1-Fassung

Ein Skript je Geräusch (`tools/audio/ableiten_<geraeusch>.py` oder wie bisher unter `design/proben/audio/`), das aus der Originaldatei und festen Parametern immer dieselbe Spiel-Datei erzeugt. Schritte:

| Nr. | Schritt | Womit | Geht mit unseren Mitteln? |
|---|---|---|---|
| 1 | **Einlesen und vereinheitlichen:** jede Form (WAV 24/32 Bit, FLAC, MP3, AIFF) nach WAV 44,1 kHz, 16 Bit, mono (Kanäle gemittelt oder der bessere Kanal) | `ffmpeg -ac 1 -ar 44100 -c:a pcm_s16le` | ja |
| 2 | **Prüfen:** Übersteuerung, Gleichanteil, Rauschgrund, Spitze, Einsätze | `ffmpeg -af astats`, eigene Hüllkurve in Python (wie `messen.py`) | ja |
| 3 | **Auf eine Geste schneiden:** Einsätze finden (Regel-2-Messung), den gewählten Einsatz mit 5 ms Vorlauf herausschneiden, Ende dort, wo der Pegel 60 dB unter der Spitze liegt | Python (Einsätze), `ffmpeg -ss/-t` oder Python | ja |
| 4 | **Stille und Rauschen entfernen:** Rauschgrund mindern, Brummen unter 40 Hz weg, Klicks weg | `ffmpeg afftdn` oder `anlmdn` (Rauschen), `highpass=f=40`, `adeclick`; Ränder blenden in Python | ja, mit Grenzen: starkes Rauschen lässt sich nur auf Kosten von Artefakten mindern |
| 5 | **Tonhöhe und Länge:** tiefer und größer per „Varispeed“ (Tonhöhe und Länge zusammen, laut Violet Recording am natürlichsten), oder getrennt | `asetrate=…,aresample=44100` (Varispeed); `rubberband` (Tonhöhe/Länge getrennt, ist in unserem `ffmpeg` enthalten); `atempo` | ja |
| 6 | **Schichten:** echter Anschlag plus synthetischer Körper (oder umgekehrt), auf den ersten Einsatz ausgerichtet | Python (`synth.py`) | ja |
| 7 | **Hall/Raum:** trocken lassen oder kurzen eigenen Raum; Hall in der Aufnahme lässt sich **nicht** entfernen | Python (`synth.reverb`) | Hinzufügen ja, Entfernen nein |
| 8 | **Lautheit je Kategorie:** Spitze und Mittel nach Tabelle (Vorschlag aus den bisherigen Hörproben: Schuss −1,5 dBFS Spitze, Treffer −4, Gegner −3, Schritte −12, Ambiente −38 dBFS Mittel) | Python; für lange Dateien `ffmpeg ebur128` | ja |
| 9 | **Höhenausgleich und PS1-Kette:** Höhen über 3 kHz anheben, dann `ps1_klang.py` mit den Parametern der Kategorie (Rate, ggf. Hall) | `hoerprobe-4/klinge.py: pre_emphasis`, `ps1_klang.process` | ja |
| 10 | **Abschluss:** Ränder auf Stille, Spitze ≤ −1 dBFS, Messwerte, Spektrogramm, Herkunftszeile | wie bisher | ja |

**Was unsere Mittel nicht können:** Hall oder Raum aus einer Aufnahme herausrechnen; einzelne Schallquellen aus einem Gemisch trennen (z. B. Tropfen aus einer Höhlenaufnahme mit Wind); Stimmen umformen, ohne dass es künstlich klingt (Formanten verschieben geht nur grob); starkes Rauschen ohne hörbare Spuren entfernen. Deshalb Rauschgrund und Hall schon bei der Auswahl (Abschnitt 2) streng prüfen.

---

## 4. Kuratieren, ohne zu hören

**Meine Vorauswahl** (alles messbar, je Kandidat in einer Tabelle wie bisher):

1. Lizenz- und Herkunftsprüfung bestanden (Abschnitt 1).
2. Technischer Rahmen: Länge, Spitze, Ränder, keine Übersteuerung.
3. **Eine Geste:** Einsätze zählen (Regel 2); bei Treffern und Schüssen genau 1.
4. **Rauschabstand** vor dem Einsatz ≥ 40 dB.
5. **Klangbalance gegen die Fassungen, die der Mensch gut fand:** Pegel in den Bändern < 300 Hz, 300–2 000 Hz, 2–8 kHz, > 8 kHz im Vergleich zur bestätigten Fassung derselben Kategorie (derzeit die PS1-Fassungen aus Hörprobe 3, bei Tropfen und Klinge die aus Hörprobe 4 nach seinem Urteil). Große Abweichungen schreibe ich dazu, statt sie still zu verwerfen.
6. **Hüllkurve:** Anstiegszeit, Zeit bis −20/−40/−60 dB; Ausklang nicht länger, als Regel 5 erlaubt.
7. **Spektrogramm** ansehen: tonale Linien (Klingeln), Brummen, abgeschnittene Höhen, Störgeräusche.

Nur was alle Punkte besteht, kommt in die Hörprobe – höchstens **drei Kandidaten je Geräusch**.

**Die Hörprobe zum Kuratieren** (wenig Zeit für den Menschen):

- je Geräusch: **die bestätigte Fassung als Bezug**, dann bis zu drei Kandidaten, alle schon als PS1-Fassung, in derselben Lautheit;
- eine Frage je Geräusch: „Welche Nummer (oder keine)? Ein Wort, was stört?“;
- eine Antwortzeile zum Ausfüllen in `hoerprobe.md` (z. B. `Schuss: 2 – etwas zu hell`), damit die Leitung das Urteil wörtlich übernehmen kann;
- Dateinamen mit Nummer und Kurzbeschreibung, keine Quellennamen (damit die Herkunft das Urteil nicht beeinflusst); die Zuordnung steht in `messwerte.md`.

**Vorschlag für das Kuratieren der PS1-Fassungen:** Die PS1-Kette hat wenige Stellschrauben: die Rate (22 050 oder 11 025 Hz), der Höhenausgleich davor (aus / mittel / stark) und der Hall (keiner / „Room“ / „Studio Small“). Ich schlage vor:

1. **Einmal je Kategorie festlegen** (Schüsse, Treffer, Schritte, Gegner, Ambiente): Der Mensch hört dieselbe Datei mit zwei bis drei Einstellungen und wählt. Das Ergebnis wird eine Regel in der Audio Bible („Schritte: 11 025 Hz, Ausgleich mittel, kein Hall“).
2. Danach erzeugt das Skript jede neue Datei mit den Einstellungen ihrer Kategorie. Kuratiert wird nur noch, **ob die Datei passt**, nicht mehr die Einstellung.
3. Neue Dateien sammeln sich zu einer kleinen **Abnahme-Hörprobe** (höchstens zehn Dateien, je eine Zeile „ok / nicht ok + ein Wort“).
4. Die saubere Fassung bleibt neben jeder PS1-Fassung im Skript erzeugbar, damit eine geänderte Einstellung später alles neu rechnen kann.

---

## 5. Herkunft

**Eine Zeile je Spiel-Datei in `assets/HERKUNFT.md`** mit:

| Feld | Beispiel |
|---|---|
| Datei | `assets/audio/pistole.wav` |
| Art | abgeleitet aus Aufnahme / Synthese / gemischt |
| Quelle (Seite) | `https://freesound.org/people/<urheber>/sounds/<nr>/` |
| Titel und Urheber | „Pistol Shot“, <Urheber> |
| Lizenz mit Link | CC0 1.0, `https://creativecommons.org/publicdomain/zero/1.0/` |
| Abrufdatum | 2026-11-… |
| Originaldatei | Dateiname, Größe, **SHA-256** |
| Skript und Parameter | `tools/audio/ableiten_pistole.py`, Schnitt 0,412–0,79 s, Varispeed −3 Halbtöne, … |
| Änderungen | „geschnitten, entrauscht, tiefer, mit synthetischem Körper geschichtet, PS1-Kette“ (bei CC BY Pflicht) |
| Namensnennung | bei CC BY: der vollständige Text, der auch in den Abspann kommt |

Dazu je Quelle eine kleine Textdatei mit Titel, Beschreibung und Lizenzangabe der Seite zum Abrufzeitpunkt (Beleg, falls die Seite sich ändert oder verschwindet).

**Gehören die Originale ins Repository?** Mein Vorschlag: **ja, wenn sie klein sind** (bis etwa 10 MB je Datei), in einem eigenen Ordner außerhalb der Spiel-Assets (z. B. `quellen/audio/`), damit jede Spiel-Datei aus dem Repository allein neu erzeugt werden kann. Freesound verlangt zum Laden ein Konto; ein Skript könnte die Originale also später nicht selbst wieder holen. CC0 und CC BY erlauben das Weitergeben. Lange Dateien (z. B. 75 MB Fledermäuse) nur als Ausschnitt; dann stehen Prüfsumme des Originals **und** des Ausschnitts in der Akte. Git LFS (eine Erweiterung für große Dateien) wäre ein neues Werkzeug und damit eine Entscheidung des Menschen; ich schlage es nicht vor, solange die Größen klein bleiben.

---

## 6. Wenn keine Quelle passt: improvisieren

1. **Synthese wie bisher, verbessert** – die Rezepte aus den Tutorials (Grundlagen, Nachtrag) und die PS1-Kette tragen. Die Hörproben 3 und 4 zeigen, dass das trägt.
2. **Aufnahmen, die der Mensch selbst macht** – das ist für zwei Geräusche sogar die beste Quelle, weil die Herkunft dann zweifelsfrei ist:
   - **Ghul-Laut:** eigene Stimme, tief, kehlig, ohne Worte, je Laut eine Phrase von etwa 0,5 s bis zum „Schlag“; fünf Versuche.
   - **Schritte:** Schuhe auf einem Steinboden (Keller, Treppenhaus), einzelne Schritte mit Pause.
   - Anleitung: Handy oder Rekorder, 20–30 cm Abstand, ruhiger Raum, möglichst wenig Hall (Teppich, Vorhänge), WAV wenn möglich, vor jeder Aufnahme den Namen sagen. Der Mensch gibt die Dateien als eigene Arbeit frei; Eintrag in `HERKUNFT.md` mit „eigene Aufnahme, <Datum>“.
3. **Mischformen:** synthetischer Körper mit einem echten Anschlag aus Kenney (CC0, ohne Anmeldung).

---

## Empfehlung

1. **Erst Kenney** (CC0, keine Anmeldung, ein Herausgeber, sauberste Herkunft) für Anschläge, Treffer, Schritte und Bedienung.
2. **Freesound nur mit CC0** und nur mit nachgewiesener eigener Aufnahme (Prüfliste in Abschnitt 1), für das, was Kenney nicht hat (Schuss, Ketten, Türen, Fledermäuse).
3. **Ghul-Laut aus der eigenen Stimme des Menschen** – die beste Mischung aus Passgenauigkeit und sauberer Herkunft.
4. **Raumton bleibt synthetisch** – die Fläche ist bestätigt; Aufnahmen bringen hier am wenigsten.
5. **Hybride bevorzugen:** echter Anschlag plus synthetischer Körper; das hält jede Datei kurz und kontrollierbar.
6. Je Kategorie die PS1-Einstellung einmal festlegen lassen, danach nur noch kurze Abnahme-Hörproben.

## Was der Mensch entscheiden muss

1. **Die Änderung an ADR 0009** freigeben (echte Aufnahmen aus seriösen, kostenfreien Quellen).
2. **Freesound-Konto:** Legt er selbst ein Konto an und lädt die ausgewählten Dateien selbst (bzw. gibt das Laden mit *seinem* Konto frei) – oder bleiben wir bei Kenney?
3. **CC BY zulassen?** Dann gehört ein Abspann mit Namensnennungen ins Spiel.
4. **Originale ins Repository** (klein, eigener Ordner) – ja oder nein?
5. **Eigene Aufnahmen:** Möchte er die Ghul-Stimme und Schritte selbst aufnehmen?
6. **PS1-Einstellungen je Kategorie** in einer kurzen Hörprobe festlegen.
