---
name: ui-redesign-tracker
description: Verify and record completion of a Cosmic Shore UI redesign task (T1-T6) before merging its PR into bleeding-edge. Use when a UI redesign task branch is ready to merge, when asked to update the redesign tracker or checklist, or when asked whether a redesign task is actually done. Verifies acceptance criteria against the working tree rather than trusting a completion claim, updates Docs/UI_REDESIGN_TASKS.md, and routes design questions to the feedback queue.
---

# UI Redesign Tracker

Maintains `Docs/UI_REDESIGN_TASKS.md` for the Cosmic Shore UI overhaul. Runs **before** a task PR merges into `bleeding-edge`.

The point of this skill is not bookkeeping. It is to catch the gap between what a task specified and what actually landed, while that gap is still cheap to close.

The BOARD side is `/frogma` (`.claude/skills/frogma/`): opening the Frogma canvas (formerly Frogma, Menu Redesign B) in a session,
comparing a new design against the board and the build, planning and publishing a round, refreshing the repo copy, and
the offline board check of T16 as a tool (`frogma.py check <root> --only R<N>`). This skill verifies the BUILD
against the board the doc names as picked.

## Core rule

**Never tick a criterion you have not verified against the working tree.** A commit message, a PR description, or a previous assistant turn claiming a task is done is not evidence. Open the file, read the value, run the check. If a criterion cannot be verified without the Unity editor or a play session, mark it `[~]` and say so — do not mark it `[x]` and do not mark it `[ ]`.

A partially complete task stays `IN PROGRESS`. Do not round up.

## Process

### 1. Identify the task

Determine which of T1–T6 this branch addresses, from the branch name, the commit range against `bleeding-edge`, and the changed files. If more than one task is touched, handle each separately. If none maps to a tracked task, stop and say so — this skill does not track ad-hoc work.

### 2. Verify each acceptance criterion

Read the task's criteria from the tracker and check each one against the actual repo state. Notes on the ones that need care:

- **Values in prefab/scene YAML** (reference resolutions, PPU, aspect settings) — grep the serialized files for the real numbers. Do not infer from a script that sets them.
- **Override counts (T3)** — count the override entries on the canvas instance in each scene file. This is the criterion most likely to be claimed and not met.
- **"Not yet applied" / "not executed" criteria** — these are satisfied by *absence*. Confirm the change genuinely did not happen; over-delivery is a deviation, not a bonus.
- **Editor-only checks** (atlas generation, screenshots, smoke passes) — mark `[~]` and list what the human needs to confirm.

### 3. Update the tracker

**Touch only this task's own section and its own row in the status table.** Parallel branches
edit this file at the same time, so any change outside those two places — reformatting, reordering,
reflowing, re-wrapping, tidying another task's checkboxes, renumbering the queue — turns a one-line
conflict into a manual merge. Leave whitespace, column widths and line breaks exactly as found, even
where they are ugly. If another task's content looks wrong, say so in the report; do not fix it here.

The one exception is step 4: unblocking a dependent means writing that task's **Status cell and
nothing else** — not its criteria, not its notes, not its row's spacing.

- Set the status. `DONE` only when every criterion is `[x]`, or every remaining one is `[~]` and the human has confirmed them.
- Fill in branch, PR number, and completion date.
- **Deliverables** — what actually landed, with paths.
- **Findings** — anything discovered that the audit did not already record. Facts about the codebase, not opinions.
- **Deviations from spec** — every place the implementation differs from the written task, with the reason. An undocumented deviation is the failure mode this whole skill exists to prevent.

### 4. Unblock dependents

If the completed task was a dependency, move dependent tasks from `BLOCKED` to `TODO` and note it. T2 unblocks T3. T5 unblocks T6.

### 5. Route design questions

If implementation surfaced anything needing a design decision — a colour with no token, a layout the foundation does not cover, a state not specified — add a row to the **Design feedback queue** with the task, the question, and status `OPEN`.

**Do not edit `Docs/STYLE_FOUNDATION.md`.** That document is owned by the design side. Proposals go in the queue; the version log records changes only after they are approved upstream.

### 6. Report

Summarise: task, status, criteria met vs total, deviations, new queue entries, what the human still needs to verify in the editor. If the task is not actually complete, say what is missing and stop — do not merge.

## Task-specific traps

