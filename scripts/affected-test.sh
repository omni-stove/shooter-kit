#!/usr/bin/env bash
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
fail=0

# スクリプトは _lib.sh でリポジトリの根に移るので、本物のリポジトリを汚さないよう使い捨ての git リポジトリに置いて走らせる。
# dotnet は呼ばれた引数を書き残すだけの偽物に差し替え、lint.sh と test.sh が何を渡したかを見る。
mkdir -p "$work/repo/scripts" "$work/bin"
cp "$here/affected.sh" "$here/lint.sh" "$here/test.sh" "$here/_lib.sh" "$here/server-tuning.sh" "$here/server-tuning.txt" "$here/server-rules.sh" "$here/docker-sources.sh" "$work/repo/scripts/"
cat >"$work/bin/dotnet" <<EOF2
#!/usr/bin/env bash
printf 'dotnet %s\n' "\$*" >>"$work/dotnet.log"
EOF2
chmod +x "$work/bin/dotnet"
export PATH="$work/bin:$PATH"
cd "$work/repo"
git init -q -b main
git config user.email test@example.com
git config user.name test

check() {
	if [ "$2" != "$3" ]; then
		printf 'FAIL %s\n--- expected\n%s\n--- actual\n%s\n' "$1" "$2" "$3"
		fail=1
	fi
}

# 1 つの変更の後に、affected.sh の出力・--cs の出力・lint.sh --since と test.sh --since が呼んだ dotnet を比べてから、変更を捨てる。
expect() {
	local name="$1" flags="$2" cs="$3" lint="$4" test="$5" out rc
	check "$name: flags" "$flags" "$(bash scripts/affected.sh HEAD)"
	check "$name: --cs" "$cs" "$(bash scripts/affected.sh --cs HEAD)"
	: >"$work/dotnet.log"
	out="$(bash scripts/lint.sh --since HEAD 2>&1)" && rc=0 || rc=$?
	check "$name: lint.sh --since" "0:$lint" "$rc:$(cat "$work/dotnet.log")"
	: >"$work/dotnet.log"
	out="$(bash scripts/test.sh --since HEAD 2>&1)" && rc=0 || rc=$?
	check "$name: test.sh --since" "0:$test" "$rc:$(cat "$work/dotnet.log")"
	git reset -q --hard
	git clean -qfd
}

full_lint='dotnet format dotnet/GameKit.slnx --verify-no-changes -v q
dotnet format whitespace Assets/kits --folder --verify-no-changes -v q'
full_test="dotnet test dotnet/GameKit.slnx -v q --nologo --logger console;verbosity=normal"

mkdir -p Assets/kits/shooter/Scripts/Core Assets/kits/shooter/Scripts/UI dotnet/Shooter.Core server-data/rules docs
cat >dotnet/Shooter.Core/Shooter.Core.csproj <<'EOF2'
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <Compile Include="../../Assets/kits/shooter/Scripts/Core/**/*.cs" />
  </ItemGroup>
</Project>
EOF2
echo 'class Rules {}' >Assets/kits/shooter/Scripts/Core/Rules.cs
echo 'class Old {}' >Assets/kits/shooter/Scripts/Core/Old.cs
echo 'class Hud {}' >Assets/kits/shooter/Scripts/UI/Hud.cs
echo 'class Host {}' >dotnet/Shooter.Core/Host.cs
echo '{}' >server-data/rules/tdm.json
echo tdm >server-data/rules/hand-written.txt
mkdir -p docker/server
printf 'RUN dotnet publish dotnet/Shooter.Core/Shooter.Core.csproj\nCOPY Assets/kits/shooter/Scripts/Core/ Assets/kits/shooter/Scripts/Core/\n' >docker/server/Dockerfile
echo '!Assets/kits/shooter/Scripts/Core/' >docker/server/Dockerfile.dockerignore
echo '# x' >docs/a.md
echo 'root = true' >.editorconfig
git add -A && bash scripts/lint.sh --fix-meta && git add -A && git commit -qm init

echo '# y' >>docs/a.md
expect "docs だけ" "full=false
test=false
format=false" "" "" ""

echo 'class Hud2 {}' >>Assets/kits/shooter/Scripts/UI/Hud.cs
expect "Unity だけの .cs" "full=false
test=false
format=true" "Assets/kits/shooter/Scripts/UI/Hud.cs" "dotnet format whitespace Assets/kits --folder --verify-no-changes -v q --include shooter/Scripts/UI/Hud.cs" ""

echo 'class Rules2 {}' >>Assets/kits/shooter/Scripts/Core/Rules.cs
echo 'class Hud2 {}' >>Assets/kits/shooter/Scripts/UI/Hud.cs
expect "csproj が取り込む .cs と Unity だけの .cs" "full=false
test=true
format=true" "Assets/kits/shooter/Scripts/Core/Rules.cs
Assets/kits/shooter/Scripts/UI/Hud.cs" "dotnet format dotnet/GameKit.slnx --verify-no-changes -v q --include Assets/kits/shooter/Scripts/Core/Rules.cs Assets/kits/shooter/Scripts/UI/Hud.cs
dotnet format whitespace Assets/kits --folder --verify-no-changes -v q --include shooter/Scripts/Core/Rules.cs shooter/Scripts/UI/Hud.cs" "$full_test"

