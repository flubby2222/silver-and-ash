# Grundlagen Sounddesign – Recherche des Audiodesigners

**Stand:** 2026-11-16, nach Hörprobe 1. Auftrag der Leitung: Handwerk und Prinzipien des Sounddesigns der PS2-Zeit und für Spielgeräusche allgemein, passend zu unserer Arbeitsweise (Synthese per Skript, nur Python-Standardbibliothek und `ffmpeg`).

**Wie dieses Dokument zu lesen ist**

- **[Beleg]** heißt: Das steht so in der verlinkten Quelle (in meinen Worten zusammengefasst).
- **[Schluss]** heißt: Das ist meine Folgerung daraus oder eigene Erfahrung mit der Synthese – nicht belegt.
- Ich habe die Quellen über eine Websuche gefunden und mich auf deren Inhalte gestützt, wie die Suche sie wiedergab; nicht jede Seite habe ich vollständig gelesen. Viele Quellen zum Handwerk sind Blogs und Praxisseiten, keine Lehrbücher. Wo eine Quelle schwach ist, steht das dabei.
- Nichts wurde heruntergeladen, kein Klang eines bestehenden Spiels wird nachgebaut.

---

## 1. Wie klangen Spiele der PS2-Zeit (etwa 2000–2006) – und warum?

### Technik

