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
#   <replace>      text to put in its place - literal; with --regex, $1, $2 ... or ${1} insert the groups of
#                  <search> (\$ writes a plain $, \\ a plain \, every other backslash stays as it is)
#   <test filter>  a class or test name part (becomes FullyQualifiedName~<filter>),
#                  or a full dotnet test filter expression if it contains = ~ | & or !
#                  with --bot: the arguments of the bot run instead (e.g. 'slam --seeds 1')
#
# Options:
#   --all            replace every place the text occurs (default: it must occur exactly once)
#   --regex          <search> is a Perl regular expression
#   --project PATH   test project (default: tests/SilverAndAsh.Core.Tests)
#   --name NAME      name of this probe in the output (default: the search text)
#   --bot            check with a bot run (tools/SilverAndAsh.BotRun, issue #647) instead of tests: the control run
#                    must end without findings, the change is caught if the bot reports a finding; the output names
#                    the exit code of the bot (1 with findings) besides the one of this script (issue #660)
#
# Every new option or way out of this script gets a case in its self-test tools/gegenprobe.tests.sh (issue #660).
# Log lines it shows (build errors, the end of an aborted run) have their absolute paths shortened to ".../<name>", so
# no temp folder or user name ends up in a PR comment.
#
# Only one run per working folder at a time (issue #604): a second run started while the first one has the file
# changed would take the broken file for the original. It stops at once with exit code 2 and names the running one.
# A lock left behind by a run that is gone is taken over by exactly one run (issue #639, tools/gegenprobe-lock.sh).
#
# Afterwards the build output still holds the changed code: build again before anything runs with --no-build (also
# after exit code 3: no result came out, but parts of the change may have been built). Closing the output early
# (2>&1 | head) still puts the file back and lets go of the lock (issue #651).
#
# Exit code: 0 = caught (at least one test red, with --bot: a finding), 1 = survived (all green, no finding), 2 = could
# not run (another run in this working folder, unclean working folder, text not found, control run red or with
# findings ...), 3 = no result with the change (it does not compile, a built file could not be copied, or the test
# project does not build: no statement about the tests), 4 = the test run with the change was aborted (fewer tests ran
# than in the control run, e.g. the test process crashed: no statement about the tests, issue #657).
#
# Example (issue #578): the restart must reset the doors.
#   tools/gegenprobe.sh src/SilverAndAsh.Core/Gameplay/GameWorld.cs \
#     'GameWorld Restarted() => new(_levelWithOpenDoors,' 'GameWorld Restarted() => new(Level,' GrateTests
#
# The same with --regex and a group (issue #604): ${1} puts back what the brackets in <search> matched.
#   tools/gegenprobe.sh --regex src/SilverAndAsh.Core/Gameplay/GameWorld.cs \
#     '(GameWorld Restarted[(][)] => new[(])_levelWithOpenDoors,' '${1}Level,' GrateTests
#
# With --bot (issue #647): a grid half as fine must make mode "slam" report a finding.
#   tools/gegenprobe.sh --bot tools/SilverAndAsh.BotRun/BossPortalRuns.cs \
#     'PortalSite.Steps(-1.95f, 1.95f, 0.05f)' 'PortalSite.Steps(-1.95f, 1.95f, 0.1f)' 'slam --seeds 1'

set -u

all=0
regex=0
bot=0
project="tests/SilverAndAsh.Core.Tests"
name=""
while [ $# -gt 0 ]; do
    case "$1" in
        --all) all=1; shift ;;
        --regex) regex=1; shift ;;
        --bot) bot=1; shift ;;
        --project) project="${2:?--project needs a path}"; shift 2 ;;
        --name) name="${2:?--name needs a text}"; shift 2 ;;
        -h|--help) sed -n '2,/^$/p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
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
if [ "$bot" = 0 ]; then
    case "$filter" in
        *[=~\|\&!]*) ;;
        *) filter="FullyQualifiedName~$filter" ;;
    esac
fi

tools="$(cd "$(dirname "$0")" && pwd)" || exit 2
root="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "gegenprobe: not inside a git repository" >&2; exit 2; }
cd "$root" || exit 2

work=""
bot_exit=""
changed=0
built_changed=0

