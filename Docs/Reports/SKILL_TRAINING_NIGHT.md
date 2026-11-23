# The skill-training night: what was built, what to review, the questions (2026-11-07 to 11-08)

**The page:** https://claude.ai/artifact/HA3pa8zrPqcAnEiU2Bg8hS (private until shared from its Share menu). **For:** Garrett (who asked), and the four. **Branch:** `cece/affectionate-fermi-x1lp81`, 14 commits, pushed;
nothing on bleeding-edge; no pull request opened. **The plan it executes:** `Docs/SKILL_TRAINING_PLAN.md`.
**The ask (Garrett, 2026-11-07):** "work for the next 10 hours taking everything as far as you can. Give me a full
report in the morning of everything I need to review, all your questions, etc."

## 1. In one minute

- Every entry of the plan that a cloud session can take forward was taken forward. The ones that need a Unity
  editor, a CI licence, an owner's eye or an owner's yes stop exactly there and say so.
- **Nothing shipped changed.** Two new editor-only scripts (the look-sheet capture and launcher); no shipped asset,
  prefab, scene or runtime script was touched. One runtime file an agent had altered as a test was restored.
- **Two discoveries changed the first entry**: the real editor ALREADY compiles on hosted runners on
  `claude/ci-hosted-unity-compile` (it waits on a licence in the `unity-ci` environment and a merge), and the
  per-mode smoke test (PR #1754) already plays every rostered card headless. The look sheet was built on both.
- **The reference compile runs in a cloud session now**: the installers are refused by the proxy, so a .NET 8 root
  was assembled from nuget.org packages alone and made a repo tool. The look-sheet C# passes it with zero errors
  (a planted bad line was caught, then removed).
- **Each audit paid on its first run**: 13 mis-wired or unbanked sound slots and no mobile banks; 72 flat signals on
  25 world effects, the GrizzlyBomb's "ring that says burst" among them; one UI round never decided; the bug number
  B48 claimed on two branches.
- **One honest negative result**: the principles ledger, checked against the eight retirements, would have stopped
  one and flagged one late. A ledger catches the second violation of a rule; the design brief is what asks the owner
  before the first.
- The night's workflows ran 60 agents; the writer and check stages of five of six hit the account's model limit
  after the miners and auditors had spent the budget. Their mined data survived; the writing was finished in the
  session itself. What that leaves unchecked is in section 4.

## 2. What was built, entry by entry

| Entry | Built | Where | Status |
|---|---|---|---|
| The ledger | the counters behind the plan, regenerable | `Tools/Build/rework_ledger.py` | done |
| The reference compile in a cloud session | a .NET 8 root from nuget packages (host, runtime, ref pack, Roslyn), `--check`, `--self-test` | `Tools/Build/unity_refcompile/dotnet_from_nuget.py` + README | done; `run.sh` and `run.sh --config editor` ran end to end on it |
| 3.1 See the real game | the look-sheet kit: capture, launcher (`-executeMethod`, FrogletTools menu), gate, sheet, driver, CI job, doc, skill | `Assets/_Scripts/Editor/Look/`, `Tools/Build/unity_look/`, `Tools/CI/unity_look_in_container.sh`, `.github/workflows/look-sheet.yml`, `Docs/LOOK_SHEET.md`, `/unity-look` | built, reference-compiled (0 errors), **not run in Unity** (checklist entry "Look sheet: first run") |
| 3.2 The world's look | the asset audit with a baseline gate (67 effects, 7 signals), the look language (14 rules, draft), the vocabulary, 17 lessons, the skill, the plan | `Tools/Build/look_kit/`, `.claude/skills/world-look/`, `Docs/Shaders/WORLD_LOOK_AUDIT.md`, `WORLD_LOOK_SKILL_PLAN.md` | built; the language waits on Garrett |
| 3.3 Decide before building | the principles ledger (271 entries, each verified at its source line), its check, the design brief, the brief step in four skills and the philosophy doc | `Docs/claude/PRINCIPLES.md`, `principles.json`, `DESIGN_BRIEF.md`, `Tools/Build/principles.py` | built; every entry drafted, waiting on its owner |
| 3.4 UI | `UI_LESSONS.md` (what Shombith picks and rejects, in his words) with its generator and check; the mockup gate with seven negative controls, measured on five real boards | `.claude/skills/frogma/references/UI_LESSONS.md`, `Tools/Build/ui_lessons.py`, `Tools/Build/check_mockup.py` | built; the parity sheet (R2) waits on 3.1 |
| 3.5 Sound | the audit of every FMOD slot with a gate; the sound language (draft); the skill | `Tools/Build/sound_slots.py`, `Docs/AudioSystem/SOUND_LANGUAGE.md`, `/sound-brief` | built; one self-test assertion open; the Meerkat briefs (R1) owed |
| 3.6 Multiplayer | the skill from the owner docs; the hand tests with what a machine can run; the scenario scaffold (rows 2-5 as data, a judge over the log timeline) | `.claude/skills/multiplayer/`, `Tools/Build/net_scenarios.py` | built; `--run` unverified (no port build here) |
| 3.7 Near-A skills | the measurement of fauna-creation, cardart and prism-morph: rounds so far, every gate with or without a negative control, the smallest steps to A | a section at the end of each SKILL.md | measured; flora and ecology not reached |
| 3.8 Later | nothing started | | by design |

Skill grades by the ledger's proxy, before and after the night: A 5 to 8, B 27 to 28, C 11 to 11, D 12 to 12 (of 55,
now 59 skills; `python3 Tools/Build/rework_ledger.py --skills`).

