"""Hoerprobe 5 – Kandidaten aus Kenney-Aufnahmen (CC0), Mischungen und PS1-Einstellungen.

Aufruf:
  python erzeugen.py [--quellen <ordner>]
<ordner> ist der Quellen-Ordner ausserhalb des Repositorys (siehe quelle.py).
Schreibt die Hoerprobe-Dateien in diesen Ordner und die Quellenliste nach
design/grundlagen/klangquellen.md.

Alle Kandidaten gehen durch die PS1-Kette, Einstellung "a-leicht" (22 050 Hz,
Hoehenausgleich, kein Hall). Zusaetzlich je Kategorie eine Datei in den
Einstellungen "a-leicht" und "b-stark" (11 025 Hz, kein Ausgleich, Hall "Room").
"""
import argparse
import importlib.util
import math
import random
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
AUDIO = HERE.parent
sys.path.insert(0, str(AUDIO / "hoerprobe-1"))
sys.path.insert(0, str(AUDIO / "hoerprobe-3"))

import ableiten as A  # noqa: E402
import quelle as Q  # noqa: E402
from synth import (SR, add_into, curve, env_ad, finish, mul, n_of, normalize, osc_sine,  # noqa: E402
                   reflections, reverb, svf, white, write_wav, zeros)


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


HP3 = load_module("geraeusche_hp3", AUDIO / "hoerprobe-3" / "geraeusche.py")
HP3AMB = load_module("ambiente_hp3", AUDIO / "hoerprobe-3" / "ambiente.py")
HP4T = load_module("tropfen_hp4", AUDIO / "hoerprobe-4" / "tropfen.py")
HP4K = load_module("klinge_hp4", AUDIO / "hoerprobe-4" / "klinge.py")

PAKETE = {
    "impact-sounds": ("Impact Sounds", "https://kenney.nl/assets/impact-sounds"),
    "rpg-audio": ("RPG Audio", "https://kenney.nl/assets/rpg-audio"),
    "interface-sounds": ("Interface Sounds", "https://kenney.nl/assets/interface-sounds"),
}
USED = {}  # Dateiname -> (Paket, Pfad)
ROOT = None


def src(paket, name):
    p = Q.find(ROOT, paket, name)
    USED[name] = (paket, p)
    return Q.load(p)


def save(name, x):
    write_wav(HERE / f"{name}.wav", [x])


# --- synthetische Koerper (aus Hoerprobe 3) ----------------------------------

def gun_body(rng, n):
    """Koerper des Schusses aus Hoerprobe 3: Rauschen in zwei Baendern (550 Hz Band, 250 Hz Tiefpass), Sub 90->45 Hz."""
    noise = white(n, rng)
    band = svf(noise, 550.0, 1.2, "bp")
    low = svf(noise, 250.0, 0.7, "lp")
    body = normalize(mul([0.7 * a + b for a, b in zip(band, low)], env_ad(n, 0.001, 0.03)), 1.0)
    sub = mul(osc_sine(curve(n, [(0.0, 90.0), (0.08, 45.0)])), env_ad(n, 0.002, 0.04))
    return add_into(body, sub, 0, 0.6)


def gun_room(dry):
    er = reflections(dry, [(6.0, 0.3), (11.0, 0.22), (17.0, 0.15)], tail=0.0, damp_hz=6000.0)
    room = reverb(dry, rt60=0.35, size=0.8, damp_hz=5000.0, tail=0.0, seed=131)
    return add_into(add_into(list(dry), er, 0, 1.0), room, 0, 0.5)


# --- Pistole -----------------------------------------------------------------

def pistole_1():
    """Rein aus einer Aufnahme: impactPunch_heavy_001 (dumpfer Schlag), 2 Halbtoene tiefer, stark gesaettigt, kurzer Raum."""
    x = A.cut_gesture(src("impact-sounds", "impactPunch_heavy_001.ogg"), 0.3)
    x = A.varispeed(A.clean(x), -2)
    x = HP3.saturate(x, 4.0)
    return finish(gun_room(x + [0.0] * n_of(0.1)), -1.5, length=0.4, fade_out_ms=60)


