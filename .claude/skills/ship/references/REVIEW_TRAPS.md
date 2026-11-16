# /ship review traps (§2)

<!-- Moved word for word out of .claude/skills/ship/SKILL.md on 2026-11-13 (Shombith: "/ship is very slow; it does not need to read every line every time"). SKILL.md keeps the procedure; this file is read when `ship_triage.py` or the step says so. -->

The four §2 gates stay in SKILL.md. These are the defects past reviews found that those gates did not name. Read the index; `ship_triage.py` lists the traps that match this branch's files (C#, assets, docs, tests, gates, a retirement or rename) - open those. `/ship-deep` reads them all.

## Index (read this, then open only the traps that match your diff; `ship_triage.py` names them)

 1. Inserting a member ABOVE a method silently reassigns that method's doc comment
 2. Do the scan in Python, not `awk`
 3. A rename sweep must split its hits by which THING the name means
 4. A doc that asserts a consequence is not evidence the consequence happens — find the PRODUCER
 5. A GUARDED call site reads exactly like a working feature when the reference is null
 6. A number read off a ScriptableObject's FIELD INITIALIZER is not the number the game runs on
 7. The mirror of that rule: a "dead surface" claim decays into a live path, and nothing announces it
 8. A shared channel carrying a bare primitive cannot encode what the primitive MEANS — ask the PRODUCER's type, never the value
 9. A rule-guarding test that NAMES the members it knows about stops testing the rule the day a member is added
10. A test that SUPPLIES the value under test proves the consequence, not the design
11. When a fix COMPOSES onto existing behaviour, the test that matters is that the UNTOUCHED half is bit-identical — asserting the new half works is satisfied by every wrong composition too
12. Your own branch's WORKED EXAMPLES go stale, not just its constants
13. A gate that ABORTS looks exactly like a gate that passes, if nobody reads its output
14. Sweep the whole family, not just yours
15. re-measure, never quote
16. Registration can land in TWO lists, and a generator knows one
17. A gate you wrote this branch is an INSTRUMENT — point it at the whole tree and READ it, don't just confirm your branch is green
18. A LONG branch invalidates its own earlier rounds, and the doc from round 2 is the last place anyone looks
19. An absence that is true only because of where an ASSET was filed is not guarded by the code
20. Deleting a FIELD by pattern orphans its attributes, and the orphan adopts the next member
21. A `try/catch` turns a hang into a silent degradation. That is worth having and it is not a fix
22. A successor rule resolved TWICE is resolved once wrong
23. A RETIREMENT leaves residue, and the residue is the part that later reads as a live feature
24. A comment asserting an ABSENCE rots exactly as silently as one asserting a presence

## The traps

- **[T1] Inserting a member ABOVE a method silently reassigns that method's doc comment.** A
  `///` block belongs to whatever declaration follows it, so adding a helper between an
  existing doc comment and its method hands the doc to the HELPER and leaves the method
  undocumented — with two `<summary>` tags in one block and no compile error, in a file that
  otherwise reads perfectly. Three landed in one branch. **Since 2026-11-11 it is a gate:
  `doc_summaries` (`Tools/Build/check_doc_summaries.py`, run by `gates.sh`) fails on any doc
  comment under `Assets/` that holds two `<summary>` blocks, so every hit is new.** It also sees
  the shapes the old one-line scan (`/// </summary>` directly followed by `/// <summary>`) misses:
  a first block closing on a text line, a `<param>` or `<returns>` between the two, or a blank
  line or `//` comment between them. On bleeding-edge that day the scan found 55 and the gate 76
  (`cece/loving-volta-kkvnex`). When it fires, decide per site whether the orphaned doc should
  move DOWN onto its method, merge with the method's own doc, or be deleted as superseded — the
  new member usually has its own. A check independent of the shape: every `<paramref>` /
  `<param>` name must be a parameter of the member the doc sits on (10 mismatches before that fix,
  0 after). In a shallow clone `git log -S '<phrase>'` reaches back only to the graft
  (`.git/shallow`) and names the boundary merge for everything, so find the member from the code.
  **[T2] Do the scan in Python, not `awk`.** macOS ships BSD awk, and a one-liner using `prev ~ /…/`
  over a `/// ...</summary>` line followed by `/// <summary>` silently matched NOTHING on a branch
  that had two such hits — a scan that cannot fire reads exactly like a clean branch. For trees
  the gate does not cover, run `check_doc_summaries.py --root Port --root Tools`.
