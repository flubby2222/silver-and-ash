# 0004 – Fortschritt: Stufen, Beute, Skills

- **Status:** angenommen, teilweise abgelöst durch [0005](0005-ebenen-raeume-durchlauf.md) (was davon zu 1.0 gehört, Bonus-Wahl ohne Stillstand)
- **Datum:** 2026-11-15
- **Löst ab:** in [0001](0001-projektgrundlagen.md) die Zeilen zu *Blutrausch*, *Items* und den Meilenstein v0.7

## Kontext

Nach v0.6 (Ghul und Fledermaus) stand laut [0001](0001-projektgrundlagen.md) für v0.7 an: „Items, Blutrausch, Balancing-Datei“. Beim Planen wurde klar, dass das Spiel einen **Fortschritt innerhalb eines Durchlaufs** braucht – der Spieler soll stärker werden, und Beute soll unterschiedlich wirken. Außerdem soll der Zufall (Drops) nicht alles entscheiden.

Zur Wahl standen u. a.: Items fest im Level oder als Gegner-Drops; Blutrausch als feste Fähigkeit oder als Skill; Balancing-Werte statisch beim Start geladen oder als Objekt in der Spielwelt.

## Entscheidung

### Charakter-Stufen
- Gegner geben **Erfahrung**. Genug Erfahrung → **Stufenaufstieg**.
- Beim Aufstieg **wählt** der Spieler **einen von 3 Boni** (z. B. mehr Leben, mehr Schaden, mehr Ausdauer). Später kommen **Skillpunkte** dazu.
- **Tod = Stufe 1:** Die Stufe gilt nur für den laufenden Durchlauf (wie der Neustart auf Ebene 1).

### Beute – drei Arten
| Art | Wirkt | Beispiele | Woher |
|---|---|---|---|
| **Verbrauchsgut** (*Consumable*) | **sofort beim Aufheben**, kann nicht aufgespart werden | Blutphiole (+50 Leben), Geweihte Munition (Pistole ×2 für 15 s) | Gegner-Drops (Zufall mit Seed) |
| **Item** | **passiv für den ganzen Durchlauf**, kein Inventar – man sammelt sie an | z. B. Geschosse mit der Klinge zerschlagen (#90) | später: Boss-Drops, Kisten, Händler |
| **Waffe** | neue Waffe | – | später: Boss-Drops, Kisten, Händler |

Bosse lassen später **sicher** ein Item oder eine Waffe fallen.

### Kisten und Schlüssel (später)
- **Kisten** stehen in den Leveln und enthalten Beute. Beute hat eine **Seltenheit** (*Rarity*).
- Es gibt ein **Schlüsselsystem**: **verschlossene Kisten** und **verschlossene Räume**. Manche Kisten sind offen.
- **Je seltener der Inhalt, desto wahrscheinlicher ist die Kiste verschlossen.**
- Kisten kommen zusammen mit Items und Waffen – vorher hätten sie kaum etwas Besonderes zu bieten.

### Skills, Altare, Händler (später)
- **Blutrausch** wird ein **Skill** statt einer festen Fähigkeit.
- **Altare** in den Leveln schalten Skills frei – planbar statt Zufall, das schwächt den Zufall der Drops ab.
- **Händler:** einer als Standard, weitere mit besonderen Angeboten.

### Balancing-Werte
Die Werte kommen aus einer **JSON-Datei** und werden zu einem **unveränderlichen Werte-Objekt**, das die `GameWorld` bekommt – kein veränderlicher statischer Zustand (Coop-Regel 4). Tests können eigene Werte setzen; Neuladen heißt: neue Welt mit neuen Werten.

### Neuer Plan für v0.7
**Balancing-Datei, Consumable-Drops, Charakter-Stufen.**

Skills (mit Blutrausch), Altare, Händler, Items, Waffen, Kisten mit Schlüsseln und Bosse kommen **danach**. Ob sie noch zu 1.0 gehören oder später, entscheiden wir beim Planen von v0.8.

## Konsequenzen

- **Positiv:** Jeder Durchlauf fühlt sich anders an (Drops, Aufstiegs-Wahl), ohne dass der Zufall alles bestimmt (Altare, Händler).
- **Positiv:** Klare Begriffe (Verbrauchsgut / Item / Waffe) – jede neue Beute passt in eine der drei Schubladen.
- **Positiv:** Das modulare Schadenssystem aus v0.4 trägt: Geweihte Munition und Schadens-Boni sind einfach weitere Schadensregeln.
- **Negativ:** Der Umfang wächst gegenüber 0001 deutlich; 1.0 muss beim Planen von v0.8 neu geschnitten werden.
- **Negativ:** Das Werte-Objekt bedeutet einen größeren Umbau – alle `Tuning.X`-Stellen ändern sich.
- **Negativ:** Die Aufstiegs-Wahl braucht ein Auswahlfenster, während das Spiel steht – ein Vorgeschmack auf die Pause (v0.8).
