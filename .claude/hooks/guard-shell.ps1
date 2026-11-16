# PreToolUse hook for Bash and PowerShell.
# 1. Blocks commands whose effect cannot be undone (also in the 'git -C <dir> ...' form,
#    which the prefix-based deny rules in settings.json do not catch).
# 2. Checks a pull request before it is created (changelog, asset provenance).
# 3. Allows 'gh pr merge' only as auto-merge, only with review and tester status,
#    and never for guardrail pull requests.
# Exit code 2 blocks the command and shows the message to Claude.
# Keep this file ASCII-only.

[Console]::InputEncoding = New-Object System.Text.UTF8Encoding $false
$data = [Console]::In.ReadToEnd() | ConvertFrom-Json

function Block([string]$message) {
    [Console]::Error.WriteLine($message)
    exit 2
}

$command = [string]$data.tool_input.command
if (-not $command) { exit 0 }
if ($data.cwd -and (Test-Path -LiteralPath $data.cwd)) { Set-Location -LiteralPath $data.cwd }

# Text inside quotes (commit messages, PR bodies) is not a command: blank it out first.
$bare = $command -replace '"[^"]*"', '""' -replace "'[^']*'", "''"
$git = '(?i)\bgit\b[^|;&\r\n]*'

# --- 1. Irreversible commands -------------------------------------------------

if ($bare -match ($git + '\bpush\b[^|;&\r\n]*(--force|\s-f\b|\s\+\S)')) {
    Block 'Blocked: force push overwrites history on GitHub and cannot be undone. Push normally; if the branch diverged, merge main into it.'
}
if ($bare -match ($git + '\bpush\b[^|;&\r\n]*[\s:]main(\s|$)')) {
    Block 'Blocked: never push to main directly. Open a pull request.'
}
if ($bare -match ($git + '\breset\b[^|;&\r\n]*--hard')) {
    Block 'Blocked: git reset --hard deletes uncommitted work. Commit or stash first, or restore single files with: git restore <file>.'
}
if ($bare -match ($git + '\bclean\b[^|;&\r\n]*\s-[a-z]*f')) {
    Block 'Blocked: git clean -f deletes untracked files for good. Delete the specific files you mean instead.'
}
if ($bare -match ($git + '\b(checkout\s+--|restore)\s+(--worktree\s+)?\.(\s|$)')) {
    Block 'Blocked: this throws away every uncommitted change in the folder. Restore single files by name instead.'
}
if ($bare -match '(?i)--no-verify\b') {
    Block 'Blocked: --no-verify skips checks. Fix what the check reports.'
}
if ($bare -match ($git + '\bconfig\b[^|;&\r\n]*user\.email')) {
    Block 'Blocked: the commit e-mail stays the noreply address. A different address in a public commit cannot be taken back.'
}
if ($bare -match '(?i)\bgh\s+(repo\s+delete|release\s+delete)\b') {
    Block 'Blocked: deleting the repository or a release is the human''s decision.'
}
if ($bare -match '(?i)\bgh\s+api\b[^|;&\r\n]*(-X|--method)[\s=]*DELETE') {
    Block 'Blocked: DELETE calls against the GitHub API are the human''s decision.'
}
if ($bare -match '(?i)\bgh\s+api\b[^|;&\r\n]*rulesets[^|;&\r\n]*(-X|--method)[\s=]*(PUT|PATCH|POST)' -or
    $bare -match '(?i)\bgh\s+api\b[^|;&\r\n]*(-X|--method)[\s=]*(PUT|PATCH|POST)[^|;&\r\n]*rulesets') {
    Block 'Blocked: the branch protection of main is changed by the human only.'
}

# --- 2. Before a pull request is created --------------------------------------

if ($bare -match '(?i)\bgh\s+pr\s+create\b') {
    $files = @(git diff --name-only origin/main...HEAD 2>$null)

    $title = ''
    if ($command -match '(?i)--title[\s=]+["'']([^"'']+)') { $title = $Matches[1] }
    $invisibleKind = $title -match '^(refactor|test|chore|ci|docs|build)(\(.+\))?:'
    $touchesGame = $files | Where-Object { $_ -match '^(src/|assets/)' }
    if ($touchesGame -and -not $invisibleKind -and ($files -notcontains 'CHANGELOG.md')) {
        Block 'Blocked: this branch changes src/ or assets/ but not CHANGELOG.md. Add the visible change under "Unveroeffentlicht" (feat/fix), or use a refactor:/test:/chore: title if players cannot see it.'
    }

    $assetPattern = '^assets/.+\.(wav|ogg|mp3|flac|png|jpg|jpeg|bmp|obj|glb|gltf|fbx|blend|ttf|otf)$'
    $newAssets = @($files | Where-Object { $_ -match $assetPattern -and (Test-Path -LiteralPath $_) })
    if ($newAssets.Count -gt 0) {
        $provenance = ''
        if (Test-Path -LiteralPath 'assets/HERKUNFT.md') { $provenance = [IO.File]::ReadAllText((Resolve-Path 'assets/HERKUNFT.md')) }
        $missing = @($newAssets | Where-Object { $provenance -notmatch [regex]::Escape(($_ -replace '^assets/', '')) })
        if ($missing.Count -gt 0) {
            Block ("Blocked: these asset files have no entry in assets/HERKUNFT.md (file, generating script, date):`n  " + ($missing -join "`n  "))
        }
    }
}

# --- 3. Merging ---------------------------------------------------------------

if ($bare -match '(?i)\bgh\s+pr\s+merge\b') {
    if ($bare -notmatch '(?i)--auto\b') {
        Block 'Blocked: Claude never merges directly. Use: gh pr merge <nr> --auto --merge. GitHub then merges once CI, review and tester are green.'
    }
    try {
        $number = ''
        if ($bare -match '(?i)\bgh\s+pr\s+merge\s+(\d+)') { $number = $Matches[1] }
        $viewArgs = @('pr', 'view')
        if ($number) { $viewArgs += $number }
        $viewArgs += @('--json', 'number,headRefOid,files')
        $pr = (& gh @viewArgs) | ConvertFrom-Json
        if (-not $pr.headRefOid) { throw 'pull request not found' }

        $guardrail = @($pr.files | ForEach-Object { $_.path } | Where-Object { $_ -match '^(CLAUDE\.md$|\.claude/|\.github/|docs/entscheidungen/)' })
        if ($guardrail.Count -gt 0) {
            Block ("Blocked: pull request #$($pr.number) changes guardrail files. Only the human merges it:`n  " + ($guardrail -join "`n  "))
        }

        $repo = (& gh repo view --json nameWithOwner | ConvertFrom-Json).nameWithOwner
        $status = (& gh api "repos/$repo/commits/$($pr.headRefOid)/status") | ConvertFrom-Json
        foreach ($context in 'review', 'tester') {
            $entry = $status.statuses | Where-Object { $_.context -eq $context } | Select-Object -First 1
            if (-not $entry -or $entry.state -ne 'success') {
                $state = if ($entry) { $entry.state } else { 'missing' }
                Block "Blocked: commit status '$context' on the latest commit of #$($pr.number) is '$state'. A fresh $context session must approve first. The author never sets this status."
            }
        }
    }
    catch {
        Block "Blocked: could not verify the pull request before merging ($($_.Exception.Message)). Nothing was merged."
    }
}

exit 0
