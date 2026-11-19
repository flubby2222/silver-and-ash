# Probenblatt Gorm-Haken #437: Vorhalt (A/B/C) und Dauerverfolgung. Nichts heruntergeladen, nichts im Spiel veraendert.
# Liest die Werte aus assets/balance.json, rechnet tickweise (60/s) und zeichnet mit System.Drawing.
# Aufruf: powershell -File blatt.ps1 -Out <blatt.png> [-Zahlen <zahlen.txt>]
# Zwei Laeufe ergeben dieselbe Datei (keine Zufallszahlen, keine Uhrzeit im Bild).
param([Parameter(Mandatory)][string]$Out, [string]$Zahlen = '')
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')

# ---------- Werte ----------
$lines = Get-Content (Join-Path $root 'assets\balance.json') -Encoding UTF8 | ForEach-Object { $_ -replace '\s//.*$', '' } | Where-Object { $_.Trim() -notmatch '^//' }
$bal = ($lines -join "`n") | ConvertFrom-Json
$G = $bal.gorm; $Pl = $bal.player
$hookSpeed = [double]$G.hookSpeed            # m/s
$step = $hookSpeed / 60.0                    # m je Tick
$runSpeed = [double]$Pl.maxSpeed             # m/s
$windUp1 = [int][math]::Floor([double]$G.throwWindUp * 60 + 0.5)
$windUp2 = [int][math]::Floor([double]$G.throwWindUp / [double]$G.phase2SpeedUp * 60 + 0.5)
$pause1 = [int][math]::Floor([double]$G.pauseTime * 60 + 0.5)
$pause2 = [int][math]::Floor([double]$G.pauseTime / [double]$G.phase2SpeedUp * 60 + 0.5)
$farRange = [double]$G.farRange; $nearRange = [double]$G.nearRange
$hookMax = $farRange + 3.0                   # Core: FarRange + 3 m Flugweg
$gormR = 0.9; $clear = 0.35 + 0.3            # Koerper Gorm; Spielerradius + Hakenradius
$reaction = 15                               # Annahme: 0,25 s Reaktion
$grav = 8.0 * [double]$Pl.jumpHeight / ([double]$Pl.jumpDuration * [double]$Pl.jumpDuration)
$fallTicks = [int][math]::Floor([math]::Sqrt(2.0 * 1.5 / $grav) * 60 + 0.5)   # Haken faellt aus 1,5 m
$spool = 6
$dashStep = [double]$Pl.dashDistance / [int][math]::Floor([double]$Pl.dashDuration * 60 + 0.5)

