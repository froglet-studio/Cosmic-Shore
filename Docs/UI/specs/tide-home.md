# Spec tide-home: The Tide and the quest chain on Home - option C, folded strips, focus opens

Board: RTideC (round `Docs/MenuRedesignB/rounds/tide_home.py`, boards RTide Compare, A, B, C and Notes). Not on the
Frogma canvas yet: the canvas is not reachable from this account; the boards are in the repo.
Status: approved (Garrett, 2026-11-21: "lets not x them out. li think i like c", in the project thread)
Allowed overrides: 0
Generators: Tools/Build/author_menu_home.py

## Target

Home's two docked cards under the pilot chip in Menu_Main (`HomeUpNext`, one `HomeDockCard` per card), replacing the
weekly challenge and current quest cards. Loop Atlas build order `era.tide` (Docs/LoopAtlas/07-era.yaml).

## Regions

A stack 420 wide at (40, 236) of the 1920 x 1080 board, 10 between the cards. Each card rests as a 56-tall strip and
opens while the pad's focus or the pointer is on it.

- The Tide (strip): eyebrow "The Tide", a summary at x 120 ("Week 3 of 6 · Crystal Capture"), the six-week clock
  under them (six 4-tall segments, 380 wide). Open (132 tall): the card of the week's mode, its objective, Ends in and
  Your best.
- Quest chain (strip): eyebrow "Quest chain", a summary at x 150 ("Crystal Capture · 3 of 12"). Open (178 tall): the
  chain of five nodes with their names (the current quest centred where the list allows), the next goal, the
  intensity line and "Press A or click to open".

## Components

| Component | Role | Token(s) | Type role | Unity |
|---|---|---|---|---|
| card frame | a choice, focus Jade | fill SurfaceVeryDark .9, edge SurfaceDark, focused ControlActive | - | `dock_card` |
| Tide clock segment | passed / this week / ahead | NeutralLightest / TextLight / SurfaceDark | - | `HomeUpNext.tideSegments` |
| chain node | done / current / ahead | NeutralLightest / SurfaceVeryDark in a TextLight ring / SurfaceDark | - | `chainNodes`, `chainRings` |
| chain link | lit up to the current quest | NeutralLightest / SurfaceDark | - | `chainLinks` |
| chain label | the quest's name | current TextLight, others NeutralLightest | BodySmall | `chainLabels` |
| open hint | how to open the window | Selection | BodySmall | `UpNext/Quest/Hint` |

## States

| State | Looks like | Rule |
|---|---|---|
| rest | both cards folded to their strips | `HomeDockCard`, folded by default |
| focus / hover | that card opens; the other stays folded | `ISelectHandler`, `IPointerEnterHandler` |
| leave | the card folds again | neither selected nor hovered |
| press | Tide opens the weekly challenge, the chain the Voyage window | `ScreenSwitcher` |
| no clock | the Tide strip shows the card of the week only | `tideStartUtc` empty |
| no data | the card hides | `KitVisibility` |

## Differs from the board

- The Tide strip shows the card of the week where the board shows today's job: no voyage job service reaches Home yet.
- The current week's segment is lit whole, not filled to today.
- The current node is SurfaceVeryDark inside a TextLight ring (the board: panel colour with an inset ring).
- The chain's name ("First flight" on the board) is not shown: quest lists have no chain names. The strip names the
  quest and its place in the list.
- The progress line under the goal is the intensity ("Intensity 2 of 4"): quests carry no counted progress.
- The open hint reads "Press A or click to open", since a mouse also opens the card.
- Tide 1 began on 2026-11-16 (`HomeUpNext.tideStartUtc`), six weeks a Tide. Garrett to confirm the date.
