# Kiste – Probestück 2 (2026-11-16)

Erzeugt von [`kiste.py`](kiste.py) mit Blender 5.3, ohne Fenster. Das Skript baut ein Kistenmodell in allen vier Stufen, exportiert je Stufe eine Datei (`chest_beggar.glb`, `chest_guard.glb`, `chest_king.glb`, `chest_vampire.glb`) und rendert die Bilder:

```bash
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/kiste/kiste.py
```

## Bilder

| Bild | Was |
|---|---|
| `ansicht_vorn.png`, `ansicht_seite.png`, `ansicht_hinten.png`, `ansicht_schraeg_oben.png` | King-Kiste, 640 × 640, Flat Shading nach `FlatShading.cs`, kein Nebel, Hintergrund Nebelfarbe |
| `stufen_4m.png`, `stufen_10m.png` | Spielbedingungen; von links Beggar, Guard, King, Vampire, 1,6 m von Mitte zu Mitte, Vorderseite zur Kamera |
| `king_geschlossen_offen_4m.png` | King aus 4 m, links geschlossen, rechts offen (Deckel 110° nach hinten, wie der Platzhalter mit 1,9 rad) |

**Spielbedingungen** heißt hier, wie beim Ghul:

- 854 × 480 Pixel, 70° vertikal, Augen auf 1,6 m, waagerecht 4 m bzw. 10 m entfernt, Blick auf 0,35 m Höhe.
- Laterne und Nebel werden pro Fläche nach den Formeln der Art Bible berechnet.
- Der Raum ist 9 m breit und 3,5 m hoch, Boden, Wände und Decke in den Spielfarben.
- Harte Pixel, keine Kantenglättung.

## Aufbau: ein Modell, vier Stufen

Alle Stufen teilen sich denselben Grundkörper:

- **Kasten:** 1 × 0,455 × 0,7 m, also 65 % der Höhe wie im Platzhalter. Seine Oberseite ist der dunkle Innenraum.
- **Deckel:** im Schnitt sechseckig (senkrechter Rand, zwei abgeschrägte Kanten, flache Oberseite), ringsum 1,5 cm Überstand. Durch den Überstand ist die Fuge zwischen Kasten und Deckel sichtbar.
- **Zwei Bänder** bei x = ±0,3 m, wie im Platzhalter. Sie laufen um Kasten und Deckel.
- **Schloss vorn:** Messingplatte mit senkrechtem Schlüsselloch, ragt über die Fuge auf den Deckelrand.
- **Scharnierleiste hinten.**

Jede Stufe bekommt dazu ein Formmerkmal. Die Merkmale sitzen bewusst **auf dem Deckel**: Der Spieler sieht Kisten von leicht oben, aus 10 m vor allem vor dem Nebel, und dort entscheidet der Umriss oben.

| Stufe | Farbe (Körper / Band) | Formmerkmal | Dreiecke |
|---|---|---|---|
| Beggar | 110, 76, 46 / 56, 44, 36 | keins – schlicht, nur die Bänder | 132 |
| Guard | 92, 112, 140 / 52, 58, 70 | Eckbeschläge an den vier senkrechten Kanten, eiserner Tragegriff oben (zwei Pfosten, eine Stange, Lücke darunter) | 200 |
| King | 214, 170, 64 / 140, 100, 36 | Eckbeschläge, Kronen-Aufsatz mit drei Zacken quer auf dem Deckel | 188 |
| Vampire | 175, 25, 35 / 40, 10, 14 | Eckbeschläge, vier nach außen geneigte Dornen an den Deckelecken | 188 |

**Was man aus 10 m sieht** (`stufen_10m.png`, vergrößert nachgesehen):

- Beggar: oben glatt.
- Guard: ein eckiger Bügel.
- King: drei Zacken in der Mitte.
- Vampire: Hörner an beiden Seiten.

Also auch ohne Farbe vier verschiedene Umrisse. Die Eckbeschläge sind aus 10 m kaum zu sehen, nur aus 4 m. Sie zählen als „mehr Metall ab Guard“, nicht als Erkennungszeichen.

Alle Farben stehen unverändert so in `ChestView.cs`. Schloss in Messing 176, 140, 72, Innenraum 18, 14, 14. Die Vampir-Stufe pulsiert im Spiel zwischen 150, 20, 30 und 200, 30, 40; Modell und Bilder nehmen die Mitte 175, 25, 35.

Höchstens vier Farben je Kiste: Körper, Band, Messing, Innenraum.

## Teile und Drehpunkte

In jeder Datei drei Knoten, Ursprung jeweils der Drehpunkt. Angaben in Spiel-Achsen (x seitlich, y oben, z vorn; die Vorderseite mit dem Schloss blickt nach +z):

| Teil | Drehpunkt | Bewegung im Code |
|---|---|---|
| `base` | 0, 0, 0 (Mitte Boden) | ganze Kiste (Aufsteigen aus dem Boden wie heute) |
| `lid` | 0, 0,455, −0,365 (hintere Unterkante des Deckels = Scharnier) | um die x-Achse nach hinten klappen; offen 110°. Der Aufsatz (Griff, Krone, Dornen) gehört zum Deckel und klappt mit |
| `lock` | 0, 0,47, 0,39 (Oberkante der Schlossplatte) | ausblenden oder herunterklappen, wenn die Kiste aufgeschlossen ist |

