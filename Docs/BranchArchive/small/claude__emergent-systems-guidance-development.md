# Branch archive: `claude/emergent-systems-guidance-development`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-04-20 by Claude
- **Unmerged commits:** 2
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/491
- **Forked from:** `8a3d6d0d3` (2026-04-16, Merge pull request #483 from froglet-studio/claude/update-skimmer-prism-effect)
- **Tip:** `7aec3a3c2`
- **Files touched (1):**
  - `CLAUDE.md`

### `8ba96ed63` — Add emergent-systems design guidance to CLAUDE.md

_Claude, 2026-04-20 20:18:30 +0000_

```text
Appends a "Favor Emergent Systems Over Bespoke Solutions" section
describing a 4-step order of preference (use → tune → extend → bespoke)
and requiring explicit prompter permission before "cheating" emergence.
```

```text
 CLAUDE.md | 54 ++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 54 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/CLAUDE.md b/CLAUDE.md
index 18ed57502..68b7cf1ad 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -133,3 +133,57 @@ Every technical decision should be weighed against: **does this help the GDC dem
 - Use `Debug.Log` as a fix — it's a diagnostic tool, not a solution
 - Leave TODO comments as a substitute for completing the work
 - Generate code that compiles but ignores the established architecture patterns above
+
+## Design Philosophy: Favor Emergent Systems Over Bespoke Solutions
+
+Cosmic Shore is built on a small number of fundamental systems (mass, color,
+biomes, trails, flora/fauna, prisms, etc.) whose interactions produce a large
+number of desirable emergent outcomes. When solving a problem, maintain active
+awareness of these systems and prefer solutions that work *through* them rather
+than *around* them.
+
+### Order of preference
+
+When addressing a task, try these approaches in order and stop at the first one
+that fits:
+
+1. **Use an existing system.** Can the goal be achieved by composing behaviors
+   that the existing fundamental systems already produce?
+2. **Tune parameters.** Can it be achieved by adjusting the parameters, weights,
+   or configuration of an existing system?
+3. **Extend a system.** Can it be achieved by adding a small, general capability
+   to an existing system that other features could also benefit from?
+4. **Add a bespoke solution.** Only after the options above have been
+   considered and rejected for clear reasons.
+
+Three similar lines is better than a premature abstraction, but a bespoke
+feature that duplicates or bypasses an existing system is worse than either.
+
+### Don't "cheat" emergence without asking
+
+A "cheat" is any solution that directly hard-codes the desired outcome instead
+of letting it arise from the interaction of the fundamental systems. Cheats are
+tempting because they are shorter and more predictable, but they erode the
+systems that make the game's behavior rich and surprising, and they tend to
+accumulate special cases.
+
+If the most direct path to a goal would require reaching past the systems and
+using privileged information or a shortcut to explicitly produce the outcome,
+**stop and ask the prompter for explicit permission before doing so.** Describe
+the emergent alternative you considered and why you were tempted to bypass it,
+so the prompter can make an informed call.
+
+**Example.** Suppose the task is to balance the ecosystem by creating fauna
+that are attracted to prisms. The emergent approach is to place prisms and
+configure fauna attraction parameters, then let the fauna find them. A cheat
+would be to use the known planted locations of the fauna to directly place or
+steer things so the balance is achieved by construction. Before taking that
+shortcut — for instance, before reading fauna placement data and acting on it
+to short-circuit the attraction behavior — ask the prompter whether they want
+the cheat or the emergent solution.
+
+### When in doubt
+
+Name the fundamental systems involved, describe how each candidate solution
+interacts with them, and prefer the solution that leaves the systems intact and
+more expressive for future features.
```

</details>

### `7aec3a3c2` — Refine emergent-systems guidance with canonical fundamentals

_Claude, 2026-04-20 21:30:52 +0000_

```text
- Correct terminology: domain (not color), cells (not biomes).
- Elevate elementals as the single buff/debuff system across
  vessels and environment.
- Reframe trails as the 1D case of prismscapes to leave room for
  higher-dimensional prism constructions.
- Add a curation process (name precisely, show reach, show
  composition, prefer extension, budget the weight) so new
  fundamentals are added deliberately, not by accident.
```

```text
 CLAUDE.md | 133 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++-------------------------
 1 file changed, 97 insertions(+), 36 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 156 lines)</summary>

```diff
diff --git a/CLAUDE.md b/CLAUDE.md
index 68b7cf1ad..a63e62814 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -136,54 +136,115 @@ Every technical decision should be weighed against: **does this help the GDC dem
 
 ## Design Philosophy: Favor Emergent Systems Over Bespoke Solutions
 
-Cosmic Shore is built on a small number of fundamental systems (mass, color,
-biomes, trails, flora/fauna, prisms, etc.) whose interactions produce a large
-number of desirable emergent outcomes. When solving a problem, maintain active
-awareness of these systems and prefer solutions that work *through* them rather
-than *around* them.
+Cosmic Shore aims to be built on a small, carefully curated set of
+**fundamentals** whose interactions produce a large number of desirable
+emergent outcomes. When solving a problem, maintain active awareness of
+these fundamentals and prefer solutions that work *through* them rather than
+*around* them.
+
+### The fundamentals (working list)
+
+Use the canonical term, not a casual synonym. This list is the team's current
+best understanding and will be refined over time — propose additions or
+corrections through the process below rather than silently inventing new
+ones.
+
+- **Domain** — team/affiliation identity attached to mass, vessels, and
+  structures. Sometimes referred to casually as "color"; the canonical term
+  is *domain*.
+- **Mass** — the produced/consumed quantity that drives scoring, fueling,
+  and cell control.
+- **Cells** (with `CellType`) — the regions of play that are the unit of
+  territorial control. Casual language sometimes calls these "biomes"; the
+  canonical term is *cell*.
+- **Elementals** — the single system that governs **all** buffing and
+  debuffing across vessels and their environment. If a buff or debuff isn't
+  expressed through elementals, that's a smell.
+- **Prisms / Prismscapes** — the geometric primitive of player-generated
+  structure. Trails are the 1-dimensional case of a prismscape; higher-
+  dimensional prism constructions are planned and should reuse this
+  primitive rather than introducing parallel structure types.
+- **Flora & Fauna** — populations that live on and respond to the
+  fundamentals above (e.g. fauna attraction to prisms, flora growth on
+  cells).
+- **Vessels** — the player/AI actors whose class-specific abilities compose
+  with the fundamentals above.
+
+### Process for curating fundamentals
+
+The goal is an *exhaustive, minimal* set of fundamentals — expressive enough
+to solve every problem through composition, small enough that the team can
+hold the whole set in their head. Every fundamental costs mental overhead
+for everyone who touches the codebase, so adding one must be a deliberate
+act, not a side-effect of a feature ticket.
+
+Before treating something as a fundamental (or before proposing a new one),
+run this check:
+
+1. **Name it precisely.** Use the canonical term. If no canonical term
+   exists, propose one explicitly and get it agreed before using it.
+2. **Show its reach.** A fundamental earns its place by being load-bearing
+   for many features. Enumerate at least three distinct features or
+   behaviors that depend on it; if you can't, it's probably not fundamental.
+3. **Show how it composes.** Describe how it interacts with each existing
+   fundamental. Emergence comes from the cross-products between
+   fundamentals, so a system that doesn't meaningfully combine with the
+   others is a bespoke feature wearing a fundamental's costume.
+4. **Prefer extension over addition.** If a proposed fundamental is a
+   special case of, or expressible through, an existing one, extend or
+   rename the existing one instead.
+5. **Budget the weight.** A new fundamental must be *very* useful to justify
+   the weight it adds to the set. Flag any proposed addition to the
+   prompter and get explicit agreement before committing to it.
 
 ### Order of preference
 
-When addressing a task, try these approaches in order and stop at the first one
-that fits:
-
-1. **Use an existing system.** Can the goal be achieved by composing behaviors
-   that the existing fundamental systems already produce?
-2. **Tune parameters.** Can it be achieved by adjusting the parameters, weights,
-   or configuration of an existing system?
-3. **Extend a system.** Can it be achieved by adding a small, general capability
-   to an existing system that other features could also benefit from?
-4. **Add a bespoke solution.** Only after the options above have been
-   considered and rejected for clear reasons.
+When addressing a task, try these approaches in order and stop at the first
+one that fits:
+
+1. **Use an existing fundamental.** Can the goal be achieved by composing
+   behaviors the current fundamentals already produce?
+2. **Tune parameters.** Can it be achieved by adjusting the parameters,
+   weights, or configuration of an existing fundamental?
+3. **Extend a fundamental.** Can it be achieved by adding a small, general
+   capability to an existing fundamental that other features could also
+   benefit from?
+4. **Propose a new fundamental.** Only after the steps above have been
+   rejected for clear reasons, *and* after running the curation process
+   above with explicit prompter sign-off.
+5. **Add a bespoke solution.** Last resort, and only when a new fundamental
+   would be unjustified weight.
 
 Three similar lines is better than a premature abstraction, but a bespoke
-feature that duplicates or bypasses an existing system is worse than either.
+feature that duplicates or bypasses an existing fundamental is worse than
+either.
 
 ### Don't "cheat" emergence without asking
 
-A "cheat" is any solution that directly hard-codes the desired outcome instead
-of letting it arise from the interaction of the fundamental systems. Cheats are
-tempting because they are shorter and more predictable, but they erode the
-systems that make the game's behavior rich and surprising, and they tend to
-accumulate special cases.
+A "cheat" is any solution that directly hard-codes the desired outcome
+instead of letting it arise from the interaction of the fundamentals.
+Cheats are tempting because they are shorter and more predictable, but they
+erode the systems that make the game's behavior rich and surprising, and
+they tend to accumulate special cases.
 
-If the most direct path to a goal would require reaching past the systems and
-using privileged information or a shortcut to explicitly produce the outcome,
-**stop and ask the prompter for explicit permission before doing so.** Describe
-the emergent alternative you considered and why you were tempted to bypass it,
-so the prompter can make an informed call.
+If the most direct path to a goal would require reaching past the
+fundamentals and using privileged information or a shortcut to explicitly
+produce the outcome, **stop and ask the prompter for explicit permission
+before doing so.** Describe the emergent alternative you considered and why
+you were tempted to bypass it, so the prompter can make an informed call.
 
 **Example.** Suppose the task is to balance the ecosystem by creating fauna
 that are attracted to prisms. The emergent approach is to place prisms and
-configure fauna attraction parameters, then let the fauna find them. A cheat
-would be to use the known planted locations of the fauna to directly place or
-steer things so the balance is achieved by construction. Before taking that
-shortcut — for instance, before reading fauna placement data and acting on it
-to short-circuit the attraction behavior — ask the prompter whether they want
-the cheat or the emergent solution.
+configure fauna attraction parameters (working through the Flora & Fauna
+and Prism fundamentals), then let the fauna find them. A cheat would be to
+use the known planted locations of the fauna to directly place or steer
+things so the balance is achieved by construction. Before taking that
+shortcut — for instance, before reading fauna placement data and acting on
+it to short-circuit the attraction behavior — ask the prompter whether they
+want the cheat or the emergent solution.
 
 ### When in doubt
 
```

</details>