## 3. What to review, in order (about an hour in all)

1. **`Docs/SKILL_TRAINING_PLAN.md` sections 3 to 6**: the ranking, the decisions D1-D8, the status table. Ten
   minutes. The one decision that unlocks the most is D1 (section 5 below).
2. **The look sheet**: `Docs/LOOK_SHEET.md`, then the checklist entry "Look sheet: first run" at the end of
   `Docs/UNITY_VERIFICATION_CHECKLIST.md`. The first person at an editor runs `FrogletTools > Build > Look Sheet >
   Menu screens` and looks at `Logs/LookSheet/sheet.jpg`. Ten minutes. The expected first finding is about what
   the capture draws (the overlay canvases, a stacked camera), not a compile error.
3. **The principles ledger**: `Docs/claude/PRINCIPLES.md`, then `python3 Tools/Build/principles.py --owner Garrett`
   (136 entries), `--owner Shombith` (23), `--owner Yash` (25). Each owner strikes what is wrong (delete the entry)
   and confirms what is right (`status: confirmed`). The 80 unattributed entries belong to whoever owns the area.
   This is the longest review; it can be done an area at a time.
4. **The look language**: `.claude/skills/world-look/references/LOOK_LANGUAGE.md`, the one decision: "lit" means the
   fleet's facing two-tone, so the flatness gate measures depth and facet contrast, not Unity's Lit shader. Garrett.
5. **`UI_LESSONS.md`** (`.claude/skills/frogma/references/`): is this what Shombith meant? Five minutes. Then the
   one undecided round `ui_lessons.py --check` names (section 78, Rev 25).
6. **The sound audit's 13 findings** (`python3 Tools/Build/sound_slots.py --check`): three one-shots in loop slots,
   seven events not in the built banks, one renamed event, no mobile banks. Charles and Yash.
7. **The multiplayer skill's "contradictions between the docs"** list (section 6 below): B48 claimed twice is the one
   that needs a decision today. Yash.

## 4. What is untested, and why

- **The look-sheet C#** (two files): reference-compiled with zero errors in the editor configuration (the run's 21
  errors are the sandbox's known Vivox / Moderation reference gaps in files this branch does not touch); never run
  in an editor. The checklist entry has the first run's six steps and a negative control (`-nographics` must fail
  the gate).
- **The CI job `look-sheet.yml`**: never dispatched; without the licence it only prints a warning. Whether GameCI's
  image renders URP under Xvfb is the first run's open question (LOOK_SHEET.md 4.5).
- **`sound_slots.py --self-test`**: one assertion still fails (the expectation of a field that flows into another
  settings object); the scan, the FMOD readers, `--owners` and `--check` run on the real tree and their findings
  were spot-read.
- **`net_scenarios.py --run`**: the Amoebius two-player drive could not be exercised (no .NET SDK for the port);
  `--plan`, `--judge` and the self-test run. Several expected timeline marks are marked "to confirm with Yash".
- **The check stages the workflows did not run**: the principles entries were verified mechanically (words at the
  source line) but not read one by one for the fairness of each rule; `UI_LESSONS.md` quotes were taken from the
  miners' structured output, not re-grepped individually; the world-look references were not cross-checked against
  the LOCKED rules by a second reader. A sample read of each found nothing wrong; a full read is the owner's review.
