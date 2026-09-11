# Prompt — correct the five documents that read as authoritative and are wrong

Paste everything below into a fresh session.

---

Five documents in this repository state things that are no longer true. Each one reads as
authoritative, each one is cited by other work, and one of them describes a milestone the project
stopped pursuing six weeks ago. They are cheap to fix and expensive to leave: the whole point of
this project's documentation discipline is that a doc is the thing you read *instead of*
re-deriving, and a confidently wrong doc costs more than an absent one.

Read `Docs/STEAM_RELEASE_TASKS.md` (item R8) and `Docs/STEAM_CHECKPOINT_REV3_READINESS_AUDIT.pdf`
§05 first — the audit's drift table is the source of this list.

## The five, measured 10 Sep 2026 — re-verify each before editing

**1 · `Docs/STEAM_EA_INVESTOR_CHECKPOINT.html` / `.pdf` is Revision 1 (28 July).**
It still describes a **paid Early Access launch**. Revision 2 (31 July) replaced that destination
with an invite-only Steam Playtest, and Revision 3 (10 Sep) is the readiness audit now sitting
beside it as `STEAM_CHECKPOINT_REV3_READINESS_AUDIT.*`. Every runbook that cites the checkpoint
inherits the wrong destination from Rev 1.

*The fix is not to edit Rev 1.* It is a historical investor document and should stay intact. Add a
short banner at the top pointing forward to the current revision, and — if someone can supply the
Rev 2 PDF, which exists but is **in no repository** — commit it so the series reads 1 → 2 → 3.
Flag the missing Rev 2 explicitly rather than papering over the gap.

**2 · `Docs/QA/QA_BACKLOG.md` is 145 PRs stale.**
Generated 2026-08-13, covering PRs #583–#710. The repository is at #855. Nine game modes landed
after the scan.

*Do not hand-edit this file* — its own header says the `/qa-backlog` skill owns it. This entry is
here so the sweep does not skip it: the fix is to run the skill, which is tracked separately as
task **R2** and is a prerequisite for R3 and the bug bash. If R2 has already run, just confirm the
generated date moved.

**3 · `Docs/UI_REDESIGN_TASKS.md` understates what shipped.**
The status table says **T1 IN PROGRESS** and **T3 TODO**. Both have landed:

- T1 — `Assets/_Scripts/UI/SafeAreaFitter.cs` and `SafeAreaLayer.cs` exist, with
  `SafeAreaFitterTests.cs` beside them.
- T3 — `python3 Tools/Build/gamecanvas_unification_report.py --check` reports
  *"OK (15 scenes on Assets/_Prefabs/CORE/GameCanvas.prefab, fork gone, no override walls)"*, and
  `GameCanvas-SkimRace.prefab` no longer exists.

Check T2 as well — T3 depended on it, so it is unlikely to still be genuinely TODO. Use the
`/ui-redesign-tracker` skill if it applies; it exists to verify these against the working tree
rather than trusting a claim, which is exactly this situation.

**4 · `Docs/PERFORMANCE_OPTIMIZATION.md` is two months stale.**
File/line references were verified **2026-07-08**; the session handoff at §0 is dated 2026-07-15.
Roughly fifteen game modes have landed since. Its §4 backlog is the input to D1 and D3.

Do **not** re-verify the whole document — that is a perf session's job, not a doc sweep's. Add a
dated staleness note at the top saying what was verified when, so the next reader knows which
numbers to re-measure. Item R7 adds the load-time section separately.

**5 · Checkpoint Rev 2 item B2 says "Wwise audio init".**
The project's audio middleware is **FMOD** (`Assets/Plugins/FMOD`, `FMODUnity`). `Assets/Wwise/`
survives from an earlier middleware evaluation and has **zero** first-party references — measured:
no `AkSoundEngine` or `AkAudioListener` usage anywhere in `Assets/_Scripts`, against 14 files using
`FMODUnity`. A PC sanity pass written against Wwise would test nothing.

This one is in a PDF you cannot edit. Record the correction where the work actually happens: in
`Docs/STEAM_RELEASE_TASKS.md` under R5/H-lane B2, and in `Docs/AudioSystem/FMOD_AUDIT.md` if it
does not already say so. CLAUDE.md already states the Wwise folder is inert — check whether that is
enough or whether the B2 checklist needs its own line.

## While you are in there — one thing to check, not assume

`Docs/UNITY_VERIFICATION_CHECKLIST.md` marks itself superseded by `Docs/QA/`. Confirm its two
remaining open items have either been run or migrated into the QA backlog, so the supersession is
real rather than an orphan with live content in it.

## Constraints

- **Do not rewrite history.** Rev 1 stays as Rev 1; stale perf numbers get a dated note, not a
  silent overwrite. The point is that a reader can tell *when* something was true.
- **Do not hand-edit tool-owned files.** `QA_BACKLOG.md` belongs to `/qa-backlog`; `DEV_TASKS.md`
  closes entries when its QA item passes, never by hand.
- Where a claim can be verified by running something, run it and quote the output rather than
  asserting. The gates used above (`gamecanvas_unification_report.py --check`,
  `check_gamelist_scenes.py`) are seconds each and need no Unity.
- Keep each edit small and obviously scoped. This is five independent corrections, not a rewrite.

## Definition of done

1. Rev 1 carries a forward pointer; the Rev 2 gap is either filled or explicitly recorded as
   missing.
2. `UI_REDESIGN_TASKS.md` reflects the working tree, with each status change justified by something
   that was actually run.
3. `PERFORMANCE_OPTIMIZATION.md` carries a dated staleness note.
4. The Wwise/FMOD correction is recorded where B2 will be executed from.
5. `UNITY_VERIFICATION_CHECKLIST.md` is either genuinely empty of live items or its items are
   migrated.
6. `Docs/STEAM_RELEASE_TASKS.md` R8 is ticked.
