# Grundlagen Grafik: Wie 3D auf der PlayStation 2 gebaut wurde

Recherche des Grafikdesigners vom 2026-11-16, Auftrag der Leitung nach dem Urteil des Menschen über den Ghul („besser als vorher, aber überhaupt nicht das, was ich mir vorgestellt habe“).

## Kennzeichnung

- **[Beleg]** steht hinter Aussagen, die eine der unten genannten Quellen trägt. Die Quelle steht in Klammern. Alles ist in eigenen Worten zusammengefasst.
- **[Schluss]** steht hinter meinen eigenen Folgerungen.
- **[Gedächtnis]** steht hinter wenigen Punkten aus allgemeinem Wissen, die ich nicht nachrecherchiert habe. Sie tragen keine Entscheidung allein.

## Wie belastbar die Quellen sind

Die meisten Zahlen stammen aus Erinnerungen von Praktikern in Foren (Polycount, GameDev.net, Beyond3D) und aus Fan-Wikis. Wirklich belastbar sind drei Arten von Quellen:

- benannte Entwickler im PlayStation-Blog,
- die Hardware-Beschreibungen im PS2-Entwickler-Wiki,
- ein GDC-Vortrag von Electronic Arts (2002) über die Technik von SSX. Ihn konnte ich nur über Suchauszüge lesen, das PDF selbst ist für automatischen Abruf gesperrt.

Polycount-Seiten waren aus demselben Grund nur über Suchauszüge zugänglich. Die Zahlen sind deshalb als **Größenordnungen** zu lesen, nicht als Messwerte. Bilder habe ich keine heruntergeladen.

---

## 1. Die Zahlen der Ära

### Dreiecke

