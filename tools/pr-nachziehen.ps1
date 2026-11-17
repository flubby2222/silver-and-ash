# Brings open pull requests up to date with main and carries the commit statuses
# "review" and "tester" over to the new commit - but only if the pull request's own
# changes are identical before and after. If anything differs, the statuses are NOT
# copied and a fresh review and test are needed.
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

$list = gh pr list --base main --state open --json number,headRefOid,headRefName,isDraft,title --limit 100 | ConvertFrom-Json
if ($Number -gt 0) { $list = @($list | Where-Object { $_.number -eq $Number }) }
$list = @($list | Where-Object { -not $_.isDraft })
if ($list.Count -eq 0) { 'No open pull request to update.'; exit 0 }

foreach ($pr in $list) {
    $state = (gh pr view $pr.number --json mergeStateStatus | ConvertFrom-Json).mergeStateStatus
    if ($state -ne 'BEHIND') {
        "#$($pr.number) $($pr.headRefName): $state - nothing to do."
        continue
    }

    $oldHead = $pr.headRefOid
    $oldId = Get-PullRequestPatchId -Base 'origin/main' -Head $oldHead
    $oldStatus = (gh api "repos/$repo/commits/$oldHead/status" | ConvertFrom-Json).statuses

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