# ---------- Rechnung ----------
# Varianten: k = Staerke des Vorhalts, cap = groesster Vorhalt (m), L = Ticks Einrasten vor dem Wurf
$variants = [ordered]@{
    'HEUTE' = @{ k = 0.0; cap = 99.0; L = 0 }
    'A'     = @{ k = 1.0; cap = 99.0; L = 0 }
    'B'     = @{ k = 1.0; cap = 6.0;  L = 12 }
    'C'     = @{ k = 1.0; cap = 4.0;  L = 18 }
}
# Zielpunkt Q (Wurzel der Flugzeit per Iteration), Wurfrichtung
function Aim($d, $vx, $vy, $k, $cap) {
    $t = $d / $hookSpeed
    for ($i = 0; $i -lt 40; $i++) {
        $lx = $k * $vx * $t; $ly = $k * $vy * $t
        $len = [math]::Sqrt($lx * $lx + $ly * $ly)
        if ($len -gt $cap) { $lx *= $cap / $len; $ly *= $cap / $len }
        $qx = $d + $lx; $qy = $ly
        $t = ([math]::Sqrt($qx * $qx + $qy * $qy) - $gormR) / $hookSpeed
    }
    return @{ qx = $qx; qy = $qy; t = $t }
}
# Ein Wurf, tickweise. mode: run | stop | back | dash. nr = Tick der Reaktion (0 = Wurf, negativ = davor).
# Rueckgabe: Trefferstick (oder -1), kleinster Abstand, Spielerbahn
function Sim($d, $vx, $vy, $var, $mode, $nr) {
    $aim = Aim $d $vx $vy $var.k $var.cap
    $ql = [math]::Sqrt($aim.qx * $aim.qx + $aim.qy * $aim.qy)
    $dx = $aim.qx / $ql; $dy = $aim.qy / $ql
    $n0 = -$var.L
    $px = $d - $vx * $var.L / 60.0; $py = -$vy * $var.L / 60.0
    $min = 99.0; $hit = -1; $path = @()
    $nMax = [int][math]::Floor(($hookMax - $gormR) / $step)
    for ($n = $n0 + 1; $n -le $nMax; $n++) {
        $m = 1.0
        if ($n -gt $nr) {
            $j = $n - $nr - 1
            switch ($mode) {
                'stop' { $m = [math]::Max(0.0, 1.0 - $j / 6.0) }
                'back' { $m = [math]::Max(-1.0, 1.0 - 2.0 * $j / 6.0) }
                'dash' { $m = 0.0 }
            }
        }
        $px += $vx * $m / 60.0; $py += $vy * $m / 60.0
        if ($mode -eq 'dash' -and $n -gt $nr -and ($n - $nr) -le 9) {
            $vl = [math]::Sqrt($vx * $vx + $vy * $vy); $px -= $vx / $vl * $dashStep; $py -= $vy / $vl * $dashStep
        }
        $path += , @($n, $px, $py)
        if ($n -ge 1) {
            $hx = $dx * ($gormR + $step * $n); $hy = $dy * ($gormR + $step * $n)
            $dist = [math]::Sqrt(($hx - $px) * ($hx - $px) + ($hy - $py) * ($hy - $py))
            if ($dist -lt $min) { $min = $dist }
            if ($dist -le $clear -and $hit -lt 0) { $hit = $n }
        }
    }
    return @{ hit = $hit; min = $min; path = $path; q = $aim; dx = $dx; dy = $dy; t = $aim.t }
}
# Spaetester Reaktionstick, mit dem man noch entkommt; Fenster = spaetester - fruehester (Cue + Reaktion)
function Window($d, $vx, $vy, $var, $mode) {
    for ($nr = 130; $nr -ge -$var.L; $nr--) {
        $s = Sim $d $vx $vy $var $mode $nr
        if ($s.hit -lt 0) { return @{ latest = $nr; window = $nr + $var.L - $reaction } }
    }
    return @{ latest = -999; window = -999 }
}

$ranges = 8, 12, 17, 22, 30
$zl = New-Object System.Collections.Generic.List[string]
$zl.Add("Werte: hookSpeed=$hookSpeed m/s ($([math]::Round($step,2)) m/Tick), Lauf=$runSpeed m/s, Windup=$windUp1 Ticks (Phase 2: $windUp2), Pause=$pause1 (Phase 2: $pause2), Reichweite Haken=$hookMax m, Abstandsgrenze=$clear m, Fallzeit=$fallTicks Ticks, Dash=$([math]::Round($dashStep,3)) m/Tick")
$tab = @{}
foreach ($name in $variants.Keys) {
    $var = $variants[$name]
    foreach ($d in $ranges) {
        $q = Sim $d 0.0 $runSpeed $var 'run' 9999
        $w = $null; if ($name -ne 'HEUTE') { $w = Window $d 0.0 $runSpeed $var 'stop' }
        $wd = $null; if ($name -ne 'HEUTE') { $wd = Window $d 0.0 $runSpeed $var 'dash' }
        $r = Sim $d $runSpeed 0.0 $var 'run' 9999
        $tab["$name|$d"] = @{ quer = ($q.hit -ge 0); flug = [int][math]::Round($q.t * 60); win = $(if ($w) { $w.window } else { $null }); windash = $(if ($wd) { $wd.window } else { $null }); weg = ($r.hit -ge 0) }
        $zl.Add(("{0,-6} d={1,2} m  quer-laeufer: {2,-7} flug {3,3} Ticks  fenster(stopp) {4,4}  fenster(dash) {5,4}  weglaeufer: {6}" -f $name, $d, $(if ($q.hit -ge 0) { 'TREFFER' } else { 'verfehlt' }), $tab["$name|$d"].flug, $tab["$name|$d"].win, $tab["$name|$d"].windash, $(if ($r.hit -ge 0) { 'TREFFER' } else { 'verfehlt' })))
    }
}
if ($Zahlen -ne '') { $zl | Set-Content -Path $Zahlen -Encoding UTF8 }

