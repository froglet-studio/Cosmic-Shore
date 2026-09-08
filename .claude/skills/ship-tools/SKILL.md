---
name: ship-tools
description: Land an editor tool's ASSET OUTPUT and then retire the tool. Use whenever this session (or this branch) wrote a Unity editor tool, wirer, setup script, migration, or FrogletTools menu item that the human then RAN - the tool gets committed, its scene/prefab/SO output sits forgotten in the working tree, the PR merges, and the change is broken everywhere. Prompts the human to confirm they ran it and saved, verifies the output really is on the branch, commits it as its own commit, deletes the one-off tool, and pushes. Triggers - "did the tool changes get pushed", "commit the tool output", "clean up the tool", "validate and push", "ship the tool changes". Modes - check (report only), auto (no prompts), keep (do not retire).
---

# Ship Tools — the output is the deliverable, the tool is scaffolding

**The failure this closes.** An agent writes an editor tool because Unity work needs the
running editor. The human clicks it. It rewrites a scene, re-authors a prefab, generates
an SO. That output lands in the human's **working tree** — and the branch only carries
what someone chose to commit. The tool is in the diff (the agent wrote it, so the agent
committed it); the data it produced is not. The PR merges, the code expects data nobody
pushed, and every other machine is broken with nothing in the diff to explain it.

Two asymmetries make this specific failure likely rather than rare:

- The agent that wrote the tool **cannot see its output** — the tool runs in the human's
  editor, minutes or days later, in a different process.
- The output looks like noise. A regenerated `.unity` is thousands of YAML lines that
  nobody reads, so `git status` showing it dirty is easy to scroll past.

So the confirmation has to be **asked for**, not inferred, and the commit has to be
**scoped**, not `git add -A`.

## Modes

| Invocation | Behaviour |
|---|---|
| `/ship-tools` | Interactive: classify → prompt → verify → commit output → retire → push. |
| `/ship-tools check` | Report only. Reads nothing but git; writes nothing, pushes nothing. |
| `/ship-tools auto` | No prompts. Commits and pushes whatever is attributable to a tool; retires nothing. For an unattended pass. Refuses on a protected branch. |
| `/ship-tools keep` | Full interactive flow, but the tool stays (say why in the report). |

This skill does **not** open a pull request — it is the gate that runs before one. When
the branch is ready, `/ship` (which contains this as its §2.5) opens it.

## 1. Find the tools

```sh
BASE=$(git merge-base origin/bleeding-edge HEAD)
git diff --name-only $BASE..HEAD -- '*.cs' | xargs -r grep -l 'MenuItem("FrogletTools/'
git status --porcelain -- '*.cs' | grep -i editor       # uncommitted tools too
```

Also read `Library/FrogletToolChangeLedger.json` if it exists — it names the exact paths
each tool wrote on this machine. It is gitignored and machine-local, so a remote container
will not have it; its absence proves nothing.

## 2. Classify each one from its code, never its name

| Kind | Evidence | Expected on the branch |
|---|---|---|
| **READER** | only logs / builds a report; no `AssetDatabase.CreateAsset`/`SaveAssets`, `PrefabUtility.*`, `EditorSceneManager.Mark*`/`Save*`, `SerializedObject.ApplyModifiedProperties`, `File.Write*`, `AssetDatabase.DeleteAsset` | nothing — an auditor's whole job is to change nothing |
| **WRITER** | any of the above | its output, committed |

A tool that both audits and offers a "fix" button is a WRITER. Say so.

## 3. Establish the truth about each WRITER

Two checks, both required, before you ask the human anything:

```sh
git status --porcelain -- Assets ProjectSettings
git diff --stat $BASE..HEAD -- '*.unity' '*.prefab' '*.asset' '*.mat' '*.shadergraph'
```

Read the result into one of four states, per tool:

| State | What you see | Next |
|---|---|---|
| **Landed** | output present in the branch diff, working tree clean | verify (§5), done |
| **Stranded** | dirty `Assets/**` that matches what the tool writes | verify, then commit it (§5–6) |
| **Never ran** | tool in the diff, no output anywhere, tree clean | ask (§4) |
| **Not applicable** | READER | say so explicitly; do not ask |

"Never ran" and "ran but never saved" are indistinguishable from here. That is precisely
why the next step is a question and not a guess.

## 4. Prompt the human (blocking — skipped only in `auto` and `check`)

Use `AskUserQuestion`. Name the tool, its menu path, and the asset paths it targets. Offer:

