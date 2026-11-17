"""PS1-Klang – schickt eine WAV-Datei durch eine Nachbildung der Klangkette der ersten PlayStation.

Der Sound-Chip der PS1 (SPU) hat keine Klaenge erzeugt, sondern aufgenommene
Samples abgespielt. Diese Kette liefert deshalb eine *Faerbung*, kein
Sounddesign. Schritte:

  1. Vorbereitung   Herunterrechnen auf eine niedrige Rate (z. B. 22 050 Hz) mit ffmpeg.
                    ANNAEHERUNG: Womit die Entwickler damals umgerechnet haben, ist
                    nicht bekannt; ffmpeg rechnet sauber (ohne Spiegelfrequenzen).
  2. ADPCM          Kodieren und wieder Dekodieren im SPU-ADPCM-Format: Bloecke zu
                    16 Byte mit 28 Samples zu je 4 Bit, 5 feste Vorhersage-Filter,
                    Verschiebung 0-12. Der DEKODER folgt der Beschreibung in psx-spx
                    und dem Quelltext des Emulators DuckStation (genau). Der KODIERER
                    ist eigene Arbeit (ANNAEHERUNG): Sonys Werkzeug ist nicht
                    dokumentiert; er probiert je Block Filter und Verschiebung durch
                    und nimmt die mit dem kleinsten Fehler.
  3. Abspielen      Hochrechnen auf 44 100 Hz wie der Chip: Tonhoehen-Zaehler mit
                    4000h-Schritten (1000h = 44 100 Hz) und 4-Punkt-Gauss-Interpolation
                    nach psx-spx (Formel genau). Die 512-Werte-Tabelle wird mit einer
                    Naeherungsformel aus dem NESdev-Forum (nocash, 2019) berechnet;
                    an 18 belegten Stuetzstellen weicht sie hoechstens um 1 von 22 963
                    ab (ANNAEHERUNG, siehe ps1_pruefung.md).
  4. Hall (wahlweise) Der Hall-Algorithmus der SPU nach psx-spx mit den dort
                    angegebenen Voreinstellungen (Room, Studio Small/Medium/Large,
                    Hall, Half Echo, Space Echo), bei 22 050 Hz, mit dem 39-Punkt-
                    Filter der SPU zum Herunter- und Hochrechnen. Formeln und Werte
                    genau; ANNAEHERUNG sind: linker und rechter Kanal werden im selben
                    Schritt statt in abwechselnden Takten gerechnet, und die
                    Zwischenrundung ist nicht im Detail belegt. Mono-Dateien werden
                    auf beide Eingaenge gegeben und danach wieder zu mono gemischt.
  5. Abschluss      Raender auf Stille blenden, Spitzenpegel wie die Eingangsdatei
                    (hoechstens -1 dBFS). Gehoert nicht zur PS1, sondern zum
                    technischen Rahmen der Audio Bible.

Nicht nachgebildet: die Lautstaerke-Huellkurve (ADSR) des Chips – unsere Dateien
tragen ihre Huellkurve schon in sich –, die Rauschquelle, die Ausgangsstufe
(dazu sagt psx-spx nichts) und Eigenheiten einzelner Konsolen-Modelle.

Quellen: https://psx-spx.consoledev.net/soundprocessingunitspu/ ,
DuckStation src/core/spu.cpp (https://github.com/stenzek/duckstation),
https://nesdev.nes.science/f12/t10586.xhtml (Gauss-Tabelle).

Aufruf:
  python ps1_klang.py ein.wav aus.wav [--rate 22050] [--hall Room] [--hall-pegel 0.25]
Als Modul: process(kanaele, rate=22050, hall=None, hall_pegel=0.25)
"""
import argparse
import math
import struct
import subprocess
import sys
import tempfile
import wave
from pathlib import Path

SR = 44100

# --- Ein- und Ausgabe -------------------------------------------------------