- **[T3] A rename sweep must split its hits by which THING the name means.** One identifier is
  routinely three: a C# class, an HLSL function, a shadergraph node's `m_FunctionName`. Renaming
  the class leaves the other two correct, so a blanket search-and-replace is as wrong as no sweep
  at all, and `grep -rn <OldName>` hands you both kinds in one list. Classify every hit before
  editing any of them, and keep the old name in a HISTORICAL change-record row with the rename
  noted beside it rather than rewriting the record of what a past branch did.
- **[T4] A doc that asserts a consequence is not evidence the consequence happens — find the
  PRODUCER.** When a doc (or a verification step, or CLAUDE.md) says "X still lands", "contact
  costs Y", or "the gate leaves Z alone", grep for who actually *calls* the thing that produces
  X/Y/Z for that vessel/mode/path. A passthrough that is genuinely correct — the gate really
  does leave the channel alone — reads as verified even when nothing upstream is pushing
  anything through it, so the claim survives review indefinitely. Four such claims shipped
  across three docs plus CLAUDE.md describing a prism-collision slow on vessels that had no
  slow effect wired at all. The same shape hides behind NAMES: a serialized field called
  `vesselSlowedByRhinoDangerPrismEvent` under a `"Slow Viewer Integration"` header belonged to
  an effect that only muted an input. Treat "the docs say so" and "the identifier says so" as
  hypotheses to check, never as the check.
