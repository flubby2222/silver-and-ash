# SessionStart hook: tells every session where it is working.
# Lesson from the week 10 retro: runs were started from a stale work folder.
# Whatever this script prints is added to the session's context.
# Keep this file ASCII-only.

$root = git rev-parse --show-toplevel 2>$null
if (-not $root) { exit 0 }

$branch = git -C $root branch --show-current
$commit = git -C $root log -1 --format='%h %s'
$dirty = @(git -C $root status --porcelain).Count
$behind = git -C $root rev-list --count HEAD..origin/main 2>$null
$worktrees = @(git -C $root worktree list)

"Work folder: $root"
"Branch: $branch | Commit: $commit"
"Uncommitted files: $dirty | Commits behind origin/main (as of last fetch): $behind"
if ($worktrees.Count -gt 1) {
    'Work folders of this repository (git worktree):'
    $worktrees | ForEach-Object { "  $_" }
}
exit 0
