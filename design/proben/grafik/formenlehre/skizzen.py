"""Skizzen zur Formenlehre Gotik (Issue #281): einfache Risszeichnungen als SVG, mit Maßen im Zellraster. Reines Python, keine Abhaengigkeiten.
Aufruf: python skizzen.py     -> schreibt skizzen/*.svg
Masseinheit der Zeichnungen: Meter; 1 Zelle = 2 m; Raster der Wand: 64 Pixel je Meter (1 px = 1,5625 cm); alle Masse sind Vielfache von 1/16 m (4 px),
soweit nicht anders vermerkt."""
import html
import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "skizzen")
S = 110                                   # Pixel je Meter in den Zeichnungen
INK, GRID, STONE, ACC, DIM = "#2a2630", "#d8d4dc", "#b9a9b2", "#b0642c", "#555"


class Svg:
    def __init__(self, w_m, h_m, title, pad=1.0, extra_w=0.0):
        self.w, self.h = (w_m + extra_w) * S + 2 * pad * S, h_m * S + 2.6 * S
        self.pad = pad * S
        self.base = h_m * S + 1.7 * S          # y der Bodenlinie
        self.items = []
        self.title = title

    def X(self, x):
        return self.pad + x * S

    def Y(self, y):
        return self.base - y * S

    def add(self, s):
        self.items.append(s)

    def line(self, x0, y0, x1, y1, color=INK, w=1.5, dash=None):
        d = f' stroke-dasharray="{dash}"' if dash else ""
        self.add(f'<line x1="{self.X(x0):.1f}" y1="{self.Y(y0):.1f}" x2="{self.X(x1):.1f}" y2="{self.Y(y1):.1f}" stroke="{color}" stroke-width="{w}"{d}/>')

    def rect(self, x0, y0, x1, y1, fill="none", stroke=INK, w=1.5):
        self.add(f'<rect x="{self.X(x0):.1f}" y="{self.Y(y1):.1f}" width="{(x1 - x0) * S:.1f}" height="{(y1 - y0) * S:.1f}" fill="{fill}" stroke="{stroke}" stroke-width="{w}"/>')

    def poly(self, pts, fill="none", stroke=INK, w=1.5, close=True):
        p = " ".join(f"{self.X(x):.1f},{self.Y(y):.1f}" for x, y in pts)
        self.add(f'<{"polygon" if close else "polyline"} points="{p}" fill="{fill}" stroke="{stroke}" stroke-width="{w}"/>')

    def circle(self, x, y, r, fill="none", stroke=INK, w=1.5):
        self.add(f'<circle cx="{self.X(x):.1f}" cy="{self.Y(y):.1f}" r="{r * S:.1f}" fill="{fill}" stroke="{stroke}" stroke-width="{w}"/>')

    def text(self, x, y, s, size=13, color=INK, anchor="middle", weight="normal"):
        self.add(f'<text x="{self.X(x):.1f}" y="{self.Y(y):.1f}" font-family="Segoe UI, Arial, sans-serif" font-size="{size}" fill="{color}" text-anchor="{anchor}" font-weight="{weight}">{html.escape(s)}</text>')

    def dim_h(self, x0, x1, y, label, off=0.0):
        yy = y - off
        self.line(x0, yy, x1, yy, DIM, 1)
        for x in (x0, x1):
            self.line(x, yy - 0.04, x, yy + 0.04, DIM, 1)
        self.text((x0 + x1) / 2, yy - 0.06, label, 11, DIM)

    def dim_v(self, x, y0, y1, label, off=0.0):
        xx = x - off
        self.line(xx, y0, xx, y1, DIM, 1)
        for y in (y0, y1):
            self.line(xx - 0.04, y, xx + 0.04, y, DIM, 1)
        self.add(f'<text x="{self.X(xx) - 5:.1f}" y="{self.Y((y0 + y1) / 2):.1f}" font-family="Segoe UI, Arial, sans-serif" font-size="11" fill="{DIM}" text-anchor="end">{html.escape(label)}</text>')

    def arc_path(self, hw, spring, apex, x0=0.0, stroke=INK, w=2.2, fill="none"):
        """Spitzbogen (zwei Kreise): Linie von (x0-hw, spring) ueber den Scheitel zu (x0+hw, spring)."""
        R = ((apex - spring) ** 2 + hw ** 2) / (2 * hw)
        xl, xr, xa = self.X(x0 - hw), self.X(x0 + hw), self.X(x0)
        ya, ys = self.Y(apex), self.Y(spring)
        self.add(f'<path d="M {xl:.1f} {ys:.1f} A {R * S:.1f} {R * S:.1f} 0 0 1 {xa:.1f} {ya:.1f} A {R * S:.1f} {R * S:.1f} 0 0 1 {xr:.1f} {ys:.1f}" fill="{fill}" stroke="{stroke}" stroke-width="{w}"/>')

    def save(self, name):
        os.makedirs(OUT, exist_ok=True)
        head = (f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.w:.0f}" height="{self.h:.0f}" viewBox="0 0 {self.w:.0f} {self.h:.0f}">'
                f'<rect width="100%" height="100%" fill="#faf8fb"/>')
        title = f'<text x="14" y="24" font-family="Segoe UI, Arial, sans-serif" font-size="16" font-weight="bold" fill="{INK}">{self.title}</text>'
        with open(os.path.join(OUT, name), "w", encoding="utf-8") as f:
            f.write(head + title + "".join(self.items) + "</svg>")