- **[T5] A GUARDED call site reads exactly like a working feature when the reference is null.** The
  producer rule above is about a doc asserting a consequence; this is its sharper form, where the
  CODE asserts it and is correct. `if (leaveLobbyButton) leaveLobbyButton.SetActive(isClient);` is
  what a careful author writes — and it is also what makes a `{fileID: 0}` in the prefab silent, in
  every mode, forever. Two shipped that way on one component, and one of them had already been
  recorded in a bug table as the ✅ case whose shape was then propagated to three other screens. So
  when a claim turns on a serialized reference, grep the PREFAB/SCENE for the field
  (`^  <fieldName>: ` inside that component's `!u!114` block, never file-wide) and read the id —
  the call site cannot tell you, because a null-guard and a working feature are the same source.
  A field that is `{fileID: 0}` on every instance is a feature that has never run.

- **[T6] A number read off a ScriptableObject's FIELD INITIALIZER is not the number the game runs
  on.** The SO declares `public float dynamicMaxDistance = 40f;` and the ASSETS say 250. Reading
  the class is fast, feels authoritative, and is the wrong source — the assets are the game. One
  session built a platform law's central premise ("a pilot's own hull is always 10-40 units from
  its camera, so the near cutoff excludes it for free") on exactly that, and the shipped fleet
  spanned 6.7 to 250, so two of eight vessels marked their own ship. The premise then propagated
  into an `IsSane` branch, an edit-mode test and three documents, **all self-consistent**, because
  every one of them traced back to the same default rather than to any asset. Self-consistency
  across artifacts is not corroboration when they share one upstream source. So: whenever a claim
  turns on an authored value, enumerate the ASSETS (`AssetDatabase.FindAssets("t:Foo")`, or grep
  the `.asset` YAML) and tabulate the real spread — and where the claim must keep holding, make the
  gate re-measure from the assets rather than restating the number, so it fails loudly when an
  artist re-authors one.
- **[T7] The mirror of that rule: a "dead surface" claim decays into a live path, and nothing
  announces it.** "Unreferenced", "no producer anywhere", "provably dead" are true *as of a
  date* — the next feature branch is free to wire the thing up, and it will not think to go
  correct a deadness claim it never read. So a doc, comment, or prompt that licenses a
  DELETION must have its emptiness re-proved at ship time, not inherited: grep for the
  producer/caller again, now. `PrismType.Grow` was documented dead in three doc sites and two
  code comments, acquired a producer two days later, and the stale instruction to delete it
  survived in a follow-up prompt that a fresh session would have executed. Deadness claims
  are the most dangerous kind of stale doc, because acting on one is irreversible and the
  code that proves them wrong is somewhere you were told not to look.

- **[T8] A shared channel carrying a bare primitive cannot encode what the primitive MEANS — ask the
  PRODUCER's type, never the value.** When N producers publish into one `ScriptableEvent<string>`
  (or an int, or a float), a consumer that infers the reading by parsing is guessing, and the guess
  is invisible because every reading is a plausible value. `onUpdateTurnMonitorDisplay` carries the
  objective REMAINING from nine monitors and SECONDS remaining from a tenth — and
  `TimeBasedTurnMonitor.GetTimeToDisplay()` returns `"72"`, not `"1:12"`, so an `int.TryParse` test
  separates nothing. The mode that has BOTH a time monitor and a scoring rule then renders its
  countdown as an objective count, falling as the clock runs, under a label naming the wrong thing.
  The fix is a virtual on the producer's base type (`TurnMonitor.PublishesSecondsRemaining`), because
  the producer is the only thing that knows. **The tell to look for: you are about to write a
  heuristic — a parse, a range check, a format sniff — to recover a fact the sender had and threw
  away.** Ask what makes the two cases different at the SOURCE; if the answer is "the type", the
  heuristic is a bug waiting for the one mode that uses both.

- **[T9] A rule-guarding test that NAMES the members it knows about stops testing the rule the day a
  member is added.** A law expressed over an enum ("only `Domain` may wear a playable domain",
  "only these metrics fold by sum") is usually guarded by a test that enumerates the cases by
  hand, because at two members a hand list and a loop look identical. Add a third and the test
  still passes, still reads as the law's guard, and now covers two thirds of it — and the gap is
  invisible, because nothing fails. `ToySwitchVocabularyTests` guarded the switch reservation by
  naming `Neutral`; `ToySwitchSignal.Next` landed and the law went untested for it. Whenever a
  branch adds an enum member, grep for tests that mention the SIBLING members by name and convert
  them to enumerate the enum (`Enum.GetValues(...).Where(x => x != TheException)`), so the next
  member is covered on the day it is added rather than the day someone remembers.

- **[T10] A test that SUPPLIES the value under test proves the consequence, not the design.** A helper
  that takes the interesting quantity as a parameter is trivially easy to test, and a suite built
  on it can be thorough, green, and blind to the thing that actually decides the behaviour. The
  Sparrow's three missile tiers are ranked-not-additive because of ONE line in
  `VesselCombatHitLatch.Key`'s constructor (the three classes canonicalise to one key); every
  upgrade test called `CombatHitScoring.Credit` with a hand-supplied `supersededRank`, so keying
  the tiers apart — which makes a centre-punch pay 60 through three individually-correct fresh
  windows — would not have moved a single assertion. **The tell: you are passing a value your
  production code derives.** Find where it is derived and start the test there; if that is hard
  to reach, that difficulty is the finding. Its close relative — **when a design lives in a lookup
  KEY, a test that starts downstream of the lookup cannot see the design at all.**

- **[T11] When a fix COMPOSES onto existing behaviour, the test that matters is that the UNTOUCHED
  half is bit-identical — asserting the new half works is satisfied by every wrong composition
  too.** Sibling of the rule above, reached from the other side: nothing is hand-supplied here, the
  tests genuinely drive the shipped path, and they are still blind. A brake added to the fleet's
  throttle tracking had ten green tests, all of the form "a zero throttle now reaches zero" — which
  is true of `min(exponential, constant)`, of `exponential - constant`, and of a dozen other
  compositions that feel completely different to fly. The shipped one was the SUM, measured **40%
  under the legacy deceleration curve** half a second in: a different fall on every affected hull,
  including the one the user had named as the CORRECT reference. The class docstring described the
  intended composition correctly the whole time; only the code disagreed, and no test pinned the
  docstring. **The tell: your change has a region where it must do nothing, and you have not
  written that assertion.** Write the no-op half first — "identical to the previous implementation
  above the crossover, strictly stronger below it" — because the region where a feature must be
  INVISIBLE is the region no bug report will ever come from.

- **[T12] Your own branch's WORKED EXAMPLES go stale, not just its constants.** §2 already says to
  re-derive every constant a later round moved; the same applies to every place the prose
  ILLUSTRATES a rule with a specific case. Commit 1 explained a gate with "a vessel with a
  non-zero `MinimumSpeed` (the Rhino's 10) is untouched"; commit 2, on the user's next message,
  zeroed exactly that number — leaving the claim in a class docstring, an inspector tooltip, a
  design doc AND a test constant literally named `RhinoFloor = 10f`, all self-consistent and all
  false. A named example is a fact assertion with a friendly face, and a test const carrying a
  vessel's name is one no compiler will ever check. At ship time, grep the branch for the
  examples its own earlier rounds chose, not only for the numbers they quoted.

- **[T13] A gate that ABORTS looks exactly like a gate that passes, if nobody reads its output.** This
  repo's `Tools/Build/author_*.py` generators do a one-time migration first (clone a donor scene,
  patch its wiring) and validate everything they built AFTER it. When the donor moves on, the
  migration's `assert` fires and takes every check below it — while `--check` is still in the
  workflow, still cited in commit messages, and still exits in a way nobody looks at.
  `author_dogfight_assets.py` validated *nothing* for months, including four checks a branch had
  just added to guard its own scoring; `author_ribcage_assets.py` and
  `author_wildlife_liberation_assets.py` are in that state now. So: **a green gate is only
  evidence if you can name a failure it produced.** Before citing one, break the thing it guards
  and watch it fail — and when a spent one-shot is the blocker, make it STAND DOWN (guard the
  step, register the already-committed output so downstream checks describe the shipped artifact)
  rather than deleting the checks or living with the abort. Also worth asking of any `--check`:
  does it diff against DISK, or only validate its own recipe in memory? Those are very different
  promises and the flag name does not distinguish them.
  **[T14] Sweep the whole family, not just yours** — it is one line and it turns "mine is green" into a
  tally: `for f in Tools/Build/author_*.py; do printf '%-46s ' "$(basename $f)"; timeout 120
  python3 "$f" --check >/tmp/g 2>&1 && echo OK || echo RED; done`. **Glob `author_*.py`, not
  `author_*_assets.py`** — the narrow form was what this rule originally said and it sees **15 of
  the 36** generators, missing every one whose output is not a mode's asset set (sprite and mesh
  authors, population and layout authors). A branch whose own generators fall outside the glob gets
  a clean-looking sweep that never ran on its work. Re-measured 21 Sep 2026: the family is **50**
  and the wide glob gives **33 OK / 17 RED** — but **7 of those 17 are `ModuleNotFoundError: No
  module named 'numpy'`**, an environment gap rather than a defect, so the honest tally is 33 OK /
  **10 RED** / 7 un-runnable here. Split those two before reporting a number; a missing module is a
  container to fix, a spent `assert` is a generator to fix. (Was 49 / 32 OK / 9 RED on 18 Sep, 10 of
  36 on 12 Sep and 7 of 15 under the narrow glob; the family was 11 when this was first written --
  **[T15] re-measure, never quote**.) **A/B every red against a clean base worktree before you report
  the tally**, because the sweep cannot tell a generator your branch broke from one that was
  already red: this pass found 12 red, and two of them (`author_regatta_assets`,
  `author_broadside_assets`) were **OK on base and red on the branch** — a reader of a shipped
  measurement it depends on had gone stale, which the bare count presented as somebody else's
  debt. The real ones fall in two classes — a spent one-shot `assert` (the donor moved on) and an asset key a
  platform change deleted while the generator that authors it was left untouched. That second
  class is the one to carry: **a generator that owns an asset's content is a second place every
  schema change has to land, and it does not fail at the time of the change** — it fails months
  later, on somebody else's branch, the next time anyone runs it.

