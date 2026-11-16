# Modelle im Spiel

Wie das Spiel ein Modell der Designer lädt, bewegt und zeichnet (Issue #232, Stil aus [ADR 0010](entscheidungen/0010-stil-ps1-nah-texturen.md)). Erster Einsatz: der Ghul.

## Die Datei

- **Ablage:** `assets/models/<name>.glb`, Textur daneben als `<name>.png` (zur Ansicht; das Spiel nimmt die in der `.glb` eingebettete). Erzeuger-Skript `tools/models/<name>.py`, Eintrag in [`assets/HERKUNFT.md`](../assets/HERKUNFT.md).
- **Format:** glTF binär (`.glb`). x seitlich, y oben, z vorn, Meter; Figuren blicken nach +z.
- **Teile:** Jedes bewegliche Teil ist ein eigener Knoten mit genau einem Mesh aus einem Primitiv (Dreiecksliste mit Indizes). Die Verschiebung des Knotens ist sein Drehpunkt. Knoten sind nicht verschachtelt und nicht gedreht oder skaliert. Was davon abweicht, lehnt der Lader ab.
- **Daten je Ecke:** Position, Normale, Texturkoordinate. Keine Vertex-Farben.

## Wie Raylib lädt (geprüft, Raylib-cs 8.1 = raylib 5.5)

- `LoadModel` legt **ein Mesh je Knoten mit Mesh** an, in der **Reihenfolge der Knoten** in der Datei (Blender schreibt sie alphabetisch).
- Es **rechnet die Knoten-Verschiebung in die Ecken ein**: Die Ecken liegen danach dort, wo sie im Modell stehen, nicht um den Drehpunkt.
- **Namen und Drehpunkte gehen verloren.** Deshalb liest [`GlbScene`](../src/SilverAndAsh.Core/Presentation/GlbScene.cs) (Core, getestet) die Beschreibung der Datei selbst: Namen, Drehpunkte, Zahl der Ecken, kleinste Ecke je Teil.
- Der Lader ([`TexturedModel`](../src/SilverAndAsh.Game/Rendering/TexturedModel.cs)) **verlässt sich nicht auf die Reihenfolge**, sondern prüft je Teil: gleiche Zahl der Ecken, und die kleinste Ecke von Raylib liegt bei „kleinste Ecke der Datei + Drehpunkt“. Stimmt etwas nicht, nimmt er das Modell nicht.
- Danach kopiert er die Ecken **relativ zum Drehpunkt** (Raylib-Ecke − Drehpunkt), die Normalen, Texturkoordinaten und Indizes.
- **Textur:** Raylib lädt die eingebettete PNG ohne Mipmaps (geprüft: `Mipmaps == 1`); das Spiel stellt den Filter auf `Point`. Das `minFilter`-Feld der Datei (`NEAREST_MIPMAP_NEAREST`) wird nicht gebraucht.

## Teile drehen

- Wer an wem hängt, steht im Code (z. B. [`GhoulPose.Parts`](../src/SilverAndAsh.Core/Presentation/GhoulPose.cs)), die Drehpunkte kommen aus der Datei.
- [`Rig`](../src/SilverAndAsh.Core/Presentation/Rig.cs) (Core, getestet) rechnet je Teil: `Rahmen = Drehung × Verschiebung(Drehpunkt − Drehpunkt der Eltern) × Rahmen der Eltern`; ohne Eltern ist der „Rahmen der Eltern“ die Lage der ganzen Figur in der Welt. (System.Numerics: Zeilenvektoren, `a × b` heißt „erst a, dann b“.)
- „Heben“ um x heißt: das vordere Ende geht nach oben, ein hängendes Teil schwingt erst nach vorn. Das ist `Rig.Lift(grad)` = Drehung um −x. Im Bild geprüft (Ghul beim Ausholen: Arme oben, Kopf höher).

## Zeichnen

- **Ein Weg für die ganze 3D-Welt:** Modelle gehen durch denselben Shader wie Level und Platzhalter ([`assets/shaders/level.fs`](../assets/shaders/level.fs)): Laterne, Nebel und später die 15-Bit-Körnung gelten für alles gleich. Der Shader multipliziert jetzt mit der Textur; alles ohne Textur bekommt Raylibs weiße 1 × 1-Textur und sieht unverändert aus (geprüft: alle Fotos ohne Ghul sind byte-gleich mit vorher).
- **Ecken in C# in die Welt:** wie bei den Platzhalter-Quadern werden die Ecken jedes Bild in C# gedreht und über `Rlgl` gezeichnet. So bekommt jede Ecke die Helligkeit ihrer Normale **nach** dem Drehen.
- **Weiche Schattierung (Gouraud):** [`SmoothShading`](../src/SilverAndAsh.Core/Presentation/SmoothShading.cs) (Core, getestet) mischt die sechs Helligkeiten der flachen Schattierung mit den Quadraten der Normalen-Anteile: `x²·(x>0 ? +x : −x) + y²·(y>0 ? oben : unten) + z²·(z>0 ? +z : −z)`. Die Grafikkarte lässt den Wert über das Dreieck verlaufen.
- **Treffer-Blitz:** Ein Quader wechselt einfach die Farbe; ein Modell hat seine Farbe in der Textur. Der Shader mischt deshalb die Textur zur Blitzfarbe (`uniform vec4 flash`, rgb Farbe, a Anteil), vor Schattierung und Laterne – wie beim Platzhalter.

## Farbraum

- Die Textur steht als **sRGB** in der Datei (die Farbwerte, wie man sie in einem Bildprogramm sieht).
- Das Spiel rechnet **überall direkt mit diesen Werten** (keine Umrechnung nach linear und zurück): Level-Farben, Platzhalter und jetzt die Textur. Helligkeit und Laterne sind Faktoren auf den sRGB-Werten.
- Blender rechnet Licht dagegen linear. Ein Probebild aus Blender kann deshalb in den Schatten etwas anders aussehen als im Spiel; die Farben der Textur selbst kommen unverändert an.
- **Belegt im Bild** (Foto `ghoul-4m` mit Schattierung kurz auf 1 gestellt): Alle 2 375 Pixel des Ghuls sind genau „Texel × Laterne“ (±1/255), z. B. Kopf-Pixel (424, 211): Texel (125, 125, 100) × 0,859 = (108, 108, 87). Mit Schattierung ist es (101, 101, 80), also Helligkeit 0,935. Wäre die Textur fälschlich von sRGB nach linear gewandelt, passte kein einziges Pixel.

## Wenn es nicht klappt

Fehlt die Datei, ist sie abgeschnitten oder kaputt, fehlt ein Teil oder passt Raylibs Ergebnis nicht zur Beschreibung: eine Warnung ins technische Log (`%LOCALAPPDATA%\SilverAndAsh\logs`), und die Figur wird als Platzhalter gezeichnet. Das Spiel läuft weiter.

## Ein neues Modell einbauen (Kiste, Pistole, Klinge, Fledermaus)

1. `.glb`, `.png` und Skript an ihren Platz, Zeile in `assets/HERKUNFT.md`.
2. Im Core eine Haltungs-Klasse wie `GhoulPose`: Teile mit Eltern (`RigPart`) und die Winkel je Zustand, mit Tests.
3. Im Game eine Ansicht hinter der Schnittstelle der Figur: `TexturedModel.TryLoad(pfad, Teile, log)` einmal nach dem Shader laden, je Bild `Rig.Pose(...)` und `TexturedModel.Draw(...)`. Fehlt das Modell, bleibt der Platzhalter.
4. Für die Waffen in der Hand ist noch offen: Ihre Kamera-Matrix spiegelt heute (siehe Übergabe des Designers); für echte Modelle braucht es eine echte Drehung.
