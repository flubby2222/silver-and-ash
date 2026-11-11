# 0003 – Online-Coop offen halten

- **Status:** angenommen
- **Datum:** 2026-11-11

## Kontext

Online-Coop ist nicht Teil von v1.0. Der Product Owner möchte die Option aber offen halten (Issue #53).

Netzwerk-Code lässt sich nur dann gut nachrüsten, wenn die Spiellogik von Anfang an ein paar Eigenschaften hat. Fehlen sie, muss man später große Teile von `SilverAndAsh.Core` umschreiben. Baut man sie gleich mit ein, kostet das heute fast nichts.

Zur Wahl standen drei übliche Modelle:

- **Lockstep:** Alle Rechner rechnen dieselbe Simulation und tauschen nur Eingaben aus. Die Ergebnisse müssen auf allen Rechnern **bitgenau** gleich sein. Mit Kommazahlen (`float`) in C# ist das schwer zu garantieren, etwa bei `MathF.Sin` auf verschiedenen Prozessoren.
- **Rollback:** Wie Lockstep, nur wird bei verspäteten Eingaben zurückgespult und neu gerechnet. Das braucht ebenfalls Bitgenauigkeit und zusätzlich schnelles Speichern und Laden des ganzen Zustands.
- **Host als Schiedsrichter** (in der Fachsprache *autoritativer Server*): Ein Rechner entscheidet, die anderen schicken Eingaben und bekommen den Zustand. Die eigene Figur wird lokal vorausberechnet (*Client-Vorhersage*) und bei Abweichung korrigiert. Kleine Rechenunterschiede schaden hier nicht.

## Entscheidung

**Modell für später: Host als Schiedsrichter.**

Ab jetzt gelten für `SilverAndAsh.Core` diese fünf Regeln:

1. **Nie „der Spieler“, immer „ein Spieler“.** Code geht nicht davon aus, dass es genau einen Spieler gibt. Gegner wählen ein Ziel aus den vorhandenen Spielern, und Kamera und HUD gehören zu „ihrem“ Spieler. Solange es nur einen gibt, darf die Liste natürlich einen Eintrag haben.
2. **Alles, was das Spiel verändert, kommt als Eingabe in den Tick.** Keine Tastatur-, Maus- oder Uhrzeit-Abfragen in `Core`. Was ein Mensch tut, steht in `PlayerInput`, und was die Zeit tut, ist der Tick.
3. **Zufall nur aus einem eigenen Zufallsgenerator mit Startwert (Seed),** der zum Spielzustand gehört. Kein `Random.Shared`, kein `new Random()` ohne Seed. Nebenbei lässt sich so jeder Durchlauf nachspielen, was auch bei Fehlern hilft.
4. **Der Spielzustand liegt in klaren Objekten.** Kein veränderlicher Zustand in `static`-Feldern. So lässt sich der Zustand später als Ganzes erfassen und übertragen. Feste Werte (`const`, `static readonly` für Unveränderliches) sind erlaubt.
5. **Sound, Effekte und Bildschirmwackeln reagieren auf Ereignisse aus `Core`** („Treffer an Position X“) und werden nicht direkt in der Spiellogik ausgelöst. So kann auch der zweite Rechner sie abspielen.

### Stand bei dieser Entscheidung

- Regeln 2 bis 4 sind erfüllt: kein Zufall, keine Uhrzeit in der Simulation (nur im Log) und kein veränderlicher `static`-Zustand in `Core`.
- Regel 1 betrifft heute nur `Program.cs`, das genau einen `Player` anlegt. Das ist für ein Spiel mit einem Spieler richtig und wird erst beim Coop selbst angepasst.
- Regel 5 wird ab den ersten Sounds und Effekten wichtig (Kampf, ab v0.4).
- Das Umsehen mit der Maus läuft pro Bild (`Player.Look`) und nicht pro Tick, damit es sich direkt anfühlt. Fürs Netz schickt man später zusätzlich pro Tick die Blickrichtung mit. Das ist eine kleine Änderung und kein Hindernis.

## Konsequenzen

- **Positiv:** Ein späterer Online-Coop kostet grob 2 Meilensteine statt eines halben Neustarts.
- **Positiv:** Die Regeln helfen auch ohne Coop. Seeds machen Durchläufe nachspielbar, Ereignisse trennen Logik und Darstellung sauberer, und Tests werden leichter.
- **Negativ:** Etwas mehr Nachdenken bei neuen Systemen (Gegner-KI, Effekte, Zufall). Claude prüft die eigene Arbeit gegen diese Regeln.
- **Offen:** Die Netzwerk-Bibliothek wird erst beim Einbau gewählt. Das ist ein neues NuGet-Paket und braucht die Zustimmung des Product Owners.
