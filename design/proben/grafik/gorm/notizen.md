# Gorm, the Chained – Probestück 9, Boss (2026-11-16)

Ein massiger, gebückter Troll von 3,2 m, an Ketten gebunden. PS1-nah nach der Art Bible.

Alles erzeugt [`gorm.py`](gorm.py) mit [`../gemeinsam/psx.py`](../gemeinsam/psx.py):

```bash
"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe" --background --python design/proben/grafik/gorm/gorm.py
```

## Gestalt

**Körper:**

- Gebückt: Der Rumpf neigt sich nach vorn, Schultern und Nacken liegen 0,3 bis 0,45 m vor dem Becken, hinten ein Buckel mit Höckern der Wirbelsäule.
- Lange, schwere Arme: Die Fäuste hängen bis zu den Knien.
- Kurze, dicke Beine, Lendenschurz mit ausgefranstem Saum.

**„The Chained“ – man sieht es ihm an:**

- eiserner Halsring mit Nieten, hinten ein abgerissenes Kettenstück;
- Handschellen an beiden Handgelenken;
- Striemen von Ketten quer über Brust und Rücken;
- in der linken Hand die rostige Kette mit Haken, in der rechten (+x, wie `GormView`) die Keule mit drei Eisenbändern.

**Kopf, die Schwachstelle:**

- Blasser als der Körper (Haut über Knochen), kahl und vernarbt.
- Schwere Stirnwulst, kleine, tiefe Augen, breite Nase.
- Mund schief und nach unten gezogen, kein Grinsen (Regel 4).
- Breiter, vorgeschobener Unterkiefer mit zwei hellen Hauern, die nach oben ragen; der linke ist abgebrochen.
- **Von unten:** Der dunkle Halsring rahmt den hellen Kopf ein; die Hauer und das helle Kinn sind das, was man zuerst sieht (`spiel_2m_ruhe.png`).

**Asymmetrie:** Die Kettenschulter hängt 8 cm tiefer, der Kopf ist 6° zur Kettenseite geneigt, ein Hauer ist abgebrochen, die Kette hängt nur an einer Seite.

## Ankündigungen und Haltungen

Jede Ankündigung zeigt **wann und wo**, nach ADR 0007.

| Bild | Haltung | was man sieht |
|---|---|---|
| `spiel_2m_ruhe.png`, `spiel_4m_ruhe.png`, `spiel_10m_ruhe.png` | **Ruhe** | gebückt, Arme hängend, Keule schräg vor sich, Kette mit Haken hängt aus der linken Faust |
| `spiel_6m_schwung_an.png` | **Ankündigung Kettenschwung** | linker Arm hoch über dem Kopf, die Kette waagerecht darüber, so weit sie reicht (wie im Platzhalter kreist sie; im Bild eine Stellung). Der Umriss wird oben links hoch und lang. **Wo:** so weit die Kette reicht. **Wann:** wenn sie schneller kreist |
| `spiel_6m_schwung.png` | **Ausführung Kettenschwung** | linker Arm waagerecht zur Seite gestreckt, die Kette gestreckt auf Brusthöhe, hier bei 45° vor seiner Linken auf dem Weg nach vorn |
| `spiel_6m_haken_an.png` | **Ankündigung Haken** | linker Arm zurück und angewinkelt, der Haken hängt kurz aus der Faust und glüht rot (wie im Code: Tönung zur Warnfarbe 200, 30, 30; Rot für Gefahr ist erlaubt) |
| `spiel_6m_phase2_bruellen.png` | **Phase 2: Brüllen** | Oberkörper zurückgelehnt, Kopf im Nacken, Maul weit auf, beide Arme hoch |
| `spiel_10m_phase2_ruhe.png` | **Phase 2: Ruhe** | breitbeiniger, Arme abgespreizt, Maul offen |
| `vergleich_phase1_phase2_10m.png` | Phase 1 und 2 aus 10 m | dreifach vergrößert, links Phase 1, rechts Phase 2 |