- **Everything else** is Python and docs; each tool's `--self-test` ran and passed (`rework_ledger`,
  `dotnet_from_nuget`, `unity_look`, `principles`, `ui_lessons`, `check_mockup`, `look_kit/audit`, `net_scenarios`).

## 5. Questions and decisions waiting on the team

| # | Question | Whose |
|---|---|---|
| D1 | Put a CI Unity licence (`UNITY_EMAIL`, `UNITY_PASSWORD`, optionally a serial) in the `unity-ci` environment and merge `claude/ci-hosted-unity-compile`: the real editor then compiles every PR, and the look sheet can run in CI. This unlocks 3.1 R1-R3 and everything that rides on a sheet | Shombith |
| D2 | The look language's reading of "lit" (the facing two-tone), the lens clause, and the first three effects for the rounds (proposed: the GrizzlyBomb's halo and ring, the crystal capture, the vortex ring) | Garrett |
| D3 | Confirm or strike the drafted principles in your area (`principles.py --owner <you>`) | the four |
| D4 | `UI_LESSONS.md` and the mockup gate's noise budget (60 elements; is the Showroom's 205 right or an exception?), and which screen's parity sheet first (the Hangar is the most rebuilt) | Shombith |
| D5 | Briefs only, or synthesised reference stems too; the mobile banks and the licences before the mobile launch; which hull wires the element engine beds Charles already composed (no hull does today) | Charles, Garrett, Shombith |
| D6 | B48 is claimed twice (Ys-bleeding-edge: the reaped session; claude/mp-plan-p0: the re-invite); next free is B50. Which rows to script first; whether the live UGS check may be scheduled; whether a UGS development environment counts as "the live project" for `mp_auto.py` | Yash, Shombith |
| D7 | The order in `SKILL_TRAINING_PLAN.md` section 4, or another | the four |
| D8 | The skills' names (`/unity-look`, `/world-look`, `/sound-brief`, `/multiplayer` are suggestions) | the four |
| Q1 | Merge order: PR #1754 (the smoke test) before the look sheet's mode case can play any card; until then Switchback and Thresher Time only | Shombith, Yash |
| Q2 | The boot-gate handling now exists in four copies (two soak runners, the smoke driver, the look launcher); the smoke PR proposes one shared helper | Yash |
| Q3 | `build.py`'s `UNOBTAINABLE_NAMESPACES` could list `Unity.Services.Vivox` and `Unity.Services.Moderation`, so a cloud session's reference compile reports zero project errors instead of the sandbox's 21 | Shombith |
| Q4 | Two dangling references the world-look audit found: `Rhino.prefab`'s jet particles point at a material no `.meta` owns; `GrizzlySniperProjectile.prefab` and `fxriptidecone.prefab` point at meshes that resolve to nothing | Garrett |
| Q5 | `glslangValidator` in the environment's setup script, so `/prism-morph`'s two GLSL harnesses run in a cloud session | Shombith |

## 6. Findings worth a look on their own

- **Sound**: 238 of 277 slots empty; Charles already composed one engine bed per element (`Base sounds/...`,
  banked, parameter-gated) and no hull wires them, while the audio controller already pushes the four element
  parameters every frame; wiring them on one hull is the cheapest first test (SOUND_LANGUAGE.md).
- **World look**: every project shader graph that draws a world thing sits on URP's Unlit sub-target and shades
  itself; the flatness question is contrast between facets, not the shader's name (LOOK_LANGUAGE.md).
- **UI**: Shombith picked against the recommended option in at least five rounds (the Fleet Codex twice, the hover
  effects, the route poster, the deck): draw the others as well as the recommendation.
- **Multiplayer**: `MULTIPLAYER_START_HERE.md` still says "push only to Ys-bleeding-edge" and "/verify-unity gates
  every commit", both superseded; its header date is 10-09 while its body runs to 11-01.
- **Principles**: 6 of the 8 recent retirements were the FIRST time the rule was said. The rules now exist; the
  ledger's value starts with the next design in each area.

## 7. Regenerate

`python3 Tools/Build/rework_ledger.py --skills`; each tool's `--self-test`; `principles.py --check`;
`look_kit/audit.py --check`; `sound_slots.py --check`; `ui_lessons.py --check`; the commands in
`SKILL_TRAINING_PLAN.md` section 9.
