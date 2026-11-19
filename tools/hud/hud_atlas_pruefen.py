"""Prueft den doppelt feinen Atlas (Issue #402). Reines Python, ohne Blender.

Aufruf:
  python tools/hud/hud_atlas_pruefen.py <Lauf 1> <Lauf 2> [<Markdown-Ausgabe>]

<Lauf 1> und <Lauf 2> sind zwei Ausgabeordner von tools/hud/hud_atlas_doppelt.py (mit hud/ und menu/). Das Skript prueft:
  1. Bitgleich: je Datei SHA-256 aus beiden Laeufen gleich.
  2. Vollstaendig: jedes Teil der heutigen Massetabellen (assets/hud, assets/menu) hat ein Gegenstueck gleichen Namens.
  3. Doppelt: layoutX/Y, w, h und body des Gegenstuecks sind genau doppelt; Ausnahme nur Zeichen (icon_*): gleiche Mitte (+-1 px),
     mindestens doppelte Groesse, hoechstens 15 % + 8 px groesser.
  4. Die heutigen Atlanten in assets/ sind unveraendert gegenueber dem Stand im Git-Index (git show HEAD:...).
Mit Markdown-Ausgabe schreibt es die Tabelle alt -> neu. Beendet mit Fehlercode 1, wenn etwas nicht stimmt.
"""
import sys, os, json, hashlib, subprocess

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FILES = (("hud", "hud_parts", "hud_parts_x2"), ("menu", "menu_parts", "menu_parts_x2"))
fails = []


def sha(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def fail(msg):
    fails.append(msg)
    print("FEHLER:", msg)


def main():
    a, b = sys.argv[1], sys.argv[2]
    md = sys.argv[3] if len(sys.argv) > 3 else None
    rows = []
    n_parts = 0
    for sub, old_name, new_name in FILES:
        for ext in ("png", "json"):
            fa, fb = os.path.join(a, sub, f"{new_name}.{ext}"), os.path.join(b, sub, f"{new_name}.{ext}")
            ha, hb = sha(fa), sha(fb)
            print(f"{new_name}.{ext}: {ha}" + ("  (Lauf 2 gleich)" if ha == hb else "  (Lauf 2 ANDERS)"))
            if ha != hb:
                fail(f"{new_name}.{ext} nicht bitgleich")
        old = json.load(open(os.path.join(REPO, "assets", sub, old_name + ".json"), encoding="utf-8"))
        new = json.load(open(os.path.join(a, sub, new_name + ".json"), encoding="utf-8"))
        nm = {p["name"]: p for p in new["parts"]}
        if len(nm) != len(new["parts"]):
            fail(f"{new_name}: doppelte Namen")
        for p in old["parts"]:
            n_parts += 1
            q = nm.get(p["name"])
            if q is None:
                fail(f"{sub}: {p['name']} ohne Gegenstueck")
                continue
            art = "genau doppelt"
            if p["name"].startswith("icon_"):
                art = "gleiche Mitte, groesser"
                cx0, cy0 = 2 * p["layoutX"] + p["w"], 2 * p["layoutY"] + p["h"]
                cx1, cy1 = q["layoutX"] + q["w"] / 2.0, q["layoutY"] + q["h"] / 2.0
                if abs(cx0 - cx1) > 1 or abs(cy0 - cy1) > 1:
                    fail(f"{p['name']}: Mitte verschoben ({cx0},{cy0}) -> ({cx1},{cy1})")
                if q["w"] < 2 * p["w"] or q["h"] < 2 * p["h"] or q["w"] > 2.3 * p["w"] + 8 or q["h"] > 2.3 * p["h"] + 8:
                    fail(f"{p['name']}: Groesse {q['w']}x{q['h']} passt nicht zu 2 x {p['w']}x{p['h']}")
            else:
                if (q["layoutX"], q["layoutY"], q["w"], q["h"]) != (2 * p["layoutX"], 2 * p["layoutY"], 2 * p["w"], 2 * p["h"]):
                    fail(f"{p['name']}: nicht genau doppelt")
                if "body" in p and q.get("body") != [2 * v for v in p["body"]]:
                    fail(f"{p['name']}: body nicht doppelt")
            rows.append((sub, p["name"], f"{p['w']} x {p['h']} @ {p['layoutX']},{p['layoutY']}",
                         f"{q['w']} x {q['h']} @ {q['layoutX']},{q['layoutY']}", art))
        # heutige Atlanten unveraendert
        for ext in ("png", "json"):
            rel = f"assets/{sub}/{old_name}.{ext}"
            head = subprocess.run(["git", "show", "HEAD:" + rel], cwd=REPO, capture_output=True).stdout
            cur = open(os.path.join(REPO, rel), "rb").read()
            if ext == "json":  # der Checkout kann CRLF setzen; Zeilenenden sind kein Inhalt
                head, cur = head.replace(b"\r\n", b"\n"), cur.replace(b"\r\n", b"\n")
            if hashlib.sha256(head).hexdigest() != hashlib.sha256(cur).hexdigest():
                fail(f"{rel} gegenueber HEAD veraendert")
            else:
                print(f"{rel}: unveraendert gegenueber HEAD")
    print(f"{n_parts} heutige Teile geprueft, {len(fails)} Fehler")
    if md:
        with open(md, "w", newline="\n", encoding="utf-8") as f:
            f.write("| Atlas | Teil | heute (B x H @ Lage) | neu (B x H @ Lage) | Beziehung |\n|---|---|---|---|---|\n")
            for r in rows:
                f.write("| " + " | ".join(r) + " |\n")
    sys.exit(1 if fails else 0)


main()