# ---------- Zeichnen ----------
$W = 1800
$bg = [System.Drawing.Color]::FromArgb(24, 22, 28)
function Br($r, $g2, $b) { New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($r, $g2, $b)) }
function Pn($r, $g2, $b, $w) { New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb($r, $g2, $b)), $w }
$white = Br 240 240 244; $gold = Br 255 214 120; $cyan = Br 110 200 230; $grey = Br 150 150 162; $orange = Br 240 150 60; $green = Br 130 210 130; $dust = Br 205 200 185
$panel = Br 38 36 46; $dark = Br 18 17 22
$fT = New-Object System.Drawing.Font('Consolas', 30, [System.Drawing.FontStyle]::Bold)
$fH = New-Object System.Drawing.Font('Consolas', 24, [System.Drawing.FontStyle]::Bold)
$fB = New-Object System.Drawing.Font('Consolas', 20, [System.Drawing.FontStyle]::Bold)
$fS = New-Object System.Drawing.Font('Consolas', 17, [System.Drawing.FontStyle]::Bold)

$H = 3300
$bmp = New-Object System.Drawing.Bitmap $W, $H
$gr = [System.Drawing.Graphics]::FromImage($bmp)
$gr.Clear($bg)
$gr.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$gr.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
function T($text, $x, $y, $font, $brush) { $gr.DrawString($text, $font, $brush, [single]$x, [single]$y) }
function Rect($x, $y, $w, $h, $brush) { $gr.FillRectangle($brush, [single]$x, [single]$y, [single]$w, [single]$h) }
function Circ($cx, $cy, $r, $brush) { $gr.FillEllipse($brush, [single]($cx - $r), [single]($cy - $r), [single](2 * $r), [single](2 * $r)) }
function Line($x1, $y1, $x2, $y2, $pen) { $gr.DrawLine($pen, [single]$x1, [single]$y1, [single]$x2, [single]$y2) }

$ae = [char]0xE4; $oe = [char]0xF6; $ue = [char]0xFC; $ss = [char]0xDF