def rise(hw, R):
    return math.sqrt(R * 2 * hw - hw * hw)


def ground(s, w):
    s.line(-0.5, 0, w + 0.5, 0, INK, 2.5)


def grid(s, x0, x1, ymax, step=1.0):
    for k in range(int(ymax / step) + 1):
        s.line(x0, k * step, x1, k * step, GRID, 0.7)
        s.text(x0 - 0.1, k * step - 0.03, f"{k * step:g} m", 10, DIM, "end")


# ---------------------------------------------------------------------------------------------------------------------
def sk_bogen():
    s = Svg(2.0, 6.0, "1 Bogenarten über der Zellbreite 2 m (Kämpfer bei 2,0 m Höhe, Maße von der Bodenlinie)", extra_w=2.5)
    # Spannweite 2 m: Kreisradien 1,2 / 1,6 / 2,0 / 3,0
    spring = 2.0
    cases = [(1.2, "gedrückt stark R 1,2", "#8a6a8a"), (1.6, "gedrückt R 1,6 (Gang)", "#b0642c"), (2.0, "gleichseitig R 2,0 (Raum)", "#3d6b8f"), (3.0, "Lanzett R 3,0 (Boss)", "#4a7d4a")]
    s.rect(0, 0, 2.0, 3.5, "none", "#bbb", 1)
    s.rect(0, 0, 2.0, 6.0, "none", "#ddd", 1)
    s.text(1.0, 3.55, "Gang 3,5 m", 11, "#888")
    s.text(1.0, 6.08, "Raum 6 m", 11, "#888")
    grid(s, 0, 2.0, 6)
    for R, label, color in cases:
        h = rise(1.0, R)
        s.arc_path(1.0, spring, spring + h, 1.0, color, 2.2)
        s.text(2.2, spring + h - 0.02, f"{label}: Stich {h:.2f} m", 11, color, "start")
    # Konstruktion gedrueckt 1,6
    R = 1.6
    s.circle(1.0 - 1.0 + R, spring, 0.03, INK, INK, 1)
    s.circle(1.0 + 1.0 - R, spring, 0.03, INK, INK, 1)
    s.line(0, spring, 2.0, spring, INK, 1, "4 3")
    s.text(0.05, spring - 0.12, "Kämpferlinie", 10, DIM, "start")
    s.text(1.0, spring - 0.45, "Mittelpunkte der zwei Kreise liegen auf der Kämpferlinie:", 10.5, DIM)
    s.text(1.0, spring - 0.6, "gedrückt = zwischen den Kämpferpunkten (R < Spannweite),", 10.5, DIM)
    s.text(1.0, spring - 0.75, "gleichseitig R = Spannweite, Lanzett = außerhalb (R > Spannweite)", 10.5, DIM)
    s.text(1.0, spring - 0.95, "Stich h = √(R · s − s²/4), Spannweite s = 2 m", 10.5, DIM)
    ground(s, 2.0)
    s.save("01_bogen.svg")