Die Ankündigungen sind aus 6 m gerendert, weil die gestreckte Kette und der hochgereckte Arm aus 4 m nicht mehr ins Bild passen. Aus der Nähe sieht der Spieler bei 70° Blickwinkel nur den Rumpf.

**Woran man aus 10 m sieht, dass er in Phase 2 ist:**

1. **Blut:** Breite, dunkelrote Blutspuren laufen aus Wunden unter dem Halsring über Brust, Hals und Gesicht (eigene Textur für Phase 2, siehe unten). Rot ist für Blut und Gefahr reserviert, beides trifft zu. Aus 10 m sind sie der deutlichste Unterschied.
2. **Haltung:** Arme abgespreizt, breiter Stand, Maul offen. Der Umriss wird um ein Drittel breiter: 134 statt 102 Pixel.
3. **Halskette:** Das abgerissene Kettenstück am Halsring ist weg. Das ist aus 10 m kaum zu sehen, aber aus der Nähe eine schöne Einzelheit: Er hat sich losgerissen.

Der Hammerschlag (Keule hoch) und „Keule steckt fest“ waren nicht verlangt und sind nicht gerendert.

## Zahlen

**Dreiecke in der Datei: 1 992.**

| Teil | Dreiecke |
|---|---|
| Rumpf mit Halsring und Lendenschurz | 432 |
| Kopf | 176 |
| Unterkiefer mit Hauern | 72 |
| Oberarm | je 72 |
| Unterarm mit Handschelle und Faust | je 196 |
| Oberschenkel | je 72 |
| Unterschenkel mit Fuß | je 120 |
| Keule | 188 |
| ein Kettenglied | 80 |
| Haken | 124 |

**Im Spiel kommen die Kettenglieder dazu:**

- Hängend etwa 7 Glieder plus 7 am Halsring, also gut 1 100 Dreiecke.
- Gestreckt bei 8 m Reichweite etwa 60 Glieder, also 4 800.

Zusammen liegt er damit hängend bei etwa 3 100 Dreiecken. Beim Schwung wären es zu viele; dafür schlage ich ein vereinfachtes Glied vor (Quader mit 12 Dreiecken) oder jedes zweite Glied als flaches Viereck. Das entscheidet der Einbau.

**Messung im Bild** (`messung.txt`):

| Aus | Haltung | Größe |
|---|---|---|
| 4 m | Ruhe | 366 × 338 Pixel |
| 4 m | nur Kopf und Kiefer | 76 × 54 Pixel |
| 10 m | Ruhe | 102 × 143 Pixel |
| 10 m | Phase 2 | 134 × 138 Pixel |

## Texturen: zwei statt einer, dazu eine Variante

| Datei | Inhalt |
|---|---|
| `gorm_textur_rumpf.png` (128 × 128) | Rumpf 128 × 64, Kopf 64 × 48, Unterkiefer, Lendenschurz, Halsring, Hauer, Ohren |
| `gorm_textur_glieder.png` (128 × 128) | Ober- und Unterarme, Oberschenkel, Unterschenkel je 64 × 48, Hände, Füße, Keule, Eisen, Kette |
| `gorm_textur_rumpf_phase2.png` (128 × 128) | wie die Rumpftextur, mit Blutspuren und Wunden. Der Code tauscht sie beim Wechsel in Phase 2 |

Alle mit 32 Farben, keine Filterung.

**Warum zwei Texturen:**

