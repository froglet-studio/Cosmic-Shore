---
name: unity-look
description: See the real Cosmic Shore game without a person at the editor - the look sheet (Docs/LOOK_SHEET.md): the real renderer captured to PNGs headless (menu screens through ScreenSwitcher's own navigation; a race with the AI on every seat at given game seconds), gated by Tools/Build/unity_look/verify.py (a blank frame fails) and laid out by sheet.py beside a picked Frogma board or an approved earlier sheet, run locally with look.py or in CI (.github/workflows/look-sheet.yml on GameCI's image). Use on "look sheet", "screenshot the game", "what does the menu look like", "show me the Hangar", "capture the HUD", "see it in the real renderer", "NOT RUN IN EDITOR", "awaiting the owner's flight", "does it look right", before asking a person to go and check a look, and when a PR touches Assets/_Prefabs/UI*, a shader, a world effect or a vessel model. The real-renderer sheet every craft skill (/vessel-model, /ability-icon, /frogma, /world-look) looks at before the owner does.
---

# /unity-look: the look sheet

## 0. The bar, in one paragraph

A session that changed how something LOOKS shows it before asking anyone to go and check
(`Docs/claude/VIDEOS_AND_PREVIEWS.md`). Amoebius shows motion and order of events with its own renderer; the look
sheet shows the final look, from the real Unity renderer, with nobody at the editor. Garrett, 2026-10-18: "render ...
as true to the game as possible; err on the side of being too true to the game." The sheet is looked at (the Read
tool on the JPG), judged against the language of the thing (ICON_LANGUAGE, LOOK_LANGUAGE, the UI style guide, the
picked board), and attached to the branch. The owner's eye still decides; the sheet means the owner is no longer the
first person to see the thing at all.

## 1. Read before you touch

- `Docs/LOOK_SHEET.md`: the pieces, how to run it, what it does not show, the first-run checks, the rounds.
- `Docs/SKILL_TRAINING_PLAN.md` 3.1: why, and what it builds on (the hosted compile branch, the per-mode smoke
  test PR #1754, the soak launchers).
- `Assets/_Scripts/Editor/Look/LookSheetLauncher.cs` (the launcher's arguments and the two flows) and
  `LookSheetCapture.cs` (the capture and its known limits).
- Status: built and reference-compiled 2026-11-07 (`unity_refcompile --config editor`, zero errors, negative
  control proven), **not yet run in Unity**. Until the first run's checklist entry is green, say so when you hand a
  sheet over, and prefer the Game view yardstick (`*_gameview.png`) when both exist.

## 2. The pipeline (one command)

```
python3 Tools/Build/unity_look/look.py menu HOME,HANGAR,PORT,PROFILE,CODEX     # the menu, PC size + phone scale
python3 Tools/Build/unity_look/look.py mode switchback --times 5,15,30         # a race, the AI on every seat
python3 Tools/Build/unity_look/look.py mode thresher-time --times 5,15,30
python3 Tools/Build/unity_look/look.py verify Logs/LookSheet                   # the gate on an existing run
python3 Tools/Build/unity_look/sheet.py Logs/LookSheet --beside <board.png>     # the build beside its board
python3 Tools/Build/unity_look/look.py menu HOME --print                       # the Unity command, for another machine
python3 Tools/Build/unity_look/look.py --self-test
```

A cloud session has no Unity: it dispatches the CI job (Actions > Look sheet; needs the `unity-ci` licence secrets)
or hands the printed command to a machine that has one, then reads the sheet back. Never pass `-nographics` (nothing
renders) or `-quit` (Unity exits before Play).

## 3. The round

1. **Name what must be seen** (the screen, the hull in its mode, the effect at its distance) and the yardstick it is
   judged against (the picked board, the language doc, the approved earlier sheet).
2. **Run the sheet** (section 2), locally or in CI. The gate must be `ALL OK`; a blank frame is a harness fault, not
   a look.
3. **Look at it** (Read the JPG; 3-4 tiles at most, about 1.5k tokens each) and write down what fails the yardstick
   in words before touching anything.
4. **Attach it**: the sheet under `Docs/Look/<what>_<date>.jpg` when it is kept, the checklist entry naming it, the
   PR body showing it. The owner judges in the editor; fold what they say into the craft skill's LESSONS.
5. **Compare before and after** on the same seed and times: the sheet is a diff tool when both runs are kept.

## 4. Rules that keep being relearned

- A harness is not a sheet: the Urchin cradle's two rounds were proven by a harness and both were wrong on screen
  (`Docs/PRISM_ANIMATION.md`). Measure with the gate, judge with the eye.
- A black frame reads exactly like a failed gate if nobody looks: `verify.py` fails it; keep the negative control.
- The phone-scale capture is the PC layout at the phone's aspect, not the phone's canvases (they switch by platform).
  A phone look needs a device build (R3b).
- One Unity per project: a sheet run refuses when an editor has the project open; use another checkout.
- The boot-gate handling (privacy overlay, username step) exists in four copies now; when the smoke PR's shared
  helper lands, call it.

## 5. Wiring and hand-off

- New files only: `Assets/_Scripts/Editor/Look/` (editor assembly), `Tools/Build/unity_look/`, `Tools/CI/`, the
  workflow. A READER tool: it writes PNGs and JSON under `Logs/LookSheet/` (git-ignored), never an asset.
- The CI licence (`unity-ci` environment) and the shared activation helper are Shombith's (tooling, CI).
- Every card, not just Switchback and Thresher Time: call `ModeSmokeDriver` once PR #1754 lands.
- The checklist entry for the first run: "Look sheet: first run" in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
