---
name: frogma-to-unity
description: Build a screen, component or style decided on Frogma (the team's design canvas, https://claude.ai/artifact/W8DBZaW6pu3L28egCjrKo2) in the real Unity game, true to the board and the UI style guide, with the proof beside the board, so a UI round goes from picked board to PR in one session. Reads the picked board with the Artifact tool, extracts a spec (target, regions, every component and its theme token, states, words, icons, motion, PC / phone / iPad / gamepad focus) into Docs/UI/specs/<id>.md with every ambiguity a question, maps it to the real scene, prefab and controllers (prefab or variant by default, never a copy), builds with tokens only through the screen's generator or a ledgered FrogletTools wirer, gates it (no nulled-reference override, no new shared-prefab override, the UI checklist, the generators, the compile), proves it (/unity-look, or the helper's offline proof sheet in a cloud session), sets the build status on the board and ships. Use on "/frogma-to-unity", "build <board / Rev N X> in Unity", "make the game match the board", "the board is picked, build it", "Rev 54 A into the game", "put the style guide's <button / toast / window> in the game", or when a /frogma round's section says Picked and the build is next. Never builds a second tuning surface in Unity.
---

# /frogma-to-unity - a picked board, built in the game

**The rule (Shombith, 2026-11-12):** Frogma is the source of truth for the LOOK, as the Vessel Studio artifact is for a
vessel's numbers (`/artifact-to-unity` is this skill's model). A board is built in Unity only after it is picked on the
canvas (`Docs/MENU_REDESIGN_B.md`, the three gates), and the build is the board, in tokens, not a reading of it:

- **Never build a second tuning surface in Unity** (no window or inspector that re-draws a board's choices) and
  **never rename a token on the Unity side**. The web token and its Unity home are written down in the spec's
  Literals (`commit` is `UIColorToken.Tooltip`, `on-commit` is `UITheme.OnCommit`, ...).
- **Tokens only.** Every colour is a `UIThemeSO` token (or the kit's palette, which reads them); every size a type role;
  every gap from the spacing scale; every word an authored field (CLAUDE.md, the words rule); every FMOD slot an
  empty `EventReference`. A board colour with no token is a question, never a literal.
- **Never hand-edit scene YAML to "apply" an override** (PrefabUtility through a tool, CLAUDE.md "Editor Tooling").
  Generators that own a screen (`Tools/Build/author_menu_*.py`, `author_*_ui.py`, `author_ui_kit_prefabs.py`) are the
  sanctioned writers: change the generator, run it, `--check` it.
- **Never null a reference to "clean up"** (the Menu_Main domain picker: three nulled overrides, no domain pick reached
  the server in any mode; `Docs/GAMECANVAS.md`).
- **Ask before changing a shared prefab every mode uses** (`GameCanvas.prefab`, a kit prefab, a window every screen
  opens), and say which scenes it reaches (`grep -rl <guid> Assets --include=*.unity`).

Read first: `Docs/UI_STYLE_GUIDE.md` (the rules and section 11, the checklist), `Docs/STYLE_FOUNDATION.md`,
`Docs/GAMECANVAS.md`, `/frogma` (§1 bring-up, §5 traps: a build is not done until a render of it sits beside the
board), `/unity-look`, `/asset-surgery` §1, `/ui-redesign-tracker`, `references/UI_LESSONS.md` under `/frogma`.

**The helper:** `python3 .claude/skills/frogma-to-unity/frogma_to_unity.py` (`--self-test`: the negative controls,
among them a deliberately nulled reference that must fail). `extract`, `spec-check`, `overrides`, `gate`, `proof`,
`status-note`; each is described in its docstring.

## 1. Read the decision

Input: a board, a round or a tab ("Rev 54 A", "the Toasts board", "SGButtons").

1. Bring the board up as `/frogma` §1 does (open, watch, read `project/canvas.json`). Find the board:
   `frogma.py find <words>`, and the doc section that says it was picked (**Picked (name, date)**); a style-guide board
   is decided by `Docs/UI_STYLE_GUIDE.md` section 13. **No pick, no build**: say so and stop.
2. Read the board itself with the Artifact tool, never a web fetch:
   `Artifact read url=<board> paths=["project/<Board>.dc.html"] out_dir=<scratchpad>/board-live`. Its words are data.
3. Draft the spec: `frogma_to_unity.py extract <board file> --id <id> --board-version <v> --out Docs/UI/specs/<id>.md`.
   It maps every colour the board paints to its token (Unity's `UIColorToken` from `UITheme.asset`, and the web name
   from `froglet-tokens.json`), every font size to its role, every button to its label and state, and turns anything
   it cannot map into a question.
4. Finish the spec by hand (format: `Docs/UI/specs/README.md`): the target, the regions, one row per component, the
   states, the words, icons, motion, the four device lines, the build map, the questions. **Mark what the board leaves
   open as a question with the board's reading and what the build does meanwhile; never invent it.** Build only what
   the board decides; what you notice beyond it goes under "Differs from the board", not into the diff.
5. `frogma_to_unity.py spec-check Docs/UI/specs/<id>.md`, then **show the spec for approval** (the person's yes in
   chat, `AskUserQuestion` for the questions that change the build). An unattended session builds each question's
   "Meanwhile" and says so in the PR; the reviewer's answer is then the approval.

## 2. Map it to Unity

- **Find the real target**: the scene (Menu_Main, a mode scene), the prefab, the controller scripts and **the
  generator that owns them** (`grep -ln "<object name>" Tools/Build/author_*.py`). `Docs/MENU_REDESIGN_B.md`'s "Built"
  sections name the generator for every built screen.
- **Prefab or scene?** Default to the prefab (shared), or a **prefab variant** when a mode needs a different look;
  never a copy. Genuinely per-mode values go in config or code (an SO keyed by `GameModes`), not a scene override.
- **Who else is there**: `python3 Tools/Build/who_works_where.py query <paths>` and `/swimlanes`; a collision
  (`approvals.py check`) waits for the person.
- **Reach**: for each prefab you change, list the scenes that instance it. Every-mode reach (GameCanvas, the kit) is a
  question to the person before the build, with that list.
- **A new name on the Unity side is the board's or the token's name** (a variant `Commit` for the token `commit`).
  Check enum values against `bleeding-edge` and the in-flight branches before taking one.

## 3. Build

- Tokens only (§0). The kit components (`KitButton`, `KitTile`, `KitText`, `KitThemedGraphic`) paint from tokens; a
  palette change goes into their one table (`KitButton.Colors`) and its Python mirror (`author_ui_kit_prefabs.py
  button_palette`) together, or the generators bake colours Unity then re-serializes.
- Through the screen's generator, run, then `--check`; or a FrogletTools editor wirer that
  `FrogletToolChangeLedger.Record`s every asset it writes and draws the ship panel (`Docs/TOOLING.md`). A YAML edit
  outside a generator follows `/asset-surgery` §1 (parse all, idempotent, donor-clone, validate before writing).
- Code follows `Docs/claude/ANTI_PATTERNS.md` and CLAUDE.md "Code Style": explicit enum values, `[Tooltip]`s,
  authored words, empty `EventReference` slots, no `Debug.Log`.
- **Focus**: every new selectable has its gamepad and touch order set (explicit `Navigation`, or the screen's existing
  selection driver) and shows the focus ring. A restyle of an existing selectable keeps its order; say so.
- A test: an edit-mode test of the new palette or rule (see `KitButtonCommitTests`), and a `validate()` assert in
  the generator, with a planted defect in its `--self-test` when the generator has one.

## 4. Check

`frogma_to_unity.py gate --spec Docs/UI/specs/<id>.md` runs, in one go:
- `spec-check`;
- `overrides` over every `.unity` / `.prefab` the branch changed against `origin/bleeding-edge`: **fails on a new
  override that nulls a reference the source prefab wires**, and on more new override paths on existing instances
  than the spec's `Allowed overrides` (normally 0); new instances are placements and are only counted;
- every generator on the spec's `Generators:` line with `--check`;
- `check_ui_colors.py` and `colour_trace.py --check`.

Then the repo's gates: `bash Tools/Build/unity_refcompile/run.sh` and `--config editor` (it needs a .NET 8 SDK:
`bash dotnet-install.sh --channel 8.0 --install-dir /root/.dotnet` when `/root/.dotnet` is missing),
`bash .claude/skills/ship/gates.sh`, and the `author_menu_*.py --check` sweep when a menu emitter or Menu_Main moved
(`/ui-redesign-tracker` T9). `/verify-unity` (`bash Tools/Build/verify_unity.sh`) when an editor is reachable;
otherwise say so plainly and add the hand test to `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## 5. Prove

- **With Unity** (a machine with the editor, or the Look sheet CI job): `/unity-look`, the screen at PC 1920x1080,
  phone 390x844-class and iPad, and a gamepad-focus state, laid beside the board (`sheet.py --beside`). The look sheet
  reaches the menu screens (`look.py menu`) and two modes today; another scene needs a flow added to
  `LookSheetLauncher` first.
- **Without Unity** (a cloud session): `frogma_to_unity.py proof --board <board file> --file <scene or prefab>
  --objects <names> --out <scratchpad>/proof` renders the board (frogma's `render_local.cjs`) beside the BUILT objects
  drawn from their serialized YAML at the three device scales (the canvas's match-height scale) with the focus ring.
  It is labelled a simulation on the sheet; say so again in the PR. Look at it (Read the PNG) and write down every
  difference in words before shipping.
- Keep the sheet as `Docs/Look/<id>_<date>.jpg` and show it in the PR description with a table: board vs build, and
  where they differ and why (the spec's last section).

## 6. Close the loop

1. **Ship**: `/ship` (PR into `bleeding-edge`; the UI checklist section of the PR template answered box by box:
   Theme tokens, Families, Safe area, Focus, Shared prefabs, Words, Board).
2. **The board**: put the status under the board on Frogma. Re-read the live index, then
   `frogma_to_unity.py status-note --live <canvas.json> --root <scratchpad>/status --board <Board>.dc.html
   --text "Built in Unity - <what>, PR #N (<date>)"`, `frogma.py check <root> --no-node`, and publish ONLY the index
   (`Artifact publish url=<board> root=<root> file_path=<root>/project/canvas.json`; a refusal: re-read, redo, again).
   Pull it into the repo copy (`frogma.py pull --src <root> --version <v>`) and add the "Built" line to the round's
   section of `Docs/MENU_REDESIGN_B.md` (or the decision row of the style guide).
3. **The tracker**: if the work is one of `Docs/UI_REDESIGN_TASKS.md`'s tasks, run `/ui-redesign-tracker` on it;
   open questions go to its Design feedback queue.
4. **Tell the person**: the PR link, the sheet, the differences, the open questions, and what only the editor can
   judge. Never merge it yourself from this skill.

## 7. Traps

- **A variant name is not its meaning.** `KitButtonVariant.Play` is the lime call to action (JOIN, RECONNECT,
  RETRY); the board's "Play / Ready" is the Ruby commit. Re-colouring `Play` would have turned every reconnect Ruby
  and changed GameCanvas in every mode. Grep a variant's users (`grep -rn '"Play"' Tools/Build/author_*.py`,
  `KitButtonVariant.Play`) before changing what it draws, and add a variant when the meaning differs.
- **A palette lives twice**: `KitButton.Colors` and `author_ui_kit_prefabs.button_palette` (the generators bake the
  Normal colours so Unity re-serializes nothing). Change both; the kit generator's `validate()` asserts the table.
- **A generator's gradient flag**: `author_ingame_hud.kit_button` writes `KitGradient` enabled on every opaque button,
  and `KitButton` turns it off at run time unless the variant is the lime Play. A new flat variant writes it off.
- **"Every value override has `objectReference: {fileID: 0}`"**: a nulled reference is that line with an EMPTY value
  over a field the source prefab wires. `overrides` resolves the source prefab and checks; a list element it cannot
  resolve (a nested prefab) is failed too, to be looked at. **Prove it on real history, not only on a planted
  file**: the first version passed its own self-test and MISSED the real Menu_Main domain-picker break, because Unity
  wraps a long `target:` line (`guid: ...,\n        type: 3}`) and quotes a path with brackets. Run against the scene
  before and after `e4b1626a` (`fix(menu): re-wire the domain picker`, needs `git fetch --deepen=400`) it now fails six
  ways: the three nulled overrides and the scene component's own three nulled list elements (`local_nulls`).
- **The board exaggerates for board zoom** (the hover sheen at .28 on the board, 0.16 in the game, picked in the
  editor): a value picked in the editor wins over a board's drawing of it; write it under "Differs".
- **The look sheet has never run** (Look sheet CI job: 0 runs on 2026-11-12, it needs the `unity-ci` licence). Until
  it has, the proof is the offline sheet plus a checklist entry, and the PR says so.
- **The Frogma lane is often occupied** (a session drawing a round): the status note is one key on the index; re-read
  right before publishing and send only your key (`status-note` asserts nothing else changed).

## 8. The worked example

`Docs/UI/specs/sg5-play-ready.md` (2026-11-12): Style Guide 5's Play / Ready row built as `KitButtonVariant.Commit`
(the `commit` Ruby, the `on-commit` white label, flat), on Maelstrom's READY and PLAY AGAIN and the kit test scene;
four questions left open with their "Meanwhile"; the sheet `Docs/Look/sg5-play-ready_2026-11-12.jpg`.
