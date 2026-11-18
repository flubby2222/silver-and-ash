# Grundlagen HUD: Wie ähnliche Spiele ihre Anzeige bauen

Recherche des Grafikdesigners vom 2026-11-18, Auftrag der Leitung zu Issue #341 nach dem Urteil des Menschen („Der Hud Designer soll sich bei ähnlichen spielen inspiration holen“). Ausgangspunkt: Kathedrale 1A war die beste Vorlage, wirkte aber „zu minimalistisch“; es soll „homogen wirken und zum Stil passen“.

## Kennzeichnung

- **[Gesehen]** Ich habe ein Spielbild des Spiels selbst angesehen (Bildschirmfoto im Wikipedia-Artikel, im eingebauten Browser betrachtet, nichts heruntergeladen oder abgelegt). Die Bilder sind klein (320 bis 420 px breit), Maße sind daher **Größenordnungen**.
- **[Beleg]** Eine Textquelle trägt die Aussage.
- **[Gedächtnis]** Aus allgemeinem Wissen, nicht nachgeprüft. Trägt keine Entscheidung allein.
- **[Schluss]** Meine Folgerung.
- **[nicht belegt]** Ich habe es nicht klären können.

## Wie belastbar das ist

Das ist schwächer als bei der Grafik-Recherche. Texte beschreiben HUDs fast nie; die Beschreibung steckt im Bild. Die **Doom-Wiki-Seiten (Heretic, Hexen) liefern für automatische Abrufe einen 403-Fehler**, die Store-Bilder von Steam zeigen meist keine Anzeige (die Werbebilder sind ohne HUD aufgenommen). Belastbar angesehen habe ich deshalb fünf Spiele; weitere fünf stehen nur mit dem, was Texte tragen, und sind so gekennzeichnet.

## 1. Spiele, die ich gesehen habe

Auswahl: Quake und Blood als die Retro-Ego-Shooter, auf die sich Dusk, Ion Fury und Amid Evil berufen; Diablo und Castlevania als gotische Gegenstücke mit sehr „satten“ Anzeigen; Bloodborne als Gegenprobe, ein modernes Dark-Fantasy-HUD, das bewusst dünn ist.

