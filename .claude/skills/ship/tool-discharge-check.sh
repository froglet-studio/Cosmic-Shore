#!/usr/bin/env bash
# tool-discharge-check.sh — evidence for the /ship §2.5 editor-tool discharge gate.
#
# Claude cannot run Unity, so an editor tool it writes is inert until a human clicks the
# menu item and commits the asset diff. This script gathers the evidence needed to judge
# whether that happened; it does NOT decide for you — read the output and classify.
#
#   usage: .claude/skills/ship/tool-discharge-check.sh [base-ref]   (default: bleeding-edge)
#
# Exit status is always 0: this is a report, not a CI gate.

set -uo pipefail

BASE="${1:-bleeding-edge}"
LEDGER="Docs/EDITOR_TOOL_LEDGER.md"

cd "$(git rev-parse --show-toplevel)" || exit 1

# Prefer the remote-tracking ref: a stale local `bleeding-edge` silently widens the range
# and makes already-merged tools look like this branch's undischarged work.
if git rev-parse --verify --quiet "origin/$BASE" >/dev/null; then
  if git rev-parse --verify --quiet "$BASE" >/dev/null \
     && [ "$(git rev-parse "$BASE")" != "$(git rev-parse "origin/$BASE")" ]; then
    echo "note: local '$BASE' differs from 'origin/$BASE' — using the remote ref."
    echo
  fi
  BASE="origin/$BASE"
elif ! git rev-parse --verify --quiet "$BASE" >/dev/null; then
  echo "!! base ref '$BASE' not found (tried origin/$BASE too)." >&2
  exit 0
fi

RANGE="$BASE..HEAD"
echo "=== editor-tool discharge check — $RANGE ==="
echo

# ---------------------------------------------------------------- 1. asset output
ASSETS=$(git diff --name-only "$RANGE" -- \
  '*.prefab' '*.asset' '*.unity' '*.shadergraph' '*.mat' '*.shadersubgraph' 2>/dev/null)
ASSET_COUNT=$(printf '%s' "$ASSETS" | grep -c . || true)

echo "--- 1. asset files changed on this branch: $ASSET_COUNT"
if [ "$ASSET_COUNT" -gt 0 ]; then
  printf '%s\n' "$ASSETS" | sed 's/^/    /' | head -40
  [ "$ASSET_COUNT" -gt 40 ] && echo "    … $((ASSET_COUNT - 40)) more"
else
  echo "    (none) — any one-shot tool below is UNDISCHARGED unless proven otherwise"
fi
echo

# ------------------------------------------------------- 2. tools touched by the branch
TOOLS=""
while IFS= read -r f; do
  [ -n "$f" ] && [ -f "$f" ] && grep -q '\[MenuItem' "$f" 2>/dev/null && TOOLS="$TOOLS$f"$'\n'
done < <(git diff --name-only --diff-filter=d "$RANGE" -- '*.cs' 2>/dev/null)
TOOL_COUNT=$(printf '%s' "$TOOLS" | grep -c . || true)

echo "--- 2. [MenuItem] tools added/modified on this branch: $TOOL_COUNT"
if [ "$TOOL_COUNT" -eq 0 ]; then
  echo "    (none) — §2.5 requires no discharge block for this branch"
else
  while IFS= read -r f; do
    [ -n "$f" ] || continue
    status=$(git diff --name-status "$RANGE" -- "$f" | cut -f1 | head -1)
    echo "    [${status:-M}] $f"
    grep -o '\[MenuItem("[^"]*"' "$f" | sed 's/\[MenuItem("/          menu: /; s/"$//'
    # writer or reporter?
    # NOTE: asset-authoring tools do not always go through AssetDatabase — graph/JSON
    # wirers write the file directly (File.WriteAllText) and reimport. Catch both.
    w=$(grep -cE 'AssetDatabase\.(CreateAsset|SaveAssets|AddObjectToAsset|MoveAsset|DeleteAsset|CopyAsset|ImportAsset|WriteImportSettingsIfDirty)|PrefabUtility\.(SaveAsPrefabAsset|SavePrefabAsset|ApplyPrefabInstance)|EditorSceneManager\.(SaveScene|MarkSceneDirty)|EditorUtility\.SetDirty|File\.(WriteAllText|WriteAllBytes|WriteAllLines|Copy|Move|Delete)' "$f" 2>/dev/null || true)
    if [ "${w:-0}" -gt 0 ]; then
      echo "          WRITES ASSETS ($w call sites) → must be classified standing vs one-shot"
    else
      echo "          read-only (no asset writes detected) → likely a standing auditor"
    fi
    base=$(basename "$f" .cs)
    if [ -f "$LEDGER" ] && grep -q "$base" "$LEDGER" 2>/dev/null; then
      grep -n "$base" "$LEDGER" | head -3 | sed 's/^/          ledger: /'
    else
      echo "          ledger: NO ROW — add one to $LEDGER"
    fi
  done <<< "$TOOLS"
fi
echo

# --------------------------------------------------------------- 3. pending ledger rows
echo "--- 3. open ledger obligations ($LEDGER)"
if [ -f "$LEDGER" ]; then
  if grep -q 'PENDING' "$LEDGER"; then
    grep -n 'PENDING' "$LEDGER" | sed 's/^/    /'
  else
    echo "    (no PENDING rows)"
  fi
  echo
  echo "    one-shot rows marked RUN whose tool file still exists (retire these):"
  found=0
  while IFS= read -r path; do
    [ -n "$path" ] && [ -f "$path" ] && { echo "        $path"; found=1; }
  done < <(grep -oE 'Assets/[A-Za-z0-9_/.-]+\.cs' "$LEDGER" 2>/dev/null | sort -u)
  [ "$found" -eq 0 ] && echo "        (none)"
else
  echo "    !! $LEDGER missing — create it (see /ship §2.5 step 6)"
fi
echo

echo "=== end. §2.5: every one-shot tool needs its output in section 1, or a PENDING row. ==="
