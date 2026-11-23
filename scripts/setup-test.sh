#!/bin/sh
# mise の setup タスクのテスト。デモを動かすのに要るものを、頼る順に作るかを見る。
set -eu
cd "$(dirname "$0")/.."
fail=0

run="$(mise tasks info setup --json | sed -n '/"run": \[/,/\]/{/"mise run /s/.*"mise run \([^"]*\)".*/\1/p;}')"
expected="build-weapons
build-rules
build-abilities
build-scenes
export-server-rules"
if [ "$run" != "$expected" ]; then
	printf 'FAIL setup runs the demo tasks in order: got\n%s\nexpected\n%s\n' "$run" "$expected"
	fail=1
fi

depends="$(mise tasks info setup --json | sed -n '/"depends": \[/,/\]/{/"depends"/d;/^ *"/s/^ *"\([^"]*\)".*/\1/p;}')"
if [ "$depends" != "install" ]; then
	printf 'FAIL setup depends on install: got %s\n' "$depends"
	fail=1
fi

for task in $expected; do
	if ! mise tasks info "$task" >/dev/null 2>&1; then
		printf 'FAIL setup runs %s, which is not a task\n' "$task"
		fail=1
	fi
done

[ "$fail" = 0 ] && echo "setup-test: all passed"
exit "$fail"
