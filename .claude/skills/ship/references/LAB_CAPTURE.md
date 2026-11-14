# /ship §3.55 Lab capture in full

<!-- Moved word for word out of .claude/skills/ship/SKILL.md on 2026-11-13 (Shombith: "/ship is very slow; it does not need to read every line every time"). SKILL.md keeps the procedure; this file is read when `ship_triage.py` or the step says so. -->

Read when SKILL.md's step says so (its trigger matched this branch).


A lab is a prototype rig built to DECIDE a mechanic before Unity: a browser studio, a research
rig with a generated viewer, a calibrated headless model, or an in-editor lab window
(`/labmaker` §0). Labs are built by many contributors on many branches. The craft compounds only
if every branch that touched one leaves what it learned in **`.claude/skills/labmaker/`**. This
is §3.5 made specific and mechanical for labs. It runs in every mode that runs §3.5, and
`/ship-quick` keeps a minimum of it (see that file).

**1. Detect.** The branch touched a lab if any of these hits:

```sh
git diff --name-only <merge-base>..HEAD | grep -iE \
  '(^|/)(Docs/Studios|Tools/(NCA|Ecology|ecosim))/|(lab|studio|bestiary|sandbox|playground)[^/]*\.(html|py|cjs|js|cs)$|(^|/)(DISCOVERIES|PROGRAM)\.md$|/briefs/'
git diff <merge-base>..HEAD -- '*.html' | grep -cE '^\+.*(window\.__[A-Za-z]+(Studio|Lab)\b|use\(.db.\))'   # >0 = a lab page
```

(Measured on `claude/peaceful-rubin-hhw49n`: the first command lists `Docs/Studios/**`, and the
second counts 8. On `cece/swarm-x-live2` the first lists about 500 `Tools/NCA` paths.)

Also count a lab the session PUBLISHED as an artifact even if its repo copy did not change. No hit
means one report line, "no lab touched", and you are done.

**2. Verify each single-file browser lab** it touched:

```sh
node .claude/skills/labmaker/verify_lab.cjs <lab.html> --out <scratch>
```

- Read both screenshots.
- A page that fails to load cleanly, or scrolls on desktop, is a §2 finding against the branch.
- A lab that predates the `window.__lab` hook fails the hook checks: report that as "not on the
  contract yet". It is not a §4 blocker.
- Research rigs and models run their own gates (fidelity, `--check`, parity harness). Report what
  ran.

**3. Update `/labmaker` — edits, not intentions:**

- **`CATALOG.md`**: add or refresh the lab's row (branch, path, live URL, status, what it
  decided). A lab that is not in the catalog is invisible to the next contributor.
- **`LEARNINGS.md`**: one attributed entry (branch, session, date) per lesson that cost real time
  or that the next lab would re-learn. That covers:
  - a trap;
  - a verification that caught something;
  - a technique that made the lab legible;
  - a process change the human asked for between rounds.

  Use that file's entry format. Grep the id before you claim it (§1's id-collision rule), and again after §1.5's
  merge: `grep -oE '^### L-[A-Z]+-[0-9]+' .claude/skills/labmaker/LEARNINGS.md | sort | uniq -d` names every id
  two branches claimed apart, and git joins two entries with one id without a conflict. Renumber yours and every
  reference to it (2026-11-11: this check found `cece/nifty-cray-41hy43` and bleeding-edge's Brittle Star ship pass
  had both taken L-STU-168 to 170 the same day; L-STU-41, 42 and 102 were already doubled on bleeding-edge).
- **Promote** into `SKILL.md` §2–§8 any entry that has now recurred in a second lab or cost a
  round. Mark it `Promoted: §N`.
- If the branch added a reusable check, extend `verify_lab.cjs`, add its planted defect to
  `--self-test`, and run both.

**4. Two traps specific to this step.**

- **A lab branch is often not the branch being shipped.** Its learnings still belong in this
  branch's copy of `/labmaker` when this session learned them. Attribute them to the branch where
  the lab lives.
- **Never copy a lab's numbers into LEARNINGS as current truth.** Quote them as "measured at round
  N", with the tool that produced them (§3: a measurement is a derived value).

**Report**: labs touched, the verifier verdict per lab, the CATALOG rows changed, the LEARNINGS
ids added and anything promoted, or an explicit "lab touched, nothing new learned: <why>".
