# Video zu den Waffenärmeln (Issue #265)

**Video:** „Blender 3D | FPS Viewmodel Animation Posing Tutorial“ von ShortStak, 33:56, [https://www.youtube.com/watch?v=ZkrYSDrTL08](https://www.youtube.com/watch?v=ZkrYSDrTL08). Mit dem Skill `watch:watch` lokal geladen (Untertitel automatisch, Englisch); 40 Übersichtsbilder (512 px) plus 8 Einzelbilder in 1280 px. Ich habe **8 Übersichtsbilder und 4 Einzelbilder** wirklich angesehen (1:42, 2:10, 3:24, 5:36, 9:18, 14:40 und weitere aus der Übersicht); die übrigen sind fast alle derselbe Blender-Bildschirm. Nichts daraus liegt im Repository (Arbeitsdateien des Skills im Scratchpad außerhalb).

**Worum es geht:** Ein Anleitungsvideo, wie man in Blender die **Haltungen** („Posen“) einer Ich-Sicht-Waffe mit fertigem Arm-Rig einstellt: Gewehr (M16-Form), dann Maschinenpistole, jeweils Feuerhaltung und Ruhehaltung. **Es zeigt nicht, wie man Arme modelliert oder texturiert**; Dreiecke, Texturgröße und Faltenmalerei kommen im Video nicht vor. Was es zeigt, ist, **wo Waffe, Hand und Unterarm im Bild sitzen**. Das ist genau die Frage, die wir bisher nicht gestellt haben.

## Beobachtungen mit Zeit

- **0:30 bis 1:08, 9:17:** Die Waffe wird so hoch gezogen, dass man „kaum noch das Ende des Schaftes sieht“; dazu wird der Blickwinkel auf **90 Grad** gestellt. (Unser Spiel: 70 Grad senkrecht.)
- **1:40 bis 2:10:** Schulterpunkt und Handgelenk werden so gesetzt, dass **das Handgelenk nicht verdreht aussieht und nicht nach oben zeigt**, sondern „gerade“ in der Verlängerung des Unterarms liegt.
- **2:16 bis 5:35:** Die Finger werden eng um den Griff gelegt, **Lücken zwischen Hand und Waffe werden geschlossen** (4:32), die Knöchel stehen in einer Reihe (5:26). Gegen Ende ist die Faust ein geschlossener dunkler Körper.
- **3:59 bis 4:15:** Regel: **Nicht zu viel vom Bild verdecken**; die Mitte des Bildschirms bleibt frei (Verweis auf Call of Duty).
- **Bild 5:36 (Kamerablick):** Die Waffe sitzt im **unteren rechten Bilddrittel**, die Mündung zeigt schräg nach oben zur Bildmitte. Der Unterarm **kommt von der unteren Bildkante** (etwa im Bereich 35 bis 60 Prozent der Breite) und steigt diagonal, mit rund **einem Drittel der Bildhöhe** sichtbar, zur Faust. Die Hand ist ein **schwarzer Handschuh aus großen Flächen** (Fingerrücken, Knöchel), am Handgelenk liegt eine **breite helle Stulpe/Binde mit Streifen** (olivbraune Bänder auf dunklem Grund), darüber der dunkle Ärmel. Die Faust deckt den Griff fast ganz.
- **9:49 bis 10:04:** „Die Arme sollen entspannt wirken“: Ellbogenpunkt wird so gesetzt, dass der Unterarm natürlich liegt.
- **Bild 14:40, 18:20 (Maschinenpistole):** gleiche Anordnung: Faust am Griff, Stulpe mit Binde am Handgelenk, Unterarm von unten aus dem Bild.

## Was wir bisher falsch gemacht haben, gemessen am Video

1. **Haltung zu tief.** Wir halten Pistole (y = −0,08 m) und Klinge (y = −0,095 m) so tief, dass das Handgelenk fast am unteren Bildrand liegt; der Unterarm läuft sofort aus dem Bild, sichtbar ist nur die Ärmelöffnung von oben (Scheibe, Klumpen, Fleck). Das Video hält die Waffe im unteren Drittel, aber so hoch, dass **ein Drittel der Bildhöhe Unterarm** sichtbar bleibt. **Deine Vermutung stimmt.**
2. **Blickwinkel 70 Grad statt 90.** Mit 90 Grad sieht man bei gleicher Lage mehr vom Arm. Das ist eine Spielentscheidung; ich habe ihn nicht verändert.
3. **Kein Abschluss am Handgelenk, der sich als Stulpe liest.** Im Video ist es eine breite, hellere Binde mit Streifen zwischen dunkler Faust und dunklem Ärmel; wir hatten einfarbige Röhren.
4. **Hand weglassen funktioniert so nicht.** Das Video zeigt immer eine Faust in großen Flächen. Ohne sichtbare Hand fehlt die Form, die den Arm als Arm erkennbar macht; unsere handlosen Entwürfe b und c waren deshalb Scheiben. **Das ist eine Entscheidung des Menschen:** ein Handschuh in wenigen großen Flächen, kein Fingerwerk, oder die Haltung so wählen, dass die Öffnung auch ohne Hand liest (Entwurf d kann beides nicht ersetzen).

## Haltungswerte für die Probefotos (lokal, nicht committet)
In `src/SilverAndAsh.Core/Presentation/WeaponPose.cs` nur für die Fotos geändert:
- `PistolGrip`: (0,095; −0,08; 0,18) → **(0,09; −0,04; 0,18)** (4 cm höher, 0,5 cm näher zur Mitte).
- `BladeGrip`: (−0,11; −0,095; 0,19) → **(−0,11; −0,07; 0,19)** (2,5 cm höher).
- Alles andere (Roll, Rückstoß, Ausholen, Schwung, FOV, `ShrinkTowardsEye`) unverändert. Der Code-Test zu `Reach` (30 cm) ist nicht gelaufen und muss beim Einbau geprüft werden; höher gehalten liegt mehr Arm im Bild, aber die Teile liegen näher an der Mitte.

## Entwurf d (nach dem Video)
Ordner `modelle_d/` (Namen wie im Spiel), Skripte `pistol.py`, `blade.py`, `arm.py` (`AERMEL=d`), `fotos.sh`, `blatt.py`; Blatt `blatt_heute_und_d.png` (links heute, rechts d; oben Gang, Mitte Schuss, unten aufgeladene Klinge). Aufbau: Handschuh in großen Flächen (Fingerrücken und Knöchel hell abgesetzt), Stulpe mit zwei hellen Riemen und Schnallen, Ärmel mit langen Faltenbahnen; Unterarm läuft nach rechts unten (Pistole) bzw. links unten (Klinge) aus dem Bild; dieselbe Geometrie wie Entwurf a (376 / 396 Dreiecke), Texturen neu.

**Was noch nicht stimmt:**
- Der Arm ist jetzt als Arm lesbar (Achse, Länge, Faust); das ist der große Gewinn der höheren Haltung. Aber der Handschuh sieht im Spiel **wie ein grauer Stahlhandschuh** aus, nicht wie Leder (die 32-Farben-Palette des Atlas zieht das Blau/Grau zusammen), und die linke Hand an der Klinge wirkt wie eine **Kralle** aus vielen kleinen Teilen.
- Die Stulpenriemen sind im Foto kaum zu erkennen.
- Es ist wieder eine sichtbare Hand mit Fingern. Das Video zeigt, dass es so üblich ist; der Mensch hatte so etwas abgelehnt. Er sollte entscheiden, ob ein Handschuh in wenigen größeren Flächen (weniger Dreiecke an den Fingern) akzeptabel ist; das Fingerwerk ließe sich vereinfachen.
- Ärmel und Handschuh sind als Farben nicht klar getrennt, die Palette müsste dafür umgebaut werden.
