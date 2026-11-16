# The comparison used by pr-nachziehen.ps1, in its own file so it can be tested
# (tools/pr-nachziehen.tests.ps1) without touching GitHub.
# Keep this file ASCII-only.

function Get-PullRequestPatchId {
    <#
    The fingerprint of what a pull request changes: the diff from the point where the
    branch and the base meet (merge base) to the branch head, hashed without the parts
    that change on their own (blob hashes, line numbers of the hunks). So the fingerprint
    stays the same when main moved elsewhere and was merged in - and changes as soon as
    the pull request itself adds, removes or alters a line, or when main changed the
    lines right around the pull request's changes.
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

        # git writes the diff to a file itself, so no pipe re-encodes it on the way.
        $patch = [IO.Path]::GetTempFileName()
        try {
            git diff --full-index --no-color --no-ext-diff "--output=$patch" $mergeBase $headCommit 2>$null
            if ($LASTEXITCODE -ne 0) { return '' }
            $bytes = [IO.File]::ReadAllBytes($patch)
            if ($bytes.Length -eq 0) { return '' }

            # Latin-1 maps every byte to one character and back, so nothing is lost.
            $latin1 = [Text.Encoding]::GetEncoding(28591)
            $kept = New-Object System.Text.StringBuilder
            foreach ($line in $latin1.GetString($bytes) -split "`n") {
                if ($line -match '^index [0-9a-f]+\.\.[0-9a-f]+') { continue }   # blob hashes change with every merge of main
                if ($line -match '^@@ ') { $line = '@@' }                         # line numbers shift when main adds lines above
                [void]$kept.Append($line.TrimEnd("`r")).Append("`n")
            }
            $sha = [Security.Cryptography.SHA256]::Create()
            return ([BitConverter]::ToString($sha.ComputeHash($latin1.GetBytes($kept.ToString()))) -replace '-', '').ToLower()
        }
        finally {
            Remove-Item -LiteralPath $patch -Force -ErrorAction SilentlyContinue
        }
    }
    finally {
        Pop-Location
    }
}
