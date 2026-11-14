# /ship §3.6 Refactor-opportunity pass in full

<!-- Moved word for word out of .claude/skills/ship/SKILL.md on 2026-11-13 (Shombith: "/ship is very slow; it does not need to read every line every time"). SKILL.md keeps the procedure; this file is read when `ship_triage.py` or the step says so. -->

Read when SKILL.md's step says so (its trigger matched this branch).


§3.5 harvests what the session learned. This harvests what the session **saw and did not
fix**. It runs here for one reason: by now you have read every hunk adversarially and
opened every doc the branch touches, so the measurement a `/refactor` pass would have to
spend an hour deriving is already in your hands. It will not be next week.

**The output is rows in the owning `BACKLOG`/`TODOS`/`REFACTOR` doc (or `spawn_task`),
each carrying the command and its output. Never an edit to this branch.** `/refactor` §4
forbids widening for the same reason §4 below forbids mixing an experiment with finished
work: a branch that grows a second subject cannot be reviewed as either one.

Three sources, in descending order of how often they pay:

1. **Debt this branch CREATED.** Every "kept for compatibility" field, every accessor you
   left because something might still read it, every tool you kept rather than retired
   (§2.5), every doc paragraph you SOFTENED rather than deleted because you were not sure
   the old behaviour was gone. Each is a row with a blocking question, and you are the
   only person who will ever know it is there.
2. **Debt this branch WALKED PAST.** The sibling class with the same latent bug, the dead
   component the guid sweep turned up, the shared-asset write on a live path, the second
   copy of the constant you just moved. One pass found four of these and put all four in
   rows; that is the expected yield, not an unusually messy branch.
3. **Debt the branch made VISIBLE.** A count, table or "N of M" claim you had to
   re-derive in §3 — if a hand-maintained table was wrong, the row is *"ask the tool"*,
   not *"fix the table"* (`/refactor` §7).

Check what you saw against `/refactor` §3's ten shapes before writing the row, because
the shape is what makes it actionable — *a read whose writer is elsewhere*, *two members
sharing a name*, *"referenced by nothing"*, *an absent YAML key falling back to the
initializer*, *a sentinel read as a measurement*, *a silent clamp*, *per-instance state on
a shared SO*, *prose describing a deleted system*, *a spent one-shot `assert` above a
generator's validation*.

Two rules that decide whether the row is worth writing:

- **Incompleteness is a REPORT; inconsistency is a FIX.** A vessel with an open design
  slot is not debt — it is unfinished design, and a row that files it as cleanup invites
  somebody to invent the design. Say which one it is.
- **A row with no measurement is worse than no row.** It becomes the next session's claim
  to disprove, which is exactly the failure `/refactor` exists to answer. If you cannot
  attach evidence, write down what you would have to measure instead of asserting what
  you suspect.

A branch that found nothing says so in the report. Silence reads as "not checked".