**T2** — `CanvasUpgraderUpgradedPrefabs.txt` must be checked before trusting any migration. A double pass compounds ×2.4 to ×5.76 and looks like a layout bug, not a migration bug.

**T3** — The go-ahead gate is a hard criterion. If more than one scene was re-placed without an explicit approval between them, that is a deviation and must be recorded even if the result looks correct. Also verify `statsToTrack` survived per mode; it is the single genuine per-mode value among the ~20 that differ.

**T4** — `UIThemeSO` must have exactly the 25 fields in §10. Added fields are a deviation. **Team colour fields are a spec violation**, not a helpful addition — the omission is what enforces the team-colour contract. Also confirm no call sites changed; the mapping report is the deliverable, not a refactor.

**T5** — Fonts must not be in `Assets/Unity Assests/TextMesh Pro/`. Confirm `OFL.txt` per family and the credits entry; the licence obligation is real and easy to skip.

**T6** — The Aldrich migration is an estimate. If it was executed, that is a significant deviation — roughly 1,670 references moved without design sign-off.

**T7 — a layout group with `childControl` OFF reads a child's CURRENT rect, so a self-sizing child lags one pass**
(claude/new-session-76hnyc, 2026-10-26). A `KitButton` sizes itself with its own ContentSizeFitter. A wrapper whose
layout group does not control the child's width reads the button's rect, which the fitter sets later in the same pass.
So a JOIN that turns into SPECTATE keeps JOIN's slot and spills out of the drawer. Make the wrapper control the child's
width (`author_menu_arcade.wrap_slot`). The same fitter is why a KitButton dropped into a fixed slot (the configure
strip's `<` `>`) grows past the slot onto its neighbours: use a fixed-size plain button there.

**T8 — the offline rect renderer models layout groups, but not ContentSizeFitters and not Button transitions.** A clean
"0 overlaps" from it says nothing about a self-sizing button's real width, and nothing about a tile whose outline its
component hides at runtime (`KitTile.borderAtRest`). When a fix turns on either of those, assert it in the generator
(the configure strip asserts its arrows are plain fixed buttons) instead of trusting the render. It does not model a
GridLayoutGroup or the preferred height a nested Vertical/Horizontal layout group passes up either (the Codex grid and
page, section 88), so blocks inside a scrolling page render stacked on top of each other. Check those by their layout
settings.

**T9 — `gates.sh` runs only the `author_*_assets.py` generators; the menu generators are not in it**
(claude/new-session-76hnyc, 2026-10-26, the Codex). Every `author_menu_*.py` reads and writes `Menu_Main`, so a change
to the scene or to a shared emitter (`author_menu_arcade.emit_node`) can put any of the twenty in drift with GATES:
ALL OK on screen. Sweep them yourself: `for f in Tools/Build/author_menu_*.py; do python3 "$f" --check >/dev/null 2>&1;
echo "$? $f"; done`. Also, `check_generated_assets.py --base` audits COMMITS, not the working tree ("audited 0 added"
before the commit), and it needs `DOTNET_ROOT=/root/.dotnet`.

**T10 — the menu emitters do not share every RectTransform field.** `author_ui_kit_prefabs.emit_node` writes a node's
`scale`; `author_menu_arcade.emit_node` (the configure / profile / credits chain) wrote `{x: 1, y: 1, z: 1}` whatever
the node said, until section 88 made it read `node.scale`. A generator that sets `.scale` (or `.rot`) and is not on the
Hangar's own emitter must assert the field in its validation. The Codex asserts its 0.88. Otherwise the setting
silently does nothing.

**T11 — lifting a lock (or retiring any optional part) must remove the documents the generator minted for it.**
`author_menu_topnav.py` removed only the ids its CURRENT config mints, so taking CODEX out of `LOCKED` would have
stranded the old lock view in the scene. It now removes `Ids.everything()` and allows those removals in its blast-radius
check. Any generator whose output depends on a set (locked tabs, shown seats, offered modes) needs the same: remove
everything it can ever mint, then write what it mints now.

**T12 — two generators that insert under the same parent must not both take "first"** (claude/affectionate-bardeen-pzh99d,
2026-10-27, the Port). `author_menu_codex.py` puts its CodexView first under `Screens` and writes its documents right after
Screens'. A second screen generator doing the same makes each one's re-run move the other, so the other's `--check`
drifts. Take a place relative to something only you own: the Port takes the old Port panel's place among the children
and anchors its documents after that panel's transform. Run the whole `author_menu_*.py --check` sweep (T9) after adding
a screen.

**T13 — the Frogma canvas (formerly Frogma, Menu Redesign B) is shared, so a round's number is claimed when you publish, not when you plan it**
(claude/affectionate-bardeen-pzh99d, 2026-10-27). Rev 28 was free at noon; by the time the Port's looks were drawn,
another session had published Rev 28 to 31 (Ark tabs, Squads, the Codex rethought). Re-read `project/canvas.json`
right before the publish, take the first free `rev<N>` page id AND board prefix (`R<N>*.dc.html`), and keep every
page and board the other session added (send the index with only your keys changed).

**T14 — the doc's section numbers are claimed the same way, and C# comments cite them** (claude/awesome-dirac-hssan7,
2026-10-27). This branch wrote the Ark's sections as 90-94 of `Docs/MENU_REDESIGN_B.md`; the Port merged 90-91 into
bleeding-edge first, and the merge conflicted on the doc alone. Renumbering is not a doc edit: the runtime classes'
docstrings say `Docs/MENU_REDESIGN_B.md section 93`, so a renumber is a sweep over `Assets/_Scripts`, `Tools/Build` and
`Docs/` for `MENU_REDESIGN_B.md section <N>` and `§<N>`, split by which branch's section each hit means (the other
branch's comments cite the SAME numbers and must not move). Read the base's last section number right before the PR,
not when the branch starts.