| Spiel | Aufbau | Größe im Bild | Leben / Munition | Material, Rahmen, Schrift |
|---|---|---|---|---|
| **Quake** (1996), [Artikel](https://en.wikipedia.org/wiki/Quake_(video_game)) | **Durchgehende Leiste unten** über die ganze Breite, darüber eine zweite schmale Reihe mit den übrigen Munitionsarten. [Gesehen] | Leiste etwa ein Fünftel der Bildhöhe. [Gesehen] | Große Ziffern, daneben je ein kleines Symbol: Rüstung, Leben, Munition. Keine Balken. [Gesehen] | Die Leiste ist aus **demselben braunen Stein und Metall wie die Welt** gemalt, die Ziffern geprägt in warmem Ocker/Rot. [Gesehen] |
| **Blood** (1997), [Artikel](https://en.wikipedia.org/wiki/Blood_(video_game)) | **Zwei Ecken-Gruppen unten**, keine durchgehende Leiste: links Leben und Munition, rechts Schlüssel und kleines Rüstungs-Feld. [Gesehen] | Niedrig, etwa ein Zehntel der Höhe. [Gesehen] | Jeder Wert in einer **eigenen gerahmten Zelle**, große Zahl, darunter ein winziges Wort. Zelle ist farbcodiert (rot Leben, blau Munition). [Gesehen] | Grau-metallische Zellen mit schmalem Rand, Pixelschrift. [Gesehen] |
| **Diablo** (1996), [Artikel](https://en.wikipedia.org/wiki/Diablo_(video_game)) | **Durchgehende Steinleiste** unten, symmetrisch: links Leben-Kugel, rechts Mana-Kugel, Mitte Textfeld und Gürtel, außen Knopfspalten. [Gesehen] | Etwa ein Viertel der Bildhöhe, mittig am breitesten. [Gesehen] | **Behälter statt Balken:** Kugeln, die sich füllen, jede in einer **gemeißelten Fassung** (Figuren neben der Kugel). Zahlen nur bei Bedarf. [Gesehen] | Grauer Stein mit Metallkanten; Schrift in der Mitte in einem schwarzen Kasten mit Messingrand. [Gesehen] |
| **Castlevania: Symphony of the Night** (1997), [Artikel](https://en.wikipedia.org/wiki/Castlevania:_Symphony_of_the_Night) | **Eine kompakte Gruppe oben links**, sonst nichts im Bild. Rundes Medaillon mit Zahl, daran angeschlossen ein gerahmter Doppelbalken. [Gesehen] | Klein, aber **mit auffälligem Metallrahmen**; die Rahmung gibt ihr Gewicht. [Gesehen] | Balken in einer Fassung, Zahl daneben. [Gesehen] | Blaugrauer Zierrahmen, Rundung am Medaillon. [Gesehen] |
| **Bloodborne** (2015), [Artikel](https://en.wikipedia.org/wiki/Bloodborne) | **Dünn und flach**: Balken oben links als Linien, Boss-Balken als schmale Linie unten mit Name darüber. Nichts gerahmt, kein Material. [Gesehen] | Sehr klein, absichtlich unauffällig. [Gesehen] | Linienbalken, keine Zahl bei Leben. [Gesehen] | Weiße feine Schrift, keine Texturen. [Gesehen] |

## 2. Spiele, die ich nur aus Text kenne

- **Heretic / Hexen** (Raven, 1994/95): [Gedächtnis] durchgehende Steinleiste unten mit Verzierung (bei Hexen Wasserspeier-Figuren), Lebenskette statt Gesicht, Inventarfeld in der Mitte. Das Doom-Wiki nennt die Leistengrößen 640 × 125 und 640 × 156 (nur über Suchauszug, Seite selbst gesperrt). Direkt angesehen habe ich sie nicht.
- **King's Field** (1994): [Beleg] Anzeigen für Leben und Magie sowie Kompass immer sichtbar, Waffen und Zauber über Aufladebalken geregelt ([Super Jump](https://superjumpmagazine.com/kings-field-fromsoftware-before-demons-souls), [Hardcore Gaming 101](https://www.hardcoregaming101.net/kings-field/)). Wie sie aussehen: [nicht belegt].
- **Dusk** (2018): [Beleg] Rüstung heißt „Morale“, Gesundheit regeneriert nicht; Optik bewusst Low-Poly-Software-Rendering nach Quake und Blood ([Game Developer](https://www.gamedeveloper.com/design/more-than-a-throwback-how-i-dusk-i-nails-the-best-parts-of-90s-fps-games)). Die Anzeige selbst: [nicht belegt]. Die Store-Bilder auf [Steam](https://store.steampowered.com/app/519860/DUSK/) zeigen keine.
- **Ion Fury** (2019, [Steam](https://store.steampowered.com/app/562860/Ion_Fury/)), **Amid Evil** ([Steam](https://store.steampowered.com/app/673130/AMID_EVIL/)), **Gloomwood** ([Steam](https://store.steampowered.com/app/1150760/Gloomwood/)): Look und Setting belegt (Build-Engine, UE4 mit Retro-Anstrich, viktorianisch-gotisch), HUD **nicht belegt**.

Allgemeine Quellen zum Gestalten, nur Prinzipien, keine Rahmenmuster: [bit-tech, „Thoughts on HUD Design“](https://bit-tech.net/blogs/gaming/pc/thoughts-on-huds/1) (Doom trenne mit der Leiste HUD und Spielwelt; Konventionen nicht blind übernehmen), [Pluralsight](https://www.pluralsight.com/resources/blog/software-development/designing-a-hud-that-works-for-your-game) (HUD soll zum Spielstil passen), [There Is No Sleep](https://thereisnosleep.bearblog.dev/the-best-huds-in-gaming/) (Anzeigen als Teil der Welt; eigene Form je Gegenstand).

## 3. Muster

Was die „satten“ HUDs (Quake, Diablo, Blood; mit Abstrichen Castlevania) gemeinsam haben, **und** was unseren Runden 1 und 2 fehlt:

1. **Ein Körper statt Inseln.** Quake und Diablo sind **eine** zusammenhängende Fläche, in die die Werte eingelassen sind. Unsere Gruppen sind einzelne Platten mit Bild dazwischen, bei denen jede für sich klein ist. Das Auge liest „Aufkleber“, nicht „Rahmen des Bildes“. [Schluss]
2. **Anteil am Bild.** Die satten Leisten nehmen **ein Fünftel bis ein Viertel** der Höhe ein; unsere größte Gruppe (Waffen) etwa ein Neuntel, die meisten weniger. [Gesehen/Schluss]
3. **Der Körper ist das Material der Welt.** Quakes Leiste ist die Wandtextur; Diablos Leiste Stein. Wir haben mit Eisen/Holz/Stein in Runde 2 den ersten Schritt gemacht, aber die Platten sind **dunkler und flacher** als die Welt und tragen keine Fugen. [Gesehen/Schluss]
4. **Ein Hauptdarsteller.** Quake: die großen Ziffern; Diablo: die zwei Kugeln; Blood: die Zahlenzellen. Alles Weitere ordnet sich unter. Bei uns ist **nichts** groß, deshalb wirkt alles gleich wichtig und mager. [Schluss]
5. **Behälter statt Strich.** Leben als füllbares Gefäß in einer Fassung (Diablo) oder als Balken in einer **Nut** (Castlevania), nicht als Strich auf der Platte. Unsere Balken sind dünn. [Gesehen/Schluss]
6. **Ein Zellenmaß.** Blood nutzt überall dieselbe Zelle (Rand, Beschriftung, Zahl), Diablo dieselben Knöpfe. Das macht es homogen. [Gesehen]
7. **Die Gegenprobe:** Bloodborne ist das, was der Mensch ablehnt: dünn, flach, ohne Material. Es passt zu seinem Spiel (Bildmitte füllend, realistische Welt), nicht zu einer PS1-Welt mit Körnung und Nebel. [Schluss]

## 4. Daraus für Silver & Ash

- Die Auflösung ist **854 × 480**; ein Fünftel der Höhe sind ~96 px. Mehr als 60 bis 70 px Leiste ist für die Bildmitte nicht nötig, weil die Waffen und Hände unten ohnehin viel Platz brauchen. [Schluss]
- **Nichts 1:1 übernehmen:** Orbs (Diablo), Wasserspeier (Hexen) und Ziffernprägung (Quake) sind Muster, keine Formen. Eigene Entsprechungen: Kathedralen-Rosette statt Kugel, Pfeiler und Strebewerk statt Figuren, Eisenbänder und Nieten statt Prägung.
- **Schrift:** unverändert ein Problem: Cinzel ist nur eine Windows-Schrift für Vorlagen. Fürs Spiel braucht es eine freie (OFL) oder eine Pixelschrift.
- **Nicht geklärt:** Heretic/Hexen und King's Field nicht angesehen; Dusk, Ion Fury, Amid Evil, Gloomwood nur aus Text. Eine Hörprobe gibt es hier nicht, aber das Urteil über „satt“ bleibt dem Menschen.
- **Zahlen (Runde 5):** Quake und Blood zeigen Leben als große Ziffer. Für Silver & Ash gilt anders: Leben und Ausdauer stehen im HUD nur als Balken, die Zahlen kommen auf die Tab-Seite (#347); Munition, Silber, Stufe und Schlüssel behalten ihre Zahl. Das gilt statt der Zwischenfassung aus Runde 4, die Zahlen am Balken zeigte.
