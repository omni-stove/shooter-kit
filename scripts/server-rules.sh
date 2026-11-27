#!/bin/sh
# server-data/rules の *.json がどれも、make export-server-rules (ServerRulesExporter) が書き出すルールか、
# 手で保つルールの一覧 (server-data/rules/hand-written.txt) にあるルールかを確かめる。どちらでもないファイルは誰も作り直さず、古いまま残る。
# 書き出すのは、試合の設定 (NetMatch.asset) の並びにあるルールアセットと、Objective が None でないルールアセット (ServerRulesExporter.ModeRulesToExport)。
# 引数でディレクトリを替えられるのは、scripts/server-rules-test.sh が作ったディレクトリで確かめるため。
# 使い方: sh scripts/server-rules.sh [<Demo の Rules> <server-data/rules>]
set -eu
cd "$(dirname "$0")/.."
assets="${1:-Assets/kits/shooter/Samples~/Demo/Rules}"
server="${2:-server-data/rules}"
list="$server/hand-written.txt"
match="$assets/NetMatch.asset"

is_hand_written() {
	[ -f "$list" ] || return 1
	sed 's/#.*//' "$list" | awk -v name="$1" '$1 == name { found = 1 } END { exit found ? 0 : 1 }'
}

has_objective() {
	awk '$1 == "_objective:" && $2 != "0" { found = 1 } END { exit found ? 0 : 1 }' "$assets/$1.asset"
}

is_in_match() {
	[ -f "$match" ] && [ -f "$assets/$1.asset.meta" ] || return 1
	guid="$(awk '$1 == "guid:" { print $2; exit }' "$assets/$1.asset.meta")"
	[ -n "$guid" ] || return 1
	awk -v guid="$guid" '
		/^  _rules:/ { inside = 1; next }
		inside && !/^  - / { inside = 0 }
		inside && index($0, "guid: " guid ",") { found = 1 }
		END { exit found ? 0 : 1 }' "$match"
}

is_exported() {
	[ -f "$assets/$1.asset" ] || return 1
	has_objective "$1" || is_in_match "$1"
}

fail=0
for path in "$server"/*.json; do
	[ -e "$path" ] || continue
	name="$(basename "$path" .json)"
	is_hand_written "$name" && continue
	if ! is_exported "$name"; then
		printf '%s is not written by make export-server-rules (%s is missing, or has the objective None and is not listed in %s); delete it, or list %s in %s and keep it by hand\n' \
			"$path" "$assets/$name.asset" "$match" "$name" "$list" >&2
		fail=1
	fi
done
if [ -f "$list" ]; then
	for name in $(sed 's/#.*//' "$list" | awk 'NF { print $1 }'); do
		if [ ! -e "$server/$name.json" ]; then
			printf '%s lists %s, but %s is missing; remove the line\n' "$list" "$name" "$server/$name.json" >&2
			fail=1
		fi
	done
fi
exit "$fail"
