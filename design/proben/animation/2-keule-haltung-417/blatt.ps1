<#
Probenblatt Keule #417: setzt die Einzelbilder aus rendern.sh zu einem Blatt zusammen (System.Drawing, nichts heruntergeladen).
Aufruf: powershell -File blatt.ps1 -Frames <Ordner mit heute, A, B, C, q1..q6, c1..c6> -Out <blatt.png>
#>
param([Parameter(Mandatory)][string]$Frames, [Parameter(Mandatory)][string]$Out)
Add-Type -AssemblyName System.Drawing

function Load($name) {
    $dir = Join-Path $Frames $name
    $file = Get-ChildItem $dir -Filter 'club-rest.png' | Select-Object -First 1
    return [System.Drawing.Image]::FromFile($file.FullName)
}

$bg = [System.Drawing.Color]::FromArgb(24, 22, 28)
$fg = [System.Drawing.Brushes]::White
$hi = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 214, 120))
$fontBig = New-Object System.Drawing.Font('Consolas', 20, [System.Drawing.FontStyle]::Bold)
$font = New-Object System.Drawing.Font('Consolas', 14, [System.Drawing.FontStyle]::Bold)

$W = 1708
$title = 44; $cap = 30
$h1 = $title + 2 * (480 + $cap)
$pw = 569; $ph = 320
$h2 = $title + 2 * ($ph + $cap)
$H = $h1 + $h2 + $h2
$bmp = New-Object System.Drawing.Bitmap $W, $H
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear($bg)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

function Title($y, $text) { $g.DrawString($text, $fontBig, $hi, 10, $y + 8) }
function Panel($name, $x, $y, $w, $h, $label, $best) {
    $img = Load $name
    $g.DrawImage($img, $x, $y + $cap, $w, $h)
    $img.Dispose()
    $brush = $fg
    if ($best) { $brush = $hi }
    $g.DrawString($label, $font, $brush, $x + 6, $y + 5)
}

# 1. Ruhehaltung
Title 0 '1  Ruhehaltung: heute und drei Varianten (Ich-Sicht 854 x 480)'
$y0 = $title
Panel 'heute' 0 $y0 854 480 'HEUTE: Kopf nach vorn in die Tiefe (wie eine Stichwaffe)' $false
Panel 'A' 854 $y0 854 480 'A leicht: Kopf 35 Grad hoch, 12 Grad nach aussen' $false
Panel 'B' 0 ($y0 + 480 + $cap) 854 480 'B mittel: Kopf 55 Grad hoch, 20 Grad nach aussen  (EMPFOHLEN)' $true
Panel 'C' 854 ($y0 + 480 + $cap) 854 480 'C stark: Kopf 70 Grad hoch, 25 Grad nach aussen' $false

# 2. Schnellschlag (B)
$y1 = $h1
Title $y1 '2  Schnellschlag aus B: Bogen von oben links nach rechts (17 Ticks; gezeigt Tick 0 / 1 / 2 / 4 / 6 / 11)'
$labels = @('Tick 0: Ruhe', 'Tick 1: Schlag 55 %', 'Tick 2: Schlag 85 %', 'Tick 4: Treffer-Moment', 'Tick 6: Ausschwingen', 'Tick 11: Rueckweg')
for ($i = 0; $i -lt 6; $i++) {
    $row = [int][math]::Floor($i / 3)
    Panel ('q' + ($i + 1)) (($i % 3) * $pw) ($y1 + $title + $row * ($ph + $cap)) $pw $ph $labels[$i] ($i -eq 3)
}

# 3. Aufgeladener Schlag (B)
$y2 = $h1 + $h2
Title $y2 '3  Aufgeladener Schlag aus B: Ausholen beim Halten, dann derselbe Bogen aus der Ausholstellung'
$labels = @('Ausholen 25 %', 'Ausholen 60 %', 'Ausholen 100 % (voll)', 'Loslassen, Tick 1', 'Loslassen, Tick 2', 'Treffer-Moment, Tick 4')
for ($i = 0; $i -lt 6; $i++) {
    $row = [int][math]::Floor($i / 3)
    Panel ('c' + ($i + 1)) (($i % 3) * $pw) ($y2 + $title + $row * ($ph + $cap)) $pw $ph $labels[$i] ($i -eq 2)
}
$g.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Blatt: $Out"
