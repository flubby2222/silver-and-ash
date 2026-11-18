# The comparison and the list merge used by pr-nachziehen.ps1, in their own file so they
# can be tested (tools/pr-nachziehen.tests.ps1) without touching GitHub.
# Keep this file ASCII-only.
#
# Safety rule: nothing here may depend on how or from where it is called. Every git call
# runs at the root of the repository with a cleaned environment and pinned options
# (Invoke-CleanGit); paths are compared exactly and case-sensitively; anything unclear
# returns '' ("not comparable"), and the caller then carries nothing over.

# Files that nearly every pull request appends a line to (changelog, asset provenance,
# a few lists in the docs). Two open pull requests touch the same spot there, so after
# one is merged the other sees new neighbouring lines or even a merge conflict - although
# its own entry did not change. Only these exact paths (relative to the repository root,
# case-sensitive) are compared without context lines and may be merged with git's
# built-in "union" driver (see New-ListUnionMergeCommit). Everything else is strict.
$ListFiles = @(
    'CHANGELOG.md',
    'assets/HERKUNFT.md',
    'docs/foto-skript.md',
    'docs/modelle.md',
    'docs/musterraum.md'
)

# The commit main is compared against. A full ref name, so that a local branch or tag
# called "origin/main" cannot take its place.
$MainRef = 'refs/remotes/origin/main'

# Environment variables through which a caller could change what git reads or prints
# (another repository, extra config, pathspec rules, external diff, replaced objects).
$GitEnvironmentToClear = @(
    'GIT_DIR', 'GIT_WORK_TREE', 'GIT_INDEX_FILE', 'GIT_COMMON_DIR', 'GIT_NAMESPACE',
    'GIT_OBJECT_DIRECTORY', 'GIT_ALTERNATE_OBJECT_DIRECTORIES',
    'GIT_CONFIG', 'GIT_CONFIG_PARAMETERS', 'GIT_CONFIG_COUNT', 'GIT_CONFIG_GLOBAL', 'GIT_CONFIG_SYSTEM',
    'GIT_DIFF_OPTS', 'GIT_EXTERNAL_DIFF', 'GIT_DIFF_PATH_COUNTER', 'GIT_DIFF_PATH_TOTAL',
    'GIT_ICASE_PATHSPECS', 'GIT_GLOB_PATHSPECS', 'GIT_NOGLOB_PATHSPECS', 'GIT_LITERAL_PATHSPECS',
    'GIT_ATTR_SOURCE', 'GIT_ATTR_NOSYSTEM', 'GIT_REPLACE_REF_BASE', 'GIT_NO_REPLACE_OBJECTS',
    'GIT_GRAFT_FILE', 'GIT_SHALLOW_FILE'
)

# Options that pin every setting a caller's git config could use to change a diff or a
# merge (context lines, relative paths, prefixes, quoting, case, renormalizing).
$PinnedGitConfig = @(
    '-c', 'diff.relative=false', '-c', 'diff.noprefix=false', '-c', 'diff.mnemonicPrefix=false',
    '-c', 'diff.renames=false', '-c', 'core.quotePath=true',
    '-c', 'core.ignoreCase=false', '-c', 'merge.renormalize=false'
)

function Invoke-CleanGit {
    # Runs git with the caller's GIT_* environment removed (restored afterwards), without
    # replaced objects and with $PinnedGitConfig. $LASTEXITCODE is git's exit code.
    param([string[]]$Arguments)
    $ErrorActionPreference = 'Continue'   # git's messages on stderr are not errors here
    $saved = @{}
    foreach ($name in $GitEnvironmentToClear) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if ($null -ne $value) { $saved[$name] = $value; [Environment]::SetEnvironmentVariable($name, $null) }
    }
    foreach ($key in @([Environment]::GetEnvironmentVariables().Keys)) {
        if ($key -match '^GIT_CONFIG_(KEY|VALUE)_\d+$') {
            $saved[$key] = [Environment]::GetEnvironmentVariable($key); [Environment]::SetEnvironmentVariable($key, $null)
        }
    }
    try {
        git --no-replace-objects @PinnedGitConfig @Arguments 2>$null
    }
    finally {
        foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
    }
}

