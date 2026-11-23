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

echo "gegenprobe.tests: $passed passed, $failed failed"
[ "$failed" = 0 ]
