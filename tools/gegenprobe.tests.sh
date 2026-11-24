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

echo "gegenprobe.tests: $passed passed, $failed failed"
[ "$failed" = 0 ]