restore() {
    # The output may be closed already (2>&1 | head): a message must not end the clean-up before the lock is let go
    # (issue #651).
    trap '' PIPE
    if [ "$changed" = 1 ]; then
        cp -- "$backup" "$file"
        changed=0
    fi
    if [ -n "$work" ]; then
        rm -rf -- "$work"
        if [ -n "$(git status --porcelain)" ]; then
            echo "gegenprobe: WARNING - the working folder is not clean after restoring:" >&2
            git status --short >&2
        fi
    fi
    if [ "$built_changed" = 1 ]; then
        # The build output still holds the changed code until the next build (issue #639).
        echo "gegenprobe: the build output still holds the changed code - build again before running anything with" >&2
        echo "   --no-build: dotnet build -c Release" >&2
        built_changed=0
    fi
    if declare -F lock_release >/dev/null; then
        lock_release
    fi
}
trap restore EXIT
trap 'echo "gegenprobe: interrupted - restoring $file" >&2; exit 130' INT TERM HUP

# One run per working folder (issues #604, #639), see tools/gegenprobe-lock.sh: of two runs started together exactly
# one gets the lock, also when both find the same stale lock. It lives in the git folder of this working folder (each
# worktree has its own), so git status never shows it.
. "$tools/gegenprobe-lock.sh" || exit 2
lock_take "$(git rev-parse --absolute-git-dir)/gegenprobe.lock" || exit 2

if [ -n "$(git status --porcelain)" ]; then
    echo "gegenprobe: the working folder is not clean - commit or set your changes aside first:" >&2
    git status --short >&2
    exit 2
fi

if ! git ls-files --error-unmatch -- "$file" >/dev/null 2>&1; then
    echo "gegenprobe: $file is not a tracked file of this repository" >&2
    exit 2
fi

# The replacement step (count, change) is tools/gegenprobe-replace.pl; tools/gegenprobe.tests.sh checks it.
modes=()
[ "$regex" = 1 ] && modes+=(--regex)
[ "$all" = 1 ] && modes+=(--all)
hits="$(perl "$tools/gegenprobe-replace.pl" "${modes[@]}" --count "$file")" || exit 2
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

# The changed file is made before the control run, so a wrong <replace> (a group <search> does not have) stops at
# once.
perl "$tools/gegenprobe-replace.pl" "${modes[@]}" "$file" > "$work/changed-file" || exit 2
if cmp -s -- "$work/changed-file" "$backup"; then
    echo "gegenprobe: the change did not apply (<replace> gives the same text)" >&2
    exit 2
fi

# Lines of a log shown to the reader may end up in PR comments: absolute paths (the temp folder of the trx file, the
# user folder) become ".../<last part>" (issue #660).
shorten_paths() {
    perl -pe 's{(?<![^\s"'"'"'(\[=])(?:[A-Za-z]:)?([\\/])(?:[^\s\\/:"'"'"']+[\\/])+}{...$1}g'
}

# The error lines of a build log (error CS..., error MSB...), each once, at most ten.
build_errors() {
    grep -E "error [A-Z]+[0-9]+" "$1" | sed -E 's/ \[[^]]*\]$//' | shorten_paths | sort -u | head -n 10 >&2
}

# Runs the filtered tests; sets total, failed, red (names) and returns 3 if nothing could be run (build error).
run_tests() {
    local label="$1"
    local results="$work/$label"
    dotnet test "$project" -c Release --filter "$filter" \
        --logger "trx;LogFileName=results.trx" --results-directory "$results" > "$work/$label.log" 2>&1
    local trx
    trx="$(find "$results" -name '*.trx' 2>/dev/null | head -n 1)"
    if [ -z "$trx" ]; then
        build_errors "$work/$label.log"
        return 3
    fi
    total="$(perl -ne 'print $1 if /<Counters\b[^>]*\btotal="(\d+)"/' "$trx")"
    failed="$(perl -ne 'print $1 if /<Counters\b[^>]*\bfailed="(\d+)"/' "$trx")"
    red="$(perl -ne 'while (/<UnitTestResult\b[^>]*>/g) { my $t = $&; print "$1\n" if $t =~ /outcome="Failed"/ && $t =~ /testName="([^"]*)"/ }' "$trx" | sort)"
    total="${total:-0}"
    failed="${failed:-0}"
    return 0
}

# With --bot: builds the bot and runs it with the arguments in $filter (word by word); sets total (1: the run),
# failed (1 if it reported a finding) and red (its FINDING lines). Returns 3 if the bot does not build, 2 if it could
# not run (its exit code 2: wrong arguments or the tool itself failed).
run_bot() {
    local label="$1"
    local bot_project="tools/SilverAndAsh.BotRun"
    if ! dotnet build "$bot_project" -c Release > "$work/$label.build.log" 2>&1; then
        build_errors "$work/$label.build.log"
        return 3
    fi
    local args
    read -r -a args <<< "$filter"
    dotnet run --project "$bot_project" -c Release --no-build -- "${args[@]}" > "$work/$label.log" 2>&1
    bot_exit=$?
    total=1
    red="$(grep -E '^FINDING ' "$work/$label.log" | head -n 10)"
    case "$bot_exit" in
        0) failed=0 ;;
        1) failed=1 ;;
        *) tail -n 5 "$work/$label.log" | shorten_paths >&2; return 2 ;;
    esac
    return 0
}

