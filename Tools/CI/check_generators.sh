#!/usr/bin/env bash
# Every asset generator's --check, reporting EVERY red generator rather than stopping at the first.
#
# Why: the loop used to run under `set -e` in bleeding-edge-guard.yml and unity-ci.yml, so CI showed
# only the alphabetically first red generator. On 2026-11-01 four were red (Angler, Meerkat, Regatta,
# Undertow) and every run and every tracking-issue comment named only Angler; three breaks merged
# behind the first and never appeared as new failures (Docs/Reports/CI.md).
#
# Usage:
#   bash Tools/CI/check_generators.sh               # the whole family (what CI runs)
#   bash Tools/CI/check_generators.sh <gen.py>...   # only these (a negative control, or one generator)
#
# Output: each generator's own --check output under a "== <path>" header, then a summary naming every
# red one. Under GitHub Actions it also emits one ::error annotation per red generator, appends the
# summary to $GITHUB_STEP_SUMMARY, and sets the step output `red` (space-separated basenames).
# Exit 1 if arcade_mode_lib's self-test or any generator failed, 0 otherwise.
# A READER script: it writes nothing into the repository.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"

if [ "$#" -gt 0 ]; then
  generators=("$@")
else
  generators=(Tools/Build/author_*_assets.py Tools/Build/author_skimrace_ai_config.py Tools/Build/author_lifeform_heart_sizes.py)
fi

red=()
self_test_ok=true
echo "== Tools/Build/arcade_mode_lib.py --self-test"
if ! python3 Tools/Build/arcade_mode_lib.py --self-test; then
  self_test_ok=false
  red+=("Tools/Build/arcade_mode_lib.py --self-test")
fi

checked=0
for f in "${generators[@]}"; do
  if [ ! -f "$f" ]; then
    echo "== $f: no such file"
    red+=("$f (missing)")
    continue
  fi
  echo "== $f"
  checked=$((checked + 1))
  python3 "$f" --check || red+=("$f")
done

echo
if [ "${#red[@]}" -eq 0 ]; then
  echo "generators: OK ($checked checked, arcade_mode_lib self-test OK)"
else
  echo "generators: ${#red[@]} RED ($checked checked$([ "$self_test_ok" = true ] || echo ", and arcade_mode_lib's self-test failed")):"
  printf '  RED  %s\n' "${red[@]}"
  echo "Each one's own output is under its '== <path>' header above."
fi

if [ -n "${GITHUB_ACTIONS:-}" ]; then
  names=""
  for r in "${red[@]}"; do
    path="${r%% *}"
    echo "::error file=$path,title=Generator red::$r --check failed; its output is under '== $path' in this step's log"
    names="$names $(basename "$path")"
  done
  [ -n "${GITHUB_OUTPUT:-}" ] && echo "red=${names# }" >> "$GITHUB_OUTPUT"
  if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    {
      echo "### Asset generators"
      if [ "${#red[@]}" -eq 0 ]; then
        echo "All $checked generators match disk."
      else
        echo "**${#red[@]} red** ($checked checked):"
        echo
        printf -- '- `%s`\n' "${red[@]}"
      fi
    } >> "$GITHUB_STEP_SUMMARY"
  fi
fi

[ "${#red[@]}" -eq 0 ]
