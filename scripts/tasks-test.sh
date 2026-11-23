#!/bin/sh
# mise のタスクが Unity を呼ぶ形のテスト。PATH の unity は Unity CLI なので、Editor の引数を直に渡していないか、
# unity run が自分で付ける引数を重ねていないかを見て、build と smoke は引数を記録する偽の unity で走らせる。Unity は起動しない。
set -eu
cd "$(dirname "$0")/.."
fail=0
tasks=.mise/tasks.toml

check() {
	if [ "$2" != "$3" ]; then
		printf 'FAIL %s: got\n%s\nexpected\n%s\n' "$1" "$2" "$3"
		fail=1
	fi
}

# Unity CLI の全体のオプションは -- で始まる。- 1 つで始まるのは Editor の引数で、CLI は unknown option で終了コード 2 になる
direct="$(grep -v '^ *#' "$tasks" | grep -E '(^|[^A-Za-z0-9_-])unity +-[A-Za-z]' || true)"
check "no task passes Editor arguments straight to unity" "$direct" ""

# unity run は -batchmode・-quit・-projectPath を自分で付け、-- の後ろに書くと Editor を起動する前に終了コード 6 になる
reserved="$(grep -v '^ *#' "$tasks" | grep -E 'unity( +--[a-z-]+)* +run .* -(batchmode|quit|projectPath)( |"|'"'"'|$)' || true)"
check "no unity run task forwards a flag unity run manages" "$reserved" ""

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
mkdir "$work/bin"
cat >"$work/bin/unity" <<'EOF'
#!/bin/sh
for arg in "$@"; do printf '%s\n' "$arg"; done >"$FAKE_UNITY_ARGS"
log="$(sed -n 's/.*-logFile "\([^"]*\)".*/\1/p' "$FAKE_UNITY_ARGS")"
if [ -n "$log" ] && [ -n "${FAKE_UNITY_LOG:-}" ]; then printf '%s\n' "$FAKE_UNITY_LOG" >"$log"; fi
exit "${FAKE_UNITY_EXIT:-0}"
EOF
chmod +x "$work/bin/unity"

# 偽の unity を終了コード $1 で終わらせ、$2 が空でなければ -logFile の先に書かせて、残りの引数のタスクを走らせる。
# タスクの終了コードを返し、偽の unity が受けた引数を 1 行に 1 つ $work/args に残す
run_task() {
	fake_exit="$1"
	fake_log="$2"
	shift 2
	rm -f "$work/args"
	PATH="$work/bin:$PATH" FAKE_UNITY_ARGS="$work/args" FAKE_UNITY_EXIT="$fake_exit" FAKE_UNITY_LOG="$fake_log" \
		mise run "$@" >"$work/out" 2>&1
}

status=0
run_task 0 "" build windows --version v1.2.3 || status=$?
check "build exits with unity's exit code" "$status" "0"
check "build calls unity run" "$(cat "$work/args" 2>/dev/null)" "--non-interactive
run
.
--
-executeMethod
Shooter.Editor.ClientBuilder.Build
-clientPlatform
windows
-buildVersion
v1.2.3"

status=0
run_task 0 "" build || status=$?
check "build defaults to macos and no version" "$(cat "$work/args" 2>/dev/null)" "--non-interactive
run
.
--
-executeMethod
Shooter.Editor.ClientBuilder.Build
-clientPlatform
macos"

status=0
run_task 6 "" build || status=$?
check "build fails when unity fails" "$status" "6"

# smoke は Logs/smoke.log を書き直すので、手元に残っているログは退けておいて戻す
if [ -f Logs/smoke.log ]; then
	mv Logs/smoke.log "$work/smoke.log.kept"
	trap 'mv "$work/smoke.log.kept" Logs/smoke.log; rm -rf "$work"' EXIT
else
	trap 'rm -f Logs/smoke.log; rm -rf "$work"' EXIT
fi
log="$PWD/Logs/smoke.log"

status=0
run_task 0 "[SmokeSuite] passed:
  passed CampaignSmokeRun (exit code 0) (exit code 0)" smoke --root "Assets/My Game" || status=$?
check "smoke passes when unity exits 0 and the suite logged passed" "$status" "0"
check "smoke opens a batch-mode editor without -quit and waits for it" "$(cat "$work/args" 2>/dev/null)" "--non-interactive
open
.
--batchmode
--wait
--args
-executeMethod Shooter.Editor.SmokeSuite.RunAll -shooterKitRoot \"Assets/My Game\" -logFile \"$log\""
check "smoke prints the suite's lines" "$(grep -c -E 'SmokeSuite\] passed:|passed CampaignSmokeRun' "$work/out")" "2"

status=0
run_task 0 "[SmokeSuite] passed:" smoke || status=$?
check "smoke without --root passes no -shooterKitRoot" "$(sed -n '$p' "$work/args" 2>/dev/null)" "-executeMethod Shooter.Editor.SmokeSuite.RunAll  -logFile \"$log\""

status=0
run_task 6 "[SmokeSuite] failed:
  FAILED CampaignSmokeRun (exit code 1) (exit code 1)" smoke || status=$?
check "smoke fails with unity's exit code when the editor exits non-zero" "$status" "6"
check "smoke prints the failed run" "$(grep -c 'FAILED CampaignSmokeRun' "$work/out")" "1"

# プロジェクトが Editor で開かれていると unity open は何も起動せず 0 で終わり、ログは書かれない
status=0
run_task 0 "" smoke || status=$?
check "smoke fails when unity exits 0 but the suite never logged passed" "$status" "1"

[ "$fail" = 0 ] && echo "tasks-test: all passed"
exit "$fail"