Das passt zu #209: Die Vorderseite ist fest, das Schloss sitzt nur vorn, der Deckel klappt immer nach hinten. Das Spiel muss die Kiste nur noch nach der Vorderseite aus `ChestPlan` drehen (+z oder −z).

## Vom Modell ins Spiel

Wie beim Ghul angenommen:

- **Format:** `.glb`
- **Achsen:** Export mit +Y oben
- **Ursprung:** Mitte Boden
- **Farbraum:** glTF speichert Vertex-Farben linear; beim Laden zurück nach sRGB rechnen.

Neu bei der Kiste ist die Frage, **wie ein Modell vier Stufen trägt**.

**Mein Vorschlag:** Das Skript erzeugt aus einer Beschreibung **eine Datei je Stufe**, Ablage dann `assets/models/chest_<stufe>.glb`. Jede Datei hat ihre Farben und ihre Formmerkmale schon drin. Das Spiel lädt vier Modelle und wählt nach `ChestTier`.

- **Vorteil:** einfach. Kein Umfärben, kein Ein- und Ausblenden von Teilen im Code.
- **Nachteil:** Der Kasten ist viermal gespeichert, zusammen etwa 30 KB.

**Die Alternative:** eine einzige Datei mit allen Aufsätzen als eigene Knoten und Platzhalterfarben, die das Spiel je Stufe ersetzt. Die spart Speicher, braucht aber mehr Code und eine Farbsuche nach festen Werten, die den Umweg linear → sRGB überleben müssen.

**Pulsieren der Vampir-Stufe:** Mit fest eingebrannten Farben geht das am einfachsten über die Tönung beim Zeichnen (`DrawModel` mit einer Farbe, die zwischen etwa 86 % und 114 % schwankt). Diese Tönung färbt allerdings Bänder und Schloss mit. Soll nur der Körper pulsieren, muss das Spiel die Körperfarbe in `mesh.colors` jedes Bild neu schreiben. Das ist für eine Handvoll Kisten billig.

## Angewendete Regeln aus den Ghul-Runden

1. **Farbton statt nur Helligkeit gegen den Stein** (Rumpf-Regel aus Runde 1): Der Stein ist rosagrau, Farbton um 330°. Die großen Flächen der Kisten liegen bei:
   - Holzbraun um 28°
   - Stahlblau um 215°
   - Gold um 43°
   - Blutrot um 357°

   Am nächsten am Stein liegt das Rot (etwa 27° Abstand), ist aber viel gesättigter und hebt sich in `stufen_10m.png` klar ab.
2. **Dunkle Formen machen Flächen lesbar** (Gesichts-Regel aus Runde 1): Die Vorderseite trägt eine dunkle Form, das Schlüsselloch; der Innenraum ist dunkel. Offen zeigt die Kiste den dunklen Spalt des Innenraums. Den Deckel selbst habe ich innen **nicht** dunkel gemacht: In meiner ersten Fassung stand der offene Deckel als großer schwarzer Block hinter der Kiste. Jetzt ist er innen in der Bandfarbe.
3. **Keine Grimassen** (Regel aus Runde 2: Augen über einem breiten geraden Rechteck lesen sich als Grinsen): Die Vorderseite hat nur **eine** dunkle Form, das senkrechte Schlüsselloch, dazu zwei senkrechte Bänder. Es gibt keine paarigen dunklen Flecken über einem waagerechten Strich, also kein Gesicht.
4. **Kantig, Flat Shading nach der Regel des Spiels, Silhouette zuerst:** Die Unterscheidung der Stufen sitzt im Umriss oben.

## Wo ich unsicher bin

- **Schloss auf der King-Kiste:** Messing (176, 140, 72) auf Gold (214, 170, 64) hat wenig Abstand im Farbton. Das Schloss liest sich dort nur über das dunkle Schlüsselloch und die etwas dunklere Platte (`ansicht_vorn.png`). Wenn die Messingfarbe nicht zu den festen Stufenfarben zählt, würde ich das Schloss auf allen Kisten dunkel-eisern machen. Dann ist es überall gleich gut sichtbar, und Messing ist ohnehin fast Gold.
- **Höhe der Aufsätze:** Krone bis 0,99 m, Dornen bis 0,91 m, Griff bis 0,86 m, über `Chest.Height = 0,7` hinaus. Kollision und Interaktion merken davon nichts. Ein Schuss, der knapp über die Kiste geht, fliegt aber sichtbar durch die Krone. Die Aufsätze sind schmal, ich halte das für vertretbar; flacher würden sie aus 10 m wieder untergehen.
- **Beggar aus 10 m:** Das dunkle Holzbraun verliert im Nebel viel. Die Kiste bleibt erkennbar, ist aber die unscheinbarste. Für die billigste Stufe passt das.
- **Guard am Richtwert:** genau 200 Dreiecke. Der Griff kostet 36 davon; ein einfacher Block ohne Lücke würde 24 sparen, liest sich aus 10 m aber eher wie ein flacher Buckel.
- **Seitenansicht:** Von der Seite sind Vorder- und Rückseite kaum zu unterscheiden, nur am Überstand von Schloss und Scharnierleiste. Im Spiel steht die Kiste meist mit der Vorderseite zur Raummitte, das sollte reichen.
- **Doppelter Code:** Die Hilfsfunktionen (Teile, Farben, Nebel, Raum, Rendern) sind aus `ghul.py` kopiert. Beim ersten Einbau unter `tools/` sollten sie ein gemeinsames Modul werden.
