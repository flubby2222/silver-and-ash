# Vorschlag: Verteilung der Katakomben-Varianten (Issue #244)

Je Fläche gibt es drei Texturen: **a** (vorhanden), **b** und **c** (neu, `assets/textures/catacomb_<wall|floor|ceiling>_<b|c>.png`). Alle haben dieselbe mittlere Farbe wie a (Wand 122/104/112, Boden 84/72/78, Decke 46/40/47), damit an keiner Grenze eine Kante entsteht. Unterschiedlich sind nur Steingröße, Fugenbild und flache Flecken.

| | a (vorhanden) | b | c |
|---|---|---|---|
| Wand | Bruchstein, vier Lagen | behauene Quader, drei Lagen, schmale Fugen, sehr ruhig | kleiner Bruchstein, fünf Lagen, weiche Feuchtespuren |
| Boden | Platten, zwei je Bahn | große Platten 1 m × 0,5 m, ausgetreten, **am ruhigsten** | Kopfsteinpflaster aus runden Steinen, **am unruhigsten** |
| Decke | Ziegel mit Ruß | glatte Großplatten | kleine Ziegel im Läuferverband |

**Vorschlag: je Raum, nach Seed, jede Fläche für sich – keine Mischung innerhalb einer Fläche.**

1. Der Seed des Raums (Seed der Ebene plus Raumnummer) wählt für Wand, Boden und Decke je eine der drei Varianten. Gleicher Seed, gleiches Bild. Ein Gang nimmt die Wahl des Raums, an dem er beginnt.
2. **Ausnahme Boden c (Pflaster):** nur in Schatz-, Geheim- und Startraum, nie in Kampf- und Bossräumen und nie in Gängen. Dort entscheiden die Treffer, und der Boden ist der Hintergrund der Gegner.
3. Innerhalb einer Fläche bleibt eine Textur, denn jede Mischung (Flecken, abweichende Steine) fügt der Umgebung zusätzliche Reize hinzu, und die Regel „Umgebung ruhiger als die Figuren“ gilt vor der Abwechslung. Die Abwechslung kommt von Raum zu Raum: wer durch eine Tür geht, merkt den Wechsel, ohne dass ein einzelner Blick unruhiger wird.
4. Nach Raumart zu verteilen (Kampf bekommt immer a/b, Schatz immer c) würde die Räume erkennbar machen, bevor man sie betritt – das ist Spielinformation, die man bewusst entscheiden sollte, nicht nebenbei durch eine Textur. Deshalb nicht.

Mischung innerhalb einer Fläche bleibt als spätere Ausbaustufe möglich (Texturen sind farbgleich, es gäbe keine Kante); sie braucht aber eine eigene Prüfung am Bild.

## Ghul aus 10 m vor dem Boden

Im Foto `ghoul-10m` ändert keine Variante das Bild merklich: Die Figur ist dort fast so dunkel wie der Boden (Nebel und Laterne ziehen beide zur Nebelfarbe). Grobe Messung (Ausschnitt um die Figur, mittlere Abweichung zur Randhelligkeit): a 9,7 / b 9,7 / c 9,7; das Rauschen im Rand des Ausschnitts ist bei c am höchsten (9,2), bei b am niedrigsten (8,7). Bei c lenkt das Pflaster also etwas mehr ab, b ist etwas ruhiger als a. Der Boden ist nicht die Ursache der Schwäche; sie liegt in der Figurenfarbe und dem Nebel bei 10 m.