check() {
    if [ "$bot" = 1 ]; then
        run_bot "$1"
    else
        run_tests "$1"
    fi
}

# No result came out with the change (exit code 3). That has three causes (issue #657): the change does not compile
# (nothing was built with it), a built file could not be copied (locked by another process), or the test project or
# the bot does not build - in the last two the changed code is built already, at least in part. So say neither that
# the output holds the change nor that it does not.
no_result() {
    built_changed=0
    echo "   result: no result with the change - no statement about the tests"
    echo "gegenprobe: no result with the change: the change did not compile, a built file could not be copied (locked" >&2
    echo "   by another process?), or the test project does not build - see the errors above. Parts of the build output" >&2
    echo "   may hold the changed code: build again before running anything with --no-build: dotnet build -c Release" >&2
    exit 3
}

# The result of the bot run with the change: a finding means caught.
report_bot() {
    if [ "$failed" = 0 ]; then
        echo "   result: SURVIVED - the bot reports no finding with the change (bot exit code $bot_exit)"
        exit 1
    fi
    # The exit code of this script is 0 here; the one of the bot is 1 (issue #660).
    echo "   result: CAUGHT - the bot reports findings (bot exit code $bot_exit), the first ten:"
    echo "$red" | sed 's/^/     /'
    exit 0
}

# The result of the test run with the change: aborted, survived or caught.
report_tests() {
    if [ "$total" -lt "$control_total" ]; then
        report_aborted
    fi
    if [ "$failed" = 0 ]; then
        echo "   result: SURVIVED - $total test(s), all green with the change"
        exit 1
    fi
    echo "   result: CAUGHT - $failed of $total test(s) red:"
    echo "$red" | sed 's/^/     /'
    exit 0
}

# Fewer tests ran with the change than in the control run (issue #657): the test process ended early - a change that
# makes it crash (endless recursion, stack overflow) leaves a result file with total="0" - or the change took tests
# away. Either way the green ones say nothing; this is neither "caught" nor "survived".
report_aborted() {
    echo "   result: ABORTED - only $total of $control_total test(s) ran with the change - no statement about the tests"
    if [ "$failed" != 0 ]; then
        echo "   red before the run ended:"
        echo "$red" | sed 's/^/     /'
    fi
    echo "gegenprobe: the test run with the change was aborted (the test process crashed?) or the change took tests" >&2
    # After a crash the end of the output is the stack; the cause ("Stack overflow") stands further up (issue #660).
    local cause
    cause="$(grep -i -E 'Stack overflow|crashed|abgebrochen|aborted' "$work/mutated.log" | awk '!seen[$0]++' | head -n 5)"
    if [ -n "$cause" ]; then
        echo "   away. The cause:" >&2
        echo "$cause" | shorten_paths | sed 's/^/   /' >&2
        echo "   The end of its output:" >&2
    else
        echo "   away. The end of its output:" >&2
    fi
    tail -n 5 "$work/mutated.log" | shorten_paths | sed 's/^/   /' >&2
    exit 4
}

echo "== gegenprobe: $name"
echo "   file:   $file"
if [ "$bot" = 1 ]; then
    echo "   bot:    $filter"
else
    echo "   filter: $filter"
fi

check control
status=$?
control_total="${total:-0}"
if [ "$status" = 3 ]; then
    echo "gegenprobe: control run does not build" >&2
    exit 2
fi
if [ "$status" = 2 ]; then
    echo "gegenprobe: the bot could not run with '$filter' (exit code 2)" >&2
    exit 2
fi
if [ "$bot" = 1 ] && [ "$failed" != 0 ]; then
    echo "gegenprobe: control run of the bot already reports findings - fix those first:" >&2
    echo "$red" | sed 's/^/   /' >&2
    exit 2
fi
if [ "$bot" = 1 ]; then
    echo "   control run: bot without findings (bot exit code $bot_exit)"
else
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
fi

changed=1
built_changed=1
cp -- "$work/changed-file" "$file"

check mutated
status=$?
if [ "$status" = 3 ]; then
    no_result
fi
if [ "$status" = 2 ]; then
    echo "gegenprobe: the bot could not run with the change (exit code 2) - no statement" >&2
    exit 2
fi
if [ "$bot" = 1 ]; then
    report_bot
fi
report_tests
