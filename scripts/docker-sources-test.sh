#!/bin/sh
# scripts/docker-sources.sh のテスト。一時ディレクトリに csproj と Dockerfile と dockerignore を作って照合させる。
set -eu
cd "$(dirname "$0")/.."
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
fail=0

# $1: テストの名前、$2: 期待する終了コード、$3: 標準エラーに出るはずの文字列 (空なら何も出ないこと)
expect() {
	status=0
	err="$(sh scripts/docker-sources.sh "$work" "$work/Dockerfile" "$work/ignore" 2>&1)" || status=$?
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

# $1: プロジェクトの名前、$2: 取り込むフォルダ (空なら取り込まない)、$3 以降: 参照するプロジェクトの名前
project() {
	name="$1" source="$2"
	shift 2
	mkdir -p "$work/dotnet/$name"
	{
		echo '<Project>'
		echo '  <ItemGroup>'
		[ -z "$source" ] || echo "    <Compile Include=\"../../$source/**/*.cs\" />"
		for reference in "$@"; do
			echo "    <ProjectReference Include=\"../$reference/$reference.csproj\" />"
		done
		echo '  </ItemGroup>'
		echo '</Project>'
	} >"$work/dotnet/$name/$name.csproj"
}

reset() {
	rm -rf "$work/dotnet"
	mkdir -p "$work/dotnet"
	echo 'RUN dotnet publish dotnet/Server/Server.csproj -c Release -o /out' >"$work/Dockerfile"
	: >"$work/ignore"
}

copied() {
	echo "COPY $1/ $1/" >>"$work/Dockerfile"
	echo "!$1/" >>"$work/ignore"
}

reset
project Server "" Protocol
project Protocol Assets/kits/shooter/Scripts/Protocol Net
project Net Assets/kits/core/Scripts/Net
copied Assets/kits/shooter/Scripts/Protocol
copied Assets/kits/core/Scripts/Net
expect "たどったプロジェクトのフォルダをどれも写して残している" 0 ""

reset
project Server "" Protocol
project Protocol Assets/kits/shooter/Scripts/Protocol Net
project Net Assets/kits/core/Scripts/Net
copied Assets/kits/shooter/Scripts/Protocol
echo '!Assets/kits/core/Scripts/Net/' >>"$work/ignore"
expect "参照の参照の COPY の抜け" 1 "dotnet/Net/Net.csproj compiles Assets/kits/core/Scripts/Net but the image does not copy it; add COPY Assets/kits/core/Scripts/Net/ Assets/kits/core/Scripts/Net/"

reset
project Server "" Net
project Net Assets/kits/core/Scripts/Net
echo 'COPY Assets/kits/core/Scripts/Net/ Assets/kits/core/Scripts/Net/' >>"$work/Dockerfile"
expect "dockerignore の抜け" 1 "leaves it out; add !Assets/kits/core/Scripts/Net/"

reset
project Server "" Net
project Net Assets/kits/core/Scripts/Net Server
copied Assets/kits/core/Scripts/Net
expect "参照が輪になっていても止まる" 0 ""

reset
project Server ""
project Aircraft Assets/kits/shooter/Scripts/Aircraft/Core
expect "サーバーがたどらないプロジェクトは見ない" 0 ""

reset
: >"$work/Dockerfile"
project Server ""
expect "dotnet publish の無い Dockerfile" 1 "no dotnet publish of a .csproj found"

exit "$fail"
