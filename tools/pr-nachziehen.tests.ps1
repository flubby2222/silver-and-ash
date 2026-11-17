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

""
if ($script:failed -eq 0) { 'all passed'; exit 0 } else { "$script:failed failed"; exit 1 }