def archivolt(s, x0, spring, hw_list, apexes, z=None):
    pass


def sk_tuer():
    s = Svg(7.0, 3.8, "2 Türrahmen: Normal/Verschlossen (links), Boss A mit Bogenläufen (rechts)", extra_w=0.5)
    # Normal: Zelle 2 x 3,5; Spitzbogen hw 1,0, Kaempfer 2,0, Scheitel 3,45 -> Stich 1,45 (R 1,55)
    x0 = 1.0
    s.rect(0, 0, 2.0, 3.5, "#e9e2e7", "#999", 1)
    s.arc_path(1.0, 2.0, 3.45, 1.0, INK, 2.5)
    s.rect(0, 3.45, 2.0, 3.5, "none", "none", 0)
    # Bogenfeld (Stein) grau, Kopffeld Holz
    s.poly([(0, 2.0), (0.0, 3.5), (2.0, 3.5), (2.0, 2.0)], "none", "none", 0)
    s.rect(0, 0, 0.1, 1.94, "#c9b8b0", INK, 1)
    s.rect(1.9, 0, 2.0, 1.94, "#c9b8b0", INK, 1)
    s.line(0, 1.94, 2.0, 1.94, ACC, 2)
    s.line(1.0, 0, 1.0, 1.94, INK, 1.2)
    s.text(1.0, 1.0, "Flügel", 12)
    s.text(1.0, 2.6, "Kopffeld", 12)
    s.text(0.3, 3.2, "Bogenfeld", 10, DIM, "middle")
    s.text(1.7, 3.2, "Bogenfeld", 10, DIM, "middle")
    s.rect(-0.02, 0, 2.02, 0.2, "#d9d2d6", INK, 1)
    s.text(1.0, 0.07, "Sockel 0,20", 9, DIM)
    s.rect(-0.03, 1.94, 0.13, 2.04, "#d9d2d6", INK, 1)
    s.rect(1.87, 1.94, 2.03, 2.04, "#d9d2d6", INK, 1)
    s.text(-0.12, 1.99, "Kämpfer", 9, DIM, "end")
    s.dim_h(0, 2.0, 0, "2,00 m", -0.18)
    s.dim_v(0.0, 0, 2.0, "Ansatz 2,00", -0.25)
    s.dim_v(0.0, 2.0, 3.45, "Stich 1,45", -0.25)
    ground(s, 2.0)
    # Boss A: vier Kreise um dieselben Mittelpunkte, Ringbreite 0,11
    x0 = 3.9
    s.rect(x0 - 1.0, 0, x0 + 1.0, 3.5, "#e9e2e7", "#999", 1)
    spring = 1.9
    hws = [1.0, 0.89, 0.78, 0.66]
    R0 = 1.7
    for k, hw in enumerate(hws):
        R = R0 - 0.11 * k                           # gleiche Mittelpunkte: Radius je Lauf 0,11 kleiner => Ringbreite ueberall 0,11
        apex = spring + rise(hw, R)
        s.arc_path(hw, spring, apex, x0, "#3d6b8f" if k < 3 else INK, 2)
        if k < 3:
            s.rect(x0 - hws[k], 0, x0 - hws[k + 1], spring, "#d8cfd2", "#3d6b8f", 1)
            s.rect(x0 + hws[k + 1], 0, x0 + hws[k], spring, "#d8cfd2", "#3d6b8f", 1)
    s.text(x0, 1.0, "Flügel", 12)
    s.circle(x0, 2.38, 0.21, "#d9a07a", INK, 1.2)
    s.text(x0, 2.9, "Maßwerk als Textur", 10, DIM)
    s.text(x0 + 1.25, 3.0, "R − 0,11 je Lauf:", 10, DIM, "start")
    s.text(x0 + 1.25, 2.88, "gleiche Mittelpunkte,", 10, DIM, "start")
    s.text(x0 + 1.25, 2.76, "Ringbreite überall 0,11", 10, DIM, "start")
    ground(s, 5.0)
    s.save("02_tuerrahmen.svg")