def pistole_2():
    """Mischung: Anschlag aus impactMetal_heavy_002 (erste 25 ms) + synthetischer Koerper und Raum aus Hoerprobe 3."""
    rng = random.Random(51)
    n = n_of(0.3)
    hit = A.head(A.cut_gesture(src("impact-sounds", "impactMetal_heavy_002.ogg"), 0.2), 25)
    dry = A.layer(normalize(gun_body(rng, n), 0.9), normalize(hit, 1.0), 1.0)
    dry = HP3.saturate(dry, 2.0)
    return finish(gun_room(dry), -1.5, length=0.4, fade_out_ms=60)


def pistole_3():
    """Mischung: Anschlag aus impactPlate_heavy_001 (erste 20 ms, 3 Halbtoene tiefer) + synthetischer Koerper und Raum."""
    rng = random.Random(52)
    n = n_of(0.3)
    hit = A.head(A.varispeed(A.cut_gesture(src("impact-sounds", "impactPlate_heavy_001.ogg"), 0.3), -3), 20)
    dry = A.layer(normalize(gun_body(rng, n), 0.9), normalize(hit, 1.0), 0.9)
    dry = HP3.saturate(dry, 2.0)
    return finish(gun_room(dry), -1.5, length=0.4, fade_out_ms=60)


# --- Klinge ------------------------------------------------------------------

def klinge_1():
    """Rein aus einer Aufnahme: chop.ogg (Hieb), 2 Halbtoene tiefer."""
    x = A.varispeed(A.clean(A.cut_gesture(src("rpg-audio", "chop.ogg"), 0.4)), -2)
    return finish(x, -4.0, length=min(0.5, len(x) / SR), fade_out_ms=40)


def klinge_2():
    """Rein aus zwei Aufnahmen: Koerper impactPunch_medium_000 + Schneide knifeSlice.ogg (erste 60 ms), gleichzeitig."""
    body = A.cut_gesture(src("impact-sounds", "impactPunch_medium_000.ogg"), 0.35)
    edge = A.head(A.cut_gesture(src("rpg-audio", "knifeSlice.ogg"), 0.3), 60)
    x = A.layer(normalize(A.clean(body), 1.0), normalize(edge, 1.0), 0.6)
    return finish(x, -4.0, length=0.35, fade_out_ms=40)


def klinge_3():
    """Mischung: Schneide aus knifeSlice.ogg (erste 40 ms) + synthetischer Koerper aus Hoerprobe 4 (fallender Tiefpass, Ton)."""
    rng = random.Random(53)
    n = n_of(0.26)
    body = HP4K.body(rng, n, rng.uniform)
    edge = A.head(A.cut_gesture(src("rpg-audio", "knifeSlice.ogg"), 0.3), 40)
    x = A.layer(normalize(body, 1.0), normalize(edge, 1.0), 0.6)
    return finish(x, -4.0, length=0.26, fade_out_ms=40)


# --- Schritte ----------------------------------------------------------------

RPG_STEPS = ["footstep00.ogg", "footstep02.ogg", "footstep06.ogg", "footstep07.ogg"]
CONCRETE = ["footstep_concrete_000.ogg", "footstep_concrete_001.ogg", "footstep_concrete_002.ogg",
            "footstep_concrete_003.ogg"]


def schritt_1(k):
    """Rein: RPG Audio footstep, auf eine Geste geschnitten (hoechstens 0,28 s)."""
    x = A.clean(A.cut_gesture(src("rpg-audio", RPG_STEPS[k]), 0.28))
    return finish(x, -12.0 - 0.5 * k, length=min(0.28, len(x) / SR), fade_out_ms=30)


