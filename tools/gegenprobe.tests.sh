#!/usr/bin/env bash
# Self-test of the replacement step of tools/gegenprobe.sh (tools/gegenprobe-replace.pl, issue #604), without dotnet:
# each case changes a small file in a temporary folder and compares the result with the expected text.
#
# Usage (Git Bash): tools/gegenprobe.tests.sh      Exit code 0 = all cases pass, 1 = at least one fails.

set -u

replace="$(cd "$(dirname "$0")" && pwd)/gegenprobe-replace.pl"
work="$(mktemp -d)"
trap 'rm -rf -- "$work"' EXIT
passed=0
failed=0

# check <name> <expected exit> <expected output> <file text> <search> <replace> [modes...]
check() {
    local name="$1" want_exit="$2" want="$3" text="$4"
    export GEGENPROBE_SEARCH="$5" GEGENPROBE_REPLACE="$6"
    shift 6
    printf '%s' "$text" > "$work/file"
    local got status
    got="$(perl "$replace" "$@" "$work/file" 2>"$work/err")"
    status=$?
    [ "$status" = 0 ] || got="$(cat "$work/err")"
    if [ "$status" = "$want_exit" ] && [ "$got" = "$want" ]; then
        passed=$((passed + 1))
    else
        failed=$((failed + 1))
        echo "FAIL $name"
        echo "   exit $status (want $want_exit)"
        echo "   got:  $got"
        echo "   want: $want"
    fi
}

# Without --regex everything stays literal.
check 'literal: $1 stays'          0 'a = $1;'              'a = b;'          'b' '$1'
check 'literal: backslashes stay'  0 'a = "p\nq\\r";'       'a = "x";'        '"x"' '"p\nq\\r"'
check 'literal: regex signs stay'  0 'a = c;'               'a = (b);'        '(b)' 'c'
# With --regex: groups, escapes, every other backslash literal.
check 'regex: $1'                  0 'c = 1;'               'b = 1;'          'b (= 1;)' 'c $1' --regex
check 'regex: ${1} before a digit' 0 'x = 70;'              'x = 5;'          '(x = )5' '${1}70' --regex
check 'regex: $0 is the match'     0 'ab-ab'                'ab'              'a(b)' '$0-$0' --regex
check 'regex: \n stays \n'         0 'b = "p\nq";'          'b = "x";'        '"x"' '"p\nq"' --regex
check 'regex: \$ is a plain $'     0 'v = $1;'              'v = 0;'          '0' '\$1' --regex
check 'regex: \\ is a plain \'     0 'p = "a\b";'           'p = "x";'        'x' 'a\\b' --regex
check 'regex: group not matched'   0 'gh'                   'foo'             'foo(Z)?' 'g$1h' --regex
check 'regex: middle not matched'  0 '[|c]'                 'ac'              'a(b)?(c)' '[$1|$2]' --regex
check 'regex: --all per match'     0 'x1 x2'                'a1 a2'           'a([0-9])' 'x$1' --regex --all
check 'regex: only first'          0 'x1 a2'                'a1 a2'           'a([0-9])' 'x$1' --regex
check 'regex: missing group'       2 'gegenprobe: <replace> uses $2, but <search> has only 1 group(s)' \
                                     'ab'                   'a(b)'            '$2' --regex
# CRLF stays byte for byte.
check 'crlf stays'                 0 "$(printf 'x\r\nc\r\n')" "$(printf 'x\r\ny\r\n')" 'y' 'c'
# Counting.
check 'count'                      0 '3'                    'a a a'           'a' '' --count
# Counting with --regex counts matches, not groups (issue #676): one match with two groups is 1, not 2.
check 'regex count: two groups'    0 '1'                    'ab cd'           '(a)(b)' '' --regex --count
check 'regex count: groups twice'  0 '2'                    'ab ab'           '(a)(b)' '' --regex --count
check 'regex count: group unset'   0 '1'                    'ac'              'a(b)?(c)' '' --regex --count

# The lock (tools/gegenprobe-lock.sh, issue #639). A contender is a run of its own: it takes the lock, writes its
# number into "won", holds the lock <hold> seconds and lets it go. Its exit code is the one of lock_take.
# If a waiting hook fails, a case waits at most this long instead of 60 s (issue #660): red in about a minute, not
# four. The runs reach each other within a second even on a busy machine.
export GEGENPROBE_TEST_WAIT_SECONDS=10
lockfile="$(cd "$(dirname "$0")" && pwd)/gegenprobe-lock.sh"
contender() {
    local hold="$1"
    bash -c '
        . "$1" || exit 9
        lock_take "$2" || exit $?
        echo $$ >> "$3"
        sleep "$4"
        lock_release' contender "$lockfile" "$work/lock" "$work/won" "$hold" 2>>"$work/lock-err"
}

