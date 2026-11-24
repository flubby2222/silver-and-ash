"""Vorher/Nachher-Blatt der Stationsfotos 1 und 3 (Issue #736), nur mit der Standardbibliothek.

Aufruf: python blatt.py <vorher-ordner> <nachher-ordner> <ausgabe.png>

In beiden Ordnern liegen die Fensterbilder `<szene>-screen.png` nebeneinander. Das Blatt hat je Paar eine Zeile, links
vorher, rechts nachher, auf die halbe Groesse verkleinert. Die Paare stehen in PAARE: Station 1 vorher (das eine
Foto) gegen das neue zweite Foto nah an der Waffenreihe, Station 3 vorher gegen nachher. PNG lesen, verkleinern und
schreiben kommt aus dem Blatt von Issue #722 (../stationsfotos-722/blatt.py).
"""
import importlib.util
import pathlib
import sys

_quelle = pathlib.Path(__file__).resolve().parent.parent / "stationsfotos-722" / "blatt.py"
_spec = importlib.util.spec_from_file_location("blatt722", _quelle)
blatt722 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(blatt722)

PAARE = [
    ("course-1-arms", "course-1-arms-row"),
    ("course-3-cracked-wall", "course-3-cracked-wall"),
]
LUECKE = 8
HINTERGRUND = (24, 24, 28)


def main(vorher, nachher, ausgabe):
    paare = [
        [blatt722.halbieren(blatt722.lesen(f"{ordner}/{szene}-screen.png")) for ordner, szene in ((vorher, alt), (nachher, neu))]
        for alt, neu in PAARE
    ]
    zelle_b, zelle_h = paare[0][0][0], paare[0][0][1]
    breite = 2 * zelle_b + 3 * LUECKE
    hoehe = len(paare) * (zelle_h + LUECKE) + LUECKE
    zeilen = [bytearray(bytes(HINTERGRUND) * breite) for _ in range(hoehe)]
    for zeile_nr, paar in enumerate(paare):
        oben = LUECKE + zeile_nr * (zelle_h + LUECKE)
        for spalte, (b, h, bildzeilen) in enumerate(paar):
            links = LUECKE + spalte * (zelle_b + LUECKE)
            for y in range(min(h, zelle_h)):
                zeilen[oben + y][links * 3:(links + min(b, zelle_b)) * 3] = bildzeilen[y][:min(b, zelle_b) * 3]
    blatt722.schreiben(ausgabe, breite, hoehe, zeilen)


if __name__ == "__main__":
    main(*sys.argv[1:4])