def schritt_2(k):
    """Rein: Impact Sounds footstep_concrete, 2 Halbtoene tiefer (schwerer)."""
    x = A.varispeed(A.clean(A.cut_gesture(src("impact-sounds", CONCRETE[k]), 0.2)), -2)
    x = x + [0.0] * n_of(0.02)
    return finish(x, -12.0 - 0.5 * k, length=min(0.25, len(x) / SR), fade_out_ms=20)


def schritt_3(k):
    """Mischung: Anschlag aus RPG footstep (erste 20 ms) + synthetische Bodenreaktions-Kurve aus Hoerprobe 3."""
    rng = random.Random(15100 + k + 1)
    j = rng.uniform
    n = n_of(0.24)
    curve_ = HP3.grf(n, 0.008 * j(0.8, 1.2), j(0.045, 0.065), j(0.25, 0.4))
    tex = svf(svf(white(n, rng), 1300.0 * j(0.85, 1.15), 0.8, "lp"), 200.0, 0.7, "hp")
    body = normalize(mul(tex, curve_), 1.0)
    hit = A.head(A.cut_gesture(src("rpg-audio", RPG_STEPS[k]), 0.2), 20)
    x = A.layer(body, normalize(hit, 1.0), 0.8)
    return finish(x, -12.0 - 0.5 * k, length=0.24, fade_out_ms=40)


def folge(takes):
    seq = zeros(n_of(0.5 * 8 + 0.3))
    for i in range(8):
        add_into(seq, takes[i % len(takes)], n_of(0.1 + 0.5 * i), 1.0)
    return seq


# --- Tropfen -----------------------------------------------------------------
# Jede drip-Funktion verbraucht genau zwei Zufallszahlen (wie in Hoerprobe 1-4),
# damit die sieben Zeitpunkte im Ambiente gleich bleiben.

def _drip_from(x_cache, rng, semis_base):
    st = semis_base + rng.uniform(-2.0, 2.0)
    g = rng.uniform(0.8, 1.0)
    y = A.varispeed(x_cache, st)
    return [v * g for v in HP4T.room(normalize(y, 1.0))]


_CACHE = {}


def _cached(key, fn):
    if key not in _CACHE:
        _CACHE[key] = fn()
    return _CACHE[key]


def drip_1(rng):
    """Rein: impactGlass_light_000 (Glas, leicht), eine Oktave tiefer, gedaempfter Raum."""
    x = _cached("g", lambda: A.clean(A.cut_gesture(src("impact-sounds", "impactGlass_light_000.ogg"), 0.15)))
    return [v * 0.35 for v in _drip_from(x, rng, -12)]


def drip_2(rng):
    """Rein: impactTin_medium_004 (Blech), 7 Halbtoene tiefer, Tiefpass 2,5 kHz, gedaempfter Raum."""
    x = _cached("t", lambda: svf(A.clean(A.cut_gesture(src("impact-sounds", "impactTin_medium_004.ogg"), 0.15)),
                                 2500.0, 0.707, "lp"))
    return [v * 0.35 for v in _drip_from(x, rng, -7)]


def drip_3(rng):
    """Mischung: Aufschlag aus impactGeneric_light_000 (erste 8 ms) + synthetischer kurzer Ton (Hoerprobe 4, Fassung a)."""
    hit = _cached("h", lambda: A.head(A.cut_gesture(src("impact-sounds", "impactGeneric_light_000.ogg"), 0.1), 8, 4))
    f_a = rng.uniform(3000.0, 4200.0) * 2 ** -1.5
    f_b = f_a * rng.uniform(1.15, 1.3)
    t = HP4T.tone(f_a, f_b, 0.005, n_of(0.05))
    x = A.layer(normalize(t, 1.0), normalize(hit, 1.0), 0.5)
    return [v * 0.35 for v in HP4T.room(normalize(x, 1.0))]


DRIPS = {"1-glas": drip_1, "2-blech": drip_2, "3-klopfen-plus-ton": drip_3}


