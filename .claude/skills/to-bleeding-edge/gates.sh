#!/usr/bin/env bash
# The gates /to-bleeding-edge runs on the merged tree before the PR: the static checks
# bleeding-edge-guard.yml runs, every asset generator's --check, and the headless reference
# compile (player + editor). Prints one line per check and a final verdict; exit 1 on any failure.
# A READER script: it writes nothing into the repo.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
fail=0

run() {   # run <label> <command...>
  local label="$1"; shift
  local out rc
  out=$(timeout 600 "$@" 2>&1); rc=$?
  if [ $rc -eq 0 ]; then echo "ok    $label"; else echo "FAIL  $label :: $(echo "$out" | tail -3 | tr '\n' ' ' | cut -c1-220)"; fail=1; fi
}

run validate_project            python3 Tools/CI/validate_project.py
run conditional_compilation     python3 Tools/Build/check_conditional_compilation.py --root Assets
run console_logging             python3 Tools/Build/check_console_logging.py
run gamelist_scenes             python3 Tools/Build/check_gamelist_scenes.py
run hud_onscreen                python3 Tools/Build/check_hud_onscreen.py
run junie_skills                python3 Tools/Build/check_junie_skills.py
run retire_call_to_action       python3 Tools/Build/retire_call_to_action.py --check
run graphic_canvas_renderer     python3 Tools/Build/check_graphic_canvas_renderer.py
run ai_no_state_writes          python3 Tools/Build/check_ai_no_state_writes.py --check
run arcade_mode_lib             python3 Tools/Build/arcade_mode_lib.py --self-test
for f in Tools/Build/author_*_assets.py Tools/Build/author_skimrace_ai_config.py Tools/Build/author_lifeform_heart_sizes.py; do
  [ -f "$f" ] && run "$(basename "$f") --check" python3 "$f" --check
done

if [ -x "$DOTNET_ROOT/dotnet" ]; then
  out=$(bash Tools/Build/unity_refcompile/run.sh --quiet-buckets 2>&1)
  if echo "$out" | grep -q "RESULT: OK"; then echo "ok    refcompile player"
  else echo "FAIL  refcompile player :: $(echo "$out" | grep -E 'RESULT|error CS' | head -5 | tr '\n' ' ')"; fail=1; fi
  # Editor config is approximate: only errors in files changed since bleeding-edge gate.
  out=$(bash Tools/Build/unity_refcompile/run.sh --config editor --quiet-buckets 2>&1)
  if echo "$out" | grep -qE "RESULT: OK|\(0 in files changed since"; then echo "ok    refcompile editor (changed files)"
  else echo "FAIL  refcompile editor :: $(echo "$out" | grep -E 'RESULT|CHANGED-TONIGHT' | head -5 | tr '\n' ' ')"; fail=1; fi
  # check_generated_assets needs the player compile output, so it runs after it.
  run generated_assets          python3 Tools/Build/check_generated_assets.py
else
  echo "SKIP  refcompile + generated_assets: no .NET SDK at $DOTNET_ROOT (install: see SKILL.md step 4)"
fi

[ $fail -eq 0 ] && echo "GATES: ALL OK" || echo "GATES: FAILED"
exit $fail
