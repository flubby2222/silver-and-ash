# Weg ins Spiel – Stand der Dinge und was fehlt

Nur gelesen (Stand `main` bei `df4bbdb` und neuer), kein Spielcode geändert.

## Was vorhanden ist

- **Ereignisse in Core** ([GameEvent.cs](../../../src/SilverAndAsh.Core/Gameplay/GameEvent.cs)) mit allem, was die fünf Kategorien brauchen: `ShotFired` (Schuss), `MeleeSwung` (Schwung), `GhoulHit`/`BatHit`/`GormHit` (Treffer, `Critical` für Kopftreffer), `GhoulWindUp` (**Ankündigung des Ghul-Schlags**), `GhoulStruck`, `BatScreeched`, `GormWindUp`, `DoorOpened`, `ChestOpened`, `PickupTaken`, `BulletImpact` u. a. `Program.cs:ReadEvents()` ([Program.cs](../../../src/SilverAndAsh.Game/Program.cs)) liest sie nach jedem Tick und reicht sie an die Ansichten weiter; für Klang wäre das ein weiterer Empfänger.
- **Positionen:** Viele Ereignisse tragen eine Position (`Origin`, `Point`, `Position`); die Gegner-Ereignisse tragen nur eine Id (Ort über die Gegnerliste im Core).
- **Raylib-cs 8.1.0** ist eingebunden; seine Audio-Funktionen (`InitAudioDevice`, `LoadSound`, `PlaySound`, `SetSoundVolume`, `SetSoundPan`, `SetSoundPitch`, `LoadMusicStream`) sind Teil des Pakets, kein neues Paket nötig. Raylib lädt WAV, OGG, MP3, FLAC. `assets/**` wird ins Ausgabeverzeichnis kopiert.

## Was fehlt

- **Keine Klang-Ausgabe:** Im Game-Projekt gibt es keinen Aufruf von `InitAudioDevice`, `Sound` oder `Music`; `assets/audio/` existiert nicht.
- **Kein Ereignis für Schritte** (`IsGrounded` ist da, aber kein „Schritt getan“) – Schritte müsste das Spiel aus Bewegung und Zeit ableiten, am sauberen im Core als Ereignis (Regel: Spielregeln nicht ins Game).
- **Kein Ereignis für Tropfen** – bewusst: Einzelereignisse (Tropfen) sollen laut Audio Bible vom Spiel zufällig abgespielt werden; für die Testversion genügt das fest eingebaute Ambiente.
- **Raumklang** (Lautstärke/Pan aus Position und Blickrichtung) ist Issue [#225](https://github.com/flubby2222/silver-and-ash/issues/225), noch offen.

## Mindestens zu bauen, damit der Mensch die fünf Kategorien beim Spielen hört (Testversion, nicht main)

1. **Ein `AudioPlayer` im Game-Projekt:** `InitAudioDevice` nach `InitWindow`, `CloseAudioDevice` beim Beenden; lädt die Dateien aus `assets/audio/` (fehlt eine: Warnung ins Log, Spiel läuft weiter, wie bei den Texturen).
2. **Zuordnung Ereignis → Datei** in `ReadEvents()` (feste Tabelle, ohne Raumberechnung; Lautstärke fest je Kategorie):
   `ShotFired` → Schuss · `GhoulHit`/`BatHit`/`GormHit`/`DummyHit` → Klingentreffer · `GhoulWindUp` → Ghul-Laut (Schlag liegt 0,5 s danach; passt zu der Ankündigungszeit des Ghuls prüfen) · Ambiente als Schleife (`LoadMusicStream` + `SetMusicVolume`, `UpdateMusicStream` je Bild).
3. **Schritte:** Mit einer Schrittlänge aus gelaufener Strecke am Boden (`IsGrounded`) im Game als **vorläufiger** Hilfsweg; abwechselnd die vier Schritt-Dateien. Sauberer wäre ein Core-Ereignis.
4. **Stummschalter** (z. B. Taste), weil der Ton in der Testversion ohne Raumberechnung nur grob stimmt.

## Dateien, die ich liefern würde

Alle 16 Bit, 44 100 Hz, mono (Ambiente stereo), nach `assets/audio/`, je Datei eine Zeile in `assets/HERKUNFT.md` (Quelle, Link, Lizenz, Abrufdatum, SHA-256 des Originals, Skript):

| Kategorie | Datei(en) | Format |
|---|---|---|
| Schuss | `pistole.wav` (später 3 Varianten) | WAV |
| Klingentreffer | `klinge-1.wav` … `klinge-3.wav` | WAV |
| Schritte | `schritt-1.wav` … `schritt-4.wav` | WAV |
| Ghul-Laut | `ghul-ankuendigung.wav` | WAV |
| Ambiente | `ambiente-katakomben.ogg` (30 s, nahtlos, Stream) | OGG |

Zwei Anmerkungen: Der Ghul-Laut ist auf „Schlag bei 0,5 s“ gebaut – ob die Ankündigungszeit im Spiel gleich lang ist, sollte die Code-Sitzung gegen `Balance` prüfen. Die Kategorien Schuss, Klinge und Schritte haben jeweils nur eine bestätigte Fassung nach dem Urteil des Menschen; erst danach lohnt sich die Lieferung als `assets/`.