def read_wav(path):
    """Liest 16-Bit-PCM; gibt Kanaele als Listen von int (-32768..32767) und die Rate zurueck."""
    with wave.open(str(path), "rb") as w:
        ch, sw, sr, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        raw = w.readframes(n)
    if sw != 2:
        raise ValueError(f"{path}: nur 16 Bit unterstuetzt")
    data = struct.unpack("<%dh" % (n * ch), raw)
    return [list(data[c::ch]) for c in range(ch)], sr


def write_wav(path, chans, rate=SR):
    n = len(chans[0])
    frames = bytearray()
    pack = struct.Struct("<h").pack
    for i in range(n):
        for c in chans:
            frames += pack(clamp16(int(round(c[i]))))
    with wave.open(str(path), "wb") as w:
        w.setnchannels(len(chans))
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(bytes(frames))


def clamp16(v):
    return -32768 if v < -32768 else 32767 if v > 32767 else v


# --- 1. Vorbereitung: Herunterrechnen --------------------------------------

def resample(chans, rate_in, rate_out):
    """Rechnet mit ffmpeg (aresample) um. Gibt Kanaele als int-Listen zurueck."""
    if rate_in == rate_out:
        return [list(c) for c in chans]
    with tempfile.TemporaryDirectory() as td:
        src, dst = Path(td) / "a.wav", Path(td) / "b.wav"
        write_wav(src, chans, rate_in)
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(src), "-af",
                        f"aresample={rate_out}", "-c:a", "pcm_s16le", str(dst)], check=True)
        out, _ = read_wav(dst)
    return out


# --- 2. SPU-ADPCM -------------------------------------------------------------

# Vorhersage-Filter (positiv, negativ) in 1/64 – psx-spx / DuckStation; Filter 5-15 wirken wie 0.
FILTERS = [(0, 0), (60, 0), (115, -52), (98, -55), (122, -60)]
BLOCK = 28


def _nibble_base(nibble, shift):
    v = (nibble << 12) & 0xFFFF
    if v >= 0x8000:
        v -= 0x10000
    return v >> shift  # arithmetische Verschiebung wie im Chip


def decode_block(header, nibbles, h0, h1):
    """Dekodiert einen Block wie DuckStation. header = Byte 0 (Bits 0-3 Shift, 4-7 Filter)."""
    shift = header & 0x0F
    if shift > 12:
        shift = 9  # reservierte Werte 13-15 wirken wie 9
    filt = header >> 4
    pos, neg = FILTERS[filt] if filt < len(FILTERS) else (0, 0)
    out = []
    for nib in nibbles:
        s = _nibble_base(nib, shift) + ((h0 * pos) >> 6) + ((h1 * neg) >> 6)
        s = clamp16(s)
        out.append(s)
        h1, h0 = h0, s
    return out, h0, h1


def _trial(x, filt, shift, h0, h1):
    """Kodiert einen Block mit festem Filter und Shift; rechnet dabei genau wie der Dekoder."""
    pos, neg = FILTERS[filt]
    scale = (1 << shift) / 4096.0
    nibbles, err = [], 0
    for target in x:
        pred = ((h0 * pos) >> 6) + ((h1 * neg) >> 6)
        q = int(math.floor((target - pred) * scale + 0.5))
        q = -8 if q < -8 else 7 if q > 7 else q
        nib = q & 0x0F
        s = clamp16(_nibble_base(nib, shift) + pred)
        err += (target - s) * (target - s)
        nibbles.append(nib)
        h1, h0 = h0, s
    return err, nibbles, h0, h1