def sk_tuer_plan():
    s = Svg(5.0, 2.0, "2b Türrahmen im Grundriss (Zelle 2 × 2 m, Schnitt auf Kämpferhöhe)", extra_w=0.2)
    s.base = 1.8 * S
    # Normal links
    s.rect(0, 0, 2.0, 2.0, "#f1ecef", "#999", 1)                       # Zelle
    s.rect(-0.4, 0, 0.0, 2.0, STONE, INK, 1)                           # Wand links
    s.rect(2.0, 0, 2.4, 2.0, STONE, INK, 1)                            # Wand rechts
    s.rect(0.9, 0.93, 1.1, 1.07, "#c9a37a", INK, 1)                    # Fluegel in der Mitte (0,14)
    s.text(1.0, 1.2, "Flügel 0,14 in Zellmitte", 10, DIM)
    s.text(1.0, -0.2, "Normal / Verschlossen: Pfosten bis zur Zellkante, Bogenfeld ±0,97 tief", 10, DIM)
    # Boss A rechts
    ox = 3.4
    s.rect(ox - 1.0, 0, ox + 1.0, 2.0, "#f1ecef", "#999", 1)
    for k, (zz, hw) in enumerate(((0.97, 1.0), (0.65, 0.89), (0.33, 0.78))):
        for sgn in (-1, 1):
            xa, xb = sorted((ox + sgn * hw, ox + sgn * (hw - 0.11)))
            s.rect(xa, 1.0 - zz, xb, 1.0 + zz, "#d8cfd2", "#3d6b8f", 1)
    s.rect(ox - 0.2, 0.9, ox + 0.2, 1.1, "#c9a37a", INK, 1)
    s.text(ox, 1.25, "Läufe treten zur Mitte zurück", 10, DIM)
    s.text(ox, 1.14, "(0,97 / 0,65 / 0,33)", 10, DIM)
    s.text(ox, -0.2, "Boss A: drei Läufe, Flügel 0,20 dick in der Mitte", 10, DIM)
    s.save("02b_tuerrahmen_grundriss.svg")


