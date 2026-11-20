# Musterraum

Der Musterraum (auch „Schauraum“, englisch *showroom*, Issue #283; Begriffe im [Glossar](glossar.md)) zeigt alle Bauteile des Spiels nebeneinander – unter Spielbedingungen, mit Licht, Nebel, Bild-Variante, Musik und Geräuschen. Er ist für Grafikdesigner, die ein neues Modell in allen Wandvarianten und Raumhöhen sehen wollen, und für Code-Sitzungen, die etwas an Türen, Kisten oder Gegnern ändern.

## Starten

```powershell
dotnet run --project src/SilverAndAsh.Game -- --showroom
```

Nach dem Titelbildschirm stehst du am Anfang des Rundgangs. Dazu passt nur `--look <1-4>`; `--level`, `--seed`, `--give`, `--photo`, `--bench` und `--clip` lehnt der Schalter mit einer Meldung ab.

**Nur in der Debug-Version** (`dotnet run` baut Debug). Wie `--give` und `--photo` ist der Musterraum ein Werkzeug für die Entwicklung: Wer dort steht, kann nicht sterben und hat alle Waffen. Das gehört nicht in die Version, die Spieler bekommen. Die Release-Version meldet „works only in the debug version“.

| Taste | Wirkung |
|---|---|
| **L** | Schilder aus- und einblenden |
| **K** | Klangpaket wechseln (wie im Spiel) |
| **N** | Ton und Musik stumm |
| **1 / 2 / 3**, Mausrad | Pistole, Gewehr, Donnerbüchse; die Keule liegt im Inventar (**G**) |
| **F** | Türen und Kisten öffnen; die verschlossene Tür braucht den Schlüssel, der vor ihr liegt |

## Was zu sehen ist

Der Rundgang ist eine Schleife aus **drei Abschnitten**, einer je Wandvariante **A, B, C** (Wände, Boden und Decke in derselben Variante). Jeder Abschnitt hat zwei Teile nebeneinander: zuerst einen schmalen Gang in **Ganghöhe (3,5 m)**, dann einen breiteren Raum in **Raumhöhe (6 m)**. Gänge in Ganghöhe (Variante A) verbinden die Abschnitte, so dass man im Kreis gehen kann.

- **Einmal je Wandvariante**, im Gangteil, stehen in der Nordwand alle Wandstücke: Holztür, verschlossene Tür (Schlüssel davor am Boden), Bosstor und rissige Wand. Hinter jeder steht eine kleine Kammer, so dass sie sich öffnen bzw. zerschlagen lässt (die rissige Wand mit einem voll aufgeladenen Nahkampfschlag).
  - **Warum im Gang:** Im Spiel haben alle Türzellen Ganghöhe, auch die des Bosstors (geprüft an 100 erzeugten Ebenen). So sieht man jede Tür, wie sie im Spiel steht.
  - **Warum schmal:** Der Gang ist drei Zellen breit. Wer in der Mitte geht, ist höchstens zwei Zellen von jeder Tür entfernt. Die Laterne ist das einzige Licht, also liegt keine Tür im Dunkeln.
- **Einmal**, verteilt auf die drei Raum-Teile, mit mindestens 6 m Abstand: die vier Kisten (offen zu öffnen), die Wendeltreppe (steht, ist aber kein Ausgang), Ghul, Fledermaus und Gorm, das Armbrust-Skelett in Ruhe und beim Ankündigen (nur als Figur, ohne Körper und ohne Schuss; Issue #497), die Waffen und vier Gegenstände (je einer jeder Stufe) in ihrer Stufenfarbe, Silber, Blutphiole und Weihmunition.
- Über jedem Stück steht sein englischer Name, an jedem Teil „Wall A - corridor 3.5 m“ usw. Schilder hinter Wänden, weiter als 16 m entfernt oder nicht ganz im Bild werden nicht gezeichnet.

**Gegner sind Ausstellungsstücke:** Sie bewegen sich nicht, greifen nicht an und nehmen keinen Schaden. Es gibt keine Arenen, keine Siegel, keinen Bosskampf und keinen Ebenenwechsel. Silber, Schlüssel, Phiole und Munition nimmst du wie im Spiel beim Darüberlaufen auf – dann fehlen sie bis zum nächsten Start.

**Ausrüstung nimmst du mit F** (Issue #317): Waffen und Gegenstände nimmst du wie im Spiel mit F, nicht beim Darüberlaufen. Danach zeigt links eine Karte Name und Wirkung.

**Stufenfarben:** Es gibt bisher nur Waffen der Stufen *Worn* und *Forged*. Alle vier Stufen zeigen die Gegenstände: Hunter's Boots (*Worn*), Silver Rosary (*Forged*), Hunter's Eye (*Royal*) und Leech (*Vampiric*). Sie sind Platzhalter: ein Würfel in der Stufenfarbe, noch ohne eigenes Modell und ohne Wirkung.

## Ein Stück ergänzen

Alle Stücke stehen in **einer Liste**: `ShowroomPieces.All` in [src/SilverAndAsh.Core/Showroom/ShowroomPiece.cs](../src/SilverAndAsh.Core/Showroom/ShowroomPiece.cs). Ein neues Modell einer vorhandenen Art ist **ein Eintrag mehr**, sonst nichts:

```csharp
new ChestPiece("Bone chest", ChestTier.Bone),       // eine neue Kistenstufe
new WallPiece("Iron gate", DoorKind.IronGate),      // eine neue Türart: steht in jedem Gangteil (A, B, C)
new WeaponPiece("Crossbow (Royal)", WeaponKind.Crossbow),
```

Der Aufbau (`ShowroomLayout.Build`) rechnet die Länge der Teile aus der Liste aus und verteilt die Stücke selbst; die Tests prüfen, dass jedes Stück vorkommt, erreichbar ist und keines auf einem anderen steht. Nur eine **neue Art** von Stück (z. B. eine Falle) braucht eine neue Unterklasse von `ShowroomPiece` und eine Zeile in `ShowroomLayout.Build`, die sie in die Welt setzt.

Einschränkung: Es gibt nur einen Gorm je Welt; ein zweiter `EnemyPiece` mit `ShowroomEnemy.Gorm` wird abgelehnt.

## Fotos

Je Teil gibt es eine Foto-Szene mit genau diesem Aufbau (siehe [foto-skript.md](foto-skript.md)):

```powershell
dotnet run --project src/SilverAndAsh.Game -- --photo showroom-a-corridor --out fotos
```

Szenen: `showroom-a-corridor`, `showroom-a-room`, `showroom-b-corridor`, `showroom-b-room`, `showroom-c-corridor`, `showroom-c-room`. Der Blick geht vom Westende des südlichen Laufwegs schräg auf die Nordwand; weit entfernte Stücke liegen – wie im Spiel – im Dunkeln. Das Armbrust-Skelett hat eigene Szenen von vorn: `crossbow-skeleton-rest-4m`, `crossbow-skeleton-announce-4m` und `crossbow-skeleton-announce-2m`. Für ein bestimmtes Stück aus der Nähe baut `ShowroomLayout.NewWorldAt(x, z, blickziel)` eine Welt mit dem Spieler an einer Zelle.

## Wie es funktioniert

- **Aufbau in `Core`, ohne Raylib:** [ShowroomLayout.cs](../src/SilverAndAsh.Core/Showroom/ShowroomLayout.cs) baut aus der Liste Zellen, Türen, Kisten, Oberflächen-Varianten, Ausstellungsstücke und Schilder – immer gleich, ohne Zufall. Tests: [tests/SilverAndAsh.Core.Tests/Showroom/](../tests/SilverAndAsh.Core.Tests/Showroom/).
- **Harmlose Gegner:** Eine `GameWorld` mit `Showroom` (den Ausstellungsstücken) ist ein Musterraum: Gegner handeln nicht, `Damage` wirkt nicht, die Treppe ist kein Ausgang. Eine normale Welt hat nie Ausstellungsstücke – Spiel, Generator und Balancing bleiben unberührt (der Balance-Report ist vorher und nachher gleich).
- **Schilder im `Game`:** [ShowroomLabelView.cs](../src/SilverAndAsh.Game/Rendering/ShowroomLabelView.cs) rechnet die Schild-Positionen aus dem kleinen 3D-Bild auf den Bildschirm um und zeichnet sie scharf darüber; `ShowroomLabelSight` in `Core` entscheidet, ob ein Schild zu sehen ist.
