# PostToolUse hook for Bash and PowerShell.
# After every merge the lead session runs tools/pr-nachziehen.ps1 (CLAUDE.md).
# This hook reacts to that call and tells the lead session to hand out the
# next issue, within the limits from CLAUDE.md. It never blocks anything.
# Keep this file ASCII-only.

[Console]::InputEncoding = New-Object System.Text.UTF8Encoding $false
$data = [Console]::In.ReadToEnd() | ConvertFrom-Json

$command = [string]$data.tool_input.command
if ($command -notmatch 'pr-nachziehen\.ps1') { exit 0 }

$open = '?'
$list = ''
try {
    # Windows PowerShell 5.1 hands a JSON array on as one object; unroll it.
    $prs = @((gh pr list --state open --json number,title,headRefName 2>$null | ConvertFrom-Json) | ForEach-Object { $_ })
    $open = $prs.Count
    $list = ($prs | ForEach-Object { "#$($_.number) $($_.headRefName)" }) -join ', '
} catch { }

$text = "Merge follow-up (hook nach-merge): open pull requests: $open"
if ($list) { $text += " ($list)" }
$text += ". Now hand out the next issue without asking the human: " +
    "1) update the board; " +
    "2) if two pull requests are open or two code sessions are running, start no further code session, " +
    "but bring the next open pull request through review and test; " +
    "3) otherwise pick the next fitting issue from the board (check issue comments and probe branches first) " +
    "and start a work session for it; designers with probe pieces do not count towards the limit. " +
    "Say in one line which issue you handed out, or why none."

$out = @{
    hookSpecificOutput = @{
        hookEventName     = 'PostToolUse'
        additionalContext = $text
    }
}
$out | ConvertTo-Json -Depth 4 -Compress
exit 0
