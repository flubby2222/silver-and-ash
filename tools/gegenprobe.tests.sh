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

# The lock (tools/gegenprobe-lock.sh, issue #639). A contender is a run of its own: it takes the lock, writes its
# number into "won", holds the lock <hold> seconds and lets it go. Its exit code is the one of lock_take.
lockfile="$(cd "$(dirname "$0")" && pwd)/gegenprobe-lock.sh"
contender() {
    local hold="$1" pause="$2"
    GEGENPROBE_TEST_PAUSE_BEFORE_TAKEOVER="$pause" bash -c '
        . "$1" || exit 9
        lock_take "$2" || exit $?
        echo $$ >> "$3"
        sleep "$4"
        lock_release' contender "$lockfile" "$work/lock" "$work/won" "$hold" 2>>"$work/lock-err"
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
    rm -rf -- "$work/lock" "$work/lock".* "$work/won" "$work/lock-err"
    : > "$work/won"
    : > "$work/lock-err"
}
winners() { [ "$(wc -l < "$work/won" | tr -d ' ')" = "$1" ]; }
no_lock_left() { [ -z "$(ls -d -- "$work/lock" "$work/lock".* 2>/dev/null)" ]; }
stale_lock() { mkdir -- "$work/lock" && echo "$dead" > "$work/lock/pid"; }

fresh_lock
contender 0 ''
expect 'lock: free lock is taken'           [ $? = 0 ]
expect 'lock: free lock, one winner'        winners 1
expect 'lock: let go after the run'         no_lock_left

fresh_lock
mkdir -- "$work/lock" && echo $$ > "$work/lock/pid"
contender 0 ''
expect 'lock: running owner, exit 2'        [ $? = 2 ]
expect 'lock: running owner, no winner'     winners 0
expect 'lock: running owner keeps it'       [ "$(cat "$work/lock/pid")" = $$ ]

fresh_lock
mkdir -- "$work/lock"
contender 0 ''
expect 'lock: without a number, exit 2'     [ $? = 2 ]
expect 'lock: without a number, kept'       [ -d "$work/lock" ]

fresh_lock
stale_lock
contender 0 ''
expect 'lock: stale lock is taken over'     [ $? = 0 ]
expect 'lock: stale lock, one winner'       winners 1
expect 'lock: stale lock, says so'          grep -q "removing the stale lock of run $dead" "$work/lock-err"
expect 'lock: stale lock, nothing left'     no_lock_left

# Two runs find the same stale lock. The first takes it over after 1 s and holds it for 4 s; the second goes on only
# after 2.5 s - while the first is holding its fresh lock. Exactly one may win, and the lock of the first must stay.
fresh_lock
stale_lock
contender 4 1 &
first=$!
contender 0 2.5 &
second=$!
sleep 3.2
expect 'lock: race, the first holds it'     [ "$(cat "$work/lock/pid" 2>/dev/null)" = "$(head -n 1 "$work/won")" ]
wait "$first"
first_exit=$?
wait "$second"
second_exit=$?
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
# "nocompile". As the bot (--bot, issue #647) it answers "build" (fails on "nocompile") and "run": a finding (exit 1)
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
        if grep -q nocompile probe.txt; then
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
        if grep -q crash probe.txt; then echo "the tool failed"; exit 2; fi
        if grep -q broken probe.txt; then echo "FINDING [probe] value is broken"; exit 1; fi
        echo "SUMMARY mode probe: 0 finding(s)"
        exit 0 ;;
esac
results=""
while [ $# -gt 0 ]; do
    [ "$1" = --results-directory ] && results="$2"
    shift
done
sleep 1
if grep -q nocompile probe.txt; then
    echo "probe.txt(1,9): error CS1002: ; expected"
    exit 1
fi
outcome=Passed
grep -q broken probe.txt && outcome=Failed
failed=0
[ "$outcome" = Failed ] && failed=1
mkdir -p -- "$results"
printf '<TestRun><Counters total="1" failed="%s" /><UnitTestResult testName="Probe.Test" outcome="%s" /></TestRun>\n' \
    "$failed" "$outcome" > "$results/results.trx"
STANDIN
chmod +x "$work/stand-in/dotnet"
lock_of_repo="$repo/.git/gegenprobe.lock"
lacks() { ! grep -q "$1" "$work/lock-err"; }
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

# The output closed early (2>&1 | head, as in the test of PR #648): head stops after the line of the control run, so
# the result line and the messages of the clean-up meet a closed pipe while the file is changed. The run must still
# put the file back and let go of the lock.
: > "$work/lock-err"
run_probe broken 2>&1 | head -n 4 > /dev/null
expect 'run: | head, file back'             probe_back
expect 'run: | head, lock gone'             [ ! -e "$lock_of_repo" ]
expect 'run: | head, working folder clean'  [ -z "$(git -C "$repo" status --porcelain)" ]

# The same with --bot (issue #647): one case per way out of run_bot and check.
run_bot_probe() { ( cd "$repo" && PATH="$work/stand-in:$PATH" bash "$gegenprobe" --bot probe.txt good "$1" "$2" ); }
said() { grep -q "$1" "$work/bot-out"; }

: > "$work/lock-err"
run_bot_probe broken 'probe --seeds 1' > "$work/bot-out" 2>"$work/lock-err"
expect 'bot: caught, exit 0'                [ $? = 0 ]
expect 'bot: caught, names the finding'     said 'FINDING \[probe\] value is broken'
expect 'bot: caught, file back'             probe_back
expect 'bot: caught, lock gone'             [ ! -e "$lock_of_repo" ]
expect 'bot: caught, says to build again'   grep -q 'still holds the changed code' "$work/lock-err"

: > "$work/lock-err"
run_bot_probe fine 'probe --seeds 1' > "$work/bot-out" 2>"$work/lock-err"
expect 'bot: survived, exit 1'              [ $? = 1 ]
expect 'bot: survived, says so'             said 'SURVIVED - the bot reports no finding'
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

: > "$work/lock-err"
run_bot_probe nocompile 'probe --seeds 1' > "$work/bot-out" 2>"$work/lock-err"
expect 'bot: no compile, exit 3'            [ $? = 3 ]
expect 'bot: no compile, names the error'   grep -q 'error CS1002' "$work/lock-err"
expect 'bot: no compile, no false claim'    lacks 'still holds the changed code'
expect 'bot: no compile, file back'         probe_back

: > "$work/lock-err"
run_bot_probe broken 'probe --seeds 1' 2>&1 | head -n 4 > /dev/null
expect 'bot: | head, file back'             probe_back
expect 'bot: | head, lock gone'             [ ! -e "$lock_of_repo" ]
expect 'bot: | head, working folder clean'  [ -z "$(git -C "$repo" status --porcelain)" ]

echo "gegenprobe.tests: $passed passed, $failed failed"
[ "$failed" = 0 ]
