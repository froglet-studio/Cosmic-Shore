# /ship merge traps (§1 and §1.5)

<!-- Moved word for word out of .claude/skills/ship/SKILL.md on 2026-11-13 (Shombith: "/ship is very slow; it does not need to read every line every time"). SKILL.md keeps the procedure; this file is read when `ship_triage.py` or the step says so. -->

Read the index when the trial merge (`ship_triage.py`) reports conflicts, when a merge was clean but touched files your branch also changed, or before the second fetch right before the PR. Open the traps whose headline matches what you see.

## Index (read this, then open only the traps that match your diff; `ship_triage.py` names them)

 1. A SHALLOW CLONE INVENTS A MERGE BASE, and the branch then looks catastrophically stale
 2. A `.prefab` / `.unity` conflict is resolved per OBJECT, never per line:
 3. The same collision happens WITHOUT a second branch, against the document's own past
 4. A parallel mode/feature branch will have claimed the same ENUM VALUES
 5. Two ways that collision actually arrives, and the second is the one review misses
 6. Two branches appending the IDENTICAL line to a list is silently deduplicated to one
 7. A conflict whose two sides are two function BODIES shares the hunk's trailing lines
 8. A test mock of a wide interface is what a parallel branch breaks
 9. When your branch changed a shared structure's VALUE TYPE and theirs added an entry in the old one, "keep both" builds a heterogeneous container
10. A clean auto-merge can leave TWO bookkeepers for one subscription
11. "Keep both sides" is right for list entries and WRONG inside a chain
12. The dangling-operator test is necessary and NOT sufficient: the shared line the conflict split on can be a COMPLETE statement
13. The same trap exists in hand-authored YAML, and it is invisible to a compiler
14. "Take theirs MINUS my removals" is a third resolution shape, and filtering by TOKEN splits every multi-line construct
15. The shared-tail trap has a SOURCE-CODE form, and it produces no conflict marker at all
16. A generator's `--check` that validates only its own constants is not a gate
17. A conflict hunk's first line can be SHARED CONTEXT, so dropping your side drops it too
18. A verdict that is CONDITIONAL on another row does not conflict when that row changes
19. A doc that describes its own neighbouring content is making a claim you must check
20. A removal's doc sweep is not finished at the system's own `Docs/` folder — the per-feature doc beside the code is where a reader looks FIRST, and it is the last place anyone edits
21. A parallel branch may have fixed the SAME root cause while you worked
22. re-scope your docs to what is still genuinely yours
23. That check sees only what has MERGED. The collision that costs a whole branch is the one still in flight
24. Taking THEIR side of a file your branch refactored voids your branch's PROSE about that refactor, and nothing conflicts there
25. An add/add conflict on a `.meta` is two GUIDs for one asset path, and taking one side wholesale orphans every reference to the other
26. "Pick one wholesale" is right when the two fixes have the same BLAST RADIUS, and wrong when they do not — ask about scope before you discard either
27. A gate this branch adds must be negative-controlled against the content the MERGE brought in, not only against your own
28. If the branch ships a doc that cites `file:line`, the merge just rotted it
29. A clean `git status` is not evidence an asset does not exist — fetch the base first

## The traps

- **[T1] A SHALLOW CLONE INVENTS A MERGE BASE, and the branch then looks catastrophically stale.**
  Check `.git/shallow` before you believe any staleness number. With truncated history git
  cannot see the real common ancestor, so `git merge-base --all` returns SEVERAL bases and the
  `ort` strategy builds a *virtual* one by merging them — against grafted history, that virtual
  base is garbage. The symptoms are loud and entirely false: one branch measured **8,860 commits
  ahead** of a base it was four days from, and the trial merge produced **235 conflicts** in
  files neither side had touched (vendor trees that had been deleted, lifeform assets, whole
  unrelated subsystems). `git fetch --unshallow` and re-measure: the real numbers were **109/74
  with ONE merge base**, and the same merge then ran with **zero conflicts**. Nothing about the
  branch had changed.
  **`git merge-base --all` is the cheaper and sharper discriminator, and it separates two
  failures the conflict count conflates.** Shallow history can inflate the *count* while leaving
  the *base* correct, and only the second is dangerous. One branch measured **9,451 commits
  behind** a base it was 481 from — a 20x inflation — and `merge-base --all` still returned
  **exactly ONE** base, so the merge was against the real ancestor and the three conflicts it
  produced were all in files both sides had genuinely edited. Unshallowing changed the number and
  nothing else: same base commit, same three conflicts, resolutions unaffected. So run
  `git merge-base --all origin/<base> HEAD` FIRST — **more than one base means stop and
  unshallow**; exactly one means the base is sound and an alarming staleness number is cosmetic.
  Re-run the trial merge after unshallowing either way, and confirm the conflict set is identical
  before trusting work you already resolved.
  Two things to carry. **The conflict COUNT is the tell** — conflicts concentrated in files
  neither branch plausibly edited mean the base is wrong, not the branch; do not start resolving
  them, because every resolution you hand-craft against a phantom base is work thrown away and
  some of it will be wrong in a way no review catches. And **an independently-resolved merge is
  the strongest corroboration available**: when a parallel session had merged the same base into
  the same branch, `git diff` between the two merge commits' trees was EMPTY, which is better
  evidence the resolution is right than any amount of re-reading it.

