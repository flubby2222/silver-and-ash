# Animation, Probe 1: Klingenschlag und Nachladen

Branch `claude/animation-1-a59795`, Entwurf in `src/SilverAndAsh.Core/Presentation/WeaponPose.cs` und `src/SilverAndAsh.Game/Rendering/WeaponViews.cs`. Kein PR, nichts in `assets/`, keine Regeln, Trefferzeiten oder Balancing angefasst.

Filme (halbe Geschwindigkeit, nicht im Repo): `D:\Game\bilder\animation-1\` – `vorher\…`, `nachher1\blade-quick-half.mp4`, `nachher1\blade-charge-half.mp4`, `nachher1-r0\pistol-reload-half.mp4` (Variante A), `nachher1-r1\pistol-reload-half.mp4` (Variante B). Übersichtsbilder (`-Sheet`) stehen neben dieser Datei.

## A. Klingenschlag

### Was die Artefakte waren

- **Dunkle offene Fläche zwischen Ärmel und Manschette:** Das ist die dunkle flache Deckfläche am Ende von `arm_cuff` (UV `DARK_V`) samt Anfangsfläche von `arm_forearm`. Manschette ist starr an der Klinge, der Unterarm dreht sich zur Schulter (`BladeShoulder`). Beim alten Schlag (Gier 95°, Neigung +25°) knickt der Unterarm bis zu **80°** gegen die Manschette; die Knickstelle klafft, beide Endflächen werden sichtbar. **Ursache: Haltung (meine).**
- **Brauner Fleck am Griff:** das Holz des Griffs, an der Manschettenöffnung von der Seite gesehen, wenn die Manschette so weit kippt. Gleiche Ursache, gleiche Phase (Spitze des Schlags, Ticks etwa 4–7 von 17).
- **Modell-Seite (für den Grafikdesigner, nicht dringend):** Ein Kugelgelenk – Manschettenende und Unterarmanfang als abgerundeter Ring/Kugel statt flacher Deckflächen – würde Knicke bis etwa 90° unsichtbar machen. Dann könnte der Schlag wieder größer werden. Außerdem: der Knauf (graues Oval links, `sword`) ragt im Höhepunkt über den Ärmel – gebaut, aber wirkt fremd; ein kürzerer Knauf oder eine Kappe am Ärmel wäre ruhiger.

### Neue Haltung (alles in Code)

| Wert | alt | neu |
|---|---|---|
| Gier der Spitze (`SwingYawDegrees`) | 95° | **50°** |
| Neigung dazu (`SwingRollDegrees`) | +25° | **+8°** |
| Spitze nach unten (`SwingDipDegrees`) | – | **12°** (Hieb statt Wischen) |
| Handverschiebung (`SwingShift`) | (0,06 / 0,04 / 0) | **(0,09 / 0 / 0,03)** |

Zeitverlauf (`WeaponPose.SwingCurve`, Anteil der Schlagzeit, 17 Ticks):
1. **Schlag** 0–25 % (4 Ticks): `1-(1-u)³` – sehr schnell raus, bremst am Ende.
2. **Halten + Überschwingen** 25–40 % (2–3 Ticks): 1 + 0,06·sin(πu) – hier trifft die Klinge, kurzes Nachfedern.
3. **Rückweg** 40–100 % (10 Ticks): smoothstep zurück, ab 55 % ein kleiner Unterschwinger (4 % der Weite), dann ruhig.

Die **Dauer (17 Ticks, `SwingSeconds` 0,28 s) und die Trefferzeit sind unverändert** – es ändert sich nur, wie der Wert 0..1 verläuft. Der Aufladeschlag nutzt dieselbe Kurve und denselben Zielwinkel; das Ausholen (`WindUp*`) ist unverändert.

Pivot: Haltung um den Griff der Klinge (`BladeGrip`), der Unterarm dreht sich um seinen Gelenkpunkt zur Schulter (unverändert).

Ergebnis (Einzelbilder durchgesehen): Der Ärmel reicht links bis aus dem Bild, keine dunkle Deckfläche, kein brauner Fleck in keiner Phase von `blade-quick` und `blade-charge`.

## B. Nachladen mit der Pistole

Die Ansicht liest `Magazine.ReloadProgress` (0→1 über `ReloadTime`, 1,5 s Pistole). Eingeblendet wird in 0,1 s (`_reloadWeight`); bricht der Waffenwechsel das Nachladen ab (`CancelReload`), blendet es in 0,1 s aus statt zu springen. `ReloadFinished` setzt sofort zurück.

Phasen (Anteil der Nachladezeit, bei 1,5 s):
1. **Weg** 0–20 % (0,3 s): smoothstep.
2. **Halten** 20–80 % (0,9 s) mit zwei kleinen Klopfern der Hand (Munition einlegen, Betrag von sin, 1,2 cm) zwischen 30 und 70 %.
3. **Zurück** 80–92 % (0,18 s): smoothstep.
4. **Bereit-Signal** 92–100 %: Hammer springt gespannt (Hammer bleibt vorher „gefallen“) plus ein kleiner Ruck der Hand (1,2 cm hoch/zurück, sin).

In beiden Varianten bleibt die Pistole rechts der Bildmitte; die Klinge links wird in den Bildern nie berührt.

- **Variante A „Kippen“** (nur Probe, nicht übernommen; stand als `ReloadVariant = 0` im Entwurf): Roll **70°** nach rechts weg, Mündung **25°** hoch, Hand (+2 cm rechts, +1 cm hoch). Sieht aus wie „aufklappen und laden“, Pistole bleibt ganz im Bild.
- **Variante B „Wegtauchen“** (**vom Menschen gewählt, im Spiel mit PR #292**): Roll 25°, Mündung **40°** hoch, Hand (+5 cm rechts, −7 cm unten, −2 cm zurück). Die Pistole steht fast senkrecht am rechten Rand, die Waffe „verschwindet“ halb. Lesbarer neben dem HUD-Hinweis (der Text „Reloading…“ rechts unten bleibt frei), aber ruppiger.

Empfehlung war A; der Mensch hat **B** gewählt. Beim Übernehmen deckte B im Haltebereich etwa ein Viertel der Nachlade-Anzeige des HUDs („Reloading…“ samt Leiste) ab (Test über das ganze Nachladen). Darum wurde die Handverschiebung von (+5 / −7 / −2 cm) auf **(+1 / 0 / −2 cm)** zurückgenommen; Roll 25° und Mündung 40° hoch blieben. Die Pistole „taucht“ also weniger nach unten, sie kippt und hebt die Mündung.

## Tests

- `dotnet build -c Release`: ohne Warnung und Fehler.
- `dotnet test` (ganze Lösung): **1680 von 1680 grün**. Keine alten Winkel festgenagelt; `SleeveForearmTests` und `WeaponPoseTests` (25 Tests) bestehen mit den neuen Werten. Sie waren die Grenze für den Schlag: bei Verschiebung nach rechts ≥ 0,10 (mit z = 0) schneidet die Nahebene den Unterarm im Bild auf.
- Rote Tests: keine.

## Wo ich unsicher bin

- **Der Schlag ist kleiner geworden** (Gier 50° statt 95°, Spitze erreicht die Bildmitte nicht mehr). Das nimmt Reichweitengefühl; der Ausgleich sind das Gewicht (schneller Schlag, Halten, Nachfedern) und die Hiebneigung. Wenn der Mensch mehr Ausholen will, braucht es erst das Kugelgelenk im Modell, dann wieder größere Winkel.
- **Klinge kreuzt die Pistole** im Höhepunkt (Spitze über dem Lauf, Pistole in Ruhe). Nicht verdeckend, aber nah. Eine Prüfung der Kombination „Schlag und Nachladen gleichzeitig“ und „Schuss im Schlag“ habe ich **nicht als eigene Szene gerendert**: die Hände sind unabhängig, die Schlagbewegung bleibt links der Bildmitte, Variante A kippt rechts weg; eine Gegenprobe im Spiel steht aus.
- (Erledigt mit #292: Nachladen ist jetzt getestet.) Früher: Nachladehaltungen **nicht durch Tests abgedeckt** (Reichweite 0,30, Nahebene, Unterarmende): nur im Bild geprüft. Vor dem Übernehmen lohnt ein Test für `ReloadOffsets` über p = 0…1 beider Varianten.
- Das Bereit-Signal ist ein Klick des Hammers und ein Ruck – **ohne Ton**; eine Hörprobe für „Nachladen fertig“ ist Sache des Audiodesigners.
- Einen weiteren Zwischenzustand (Nachladen, während eine Waffe gewechselt wird) deckt das Ausblenden ab; als Szene nicht gerendert.
