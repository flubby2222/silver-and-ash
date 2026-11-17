# Brings open pull requests up to date with main and carries the commit statuses
# "review" and "tester" over to the new commit - but only if the pull request's own
# changes are identical before and after. If anything differs, the statuses are NOT
# copied and a fresh review and test are needed.
# In the list files (CHANGELOG.md, assets/HERKUNFT.md, docs/**/*.md) only the pull
# request's own added and removed lines count, not the neighbouring entries from main.
# A pull request with a merge conflict (DIRTY) is merged here if the conflicts lie only
# in those list files: both sides' entries are kept. That merge is pushed only if the
# pull request's own changes stay identical; otherwise it is left for a person.
#
# Usage (from the repository):
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/pr-nachziehen.ps1            # all open PRs against main
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/pr-nachziehen.ps1 -Number 235
#   ... -DryRun    # only report what would happen
#
# Keep this file ASCII-only: Windows PowerShell 5.1 reads scripts without BOM as ANSI.

param(
    [int]$Number = 0,
    [switch]$DryRun
)

. (Join-Path $PSScriptRoot 'pr-nachziehen.lib.ps1')

$ErrorActionPreference = 'Stop'
$carried = 'review', 'tester'

$repo = (gh repo view --json nameWithOwner | ConvertFrom-Json).nameWithOwner
git fetch origin --quiet

$list = gh pr list --base main --state open --json number,headRefOid,headRefName,isDraft,isCrossRepository,title --limit 100 | ConvertFrom-Json
if ($Number -gt 0) { $list = @($list | Where-Object { $_.number -eq $Number }) }
$list = @($list | Where-Object { -not $_.isDraft })
if ($list.Count -eq 0) { 'No open pull request to update.'; exit 0 }

foreach ($pr in $list) {
    $state = (gh pr view $pr.number --json mergeStateStatus | ConvertFrom-Json).mergeStateStatus
    if ($state -ne 'BEHIND' -and $state -ne 'DIRTY') {
        "#$($pr.number) $($pr.headRefName): $state - nothing to do."
        continue
    }

    $oldHead = $pr.headRefOid
    $oldId = Get-PullRequestPatchId -Base 'origin/main' -Head $oldHead
    $oldStatus = (gh api "repos/$repo/commits/$oldHead/status" | ConvertFrom-Json).statuses

    if ($state -eq 'DIRTY') {
        # Merge conflict: merge here, resolving only the list files (both sides' entries kept).
        if ($pr.isCrossRepository -or $pr.headRefName -eq 'main') {
            "#$($pr.number) $($pr.headRefName): merge conflict, branch is not ours to push - resolve by hand."
            continue
        }
        $mainHead = (git rev-parse origin/main).Trim()
        $merge = New-ListUnionMergeCommit -Base $mainHead -Head $oldHead -Message "Merge origin/main into $($pr.headRefName)`n`nList files merged by tools/pr-nachziehen.ps1 (union: both sides' entries kept)."
        if (-not $merge) {
            "#$($pr.number) $($pr.headRefName): merge conflict outside the list files - resolve by hand, then review and test again."
            continue
        }
        $mergeId = Get-PullRequestPatchId -Base $mainHead -Head $merge
        if (-not $oldId -or $oldId -ne $mergeId) {
            "#$($pr.number) $($pr.headRefName): merge conflict; merging the lists would change the pull request's own changes (e.g. both sides edited the same entry) - not pushed, resolve by hand."
            continue
        }
        if ($DryRun) {
            "#$($pr.number) $($pr.headRefName): DIRTY - conflicts only in list files, would push merge $($merge.Substring(0,7)) and carry the statuses over."
            continue
        }
        git push --quiet origin "$($merge):refs/heads/$($pr.headRefName)"
        if ($LASTEXITCODE -ne 0) {
            "#$($pr.number) $($pr.headRefName): push of the merge failed (branch moved?). Statuses not carried over."
            continue
        }
        $newHead = $merge
        $newId = $mergeId
    }
    else {
        if ($DryRun) {
            $summary = ($carried | ForEach-Object { $c = $_; $s = $oldStatus | Where-Object { $_.context -eq $c } | Select-Object -First 1; "$c=" + $(if ($s) { $s.state } else { 'missing' }) }) -join ', '
            "#$($pr.number) $($pr.headRefName): BEHIND - would update; statuses on $($oldHead.Substring(0,7)): $summary"
            continue
        }

        gh pr update-branch $pr.number | Out-Null

        # Wait until GitHub reports the new head commit.
        $newHead = $oldHead
        for ($i = 0; $i -lt 20 -and $newHead -eq $oldHead; $i++) {
            Start-Sleep -Seconds 2
            $newHead = (gh pr view $pr.number --json headRefOid | ConvertFrom-Json).headRefOid
        }
        if ($newHead -eq $oldHead) {
            "#$($pr.number) $($pr.headRefName): update requested, but no new commit appeared. Statuses not carried over."
            continue
        }

        git fetch origin --quiet
        $newId = Get-PullRequestPatchId -Base 'origin/main' -Head $newHead
    }

    if (-not $oldId -or $oldId -ne $newId) {
        "#$($pr.number) $($pr.headRefName): updated to $($newHead.Substring(0,7)). The pull request's own changes differ after the update - statuses NOT carried over, review and test again."
        continue
    }

    $done = @()
    foreach ($context in $carried) {
        $entry = $oldStatus | Where-Object { $_.context -eq $context } | Select-Object -First 1
        if (-not $entry) { continue }
        $description = "Uebernommen von $($oldHead.Substring(0,7)): Aenderungen unveraendert"
        gh api "repos/$repo/statuses/$newHead" -f state=$($entry.state) -f context=$context -f description=$description | Out-Null
        $done += "$context=$($entry.state)"
    }
    $what = if ($done.Count -gt 0) { $done -join ', ' } else { 'none were set' }
    "#$($pr.number) $($pr.headRefName): updated to $($newHead.Substring(0,7)). Own changes identical - statuses carried over: $what."
}
