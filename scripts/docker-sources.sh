#!/bin/sh
# 専用サーバーのイメージ (docker/server/Dockerfile) が、dotnet publish するプロジェクトとそれが ProjectReference でたどる各プロジェクトの
# Assets から取り込むソースのフォルダをどれも COPY し、Dockerfile.dockerignore でも除かずに残しているかを確かめる。
# 抜けていると手元の dotnet build は通るのに、イメージのビルドだけが失敗する。
# 引数でパスを替えられるのは、scripts/docker-sources-test.sh が作ったファイルで確かめるため。
# 使い方: sh scripts/docker-sources.sh [<csproj のパスの基点> <Dockerfile> <dockerignore>]
set -eu
cd "$(dirname "$0")/.."
root="${1:-.}"
dockerfile="${2:-docker/server/Dockerfile}"
ignore="${3:-docker/server/Dockerfile.dockerignore}"

published="$(sed -n 's#.*dotnet publish \([^ ]*\.csproj\).*#\1#p' "$dockerfile")"
if [ -z "$published" ]; then
	echo "$dockerfile: error DOCKER: no dotnet publish of a .csproj found" >&2
	exit 1
fi

# ProjectReference をたどり、取り込むプロジェクトを 1 行ずつ (root からの相対パスで) 重複なく出す。
projects() {
	queue="$published"
	seen=""
	while [ -n "$queue" ]; do
		csproj="${queue%%
*}"
		case "$queue" in *"
"*) queue="${queue#*
}" ;; *) queue="" ;; esac
		case "
$seen
" in *"
$csproj
"*) continue ;; esac
		seen="$seen
$csproj"
		echo "$csproj"
		dir="$(dirname "$csproj")"
		for reference in $(sed -n 's#.*<ProjectReference Include="\([^"]*\)".*#\1#p' "$root/$csproj"); do
			next="$dir/$reference"
			while :; do
				case "$next" in
				*/./*) next="$(echo "$next" | sed 's#/\./#/#')" ;;
				*/*/../*) next="$(echo "$next" | sed 's#[^/]*/\.\./##')" ;;
				*) break ;;
				esac
			done
			queue="${queue:+$queue
}$next"
		done
	done
}

problems="$(projects | while IFS= read -r csproj; do
	sed -n 's#.*<Compile Include="\.\./\.\./\(Assets/[^"*]*\)/\*\*/\*\.cs".*#\1#p' "$root/$csproj" | while IFS= read -r source; do
		grep -qxF "COPY $source/ $source/" "$dockerfile" \
			|| echo "$dockerfile: error DOCKER: $csproj compiles $source but the image does not copy it; add COPY $source/ $source/"
		grep -qxF "!$source/" "$ignore" \
			|| echo "$ignore: error DOCKER: $csproj compiles $source but the build context leaves it out; add !$source/"
	done
done)"
[ -z "$problems" ] && exit 0
printf '%s\n' "$problems" >&2
exit 1