- **[T16] Registration can land in TWO lists, and a generator knows one.** The ship rule above
  ("a generator that owns an asset's content is a second place every schema change has to
  land") has a sharper variant when the schema change is a SPLIT. This project's arcade
  rosters split into a master (what the injected `SO_GameList` resolves) and per-grid override
  lists; every `Tools/Build/author_*_assets.py` predates the split and registers the master
  alone. Four modes shipped launchable-but-invisible before anyone noticed, and the fix in the
  ASSET does not fix the GENERATOR — re-run it and the card falls off the grid again. When a
  branch discovers a registration gap, ask how many lists the thing has to be in, then grep the
  generators for how many they write; the answer is usually "one" and it does not fail until
  somebody regenerates, on another branch, months later.

- **[T17] A gate you wrote this branch is an INSTRUMENT — point it at the whole tree and READ it, don't
  just confirm your branch is green.** A gate exists because you just learned a rule; the rule is
  older than the gate, so the tree is full of places nobody applied it. One session wrote a gate for
  a fauna-replication seam, watched it pass, and shipped — then a §2 read of a neighbouring file
  suggested WIDENING the heuristic, and the widened gate immediately named **three more producers**,
  one of them live and spawning a hundred of the exact object the bug is about. Reading the diff had
  produced none of them, because none of them contained the word the search was built on. Two
  corollaries. **Before widening a heuristic gate, enumerate its candidate hits on the current tree
  and classify them by hand** — that turns "will this cry wolf?" from a fear into a measurement (8
  hits, 1 false, and the false one was excluded by a rule already in the gate). And **a gate that
  flags a correct-but-one-call-away funnel is not wrong** — it asks about the enclosing method
  because a sibling holding the rule is a real failure mode; the answer is usually to move the
  producer INTO the funnel, which leaves better code than the exemption would have.

- **[T18] A LONG branch invalidates its own earlier rounds, and the doc from round 2 is the last place
  anyone looks.** The rule "a threshold that is a function of X must be re-derived when X moves"
  is normally about two branches or two authors; on a multi-round branch it is about YOU. Round 3
  of one branch sized a prism specifically to clear a PhysX sample step at the vessel's then
  speed, and wrote the derivation up as *"an 8-10% margin"*; round 4 doubled that speed on the
  user's request and added a launch kick. Nothing failed — no gate covered it, and the round-3
  prose went on describing a vessel the branch no longer shipped, with the margin actually
  **negative** (measured: the mode's signature manoeuvre landing 5 times in 6). So at §2, list
  every constant the branch DERIVED from another, and re-derive each against the values the
  branch is shipping NOW. **A MEASUREMENT quoted into a doc is a derived value too**, and
  the commonest stale one: a count taken at commit 2 describes the tree at commit 2, not the tree
  you are shipping. One branch's write-up said "shared by 25 font assets" — true when measured,
  **21** by ship time, because a later commit in the same branch deleted four of them. Re-run
  every number a doc states, at ship time, against the tree as it now stands. Then close it the
  way it should have been closed: put the derivation in the offline model so it recomputes, and
  add a mirror gate that re-reads the source values out of the shipped assets — a transcription
  is only true on the day it is made.

- **[T19] An absence that is true only because of where an ASSET was filed is not guarded by the code.**
  The absence-claim rule below covers comments that rot. This is the variant that was never true
  in the way it read: `Projectile.SweepVesselsAlong` used single static scratch and argued it was
  safe because no vessel effect fires another projectile mid-sweep — correct, but only because
  `ProjectileChainFire` happens to be authored as a projectile-PRISM effect, so the ship arm
  cannot reach it. Nothing stops someone dropping it into a ship container. When a comment
  explains why a hazard cannot arise, ask whether the reason is structural or editorial; if a
  designer could falsify it from the inspector, take the class (here: rent by depth, as the
  sibling sweep already did) rather than restating the instance.

- **[T20] Deleting a FIELD by pattern orphans its attributes, and the orphan adopts the next member.**
  A regex that removes `[SerializeField] float thing = 1f;` leaves the `[Tooltip(...)]` block above
  it — which then stacks onto whatever member follows, and `CS0579: Duplicate 'Tooltip'` names the
  INNOCENT member several lines away. Multi-line attributes make this invisible in a diff read at
  speed. When retiring a serialized field, delete the whole DECLARATION — attributes, doc comment,
  trailing blank line — and read the two members either side of the hole afterwards. Same shape as
  the merge trap above (§1, shared trailing lines), reached without a merge.

- **[T21] A `try/catch` turns a hang into a silent degradation. That is worth having and it is not a
  fix.** Containing an uncontained throw is the right engineering move — an exception inside a
  covered-screen phase is otherwise a load screen and no other symptom — but the moment you ship it,
  the bug stops presenting as "it hangs" and starts presenting as "it works but half of it is
  missing", which reads as a *different, smaller* bug. If you add containment while still hunting a
  cause, say so explicitly and keep hunting: a session shipped exactly this and the user's next
  report ("it lost all the structure") was the same one-line defect wearing the fix as a costume.

- **[T22] A successor rule resolved TWICE is resolved once wrong.** When a loop learns a new "what
  follows what" (a circuit's closing leg, a wrap, a ring buffer), every index in that loop is part
  of the change — and a raw `+ 1` sitting twenty lines below the call you did update is not a
  smaller version of the same thing, it is the OLD rule surviving where nobody re-read. Resolve the
  successor ONCE into a local and have everything in the body read from it, then gate it: a source
  rule refusing any arithmetic index into the collection, with a negative control that re-injects
  the line that shipped. Static analysis cannot see this — the model that mirrors the code computes
  its own successors correctly, and unit tests that exercise the element builder never run the
  assembler.

- **[T23] A RETIREMENT leaves residue, and the residue is the part that later reads as a live
  feature.** When a branch builds a mechanic and then cuts it, sweep for what the cut could not
  see: a local whose only reader went (`var root = vessel.Transform;`), a helper with zero callers
  (an "external-motion mode" nobody drives), a member made `public`/`static` for the consumer that
  no longer exists, a docstring fragment orphaned when its enum member left, a cross-reference to
  a section number the retirement record took over, and line-wraps that only existed to fit a
  deleted parameter. Grep each retired identifier repo-wide, then `git diff <base> -- <file>` each
  touched file: **a file whose remaining diff is only residue should come back to zero**, which is
  a much sharper test than reading the diff.
  Two specific shapes are worth naming. **A public surface that must never be read is a trap
  generator, not a trap record** — an accessor kept "for documentation" after its consumer is gone
  is exactly where the next author reaches; put the warning on the thing that still exists and
  delete the surface. And **a green test named for a retired feature is worse than no test**:
  rename it to the contract it actually pins (usually a property of the pure function underneath,
  independent of the caller policy that was cut) or delete it — a passing assertion is read as
  evidence the named behaviour still ships.

- **[T24] A comment asserting an ABSENCE rots exactly as silently as one asserting a presence.**
  §2's producer rule and its dead-surface mirror both cover claims about what the code DOES.
  The third shape is a comment that argues why something is NOT there — "no property block",
  "nothing here is per-instance", "no per-frame write" — written as justification for a design
  the branch then changed. It reads as doctrine, it sits next to the code that now contradicts
  it, and a future reader takes it as forbidding what the code already does. One session left
  five such sites across an HLSL header, a shader cbuffer note and a C# docstring, the worst of
  which argued against the method directly above it. When a branch adds the very mechanism an
  old comment rules out, grep the touched files for the old justification — the missing
  distinction is usually a qualifier the original never needed (here: per-FRAME is ruinous,
  per-SHOT is not).
