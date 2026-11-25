# Spec toybox-rev145h: the Toy Box and its Configure window, Rev 145 H

Board: project/R145H.dc.html on Frogma's Toy Box tab (Rev 145), read at canvas version 1795561163-fa75
Status: approved (Shombith, 2026-11-24: "Let's go with H. Build it. H."), in PR
Allowed overrides: 0
Generators: Tools/Build/author_menu_toybox.py, Tools/Build/author_menu_toyconfigure.py

## Target

Menu_Main's `ModalWindows/ToyboxScreenModal` (ToyboxView.prefab, ToyCard.prefab) and
`ModalWindows/ToyboxGameConfigureModal` (ToyConfigureView.prefab, ToyVariantTile.prefab), both written by their
generators. Rev 145 H is Rev 142 D's Toy Box with Rev 142 A's Configure (Rev 143 E), Garrett's 2D toy glyphs with a
depth look (Rev 144 G), the Shelf variation tiles and Today's activity as a banner (Docs/MENU_REDESIGN_B.md
sections 235 to 235.3).

## Regions

- Toy Box: header (Toy Box, the toy count, Shuffle, Back); the Today banner across the window; the toy grid, four
  lens cards over three, centred.
- Configure: header (Toy Box / section / toy name, Back); the live preview large on the left; a column on the right:
  the toy's line, Variations with the count, the Shelf (three across), the status line, NAVIGATE and SWITCH.

## Components

| Component | Role | Tokens | Type role | Spacing | Sliver | Unity |
|---|---|---|---|---|---|---|
| Toy card | open a toy | SurfaceVeryDark fill, SurfaceDark border, Selection focus | H3 name, Label section | 252 x 302, gaps 34 / 18 | 14 | ToyCard.prefab (KitTile, ToyboxCard) |
| Lens | the toy's glyph in a round of sea | SurfaceDark 55% disc, SurfaceLight ring | - | 196 round, 20 from the top | - | ToyCard/Lens, kit_ring_fill mask |
| Glyph depth | Rev 144 G | SurfaceLight to SurfaceDark extrusion, SurfaceBlack shadow | - | 12 layers | - | KitGlyphDepth (new) |
| Toy grid | centre the short row | - | - | 4 columns | - | KitCenteredGrid (new) |
| Today banner | open today's activity | SurfaceVeryDark fill, sea, TextLight 8% sweep | Label eyebrow, H1 activity, Label countdown | full width x 178 | 14 | ToyboxView/Daily (DailyActivityCard) |
| Try it chip | the call to action | Cta fill, SurfaceBlack label | ButtonSmall | 240 x 56 | 10 | Daily/Reward |
| Shuffle | shuffle the toy box | KitButtonSmall Default | ButtonSmall | beside Back | 10 | ToyboxView/Header/Shuffle (ToyboxShuffleCard) |
| Live preview | the toy, live | panel frame, sea grid | Label Live preview, H3 caption | 1120 x 720 | 14 | ToyConfigureView/Preview (ToyPreviewCamera) |
| Shelf tile | pick a variation | SurfaceVeryDark fill, SurfaceLight 55% floor glow | Body name, Label state | 205 x 150, gaps 12 | 10 | ToyVariantTile.prefab (KitTile, KitToyVariantCard) |
| SWITCH | commit the variation | KitButton Play, opaque (lime) | Button | half the column | - | SwitchButton |
| NAVIGATE | fly there | KitButton Default, outlined | Button | half the column | - | NavigateButton |

## States

- Rest: as above. Hover: the kit sheen (KitHoverSheen) on cards and tiles. Focus: the Selection ring.
- Pressed: KitTile press. Disabled: a toy that cannot answer shows Unavailable over its card; SWITCH and NAVIGATE
  grey as before.
- Selected (Shelf): the 2 px TextLight border and scale 1.04, the state reads Cta on the row you fly ("Flying").

## Text

Authored on the prefabs: "Toy Box", "Toys", "Today's activity" (from the daily config), "/", "Ends in",
"Toy Box  /", "Live preview", "Variations", "Pick a variation.", "SHUFFLE", "SWITCH", "NAVIGATE". Filled by code as
before: the count, the activity, the toy, the countdown, the reward ("Try it +10"), the toy's name, section and line,
the caption, the variation names and states.

## Icons

The toy glyphs are the codex's (ToyPortraitLibrary, drawn by Tools/Build/icon_kit/toy_portraits.py), unchanged; the
depth is drawn at run time by KitGlyphDepth, so a redraw of a glyph needs nothing re-baked. Variation pictures are the
hull icons (SO_VesselList) and option icons, as before.

## Motion

- Open: the window's blocks rise (KitEntrance, as before); the toy cards pop in one after another (Pop, by sibling).
- The glyphs bob in their lens (Card_PortraitBob.mat).
- The Today banner fades in; a soft light sweeps across it every six seconds (KitSpark on kit_sheen).
- Configure: the column rises, the toy's line fades in, the Shelf tiles rise in by position.
- Switch: the selected tile scales and its border lights (KitToyVariantCard), the live preview re-renders.
- Navigate: KitDive grows the live preview to fill the window over 0.4 s while the column and header fade, then the
  window closes and the player is placed in the world as before.

## Devices