**T15 — a menu generator's node key is its fileID, so a node named for its CONTENT collides with a sibling named for its
ROLE** (claude/awesome-dirac-hssan7, 2026-10-27). A squad card had a `Pilot` value label (one of the four pick rows) and a
`Pilot` full-figure image; both became `<card>/Pilot`, and `author_menu_arcade.check_file` failed with `duplicate fileID
anchors`. It names three fileIDs and no node. Grep the generator for the shared key, and name the figure by what it is
(`Figure`), not by what it shows.

**T16 — check a canvas board's script offline before publishing it** (claude/awesome-dirac-hssan7, 2026-10-27). A
`.dc.html` board's `class Component extends DCLogic` runs in node with a two-line stub (`class DCLogic { constructor(p)
{ this.props = p; } setState(o) { Object.assign(this.state, o); } }`): build it with `new Function('DCLogic', js +
'return Component;')`, call `renderVals()`, and check that every `{{hole}}` in the template is a key it returns (loop
variables against one item of their list). Then press the handlers in order (make, pick, create). On the Rev 31 boards
this caught a `create` that read the draft after clearing it, and it gives the layout numbers (which card overlaps the
list) without rendering, which the canvas type asks sessions not to do.

**T17 - a kit TMP auto-sizes, so a runtime `fontSize` write is a no-op and the role size is a CAP** (claude/new-session-76hnyc,
2026-10-28). `K.tmp` / `H.text` emit `m_enableAutoSizing: 1` with `m_fontSizeMax` = the ROLE's size. A generator that then
replaces only `m_fontSize` (the countdown's "260 px" beat on the Display role) ships text auto-sized up to 48, and code
that sets `text.fontSize` per beat changes nothing. The countdown drew at 48 px from item 12b until Rev 33. When a text's
size is custom or set in code, also write `m_enableAutoSizing: 0` and min = max = the size, and check that line in
`validate()`.

**T18 - restructuring a prefab in place: lift the kept objects out BEFORE removing the generated parents** (same
branch). A `Patch` transform re-runs on its own output, so a kept object you reparented under a generated node (the
Slider into the footer, PlayerIcons into the pilots panel) is inside that node on the second run, and
`remove_subtree(generated)` deletes it with the node. Reparent every kept object back to a stable parent first, then
remove and re-emit the generated nodes. Also: `remove_subtree` assumes a RectTransform (`rect_of` raises
StopIteration on a plain `Transform`, e.g. a Camera object inside a UI prefab), so remove such an object by its GO and
component ids. Prove both by transforming the output a second time and asserting it is unchanged.

**T19 - after removing objects from a prefab nested in GameCanvas, check the SCENES' overrides by the derived id**
(same branch). A scene that overrides an object in a prefab nested in GameCanvas targets
`source fileID XOR GameCanvas's PrefabInstance id` with GameCanvas's guid, not the inner prefab's. So grepping the
scenes for the inner guid finds nothing either way. Compute the derived ids of every removed object (and of every
restyled kept one) and grep the 41 files that nest GameCanvas for them; 0 hits is the evidence.

