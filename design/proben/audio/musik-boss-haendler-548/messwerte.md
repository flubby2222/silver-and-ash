# Messwerte – Musik-Skizzen Bosskampf und Händler (Issue #548)

Erzeugt von `messen.py`. Alle Zahlen sind gemessen, nicht gehört. Pegel in dBFS, Bandwerte in dB unter dem Gesamtpegel.

## Die vier Skizzen

| Datei | Länge | Spitze | Mittel | Lautheit | Mittel gegen Katakomben | Spitze gegen Katakomben | Pegel je Sekunde (min / max) | Frequenzschwerpunkt | 90 % der Energie unter | Abstand Mitte 300–3 000 Hz | außerhalb e-Moll |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `boss-a-schwerer-marsch` | 42.0 s | -9.7 dBFS | -24.0 dBFS | -23.3 LUFS | +0.0 dB | +0.9 dB | -31.0 / -23.4 dBFS | 95 Hz | 167 Hz | 21.9 dB | 0.3 % |
| `boss-b-basspuls` | 40.0 s | -10.3 dBFS | -24.0 dBFS | -22.9 LUFS | +0.0 dB | +0.3 dB | -28.7 / -22.0 dBFS | 110 Hz | 167 Hz | 19.3 dB | 0.2 % |
| `haendler-a-glocken-tropfen` | 40.0 s | -9.1 dBFS | -24.0 dBFS | -22.4 LUFS | +0.0 dB | +1.5 dB | -29.1 / -21.3 dBFS | 161 Hz | 258 Hz | 12.5 dB | 0.0 % |
| `haendler-b-chor-atem` | 40.0 s | -10.6 dBFS | -24.0 dBFS | -22.6 LUFS | +0.0 dB | +0.0 dB | -29.7 / -22.0 dBFS | 145 Hz | 334 Hz | 11.9 dB | 0.0 % |

## Maßstab: vorhandene Stücke im Spiel

| Datei | Länge | Spitze | Mittel | Lautheit | Mittel gegen Katakomben | Spitze gegen Katakomben | Pegel je Sekunde (min / max) | Frequenzschwerpunkt | 90 % der Energie unter | Abstand Mitte 300–3 000 Hz | außerhalb e-Moll |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `musik-katakomben.ogg` | 64.0 s | -10.6 dBFS | -24.0 dBFS | -22.5 LUFS | +0.0 dB | +0.0 dB | -30.7 / -23.6 dBFS | 128 Hz | 167 Hz | 19.0 dB | 0.0 % |
| `musik-hauptmenue.ogg` | 72.0 s | -7.8 dBFS | -23.5 dBFS | -22.3 LUFS | +0.5 dB | +2.8 dB | -33.6 / -18.3 dBFS | 142 Hz | 291 Hz | 12.5 dB | 0.0 % |

Maßstab ist `musik-katakomben.ogg` (Mittel nach Issue #427 bei −24 dBFS). Alle vier Skizzen sind auf dasselbe Mittel
gebracht; die Spitze liegt dort, wo die Dynamik der Stücke sie hinsetzt, nie über −1,5 dBFS (Rahmen: höchstens −1 dBFS).

## Naht der Schleife

Anfang und Ende laufen kreisförmig ineinander (Noten, Hall und PS1-Kette). Gemessen wird an der fertigen Datei:
der Sprung vom letzten zum ersten Sample darf nicht größer sein als der 99,9-%-Abstand zweier Nachbar-Samples, und
der Pegel des letzten Fensters (ein Takt bei den Boss-Stücken, sonst 4 s) darf höchstens 3 dB vom Pegel des ersten gleich langen Fensters abweichen. Die ersten 100 ms allein sind kein Maß: dort liegt bei den Boss-Stücken der Trommelschlag auf der Eins.

| Datei | Sprung letztes→erstes Sample (L; R) gegen 99,9-%-Abstand | Pegel letztes Fenster | Pegel erstes Fenster | Unterschied | letzte / erste 100 ms (zur Info) | Ergebnis |
|---|---|---|---|---|---|---|
| `boss-a-schwerer-marsch` | 0.00018 ≤ 0.00348; 0.00046 ≤ 0.00360 | -25.4 dBFS | -26.0 dBFS | 0.6 dB | -28.9 / -19.3 dBFS | bestanden |
| `boss-b-basspuls` | 0.00140 ≤ 0.00598; 0.00003 ≤ 0.00601 | -26.2 dBFS | -23.4 dBFS | 2.8 dB | -27.3 / -18.1 dBFS | bestanden |
| `haendler-a-glocken-tropfen` | 0.00079 ≤ 0.00748; 0.00266 ≤ 0.00580 | -24.9 dBFS | -24.5 dBFS | 0.4 dB | -25.1 / -24.1 dBFS | bestanden |
| `haendler-b-chor-atem` | 0.00150 ≤ 0.00522; 0.00150 ≤ 0.00528 | -27.4 dBFS | -25.3 dBFS | 2.1 dB | -28.5 / -23.2 dBFS | bestanden |

## Frequenzbänder

| Datei | < 300 Hz | 300–3 000 Hz | > 3 000 Hz | stärkste Tonklasse |
|---|---|---|---|---|
| `boss-a-schwerer-marsch` | -0.3 dB | -21.9 dB | -65.5 dB | E (51 %) |
| `boss-b-basspuls` | -0.3 dB | -19.3 dB | -63.8 dB | E (63 %) |
| `haendler-a-glocken-tropfen` | -1.0 dB | -12.5 dB | -63.9 dB | E (33 %) |
| `haendler-b-chor-atem` | -0.8 dB | -11.9 dB | -63.8 dB | E (40 %) |

Die Katakomben-Regel „Mitte frei“ (mindestens 15 dB Abstand im Band 300–3 000 Hz, Audio Bible Regel 6) ist für die Boss-Stücke
der Maßstab, weil dort Ankündigungen gegnerischer Schläge durchkommen müssen. Für die Händler-Stücke gilt sie nicht streng
(kein Kampf), die Zahl steht zum Vergleich da.

## Spektrogramme

- `spektrogramme/boss-a-schwerer-marsch.png`
- `spektrogramme/boss-b-basspuls.png`
- `spektrogramme/haendler-a-glocken-tropfen.png`
- `spektrogramme/haendler-b-chor-atem.png`
