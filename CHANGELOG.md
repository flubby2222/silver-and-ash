# Changelog

Alle wichtigen Änderungen an Silver & Ash, pro Version. Format nach [Keep a Changelog](https://keepachangelog.com/de/1.1.0/), Versionsnummern nach [Semantic Versioning](https://semver.org/lang/de/).

Neue Einträge kommen zuerst unter **Unveröffentlicht**. Beim Release wird daraus ein Abschnitt mit Versionsnummer und Datum (siehe [docs/release.md](docs/release.md)).

## [Unveröffentlicht]

### Hinzugefügt
- **Ghul-Augen kündigen den Schlag an:** Holt ein Ghul zum Schlag aus, leuchten seine Augen blutrot und werden in drei harten Stufen heller, bis er zuschlägt (eine halbe Sekunde, so lange wie das Ausholen). Jeder Ghul leuchtet für sich; ein toter Ghul leuchtet nicht. Zeitpunkt, Schaden und Balancing bleiben gleich. Dauer und Stärke sind vorläufig bis zum nächsten Spieltest (#447)

### Geändert
- **Musik in den Katakomben lauter:** Die Musik im Lauf ist 10 dB lauter als bisher, etwa so laut wie die Musik im Hauptmenü (nach der Hörprobe zum Spieltest vom 19.11.). Klang und Stück sind gleich, der Regler für die Atmosphäre wirkt weiter von 0 bis 100 % (#427)
- **Aufgehobene Fernwaffen landen gleich im Waffenplatz:** Ist einer der drei Fernwaffen-Plätze frei, kommt eine mit F aufgehobene Fernwaffe direkt dorthin (auf den ersten freien) und lässt sich sofort mit Mausrad oder 1/2/3 wählen. Die Waffe in der Hand wechselt dabei nicht. Sind alle Plätze belegt, geht sie wie bisher ins Inventar; erst wenn auch das voll ist, steht „Inventory full“. Nahkampfwaffen kommen weiter ins Inventar, denn die Hand ist nie leer (nach dem Spieltest vom 19.11., #434)
- **Die Keule liegt wie ein Hammer in der Faust:** In Ruhe knickt die Keule am Griff ab, den Kopf 35 Grad nach oben und 10 Grad nach außen; bisher zeigte sie wie die Klinge gerade in die Bildtiefe und wirkte wie eine Stoßwaffe. Der Ärmel bleibt, wie er war, und verdeckt den Knick. Beim Schlag wird die Keule gerade und trifft gerade, auch nach einem aufgeladenen Schlag; beim vollen Ausholen knickt sie 20 Grad hoch und 10 Grad nach innen. Zeiten, Reichweite und Schaden bleiben gleich (#417)
- **Titelbild ohne Untertitel:** Unter „Silver & Ash“ steht nicht mehr „a hunt in the catacombs“ (nach dem Spieltest vom 19.11.). Name, Menü und Versionsnummer bleiben, wo sie waren.
- **Die Silberklinge reicht weiter:** 2,5 m statt 2 m vor den Augen, also ein Viertel mehr (vorläufig, nach dem Spieltest vom 19.11.). Das gilt für jeden Hieb und für den aufgeladenen Schlag, auch beim Aufbrechen rissiger Wände. Die Keule reicht weiter 2 m (#425)
- **Gorms Kettenschwung reicht weiter:** 12 m statt 8 m (vorläufig bis zum nächsten Spieltest). Ein Schritt zurück reicht nicht mehr, man muss ausweichen oder die ganze Ausholzeit weglaufen. Die Kette im Bild wird entsprechend länger (#430)

### Behoben
- **Türen öffnen sich auf ganzer Höhe:** Bei Holztür, verschlossener Tür und Bosstor blieb beim Öffnen der obere Teil im Spitzbogen stehen (Kopffeld, Eisenband, beim Bosstor die Glasscheibe); man lief unter einem schwebenden Brett hindurch. Jetzt schwingt jeder Flügel mit seinem Teil des Kopffelds auf. Aussehen der geschlossenen Türen, Steinrahmen und Durchgang bleiben gleich (#429)
- **Alt+Tab im Vollbild:** Im Vollbild wechselt Alt+Tab wieder zu anderen Fenstern; bisher lag das Spielfenster immer obenauf und verdeckte das gewählte Fenster. Verliert das Spiel den Fokus, öffnet sich wie bisher das Esc-Menü: der Lauf hält an, die Maus ist frei. Zurück im Spiel geht es mit „Resume“ weiter (#428)
- **Schüsse treffen den Ghul, wo man ihn sieht:** Bisher war der Ghul für Schüsse eine runde Säule von 1 m Höhe. Schüsse über seinen Rücken, neben oder knapp über seinen Kopf trafen, obwohl dort nichts vom Modell ist. Jetzt treffen Schüsse den Rumpf (0,78 m hoch) und den Kopf, der vorn darüber sitzt (bis 0,95 m) – so groß wie das Modell. Kopf und Schultern treffen weiter, Kopftreffer bleiben kritisch. Laufen, Anstoßen und Klingenhiebe bleiben, wie sie waren. Die Maße sind vorläufig bis zum nächsten Spieltest (#433)
- **Ausdauer beim Aufladen:** Weicht man mit gehaltenem Klingenschlag aus (Ausweichrolle am Boden oder Dash in der Luft), reichte die Ausdauer danach oft nicht mehr für das nächste Stück der Ladung. Die Ladung kam dann nicht weiter, und die Ausdauerleiste füllte sich erst wieder, wenn man die Taste losließ. Dasselbe passierte, wenn das Aufladen die Ausdauer von selbst aufbrauchte. Jetzt hält die Ladung auf dem erreichten Stand, und die Leiste füllt sich auch bei gehaltener Taste wieder auf. Der Schlag löst nicht von selbst aus, die Kosten des Aufladens bleiben gleich (#413)
- **Pistole, silberne Platte:** Die silberne Platte hinten am Lauf stand über Griff und Schloss hinaus und schwebte in der Ich-Sicht als heller Stab. Sie sitzt jetzt weiter vorn auf dem Laufende; Form, Lauf und alles andere sind unverändert (#431)
- **Kein Festsitzen mehr in Kampfräumen:** Bisher standen die Gegner eines Kampfraums schon vor dem Betreten im Raum. Man konnte sie durch die offene Tür treffen und herauslocken. Danach blieb der verriegelte Raum für immer zu. Jetzt sind die Gegner erst da, wenn sich der Raum verriegelt: Ghule wachsen aus dem Boden, Fledermäuse sinken aus der Decke, nie nah an einer Tür. In dieser kurzen Phase greifen sie nicht an und nehmen keinen Schaden. Die Dauer steht als `emergeTime` in `balance.json`. Der Bossraum mit Gorm bleibt wie bisher (#424)
- **Gegner erscheinen nicht mehr direkt vor einem Spieler:** Steht ein Spieler beim Verriegeln eines Kampfraums auf oder nahe einer Startzelle, kommt der Gegner dort nicht mehr heraus (bisher überlappte ein Ghul ihn kurz). Er weicht auf die nächste freie Zelle des Raums aus, die mindestens `playerDistance` (vorläufig 3 m, in `balance.json`) von jedem Spieler und weiter `doorDistance` von den Türen entfernt ist. `doorDistance` und `playerDistance` sind jetzt mindestens eine Zelle (2 m); kleinere Werte in `balance.json` ergeben eine Warnung und den Standardwert (#445)
- **Regler mit der Maus ziehen:** In den Einstellungen folgt ein Regler jetzt der gedrückten Maus in beide Richtungen, ein Klick setzt ihn an die Klickstelle. Bisher änderte nur jeder einzelne Klick den Wert um einen Schritt, je nachdem, in welches Drittel der Zeile er fiel; Ziehen bewirkte nichts, und zurück nach links ging es nur Klick für Klick ganz links in der Zeile. Links/Rechts arbeiten wie bisher, ein Klick auf „Display“ schaltet weiter um. Der gezogene Wert wirkt sofort, gespeichert wird er einmal beim Loslassen (#426)

## [0.11.1] – 2026-11-19

### Behoben
- **Musik im Release:** In der heruntergeladenen Version 0.11.0 lief keine Musik, weder im Titel noch im Lauf. Beim Bau der einen `.exe` waren die beiden Musikdateien in die `.exe` gewandert, statt im Ordner `assets/audio` zu liegen, wo das Spiel sie sucht. Jetzt bleiben alle Spieldaten neben der `.exe`. Beim Start mit `dotnet run` war der Fehler nicht zu hören (#422).

## [0.11.0] – 2026-11-19 – Gestalt & Klang

### Hinzugefügt
- **Neue Monsterklänge:** Fünfzehn Klänge von Fledermaus, Ghul und Gorm sind neu gebaut, nach deinem Urteil zur Hörprobe. Die Fledermaus faucht, zischt und spuckt statt zu tönen; ihr Ankündigungsruf ist ein raues Zirpen-Schnarren; der Ghul klingt stimmhaft mit Atem und Kehle; Gorm klingt tiefer und knarrender. Das Skript ist `tools/audio/monsterklaenge.py` (#392)
- **Die Keule als Modell:** In der Hand ist die Keule jetzt ein eigenes 3D-Modell – Holzgriff, schwerer Kopf mit Eisenbändern, abgenutzt – statt einfacher Klötze, mit Ärmel und Hand wie bei der Klinge. Halten und Schlagen bleiben gleich. Fehlt die Datei, zeichnet das Spiel weiter die Klötze. Neue Foto-Szenen `club-rest`, `club-swing-half`, `showroom-club` (#403)
- **Sieben neue HUD-Zeichen:** Keule, Stiefel, Rosenkranz, Schnellzieh-Holster, Klingen-Parade, Egel und Aschenherz haben jetzt ein eigenes Zeichen im HUD, im Stil von Klinge, Flasche und Auge (neutrales Silber und Knochenton, harte Pixel, kein Rot). Statt des Anfangsbuchstabens zeigt das HUD jetzt bei jeder Waffe und jedem Gegenstand ein Bild; die Keule steht wie die Klinge im Nahkampf-Feld. Neue Foto-Szene `hud-items` (#381)
- **Nachlade-Schleier und Effekte am Bildrand:** Lädst du nach, liegt ein dunkler Schleier über dem Bild der Waffe und zieht sich mit dem Fortschritt nach links zurück; seine Vorderkante ist eine helle Linie. Ein Treffer dunkelt das Bild kurz ab (statt des roten Blitzes). Bei weniger als einem Viertel Leben pulsiert langsam ein dunkelroter Rand, bei einem neuen Skillpunkt leuchtet der Rand kurz golden. Beide Ränder bestehen aus harten, gekörnten Pixeln und lassen die Bildmitte frei. Neue Foto-Szenen `hud-reload-start`, `hud-reload-mid`, `hud-hit`, `hud-lowlife`, `hud-skillpoint-glow` (#370)
- **Menüs im neuen Gewand:** Titelbild, Esc-Menü, Einstellungen, die Frage vor dem Beenden und die Reiter „Map | Equipment“ tragen jetzt den Stil C2 „Schlank“: dunkle Tafeln mit silbernem Rand und kleinem Giebel, ein spitzer Rahmen um die gewählte Zeile, goldene Regler mit Knopf und Reiter mit Giebel. Bedienung und Tasten bleiben gleich. Die Ausrüstungsseite passt jetzt auch bei 854 × 480 ganz ins Bild, und alle Menüs sitzen in jedem Seitenverhältnis in der Mitte. Die Bildteile erzeugt `tools/menu/menue_teile.py` (#371)
- **Klang für den Skillpunkt:** Steigst du eine Stufe auf, erklingt ein tiefer Schlag mit kurzem, dunklem Chor, der langsam und ohne Wummern ausklingt – die Fassung aus der Hörprobe. Bringt ein Treffer gleich zwei Stufen, hörst du ihn einmal. Er folgt dem Regler „Effects volume“ in den Einstellungen; mit den alten Geräuschen (**K**) bleibt er still (#375, #377, Teil von #342)
- **Einstellungen:** Titelbild (Start, Settings, Quit) und Esc-Menü (Resume, Settings, Back to title, Quit) haben jetzt eine Seite mit Einstellungen: Mausempfindlichkeit (bisher direkt im Esc-Menü), Gesamtlautstärke, Effekte (alle kurzen Geräusche samt Menüklick), Atmosphäre (die Musik) und Fenster oder Vollbild. **Links/Rechts** ändert einen Wert, die Lautstärken in Schritten von 10 % zwischen stumm und voll; jede Änderung wirkt sofort und gilt auch nach einem Neustart – das Spiel startet dann wieder im Vollbild, wenn du es so verlassen hast. **Alt + Enter** schaltet weiter direkt um, die Einstellung zieht mit. **N** schaltet weiter alles stumm. Fehlt die Datei mit den Einstellungen oder ist sie kaputt, gelten die Standardwerte; ein einzelner falscher Eintrag kostet nur diesen Wert. **Quit** fragt jetzt vorher nach, vorausgewählt ist „Cancel“, und Esc geht dort wie in den Einstellungen einen Schritt zurück (#349)
- **Vollbild in jedem Seitenverhältnis:** **Alt + Enter** schaltet zwischen Fenster und randlosem Vollbild in der Auflösung des Bildschirms um. Das Bild behält seine 480 Zeilen, seine Breite folgt der Form des Fensters: 16:9 bleibt 854 Pixel breit, 21:9 wird 1120, 16:10 768 und 4:3 640 – ohne Balken und ohne Verzerrung, ein breiter Bildschirm sieht einfach mehr zur Seite. Dasselbe gilt, wenn du das Fenster frei aufziehst. Schmaler als 5:4 oder breiter als 32:9 hält das Bild an und zeigt Balken. HUD, Texte und Menüs bleiben an Ecken und Mitte. Für Fotos wählt `--window 2520x1080` (nur Debug-Version) die Fenstergröße; ohne bleibt es bei 1280 × 720 und 854 × 480 (#350)
- **Kisten und Gorm geben Ausrüstung:** Neben Silber und Phiole springt aus einer Kiste jetzt manchmal eine Waffe oder ein Gegenstand – aus der Bettlerkiste in 30 % der Fälle, aus der Wächterkiste in 50 %, aus der Königskiste in 80 %, aus der Vampirkiste immer. Das Stück hat mindestens die Stufe der Kiste, selten eine darüber, und ist meist ein Gegenstand (70 %), sonst eine Waffe. Es bleibt im Raum der Kiste und landet nie in einer Wand. Gorm lässt immer ein Stück fallen: Royal, in 20 % der Fälle Vampiric. Jedes Stück gibt es pro Lauf nur einmal; ist nichts Passendes mehr übrig, gibt die Kiste ihr Silber ein zweites Mal und Gorm 100 Silber. Alle Chancen stehen in `balance.json`, der Balance-Report zählt die erwartete Ausrüstung pro Ebene. Gegenstände wirken noch nicht (#318, schließt #194)
- **Ausrüstung am Boden, mit F nehmen:** Waffen und Gegenstände am Boden leuchten in ihrer Stufenfarbe, und der Hinweis „[F] Take Rifle (Forged)“ steht in derselben Farbe. Darüberlaufen nimmt nichts. Ist das Inventar voll, bleibt die Waffe liegen und der Hinweis sagt „Inventory full“. Gegenstände brauchen keinen Platz. Nach dem Nehmen zeigt links eine Karte Name und Wirkung; je höher die Stufe, desto mehr wird der Fund gefeiert, und ein Vampiric-Fund lässt den Bildrand kurz blutrot aufleuchten. Gegenstände wirken noch nicht, und die Karte sagt das auch. Im Musterraum liegen vier Gegenstände, je einer jeder Stufe; im normalen Spiel liegt Ausrüstung erst mit #318 (Teil von #194, #317)
- **Beute-Topf (noch unsichtbar):** Jeder Lauf hat jetzt einen Topf mit allen Waffen und den ersten acht Gegenständen – jedes Stück genau einmal, also keine Doppelten. Daraus werden später Kisten und Gorm ziehen; zu sehen ist davon noch nichts, und die Gegenstände wirken noch nicht. Chance auf eine Stufe höher und Anteil Gegenstand statt Waffe stehen in `balance.json` unter `loot` (Teil von #194)
- **Gorm hat Klang:** In Klangpaket 1 hörst du Gorm schwer auftreten – ein tiefer Schritt etwa alle 0,8 s, in Phase 2 schneller. Jeder seiner drei Angriffe kündigt sich mit eigenem Klang an (Hammer, Kettenschwung, Hakenwurf), und jede Ankündigung kommt durch, egal wie viel gleichzeitig passiert. Dazu der Aufschlag des Hammers, das Rasseln der Kette, Hakenwurf und Hakentreffer, sein Treffer (ein eigener für den Kopfschuss), das Brüllen beim Wechsel in Phase 2 und sein Tod. Triffst du ihn mit der Klinge, hörst du seinen eigenen Treffer statt des Klingentreffers. Mit den alten Geräuschen (**K**) bleibt er still (#298, Teil von #271)
- **Die Wendeltreppe aus Stein:** Nach Gorms Tod steigt im Bossraum statt der Kästen eine steinerne Wendeltreppe aus dem Boden – in den Steinen der Wände dieses Raums, mit einer Messinglampe obenauf, die erst leuchtet, wenn die Treppe steht, und dann langsam pulsiert. Aufstieg (3 s), **F** und Sieg bleiben wie bisher. Hineinlaufen kannst du vorerst noch (#300)
- **Gorms Schlag bricht den Boden auf:** Statt eines Rings aus hellen Kästen läuft nach dem Hammerschlag ein Ring aus aufgeworfenen Steinplatten über den Boden, ganz ohne Rot: vorn eine helle, frisch gebrochene Steinkante, dahinter eine dunkle Furche und Staub. Die Vorderkante liegt genau auf dem Rand der Trefferzone: Was dich noch nicht erreicht hat, trifft dich auch nicht, und wer rechtzeitig springt, landet sicher dahinter. Reichweite, Tempo und Schaden bleiben gleich (#299, #314)
- **Die Fledermaus hat Klang:** In Klangpaket 1 flattert eine wache Fledermaus hörbar, aber nur bis etwa 8 m Abstand, und auch mehrere zusammen höchstens alle 0,25 s – kein Dauerrattern. Vor dem Spucken klingt ein heller Glockenton, vor dem Sturzflug ein Kreischen mit fallendem Ton; beides klingt anders als der Ghul und kommt aus jeder Entfernung durch. Dazu Spucken, Biss, Sturz ins Leere, Treffer und Tod. Triffst du sie mit der Klinge, hörst du ihren eigenen Treffer statt des Klingentreffers. Mit den alten Geräuschen (**K**) bleibt sie still (#297, Teil von #271)
- **Mehr Geräusche in Klangpaket 1:** Sprung, Dash und Ausweichen, eine Landung nach spürbarem Fall (ab 0,6 m – kleine Stufen bleiben still), ein Laut, wenn ein Ghul dich entdeckt, ein leiser Glockenton, wenn die Weihe endet, und ein Klick in den Menüs. Ghul-Entdecken und -Sterben haben je eine zweite Variante; Ghul-Ankündigung und Schmerzlaut sind tiefer, ein Schritt hat eine eigene Tonhöhe. Mit den alten Geräuschen (**K**) bleibt all das still (#291, Teil von #271)
- **Musterraum (nur Debug-Version):** `dotnet run --project src/SilverAndAsh.Game -- --showroom` startet statt eines Laufs einen Rundgang durch alle Bauteile: jede Türart und die rissige Wand einmal in jeder Wandvariante (A, B, C), im schmalen Gangteil, wo dich die Laterne an jeder Tür vorbei beleuchtet; je Wandvariante dazu ein Raumteil in Raumhöhe; dazu die vier Kisten, die Wendeltreppe, Ghul, Fledermaus und Gorm als harmlose Ausstellungsstücke, Waffen, Silber, Blutphiole und Weihmunition am Boden. Du kannst nicht sterben, hast alle Waffen, und über jedem Stück steht ein kleines Schild – **L** blendet die Schilder aus und ein. Für Grafikdesigner und zum Prüfen neuer Modelle; neue Stücke sind ein Listeneintrag ([docs/musterraum.md](docs/musterraum.md), #283)
- **Klangpaket 1:** Neue Geräusche für Pistole, geweihte Schüsse (mit leiser Glocke), Nachladen, Klingenschwung und Klingentreffer, linke und rechte Schritte, Schmerz, Ghul-Ankündigung und -Tod, Türen, Kisten und das Aufheben von Silber, Blutphiolen, Weihmunition und Schlüsseln. Ein Schwung ins Leere klingt anders als ein Treffer; trifft er, hörst du nur den Treffer. **K** wechselt zwischen Klangpaket 1 und den alten Geräuschen, oben steht kurz der Name. Das Spiel startet mit Klangpaket 1 (#290, Teil von #271)
- **Türen mit Gestalt:** Holz-, Eisen- und Bosstüren sind jetzt Modelle mit Textur in einem steinernen Spitzbogen-Portal; die rissige Wand zum Geheimraum zeigt ihre Risse. Drückst du **F**, schwingen die Flügel von dir weg auf – die Holztür in 0,9 s, die verschlossene Eisentür in 1,3 s (ihr Schloss ist mit dem Aufschließen verschwunden), das Bosstor in 2,2 s. Durchgehen kannst du wie bisher sofort. Türen in Ost-West-Gängen stehen quer zum Gang (#245)
- **Die rissige Wand sitzt im Gemäuer:** Die Wand zum Geheimraum zeigt jetzt auf jeder Seite einen Riss, der in den Stein derselben Mauer gemalt ist – ohne sichtbare Kante, in der Spielart der Wand des Raums davor, mit etwas Schutt an ihrem Fuß. Zerschlägst du sie, fliegen Brocken aus genau diesem Stein (#246)
- **Musik:** Im Titelbildschirm läuft ein eigenes Thema – tiefe Glocke und Chor in e-Moll. Im Lauf liegt eine kargere, leisere Fassung derselben Klangwelt unter den Katakomben; sie ersetzt das bisherige Ambiente. Beim Wechsel zwischen Titel und Lauf blendet die eine Musik aus, dann die andere ein; im Esc-Menü hält sie an und spielt danach weiter, auf dem Endbildschirm verklingt sie. **N** schaltet auch die Musik stumm. Beide Stücke sind vorläufig (Teil von #271)
- **Erste Geräusche:** Das Spiel hat Ton. Die Pistole knallt, die Silberklinge klingt, sobald sie trifft (ein Schlag ins Leere bleibt stumm, die Keule vorerst auch), deine Schritte hallen etwa zweimal pro Sekunde, solange du am Boden läufst, und ein Ghul kündigt seinen Hieb mit einem Laut an. Klinge und Schritte haben mehrere Varianten, nie zweimal dieselbe hintereinander. **N** schaltet den Ton aus und wieder an. Fehlt ein Klanggerät oder eine Datei, läuft das Spiel stumm weiter und schreibt eine Warnung ins Log (#267)
- **Kisten mit Textur:** Alle vier Kistenstufen sind neu gemalt (Atlas 128 × 128): gedecktes Holz mit Metallbändern und Nieten, die Bretter laufen über die Deckelwölbung. Die Stufe zeigt sich nur noch als Akzent (Stahlblau, Gold, Blutrot). Form, Größe und Deckel bleiben gleich (#258).
- **Nachlade-Hinweis:** Ist das Magazin der Fernwaffe in der Hand leer, steht „Reload [R]“ gut lesbar knapp unter dem Fadenkreuz. Er verschwindet, sobald du nachlädst oder zu einer geladenen Waffe wechselst
- **Jeder Raum hat sein eigenes Gemäuer:** Wände, Böden und Decken der Katakomben gibt es jetzt in drei Spielarten, und jeder Raum wählt für jede Fläche eine davon – derselbe Seed zeigt immer dasselbe Bild. Ein Gang sieht aus wie der Raum, an dem er beginnt. Kopfsteinpflaster liegt nur im Startraum, in Schatzkammern und Geheimräumen, nie dort, wo gekämpft wird
- **Gorm hat Gestalt:** Statt Quadern stapft ein grüner Riese mit eiserner Maske, Keule und schwerer bleicher Kette auf dich zu; in Ruhe schleift die Kette mit dem Haken über den Boden. Vor dem Kettenschwung wirbelt er sie über dem Kopf, beim Schwung reicht sie genau so weit, wie sie trifft. Vor dem Wurf glüht der Haken rot, im Flug zieht er die Kette hinter sich her. Vor dem Hammerschlag reißt er die Keule hoch, eine festgesteckte Keule glüht. In Phase 2 brechen rote Male auf Körper und Maske auf. Er blitzt bei Treffern (rot am Kopf) und fällt tot auf den Rücken
- **Die Fledermaus hat Gestalt:** Statt Quadern flattert jetzt eine bleiche Fledermaus mit Lederflügeln, Ohren und Fangzähnen; der äußere Flügel schlägt dem inneren nach. Kreischt sie, richtet sie sich auf, reckt die Flügel steil nach oben und reißt das Maul auf – daran erkennst du den kommenden Sturzflug. Im Sturzflug legt sie die Flügel nach hinten und stößt mit dem Kopf voran herab. Sie blitzt bei Treffern, glüht vor dem Spucken rot und trudelt tot zu Boden
- **Pistole und Silberklinge in den Mantelärmeln:** Statt grauer Quader hältst du eine Steinschlosspistole mit Holzschaft und Silberbeschlägen und eine Silberklinge mit Parierstange; beide ragen aus dunkelblauen Mantelärmeln mit heller Rüschenmanschette, ohne Hände. Der Unterarm zeigt dabei immer zur Schulter: Holst du mit der Klinge aus oder schlägst zu, dreht sich nur die Manschette mit der Waffe, der Ärmel läuft weiter nach unten aus dem Bild (#265). Der kleine Hahn der Pistole trägt einen hellen Feuerstein. Beim Schuss reißt die Pistole hoch und der Hahn fällt nach vorn; ist sie geweiht, glüht der Lauf. Die Klinge holt beim Aufladen nach links aus, hebt dabei die Spitze und zittert voll aufgeladen. Keule, Gewehr und Donnerbüchse sind vorerst noch Quader
- **Echte Truhen:** Die Kisten aller vier Stufen sind jetzt Holz- und Metalltruhen mit Bändern und Nieten. Die Bettlerkiste ist schlicht, die Wächterkiste trägt einen Griff, die Königskiste eine Krone und die Vampirkiste Dornen; sie pulsiert weiter rot. Das Messingschloss sitzt vorn und verschwindet beim Aufschließen. Der Deckel klappt am Scharnier nach hinten auf, und innen ist es dunkel. Jede Kiste zeigt mit ihrer Vorderseite zur Raummitte
- **Steinerne Katakomben:** Wände, Böden und Decken zeigen jetzt Bruchsteinmauern, abgetretene Bodenplatten und ein verrußtes Gewölbe statt einfarbiger Flächen – grobe, ungefilterte Pixel wie beim Ghul. Die Steinlagen laufen ohne Naht über den ganzen Raum und liegen ringsum auf gleicher Höhe, auch an Stufen
- **Der Ghul hat Gestalt:** Statt Quadern kriecht jetzt ein ausgemergelter Mensch mit Textur auf allen vieren auf dich zu, weich schattiert im Licht deiner Laterne. Beim Ausholen richtet er sich auf, reißt die Arme hoch und öffnet das Maul weit; er blitzt bei Treffern (rot bei Kopftreffern), zuckt zurück und sackt beim Tod zur Seite
- **Wandbruch mit Wucht:** Bricht die rissige Wand zum Geheimraum, fliegen Steinbrocken heraus (die meisten von dir weg), poltern zu Boden und versinken; eine Staubwolke steigt auf, und das Bild ruckt kurz
- **Waffen-Plätze:** Du trägst eine Nahkampfwaffe und bis zu **drei Fernwaffen**. Wechseln mit dem **Mausrad** oder **1 / 2 / 3**; leere Plätze werden übersprungen, ein Wechsel bricht das Nachladen ab, jede Waffe behält ihr Magazin. Unten rechts stehen die Plätze, umrandet in der Farbe ihrer Stufe. Neue Waffen kommen mit den nächsten Updates
- **Club (Keule, Worn):** neue Nahkampfwaffe, langsamer als die Klinge, aber härter (50 Schaden). Er stößt Ghule und Fledermäuse **3 m zurück**, voll aufgeladen **6 m**; Wände halten sie auf, Gorm bleibt stehen. Zu finden ist er mit dem Beute-System; zum Ausprobieren startet die Debug-Version mit `--give club`
- **Jede Nahkampfwaffe lässt sich aufladen**, und jeder voll aufgeladene Nahkampfschlag bricht die rissige Wand zum Geheimraum
- **Rifle (Gewehr, Forged):** neue Fernwaffe für schwere, gezielte Schüsse: 80 Schaden, Kopftreffer ×3, 80 m Reichweite, 4 Schuss, 1,4 s zwischen den Schüssen, 2,5 s Nachladen. Ein Schuss **durchschlägt einen Gegner** und trifft den dahinter; Wände halten ihn auf. Zum Ausprobieren startet die Debug-Version mit `--give rifle` (Taste 2 nimmt sie in die Hand)
- **Blunderbuss (Donnerbüchse, Forged):** neue Fernwaffe für kurze Distanz: Ein Schuss feuert **9 Kugeln** in einem 20°-Kegel, je 12 Schaden, **wirksam bis 12 m**, darüber nichts. 1 Schuss, dann 1,8 s Nachladen. Die Streuung hängt vom Seed ab. Zum Ausprobieren: `--give blunderbuss`
- **Inventar (G):** **G** öffnet und schließt es; das Spiel läuft weiter, **Bewegen oder ein Treffer schließt es**. Öffnest du es im Laufen, läufst du weiter und es bleibt offen; erst eine neu gedrückte Richtung, Springen oder Shift schließt es. Oben stehen die Waffen in der Hand, darunter **sechs Plätze** für Waffen, die du nicht ausgerüstet hast, rechts die Items (noch leer). Alle Rahmen in der Farbe ihrer Stufe. **Linksklick** rüstet aus (Nahkampf tauscht, Fernwaffe auf einen freien Platz, sonst Tausch mit der in der Hand), **Rechtsklick** lässt fallen – die Waffe liegt dann leuchtend am Boden. Fernwaffen behalten in der Tasche ihr Magazin; ein laufendes Nachladen bricht beim Verstauen ab. Die letzte Fernwaffe kann man nicht fallen lassen. Zum Ausprobieren legt `--give` überzählige Waffen ins Inventar (z. B. `--give club --give rifle --give blunderbuss --give pistol`)
- **Bewegte Vorschau (Werkzeug):** Die Debug-Version filmt mit `--clip <szene>` feste Bewegungsabläufe als nummerierte Bilder – Klinge aufladen und zuschlagen, schneller Hieb, Pistolenschuss, Nachladen, Holztür und Bosstor öffnen. `tools\clip.ps1` setzt daraus mit ffmpeg ein MP4 oder GIF zusammen, auf Wunsch in halber Geschwindigkeit. So lassen sich Animationen ohne Spielen ansehen (#286)
- **Qualitätsbericht (Werkzeug):** `python tools/crap-report.py` misst einmal je Meilenstein die Testabdeckung von `SilverAndAsh.Core` und schreibt einen kurzen Bericht mit dem CRAP-Score je Methode – die riskantesten Stellen zum Ändern, Summen je Namensraum und der Vergleich zum vorigen Bericht. Eine Landkarte, keine Schranke; der normale Testlauf bleibt unverändert. Erster Bericht und Erklärung in [docs/qualitaet/](docs/qualitaet/README.md) (#320)

### Geändert
- **HUD und Menüs feiner gezeichnet:** HUD, Karte, Ausrüstung und die übrigen Menüs zeichnen ihre Rahmen, Balken und Zeichen jetzt aus Bildteilen in doppelter Auflösung (Richtung A „harte Pixel“). Lage und Größe auf dem Bildschirm bleiben gleich; die Zeichen von Waffen und Gegenständen sind etwas größer und heller, Rahmen und Reiter vorerst schlicht ohne Giebel und Nieten (vorläufig). Fehlen die feinen Bildteile, nimmt das Spiel die bisherigen (#409)
- **Nachlade-Schleier im Fenster, Skillpunkt-Schein dezenter:** Der Schleier beim Nachladen liegt nur noch innerhalb des Waffenfensters, nicht mehr auf dem Silberrahmen, und seine helle Kante wandert von links nach rechts. Der goldene Schein am Bildrand bei einem neuen Skillpunkt ist etwa halb so stark und halb so breit (vorläufig, bis zum nächsten Spieltest) (#389)
- **Große Munitionszahl über den Waffen:** Neben den Waffenbildern stehen keine Ziffern mehr. Stattdessen steht mittig über den Waffenfeldern groß die Munition der gewählten Waffe und klein daneben die Magazingröße, z. B. „6 / 7“ – in Silber, beim Nachladen leiser. Solange du die Nahkampfwaffe auflädst oder schlägst, verschwindet die Zahl. „Consecrated“ steht jetzt darüber. Neue Foto-Szenen `hud-blunderbuss` und `hud-melee` (#388)
- **Stufe 0 am Start, höchstens drei Fledermäuse je Raum:** Ein neuer Lauf beginnt auf Stufe 0, der erste Aufstieg führt auf Stufe 1; wie viel Erfahrung jede Stufe braucht und wann es einen Skillpunkt gibt, bleibt gleich – nur die Zählung beginnt eins tiefer (auch in Charakterwerten, Endbildschirm und Balance-Report). Kein Raum hat mehr als drei Fledermäuse; was das Budget eines Raums darüber hinaus hergibt, kauft Ghule. Die Grenze steht in `balance.json` unter `difficulty.maxBatsPerRoom` und ist ein vorläufiger Wert (#391)
- **HUD-Feinschliff nach dem Spieltest:** Die kleinen grauen Innenrahmen in den HUD-Fenstern und der senkrechte rote Strich im Lebensbalken sind weg. Der Erfahrungsbalken ist golden. Solange ein Bonus wartet, steht unter Stufe und Erfahrung gelb „[X] to level up“; die Werte-Tafel in Karte und Ausrüstung beginnt darunter und verdeckt ihn nicht. Auch die Stufe hat keinen hellen Innenring mehr. Alle HUD-Fenster haben jetzt denselben Abstand zueinander und zum Bildrand, und was in einem Fenster steht, ist darin gleichmäßig verteilt. Neue Foto-Szene `map-skillpoint` (#387)
- **Karte und Ausrüstung sind ein Menü:** Beide Seiten tragen oben dieselben zwei Reiter „[Tab] Map“ und „[G] Equipment“ an genau derselben Stelle; im offenen Menü blätterst du mit **Tab**/**G** oder per Mausklick auf den Reiter. Links unter Stufe und Erfahrungsbalken stehen auf beiden Seiten deine Werte und Boni – ohne schwarzen Grund, nur in einem dünnen Rahmen. Die Menü-Tafeln (auch Pause, Einstellungen und Titel) sind jetzt zu 70 % deckend, sodass das Spiel durchscheint. Bei offener Karte ist die Maus frei, Umsehen mit der Maus geht dann nicht (#390)
- **Neues HUD im abgenommenen Entwurf:** Die Anzeigen am Rand stehen jetzt in silbernen Zierrahmen aus den HUD-Teilen und in der Schrift „Kapitell“. **Unten** links die Items, in der Mitte der Lebensbalken mit fünf Ausdauer-Abschnitten – beide **ohne Zahlen** –, rechts die vier Waffen mit ihrer Munition (die gewählte leuchtet, beim Nachladen wird die Zahl grau). **Oben** links Stufe, Erfahrungsbalken und – solange ein Bonus wartet – ein Stern mit der Taste **X**, in der Mitte Name und Lebensbalken des Bosses (nur im Kampf), rechts Silber und Schlüssel. Nimmst du etwas auf, steht über den Items drei Sekunden lang „Picked up …“. Das HUD wächst mit dem Bild und bleibt in jedem Seitenverhältnis an seinen Ecken; es deckt weniger als ein Zehntel des Bildes ab. Titel und Version stehen nicht mehr im Spielbild. Neue Foto-Szenen `hud-calm`, `hud-boss`, `hud-skillpoint`, `hud-pickup` (#369)
- **Eigene Pixelschrift:** Alle Texte im Spiel – HUD, Titelbild, Menüs, Einstellungen, Hinweise wie „[F] Take Rifle (Forged)“, Fenster, Karte und Ausrüstung – stehen jetzt in der eigenen Schrift „Kapitell“ statt in Raylibs Standardschrift, nur ganzzahlig vergrößert mit harten Pixeln. Stufe („LV 2“) und Munition sind doppelt so groß wie die Grundschrift (Versalien 14 Pixel hoch). Ein Zeichen, das die Schrift nicht hat, erscheint als hohles Kästchen statt zu fehlen. Die Anordnung der Seiten bleibt vorerst wie bisher (#367)
- **Karte und Ausrüstung in einem Menü:** **Tab** öffnet die Seite „Map“, **G** die Seite „Equipment“ (das bisherige Inventar); dieselbe Taste schließt, die andere blättert um, **Esc** schließt. Oben zeigen zwei Reiter, welche Seite offen ist; der Mauszeiger erscheint nur auf der Ausrüstungsseite. Während der Bonus-Wahl öffnet sich das Menü nicht, und bei offenem Menü öffnet **X** die Bonus-Wahl nicht (#348)
- **Kartenseite mit Werten und Boons:** **Tab** öffnet und schließt jetzt die Kartenseite, statt gehalten zu werden; **M** tut nichts mehr. Links am Rand stehen deine Werte – Stufe, Leben und Ausdauer als aktueller und höchster Wert (die Ausdauer in Balken, z. B. „2.4 / 5 bars“; die Zahlen laufen mit, solange die Seite offen ist), Ausdauer-Erholung, Schaden der Nahkampfwaffe und der Fernwaffe in der Hand (ein Körpertreffer, mit deinen Boons), Tempo und Erfahrungsbonus – und darunter die gewählten Boons mit Anzahl und Gesamtwirkung, rechts die Karte wie bisher. Das Spiel läuft weiter, Laufen und Treffer schließen die Seite nicht, **Tab** oder **Esc** schon. Die Foto-Szene `map` zeigt die Seite mit fünf gewählten Boons (#347)
- **Fenster schließt nur noch du:** Inventar (**G**) und Bonus-Wahl (**X**) bleiben offen, während du läufst, springst, ausweichst oder getroffen wirst – so kannst du auch mitten im Kampf in Ruhe einen Bonus wählen. Zu gehen sie mit ihrer eigenen Taste oder mit **Esc**. Esc schließt zuerst ein offenes Fenster (auch die offene Karte) und öffnet erst beim nächsten Druck das Pause-Menü (#346)
- **HUD aufgeräumt:** Die beiden Balken neben dem Fadenkreuz (Bereitschaft von Klinge und Pistole) und der Aufladebalken darunter sind weg. Die Schriftzüge haben jetzt feste Zeilen und liegen nie mehr übereinander: unter dem Fadenkreuz zuerst „Reload [R]“, darunter „[F] …“; unten links steht „[X] Choose boon“ über der Stufe, die Weihezeit bleibt über dem Leben. Die Bildrate unten links zeigt nur noch die Debug-Version. Neue Foto-Szene `hud-texts` mit allen Schriftzügen zugleich (#340)
- **Türrahmen aus dem Stein der Mauer:** Holz-, Eisen- und Bosstür stehen jetzt in einem Rahmen, der auf jeder Seite aus dem Stein der Wand des Raums davor gemauert ist – in derselben Spielart, ohne sichtbare Kante; nur Bogen, Kämpfer und Sockel sind heller behauen. Das Bosstor trägt einen hellen Werkstein und hebt sich deutlich ab, Holz- und Eisentür fügen sich ruhiger in die Wand. Flügel, Öffnen und Durchgehen bleiben wie bisher (#303, Teil von #247)
- **Klingenschlag mit Gewicht, Pistole kippt beim Nachladen:** Der Klingenschlag knickt den Ärmel nicht mehr ab (kein dunkles Loch an der Manschette, kein brauner Fleck am Griff): kleinerer Ausschlag, dafür schnell hinaus, kurz Halten im Treffer, Nachfedern und ruhiger Rückweg – Dauer und Trefferzeit unverändert. Beim Nachladen (R) kippt die Pistole nach rechts, die Mündung steigt, die Hand klopft zweimal beim Einlegen, am Ende springt der Hahn mit einem kleinen Ruck. Ein Waffenwechsel mitten im Nachladen blendet die Bewegung aus, statt zu springen; die Nachladebewegung gehört zur gezeichneten Waffe (die Büchse löst sie nicht an der Pistole aus). Neue Bewegungs-Szenen: `pistol-shot-in-swing`, `pistol-reload-in-swing`, `pistol-reload-cancelled`, `rifle-reload-to-pistol`. (#292)
- **Gorm holt so weit aus, wie der Designer ihn gezeichnet hat:** Vor dem Hammerschlag reißt er die Keule hoch über den Kopf, vor dem Kettenschwung reckt er den Kettenarm steil nach oben – sein Bossraum ist 6 m hoch, da ist Platz. Beim Schwung bleibt die Keule über dem Boden, und vor dem Wurf hängt der glühende Haken nicht mehr in den Boden, solange die Faust noch tief ist, sondern reicht flach nach vorn. Zeiten, Reichweiten und Trefferzonen bleiben gleich (#254)
- **Körnung als Standardbild:** Die 3D-Welt wird jetzt in 15-Bit-Farbe mit einem festen, ruhigen Punktmuster gezeichnet, wie auf alten Konsolen – auf Steinen, Figuren, Beute und Waffen in der Hand. Nebel und Laterne bleiben, die Anzeigen am Rand bleiben scharf. Die Debug-Version zeigt mit `--look 1` das Bild ohne Körnung
- **Nachladen:** Die Pistole hat ein Magazin mit **7 Schuss**. **R** lädt nach (1,5 s); ist das Magazin leer, startet ein Schuss das Nachladen. Munition gibt es unbegrenzt. Unten rechts steht die Munition („6 / 7“, leer rot mit „[R]“), beim Nachladen „Reloading...“ mit einem Balken. Dafür schießt sie etwas schneller (0,7 statt 0,8 s)
- **Bonus-Wahl ohne Stillstand:** Ein Stufenaufstieg hält das Spiel nicht mehr an. Unter der Stufenanzeige blinkt „[X] Choose boon“ (bei mehreren mit Anzahl). **X** öffnet die Wahl, **1–4** oder ein **Klick** wählt, das Spiel läuft weiter, und du kannst dabei laufen. Mehrere Aufstiege kommen nacheinander. Nur ein Treffer schließt die Wahl, der Bonus wartet weiter
- **Kisten sind fest:** Man läuft nicht mehr durch Kisten hindurch, kann aber auf sie springen (0,7 m). Steigt eine Kiste unter dir aus dem Boden, hebt sie dich an. Ghule und Gorm laufen um Kisten herum; Fledermäuse fliegen darüber. Noch versteckte Kisten blockieren nicht

### Behoben
- **Die Keule ist fertig gebaut:** Zwischen Griff und Kopf klafft keine Lücke mehr, der Kopf hat keine offene Seite mehr und sitzt gerade auf dem Griff statt um 25 Grad geknickt. Neue Regel in der Art Bible: 3D-Modelle sind rundum geschlossen; `tools/models/check_closed.py` prüft das.
- **Gorms Beute liegt nie mehr unter der Treppe:** Starb Gorm nahe der Saalmitte, lag sein Ausrüstungsstück dort, wo die Wendeltreppe aufsteigt – sobald sie stand, zeigte **F** nur noch „Climb the stairs“, und das Stück ließ sich nicht mehr nehmen. Jetzt rückt es in diesem Fall knapp neben die Treppe, auf Gorms Boden und nie in eine Wand (#338)

## [0.10.0] – 2026-11-16 – Boss & Bewegung

Jede Ebene hat jetzt ihren Höhepunkt: Gorm, the Chained, ein Troll mit Keule und Kette, drei angekündigten Angriffen und einer zweiten Phase. Nach seinem Sieg führt eine Wendeltreppe hinaus. Dazu ein Luft-Dash in Blickrichtung, Gegner, die jedes Mal anders stehen, eine Karte mit Symbolen und die zweite Balancing-Runde.

### Hinzugefügt
- **Gorm, the Chained:** Im Bossraum wartet jetzt ein Troll, 3,2 m groß, mit Keule und Kette (Platzhalter aus Kästen).
  - Betrittst du den Bossraum, erscheint oben sein Lebensbalken (3600 Leben).
  - Er geht langsam auf dich zu und **greift an**, jeder Angriff mit deutlicher Ankündigung und einer Antwort:
    - **Hammerschlag** (Keule hoch, roter Kreis am Boden): Eine Schockwelle läuft über den Boden → **springen**. Verfehlt er, steckt die Keule 2 s fest: Er beugt sich vor und nimmt doppelten Schaden.
    - **Kettenschwung** (Kette wird über dem Kopf angedreht, so lang, wie sie reicht): Die Kette streicht in 0,6 s auf Brusthöhe von seiner Linken über vorne nach rechts → **Dodge** (geduckt), wenn sie ankommt, oder **hinter ihn**; Springen hilft nicht.
    - **Kettenwurf** (Haken glüht rot): Der Haken fliegt dorthin, wo du standest → **zur Seite** oder hinter eine Säule. Trifft er, zieht er dich heran.
    - Nach jedem Angriff ruht er kurz; nie zweimal hintereinander derselbe Angriff.
  - **Phase 2:** Bei halbem Leben brüllt er 1,5 s lang (Kopf in den Nacken, der Boden bebt) und greift solange nicht an. Danach läuft, kündigt an und ruht er 40 % schneller. Jeder Hammerschlag löst dann **6 Steine von der Decke**: Ihr dunkler Schatten wächst 1 s lang am Boden um dich herum, dann schlägt der Stein ein (25 Schaden) → **aus dem Schatten gehen**.
  - Sein Kopf ist ein Schwachpunkt: Ein Pistolenschuss dorthin macht 150 % Schaden.
  - Er hinterlässt 100–150 Silber und eine Blutphiole.
- **Wendeltreppe und neuer Sieg:** Nach Gorms Tod fährt in der Mitte des Bossraums eine Wendeltreppe aus dem Boden. „[F] Climb the stairs“ gewinnt den Durchlauf. Der Endbildschirm und `runs.log` zeigen, ob Gorm besiegt wurde und wie lange der Kampf dauerte
- **Luft-Dash in Blickrichtung:** W + Shift in der Luft dasht dorthin, wohin man schaut, auch schräg nach oben, z. B. auf höhere Stellen in den 6 m hohen Räumen. Andere Richtungen dashen wie bisher flach. Gleiche Strecke und Ausdauer, einmal pro Sprung; an Decke und Boden endet der Dash
- **Blutphiolen auf der Karte:** Eine liegen gelassene Blutphiole in einem schon entdeckten Raum erscheint auf der Karte als roter Punkt. Hebt man sie auf, verschwindet der Punkt
- **Kisten und Symbole auf der Karte:** Ungeöffnete Kisten in entdeckten Räumen erscheinen in der Farbe ihrer Stufe, verschlossene mit Schloss; geöffnete verschwinden. Verschlossene Türen tragen ein Schloss, die Bossraum-Tür einen Totenkopf

### Geändert
- **Sieg:** Der Durchlauf ist nicht mehr gewonnen, sobald der Bossraum geräumt ist, sondern erst, wenn man die Treppe nimmt. Im Bossraum gibt es keine Ghule und Fledermäuse mehr (Gorm kämpft allein)
- **Gegner stehen jedes Mal anders:** Statt fester Plätze bekommt jeder Kampfraum ein Gegner-Budget, das weiter vom Start wächst. Davon kauft er Ghule und Fledermäuse (hinten mehr Fledermäuse) und stellt sie auf zufällige freie Felder, mit Abstand zu den Türen und zueinander. Insgesamt etwa so viele Gegner wie bisher. Einstellbar in `balance.json` unter `difficulty`
- Der **Startraum hat nur noch eine Tür**. Der Ausgang liegt je Durchlauf in einer anderen Richtung, und der Weg zum Boss wird etwas länger
- **Karte:** **Tab halten** zeigt sie, **M** schaltet sie an und aus, damit sie beim Laufen offen bleiben kann. Sie ist jetzt durchsichtiger, sodass man das Spiel dahinter gut sieht
- **Balancing-Runde 2:** Gorm ist zäher und härter (3600 Leben, mehr Schaden, kürzere Pausen), Phase 2 schneller und mit mehr Steinen. Ghule und Fledermäuse sind gefährlicher: Ghule kriechen schneller (5 m/s), schlagen härter (30) und erholen sich schneller; Fledermäuse beißen und spucken härter (20) und öfter. Hinten etwas mehr Gegner. Ein Durchlauf mit allen Räumen dauert etwa 12 Minuten, Gorm um 1,5 Minuten. Protokoll in `docs/balancing.md`

## [0.9.0] – 2026-11-15 – Schatz & Wirtschaft

Erkunden lohnt sich jetzt: Gegner lassen Silber und Schlüssel fallen, Kisten in vier Stufen warten in Kampfräumen, Schatzräumen und einem versteckten Raum hinter einer rissigen Wand. Dazu die erste Balancing-Runde mit eigenem Werkzeug.

### Hinzugefügt
- **Silber**, die Währung des Durchlaufs: Getötete Ghule und Fledermäuse lassen in der Hälfte der Fälle 1–3 Silber fallen, als kleinen Münzstapel am Boden. Drüberlaufen sammelt es ein. Oben rechts zeigt der Zähler „SILVER“; der Endbildschirm und `runs.log` nennen das gesammelte Silber. Ein neuer Durchlauf beginnt bei 0. Einstellbar in `balance.json` unter `silver`. Ausgeben kann man es ab v0.11 beim Händler
- **Schlüssel und verschlossene Türen:** Ein geräumter Kampfraum lässt mit 10 % einen goldenen Schlüssel in seiner Mitte liegen, ein getöteter Gegner mit 2,5 %. Drüberlaufen sammelt ihn ein, der Zähler „KEYS“ steht unter dem Silber. Verschlossene Türen sind aus dunklem Eisen mit Vorhängeschloss (auf der Karte golden): Mit Schlüssel öffnet **F** sie („[F] Unlock (1 key)“) und verbraucht einen, ohne Schlüssel steht dort „Locked - needs a key“. Schlüssel gelten nur für den Durchlauf. Einstellbar in `balance.json` unter `keys`. Die verschlossenen Türen setzt der Generator mit den Schatzräumen ein
- **Kisten mit vier Stufen:** Beggar's (Holz), Guard's (stahlblau), King's (gold) und Vampire's Chest (blutrot), schon von weitem zu unterscheiden. Etwa jeder vierte Kampfraum hat eine; sie steigt aus dem Boden, sobald der Raum geräumt ist, und wird weiter vom Start entfernt etwas besser. **F** öffnet sie („[F] Open King's Chest“); höhere Stufen sind öfter verschlossen und brauchen dann einen Schlüssel. Heraus springen Silber (Beggar's 10–20 bis Vampire's 80–120) und mit etwas Glück eine Blutphiole. Alle Werte in `balance.json` unter `chests`
- **Schatzräume:** Jede Ebene hat einen Schatzraum und einen zweiten hinter einer **verschlossenen Tür**. Beide sind gegnerfreie Sackgassen mit 1–2 Kisten, hinter dem Schloss eher King's und Vampire's Chests. Sie liegen nie direkt am Start und nie hinter dem Boss; der Boss ist immer ohne Schlüssel erreichbar. Auf der Karte sind Schatzräume golden. Anzahl und Kisten-Anteile stehen in `balance.json` (`layout`, `chests`)
- **Secret Room:** Jede Ebene versteckt einen Raum hinter einer Wand mit feinen Rissen an einem Kampfraum. Nur ein **voll aufgeladener Klingenhieb** bricht sie auf; normale Hiebe, Schüsse und F tun nichts. Bis dahin verrät auch die Karte nichts. Drinnen steht eine Kiste: Guard's, King's oder mit Glück Vampire's Chest. Auf der Karte ist der Secret Room violett
- **Für die Entwicklung:** Balance-Report (`tools/SilverAndAsh.BalanceReport`) baut 500 Ebenen aus `balance.json` und zeigt Erwartungswerte (Räume, Gegner, Silber, Schlüssel, Kisten, Heilung, grobe Spielzeit), auch als Vorher/Nachher-Vergleich. Siehe `docs/balancing.md`

### Geändert
- **Balancing-Runde 1:** Eine Ebene ist länger und schwerer.
  - 12–14 statt 7–9 Kampfräume, schon nahe am Start mehr Gegner und erste Fledermäuse.
  - Ghule halten drei statt zwei Klingenhiebe aus und schlagen härter zu; Fledermäuse überstehen einen Pistolenschuss.
  - Blutphiolen fallen seltener und heilen 30 statt 50.
  - Schlüssel sind an die größeren Ebenen angepasst.
  - Alle Werte und Gründe stehen in `docs/balancing.md`.
- **Ghul-Köpfe** zählen nur noch dort als Kopftreffer, wo der Kopf gezeichnet ist, nicht mehr in der ganzen vorderen Körperhälfte.

### Behoben
- Gegner konnten dem Spieler in Schatz- und Secret-Räume folgen, z. B. nachdem man sie durch eine offene Tür angeschossen hatte. Diese Räume sind jetzt **Schutzräume**: Ghule und Fledermäuse betreten sie nie und greifen niemanden darin an
- Ein Durchlauf stand doppelt in `runs.log`, wenn man vom Endbildschirm zum Titel ging. Jetzt steht er genau einmal darin

## [0.8.0] – 2026-11-15 – Run-Grundgerüst

Aus dem Testlevel wird ein Durchlauf: Jede Runde baut eine neue Ebene aus handgebauten Räumen, Kampfräume schließen sich zur Arena, je weiter man kommt, desto mehr wartet – mit Titel, Pause, Karte und einer Statistik am Ende.

### Hinzugefügt
- **Durchläufe auf Ebene 0 „The Catacombs“:** Das Spiel startet auf einer zufällig erzeugten Ebene aus handgebauten Räumen, mit Gängen und Schleifen, der Bossraum am weitesten vom Start. Beim Start erscheint kurz der Name der Ebene. Nach dem Tod beginnt ein neuer Durchlauf mit neuem Grundriss. Ist der Bossraum geräumt, ist der Durchlauf gewonnen. Mit `--seed <Zahl>` immer derselbe Grundriss; die alten Testlevel gibt es weiter mit `--level test` / `--level heights`
- Türen zwischen den Räumen der erzeugten Ebenen: Alle sind zu und halten Spieler, Gegner, Sicht und Schüsse auf. Mit **F** öffnet man die Tür, auf die man aus der Nähe schaut („[F] Open“); offen bleibt offen. Die Tür zum Bossraum ist dunkel, mit Schädeln und rotem Schimmer
- Kampfräume werden zur Arena: Beim ersten Betreten fallen an allen Türen Gitter, die schlafenden Gegner erwachen. Sind alle tot, heben sich die Gitter und die Tür, durch die man kam, öffnet sich
- Je weiter ein Kampfraum vom Start entfernt ist, desto mehr Ghule und Fledermäuse warten darin; der Bossraum ist voll besetzt. Einstellbar in `balance.json` unter `difficulty`
- Karte auf **M** (halten): zeigt von oben die schon betretenen Räume und Gänge, ihre Türen (zu, offen, vergittert, Boss) und wo man steht und hinschaut. Das Spiel läuft dabei weiter
- Titelbildschirm mit „Start“ und „Quit“
- Esc-Menü: weiterspielen, Mausempfindlichkeit einstellen (bleibt gespeichert), zurück zum Titel, beenden. Solange es offen ist, steht das Spiel still
- Endbildschirm „YOU DIED“ bzw. „VICTORY“ mit der Statistik des Durchlaufs (Zeit, getötete Ghule und Fledermäuse, Schaden ausgeteilt und genommen, erreichte Stufe, geräumte Räume, Seed) und der Wahl „New run“ / „Title“. Jeder Durchlauf wird außerdem als Zeile in `%LOCALAPPDATA%\SilverAndAsh\runs.log` festgehalten

### Geändert
- Nach dem Tod startet der nächste Durchlauf nicht mehr von selbst – erst der Endbildschirm, dann „New run“. Das große „YOU DIED“ mitten im Bild entfällt, nur das Bild färbt sich rot
- Esc gibt nicht mehr nur die Maus frei, sondern öffnet das Menü; ein Klick ins Bild fängt die Maus nicht mehr ein (das macht „Resume“). Auch wenn das Fenster den Fokus verliert, öffnet sich das Menü
- Ist die Silberklinge voll aufgeladen, füllt sich die Ausdauer wieder auf, auch wenn man die Taste weiter hält. Während des Ladens selbst füllt sie sich weiterhin nicht

## [0.7.0] – 2026-11-15 – Stufen & Beute

Kämpfen lohnt sich: Gegner lassen Blutphiolen und Geweihte Munition fallen, Kills bringen Erfahrung, und jeder Aufstieg bietet eine Wahl – dazu alle Werte in einer Datei zum Nachjustieren.

### Hinzugefügt
- Balancing-Datei `assets/balance.json`: Leben, Schaden, Abklingzeiten, Geschwindigkeiten und Zeiten lassen sich ändern, ohne das Spiel neu zu bauen – mit Kommentaren zu jedem Wert. Fehler in der Datei halten das Spiel nicht auf: Es nimmt dann die Standardwerte und schreibt eine Warnung ins Log. In der Testversion lädt F5 die Datei neu. Anleitung: `docs/balancing.md`
- Blutphiolen: Getötete Ghule lassen in 30 % der Fälle, Fledermäuse in 15 % eine Blutphiole fallen – eine kleine, rot pulsierende Flasche, die über dem Boden schwebt. Drüberlaufen heilt 50 Leben (nie über das Maximum), neben der Lebensanzeige steigt „+50“ auf. Bei vollem Leben bleibt sie liegen
- Geweihte Munition: Etwa jeder dritte Drop sind statt einer Blutphiole drei Patronen mit silbern leuchtenden Spitzen. Aufheben (auch mit vollem Leben) lädt sie sofort: 10 s lang schießt die Pistole doppelt so schnell (alle 0,4 statt 0,8 s), der Schaden bleibt gleich. Der Lauf schimmert hell, der Mündungsblitz wird weiß-blau, über der Lebensanzeige läuft „CONSECRATED 10s“ ab (blinkt in den letzten 3 s). Nochmal aufheben startet die Zeit neu; der Tod beendet sie
- Erfahrung und Stufen: Wer einen Gegner tötet, bekommt Erfahrung – 30 für einen Ghul, 20 für eine Fledermaus. Stufe 2 braucht 100, jede weitere ein Viertel mehr als die vorige (100, 125, 156, 195, 244 …); was übrig bleibt, zählt für die nächste. Unten links stehen Stufe und ein goldener Erfahrungsbalken. Der Tod setzt wieder auf Stufe 1
- Stufenaufstieg mit Wahl: Bei jedem Aufstieg hält das Spiel an und „LEVEL UP – choose a boon“ bietet drei zufällige Boni aus einem Pool von vier – kleine Schritte, jeweils vom Grundwert gerechnet und beliebig oft wählbar: Ausdauer-Regeneration +5 %, maximales Leben +5 (sofort geheilt), Schaden +5 % mit allen Waffen, Erfahrung +2,5 %. Jeder Kasten zeigt auch, wie viel man schon hat. Wahl mit 1 / 2 / 3 oder Mausklick (Klicks zählen erst nach einem kurzen Moment, damit ein Angriffsklick nicht aus Versehen wählt). Mehrere Aufstiege auf einmal: nacheinander wählen. Der Tod nimmt alle Boni

## [0.6.0] – 2026-11-14 – Vampirfledermaus

Der Angriff kommt von oben: Vampirfledermäuse kreisen um dich, spucken Blutgeschosse und stoßen herab – alles mit Vorwarnung, alles ausweichbar.

### Hinzugefügt
- Vampirfledermäuse in beiden Testleveln (im Level-Format: `B`): klein, mit schlagenden Flügeln, schweben sie über deinem Kopf – du läufst unter ihnen durch. 30 Leben: ein Pistolenschuss oder ein Klingenhieb, wenn du zu ihr hochschaust. Getroffen blinken sie, tot trudeln sie zu Boden und verschwinden kurz darauf
- Blutgeschosse: glühend rote Kugeln, die man fliegen sieht (12 m/s) und denen man ausweichen kann – Seitschritt, Sprung oder Dodge (geduckt fliegen sie über dich hinweg). Ein Treffer kostet 15 Leben; an Wänden zerplatzen sie. Gegner und Puppen treffen sie nicht
- Fledermäuse greifen an: Sie hängen still, bis sie dich sehen (bis 15 m) oder getroffen werden. Dann kreisen sie um dich – jede mit eigenem Abstand (4–6 m), eigener Richtung und eigenem Auf und Ab –, wechseln alle 2–4 s zufällig die Richtung und finden im Flug ihren Weg um Ecken. Jedes Spiel und jeder Neustart fliegt anders. Etwa alle 2 s spucken sie ein Blutgeschoss – vorher leuchten sie kurz rot auf. Etwa alle 6 s kreischen sie (Flügel hoch) und stoßen dann auf die Stelle herab, an der du beim Kreischen standest: Wer jetzt zur Seite geht oder dodged, lässt sie ins Leere stoßen, sonst beißen sie für 15 Leben. Im Sturzflug sind sie tief – ein guter Moment für die Klinge

### Geändert
- Die Silberklinge trifft nahe Gegner leichter: Ihr Treffkegel beginnt jetzt etwas hinter dir und ist dadurch direkt vor dir schon gut einen Meter breit – auch Gegner seitlich vor dir werden getroffen. Reichweite (2 m) und Winkel (90°) bleiben

## [0.5.0] – 2026-11-11 – Ghul

Der erste echte Gegner: Ghule lauern in den Gängen, jagen dich um Ecken und schlagen zurück – und zum ersten Mal kannst du sterben.

### Hinzugefügt
- Ghule in beiden Testleveln (im Level-Format: `G`): kriechende Gestalten, niedriger als du, mit dem Kopf vorn. 80 Leben – zwei Klingenhiebe oder ein Pistolenschuss auf den Kopf. Sie blinken bei Treffern (blutrot bei Kopfschuss), zucken zurück und brechen bei 0 Leben zur Seite zusammen; kurz darauf ist der Körper verschwunden. Sie lauern still, bis sie dich in weniger als 12 m sehen oder getroffen werden. Dann kriechen sie dir nach (4,5 m/s, etwas langsamer als du), finden den Weg um Ecken und durch Türen, richten sich 0,5 s lang drohend auf und schlagen zu: 20 Schaden, bis 2,4 m weit. Ein kleiner Schritt zurück reicht nicht, wer richtig ausweicht oder dodget, wird verfehlt. Danach brauchen sie kurz Erholung
- Schaden und Tod: Bei jedem Treffer leuchten die Bildränder rot auf und die Kamera zuckt kurz, die Lebensanzeige sinkt. Bei 0 Leben färbt sich das Bild rot, „YOU DIED“ erscheint, und nach 2 s beginnt das Level von vorn – mit vollem Leben und allen Gegnern wie am Anfang

## [0.4.0] – 2026-11-11 – Waffen & HUD

Silber gegen Stroh: Klinge und Pistole treffen Trainingspuppen – mit Kopfschüssen, aufgeladenen Schlägen, Waffen im Bild und einem HUD.

### Hinzugefügt
- Trainingspuppen aus Holz und Stroh in beiden Testleveln (im Level-Format: `D`). Man läuft nicht durch sie hindurch. Sie haben 100 Leben, blinken bei Treffern hell auf, kippen bei 0 vom Spieler weg um und stehen nach 3 s wieder auf
- Silberklinge (linke Maustaste): trifft alles in einem Kegel vor dir – 2 m weit, 90° breit, auch nach oben und unten –, aber nicht durch Wände. 40 Schaden, ein Schlag alle 0,4 s; drei Treffer werfen eine Trainingspuppe um
- Pistole mit Silberkugeln (rechte Maustaste): trifft sofort genau dort, wohin du schaust – das erste Ziel bis 50 m. 35 Schaden, ein Schuss alle 0,8 s; drei Treffer werfen eine Trainingspuppe um. An Wänden, Boden und Decke bleibt ein Einschlag zurück: kurzer Funke, dann einige Sekunden ein dunkler Fleck
- Silberklinge aufladen: Linke Maustaste halten, beim Loslassen wird zugeschlagen. Der Schaden steigt linear bis 200 % nach 1 s Halten (80 statt 40), das kostet bis zu einem Stamina-Segment. Reicht die Stamina nicht, lädt die Klinge nur so weit wie möglich. Die Klinge holt dabei sichtbar aus und zittert, wenn sie voll ist
- Kritische Treffer: Ein Pistolenschuss auf den Kopf einer Trainingspuppe macht 300 % Schaden (105) und wirft sie mit einem Schuss um; die Puppe blitzt dabei blutrot statt weiß auf. Die Silberklinge bekommt keinen Zonenbonus
- HUD für den Kampf: Fadenkreuz in der Bildmitte; links und rechts davon zeigen kleine Balken, ob Klinge und Pistole bereit sind; darunter ein Ladebalken beim Aufladen der Klinge (golden, wenn voll); unten links das Leben
- Waffen im Bild: links die Silberklinge, rechts die Pistole (Platzhalter aus Grundformen). Sie bewegen sich weich, als würde man durch die Welt gleiten: ruhiges Wippen beim Laufen, Nachziehen beim Umsehen und Seitwärtslaufen, Mitnicken beim Landen. Die Klinge schwingt beim Schlag quer durchs Bild, die Pistole kickt beim Schuss nach oben, vorn blitzt die Mündung

## [0.3.2] – 2026-11-11 – Fenster-Fix

Eine Fehlerkorrektur für 0.3.0.

### Behoben
- Beim Start auf einem Bildschirm links vom Hauptbildschirm war das Fenster unsichtbar (weit außerhalb aller Bildschirme) und die Maus eingefangen. Liegt das Fenster nach dem Start auf keinem Bildschirm, wird es jetzt auf den Hauptbildschirm geholt

## [0.3.1] – 2026-11-11 – fehlerhaftes Release

Versehentlich auf dem Stand von 0.3.0 veröffentlicht – enthält den Fenster-Fix **nicht**. Bitte 0.3.2 verwenden.

## [0.3.0] – 2026-11-11 – PS2-Look

Das Schloss bekommt Stimmung: grobe Pixel, Licht wie von einer Laterne, Dunkelheit in der Ferne – und erstmals Höhenunterschiede. Dazu wird die Bewegung zum Können: Perfect Jump, Stamina, Dash in der Luft und Dodge am Boden.

### Geändert
- Retro-Look: Die 3D-Welt wird in 854 × 480 gezeichnet und mit harten Pixelkanten hochskaliert; Texte bleiben scharf
- Das Fenster lässt sich in der Größe ändern; das Bild behält sein Seitenverhältnis (schwarze Balken bei abweichender Form)
- Flat Shading: Wände, Boden und Decke haben je nach Ausrichtung eine eigene Helligkeit; Ecken sind ohne Kantenlinien erkennbar
- Sprung schwebender: 1,3 m hoch und 0,8 s in der Luft, mehr Kontrolle in der Luft und ein kleiner Tempo-Schub beim Springen
- Räume sind 3,5 m hoch (vorher 3 m), damit der höhere Sprung nicht an die Decke stößt

### Hinzugefügt
- Laternenlicht und Nebel: In der Nähe ist es hell, ab etwa 18 m versinkt alles in violett-schwarzem Dunkel
- Level-Format kennt Podeste (`1`, `2`, `3`) und Gruben (`_`); Beschreibung in `docs/level-format.md`
- Stufen, Podeste und Gruben werden gezeichnet; man steigt Treppen hinauf, springt auf hohe Podeste und aus Gruben heraus
- Zweites Testlevel „heights“ mit Treppe, hohem Podest und Grube – starten mit `--level heights`
- Landungs-Nicken: Beim Aufkommen sackt der Blick kurz und kräftig ab (normaler Sprung etwa 12 cm, 0,3 s) und neigt sich – je tiefer der Fall, desto stärker
- Perfect Jump: Wer kurz vor oder nach dem Aufkommen erneut springt, bekommt +20 % Tempo (nicht stapelbar); die Landung fällt dabei sanfter aus
- Stamina-Balken (unten in der Mitte) mit 5 Segmenten: Jeder Sprung kostet eins. Er lädt nur am Boden, in 3 s von leer bis voll. Ein Sprung mit leerem Balken ist erschöpft: 20 % langsamer als Laufen (4,8 statt 6 m/s) bis zum Ende der Sprungkette
- Dash in der Luft mit der linken Umschalttaste: 4 m in 0,15 s in Laufrichtung (ohne Richtung nach vorn), ohne Schwerkraft. Einmal pro Sprung, kostet ein Stamina-Segment. Wer am Ende des Dash springt, bekommt bei der Landung einen Perfect Jump
- Dodge am Boden mit der linken Umschalttaste: 0,4 s geduckt und schnell (10 m/s, etwa 4 m) in Laufrichtung, frei lenkbar (ohne Taste behält er die Richtung); die Kamera sinkt dabei weich ab. Kostet zwei Stamina-Segmente, währenddessen kein Aufladen. Springen nur am Ende: Wer kurz vor oder nach dem Ende des Dodge springt, bekommt einen Perfect Jump – und kann in dessen Luftphase wieder dashen

## [0.2.0] – 2026-11-10 – Bewegung & Physik

Zum ersten Mal ein begehbarer Ort: Du stehst in einem Level, siehst dich um, läufst und springst.

### Hinzugefügt
- Level aus Textdateien (Raster aus 2 × 2 m großen Zellen mit eigener Boden- und Deckenhöhe), erstes Testlevel mit vier Räumen
- Darstellung des Levels mit Wänden, Boden und Decke
- Ego-Perspektive: Umsehen mit der Maus (nicht invertiert, Blick nach oben und unten begrenzt)
- Laufen mit WASD (6 m/s, kurzes Anfahren und Abbremsen), flüssig auch auf Monitoren über 60 Hz
- Kollision mit Wänden: Man gleitet an Wänden entlang und rutscht um Türkanten herum
- Schwerkraft und Springen mit der Leertaste (ca. 1 m hoch), in der Luft eingeschränkt lenkbar
- Esc gibt die Maus frei, ein Klick ins Fenster fängt sie wieder ein

### Geändert
- Esc beendet das Spiel nicht mehr – Beenden über das X des Fensters
- Kein Konsolenfenster mehr hinter dem Spiel
- Texte im Spiel sind englisch

### Entfernt
- Der drehende Würfel aus 0.1.0

## [0.1.0] – 2026-11-10 – Walking Skeleton

Das technische Grundgerüst: noch kein Spiel, aber alles, was ein Spiel braucht, um entwickelt, geprüft und ausgeliefert zu werden.

### Hinzugefügt
- Fenster mit drehendem Würfel als erstes Lebenszeichen des Spiels
- Feste Zeitschritte: Die Simulation läuft mit 60 Ticks pro Sekunde, unabhängig von der Bildrate
- Technisches Log unter `%LOCALAPPDATA%\SilverAndAsh\logs\` mit Absturzprotokoll
- Versionsnummer unten rechts im Spiel
- Automatische Prüfung (CI) jedes Pull Requests
- Automatisches Windows-Paket bei jedem Versions-Tag

[Unveröffentlicht]: https://github.com/flubby2222/silver-and-ash/compare/v0.11.1...HEAD
[0.11.1]: https://github.com/flubby2222/silver-and-ash/compare/v0.11.0...v0.11.1
[0.11.0]: https://github.com/flubby2222/silver-and-ash/compare/v0.10.0...v0.11.0
[0.10.0]: https://github.com/flubby2222/silver-and-ash/compare/v0.9.0...v0.10.0
[0.9.0]: https://github.com/flubby2222/silver-and-ash/compare/v0.8.0...v0.9.0
[0.8.0]: https://github.com/flubby2222/silver-and-ash/compare/v0.7.0...v0.8.0
[0.7.0]: https://github.com/flubby2222/silver-and-ash/compare/v0.6.0...v0.7.0
[0.6.0]: https://github.com/flubby2222/silver-and-ash/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/flubby2222/silver-and-ash/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/flubby2222/silver-and-ash/compare/v0.3.2...v0.4.0
[0.3.2]: https://github.com/flubby2222/silver-and-ash/compare/v0.3.1...v0.3.2
[0.3.1]: https://github.com/flubby2222/silver-and-ash/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/flubby2222/silver-and-ash/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/flubby2222/silver-and-ash/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/flubby2222/silver-and-ash/releases/tag/v0.1.0
