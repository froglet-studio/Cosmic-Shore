# Dev tasks from QA failures

Every entry here was created by a `FAIL` in a submitted `Docs/QA/RESULTS/` file.
Written by the `/qa-backlog` skill — add detail freely, but do not delete an entry
by hand: it closes when its QA item passes on a later run.

**Definition of done for every task below:** the named QA item passes on a build
that contains the fix. Nothing here is done because the code "looks right" — that
is exactly how these items got onto the QA list in the first place.

Status: 🔵 open · 🟠 in progress (branch named) · 🟢 fixed, awaiting retest.

<!-- qa-dev-tasks -->

*No open tasks. This file fills in as QA submits results.*


<!-- /qa-dev-tasks -->

---

### Entry format (for reference)

```
## DT-NNN — <one-line symptom> 🔵
- **QA item:** QA-PRISM-OCCLUSION (step 1)
- **Failed on:** bleeding-edge @ 2e2d3aaf, Unity 6000.0.x, Editor/Windows, 2026-08-06 by <tester>
- **Observed:** <verbatim console text / description>
- **Source of the change:** PR #661 (`claude/transparent-prism-occlusion-3fwjky`)
- **Likely files:** `_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl`, `PrismOcclusionCorridor.cs`
- **Done when:** QA-PRISM-OCCLUSION passes.
```