- **[T2] A `.prefab` / `.unity` conflict is resolved per OBJECT, never per line:** `/asset-surgery` § "resolve a
  `.unity` / `.prefab` MERGE per OBJECT" and `Tools/Build/merge_unity_scene.py`, with its five assertions. That
  includes the shape where each side DELETED different adjacent documents. Git then pairs one side's deleted
  object against the other's surviving neighbour, and every "keep both" builds an object out of two. (2026-10-18:
  the Rhino rig swap against bleeding-edge's HUD cleanup, 74 deletions and 0 genuine conflicts.)

  **`git fetch` the base again right before you open the PR, and treat a moved tip as a
  SECOND merge, not a formality.** `git merge-base origin/<base> HEAD` equalling the base's
  tip is the test; if it does not, you are about to open a PR that is already behind. One
  session merged, reviewed the whole tree, re-ran every gate — and then found the base had
  advanced 18 commits, one of which renumbered the very enum the branch was editing (a new
  mode taking the id next to the one the branch retired). The second merge produced eight
  conflicts the first had not, in the same files. The window between "I merged" and "I
  pushed" is exactly as long as your review, which on a big branch is long enough for a
  collision to land in it.
  Watch for conflicts a text merge CANNOT see: two branches independently claiming the
  same new **doc section number** (both took `§4.6`) merges clean per-hunk and produces a
  document with two of them. When you renumber, renumber every inbound reference — and
  only YOURS: grep the whole repo, then split the hits by which section they mean.
  **[T3] The same collision happens WITHOUT a second branch, against the document's own past** —
  a migration-tracker row id (`C6`), a bug id (`B10`), a test id. You pick the "next" id by
  reading the last row, and the last row is not the highest: `PRISM_ANIMATION.md`'s tracker
  runs C1…C13b with C6 sitting mid-table. Grep for the id you intend to claim BEFORE writing
  it into code comments, doc prose, tool docstrings and commit messages — by the time you
  notice, it is spread across a dozen files and the fix is a sweep, not an edit.
- **[T4] A parallel mode/feature branch will have claimed the same ENUM VALUES.** The id-collision
  rule above is not only about doc sections and tracker rows: two branches adding a game mode, a
  toast situation, a log-channel bit, or any hand-numbered enum member both pick "the next one",
  and git merges the two additions cleanly into an enum with duplicate values. One session hit
  this on THREE enums at once (`GameModes` 42, `GameToastSituation` 60-62, `CSLogChannel 1 << 2`).
  Resolve by renumbering YOURS (theirs already merged), then sweep every place the NUMBER travels
  rather than the name — for a game mode that is the enum, the arcade card asset's `Mode:`, and
  the toast config's `gameMode:`/`situation:`/`resetOnSituation:` ids. Code that switches on the
  enum by NAME needs no change, which is exactly why the stale numbers hide in assets. Verify with
  a duplicate-value check over the whole enum, not just your own rows.
  **[T5] Two ways that collision actually arrives, and the second is the one review misses.** The
  obvious one is both sides authoring the same literal. The subtle one is a member LOSING its
  explicit value in the resolution and silently taking the next IMPLICIT one: keep-both on
  `VolumeDestroyed = 9` / `SwitchesThreaded = 9` produced an enum where mine had no `= N` at all,
  so it took `CombatPoints + 1` and collided with a `Jousts = 7` neither branch had touched. The
  enum's own "never reorder, only append" comment cannot prevent this — nothing was reordered by
  hand — so the duplicate-value check is the ONLY thing that catches it. Run it over the whole
  enum, and separately assert every member still carries an explicit value if the enum requires
  them.
  **[T6] Two branches appending the IDENTICAL line to a list is silently deduplicated to one.** The
  YAML trap above is about mis-concatenation; this is its mirror, omission by identity. Both
  branches added `  - 45` to `ProgressionConfig`'s unlock list after the same anchor, git saw one
  change, kept one line, and one mode's entry simply was not there — no conflict, no marker, and
  the file still parses. Any list where two branches append a value derived from the same "next
  free number" is exposed: count the entries against what both sides should SUM to, and check
  your own value is present by name, not just that the list grew.
  **[T7] A conflict whose two sides are two function BODIES shares the hunk's trailing lines.** Git
  ends the hunk at the last differing line, so a common tail — a `return`, a closing call, a
  `}` — belongs to whichever body you put LAST. Ordering the two bodies therefore silently
  strips it from the other: `switches_threaded()` lost its `return m` and the generator crashed
  with `'NoneType' object is not subscriptable` several frames away from the cause. After any
  keep-both of two callables, check each one still ends the way it did on its own branch — or
  just run the thing, which is what caught it here.
  **[T8] A test mock of a wide interface is what a parallel branch breaks.** `IRoundStats` gained two
  members, so every hand-written `IRoundStats` mock stopped compiling — and a broken test mock
  takes `Assembly-CSharp-Editor` down for everyone, which no gameplay compile-check would show.
  Grep for other implementers of any interface the base branch widened.
