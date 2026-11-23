#!/usr/bin/env bash
# Gegenprobe (issue #573): break one place in the code on purpose, run the filtered tests and count the red ones,
# then put the file back byte for byte - also on Ctrl+C or an error. A control run without the change comes first:
# the filtered tests must exist and be green, otherwise the result would mean nothing.
#
# Usage (Git Bash, from anywhere inside the repository):
#   tools/gegenprobe.sh [options] <file> <search> <replace> <test filter>
#
#   <file>         file to change, relative to the repository root (e.g. src/SilverAndAsh.Core/Gameplay/GameWorld.cs)
#   <search>       text to find - taken literally (no regex) unless --regex is given
#   <replace>      text to put in its place (always literal)
#   <test filter>  a class or test name part (becomes FullyQualifiedName~<filter>),
#                  or a full dotnet test filter expression if it contains = ~ | & or !
#
# Options:
#   --all            replace every place the text occurs (default: it must occur exactly once)
#   --regex          <search> is a Perl regular expression
#   --project PATH   test project (default: tests/SilverAndAsh.Core.Tests)
#   --name NAME      name of this probe in the output (default: the search text)
#
# Exit code: 0 = caught (at least one test red), 1 = survived (all green), 2 = could not run (unclean working
# folder, text not found, control run red ...), 3 = the change does not compile (no statement about the tests).
#
# Example (issue #578): the restart must reset the doors.
#   tools/gegenprobe.sh src/SilverAndAsh.Core/Gameplay/GameWorld.cs \
#     'GameWorld Restarted() => new(_levelWithOpenDoors,' 'GameWorld Restarted() => new(Level,' GrateTests

set -u

all=0
regex=0
project="tests/SilverAndAsh.Core.Tests"
name=""
while [ $# -gt 0 ]; do
    case "$1" in
        --all) all=1; shift ;;
        --regex) regex=1; shift ;;
        --project) project="${2:?--project needs a path}"; shift 2 ;;
        --name) name="${2:?--name needs a text}"; shift 2 ;;
        -h|--help) sed -n '2,27p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        --) shift; break ;;
        -*) echo "gegenprobe: unknown option $1 (see --help)" >&2; exit 2 ;;
        *) break ;;
    esac
done

if [ $# -ne 4 ]; then
    echo "gegenprobe: need <file> <search> <replace> <test filter> (see --help)" >&2
    exit 2
fi

file="$1"
export GEGENPROBE_SEARCH="$2"
export GEGENPROBE_REPLACE="$3"
filter="$4"
name="${name:-$2}"
case "$filter" in
    *[=~\|\&!]*) ;;
    *) filter="FullyQualifiedName~$filter" ;;
esac

root="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "gegenprobe: not inside a git repository" >&2; exit 2; }
cd "$root" || exit 2

if [ -n "$(git status --porcelain)" ]; then
    echo "gegenprobe: the working folder is not clean - commit or set your changes aside first:" >&2
    git status --short >&2
    exit 2
fi

if ! git ls-files --error-unmatch -- "$file" >/dev/null 2>&1; then
    echo "gegenprobe: $file is not a tracked file of this repository" >&2
    exit 2
fi

if [ "$regex" = 1 ]; then pattern='$ENV{GEGENPROBE_SEARCH}'; else pattern='\Q$ENV{GEGENPROBE_SEARCH}\E'; fi
hits="$(perl -0777 -ne "\$c = 0; \$c++ while /$pattern/g; print \$c" -- "$file")"
if [ "$hits" = 0 ]; then
    echo "gegenprobe: '$GEGENPROBE_SEARCH' not found in $file - nothing to change" >&2
    exit 2
fi
if [ "$hits" != 1 ] && [ "$all" = 0 ]; then
    echo "gegenprobe: '$GEGENPROBE_SEARCH' occurs $hits times in $file - make it unique or pass --all" >&2
    exit 2
fi

work="$(mktemp -d)"
backup="$work/original"
cp -- "$file" "$backup"
changed=0

restore() {
    if [ "$changed" = 1 ]; then
        cp -- "$backup" "$file"
        changed=0
    fi
    rm -rf -- "$work"
    if [ -n "$(git status --porcelain)" ]; then
        echo "gegenprobe: WARNING - the working folder is not clean after restoring:" >&2
        git status --short >&2
    fi
}
trap restore EXIT
trap 'echo "gegenprobe: interrupted - restoring $file" >&2; exit 130' INT TERM HUP

# Runs the filtered tests; sets total, failed, red (names) and returns 3 if nothing could be run (build error).
run_tests() {
    local label="$1"
    local results="$work/$label"
    dotnet test "$project" -c Release --filter "$filter" \
        --logger "trx;LogFileName=results.trx" --results-directory "$results" > "$work/$label.log" 2>&1
    local trx
    trx="$(find "$results" -name '*.trx' 2>/dev/null | head -n 1)"
    if [ -z "$trx" ]; then
        grep -E "error [A-Z]+[0-9]+" "$work/$label.log" | sed -E 's/ \[[^]]*\]$//' | sort -u | head -n 10 >&2
        return 3
    fi
    total="$(perl -ne 'print $1 if /<Counters\b[^>]*\btotal="(\d+)"/' "$trx")"
    failed="$(perl -ne 'print $1 if /<Counters\b[^>]*\bfailed="(\d+)"/' "$trx")"
    red="$(perl -ne 'while (/<UnitTestResult\b[^>]*>/g) { my $t = $&; print "$1\n" if $t =~ /outcome="Failed"/ && $t =~ /testName="([^"]*)"/ }' "$trx" | sort)"
    total="${total:-0}"
    failed="${failed:-0}"
    return 0
}

echo "== gegenprobe: $name"
echo "   file:   $file"
echo "   filter: $filter"

run_tests control
status=$?
if [ "$status" = 3 ]; then
    echo "gegenprobe: control run does not build" >&2
    exit 2
fi
if [ "$total" = 0 ]; then
    echo "gegenprobe: the filter '$filter' selects no tests" >&2
    exit 2
fi
if [ "$failed" != 0 ]; then
    echo "gegenprobe: control run already has $failed red test(s) - fix those first:" >&2
    echo "$red" | sed 's/^/   /' >&2
    exit 2
fi
echo "   control run: $total test(s), all green"

changed=1
if [ "$all" = 1 ]; then flags=g; else flags=; fi
perl -0777 -pi -e "s/$pattern/\$ENV{GEGENPROBE_REPLACE}/$flags" -- "$file"
if cmp -s -- "$file" "$backup"; then
    echo "gegenprobe: the change did not apply" >&2
    exit 2
fi

run_tests mutated
status=$?
if [ "$status" = 3 ]; then
    echo "   result: the change does not compile - no statement about the tests"
    exit 3
fi
if [ "$failed" = 0 ]; then
    echo "   result: SURVIVED - $total test(s), all green with the change"
    exit 1
fi
echo "   result: CAUGHT - $failed of $total test(s) red:"
echo "$red" | sed 's/^/     /'
exit 0