# Draufsicht: Gorm links, Spieler laeuft quer (nach unten). ox/oy = Ecke, sc = Pixel je Meter
function TopView($ox, $oy, $w, $h, $title, $titleBrush, $d, $name, $dodgeMode, $sc, $note) {
    Rect $ox $oy $w $h $panel
    T $title ($ox + 10) ($oy + 6) $fH $titleBrush
    $var = $variants[$name]
    $x0 = $ox + 40; $y0 = $oy + 80
    $s = Sim $d 0.0 $runSpeed $var 'run' 9999
    $mx = { param($x) $x0 + $x * $sc }; $my = { param($y) $y0 + $y * $sc }
    Circ (& $mx 0) (& $my 0) ($gormR * $sc) (Br 90 80 100)
    T 'G' ((& $mx 0) - 10) ((& $my 0) - 14) $fB $white
    # Wurfbahn bis Flugende
    $endN = [int][math]::Floor(($hookMax - $gormR) / $step)
    $pHook = Pn 240 150 60 4
    $reach = [math]::Min($d + 3.0, $hookMax)
    $ex = $s.dx * $reach; $ey = $s.dy * $reach
    Line (& $mx ($s.dx * $gormR)) (& $my ($s.dy * $gormR)) (& $mx $ex) (& $my $ey) $pHook
    # Spielerbahn (laufend), Punkte alle 12 Ticks
    $pRun = Pn 150 150 162 2
    for ($n = - $var.L; $n -le 60; $n += 12) {
        $y = $runSpeed * $n / 60.0
        $a = if ($n -eq 0) { 255 } else { 255 }
        Circ (& $mx $d) (& $my $y) (0.35 * $sc) (Br 150 150 162)
        $gr.DrawEllipse($pRun, [single]((& $mx $d) - 0.35 * $sc), [single]((& $my $y) - 0.35 * $sc), [single](0.7 * $sc), [single](0.7 * $sc))
    }
    # Zielpunkt
    $qx = & $mx $s.q.qx; $qy = & $my $s.q.qy
    $gr.DrawLine((Pn 255 214 120 4), [single]($qx - 12), [single]($qy - 12), [single]($qx + 12), [single]($qy + 12))
    $gr.DrawLine((Pn 255 214 120 4), [single]($qx - 12), [single]($qy + 12), [single]($qx + 12), [single]($qy - 12))
    # Staubspur (C): helle Linie vom Haken zum Zielpunkt waehrend Einrasten und Flug
    if ($name -eq 'C') {
        $pd = Pn 205 200 185 5; $pd.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dot
        Line (& $mx ($s.dx * 1.6)) (& $my ($s.dy * 1.6)) $qx $qy $pd
    }
    # Hakenpunkte alle 12 Ticks
    for ($n = 12; $n * $step + $gormR -le $reach; $n += 12) {
        Circ (& $mx ($s.dx * ($gormR + $step * $n))) (& $my ($s.dy * ($gormR + $step * $n))) 7 $orange
    }
    # Ausweichender (hellblau): letzter Zeitpunkt zum Stehenbleiben
    if ($dodgeMode) {
        $wd = Window $d 0.0 $runSpeed $var 'stop'
        $sd = Sim $d 0.0 $runSpeed $var 'stop' $wd.latest
        $pc = Pn 110 200 230 3
        $prev = $null
        foreach ($pt in $sd.path) {
            if ($pt[0] -ge 0 -and $pt[0] % 3 -eq 0) {
                $xx = & $mx $pt[1]; $yy = & $my $pt[2]
                if ($prev) { Line $prev[0] $prev[1] $xx $yy $pc }
                $prev = @($xx, $yy)
            }
        }
        $last = $sd.path[$sd.path.Count - 1]
        $gr.DrawEllipse($pc, [single]((& $mx $last[1]) - 0.35 * $sc), [single]((& $my $last[2]) - 0.35 * $sc), [single](0.7 * $sc), [single](0.7 * $sc))
        # Stelle des Spielers beim Vorbeiflug des Hakens
        $hitTick = [int][math]::Round($s.t * 60)
        foreach ($pt in $sd.path) { if ($pt[0] -eq $hitTick) { Circ (& $mx $pt[1]) (& $my $pt[2]) (0.35 * $sc) (Br 110 200 230) } }
        T ("Ausweichfenster $($wd.window) Ticks (Stopp)") ($ox + 10) ($oy + $h - 70) $fS $cyan
        T ("Abstand beim Vorbeiflug $([math]::Round($sd.min,2)) m") ($ox + 10) ($oy + $h - 42) $fS $cyan
    }
    T $note ($ox + 10) ($oy + 44) $fS $grey
}

# ---------- Kopf ----------
T "GORMS HAKEN (#437): Vorhalt und Dauerverfolgung - Entwurf" 20 12 $fT $gold
T "Zahlen aus balance.json, tickweise (60/s). Haken $([math]::Round($step,2)) m/Tick, Spieler $runSpeed m/s, Trefferabstand $clear m." 20 56 $fS $grey

