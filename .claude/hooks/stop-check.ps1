# Stop hook: before a session ends its turn, build and test in Release
# if C# code or balancing differs from origin/main and this exact state was not green yet.
# Exit code 2 sends the failure back to Claude so it keeps working.
# Keep this file ASCII-only.

[Console]::InputEncoding = New-Object System.Text.UTF8Encoding $false
$data = [Console]::In.ReadToEnd() | ConvertFrom-Json

# Second stop in a row: do not loop forever. The three-attempts rule in CLAUDE.md takes over.
if ($data.stop_hook_active) { exit 0 }

if ($data.cwd -and (Test-Path -LiteralPath $data.cwd)) { Set-Location -LiteralPath $data.cwd }
$root = git rev-parse --show-toplevel 2>$null
if (-not $root) { exit 0 }
Set-Location -LiteralPath $root
if (-not (Test-Path -LiteralPath 'SilverAndAsh.slnx')) { exit 0 }

$watched = @('*.cs', '*.csproj', '*.props', 'assets/balance.json')
$committed = @(git diff --name-only origin/main...HEAD -- $watched 2>$null)
$uncommitted = @(git status --porcelain -- $watched 2>$null)
if (($committed.Count + $uncommitted.Count) -eq 0) { exit 0 }

# Fingerprint of the current state: the commit plus every uncommitted change to watched files.
$text = (git rev-parse HEAD) + "`n" + ($uncommitted -join "`n") + "`n" + ((git diff HEAD -- $watched 2>$null) -join "`n")
$sha = [Security.Cryptography.SHA256]::Create()
$fingerprint = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))) -replace '-', ''
$marker = git rev-parse --git-path silver-and-ash-last-green
if ((Test-Path -LiteralPath $marker) -and ((Get-Content -LiteralPath $marker -TotalCount 1) -eq $fingerprint)) { exit 0 }

$build = & dotnet build SilverAndAsh.slnx --configuration Release --nologo --verbosity quiet 2>&1
if ($LASTEXITCODE -ne 0) {
    [Console]::Error.WriteLine("Release build failed. Fix it before you stop:`n" + (($build | Select-Object -Last 25) -join "`n"))
    exit 2
}

$test = & dotnet test SilverAndAsh.slnx --configuration Release --no-build --nologo --verbosity quiet 2>&1
if ($LASTEXITCODE -ne 0) {
    [Console]::Error.WriteLine("Tests failed. Fix the code, never the test, before you stop:`n" + (($test | Select-Object -Last 40) -join "`n"))
    exit 2
}

Set-Content -LiteralPath $marker -Value $fingerprint -Encoding ascii
exit 0