echo 'class Host2 {}' >>dotnet/Shooter.Core/Host.cs
expect "dotnet の .cs" "full=false
test=true
format=true" "dotnet/Shooter.Core/Host.cs" "dotnet format dotnet/GameKit.slnx --verify-no-changes -v q --include dotnet/Shooter.Core/Host.cs" "$full_test"

echo 'class New {}' >Assets/kits/shooter/Scripts/Core/New.cs
expect "未追跡の新しい .cs" "full=false
test=true
format=true" "Assets/kits/shooter/Scripts/Core/New.cs" "dotnet format dotnet/GameKit.slnx --verify-no-changes -v q --include Assets/kits/shooter/Scripts/Core/New.cs
dotnet format whitespace Assets/kits --folder --verify-no-changes -v q --include shooter/Scripts/Core/New.cs" "$full_test"

git rm -q Assets/kits/shooter/Scripts/Core/Old.cs Assets/kits/shooter/Scripts/Core/Old.cs.meta
expect "消した .cs は書式を見ずにテストだけ回す" "full=false
test=true
format=true" "" "" "$full_test"

echo '{"a":1}' >server-data/rules/tdm.json
expect "テストが読む server-data" "full=false
test=true
format=false" "" "" "$full_test"

mkdir -p Assets/kits/shooter/Resources/Tuning
echo '{}' >Assets/kits/shooter/Resources/Tuning/grenade.json
mkdir -p server-data/tuning
echo '{}' >server-data/tuning/grenade.json
expect "Unity 側のデータ (テストが読むことがある)" "full=false
test=true
format=false" "" "" "$full_test"

echo 'x' >Assets/kits/shooter/Scripts/UI/Hud.prefab
echo 'class Hud2 {}' >>Assets/kits/shooter/Scripts/UI/Hud.cs
expect "Unity だけの .cs とプレハブ" "full=false
test=true
format=true" "Assets/kits/shooter/Scripts/UI/Hud.cs" "dotnet format dotnet/GameKit.slnx --verify-no-changes -v q --include Assets/kits/shooter/Scripts/UI/Hud.cs
dotnet format whitespace Assets/kits --folder --verify-no-changes -v q --include shooter/Scripts/UI/Hud.cs" "$full_test"

echo 'x' >Assets/kits/shooter/Scripts/UI/Panel.cs
echo 'x' >Assets/kits/shooter/Scripts/UI/Panel.cs.meta
echo '# y' >>docs/a.md
expect "Unity だけの新しい .cs とその .meta と文書" "full=false
test=false
format=true" "Assets/kits/shooter/Scripts/UI/Panel.cs" "dotnet format whitespace Assets/kits --folder --verify-no-changes -v q --include shooter/Scripts/UI/Panel.cs" ""

mkdir -p Assets/kits/core/Scripts/Runtime
echo 'class Bindings {}' >Assets/kits/core/Scripts/Runtime/Bindings.cs
echo 'x' >Assets/kits/core/Scripts/Runtime/Bindings.cs.meta
expect "core キットの Unity だけの新しい .cs とその .meta" "full=false
test=false
format=true" "Assets/kits/core/Scripts/Runtime/Bindings.cs" "dotnet format whitespace Assets/kits --folder --verify-no-changes -v q --include core/Scripts/Runtime/Bindings.cs" ""

mkdir -p Assets/kits/racing/Scripts
echo 'class Lap {}' >Assets/kits/racing/Scripts/Lap.cs
echo 'x' >Assets/kits/racing/Scripts/Lap.cs.meta
echo 'class Hud2 {}' >>Assets/kits/shooter/Scripts/UI/Hud.cs
expect "ほかのジャンルのキットとシューターの Unity だけの .cs は 1 回の書式検査にまとめる" "full=false
test=false
format=true" "Assets/kits/shooter/Scripts/UI/Hud.cs
Assets/kits/racing/Scripts/Lap.cs" "dotnet format whitespace Assets/kits --folder --verify-no-changes -v q --include shooter/Scripts/UI/Hud.cs racing/Scripts/Lap.cs" ""

echo 'indent_style = space' >>.editorconfig
expect "書式の規則" "full=true
test=true
format=true" "" "$full_lint" "$full_test"

echo '<Project />' >dotnet/Shooter.Core/Shooter.Core.csproj
expect "csproj" "full=true
test=true
format=true" "" "$full_lint" "$full_test"

check "引けない base は全部" "full=true
test=true
format=true" "$(bash scripts/affected.sh no-such-ref)"

exit "$fail"