- **Ran it, saved, it looks right** → proceed to §5.
- **Ran it, not sure it saved** → tell them: focus Unity, `File ▸ Save Project` (and
  `Ctrl/Cmd+S` for any open scene), then say done. Re-run §3 afterwards — the tree changed
  under you.
- **Haven't run it yet** → **NO-GO.** Give them the exact menu path and what to expect,
  and stop. Do not open a PR, do not retire anything.
- **It's read-only** → you misclassified it; re-read §2 and record the correction.

Point them at the in-editor equivalents, which answer all of this in one click:
**FrogletTools ▸ Build ▸ Pending Tool Changes**, or the **Validate & Push** button on the
tool's own window.

## 5. Verify the output before you commit it

Never commit tool output blind. It is the one commit nobody reviews, so you review it:

- Every new asset has its `.meta`; no orphan `.meta` (a `.meta` whose asset is gone).
- Every new GUID appears exactly **once** across `Assets/**/*.meta`.
- No `Missing (Mono Script)` rows: every `m_Script` GUID in a changed prefab/scene still
  resolves.
- The changed set is consistent with what the tool's write path can actually touch — read
  the tool and enumerate it. Files it cannot write are somebody else's change riding
  along: **split them out**, do not sweep them in.
- `python3 Tools/Build/check_conditional_compilation.py` if any script changed.

## 6. Commit the output as its own commit

```sh
git add -- <the exact paths>          # never -A, never a wildcard
git commit -m "chore(tools): <ToolName> output — N file(s)"
git push -u origin <branch>
```

Its own commit, because that is what makes it reviewable and revertable independently of
the code that consumes it. If the output is large and mechanical, say so in the commit
body along with the tool and menu path that produced it — the next person needs to know it
is regenerable and how.

## 7. Retire the one-off (default; skipped by `keep` and `auto`)

A tool written to perform ONE migration is scaffolding. Once its output is verified and
pushed, delete it and its scratch assets in their own commit:

```
chore(tools): retire <ToolName> after verification
```

**Keep it instead** — and say why in the report — when it is idempotent and re-runnable:
an auditor, a validator, a wirer someone will need again, anything the docs point at.
Those belong in `Docs/TOOLING.md`'s tool index; scaffolding does not.

Order matters: **output first, retirement second.** Deleting the tool while its output is
still uncommitted strands the output with nothing left that could reproduce it.

## 8. Report

- Every tool: name, menu path, READER/WRITER, and its state from §3.
- Which commit carries the output, and what §5 checked on it.
- What was retired, and what was kept with the reason.
- Anything a human must still do (an unrun tool, an unsaved editor), stated as a blocker.

---

## The in-editor half — every tool you write carries these two buttons

An agent cannot run Unity, so the human's editor is where the output appears and where it
is easiest to lose. `FrogletToolShipPanel` puts the fix at the point of the mistake.
**Any tool you author that writes assets must record what it wrote and draw the panel** —
full API and the authoring rules are in `Docs/TOOLING.md` § "Tool output is a deliverable".

```csharp
using CosmicShore.Editor.Froglet;

sealed class MyWirer : EditorWindow
{
    const string ToolName = "Wire Foo Widgets";

    static readonly FrogletToolShipContext Ship = new FrogletToolShipContext(ToolName)
    {
        ToolScriptPaths = new[] { "Assets/_Scripts/Editor/FrogletTools/MyWirer.cs" },
        Validate = () => ValidateWiring(),   // the tool's own correctness check
    };

    void Wire()
    {
        // ... write assets ...
        FrogletToolChangeLedger.Record(ToolName, changedAssetPath);   // as you write
        FrogletToolChangeLedger.RecordOpenScenes(ToolName);           // if you edited scenes
    }

    void OnGUI()
    {
        // ... the tool's own UI ...
        FrogletToolShipPanel.Draw(Ship, this);   // Validate & Push + Retire Tool
    }
}
```

- **Validate & Push** — saves assets and open scenes, runs the built-in checks (`.meta`
  present, no orphan metas, no unsaved scenes, scripts compile) plus the tool's own
  `Validate`, then stages **only** this tool's recorded paths, commits and pushes to the
  current branch. Everything else dirty is listed and deliberately left alone.
- **Retire Tool** — deletes the tool's own scripts and scratch assets and commits the
  removal. Refuses while the tool still has unpushed output, so retirement can never
  strand it.
- Neither touches a protected branch (`main`, `master`, `bleeding-edge`, `develop`,
  `release/*`).

The fallback for tools that never drew the panel is **FrogletTools ▸ Build ▸ Pending Tool
Changes**: it lists every tool's recorded output *and* every other dirty project file, so
nothing hides in the gap between what a tool recorded and what it actually wrote.
