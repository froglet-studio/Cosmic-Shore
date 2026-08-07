# QA Dev Tasks — failures converted to handoff work

The handoff surface for engineering. **Owner: the `/qa-backlog` skill — do not hand-edit.**

Each failed QA item becomes one task below, keyed by its QA item ID. The definition of
done is always literally "the QA item passes" — when the fix merges, the QA item is still
marked 🔴 on the backlog, so it returns to the top of the next list automatically.

The `<!-- devtask:QA-... -->` markers let the apply engine refresh a task in place instead
of duplicating it.

<!-- devtask:QA-EDITMODE-TESTS -->
### QA-EDITMODE-TESTS — run the test suites that were written but never executed
- **Failed on:** claude/ftue-editor-tool-69acq5 @ 68d2dab · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-07, andrew)
- **Symptom:** None of the required tests were even there for me to test.
- **Definition of done:** QA item `QA-EDITMODE-TESTS` passes.
<!-- /devtask:QA-EDITMODE-TESTS -->