- **Das Problem:** Gorm ist 3,2 m groß, der Rumpf misst rundherum fast 4 m. Mit einer einzigen 128 × 128-Textur für alles bekäme der Rumpf etwa 64 Pixel auf 4 m Umfang, also 6 cm je Texel.
- **Was das im Bild heißt:** Aus 2 m, von wo der Spieler beim Schlagen zu ihm aufsieht, wären das 4 bis 5 Bildpixel je Texel – grobe Klötze statt Haut.
- **Mit zwei Texturen:** Der Rumpf hat 128 Pixel auf dem Umfang (3 cm je Texel), der Kopf 64 × 48. Das ist ähnlich dicht wie beim Ghul.
- **Bezug zur Art Bible:** Die Regel „eine Textur je Figur“ ist für Figuren von 1 bis 1,8 m gedacht; für einen Boss doppelter Größe halte ich zwei für angemessen.

**Warum eine Phase-2-Variante:** Der Wechsel in Phase 2 muss aus 10 m lesbar sein, und Farbe wirkt dort stärker als jede Einzelheit der Form.

## Teile und Drehpunkte

Spiel-Achsen, Ursprung zwischen den Füßen, Blick nach +z. In der Datei stehen alle Winkel auf 0.

| Teil | Drehpunkt | hängt an |
|---|---|---|
| `torso` (mit Halsring, Lendenschurz) | 0, 1,15, 0 (Hüfte) | – |
| `head` | 0, 2,5, 0,42 (Nacken) | torso |
| `jaw` | 0, 2,7, 0,58 | head |
| `upper_arm_px` | 0,86, 2,3, 0,26 (Schulter, Keulenseite) | torso |
| `upper_arm_nx` | −0,86, 2,22, 0,26 (Schulter, Kettenseite, tiefer) | torso |
| `forearm_px`, `forearm_nx` (mit Handschelle, Faust) | ±1,04, 1,46 bzw. 1,38, 0,42 (Ellbogen) | Oberarm |
| `club` | 1,0, 0,56, 0,58 (Mitte der rechten Faust) | forearm_px |
| `thigh_px`, `thigh_nx` | ±0,36, 1,12, 0 (Hüftgelenk) | torso |
| `shin_px`, `shin_nx` (mit Fuß) | ±0,44, 0,6, 0,12 (Knie) | Oberschenkel |
| `chain_link` | Mitte des Glieds, längs der y-Achse | – |
| `hook` | Öse oben, Spitze nach unten und vorn | – |

**Kette im Code:** Glieder entlang der Strecke von der linken Faust zum Ziel aufreihen, Abstand 0,13 m, jedes zweite um 90° um die Kettenrichtung gedreht; am Ende der Haken. Die Strecke ist beim Hängen etwa 0,9 m nach unten, beim Schwung die gestreckte Kette, beim Wurf der Weg zum Haken – wie heute die Kästchen in `GormView`.

**Haltungen** (Grad: „heben“ um x wie in der Übergabe-README, „abspreizen“ um die Längsachse z, positiv = Hand nach außen):

| Haltung | Winkel |
|---|---|
| Ruhe | `jaw` −6, `forearm_px` 14, `forearm_nx` 10 |
| Ankündigung Schwung | `upper_arm_nx` heben 160 / abspreizen 25, `forearm_nx` 20, `head` 8, `jaw` −20, `torso` 6 |
| Schwung | `upper_arm_nx` heben 15 / abspreizen 85, `upper_arm_px` −10 / −15, `torso` −6, `jaw` −25 |
| Ankündigung Haken | `upper_arm_nx` −40 / 45, `forearm_nx` 60, `torso` 4, `head` −4 |
| Phase 2 Brüllen | `torso` 16, `head` 38, `jaw` −45, beide Oberarme 150 / 30, beide Unterarme 20 |
| Phase 2 Ruhe | `torso` −8, `head` −6, `jaw` −22, beide Oberarme 8 / 18, beide Unterarme 24 |

## Vergleich mit dem Trefferkörper (`BodySizes.Gorm`)

