# PreToolUse hook for Edit and Write.
# Blocks (exit code 2) edits that would weaken tests or touch the Obsidian settings.
# Keep this file ASCII-only: Windows PowerShell 5.1 reads scripts without BOM as ANSI.

[Console]::InputEncoding = New-Object System.Text.UTF8Encoding $false
$data = [Console]::In.ReadToEnd() | ConvertFrom-Json

function Block([string]$message) {
    [Console]::Error.WriteLine($message)
    exit 2
}

$path = [string]$data.tool_input.file_path
if (-not $path) { exit 0 }
$normalized = $path -replace '\\', '/'

# The vault has no version history; its settings folder is never ours to change.
if ($normalized -match '(?i)/Obsidian/Gamedev/\.obsidian/') {
    Block "Blocked: $path belongs to the Obsidian settings (.obsidian). The vault has no version history. Write notes only, never settings."
}

# Tests must not be removed or skipped to get a green run.
if ($normalized -match '(?i)/tests/.+\.cs$') {
    if ($data.tool_name -eq 'Write') {
        $before = ''
        if (Test-Path -LiteralPath $path) { $before = [IO.File]::ReadAllText($path) }
        $after = [string]$data.tool_input.content
    }
    else {
        $before = [string]$data.tool_input.old_string
        $after = [string]$data.tool_input.new_string
    }

    $testAttribute = '\[(Fact|Theory)\b'
    $testsBefore = [regex]::Matches($before, $testAttribute).Count
    $testsAfter = [regex]::Matches($after, $testAttribute).Count
    if ($testsAfter -lt $testsBefore) {
        Block "Blocked: this edit removes $($testsBefore - $testsAfter) test(s) from $path. Tests are never deleted to get green. If the test itself is wrong, stop and ask the lead; the human decides."
    }

    $skip = '\bSkip\s*='
    if ([regex]::Matches($after, $skip).Count -gt [regex]::Matches($before, $skip).Count) {
        Block "Blocked: this edit adds 'Skip =' to a test in $path. Tests are never skipped to get green. Fix the code or ask the lead."
    }
}

exit 0
