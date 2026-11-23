#!/bin/sh
# scripts/server-rules.sh のテスト。一時ディレクトリに Demo のルールアセットとサーバーの rules を作って照合させる。
set -eu
cd "$(dirname "$0")/.."
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
fail=0

# $1: テストの名前、$2: 期待する終了コード、$3: 標準エラーに出るはずの文字列 (空なら何も出ないこと)
expect() {
	status=0
	err="$(sh scripts/server-rules.sh "$work/assets" "$work/server" 2>&1)" || status=$?
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
	rm -rf "$work/assets" "$work/server"
	mkdir -p "$work/assets" "$work/server"
}

# $1: アセットの名前、$2: _objective の値、$3: .meta の guid
asset() {
	printf 'MonoBehaviour:\n  m_Name: %s\n  _friendlyFire: 0\n  _objective: %s\n  _minPlayers: 0\n' "$1" "$2" > "$work/assets/$1.asset"
	printf 'fileFormatVersion: 2\nguid: %s\n' "$3" > "$work/assets/$1.asset.meta"
}

# 引数: 試合の設定の並びに入れる guid
match() {
	{
		printf 'MonoBehaviour:\n  m_Name: NetMatch\n  _rules:\n'
		for guid in "$@"; do
			printf '  - {fileID: 11400000, guid: %s, type: 2}\n' "$guid"
		done
		printf '  _loadout: {fileID: 11400000, guid: 0000000000000000000000000000000f, type: 2}\n'
	} > "$work/assets/NetMatch.asset"
}

not_written="is not written by make export-server-rules"

reset
asset BattleRoyale 10 000000000000000000000000000000a1
match
printf '{}\n' > "$work/server/BattleRoyale.json"
expect "rules whose asset has an objective pass" 0 ""

reset
asset TeamDeathmatch 0 000000000000000000000000000000a2
match 000000000000000000000000000000a2
printf '{}\n' > "$work/server/TeamDeathmatch.json"
expect "rules listed in the net match pass without an objective" 0 ""

reset
asset FreeForAll 0 000000000000000000000000000000a3
match 000000000000000000000000000000a2
printf '{}\n' > "$work/server/FreeForAll.json"
expect "rules whose asset has no objective and is not in the net match fail" 1 "$work/server/FreeForAll.json $not_written"

reset
asset DefaultLoadout 0 0000000000000000000000000000000f
match
printf '{}\n' > "$work/server/DefaultLoadout.json"
expect "a guid outside the rules of the net match does not count" 1 "$work/server/DefaultLoadout.json $not_written"

reset
match
printf '{}\n' > "$work/server/Elimination.json"
expect "rules without an asset fail" 1 "$work/server/Elimination.json $not_written ($work/assets/Elimination.asset is missing"

reset
asset FreeForAll 0 000000000000000000000000000000a3
match
printf '{}\n' > "$work/server/FreeForAll.json"
printf '{}\n' > "$work/server/Elimination.json"
printf '# kept by hand\nElimination\nFreeForAll  # deathCamera KillCam\n' > "$work/server/hand-written.txt"
expect "rules listed as hand-written pass" 0 ""

reset
match
printf '{}\n' > "$work/server/Elimination.json"
printf '# Elimination\nGuardian # Elimination\n' > "$work/server/hand-written.txt"
printf '{}\n' > "$work/server/Guardian.json"
expect "a name in a comment is not listed" 1 "$work/server/Elimination.json $not_written"

reset
match
printf 'Elimination\n' > "$work/server/hand-written.txt"
expect "a listed name without its file fails" 1 "$work/server/hand-written.txt lists Elimination, but $work/server/Elimination.json is missing; remove the line"

reset
expect "empty directories pass" 0 ""

status=0
sh scripts/server-rules.sh >/dev/null 2>&1 || status=$?
if [ "$status" != 0 ]; then
	printf 'FAIL every committed server-data/rules file is exported or listed as hand-written\n'
	sh scripts/server-rules.sh || true
	fail=1
fi

exit "$fail"