def encode(samples):
    """Kodiert int-Samples in Bloecke (header, nibbles). Eigener Kodierer (Annaeherung)."""
    x = list(samples) + [0] * ((-len(samples)) % BLOCK)
    blocks = []
    h0 = h1 = 0
    for b in range(0, len(x), BLOCK):
        blk = x[b:b + BLOCK]
        best = None
        for filt, (pos, neg) in enumerate(FILTERS):
            # Shift schaetzen: groesster Vorhersagerest mit den Originalwerten als Vorgeschichte
            p0, p1, r = h0, h1, 1
            for t in blk:
                r = max(r, abs(t - (((p0 * pos) >> 6) + ((p1 * neg) >> 6))))
                p1, p0 = p0, t
            est = int(math.floor(math.log2(28672.0 / r))) if r > 0 else 12
            for shift in {max(0, min(12, est + d)) for d in (-1, 0, 1)}:
                res = _trial(blk, filt, shift, h0, h1)
                if best is None or res[0] < best[0]:
                    best = res + ((filt << 4) | shift,)
        _, nibbles, h0, h1, header = best
        blocks.append((header, nibbles))
    return blocks


def decode(blocks, length=None):
    out = []
    h0 = h1 = 0
    for header, nibbles in blocks:
        s, h0, h1 = decode_block(header, nibbles, h0, h1)
        out.extend(s)
    return out[:length] if length is not None else out


def adpcm_roundtrip(samples):
    return decode(encode(samples), len(samples))


# --- 3. Abspielen mit Gauss-Interpolation ------------------------------------

def gauss_table():
    """512-Werte-Tabelle, Naeherungsformel nach nocash (NESdev-Forum, 2019-04-25) fuer die PS1.

    Die ersten 16 Werte sind laut psx-spx/DuckStation -1. Die Abweichung zu den
    belegten Stuetzstellen misst ps1_pruefung.py.
    """
    t = [0] * 512
    for i in range(512):
        k = 0.5 + i
        s = math.sin(math.pi * k * 2.048 / 1024.0)
        a = (math.cos(math.pi * k * 2.0 / 1023.0) - 1.0) * 0.50
        b = (math.cos(math.pi * k * 4.0 / 1023.0) - 1.0) * 0.08
        t[511 - i] = int(round(s * (a + b + 1.0) / k * 0x37C500))
    for i in range(16):
        t[i] = -1
    return t


GAUSS = gauss_table()


def play(samples, rate):
    """Spielt int-Samples mit der Rate `rate` ab wie eine SPU-Stimme und gibt 44,1-kHz-Samples zurueck."""
    pitch = max(1, min(0x4000, int(round(rate * 4096.0 / SR))))
    s = [0, 0, 0] + list(samples) + [0, 0, 0]  # drei Vorgaenger fuer den Anfang, Nachlauf am Ende
    g = GAUSS
    out = []
    counter = 3 << 12
    end = len(s) << 12
    while counter < end:
        idx = counter >> 12
        i = (counter >> 4) & 0xFF
        v = ((g[0x0FF - i] * s[idx - 3]) >> 15) + ((g[0x1FF - i] * s[idx - 2]) >> 15) \
            + ((g[0x100 + i] * s[idx - 1]) >> 15) + ((g[i] * s[idx]) >> 15)
        out.append(v)
        counter += pitch
    return out


# --- 4. Hall der SPU ------------------------------------------------------------

REVERB_ORDER = ("dAPF1 dAPF2 vIIR vCOMB1 vCOMB2 vCOMB3 vCOMB4 vWALL vAPF1 vAPF2 mLSAME mRSAME "
                "mLCOMB1 mRCOMB1 mLCOMB2 mRCOMB2 dLSAME dRSAME mLDIFF mRDIFF mLCOMB3 mRCOMB3 "
                "mLCOMB4 mRCOMB4 dLDIFF dRDIFF mLAPF1 mRAPF1 mLAPF2 mRAPF2 vLIN vRIN").split()