- **[T9] When your branch changed a shared structure's VALUE TYPE and theirs added an entry in the
  old one, "keep both" builds a heterogeneous container.** The two edits are different lines of
  the same dict/list/table, so git merges them without a conflict marker in the common case and
  with a trivially-resolvable one otherwise — and the result is a structure holding two shapes
  that its consumer unpacks as one. This branch widened a registry's value from a bare path to a
  `(kind, path)` pair so an entry could match by SPECIES as well as by prefix; a parallel branch
  added a new bare-path entry the same day. Keeping both verbatim left the consumer's
  `for token, (kind, path) in …` unpacking a string — a crash at the tool's NEXT run, in a
  fleet-wide tool, with nothing at merge time to say so. Python has no compiler to catch it and a
  C# analogue (a serialized field whose type changed under an entry authored against the old one)
  fails at deserialization instead. Resolve by **carrying their entry across into the new shape**
  and deciding, for that entry specifically, which variant it is — then run the consumer over
  every entry, theirs included. *Whenever a merge touches a container you retyped, the entries you
  did not write are the ones that are still the old type.*
- **[T10] A clean auto-merge can leave TWO bookkeepers for one subscription.** When your branch and the
  base both fix the same binding from different directions, the edits often land in different
  methods and git merges them without a marker. This branch recorded the pause-event source in a
  new `_pauseSource` field (so `Initialize` detaches from the right `InputStatus`); the base added
  `DetachInputPause`/`AttachInputPause` for a mid-match pilot swap that re-read
  `vesselStatus.InputStatus` instead. Both compile; they disagree about WHICH status holds the
  handler the moment the pointer moves. After any merge, grep the auto-merged files for fields and
  methods that touch the same event/flag and make them share ONE record — `git diff HEAD -- <file>`
  on every auto-merged file you also edited is the cheap way to see what arrived beside your code.
