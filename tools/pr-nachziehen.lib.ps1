# The comparison and the list merge used by pr-nachziehen.ps1, in their own file so they
# can be tested (tools/pr-nachziehen.tests.ps1) without touching GitHub.
# Keep this file ASCII-only.

# Markdown files that nearly every pull request appends a line to (changelog, asset
# provenance, lists in the docs). Two open pull requests touch the same spot there, so
# after one is merged the other sees new neighbouring lines or even a merge conflict -
# although its own entry did not change. These files are compared without context lines
# and may be merged with git's built-in "union" driver (see New-ListUnionMergeCommit).
# Patterns use gitattributes / pathspec glob syntax.
$ListFilePatterns = @('CHANGELOG.md', 'assets/HERKUNFT.md', 'docs/**/*.md')

function Get-GitDiffText {
    # Runs 'git diff <arguments>' and returns the patch as Latin-1 text. git writes the
    # diff to a file itself, so no pipe re-encodes it on the way; Latin-1 maps every byte
    # to one character, so nothing is lost. Returns $null on a git error.
    param([string[]]$Arguments)
    $patch = [IO.Path]::GetTempFileName()
    try {
        git diff --no-color --no-ext-diff "--output=$patch" @Arguments 2>$null
        if ($LASTEXITCODE -ne 0) { return $null }
        return [Text.Encoding]::GetEncoding(28591).GetString([IO.File]::ReadAllBytes($patch))
    }
    finally {
        Remove-Item -LiteralPath $patch -Force -ErrorAction SilentlyContinue
    }
}