def sk_pfeiler():
    s = Svg(3.6, 3.7, "3 Wandpfeiler (Gang, Kämpfer 2,0 m): Ansicht, Grundriss, Profil", extra_w=0.6)
    # Ansicht: Basis 0,5 breit x 0,25, Schaft 0,375 x 1,5, Kapitell 0,5 x 0,25 -> Kaempfer 2,0
    cx = 0.8
    s.rect(cx - 0.25, 0, cx + 0.25, 0.25, "#cfc2c8", INK, 1.5)             # Basis
    s.rect(cx - 0.1875, 0.25, cx + 0.1875, 1.75, "#e0d5da", INK, 1.5)      # Schaft
    s.rect(cx - 0.25, 1.75, cx + 0.25, 2.0, "#cfc2c8", INK, 1.5)           # Kapitell
    s.rect(cx - 0.3, 1.94, cx + 0.3, 2.0, "#bfb0b8", INK, 1)               # Kaempferplatte
    s.text(cx + 0.45, 0.12, "Basis 0,50 × 0,25", 10, DIM, "start")
    s.text(cx + 0.45, 1.0, "Schaft 0,375 × 1,50", 10, DIM, "start")
    s.text(cx + 0.45, 1.85, "Kapitell 0,50 × 0,25", 10, DIM, "start")
    s.text(cx + 0.45, 2.03, "Kämpferplatte 0,60 × 0,06", 10, DIM, "start")
    s.dim_v(cx - 0.3, 0, 2.0, "Kämpfer 2,00", -0.15)
    s.rect(-0.4, 0, 0.0, 3.5, STONE, "none", 0)
    s.line(0, 0, 0, 3.5, INK, 2)
    s.line(0, 0.5, 2.2, 0.5, ACC, 1.5, "5 3")
    s.text(2.3, 0.45, "Oberkante Sockelband 0,50", 10, ACC, "start")
    s.text(2.3, 0.3, "(Basis steht vor dem Sockel)", 10, DIM, "start")
    ground(s, 1.6)
    # Grundriss rechts oben
    ox, oy = 3.2, 3.0
    s.rect(ox - 0.8, oy, ox + 0.8, oy + 0.12, STONE, INK, 1)               # Wand
    s.poly([(ox - 0.1875, oy + 0.12), (ox + 0.1875, oy + 0.12), (ox + 0.1875 - 0.04, oy + 0.37), (ox - 0.1875 + 0.04, oy + 0.37)], "#e0d5da", INK, 1.5)
    s.text(ox, oy + 0.55, "Grundriss: 0,375 breit, 0,25 vor der Wand, Fasen 0,04", 9.5, DIM)
    s.text(ox, oy - 0.1, "Wand", 10, DIM)
    s.text(ox, oy + 0.72, "Vorsprung ≤ 0,30 (Spieler hält 0,35 Abstand)", 9.5, DIM)
    s.save("03_wandpfeiler.svg")


def sk_saeule():
    s = Svg(3.0, 6.1, "4 Frei stehende Säule (Raum, 6 m): Ansicht und Grundriss", extra_w=2.6)
    cx = 1.0
    s.rect(cx - 0.55, 0, cx + 0.55, 0.35, "#cfc2c8", INK, 1.5)             # Sockel/Basis (Platte 1,10)
    s.rect(cx - 0.45, 0.35, cx + 0.45, 4.9, "#e0d5da", INK, 1.5)           # Schaft 0,90
    s.rect(cx - 0.55, 4.9, cx + 0.55, 5.4, "#cfc2c8", INK, 1.5)            # Kapitell
    s.rect(cx - 0.6, 5.4, cx + 0.6, 5.5, "#bfb0b8", INK, 1.5)              # Deckplatte
    s.poly([(cx - 0.6, 5.5), (cx + 0.6, 5.5), (cx + 0.3, 6.0), (cx - 0.3, 6.0)], "#e0d5da", INK, 1.2)   # Anlauf zur Decke
    s.text(cx + 0.85, 0.17, "Basis 1,10 × 0,35 (viereckig)", 10, DIM, "start")
    s.text(cx + 0.85, 2.6, "Schaft Ø 0,90 × 4,55 (Verhältnis 5 : 1)", 10, DIM, "start")
    s.text(cx + 0.85, 5.15, "Kapitell 1,10 × 0,50", 10, DIM, "start")
    s.text(cx + 0.85, 5.45, "Deckplatte 1,20 × 0,10", 10, DIM, "start")
    s.text(cx + 0.85, 5.75, "Anlauf zur Decke 0,50", 10, DIM, "start")
    s.line(-0.3, 0.5, 2.5, 0.5, ACC, 1.2, "5 3")
    s.text(-0.3, 0.55, "Sockelband 0,50", 10, ACC, "start")
    s.line(-0.3, 5.5, 2.5, 5.5, ACC, 1.2, "5 3")
    s.text(-0.3, 5.55, "Abschlussband 5,50", 10, ACC, "start")
    ground(s, 2.0)
    ox, oy = 4.2, 3.0
    s.rect(ox - 0.55, oy - 0.55, ox + 0.55, oy + 0.55, "#cfc2c8", INK, 1)
    s.circle(ox, oy, 0.45, "#e0d5da", INK, 1.5)
    s.circle(ox, oy, 0.35 + 0.0, "none", "#999", 0.8)
    s.text(ox, oy - 0.8, "Grundriss", 12, INK, "middle", "bold")
    s.text(ox, oy - 1.0, "Sperrt nur den Kreis Ø 0,90", 10, DIM)
    s.text(ox, oy - 1.15, "(Kollision), Platte 1,10 ist Zierde", 10, DIM)
    s.text(ox, oy + 0.85, "Schaft: 12-Eck (24 Dreiecke je Meter)", 10, DIM)
    s.save("04_saeule.svg")


