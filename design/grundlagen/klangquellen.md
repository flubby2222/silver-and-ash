# Klangquellen

Herkunft aller heruntergeladenen Originale. Die Originale liegen **nicht** im Repository, sondern lokal unter `%LOCALAPPDATA%\SilverAndAsh\klangquellen\kenney\<paket>\` (`archiv\` = geladenes Archiv, `inhalt\` = entpackt). Ins Repository kommen nur abgeleitete Dateien und die Skripte, die sie erzeugen.

Freigabe: der Mensch, 2026-11-16 („Ja, Kenney laden.“, im Chat bestätigt). Quelle nur kenney.nl.

## Pakete

| Paket | Seite | Abruf | Archiv | Größe | SHA-256 des Archivs | Lizenzsatz aus `License.txt` im Archiv |
|---|---|---|---|---|---|---|
| Impact Sounds | https://kenney.nl/assets/impact-sounds | 2026-11-16 | `kenney_impact-sounds.zip` | 800850 Byte | `029d734af1582474edf3a694d1b0cebc97c1c152f2f39fa34d4c2bafc5de77f8` | „License: (Creative Commons Zero, CC0)“ – „This content is free to use in personal, educational and commercial projects.“ |
| RPG Audio | https://kenney.nl/assets/rpg-audio | 2026-11-16 | `kenney_rpg-audio.zip` | 964837 Byte | `6dbeaf8544da958d8f2adcb4a4a4b76c1ade34a05f8ab9edccd327da7375f38b` | „License (Creative Commons Zero, CC0)“ – „You may use these assets in personal and commercial projects.“ |
| Interface Sounds | https://kenney.nl/assets/interface-sounds | 2026-11-16 | `kenney_interface-sounds.zip` | 834536 Byte | `f2193d072726d6758a5f7871b2dcc54dcce0d5c35c6f0a62f92549b327c81232` | „License: (Creative Commons Zero, CC0)“ – „This content is free to use in personal, educational and commercial projects.“ |

Das Paket *Interface Sounds* ist geladen, aber in Hörprobe 5 noch nicht verwendet (gedacht für Menü und Bedienung).

## Wofür Kenney nichts hergibt

Durchgesehen: alle Dateien von Impact Sounds und RPG Audio (Namen und Messwerte).

| Geräusch | Kenney | Lücke? |
|---|---|---|
| Pistolenschuss | kein Schuss, nur Schläge auf Metall, Platten, Faustschläge | **ja** – nur Ersatz-Anschläge |
| Tropfen / Wasser | kein Wasser | **ja** – nur Ersatz (Glas, Blech, Klopfen) |
| Ghul-Laut | keine Kreaturen-Stimmen (die Voiceover-Pakete sprechen Wörter; nicht geladen) | **ja** – bleibt Synthese |
| Gorm mit Kette | keine Kette; nur `metalLatch`, `metalClick`, `metalPot` | **ja** |
| Fledermaus | nichts | **ja** |
| Türen | `doorOpen_1–2`, `doorClose_1–4`, `creak1–3` (RPG Audio); einige übersteuert (`doorClose_1`, `doorClose_3`, `doorOpen_1–2`) | nein, aber Auswahl knapp |
| Kisten | `impactWood_*`, `impactPlank_medium_*` (Impact Sounds), `creak*` | nein |
| Schritte auf Stein | `footstep_concrete_000–004` (Impact Sounds), `footstep00–09` (RPG Audio) | nein |
| Klingentreffer | `knifeSlice`, `knifeSlice2`, `chop` (RPG Audio), `impactPunch_*` (Impact Sounds) | nein |
| Klinge ziehen | `drawKnife1–3` (RPG Audio, sehr leise aufgenommen: Spitze −12 bis −26 dBFS) | nein |

Für die Lücken käme laut Entscheidung der Leitung Freesound über das Konto des Menschen in Frage; dort habe ich nichts geladen.

## Verwendete Einzeldateien

| Datei | Paket | SHA-256 |
|---|---|---|
| `chop.ogg` | RPG Audio | `d00c2b3c9fff07e376145c8c8c45c90e5084ec192f6ce0387db233f7b86f1486` |
| `footstep00.ogg` | RPG Audio | `6fe61ef1fc3bcf0e253bf2eb64759db6cb69e2fe452f4d88cc597ecf78a3d601` |
| `footstep02.ogg` | RPG Audio | `2d9575b7dead6e1217ca113991fc5229e9b6d998ea0726ebbb277e7398ab888f` |
| `footstep06.ogg` | RPG Audio | `6cabfe7ae95d227a84066c6e17014ac31cc7c89b14ef51d7877e98352d4a64fb` |
| `footstep07.ogg` | RPG Audio | `42924cdbf998d5b3db180a339fd74afc94c570d594a536687e337ef1bdfdb346` |
| `footstep_concrete_000.ogg` | Impact Sounds | `d7267e183067757c92c169de2a379abea592cfca6b39bb2e8feea15221ad79fe` |
| `footstep_concrete_001.ogg` | Impact Sounds | `507a75eb19b897087855a3be3cf11a12d7fa37d143ad535eeebe0b8031c7325d` |
| `footstep_concrete_002.ogg` | Impact Sounds | `b21fa546d74941196b25da8a20b7010abd8c05c4fcf93545d1f344e66a664e13` |
| `footstep_concrete_003.ogg` | Impact Sounds | `8b513c2f0316324060b9174b8f28df6c2147418c506e6a53b77f18e149a5547b` |
| `impactGeneric_light_000.ogg` | Impact Sounds | `f0e982611e97512fee5f777986b67e8b435434b601f94992ec044f7e89fb5acb` |
| `impactGlass_light_000.ogg` | Impact Sounds | `d750cf88097d0c3e69920e6e14496e1444e1a9da01aa4cca8f5ec8cb44c57b4f` |
| `impactMetal_heavy_002.ogg` | Impact Sounds | `b914c8f1eb7c0f34bb165d7c77f4be0351f6be0660c13c53e65424e262e2c093` |
| `impactPlate_heavy_001.ogg` | Impact Sounds | `142fd6c77f13d25f318265901163f70f8dfe12f87829169a570025be43de7011` |
| `impactPunch_heavy_001.ogg` | Impact Sounds | `f92f5cb6ba4ff2766497292ffd90865654317eeca976f5652e0708dbdcdc0dd9` |
| `impactTin_medium_004.ogg` | Impact Sounds | `fd53e24efec3d5c1cee5a46fee70f97efd4bd01e53ac5d9b5f3b6719dfe9f5aa` |
| `knifeSlice.ogg` | RPG Audio | `4cd96dc630bed9840c15f1dd2306da2cc56a4da26a5d3f1a03c5a7265ac5e54f` |