| Was | Größenordnung | Beleg |
|---|---|---|
| **Hauptfigur, typisch** | etwa 3 000 bis 5 000 Polygone, Spitzenwerte um 10 000 | [Beleg] mehrere Forenstimmen, nicht ganz einig ([GameDev.net](https://www.gamedev.net/forums/topic/438909-character-polygon-limits/?page=1), [Polycount](https://polycount.com/discussion/comment/873059)) |
| Hauptfigur eines späten Actionspiels | 5 700 Polygone für den ganzen Körper, davon 1 200 allein im Gesicht, fünf Texturen | [Beleg] benannter Art Director von Santa Monica Studio über eine PS2-Hauptfigur von 2007 ([PlayStation Blog](https://blog.playstation.com/2019/12/16/the-polygonal-evolution-of-5-iconic-playstation-characters/)) |
| Hauptfigur eines Jump’n’Run von 2001 | etwa 5 400 Flächen | [Beleg] Polycount-Forum, Suchauszug ([Polycount](https://polycount.com/discussion/comment/2045771)) |
| Figuren eines Kampfspiels von 2003 (nur zwei Figuren gleichzeitig im Bild) | etwa 8 500 Dreiecke je Figur, nach Meinung des Autors etwa doppelt so viel wie üblich | [Beleg] Auswertung ausgelesener Spieldaten durch einen Blogger, kein Entwickler ([Game Art Research](https://gameartresearch.blogspot.com/2012/12)) |
| **Normale Gegner** | keine belastbare Zahl gefunden. Die Faustregel lautet nur: Was in Gruppen auftritt, bekommt weniger Polygone | [Beleg] für die Faustregel ([Polycount](https://polycount.com/discussion/comment/873059)). Meine Schätzung: 1 000 bis 3 000 Dreiecke [Schluss] |
| **Bosse** | keine Zahl gefunden. Ein großes Monster eines PS2-Jagdspiels steht im PlayStation-Blog mit 1 390 Polygonen in der Spalte „Gesicht“ | [Beleg] für die Zahl ([PlayStation Blog](https://blog.playstation.com/2019/12/16/the-polygonal-evolution-of-5-iconic-playstation-characters/)). Meine Schätzung für ganze Bosse: 5 000 bis 10 000 [Schluss] |
| **Requisiten** | keine Zahl gefunden. Ich schätze einige Hundert bis gut 1 000 | [Schluss] |
| **Ganze Szene** | Spiele bis 2003 meist 2 bis 5 Millionen Polygone pro Sekunde, im Schnitt etwa 52 000 je Bild, Spitzen bis 145 000 je Bild. Ein späteres Action-Horror-Spiel lag laut Forum bei 30 000 bis 50 000 je Bild und 30 Bildern pro Sekunde | [Beleg] Sega-Retro-Wiki über Suchauszug, die Seite selbst ist gesperrt; Forum ([Beyond3D](https://forum.beyond3d.com/threads/yes-but-how-many-polygons-an-artist-blog-entry-with-interesting-numbers.39321/page-3), [videogameforums](https://videogameforums.com/t/re4-ps2-within-dreamcast-reach.3899/latest)) |
| Zum Vergleich: **PS1-Figur** | 250 bis 750 Dreiecke | [Beleg] Polycount-FAQ über Suchauszug ([Polycount](https://polycount.com/discussion/226167/)) |

### Texturen, Farbtiefe, Speicher

- **Speicher:** Die PS2 hatte 4 MB Grafikspeicher für Bildpuffer, Tiefenpuffer *und* Texturen zusammen. Texturen mussten oft pro Bild nachgeladen werden. [Beleg] ([PS2 Linux Programming](https://ps2-edu.tensioncore.com/texmap1/texmap1.html), [PS2 Dev Wiki](https://playstationdev.wiki/ps2devwiki/Graphics_Synthesizer))
- **Texturgrößen:** 128 × 128 war üblich, 256 × 256 das normale Maximum, 512 × 512 sehr selten. [Beleg] Polycount-FAQ ([Polycount](https://polycount.com/discussion/226167/))
- **Farbtiefe:** Texturen mit Farbpalette waren der Normalfall. Ein Pixel speichert dann nur eine Nummer in einer Farbtabelle:
  - 4 Bit = 16 Farben, für kleine Teile und Unwichtiges,
  - 8 Bit = 256 Farben, für größere Flächen.

  Echte Farbe pro Pixel war die Ausnahme. [Beleg] ([Polycount](https://polycount.com/discussion/comment/704016), [PS2 Linux Programming](https://ps2-edu.tensioncore.com/old/palt/palt.html))
- **Texturen je Figur:**
  - Die genannte Hauptfigur von 2007 hatte fünf Texturen. [Beleg] ([PlayStation Blog](https://blog.playstation.com/2019/12/16/the-polygonal-evolution-of-5-iconic-playstation-characters/))
  - Die Kampfspiel-Figuren hatten etwa zehn kleine Texturen für einzelne Teile. [Beleg] ([Game Art Research](https://gameartresearch.blogspot.com/2012/12))
  - Ein Praktiker beschreibt das übliche Muster: 256 × 256 für das Gesicht, 128 × 128 für Stiefel, Hände und Taschen. [Beleg] ([Polycount](https://polycount.com/discussion/comment/703863))

### Bildauflösung

- **Ausgabe:** meist 640 × 448 oder 512 × 448 im Zeilensprung (interlaced). Viele Spiele rechneten nur Halbbilder mit 640 × 224, um Speicher und Zeit zu sparen. Der Röhrenfernseher mischte die Halbbilder zu einem Bild. [Beleg] ([libretro](https://www.libretro.com/?p=50686), [PCSX2-Forum](https://forums.pcsx2.net/archive/index.php/thread-68073.html))
- **Treppenkanten:** Frühe PS2-Spiele wurden für ihre Treppenkanten ohne Kantenglättung kritisiert. [Beleg] ([libretro](https://www.libretro.com/?p=50686))

### Wie weit liegen wir davon weg?

- **Dreiecke:** Unsere 400 Dreiecke für einen normalen Gegner liegen **am oberen Rand der PS1** (250–750), nicht bei der PS2. Ein normaler PS2-Gegner hatte vermutlich das Drei- bis Achtfache. [Schluss]
- **Keine Texturen:** „Keine Texturen“ ist **nicht einmal PS1**, denn PS1-Spiele waren texturiert. Flächige, untexturierte Figuren mit einer Farbe pro Fläche kenne ich eher von Arcade-Spielen Anfang der Neunziger. [Gedächtnis]
- **Unser Look:** Unsere Kombination – wenige Dreiecke, Flat Shading, keine Texturen, hart hochskalierte 854 × 480 – ist damit **eher „früh-3D“ oder heutiges „PS1-Indie“ als PS2**. [Schluss]

---

## 2. Woraus der PS2-Look entsteht

| Mittel | Was die Ära tat | Kennzeichnung |
|---|---|---|
| **Texturen gegenüber Geometrie** | Fast alles Material steckte in kleinen, gemalten Texturen: Stoff, Haut, Stein, Rost, Falten, Nähte, Schmutz. Die Geometrie gab den Umriss, die Textur die Oberfläche. Texturen waren gemalt, aus Fotos gebaut oder beides | [Beleg] ([Polycount](https://polycount.com/discussion/comment/2753204)) |
| **Eingebackenes Licht** | Licht wurde oft fest in Texturen oder Vertex-Farben gerechnet. Ein benannter Capcom-Entwickler sagt über ein PS2-Spiel, dynamisches Licht sei schwer gewesen; die Lichtfarben seien deshalb in die Textur gemalt worden. Praktiker beschreiben eingebackenes Vertex-Licht als sehr verbreitet und als Grundlage, auf die Texturen gemalt wurden | [Beleg] ([PlayStation Blog](https://blog.playstation.com/2019/12/16/the-polygonal-evolution-of-5-iconic-playstation-characters/), [Polycount](https://polycount.com/discussion/comment/2798550)) |
| **Gouraud gegenüber Flat** | Die PS2-Grafikeinheit konnte beides, für jedes gezeichnete Dreieck einstellbar. Gouraud-Schattierung mischt die Helligkeit weich zwischen den Ecken | [Beleg] ([PS2 Dev Wiki](https://psdevwiki.com/ps2/Graphics_Synthesizer), [GameDev.net-Tutorial](https://gamedev.net/tutorials/programming/general-and-gameplay-programming/playstation-2-game-programming-part-2-drawing-r2027)). Dass Figuren fast immer weich (Gouraud) schattiert wurden, harte Kanten nur gewollt an Metall und Stein, halte ich für sicher, habe aber keine Quelle gefunden, die das ausdrücklich sagt [Schluss] |
| **Nebel und Sichtweite** | Nebel war in der Hardware eingebaut und wurde breit eingesetzt: um die begrenzte Sichtweite zu verdecken, aber auch als Stimmung. Bei einem bekannten Horror-Spiel von 2001 gilt er als Teil der Erzählung | [Beleg] ([PS2 Dev Wiki](https://psdevwiki.com/ps2/Graphics_Synthesizer), [GamesRadar](https://gamesradar.com/why-silent-hill-2-so-high-our-new-top-100-because-its-perfect-game), [Superjump](https://www.superjumpmagazine.com/drafting-through-the-mists/)) |
| **Farbpaletten** | Durch die Paletten-Texturen hatte jede Textur höchstens 16 oder 256 Farben. Das gibt leichte Stufen in Verläufen und eine begrenzte, abgestimmte Farbwelt je Fläche | [Beleg] für die Paletten. Die Wirkung ist meine Folgerung [Schluss] |
| **Texturfilter** | Anders als die PS1 filterte die PS2 Texturen (bilinear, mit Mipmaps). Texturen wirken dadurch eher weich als pixelig | [Beleg] ([PS2 Dev Wiki](https://psdevwiki.com/ps2/Graphics_Synthesizer), [Scribd, PS2-Effekte](https://www.scribd.com/document/253570275/Special-Effects)) |
| **PS1-Artefakte fehlen** | Das Wackeln der Ecken und das Verzerren der Texturen sind PS1-Merkmale (fehlende Perspektivkorrektur, gerundete Eckpunkte). Die PS2 hatte sie nicht | [Beleg] für die PS1 ([Pikuma](https://pikuma.com/blog/how-to-make-ps1-graphics), [Hackaday](https://hackaday.com/2026/02/06/why-playstation-graphics-wobble-flicker-and-twitch)). Für die PS2 [Gedächtnis] |
| **Kantenglättung** | meist keine; Treppenkanten, auf dem Röhrenfernseher weich gezeichnet | [Beleg] ([libretro](https://www.libretro.com/?p=50686)) |
| **Farbtiefe des Bilds** | Teils 16-Bit-Bildpuffer mit Dithering, meist aber 32 Bit. Die Quellen sind sich nicht einig | [Beleg], widersprüchlich ([shmups-Forum](https://shmups.system11.org/viewtopic.php?p=1547286), [Emulation Wiki](https://emulation.gametechwiki.com/index.php/Dithering)) |

**Was ist für den Eindruck „PS2“ unverzichtbar?** [Schluss]

1. **Kleine, gemalte Texturen mit eingebackenem Licht und Schmutz.** Sie sind der größte Unterschied zu uns. Ohne sie sieht jede Fläche nach Spielzeug oder Bauklotz aus, egal wie viele Dreiecke da sind.
2. **Weiche Schattierung (Gouraud) auf allem Organischen.** Flat Shading macht aus Haut Facetten, und Facetten lesen sich als PS1 oder Retro-Indie.
3. **Genug Dreiecke für einen runden Umriss:** Schultern, Schädel, Gelenke. Etwa 1 000 bis 3 000 für einen Gegner.
4. **Nebel und dunkle, begrenzte Sicht.** Das haben wir schon.
5. **Keine Kantenglättung, niedrige Auflösung.** Das haben wir, aber nur als Ergänzung. Hart hochskalierte, sichtbare Pixel sind heute ein Kennzeichen von „PS1-Stil“, auf dem Röhrenfernseher wirkte PS2 weicher.

---

## 3. Figurenbau

- **Proportionen:**
  - Die Hauptfigur von 2007 steckte rund ein Fünftel ihrer Polygone ins Gesicht (1 200 von 5 700). Das zeigt, wohin das Budget ging: Gesicht und Kopf. [Beleg] ([PlayStation Blog](https://blog.playstation.com/2019/12/16/the-polygonal-evolution-of-5-iconic-playstation-characters/))
  - Ein Kanon für Proportionen ließ sich nicht belegen. In der Ära waren heroisch überzeichnete Körper üblich: breite Schultern, große Hände und Waffen, kleiner Kopf im Verhältnis. Das Ziel war die Lesbarkeit aus der Kamerahöhe. [Schluss]
- **Gesichter:**
  - Bei einem Kampfspiel von 2003 wurde der Mund innen grob modelliert. Die Kanten liefen in Ringen um Augen und Mund, wie man es für Mimik tut. [Beleg] ([Game Art Research](https://gameartresearch.blogspot.com/2012/12))
  - Praktiker beschreiben Kantenringe um Augen und Mund als Regel. [Beleg] ([Shoot First](https://www.shootfirst.art/toplogy))
  - Die eigentlichen Augen, Brauen und Lippen standen meist in der Textur. Die Geometrie gab nur Augenhöhle, Nase, Kiefer und Wangenknochen. [Schluss]
- **Hände:** Hände bekamen eine kleine eigene Textur, kleiner als das Gesicht. Finger kosten viele Dreiecke an den Gelenken; darauf weisen Praktiker ausdrücklich hin. [Beleg] ([Polycount](https://polycount.com/discussion/comment/2728413), Beispiel 64 × 64 für Hände bei einem Retro-Paket auf [itch.io](https://starkcrafts.itch.io/psx-japanese-girl)). Für Gegner auf Distanz genügen Fäustlinge oder drei Krallen, wenn die Textur Finger andeutet. [Schluss]
- **Kleidung:**
  - Kleidung saß fest am Körper, Falten standen in der Textur.
  - Lose Teile wie Umhang, Fetzen und Haare waren eigene Flächen. Bei einer Kampfspiel-Figur sind Haare als Geometrie gebaut, bei einer anderen mit durchsichtigen Texturen (Alpha). [Beleg] ([Game Art Research](https://gameartresearch.blogspot.com/2012/12))
  - Durchsichtige Fetzen-Texturen sind das typische Mittel für zerrissene Lumpen. [Schluss]
- **Wo Dreiecke hingehören:**
  - Wo sich etwas bewegt (Schultern, Ellbogen, Knie, Hüfte) und wo Ausdruck sitzt (Augen, Mund), gehören Kantenringe hin. [Beleg] ([Altava-Leitfaden](https://docs.altava.com/creator-guide/3d-modeling/low-polygons), [Shoot First](https://www.shootfirst.art/toplogy))
  - Dazu kommt der **Umriss**, denn eine Kante, die man im Profil sieht, ist mehr wert als eine Fläche in der Mitte. [Schluss]
  - **Nicht** hin gehört: Rückseiten flacher Flächen, verdeckte Innenseiten, Details unter der Größe eines Texturpixels.
- **Topologie für Verformung:**
  - Etwa drei Kantenringe um jedes Gelenk. [Beleg] ([Polycount](https://polycount.com/discussion/comment/2076031))
  - Bei sehr wenigen Polygonen helfen schräg gestellte Gelenkringe. Innen am Knie und Ellbogen sollte keine Mittelkante sitzen, damit die Fläche sauber einknickt. [Beleg] ([Polycount](https://polycount.com/discussion/comment/1635141), [Reddit/ps1graphics](https://redlib.hackliberty.org/r/ps1graphics/comments/1lj0af0/does_anyone_have_any_proper_psx_style_character))

---

## 4. Monster und Untote: bedrohlich statt niedlich

- **Kindchenschema:**
  - Großer Kopf, runde Formen, große Augen, kleiner Mund und kurze, dicke Gliedmaßen werden als niedlich wahrgenommen. Konrad Lorenz hat das 1943 beschrieben; Glocker und Kollegen haben es 2009 experimentell bestätigt. [Beleg] ([PMC 3105163](https://pmc.ncbi.nlm.nih.gov/articles/PMC3105163), [PMC 3260535](https://pmc.ncbi.nlm.nih.gov/articles/PMC3260535))
  - Ein bedrohliches Monster muss **gegen** diese Merkmale gebaut werden. [Schluss]
- **Formsprache:**
  - Spitze, kantige, nach vorn ragende Formen und schmale, fokussierte Augen wirken bedrohlich. Runde, weiche Formen und große, weit auseinanderstehende Augen wirken harmlos (Beuteschema). [Beleg] Praktiker-Leitfäden, keine Studien ([Prolific](https://prolificstudio.co/blog/shape-language-in-character-design/), [Creature-Design-Leitfaden](https://skillselion.com/skills/omer-metin/skills-for-antigravity/creature-design))
  - Größe gegenüber dem Spieler gilt ebenfalls als Bedrohung. Gestalten, deren Proportionen bewusst „falsch“ wirken, wirken unheimlich. [Beleg] dieselben Quellen und eine studentische Recherche ([Boonacker](https://sites.google.com/view/graduationrobinboonacker/research/games))
- **Lesbarkeit im Dunkeln:**
  - Gegner erkennt man an Umriss und Bewegung, oft nur kurz und bei schlechtem Licht.
  - Gegner mit vielen Einzelheiten verschwinden vor unruhigem Hintergrund.
  - Nebel und gezieltes Gegenlicht trennen Umrisse vom Hintergrund.
  - Leuchtende Augen verraten die Position, ohne die Figur zu zeigen.

  [Beleg] ([Level Design Book](https://https-book_leveldesignbook_com.proxy.bugsmash.io/process/lighting/darkness), [GameTyrant](https://gametyrant.com/news/why-great-video-game-monsters-are-recognizable-at-a-glance), Spielerstimmen auf [itch.io](https://itch.io/post/10093049))
- **Typische Mittel für Untote** [Schluss]:
  - **Asymmetrie:** ein längerer Arm, ein hängender Kiefer, eine verdrehte Schulter.
  - **Haltung:** Der Kopf hängt *tiefer* als die Schultern und ist nach vorn gestreckt; die Wirbelsäule ist gekrümmt.
  - **Proportionen:** Ein **kleiner** Kopf im Verhältnis zum Körper gibt einen ausgemergelten Hals und verlängerte Gliedmaßen mit sichtbaren Gelenken.
  - **Kontrast am Kopf:** Die Augen liegen tief in dunklen Höhlen, statt auf der Fläche zu sitzen.
  - **Mund:** offen, schief, mit wenigen sichtbaren Zähnen.
  - **Oberfläche:** Material gibt die Textur – gespannte Haut, Flecken, Adern, Fetzen.

---

## 5. Umgebung im Gothic-Stil

- **Bauteile:**
  - Kennzeichen der Gotik sind Spitzbogen, Rippengewölbe, Strebebögen, Maßwerk (steinerne Fensterteilungen) und Bündelpfeiler. [Beleg] ([Palomar College](https://pressbooks.palomar.edu/arthc1100/chapter/gothic-art-and-architecture/), [My Modern Met](https://mymodernmet.com/gothic-architecture-characteristics))
  - Spiele bauen solche Welten aus einem **Baukasten** wiederkehrender Teile, die auf ein Raster passen: Wandstück, Pfeiler, Bogen, Gewölbefeld, Treppe, Tür. [Beleg] für heutige Baukästen ([Unity Asset Store](https://assetstore.unity.com/packages/3d/environments/dungeons/multistory-dungeons-33955)). Für die PS2 ist das meine Folgerung [Schluss]
- **Licht und Dunkel:**
  - Szenen sollen dunkel *wirken*, ohne unlesbar dunkel zu *sein*. [Beleg] ([Level Design Book](https://https-book_leveldesignbook_com.proxy.bugsmash.io/process/lighting/darkness))
  - In der Ära wurde Raumlicht vorab berechnet und in Vertex-Farben oder Texturen gespeichert, also helle Kegel um Fackeln und dunkle Ecken. [Beleg] allgemein ([Autodesk Maya Prelight](https://help.autodesk.com/cloudhelp/2017/ENU/Maya/files/GUID-2EC022C8-B33C-4CCD-BBBE-73FE1FDE6E51.htm), [Battlefront-II-Doku](https://sites.google.com/site/swbf2modtoolsdocumentation/bf2_lighting)). Belege speziell für PS2-Level fehlen
- **Farbstimmung** [Schluss]:
  - Kalte, entsättigte Grundtöne für Stein; warme Inseln für Licht; Farbe als Signal. Das deckt sich mit unserer Art Bible.
- **Stein, Holz und Metall unterscheiden:**
  - In der Ära lief das fast ganz über Texturen: Fugen und Körnung für Stein, Maserung für Holz, Kratzer und Glanzstreifen für Metall. [Schluss]
  - Ohne Texturen bleiben nur drei Mittel. [Schluss]
    - **Farbton:** Stein kalt-grau, Holz warm-braun, Metall bläulich oder messingfarben.
    - **Form:** Stein in großen Blöcken mit Fase, Holz in Brettern mit Fugen als eigene Flächen, Metall in dünnen Leisten mit Nieten.
    - **Schattierung:** Metall mit harten Kanten und hellem Streifen, Stein matt.

---

## 6. Animation mit wenig oder ohne Skelett

- **PS2:**
  - Skelett-Animation mit gewichteten Eckpunkten war Standard. Ein EA-Vortrag von 2002 beschreibt bis zu drei Knochen je Eckpunkt, grob gestufte Gewichte und Budgets von rund 75 bis 250 Knochen-Matrizen je Detailstufe. [Beleg] Suchauszug aus dem gesperrten PDF ([GDC 2002, SSX](https://computernewb.com/~lily/files/SSX/talks/gdc2002/GDC_Visual_Engineering_SSX.pdf))
- **PS1 und Kampfspiele davor:**
  - Oft **zerlegte, starre Figuren**: Jedes Glied ist ein eigenes Stück, das an seinem Knochen hängt (Oberarm, Unterarm, Hand getrennt), ohne Verformung. Das war eine Leistungsgrenze, kein Stilwunsch.
  - Einige Spiele animierten stattdessen jeden Eckpunkt einzeln (Vertex-Animation).

  [Beleg] Praktiker auf Polycount über Suchauszug ([Polycount-Thread](https://polycount.com/discussion/182247/how-did-artists-rig-animate-ps1-era-characters), [Polycount](https://polycount.com/discussion/comment/2795051))
- **Was mit starren, per Code gedrehten Teilen geht** [Schluss]:
  - Mit **genug Teilen** – Kopf, Kiefer, Rumpf oben und unten, je Arm Ober- und Unterarm und Hand, je Bein Ober- und Unterschenkel, also etwa 14 Teile – lassen sich Kriechen, Aufrichten, Schlagen und Zusammenbrechen glaubhaft zeigen.
  - Die Fugen an den Gelenken sieht man. Man versteckt sie mit Überlappung, Gelenkkappen oder Fetzen. So sah PS1 aus, nicht PS2.
  - Der Ghul hat heute fünf Teile; mit Unterarmen und Händen wären es neun.
  - Für echtes PS2-Gefühl (weiche Schultern, gebeugte Ellbogen ohne Knick) braucht es Verformung, also ein Skelett.

---

## 7. Was geht mit unseren Mitteln, und was muss der Mensch entscheiden?

| Thema | Geht technisch? | Aufwand | Entscheidung nötig? |
|---|---|---|---|
| **Mehr Dreiecke** (1 000–3 000 je Gegner) | ja; Raylib zeichnet Zehntausende Dreiecke ohne Mühe | im Skript nur mehr Ringe und Seiten | ja: Die Richtwerte der Art Bible ändern sich, und das Skript braucht dann ein Werkzeug für runde Formen (Subdivision und Spiegeln in Blender, weiter per Skript) [Schluss] |
| **Gouraud-Schattierung** | ja; Raylib mischt Vertex-Farben ohnehin weich. Es braucht weiche Normalen und eine Helligkeit je Ecke statt je Fläche | klein; Shader oder C#-Rechnung wie heute, nur je Ecke | ja: Das ändert den Look des ganzen Spiels, auch der Wände. Für das Level könnte Flat bleiben, für Figuren Gouraud [Schluss] |
| **Texturen, per Skript erzeugt** | ja; Blender-Python kann Bilder aus Rauschen, Verläufen und gemalten Formen erzeugen, UVs abwickeln, Licht und Schmutz in die Textur backen und sie auf 16 oder 256 Farben reduzieren. Raylib lädt PNG-Texturen | groß; braucht eine eigene kleine Textur-Bibliothek im Skript | ja, ausdrücklich: Die Art Bible sagt heute „keine Texturen“, und der Shader im Spiel arbeitet ohne Texturkoordinaten. Herkunft bleibt sauber, solange alles per Skript entsteht [Schluss] |
| **Eingebackenes Licht in Vertex-Farben** | ja; Blender kann Licht in Vertex-Farben backen, auch ohne Fenster | klein bis mittel | eher nicht: verträgt sich mit „keine Texturen“. Aber Vorsicht bei beweglichen Teilen, deren Licht dann mitwandert [Schluss] |
| **Mehr starre Teile** (Ober- und Unterarm, Hände) | ja, wie heute | klein | nein, das ist Handwerk [Schluss] |
| **Skelett** | ja; Raylib lädt Skelett und Animationen aus glTF. Blender kann Knochen, Gewichte und Schlüsselbilder per Skript setzen | groß; Animationen per Skript zu schreiben ist mühsam, und die Code-Bewegung wird anders | ja, ausdrücklich: Die Art Bible sagt „keine Skelett-Animation“. Zwischenweg: Skelett nur für die Verformung, die Winkel weiter per Code [Schluss] |

---

## Vorschlag für neue Regeln der Art Bible

Alles hier ist **[Schluss]**, abgeleitet aus den Abschnitten oben. Die Zahlen sind Startwerte; die ersten Probestücke sollen sie bestätigen.

1. **Zielbild:** „PS2-Gothic“ heißt: gemalte Oberfläche auf glatt schattierter, sparsamer Form im Nebel. Nicht: einfarbige Facetten. Facetten bleiben nur, wo das Material hart ist (Stein, Metall, Holzkanten).
2. **Dreiecke (neue Richtwerte):**

   | Art | Dreiecke |
   |---|---|
   | normaler Gegner | 1 200 bis 2 500 |
   | großer Gegner oder Boss | 4 000 bis 8 000 |
   | Waffe im Bild | 1 500 bis 3 000 (nah und immer sichtbar) |
   | Kiste, Requisite | 150 bis 600 |
   | kleiner Gegenstand | 50 bis 200 |
   | sichtbare Szene | etwa 50 000 |

   Mindestens ein Viertel der Dreiecke einer Figur geht in Kopf und Hände.
3. **Schattierung:** Figuren mit Gouraud (weiche Normalen). Harte Kanten nur bewusst an Krallen, Knochenkanten, Metall. Level vorerst weiter wie heute, bis ein Probestück zeigt, wie Gouraud-Wände wirken.
4. **Oberfläche, wenn der Mensch Texturen erlaubt:**
   - **Figuren:** eine Textur je Figur, 256 × 256, davon mindestens ein Viertel für den Kopf.
   - **Requisiten:** 128 × 128.
   - **Farbtiefe:** im Skript auf höchstens 256 Farben je Textur reduziert.
   - **Inhalt:** Licht von oben, Schmutz in Vertiefungen, Flecken und Adern eingebacken.
   - **Herkunft:** Alles per Skript erzeugt, wie bisher.
5. **Oberfläche, wenn keine Texturen:**
   - Vertex-Farben mit **Verläufen**, nicht eine Farbe pro Fläche: dunkler zu Gelenken, Vertiefungen und Unterseiten, heller an Kanten, die zum Licht zeigen.
   - Mindestens drei Abstufungen je Farbe auf einem Teil.
6. **Proportionen von Gegnern:**
   - Der Kopf ist **klein**: höchstens ein Sechstel bis ein Siebtel der aufgerichteten Körperhöhe, nie breiter als der Hals mal zwei.
   - Augen klein, tief, nicht rund und nicht quadratisch.
   - Kein Merkmal des Kindchenschemas: kein großer runder Kopf, keine großen Augen, keine kurzen dicken Glieder.
7. **Asymmetrie:** Jeder Untote hat mindestens zwei deutliche Asymmetrien (Arm, Schulter, Kiefer, Kopfneigung, Fetzen).
8. **Gelenke:**
   - Gliedmaßen von Figuren in mindestens zwei Stücken (Ober- und Unterarm, Ober- und Unterschenkel), dazu die Hand.
   - Gelenke mit Kappe oder Überlappung, damit beim Drehen keine Lücke entsteht.
   - Mit Skelett: drei Kantenringe je Gelenk.
9. **Umriss vor Fläche:** Rundungen (Schädel, Schultern, Rippen) mit 8 bis 12 Seiten statt 4 bis 6, weil man sie im Umriss sieht. Wo der Umriss nicht spricht, wenig Dreiecke.
10. **Prüfbild im Spiel:** Weiterhin 4 m und 10 m. Dazu ein Nahbild aus 2 m, denn in der Ego-Sicht steht der Gegner beim Schlag direkt vor dem Spieler. Dort entscheidet sich, ob er bedrohlich oder niedlich wirkt.

---

## Selbstkritik am Ghul

**Was ich nach der Recherche anders bauen würde** [Schluss]:

1. **Der Kopf ist zu groß und zu rund-kastig.**
   - Mit 0,28 m Höhe und 0,23 m Breite bei einem 1 m hohen Körper ist er fast ein Drittel des Körpers.
   - Er sitzt aufrecht über den Schultern und hat ein flaches Gesicht mit zwei quadratischen Augen über einem breiten Maul.
   - Das sind Merkmale des Kindchenschemas und eines Smileys. Die Leitung hat das Grinsen schon bemerkt.
   - Ein bedrohlicher Ghulkopf wäre klein, schmal und lang, hinge tiefer als die Schultern, mit tiefen Augenhöhlen und einem schiefen, hängenden Kiefer.
2. **Die Glieder sind Stöcke.**
   - Arme und Beine sind vierkantige Stäbe mit gleichmäßiger Dicke, ohne Muskel, Sehne oder Gelenkknoten.
   - Ein ausgemergelter Untoter lebt davon, dass Gelenke dick und Glieder dazwischen dünn sind, dass Schulterblätter und Rippen sich abzeichnen und dass Hände zu groß sind.
   - Mit 40 Dreiecken pro Arm geht das nicht.
3. **Einfarbige Facetten statt Oberfläche.**
   - Haut, Lumpen und Knochen unterscheiden sich nur im Farbton, nie in der Oberfläche.
   - Jede Fläche hat genau eine Farbe; das liest sich als Bauklotz oder Spielzeug.
   - Gerade der Eindruck „verwest“ entsteht in der Ära über die Textur: Flecken, Adern, dunkle Ränder an Fetzen.
4. **Flat Shading mit sechs Helligkeiten.**
   - Das macht aus organischen Formen Kristalle.
   - Der Vergleich mit der stufenlosen Helligkeit zeigte das schon an, war aber nur ein halber Schritt; Gouraud mit weichen Normalen wäre der ganze.
5. **Vollkommen symmetrisch.**
   - Haltung, Arme, Wirbel und Fetzen sind gespiegelt.
   - Symmetrie wirkt geordnet und gebaut, nicht verfallen.
6. **Zu wenige Teile für die Bewegung.**
   - Ein Arm, der sich nur an der Schulter dreht, schwingt wie ein Pendel.
   - Kriechen braucht einen Ellbogen, der einknickt, und eine Hand, die greift.

**Warum er vermutlich nicht dem entsprach, was sich jemand unter einem PS2-Gothic-Gegner vorstellt** [Schluss]:

Ich habe die Vorgaben genau umgesetzt, aber die Vorgaben beschreiben einen anderen Stil. „Wenige kantige Flächen, keine Texturen, höchstens 400 Dreiecke, Flat Shading“ ergibt zwangsläufig etwas zwischen früher Arcade-3D und heutigem „PS1-Indie“.

Wer an PS2-Gothic denkt, erinnert sich an:

- Figuren mit glaubhafter Anatomie und weich schattierter Haut,
- gemalte Oberflächen voller Schmutz und Stoff,
- schwere, eingebackene Schatten,
- Nebel, aus dem eine Gestalt mit einem kleinen, hässlichen, asymmetrischen Kopf auftaucht.

Der Ghul ist dagegen sauber, symmetrisch, einfarbig und hat einen großen Kopf mit Grinsen. Er wirkt eher wie eine Spielfigur aus einem Baukasten als wie etwas, das in einer Gruft verrottet.

Dazu kommt mein eigener Fehler: Ich habe auf Lesbarkeit aus 10 m optimiert, also auf große, klare, kontrastreiche Formen, und dabei Bedrohlichkeit nicht geprüft. Die Lesbarkeitsregeln haben den Kopf noch größer und das Gesicht noch einfacher gemacht – genau in Richtung Kindchenschema. Künftig prüfe ich beides: Lesbarkeit aus 10 m **und** Wirkung aus 2 m.

---

# Nachtrag nach den Antworten des Menschen (2026-11-16)

## Neuer Rahmen

Der Mensch hat vier Antworten gegeben:

- **Was am Ghul nicht passte:** Gestalt und Proportionen, zu klotzig und zu wenig Detail, flächige Farben ohne Textur, Gesicht und Ausdruck – alles vier.
- **Texturen:** Sie sind erlaubt, wenn sie per Skript entstehen.
- **Detailgrad:** Gewünscht ist „wie echte PS2“. Ein unscharfer Eindruck ist in Ordnung, und wenn PS1-Stil einfacher ist, gern so.
- **Was der Ghul ist:** ein ausgemergelter, kriechender Mensch.

Er überlegt außerdem, die Sichtweite per Dithering zu begrenzen, dann aber alles im selben Stil.

Die Fragen a bis d der Leitung beantworte ich hier. Probestück 3 (`design/proben/grafik/ghul-2/`) setzt die Empfehlung um.

## a) PS1-Stil und PS2-Stil nebeneinander

| | PS1 (1994–2000) | PS2 (2000–2006) |
|---|---|---|
| **Dreiecke je Figur** | 250–750, Hauptfiguren eher oben [Beleg] ([Polycount](https://polycount.com/discussion/226167/)) | 3 000–5 000, bis 10 000 [Beleg] (Abschnitt 1) |
| **Textur-Speicher** | 1 MB für alles, als Fläche von 1024 × 512 Pixeln mit 16 Bit [Beleg] ([Wikipedia](https://en.wikipedia.org/wiki/PlayStation_technical_specifications), [psdevwiki PS1 GPU](https://psdevwiki.com/ps1/GPU)) | 4 MB für Bild, Tiefe und Texturen [Beleg] |
| **Texturgröße** | höchstens 256 × 256; üblich 32 × 32 bis 128 × 128 [Beleg] ([SplashEdit-Doku](https://psxsplash.github.io/docs/components/textures), [Polycount](https://polycount.com/discussion/comment/2753204)) | meist 128 × 128 bis 256 × 256 [Beleg] |
| **Farbtiefe Textur** | 4 Bit (16 Farben) oder 8 Bit (256 Farben) mit Farbtabelle, selten 15 Bit direkt [Beleg] ([Wikipedia](https://en.wikipedia.org/wiki/PlayStation_technical_specifications)) | ebenso überwiegend 4 und 8 Bit [Beleg] |
| **Filterung** | keine: Texel bleiben harte Quadrate | bilinear und Mipmaps: weich, eher unscharf [Beleg] (Abschnitt 2) |
| **Farbtiefe Bild** | intern 24 Bit, ausgegeben mit 15 Bit (5 je Kanal) und optional geordnetem Dithering. Daher das typische Körnchenmuster in dunklen Verläufen [Beleg] ([Bitmapped-Doku PS1](https://martink-rsa.github.io/bitmapped/presets/ps1), [Emulation Wiki](https://emulation.gametechwiki.com/index.php/Dithering)) | meist 32 Bit, teils 16 Bit mit Dithering; die Quellen sind uneinig [Beleg] |
| **Bildauflösung** | meist 320 × 240 [Beleg] ([Bitmapped-Doku PS1](https://martink-rsa.github.io/bitmapped/presets/ps1)) | 512–640 × 448, oft als Halbbilder [Beleg] |
| **Schattierung** | Flat und Gouraud; Licht fast immer vorab gebacken, weil die PS1 keine Lichtberechnung in Hardware hatte [Beleg] ([Polycount](https://polycount.com/discussion/comment/2798550)) | Flat und Gouraud je Dreieck einstellbar; eingebackenes Licht verbreitet [Beleg] |
| **Bildfehler** | wackelnde Ecken (gerundete Bildschirmkoordinaten), verzogene Texturen (keine Perspektivkorrektur), Flackern beim Sortieren (kein Tiefenpuffer) [Beleg] ([Pikuma](https://pikuma.com/blog/how-to-make-ps1-graphics), [Hackaday](https://hackaday.com/2026/02/06/why-playstation-graphics-wobble-flicker-and-twitch)) | Treppenkanten, Flimmern durch Halbbilder |

**Ein Zeitzeuge:** Ein ehemaliger Programmierer einer großen PS2-Rollenspielreihe wundert sich öffentlich, dass heutige Entwickler das Verziehen nachbauen. Damals habe man hart daran gearbeitet, es loszuwerden. [Beleg] ([Automaton](https://automaton-media.com/en/news/final-fantasy-x-programmer-doesnt-get-why-devs-want-to-replicate-low-poly-ps1-era-games-we-worked-so-hard-to-avoid-warping-but-now-they-say-its-charming/))

**Heutige Spiele im PS1-Stil:**

- Texturen in 64 × 64 oder 128 × 128 mit begrenzter Palette, Filterung und Mipmaps ausgeschaltet. [Beleg] ([foro3d über ein heutiges Spiel im N64/PS1-Stil](https://foro3d.com/en/2026/mayo/pseudoregalia-como-lograr-estetica-n64ps1-en-unreal-engine-4.html))
- 64 × 64 gilt als guter Mittelweg zwischen Qualität und Aufwand. [Beleg] ([itch.io-Jam](https://itch.io/jam/32-bit-jam/topic/981913/texture-resolution-polygon-count))
- Einfach herunterskalierte Bilder sehen dabei nicht richtig aus. [Beleg] ([Unity-Forum](https://discussions.unity.com/t/support-for-low-resolution-pixel-art-textures-and-sprites/342353))

### Empfehlung: PS1-nah, mit zwei Anleihen bei der PS2 [Schluss]

Die Vermutung der Leitung stimmt im Kern. Für reine Skript-Erzeugung ist PS1-nah klar verzeihender:

1. **Kleine Texturen verzeihen.** Eine 64- bis 128-Pixel-Textur ohne Filterung braucht nur grobe, kräftige Formen: Rippen als Streifen, Augenhöhlen als Flecken, Schmutz als Rauschen. Genau das kann ein Skript gut. PS2-Texturen leben von gemalten Feinheiten wie Hautporen, Stoffgewebe und Gesichtsmimik. Die überzeugend per Skript zu erzeugen, ist sehr viel schwerer; sie sehen schnell nach „Rauschfilter“ aus. Davor warnt auch die Literatur zu prozeduralen Texturen. [Beleg] ([GarageFarm](https://garagefarm.net/blog/procedural-texturing-in-3d-power-precision-and-possibility))
2. **Wenige Dreiecke verzeihen.** Um 1 000 Dreiecke kann man mit Ringen und Lofts im Skript sauber bauen. 5 000 Dreiecke mit glaubhafter Anatomie bräuchten Werkzeuge, die wir nicht haben: Bildhauern, Retopologie.
3. **Die Ära-Fehler bekommen wir gratis**, wenn wir sie wollen: keine Filterung, 15-Bit-Dithering, niedrige Auflösung. Die PS2-Weichheit (bilinear, Halbbilder, Röhre) müssten wir dagegen nachahmen.
4. **Stimmigkeit:** Unser Spiel hat schon harte Pixel (854 × 480, hart hochskaliert) und kantige Räume. PS1-nah passt dazu; ein weicher PS2-Ghul in einem harten Pixelraum würde fremd wirken.

**Zwei Anleihen bei der PS2** sind trotzdem sinnvoll:

- **Gouraud-Schattierung** statt Flat auf allen Figuren. Das hatte die PS1 auch schon, und es macht aus Facetten Körper.
- **Etwas mehr Dreiecke als typische PS1-Figuren**, etwa 800 bis 1 200 für einen normalen Gegner. Das reicht für Finger, Rippenkorb und Gelenkkugeln.

**Wackelnde Ecken und verzogene Texturen** würde ich **nicht** nachbauen. Sie sind die Fehler, die man damals loswerden wollte. Sie stören beim Zielen und lesen sich heute als Zitat, nicht als Stimmung.

**Was ich nicht empfehle:** „Unscharf wie PS2“ über bilineare Filterung. Kleine Skript-Texturen werden dann matschig, und die Pixelkante, die bei uns Stil ist, verschwindet. Der unscharfe Eindruck, den der Mensch zulässt, entsteht besser über das Dithering und die niedrige Auflösung. [Schluss]

## b) Dithering

**Wie es entstand:**

- Hardware mit wenigen Bits je Farbe (PS1: 5 Bit je Kanal) erzeugt in Verläufen sichtbare Stufen. Ein festes 4 × 4-Muster verschiebt die Schwelle von Pixel zu Pixel. So mischt das Auge Zwischentöne, und aus Stufen wird Körnung. [Beleg] ([Emulation Wiki](https://emulation.gametechwiki.com/index.php/Dithering), [Bitmapped](https://martink-rsa.github.io/bitmapped/guides/dithering))
- Auf Röhrenfernsehern verschwamm das Muster weitgehend; auf heutigen Bildschirmen sieht man es deutlich. [Beleg] ([VOGONS](https://www.vogons.org/viewtopic.php?p=208875))

**Dithering für Transparenz und Übergänge („Fliegengitter-Transparenz“):**

- Statt halbdurchsichtig zu mischen, wird ein Pixel nach einem geordneten Muster (Bayer-Matrix) entweder gezeichnet oder weggelassen.
- Damit blendet man Objekte an der Sichtgrenze aus oder wechselt zwischen Detailstufen.
- Das Muster muss an die **Bildschirmposition** gebunden sein, nicht an die Textur, sonst flimmert es.
- Liegen mehrere „durchsichtige“ Dinge hintereinander, brauchen sie versetzte Muster.

[Beleg] ([DigitalRune-Doku](https://digitalrune.github.io/DigitalRune-Documentation/html/fa431d48-b457-4c70-a590-d44b0840ab1e.htm), [Godot Shaders](https://godotshaders.com/shader/dithering-screen-door-transparency/), [Eurographics](https://diglib.eg.org/bitstreams/1850ae15-9d38-4522-a389-e6cfe8e990ed/download))

**Sichtgrenze per Dithering (im Probestück 3 umgesetzt)** [Schluss]:

- **Wie es funktioniert:** Für jedes Pixel gibt es einen Ausblend-Anteil aus der Entfernung. Liegt er über der Schwelle des 4 × 4-Musters an dieser Bildschirmstelle, fällt das Pixel weg und zeigt den Hintergrund. Die Farbe wird **nicht** mit Nebel gemischt; die Laterne dunkelt weiter ab. Dazu wird die ganze Farbe auf 5 Bit je Kanal mit demselben Muster gerastert.
- **Die Kurve ist nicht egal.** Mit der Nebelkurve des Spiels (ab 3,25 m) fehlten aus 10 m schon 43 % der Pixel, und die dünnen Glieder des neuen Ghuls zerfielen in Punkte. Für die Bilder lasse ich das Ausblenden deshalb erst ab 9 m beginnen und bei 18,2 m enden. Das ist ein Gestaltungswert, den der Mensch festlegen müsste.
- **Ehrlicher Nachteil:** Nahe Wände zeigen das Punktmuster deutlich (`spiel_4m_ruhe_dither.png`). Das ist gewollt PS1, aber es ist viel Unruhe im Bild. Abmildern ließe es sich mit einem 8 × 8-Muster oder einem Muster nur im Übergangsbereich.

**Was „alles im selben Stil“ dann heißt** [Schluss]:

- **Ein Muster, eine Auflösung:**
  - Dasselbe 4 × 4-Muster und dieselbe 15-Bit-Rasterung für alles, was in der 3D-Welt gezeichnet wird: Level, Figuren, Beute, Effekte.
  - Gerastert wird **nach** Licht und Laterne, also im Shader am Ende, nicht in den Texturen.
- **Texturen:** Sie bekommen selbst **kein** eingebranntes Dithering, sonst doppelt sich das Muster. Sie haben nur eine begrenzte Palette (16 bis 32 Farben) und keine Filterung.
- **Modelle:** Gouraud mit Licht je Ecke. Dünne Teile müssen dick genug sein, dass sie das Ausblenden überstehen: Glieder mindestens 2 bis 3 Pixel aus 10 m.
- **Transparenz:** Rauch, Geister und Blut würden als Fliegengitter gezeichnet statt halbdurchsichtig gemischt.
- **HUD:**
  - Ebenfalls in der niedrigen Auflösung gezeichnet, mit derselben Palette-Logik.
  - Weiche Verläufe wie Lebensbalken oder Abdunklungen werden mit demselben Muster gerastert.
  - Schrift bleibt scharf und ohne Dither, sonst ist sie nicht lesbar.
- **Level:** Wände und Böden bekommen ebenfalls kleine Texturen ohne Filterung, Stein in 64 × 64. Heute sind sie einfarbig. Ein texturierter Ghul in einfarbigen Räumen ist **nicht** derselbe Stil. Das ist im Probestück noch so und die größte Lücke.
- **Auflösung:** 854 × 480 ist für PS1-Stil eher hoch, weil das Muster sehr fein wird. Mit 640 × 360 oder 427 × 240 würde es gröber und „echter“, aber Ziele werden kleiner. Das muss der Mensch entscheiden.

## c) Texturen per Skript

**Verfahren, die in der Praxis genutzt werden:**

- Licht und Umgebungsverdeckung (AO) einbacken, Farben quantisieren (Posterize, begrenzte Palette) und Schatten und Konturen dazumalen. [Beleg] ([80.lv](https://80.lv/articles/breakdown-stylized-clover-material-in-substance-3d-designer), [Ultipixelizer](https://r60d.itch.io/ultipixelizer))
- Nicht nur ein Rauschen verwenden, sondern mehrere mischen und bewusst Unregelmäßigkeit setzen, sonst entsteht der „Prozedural-Look“. [Beleg] ([GarageFarm](https://garagefarm.net/blog/procedural-texturing-in-3d-power-precision-and-possibility))
- Blender kann per Skript UVs abwickeln (`smart_project`) und AO in ein Bild backen; dazu braucht es eine UV-Karte und einen Bild-Knoten als Ziel. [Beleg] ([Blender-Handbuch Baking](https://docs.blender.org/manual/en/4.5/render/cycles/baking.html), [Blender Artists](https://blenderartists.org/t/assign-image-to-mesh-uv-in-python/511119)). Der Bake-Aufruf mit fester UV-Ebene gilt als absturzanfällig. [Beleg] ([Blender-Bug T60093](https://developer.blender.org/T60093))

**Was ich im Probestück gemacht habe** [Schluss], weil es mit Skript-Erzeugung am besten kontrollierbar ist:

1. **UVs aus der Bauweise statt automatischer Abwicklung.** Jedes Teil entsteht als Loft aus Ringen. Dann ist die UV-Karte geschenkt: u = rundherum (0 unten am Bauch, 0,5 oben auf dem Rücken), v = entlang des Teils. Jedes Teil bekommt ein festes Rechteck im Atlas; linke und rechte Gliedmaßen teilen sich dasselbe.
   - **Vorteil:** Der Maler weiß, wo was ist – Rippen sind Streifen in v, die Wirbelsäule ist die Linie u = 0,5, die Augen liegen bei u = 0,5 ± 0,085.
   - Die Naht liegt jeweils auf der Unterseite (Bauch, Kinn, Hinterkopf), wo man sie kaum sieht.
2. **Malen in vierfacher Größe** (512 × 512) in numpy, dann auf 128 × 128 verkleinert. Das gibt leicht weiche Übergänge wie bei einer verkleinerten Malerei, statt harter Rechenkanten.
3. **Bausteine:**
   - Wertrauschen in zwei Größen für Hautunruhe.
   - Ein grobes Rauschen mit Schwelle für Flecken.
   - Ein Verlauf über u für Licht von oben (Unterseite dunkler).
   - Ein Verlauf über v für dunkle Gelenke.
   - Weiche Ellipsen für Augenhöhlen, Wangen und Schulterblätter.
   - Streifenmuster für Rippen, Zähne und Stofffalten.
   - Fein gestreiftes Rauschen für Haarsträhnen.
4. **Begrenzte Palette:** K-Means auf 32 Farben. Das gibt die abgestimmten, leicht gestuften Töne einer Textur mit Farbtabelle.
5. **AO nicht gebacken, sondern gemalt** (Gelenke, Rippenzwischenräume, Bauch). Das ist billiger und genau steuerbar. Echtes AO-Backen mit Cycles wäre der nächste Schritt für Flächen, die sich nicht so klar beschreiben lassen.

**Was sich je Material bewährt** [Schluss]:

| Material | Rezept |
|---|---|
| **Haut** | Grundton, zwei Rauschgrößen ±10 %, oliv-braune Flecken über einer Schwelle, Unterseite dunkler, über Knochen heller (Schädel, Gelenke) |
| **Knochen und Zähne** | heller, wärmerer Ton, dunkle Fugen dazwischen; harte Kanten, kein Rauschen |
| **Stoff** | Grundton, Faltenstreifen quer zum Fall, Schmutzflecken, zum Saum hin dunkler; ausgefranster Saum über die Geometrie |
| **Stein** (nicht im Probestück) | Fugenraster mit versetzten Reihen, je Stein leicht anderer Ton, dunkle Fugen, helle obere Kanten |
| **Holz** (nicht im Probestück) | Maserung als gestauchtes Rauschen entlang einer Richtung, dunkle Brettfugen, Astlöcher als kleine Ellipsen |

## d) Ein ausgemergelter, kriechender Mensch

**Proportionen eines Menschen:**

- Etwa 7 bis 8 Kopfhöhen.
- Die Spannweite der Arme entspricht der Körpergröße.
- Hängend reichen die Fingerspitzen bis zur Mitte des Oberschenkels.
- Die Körpermitte liegt am Schambein.

[Beleg] ([drawingforall](https://www.drawingforall.net/human-body-proportions/), [Wikipedia Proportionskanon](https://www.wikiwand.com/en/articles/Artistic_canons_of_body_proportions))

**Abgemagerte Körper:**

- Bei Auszehrung treten Rippen und Rippenzwischenräume, Schlüsselbeine, Schulterblätter und Wirbelsäule hervor; Gelenke wirken knotig, weil die Muskeln dazwischen fehlen. [Beleg] ([Mind the Graph](https://mindthegraph.com/illustrations/anorexia-cachexia-muscle-loss-male-front-indian))
- Unheimlich wirkt, was **fast** menschlich ist. Das ist das „Uncanny Valley“ nach Mori (1970). [Beleg] ([Wellcome Collection](https://wellcomecollection.org/stories/why-the-scariest-monsters-look-almost-human), [Game Developer](https://www.gamedeveloper.com/design/designing-the-uncanny))
- Verstärkt wird das durch leere oder falsche Augen, gespannte Haut, verlängerte Glieder und ungelenke Bewegung. [Beleg] ([Game Developer](https://www.gamedeveloper.com/design/designing-the-uncanny), [KeenGamer](https://www.keengamer.com/articles/features/opinion-pieces/the-art-of-scary-monster-design))

**Umsetzung im Probestück** [Schluss]:

- **Menschliche Grundlage, kleiner Mensch:** Die Teile sind wie bei einem Menschen von etwa 1,5 m bemessen. Der Kopf ist rund 0,22 m hoch, also ein Siebtel. Arme und Beine sind leicht verlängert: Unterarme etwa 0,39 m, Finger bis 0,1 m.
- **Haltung:**
  - Auf Händen und Knien, die Zehen aufgestellt.
  - Der Rücken ist zum Buckel gekrümmt, das Becken liegt tiefer als der Buckel.
  - Der Hals ist lang und dünn, schräg nach vorn gestreckt.
  - Der Kopf schaut zum Spieler hoch und ist um 10° zur Seite geneigt.
  - Der Kiefer hängt schief offen.
- **Rumpf:**
  - Breiter Rippenkorb, eingeschnürte Taille, Becken.
  - Die Wirbelsäule ist ein Grat in der Geometrie *und* eine Knotenreihe in der Textur; die Schulterblätter stehen in Geometrie und Textur vor.
  - Rippen als Streifen an den Flanken.
- **Gelenke:** Die Teile sind starr und überlappen. Das Ende des oberen Teils ist dicker (Ellbogen, Knie) und deckt die Fuge.
- **Hände:** groß, fünf lange, gekrümmte Finger, die Spitzen krallen sich in den Boden, Nägel und Schmutz in der Textur.
- **Asymmetrie:** Ein Arm greift 5 cm weiter vor; Kopfneigung, schiefer Kiefer, ein Lumpenfetzen hängt tiefer.
- **Gesicht:**
  - Schmale, schräge, tiefe Augenhöhlen – geometrisch eingedellt und dunkel gemalt – mit einem winzigen, trüben Augapfel.
  - Hohle Wangen, statt einer Nase zwei Schlitze.
  - Die Oberlippe ist weggefault; man sieht unregelmäßige Zähne und dunkles Zahnfleisch.
  - Strähnige, verfilzte Haarreste: Das ist das deutlichste Zeichen „war einmal ein Mensch“.

### Neue Regeln, ergänzt (ersetzt die Zahlen im Vorschlag oben, wo sie abweichen) [Schluss]

- **Stil:** PS1-nah mit Gouraud.
  - Normale Gegner 800 bis 1 200 Dreiecke, große Gegner 2 000 bis 4 000.
  - Requisiten 100 bis 400.
  - Waffe im Bild bis 1 500.
- **Texturen:**
  - Eine je Figur, 128 × 128, höchstens 32 Farben, keine Filterung, keine Mipmaps.
  - Requisiten und Wandstücke 64 × 64, 16 bis 32 Farben.
  - Kopf mindestens ein Achtel der Atlasfläche.
- **UVs:** entstehen beim Bauen (Loft: rundherum und entlang). Nähte auf die Unterseite. Spiegelbildliche Teile teilen sich die Textur.
- **Licht:** je Ecke aus der Welt-Normale (Gouraud). In der Textur nur gemalte Verdeckung und Licht von oben, nicht die Richtung der Laterne.
- **Bild:** 15-Bit-Rasterung mit 4 × 4-Muster am Ende des Shaders. Die Sichtgrenze wählt der Mensch zwischen Nebel und Dither; in jedem Fall dieselbe für alles.
- **Keine** wackelnden Ecken, **keine** verzogenen Texturen.

## Quellen

Alle Quellen sind in eigenen Worten zusammengefasst. Gesperrte Seiten habe ich nur über die Auszüge der Suchmaschine gelesen; das steht beim jeweiligen Punkt.

**Einschränkung bei Polycount:** Die Suchmaschine fasste mehrere Polycount-Kommentare zusammen. Welche Aussage genau aus welchem Kommentar stammt, ist aus den Auszügen nicht sicher zu erkennen. Die Links nennen die Fundstellen der Suche, nicht in jedem Fall genau den Kommentar mit dem zitierten Satz. Die Zahl für die Hauptfigur von 2001 (etwa 5 400 Flächen) stammt aus einem solchen Auszug.

**Gesperrt:** Das Sega-Retro-Wiki (Szenen-Zahlen) blockiert automatische Abrufe; die Zahlen stammen aus dem Suchauszug.

- PlayStation Blog, Polygon-Entwicklung bekannter Figuren mit Zitaten benannter Entwickler: <https://blog.playstation.com/2019/12/16/the-polygonal-evolution-of-5-iconic-playstation-characters/>
- Game Art Research (Blog), Auswertung von Spieldaten eines PS2-Kampfspiels: <https://gameartresearch.blogspot.com/2012/12>
- Polycount, PS1/PS2-FAQ und Diskussionen (nur Suchauszüge): <https://polycount.com/discussion/226167/>, <https://polycount.com/discussion/comment/704016>, <https://polycount.com/discussion/comment/703863>, <https://polycount.com/discussion/comment/873059>, <https://polycount.com/discussion/comment/2753204>, <https://polycount.com/discussion/comment/2798550>, <https://polycount.com/discussion/182247/how-did-artists-rig-animate-ps1-era-characters>, <https://polycount.com/discussion/comment/2795051>, <https://polycount.com/discussion/comment/2076031>, <https://polycount.com/discussion/comment/1635141>, <https://polycount.com/discussion/comment/2728413>, <https://polycount.com/discussion/comment/2045771>
- GameDev.net, Polygon-Grenzen für Figuren: <https://www.gamedev.net/forums/topic/438909-character-polygon-limits/?page=1>
- EA, GDC 2002, Visual Engineering SSX (nur Suchauszug): <https://computernewb.com/~lily/files/SSX/talks/gdc2002/GDC_Visual_Engineering_SSX.pdf>
- PS2 Dev Wiki, Graphics Synthesizer: <https://psdevwiki.com/ps2/Graphics_Synthesizer>, <https://playstationdev.wiki/ps2devwiki/Graphics_Synthesizer>
- PS2 Linux Programming, Texturen und Paletten: <https://ps2-edu.tensioncore.com/texmap1/texmap1.html>, <https://ps2-edu.tensioncore.com/old/palt/palt.html>
- GameDev.net, PS2-Programmierung Teil 2: <https://gamedev.net/tutorials/programming/general-and-gameplay-programming/playstation-2-game-programming-part-2-drawing-r2027>
- Scribd, PS2 Special Effects: <https://www.scribd.com/document/253570275/Special-Effects>
- libretro-Blog, Halbbilder und Kantenglättung auf der PS2: <https://www.libretro.com/?p=50686>
- PCSX2-Forum, Videomodi: <https://forums.pcsx2.net/archive/index.php/thread-68073.html>
- Beyond3D, Polygonzahlen: <https://forum.beyond3d.com/threads/yes-but-how-many-polygons-an-artist-blog-entry-with-interesting-numbers.39321/page-3>
- videogameforums, Polygonzahl je Bild eines Action-Horror-Spiels: <https://videogameforums.com/t/re4-ps2-within-dreamcast-reach.3899/latest>
- Dithering: <https://shmups.system11.org/viewtopic.php?p=1547286>, <https://emulation.gametechwiki.com/index.php/Dithering>
- PS1-Artefakte: <https://pikuma.com/blog/how-to-make-ps1-graphics>, <https://hackaday.com/2026/02/06/why-playstation-graphics-wobble-flicker-and-twitch>
- Nebel als Stimmung: <https://gamesradar.com/why-silent-hill-2-so-high-our-new-top-100-because-its-perfect-game>, <https://www.superjumpmagazine.com/drafting-through-the-mists/>
- Topologie: <https://docs.altava.com/creator-guide/3d-modeling/low-polygons>, <https://www.shootfirst.art/toplogy>, <https://redlib.hackliberty.org/r/ps1graphics/comments/1lj0af0/does_anyone_have_any_proper_psx_style_character>
- Retro-Figurenpaket (Texturgrößen Gesicht/Hände): <https://starkcrafts.itch.io/psx-japanese-girl>
- Kindchenschema: <https://pmc.ncbi.nlm.nih.gov/articles/PMC3105163>, <https://pmc.ncbi.nlm.nih.gov/articles/PMC3260535>
- Formsprache und Creature Design: <https://prolificstudio.co/blog/shape-language-in-character-design/>, <https://skillselion.com/skills/omer-metin/skills-for-antigravity/creature-design>, <https://sites.google.com/view/graduationrobinboonacker/research/games>
- Lesbarkeit im Dunkeln: <https://https-book_leveldesignbook_com.proxy.bugsmash.io/process/lighting/darkness>, <https://gametyrant.com/news/why-great-video-game-monsters-are-recognizable-at-a-glance>, <https://itch.io/post/10093049>
- Gotische Architektur: <https://pressbooks.palomar.edu/arthc1100/chapter/gothic-art-and-architecture/>, <https://mymodernmet.com/gothic-architecture-characteristics>
- Baukästen heute: <https://assetstore.unity.com/packages/3d/environments/dungeons/multistory-dungeons-33955>
- Vorberechnetes Licht: <https://help.autodesk.com/cloudhelp/2017/ENU/Maya/files/GUID-2EC022C8-B33C-4CCD-BBBE-73FE1FDE6E51.htm>, <https://sites.google.com/site/swbf2modtoolsdocumentation/bf2_lighting>

**Quellen des Nachtrags:**

- PS1-Technik: <https://en.wikipedia.org/wiki/PlayStation_technical_specifications>, <https://psdevwiki.com/ps1/GPU>, <https://martink-rsa.github.io/bitmapped/presets/ps1>, <https://martink-rsa.github.io/bitmapped/guides/dithering>, <https://psxsplash.github.io/docs/components/textures>
- Dithering auf Röhren: <https://www.vogons.org/viewtopic.php?p=208875>
- Rückblick eines PS2-Programmierers: <https://automaton-media.com/en/news/final-fantasy-x-programmer-doesnt-get-why-devs-want-to-replicate-low-poly-ps1-era-games-we-worked-so-hard-to-avoid-warping-but-now-they-say-its-charming/>
- Heutiger PS1-Stil: <https://foro3d.com/en/2026/mayo/pseudoregalia-como-lograr-estetica-n64ps1-en-unreal-engine-4.html>, <https://itch.io/jam/32-bit-jam/topic/981913/texture-resolution-polygon-count>, <https://discussions.unity.com/t/support-for-low-resolution-pixel-art-textures-and-sprites/342353>
- Fliegengitter-Transparenz: <https://digitalrune.github.io/DigitalRune-Documentation/html/fa431d48-b457-4c70-a590-d44b0840ab1e.htm>, <https://godotshaders.com/shader/dithering-screen-door-transparency/>, <https://diglib.eg.org/bitstreams/1850ae15-9d38-4522-a389-e6cfe8e990ed/download>
- Prozedurale Texturen und Backen: <https://80.lv/articles/breakdown-stylized-clover-material-in-substance-3d-designer>, <https://r60d.itch.io/ultipixelizer>, <https://garagefarm.net/blog/procedural-texturing-in-3d-power-precision-and-possibility>, <https://docs.blender.org/manual/en/4.5/render/cycles/baking.html>, <https://blenderartists.org/t/assign-image-to-mesh-uv-in-python/511119>, <https://developer.blender.org/T60093>
- Proportionen und Auszehrung: <https://www.drawingforall.net/human-body-proportions/>, <https://www.wikiwand.com/en/articles/Artistic_canons_of_body_proportions>, <https://mindthegraph.com/illustrations/anorexia-cachexia-muscle-loss-male-front-indian>
- Uncanny Valley: <https://wellcomecollection.org/stories/why-the-scariest-monsters-look-almost-human>, <https://www.gamedeveloper.com/design/designing-the-uncanny>, <https://www.keengamer.com/articles/features/opinion-pieces/the-art-of-scary-monster-design>