# Voreinstellungen aus psx-spx: (Groesse des Arbeitsspeichers in Byte, 32 Registerwerte)
REVERB_PRESETS = {
    "Room": (0x26C0, [0x007D, 0x005B, 0x6D80, 0x54B8, 0xBED0, 0x0000, 0x0000, 0xBA80, 0x5800, 0x5300, 0x04D6, 0x0333,
                      0x03F0, 0x0227, 0x0374, 0x01EF, 0x0334, 0x01B5, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                      0x0000, 0x0000, 0x01B4, 0x0136, 0x00B8, 0x005C, 0x8000, 0x8000]),
    "Studio Small": (0x1F40, [0x0033, 0x0025, 0x70F0, 0x4FA8, 0xBCE0, 0x4410, 0xC0F0, 0x9C00, 0x5280, 0x4EC0, 0x03E4, 0x031B,
                              0x03A4, 0x02AF, 0x0372, 0x0266, 0x031C, 0x025D, 0x025C, 0x018E, 0x022F, 0x0135, 0x01D2, 0x00B7,
                              0x018F, 0x00B5, 0x00B4, 0x0080, 0x004C, 0x0026, 0x8000, 0x8000]),
    "Studio Medium": (0x4840, [0x00B1, 0x007F, 0x70F0, 0x4FA8, 0xBCE0, 0x4510, 0xBEF0, 0xB4C0, 0x5280, 0x4EC0, 0x0904, 0x076B,
                               0x0824, 0x065F, 0x07A2, 0x0616, 0x076C, 0x05ED, 0x05EC, 0x042E, 0x050F, 0x0305, 0x0462, 0x02B7,
                               0x042F, 0x0265, 0x0264, 0x01B2, 0x0100, 0x0080, 0x8000, 0x8000]),
    "Studio Large": (0x6FE0, [0x00E3, 0x00A9, 0x6F60, 0x4FA8, 0xBCE0, 0x4510, 0xBEF0, 0xA680, 0x5680, 0x52C0, 0x0DFB, 0x0B58,
                              0x0D09, 0x0A3C, 0x0BD9, 0x0973, 0x0B59, 0x08DA, 0x08D9, 0x05E9, 0x07EC, 0x04B0, 0x06EF, 0x03D2,
                              0x05EA, 0x031D, 0x031C, 0x0238, 0x0154, 0x00AA, 0x8000, 0x8000]),
    "Hall": (0xADE0, [0x01A5, 0x0139, 0x6000, 0x5000, 0x4C00, 0xB800, 0xBC00, 0xC000, 0x6000, 0x5C00, 0x15BA, 0x11BB,
                      0x14C2, 0x10BD, 0x11BC, 0x0DC1, 0x11C0, 0x0DC3, 0x0DC0, 0x09C1, 0x0BC4, 0x07C1, 0x0A00, 0x06CD,
                      0x09C2, 0x05C1, 0x05C0, 0x041A, 0x0274, 0x013A, 0x8000, 0x8000]),
    "Half Echo": (0x3C00, [0x0017, 0x0013, 0x70F0, 0x4FA8, 0xBCE0, 0x4510, 0xBEF0, 0x8500, 0x5F80, 0x54C0, 0x0371, 0x02AF,
                           0x02E5, 0x01DF, 0x02B0, 0x01D7, 0x0358, 0x026A, 0x01D6, 0x011E, 0x012D, 0x00B1, 0x011F, 0x0059,
                           0x01A0, 0x00E3, 0x0058, 0x0040, 0x0028, 0x0014, 0x8000, 0x8000]),
    "Space Echo": (0xF6C0, [0x033D, 0x0231, 0x7E00, 0x5000, 0xB400, 0xB000, 0x4C00, 0xB000, 0x6000, 0x5400, 0x1ED6, 0x1A31,
                            0x1D14, 0x183B, 0x1BC2, 0x16B2, 0x1A32, 0x15EF, 0x15EE, 0x1055, 0x1334, 0x0F2D, 0x11F6, 0x0C5D,
                            0x1056, 0x0AE1, 0x0AE0, 0x07A2, 0x0464, 0x0232, 0x8000, 0x8000]),
}