# One of two runs that meet in the gap of the takeover (issue #657: they wait for each other by files, not by fixed
# times). Between finding the stale lock and taking it over it makes <found> (if given) and waits for <wait>. If it
# gets the lock, it writes its number into "won", makes "holds" and keeps the lock until "release" is there.
racer() {
    local found="$1" wait="$2"
    GEGENPROBE_TEST_FOUND_STALE="$found" GEGENPROBE_TEST_WAIT_BEFORE_TAKEOVER="$wait" bash -c '
        . "$1" || exit 9
        lock_take "$2" || exit $?
        echo $$ >> "$3"
        : > "$4"
        lock_test_wait_for "$5"
        lock_release' racer "$lockfile" "$work/lock" "$work/won" "$work/holds" "$work/release" 2>>"$work/lock-err"
}

# A process number nobody has any more: a run that has ended.
bash -c 'exit 0' &
dead=$!
wait "$dead"

# expect <name> <condition...>: one passed or failed case.
expect() {
    local name="$1"
    shift
    if "$@"; then
        passed=$((passed + 1))
    else
        failed=$((failed + 1))
        echo "FAIL $name"
        sed 's/^/   stderr: /' "$work/lock-err" 2>/dev/null
    fi
}
fresh_lock() {
    rm -rf -- "$work/lock" "$work/lock".* "$work/won" "$work/lock-err" "$work/found" "$work/holds" "$work/release"
    : > "$work/won"
    : > "$work/lock-err"
}
winners() { [ "$(wc -l < "$work/won" | tr -d ' ')" = "$1" ]; }
no_lock_left() { [ -z "$(ls -d -- "$work/lock" "$work/lock".* 2>/dev/null)" ]; }
stale_lock() { mkdir -- "$work/lock" && echo "$dead" > "$work/lock/pid"; }

fresh_lock
contender 0
expect 'lock: free lock is taken'           [ $? = 0 ]
expect 'lock: free lock, one winner'        winners 1
expect 'lock: let go after the run'         no_lock_left

fresh_lock
mkdir -- "$work/lock" && echo $$ > "$work/lock/pid"
contender 0
expect 'lock: running owner, exit 2'        [ $? = 2 ]
expect 'lock: running owner, no winner'     winners 0
expect 'lock: running owner keeps it'       [ "$(cat "$work/lock/pid")" = $$ ]

fresh_lock
mkdir -- "$work/lock"
contender 0
expect 'lock: without a number, exit 2'     [ $? = 2 ]
expect 'lock: without a number, kept'       [ -d "$work/lock" ]

fresh_lock
stale_lock
contender 0
expect 'lock: stale lock is taken over'     [ $? = 0 ]
expect 'lock: stale lock, one winner'       winners 1
expect 'lock: stale lock, says so'          grep -q "removing the stale lock of run $dead" "$work/lock-err"
expect 'lock: stale lock, nothing left'     no_lock_left

# Two runs find the same stale lock. The first takes it over only once the second has found it too; the second goes on
# only once the first holds its fresh lock. Exactly one may win, and the lock of the first must stay: it is checked
# after the second has ended and before the first lets go.
fresh_lock
stale_lock
racer '' "$work/found" &
first=$!
racer "$work/found" "$work/holds" &
second=$!
wait "$second"
second_exit=$?
expect 'lock: race, the first holds it'     [ "$(cat "$work/lock/pid" 2>/dev/null)" = "$(head -n 1 "$work/won")" ]
: > "$work/release"
wait "$first"
first_exit=$?
expect 'lock: race, first run wins'         [ "$first_exit" = 0 ]
expect 'lock: race, second run exit 2'      [ "$second_exit" = 2 ]
expect 'lock: race, exactly one winner'     winners 1
expect 'lock: race, nothing left'           no_lock_left

# Letting go removes only the own lock.
fresh_lock
bash -c '
    . "$1" || exit 9
    lock_take "$2" || exit 8
    echo 1 > "$2/pid"
    lock_release
    [ -d "$2" ]' contender "$lockfile" "$work/lock" 2>>"$work/lock-err"
expect 'lock: release keeps a foreign lock' [ $? = 0 ]

# The whole run (issue #651) in a small repository of its own, with a stand-in for dotnet: it waits a moment, then
# writes a result file whose one test is red while probe.txt holds "broken", and builds nothing while it holds
# "nocompile"; while it holds "aborted", the test process crashes (issue #657); while it holds "locked", a built file
# cannot be copied (MSB3027, issue #660). As the bot (--bot, issue #647) it answers "build" (fails on "nocompile", and
# always with STANDIN_BUILD_FAILS=1) and "run": a finding (exit 1)
# while probe.txt holds "broken" or the bot arguments say "found", exit 2 while probe.txt or the arguments say "crash".
gegenprobe="$(cd "$(dirname "$0")" && pwd)/gegenprobe.sh"
repo="$work/repo"
mkdir -p -- "$repo" "$work/stand-in"
git -C "$repo" init -q
printf 'value = good;\n' > "$repo/probe.txt"
git -C "$repo" add probe.txt
git -C "$repo" -c user.name=probe -c user.email=probe@example.invalid commit -q -m probe
cat > "$work/stand-in/dotnet" <<'STANDIN'
#!/usr/bin/env bash
case "${1:-}" in
    build)
        sleep 1
        if grep -q nocompile probe.txt || [ "${STANDIN_BUILD_FAILS:-0}" = 1 ]; then
            echo "probe.txt(1,9): error CS1002: ; expected"
            exit 1
        fi
        exit 0 ;;
    run)
        while [ $# -gt 0 ] && [ "$1" != -- ]; do shift; done
        [ $# -gt 0 ] && shift
        case " $* " in
            *' found '*) echo "FINDING [probe] found in every run"; exit 1 ;;
            *' crash '*) echo "Unknown mode 'crash'"; exit 2 ;;
        esac
        if grep -q explode probe.txt; then echo "Fatal error. Internal CLR error."; exit 134; fi
        if grep -q crash probe.txt; then echo "the tool failed"; exit 2; fi
        if grep -q broken probe.txt; then echo "FINDING [probe] value is broken"; exit 1; fi
        echo "SUMMARY mode probe: 0 finding(s)"
        exit 0 ;;