function ConvertTo-ListChangeLines {
    <#
    Reduces a patch of list files (made with a context so large that each file is one
    hunk) to the lines the pull request adds or removes. Each such line is kept together
    with the Markdown headings it stands under, e.g.
        + [Changelog > [Unveroeffentlicht] > Hinzugefuegt] - **New thing** (#1)
    Context lines themselves are dropped: a neighbour that main added or changed does not
    change the result, but the same entry under another heading (say, in a released
    version instead of "Unveroeffentlicht") does.
    #>
    param([string]$Patch)

    $out = New-Object System.Text.StringBuilder
    $inHeader = $false
    $fence = $false
    $headings = New-Object string[] 7
    foreach ($raw in $Patch -split "`n") {
        $line = $raw.TrimEnd("`r")
        if ($line.StartsWith('diff --git ')) {
            $inHeader = $true; $fence = $false; $headings = New-Object string[] 7
            [void]$out.Append($line).Append("`n")
            continue
        }
        if ($inHeader) {
            if ($line.StartsWith('@@')) { $inHeader = $false; continue }
            if ($line -match '^index [0-9a-f]+\.\.[0-9a-f]+') { continue }   # blob hashes change with every merge of main
            [void]$out.Append($line).Append("`n")                              # ---/+++ names, new/deleted file modes
            continue
        }
        if ($line.Length -eq 0 -or $line.StartsWith('@@')) { continue }
        if ($line.StartsWith('\')) { [void]$out.Append($line).Append("`n"); continue }   # "\ No newline at end of file"

        $mark = $line[0]
        $body = $line.Substring(1)
        if ($mark -ne '-') {
            # Context and added lines make up the file after the change: follow its headings.
            if ($body -match '^ {0,3}(```|~~~)') { $fence = -not $fence }
            elseif (-not $fence -and $body -match '^(#{1,6})\s+(.*?)\s*#*\s*$') {
                $level = $Matches[1].Length
                $headings[$level] = $Matches[2]
                for ($i = $level + 1; $i -lt 7; $i++) { $headings[$i] = $null }
            }
        }
        if ($mark -eq '+' -or $mark -eq '-') {
            $where = ($headings | Where-Object { $_ }) -join ' > '
            [void]$out.Append("$mark [$where] $body").Append("`n")
        }
    }
    return $out.ToString()
}

function Get-PullRequestPatchId {
    <#
    The fingerprint of what a pull request changes: the diff from the point where the
    branch and the base meet (merge base) to the branch head, hashed without the parts
    that change on their own (blob hashes, line numbers of the hunks). So the fingerprint
    stays the same when main moved elsewhere and was merged in - and changes as soon as
    the pull request itself adds, removes or alters a line, or when main changed the
    lines right around the pull request's changes.
    Exception: in the list files ($ListFilePatterns) only the added and removed lines
    count, each with the headings it stands under (ConvertTo-ListChangeLines). New
    neighbouring entries from main leave the fingerprint alone there; a changed word in
    the pull request's own entry, or the entry under another heading, does not.
    Returns an empty string for an empty diff or on any git error; callers treat that
    as "not comparable".
    #>
    param(
        [Parameter(Mandatory)] [string]$Base,
        [Parameter(Mandatory)] [string]$Head,
        [string]$Repository = '.'
    )

    Push-Location -LiteralPath $Repository
    try {
        $mergeBase = (git merge-base $Base $Head 2>$null)
        if (-not $mergeBase) { return '' }

        $headCommit = (git rev-parse --verify --quiet "$Head^{commit}" 2>$null)
        if (-not $headCommit) { return '' }

        $lists = @($ListFilePatterns | ForEach-Object { ":(glob)$_" })
        $notLists = @('.') + @($ListFilePatterns | ForEach-Object { ":(glob,exclude)$_" })

        $other = Get-GitDiffText (@('--full-index', $mergeBase, $headCommit, '--') + $notLists)
        $listPatch = Get-GitDiffText (@('--full-index', '-U1000000', $mergeBase, $headCommit, '--') + $lists)
        if ($null -eq $other -or $null -eq $listPatch) { return '' }
        if ($other.Length -eq 0 -and $listPatch.Length -eq 0) { return '' }

        $kept = New-Object System.Text.StringBuilder
        foreach ($line in $other -split "`n") {
            if ($line -match '^index [0-9a-f]+\.\.[0-9a-f]+') { continue }   # blob hashes change with every merge of main
            if ($line -match '^@@ ') { $line = '@@' }                         # line numbers shift when main adds lines above
            [void]$kept.Append($line.TrimEnd("`r")).Append("`n")
        }
        [void]$kept.Append("=== list files ===`n").Append((ConvertTo-ListChangeLines $listPatch))

        $latin1 = [Text.Encoding]::GetEncoding(28591)
        $sha = [Security.Cryptography.SHA256]::Create()
        return ([BitConverter]::ToString($sha.ComputeHash($latin1.GetBytes($kept.ToString()))) -replace '-', '').ToLower()
    }
    finally {
        Pop-Location
    }
}

function New-ListUnionMergeCommit {
    <#
    Merges $Base into $Head without a working tree (git merge-tree), letting git's
    built-in "union" driver resolve conflicts in the list files: both sides' new lines
    are kept. Returns the new merge commit (parents: $Head, $Base), which exists only
    locally until someone pushes it. Returns an empty string if a conflict remains in
    any other file, or on a git error.
    Union can also keep two versions of a line both sides changed. So a caller must
    compare the fingerprint (Get-PullRequestPatchId) before using the commit.
    #>
    param(
        [Parameter(Mandatory)] [string]$Base,
        [Parameter(Mandatory)] [string]$Head,
        [Parameter(Mandatory)] [string]$Message,
        [string]$Repository = '.'
    )

    Push-Location -LiteralPath $Repository
    $attributes = [IO.Path]::GetTempFileName()
    try {
        $baseCommit = (git rev-parse --verify --quiet "$Base^{commit}" 2>$null)
        $headCommit = (git rev-parse --verify --quiet "$Head^{commit}" 2>$null)
        if (-not $baseCommit -or -not $headCommit) { return '' }

        [IO.File]::WriteAllText($attributes, (($ListFilePatterns | ForEach-Object { "$_ merge=union" }) -join "`n") + "`n")
        $result = @(git -c "core.attributesFile=$attributes" merge-tree --write-tree $headCommit $baseCommit 2>$null)
        if ($LASTEXITCODE -ne 0 -or $result.Count -eq 0) { return '' }   # 1 = conflicts left, other = error
        $tree = $result[0].Trim()

        $commit = (git commit-tree $tree -p $headCommit -p $baseCommit -m $Message 2>$null)
        if ($LASTEXITCODE -ne 0 -or -not $commit) { return '' }
        return $commit.Trim()
    }
    finally {
        Remove-Item -LiteralPath $attributes -Force -ErrorAction SilentlyContinue
        Pop-Location
    }
}
