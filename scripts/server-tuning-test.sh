#!/bin/sh
# scripts/server-tuning.sh のテスト。一時ディレクトリに Resources とサーバーの tuning と一覧を作って照合させる。
set -eu
cd "$(dirname "$0")/.."
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
fail=0

# $1: テストの名前、$2: 期待する終了コード、$3: 標準エラーに出るはずの文字列 (空なら何も出ないこと)
expect() {
	status=0
	err="$(sh scripts/server-tuning.sh "$work/resources" "$work/server" "$work/list" 2>&1)" || status=$?
	if [ "$status" != "$2" ]; then
		printf 'FAIL %s: exit %s, expected %s\n%s\n' "$1" "$status" "$2" "$err"
		fail=1
	elif [ -z "$3" ] && [ -n "$err" ]; then
		printf 'FAIL %s: expected no output\n%s\n' "$1" "$err"
		fail=1
	elif [ -n "$3" ] && ! printf '%s' "$err" | grep -qF "$3"; then
		printf 'FAIL %s: expected output containing %s\n%s\n' "$1" "$3" "$err"
		fail=1
	fi
}

reset() {
	rm -rf "$work/resources" "$work/server"
	mkdir -p "$work/resources" "$work/server"
	: > "$work/list"
}

reset
printf '{ "a": 1 }\n' > "$work/resources/ping.json"
cp "$work/resources/ping.json" "$work/server/ping.json"
expect "identical copies pass" 0 ""

reset
printf '{ "a": 1 }\n' > "$work/resources/ping.json"
printf '{ "a": 2 }\n' > "$work/server/ping.json"
expect "a differing copy fails" 1 "$work/server/ping.json differs from $work/resources/ping.json"

reset
printf '{ "startMoney": 800, "items": [] }\n' > "$work/resources/economy.json"
printf '{ "items": [] }\n' > "$work/server/economy.json"
printf '{ "words": [] }\n' > "$work/resources/chat-words.json"
printf '{ "words": ["noob"] }\n' > "$work/server/chat-words.json"
printf 'server-owned economy.json\nserver-owned chat-words.json # the server keeps its own words\n' > "$work/list"
expect "listed server-owned files may differ" 0 ""

reset
printf '{}\n' > "$work/server/anticheat.json"
printf '# the server alone reads it\nserver-only anticheat.json\n' > "$work/list"
expect "a listed server-only file passes" 0 ""

reset
printf '{}\n' > "$work/server/anticheat.json"
expect "an unlisted server-only file fails" 1 "$work/server/anticheat.json has no $work/resources/anticheat.json; copy it there, or list it as server-only in $work/list"

reset
printf '{}\n' > "$work/resources/telemetry.json"
printf 'resources-only telemetry.json\n' > "$work/list"
expect "a listed Resources-only file passes" 0 ""

reset
printf '{}\n' > "$work/resources/holster.json"
expect "an unlisted Resources-only file fails" 1 "$work/resources/holster.json has no $work/server/holster.json; run make export-server-rules (or copy the Resources file) and commit it, or list it as resources-only in $work/list"

reset
printf '{}\n' > "$work/resources/holster.json"
printf '{}\n' > "$work/server/anticheat.json"
printf 'server-only holster.json\nresources-only anticheat.json\n# resources-only holster.json\n' > "$work/list"
expect "a file listed under the other kind or in a comment still fails" 1 "$work/resources/holster.json has no $work/server/holster.json"

reset
printf '{ "a": 1 }\n' > "$work/resources/ping.json"
printf '{ "a": 2 }\n' > "$work/server/ping.json"
printf 'server-only ping.json\nresources-only ping.json\n' > "$work/list"
expect "a file on both sides is compared even when listed as one-sided" 1 "$work/server/ping.json differs from $work/resources/ping.json"

reset
expect "empty directories pass" 0 ""

reset
rm "$work/list"
expect "a missing list fails" 1 "$work/list is missing"

status=0
sh scripts/server-tuning.sh >/dev/null 2>&1 || status=$?
if [ "$status" != 0 ]; then
	printf 'FAIL the committed server-data/tuning matches Resources/Tuning\n'
	sh scripts/server-tuning.sh || true
	fail=1
fi

exit "$fail"
