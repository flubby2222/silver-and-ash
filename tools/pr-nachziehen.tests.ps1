# Tests for the comparison in pr-nachziehen.lib.ps1. They build a throwaway git
# repository in the temp folder; nothing touches this repository or GitHub.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/pr-nachziehen.tests.ps1
#
# Exit code 0 = all passed, 1 = at least one failed. Keep this file ASCII-only.

. (Join-Path $PSScriptRoot 'pr-nachziehen.lib.ps1')

$script:failed = 0
$script:checks = 0
function Check([string]$name, [bool]$condition) {
    $script:checks++
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

# --- Independent of how and from where it is called (review of PR #315) ---------------
# A real code change must never be carried over: not from a sub folder, not with paths
# containing "..", not with the caller's git config or GIT_* variables, not with a dirty
# working tree, attribute tricks, binary files, replaced objects or a file that only has
# a list file's name.

$repo = Join-Path ([IO.Path]::GetTempPath()) ("pr-nachziehen-" + [Guid]::NewGuid().ToString('N'))
foreach ($dir in 'src', 'tools', 'docs', 'assets') { New-Item -ItemType Directory -Path (Join-Path $repo $dir) -Force | Out-Null }

function RG { git -C $repo @args 2>$null | Out-Null }
function RHead([string]$ref) { (git -C $repo rev-parse $ref).Trim() }
function RWrite([string]$file, [string[]]$lines) { [IO.File]::WriteAllText((Join-Path $repo $file), (($lines -join "`n") + "`n")) }
function RBytes([string]$file, [byte[]]$bytes) { [IO.File]::WriteAllBytes((Join-Path $repo $file), $bytes) }
function RRead([string]$file) { @(Get-Content -LiteralPath (Join-Path $repo $file)) }
function RCommit([string]$message) { RG add -A; RG commit --quiet -m $message }
function RBranch([string]$name, [string]$from) { RG checkout --quiet -B $name $from }
function RSet([string]$file, [int]$index, [string]$text) { $l = RRead $file; $l[$index] = $text; RWrite $file $l }
function RId([string]$ref, [string]$where = $repo) { Get-PullRequestPatchId -Base main -Head (RHead $ref) -Repository $where }
function RClean { RG reset --quiet --hard; RG clean -fdxq }
function RMerged { -not (Test-Path -LiteralPath (Join-Path $repo '.git\MERGE_HEAD')) }

try {
    RG init --quiet --initial-branch=main
    RG config user.email test@example.invalid
    RG config user.name Test
    RG config core.autocrlf false
    RWrite 'src/x.cs' (1..40 | ForEach-Object { "line $_" })
    RWrite 'tools/t.ps1' (1..5 | ForEach-Object { "tool $_" })
    RWrite 'src/CHANGELOG.md' (1..10 | ForEach-Object { "- code list $_" })
    RWrite 'CHANGELOG.md' @('# Changelog', '', '## [Unreleased]', '', '### Added', '- Older entry A', '- Older entry B', '', '## [0.1.0]', '', '- First')
    RWrite 'docs/release.md' @('# Release', '', '## Steps', '', '1. build', '2. test', '3. tag', '4. publish', '', '## Notes', '', 'Text.')
    RWrite 'docs/foto-skript.md' @('# Photos', '', '## Scenes', '', '- one', '- two')
    RBytes 'assets/img.png' ([byte[]](137, 80, 78, 71, 0, 1, 2, 0))
    RCommit base

    # The pull request changes src/x.cs line 20 and tools/t.ps1 (as in the review).
    RBranch 'feature' 'main'
    RSet 'src/x.cs' 19 'line 20 changed by the pull request'
    RSet 'tools/t.ps1' 2 'tool changed by the pull request'
    RCommit 'feature'
    $origHead = RHead 'feature'
    $ref = RId 'feature'
    Check 'R0 the pull request has a fingerprint' ($ref.Length -ge 40)
    Check 'R1 same fingerprint from the sub folder tools' ((RId 'feature' (Join-Path $repo 'tools')) -eq $ref)
    Check 'R2 same fingerprint from a path with ".."' ((RId 'feature' (Join-Path $repo 'tools\..\src')) -eq $ref)
    # Baselines taken under the same conditions as the checks below, so that a check
    # "different" cannot pass only because the place of the call already changed it.
    $refTools = RId 'feature' (Join-Path $repo 'tools')
    $refDots = RId 'feature' (Join-Path $repo 'src\..\tools')
    Push-Location -LiteralPath (Join-Path $repo 'tools')
    try { $refCwd = Get-PullRequestPatchId -Base main -Head $origHead } finally { Pop-Location }

    # main changes src/x.cs line 22, right next to the pull request's line 20; merged cleanly.
    RG checkout --quiet main
    RSet 'src/x.cs' 21 'line 22 changed on main'
    RCommit 'main: next to the pull request'
    RG checkout --quiet feature; RG merge --quiet --no-edit main
    Check 'R3 the merge next to the code change went through' (RMerged)
    $mergedHead = RHead 'feature'
    Check 'R4 main changed code close by, from the root -> different' ((RId 'feature') -ne $ref)
    Check 'R5 main changed code close by, -Repository tools -> different (review finding 1)' ((RId 'feature' (Join-Path $repo 'tools')) -ne $refTools)
    Push-Location -LiteralPath (Join-Path $repo 'tools')
    try { $fromCwd = Get-PullRequestPatchId -Base main -Head $mergedHead } finally { Pop-Location }
    Check 'R6 main changed code close by, current folder tools -> different (review finding 1)' ($fromCwd -ne $refCwd)
    Check 'R7 main changed code close by, path with ".." -> different' ((RId 'feature' (Join-Path $repo 'src\..\tools')) -ne $refDots)

    # The pull request rewrites its own code line; checked from the sub folder.
    RBranch 'rewritten' $origHead
    RSet 'src/x.cs' 19 'line 20 changed by the pull request, rewritten'
    RCommit 'rewritten'
    Check 'R8 own code change rewritten, from tools -> different (review finding 1)' ((RId 'rewritten' (Join-Path $repo 'tools')) -ne $refTools)
    Push-Location -LiteralPath (Join-Path $repo 'tools')
    try { $fromCwd = Get-PullRequestPatchId -Base main -Head (RHead 'rewritten') } finally { Pop-Location }
    Check 'R9 own code change rewritten, current folder tools -> different' ($fromCwd -ne $refCwd)

    # The caller's GIT_* environment: no context, relative paths, case-insensitive pathspecs, another repository.
    $env:GIT_DIFF_OPTS = '--unified=0'
    $env:GIT_CONFIG_PARAMETERS = "'diff.context=0' 'diff.relative=true' 'diff.noprefix=true'"
    $env:GIT_CONFIG_COUNT = '1'; $env:GIT_CONFIG_KEY_0 = 'diff.context'; $env:GIT_CONFIG_VALUE_0 = '0'
    $env:GIT_ICASE_PATHSPECS = '1'
    $env:GIT_DIR = Join-Path $repo 'no-such-repository'
    try {
        $envOrig = Get-PullRequestPatchId -Base main -Head $origHead -Repository $repo
        $envOrigTools = Get-PullRequestPatchId -Base main -Head $origHead -Repository (Join-Path $repo 'tools')
        $envMerged = Get-PullRequestPatchId -Base main -Head $mergedHead -Repository (Join-Path $repo 'tools')
    }
    finally {
        foreach ($n in 'GIT_DIFF_OPTS', 'GIT_CONFIG_PARAMETERS', 'GIT_CONFIG_COUNT', 'GIT_CONFIG_KEY_0', 'GIT_CONFIG_VALUE_0', 'GIT_ICASE_PATHSPECS', 'GIT_DIR') {
            Remove-Item -LiteralPath "Env:$n" -ErrorAction SilentlyContinue
        }
    }
    Check 'R10 caller GIT_* variables -> unchanged fingerprint' ($envOrig -eq $ref)
    Check 'R11 caller GIT_* variables -> code change close by still different' ($envMerged -ne $envOrigTools -and $envOrigTools -eq $ref)
    Check 'R12 the caller GIT_* variables are restored' ($null -eq $env:GIT_DIR -and $null -eq $env:GIT_DIFF_OPTS)

    # The repository's own git config: no context, relative, no prefix, other algorithm.
    RG config diff.context 0; RG config diff.relative true; RG config diff.noprefix true
    RG config diff.algorithm histogram; RG config core.quotepath false; RG config diff.renames copies
    RG checkout --quiet feature
    $cfgOrig = Get-PullRequestPatchId -Base main -Head $origHead -Repository (Join-Path $repo 'src')
    $cfgMerged = Get-PullRequestPatchId -Base main -Head $mergedHead -Repository (Join-Path $repo 'src')
    foreach ($k in 'diff.context', 'diff.relative', 'diff.noprefix', 'diff.algorithm', 'core.quotepath', 'diff.renames') { RG config --unset $k }
    Check 'R13 repository git config -> unchanged fingerprint' ($cfgOrig -eq $ref)
    Check 'R14 repository git config -> code change close by still different' ($cfgMerged -ne $cfgOrig)

    # A dirty working tree on another branch: uncommitted code and an uncommitted .gitattributes.
    RG checkout --quiet main
    RSet 'src/x.cs' 19 'uncommitted change'
    RWrite '.gitattributes' @('* -diff')
    $dirtyOrig = Get-PullRequestPatchId -Base main -Head $origHead -Repository (Join-Path $repo 'src')
    $dirtyMerged = Get-PullRequestPatchId -Base main -Head $mergedHead -Repository (Join-Path $repo 'src')
    RClean
    Check 'R15 dirty working tree on main -> unchanged fingerprint' ($dirtyOrig -eq $ref)
    Check 'R16 dirty working tree on main -> code change close by still different' ($dirtyMerged -ne $dirtyOrig)

    # Attribute tricks: a textconv that hides the content, or "-diff" (binary).
    RBranch 'tc1' 'main'; RSet 'src/x.cs' 4 'line 5 version one'; RCommit 'tc1'
    RBranch 'tc2' 'main'; RSet 'src/x.cs' 4 'line 5 version two'; RCommit 'tc2'
    $info = Join-Path $repo '.git\info'
    New-Item -ItemType Directory -Path $info -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $info 'attributes'), "*.cs diff=hide`n")
    RG config diff.hide.textconv "sh -c 'echo hidden'"
    Check 'R17 textconv hiding the content -> two different changes stay different' ((RId 'tc1') -ne (RId 'tc2'))
    [IO.File]::WriteAllText((Join-Path $info 'attributes'), "*.cs -diff`n")
    Check 'R18 "-diff" (binary) attribute -> two different changes stay different' ((RId 'tc1') -ne (RId 'tc2'))
    Remove-Item -LiteralPath (Join-Path $info 'attributes') -Force
    RG config --unset diff.hide.textconv

    # Binary files (review hint 2): different content must give a different fingerprint.
    RBranch 'bin1' 'main'; RBytes 'assets/img.png' ([byte[]](137, 80, 78, 71, 0, 1, 2, 3)); RCommit 'bin1'
    RBranch 'bin2' 'main'; RBytes 'assets/img.png' ([byte[]](137, 80, 78, 71, 0, 1, 2, 4)); RCommit 'bin2'
    Check 'R19 binary file with other content -> different (review hint 2)' ((RId 'bin1') -ne (RId 'bin2'))
    $photos = [Text.Encoding]::ASCII.GetBytes("# Photos`n`n## Scenes`n`n- one`n- two`n")
    RBranch 'nul1' 'main'; RBytes 'docs/foto-skript.md' ($photos + [byte[]](0, 97, 10)); RCommit 'nul1'
    RBranch 'nul2' 'main'; RBytes 'docs/foto-skript.md' ($photos + [byte[]](0, 98, 10)); RCommit 'nul2'
    Check 'R20 list file with a NUL byte (binary) and other content -> different' ((RId 'nul1') -ne (RId 'nul2'))

    # A file that is not exactly a list file is compared strictly (review hints 3 and 4).
    RBranch 'rel' 'main'; RSet 'docs/release.md' 5 '2. test everything'; RCommit 'rel'
    $relRef = RId 'rel'
    RG checkout --quiet main; RSet 'docs/release.md' 7 '4. publish the release'; RCommit 'main: release step'
    RG checkout --quiet rel; RG merge --quiet --no-edit main
    Check 'R21 docs/release.md (not a list) changed close by -> different' ((RMerged) -and (RId 'rel') -ne $relRef)
    $r = RRead 'docs/release.md'
    RBranch 'ro1' 'main'; RWrite 'docs/release.md' (@($r[0..3]) + '0. prepare' + @($r[4..($r.Length - 1)])); RCommit 'ro1'
    RBranch 'ro2' 'main'; $r2 = RRead 'docs/release.md'; $at = [Array]::IndexOf($r2, '## Notes') - 1
    RWrite 'docs/release.md' (@($r2[0..($at - 1)]) + '0. prepare' + @($r2[$at..($r2.Length - 1)])); RCommit 'ro2'
    Check 'R22 docs/release.md: same line at another place in the section -> different' ((RId 'ro1') -ne (RId 'ro2'))
    RBranch 'sc' 'main'; RSet 'src/CHANGELOG.md' 4 '- code list 5 changed'; RCommit 'sc'
    $scRef = RId 'sc'
    RG checkout --quiet main; RSet 'src/CHANGELOG.md' 6 '- code list 7 changed on main'; RCommit 'main: src changelog'
    RG checkout --quiet sc; RG merge --quiet --no-edit main
    Check 'R23 src/CHANGELOG.md (list name in a sub folder) changed close by -> different' ((RMerged) -and (RId 'sc') -ne $scRef)
    RBranch 'sc2' 'main'; RSet 'src/CHANGELOG.md' 2 '- code list 3 by the PR'; RCommit 'sc2'
    RG checkout --quiet main; RSet 'src/CHANGELOG.md' 2 '- code list 3 on main'; RCommit 'main: src changelog conflict'
    Check 'R24 conflict in src/CHANGELOG.md -> no list merge (review hint 4)' ((New-ListUnionMergeCommit -Base main -Head (RHead 'sc2') -Message m -Repository $repo) -eq '')
    Check 'R25 only exact, case-sensitive list paths count as list files' (
        (Test-ListFile 'CHANGELOG.md') -and (Test-ListFile 'docs/foto-skript.md') -and
        -not (Test-ListFile 'changelog.md') -and -not (Test-ListFile 'src/CHANGELOG.md') -and -not (Test-ListFile './CHANGELOG.md') -and
        -not (Test-ListFile 'docs/../CHANGELOG.md') -and -not (Test-ListFile 'docs/Foto-Skript.md') -and -not (Test-ListFile 'docs/release.md') -and
        -not (Test-ListFile 'docs/entscheidungen/0001.md') -and -not (Test-ListFile 'CHANGELOG.md' '120000' '120000') -and -not (Test-ListFile 'CHANGELOG.md' '100644' '160000'))

    # Conflicts in a list and in code at once (review hint 5).
    RBranch 'both' 'main'
    RWrite 'CHANGELOG.md' (@(RRead 'CHANGELOG.md')[0..4] + '- **both** entry' + @(RRead 'CHANGELOG.md')[5..10])
    RSet 'src/x.cs' 29 'line 30 by the PR'; RCommit 'both'
    RG checkout --quiet main
    RWrite 'CHANGELOG.md' (@(RRead 'CHANGELOG.md')[0..4] + '- **main** entry' + @(RRead 'CHANGELOG.md')[5..10])
    RSet 'src/x.cs' 29 'line 30 on main'; RCommit 'main: list and code'
    Check 'R26 conflict in a list file and in code -> no list merge (review hint 5)' ((New-ListUnionMergeCommit -Base main -Head (RHead 'both') -Message m -Repository $repo) -eq '')

    # main deletes the neighbouring entry; union brings it back -> must not pass (review hint 5).
    RBranch 'del' 'main'
    $c = RRead 'CHANGELOG.md'; $at = [Array]::IndexOf($c, '### Added') + 1
    RWrite 'CHANGELOG.md' (@($c[0..($at - 1)]) + '- **del** entry' + @($c[$at..($c.Length - 1)])); RCommit 'del'
    $delRef = RId 'del'
    RG checkout --quiet main
    RWrite 'CHANGELOG.md' @(RRead 'CHANGELOG.md' | Where-Object { $_ -ne '- **main** entry' }); RCommit 'main: deletes the entry next to it'
    $m = New-ListUnionMergeCommit -Base main -Head (RHead 'del') -Message m -Repository $repo
    Check 'R27 main deleted the neighbour, union brings it back -> not carried' ($m -eq '' -or (Get-PullRequestPatchId -Base main -Head $m -Repository $repo) -ne $delRef)

    # A union driver for code in .git/info/attributes must not resolve a code conflict unnoticed.
    RBranch 'k' 'main'; RSet 'src/x.cs' 34 'line 35 by the PR'; RCommit 'k'
    $kRef = RId 'k'
    RG checkout --quiet main; RSet 'src/x.cs' 34 'line 35 on main'; RCommit 'main: line 35'
    [IO.File]::WriteAllText((Join-Path $info 'attributes'), "*.cs merge=union`n")
    $m = New-ListUnionMergeCommit -Base main -Head (RHead 'k') -Message m -Repository $repo
    $mId = if ($m) { Get-PullRequestPatchId -Base main -Head $m -Repository $repo } else { '' }
    Remove-Item -LiteralPath (Join-Path $info 'attributes') -Force
    Check 'R28 union for code in info/attributes -> code conflict not carried' ($m -eq '' -or $mId -ne $kRef)

    # Line endings count in code: the same line with CRLF instead of LF.
    RBranch 'crlf' $origHead
    $x = RRead 'src/x.cs'; $x[19] = $x[19] + "`r"; RWrite 'src/x.cs' $x; RCommit 'crlf'
    Check 'R29 own code line with CRLF instead of LF -> different' ((RId 'crlf') -ne (Get-PullRequestPatchId -Base main -Head $origHead -Repository $repo))

    # Replaced objects (git replace) must not change what is compared.
    $before = Get-PullRequestPatchId -Base main -Head $origHead -Repository $repo
    RG replace $origHead (RHead 'rewritten')
    $replaced = Get-PullRequestPatchId -Base main -Head $origHead -Repository $repo
    RG replace -d $origHead
    Check 'R30 git replace of the head -> fingerprint of the real commit' ($before.Length -ge 40 -and $replaced -eq $before)

    # main is read from the remote-tracking ref, never from a branch or tag named "origin/main".
    RG update-ref refs/remotes/origin/main main
    RG tag 'origin/main' $origHead
    RG branch 'origin/main' $origHead
    Check 'R31 $MainRef is the remote-tracking main, not a tag or branch "origin/main"' ((RHead $MainRef) -eq (RHead 'refs/heads/main'))

    # GitHub's "Update branch" must be a merge on top of the old head.
    Check 'R32 merge commit on top of the old head -> accepted as update' (Test-UpdateMergeOf -OldHead $origHead -NewHead $mergedHead -Repository $repo)
    Check 'R33 a plain commit instead of the update merge -> rejected' (-not (Test-UpdateMergeOf -OldHead $origHead -NewHead (RHead 'rewritten') -Repository $repo))
    Check 'R34 a merge on top of another commit -> rejected' (-not (Test-UpdateMergeOf -OldHead (RHead 'tc1') -NewHead $mergedHead -Repository $repo))

    # The root is found from anywhere inside, and not outside a repository.
    $top = Get-RepositoryRoot $repo
    Check 'R35 the root from a sub folder and a ".." path is the same' ($top -and (Get-RepositoryRoot (Join-Path $repo 'tools\..\src')) -eq $top)
    Check 'R36 outside a repository there is no fingerprint' ((Get-PullRequestPatchId -Base main -Head $origHead -Repository ([IO.Path]::GetTempPath())) -eq '' -and (Get-PullRequestPatchId -Base main -Head $origHead -Repository (Join-Path $repo 'no-such-folder')) -eq '')
}
catch {
    "FAIL  unexpected error: $_"; $script:failed++
}
finally {
    foreach ($n in 'GIT_DIFF_OPTS', 'GIT_CONFIG_PARAMETERS', 'GIT_CONFIG_COUNT', 'GIT_CONFIG_KEY_0', 'GIT_CONFIG_VALUE_0', 'GIT_ICASE_PATHSPECS', 'GIT_DIR') {
        Remove-Item -LiteralPath "Env:$n" -ErrorAction SilentlyContinue
    }
    Remove-Item -LiteralPath $repo -Recurse -Force -ErrorAction SilentlyContinue
}

# A test block that stops early must not look like a pass.
$expected = 60
if ($script:checks -ne $expected) { "FAIL  $($script:checks) checks ran, $expected expected"; $script:failed++ }

""
if ($script:failed -eq 0) { 'all passed'; exit 0 } else { "$script:failed failed"; exit 1 }
