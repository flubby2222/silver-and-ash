# 0008 – Ausrüstung, Händler und Waffenkammer

- **Status:** angenommen
- **Datum:** 2026-11-16
- **Löst ab:**
  - in [0005](0005-ebenen-raeume-durchlauf.md): „Inventar (Tab)“, den pauschalen Händlerpreis von 100 für Items und Waffen, die Item-Liste (nur zwei Items) und den Meilensteinplan ab v0.11;
  - in [0006](0006-kisten-beute-rarity.md): „4–5 Waffen für 1.0“ und „jede Kiste gibt Beute“.
- **Ergänzt:** [0006](0006-kisten-beute-rarity.md): Stufen, „mindestens ihrer Stufe“, 70 % Item / 30 % Waffe und „keine Doppelten“ gelten weiter.

## Kontext

ADR 0005 und 0006 legten Waffen-Slots, Inventar, Nachladen, Items, Waffen und den Händler grob fest. Die genaue Liste der Items und Waffen sollte beim Planen von v0.11 („Ausrüstung“) entstehen. Inzwischen liegt die Karte auf Tab (#179), und die Bonus-Wahl hält die Welt noch an. In einer Grilling-Session wurde geklärt:
- welche Items und Waffen es gibt;
- wie man sie findet, ausrüstet und handelt;
- wie der Händler funktioniert;
- wie der Umfang auf die Meilensteine verteilt wird.

## Entscheidung

### Bedienung

| Taste | Wirkung |
|---|---|
| Linke Maus | Nahkampfwaffe (halten = aufladen) |
| Rechte Maus | gewählte Fernwaffe |
| Mausrad, **1–3** | Fernwaffe wechseln (1–4 wählen den Bonus, solange die Bonus-Wahl offen ist) |
| **R** | nachladen |
| **G** | Inventar auf/zu |
| **X** | Bonus-Wahl öffnen |
| Tab (halten) / M | Karte (unverändert) |
| F | Interagieren, aufheben, kaufen |

- **Nichts außer dem Esc-Menü** (und dem Endbildschirm) hält die Welt an. Inventar und Bonus-Wahl laufen im Spiel weiter; **Bewegen oder ein Treffer schließt sie**.
- **Bonus-Wahl ohne Stillstand:** Ein Stufenaufstieg zeigt nur „X: choose bonus“; mehrere stapeln sich. X öffnet die Wahl, 1–4 wählt. Schließt sie sich, wartet der Bonus weiter.

### Waffen

- **Plätze:** eine Nahkampfwaffe, bis zu drei Fernwaffen. Start mit **Silver Blade** und **Pistol**. Die letzte Fernwaffe kann man weder fallen lassen noch verkaufen.
- **Nachladen:** Fernwaffen haben ein Magazin und einen **unbegrenzten Vorrat**. R lädt nach; ist das Magazin leer, startet ein Schuss das Nachladen. Ein Waffenwechsel bricht es ab. Die Pistole hat **6 Schuss** und **0,7 s** zwischen den Schüssen.
- **Alle Nahkampfwaffen** lassen sich aufladen; jeder voll aufgeladene Nahkampfschlag bricht eine rissige Wand.
- **Rückstoß und Heranziehen** wirken auf Ghule und Fledermäuse, **nicht auf Gorm** (er nimmt nur Schaden).
- Alle Zahlen stehen in `balance.json`. Die Stufen sind Startwerte und ändern sich voraussichtlich beim Balancing.

| Waffe | Hand | Stufe | Kurz | Meilenstein |
|---|---|---|---|---|
| Silver Blade | Nah | Worn | wie bisher | – |
| Pistol | Fern | Worn | 6 Schuss, 0,7 s | v0.11 (Nachladen) |
| **Club** | Nah | Worn | 50 Schaden, etwas langsamer, stößt 3 m zurück (aufgeladen 6 m) | v0.11 |
| **Rifle** | Fern | Forged | 80 Schaden, Kopf ×3, 80 m, 1,4 s, 4 Schuss, Nachladen 2,5 s, **durchschlägt** einen Gegner | v0.11 |
| **Blunderbuss** | Fern | Forged | 9 Kugeln im 20°-Kegel, je 12 Schaden, wirksam bis 12 m, 1 Schuss, Nachladen 1,8 s | v0.11 |
| **Hunter's Axe** | Nah | Royal | langsam, breiter Hieb, viel Schaden | v0.12 |
| **Whip** | Nah | Royal | lange Reichweite (~6 m), mittlerer Schaden, **zieht Gegner heran** | v0.12 |
| **Stake Crossbow** | Fern | Vampiric | Pflock durchbohrt mehrere Gegner, langsames Nachladen | v0.12 |
| **Throwing Knives** | Fern | Vampiric | hoher Schaden, niedrige Feuerrate; **echte Geschosse** (kein Sofort-Treffer), deutlich schneller als Blutgeschosse – man muss vorhalten | v0.12 |

### Items (passiv, für den ganzen Durchlauf)

| Stufe | Item | Wirkung |
|---|---|---|
| Worn | **Hunter's Boots** | +10 % Laufgeschwindigkeit |
| Worn | **Iron Flask** | Blutphiolen heilen 50 % mehr |
| Forged | **Silver Rosary** | jeder Kill heilt 3 |
| Forged | **Quickdraw Holster** | Fernwaffen laden 30 % schneller nach |
| Royal | **Blade Parry** | die Klinge zerschlägt Blutgeschosse (#90) |
| Royal | **Hunter's Eye** | Kopftreffer +50 % Schaden |
| Vampiric | **Leech** | 5 % des ausgeteilten Schadens heilen |
| Vampiric | **Ashen Heart** | einmal: statt zu sterben mit 30 Leben weiter; dann **zerbricht** es |

Items brauchen keinen Platz; beliebig viele sind gleichzeitig aktiv. **Ashen Heart** ist die einzige Ausnahme von „keine Doppelten“: Nach dem Zerbrechen kommt es zurück in den Topf.

### Inventar (G)

- **6 Plätze** für nicht ausgerüstete Waffen; daneben die Liste der Items mit Wirkung.
- Beim Öffnen erscheint die Maus.
  - **Linksklick** rüstet aus: Nahkampf tauscht; Fern kommt auf einen freien Platz oder tauscht mit der gewählten.
  - **Rechtsklick** lässt fallen.
  - Im Raum des Händlers: **Shift + Linksklick** verkauft.
- **Inventar voll:** Waffen bleiben liegen („Inventory full“); nichts verschwindet ungefragt.

### Beute

- Ausrüstung **liegt am Boden** und leuchtet in ihrer Stufenfarbe. **„[F] Take Rifle (Forged)“** nimmt sie. Danach zeigt ein Kärtchen ein paar Sekunden Name und Wirkung.
- **Kisten** enthalten zusätzlich zu Silber und Phiole Ausrüstung mit einer Chance je Stufe:

  | Kiste | Chance |
  |---|---|
  | Beggar | 30 % |
  | Guard | 50 % |
  | King | 80 % |
  | Vampire | 100 % |

  Es gilt weiter: mindestens die Stufe der Kiste, 70 % Item / 30 % Waffe.
- **Gorm** lässt sicher Ausrüstung fallen: 80 % **Royal**, 20 % **Vampiric**.
- **Keine Doppelten.** Die Ware des Händlers ist **reserviert**: Kisten und Boss würfeln nur aus dem Rest. Ist eine Stufe leer, gilt die nächsthöhere; ist alles leer, gibt es Extra-Silber (wie ADR 0006).
- **Stufenfarbe überall**, mit den Farben der Kisten:
  - am Boden (mit Leuchten);
  - im F-Hinweis;
  - auf dem Kärtchen;
  - im Inventar;
  - im HUD (ausgerüstete Waffen mit Munition);
  - beim Händler.

### Händler: Old Plunderer

- **Raumart „Merchant“**, genau einer pro Ebene, etwa auf halbem Weg zwischen Start und Boss.
  - Er hat einen Eingang, ist ohne Schlüssel erreichbar und gegnerfrei.
  - Er ist ein **Schutzraum** wie Schatz- und Geheimraum.
  - Auf der Karte trägt er ein **Münz-Symbol**.
- **Old Plunderer:** ein buckliger Grabräuber mit Laterne und Karren voller Ware, als Platzhalter aus Kästen.
- **Seine Ware liegt vor ihm am Boden**, mit „[F] Buy … (Preis)“.
  - Gekauftes springt heraus wie aus einer Kiste. Verbrauchsgüter nimmt man dann durch Drüberlaufen auf, Ausrüstung mit F; ist das Inventar voll, bleibt sie liegen.
  - Vor dem Kauf kann man nichts durch Drüberlaufen aufheben.
  - Seine Ware erscheint **nicht auf der Karte**.
- **Angebot:** Es steht **beim Bauen der Ebene** fest (gleicher Seed, gleiches Angebot):

  | Ware | Preis | Vorrat |
  |---|---|---|
  | Blutphiole | 20 | 3 |
  | Schlüssel | 40 | 2 |
  | 2 Ausrüstungsteile (Worn / Forged / Royal / Vampiric) | 60 / 100 / 160 / 250 | je 1 |

  Bei Phiole und Schlüssel liegt nach dem Kauf das nächste Stück bereit, bis der Vorrat leer ist.
- **Zu wenig Silber:** Er sagt in einer Sprechblase **„Not gonna happen...“**.
- **Ankauf** zu **25 %** des Preises (im Inventar, Shift + Linksklick). Ein verkauftes Item verliert seine Wirkung.
- **Spezial-Händler** mit besonderen Angeboten (#105) kommen **nach 1.0**.

### Meilensteine

| Version | Inhalt |
|---|---|
| **v0.11 – Ausrüstung** | Kisten blockieren (#184), Wandbruch-Effekt (#185), Bonus-Wahl mit X, Nachladen, Waffen-Grundgerüst und HUD, Aufladen für alle Nahkampfwaffen + Club, Rifle, Blunderbuss, Inventar, Beute-System, alle 8 Items, Händler-Raum, Old Plunderer, Balancing-Runde 3, Release |
| **v0.12 – Waffenkammer** | Hunter's Axe, Whip, Stake Crossbow, Throwing Knives, Balancing, Release |
| v0.13 – Skills | Altar und erste Skills (bisher v0.12) |
| v0.14 – Modelle | bisher v0.13 |
| v1.0 | Balancing, Bugfixes, Feinschliff |

## Konsequenzen

- **Positiv:**
  - Jeder Durchlauf bekommt eine eigene Ausrüstung; Silber hat endlich einen Nutzen.
  - Das Inventar zwingt zu Entscheidungen.
- **Positiv:** Die Stufenfarben machen Beute auf einen Blick lesbar, vom Boden bis zum Händler.
- **Positiv:**
  - Ohne Händler-Fenster bleibt die Bedienung einheitlich: F für alles am Boden.
  - Ohne Stillstand bleibt die Coop-Regel (ADR 0003) gewahrt.
- **Negativ:**
  - Mit neun Waffen ist der Umfang deutlich größer als geplant.
  - Ein zusätzlicher Meilenstein schiebt Skills und Modelle nach hinten.
- **Negativ:** Viele neue Zahlen, alle geschätzt. Die Balancing-Runden 3 und 4 müssen sie einstellen.