# 39-Punkt-Filter der SPU zum Herunter- und Hochrechnen des Hall-Signals (psx-spx), Summe etwa 8000h
REVERB_FIR = [-0x0001, 0x0000, 0x0002, 0x0000, -0x000A, 0x0000, 0x0023, 0x0000, -0x0067, 0x0000, 0x010A, 0x0000,
              -0x0268, 0x0000, 0x0534, 0x0000, -0x0B90, 0x0000, 0x2806, 0x4000, 0x2806, 0x0000, -0x0B90, 0x0000,
              0x0534, 0x0000, -0x0268, 0x0000, 0x010A, 0x0000, -0x0067, 0x0000, 0x0023, 0x0000, -0x000A, 0x0000,
              0x0002, 0x0000, -0x0001]


def _s16(v):
    return v - 0x10000 if v >= 0x8000 else v


def _fir_down(x):
    """44,1 kHz -> 22,05 kHz mit dem SPU-Filter."""
    h = REVERB_FIR
    n = len(x)
    out = []
    for m in range(0, n, 2):
        acc = 0
        for k, c in enumerate(h):
            if c:
                j = m - k + 19
                if 0 <= j < n:
                    acc += c * x[j]
        out.append(acc >> 15)
    return out


def _fir_up(y, n_out):
    """22,05 kHz -> 44,1 kHz: Nullen einfuegen, mit dem SPU-Filter glaetten (Verstaerkung 2)."""
    z = [0] * n_out
    for m, v in enumerate(y):
        if 2 * m < n_out:
            z[2 * m] = v
    h = REVERB_FIR
    out = [0] * n_out
    for i in range(n_out):
        acc = 0
        for k, c in enumerate(h):
            if c:
                j = i - k + 19
                if 0 <= j < n_out:
                    acc += c * z[j]
        out[i] = acc >> 14
    return out


def reverb(left, right, preset, tail_s=1.0):
    """SPU-Hall nach psx-spx. Eingaenge und Ausgaenge 44,1 kHz (int). Gibt nur den Hallanteil zurueck."""
    size, regs = REVERB_PRESETS[preset]
    R = dict(zip(REVERB_ORDER, regs))
    v = {k: _s16(val) for k, val in R.items() if k.startswith("v")}
    off = {k: val * 4 for k, val in R.items() if not k.startswith("v")}  # 8-Byte-Einheiten -> Halbworte
    n_out = len(left) + int(tail_s * SR)
    lin = _fir_down(left + [0] * (n_out - len(left)))
    rin = _fir_down(right + [0] * (n_out - len(right)))
    N = size // 2
    mem = [0] * N
    cur = 0

    def a(o):
        return (cur + o) % N

    def sat(x):
        return -32768 if x < -32768 else 32767 if x > 32767 else x

    vIIR, vWALL = v["vIIR"], v["vWALL"]
    lo, ro = [], []
    for li, ri in zip(lin, rin):
        L = (v["vLIN"] * li) >> 15
        Rr = (v["vRIN"] * ri) >> 15
        # gleiche Seite
        prev = mem[a(off["mLSAME"]) - 1]
        mem[a(off["mLSAME"])] = sat((((L + ((mem[a(off["dLSAME"])] * vWALL) >> 15) - prev) * vIIR) >> 15) + prev)
        prev = mem[a(off["mRSAME"]) - 1]
        mem[a(off["mRSAME"])] = sat((((Rr + ((mem[a(off["dRSAME"])] * vWALL) >> 15) - prev) * vIIR) >> 15) + prev)
        # andere Seite
        prev = mem[a(off["mLDIFF"]) - 1]
        mem[a(off["mLDIFF"])] = sat((((L + ((mem[a(off["dRDIFF"])] * vWALL) >> 15) - prev) * vIIR) >> 15) + prev)
        prev = mem[a(off["mRDIFF"]) - 1]
        mem[a(off["mRDIFF"])] = sat((((Rr + ((mem[a(off["dLDIFF"])] * vWALL) >> 15) - prev) * vIIR) >> 15) + prev)
        outs = []
        for side in "LR":
            o = (v["vCOMB1"] * mem[a(off[f"m{side}COMB1"])] + v["vCOMB2"] * mem[a(off[f"m{side}COMB2"])]
                 + v["vCOMB3"] * mem[a(off[f"m{side}COMB3"])] + v["vCOMB4"] * mem[a(off[f"m{side}COMB4"])]) >> 15
            for stage in ("1", "2"):
                vA = v["vAPF" + stage]
                t = mem[(a(off[f"m{side}APF{stage}"]) - off["dAPF" + stage]) % N]
                o = o - ((vA * t) >> 15)
                mem[a(off[f"m{side}APF{stage}"])] = sat(o)
                o = ((o * vA) >> 15) + t
            outs.append(sat(o))
        lo.append(outs[0])
        ro.append(outs[1])
        cur = (cur + 1) % N
    return _fir_up(lo, n_out), _fir_up(ro, n_out)


