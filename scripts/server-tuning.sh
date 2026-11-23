#!/bin/sh
# server-data/tuning の JSON が、同じ名前の Assets/game/Resources/Tuning の JSON と同じ中身かを確かめる。
# 両方にあるファイルは Resources を正とし、サーバーへは make export-server-rules (ServerRulesExporter) で写す。
# 片方にしか無いファイルと、名前は同じでもサーバー用に別の中身を持つファイルは、一覧 (scripts/server-tuning.txt) に書いたものだけを通す。
# 引数でディレクトリと一覧を替えられるのは、scripts/server-tuning-test.sh が作ったもので確かめるため。
# 使い方: sh scripts/server-tuning.sh [<Resources/Tuning> <server-data/tuning> [<一覧>]]
set -eu
cd "$(dirname "$0")/.."
resources="${1:-Assets/game/Resources/Tuning}"
server="${2:-server-data/tuning}"
list="${3:-scripts/server-tuning.txt}"

if [ ! -f "$list" ]; then
	printf '%s is missing; it lists the tuning files kept on one side only\n' "$list" >&2
	exit 1
fi

# $1: 種類 (server-owned・server-only・resources-only)、$2: ファイル名
is_listed() {
	sed 's/#.*//' "$list" | awk -v kind="$1" -v name="$2" '$1 == kind && $2 == name { found = 1 } END { exit found ? 0 : 1 }'
}

fail=0
for path in "$server"/*.json; do
	[ -e "$path" ] || continue
	name="$(basename "$path")"
	if [ ! -e "$resources/$name" ]; then
		if ! is_listed server-only "$name"; then
			printf '%s has no %s; copy it there, or list it as server-only in %s\n' "$path" "$resources/$name" "$list" >&2
			fail=1
		fi
		continue
	fi
	is_listed server-owned "$name" && continue
	if ! cmp -s "$resources/$name" "$path"; then
		printf '%s differs from %s; run make export-server-rules (or copy the Resources file) and commit it\n' "$path" "$resources/$name" >&2
		fail=1
	fi
done
for path in "$resources"/*.json; do
	[ -e "$path" ] || continue
	name="$(basename "$path")"
	[ -e "$server/$name" ] && continue
	if ! is_listed resources-only "$name"; then
		printf '%s has no %s; run make export-server-rules (or copy the Resources file) and commit it, or list it as resources-only in %s\n' "$path" "$server/$name" "$list" >&2
		fail=1
	fi
done
exit "$fail"