- PC: 1920 x 1080 reference, as drawn.
- Phone: the canvas matches height, so the window scales down whole; the Toy Box grid stays four over three.
- iPad: as phone, more width to the sides.
- Gamepad: focus order is the Today banner, the toys in grid order, Shuffle, Back; in Configure the variations, then
  NAVIGATE / SWITCH, then Back (sibling order, ScreenSwitcher.Refocus).

## Literals

| Literal | Count | Token |
|---|---|---|
| #E6E9FF | 30 | TextLight (web text) |
| #747BAD | 17 | NeutralLightest (web text-soft) |
| #434C89 | 6 | SurfaceLight (web surface-raised, line) |
| #00010A | 5 | SurfaceBlack (web ground-deep) |
| #222645 | 5 | SurfaceDark (web surface) |
| #00D4FF | 4 | ControlActive (web control, selection) |
| #99FF80 | 3 | Cta (web cta, cta-fill) |
| #00041F | 3 | SurfaceVeryDark (web ground, on-fill) |
| #758FFB | 3 | the sea lines: drawn by the kit sea material (Sea_Panel.mat / Sea_Window.mat), not a token colour |
| #96ACFC | 3 | the sea lines (as above) |
| #B9C9FA | 3 | the sea lines (as above) |
| #A600FF | 1 | Tooltip (web tooltip, commit, commit-fill) |
| #FFFFFF | 1 | UITheme.OnCommit (web on-commit) |
| #B8C0F0 | 1 | the toy's line in Configure: built as TextLight |
| rgba(0,0,0,0) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(0,1,10,.0) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(0,1,10,.2) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(0,1,10,.55) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(0,1,10,.6) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(0,1,10,.7) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(0,1,10,.75) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(0,1,10,.8) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(0,1,10,.82) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(0,1,10,.9) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(0,1,10,.92) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(0,1,10,.95) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(0,4,31,.75) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(0,4,31,.78) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(0,4,31,.85) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(0,4,31,.86) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(0,4,31,.92) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(0,4,31,0) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(117,143,251,.14) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(117,143,251,0) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(153,255,128,0) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(18,20,38,.7) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(18,20,38,.72) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(230,233,255,.035) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(230,233,255,.06) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(230,233,255,.07) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(230,233,255,.08) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(230,233,255,.1) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(230,233,255,.12) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(230,233,255,.14) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(230,233,255,.22) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(230,233,255,.5) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(230,233,255,.7) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(230,233,255,.8) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(230,233,255,0) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(34,38,69,.9) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |
| rgba(67,76,137,.9) | - | a token at an alpha: rgba(0,1,10,a) is SurfaceBlack, rgba(0,4,31,a) SurfaceVeryDark, rgba(117,143,251,a) the sea, rgba(153,255,128,a) Cta, rgba(18,20,38,a) SurfaceVeryDark |


## Questions

Q1. The board's lens shows a captured frame of the world; nothing in the game holds that picture.
Board: the in-game cell frame inside each lens and across the Today banner.
Meanwhile: the lens and the banner show the kit's animated sea (Sea_Panel.mat) over the dark glass.

Q2. The board drops the shuffle card.
Board: no shuffle anywhere.
Meanwhile: Shuffle is a small button beside Back, so the feature keeps a press.

Q3. The board's Today lens shows the creature (a shark) and "+10 crystals" twice.
Board: the creature in the lens, "+10 crystals" under the activity and on the button.
Meanwhile: the lens shows the offering toy's glyph (DailyActivityCard draws the toy's portrait; no creature art
exists per activity) and the reward is on the Try it chip only.

## Build map

| Change | File | Kind | Shared? |
|---|---|---|---|
| Lens card | Tools/Build/author_menu_toybox.py -> ToyCard.prefab | prefab | Toy Box only |
| Today banner, header, centred grid | Tools/Build/author_menu_toybox.py -> ToyboxView.prefab | prefab | Toy Box only |
| Shelf tile | Tools/Build/author_menu_toyconfigure.py -> ToyVariantTile.prefab | prefab | Configure only |
| A's layout, dive | Tools/Build/author_menu_toyconfigure.py -> ToyConfigureView.prefab, Menu_Main.unity (the dive field) | prefab, scene | Configure only |
| Glyph depth | Assets/_Scripts/UI/Kit/KitGlyphDepth.cs | code (new kit piece) | new |
| Centred grid | Assets/_Scripts/UI/Kit/KitCenteredGrid.cs | code (new kit piece) | new |
| Dive | Assets/_Scripts/UI/Kit/KitDive.cs, ToyConfigureModal.cs (optional field, Navigate waits for it) | code | Configure only |
| Tests | Assets/_Scripts/Tests/Editor/ToyBoxRev145Tests.cs | test | - |

## Differs from the board

- The lens and banner backgrounds are the animated sea, not a world frame (Q1).
- Shuffle is a header button with its one line beside it, what a press will do or just did (Q2); the Today lens is the toy's glyph and the reward shows once (Q3).
- The cards show the section only; the board's "6 ways" count is not something the card is told.
- The G hover tilt (a perspective turn on the glyph) is not built: the menu canvas is flat, so it is the kit's hover
  sheen and the bob instead.
- The board's breadcrumb dot is "/" (the generators write ASCII only).
- The app shell's tab rows above the window are Menu_Main's own and unchanged.