function Get-RepositoryRoot {
    # The top folder of the repository that contains $Path, or '' if there is none.
    param([string]$Path = '.')
    $full = (Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue)
    if (-not $full) { return '' }
    Push-Location -LiteralPath $full.ProviderPath
    try {
        $top = Invoke-CleanGit @('rev-parse', '--show-toplevel')
        if ($LASTEXITCODE -ne 0 -or -not $top) { return '' }
        return ([string]$top).Trim()
    }
    finally { Pop-Location }
}

function Get-GitDiffText {
    # Runs 'git diff <arguments>' with pinned output options and returns the patch as
    # Latin-1 text. git writes the diff to a file itself, so no pipe re-encodes it on the
    # way; Latin-1 maps every byte to one character, so nothing is lost (line endings
    # included). --text compares every file as text, so binary files and attributes such
    # as "-diff" (even in an uncommitted .gitattributes) cannot hide the content;
    # --no-textconv shows the real bytes instead of a converted view. Returns $null on a
    # git error.
    param([string[]]$Arguments)
    $patch = [IO.Path]::GetTempFileName()
    try {
        Invoke-CleanGit (@('diff', '--no-color', '--no-ext-diff', '--no-textconv', '--text', '--no-renames',
                '--no-relative', '--src-prefix=a/', '--dst-prefix=b/', '--diff-algorithm=myers',
                '--inter-hunk-context=0', '--ignore-submodules=none', "--output=$patch") + $Arguments) | Out-Null
        if ($LASTEXITCODE -ne 0) { return $null }
        return [Text.Encoding]::GetEncoding(28591).GetString([IO.File]::ReadAllBytes($patch))
    }
    finally {
        Remove-Item -LiteralPath $patch -Force -ErrorAction SilentlyContinue
    }
}

function Get-ChangedPaths {
    # The paths changed between two commits, each with its old and new mode, read with -z
    # so no quoting or encoding can alter a name. Returns $null on a git error.
    param([string]$From, [string]$To)
    $raw = [IO.Path]::GetTempFileName()
    try {
        Invoke-CleanGit @('diff', '--raw', '-z', '--no-renames', '--no-relative', '--no-ext-diff',
            '--ignore-submodules=none', "--output=$raw", $From, $To) | Out-Null
        if ($LASTEXITCODE -ne 0) { return $null }
        $parts = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($raw)) -split "`0"
        $result = @()
        for ($i = 0; $i + 1 -lt $parts.Count; $i += 2) {
            if ($parts[$i] -notmatch '^:(\d{6}) (\d{6}) ') { return $null }
            $result += [pscustomobject]@{ Path = $parts[$i + 1]; OldMode = $Matches[1]; NewMode = $Matches[2] }
        }
        return , $result
    }
    finally {
        Remove-Item -LiteralPath $raw -Force -ErrorAction SilentlyContinue
    }
}

function Test-ListFile {
    # True only for an exact, case-sensitive match with $ListFiles that is a plain file
    # (or missing) on both sides - a symlink or submodule under a list name is strict.
    param([string]$Path, [string]$OldMode = '100644', [string]$NewMode = '100644')
    $plain = @('100644', '000000')
    return (@($ListFiles | Where-Object { $_ -ceq $Path }).Count -eq 1) -and ($plain -contains $OldMode) -and ($plain -contains $NewMode)
}

function ConvertTo-LiteralPathspec([string]$Path) {
    # A pathspec that matches exactly this path from the repository root, nothing else.
    return ":(top,literal)$Path"
}