esac
# The arguments of each test run, one line per run, into STANDIN_ARGS if set (issue #712).
[ -n "${STANDIN_ARGS:-}" ] && echo "$*" >> "$STANDIN_ARGS"
results=""
hang_after=""
while [ $# -gt 0 ]; do
    [ "$1" = --results-directory ] && results="$2"
    [ "$1" = --blame-hang-timeout ] && hang_after="$2"
    shift
done
sleep 1
if grep -q nocompile probe.txt; then
    echo "probe.txt(1,9): error CS1002: ; expected"
    exit 1
fi
if grep -q locked probe.txt; then
    echo "C:\\Program Files\\dotnet\\sdk\\Microsoft.Common.CurrentVersion.targets(5,5): error MSB3027: Could not copy \"obj\\Probe.dll\" to \"bin\\Probe.dll\". Exceeded retry count of 10. The file is locked by: \"testhost (1234)\""
    exit 1
fi
outcome=Passed
grep -q broken probe.txt && outcome=Failed
failed=0
[ "$outcome" = Failed ] && failed=1
# STANDIN_TOTAL tests (default 1); while probe.txt holds "aborted", the test process crashes and one test fewer ran
# (issue #657) - with the default none, as dotnet test writes total="0" after a stack overflow.
total="${STANDIN_TOTAL:-1}"
# As in a real crash (issue #660), the cause comes first and the stack after it, so the last lines are stack lines;
# the path of the result file comes last, as dotnet writes it on Windows: with the temp folder of the user.
if grep -q aborted probe.txt; then
    total=$((total - 1))
    echo "The active test run was aborted. Reason: Test host process crashed : Stack overflow."
    echo "Repeat 20000 times:"
    echo "--------------------------------"
    for frame in 1 2 3 4 5 6; do echo "   at Probe.Recurse(Int32)"; done
    echo "--------------------------------"
    echo "   at Probe.Test() in $(cygpath -w "$HOME" 2>/dev/null || echo "$HOME")\\src\\Probe.cs:line 3"
fi
# "crashmany": the same cause line twice (only the path differs) and six more lines with a cause word; "fewer": a test
# less, no crash text.
if grep -q crashmany probe.txt; then
    total=$((total - 1))
    dir="$(cygpath -w "$results" 2>/dev/null || echo "$results")"
    echo "Test host process crashed : Stack overflow. Dump in $dir\\a\\crash.dmp"
    echo "Test host process crashed : Stack overflow. Dump in $dir\\b\\crash.dmp"
    for n in 1 2 3 4 5 6; do echo "Probe $n aborted"; done
    echo "   at Probe.Test()"
fi
grep -q fewer probe.txt && total=$((total - 1))
# "hang" (or STANDIN_HANG=1, also in the control run): Probe.Test runs forever (issue #712). With --blame-hang-timeout
# the stand-in answers as dotnet does after the limit - the test does not count, the blame collector names it in its
# sequence file as not completed, and the output says why (STANDIN_GERMAN=1: in German, as on this machine). Without
# the limit it cannot hang for real and says so, so a case that forgets the limit does not pass.
hung=0
if grep -q hang probe.txt || [ "${STANDIN_HANG:-0}" = 1 ]; then
    if [ -z "$hang_after" ]; then
        echo "STAND-IN: would hang forever here (no --blame-hang-timeout)"
        exit 1
    fi
    hung=1
    total=$((total - 1))
    seconds="${hang_after%s}"
    if [ "${STANDIN_GERMAN:-0}" = 1 ]; then
        echo "Der aktive Testlauf wurde abgebrochen. Grund: Der Testhostprozess ist abgestürzt."
        echo "Datensammler \"Blame\" – Meldung: Die angegebene Inaktivitätszeit von $seconds Sekunden ist abgelaufen."
    else
        echo "The active test run was aborted. Reason: Test host process crashed"
        echo "Data collector 'Blame' message: The specified inactivity time of $seconds seconds has elapsed."
    fi
    [ "${STANDIN_NO_SEQUENCE:-0}" = 1 ] || mkdir -p -- "$results/sequence"
    [ "${STANDIN_NO_SEQUENCE:-0}" = 1 ] || printf '<TestSequence>\n  <Test Name="Probe.Before" DisplayName="Probe.Before" Completed="True" />\n  <Test Name="Probe.Test" DisplayName="Probe.Test" Completed="False" />\n</TestSequence>\n' \
        > "$results/sequence/Sequence_probe.xml"
fi
# The message of a red test (--message, issue #682) as dotnet writes it: XML signs escaped (&amp;amp; must become
# &amp;, not &), line ends as &#xD;&#xA; and as a real CRLF, a path in it. "nomessage": none at all (a short tag);
# "longmessage": 35 lines; "tworeds": a second red test that stands first in the file but not in the sorted list.
error="$(printf '%s\r\n%s' 'Probe: value is &quot;broken&quot; &lt;not good&gt; &amp;amp; red&#xD;&#xA;second line in C:\Work\Folder\Probe.cs:line 3' 'third line')"
grep -q longmessage probe.txt && error="$(seq -s '&#xA;' 1 35)"
# "lines30" and "lines31": exactly on the cut at 30 lines and one beyond it (review of PR #692).
grep -q lines30 probe.txt && error="$(seq -s '&#xA;' 1 30)"
grep -q lines31 probe.txt && error="$(seq -s '&#xA;' 1 31)"
result() {
    if [ -z "$3" ]; then
        printf '<UnitTestResult testName="%s" outcome="%s" />' "$1" "$2"
    else
        printf '<UnitTestResult testName="%s" outcome="%s"><Output><ErrorInfo><Message>%s</Message><StackTrace>   at %s()</StackTrace></ErrorInfo></Output></UnitTestResult>' "$1" "$2" "$3" "$1"
    fi
}
body="$(result Probe.Test "$outcome" "$([ "$outcome" = Failed ] && ! grep -q nomessage probe.txt && printf '%s' "$error")")"
# A test that was ended after the limit has no result of its own.
[ "$hung" = 1 ] && body=""
if grep -q tworeds probe.txt; then
    total=$((total + 1))
    failed=$((failed + 1))
    body="$(result Probe.Zeta Failed 'the message of Probe.Zeta')$body"
fi
mkdir -p -- "$results"
printf '<TestRun><Counters total="%s" failed="%s" />%s</TestRun>\n' "$total" "$failed" "$body" > "$results/results.trx"
# The path of the result file as dotnet writes it on Windows, with STANDIN_UNIX_PATH=1 as Git Bash writes it.
shown="$(cygpath -w "$results" 2>/dev/null || echo "$results")"
if [ "${STANDIN_UNIX_PATH:-0}" = 1 ]; then
    echo "Results File: $(cygpath -u "$results" 2>/dev/null || echo "$results")/results.trx"
else
    echo "Results File: $shown\\results.trx"
fi
STANDIN
chmod +x "$work/stand-in/dotnet"
lock_of_repo="$repo/.git/gegenprobe.lock"
lacks() { ! grep -q "$1" "$work/lock-err"; }
lacks_regex() { ! grep -q -E "$1" "$work/lock-err"; }
probe_back() { [ "$(cat "$repo/probe.txt")" = 'value = good;' ]; }
run_probe() { ( cd "$repo" && PATH="$work/stand-in:$PATH" bash "$gegenprobe" probe.txt good "$1" Probe ); }

: > "$work/lock-err"
run_probe broken > /dev/null 2>"$work/lock-err"
expect 'run: caught, exit 0'                [ $? = 0 ]
expect 'run: caught, file back'             probe_back
expect 'run: caught, lock gone'             [ ! -e "$lock_of_repo" ]
expect 'run: caught, says to build again'   grep -q 'still holds the changed code' "$work/lock-err"

# A change that does not compile built nothing: the message must not claim the output holds it.
: > "$work/lock-err"
run_probe nocompile > /dev/null 2>"$work/lock-err"
expect 'run: no compile, exit 3'            [ $? = 3 ]
expect 'run: no compile, file back'         probe_back
expect 'run: no compile, no false claim'    lacks 'still holds the changed code'
expect 'run: no compile, says so'           grep -q 'the change did not compile' "$work/lock-err"
# Exit code 3 also comes when only the copying or the test project failed, with the change built (issue #657).
expect 'run: no compile, names every cause' grep -q 'a built file could not be copied' "$work/lock-err"
expect 'run: no compile, says to build'     grep -q 'build again before running anything with --no-build' "$work/lock-err"

# A built file that cannot be copied (MSB3027, locked by another process, issue #660): also exit code 3, and the error
# line names the cause. The stand-in writes the error as dotnet does; a file really held open is not used here.
: > "$work/lock-err"
run_probe locked > /dev/null 2>"$work/lock-err"
expect 'run: locked file, exit 3'           [ $? = 3 ]
expect 'run: locked file, names the error'  grep -q 'error MSB3027' "$work/lock-err"
expect 'run: locked file, says to build'    grep -q 'build again before running anything with --no-build' "$work/lock-err"
expect 'run: locked file, file back'        probe_back

# A test process that crashes with the change (endless recursion) leaves a result file with fewer tests than the
# control run, with the default of the stand-in none at all: that is no "SURVIVED" (issue #657).
run_out() { grep -q "$1" "$work/run-out"; }
not_in_run_out() { ! run_out "$1"; }
: > "$work/lock-err"
run_probe aborted > "$work/run-out" 2>"$work/lock-err"
expect 'run: aborted, exit 4'               [ $? = 4 ]
expect 'run: aborted, says so'              run_out 'ABORTED - only 0 of 1 test(s) ran with the change'
expect 'run: aborted, not survived'         not_in_run_out SURVIVED
expect 'run: aborted, shows the crash'      grep -q 'Test host process crashed' "$work/lock-err"
# The cause stands above the last five lines (issue #660); the path of the result file is shortened.
expect 'run: aborted, names the cause'      grep -q 'The cause:' "$work/lock-err"
expect 'run: aborted, path shortened'       grep -q -E 'Results File: [.]{3}[\\/]results[.]trx' "$work/lock-err"
expect 'run: aborted, no temp folder shown' lacks_regex '[\\/]mutated[\\/]results[.]trx'
expect 'run: aborted, file back'            probe_back
expect 'run: aborted, lock gone'            [ ! -e "$lock_of_repo" ]
expect 'run: aborted, says to build again'  grep -q 'still holds the changed code' "$work/lock-err"
# A crash is no time-out (issue #712), although the blame collector of the limit is on.
expect 'run: aborted, not timed out'        not_in_run_out 'TIMED OUT'

: > "$work/lock-err"
( export STANDIN_TOTAL=3; run_probe aborted ) > "$work/run-out" 2>"$work/lock-err"
expect 'run: fewer than control, exit 4'    [ $? = 4 ]
expect 'run: fewer than control, says so'   run_out 'ABORTED - only 2 of 3 test(s) ran'

: > "$work/lock-err"
( export STANDIN_TOTAL=3; run_probe 'aborted broken' ) > "$work/run-out" 2>"$work/lock-err"
expect 'run: aborted with red, exit 4'      [ $? = 4 ]
expect 'run: aborted with red, names it'    run_out 'Probe.Test'
expect 'run: aborted with red, file back'   probe_back

# The cause lines (issue #660): each once - also two that differ only in their path -, at most five.
cause_lines() { awk '/The cause:/ { on = 1; next } /The end of its output:/ { on = 0 } on' "$work/lock-err"; }
: > "$work/lock-err"
run_probe crashmany > "$work/run-out" 2>"$work/lock-err"
expect 'run: many causes, exit 4'           [ $? = 4 ]
expect 'run: many causes, at most five'     [ "$(cause_lines | wc -l | tr -d ' ')" = 5 ]
expect 'run: many causes, each once'        [ "$(cause_lines | grep -c 'Stack overflow')" = 1 ]
expect 'run: many causes, first ones kept'  [ "$(cause_lines | tail -n 1 | tr -d ' ')" = Probe4aborted ]

# Fewer tests without any crash text: no cause section, only the end of the output.
: > "$work/lock-err"
run_probe fewer > "$work/run-out" 2>"$work/lock-err"
expect 'run: fewer, no crash text, exit 4'  [ $? = 4 ]
expect 'run: fewer, no cause section'       lacks 'The cause:'
expect 'run: fewer, shows the end'          grep -q 'away. The end of its output:' "$work/lock-err"

# --timeout (issue #712): every test run gets the limit, 60 s if none is given; 0 gives none, as before the option.
# The stand-in writes the arguments of each run into "args", one line per run (control run and run with the change).
run_with() { local replace="$1"; shift; ( cd "$repo" && PATH="$work/stand-in:$PATH" bash "$gegenprobe" "$@" probe.txt good "$replace" Probe ); }
runs_with() { [ "$(grep -c -F -e "$1" "$work/args")" = "$2" ]; }
fresh_args() { : > "$work/args"; : > "$work/lock-err"; }
export STANDIN_ARGS="$work/args"

fresh_args
run_with broken > "$work/run-out" 2>"$work/lock-err"
expect 'timeout: default, exit 0'           [ $? = 0 ]
expect 'timeout: default, 60 s in each run' runs_with '--blame-hang-timeout 60s --blame-hang-dump-type none' 2

fresh_args
run_with broken --timeout 5 > "$work/run-out" 2>"$work/lock-err"
expect 'timeout: 5, exit 0'                 [ $? = 0 ]
expect 'timeout: 5, 5 s in each run'        runs_with '--blame-hang-timeout 5s --blame-hang-dump-type none' 2

fresh_args
run_with broken --timeout 0 > "$work/run-out" 2>"$work/lock-err"
expect 'timeout: 0, exit 0'                 [ $? = 0 ]
expect 'timeout: 0, two runs'               [ "$(wc -l < "$work/args" | tr -d ' ')" = 2 ]
expect 'timeout: 0, no limit'               runs_with '--blame-hang' 0

# A test that loops forever with the change: ended after the limit, exit code 4, the test named, all put back.
fresh_args
run_with hang > "$work/run-out" 2>"$work/lock-err"
expect 'timeout: hang, exit 4'              [ $? = 4 ]
expect 'timeout: hang, says so'             run_out 'TIMED OUT - a test ran longer than 60 s with the change'
expect 'timeout: hang, names the test'      grep -q -x '     Probe.Test' "$work/run-out"
expect 'timeout: hang, counts the rest'     run_out '0 of 1 test(s) finished before'
expect 'timeout: hang, not aborted'         not_in_run_out ABORTED
expect 'timeout: hang, not survived'        not_in_run_out SURVIVED
expect 'timeout: hang, names the way out'   grep -q -e 'raise the limit (--timeout 300) or pass --timeout 0' "$work/lock-err"
expect 'timeout: hang, file back'           probe_back
expect 'timeout: hang, lock gone'           [ ! -e "$lock_of_repo" ]
expect 'timeout: hang, working folder clean' [ -z "$(git -C "$repo" status --porcelain)" ]
expect 'timeout: hang, says to build again' grep -q 'still holds the changed code' "$work/lock-err"

fresh_args
run_with hang --timeout 7 > "$work/run-out" 2>"$work/lock-err"
expect 'timeout: hang at 7, exit 4'         [ $? = 4 ]
expect 'timeout: hang at 7, names it'       run_out 'TIMED OUT - a test ran longer than 7 s'

# dotnet in German, as on the machine of this project.
fresh_args
( export STANDIN_GERMAN=1; run_with hang ) > "$work/run-out" 2>"$work/lock-err"
expect 'timeout: hang german, exit 4'       [ $? = 4 ]
expect 'timeout: hang german, says so'      run_out 'TIMED OUT'
expect 'timeout: hang german, names test'   grep -q -x '     Probe.Test' "$work/run-out"

# No sequence file of the blame collector: still timed out, the test unknown.
fresh_args
( export STANDIN_NO_SEQUENCE=1; run_with hang ) > "$work/run-out" 2>"$work/lock-err"
expect 'timeout: no sequence, exit 4'       [ $? = 4 ]
expect 'timeout: no sequence, test unknown' grep -q -x '     ?' "$work/run-out"

# Red tests before the run was ended are named.
fresh_args
( export STANDIN_TOTAL=3; run_with 'hang tworeds' ) > "$work/run-out" 2>"$work/lock-err"
expect 'timeout: hang with red, exit 4'     [ $? = 4 ]
expect 'timeout: hang with red, names it'   run_out 'red before the run ended'
expect 'timeout: hang with red, the test'   grep -q -x '     Probe.Zeta' "$work/run-out"

# The control run hangs already: exit code 2, nothing changed.
fresh_args
( export STANDIN_HANG=1; run_with broken ) > "$work/run-out" 2>"$work/lock-err"
expect 'timeout: control hangs, exit 2'     [ $? = 2 ]
expect 'timeout: control hangs, says so'    grep -q 'control run already has a test that ran longer than 60 s' "$work/lock-err"
expect 'timeout: control hangs, the test'   grep -q -x '   Probe.Test' "$work/lock-err"
expect 'timeout: control hangs, one run'    [ "$(wc -l < "$work/args" | tr -d ' ')" = 1 ]
expect 'timeout: control hangs, file back'  probe_back
expect 'timeout: control hangs, lock gone'  [ ! -e "$lock_of_repo" ]

# Wrong values and --bot: exit code 2 before anything runs.
fresh_args
run_with broken --timeout abc > "$work/run-out" 2>"$work/lock-err"
expect 'timeout: abc, exit 2'               [ $? = 2 ]
expect 'timeout: abc, says so'              grep -q -e "--timeout needs a whole number of seconds (0 = no limit), not 'abc'" "$work/lock-err"
expect 'timeout: abc, nothing ran'          [ ! -s "$work/args" ]
fresh_args
run_with broken --timeout '' > "$work/run-out" 2>"$work/lock-err"
expect 'timeout: empty, exit 2'             [ $? = 2 ]
fresh_args
run_with broken --timeout -5 > "$work/run-out" 2>"$work/lock-err"
expect 'timeout: negative, exit 2'          [ $? = 2 ]
fresh_args
run_with broken --bot --timeout 5 > "$work/run-out" 2>"$work/lock-err"
expect 'timeout: with --bot, exit 2'        [ $? = 2 ]
expect 'timeout: with --bot, says so'       grep -q -e '--timeout limits a test of dotnet test - the bot run has no such limit' "$work/lock-err"
expect 'timeout: with --bot, file back'     probe_back
unset STANDIN_ARGS

# A user whose folder name holds a space (issue #660): neither the Windows nor the Git Bash form of his folders may
# show his name. Home, user folder and temp folder of the run are set to such a folder.
spaced_home="$work/Max Mustermann"
mkdir -p -- "$spaced_home/AppData/Local/Temp"
as_spaced_user() {
    (
        export HOME="$spaced_home"
        USERPROFILE="$(cygpath -w "$spaced_home" 2>/dev/null || echo "$spaced_home")"
        TEMP="$(cygpath -w "$spaced_home/AppData/Local/Temp" 2>/dev/null || echo "$spaced_home/AppData/Local/Temp")"
        export USERPROFILE TEMP TMP="$TEMP" TMPDIR="$spaced_home/AppData/Local/Temp"
        "$@"
    )
}
: > "$work/lock-err"
as_spaced_user run_probe aborted > "$work/run-out" 2>"$work/lock-err"
expect 'run: spaced user, exit 4'           [ $? = 4 ]
expect 'run: spaced user, windows form'     lacks 'Mustermann'
expect 'run: spaced user, still the file'   grep -q 'results[.]trx' "$work/lock-err"
expect 'run: spaced user, file back'        probe_back
: > "$work/lock-err"
( export STANDIN_UNIX_PATH=1; as_spaced_user run_probe aborted ) > "$work/run-out" 2>"$work/lock-err"
expect 'run: spaced user, git bash, exit 4' [ $? = 4 ]
expect 'run: spaced user, git bash form'    lacks 'Mustermann'
expect 'run: spaced user, git bash file'    grep -q 'results[.]trx' "$work/lock-err"

# The output closed early (2>&1 | head, as in the test of PR #648): head stops after the line of the control run, so
# the result line and the messages of the clean-up meet a closed pipe while the file is changed. The run must still
# put the file back and let go of the lock.
: > "$work/lock-err"
run_probe broken 2>&1 | head -n 4 > /dev/null
expect 'run: | head, file back'             probe_back
expect 'run: | head, lock gone'             [ ! -e "$lock_of_repo" ]
expect 'run: | head, working folder clean'  [ -z "$(git -C "$repo" status --porcelain)" ]

# --message (issue #682): when caught, the message of the first red test as listed, decoded, paths shortened.
run_message_probe() { ( cd "$repo" && PATH="$work/stand-in:$PATH" bash "$gegenprobe" --message probe.txt good "$1" Probe ); }
# perl, not awk or sed: those drop a CR in Git Bash, and "no CR left" would then never fail.
message_lines() { perl -ne 'print if $on; $on = 1 if /message of the first red test/' "$work/run-out"; }
message_has() { message_lines | grep -q -F -- "$1"; }
# A message line that begins with $1 (plain words, no regex signs) right after the indent.
message_line_starts() { message_lines | grep -q -- "^     $1"; }
: > "$work/lock-err"
run_message_probe broken > "$work/run-out" 2>"$work/lock-err"
expect 'message: caught, exit 0'            [ $? = 0 ]
expect 'message: caught, names the test'    run_out 'message of the first red test (Probe.Test):'
expect 'message: caught, signs decoded'     message_has 'Probe: value is "broken" <not good> &amp; red'
expect 'message: caught, &#xA; is a line'   message_line_starts 'second line in'
expect 'message: caught, CRLF is a line'    [ "$(message_lines | tail -n 1)" = '     third line' ]
expect 'message: caught, no CR left'        [ -z "$(message_lines | tr -d -c '\r')" ]
expect 'message: caught, path shortened'    message_has '...\Probe.cs:line 3'
expect 'message: caught, no folder shown'   not_in_run_out 'Work\\Folder'
expect 'message: caught, file back'         probe_back
expect 'message: caught, lock gone'         [ ! -e "$lock_of_repo" ]

: > "$work/lock-err"
run_probe broken > "$work/run-out" 2>"$work/lock-err"
expect 'message: not asked, not shown'      not_in_run_out 'message of the first red test'

: > "$work/lock-err"
run_message_probe 'broken tworeds' > "$work/run-out" 2>"$work/lock-err"
expect 'message: two red, exit 0'           [ $? = 0 ]
expect 'message: two red, first as listed'  run_out 'message of the first red test (Probe.Test):'
expect 'message: two red, its message'      message_has 'value is "broken"'
expect 'message: two red, not the other'    not_in_run_out 'the message of Probe.Zeta'

: > "$work/lock-err"
run_message_probe 'broken longmessage' > "$work/run-out" 2>"$work/lock-err"
expect 'message: long, exit 0'              [ $? = 0 ]
expect 'message: long, thirty lines'        [ "$(message_lines | grep -c -E '^ +[0-9]+$')" = 30 ]
expect 'message: long, counts the rest'     [ "$(message_lines | tail -n 1)" = '     ... and 5 more line(s)' ]

# The cut itself: exactly 30 lines show all and add nothing, 31 count the one beyond.
: > "$work/lock-err"
run_message_probe 'broken lines30' > "$work/run-out" 2>"$work/lock-err"
expect 'message: 30 lines, exit 0'          [ $? = 0 ]
expect 'message: 30 lines, all shown'       [ "$(message_lines | grep -c -E '^ +[0-9]+$')" = 30 ]
expect 'message: 30 lines, nothing counted' not_in_run_out 'more line(s)'
: > "$work/lock-err"
run_message_probe 'broken lines31' > "$work/run-out" 2>"$work/lock-err"
expect 'message: 31 lines, exit 0'          [ $? = 0 ]
expect 'message: 31 lines, thirty shown'    [ "$(message_lines | grep -c -E '^ +[0-9]+$')" = 30 ]
expect 'message: 31 lines, counts one'      [ "$(message_lines | tail -n 1)" = '     ... and 1 more line(s)' ]

: > "$work/lock-err"
run_message_probe 'broken nomessage' > "$work/run-out" 2>"$work/lock-err"
expect 'message: none, exit 0'              [ $? = 0 ]
expect 'message: none, says so'             run_out '(the result file holds no message for it)'

: > "$work/lock-err"
run_message_probe fine > "$work/run-out" 2>"$work/lock-err"
expect 'message: survived, exit 1'          [ $? = 1 ]
expect 'message: survived, nothing shown'   not_in_run_out 'message of the first red test'

: > "$work/lock-err"
( cd "$repo" && PATH="$work/stand-in:$PATH" bash "$gegenprobe" --message --bot probe.txt good broken 'probe --seeds 1' ) \
    > "$work/run-out" 2>"$work/lock-err"
expect 'message: with --bot, exit 2'        [ $? = 2 ]
expect 'message: with --bot, says so'       grep -q -e '--message shows the message of a red test' "$work/lock-err"
expect 'message: with --bot, file back'     probe_back

# The same with --bot (issue #647): one case per way out of run_bot and check.
run_bot_probe() { ( cd "$repo" && PATH="$work/stand-in:$PATH" bash "$gegenprobe" --bot probe.txt good "$1" "$2" ); }
said() { grep -q "$1" "$work/bot-out"; }

: > "$work/lock-err"
run_bot_probe broken 'probe --seeds 1' > "$work/bot-out" 2>"$work/lock-err"
expect 'bot: caught, exit 0'                [ $? = 0 ]
expect 'bot: caught, names the finding'     said 'FINDING \[probe\] value is broken'
expect 'bot: caught, names the bot exit'    said 'CAUGHT - the bot reports findings (bot exit code 1)'
expect 'bot: control, names the bot exit'   said 'control run: bot without findings (bot exit code 0)'
expect 'bot: caught, file back'             probe_back
expect 'bot: caught, lock gone'             [ ! -e "$lock_of_repo" ]
expect 'bot: caught, says to build again'   grep -q 'still holds the changed code' "$work/lock-err"

: > "$work/lock-err"
run_bot_probe fine 'probe --seeds 1' > "$work/bot-out" 2>"$work/lock-err"
expect 'bot: survived, exit 1'              [ $? = 1 ]
expect 'bot: survived, says so'             said 'SURVIVED - the bot reports no finding'
expect 'bot: survived, names the bot exit'  said 'no finding with the change (bot exit code 0)'
expect 'bot: survived, file back'           probe_back

: > "$work/lock-err"
run_bot_probe broken 'probe found' > "$work/bot-out" 2>"$work/lock-err"
expect 'bot: control finding, exit 2'       [ $? = 2 ]
expect 'bot: control finding, says so'      grep -q 'control run of the bot already reports findings' "$work/lock-err"
expect 'bot: control finding, file back'    probe_back

: > "$work/lock-err"
run_bot_probe broken 'probe crash' > "$work/bot-out" 2>"$work/lock-err"
expect 'bot: control exit 2, exit 2'        [ $? = 2 ]
expect 'bot: control exit 2, says so'       grep -q "the bot could not run with 'probe crash'" "$work/lock-err"
expect 'bot: control exit 2, file back'     probe_back

: > "$work/lock-err"
run_bot_probe crash 'probe --seeds 1' > "$work/bot-out" 2>"$work/lock-err"
expect 'bot: changed exit 2, exit 2'        [ $? = 2 ]
expect 'bot: changed exit 2, says so'       grep -q 'the bot could not run with the change' "$work/lock-err"
expect 'bot: changed exit 2, file back'     probe_back
expect 'bot: changed exit 2, lock gone'     [ ! -e "$lock_of_repo" ]

# The bot ends with another code than 0, 1 or 2 (a crash, 134): the script ends with 2 and names the real code.
: > "$work/lock-err"
run_bot_probe explode 'probe --seeds 1' > "$work/bot-out" 2>"$work/lock-err"
expect 'bot: changed exit 134, exit 2'      [ $? = 2 ]
expect 'bot: changed exit 134, names it'    grep -q 'could not run with the change (bot exit code 134)' "$work/lock-err"
expect 'bot: changed exit 134, file back'   probe_back

: > "$work/lock-err"
run_bot_probe nocompile 'probe --seeds 1' > "$work/bot-out" 2>"$work/lock-err"
expect 'bot: no compile, exit 3'            [ $? = 3 ]
expect 'bot: no compile, names the error'   grep -q 'error CS1002' "$work/lock-err"
expect 'bot: no compile, no false claim'    lacks 'still holds the changed code'
expect 'bot: no compile, file back'         probe_back

# The control run of the bot does not build (issue #660): exit code 2, before anything is changed.
: > "$work/lock-err"
( export STANDIN_BUILD_FAILS=1; run_bot_probe broken 'probe --seeds 1' ) > "$work/bot-out" 2>"$work/lock-err"
expect 'bot: control no build, exit 2'      [ $? = 2 ]
expect 'bot: control no build, says so'     grep -q 'control run does not build' "$work/lock-err"
expect 'bot: control no build, names it'    grep -q 'error CS1002' "$work/lock-err"
expect 'bot: control no build, file back'   probe_back
expect 'bot: control no build, lock gone'   [ ! -e "$lock_of_repo" ]

: > "$work/lock-err"
run_bot_probe broken 'probe --seeds 1' 2>&1 | head -n 4 > /dev/null
expect 'bot: | head, file back'             probe_back
expect 'bot: | head, lock gone'             [ ! -e "$lock_of_repo" ]
expect 'bot: | head, working folder clean'  [ -z "$(git -C "$repo" status --porcelain)" ]

echo "gegenprobe.tests: $passed passed, $failed failed"
[ "$failed" = 0 ]