- **48 Stimmen, 2 MB Klang-Speicher.** Die PS2 hat zwei Sound-Prozessoren (SPU2) mit zusammen 48 Hardware-Stimmen, doppelt so viele wie die erste PlayStation, und 2 MB eigenen Speicher für Samples. **[Beleg]** [PS2 Developer Wiki: SPU2](https://www.psdevwiki.com/ps2/SPU2), [Mix Online: Sound for PlayStation 2](https://www.mixonline.com/sfp/sound-playstation2-369087), [Wikipedia: PS2 technical specifications](https://en.wikipedia.org/wiki/PlayStation_2_technical_specifications)
- **Kompression ADPCM (Format „VAG“), etwa 3,5:1 bis 4:1.** Aus 16 Bit werden etwa 4 Bit je Sample; das spart Speicher bei sehr geringem Rechenaufwand und ist leicht verlustbehaftet. **[Beleg]** [Mix Online](https://www.mixonline.com/sfp/sound-playstation2-369087), [Valve Developer Wiki: ADPCM](https://developer.valvesoftware.com/wiki/ADPCM)
- **Abtastraten gemischt.** Die Ausgabe läuft mit 48 kHz, aber einzelne Geräusche wurden mit niedrigeren Raten abgelegt, um Speicher und Platz auf der Disc zu sparen; die Hardware rechnet sie beim Abspielen hoch. Empfohlen wurde, die Rate je Geräusch nach Bedarf zu wählen, niedrig für tiefe Geräusche und Sprache. **[Beleg]** [Mix Online](https://www.mixonline.com/sfp/sound-playstation2-369087). Rechenbeispiel: 22 050 Hz mono kosten als ADPCM 88,2 kbit/s statt 352,8 kbit/s. **[Beleg]** [Valve Developer Wiki](https://developer.valvesoftware.com/wiki/ADPCM)
- **Eingebaute Hall-Einheit.** Die SPU2 hat einen Hall in Hardware, der dem der ersten PlayStation fast gleicht: ein einziger umlaufender Speicher, aus dem mit Abständen die Verzögerungen gelesen werden. **[Beleg]** [PCSX2-Dokumentation: Reverb Engine](https://wiki.pcsx2.net/PCSX2_Documentation/Reverb_Engine). Bei der ersten PlayStation lief diese Einheit nur mit halber Abtastrate. **[Beleg]** [psx-spx: Sound Processing Unit](https://forge.chapril.org/ABelliqueux/psx-spx.github.io/src/commit/654d8bc74ce5fc533aa05cb5506389f25ec2dddb/soundprocessingunitspu.md) – ob das bei der PS2 genauso ist, habe ich nicht belegt gefunden.
- **Surround.** Die PS2 konnte Dolby Digital und DTS ausgeben (vor allem für vorgerechnete Ströme wie Zwischensequenzen). **[Beleg]** [Liste von Sound-Chips](https://en.wikipedia-on-ipfs.org/wiki/List_of_sound_chips)
- **Der Mix war dynamisch.** Schon damals wurde die Mischung der Geräusche im Spiel je nach Lage verändert. **[Beleg]** [Mix Online](https://www.mixonline.com/sfp/sound-playstation2-369087)

### Was davon den Eindruck der Ära ausmacht – [Schluss]

- **Wenige, kurze Samples.** 2 MB für alle Geräusche einer Szene zwingen zu kurzen Dateien, wenigen Varianten und einfachen Gesten. Ein Geräusch ist *ein* Ereignis, nicht eine kleine Komposition.
- **Etwas dumpfe Höhen.** Wer mit 22 kHz ablegt, hat nichts über etwa 11 kHz. Dazu kommt ein leichtes Rauschen/Körnen durch ADPCM. Das klingt „näher“ und weniger glatt als heute.
- **Ein gemeinsamer Raum statt eingebauter Räume.** Weil die Konsole einen Hall für alles hatte, waren viele Einzelgeräusche eher trocken, und der Raum kam aus der Hall-Einheit.
- **Stimmenzahl begrenzt.** 48 Stimmen für Musik, Ambiente und Geräusche zusammen bedeuten: Nicht jedes Ereignis bekommt eigene Schichten. Auch das spricht für einfache Geräusche.

---

## 2. Grundregeln für Spielgeräusche

- **Drei Rollen: Einschwingen, Körper, Ausklang.** Ein Geräusch wird oft aus einem kurzen Anschlag (Klick, Knall), einem Körper (Gewicht, Charakter) und einem Ausklang (Raum, Abklingen) gebaut. **[Beleg]** [Splice: Weapon sounds](https://splice.com/blog/?p=16617), [Violet Recording: Weapon sounds](https://violetrecording.com/how-to-design-weapon-sounds/)
- **Fehlt eine Rolle, merkt man es.** Körper ohne Anschlag klingt gedämpft, Anschlag ohne Körper dünn wie ein Spielzeug. **[Beleg]** (Praxisseiten, siehe oben)
- **Schichten müssen zusammenfallen.** Anschlag und Körper zeitlich so legen, dass man *ein* Ereignis hört; schon kleine Verschiebungen verändern die wahrgenommene Größe stark. **[Beleg]** [Lio Ortega: FPS gun sound design](https://www.liortega.com/post/first-person-shooter-gun-sound-design)
- **Ausklang kurz und getrennt halten.** Trockenes Geräusch und Ausklang getrennt zu halten erlaubt, den Raum je Ort zu wählen; kurze, eng begrenzte Tiefen und Ausklänge vermeiden Matsch bei schneller Folge. **[Beleg]** [Splice](https://splice.com/blog/?p=16617), [Violet Recording](https://violetrecording.com/how-to-design-weapon-sounds/)
- **Lesbarkeit im Kampf.** Jedes Geräusch soll sagen: was, wo, wie wichtig. Die lautesten und hellsten Geräusche sind wichtigen Ereignissen vorbehalten. **[Beleg, schwache Quelle]** [Game Audio Principles (Praxis-Sammlung)](https://www.mdskills.ai/skills/game-audio)
- **Lautheit zwischen Kategorien.** Eine Praxis-Tabelle schlägt vor: eigene Geräusche des Spielers −3 bis −6 dB, Gegner −6 bis −9 dB, Ambiente −12 bis −18 dB. **[Beleg, schwache Quelle – nur Startwert]** (ebenda). Für den Gesamtmix gilt bei Konsolen als Richtwert −24 LUFS (±2) und höchstens −1 dBTP Spitze. **[Beleg]** [Audiokinetic: Mastering a game with Wwise](https://audiokinetic.com/en/mastering-a-game-with-wwise-part1), [ITU-R BS.2434](https://www.itu.int/dms_pub/itu-r/opb/rep/R-REP-BS.2434-2018-PDF-E.pdf), ältere Fassung −23 LUFS: [Designing Sound: Interview Garry Taylor](https://designingsound.org/2012/07/30/video-games-and-loudness-standards-interview-with-sonys-garry-taylor/)
- **Frequenzen freihalten.** Häufungen in bestimmten Frequenzbereichen prüfen, bevor gemischt wird; Hall als eigene, getrennte Ebene, damit die Hauptgeräusche klar bleiben. **[Beleg]** [Game Developer: Ambient audio für L.A. Noire / The Banner Saga](https://gamedeveloper.com/audio/creating-ambient-audio-for-la-noire-the-banner-saga)
- **Varianten gegen Wiederholung.** Je mehr dieselbe Probe wiederkehrt, desto eher reißt es aus dem Spiel. **[Beleg]** [The Game Kitchen: Seamless looping ambience](https://thegamekitchen.com/creating-seamless-looping-ambience-tracks/)
- **Ein Geräusch = eine klare Geste. [Schluss]** Die drei Rollen oben beschreiben *ein* Ereignis. Was der Spieler als zwei Ereignisse hört (Klick *und* Knall, Schnitt *und* Aufprall), liest er als zwei Informationen. Zusatzschichten („Sweetener“) nur, wenn sie mit dem Anschlag verschmelzen.

---

## 3. Kreaturen-Laute: bedrohlich und einfach

### Belegt

- **Erst die Aufgabe festlegen.** Enthüllung, Anwesenheit, Reaktion oder Angriffswarnung – und Warnung, Reaktion und Drohung sollen sich unterscheiden. **[Beleg]** [Boom Box Post: Creature vocals](https://boombox-post.squarespace.com/blog/2021/11/9/lunch-and-learn-creature-vocals-concept-and-design)
- **Den Laut als Hüllkurve denken** (Anfang, Mitte, Ende). Aggression: schneller, scharfer Anstieg; Schwäche: langer Abfall, weicher Anstieg. **[Beleg]** [Boom Box Post: Organic creature vocals](https://boombox-post.squarespace.com/blog/2023/2/27/lunch-and-learn-organic-robot-creature-vocal-and-movement-design)
- **Aggression als Regler.** Ein Kreaturenlaut kann mit steigender Aggression lauter und intensiver werden. **[Beleg]** [CRI Ware: Creature vocalizations](https://blog.criware.com/index.php/2023/03/11/creature-vocalizations/)
- **Rauheit macht groß und bedrohlich.** Nichtlineare Stimmphänomene (Subharmonische, Chaos, Seitenbänder) lassen eine Stimme tiefer, rauer, größer und aggressiver wirken; tief und rau signalisiert bei vielen Arten Aggression. **[Beleg, wissenschaftlich]** [Anikin u. a. 2021: Harsh is large](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC8261225/), [Übersicht 2025](https://pubmed.ncbi.nlm.nih.gov/40176513/)
- **Ankündigung (Telegraph).** Eine Warnung nützt nur, wenn sie deutlich wahrnehmbar und einem Angriff eindeutig zuzuordnen ist; der Abstand zwischen Warnung und Treffer muss zum Reagieren reichen, ohne den Kampf träge zu machen. Manche Spiele geben jedem Angriff ein eigenes Signal, andere allen Nahkampfangriffen dasselbe – Letzteres macht Muster leichter lernbar. **[Beleg]** [Game Developer: Enemy attacks and telegraphing](https://gamedeveloper.com/design/enemy-attacks-and-telegraphing), [Critical Points: Audio cues in action games](https://critpoints.net/2017/02/02/audio-cues-in-action-games/), [Prima: Paragons Audio-Telegraphs](https://primagames.com/featured/importance-paragon-audio-telegraphs-epic). Die oft genannte Zahl, auf Klang reagiere man schneller als auf Bild (etwa 170 statt 250 ms), stammt aus einem Forenbeitrag; ich konnte sie nicht prüfen.

### Folgerungen – [Schluss]

- **Ein einfacher bedrohlicher Laut** = eine Stimmquelle (tief, rau durch Subharmonische), *ein* Vokalfilter, *eine* Hüllkurve. Bedrohung entsteht durch Tiefe und Rauheit, nicht durch Menge.
- **Überladen wird ein Laut**, wenn er aus mehreren Ereignissen besteht (Stöße, Lücken, Blasen, Tonhöhensprünge) oder wenn mehrere unabhängige Modulationen gleichzeitig laufen (Gurgeln + Zittern + Blasen + Formantfahrt). Jedes Ereignis zieht Aufmerksamkeit und konkurriert mit dem einen, das zählt.
- **Einen Zeitpunkt ohne viele Ereignisse anzeigen:** Die Hüllkurve selbst ist die Uhr. Möglichkeiten: (a) gleichmäßiges Anschwellen bis zum Schlag, dann Abbruch; (b) eine stetige Tonhöhenfahrt, die beim Schlag ihr Ziel erreicht; (c) ein Puls, der gleichmäßig schneller wird und beim Schlag endet. Wichtig ist, dass der Verlauf *gleichförmig* ist, damit das Ohr ihn hochrechnen kann.

---

## 4. Synthese-Rezepte für die Python-Standardbibliothek

Bausteine, die wir schon haben (`synth.py` aus Hörprobe 1): Rauschen, Sinus mit Frequenzverlauf, Ein-Pol- und Zustandsvariablen-Filter (Tief-, Band-, Hochpass, auch mit wanderender Frequenz), Hüllkurven, einfacher Hall.

- **Schuss.** Physikalisch: Mündungsknall als Druckpuls (Friedlander-Form: steiler Anstieg, kurzer positiver Teil, flacher negativer Teil), bei Überschallgeschoss ein zusätzlicher N-förmiger Knall; mit Entfernung wandert das Spektrum nach unten. **[Beleg]** [Hacıhabiboğlu: Procedural gunshot synthesis](https://rd.springer.com/chapter/10.1007/978-3-319-53088-8_4), [Salomons u. a.: N-wave propagation](https://author.ioa.org.uk/conference-proceeding/internoise-glasgow-21-24-aug-2022-vol44-pt2/e-salomons-f-van-der-eerden-f-van-den-berg-investigation-of-nonlinear-propagation-effects-on-sound-from-supersonic-bullets). Rezept **[Schluss]**: Anschlag = ein geformter Druckpuls (1–2 ms, positiv, dann kleiner negativer Nachschwinger) plus 3 ms Hochpass-Rauschen; Körper = Tiefpass-Rauschen 20–30 ms plus fallender Sinus 100→45 Hz; Ausklang = frühe Reflexionen und kurzer Hall.
- **Aufprall auf Fleisch.** **[Schluss]** Kurzer, gedämpfter Anschlag (wenig Höhen), Körper als Tiefpass-Rauschen unter 400 Hz mit fallendem Sinus, „Nässe“ höchstens als eine kurze Bandrausch-Schicht – nicht mehrere.
- **Aufprall auf Stein / Schritt.** Für Kontaktgeräusche eignet sich Körnige Synthese schlecht, für Texturen gut. **[Beleg]** [Doran, Vortrag über Schritte](https://docs.google.com/presentation/d/1FwQPPxXfKgMNSCOQ2_hqPytZuup8_2-Ro0VS4TRglaE/htmlpresent). Farnell modelliert Schritte nach den Phasen des Gehens. **[Beleg]** [Interview mit Andy Farnell](https://designingsound.org/2012/01/18/procedural-audio-interview-with-andy-farnell/). Rezept **[Schluss]**: harter Klick (Hochpass-Rauschen, 1 ms) + Körper (Tiefpass-Rauschen 800–1500 Hz, 8–12 ms) + leiser tiefer Stoß; Varianten über Filter, Tonhöhe und Pegel um ±10–20 %.
- **Tropfen.** Ein Tropfen klingt durch zwei Quellen: den Aufschlag und eine eingeschlossene Luftblase, die mit ihrer Eigenfrequenz schwingt (Minnaert-Resonanz). Die Frequenz hängt vom Radius ab: Frequenz × Radius ≈ 3,26 m/s. **[Beleg]** [Wikipedia: Minnaert resonance](https://en.wikipedia.org/wiki/Minnaert_resonance), [Preprint 2025 zu Regengeräusch](https://arxiv.org/abs/2512.09336). Folgerung **[Schluss]**: Ein Blasenradius von 1 mm ergibt etwa 3,3 kHz, 2 mm etwa 1,6 kHz, 3 mm etwa 1,1 kHz. Ein tieferer Tropfen ist also physikalisch einfach eine größere Blase – plausibel und kein Widerspruch zur Natur. Rezept: abklingender Sinus mit leicht steigender Tonhöhe.
- **Wind und Raumton.** **[Schluss]** Rauschen durch einen Bandpass mit langsam wandernder Mittenfrequenz und langsamer Lautstärkeschwankung; für Raumton tiefes, zweifach tiefpassgefiltertes Rauschen.
- **Kehlige Stimmlaute.** Quelle-Filter-Modell: Die Stimmlippen liefern einen Impulszug (Quelle), der Rachen formt ihn mit Resonanzen, den Formanten (Filter). Formantsynthese schaltet je Formant einen Resonator zweiter Ordnung; Beispielwerte F1 560, F2 1180, F3 2480 Hz mit Bandbreite etwa 10 % der Frequenz. Formanten können nur formen, was die Quelle an Obertönen enthält. **[Beleg]** [Praat: Source-filter synthesis](https://www.fon.hum.uva.nl/praat/manual/Source-filter_synthesis_2__Filtering_a_source.html), [IIIT Vlab: Formant synthesis](https://ssp-iiith.vlabs.ac.in/exp/formant-synthesis/theory.html), [CCRMA: Formant-filter vowel synthesis](https://ccrma.stanford.edu/~rjc/pubs/audio_speech/Analysis_Synthesis_Pure.html). Rauheit durch Subharmonische (jede zweite Periode schwächer) – siehe Abschnitt 3. **[Beleg]** Alternativ Rauschen durch dieselben Resonanzfilter (Fauchen, Atem). **[Schluss]**
- **Einfacher Hall.** Schroeder: mehrere parallele Kammfilter mit verschiedenen Längen (die Rückkopplung bestimmt die Nachhallzeit, verschiedene Längen mindern metallische Resonanzen), danach Allpässe für Dichte; Allpässe klingen nur farblos, wenn sie kurz sind (unter etwa 10 ms). Ein Tiefpass in der Rückkopplung bildet die Dämpfung der Höhen nach (so macht es Freeverb). **[Beleg]** [J. O. Smith: Schroeder Reverberators](https://www.dsprelated.com/freebooks/pasp/Schroeder_Reverberators.html), [J. O. Smith: Freeverb](https://www.dsprelated.com/freebooks/pasp/Freeverb.html), [Valhalla DSP: Reverb-Grundlagen](https://valhalladsp.com/2021/09/22/getting-started-with-reverb-design-part-2-the-foundations/). Frühe Reflexionen als einzelne verzögerte Kopien. **[Schluss]**

---

## 5. Ambiente für Horror und Gewölbe

- **Aufbau: Fläche + seltene Einzelereignisse.** Eine durchgehende Grundfläche wird geschleift, darüber kommen sporadische Einzelgeräusche. **[Beleg]** [Splice: Soundscape](https://splice.com/blog/audio-soundscape-for-video-games), [The Game Kitchen](https://thegamekitchen.com/creating-seamless-looping-ambience-tracks/)
- **Wie viel darf passieren?** Wenig. Stille darf länger dauern als erwartet; das nächste Geräusch wirkt dann stärker. **[Beleg]** [The Game Kitchen](https://thegamekitchen.com/creating-seamless-looping-ambience-tracks/). Ereignisse sollen zufällig wirken, nicht mechanisch, denn der Spieler bestimmt, wie lange er bleibt. **[Beleg]** [Game Developer](https://gamedeveloper.com/audio/creating-ambient-audio-for-la-noire-the-banner-saga)
- **Wie lang muss eine Schleife sein?** Für die Fläche reichen oft 10–30 s. Aber ein *erkennbares* Ereignis, das in fester Folge wiederkehrt (Beispiel: derselbe Vogelruf alle 15 s), zerstört mit der Zeit die Wirkung. **[Beleg]** [The Game Kitchen](https://thegamekitchen.com/creating-seamless-looping-ambience-tracks/)
- **Wie leise?** Praxiswert für Ambiente −12 bis −18 dB unter den Hauptgeräuschen. **[Beleg, schwache Quelle]** [Game Audio Principles](https://www.mdskills.ai/skills/game-audio)
- **Welche Frequenzen?** Häufungen vermeiden, Hall getrennt halten. **[Beleg]** [Game Developer](https://gamedeveloper.com/audio/creating-ambient-audio-for-la-noire-the-banner-saga). Für uns **[Schluss]**: Fläche unter etwa 300 Hz und über etwa 5 kHz; die Mitte gehört den Ankündigungen (Audio Bible).
- **Folgerung für unsere Tropfen [Schluss]:** In Hörprobe 1 sind die sieben Tropfen fest in die 30-s-Schleife eingebaut. Im Spiel wiederholt sich dieses Muster alle 30 s genau gleich – das ist der „Vogelruf“-Fall. Besser: Die Fläche bleibt die Schleife, Tropfen werden einzelne kurze Dateien, die das Spiel in zufälligen Abständen und an zufälligen Orten abspielt. Die Tonhöhe wählt der Mensch jetzt in Hörprobe 2; die Trennung wäre Sache der Integration. Tiefere Tropfen (1–2 kHz) liegen außerdem in der Mitte des Hörbereichs; einzeln und kurz ist das vermutlich unkritisch, aber messbar (siehe Messwerte Hörprobe 2).

---

## 6. Was unsere Mittel nicht können – und was der Mensch entscheiden müsste

**Grenzen [Schluss]:**

- **Organisches** (Fleisch, Stimmen, Atem) ist mit reiner Synthese am schwersten glaubhaft. Ohne Aufnahmen bleiben Stimmen „synthetisch“; das passt eher zu einem rauen PS2-Stil als zu Realismus.
- **Wir können nicht hören.** Jede Aussage über Wirkung ist Vermutung, bis der Mensch geurteilt hat. Messen können wir Länge, Pegel, Spektrum, Nahtstellen, Zeitpunkte.
- **Kein echter Raum:** kein gemessener Raumimpuls (das wäre eine Aufnahme), nur synthetischer Hall.
- **Rechenzeit:** reines Python ist langsam, für kurze Dateien aber ausreichend (Hörprobe 1: etwa 30 s für alles).
- **Lautheit in LUFS** kann `ffmpeg` messen (Filter `ebur128`); für Dateien unter 0,4 s ist der integrierte Wert aber nicht aussagekräftig. Für Ambiente nutzbar.

**Entscheidungen für den Menschen:**

1. **Klang der Ära:** Sollen Geräusche bewusst „22-kHz-dumpf“ sein (nichts über etwa 11 kHz, leichtes Korn) oder voll aufgelöst? Hörprobe 2 enthält dazu ein einzelnes Vergleichspaar beim Schuss.
2. **Raum in der Datei oder im Spiel?** Bisher steckt der Hall in jeder Datei. Ein gemeinsamer Hall im Spiel (wie die PS2-Hall-Einheit) wäre eine Code-Aufgabe und eine Entscheidung der Leitung.
3. **Tropfen als eigene Einzelgeräusche** statt fest in der Schleife (siehe Abschnitt 5).
4. **Lautheitsverhältnis** zwischen Schuss, Treffer, Gegner, Schritten und Ambiente.

---

## Vorschlag für neue Regeln der Audio Bible

Die Leitung entscheidet, was davon übernommen wird. Regeln 1–2 folgen direkt aus dem Urteil des Menschen zu Hörprobe 1, die übrigen sind Vorschläge aus der Recherche.

1. **Ein Geräusch = eine Geste.** Jede Datei ist *ein* hörbares Ereignis. Aus höchstens drei Schichten (Anschlag, Körper, Ausklang), die zu einem Ereignis verschmelzen. Kein Vorlauf, keine Nachschläge, keine Zusatzereignisse.
2. **Gegner-Ankündigung: eine Hüllkurve als Uhr.** Ein Ankündigungslaut besteht aus einer gleichförmigen Bewegung (Anschwellen, Tonhöhenfahrt oder schneller werdender Puls), die beim Schlag ihr Ende oder ihren Gipfel erreicht. Höchstens zwei Schichten. Keine Pausen, Stöße oder Blasen mittendrin.
3. **Bedrohung durch Tiefe und Rauheit, nicht durch Menge.** Untote: tiefe Grundtonlage, Rauheit über Subharmonische; nur eine Modulation gleichzeitig.
4. **Kurze Ausklänge.** Hall in der Datei höchstens so lang, wie das Geräusch es braucht; kein Ausklang, der in das nächste Ereignis ragt.
5. **Ambiente: Fläche schleifen, Ereignisse einzeln.** Erkennbare Einzelereignisse (Tropfen, Poltern) nicht fest in die Schleife einbauen, sobald das Spiel sie einzeln abspielen kann.
6. **Mitte frei: messbar.** Die Ambiente-Fläche liegt im Band 300–3000 Hz mindestens 25 dB unter ihrem Gesamtpegel (Hörprobe 1: 28–39 dB).

---

## Selbstkritik an Hörprobe 1

**Was der Mensch gesagt hat:** Der Ghul gefällt nicht, „zu viel los, das kann ruhig simpler sein“. Ambiente gut, Tropfen tiefer. Zu Pistole, Klinge und Schritten kein Urteil.

**Warum der Ghul vermutlich überladen war [Schluss]:**

- Alle drei Varianten bestanden aus **mehreren Ereignissen**: (a) Anschwellen *plus* Tonhöhensprung *plus* Rauschstoß beim Schlag *plus* Blasen; (b) Einatmen *plus* Lücke *plus* Schrei *plus* Blasen; (c) vier getrennte Stöße *plus* Blasen.
- Zusätzlich liefen in jeder Stimme **mehrere unabhängige Bewegungen gleichzeitig**: Tonhöhen-Zittern, Lautstärke-Zittern, Subharmonische, Gurgel-Modulation, wandernde Formanten, Hauch-Rauschen. Jede für sich ist ein „Lebendigkeits“-Trick; zusammen ergeben sie Unruhe.
- Ich habe „feucht, unregelmäßig“ aus der Audio Bible wörtlich in viele kleine Zufallsbewegungen übersetzt – und dabei die wichtigere Regel derselben Bibel vernachlässigt: Die Ankündigung ist **Information** und muss lesbar sein. Das ist der Kern des Fehlers.
- Dasselbe Muster steckt in kleinerem Maß in anderen Stücken: Pistole C (Klick vorweg), Klinge B (Schnitt vorweg). Auch dort habe ich Varianten durch *Hinzufügen* unterschieden statt durch einen anderen Charakter derselben einen Geste.

**Was daraus folgt:**

- Varianten unterscheiden sich künftig durch den **Charakter einer Geste** (Tonlage, Härte, Länge, Raum), nicht durch zusätzliche Ereignisse.
- Für den Ghul: genau eine Bewegung, höchstens zwei Schichten, Zufall nur dort, wo er Rauheit erzeugt (Subharmonische), nicht als Bewegung.
- Messbar machen: In Hörprobe 2 zähle ich je Ghul-Variante die Einsätze (Stellen, an denen der Pegel innerhalb von 50 ms um mehr als 8 dB steigt; der Beginn zählt mit). Ergebnis: Hörprobe 1 hatte 2, 2 und 4 Einsätze, die drei neuen Varianten je 1 (siehe [Messwerte Hörprobe 2](../proben/audio/hoerprobe-2/messwerte.md)). Das zeigt nur die Zahl der Ereignisse, nicht, ob es gut klingt.

---

# Nachtrag nach Hörprobe 2 (2026-11-16)

Urteil des Menschen zu Hörprobe 2: „besser“, aber „nicht optimal“; Einzelheiten stehen noch aus. Er hat zwei Wege vorgeschlagen: den Klang des PS1-Chips nachbilden und Tutorials auswerten. Der Bildstil ist inzwischen PS1-nah ([ADR 0010](../../docs/entscheidungen/0010-stil-ps1-nah-texturen.md)).

## A. Der Sound-Chip der ersten PlayStation (SPU)

**Einordnung:** Die SPU hat aufgenommene Samples abgespielt, keine Klänge erzeugt. Was sie zum Klang beiträgt, ist eine **Färbung** – Sounddesign bleibt unsere Arbeit. **[Beleg]** [psx-spx: Sound Processing Unit](https://psx-spx.consoledev.net/soundprocessingunitspu/)

**Belegt** (psx-spx und der Quelltext des Emulators [DuckStation](https://github.com/stenzek/duckstation), `src/core/spu.cpp`):

- **24 Stimmen, 512 KB Klang-Speicher.** [Beleg] (DuckStation; Wikipedia: [PlayStation technical specifications](https://en.wikipedia.org/wiki/PlayStation_technical_specifications))
- **ADPCM in Blöcken:** 16 Byte je Block, darin 28 Samples zu je 4 Bit. Byte 0 enthält Verschiebung (Bits 0–3) und Filter (Bits 4–7), Byte 1 Schleifen-Merker. Fünf feste Vorhersage-Filter (in 1/64): 0/0, 60/0, 115/−52, 98/−55, 122/−60. Verschiebung 0–12; die Werte 13–15 wirken wie 9. Jedes Sample = 4-Bit-Wert (nach oben geschoben und um die Verschiebung zurückgeschoben) plus Vorhersage aus den zwei letzten Samples, auf 16 Bit begrenzt, ohne Rundung. [Beleg] (psx-spx; DuckStation)
- **Abspielrate:** ein Tonhöhen-Register mit 1000h = 44 100 Hz, Höchstwert 4000h; der Mischer läuft immer mit 44,1 kHz. [Beleg] (psx-spx)
- **Gauß-Interpolation:** Jedes Ausgabe-Sample wird aus den vier letzten Samples mit Gewichten aus einer Tabelle mit 512 Einträgen berechnet; Bits 4–11 des Tonhöhen-Zählers wählen die Gewichte. Die Gewichte ergeben zusammen knapp 1 (7F7Fh–7F81h von 8000h). Diese Interpolation glättet und dämpft die Höhen. [Beleg] (psx-spx)
- **Typische Raten:** Ein Leitfaden für heutige PS1-Eigenentwicklungen nennt 11 025 Hz für kurze Effekte und 22 050 Hz für Stimme und Musik; 44 100 Hz braucht doppelt so viel Speicher wie 22 050 Hz. [Beleg, aber Quelle aus der Homebrew-Szene, nicht von damals] [psxsplash: Audio](https://psxsplash.github.io/docs/components/audio)
- **Hall in Hardware:** läuft mit 22 050 Hz in einem Ringspeicher; das Hall-Signal wird mit einem 39-Punkt-Filter herunter- und hochgerechnet. Aufbau: zwei Reflexionen „gleiche Seite“ und zwei „andere Seite“ mit Dämpfung, vier Kammfilter, zwei Allpässe. Voreinstellungen mit allen Registerwerten: Room, Studio Small/Medium/Large, Hall, Half Echo, Space Echo, Chaos Echo, Delay. [Beleg] (psx-spx). Die PS2 hat fast denselben Hall. [Beleg] ([PCSX2-Doku](https://wiki.pcsx2.net/PCSX2_Documentation/Reverb_Engine))
- **Lautstärke-Hüllkurve (ADSR):** Anstieg linear oder „exponentiell“ (in Wahrheit ab 6000h nur langsamer linear), Abfall und Ausklang exponentiell, Haltepegel in 16 Stufen. [Beleg] (psx-spx)

**Warum es „nach PS1“ klingt – [Schluss]:** Kurze Effekte lagen oft mit 11 oder 22 kHz im Speicher, also fehlt alles über 5,5 bzw. 11 kHz. Die Gauß-Interpolation dämpft zusätzlich schon darunter (gemessen bei 22 050 Hz: −1,7 dB bei 4 kHz, −6,9 dB bei 8 kHz). ADPCM fügt ein leises, signalabhängiges Rauschen hinzu (gemessen: 30–50 dB Abstand statt 98 dB bei 16 Bit). Dazu kommt bei vielen Spielen der Hardware-Hall.

**Nachbildung:** [`ps1_klang.py`](../proben/audio/hoerprobe-3/ps1_klang.py). Genau nach Quelle: ADPCM-Dekoder, Tonhöhen-Zähler, Interpolationsformel, Hall-Formeln und -Voreinstellungen. **Annäherungen**, im Skript gekennzeichnet: das Herunterrechnen (heute mit `ffmpeg`; womit damals gerechnet wurde, ist unbekannt), der Kodierer (Sonys Werkzeug ist nicht dokumentiert; meiner probiert Filter und Verschiebung je Block aus), die Gauß-Tabelle (mit einer Näherungsformel aus dem [NESdev-Forum](https://nesdev.nes.science/f12/t10586.xhtml) berechnet; an 18 belegten Stützstellen höchstens 1 von 22 963 daneben) sowie die Reihenfolge von linkem und rechtem Kanal im Hall. Nicht nachgebildet: ADSR (unsere Dateien tragen ihre Hüllkurve schon), Rauschquelle, Ausgangsstufe. Prüfung: [`ps1_pruefung.md`](../proben/audio/hoerprobe-3/ps1_pruefung.md).

## B. Tutorials

**Videos:** Mit dem Werkzeug `watch` habe ich drei Lehrvideos versucht. „Quick Gun Shot Sounds From Scratch“ ([YouTube](https://www.youtube.com/watch?v=wUI59pnJHqo)) hat keine gesprochene Erklärung (die Untertitel enthalten nur Musik). „Distant Gunshot Sound Design“ ([YouTube](https://www.youtube.com/watch?v=YpSmVXro16w)) und „Explosion Sound Design Tutorial“ ([YouTube](https://www.youtube.com/watch?v=qpcuSbM9hb4)) scheiterten auch beim zweiten Versuch an der Drosselung durch YouTube (Fehler 429). Ich bin daher auf Artikel ausgewichen; aus Videos stammt in diesem Nachtrag nichts.

Ausgewertete Quellen mit den übernommenen Rezepten. Die Seiten zu Farnells Buch enthalten Pure-Data-Patches mit wenig Text; die Rezepte habe ich aus der Verschaltung gelesen (**[Beleg]** für die Bausteine, **[Schluss]** für meine Deutung).

1. **Andy Farnell, *Designing Sound* (MIT Press), Begleitseiten:**
   - [Practical 30: Guns](https://mitp-content-server.mit.edu/books/content/sectbyfn/books_pres_0/8375/designing_sound.zip/practical30.html): Knall als Rauschen durch Hochpass, dann Bandpass, mit kurzer Hüllkurve; weiche Sättigung (tanh); Raum über Verzögerungen. Nur teilweise gelesen.
   - [Practical 26: Footsteps](https://mitp-content-server.mit.edu/books/content/sectbyfn/books_pres_0/8375/designing_sound.zip/practical26.html): Ein Schritt ist eine Bodenreaktions-Kurve (Ferse, Abrollen, Ballen) mal einem für den Untergrund typischen gefilterten Rauschen; das Tempo verschiebt Ferse und Ballen.
   - [Practical 29: Mammals](https://mitp-content-server.mit.edu/books/content/sectbyfn/books_pres_0/8375/designing_sound.zip/practical29.html): Stimmquelle als Impulszug mit erhöhter Kosinusform, Rachen als Bandpass-Bank; Vokal-Formanten als Zahlentripel (z. B. „U“ 450/1030/2380 Hz); eine Artikulationskurve mit Anstieg und Abfall steuert alles.
   - [Practical 12: Bubbles](https://mitp-content-server.mit.edu/books/content/sectbyfn/books_pres_0/8375/designing_sound.zip/practical12.html): Tropfen = Sinus mit exponentiell steigender Tonhöhe, etwa 10 ms Einsatz und etwa 80 ms Ausklang; Höhlenhall aus vier Verzögerungen im goldenen Schnitt (61,8 / 100 / 161,8 / 261,8 ms) mit Summen-/Differenz-Mischung.
   - [Practical 31: Explosions](https://mitp-content-server.mit.edu/books/content/sectbyfn/books_pres_0/8375/designing_sound.zip/practical31.html) und [Practical 7: Bouncing Ball](https://mitp-content-server.mit.edu/books/content/sectbyfn/books_pres_0/8375/designing_sound.zip/practical07.html): Aufprall als Rauschen durch einen resonanten Tiefpass mit schnell fallender Grenzfrequenz; Ton mit fallender Tonhöhe.
   - [Practical 18: Wind](https://mitp-content-server.mit.edu/books/content/sectbyfn/books_pres_0/8375/designing_sound.zip/practical18.html): Böen als zweifach mit 0,5 Hz tiefpassgefiltertes Rauschen, das die Grenzfrequenz eines Resonanzfilters steuert. (Noch nicht übernommen: Die Grundfläche hat dem Menschen gefallen und bleibt.)
2. **Piotr Goj, [„Shoot SFX – Procedural Audio“](https://www.gamedeveloper.com/audio/shoot-sfx-procedural-audio)** (Game Developer, 2018): ein Schuss aus einer einzigen Rauschquelle in zwei Bändern (Band um 500–600 Hz, Tiefpass um 200–300 Hz) mit eigener Lautstärke je Band und einem Ausklang-Wert von 90–120; Varianten nur über Filter, Streuung und Ausklang. [Beleg; Einheiten nennt der Artikel nicht]
3. **Bryce Matsumori, [„Springbound Devlog 4: Making critters talk“](https://brycetown.substack.com/p/springbound-devlog-4-making-critters-talk)** (2025): Ein geschlossener Mund ist ein Tiefpass um 280 Hz, Öffnen hebt ihn an; dazu zwei Formant-Filter. Absichtlich nur zwei Vokale und zwei Regler, damit es lesbar bleibt. [Beleg]
4. **Violet Recording, [„How to Design a Creature or Monster Sound“](https://violetrecording.com/how-to-design-a-creature-sound/)**: Schichten nach Aufgabe (tief = Größe, Mitte = Charakter, hoch = Detail); ein Sub-Sinus unter dem Knurren gibt Gewicht; Sättigung parallel beimischen; Schichten stapeln, bis es matschig wird, ist ein typischer Fehler. [Beleg; ohne Zahlen]
5. **Ronny Mraz, [„How to design a weapon sound for video games“](https://splice.com/blog/design-weapon-sound-video-games/)** (Splice, 2021): Schichten Mechanik, Tiefe, Körper, Ferne, Ausklang; Tiefe als eigenes Element; je Abspielen Tonhöhe und Lautstärke leicht streuen; fremde Waffen mit weniger Schichten, damit die eigene Platz hat. [Beleg; ohne Zahlen]

## Was ich deshalb an meinen Skripten ändere

| Geräusch | Änderung gegenüber Hörprobe 2 | Quelle |
|---|---|---|
| Ghul | Eine Steuerkurve „Maul offen“ treibt Lautstärke **und** einen Tiefpass von 280 Hz auf 2,5 kHz – eine Bewegung statt zweier. Formanten aus Farnells Vokaltabelle („U“). Zweite Schicht ist ein Sub-Sinus statt Atem. Leise parallele Sättigung. | Springbound, Farnell 29, Violet |
| Pistole | Körper aus einer Rauschquelle in zwei Bändern (Band 550 Hz, Tiefpass 250 Hz, etwa 120 ms) statt Tiefpass 1,4 kHz; Knall-Rauschen Hochpass → Bandpass; weiche Sättigung des trockenen Schusses. Raum unverändert. | Goj, Farnell 30, Splice |
| Klinge | Resonanter Tiefpass mit fallender Grenzfrequenz (1,8 kHz → 180 Hz in etwa 50 ms) statt festem Tiefpass; drei Varianten mit Streuung (die Audio Bible verlangt drei je Treffer). | Farnell 31 und 7, Splice |
| Schritt | Bodenreaktions-Kurve (Ferse – Abrollen – weicher Ballen ohne Lücke) trägt das Rauschen statt eines einzelnen Abklingens. Bleibt ein Aufsatz (Regel 1). | Farnell 26 |
| Tropfen | Exponentiell steigende Tonhöhe, 2 ms Einsatz, 20 ms Abklingzeit statt 6 ms, Höhlenhall statt zweier fester Echos; tiefste Stufe aus Hörprobe 2. | Farnell 12 |
| alle | Jede Datei zusätzlich durch die PS1-Kette (22 050 Hz, ohne Hall), damit der Mensch die Färbung beurteilt. | Teil A |

**[Schluss] Was die Tutorials an meiner bisherigen Arbeit bestätigen:** Alle Quellen bauen aus wenigen Bausteinen (Rauschen, ein Oszillator, Filter, Hüllkurve) und steuern sie mit **einer** Kurve – genau das verlangen Regel 1 und 2. **Was neu ist:** Bewegung entsteht bei ihnen durch Filter, die sich mit der Hüllkurve bewegen (fallender Tiefpass beim Aufprall, aufgehender Tiefpass beim Maul). Meine Geräusche in Hörprobe 2 hatten meist feste Filter; das dürfte sie statischer gemacht haben.

**[Schluss] Eine Folge, die der Mensch kennen sollte:** Der neue Ghul hat fast keine Energie über 2 kHz (gemessen −85 dBFS im Band 2–8 kHz bei −18 dBFS gesamt). Er ist also sehr dunkel. Durch die PS1-Kette kommt in diesem Band sogar etwas hinzu (−82,5 dBFS): das Rauschen der ADPCM-Kodierung.
