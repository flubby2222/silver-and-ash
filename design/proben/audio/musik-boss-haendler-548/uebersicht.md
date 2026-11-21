# Übersicht – Musik-Skizzen Bosskampf und Händler (Issue #548)

Hörprobe, nicht im Spiel. Vier Einzeldateien, je als Schleife gedacht (Länge 40 bis 42 s). Alles per Skript (`erzeugen.py`) aus der
Klangwelt von `musik-hauptmenue.ogg` und `musik-katakomben.ogg`: tiefe Glocke, Chor-Pad, derselbe Hall, dieselbe PS1-Kette, e-Moll. Kein Herzschlag.
Weder die Leitung noch der Audiodesigner können hören: Die Zahlen sagen nichts darüber, ob es gut klingt. Das entscheidet der Mensch.

Pegel zum Vergleich: `musik-katakomben.ogg` hat Mittel -24.0 dBFS, Spitze -10.6 dBFS, Lautheit -22.5 LUFS. 
Alle vier Skizzen sind auf dasselbe Mittel gebracht, damit nur der Klang verglichen wird. Wer sie im Spiel einstellt, nimmt als Spielfaktor 1,0 wie bei den bestehenden Stücken.

## Bosskampf: schwer, tief, treibend, Platz für Kampfgeräusche

| Datei | Länge | Spitze | Mittel | Lautheit | Mittel gegen Katakomben | Frequenzschwerpunkt | Abstand Mitte 300–3 000 Hz |
|---|---|---|---|---|---|---|---|
| `boss-a-schwerer-marsch.wav` | 42.0 s | -9.7 dBFS | -24.0 dBFS | -23.3 LUFS | +0.0 dB | 95 Hz | 21.9 dB |
| `boss-b-basspuls.wav` | 40.0 s | -10.3 dBFS | -24.0 dBFS | -22.9 LUFS | +0.0 dB | 110 Hz | 19.3 dB |

- **a (`boss-a-schwerer-marsch`):** Schlagwerk-Stück: eine tiefe Trommel auf 1 und 3 und ein Ambossschlag (Metall auf Stein) auf 4 tragen es bei 80 Schlägen pro Minute, darunter liegen langsame Chor-Akkorde und eine tiefe Glocke alle zwei Takte.
- **b (`boss-b-basspuls`):** Puls-Stück: ein gleichmäßiger Achtelpuls auf dem Grundton (tiefer Ton mit Obertönen bis etwa 330 Hz) treibt bei 96 Schlägen pro Minute, darüber stehen Chor-Akkorde und nur drei Glockenschläge; keine Trommel, kein Amboss.

## Händler: ruhig, eine Atempause, dieselbe düstere Welt

| Datei | Länge | Spitze | Mittel | Lautheit | Mittel gegen Katakomben | Frequenzschwerpunkt | Abstand Mitte 300–3 000 Hz |
|---|---|---|---|---|---|---|---|
| `haendler-a-glocken-tropfen.wav` | 40.0 s | -9.1 dBFS | -24.0 dBFS | -22.4 LUFS | +0.0 dB | 161 Hz | 12.5 dB |
| `haendler-b-chor-atem.wav` | 40.0 s | -10.6 dBFS | -24.0 dBFS | -22.6 LUFS | +0.0 dB | 145 Hz | 11.9 dB |

- **a (`haendler-a-glocken-tropfen`):** Melodisch: ein leiser Chor liegt unter acht einzelnen Glockentönen (E, G, A, H, D) mit großen Abständen, die wie langsame Tropfen fallen; der Akkord wechselt alle 10 s.
- **b (`haendler-b-chor-atem`):** Ohne Melodie: nur Chor-Stimmen, die versetzt einsetzen und wieder ausatmen (der Akkord wandert fließend), dazu zwei tiefe Glockenschläge bei 0 s und 20 s; die Vokalfärbung ist voller als bei den Boss-Stücken.

Naht, Bänder, Pegel je Sekunde und Spektrogramme stehen in `messwerte.md`. Zwei Läufe von `erzeugen.py` ergeben bitgleiche Dateien (`wav.sha256`).