| Maß | Trefferkörper | Modell (aus der Datei) | Bewertung |
|---|---|---|---|
| Höhe | 3,2 m | 3,1 m (Scheitel) | passt |
| Radius | 0,9 m (also 1,8 m breit) | Rumpf ±0,86 m; mit Armen ±1,27 m; die Keule ragt nach vorn bis 1,76 m | Rumpf passt; Arme und Keule ragen wie beim Platzhalter hinaus (dort ±1,14 m) |
| Kopfzone: Höhe | oberste 0,6 m, also ab 2,6 m | Kopf 2,68 bis 3,1 m, Unterkiefer ab 2,56 m | passt; die Kinnspitze liegt 4 cm unter der Zone |
| Kopfzone: Breite | 0,7 m | Schädel 0,6 m, mit Ohren 0,84 m | Schädel passt, die Ohren ragen 7 cm je Seite hinaus |
| Kopfzone: Lage | „at the front“ | Kopf von 0,35 bis 0,94 m vor der Mitte | passt zur gebückten Haltung |

**Vorschlag:** keine Änderung an `BodySizes`. Die Ohren und die Kinnspitze außerhalb der Zone sind zu klein, um sie zu treffen.

Durch die gebückte Haltung liegt der Kopf weiter vorn als beim Platzhalter (dort Mitte bei 0,35 m, hier etwa 0,62 m). Liest das Spiel die Kopfzone von der Körpermitte aus, sollte die Code-Sitzung prüfen, ob „vorn“ weit genug reicht.

## Wo ich unsicher bin

- **Allgemeiner Troll:** Die Grundgestalt (grüne Haut, Hauer, Lendenschurz) ist ein sehr allgemeiner Troll. Eigen machen ihn Halsring, Handschellen, die Kette als Waffe und die Ketten-Striemen. Wenn er zu sehr nach „Fantasy-Troll von der Stange“ aussieht, wäre der nächste Schritt eine eigenere Kopfform, etwa ein Maulkorb oder eine eingewachsene Eisenmaske, die er in Phase 2 abreißt. Das wäre ein starkes Zeichen für den Phasenwechsel.
- **Blutspuren als Streifen:** Sie sind zu regelmäßig, fast wie ein Muster. Mehr Zufall in Breite und Länge würde sie glaubhafter machen.
- **Viele Kettenglieder:** Die Zahl beim Schwung ist eine Frage an den Einbau (siehe Zahlen).
- **Bossraum:** Ich habe eine Halle von 16 × 16 m mit 6 m Decke und vier Säulen gebaut, angelehnt an `great-hall`. Die echte Vorlage hat andere Maße.

---

# Kopf-Runde: drei Gesichter zur Auswahl (2026-11-16)

**Urteil des Menschen:** „Der Körper ist ok, das Gesicht soll nochmal überarbeitet werden.“

**Was neu ist:** Der Körper kommt unverändert aus `gorm.py`; nur Kopf, Unterkiefer und die Kopftextur sind ersetzt. Keine Hauer, keine spitzen Ohren.

**Skript:** [`gorm_koepfe.py`](gorm_koepfe.py). `gorm.py` ist dafür nur importierbar gemacht (`main()` läuft nur beim direkten Aufruf).

| Variante | Gesicht | Kopf und Kiefer |
|---|---|---|
| **a) Mensch** | entstellter, massiger Mensch: schwerer Schädel, tief eingefallene, ungleiche Augen, platte gebrochene Nase, Narben, zugenähter, nach unten gezogener Mund, ein Ohr abgerissen. Phase 2: Nähte aufgerissen, dunkles Blut | 240 Dreiecke |
| **b) Eisen** | Gefangener in Eisen: angeschmiedeter Helm mit Nieten, waagerechte Sehschlitze mit den Augen dahinter, Stirnplatte, zwei Bolzen durch die Schläfen, Gitter-Maulkorb vor dem Mund, dahinter schiefe helle Zähne, Kette vom Kinnring zum Halsring. Phase 2: Blut quillt unter dem Helm hervor | 324 Dreiecke |
| **c) Sack** (mein Vorschlag) | ein grober Kerkersack über dem Kopf, am Hals unter dem Eisenring zugezogen, Naht über den Scheitel, gerissene, ungleiche Augenlöcher. Vor dem Mund ist der Stoff durchnässt, ein dunkler Fleck, der wie ein offener Mund wirkt. Phase 2: Blut sickert durch | 196 Dreiecke |

