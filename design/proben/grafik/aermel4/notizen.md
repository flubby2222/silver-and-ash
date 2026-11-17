# Entwurf e (Issue #265, zweite Fassung nach dem Video und dem Urteil zu d)

Blatt `blatt_heute_d_e.png`: Spalten heute | d | e; Zeilen oben `room-corridor`, Mitte `pistol-recoil`, unten `blade-charged`. Modelle `modelle_e/` (Namen wie im Spiel, Knoten und Drehpunkte unverändert): `pistol.glb`, `pistol_hand.glb`, `blade.glb`, `blade_hand.glb` und die PNGs. Skripte `pistol.py`, `blade.py`, `arm.py` (`AERMEL=e` ist Voreinstellung), `fotos.sh`, `blatt.py`.

## Was gebaut ist (nach den Korrekturen der Leitung)
1. **Farbe und Material:** Handschuh in dunklem warmem Leder (40, 29, 25, deutlich dunkler als der Schaft), Stulpe in etwas hellerem Leder (134, 102, 72) mit Naht und Riemen (Silbernieten), Ärmel in sehr dunklem Blaugrau (40, 44, 54) mit drei langen Faltenbahnen. Kein Stahlgrau mehr.
2. **Paletten getrennt:** `pistol_hand.glb` und `blade_hand.glb` tragen jetzt **ihre eigene 64 × 64-Textur mit eigener 32-Farben-Palette** (eingebettet in die Datei, das Spiel lädt sie so wie bisher); die Waffen-Atlanten haben die Arm-Felder nicht mehr. Dazu liegen `pistol_hand.png` und `blade_hand.png` (die Skripte schreiben sie wie die anderen PNGs); für das Spiel zählt nur die eingebettete Textur.
3. **Klingenhand:** geschlossene Faust aus einem Körper (fünf Ringe, acht Seiten) um den Griff; die Fingerrücken sind ein Band mit drei eingezeichneten Fugen, die Knöchelreihe ein heller Streifen im Handrücken, der Daumen ein Stück; keine abstehenden Finger. Die Parierstange liegt sichtbar vor der Faust.
4. **Pistolenhand:** gleiche Faust, Zeigefinger nur als kurzer Stummel am Abzug. **Dreiecke:** Pistole-Arm 262 (vorher 376), Klinge-Arm 238 (vorher 396).
5. **Stulpe** als eigenes Band mit Riemen, flach ausgestellt.
6. **Haltungswerte (lokal, nicht committet), alle in `src/SilverAndAsh.Core/Presentation/WeaponPose.cs`:**
   - `PistolGrip` (0,09; −0,04; 0,18) statt (0,095; −0,08; 0,18)
   - `BladeGrip` (−0,11; −0,07; 0,19) statt (−0,11; −0,095; 0,19)
   - `WindUpShift` (−0,03; 0,01; 0,0) statt (−0,03; −0,01; −0,03)
   - `WindUpYawDegrees` 22 statt 35

## Die helle Fläche unten links bei der aufgeladenen Klinge (Punkt 5)
Es ist die **Parierstange** (5,6 cm × 1,8 = rund 10 cm breit, helles Silber). Beim Aufladen zieht die Hand zurück und die Klinge dreht um 35 Grad nach links; dabei liegt die Stange quer und nah vor dem Auge. Mit den Werten oben (weniger Drehung, Hand 3 cm weniger zurück, 2 cm höher) steht sie als Parierstange über der Faust und liest sich als Schwert, nicht als helle Fläche. Vorher war es zusätzlich so, dass die alte Hand den Griff nicht verdeckte.

## Was noch nicht stimmt
- **Stulpe zu wenig abgesetzt:** Im Foto sind Faust und Stulpe beide dunkelbraun; der Helligkeitsunterschied ist klein, das Band liest sich nur mit Mühe. Der Ton der Stulpe müsste noch heller (etwa 160, 125, 90) oder das Leder der Faust noch dunkler.
- **Pistolenfaust wirkt wie ein dicker Klotz** mit einer Kerbe; sie ist wegen der Stulpe und des Unterarms ein großer dunkler Körper, der Griff ragt kaum heraus. Die Fugen sind aus dieser Sicht kaum zu sehen.
- **Der Ärmel (blaugrau) ist nicht zu sehen**; er läuft unter dem Bildrand weg. Gut sichtbar sind nur Faust und Stulpe.
- **Linke Faust an der Klinge:** die Stulpe steht als Flügel nach links ab (ausgestellte Kante), das liest sich noch zu groß.
- Die Haltungswerte ändern den Schwung und den Rückstoß mit; getestet habe ich nur die drei Fotos. Der Test zu `Reach` (30 cm) ist nicht gelaufen.