def sk_deckenbogen():
    s = Svg(2.0, 3.8, "5 Deckenbogen (Gurtbogen) über einem Gang: Querschnitt und Längsschnitt", extra_w=6.2)
    s.rect(0, 0, 2.0, 3.5, "#f3eff2", "#999", 1)
    s.rect(-0.3, 0, 0, 3.5, STONE, INK, 1)
    s.rect(2.0, 0, 2.3, 3.5, STONE, INK, 1)
    # Wandpfeiler 0,25 vor der Wand
    for x in (0.0, 1.75):
        s.rect(x, 0, x + 0.25, 2.0, "#e0d5da", INK, 1.2)
        s.rect(x - (0.0 if x == 0 else 0.0), 1.94, x + 0.25, 2.0, "#bfb0b8", INK, 1)
    # Gurtbogen: innere Kante hw 0,75 (zwischen den Pfeilern), aussen +0,25
    s.arc_path(0.75 + 0.0, 2.0, 2.0 + rise(0.75, 1.2), 1.0, INK, 2.5)
    s.arc_path(1.0, 2.0, 2.0 + rise(1.0, 1.6), 1.0, "#3d6b8f", 2)
    s.text(1.0, 0.6, "lichte Weite 1,50", 10, DIM)
    s.text(1.0, 0.45, "(Pfeiler 0,25 je Seite)", 10, DIM)
    s.text(1.0, 3.55, "Scheitel 3,48 (R 1,6) an der Decke", 10, DIM)
    s.dim_v(0.0, 0, 2.0, "Kämpfer 2,00", -0.35)
    ground(s, 2.0)
    # Laengsschnitt
    ox = 3.2
    s.rect(ox, 0, ox + 4.0, 3.5, "#f3eff2", "#999", 1)
    s.line(ox, 0.5, ox + 4.0, 0.5, ACC, 1.2, "5 3")
    s.text(ox + 4.05, 0.47, "Sockel 0,50", 10, ACC, "start")
    s.line(ox, 3.0, ox + 4.0, 3.0, ACC, 1.2, "5 3")
    s.text(ox + 4.05, 2.97, "Abschluss 3,00", 10, ACC, "start")
    for k in (0.0, 2.0, 4.0):
        x = ox + k
        s.rect(x - 0.125, 0, x + 0.125, 3.5, "none", "none", 0)
    for x in (ox + 0.0, ox + 4.0):
        s.rect(x - 0.125, 3.5 - 0.35, x + 0.125, 3.5, "#d8cfd2", INK, 1.5)       # Rippe 0,25 breit, 0,35 hoch (Profil, mit Fase)
        s.rect(x - 0.125, 0, x + 0.125, 2.0, "#e0d5da", INK, 1.2)                  # Pfeiler
    s.text(ox + 2.0, 3.62, "Joch 4,00 m: ein Bogen je zwei Zellen, Pfeiler gegenüber", 10, DIM)
    s.text(ox + 2.0, 1.2, "Fugen der Wand laufen zwischen den Pfeilern weiter", 10, DIM)
    s.save("05_deckenbogen.svg")


