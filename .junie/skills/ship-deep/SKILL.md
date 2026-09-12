---
name: ship-deep
description: The thorough lane of the ship protocol - for a large branch, a LOCKED system (ecology, party/presence, threading, scoring, prism animation, elemental abilities), hand-authored asset YAML/JSON, or the end of a long session. Runs everything /ship does, then adds an adversarial re-read of the diff, a blast-radius sweep over every changed public surface, a semantic-merge duplicate scan, an asset-integrity pass, and a doc-drift sweep. Use for "ship it properly", "full review before the PR", "deep check", or any branch you would be uncomfortable defending line by line.
---

# Ship Deep — assume the diff is wrong until each part survives a check

**Read `.claude/skills/ship/SKILL.md` first and run all of it.** This file adds passes;
it replaces nothing. Run them after §2 and before the §4 go/no-go.

The premise: on a branch this size, "I read it and it looked right" has a known failure
rate, and every check below exists because a specific class of defect survived exactly
that reading. Each pass produces EVIDENCE — a command and its output — not an impression.

## D1. Adversarial re-read

Walk the diff hunk by hunk and, for each, answer out loud: *what input makes this
wrong?* Not "is this correct" — you already believe it is, which is why the pass is
adversarial. Concretely, per changed file:

- What does this assume about state set up elsewhere (init order, spawn timing, a
  NetworkVariable that has not replicated yet, a field still empty during a spawn chain)?
- Which of its early-outs can fire silently, and what does the caller see when it does?
- Which branch of it did you never trace? Trace it now.

Anything you cannot answer is a finding, not a note.

## D2. Blast radius, mechanically

Do not eyeball this — grep it.

```sh
# every public member the branch renamed, deleted or re-signed
git diff <merge-base>..HEAD -- '*.cs' | grep -E '^-\s*(public|protected|internal)' 
# then, per member:
grep -rn '\.<Member>\b' Assets --include=*.cs
```

- Every caller migrated? A member that still appears outside historical docs is a finding.
- Deleted or renamed an asset? Sweep its **GUID** across `Assets/**` (`.unity`,
  `.prefab`, `.asset`, `.meta`, `.shadergraph`) — a dangling GUID is usually
  project-wide, not local (`/asset-surgery` §5).
- Deleted a system? Grep its NAME across code AND docs and rewrite the comments that
  still describe it as live. Harder case: the system SURVIVED but its ROLE changed —
  grep the retired tier's name for *fallback / falls back / legacy path / degrades to*
  and re-read each hit against what the code now does.

## D3. Semantic-merge scan (after any merge of sibling branches)

Zero conflict markers is not evidence the merge is correct. Two branches adding the same
member at different offsets auto-merge into a `CS0102` that only Unity will find.

1. Narrow to the files the merge genuinely combined (changed relative to BOTH parents) —
   the recipe is in `/asset-surgery` § "a clean merge can still be a semantic conflict".
2. Scan those for repeated member names; verify the enclosing class before calling one a
   defect.
3. Same trap in docs: two branches both claiming `§4.6` merges clean and produces a
   document with two of them. Renumber yours and fix every inbound reference.

## D4. Asset integrity (any branch that touched serialized assets)

- Every new asset has a `.meta`; no orphan `.meta`; every new GUID appears exactly once
  across `Assets/**/*.meta`.
- Hand-authored MonoBehaviour/SO YAML: diff its top-level keys against the C# class's
  `[SerializeField]`/public field names **and its base classes**, both directions. A key
  you misspelled is silently dropped; a field you omitted silently takes its initializer.
- No `Missing (Mono Script)` rows: every `m_Script` GUID in a changed prefab/scene
  resolves to a file that still exists.
- Shader/graph edits: the property you rely on is actually referenced in the graph text
  (`Material.HasProperty` cannot see an unexposed property — `/asset-surgery` §5).

## D5. Verification matrix, written down

A table, not a paragraph: one row per changed system, columns = *verified how* (edit-mode
test / offline sim / compile / in-editor play by the human / not verified). "Compiles by
inspection" is a legitimate row value; leaving a row blank is not. This table becomes the
PR's verification section verbatim, and it is where an honest NO usually announces itself.

## D6. Doc-drift sweep

Beyond §3's "update what you touched": grep the docs for claims this branch made false.

- Every `Docs/**/*.md` and `CLAUDE.md` sentence naming a system you changed — still true?
- Key-files tables, status tables and per-vessel/per-mode matrices: still accurate?
- Menu paths you moved (`FrogletTools/...`), skills you added, invariants you extended.

## D7. §2.5, held to the deep standard

The base gate asks whether a WRITER tool's output landed. Here, also:

- Read the tool's write path and enumerate WHICH assets it can touch, then confirm the
  committed diff is consistent with that set — output in files the tool cannot write is
  someone else's change riding along; files it should have written and didn't are a
  half-run.
- If the tool is idempotent, say so and say how you know (it re-reads before writing / it
  guards on an already-applied marker). A non-idempotent tool that stays in the repo is a
  loaded gun; retire it or make it idempotent.

## Then

Return to `/ship` §3.5 (skill capture — a branch this size almost always taught
something), §4 (go/no-go), §5 (PR). The PR body carries D5's matrix and D2's sweep
results; the report carries every pass's verdict, including the ones that found nothing —
"D3 found no duplicate members across the 2 genuinely-merged files" is a result.
