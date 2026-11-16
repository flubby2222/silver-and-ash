# PostToolUse hook for Edit and Write.
# SilverAndAsh.Core must not reference Raylib (architecture rule in CLAUDE.md).
# Exit code 2 sends the message back to Claude so it fixes the file.
# Keep this file ASCII-only.

[Console]::InputEncoding = New-Object System.Text.UTF8Encoding $false
$data = [Console]::In.ReadToEnd() | ConvertFrom-Json

$path = [string]$data.tool_input.file_path
if (-not $path) { exit 0 }
$normalized = $path -replace '\\', '/'

if ($normalized -notmatch '(?i)/src/SilverAndAsh\.Core/.+\.(cs|csproj)$') { exit 0 }
if (-not (Test-Path -LiteralPath $path)) { exit 0 }

$hits = Select-String -LiteralPath $path -Pattern '^\s*(global\s+)?using\s+(static\s+)?Raylib', 'Raylib_cs\.', 'Include="Raylib' -AllMatches
if ($hits) {
    $lines = ($hits | ForEach-Object { "  line $($_.LineNumber): $($_.Line.Trim())" }) -join "`n"
    [Console]::Error.WriteLine("Architecture rule broken: $path is in SilverAndAsh.Core and references Raylib.`n$lines`nCore holds game logic only and uses System.Numerics. Move the Raylib part to SilverAndAsh.Game.")
    exit 2
}

exit 0
