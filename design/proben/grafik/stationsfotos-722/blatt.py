"""Vorher/Nachher-Blatt der Stationsfotos (Issue #722), nur mit der Standardbibliothek.

Aufruf: python blatt.py <vorher-ordner> <nachher-ordner> <ausgabe.png>

In beiden Ordnern liegt je Szene ein Unterordner mit `<szene>-screen.png`. Das Blatt hat je Szene eine Zeile,
links vorher, rechts nachher, auf die halbe Groesse verkleinert. Die Reihenfolge steht in SZENEN. Unten folgt eine Zeile mit dem Kaempfer der beiden Tuer-Szenen (ohne und mit Gitter)\nvergroessert nebeneinander (Ausschnitt AUSSCHNITT, Faktor VERGROESSERUNG, Bilder ohne HUD).
"""
import struct
import sys
import zlib

SZENEN = [
    "course-1-arms",
    "course-2-columns",
    "course-3-cracked-wall",
    "course-4-portcullis",
    "course-5-teleporter",
    "course-6-boss",
    "course-boss-stairs",
    "door-normal-open-below",
    "door-barred-open-below",
]
LUECKE = 8
AUSSCHNITT = (370, 205, 450, 265)  # x0, y0, x1, y1 im 854x480-Bild
VERGROESSERUNG = 7
HINTERGRUND = (24, 24, 28)


def lesen(pfad):
    with open(pfad, "rb") as datei:
        daten = datei.read()
    pos = 8
    breite = hoehe = tiefe = farbtyp = 0
    idat = b""
    while pos < len(daten):
        laenge, art = struct.unpack(">I4s", daten[pos:pos + 8])
        inhalt = daten[pos + 8:pos + 8 + laenge]
        pos += 12 + laenge
        if art == b"IHDR":
            breite, hoehe, tiefe, farbtyp, _, _, interlace = struct.unpack(">IIBBBBB", inhalt)
            if tiefe != 8 or farbtyp not in (2, 6) or interlace:
                raise ValueError(f"{pfad}: nur 8 Bit RGB/RGBA ohne Interlace")
        elif art == b"IDAT":
            idat += inhalt
    kanaele = 3 if farbtyp == 2 else 4
    roh = zlib.decompress(idat)
    zeile = breite * kanaele
    zeilen = []
    davor = bytearray(zeile)
    for y in range(hoehe):
        filter_ = roh[y * (zeile + 1)]
        aktuell = bytearray(roh[y * (zeile + 1) + 1:(y + 1) * (zeile + 1)])
        for i in range(zeile):
            links = aktuell[i - kanaele] if i >= kanaele else 0
            oben = davor[i]
            linksoben = davor[i - kanaele] if i >= kanaele else 0
            if filter_ == 1:
                aktuell[i] = (aktuell[i] + links) & 255
            elif filter_ == 2:
                aktuell[i] = (aktuell[i] + oben) & 255
            elif filter_ == 3:
                aktuell[i] = (aktuell[i] + (links + oben) // 2) & 255
            elif filter_ == 4:
                p = links + oben - linksoben
                pa, pb, pc = abs(p - links), abs(p - oben), abs(p - linksoben)
                vorhersage = links if pa <= pb and pa <= pc else (oben if pb <= pc else linksoben)
                aktuell[i] = (aktuell[i] + vorhersage) & 255
        zeilen.append(aktuell)
        davor = aktuell
    return breite, hoehe, kanaele, zeilen


def halbieren(bild):
    breite, hoehe, kanaele, zeilen = bild
    ergebnis = []
    for y in range(0, hoehe - 1, 2):
        neu = bytearray()
        for x in range(0, breite - 1, 2):
            for k in range(3):
                summe = sum(zeilen[y + dy][(x + dx) * kanaele + k] for dy in (0, 1) for dx in (0, 1))
                neu.append(summe // 4)
        ergebnis.append(neu)
    return breite // 2, hoehe // 2, ergebnis


def ausschnitt(pfad):
    breite, hoehe, kanaele, zeilen = lesen(pfad)
    x0, y0, x1, y1 = AUSSCHNITT
    ergebnis = []
    for y in range(y0, y1):
        neu = bytearray()
        for x in range(x0, x1):
            pixel = bytes(zeilen[y][x * kanaele:x * kanaele + 3])
            neu += pixel * VERGROESSERUNG
        for _ in range(VERGROESSERUNG):
            ergebnis.append(bytearray(neu))
    return (x1 - x0) * VERGROESSERUNG, (y1 - y0) * VERGROESSERUNG, ergebnis


def unterschied(pfad_a, pfad_b):
    a, b = lesen(pfad_a), lesen(pfad_b)
    kanaele = a[2]
    anzahl = 0
    for y in range(a[1]):
        for x in range(a[0]):
            if a[3][y][x * kanaele:x * kanaele + 3] != b[3][y][x * kanaele:x * kanaele + 3]:
                anzahl += 1
    return anzahl

def schreiben(pfad, breite, hoehe, zeilen):
    roh = b"".join(b"\x00" + bytes(zeile) for zeile in zeilen)

    def block(art, inhalt):
        rumpf = art + inhalt
        return struct.pack(">I", len(inhalt)) + rumpf + struct.pack(">I", zlib.crc32(rumpf) & 0xFFFFFFFF)

    with open(pfad, "wb") as datei:
        datei.write(b"\x89PNG\r\n\x1a\n")
        datei.write(block(b"IHDR", struct.pack(">IIBBBBB", breite, hoehe, 8, 2, 0, 0, 0)))
        datei.write(block(b"IDAT", zlib.compress(roh, 9)))
        datei.write(block(b"IEND", b""))


def main(vorher, nachher, ausgabe):
    paare = []
    for szene in SZENEN:
        paare.append([halbieren(lesen(f"{ordner}/{szene}/{szene}-screen.png")) for ordner in (vorher, nachher)])
    nah = [ausschnitt(f"{nachher}/{s}/{s}.png") for s in ("door-normal-open-below", "door-barred-open-below")]
    print("Pixel verschieden (ohne/mit Gitter):", unterschied(*[f"{nachher}/{s}/{s}.png" for s in ("door-normal-open-below", "door-barred-open-below")]))
    zelle_b, zelle_h = paare[0][0][0], paare[0][0][1]
    breite = 2 * zelle_b + 3 * LUECKE
    hoehe = len(paare) * (zelle_h + LUECKE) + LUECKE + nah[0][1] + LUECKE
    leer = bytes(HINTERGRUND) * breite
    zeilen = [bytearray(leer) for _ in range(hoehe)]
    for zeile_nr, paar in enumerate(paare):
        oben = LUECKE + zeile_nr * (zelle_h + LUECKE)
        for spalte, (b, h, bildzeilen) in enumerate(paar):
            links = LUECKE + spalte * (zelle_b + LUECKE)
            for y in range(min(h, zelle_h)):
                zeilen[oben + y][links * 3:(links + min(b, zelle_b)) * 3] = bildzeilen[y][:min(b, zelle_b) * 3]
    oben = LUECKE + len(paare) * (zelle_h + LUECKE)
    for spalte, (b, h, bildzeilen) in enumerate(nah):
        links = LUECKE + spalte * (b + LUECKE)
        for y in range(h):
            zeilen[oben + y][links * 3:(links + b) * 3] = bildzeilen[y]
    schreiben(ausgabe, breite, hoehe, zeilen)


if __name__ == "__main__":
    main(*sys.argv[1:4])
