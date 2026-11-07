# The skill-training night: what was built, what to review, the questions (2026-11-07 to 11-08)

**For:** Garrett (who asked), and the four. **Branch:** `cece/affectionate-fermi-x1lp81` (nothing on bleeding-edge;
no pull request opened). **The plan it executes:** `Docs/SKILL_TRAINING_PLAN.md`. **The ask (Garrett, 2026-11-07):**
"work for the next 10 hours taking everything as far as you can. Give me a full report in the morning of everything I
need to review, all your questions, etc."

> This file is written as the night goes. A section marked *(running)* names work whose result had not landed when
> the section was last written; the final version of this file replaces every such mark.

## 1. In one minute

- The plan (section 3 of `SKILL_TRAINING_PLAN.md`) had eight entries. Tonight every entry that a cloud session can
  take forward was taken forward: the ones that need a Unity editor, a licence, an owner's eye or an owner's yes
  stop exactly there and say so.
- **Two discoveries changed entry 3.1** (see the real game): the real editor ALREADY compiles on hosted runners on
  `claude/ci-hosted-unity-compile` (2026-11-01; it waits on the licence secrets and a merge), and the per-mode smoke
  test (PR #1754) already plays every rostered card headless with the AI on every seat (built, not run). The
  look-sheet kit was built on both, not beside them.
- **Nothing in `Assets/` changed except two new editor-only scripts** (the look-sheet capture and launcher). No
  shipped asset, prefab, scene or runtime script was touched.
- **The reference compile runs here after all.** The installers are blocked by the proxy, so a .NET 8 root was
  assembled from nuget.org packages alone and made a repo tool (`Tools/Build/unity_refcompile/dotnet_from_nuget.py`):
  the look-sheet C# passes `unity_refcompile --config editor` with zero errors, negative control proven. The editor
  run is still owed (section 4).

## 2. What was built, entry by entry

| Entry | Built tonight | Where | Status |
|---|---|---|---|
| Rework ledger | the counters behind the plan, regenerable | `Tools/Build/rework_ledger.py` (`--skills`, `--json`, `--self-test`) | done, in the first commit |
| The reference compile in a cloud session | a .NET 8 root from nuget.org packages alone (host, runtime, ref pack, Roslyn), `--check`, `--self-test` | `Tools/Build/unity_refcompile/dotnet_from_nuget.py`, README paragraph | done; `run.sh` and `run.sh --config editor` ran end to end on it |
| 3.1 See the real game | the look-sheet kit: capture, launcher, gate, sheet, driver, CI job, doc, skill | `Assets/_Scripts/Editor/Look/`, `Tools/Build/unity_look/`, `Tools/CI/unity_look_in_container.sh`, `.github/workflows/look-sheet.yml`, `Docs/LOOK_SHEET.md`, `/unity-look` | built, reference-compiled (0 errors, negative control proven), NOT run in Unity (checklist entry "Look sheet: first run") |
| 3.2 The world's look | *(running)* the asset audit with a baseline gate, the look language, vocabulary and lessons, the skill and its plan | `Tools/Build/look_kit/`, `.claude/skills/world-look/`, `Docs/Shaders/WORLD_LOOK_*.md` | *(running)* |
| 3.3 Decide before building | *(running)* the principles ledger mined from the owners' words and the LOCKED rules, its check, the design brief, the brief step in three skills | `Docs/claude/PRINCIPLES.md`, `principles.json`, `Tools/Build/principles.py`, `Docs/claude/DESIGN_BRIEF.md` | *(running)* |
| 3.4 UI | *(running)* the UI lessons mined from 43 mockup rounds with a generator and check; the mockup gate with negative controls on rejected rounds | `.claude/skills/frogma/references/UI_LESSONS.md`, `Tools/Build/ui_lessons.py`, `Tools/Build/check_mockup.py` | *(running)* |
| 3.5 Sound | *(running)* every sound slot audited with a check, the sound language, the Meerkat briefs, the skill | `Tools/Build/sound_slots.py`, `Docs/AudioSystem/SOUND_LANGUAGE.md`, `Docs/AudioSystem/briefs/MEERKAT.md`, `/sound-brief` | *(running)* |
| 3.6 Multiplayer | *(running)* the skill from the owner docs, the hand tests with what a machine can run, the scenario scaffold | `.claude/skills/multiplayer/`, `Tools/Build/net_scenarios.py` | *(running)* |
| 3.7 Near-A skills | *(running)* for fauna-creation, cardart, prism-morph, flora, ecology: lessons seeded, a round table, the negative controls each gate still owes | each skill's `references/LESSONS.md` and an appended SKILL.md section | *(running)* |
| 3.8 Later | nothing started (store assets, portraits, motion, voice, analytics) | | by design |

## 3. What to review, in order

1. `Docs/SKILL_TRAINING_PLAN.md` sections 3 to 5: the ranking and the decisions D1-D8. Section 6 is the status.
2. `Docs/LOOK_SHEET.md` and the two C# files under `Assets/_Scripts/Editor/Look/`: the first person at an editor
   runs the checklist entry "Look sheet: first run" (ten minutes). The files reference-compile clean; the first
   finding to expect is about what the capture draws (the overlay canvases, a stacked camera), not a compile error.
3. *(running)* the principles ledger: each owner reads `Docs/claude/PRINCIPLES.md` "To confirm or strike" for their
   area; every entry is drafted from the record and nothing is confirmed yet.
4. *(running)* the look language draft `.claude/skills/world-look/references/LOOK_LANGUAGE.md`: Garrett's one
   decision before the kit is built.
5. *(running)* `UI_LESSONS.md` and the mockup gate's measurement on six real boards: Shombith.
6. *(running)* the Meerkat sound briefs and `sound_slots.py --check`'s real findings: Yash, Charles.
7. *(running)* the multiplayer skill's standing rules and the scenario marks "to confirm": Yash.

## 4. What is untested, and why

- The look-sheet C# (two files): reference-compiled (zero errors in the editor configuration; the run's 21 errors are
  the sandbox's known Vivox / Moderation reference gaps in files this branch does not touch), never run in an editor.
  The editor run is the checklist entry; its open questions are the overlay-canvas capture and URP camera stacks.
- The CI job `look-sheet.yml`: never dispatched (no licence in `unity-ci`; dispatching it without one only prints the
  warning). The GameCI image's Xvfb and Mesa rendering of URP is the first run's open question (LOOK_SHEET.md 4.5).
- Everything else tonight is Python and docs, each with a `--self-test` that ran; the real-tree findings of each gate
  are recorded in its section.

## 5. Questions and decisions waiting on the team

| # | Question | Whose |
|---|---|---|
| D1 | Put a CI Unity licence (`UNITY_EMAIL`, `UNITY_PASSWORD`, optionally a serial) in the `unity-ci` environment and merge `claude/ci-hosted-unity-compile`: the real editor then compiles every PR, and the look sheet can run in CI | Shombith |
| D2 | The look language's draft and the first three effects for the rounds | Garrett |
| D3 | Confirm or strike the drafted principles in your area | the four |
| D4 | `UI_LESSONS.md`, the mockup gate, which screen's parity sheet first | Shombith |
| D5 | Briefs only, or synthesised reference stems too; mobile banks before the mobile launch | Charles, Garrett, Shombith |
| D6 | Which hand-test rows to script first; whether the live UGS check may be scheduled | Yash, Shombith |
| D7 | The order in `SKILL_TRAINING_PLAN.md` section 4, or another | the four |
| D8 | The skills' names (`/unity-look`, `/world-look`, `/sound-brief`, `/multiplayer` are suggestions) | the four |
| Q1 | Merge order: PR #1754 (the smoke test) before the look sheet's mode case can play any card; until then Switchback and Thresher Time only | Shombith, Yash |
| Q2 | The boot-gate handling now exists in four copies (two soak runners, the smoke driver, the look launcher); one shared helper is the smoke PR's proposal and this report's too | Yash |

## 6. Regenerate

`python3 Tools/Build/rework_ledger.py --skills`; each tool's `--self-test`; the commands in `SKILL_TRAINING_PLAN.md`
section 9.
