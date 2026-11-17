# Tests for the comparison in pr-nachziehen.lib.ps1. They build a throwaway git
# repository in the temp folder; nothing touches this repository or GitHub.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/pr-nachziehen.tests.ps1
#
# Exit code 0 = all passed, 1 = at least one failed. Keep this file ASCII-only.

. (Join-Path $PSScriptRoot 'pr-nachziehen.lib.ps1')

$script:failed = 0
function Check([string]$name, [bool]$condition) {
    if ($condition) { "ok    $name" } else { "FAIL  $name"; $script:failed++ }
}

$root = Join-Path ([IO.Path]::GetTempPath()) ("pr-nachziehen-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null

function G { git -C $root @args 2>$null | Out-Null }
function Write-Lines([string]$file, [string[]]$lines) {
    [IO.File]::WriteAllText((Join-Path $root $file), (($lines -join "`n") + "`n"))
}
function Head([string]$ref) { (git -C $root rev-parse $ref).Trim() }
# True if the last merge finished: no merge in progress and main is contained in the branch.
function Merged {
    $inProgress = Test-Path -LiteralPath (Join-Path $root '.git\MERGE_HEAD')
    git -C $root merge-base --is-ancestor main feature 2>$null
    return (-not $inProgress) -and ($LASTEXITCODE -eq 0)
}

try {
    G init --quiet --initial-branch=main
    G config user.email test@example.invalid
    G config user.name Test
    G config core.autocrlf false

    $code = 1..40 | ForEach-Object { "line $_" }
    Write-Lines 'code.txt' $code
    Write-Lines 'other.txt' @('other')
    G add -A; G commit --quiet -m base

    # The pull request changes line 20 of code.txt.
    G checkout --quiet -b feature
    $changed = $code.Clone(); $changed[19] = 'line 20 changed by the pull request'
    Write-Lines 'code.txt' $changed
    G add -A; G commit --quiet -m feature
    $before = Get-PullRequestPatchId -Base main -Head (Head feature) -Repository $root
    Check 'a pull request with changes has a fingerprint' ($before.Length -ge 40)

    # 1. main moves in another file; main is merged into the branch.
    G checkout --quiet main
    Write-Lines 'other.txt' @('other', 'more')
    G add -A; G commit --quiet -m 'main: other file'
    G checkout --quiet feature; G merge --quiet --no-edit main
    $afterOtherFile = Get-PullRequestPatchId -Base main -Head (Head feature) -Repository $root
    Check 'main changed another file -> same fingerprint' ((Merged) -and $afterOtherFile -eq $before)

    # 2. main moves in the same file, far away from the pull request's change.
    G checkout --quiet main
    $far = (Get-Content (Join-Path $root 'code.txt')); $far[1] = 'line 2 changed on main'
    Write-Lines 'code.txt' $far
    G add -A; G commit --quiet -m 'main: same file, far away'
    G checkout --quiet feature; G merge --quiet --no-edit main
    $afterFarEdit = Get-PullRequestPatchId -Base main -Head (Head feature) -Repository $root
    Check 'main changed the same file far away -> same fingerprint' ((Merged) -and $afterFarEdit -eq $before)

    # 3. main inserts lines above, so the pull request's change moves down.
    G checkout --quiet main
    $shifted = @('new first line', 'new second line') + (Get-Content (Join-Path $root 'code.txt'))
    Write-Lines 'code.txt' $shifted
    G add -A; G commit --quiet -m 'main: lines inserted above'
    G checkout --quiet feature; G merge --quiet --no-edit main
    $afterShift = Get-PullRequestPatchId -Base main -Head (Head feature) -Repository $root
    Check 'main shifted the line numbers -> same fingerprint' ((Merged) -and $afterShift -eq $before)

    # 4. main changes a line close to the pull request's change (inside the shown context,
    #    one untouched line in between so that the merge itself still succeeds).
    G checkout --quiet main
    $near = (Get-Content (Join-Path $root 'code.txt'))
    $index = [Array]::IndexOf($near, 'line 22'); $near[$index] = 'line 22 changed on main'
    Write-Lines 'code.txt' $near
    G add -A; G commit --quiet -m 'main: neighbouring line'
    G checkout --quiet feature; G merge --quiet --no-edit main
    $afterNeighbour = Get-PullRequestPatchId -Base main -Head (Head feature) -Repository $root
    Check 'the merge with the nearby change went through' (Merged)
    Check 'main changed a line close by -> different fingerprint' ($afterNeighbour -and $afterNeighbour -ne $before)

    # 5. The pull request itself gets one more change.
    $again = (Get-Content (Join-Path $root 'code.txt')); $again[$again.Length - 1] = 'last line changed by the pull request'
    Write-Lines 'code.txt' $again
    G add -A; G commit --quiet -m 'feature: second change'
    $afterOwnChange = Get-PullRequestPatchId -Base main -Head (Head feature) -Repository $root
    Check 'the pull request changed one more line -> different fingerprint' ($afterOwnChange -ne $afterNeighbour)

    # 6. A sneaked-in change inside the merge commit itself counts as the pull request's change.
    G checkout --quiet main
    Write-Lines 'other.txt' @('other', 'more', 'even more')
    G add -A; G commit --quiet -m 'main: other file again'
    G checkout --quiet feature
    $beforeEvilMerge = Get-PullRequestPatchId -Base main -Head (Head feature) -Repository $root
    G merge --quiet --no-commit --no-ff main
    Write-Lines 'extra.txt' @('added during the merge')
    G add -A; G commit --quiet -m 'merge main (with an extra file)'
    $afterEvilMerge = Get-PullRequestPatchId -Base main -Head (Head feature) -Repository $root
    Check 'a file added inside the merge commit -> different fingerprint' ($afterEvilMerge -ne $beforeEvilMerge)

    # 7. No changes at all, and an unknown commit.
    G checkout --quiet main
    Check 'a branch without changes has no fingerprint' ((Get-PullRequestPatchId -Base main -Head (Head main) -Repository $root) -eq '')
    Check 'an unknown commit has no fingerprint' ((Get-PullRequestPatchId -Base main -Head ('0' * 40) -Repository $root) -eq '')
}
finally {
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}

# --- List files (CHANGELOG.md, assets/HERKUNFT.md, docs/**/*.md) ----------------------
# Two pull requests open at the same time each add an entry at the same spots. After the
# first is merged, the second must keep its fingerprint - and must lose it as soon as its
# own entry changes.

$root = Join-Path ([IO.Path]::GetTempPath()) ("pr-nachziehen-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $root 'assets') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $root 'docs') -Force | Out-Null

$changelog = @(
    '# Changelog', '', '## [Unreleased]', '', '### Added',
    '- Older entry A', '- Older entry B', '', '### Changed', '- Older change',
    '', '## [0.1.0] - 2026-01-01', '', '### Added', '- First release')
$provenance = @('# Provenance', '', '- `a.png`: made by a.py, 2026-01-01', '', '## Rebuild', '', '```', '# not a heading', '```')
$photoList = @('# Photos', '', '## Scenes', '', '- `one`', '- `two`', '', '## Notes', '', 'Text.')

# Adds $entry right below the "### Added" heading of "Unreleased", like every PR does.
function Add-Entry([string[]]$lines, [string]$entry) {
    $at = [Array]::IndexOf($lines, '### Added') + 1
    return @($lines[0..($at - 1)]) + $entry + @($lines[$at..($lines.Length - 1)])
}
function Read-Lines([string]$file) { @(Get-Content -LiteralPath (Join-Path $root $file)) }
function Commit-All([string]$message) { G add -A; G commit --quiet -m $message }
# A pull request in the usual shape: one changelog line, one provenance line, one list line, one code file.
function New-PullRequestBranch([string]$name) {
    G checkout --quiet main
    G checkout --quiet -b $name
    Write-Lines 'CHANGELOG.md' (Add-Entry (Read-Lines 'CHANGELOG.md') "- **$name** new thing (#1)")
    $p = Read-Lines 'assets/HERKUNFT.md'; $at = [Array]::IndexOf($p, '') + 0
    Write-Lines 'assets/HERKUNFT.md' (@($p[0..($at - 1)]) + "- ``$name.png``: made by $name.py, 2026-01-02" + @($p[$at..($p.Length - 1)]))
    $d = Read-Lines 'docs/foto-skript.md'; $at = [Array]::IndexOf($d, '## Notes') - 1
    Write-Lines 'docs/foto-skript.md' (@($d[0..($at - 1)]) + "- ``$name``" + @($d[$at..($d.Length - 1)]))
    Write-Lines "$name.txt" @("code of $name")
    Commit-All $name
}
function Merge-IntoMain([string]$name) {
    G checkout --quiet main
    G merge --quiet --no-ff --no-edit $name
}
function Plain-Merge-Conflicts([string]$name) {
    git -C $root merge-tree --write-tree (Head $name) (Head main) 2>$null | Out-Null
    return $LASTEXITCODE -ne 0
}
function Id([string]$ref) { Get-PullRequestPatchId -Base main -Head (Head $ref) -Repository $root }

try {
    G init --quiet --initial-branch=main
    G config user.email test@example.invalid
    G config user.name Test
    G config core.autocrlf false
    Write-Lines 'CHANGELOG.md' $changelog
    Write-Lines 'assets/HERKUNFT.md' $provenance
    Write-Lines 'docs/foto-skript.md' $photoList
    Write-Lines 'code.txt' (1..40 | ForEach-Object { "line $_" })
    Commit-All base

    # 8. Two PRs, the first one merged: the plain merge conflicts, the list merge does not,
    #    and the second PR keeps its fingerprint.
    New-PullRequestBranch 'first'
    New-PullRequestBranch 'second'
    $second = Id 'second'
    Merge-IntoMain 'first'
    Check 'two entries at the same spot conflict in a plain merge' (Plain-Merge-Conflicts 'second')
    $merge = New-ListUnionMergeCommit -Base main -Head (Head 'second') -Message 'merge main' -Repository $root
    Check 'the list merge resolves conflicts that lie only in list files' ($merge.Length -ge 40)
    if (-not $merge) { $merge = Head 'second' }   # keeps the remaining checks running (and failing)
    $merged = (git -C $root show "$($merge):CHANGELOG.md") -join "`n"
    Check 'the list merge keeps both entries' ($merged -match '\*\*first\*\*' -and $merged -match '\*\*second\*\*')
    $afterList = Get-PullRequestPatchId -Base main -Head $merge -Repository $root
    Check 'second PR after the list merge -> same fingerprint' ($afterList -eq $second)

    # 9. main rewords an entry two lines below the PR's entry: the merge is clean, only the
    #    context around the PR's entry changes (as in PR #312).
    G checkout --quiet 'second'; G merge --quiet --no-edit $merge
    $before = Id 'second'
    G checkout --quiet main
    $c = Read-Lines 'CHANGELOG.md'; $c[[Array]::IndexOf($c, '- Older entry A')] = '- Older entry A, reworded on main'
    Write-Lines 'CHANGELOG.md' $c
    Commit-All 'main: nearby changelog entry reworded'
    G checkout --quiet 'second'; G merge --quiet --no-edit main
    $clean = -not (Test-Path -LiteralPath (Join-Path $root '.git\MERGE_HEAD'))
    git -C $root merge-base --is-ancestor main 'second' 2>$null
    Check 'the merge with the nearby changelog change went through' ($clean -and $LASTEXITCODE -eq 0)
    Check 'main reworded a nearby changelog entry -> same fingerprint' ((Id 'second') -eq $before)

    # 10. The PR's own entry changes by one word during the update -> must not count as unchanged.
    $own = Read-Lines 'CHANGELOG.md'; $own[[Array]::IndexOf($own, '- **second** new thing (#1)')] = '- **second** new things (#1)'
    Write-Lines 'CHANGELOG.md' $own
    Commit-All 'second: own entry reworded'
    Check 'own changelog entry reworded -> different fingerprint' ((Id 'second') -ne $before)
    $own[[Array]::IndexOf($own, '- **second** new things (#1)')] = '- **second** new thing (#1)'
    Write-Lines 'CHANGELOG.md' $own
    Commit-All 'second: wording back'
    Check 'own entry back to the old wording -> same fingerprint again' ((Id 'second') -eq $before)

    # 11. The same entry under another heading (in the released version instead of Unreleased).
    $moved = @(Read-Lines 'CHANGELOG.md' | Where-Object { $_ -ne '- **second** new thing (#1)' })
    $at = [Array]::IndexOf($moved, '- First release')
    Write-Lines 'CHANGELOG.md' (@($moved[0..$at]) + '- **second** new thing (#1)')
    Commit-All 'second: entry moved to 0.1.0'
    Check 'own entry moved under another heading -> different fingerprint' ((Id 'second') -ne $before)
    Write-Lines 'CHANGELOG.md' (Add-Entry $moved '- **second** new thing (#1)')
    Commit-All 'second: entry back under Unreleased'

    # 12. A foreign line slipped in twice while resolving a conflict (as in PR #311).
    $before = Id 'second'
    $twice = Add-Entry (Read-Lines 'CHANGELOG.md') '- Older entry A'
    Write-Lines 'CHANGELOG.md' $twice
    Commit-All 'second: foreign line doubled'
    Check 'a foreign entry doubled by the PR -> different fingerprint' ((Id 'second') -ne $before)

    # 13. Both sides change the same existing entry differently: union keeps both versions,
    #     so the merge changes the PR's own changes and must not pass.
    New-PullRequestBranch 'third'
    $t = Read-Lines 'CHANGELOG.md'; $t[[Array]::IndexOf($t, '- Older change')] = '- Older change, fixed by third'
    Write-Lines 'CHANGELOG.md' $t
    Commit-All 'third: edits an old entry'
    $third = Id 'third'
    G checkout --quiet main
    $m = Read-Lines 'CHANGELOG.md'; $m[[Array]::IndexOf($m, '- Older change')] = '- Older change, fixed on main'
    Write-Lines 'CHANGELOG.md' $m
    Commit-All 'main: edits the same old entry'
    $merge = New-ListUnionMergeCommit -Base main -Head (Head 'third') -Message 'merge main' -Repository $root
    $afterUnion = if ($merge) { Get-PullRequestPatchId -Base main -Head $merge -Repository $root } else { '' }
    Check 'both sides edited the same entry -> union merge gets a different fingerprint' ($afterUnion -ne $third)

    # 14. A conflict in a code file is never resolved by the list merge.
    New-PullRequestBranch 'fourth'
    $f = Read-Lines 'code.txt'; $f[19] = 'line 20 changed by fourth'
    Write-Lines 'code.txt' $f
    Commit-All 'fourth: code'
    G checkout --quiet main
    $f[19] = 'line 20 changed on main'
    Write-Lines 'code.txt' $f
    Commit-All 'main: same code line'
    $merge = New-ListUnionMergeCommit -Base main -Head (Head 'fourth') -Message 'merge main' -Repository $root
    Check 'a conflict in a code file -> no list merge' ($merge -eq '')

    # 15. A heading-like line inside a code block of a list file does not count as a heading.
    G checkout --quiet main
    G checkout --quiet -b 'fifth'
    $p = Read-Lines 'assets/HERKUNFT.md'
    Write-Lines 'assets/HERKUNFT.md' ($p + '- `late.png`: made by late.py, 2026-01-03')
    Commit-All 'fifth: provenance after the code block'
    Push-Location -LiteralPath $root
    try { $patch = Get-GitDiffText -Arguments @('-U1000000', (Head main), (Head 'fifth'), '--', 'assets/HERKUNFT.md') }
    finally { Pop-Location }
    Check 'a "#" line inside a code block is not a heading' ((ConvertTo-ListChangeLines $patch) -match '\+ \[Provenance > Rebuild\] - `late.png`')
}
finally {
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}

""
if ($script:failed -eq 0) { 'all passed'; exit 0 } else { "$script:failed failed"; exit 1 }