**Warum Variante c:** „The Chained“ als namenloser Gefangener, dem man nicht einmal ein Gesicht lässt – Kerker statt Stammeskrieger. Der helle Sack ist dazu das beste Ziel: aus 10 m der hellste Punkt am Körper.

**Textur:** Je Variante eine eigene Rumpftextur, weil der Kopf darin liegt, plus die Phase-2-Variante; je 128 × 128, 32 Farben. Die Gliedertextur bleibt.

**Dreiecke:** Körper und Waffen sind gleich geblieben; ganz Gorm liegt je nach Kopf zwischen 1 940 und 2 070.

## Bilder

| Bild | Was |
|---|---|
| `vergleich_koepfe_4m_ruhe.png`, `vergleich_koepfe_4m_phase2.png` | **zum Auswählen:** alle drei Köpfe aus 4 m, dreifach vergrößert; von links a, b, c |
| `vergleich_koepfe_10m_ruhe.png`, `vergleich_koepfe_10m_phase2.png` | ganzer Gorm aus 10 m, dreifach vergrößert; von links a, b, c |
| `kopf_<variante>_vorn.png`, `kopf_<variante>_unten.png` | Kopf groß von vorn und von schräg unten, ohne Nebel |
| `kopf_<variante>_ruhe_4m.png`, `kopf_<variante>_ruhe_10m.png`, `kopf_<variante>_phase2_4m.png`, `kopf_<variante>_phase2_10m.png` | im Bossraum, Nebel mit Körnung |
| `kopf_<variante>_textur_rumpf.png`, `kopf_<variante>_textur_rumpf_phase2.png` | die Texturen |

## Wie sie sich als Ziel lesen

| Variante | aus 4 m | aus 10 m | Schwäche |
|---|---|---|---|
| a) Mensch | Gesicht lesbar | heller Fleck über dem dunklen Halsring | Die Naht über dem Mund ist zu fein; man liest eher einen schmalen, schiefen Mund. Der Kopf wirkt etwas weich und rund |
| b) Eisen | am deutlichsten Gefangener, Zähne hinter dem Gitter gut zu sehen | **am schwächsten**: Das mittlere Eisengrau liegt nah am dunklen Raum, nur Nieten und Zähne leuchten | Wenn gewählt, sollte das Eisen heller und blanker werden, oder die Zähne größer |
| c) Sack | klar als Kopf und Ziel | **am stärksten**: der hellste Punkt am Körper | Der nasse Fleck wirkt wie ein großer offener Mund; das kann je nach Geschmack eher unheimlich oder eher komisch wirken |

## Kette sichtbarer machen (Vorschlag, noch nicht umgesetzt)

Heute zieht die Keule das Auge: hell, groß, warm-braun mit hellen Bändern. Die Kette ist rostbraun, dünn (Glieder 10 × 16 cm) und hängt in der Faust am Körper. So würde ich das ändern:

1. **Glieder größer und kantiger:** etwa 18 × 28 cm statt 10 × 16 cm, im Umriss deutlich größer. Das sind Ketten eines Riesen, keine Ankerkette. Ein Glied als achteckiger Ring kostet nicht mehr Dreiecke als heute.
2. **Hellere Glieder:** blank geriebenes Eisen an den Berührungsflächen (hellgrau), Rost nur in den Innenseiten. Dann blitzt die Kette im Laternenlicht, die Keule nicht.
3. **Länger und am Körper:** in Ruhe zwei- bis dreimal um den linken Unterarm gewickelt, dann bis zum Boden hängend und ein Stück am Boden schleifend. So ist sie in jedem Bild als langes, helles Band da.
4. **Keule zurücknehmen:** dunkleres, älteres Holz, die Eisenbänder dunkel statt hell. Die Keule bleibt groß; nur die Helligkeit wandert zur Kette.
5. **Haken größer:** etwa 40 cm, mit einem hellen, geschliffenen Spitzenrand, damit das rote Aufglühen vor dem Wurf auf einer großen Fläche liegt.