function ConvertTo-ListChangeLines {
    <#
    Reduces a patch of list files (made with a context so large that each file is one
    hunk) to the lines the pull request adds or removes. Each such line is kept together
    with the Markdown headings it stands under, e.g.
        + [Changelog > [Unveroeffentlicht] > Hinzugefuegt] - **New thing** (#1)
    Context lines themselves are dropped: a neighbour that main added or changed does not
    change the result, but the same entry under another heading (say, in a released
    version instead of "Unveroeffentlicht") does. Header lines (file names, modes, a
    binary patch) are kept as they are. Line endings are kept.
    #>
    param([string]$Patch)

    $out = New-Object System.Text.StringBuilder
    $inHeader = $false
    $fence = $false
    $headings = New-Object string[] 7
    foreach ($line in $Patch -split "`n") {
        if ($line.StartsWith('diff --git ')) {
            $inHeader = $true; $fence = $false; $headings = New-Object string[] 7
            [void]$out.Append($line).Append("`n")
            continue
        }
        if ($inHeader) {
            if ($line.StartsWith('@@')) { $inHeader = $false; continue }
            if ($line -match '^index [0-9a-f]+\.\.[0-9a-f]+') { continue }   # blob hashes change with every merge of main
            [void]$out.Append($line).Append("`n")                              # names, modes, binary patch
            continue
        }
        if ($line.Length -eq 0 -or $line.StartsWith('@@')) { continue }
        $mark = $line[0]
        if ($mark -ne ' ' -and $mark -ne '+' -and $mark -ne '-') { [void]$out.Append($line).Append("`n"); continue }   # "\ No newline at end of file"
        $body = $line.Substring(1)
        if ($mark -ne '-') {
            # Context and added lines make up the file after the change: follow its headings.
            $text = $body.TrimEnd("`r")
            if ($text -match '^ {0,3}(```|~~~)') { $fence = -not $fence }
            elseif (-not $fence -and $text -match '^(#{1,6})\s+(.*?)\s*#*\s*$') {
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
    the pull request itself adds, removes or alters a byte, or when main changed the
    lines right around the pull request's changes (three lines of context).
    Exception: in the list files ($ListFiles, exact paths) only the added and removed
    lines count, each with the headings it stands under (ConvertTo-ListChangeLines).
    $Repository may be any folder inside the repository; the result is the same.
    Returns an empty string for an empty diff or on any error; callers treat that as
    "not comparable".
    #>
    param(
        [Parameter(Mandatory)] [string]$Base,
        [Parameter(Mandatory)] [string]$Head,
        [string]$Repository = '.'
    )

    $root = Get-RepositoryRoot $Repository
    if (-not $root) { return '' }
    Push-Location -LiteralPath $root
    try {
        $headCommit = Invoke-CleanGit @('rev-parse', '--verify', '--quiet', "$Head^{commit}")
        $baseCommit = Invoke-CleanGit @('rev-parse', '--verify', '--quiet', "$Base^{commit}")
        if (-not $headCommit -or -not $baseCommit) { return '' }
        $headCommit = ([string]$headCommit).Trim(); $baseCommit = ([string]$baseCommit).Trim()
        $mergeBase = Invoke-CleanGit @('merge-base', $baseCommit, $headCommit)
        if ($LASTEXITCODE -ne 0 -or -not $mergeBase) { return '' }
        $mergeBase = ([string]$mergeBase).Trim()

        $changed = Get-ChangedPaths $mergeBase $headCommit
        if ($null -eq $changed) { return '' }
        if ($changed.Count -eq 0) { return '' }
        if ($changed.Count -gt 300) { return '' }   # command line would get too long: not comparable

        $lists = @($changed | Where-Object { Test-ListFile $_.Path $_.OldMode $_.NewMode } | ForEach-Object { $_.Path })
        $others = @($changed | Where-Object { -not (Test-ListFile $_.Path $_.OldMode $_.NewMode) } | ForEach-Object { $_.Path })

        $other = ''
        if ($others.Count -gt 0) {
            $other = Get-GitDiffText (@('--full-index', '-U3', $mergeBase, $headCommit, '--') + @($others | ForEach-Object { ConvertTo-LiteralPathspec $_ }))
            if ($null -eq $other) { return '' }
            # Every strictly compared file must really be in the patch (one header each).
            $headers = @($other -split "`n" | Where-Object { $_.StartsWith('diff --git ') }).Count
            if ($headers -ne $others.Count) { return '' }
        }
        $listPatch = ''
        if ($lists.Count -gt 0) {
            $listPatch = Get-GitDiffText (@('--full-index', '-U1000000', $mergeBase, $headCommit, '--') + @($lists | ForEach-Object { ConvertTo-LiteralPathspec $_ }))
            if ($null -eq $listPatch) { return '' }
        }
        if ($other.Length -eq 0 -and $listPatch.Length -eq 0) { return '' }

        $kept = New-Object System.Text.StringBuilder
        foreach ($line in $other -split "`n") {
            if ($line -match '^index [0-9a-f]+\.\.[0-9a-f]+') { continue }   # blob hashes change with every merge of main
            if ($line -match '^@@ ') { $line = '@@' }                         # line numbers shift when main adds lines above
            [void]$kept.Append($line).Append("`n")                            # line endings stay: a CRLF/LF change counts
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

function Get-ConflictedPaths {
    # The files a plain merge of $Base into $Head leaves in conflict, without any merge
    # driver from attribute files. Returns $null on a git error.
    param([string]$Base, [string]$Head, [string]$Attributes)
    $result = @(Invoke-CleanGit @('-c', "core.attributesFile=$Attributes", 'merge-tree', '--write-tree', '--name-only', '--no-messages', $Head, $Base))
    if ($LASTEXITCODE -eq 0) { return , @() }
    if ($LASTEXITCODE -ne 1 -or $result.Count -lt 2) { return $null }
    return , @($result | Select-Object -Skip 1 | Where-Object { $_ } | ForEach-Object { ([string]$_).Trim() } | Select-Object -Unique)
}

function New-ListUnionMergeCommit {
    <#
    Merges $Base into $Head without a working tree (git merge-tree), letting git's
    built-in "union" driver resolve conflicts in the list files: both sides' new lines
    are kept. Returns the new merge commit (parents: $Head, $Base), which exists only
    locally until someone pushes it. Returns an empty string if a plain merge would leave
    a conflict in any file that is not exactly a list file, or on a git error.
    Union can also keep two versions of a line both sides changed. So a caller must
    compare the fingerprint (Get-PullRequestPatchId) before using the commit.
    #>
    param(
        [Parameter(Mandatory)] [string]$Base,
        [Parameter(Mandatory)] [string]$Head,
        [Parameter(Mandatory)] [string]$Message,
        [string]$Repository = '.'
    )

    $root = Get-RepositoryRoot $Repository
    if (-not $root) { return '' }
    Push-Location -LiteralPath $root
    $empty = [IO.Path]::GetTempFileName()
    $attributes = [IO.Path]::GetTempFileName()
    try {
        $baseCommit = Invoke-CleanGit @('rev-parse', '--verify', '--quiet', "$Base^{commit}")
        $headCommit = Invoke-CleanGit @('rev-parse', '--verify', '--quiet', "$Head^{commit}")
        if (-not $baseCommit -or -not $headCommit) { return '' }
        $baseCommit = ([string]$baseCommit).Trim(); $headCommit = ([string]$headCommit).Trim()

        # Only conflicts in exact list files may be resolved here.
        $conflicts = Get-ConflictedPaths $baseCommit $headCommit $empty
        if ($null -eq $conflicts) { return '' }
        foreach ($path in $conflicts) { if (-not (Test-ListFile $path)) { return '' } }

        # Leading "/" anchors each pattern at the root (no file of that name in a sub folder).
        [IO.File]::WriteAllText($attributes, (($ListFiles | ForEach-Object { "/$_ merge=union" }) -join "`n") + "`n")
        $result = @(Invoke-CleanGit @('-c', "core.attributesFile=$attributes", 'merge-tree', '--write-tree', $headCommit, $baseCommit))
        if ($LASTEXITCODE -ne 0 -or $result.Count -eq 0) { return '' }   # 1 = conflicts left, other = error
        $tree = ([string]$result[0]).Trim()

        $commit = Invoke-CleanGit @('commit-tree', $tree, '-p', $headCommit, '-p', $baseCommit, '-m', $Message)
        if ($LASTEXITCODE -ne 0 -or -not $commit) { return '' }
        return ([string]$commit).Trim()
    }
    finally {
        Remove-Item -LiteralPath $attributes, $empty -Force -ErrorAction SilentlyContinue
        Pop-Location
    }
}

function Test-UpdateMergeOf {
    # True if $NewHead is a merge commit whose first parent is $OldHead - what GitHub's
    # "Update branch" makes. Anything else (say, the author pushed meanwhile) is false.
    param([string]$OldHead, [string]$NewHead, [string]$Repository = '.')
    $root = Get-RepositoryRoot $Repository
    if (-not $root) { return $false }
    Push-Location -LiteralPath $root
    try {
        $old = Invoke-CleanGit @('rev-parse', '--verify', '--quiet', "$OldHead^{commit}")
        $parents = Invoke-CleanGit @('rev-list', '--parents', '-n', '1', "$NewHead^{commit}")
        if (-not $old -or -not $parents) { return $false }
        $ids = ([string]$parents).Trim() -split ' '
        return ($ids.Count -eq 3 -and $ids[1] -eq ([string]$old).Trim())
    }
    finally { Pop-Location }
}