def drip_singles(fn):
    rng = random.Random(77)
    out = zeros(n_of(3.6))
    for k in range(4):
        add_into(out, fn(rng), n_of(0.1 + 0.8 * k), 0.5)
    return finish(out, -12.0, fade_out_ms=20)


def ambience(fn, name):
    HP3AMB.hp1.drip = fn
    left, right = HP3AMB.hp1.variant("b")
    chans = HP3AMB.ps1_loop([left, right])
    tmp = HERE / f"_{name}.wav"
    write_wav(tmp, chans)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(tmp), "-c:a", "libvorbis", "-q:a", "5",
                    str(HERE / f"{name}.ogg")], check=True)
    tmp.unlink()


# --- Quellenliste --------------------------------------------------------------

def write_quellen():
    lines = ["# Klangquellen\n",
             "Herkunft aller heruntergeladenen Originale. Die Originale liegen **nicht** im Repository, sondern lokal "
             "unter `%LOCALAPPDATA%\\SilverAndAsh\\klangquellen\\kenney\\<paket>\\` (`archiv\\` = geladenes Archiv, "
             "`inhalt\\` = entpackt). Ins Repository kommen nur abgeleitete Dateien und die Skripte, die sie erzeugen.\n",
             "Freigabe: der Mensch, 2026-11-16 („Ja, Kenney laden.“, im Chat bestätigt). Quelle nur kenney.nl.\n",
             "## Pakete\n",
             "| Paket | Seite | Abruf | Archiv | Größe | SHA-256 des Archivs | Lizenzsatz aus `License.txt` im Archiv |",
             "|---|---|---|---|---|---|---|"]
    lic = {"impact-sounds": "„License: (Creative Commons Zero, CC0)“ – „This content is free to use in personal, educational and commercial projects.“",
           "rpg-audio": "„License (Creative Commons Zero, CC0)“ – „You may use these assets in personal and commercial projects.“",
           "interface-sounds": "„License: (Creative Commons Zero, CC0)“ – „This content is free to use in personal, educational and commercial projects.“"}
    for paket, (title, url) in PAKETE.items():
        z = Path(ROOT) / "kenney" / paket / "archiv" / f"kenney_{paket}.zip"
        lines.append(f"| {title} | {url} | 2026-11-16 | `kenney_{paket}.zip` | {z.stat().st_size} Byte | `{Q.sha256(z)}` | {lic[paket]} |")
    lines += ["\nDas Paket *Interface Sounds* ist geladen, aber in Hörprobe 5 noch nicht verwendet (gedacht für Menü und Bedienung).\n",
              "## Wofür Kenney nichts hergibt\n",
              "Durchgesehen: alle Dateien von Impact Sounds und RPG Audio (Namen und Messwerte).\n",
              "| Geräusch | Kenney | Lücke? |", "|---|---|---|",
              "| Pistolenschuss | kein Schuss, nur Schläge auf Metall, Platten, Faustschläge | **ja** – nur Ersatz-Anschläge |",
              "| Tropfen / Wasser | kein Wasser | **ja** – nur Ersatz (Glas, Blech, Klopfen) |",
              "| Ghul-Laut | keine Kreaturen-Stimmen (die Voiceover-Pakete sprechen Wörter; nicht geladen) | **ja** – bleibt Synthese |",
              "| Gorm mit Kette | keine Kette; nur `metalLatch`, `metalClick`, `metalPot` | **ja** |",
              "| Fledermaus | nichts | **ja** |",
              "| Türen | `doorOpen_1–2`, `doorClose_1–4`, `creak1–3` (RPG Audio); einige übersteuert (`doorClose_1`, `doorClose_3`, `doorOpen_1–2`) | nein, aber Auswahl knapp |",
              "| Kisten | `impactWood_*`, `impactPlank_medium_*` (Impact Sounds), `creak*` | nein |",
              "| Schritte auf Stein | `footstep_concrete_000–004` (Impact Sounds), `footstep00–09` (RPG Audio) | nein |",
              "| Klingentreffer | `knifeSlice`, `knifeSlice2`, `chop` (RPG Audio), `impactPunch_*` (Impact Sounds) | nein |",
              "| Klinge ziehen | `drawKnife1–3` (RPG Audio, sehr leise aufgenommen: Spitze −12 bis −26 dBFS) | nein |",
              "\nFür die Lücken käme laut Entscheidung der Leitung Freesound über das Konto des Menschen in Frage; dort habe ich nichts geladen.\n",
              "## Verwendete Einzeldateien\n",
              "| Datei | Paket | SHA-256 |", "|---|---|---|"]
    for name in sorted(USED):
        paket, p = USED[name]
        lines.append(f"| `{name}` | {PAKETE[paket][0]} | `{Q.sha256(p)}` |")
    (AUDIO.parent.parent / "grundlagen" / "klangquellen.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main():
    global ROOT
    ap = argparse.ArgumentParser()
    ap.add_argument("--quellen", default=str(Q.default_root()))
    ROOT = ap.parse_args().quellen

    # Kandidaten (Einstellung a-leicht)
    for name, fn in (("pistole-1-schlag-rein", pistole_1), ("pistole-2-metall-plus-koerper", pistole_2),
                     ("pistole-3-platte-plus-koerper", pistole_3), ("klinge-1-hieb-rein", klinge_1),
                     ("klinge-2-zwei-aufnahmen", klinge_2), ("klinge-3-schneide-plus-koerper", klinge_3)):
        save(name, A.to_ps1(fn()))
        print(name)
    for key, fn in (("1-rpg-rein", schritt_1), ("2-beton-rein", schritt_2), ("3-anschlag-plus-koerper", schritt_3)):
        takes = [A.to_ps1(fn(k)) for k in range(4)]
        for k, t in enumerate(takes, 1):
            save(f"schritt-{key}-{k}", t)
        save(f"schritt-{key}-folge", folge(takes))
        print("schritt", key)
    for key, fn in DRIPS.items():
        save(f"tropfen-{key}-einzeln", A.to_ps1(drip_singles(fn)))
        ambience(fn, f"ambiente-tropfen-{key}")
        print("tropfen", key)

    # Vergleich: beste rein synthetische Fassung bisher (PS1)
    for src_, dst in ((AUDIO / "hoerprobe-3" / "pistole-ps1.wav", "vergleich-pistole-hp3"),
                      (AUDIO / "hoerprobe-4" / "klinge-a-schneide-1-ps1.wav", "vergleich-klinge-hp4-a"),
                      (AUDIO / "hoerprobe-3" / "schritt-ps1-folge.wav", "vergleich-schritt-hp3-folge"),
                      (AUDIO / "hoerprobe-4" / "tropfen-b-breit-einzeln-ps1.wav", "vergleich-tropfen-hp4-b")):
        shutil.copyfile(src_, HERE / f"{dst}.wav")

    # PS1-Einstellung je Kategorie: dieselbe saubere Datei in a-leicht und b-stark
    beispiele = {"schuss": (pistole_2(), 0.58), "treffer": (klinge_3(), 0.58), "gegner": (HP3.ghul(), 0.9),
                 "tropfen": (drip_singles(drip_3), None)}
    steps = [schritt_1(k) for k in range(4)]
    for kat, (x, maxlen) in beispiele.items():
        for e in A.EINSTELLUNG:
            n = None if maxlen is None else n_of(min(maxlen, len(x) / SR + 0.2))
            save(f"einstellung-{kat}-{e}", A.to_ps1(x, e, length=n))
    for e in A.EINSTELLUNG:
        save(f"einstellung-schritte-{e}", folge([A.to_ps1(s, e, length=n_of(0.28)) for s in steps]))
    print("einstellungen")
    write_quellen()


if __name__ == "__main__":
    main()