# ---------- Zeile 1: heute / mit Vorhalt ----------
T "1  Draufsicht: Spieler l$($ae)uft quer, Gorm (G) wirft aus 20 m" 20 100 $fH $white
TopView 10 140 880 340 "HEUTE: Ziel = wo du stehst" $orange 20 'HEUTE' $false 38 "Haken fliegt gerade, du bist l$($ae)ngst weiter: verfehlt"
TopView 910 140 880 340 "MIT VORHALT: Ziel = wo du sein wirst" $gold 20 'A' $false 38 "Gelbes X = Zielpunkt: Weiterlaufen trifft"

# ---------- Zeile 2: A B C in Draufsicht mit Ausweichen ----------
T "2  Wurf aus 12 m. Orange Haken, gelb X Ziel, blau: wer sp$($ae)t stehen bleibt" 20 500 $fH $white
TopView 10 540 590 420 "A  Roh" $white 12 'A' $true 34 "Vorhalt 100 %, ohne Hinweis"
TopView 605 540 590 420 "B  Einrasten" $gold 12 'B' $true 34 "Vorhalt 100 %, max. 6 m, 12 Ticks Halt"
TopView 1200 540 590 420 "C  Einrasten + Spur" $white 12 'C' $true 34 "Vorhalt max. 4 m, 18 Halt, Spur"

# ---------- Zeile 3: Zeitleisten Vorhalt ----------
function Timeline($ox, $oy, $ppt, $ticks, $segs, $marks, $axisStep) {
    foreach ($sg in $segs) {
        Rect ($ox + $sg[0] * $ppt) $oy (($sg[1] - $sg[0]) * $ppt) 46 $sg[2]
        if ($sg[3]) { T $sg[3] ($ox + $sg[0] * $ppt + 6) ($oy + 10) $fS $dark }
    }
    for ($t = 0; $t -le $ticks; $t += $axisStep) {
        Line ($ox + $t * $ppt) ($oy + 46) ($ox + $t * $ppt) ($oy + 58) (Pn 150 150 162 2)
        T "$t" ($ox + $t * $ppt - 10) ($oy + 58) $fS $grey
    }
}
$y3 = 990
T "3  Zeit je Variante in Ticks (Wurf aus 12 m), hellblau = Zeit zum Ausweichen" 20 $y3 $fH $white
$ppt = 11.2
$rowY = $y3 + 80
$labels = @(@('A', 0), @('B', 12), @('C', 18))
$ri = 0
foreach ($lab in $labels) {
    $nm = $lab[0]; $L = $lab[1]; $var = $variants[$nm]
    $s20 = Sim 12 0.0 $runSpeed $var 'run' 9999
    $flug = [int][math]::Round($s20.t * 60)
    $win = $tab["$nm|22"]
    $w20 = Window 12 0.0 $runSpeed $var 'stop'
    $aimEnd = $windUp1 - $L
    $y = $rowY + $ri * 150
    T "$nm" 10 ($y + 6) $fT $gold
    $segs = @(, @(0, $aimEnd, (Br 120 120 135), 'zielt nach'))
    if ($L -gt 0) { $segs += , @($aimEnd, $windUp1, (Br 235 190 90), 'Halt') }
    $segs += , @($windUp1, ($windUp1 + $flug), (Br 240 150 60), "Flug $flug")
    # Ausweichfenster: von Cue+Reaktion bis spaetester Stopp
    $cue = $aimEnd; if ($L -eq 0) { $cue = $windUp1 }
    $wa = $cue + $reaction; $wb = $windUp1 + $w20.latest
    Timeline 70 $y $ppt 110 $segs $null 10
    Rect (70 + $wa * $ppt) ($y + 80) (($wb - $wa) * $ppt) 22 (Br 110 200 230)
    T "Ausweichfenster $($w20.window) Ticks" (70 + $wa * $ppt) ($y + 104) $fS $cyan
    T "Cue" (70 + $cue * $ppt - 14) ($y - 24) $fS $gold
    $ri++
}
T "Annahme: Reaktion $reaction Ticks (0,25 s). Ausholen $windUp1 Ticks ($([math]::Round($windUp1/60.0,2)) s) wie heute." 20 ($rowY + 3 * 150 - 6) $fS $grey