**T20 - a DOTween `Sequence.Insert` before the `Append`s delays every beat** (same branch). `Append` places a tween at
the sequence's current END, so inserting a long tween first (a hex fill at 0, d, 2d) lengthens the sequence and every
appended beat starts after it: 3-2-1 would have started about 3 s late. Append the timeline first, then `Insert` the
overlays at absolute times.

**T21 - the game blends UI in LINEAR light, so a see-through dark plate is far thinner than its alpha, and a web
drawing hides it** (claude/signature-pilot-busts, 2026-11-12, the pilot profile page, section 92.1).
`ProjectSettings.asset` has `m_ActiveColorSpace: 1`: a plate at alpha a keeps (1 - a) of the LIGHT behind it. A white
sky under SurfaceBlack at 0.88 (the Credits window's dim, fine over the dark menu) is still sRGB 97, and words in
NeutralLightest on it stood at 1.5:1. A stills page composites in sRGB and showed the same plate near black, so the
drawing passed and the build did not. For words over a picture, live footage or anything that can be bright:
- compute, do not look: `author_menu_pilot_profile.contrast_over_white(words, plate, alpha)` blends as the game does;
  the generator holds every plate to 4.5:1 for every colour words are drawn in, from the alpha as WRITTEN (the
  Image's own and the one `KitThemedGraphic` paints it with), and three controls prove the rule fires;
- over white, SurfaceBlack needs alpha 0.86 for TextLight and 0.993 for NeutralLightest, so a plate with quiet lines
  on it is solid, or the quiet lines are drawn brighter;
- a drawing of such a plate on a web page blends in linear light too: `out = (1 - a) * picture + a * plate` on
  linearised channels, then back to sRGB;
- quiet words are NeutralLightest. TextInactive is the DISABLED colour and does not read on SurfaceBlack.

**T22 - look at a menu build in Amoebius before calling it built** (same branch; section 159 took four menu builds
back out because "the build was never seen before it reached him"). The engine runs the real prefabs, scripts and
`Resources.Load` with no Unity (`/amoebius`: build `Port/src/CosmicShore.Player` in a scratch worktree, start it under
xvfb with the control port, offline and silent, with a throwaway profile that declines data consent). Click through
the screen, `resize` to 4:3 and 21:9, and LOOK at each capture. For the pilot page that proved, in a few minutes, the
tab's eighteen cells, the page loading from Resources, the wiring, Next, the panel's change of side, the cut at three
shapes and Close, and it found T21. What that look does not tell you:
- the menu's sea material (`Sea_Window (colour set)`) draws WHITE in the engine, on every screen that has one, so a
  white ground is not your screen's fault: switch the `Sea` Images off for the pictures (`set "Sea" Image enabled false`);
- a paragraph's lines sit closer than TextMeshPro sets them (17.6 against the kit's 23.2 at Body), so a wrapped block
  stands about a third taller in Unity;
- the diagnostics overlay (F7) covers the top left and takes the clicks there;
- the menu pauses the game off Home (`ScreenSwitcher`, `timeScale` 0): a window must not time anything on scaled time
  (`ModalWindowManager` closes on realtime), and a paused clock in `state` is not the window's doing;
- the engine's pad reaches controls Unity's cannot: its `Selectable.IsInteractable()` ignores `CanvasGroup`s, so
  automatic navigation there wanders under closed and covered windows (a press left on the pilot page selected a game
  card, and A opened its configure window underneath). Press the pad anyway (`do "pad dpad.left"`, `pad buttonSouth`
  for A, `pad buttonEast` for B; read `get "EventSystem" EventSystem currentSelectedGameObject` after each): explicit
  navigation behaves there as in Unity, and a leak is the cue to work out what Unity's search would pick (T24);
- stand-ins the engine build needs stay in the scratch worktree. A ship never patches Amoebius (`/ship` section 0.05).
The final look, the fonts, the fade, the pad and the safe area are still the hand test's.

**T23 - a negative control names the rule that must catch it, and a token list is read from the code that fills it**
(same branch, found in review). A generator's self-test that only asks "did validate() raise" passes a control caught
by a NEIGHBOURING rule, so the rule it was written for can be dead and the self-test stays green: `expect(label,
mutate, by="words of the rule")` fails a control caught by any other. And a hand-kept list of the tokens a template
may name drifts from the C# that fills them: read it from the source (`PilotProfileModal.TokenNames`; `ArkPilotsPage`'s
`TemplateText.Fill` calls, one list a line, so `{count}` is the count's alone), and check the tokens in what was
WRITTEN to the prefab, before the rule that compares the line with its authored words.

**T24 - a window that covers the nav gives its controls an explicit pad path** (same branch: the pad left the page in
an Amoebius run, and what Unity would do was then worked out, not seen). The kit's buttons are on AUTOMATIC
navigation: uGUI picks, from every live Selectable on the canvas, the one with the best `dot / distance squared` in
the pressed direction (`Selectable.FindSelectable`, recalled from uGUI's source: the reference assemblies here list
its members without their bodies). The menu keeps the pad inside an open window by switching
`CanvasGroup.interactable` off on the screens and on the windows underneath. What sits in neither has to be off some
other way, and in the engine's live scene (2026-11-12, the pilot page open) the hub strip, the friends drawer and its
orb, the in-game HUD and the disconnect notice all read off, each by a group of its own. The top nav did not: its
`LeftArrrow` and `RightArrow` are automatic Buttons in a group that is switched off only for freestyle, so they are
candidates under EVERY open window. Under a window that leaves the nav showing that is a visible button. Under a
full-screen one it is a hidden button that A still presses: by that score on the pilot page's rectangles, up from
NEXT, up or left from PREVIOUS and left from CLOSE each pick one. So:
- a window that covers the nav authors Explicit navigation among its own controls (`WAYS` and `steer()` in
  `author_menu_pilot_profile.py`: mode 4, each way a file id, an unnamed way goes nowhere), or takes a control that
  only the pad's own key works out of navigation (mode 0: the replay viewer's EXIT, the hub strip's arrows);
- the generator holds it from what was WRITTEN: every Selectable in the prefab is named, each way lands on one of the
  window's own buttons, every button can be reached from every other, and a control proves each rule fires;
- a Selectable writes its navigation in the file that DRAWS it, so a nested kit button (`P.kit_button`: a
  `KitButton.prefab` instance, or `KitButtonSmall`, a variant with no navigation block of its own) stays automatic
  where the window's table cannot name it, and a count of the window's own `m_Navigation` blocks cannot see it. Read
  the nested prefabs all the way down and refuse one (`nested_controls`; found in review, where the count rule also
  had no control of its own);
- explicit navigation does not skip a disabled button (`Selectable.Navigate` asks only whether the target is active;
  recalled the same way), so a way should not point at a control the window disables while its neighbours stay live:
  hide it instead, since an inactive target is skipped. And the menu focuses a window BEFORE the window draws
  (`ModalWindowIn` pushes the window, which runs `Refocus`, and only then raises `OnModalOpened`), so a window that
  disables a control when it opens can disable the one just focused, and uGUI's `interactable` setter then clears
  the selection (recalled the same way): the pad has no focus at all. The pilot page has one such state, a crew of
  one, which play never reaches and a damaged save can (backlog row PP6);
- to know what automatic would do without Unity, list the live buttons outside the window (`do "buttons"` in the
  engine prints every active one with its rectangle; drop those under a group whose `interactable` reads False) and
  score them yourself: origin on the pressed edge of the selected rectangle, candidates at their centres,
  `dot <= 0` skipped. `buttons` lists Buttons only: a Slider, Toggle or input field is a candidate too.
By the same score the nav's arrows can be reached from other windows too (backlog row PP4, Shombith's nav).

## Cross-cutting checks

Run these regardless of task, since they are the project's known failure modes:

- No new hardcoded colour literals in `Assets/_Scripts/UI/` (the count only goes down)
- No new scene overrides on any GameCanvas instance
- No `SetActive` toggling introduced where `CanvasGroup` alpha was in use — subscriptions are load-bearing
- Serialized SOAP references intact on any touched prefab; the project is fail-loud by design and a lost reference throws at runtime
- No new prefab created as a copy where a variant was possible
