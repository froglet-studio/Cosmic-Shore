# Spec rev121-c-whats-new: Rev 121 C - Changelog: the updates listed on the left, the picked one on the right

Board: R121C.dc.html on Frogma (https://claude.ai/artifact/W8DBZaW6pu3L28egCjrKo2), tab What's New, version 1795251681-3892
Status: approved (Shombith, 2026-11-21: "I like C build C", in the project thread)
Allowed overrides: 0
Generators: Tools/Build/author_menu_settings.py

## Target

The WHAT'S NEW window over Menu_Main (`WhatsNewModal`, modal type `WHATS_NEW = 22`), built in code by
`ScreenSwitcher.EnsureWhatsNewModal` with the Voyage window's kit (`VoyageKit`), as the Quests window is. Its words are
`Assets/Resources/WhatsNew.asset` (`WhatsNewSO`). Its door is a WHAT'S NEW button in Settings' General links
(`SettingsView.prefab`, written by `author_menu_settings.py`), and it opens once by itself after an update.

## Regions

A 1600 x 860 window at (160, 150) of the 1920 x 1080 board, fitted to the canvas (`VoyageKit.Fit`): the header and the
list of updates on the left (420 wide), a rule, and the picked update on the right (picture, version and date, name,
line, "In this update" with up to three features side by side, "Also in this update"); Close and the entry's button at
the foot right.

## Components

| Component | Role | Token(s) | Type role | Spacing | Sliver | Unity |
|---|---|---|---|---|---|---|
| update row | a choice (picked: band + selection line) | band RowPicked, line Cyan (web selection), words Light / Sub | label 14, data 20 | 80 tall, 6 apart | - | `WhatsNewModal.MakeRow` |
| button "Close" | quiet, B | plate Popup .78, edge Sub, label Light | button 18 | 200 x 56 | 10 | `VoyageKit.Quiet` |
| button "Set sail" | the call to action (Lime), A | fill CtaFill (web cta), label CtaText | button 18 | 260 x 56 | soft band | `VoyageKit.Cta` |
| Settings "WHAT'S NEW" | link button | KitButtonSmall Default | button-small | in the General links row | 10 | `author_menu_settings.py` LINKS |

## States

| State | Looks like | Token / rule |
|---|---|---|
| rest | rows quiet, the newest tagged New | Sub words |
| focus | the pad's selection on a row shows that update | RowPicked band, Cyan line |
| picked | the picked row's band and line, its words Light | RowPicked, Cyan, Light |
| newest only | Set sail shows only on the newest update with a button | WhatsNewSO.Entry.Button |
| in a match | the Settings button is hidden | GameSettingsPanelController |

## Text

Every word is a `WhatsNewSO` field (eyebrow, title, New tag, headings, the version line template, Close, the keys, and
per update: version, name, date, line, features, also, button words). Sizes as the board: label 14, h1 36, name 44,
line 18, feature title 20, feature line and also 15, buttons 18. The version line is "{version}  -  {date}": the board's
middle dot is not in the menu font (voyage_slice.json: printable ASCII only).

## Icons

None. The keycaps (B, A) are the kit's outlined boxes.

## Motion

The window's own open (ModalWindowManager); no idle motion.

## Devices

- PC: the board's layout at 1920 x 1080, fitted evenly to other sizes.
- Phone: the same window scaled to the canvas height (1080 at every width); words at 15 and up.
- iPad: 4:3 is narrower than the board, so the fit scales the window down to the width.
- Gamepad: up and down walk the updates (moving shows the update), right goes to the buttons, B closes (back to Settings when opened from there), A on Set sail.

## Literals

| Literal | Count | Token |
|---|---|---|
| #E6E9FF | 10 | TextLight (web text) |
| #747BAD | 8 | NeutralLightest (web text-soft) |
| #434C89 | 4 | SurfaceLight (web surface-raised, line) |
| #99FF80 | 4 | Cta (web cta, cta-fill) |
| #00041F | 3 | SurfaceVeryDark (web ground, on-fill) |
| #00010A | 1 | SurfaceBlack (web ground-deep) |
| #A600FF | 1 | Tooltip (web tooltip, commit, commit-fill) |
| #FFFFFF | 1 | UITheme.OnCommit (web on-commit) |
| #00D4FF | 1 | ControlActive (web control, selection) |
| #B8C0F0 | 1 | Q1 (a feature's line: the build uses the kit's Sub) |
| #758FFB | 1 | Q2 (the Home backdrop's sea lines, not part of this window) |
| #96ACFC | 1 | Q2 (the Home backdrop's sea lines, not part of this window) |
| #B9C9FA | 1 | Q2 (the Home backdrop's sea lines, not part of this window) |
| rgba(0,0,0,0) | - | Q3 (the dim, the plate and the soft fades: the kit's Popup and Outline at an alpha) |
| rgba(0,1,10,.74) | - | Q3 (the dim, the plate and the soft fades: the kit's Popup and Outline at an alpha) |
| rgba(0,1,10,.82) | - | Q3 (the dim, the plate and the soft fades: the kit's Popup and Outline at an alpha) |
| rgba(0,1,10,.95) | - | Q3 (the dim, the plate and the soft fades: the kit's Popup and Outline at an alpha) |
| rgba(0,4,31,.6) | - | Q3 (the dim, the plate and the soft fades: the kit's Popup and Outline at an alpha) |
| rgba(0,4,31,.78) | - | Q3 (the dim, the plate and the soft fades: the kit's Popup and Outline at an alpha) |
| rgba(0,4,31,.86) | - | Q3 (the dim, the plate and the soft fades: the kit's Popup and Outline at an alpha) |
| rgba(0,4,31,.92) | - | Q3 (the dim, the plate and the soft fades: the kit's Popup and Outline at an alpha) |
| rgba(0,4,31,.96) | - | Q3 (the dim, the plate and the soft fades: the kit's Popup and Outline at an alpha) |
| rgba(0,4,31,0) | - | Q3 (the dim, the plate and the soft fades: the kit's Popup and Outline at an alpha) |
| rgba(117,143,251,.14) | - | Q3 (the dim, the plate and the soft fades: the kit's Popup and Outline at an alpha) |
| rgba(117,143,251,0) | - | Q3 (the dim, the plate and the soft fades: the kit's Popup and Outline at an alpha) |
| rgba(153,255,128,0) | - | Q3 (the dim, the plate and the soft fades: the kit's Popup and Outline at an alpha) |
| rgba(230,233,255,.8) | - | Q3 (the dim, the plate and the soft fades: the kit's Popup and Outline at an alpha) |
| rgba(230,233,255,0) | - | Q3 (the dim, the plate and the soft fades: the kit's Popup and Outline at an alpha) |
| rgba(34,38,69,.9) | - | Q3 (the dim, the plate and the soft fades: the kit's Popup and Outline at an alpha) |
| rgba(67,76,137,.9) | - | Q3 (the dim, the plate and the soft fades: the kit's Popup and Outline at an alpha) |

## Questions

Q1. A feature's description line is #B8C0F0 on the board, which no UI token carries. Board: a pale blue for body lines. Meanwhile: the kit's Sub (text-soft).

Q2. The board's sea-line blues (#758FFB, #96ACFC, #B9C9FA) belong to the Home backdrop drawn behind the window. Board: the menu behind. Meanwhile: nothing is drawn; the real menu shows behind the dim.

Q3. The dim, the plate and the soft fades are translucent colours. Board: rgba of ground and line. Meanwhile: the kit's Popup at .74 (dim) and .96 (plate), Outline for edges.

Q4. The board's eyebrow read "Settings - Other", but Settings has no About group and its links live on General. Board: Settings > Other > About. Meanwhile: the eyebrow is "Cosmic Shore" and the door is a WHAT'S NEW button beside CREDITS on General.

Q5. Older updates (0.2, 0.1) were placeholders on the board. Board: three entries. Meanwhile: the asset ships with Update 0.3 only; older entries are an asset edit.

## Build map

| Change | File | Kind | Shared? |
|---|---|---|---|
| the window | Assets/_Scripts/UI/Modals/WhatsNewModal.cs, WhatsNewRowFocus.cs | code (built at start) | Menu_Main only |
| the rule | Assets/_Scripts/UI/Modals/WhatsNewRules.cs | code | - |
| the words | Assets/_Scripts/ScriptableObjects/WhatsNewSO.cs, Assets/Resources/WhatsNew.asset | data | - |
| build and auto-open | Assets/_Scripts/UI/ScreenSwitcher.cs (enum, two calls), ScreenSwitcher.WhatsNew.cs | code | Menu_Main |
| the door | GameSettingsPanelController.cs, author_menu_settings.py, SettingsView.prefab | generator | yes: SettingsModal is in Menu_Main and every mode's pause menu; the button hides outside the menu |
| test | Assets/_Scripts/Tests/Editor/WhatsNewRulesTests.cs | edit-mode | - |

## Differs from the board

- The window has no picture of its own yet: the newest entry's Art is empty, so the HyperSea (the galaxy the Voyage window paints) shows. The board's Ark over the galaxy needs an Ark sprite in the asset.
- The door is on Settings' General tab beside Credits, not Other > About (Q4).
- The version line uses " - " not a middle dot (the font).
- Only Update 0.3 is listed (Q5).
