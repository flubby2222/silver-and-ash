# Säulen-Klang Runde 3 (Issue #605)

Anlass: Runde 2 wurde abgelehnt („klingt sehr hölzern. wie bausteine die fallen. Das pattern könnte etwas chaotischer sein.“). Gebaut mit `bauen.py` (nur Python-Standardbibliothek, bitgleich), gemessen mit `messen.py` (`messwerte.md`). Mono, 44,1 kHz, 16 Bit, Spitze unter −1 dBFS; a und b eines Paares haben gleiche mittlere Lautstärke. Gemessen, nicht gehört.

**Gegenüber Runde 2:** Die Moden (Tonhöhe, hohles Ausklingen) sind weg; jedes Ereignis ist ein breitbandiger Geräuschstoß, dessen Größe Band (groß = dunkel), Dämpfung und tiefes Rauschen für die Masse bestimmt, dazu ungleiche Größen (wenige schwere, viele kleine) und dichtes Griesrauschen. Das Zeitmuster ist chaotisch: Poisson mit schwankender Rate, Folgestöße, Pausen und Überlappungen, Pegel, Länge und Klangfarbe streuen (fester Seed).

| Datei | Was |
|---|---|
| `saeule-treffer-stufe1-a.wav` | Treffer Stufe 1: ein schwerer Riss, kurzes dichtes Geröll |
| `saeule-treffer-stufe1-b.wav` | Treffer Stufe 1: leichter, loses unregelmäßiges Nachrutschen in Häufungen |
| `saeule-treffer-stufe2-a.wav` | Treffer Stufe 2: schwerer Riss, dichtes kurzes Geröll, Staub |
| `saeule-treffer-stufe2-b.wav` | Treffer Stufe 2: Hauptstoß, dann längeres ungleichmäßiges Nachstürzen mit Lücken und einem späteren schweren Stück |
| `saeule-bruch-a.wav` | Bruch: Mahlen und Reißen, Aufschlag bei 0,70 s, dichtes Geröll, das schnell abnimmt |
| `saeule-bruch-b.wav` | Bruch: dasselbe Knirschen, Aufschlag bei 0,70 s, danach längeres, unregelmäßiges Nachstürzen mit zwei späten schweren Stücken |

**Unterschied a zu b:** a ist ein klarer schwerer Stoß mit kurzem dichtem Geröll, b verteilt das Nachstürzen länger und unregelmäßiger mit Pausen; der Raum (frühe Reflexionen) ist in beiden gleich.

**Bruch:** 0 bis 0,70 s Mahlen und vier bis fünf Risse in unregelmäßigen Abständen, bei 0,70 s setzt der Aufschlag ein (zwei große Stücke), der laute Block liegt zwischen 0,70 und etwa 0,85 s.
