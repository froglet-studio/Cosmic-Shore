# Spec sg5-play-ready: Play / Ready, the Ruby commit button

Board: SGButtons.dc.html ("Style Guide 5 - Buttons and their states", Frogma Style Guide tab), version 1794500159-be82
Status: approved (Shombith, 2026-11-05: decision D5 of Docs/UI_STYLE_GUIDE.md section 13, "Play, which launches a game, is Ruby like Ready"; the board draws it) - built in this PR; Q1-Q4 open, the build follows each "Meanwhile"
Allowed overrides: 0
Generators: Tools/Build/author_ui_kit_prefabs.py, Tools/Build/author_maelstrom_ui.py, Tools/Build/author_ingame_hud.py
Extracted with: `frogma_to_unity.py extract SGButtons.dc.html --id sg5-play-ready` (then finished by hand)

Only the **Play / Ready** row of the board is built here. The other five rows (CTA, Purchase, Default, Cancel, Danger)
were built before (the kit, #1619 for the hover sheen); where they differ from the board is listed at the end, not
changed.

## Target

- **The kit button** (`Assets/_Scripts/UI/Kit/KitButton.cs`, drawn by `Tools/Build/author_ui_kit_prefabs.py`): a new
  variant `KitButtonVariant.Commit = 10`. Code, so it reaches every KitButton that names it; no prefab changes its
  look by itself.
- **Its first two buttons**, the two that are literally the board's Play / Ready:
  - Maelstrom's hub **READY** (`Hub/Ready`, `ReadyButton`) and summary **PLAY AGAIN** (`Sum/Again`), in
    `Assets/_Scenes/Multiplayer Scenes/Maelstrom.unity`, written by `Tools/Build/author_maelstrom_ui.py`. One scene,
    not a shared prefab (the hub is authored in the scene).
- **The kit test scene** (`Assets/_Scenes/Game_TestDesign/UIKitTestScene.unity`, not in the build): a Commit row
  beside the other variants, transparent and opaque, so the variant can be looked at on its own.
- **Not here**: every other "Play" button. `KitButtonVariant.Play` is the lime call to action with the gradient and
  is used for JOIN, RECONNECT, RETRY, CONFIRM, DONE, SUBMIT, WATCH, RESUME. Which of those launch a game is Q2.

## Regions

The board is one table: a label column (260 wide: the row's name in H3 and its use in Body small) and five state
columns of 210-wide buttons, 24 apart. Only the button itself is built; the table is the guide's own layout.

## Components

| Component | Role | Token(s) | Type role | Spacing | Sliver | Unity |
|---|---|---|---|---|---|---|
| Play / Ready button, opaque | commit: no way back (launch a game, Ready) | fill and border `Tooltip` (web `commit`, `commit-fill`); label `UITheme.OnCommit` (web `on-commit`) | Button (Chakra Petch SemiBold 18, caps) | padding 0 28, height 52 | 10 (`kit_sliver10_*`) | `KitButton` variant `Commit`, fill `Opaque`, `KitGradient` off |
| Play / Ready button, transparent | the same, off a popup or a line | border and label `Tooltip`, fill `Tooltip` at 12% | Button | as above | 10 | `KitButton` variant `Commit`, fill `Transparent` (Q1) |

## States

| State | Looks like | Token / rule |
|---|---|---|
| rest | flat Ruby fill, white caps label | `Tooltip`, `UITheme.OnCommit` |
| hover | one soft white band across the fill | the kit's sheen (`UIThemeSO.hoverSheen*`, Rev 35 C), unchanged; the opaque fill also lightens 10% (`KitButton.HoverLighten`) |
| focus | a ring round the button | the board draws `ControlActive`; the kit draws `Selection` (Q4) |
| pressed | sinks 2, darker | the kit darkens 15% (`PressDarken`); the sink is the kit's existing press motion |
| disabled | grey fill, dark label | `InactiveLight` fill, `TextInactive` label: the kit's `Inactive` palette, unchanged |

## Text

- The label is the Button role, caps: "READY", "PLAY AGAIN" on Maelstrom (authored in the generator; the hub's
  "Ready 4/4" count is filled by `MaelstromReadySeat` at run time, unchanged). No new words.
- Board sizes: 14px Label (the column heads), 15px Body small, 18px Button, 22px H3, 36px H1: the guide's own.

## Icons

None on the button. The Maelstrom buttons keep their keycap (A), drawn by `KitKeycap`, unchanged.

## Motion

None new. Hover is the kit's sheen (`micro`, 120 ms); the press is the kit's (`std`). The board animates nothing.

## Devices

- PC: 1920x1080, the button at its authored size (Maelstrom READY 300x60, PLAY AGAIN 280x60).
- Phone: the PC UI scaled by the canvas (D6): 390 tall is 0.36, so READY is about 108x22 points; text near 6.5 pt
  (the known cost, style guide section 8). No phone layout.
- iPad: 820 tall is 0.76: READY about 228x46 points.
- Gamepad: READY is the hub's selected button and A presses it (as before); the focus ring draws round it. No new
  selectable, so the focus order is unchanged.

## Literals

| Literal | Count | Token |
|---|---|---|
| #A600FF | 4 | Tooltip (web tooltip, commit, commit-fill) |
| #FFFFFF | 4 | UITheme.OnCommit (web on-commit) |
| #00D4FF | 6 | ControlActive (web control, selection): the board's focus ring (Q4) |
| #5C5F70 | 6 | InactiveLight (web text-off) |
| #25262D | 6 | TextInactive |
| #00041F | 27 | SurfaceVeryDark (web ground, on-fill) |
| #00010A | 1 | SurfaceBlack (web ground-deep) |
| #E6E9FF | 9 | TextLight (web text) |
| #747BAD | 8 | NeutralLightest (web text-soft) |
| #99FF80 | 5 | Cta (web cta, cta-fill): other rows |
| #FF4B3A | 5 | Danger (web danger): other rows |
| #FFAE00 | 4 | Purchase (web purchase, purchase-fill): other rows |
| #222645 | 1 | SurfaceDark (web surface) |
| #434C89 | 1 | SurfaceLight (web surface-raised, line) |
| #3DB2FF | 0 | Selection: the kit's focus ring today, not on the board (Q4) |
| rgba(255,255,255,.28) | - | the hover sheen: `UIThemeSO.hoverSheenAlpha` (0.16 in the game; the board draws .28) |

## Questions

- **Q1.** The transparent Play / Ready: what does its label wear? Board: the board draws only the opaque button and
  says "elsewhere transparent with the same border"; Ruby text on the dark ground is about 3.9:1 (under 4.5:1).
  Meanwhile: the kit's transparent rule (Ruby border and label over Ruby at 12%); both Maelstrom buttons are opaque,
  so nothing ships transparent.
- **Q2.** Which other "Play" buttons launch a game and should turn Ruby (Configure's START GAME, the arcade START, the
  end-of-match PLAY AGAIN and CONTINUE on GameCanvas, the Ark's ARENA)? Board: "Play / Ready - no way back: launch a
  game, Ready". Meanwhile: they stay the lime `Play`. GameCanvas is in every mode's scene, so its two buttons wait for
  a yes (CLAUDE.md: ask before changing a shared prefab every mode uses).
- **Q3.** Maelstrom's NEXT (shown in READY's place between rounds): a commitment? Board: not drawn. Meanwhile: lime
  `Play`.
- **Q4.** The focus ring: the board and the guide (D3) say Jade (`ControlActive`); the kit still draws the old sky-blue
  `Selection`. Board: Jade ring. Meanwhile: unchanged; it is the guide's "Still to do in Unity" item 1, which moves
  every selection (generators, SelectionRim materials, `colour_domains.json`), not this button.

## Build map

| Change | File | Where | Shared? |
|---|---|---|---|
| `KitButtonVariant.Commit = 10`; `KitButton.Colors` (static, the one palette table) | `Assets/_Scripts/UI/Kit/KitButton.cs` | code | the kit (every KitButton that names Commit) |
| `UITheme.OnCommit` (the web `on-commit`, white) | `Assets/_Scripts/UI/UITheme.cs` | code | - |
| `tooltip` documented as the web `commit` | `Assets/_Scripts/UI/UIThemeSO.cs` | code | - |
| the Python mirror: `VARIANTS["Commit"]`, `button_palette("Commit")`, the test scene row, a validate assert | `Tools/Build/author_ui_kit_prefabs.py` | generator | writes the kit test scene only |
| a Commit button's `KitGradient` is written off | `Tools/Build/author_ingame_hud.py` (`kit_button`) | generator | no output change (no HUD button is Commit) |
| READY and PLAY AGAIN to `Commit` | `Tools/Build/author_maelstrom_ui.py` -> `Maelstrom.unity` | scene, generated | one scene |
| tests | `Assets/_Scripts/Tests/Editor/KitButtonCommitTests.cs` | edit mode | - |

## Differs from the board

- **The focus ring** is the kit's `Selection` sky blue, not the board's Jade (Q4).
- **The hover sheen** is 0.16 white in the game and .28 on the board: the board exaggerates it to be seen at board
  zoom; the game's value is Rev 35 C's, picked in the editor. Not changed.
- **The other rows** (seen while extracting, not built here): the board's dark labels are `SurfaceVeryDark`; the kit
  writes `SurfaceBlack` (1 step darker, the same family). The board's Default and Cancel are opaque light fills; the
  kit draws Default transparent off a popup. Both are the kit's existing rule, for a later spec.
- **Ruby under a worn colour set**: the commit is the set's Ruby rim (`ColorSetUITheme`); white on it is 4.5:1 or
  better in the authored theme (`froglet_style.py --check`) but not checked for every set in Unity.
