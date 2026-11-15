# 0006 – Kisten, Beute und Rarity

- **Status:** angenommen
- **Datum:** 2026-11-15
- **Löst ab:** in [0005](0005-ebenen-raeume-durchlauf.md) „Kisten sind mit 33 % verschlossen (unabhängig vom Raum)“, „Silber 15–25 pro Kiste“ und die Herkunft „vereinzelt Händler“ für v0.9 (der Händler kommt erst mit v0.11)

## Kontext

ADR 0005 legte Kisten, Schlüssel, Schatzräume und Items/Waffen nur grob fest. Beim Planen von v0.9 („Schatz & Wirtschaft“) war offen, was in einer Kiste liegt, solange Items und Waffen erst in v0.11 kommen – und der Product Owner wünschte sich ein **Rarity-System**. In einer Grilling-Session wurde geklärt, welche Stufen es gibt, wo welche Kiste steht, was sie enthält und wie Items und Waffen eingeordnet werden.

## Entscheidung

### Vier Stufen – für Kisten, Items und Waffen

| Kiste | Material | Stufe (Item/Waffe) | Farbe | verschlossen |
|---|---|---|---|---|
| **Beggar's Chest** | Holz | **Worn** | grau | 10 % |
| **Guard's Chest** | Metall | **Forged** | stahlblau | 35 % |
| **King's Chest** | Gold | **Royal** | gold | 70 % |
| **Vampire's Chest** | rot | **Vampiric** | blutrot | immer |

Jedes Item und jede Waffe wird einer Stufe zugeordnet. Namen im Spiel englisch.

### Wo welche Kiste steht

| Ort | Kisten | Beggar's | Guard's | King's | Vampire's |
|---|---|---|---|---|---|
| Kampfraum | etwa jeder 4. Kampfraum hat eine; sie **steigt aus dem Boden, wenn der Raum geräumt ist** | 70 % | 27 % | 3 % | 0 % |
| Normaler Schatzraum | 1–2 | 20 % | 45 % | 30 % | 5 % |
| Schatzraum hinter verschlossener Tür | 1–2 | 15 % | 30 % | 45 % | 10 % |
| Secret Room | 1 | – | 35 % | 50 % | 15 % |

In Kampfräumen wird die Kiste etwas besser, je weiter der Raum vom Start entfernt ist. Auch im Secret Room entscheidet der Zufall – eine feste Vampire's Chest würde, einmal entdeckt, jeden Durchlauf übermächtig machen.

### Öffnen

- **F** öffnet eine Kiste; eine offene Kiste bleibt leer stehen.
- **Verschlossen:** „[F] Unlock (1 key)“ verbraucht einen Schlüssel und öffnet sie. Ohne Schlüssel: „Locked – needs a key“.
- Der Inhalt springt heraus. **Silber und Blutphiolen** nimmt man durch Drüberlaufen auf, **Items und Waffen** (ab v0.11) mit **F** – damit man nichts aus Versehen einsammelt.

### Inhalt in v0.9 (Items und Waffen kommen in v0.11 dazu)

| Kiste | Silber | Blutphiole |
|---|---|---|
| Beggar's | 10–20 | 20 % |
| Guard's | 20–35 | 35 % |
| King's | 40–60 | 60 % |
| Vampire's | 80–120 | immer |

Keine Geweihte Munition und keine Schlüssel in Kisten.

### Silber und Schlüssel

- **Silber:** 1–3 pro Gegner. In v0.9 wird es nur gesammelt (HUD, Endbildschirm, `runs.log`); ausgegeben wird es ab v0.11 beim Händler.
- **Schlüssel** wie in ADR 0005: geräumter Kampfraum 15 %, Gegner 5 %. Grob geschätzt etwa 3 Schlüssel auf etwa 3 Schlösser pro Ebene.

### Secret Room

Hinter einer **dezent rissigen Wand**, die ein aufgeladener Klingenhieb bricht; er grenzt an einen Kampfraum und erscheint erst nach dem Betreten auf der Karte.

### Beute ab v0.11

- Eine Kiste gibt Beute **mindestens ihrer Stufe**, mit kleiner Chance auf eine Stufe höher; **70 % Item, 30 % Waffe**.
- **Keine Doppelten** in einem Durchlauf. Ist in einer Stufe nichts mehr übrig: die nächsthöhere; ist gar nichts mehr übrig: Extra-Silber (der Silberwert der Kiste noch einmal).
- **Waffen haben eine feste Stufe:** Silberklinge und Pistole sind *Worn*, die Rifle *Forged*.
- **Items:** beliebig viele gleichzeitig aktiv (Waffen-Slots sind schon in ADR 0005 festgelegt). Rosenkranz aus Silber = *Forged*, Klingenparade = *Royal*.
- **Für 1.0:** 2 Items pro Stufe (8) und 4–5 Waffen; die Liste entsteht beim Planen von v0.11.
- **Boss-Beute:** Worn 15 %, Forged 35 %, Royal 40 %, Vampiric 10 %.

### Alle Raten sind einstellbar

Jede Chance, jede Spanne und jede Verteilung oben steht als Startwert in **`balance.json`** und wird nach dem Anspielen angepasst – nichts davon ist im Code festgelegt.

### Später (nicht 1.0)

**Upgrades** für Waffen mit fester Stufe – vielleicht als eigener Raum auf den weiteren Ebenen.

## Konsequenzen

- **Positiv:** Schon vor dem Öffnen sieht man, ob sich ein Schlüssel lohnt; Erkunden (Schatzräume, Secret Room) wird sichtbar belohnt.
- **Positiv:** v0.9 baut das ganze Kisten-Gerüst mit Stufen; v0.11 muss nur noch Beute einfüllen.
- **Positiv:** Feste Waffen-Stufen lassen Platz für Upgrades nach 1.0.
- **Negativ:** Viele Tabellen und Chancen – ohne Anspielen und die Statistik aus `runs.log` sind die Startwerte nur geschätzt.
- **Negativ:** In v0.9 hat Silber noch keinen Nutzen.