Umsetzen erst, wenn ein Gesicht gewählt ist.

---

# Endfassung: Eisenmaske und schwere Kette (2026-11-16)

**Der Mensch hat die Eisenmaske gewählt.** Skript: [`gorm_final.py`](gorm_final.py), mit `gorm.py` (Körper) und `gorm_koepfe.py` (Kopf) als Bausteinen. Die beiden anderen Köpfe bleiben nur als Bilder auf dem Branch.

**Was geändert wurde:**

1. **Kopf:** die Eisenmaske unverändert (Gitter mit Zähnen, Stachelband, Ring am Kinn). Kopf 264 Dreiecke, Kiefer 60.
2. **Kette als Waffe:**
   - Glieder 30 × 19 cm statt 16 × 10 cm, blankes Eisen (158, 160, 170) mit Rost nur in den Vertiefungen; das Hellste an Gorms Körper außer dem Kopf.
   - Dreimal um den linken Unterarm gewickelt (eigenes Teil `wrap_nx`).
   - In Ruhe bis zum Boden hängend und ein Stück schleifend.
   - Beim Schwung bestimmt sie die Silhouette: fünf Meter lang, waagerecht über dem Kopf.
   - Der Haken ist 1,8-fach.
3. **Keule:** bleibt, weil sie im Spiel den Hammerschlag trägt (`GormAttack.Hammer`, „Keule steckt fest“), ist aber 38 % dunkler gemalt, ohne helle Eisenbänder.

**Dreiecke:** Datei 2 268; mit hängender Kette etwa 3 300, mit gestrecktem Schwung etwa 5 000 (vereinfachtes Glied vorgeschlagen, siehe README der Übergabe).

## Bilder

Im Bossraum mit den neuen Level-Texturen, Nebel mit Körnung.

| Bild | Was |
|---|---|
| `final_4m_ruhe.png`, `final_10m_ruhe.png` | Ruhe |
| `final_6m_schwung_an.png`, `final_6m_schwung.png` | Ankündigung und Ausführung des Kettenschwungs |
| `final_6m_haken_an.png` | Ankündigung des Hakens (glüht) |
| `final_4m_phase2_ruhe.png` | Phase 2 |
| `final_vergleich_alt_neu_10m.png` | links erste Fassung, rechts Endfassung, aus 10 m, dreifach vergrößert |
| `final_textur_rumpf.png`, `final_textur_rumpf_phase2.png`, `final_textur_glieder.png` | die Texturen |

## Ist die Kette jetzt die Waffe?

- **Aus 10 m:** Ja. In der alten Fassung war die Kette ein dünner roter Strich in der Faust, das Fass das Einzige, das auffiel. Jetzt liegt die helle Kette im Bogen am Boden, die gewickelte Faust ist ein heller Fleck, und die Keule ist dunkel zurückgetreten.
- **Beim Schwung:** Die Kette zieht sich als helles Band quer durchs Bild.

## Wo ich unsicher bin

- **Wicklung:** Sie wirkt aus der Nähe eher wie ein Verband, weil die Bänder glatt sind. Quer angedeutete Glieder würden sie als Kette lesen lassen.
- **Maske aus 10 m:** weiterhin das schwächste Ziel der drei Köpfe; Zähne und Nieten tragen sie.
- **Nicht jede Haltung geprüft:** Ich habe nur die verlangten Bilder gerendert (sparsam). Haken und Phase 2 habe ich nicht einzeln durchgesehen.
