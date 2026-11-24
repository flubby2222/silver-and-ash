# The lock of tools/gegenprobe.sh: only one run per working folder at a time (issues #604, #639). Sourced by
# gegenprobe.sh; tools/gegenprobe.tests.sh checks it, also with two runs taking over the same stale lock together.
#
#   lock_take <lock folder>   0 = the lock is ours; 2 = another run has it (the reason is on stderr)
#   lock_release              removes the lock again, but only while it is still ours
#
# The lock is a folder with a file `pid` (the process number of the run that holds it); mkdir makes it, and of two
# runs only one succeeds. A lock without that number counts as held (its run may be just writing it). A lock whose run
# is gone (killed hard, machine restarted) is stale: a run renames it out of the way first - of two runs only one can
# rename the same folder (mv -T) - and checks that it really got the stale one before it deletes it. If it got the
# fresh lock of a run that was quicker, it gives it back.
#
# Known limit (issue #651): if three runs meet in exactly that gap, the third may rename the fresh lock of the second
# while it is still empty (no pid yet), as mv -T replaces an empty folder. Closing this would need a rename that fails
# on an existing folder, which Git Bash does not offer; an empty lock must count as held. Start runs one after another.
#
# For the self-test only (issue #657: by events, not by fixed times, so it holds on a busy machine too), between
# finding a stale lock and taking it over:
#   GEGENPROBE_TEST_FOUND_STALE=<file>        makes <file> - "this run has found the stale lock"
#   GEGENPROBE_TEST_WAIT_BEFORE_TAKEOVER=<file>  waits until <file> is there (at most 60 s)
# so two runs meet in exactly that gap.

lock_path=""
lock_owned=0

# Makes the lock. mkdir is atomic: of two runs exactly one makes it. 0 = made, 1 = there is one already.
lock_create() {
    mkdir -- "$lock_path" 2>/dev/null || return 1
    lock_owned=1
    echo $$ > "$lock_path/pid"
}

lock_take() {
    lock_path="$1"
    if lock_create; then
        return 0
    fi

    local other
    other="$(cat -- "$lock_path/pid" 2>/dev/null)"
    if [ -z "$other" ] || kill -0 "$other" 2>/dev/null; then
        echo "gegenprobe: another gegenprobe is already running in this working folder (process ${other:-unknown})." >&2
        echo "   Wait until it has finished and start the probes one after the other." >&2
        echo "   If no gegenprobe is running at all (its process number was given to another program), delete" >&2
        echo "   the lock folder: rm -rf '$lock_path'" >&2
        return 2
    fi

    # The run that left the lock is gone: take the lock over.
    if [ -n "${GEGENPROBE_TEST_FOUND_STALE:-}" ]; then
        : > "$GEGENPROBE_TEST_FOUND_STALE"
    fi
    if [ -n "${GEGENPROBE_TEST_WAIT_BEFORE_TAKEOVER:-}" ]; then
        lock_test_wait_for "$GEGENPROBE_TEST_WAIT_BEFORE_TAKEOVER"
    fi
    local stale="$lock_path.stale.$$"
    rm -rf -- "$stale"
    if ! mv -T -- "$lock_path" "$stale" 2>/dev/null; then
        echo "gegenprobe: another run took the lock just now - wait until it has finished" >&2
        return 2
    fi
    if [ "$(cat -- "$stale/pid" 2>/dev/null)" != "$other" ]; then
        # Another run had taken the stale lock over already, and this was its fresh lock: give it back.
        if ! mv -T -- "$stale" "$lock_path" 2>/dev/null; then
            echo "gegenprobe: WARNING - could not give back the lock of the running run; it is at '$stale'" >&2
        fi
        echo "gegenprobe: another run took the lock just now - wait until it has finished" >&2
        return 2
    fi
    echo "gegenprobe: removing the stale lock of run $other, which is no longer running" >&2
    rm -rf -- "$stale"
    if ! lock_create; then
        echo "gegenprobe: another run took the lock just now - wait until it has finished" >&2
        return 2
    fi
    return 0
}

# For the self-test only: waits until the file $1 is there, at most 60 s - a test that goes wrong fails, it does not
# hang.
lock_test_wait_for() {
    local tries=0
    while [ ! -e "$1" ] && [ "$tries" -lt 1200 ]; do
        sleep 0.05
        tries=$((tries + 1))
    done
}

lock_release() {
    if [ "$lock_owned" != 1 ]; then
        return 0
    fi
    lock_owned=0
    if [ "$(cat -- "$lock_path/pid" 2>/dev/null)" = "$$" ]; then
        rm -rf -- "$lock_path"
    fi
}