# ---------- Zeile 4: Tabelle ----------
$y4 = $rowY + 3 * 150 + 40
T "4  Querl$($ae)ufer l$($ae)uft weiter: Treffer? Fenster Stopp / Dash" 20 $y4 $fH $white
$colX = 20, 200, 440, 760, 1080, 1400
$hdr = 'Wurf aus', 'HEUTE', 'A Roh', 'B Einrasten', 'C Spur', 'Flugzeit'
for ($c = 0; $c -lt 6; $c++) { T $hdr[$c] $colX[$c] ($y4 + 44) $fB $gold }
$ry = $y4 + 84
foreach ($d in $ranges) {
    T "$d m" $colX[0] $ry $fB $white
    $ci = 1
    foreach ($nm in 'HEUTE', 'A', 'B', 'C') {
        $e = $tab["$nm|$d"]
        if ($nm -eq 'HEUTE') {
            $txt = if ($e.quer) { 'trifft' } else { 'verfehlt' }; $br = $grey
        }
        elseif ($e.quer) { $txt = "trifft, $($e.win) / $($e.windash)"; $br = $green; if ($e.win -lt 5) { $br = $orange } }
        else { $txt = 'verfehlt'; $br = $grey }
        T $txt $colX[$ci] $ry $fB $br
        $ci++
    }
    T "$($tab["A|$d"].flug) Ticks" $colX[5] $ry $fB $white
    $ry += 34
}
T "Weglaufen: heute wie mit Vorhalt Treffer bis etwa 22 m ($hookMax m Flugweg)." 20 ($ry + 6) $fS $grey
T "Fenster = Ticks, die nach Cue + Reaktion bleiben. Unter 5: orange." 20 ($ry + 34) $fS $grey

# ---------- Zeile 5: Dauerverfolgung ----------
$y5 = $ry + 86
T "5  Dauerverfolgung: ab wann bleibt Gorm stehen und wirft?" 20 $y5 $fH $white
$ppt5 = 2.9
$ox5 = 70
$by = $y5 + 74
# A: 300 Ticks
T 'A' 10 ($by + 6) $fT $gold
Timeline $ox5 $by $ppt5 600 @(@(0, 300, (Br 120 120 135), "l$($ae)uft nach, 5 s"), @(300, 330, (Br 235 190 90), 'W'), @(330, 400, (Br 240 150 60), 'Flug')) $null 60
T "W = Ausholen $windUp1 Ticks wie heute, sonst keine Vorwarnung" 70 ($by + 82) $fS $grey
# B: 480 mit Vorwarnung ab 360
$by2 = $by + 120
T 'B' 10 ($by2 + 6) $fT $gold
Timeline $ox5 $by2 $ppt5 600 @(@(0, 360, (Br 120 120 135), "l$($ae)uft nach, 6 s"), @(360, 480, (Br 205 200 185), 'Warnung 120 Ticks'), @(480, 510, (Br 235 190 90), 'W'), @(510, 580, (Br 240 150 60), 'Flug')) $null 60
# C: entfernungsabhaengig
$by3 = $by2 + 120
T 'C' 10 ($by3 + 6) $fT $gold
$c10 = 456; $c20 = 336; $c30 = 240
Rect $ox5 $by3 ($c10 * $ppt5) 22 (Br 120 120 135)
Rect $ox5 ($by3 + 24) ($c20 * $ppt5) 22 (Br 150 150 175)
Rect $ox5 ($by3 + 48) ($c30 * $ppt5) 22 (Br 180 180 205)
T "Spieler 10 m: 456 Ticks" ($ox5 + $c10 * $ppt5 + 8) ($by3 - 2) $fS $white
T "20 m: 336" ($ox5 + $c20 * $ppt5 + 8) ($by3 + 22) $fS $white
T "30 m: 240 Ticks" ($ox5 + $c30 * $ppt5 + 8) ($by3 + 46) $fS $white
for ($t = 0; $t -le 600; $t += 60) { Line ($ox5 + $t * $ppt5) ($by3 + 72) ($ox5 + $t * $ppt5) ($by3 + 84) (Pn 150 150 162 2); T "$t" ($ox5 + $t * $ppt5 - 10) ($by3 + 84) $fS $grey }
T "Schwelle = 480 - 10 Ticks je m ab 8 m, mind. 240" 70 ($by3 + 112) $fS $grey