- **[T11] "Keep both sides" is right for list entries and WRONG inside a chain.** Resolving conflicts by
  concatenating HEAD and theirs works for independent fields, list items and doc paragraphs. It
  produces invalid code when both sides are links in one expression: two halves of a `&&` chain
  become two statements, and two halves of a `+` string chain become two ARGUMENTS (a `HelpBox`
  call silently gaining a third parameter). Compile after every keep-both resolution — and treat
  any conflict hunk whose last non-blank character is `&&`, `+`, `,` or `?` as one needing a
  hand-joined merge, not a concatenation.
  **[T12] The dangling-operator test is necessary and NOT sufficient: the shared line the conflict split
  on can be a COMPLETE statement.** A conflict is bounded by the lines both sides share, so when
  two branches each add a function/member ending in the same closing line, git puts that line
  OUTSIDE the hunk and keeping both bodies gives it to whichever side you place last — the other
  falls off its own end. Three landed in one merge and only two had a dangling operator: a Python
  `return m` shared by two generator functions (so `switches_threaded()` silently returned `None`
  and the icon generator crashed), and a `/// <summary>` shared by two enum members (so the second
  member's doc comment lost its opening tag). Neither is a syntax error in C#, and the Python one
  is not caught by any parse. After every keep-both resolution, read the line immediately AFTER
  the hunk and ask which side it belongs to — if the answer is "both", duplicate it.
  **[T13] The same trap exists in hand-authored YAML, and it is invisible to a compiler.** A Unity
  `EditorBuildSettings.asset`-style list item is multiple lines wide (`- enabled: 1` /
  `path: ...` / `guid: ...`), so when two branches both append a new scene entry after the SAME
  anchor entry, the diff's shared anchor line is the leading `- enabled: 1` of the LAST entry
  before the split — additive "keep both" can concatenate the second entry's `path`/`guid` lines
  directly onto the first's body, without ever inserting the second entry's own `- enabled: 1`
  marker. The result parses as ONE list item with duplicate `path`/`guid` keys, not two — no
  syntax error, no build failure, just a scene silently missing from `m_Scenes` while its name
  string is still technically present in the file (which also defeats an idempotency guard that
  checks `"SceneName.unity" in text` rather than well-formedness). It reads at runtime as "Scene
  'X' couldn't be loaded because it has not been added to the build settings scenes list" with no
  compile error anywhere to point at it. There is no compiler to catch this for a `.asset`/`.unity`
  file — after resolving ANY multi-line-list-item YAML conflict, mechanically count entries
  (`grep -c '^  - enabled:'` for `EditorBuildSettings.asset`, or the equivalent leading marker for
  the list in question) against what both sides should sum to, and verify every entry has exactly
  its expected key set with no stray duplicate keys — do not eyeball it.
- **[T14] "Take theirs MINUS my removals" is a third resolution shape, and filtering by TOKEN
  splits every multi-line construct.** When your branch removes a feature that the base
  branch extended in the same place, the natural resolve is to take their side and drop the
  lines mentioning your removed thing. That is correct for one-line list entries and wrong
  for anything spanning lines, because the CONTINUATION lines do not contain the token: a
  three-line bullet in a `+`-chained C# string lost its first and last lines and left the
  middle one dangling into the neighbouring bullet — valid C#, wrong prose, no marker, no
  compile error. A doc comment, a multi-line attribute, a wrapped tooltip and a YAML list
  item all fail the same way. After a token filter, diff YOUR result against THEIR side
  (`git diff origin/<base> -- <file>`) and read every removed hunk: each one must be a
  complete construct, not a hole in the middle of one.
- **[T15] The shared-tail trap has a SOURCE-CODE form, and it produces no conflict marker at all.**
  Two branches that each append a same-shaped function to the same file split on the shared
  tail — `    return m` plus the blank lines — so git can hand that tail to whichever function
  came last and silently TRUNCATE the other. The result parses fine; the truncated function
  just falls off its end and returns `None`, and the crash surfaces later, deep inside a
  library, naming neither the function nor the cause. This has now happened **twice on the
  same file** (`author_objective_icons.py`, both times to the glyph function whose neighbour
  the other branch added). After merging a file of parallel same-shaped functions, CALL each
  one and assert it returned something — do not settle for a static "does it return?" check:
  `ast.walk` descends into NESTED helpers, so a truncated function that happens to define two
  local helpers reports two returns and looks healthy. Put the assert at the call site, where
  it can name the offender.
- **[T16] A generator's `--check` that validates only its own constants is not a gate.** The whole
  claim of "the generator is the source, the assets are the build" rests on `--check` failing
  when an asset drifts — and a `--check` that merely re-derives its content in memory and
  verifies its own cross-file assertions proves the SCRIPT is self-consistent while saying
  nothing about what shipped. One shipped that way: it asserted the enum still held the mode
  id it expected, then passed cheerfully with an arcade card pointing at a different mode,
  because it never re-read the card. Make `--check` diff every generated file against disk and
  fail on any mismatch (naming the file and the first differing line) — then negative-control
  it by mutating one asset and confirming it fails. The same pass usually reveals the deeper
  fix: an id the generator HARDCODES is an id that goes stale on the next upstream renumber,
  so read it out of its enum instead and the sweep disappears.
- **[T17] A conflict hunk's first line can be SHARED CONTEXT, so dropping your side drops it too.**
  git anchors a hunk at the last line both sides agree on, which for a doc-comment or an
  attribute block is often the opening tag. Resolving by "take theirs" via a scripted cut from
  `<<<<<<<` to `>>>>>>>` then silently eats the line ABOVE the marker if you also delete your
  side's lead-in — one session removed the `/// <summary>` that opened BOTH sides' docstrings and
  left theirs orphaned. After any scripted resolution, diff the file against the side you claimed
  to take wholesale (`git diff origin/<base> -- <file>` should be EMPTY for a pure take-theirs)
  rather than eyeballing the result.

- **[T18] A verdict that is CONDITIONAL on another row does not conflict when that row changes.** Two
  branches working the same status table (a launch-blocker index, a decision register, a
  migration tracker) conflict only on the rows they both edited — and the rows most likely to be
  wrong afterwards are the ones NEITHER touched. One row read `keep` *because* a neighbouring row
  was staying; the neighbour was removed on the other branch; git merged both sides cleanly and
  left the dependent verdict standing with its premise deleted. Same shape for a per-folder census
  or a total: a count of "14 third-party folders" is derived from rows both branches were editing
  and belongs to neither hunk. After merging a table, re-read the rows you did NOT touch and ask
  which of them were true only because of a row that moved.

- **[T19] A doc that describes its own neighbouring content is making a claim you must check.** A
  blockquote saying "the two rows below are gone from the table" is prose, not an edit — it reads
  as done, it survives review, and the rows are still there. Anything of the form *"the table
  below now …"*, *"see the updated column"*, *"struck through above"* gets the same treatment as a
  `file:line` reference: go and look. This is the doc-internal case of the producer rule in §2.

- **[T20] A removal's doc sweep is not finished at the system's own `Docs/` folder — the per-feature
  doc beside the code is where a reader looks FIRST, and it is the last place anyone edits.** A
  branch that retires a channel naturally documents the retirement where it is defined and where
  the architecture is written down, then stops. One session did exactly that, and a D2 sweep for
  the two removed field names found **nineteen** live sites outside that folder still describing
  the channel as the place a number lives — six per-ability docs, a verification checklist, and,
  worst, a `Docs/prompts/*.md` that INSTRUCTS a future session to author the deleted field into an
  asset (the stale-instruction hazard §2 names, in the one file class written to be executed).
  Two of the nineteen were in a doc the same branch had already rewritten: it said *"nothing in
  this document should ever again say 'map pinned to 1'"* fourteen lines above four more of them.
  So: grep the removed IDENTIFIER, not the system, across `Assets/**/*.md`, `Docs/`, `.claude/`
  and CLAUDE.md, and classify every hit as **describing the removal** (keep) or **describing it as
  live** (fix). Include `Docs/prompts/` explicitly — a prompt is code somebody will run.

- **[T21] A parallel branch may have fixed the SAME root cause while you worked.** Read the base
  branch's new commits by subject before you resolve anything — this is not a merge
  conflict, it is a design collision, and git will happily interleave two fixes for one
  bug into a tree that carries both. When it happens: pick ONE implementation on merit and
  take it **wholesale** (`git checkout --theirs <file>`), then delete the machinery yours
  needed and theirs does not — a half-merged pair of fixes is worse than either. Then
  **[T22] re-scope your docs to what is still genuinely yours**: your section was written when
  you owned the whole story, and left as-is the repo ends up with two competing narratives
  of one bug. Reference theirs rather than restating it, and keep only the part they do
  not cover. Expect this whenever the base branch touched the same files — check with
  `git log --oneline <merge-base>..origin/<base> -- <your changed files>`.
  **[T23] That check sees only what has MERGED. The collision that costs a whole branch is the one
  still in flight.** Two sessions were given the same refcompile bug within minutes of each other
  (2026-10-06). Both fixed it, and the second to reach the base conflicted in four files. The other
  branch was even NAMED in a base README this branch merged (*"an unmerged branch,
  `claude/hopeful-heisenberg-murjor`, does this"*). One `git log` of it would have shown the
  duplicate fix before the PR opened, not after the PR conflicted. Listing every remote branch is
  no help (`git ls-remote origin 'refs/heads/*'` returns 500+), so follow the names. Before opening
  the PR, grep the files you changed for branch names
  (`git grep -ohE '(claude|cece)/[a-z0-9-]+' origin/<base> -- <your changed files> | sort -u`).
  For each name that still exists (`git ls-remote origin refs/heads/<name>`), run
  `git fetch --depth=50 origin <name>` and then
  `git log --oneline origin/<base>..FETCH_HEAD -- <your changed files>`.
- **[T24] Taking THEIR side of a file your branch refactored voids your branch's PROSE about that refactor, and nothing
  conflicts there.** `cece/sharp-tesla-j0kd7z` moved the Wrasse's `KiteNavigator` onto a shared `ICrewSeat`; the next
  base merge (2026-10-21) took bleeding-edge's `KiteNavigator` wholesale because it had grown its own crew system. The
  code was right after that. Five claims that the Wrasse ran on the shared pieces were still in the docs: a CLOSED team
  task, a verification-checklist entry, the project overview row, a report and the brief. A closed task is the worst of
  them, because nobody re-reads a done row. After any take-theirs on a file your branch changed, grep your branch's diff
  for the file's types (`git diff <merge-base>..HEAD -- '*.md' | grep <Type>`) and correct or reopen each claim.
- **[T25] An add/add conflict on a `.meta` is two GUIDs for one asset path, and taking one side wholesale
  orphans every reference to the other.** Two branches each created `RampBoostAIPolicySO.cs` and
  `RhinoAIBoostPolicy.asset` (2026-10-17, `cece/relaxed-hopper-r62hbd` against bleeding-edge), so
  the file names matched and the guids did not. `git checkout --theirs` on the four files is the
  right resolve, but the branch's own `Rhino.prefab` still pointed `boostPolicy` at the LOSING
  asset guid. It surfaced here only because both sides had edited that prefab line. Had the other
  branch not wired the prefab, the line would have auto-merged to a guid no file carries: a null
  policy, and "the AI quietly never ramps" in play. After taking a side on any `AA` `.meta`,
  `git grep` the losing guid across `Assets/` and repoint every hit. Two quicker tells that the
  other copy is the one to keep: its doc comment names your branch as a source, or a
  `Docs/TEAM_TASKS.md` row already flags the duplicate.
- **[T26] "Pick one wholesale" is right when the two fixes have the same BLAST RADIUS, and wrong when
  they do not — ask about scope before you discard either.** The collision above assumes two
  implementations of one fix. The other shape is two fixes at different ALTITUDES, and there
  keeping both is correct: a parallel session found the same false claim (a collider budget for a
  cost that does not exist) and corrected it where it had bitten them — one cell's budget, its
  own doc section, one CLAUDE.md row — while this branch swept the claim's 23 project-wide sites
  and added a gate. Taking either wholesale would have thrown away real work: theirs, the
  justification for a specific number; mine, the sweep and the thing that stops the claim
  regrowing. Both narrations survive because they answer different questions. The test is
  mechanical: diff each side's file list against the other's. **Same files, same subject → pick
  one. Disjoint files with one shared subject → keep both, and make each reference the other
  rather than restating it.**
- **[T27] A gate this branch adds must be negative-controlled against the content the MERGE brought in,
  not only against your own.** A green gate over files you have never read is not evidence; it is
  as consistent with "the gate cannot see that region" as with "that region is clean" — and the
  merge just added a few thousand lines nobody wrote it for. Re-inject the exact defect into the
  merged-in text and confirm the gate names it there (`cp` the file, mutate one sentence in THEIR
  prose, run, restore). One pass did this and the gate fired on the parallel branch's new cell
  doc, which is what turned "it passes" into "it covers them" — and had it stayed silent, the
  honest report would have been that the gate's scope stops at the branch.
- **[T28] If the branch ships a doc that cites `file:line`, the merge just rotted it.** Line
  references are the one kind of prose that goes stale from a commit that never touched
  your branch. After merging the base, re-resolve EVERY reference mechanically — extract
  them, confirm the file exists, confirm the line is in range, and then **confirm the
  content at that line still says what you claimed**. The third check is the one that
  matters: in-range passes happily while pointing at the wrong line. One session shipped
  a follow-up doc whose six `Cell.cs` refs all slid 22 lines because an unrelated upstream
  commit added 28, and the same sweep caught an off-by-one that had been wrong since it
  was written. Where the reference has to survive, **anchor on the symbol and demote the
  number to a hint** ("`RetireWorldIntoSuctionRoot` (`:2058`) — re-grep before trusting
  the number"), because the next drift is not preventable, only survivable.
- **[T29] A clean `git status` is not evidence an asset does not exist — fetch the base first.** When a
  human says "it's in the prefab" and your tree does not have it, the likeliest explanation is not
  that they forgot to save: it is that they committed it to the base branch AFTER your branch was
  cut. A clean working tree says nothing about that, and reporting "it isn't on disk, save your
  prefab" is both wrong and the kind of wrong that makes a human re-do work they already did.
  `git fetch origin <base>` and grep the base's version of the file BEFORE telling anyone an asset
  is missing.

- Restate, in a few sentences, WHAT the branch delivers and WHY. If you can't, you are
  not ready to ship — go re-read the diff.
