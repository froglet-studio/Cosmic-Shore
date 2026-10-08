# Bug Hunt & Console Cleanup

This folder records the bug-hunt and console-cleanup work: what was fixed, how it was proven,
and how to handle the same kinds of problems next time.

| File | What it is |
|---|---|
| [`../BUG_HUNT_HANDOFF_2026-09.md`](../BUG_HUNT_HANDOFF_2026-09.md) | **The open backlog.** Every unfixed item, with its trigger, consequence and minimal fix. §0 lists what has shipped. This folder does not repeat that list. |
| [`FIX_LOG.md`](FIX_LOG.md) | One short report per shipped fix, newest first: symptom, root cause, repro + evidence, fix, verification, PR/commit. Ends with the known open console issues. |
| [`PLAYBOOK.md`](PLAYBOOK.md) | Troubleshooting by problem class (native leaks, objects re-spawned during teardown, YAML parse errors, teardown-order NREs, leaked materials) and how to get the Editor log. |

## Workflow

1. **Reproduce on `bleeding-edge` first.** Capture the evidence: the exact console/`Editor.log`
   lines, with stack traces if you can (see PLAYBOOK "Getting the log"). A fix without a
   before-picture can't be verified.
2. **Fix on the `Bug_Hunt` branch, one item per commit.** Keep each fix small enough for a
   reviewer to read on its own. Keep LF line endings, and don't commit `.meta` changes unless
   they're intended.
3. **Run the four gate scripts** before pushing. They are textual gates, not a compile:
   ```
   python3 Tools/Build/check_enum_member_references.py --check
   python3 Tools/Build/check_using_directives.py --check
   python3 Tools/Build/check_conditional_compilation.py
   python3 Tools/Build/check_self_referential_locals.py --all
   ```
4. **Push to `origin/Bug_Hunt`.** Yash pulls it, compiles in the Editor and retests the same
   repro. Nothing is "fixed" until the Editor retest passes.
5. **After he confirms,** open a PR from `Bug_Hunt` to `bleeding-edge` and merge it with a
   **merge commit**. `Bug_Hunt` stays open for the next item. Merge `bleeding-edge` back into it
   when it falls behind.
6. **Record it:** add a report to the top of [`FIX_LOG.md`](FIX_LOG.md), and move the item out of
   §1–§5 of the handoff doc into its §0 shipped table.

The same "a fix is not believed until the game proves it" rule drives the in-editor Bug Ledger;
see [`../DIAGNOSTICS.md`](../DIAGNOSTICS.md).

## Finding the next batch (what worked on 2026-10-08)

A read-only hunt split by subsystem (vessels, arcade/scoring, ecology, UI, multiplayer/party,
data/economy, input/audio/AI; then projectiles/AOE, the newer modes, FTUE/weekly/menus), each
asked for "small, local fix, large consequence" findings with a CONCRETE caller-to-failure trace
and a confidence level, produced ~45 real bugs from ~55 reports. Two rules made that ratio:

- **Verify every finding yourself before touching code.** Re-read the path and check the asset
  wiring (grep the script's `.meta` guid in prefabs/scenes; a serialized field's value per
  instance). About one report in six was deliberate, latent behind an unassigned field, or a
  design call - those go to the handoff, not into a commit.
- **Recurring classes worth sweeping on their own:** scaled waits in code that runs at
  `timeScale 0` (every non-HOME menu screen), `??`/`??=` on Unity objects (Editor fake null), a
  latch with no reset on the in-place replay path, server-only code whose effect every peer needs
  (and the reverse), runtime `new Material`/`new Mesh` never destroyed, and saves that report
  failure by returning `false` rather than throwing.