# ---------- Zeile 6: Haken liegt ----------
$y6 = $by3 + 170
T "6  Haken f$($ae)llt nach Fehlwurf, liegt in der Pause ($pause1 / $pause2 Ticks)" 20 $y6 $fH $white
$lie1 = $pause1 - $fallTicks - $spool; $lie2 = $pause2 - $fallTicks - $spool
$ppt6 = 11.5
$yy = $y6 + 54
T 'Ph. 1' 10 ($yy + 8) $fB $white
Timeline 110 $yy $ppt6 $pause1 @(@(0, $fallTicks, (Br 240 150 60), "f$($ae)llt $fallTicks"), @($fallTicks, ($fallTicks + $lie1), (Br 140 130 120), "liegt $lie1"), @(($fallTicks + $lie1), $pause1, (Br 205 200 185), $null)) $null 10
$yy2 = $yy + 100
T 'Ph. 2' 10 ($yy2 + 8) $fB $white
Timeline 110 $yy2 $ppt6 $pause2 @(@(0, $fallTicks, (Br 240 150 60), "f$($ae)llt $fallTicks"), @($fallTicks, ($fallTicks + $lie2), (Br 140 130 120), "liegt $lie2"), @(($fallTicks + $lie2), $pause2, (Br 205 200 185), $null)) $null 10
T "Danach $spool Ticks: Kette wird eingeholt, Haken verschwindet. Gorm geht erst danach wieder." 20 ($yy2 + 92) $fS $grey

# ---------- Empfehlung ----------
$y7 = $yy2 + 150
Rect 10 $y7 1780 330 (Br 50 46 30)
T "EMPFEHLUNG" 24 ($y7 + 10) $fH $gold
T "Vorhalt: B (Einrasten). Zielpunkt aus Tempo des Ziels und Flugzeit, h$($oe)chstens 6 m voraus." 24 ($y7 + 56) $fB $white
T "12 Ticks vor dem Wurf steht Gorm starr, dumpfes Klacken, Staub am Fu$ss." 24 ($y7 + 86) $fB $white
T "Wer dann stehen bleibt oder dasht, entkommt. Spur (C) nur, wenn B zu schwer lesbar ist." 24 ($y7 + 116) $fB $white
T "Dauerverfolgung: B. Nach 6 s Verfolgung 2 s Warnung (Kette klirrt, Haken schwingt)," 24 ($y7 + 166) $fB $white
T "dann bleibt Gorm stehen und wirft wie gewohnt. A zu grob, C braucht eine Formel mehr." 24 ($y7 + 196) $fB $white
T "Haken f$($ae)llt nach Fehlwurf und liegt bis Pausenende: nur Darstellung, keine Werte." 24 ($y7 + 226) $fB $white
T "Vorl$($ae)ufig: St$($ae)rke und Schwelle brauchen Bot-Kampf und Spielgef$($ue)hl." 24 ($y7 + 276) $fS $grey
$final = New-Object System.Drawing.Bitmap $W, ($y7 + 345)
$g2 = [System.Drawing.Graphics]::FromImage($final)
$g2.DrawImage($bmp, 0, 0)
$g2.Dispose()
$final.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose(); $final.Dispose()
Write-Output "Blatt: $Out"