def sk_nische():
    s = Svg(1.6, 3.0, "6 Wandnische (Blendnische): Ansicht und Schnitt", extra_w=3.4)
    s.rect(-0.3, 0, 1.9, 3.0, STONE, "none", 0)
    s.rect(0, 0.5, 1.0, 1.7, "#4c4450", INK, 1.5)                        # Fuellung dunkel
    s.arc_path(0.5, 1.7, 1.7 + rise(0.5, 1.0), 0.5, INK, 2.5, "#4c4450")
    s.rect(-0.1, 0.5, 0.0, 1.7, "#d8cfd2", INK, 1)
    s.rect(1.0, 0.5, 1.1, 1.7, "#d8cfd2", INK, 1)
    s.arc_path(0.6, 1.7, 1.7 + rise(0.6, 1.1), 0.5, INK, 2)
    s.rect(-0.12, 0.38, 1.12, 0.5, "#cfc2c8", INK, 1.5)                  # Bank
    s.text(0.5, 1.2, "Blendfeld", 11, "#fff")
    s.dim_h(0, 1.0, 0.38, "1,00", 0.2)
    s.dim_v(1.15, 0.5, 2.57, "Scheitel 2,57", -0.15)
    s.text(0.5, 2.55, "Spitzbogen gleichseitig R = 1,0 (Stich 0,87)", 10, DIM)
    s.line(-0.3, 0.5, 1.9, 0.5, ACC, 1.2, "5 3")
    s.text(1.95, 0.52, "Sockelband 0,50 (Bank sitzt darauf)", 10, ACC, "start")
    ground(s, 1.6)
    ox, oy = 3.4, 1.5
    s.rect(ox - 0.3, oy, ox + 1.3, oy + 0.12, STONE, INK, 1)
    s.rect(ox - 0.1, oy + 0.12, ox + 1.1, oy + 0.24, "#d8cfd2", INK, 1)
    s.rect(ox, oy + 0.12, ox + 1.0, oy + 0.18, "#4c4450", INK, 1)
    s.text(ox + 0.5, oy - 0.1, "Schnitt (Grundriss): Rahmen 0,12 vor der Wand,", 10, DIM)
    s.text(ox + 0.5, oy - 0.25, "Blendfeld nur 0,06 tief (keine Höhlung in der Wand)", 10, DIM)
    s.save("06_wandnische.svg")


def sk_gitter():
    s = Svg(2.0, 6.0, "7 Fallgitter für die Arena: Ansicht, Maße und das Höhenproblem in 3,5-m-Zellen", extra_w=4.6)
    # Raumzelle 6 m: Sturz ab 2,4; Gitter 2,3 hoch
    s.rect(0, 0, 2.0, 6.0, "#f3eff2", "#999", 1)
    s.rect(0, 2.4, 2.0, 6.0, "#e0d5da", INK, 1.5)
    s.rect(0, 0, 0.2, 2.4, "#e0d5da", INK, 1.5)
    s.rect(1.8, 0, 2.0, 2.4, "#e0d5da", INK, 1.5)
    for k in range(10):
        x = 0.28 + k * 0.16
        s.line(x, 0.1, x, 2.3, INK, 2)
    for y in (0.6, 1.4, 2.1):
        s.line(0.2, y, 1.8, y, INK, 3)
    s.add("")
    s.text(1.0, 3.5, "Sturz (Stein), Gitter fährt hinein", 11)
    s.text(1.0, 3.3, "Spitzbogen-Blende im Sturz (Textur)", 10, DIM)
    s.dim_v(2.0, 0, 2.4, "lichte Höhe 2,40", -0.25)
    s.dim_h(0.2, 1.8, 0, "Gitter 1,60", -0.2)
    s.text(1.0, 2.5, "Gitter 2,30 hoch, Stäbe 0,05, Abstand 0,16, Spitzen unten", 9.5, DIM)
    ground(s, 2.0)
    ox = 3.4
    s.rect(ox, 0, ox + 1.2, 3.5, "#f3eff2", "#999", 1)
    s.rect(ox, 1.9, ox + 1.2, 3.5, "#e0d5da", INK, 1.5)
    s.text(ox + 0.6, 2.7, "Zelle 3,5 m:", 11, "#a33")
    s.text(ox + 0.6, 2.55, "Sturz ab 1,90 lässt nur", 10, "#a33")
    s.text(ox + 0.6, 2.4, "1,60 m Platz für das Gitter", 10, "#a33")
    s.text(ox + 0.6, 2.25, "→ es fährt nicht ganz in", 10, "#a33")
    s.text(ox + 0.6, 2.1, "den Sturz (Spieler 1,8 m)", 10, "#a33")
    s.text(ox + 0.6, 0.9, "Regel: Fallgitter nur in", 10, DIM)
    s.text(ox + 0.6, 0.75, "Zellen ab 4,5 m Höhe", 10, DIM)
    s.text(ox + 0.6, 0.6, "(Gitterhöhe ≤ halbe Zellhöhe", 10, DIM)
    s.text(ox + 0.6, 0.45, "− Sturzstärke 0,3)", 10, DIM)
    s.save("07_fallgitter.svg")


