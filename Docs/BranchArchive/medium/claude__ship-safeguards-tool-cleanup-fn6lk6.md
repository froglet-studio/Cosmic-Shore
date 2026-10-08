# Branch archive: `claude/ship-safeguards-tool-cleanup-fn6lk6`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-08-12 by Claude
- **Unmerged commits:** 6
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/712
- **Forked from:** `d32f26839` (2026-08-12, Merge pull request #710 from froglet-studio/claude/charge-crystal-shader-9u2ik)
- **Tip:** `c8ed894ff`
- **Files touched (35):**
  - `.claude/skills/ship/SKILL.md`
  - `.claude/skills/ship/tool-discharge-check.sh`
  - `Assets/Editor/ToastNotificationSetup.cs`
  - `Assets/_Prefabs/Environment/ActiveCrystalMass.prefab`
  - `Assets/_Prefabs/Environment/ActiveCrystalSpace.prefab`
  - `Assets/_Prefabs/Environment/Crystal.prefab`
  - `Assets/_Prefabs/Environment/CrystalCharge.prefab`
  - `Assets/_Prefabs/Environment/CrystalMass.prefab`
  - `Assets/_Prefabs/Environment/CrystalSpace.prefab`
  - `Assets/_Prefabs/Environment/CrystalTime.prefab`
  - `Assets/_Prefabs/Environment/MazeCrystal.prefab`
  - `Assets/_Prefabs/Environment/OldCrystalTime.prefab`
  - `Assets/_Prefabs/Environment/Spawners/SpawnedSegments.prefab`
  - `Assets/_Prefabs/Spacevessels/Sparrow.prefab`
  - `Assets/_SO_Assets/Toys/Toy_Conveyor.asset`
  - `Assets/_Scripts/Controller/Vessel/ElementalBarsController.cs`
  - `Assets/_Scripts/Editor/CanvasUpgrader/CanvasUpgradeProcessor.cs`
  - `Assets/_Scripts/Editor/LifeFormCrystalValidator.cs`
  - `Assets/_Scripts/Editor/PlayfabProductGenerator.cs`
  - `Assets/_Scripts/Editor/PlayfabProductGenerator.cs.meta`
  - `Assets/_Scripts/Editor/PrismClockGraphWirer.cs.meta`
  - `Assets/_Scripts/Editor/PrismClockSmokeTest.cs.meta`
  - `Assets/_Scripts/Editor/PrismClockWiringValidator.cs.meta`
  - `Assets/_Scripts/Editor/ProfileAvatarBinder.cs`
  - `Assets/_Scripts/Editor/ProfileAvatarBinder.cs.meta`
  - `Assets/_Scripts/Editor/StripCrystalAudioSourceTool.cs`
  - `Assets/_Scripts/Editor/StripCrystalAudioSourceTool.cs.meta`
  - `Assets/_Scripts/Editor/TriangleWindowMeshGenerator.cs`
  - `Assets/_Scripts/Editor/TriangleWindowMeshGenerator.cs.meta`
  - `Assets/_Scripts/Utility/PrismClock.cs.meta`
  - `Assets/_Scripts/Utility/PrismClockDiagnostics.cs.meta`
  - `CLAUDE.md`
  - `Docs/EDITOR_TOOL_LEDGER.md`
  - `Docs/PERFORMANCE_OPTIMIZATION.md`
  - `Docs/README.md`

### `193b2b7f7` — docs(ship): editor-tool discharge gate + tool ledger

_Claude, 2026-08-02 23:06:26 +0000_

```text
Sessions cannot run Unity, so a [MenuItem] tool a session writes is inert until
the prompter clicks it and commits the asset diff. That step was being skipped:
branches merged with the TOOL but without its OUTPUT, leaving bleeding-edge with
code expecting assets nobody wired plus menu items that outlive their job.

- /ship gains §2.5, the editor-tool discharge gate: classify every tool the
  branch touches as standing vs one-shot, prove a one-shot's output is in the
  branch diff, hand the prompter one copy-pasteable run+commit+push block,
  retire the tool once discharged, and rewrite its docs to a commit pointer
  rather than a menu path that no longer exists.
- §1/§4/§5/§6 updated: survey reads the ledger, an undischarged one-shot is a
  go/no-go blocker, the PR body carries a "Tool runs required before merge"
  section, and the report leads with the discharge block.
- tool-discharge-check.sh gathers the evidence mechanically (branch [MenuItem]
  diff, asset diff, ledger rows). Prefers origin/<base>: a stale local ref
  silently widens the range and makes merged tools look undischarged.
- Docs/EDITOR_TOOL_LEDGER.md seeds from an audit of every [MenuItem] against
  the artifacts it claims to produce.
```

```text
 .claude/skills/ship/SKILL.md                |  79 ++++++++++++++++++++++++++++
 .claude/skills/ship/tool-discharge-check.sh | 106 +++++++++++++++++++++++++++++++++++++
 CLAUDE.md                                   |   1 +
 Docs/EDITOR_TOOL_LEDGER.md                  | 164 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 Docs/README.md                              |   4 ++
 5 files changed, 354 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 431 lines)</summary>

```diff
diff --git a/.claude/skills/ship/SKILL.md b/.claude/skills/ship/SKILL.md
index da69918e3..b99cd309a 100644
--- a/.claude/skills/ship/SKILL.md
+++ b/.claude/skills/ship/SKILL.md
@@ -21,6 +21,8 @@ run the `/reorient` skill first and act on its verdict before shipping.
   hunk you can't summarize from memory.
 - Restate, in a few sentences, WHAT the branch delivers and WHY. If you can't, you are
   not ready to ship — go re-read the diff.
+- Read `Docs/EDITOR_TOOL_LEDGER.md`. Any `⏳ PENDING` row this branch's work should have
+  discharged is in scope for this ship, not someone else's problem.
 
 ## 2. Review pass (the branch, not just the last commit)
 
@@ -35,6 +37,68 @@ Walk every changed file against these gates:
 - **Verification honesty**: list what was actually verified (in-editor play, tests) vs.
   what only compiles-by-inspection. Unverified risk goes in the PR body, not under the rug.
 
+## 2.5 Editor-tool discharge gate (the un-run tool trap)
+
+**You cannot run Unity.** An editor tool you wrote is *inert* until a human clicks its menu
+item and commits the resulting asset diff. Merging the tool without its output ships a
+half-landed feature: bleeding-edge gets code that expects wired assets nobody wired, plus a
+tool that clutters the menu forever. This gate exists because that has happened repeatedly.
+
+Run `.claude/skills/ship/tool-discharge-check.sh <base>` — it lists the branch's
+`[MenuItem]` tools, the asset files the branch changed, and each tool's ledger row. Then
+work its output:
+
+**1. Classify every tool the branch adds, changes, or depends on.**
+
+| Kind | Definition | Fate |
+|---|---|---|
+| **Standing** | Validator, auditor, report, or generator meant to be re-run on demand (`Validate Clock Wiring`, `Audit Vessel Ability Rows`, `Measure Cell Environment Baselines`). | Keep. Ledger row = `standing`. |
+| **One-shot** | Authors/migrates/wires assets once, then is dead weight (`Setup Freestyle Toybox`, `Canvas Upgrader`, `Strip Crystal AudioSources`). | Must be **discharged**, then **retired**. |
+
+**2. Prefer eliminating the obligation over documenting it.** Before writing a menu-item
+tool at all, check whether `/asset-surgery` can author the asset directly — a programmatic
+edit has no pending human step and cannot rot. Only write a tool when the edit genuinely
+needs the running editor (importer, mesh/lightmap bake, scene instantiation from runtime
+state). Say in the PR which case applies.
+
+**3. A one-shot tool is DISCHARGED only when its output is in this branch's diff.**
+`git diff --stat <base>..HEAD -- '*.prefab' '*.asset' '*.unity' '*.shadergraph' '*.mat'`
+— a one-shot tool with zero corresponding asset changes is **undischarged**. Do not
+rationalize it ("the runtime self-wires", "it's optional") without reading the runtime
+consumer and proving the fallback exists; if it does, the tool is a convenience, not a
+requirement — say that explicitly in the ledger.
+
+**4. Hand the human ONE copy-pasteable discharge block** (in the ship report *and* the PR
+body), per tool, in run order:
+
+```
+1. Unity ▸ Tools > Cosmic Shore > <exact menu path>
+   expect: <the console/result line that means it worked>
+   writes: <exact asset paths or globs>
+2. git add <paths> && git commit -m "chore(assets): <tool> output" \
+     && git push -u origin <branch>
+```
+
+Anything you cannot state precisely (which paths change, what success looks like) is a
+sign you don't know what your own tool does — go read it.
+
+**5. Retire the tool once its output is committed.** Delete a discharged one-shot tool in
+the same PR (or the immediate follow-up commit), and **rewrite every doc that told the
+reader to run it** — past tense, pointing at where the tool now lives, e.g.
+
+> Ran once on branch `claude/foo-abc` (PR #123, `a1b2c3d`). Recover with
+> `git show a1b2c3d -- Assets/_Scripts/Editor/FooTool.cs`.
+
+Docs must never point at a menu item that no longer exists, and a retired tool must always
+be recoverable by commit reference. Standing tools keep their menu path in the docs.
+
+**6. Record it in `Docs/EDITOR_TOOL_LEDGER.md`.** Every tool the branch adds, runs, or
+retires gets its row updated. A one-shot tool that genuinely cannot be discharged before
+merge stays as a `⏳ PENDING` row carrying the owner and the full discharge block from
+step 4 — that is the **only** acceptable way to merge an undischarged tool, and the PR
+body must call it out under Verification. While you're in the ledger, sweep it: any
+`✅ RUN` one-shot whose file still exists is cleanup this branch can do now.
+
 ## 3. Documentation pass ("ready to build from")
 
 For every system the branch touched, confirm the docs a NEW developer would reach for are
@@ -81,6 +145,11 @@ Say **NO** — and list the concrete iterations needed — when any of these hol
 - A change is known-broken or known-untested in a way that would block another dev
   building on it (compile risk on hand-authored assets counts).
 - Docs for a touched LOCKED system (ecology, party, threading, scoring) lag the code.
+- **§2.5 failed**: a one-shot editor tool on the branch is undischarged AND not registered
+  as a `⏳ PENDING` ledger row with a complete discharge block. Shipping the tool without
+  the output — or without a written obligation to produce it — is a blocker, not a
+  follow-up. (Discharging is usually minutes of the prompter's time: say GO-AFTER-RUN,
+  hand them the block, and wait for the push rather than merging half the feature.)
 
 Say **GO** when the work is coherent, documented, and honestly labeled. Loose ends that
 don't block building on the branch become a **Follow-ups** section in the PR body — named,
@@ -93,6 +162,10 @@ scoped, and assigned a doc home — not reasons to sit on finished work.
 - PR body: what & why, per-system summary, **verification status** (what a human must
   still verify in-editor, with steps), **Follow-ups** list, collider/perf impact where
   the ecology gate applies.
+- **Tool runs required before merge** section whenever §2.5 produced a discharge block —
+  reproduce it verbatim, name the ledger rows it clears, and state plainly that merging
+  ahead of the run lands the tool without its output. Omit the section entirely when the
+  branch adds no one-shot tool; never leave it as an empty heading.
 - Base is `bleeding-edge` unless told otherwise. After creating, subscribe to PR
   activity and keep watch (CI, reviews) until merged or told to stop.
 
@@ -101,3 +174,9 @@ scoped, and assigned a doc home — not reasons to sit on finished work.
 Tell the prompter: the go/no-go call and why, the PR link (or the iteration list), the
 follow-ups you recorded, and the §3.5 skill-capture outcome (skills created/extended, or
 the explicit "nothing reusable this session").
+
+Lead with the **§2.5 discharge block** if there is one — that is the prompter's next
+physical action and it must not be buried under the summary. Then say which tools this
+branch retired, and which ledger rows it opened or closed. If the branch added no editor
+tool, say "no tool discharge required" explicitly — silence reads as "nothing pending",
+which is exactly the failure this gate exists to prevent.
diff --git a/.claude/skills/ship/tool-discharge-check.sh b/.claude/skills/ship/tool-discharge-check.sh
new file mode 100755
index 000000000..5ba032f78
--- /dev/null
+++ b/.claude/skills/ship/tool-discharge-check.sh
@@ -0,0 +1,106 @@
+#!/usr/bin/env bash
+# tool-discharge-check.sh — evidence for the /ship §2.5 editor-tool discharge gate.
+#
+# Claude cannot run Unity, so an editor tool it writes is inert until a human clicks the
+# menu item and commits the asset diff. This script gathers the evidence needed to judge
+# whether that happened; it does NOT decide for you — read the output and classify.
+#
+#   usage: .claude/skills/ship/tool-discharge-check.sh [base-ref]   (default: bleeding-edge)
+#
+# Exit status is always 0: this is a report, not a CI gate.
+
+set -uo pipefail
+
+BASE="${1:-bleeding-edge}"
+LEDGER="Docs/EDITOR_TOOL_LEDGER.md"
+
+cd "$(git rev-parse --show-toplevel)" || exit 1
+
+# Prefer the remote-tracking ref: a stale local `bleeding-edge` silently widens the range
+# and makes already-merged tools look like this branch's undischarged work.
+if git rev-parse --verify --quiet "origin/$BASE" >/dev/null; then
+  if git rev-parse --verify --quiet "$BASE" >/dev/null \
+     && [ "$(git rev-parse "$BASE")" != "$(git rev-parse "origin/$BASE")" ]; then
+    echo "note: local '$BASE' differs from 'origin/$BASE' — using the remote ref."
+    echo
+  fi
+  BASE="origin/$BASE"
+elif ! git rev-parse --verify --quiet "$BASE" >/dev/null; then
+  echo "!! base ref '$BASE' not found (tried origin/$BASE too)." >&2
```

</details>

### `3193f058e` — docs(tools): seed the editor-tool ledger from a full [MenuItem] audit

_Claude, 2026-08-03 00:19:59 +0000_

```text
Audited every [MenuItem] in Assets/ against the artifacts it claims to produce,
then adversarially verified each actionable verdict. Six tools owe a discharge,
three need a fix first, three are vestigial, and four docs describe a reality
that no longer exists (or never did).

Corrections to the first ledger pass, all from adversarial review:
- Strip Crystal AudioSources: FindAssets(searchInFolders) recurses, so the target
  set is 11 prefabs, not 9 — including SpawnedSegments.prefab (25 MB, 1008
  inlined AudioSources) and BigCrystalVariant.
- Canvas Upgrader: mostly a doc bug, not a pending run. SplashScreen is not in
  build settings and GameCanvas-HexRace is overridden by all six consumers; only
  GameCanvas.prefab is a real gap, and it is blocked on a missing nested-prefab
  guard that would rescale an already-x2.4 nested instance to x5.76.
- Elemental petal bars: the bake tool cannot add a view, only re-author prefabs
  that already carry one, so its scope is Sparrow alone. CLAUDE.md's "null-safe,
  opt-in rollout" was false — the controller force-creates the view at runtime.

New failure classes this audit surfaced:
- run-then-clobber: Toy_Conveyor's omniCrystalPrefab was authored by a real tool
  run, then byte-reverted by a blanket "pushing automatic unity changes" commit,
  silently degrading ~16% of Wanderway crystals ever since.
- missing .meta: five tracked first-party .cs files have no tracked .meta (three
  editor tools + two runtime PrismClock sources), so their GUIDs differ per
  checkout. tool-discharge-check.sh now reports these, and section 3 detects
  unregistered tools instead of grepping prose.

Doc fixes: CLAUDE.md's nonexistent "Create Party Prefabs" tool and its stale
elemental-bars claim, the ElementalBarsController warning that named a tool
which no-ops for the vessels that trigger it, and PERFORMANCE_OPTIMIZATION.md's
"raycast audit never run" (its scene half landed at 9c5dd537).

Caveat recorded in the ledger: the clone is shallow (674 commits, to 2026-06-12),
so artifact existence on disk is the primary evidence, not commit archaeology.
```

```text
 .claude/skills/ship/SKILL.md                                 |   6 +
 .claude/skills/ship/tool-discharge-check.sh                  |  61 +++++++--
 Assets/_Scripts/Controller/Vessel/ElementalBarsController.cs |   6 +-
 CLAUDE.md                                                    |   4 +-
 Docs/EDITOR_TOOL_LEDGER.md                                   | 284 ++++++++++++++++++++++++++++++++---------
 Docs/PERFORMANCE_OPTIMIZATION.md                             |  17 ++-
 6 files changed, 296 insertions(+), 82 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 530 lines)</summary>

```diff
diff --git a/.claude/skills/ship/SKILL.md b/.claude/skills/ship/SKILL.md
index b99cd309a..0deccabac 100644
--- a/.claude/skills/ship/SKILL.md
+++ b/.claude/skills/ship/SKILL.md
@@ -92,6 +92,12 @@ reader to run it** — past tense, pointing at where the tool now lives, e.g.
 Docs must never point at a menu item that no longer exists, and a retired tool must always
 be recoverable by commit reference. Standing tools keep their menu path in the docs.
 
+**5.5 Same failure class, one step earlier: missing `.meta` files.** A session that authors
+a file without ever opening Unity commits the `.cs`/`.asset` **without its `.meta`**, so every
+teammate's editor mints a different GUID and any prefab/scene reference binds differently per
+checkout. Section 4 of the script lists these; committing them is part of the discharge, and
+it goes **before** any tool-output commit so those diffs stay clean.
+
 **6. Record it in `Docs/EDITOR_TOOL_LEDGER.md`.** Every tool the branch adds, runs, or
 retires gets its row updated. A one-shot tool that genuinely cannot be discharged before
 merge stays as a `⏳ PENDING` row carrying the owner and the full discharge block from
diff --git a/.claude/skills/ship/tool-discharge-check.sh b/.claude/skills/ship/tool-discharge-check.sh
index 5ba032f78..2933d2f6b 100755
--- a/.claude/skills/ship/tool-discharge-check.sh
+++ b/.claude/skills/ship/tool-discharge-check.sh
@@ -85,21 +85,60 @@ echo
 
 # --------------------------------------------------------------- 3. pending ledger rows
 echo "--- 3. open ledger obligations ($LEDGER)"
-if [ -f "$LEDGER" ]; then
-  if grep -q 'PENDING' "$LEDGER"; then
-    grep -n 'PENDING' "$LEDGER" | sed 's/^/    /'
+if [ ! -f "$LEDGER" ]; then
+  echo "    !! $LEDGER missing — create it (see /ship §2.5 step 6)"
+else
+  echo "    open discharge blocks (a human owes each of these a run + push):"
+  if grep -qE '^### D[0-9]+' "$LEDGER"; then
+    grep -nE '^### D[0-9]+' "$LEDGER" | sed 's/^/        /'
   else
-    echo "    (no PENDING rows)"
+    echo "        (none)"
   fi
   echo
-  echo "    one-shot rows marked RUN whose tool file still exists (retire these):"
-  found=0
-  while IFS= read -r path; do
-    [ -n "$path" ] && [ -f "$path" ] && { echo "        $path"; found=1; }
-  done < <(grep -oE 'Assets/[A-Za-z0-9_/.-]+\.cs' "$LEDGER" 2>/dev/null | sort -u)
-  [ "$found" -eq 0 ] && echo "        (none)"
+  echo "    rows marked RUN — these owe RETIREMENT (delete the tool + rewrite its docs):"
+  runrows=$(grep -n '✅ RUN' "$LEDGER" | grep -v 'One-shot, output committed' || true)
+  if [ -n "$runrows" ]; then
+    printf '%s\n' "$runrows" | cut -c1-140 | sed 's/^/        /'
+  else
+    echo "        (none)"
+  fi
+  echo
+  echo "    tools on disk with NO ledger row at all (unregistered — classify them):"
+  unreg=0
+  while IFS= read -r f; do
+    [ -n "$f" ] || continue
+    base=$(basename "$f" .cs)
+    grep -q "$base" "$LEDGER" 2>/dev/null || { echo "        $f"; unreg=1; }
+  done < <(grep -rl '\[MenuItem' Assets --include=*.cs 2>/dev/null \
+             | grep -viE 'Plugins/|PlayFabSDK/|NiceVibrations/|Wwise/|PrimitivePlus/|YethGameDev/|PlayFabEditorExtensions/' \
+             | sort)
+  [ "$unreg" -eq 0 ] && echo "        (none)"
+fi
+echo
+
+# ------------------------------------------------- 4. assets committed without their .meta
+# A session that authors a file but never opens Unity commits the .cs/.asset WITHOUT its
+# .meta. Unity then mints a different GUID on every teammate's machine, so any prefab or
+# scene reference to it binds differently per checkout. Same root cause as an un-run tool:
+# the editor step never happened.
+echo "--- 4. tracked first-party files missing their .meta"
+THIRD_PARTY='Plugins/|PlayFabSDK/|NiceVibrations/|Wwise/|PrimitivePlus/|YethGameDev/|PlayFabEditorExtensions/|SerializeInterface/'
+missing_total=0
+for ext in cs asset prefab unity shadergraph hlsl mat; do
+  n=$(comm -23 \
+        <(git ls-files "Assets/*.$ext" | grep -viE "$THIRD_PARTY" | sort) \
+        <(git ls-files "Assets/*.$ext.meta" | sed 's/\.meta$//' | sort))
+  c=$(printf '%s' "$n" | grep -c . || true)
+  if [ "$c" -gt 0 ]; then
+    echo "    .$ext ($c):"
+    printf '%s\n' "$n" | sed 's/^/        /'
+    missing_total=$((missing_total + c))
+  fi
+done
+if [ "$missing_total" -eq 0 ]; then
+  echo "    (none)"
 else
-  echo "    !! $LEDGER missing — create it (see /ship §2.5 step 6)"
+  echo "    → open the project in Unity once, then commit the generated .meta files."
 fi
 echo
 
diff --git a/Assets/_Scripts/Controller/Vessel/ElementalBarsController.cs b/Assets/_Scripts/Controller/Vessel/ElementalBarsController.cs
index 45925ce30..fb14617ea 100644
--- a/Assets/_Scripts/Controller/Vessel/ElementalBarsController.cs
+++ b/Assets/_Scripts/Controller/Vessel/ElementalBarsController.cs
@@ -80,8 +80,10 @@ namespace CosmicShore.Gameplay
 
             CSDebug.LogWarning($"[ElementalBarsController] '{name}' has no authored ElementalBarsView - " +
                                "creating one at RUNTIME so the fleet-required display still shows. " +
-                               "Author it into the HUD prefab: Tools > Cosmic Shore > Bake Elemental " +
-                               "Petal Bars Into All Vessel HUDs, then wire it to elementBars.");
+                               "To author it into the HUD prefab: add an ElementalBarsView to this " +
+                               "vessel's HUD, assign it to elementBars, then run Tools > Cosmic Shore > " +
+                               "Wire Elemental Petal Bars. (The 'Bake ... Into All Vessel HUDs' item only " +
+                               "re-authors prefabs that ALREADY carry a view, so it no-ops here.)");
             var go = new GameObject("ElementalBars (auto)", typeof(RectTransform));
             var rt = (RectTransform)go.transform;
             rt.SetParent(canvas.transform, false);
diff --git a/CLAUDE.md b/CLAUDE.md
index 63d0051c8..8a7b14508 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -1080,7 +1080,7 @@ Location: `_SO_Assets/Host Connection Data/`
 
 Location: `_Prefabs/UI Elements/Panels/Party/`
 
-Run `Tools > Cosmic Shore > Create Party Prefabs` in Unity Editor to generate missing prefabs with auto-wired component references. SO data container references (`HostConnectionDataSO`, `FriendsDataSO`, `SO_ProfileIconList`) must be wired manually in the inspector after creation.
+These prefabs are already on disk — build new ones by duplicating the closest existing panel. (A `Tools > Cosmic Shore > Create Party Prefabs` generator was documented here but **never existed in the repo**; do not go looking for it.) SO data container references (`HostConnectionDataSO`, `FriendsDataSO`, `SO_ProfileIconList`) are wired manually in the inspector.
 
 #### Scene Setup Checklist (Menu_Main)
 
@@ -1791,7 +1791,7 @@ At any total at most two adjacent colours show (e.g. +8 → 3 blue + 2 white). P
 
 **Single source of truth — `ElementalBarsConfigSO`** (`_Scripts/ScriptableObjects/`, asset at `Resources/ElementalBarsConfig.asset`). Per CLAUDE.md Config Separation, all shared look/feel lives here: the 5 tick colours, per-element petal sprites, and every juice timing/haptic. All vessels reference the one asset, so the spec can't drift between prefabs. Holds the petal math (`DistributePetalValues`, `ColorForTick`) and constants (`PetalCount=5`, `MinLevel=-5`, `MaxLevel=15`, `PetalSpacing=72`).
 
-**Per-vessel integration.** `ElementalBarsController` (on all 11 vessel prefabs — formerly named `SilhouetteController` before the vessel silhouette/trail-display HUD element it also drove was removed) is the driver: `InitializeElementBars()` calls `elementBars.Build()`, seeds levels, and subscribes to `ResourceSystem.OnElementLevelChange`. The `elementBars` reference is null-safe — vessels without the view wired simply show no bars (opt-in rollout). `SquirrelVesselHUDView` routes drift/joust/crystal juice into the view.
+**Per-vessel integration.** `ElementalBarsController` (on all 11 vessel prefabs — formerly named `SilhouetteController` before the vessel silhouette/trail-display HUD element it also drove was removed) is the driver: `InitializeElementBars()` calls `elementBars.Build()`, seeds levels, and subscribes to `ResourceSystem.OnElementLevelChange`. The element flower display is **required on every vessel, and wiring it is optional** — `InitializeElementBars()` force-creates an `ElementalBarsView` at runtime on any vessel whose prefab doesn't author one (parented to the vessel's HUD `Canvas`, with a warning). Squirrel and Sparrow author theirs; the rest are created at runtime. The five Canvas-less vessels (Urchin, Grizzly, Termite, Falcon, Shrike) get nothing until they have a HUD canvas. Note `Tools > Cosmic Shore > Bake Elemental Petal Bars Into All Vessel HUDs` only re-authors prefabs that *already* carry a view — it cannot add one, so it is not the fix for a vessel hitting that warning (add the view + assign `elementBars`, then run `Wire Elemental Petal Bars`). `SquirrelVesselHUDView` routes drift/joust/crystal juice into the view.
 
 **Zero-wire by default.** With no config or petalRoot assigned, the view loads `Resources/ElementalBarsConfig`, auto-creates a centred flower container per element, and loads petal sprites from `Resources/ElementPetals/{element}_petal`. To author explicitly (recommended for real positioning), run **Tools > Cosmic Shore > Wire Elemental Petal Bars** (assigns config + creates `*_Flower` containers), then position the containers. A petal authored in-prefab as `Petal{0..4}` under a container is reused (not duplicated) and normalised via `ElementalBarsView.ConfigurePetal`.
 
diff --git a/Docs/EDITOR_TOOL_LEDGER.md b/Docs/EDITOR_TOOL_LEDGER.md
index f54aa5031..07a5c0ca9 100644
--- a/Docs/EDITOR_TOOL_LEDGER.md
+++ b/Docs/EDITOR_TOOL_LEDGER.md
@@ -14,6 +14,12 @@ this one tracks *"a human must run this and commit the diff"*.
 Enforced by `/ship` §2.5 (the editor-tool discharge gate), whose evidence-gatherer is
 `.claude/skills/ship/tool-discharge-check.sh`.
 
+> **Why the script matters more than this file.** `UNITY_VERIFICATION_CHECKLIST.md` was
+> created 2026-07-22 with one entry and never updated again — a doc-only convention that
+> was abandoned after a single use. The gate is what keeps this file honest; if you find
+> yourself updating the ledger by hand without the gate having asked you to, the
+> convention is rotting again.
+
 ## Rules
 
 1. **Every tool gets a row.** Adding, changing, or retiring a `[MenuItem]` tool means
@@ -36,6 +42,10 @@ Enforced by `/ship` §2.5 (the editor-tool discharge gate), whose evidence-gathe
```

</details>

### `5ac234f83` — fix(tools): repair three broken editor tools, retire three vestigial ones

_Claude, 2026-08-03 00:31:21 +0000_

```text
The audit's fix-first and cleanup buckets, discharged in code so none of them
costs editor time.

Fixed:
- CanvasUpgradeProcessor.UpgradeRectHierarchy had no nested-prefab guard, so
  upgrading a canvas re-scaled any already-x2.4 nested fragment to x5.76. The
  canvas-less fragment path guarded its own re-runs via the upgraded-prefab log;
  descendants had nothing. CollectAlreadyUpgradedNested now computes the skip-set
  once per walk and all seven scaling loops consult it. Conservative by
  construction: a transform is skipped only when its nested instance root
  resolves to a real source asset whose GUID is positively in the log, so
  anything unresolvable scales exactly as before. This unblocks GameCanvas.prefab,
  the one genuine gap left in the canvas migration.
- LifeFormCrystalValidator filtered on LifeForm || LightFauna, but LightFauna and
  Boid are SIBLINGS under Fauna — so every Boid prefab was skipped and the tool
  reported a clean bill it never verified (TadPoleFauna, TermiteDrone). Now
  filters on ILifeFormEntity, the interface both branches implement, so future
  lifeform types are covered without another edit.
- ToastNotificationSetup wrote its settings asset to Assets/_SO_Assets while
  AddManagerToScene read from Assets/Resources. ToastNotificationAPI fetches it
  with Resources.Load, so the write path was invisible to the shipping game and a
  run would author a second asset nothing reads. Both now share one SettingsPath
  under Resources/.

Retired (each verified to have zero references across .cs/.prefab/.unity/.asset;
recover with `git show 3193f058 -- <path>`):
- ProfileAvatarBinder — binds a ProfileImage component with 0 instances and 0
  callers, superseded by ProfileDisplayWidget.
- PlayfabProductGenerator — authors catalog products against the deprecated
  PlayFab auth; the store is UGS Purchasing.
- TriangleWindowMeshGenerator — despite the name, spawned a procedural cube into
  the open scene and wrote nothing to disk. No consumer, and no artifact it could
  have left behind.
```

```text
 Assets/Editor/ToastNotificationSetup.cs                         |  17 ++-
 Assets/_Scripts/Editor/CanvasUpgrader/CanvasUpgradeProcessor.cs |  67 +++++++++
 Assets/_Scripts/Editor/LifeFormCrystalValidator.cs              |  10 +-
 Assets/_Scripts/Editor/PlayfabProductGenerator.cs               | 305 --------------------------------------
 Assets/_Scripts/Editor/PlayfabProductGenerator.cs.meta          |  11 --
 Assets/_Scripts/Editor/ProfileAvatarBinder.cs                   |  73 ---------
 Assets/_Scripts/Editor/ProfileAvatarBinder.cs.meta              |  11 --
 Assets/_Scripts/Editor/TriangleWindowMeshGenerator.cs           | 106 -------------
 Assets/_Scripts/Editor/TriangleWindowMeshGenerator.cs.meta      |  11 --
 Docs/EDITOR_TOOL_LEDGER.md                                      |  40 +++--
 10 files changed, 115 insertions(+), 536 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 773 lines)</summary>

```diff
diff --git a/Assets/Editor/ToastNotificationSetup.cs b/Assets/Editor/ToastNotificationSetup.cs
index 3477212e8..c38b42739 100644
--- a/Assets/Editor/ToastNotificationSetup.cs
+++ b/Assets/Editor/ToastNotificationSetup.cs
@@ -9,9 +9,17 @@ namespace CosmicShore.Editor
     public static class ToastNotificationSetup
     {
         private const string PrefabFolder = "Assets/_Prefabs/UI Elements";
-        private const string SOFolder = "Assets/_SO_Assets";
         private const string ChannelFolder = "Assets/Resources/Channels";
 
+        // MUST live under a Resources/ folder: ToastNotificationAPI fetches it with
+        // Resources.Load<ToastNotificationSettingsSO>(...) at runtime, so an asset written
+        // anywhere else is invisible to the shipping game. This constant is the single
+        // source of the path - CreateSettingsAsset and AddManagerToScene previously
+        // disagreed (writing to Assets/_SO_Assets, loading from Assets/Resources), so a
+        // run authored a second settings asset that nothing ever read.
+        private const string SettingsFolder = "Assets/Resources";
+        private const string SettingsPath = SettingsFolder + "/ToastNotificationSettings.asset";
+
         [MenuItem("Cosmic Shore/Toast Notification/Create All Assets", priority = 0)]
         public static void CreateAllAssets()
         {
@@ -27,14 +35,14 @@ namespace CosmicShore.Editor
         [MenuItem("Cosmic Shore/Toast Notification/Create Settings Asset")]
         public static void CreateSettingsAsset()
         {
-            var path = SOFolder + "/ToastNotificationSettings.asset";
+            var path = SettingsPath;
             if (AssetDatabase.LoadAssetAtPath<ToastNotificationSettingsSO>(path) != null)
             {
                 Debug.Log("[ToastNotification] Settings asset already exists at " + path);
                 return;
             }
 
-            EnsureFolder(SOFolder);
+            EnsureFolder(SettingsFolder);
             var settings = ScriptableObject.CreateInstance<ToastNotificationSettingsSO>();
             AssetDatabase.CreateAsset(settings, path);
             AssetDatabase.SaveAssets();
@@ -135,8 +143,7 @@ namespace CosmicShore.Editor
             var mgr = go.AddComponent<ToastNotificationManager>();
 
             // Wire settings
-            var settings = AssetDatabase.LoadAssetAtPath<ToastNotificationSettingsSO>(
-                "Assets/Resources/ToastNotificationSettings.asset");
+            var settings = AssetDatabase.LoadAssetAtPath<ToastNotificationSettingsSO>(SettingsPath);
             if (settings != null)
             {
                 var settingsField = typeof(ToastNotificationManager).GetField("settings",
diff --git a/Assets/_Scripts/Editor/CanvasUpgrader/CanvasUpgradeProcessor.cs b/Assets/_Scripts/Editor/CanvasUpgrader/CanvasUpgradeProcessor.cs
index d29c468b8..487abe05f 100644
--- a/Assets/_Scripts/Editor/CanvasUpgrader/CanvasUpgradeProcessor.cs
+++ b/Assets/_Scripts/Editor/CanvasUpgrader/CanvasUpgradeProcessor.cs
@@ -239,9 +239,16 @@ namespace CosmicShore.Editor
         {
             const float k = UpgradeScale;
 
+            // A hierarchy can contain NESTED prefab instances that were already upgraded on
+            // their own asset (the canvas-less fragment path). Their values are already x2.4,
+            // so scaling them again as part of this walk compounds to x5.76. The fragment path
+            // guards its own re-runs via the prefab log; this is the same guard for descendants.
+            var skipNested = CollectAlreadyUpgradedNested(pathRoot, report);
+
             foreach (var rt in pathRoot.GetComponentsInChildren<RectTransform>(true))
             {
                 if (!includeRoot && rt == pathRoot) continue;
+                if (skipNested.Contains(rt)) continue;
                 Vector2 ap = rt.anchoredPosition;
                 Vector2 sd = rt.sizeDelta;
                 bool apChanges = ap.sqrMagnitude > 0f;
@@ -264,6 +271,7 @@ namespace CosmicShore.Editor
             // --- TextMeshProUGUI ---
             foreach (var tmp in pathRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
             {
+                if (skipNested.Contains(tmp.transform)) continue;
                 report.AppendLine($"  {LabelPath(tmp.transform, pathRoot)} :: TextMeshProUGUI");
                 report.AppendLine($"      fontSize: {Fmt(tmp.fontSize)} -> {Fmt(tmp.fontSize * k)}, fontSizeMin: {Fmt(tmp.fontSizeMin)} -> {Fmt(tmp.fontSizeMin * k)}, fontSizeMax: {Fmt(tmp.fontSizeMax)} -> {Fmt(tmp.fontSizeMax * k)}");
                 bool marginChanges = tmp.margin.sqrMagnitude > 0f;
@@ -284,6 +292,7 @@ namespace CosmicShore.Editor
             // --- Legacy UnityEngine.UI.Text ---
             foreach (var text in pathRoot.GetComponentsInChildren<Text>(true))
             {
+                if (skipNested.Contains(text.transform)) continue;
                 int newSize = ScaleInt(text.fontSize, c);
                 int newMin = ScaleInt(text.resizeTextMinSize, c);
                 int newMax = ScaleInt(text.resizeTextMaxSize, c);
@@ -303,6 +312,7 @@ namespace CosmicShore.Editor
             // --- Layout groups (padding on the shared base; spacing/cellSize per subtype) ---
             foreach (var group in pathRoot.GetComponentsInChildren<LayoutGroup>(true))
             {
+                if (skipNested.Contains(group.transform)) continue;
                 var p = group.padding;
                 var newPadding = new RectOffset(ScaleInt(p.left, c), ScaleInt(p.right, c), ScaleInt(p.top, c), ScaleInt(p.bottom, c));
                 report.AppendLine($"  {LabelPath(group.transform, pathRoot)} :: {group.GetType().Name}");
@@ -342,6 +352,7 @@ namespace CosmicShore.Editor
             var pendingWrites = new List<System.Action>();
             foreach (var le in pathRoot.GetComponentsInChildren<LayoutElement>(true))
             {
+                if (skipNested.Contains(le.transform)) continue;
                 var sub = new StringBuilder();
                 pendingWrites.Clear();
                 ScaleLayoutSize(le.minWidth, v => pendingWrites.Add(() => le.minWidth = v), "minWidth", sub);
@@ -366,6 +377,7 @@ namespace CosmicShore.Editor
             // --- Shadow / Outline (Outline derives from Shadow) ---
             foreach (var shadow in pathRoot.GetComponentsInChildren<Shadow>(true))
             {
+                if (skipNested.Contains(shadow.transform)) continue;
                 report.AppendLine($"  {LabelPath(shadow.transform, pathRoot)} :: {shadow.GetType().Name} effectDistance: {Fmt(shadow.effectDistance)} -> {Fmt(shadow.effectDistance * k)}");
                 if (apply)
                 {
@@ -379,6 +391,7 @@ namespace CosmicShore.Editor
             // --- RectMask2D (padding/softness are canvas-unit values) ---
             foreach (var mask in pathRoot.GetComponentsInChildren<RectMask2D>(true))
             {
+                if (skipNested.Contains(mask.transform)) continue;
                 bool paddingChanges = mask.padding.sqrMagnitude > 0f;
                 bool softnessChanges = mask.softness != Vector2Int.zero;
                 if (!paddingChanges && !softnessChanges) continue;
@@ -913,6 +926,60 @@ namespace CosmicShore.Editor
                 PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
         }
 
+        /// <summary>
+        /// Collects every transform belonging to a NESTED prefab instance whose source asset is
+        /// already recorded in the upgraded-prefab log, so the hierarchy walk leaves it alone.
+        ///
+        /// Why this exists: a canvas-less UI fragment (PlayerScoreCard, an EndGameStatsPanel, a
+        /// vessel HUD variant) is upgraded x2.4 on its OWN asset via <see cref="UpgradePrefabRoot"/>,
+        /// which records it in <see cref="UpgradedPrefabLogPath"/>. When that fragment is also
+        /// nested inside a canvas being upgraded, the canvas walk would scale it a SECOND time and
+        /// compound to x5.76. The fragment path already guards its own re-runs against the log;
+        /// this applies the same guard to descendants.
+        ///
+        /// Conservative by construction: a transform is skipped ONLY when its nested instance root
+        /// resolves to a real source asset with a GUID that is positively present in the log.
+        /// Anything unresolvable is left in the walk and scaled as before.
+        /// </summary>
+        static HashSet<Transform> CollectAlreadyUpgradedNested(RectTransform pathRoot, StringBuilder report)
+        {
+            var skip = new HashSet<Transform>();
+            var log = LoadLog(UpgradedPrefabLogPath);
+            if (log.Count == 0) return skip;
+
+            foreach (var t in pathRoot.GetComponentsInChildren<Transform>(true))
+            {
+                if (t == pathRoot) continue;
+                if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
+
+                var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
```

</details>

### `14b5c0638` — chore(assets): discharge D1-D4 by asset surgery; scale nested roots in canvas guard

_Claude, 2026-08-03 18:20:24 +0000_

```text
Per /asset-surgery doctrine, everything mechanically dischargeable in the tool
ledger is now discharged in-branch - no editor run owed for D1-D4. Every edit
was validate-before-write: parse the whole file, assert the edit landed, assert
reference closure, only then write.

D1 - crystal AudioSource strip (tool now RETIRED): all 1,017 !u!82 documents
and their m_Component entries removed across the 10 Crystal-carrying prefabs
(9 crystals + 1,008 inlined in SpawnedSegments.prefab). Pure-deletion diffs.
Per-file asserts: removed ids appear nowhere, doc count dropped exactly, no
non-stripped GameObject left componentless, every remaining component ref
resolves (the stripped-doc distinction mattered: proxies into nested instances
are legitimately componentless - the validator caught its own too-broad rule
before writing Crystal.prefab). BigCrystalVariant inherits the stripped base,
avoiding the dangling m_RemovedComponents override an editor-ordered run
risked. Known dormant leftover: TadPoleFauna.prefab holds an m_Enabled
override targeting the removed component - Unity ignores it.

D2 - Toy_Conveyor.omniCrystalPrefab restored to the exact reference a0b32006
wrote (fileID re-verified against Crystal.prefab's root Crystal component).

D3 - Sparrow petal bake: 88 documents authored (4 *_Flower containers at the
wirer's default row, 20 petals: RectTransform + CanvasRenderer + Image),
donor-cloned from SquirrelHUDVariant's wirer-authored output; sprite guids
verified against Resources/ElementPetals metas, rotations against
ConfigurePetal's Euler(0,0,-72p), rest color = ColorForTick(0) grey; the
view's four bars[i].petalRoot wired; bidirectional parent/child closure
verified; the emitter's merged m_Children/m_Father line bug caught and
repaired with a line-grammar check.

D4 - five missing .meta files minted for the PrismClock sources (donor-cloned
MonoImporter block, repo-wide GUID collision sweep).

CanvasUpgrader guard corrected: the previous commit's skip-set wrongly
included the nested instance ROOT, whose RectTransform values render from
instance overrides recorded in the parent canvas's units and must migrate
with it. CollectAlreadyUpgradedNested now splits roots (RectTransform still
scales; other components skip) from internals (nothing scales), with inner
logged instances staying fully skipped via preorder claiming.

Ledger: D1-D4 rewritten as discharge records; "Remaining human steps" section
added (Unity import bless, D5 raycast prefab pass, GameCanvas upgrade, D6
benchmark decision, lifeform-crystal element choices, play-verifies). Static
composition-aware lifeform check recorded: 8 violations (TermiteDrone, worm
live-path; 6 Populations/ dead-path).
```

```text
 .claude/skills/ship/SKILL.md                                    |     2 +-
 .claude/skills/ship/tool-discharge-check.sh                     |     5 +-
 Assets/_Prefabs/Environment/ActiveCrystalMass.prefab            |    99 -
 Assets/_Prefabs/Environment/ActiveCrystalSpace.prefab           |    99 -
 Assets/_Prefabs/Environment/Crystal.prefab                      |    99 -
 Assets/_Prefabs/Environment/CrystalCharge.prefab                |    99 -
 Assets/_Prefabs/Environment/CrystalMass.prefab                  |    99 -
 Assets/_Prefabs/Environment/CrystalSpace.prefab                 |    99 -
 Assets/_Prefabs/Environment/CrystalTime.prefab                  |    99 -
 Assets/_Prefabs/Environment/MazeCrystal.prefab                  |    99 -
 Assets/_Prefabs/Environment/OldCrystalTime.prefab               |    99 -
 Assets/_Prefabs/Environment/Spawners/SpawnedSegments.prefab     | 99792 ------------------------------------
 Assets/_Prefabs/Spacevessels/Sparrow.prefab                     |  1674 +-
 Assets/_SO_Assets/Toys/Toy_Conveyor.asset                       |     2 +-
 Assets/_Scripts/Editor/CanvasUpgrader/CanvasUpgradeProcessor.cs |    80 +-
 Assets/_Scripts/Editor/PrismClockGraphWirer.cs.meta             |    11 +
 Assets/_Scripts/Editor/PrismClockSmokeTest.cs.meta              |    11 +
 Assets/_Scripts/Editor/PrismClockWiringValidator.cs.meta        |    11 +
 Assets/_Scripts/Editor/StripCrystalAudioSourceTool.cs           |    77 -
 Assets/_Scripts/Editor/StripCrystalAudioSourceTool.cs.meta      |     2 -
 Assets/_Scripts/Utility/PrismClock.cs.meta                      |    11 +
 Assets/_Scripts/Utility/PrismClockDiagnostics.cs.meta           |    11 +
 Docs/EDITOR_TOOL_LEDGER.md                                      |   234 +-
 23 files changed, 1880 insertions(+), 100934 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 546 lines)</summary>

```diff
diff --git a/.claude/skills/ship/SKILL.md b/.claude/skills/ship/SKILL.md
index 0deccabac..5d8c1c34c 100644
--- a/.claude/skills/ship/SKILL.md
+++ b/.claude/skills/ship/SKILL.md
@@ -53,7 +53,7 @@ work its output:
 | Kind | Definition | Fate |
 |---|---|---|
 | **Standing** | Validator, auditor, report, or generator meant to be re-run on demand (`Validate Clock Wiring`, `Audit Vessel Ability Rows`, `Measure Cell Environment Baselines`). | Keep. Ledger row = `standing`. |
-| **One-shot** | Authors/migrates/wires assets once, then is dead weight (`Setup Freestyle Toybox`, `Canvas Upgrader`, `Strip Crystal AudioSources`). | Must be **discharged**, then **retired**. |
+| **One-shot** | Authors/migrates/wires assets once, then is dead weight (e.g. the retired `Strip Crystal AudioSources` — discharged via `/asset-surgery`, then deleted). | Must be **discharged**, then **retired**. |
 
 **2. Prefer eliminating the obligation over documenting it.** Before writing a menu-item
 tool at all, check whether `/asset-surgery` can author the asset directly — a programmatic
diff --git a/.claude/skills/ship/tool-discharge-check.sh b/.claude/skills/ship/tool-discharge-check.sh
index 2933d2f6b..6ff3a2fce 100755
--- a/.claude/skills/ship/tool-discharge-check.sh
+++ b/.claude/skills/ship/tool-discharge-check.sh
@@ -89,8 +89,9 @@ if [ ! -f "$LEDGER" ]; then
   echo "    !! $LEDGER missing — create it (see /ship §2.5 step 6)"
 else
   echo "    open discharge blocks (a human owes each of these a run + push):"
-  if grep -qE '^### D[0-9]+' "$LEDGER"; then
-    grep -nE '^### D[0-9]+' "$LEDGER" | sed 's/^/        /'
+  openblocks=$(grep -nE '^### D[0-9]+' "$LEDGER" | grep -v 'DISCHARGED' || true)
+  if [ -n "$openblocks" ]; then
+    printf '%s\n' "$openblocks" | sed 's/^/        /'
   else
     echo "        (none)"
   fi
diff --git a/Assets/_Scripts/Editor/CanvasUpgrader/CanvasUpgradeProcessor.cs b/Assets/_Scripts/Editor/CanvasUpgrader/CanvasUpgradeProcessor.cs
index 487abe05f..cab8d2cd2 100644
--- a/Assets/_Scripts/Editor/CanvasUpgrader/CanvasUpgradeProcessor.cs
+++ b/Assets/_Scripts/Editor/CanvasUpgrader/CanvasUpgradeProcessor.cs
@@ -240,15 +240,22 @@ namespace CosmicShore.Editor
             const float k = UpgradeScale;
 
             // A hierarchy can contain NESTED prefab instances that were already upgraded on
-            // their own asset (the canvas-less fragment path). Their values are already x2.4,
-            // so scaling them again as part of this walk compounds to x5.76. The fragment path
-            // guards its own re-runs via the prefab log; this is the same guard for descendants.
-            var skipNested = CollectAlreadyUpgradedNested(pathRoot, report);
+            // their own asset (the canvas-less fragment path). Their INTERNAL values are already
+            // x2.4, so scaling them again as part of this walk compounds to x5.76. The fragment
+            // path guards its own re-runs via the prefab log; this is the same guard for
+            // descendants. The instance ROOT is the deliberate exception, RectTransform only:
+            // its anchoredPosition/sizeDelta render from the instance OVERRIDES recorded in THIS
+            // canvas (Unity always records the root RectTransform on placement), which are
+            // authored in this canvas's 800x450 units and must migrate with it. Its non-Rect
+            // components (a LayoutGroup's padding, a TMP's fontSize) read from the already-x2.4
+            // asset unless explicitly overridden, so they stay skipped with the internals.
+            CollectAlreadyUpgradedNested(pathRoot, report,
+                out var upgradedRoots, out var upgradedInternals);
 
             foreach (var rt in pathRoot.GetComponentsInChildren<RectTransform>(true))
             {
                 if (!includeRoot && rt == pathRoot) continue;
-                if (skipNested.Contains(rt)) continue;
+                if (upgradedInternals.Contains(rt)) continue;
                 Vector2 ap = rt.anchoredPosition;
                 Vector2 sd = rt.sizeDelta;
                 bool apChanges = ap.sqrMagnitude > 0f;
@@ -271,7 +278,7 @@ namespace CosmicShore.Editor
             // --- TextMeshProUGUI ---
             foreach (var tmp in pathRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
             {
-                if (skipNested.Contains(tmp.transform)) continue;
+                if (upgradedInternals.Contains(tmp.transform) || upgradedRoots.Contains(tmp.transform)) continue;
                 report.AppendLine($"  {LabelPath(tmp.transform, pathRoot)} :: TextMeshProUGUI");
                 report.AppendLine($"      fontSize: {Fmt(tmp.fontSize)} -> {Fmt(tmp.fontSize * k)}, fontSizeMin: {Fmt(tmp.fontSizeMin)} -> {Fmt(tmp.fontSizeMin * k)}, fontSizeMax: {Fmt(tmp.fontSizeMax)} -> {Fmt(tmp.fontSizeMax * k)}");
                 bool marginChanges = tmp.margin.sqrMagnitude > 0f;
@@ -292,7 +299,7 @@ namespace CosmicShore.Editor
             // --- Legacy UnityEngine.UI.Text ---
             foreach (var text in pathRoot.GetComponentsInChildren<Text>(true))
             {
-                if (skipNested.Contains(text.transform)) continue;
+                if (upgradedInternals.Contains(text.transform) || upgradedRoots.Contains(text.transform)) continue;
                 int newSize = ScaleInt(text.fontSize, c);
                 int newMin = ScaleInt(text.resizeTextMinSize, c);
                 int newMax = ScaleInt(text.resizeTextMaxSize, c);
@@ -312,7 +319,7 @@ namespace CosmicShore.Editor
             // --- Layout groups (padding on the shared base; spacing/cellSize per subtype) ---
             foreach (var group in pathRoot.GetComponentsInChildren<LayoutGroup>(true))
             {
-                if (skipNested.Contains(group.transform)) continue;
+                if (upgradedInternals.Contains(group.transform) || upgradedRoots.Contains(group.transform)) continue;
                 var p = group.padding;
                 var newPadding = new RectOffset(ScaleInt(p.left, c), ScaleInt(p.right, c), ScaleInt(p.top, c), ScaleInt(p.bottom, c));
                 report.AppendLine($"  {LabelPath(group.transform, pathRoot)} :: {group.GetType().Name}");
@@ -352,7 +359,7 @@ namespace CosmicShore.Editor
             var pendingWrites = new List<System.Action>();
             foreach (var le in pathRoot.GetComponentsInChildren<LayoutElement>(true))
             {
-                if (skipNested.Contains(le.transform)) continue;
+                if (upgradedInternals.Contains(le.transform) || upgradedRoots.Contains(le.transform)) continue;
                 var sub = new StringBuilder();
                 pendingWrites.Clear();
                 ScaleLayoutSize(le.minWidth, v => pendingWrites.Add(() => le.minWidth = v), "minWidth", sub);
@@ -377,7 +384,7 @@ namespace CosmicShore.Editor
             // --- Shadow / Outline (Outline derives from Shadow) ---
             foreach (var shadow in pathRoot.GetComponentsInChildren<Shadow>(true))
             {
-                if (skipNested.Contains(shadow.transform)) continue;
+                if (upgradedInternals.Contains(shadow.transform) || upgradedRoots.Contains(shadow.transform)) continue;
                 report.AppendLine($"  {LabelPath(shadow.transform, pathRoot)} :: {shadow.GetType().Name} effectDistance: {Fmt(shadow.effectDistance)} -> {Fmt(shadow.effectDistance * k)}");
                 if (apply)
                 {
@@ -391,7 +398,7 @@ namespace CosmicShore.Editor
             // --- RectMask2D (padding/softness are canvas-unit values) ---
             foreach (var mask in pathRoot.GetComponentsInChildren<RectMask2D>(true))
             {
-                if (skipNested.Contains(mask.transform)) continue;
+                if (upgradedInternals.Contains(mask.transform) || upgradedRoots.Contains(mask.transform)) continue;
                 bool paddingChanges = mask.padding.sqrMagnitude > 0f;
                 bool softnessChanges = mask.softness != Vector2Int.zero;
                 if (!paddingChanges && !softnessChanges) continue;
@@ -927,29 +934,47 @@ namespace CosmicShore.Editor
         }
 
         /// <summary>
-        /// Collects every transform belonging to a NESTED prefab instance whose source asset is
-        /// already recorded in the upgraded-prefab log, so the hierarchy walk leaves it alone.
+        /// Collects the parts of the hierarchy belonging to NESTED prefab instances whose source
+        /// asset is already recorded in the upgraded-prefab log, split into what the walk must
+        /// still touch and what it must leave alone.
         ///
         /// Why this exists: a canvas-less UI fragment (PlayerScoreCard, an EndGameStatsPanel, a
         /// vessel HUD variant) is upgraded x2.4 on its OWN asset via <see cref="UpgradePrefabRoot"/>,
         /// which records it in <see cref="UpgradedPrefabLogPath"/>. When that fragment is also
-        /// nested inside a canvas being upgraded, the canvas walk would scale it a SECOND time and
-        /// compound to x5.76. The fragment path already guards its own re-runs against the log;
-        /// this applies the same guard to descendants.
+        /// nested inside a canvas being upgraded, the canvas walk would scale its internals a
+        /// SECOND time and compound to x5.76. The fragment path already guards its own re-runs
+        /// against the log; this applies the same guard to descendants.
         ///
-        /// Conservative by construction: a transform is skipped ONLY when its nested instance root
-        /// resolves to a real source asset with a GUID that is positively present in the log.
-        /// Anything unresolvable is left in the walk and scaled as before.
+        /// The split matters:
+        /// <list type="bullet">
+        /// <item><paramref name="roots"/> — the OUTERMOST logged instance roots. Their
+        /// RectTransform values render from instance overrides recorded in THIS canvas, authored
+        /// in this canvas's units, so the RectTransform loop must still scale them; their other
+        /// components read from the already-upgraded asset and must not be re-scaled.</item>
+        /// <item><paramref name="internals"/> — everything strictly inside a logged instance
+        /// (including any logged instance nested deeper — its overrides live in the outer
+        /// fragment's already-x2.4 units). Nothing scales.</item>
+        /// </list>
+        ///
+        /// Conservative by construction: membership requires the nested instance root to resolve
```

</details>

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