# --- Gesamte Kette ------------------------------------------------------------

def _finish(chans, peak):
    n = len(chans[0])
    fi, fo = int(0.001 * SR), int(0.01 * SR)
    m = max(max(abs(v) for v in c) for c in chans) or 1
    g = min(peak, 32767 * 10 ** (-1.0 / 20)) / m
    out = []
    for c in chans:
        o = [v * g for v in c]
        for i in range(min(fi, n)):
            o[i] *= 0.5 - 0.5 * math.cos(math.pi * i / fi)
        for j in range(min(fo, n)):
            o[n - 1 - j] *= 0.5 - 0.5 * math.cos(math.pi * j / fo)
        o[0] = o[-1] = 0.0
        out.append(o)
    return out


def process(chans, rate=22050, hall=None, hall_pegel=0.25, length=None, use_adpcm=True):
    """Ganze Kette auf int-Kanaele (44,1 kHz). Gibt float-Kanaele (int-Skala) zurueck.

    length: Laenge der Ausgabe in Samples (Standard: wie die Eingabe; Hall wird abgeschnitten).
    use_adpcm=False laesst Schritt 2 weg (nur fuer Messungen).
    """
    n = length or len(chans[0])
    peak = max(max(abs(v) for v in c) for c in chans)
    low = resample(chans, SR, rate)
    played = []
    for c in low:
        d = adpcm_roundtrip(c) if use_adpcm else c
        p = play(d, rate)
        played.append((p + [0] * n)[:n])
    if hall:
        l_in = played[0]
        r_in = played[1] if len(played) > 1 else played[0]
        wl, wr = reverb(l_in, r_in, hall, tail_s=0.0)
        if len(played) == 1:
            played = [[d + hall_pegel * (a + b) / 2 for d, a, b in zip(played[0], wl, wr)]]
        else:
            played = [[d + hall_pegel * w for d, w in zip(played[0], wl)],
                      [d + hall_pegel * w for d, w in zip(played[1], wr)]]
    return _finish(played, peak)


def main(argv=None):
    ap = argparse.ArgumentParser(description="Schickt eine WAV-Datei durch die nachgebildete PS1-Klangkette.")
    ap.add_argument("ein")
    ap.add_argument("aus")
    ap.add_argument("--rate", type=int, default=22050, help="Rate der Samples, z. B. 22050 oder 11025")
    ap.add_argument("--hall", choices=sorted(REVERB_PRESETS), default=None)
    ap.add_argument("--hall-pegel", type=float, default=0.25)
    args = ap.parse_args(argv)
    chans, sr = read_wav(args.ein)
    if sr != SR:
        raise SystemExit("Eingabe muss 44 100 Hz haben")
    write_wav(args.aus, process(chans, args.rate, args.hall, args.hall_pegel))


if __name__ == "__main__":
    main()