def sk_zonen():
    s = Svg(5.0, 6.3, "8 Drei Zonen der Wand (#282) und wie die Bausteine anschließen", extra_w=3.4)
    # Gang 3,5 und Raum 6
    for ox, hh, name in ((0.0, 3.5, "Gang 3,5 m"), (2.8, 6.0, "Raum 6 m")):
        s.rect(ox, 0, ox + 2.0, hh, "#f3eff2", "#999", 1)
        s.rect(ox, 0, ox + 2.0, 0.5, "#cdbfc6", INK, 1)
        s.rect(ox, hh - 0.5, ox + 2.0, hh, "#cdbfc6", INK, 1)
        s.text(ox + 1.0, 0.2, "Sockel 0,50", 10)
        s.text(ox + 1.0, hh - 0.3, "Abschluss 0,50", 10)
        s.text(ox + 1.0, hh / 2, "Fläche", 12)
        s.text(ox + 1.0, hh + 0.12, name, 11, DIM)
    # Bausteine: Pfeiler (Gang) und Saeule (Raum)
    s.rect(0.0, 0, 0.25, 2.0, "#e0d5da", INK, 1.5)
    s.rect(0.0, 0, 0.3, 0.25, "#bfb0b8", INK, 1.5)
    s.rect(0.0, 1.75, 0.3, 2.0, "#bfb0b8", INK, 1.5)
    s.text(0.45, 1.0, "Pfeiler: Basis auf Sockelband,", 9, DIM, "start")
    s.text(0.45, 0.88, "Kapitell unter dem Abschluss", 9, DIM, "start")
    s.rect(4.0, 0, 4.8, 5.5, "#e0d5da", INK, 1.5)
    s.rect(3.9, 0, 4.9, 0.35, "#bfb0b8", INK, 1.5)
    s.rect(3.9, 5.0, 4.9, 5.5, "#bfb0b8", INK, 1.5)
    ground(s, 5.0)
    s.text(5.1, 0.25, "← Sockelband: Anfang | Mitte (wiederholbar) | Ende", 9.5, DIM, "start")
    s.text(5.1, 0.12, "Baustein steht VOR dem Band und deckt es ab:", 9.5, DIM, "start")
    s.text(5.1, -0.01, "Bandende = Pfeilerkante, nie nackte Kante", 9.5, DIM, "start")
    s.save("08_zonen.svg")


if __name__ == "__main__":
    sk_bogen()
    sk_tuer()
    sk_tuer_plan()
    sk_pfeiler()
    sk_saeule()
    sk_deckenbogen()
    sk_nische()
    sk_gitter()
    sk_zonen()
    print("ok", len(os.listdir(OUT)))
