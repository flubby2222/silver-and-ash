<#
.SYNOPSIS
    Puts the numbered pictures of a motion scene (--clip, issue #286) together into a clip with ffmpeg.

.DESCRIPTION
    The game writes frame-0000.png, frame-0001.png ... into one folder per motion scene. This script makes an MP4
    (or a GIF) out of them, next to the folder: <folder>.mp4 or <folder>.gif, "-half" with -Half.
    The pictures and clips stay outside the repository - never commit them. See docs/foto-skript.md.

.PARAMETER Folder
    The folder of one motion scene, e.g. "$env:LOCALAPPDATA\SilverAndAsh\clips\blade-charge".

.PARAMETER Fps
    Pictures per second at real speed. Default 30: the game films every second tick of 60 (--clip list shows it).

.PARAMETER Half
    Plays at half speed (the frame rate halved) - for fast movements.

.PARAMETER Gif
    A GIF instead of an MP4 (larger, but opens everywhere, also in a browser or chat).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\clip.ps1 "$env:LOCALAPPDATA\SilverAndAsh\clips\blade-charge" -Half
#>
param(
    [Parameter(Mandatory = $true)][string]$Folder,
    [double]$Fps = 30,
    [switch]$Half,
    [switch]$Gif
)

$ErrorActionPreference = 'Stop'

$ffmpeg = Get-Command ffmpeg -ErrorAction SilentlyContinue
if ($null -eq $ffmpeg) {
    Write-Host 'ffmpeg was not found. Install it (e.g. "winget install Gyan.FFmpeg") or put ffmpeg.exe into the PATH, then open a new console.' -ForegroundColor Red
    exit 2
}

if (-not (Test-Path -LiteralPath $Folder -PathType Container)) {
    Write-Host "Folder not found: $Folder - film the scene first: dotnet run --project src/SilverAndAsh.Game -- --clip <scene>" -ForegroundColor Red
    exit 1
}

$Folder = (Resolve-Path -LiteralPath $Folder).Path.TrimEnd('\')
$frames = @(Get-ChildItem -LiteralPath $Folder -Filter 'frame-*.png')
if ($frames.Count -lt 2) {
    Write-Host "Fewer than two pictures (frame-0000.png ...) in $Folder." -ForegroundColor Red
    exit 1
}

if ($Fps -le 0) {
    Write-Host "-Fps must be greater than 0." -ForegroundColor Red
    exit 1
}

$rate = if ($Half) { $Fps / 2 } else { $Fps }
$rateText = $rate.ToString([System.Globalization.CultureInfo]::InvariantCulture)
$suffix = if ($Half) { '-half' } else { '' }
$extension = if ($Gif) { 'gif' } else { 'mp4' }
$out = "$Folder$suffix.$extension"
$pattern = Join-Path $Folder 'frame-%04d.png'

if ($Gif) {
    # A palette made from the pictures themselves: far fewer stripes than the standard 256 colours.
    $filter = 'scale=640:-1:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse'
    & $ffmpeg.Source -hide_banner -loglevel error -y -framerate $rateText -i $pattern -filter_complex $filter -loop 0 $out
}
else {
    # yuv420p and an even size: plays in every player, also in a browser.
    & $ffmpeg.Source -hide_banner -loglevel error -y -framerate $rateText -i $pattern -c:v libx264 -crf 18 -pix_fmt yuv420p -vf 'scale=trunc(iw/2)*2:trunc(ih/2)*2' $out
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "ffmpeg failed (exit code $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}

$seconds = [math]::Round($frames.Count / $rate, 2)
Write-Host "$out ($($frames.Count) pictures, $rateText fps, $seconds s)"
