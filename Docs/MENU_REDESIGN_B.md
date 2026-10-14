# Menu redesign, style B ("Hangar bay")

Every Menu_Main screen and modal, rebuilt in the visual style of the **"B · Hangar bay"** board on
the Port mockup canvas (`https://claude.ai/artifact/KtM3MAtYnrwu9biej4uXuM`, file
`project/SquadFocus.dc.html`). **`Docs/STYLE_FOUNDATION.md` v0.3.3 is the authority.** Where the
mockup and the spec disagree, the spec wins, and the disagreement is recorded in §5 of this document.

**Mockups from item 1 on live on their own canvas, "Menu Redesign B"**
(`https://claude.ai/artifact/W8DBZaW6pu3L28egCjrKo2`). Nothing is built in Unity until that item's
board is approved there. The Port canvas keeps the item 0 kit board where it was approved; the new
canvas carries a reference copy of it (`MenuKit.dc.html`) so the kit sits beside the items built
from it.

Work goes one item at a time, with three approval gates per item:
1. **Plan:** the inventory, build order and kit (this document).
2. **Mockup:** an artboard on the mockup canvas, using real content.
3. **Build:** prefabs, scene and theme wiring, verified offline, then committed, pushed and checked
   in the editor.

Out of scope:
- The in-game HUD, GameCanvas, end-of-game screens and vessel HUDs, including Menu_Main's
  freestyle `Game UI`. Heavy panels over the arena "eat the game" (STYLE_FOUNDATION §1).
- Gameplay, networking, and how screens are navigated. This work changes how screens look and how
  they are laid out, nothing else.

---

## 1. Status

Legend: `Planned` ▸ `Mockup` ▸ `Mockup approved` ▸ `Built` ▸ `Approved in editor`.

| # | Item | Status | Mockup board | Commits |
|---|---|---|---|---|
| 0 | Component kit: T7 sprites, Chakra Petch SemiBold (T5), type roles + `TabularText` (T6), theme components, kit prefabs, kit test scene | Built | `MenuKit.dc.html` | `3b36c361`, `fe108889`, `b0918296`, `908fc2d7`, `4cba49ee`, `e932eaf2` + docs |
| 1 | Top nav (`Safe Area/NavBar`) | Approved in editor (2026-10-14) | `Main.dc.html` (interactive), `TopNavHover.dc.html`, `TopNavLocked.dc.html` on the Menu Redesign B canvas | `8d6d2acb`, `99ef3ccd` + docs |
| 2 | Hangar screen: grid, detail and the spend-crystals panel | Mockup | `Hangar.dc.html` (interactive), `HangarLocked`, `HangarSpend`, `HangarShort`, `HangarNoArt`, `HangarNotes` on the Menu Redesign B canvas | |
| 3 | Arcade modal (`ArcadeScreenModal`): explore grid plus the party/friends panel | Planned | | |
| 4 | Game configure: `ArcadeGameConfigureModal`, plus the Maelstrom and Arena configure windows and the weekly leaderboard | Planned | | |
| 5 | Profile screen, Profile modal (avatar / display name), Episodes entry | Planned | | |
| 6 | Home screen: hub row, header, settings and freestyle buttons, invite popup | Planned | | |
| 7 | Ark screen (Elemental Converter) | Planned | | |
| 8 | Arena, Toy Box and Mission modals, and Toy configure | Planned | | |
| 9 | Settings modal and Credits | Planned | | |
| 10 | Closed surfaces: Port (locked), Store (no slot), Episodes panel, purchase confirmation | Planned | | |
| 11 | Menu toasts | Planned | | |

---

## 2. Inventory, measured from `Menu_Main.unity` on 2026-10-13

Measured from the scene YAML, the prefab YAML and the C#, not from the docs. Paths are under
`UI_Refactored/Safe Area/`.

### 2.1 The screen strip

`ScreenSwitcher` is on `Screens`. Its `screens` list, in visual order, is also the nav-bar order:

| Index | `MenuScreens` | Root | Owner | State |
|---|---|---|---|---|
| 0 | HANGAR (4) | `Hangar Screen` | `HangarScreen` (IScreen): `GridPanel` then `DetailPanel` (`HangarVesselDetailView`) | Live. The legacy `Hangar_Panel` subtree is inactive. |
| 1 | ARK (1) | `ArkScreen` | `ElementalConverterView` (IScreen) | Live. Reopened 2026-10-13 as the Elemental Converter. The old `StoreScreen` component on the root is **disabled** and its purchase grid is inactive. |
| 2 | HOME (2) | `HomeScreen` | `HomeScreen` | Live. Contents: four `MenuHubButton`s (Mission, Toy Box, Arena, Arcade), avatar and username, settings button, online indicator, the "GO OFFLINE?" bar, the freestyle button, and the party-invite popup. |
| 3 | PORT (3) | `PortScreen` | `LeaderboardsMenu` (component **disabled**), `PortSquadView` | **Locked.** `disabledScreens = [PORT]`; the tab refuses with "Not open yet." |
| 4 | PROFILE (5) | `ProfileScreen` | `ProfileScreen`, `QuestTrackView` | Live. Holds the Episodes entry button and the inactive Episodes panel. |

- **STORE (0) has no slot.** `Resources/CommerceAvailability.storeScreen` = Unavailable, and no
  STORE tab is drawn.
- **None of the five roots is a prefab instance.** They are all scene objects.

### 2.2 Modals

Modals live under `ModalWindows`. All of them derive from `ModalWindowManager` (`MWM`).

| `ModalWindows` | GameObject | Class | Notes |
|---|---|---|---|
| ARCADE (10) | `ArcadeScreenModal` | MWM + `ArcadeScreen` | Opened from the Home hub or gamepad A. Contains the explore grid, the party panel (`ArcadeLobbyList`) and the friends panel (`FriendListPanel`). |
| ARENA (14) | `ArenaScreenModal` | MWM + `ArcadeScreen` | The card grid over the Arena roster, with its own party and friends panels. |
| TOYBOX (13) | `ToyboxScreenModal` | MWM + `ToyboxModal` | The toy grid, with its own party and friends panels. `ToyboxModal.ModalType` is left at 0; it hooks the sibling MWM. |
| MISSION (15) | `MissionScreenModal` | MWM + `ArcadeScreen` | The hub entry is **Unavailable**. It has its own party and friends panels. |
| ARCADE (10)! | `ArcadeGameConfigureModal` | `ArcadeGameConfigureModal` | A **prefab instance** (`_Prefabs/ArcadeGameConfigureModal.prefab`, 29 overrides). It answers **10, not ARCADE_GAME_CONFIGURE (1)**. It is opened by reference from a card pick. |
| MAELSTROM_GAME_CONFIGURE (11) | `MaelstromGameConfigurationModal` | `ArcadeGameConfigureModal` | An **unpacked scene copy** of the prefab. |
| ARENA_GAME_CONFIGURE (17) | `ArenaGameConfigureModal` | `ArcadeGameConfigureModal` | An **unpacked scene copy** of the prefab. |
| TOYBOX_CONFIGURE (16) | `ToyboxGameConfigureModal` | `ToyConfigureModal` | |
| WEEKLY_CHALLENGE_LEADERBOARD (12) | `LeaderboardConfigureModal` | `WeeklyChallengeLeaderboardModal` | **Not** in `ScreenSwitcher.Modals`. |
| SETTINGS (5) | `SettingsModal` | `SettingsModal` | A **prefab instance** (`Panels/SettingsModal.prefab`) carrying **1,659 scene overrides**. Four tabs: General, Display, Performance, Other. |
| CREDITS (18) | `CreditsModal (built)` | `CreditsModal` | **Built in code at runtime** (`ScreenSwitcher.EnsureCreditsModal` → `CreditsModal.Build`). Opened from Settings. |
| PROFILE (3) | `PlayerDataSelectModal` | `ProfileIconSelectView` | Two tabs: Avatar and Display Name. |
| PURCHASE_ITEM_CONFIRMATION (0) | `UNUSEDOBJECTS/PurchaseItemConfirmationModal` | `PurchaseConfirmationModal` | Registered, but `catalogPurchase` = Locked, so nothing reaches it today. |

Also present:

- **Overlays, not modals:**
  - `PartyInviteNotificationPanel`: a prefab variant under `HomeScreen`, always active, hidden by
    alpha.
  - `ToastNotificationContainer`.
  - `SceneTransitionModal`: the door wipe.
  - `InitializingScreen`: inactive.
- **Dormant, and not restyled:**
  - `UNUSEDOBJECTS/{ProtectMissionModal (7), SquadMemberConfigureModal (8), HangarTrainingModal (9)}`.
  - The retired `ProfileModal`.
- **Party and friends are four scene copies each.** There is one `ArcadeLobbyList` and one
  `FriendListPanel` inside each of Arcade, Arena, Toy Box and Mission. The friends panel is a
  `SetActive` sub-panel.

### 2.3 Top nav

As measured before item 1. Section 10 has the nav as built.

```
NavBar [CanvasGroup]  (ScreenSwitcher.NavBar)
├ NavBarGradient, NavBarLine (sprite swapped per index from NavBarLineSprites)
├ NavBarButtons [HorizontalLayoutGroup]
│  └ HangarLink · ArkLink · HomeLink · PortLink · ProfileLink
│     each: EventTrigger PointerClick → ScreenSwitcher.OnClick<X>Nav, children {InactiveImage, ActiveImage}
├ L1 Indicator, R1 Indicator   (sprite hints; the code reads LT/RT)
└ LeftArrrow, RightArrow       (Buttons → OnClickLeftArrow / OnClickRightArrow)
```

- **The tabs are icon-only.** They carry no TMP labels and no `Button` or `NavLink` component.
- **Active tab state:** `UpdateNavBar` toggles `NavActiveImages[i]` and `NavInactiveImages[i]`.
  Both lists are in strip order.

### 2.4 Commerce

| `CommerceAvailability` field | Value |
|---|---|
| `storeScreen` | Unavailable |
| `episodes` | Locked: "Episodes arrive with the full release." |
| `catalogPurchase` | Locked |
| `allowRealMoneyCheckout` | 0 |

### 2.5 What exists to build on

- **`UIThemeSO`** (`_SO_Assets/UI/UITheme.asset`) and **`UITheme`**, the null-safe accessors and
  token enums. The values match §11. **Nothing in Menu_Main reads the theme yet.**
- **Fonts:**
  - Aldrich SDF and **Chakra Petch Regular** SDF, each a static 97-glyph atlas.
  - **No Chakra Petch SemiBold exists anywhere in the project.**
- **Sprites and graphics:**
  - Three cut-corner 9-slice frames (`_Graphics/UI/Frames/frame_cut_*`, generated by
    `Tools/Build/author_ui_frame_sprites.py`). They have inconsistent cut sizes and PPU.
  - **No hexagon sprite.**
  - `TrapezoidGraphic` is the only procedural slant `Graphic`.
- **Missing components:** no theme-applier component, no themed button and no `TabularText`.

---

## 3. Build order

The order follows the brief's suggestion, adjusted to what the inventory found:

| # | Item | Why it sits here |
|---|---|---|
| 0 | **Component kit** | Every later item is assembled from it. It closes T7 (sprite kit) and the parts of T5 and T6 this work needs. Per T7, no shipping prefab is touched at this item. |
| 1 | **Top nav** | Small, and seen on every screen. It proves the kit in the live scene. |
| 2 | **Hangar** | The closest existing screen to B: a vessel list beside a vessel detail. |
| 3 | **Arcade modal + party/friends panel** | The main path into a match. The party panel is built once as a kit prefab and reused by items 8 and 4. |
| 4 | **Game configure** (three copies, plus the weekly leaderboard) | It follows the Arcade, which opens it. §10.11 governs its body. |
| 5 | **Profile** + Profile modal + Episodes entry | |
| 6 | **Home** | It depends on the hub-button and popup pieces settled earlier. |
| 7 | **Ark (Elemental Converter)** | Shipped 2026-10-13 with its own lab design. It is restyled late, so it does not churn under that work. |
| 8 | **Arena, Toy Box, Mission, Toy configure** | These share Arcade's layout and the party prefab. |
| 9 | **Settings + Credits** | The 1,659 overrides must be reconciled before a prefab edit can show. Credits is built in C#, so it needs a code change rather than a prefab edit. |
| 10 | **Closed surfaces** | Port, Store, the Episodes panel and purchase confirmation. See decision D4. |
| 11 | **Toasts** | |

---

## 4. Reusable components (item 0)

All of these follow the house patterns. Sprites are white plus alpha and tinted at runtime.
Colours come from `UIThemeSO` tokens, never literals.

### 4.1 Sprites (T7)

- **Generator:** `Tools/Build/author_ui_kit_sprites.py`. It uses numpy and the
  `author_objective_icons` engine, and supports `--check`.
- **Output folder:** `_Graphics/UI/Kit/`.
- **The set:**
  - **Sliver rects** at §5 sizes:
    - 14 px (panels, cards) and 10 px (buttons, chips, tiles).
    - Each as a fill, a 1 px border and a 2 px border.
    - Each in normal and **flipped** orientation.
    - 9-sliced, so the cut holds its shape at any width.
  - **Hexagon nav tile** (fill, 1 px and 2 px border) and **hexagon slider handle**.
  - **Currency pill.**
  - **End-game banner and end caps.** These are a T7 criterion. They are out of scope for the menu
    but are authored here so T7 closes.

### 4.2 Theme and type (C#, under `Assets/_Scripts/UI/`, namespace `CosmicShore.UI`)

- **`ThemedGraphic`:** tints an `Image` or TMP text from a `UIColorToken` plus alpha. It applies in
  edit mode, so prefabs show their true colours in the editor.
- **`TabularText`:** wraps live numerics in `<mspace>` per face (§4, T5/T6). It takes the face as a
  parameter.
- **Type roles:** a TMP style sheet holding the 11 §4 roles (T6).

### 4.3 Controls

- **`KitButtonStyle`:** the §10.1 variants (Default, Inactive, Cancel, Purchase, CTA, Play), each
  Transparent or Opaque.
  - Drives the border and fill Images and the label colour.
  - Focus ring is CTA.
  - Uses `micro`/`std` motion on unscaled time.
  - Works on a stock `Button`, so `onClick` wiring is untouched.
- **`KitTileStyle`:**
  - Selected: 2 px Light border.
  - Unselected: hairline Neutral (Dark) border and a Very Dark fill.
  - Used by B's left list, the element and pilot tiles, and the §10.11 configure tiles.
- **`HexNavTile`:** the §10.12 side-nav visual. It reuses `NavLink`/`NavGroup` for the behaviour.
- **`TopNavTab`:** a caps label with a 2 px underline, plus LT/RT keycap hints.

### 4.4 Kit prefabs

Under `_Prefabs/UI Elements/Kit/`:

- Button: one base prefab, with every variant a **Prefab Variant** of it.
- The rest: Tile, Panel, Popup (§10.3, with buttons straddling the bottom edge), Hex nav tile, Top
  nav tab, Keyword chip, Section label, Divider and Party panel.

### 4.5 Kit test scene

`_Scenes/Game_TestDesign/UIKitTestScene.unity` shows every sprite at three widths and every
component in every state (the T7 criterion). It is not added to Build Settings.

### 4.6 Authoring approach

Assets are authored offline:

- by Python generators with `--check` (asset-surgery);
- or by an editor tool under `FrogletTools/Interface/`, which records every write in
  `FrogletToolChangeLedger` and draws `FrogletToolShipPanel`.

---

## 5. Mockup vs spec, and where the spec wins

| # | Mockup | Spec | What will be built |
|---|---|---|---|
| S1 | Top nav STORE · ARK · HOME · PORT · HANGAR · PROFILE | — (the real strip is HANGAR · ARK · HOME · PORT · PROFILE, with no STORE slot) | The real order. See D1. |
| S2 | Labels in Chakra Petch caps at **13 px**. Body text at 17/16/14 px, chips at 12 px, pilot tags at 11 px. | §4 has no role below 14 (Button small) or 15 (Body small) | Labels use **Button small 14**. Body uses 18 or 15. Keyword chips use **Body small 15**. |
| S3 | A decorative **gradient scrim** and a **text-shadow** on the centre art | §1.4: glow and gradient are reserved for state | No gradient and no shadow. Text legibility comes from a solid `surfaceBlack` band at partial alpha. |
| S4 | A 2 px Jade (Light) **left border** on the pilot blurb | §5 accent strip is 3 × 44 px; §1.1 says colour is information | The §5 accent strip in Light. |
| S5 | A **6 px** corner cut on keycaps and micro chips | §5 defines 14 and 10 only | 10 px. |
| S6 | Text on danger is `#1A0300` | Not specified; no such token | `surfaceBlack` `#00010A`. |
| S7 | The selected tile fill is `#222645` (Neutral Dark) | §10.11 says "white border and light fill" | `surfaceDark`, as in the mockup, unless design reads "light" as `surfaceLight` `#434C89`. This is queued for design. |
| S8 | Transparent button fills `#001826` (teal tint) and `#0B1A10` (green tint) | §10.1 says "dark teal / green tint" and gives no value | Derived from the role colour at about 12% alpha, as §3 does for domain panels. No new literal. |
| S9 | The active top-nav tab is Jade text with a 2 px Jade underline | The spec has no primary-nav rule. The nearest is §10.8: active label Light with a Jade underline. | The mockup and brief's Jade text, unless overruled. |

## 6. Decisions (approved 2026-10-13: all six recommendations)

| # | Decision | Resolution (approved) |
|---|---|---|
| D1 | The top nav: keep the real five tabs in strip order, or add an Unavailable STORE tab and reorder as the mockup does? Adding or reordering changes navigation. | Keep the real five, in strip order. |
| D2 | Jade (active controls, Play, the transparent button border), Gold (purchase) and Ruby (tooltip) are UI *functions* with **no UIThemeSO token**. Team colours live only in `SO_ColorSet`, and the class doc forbids adding them to the theme. | Add role-named chrome tokens (`controlActive` `00D4FF`, `purchase` `FFAE00`, `tooltip` `A600FF`). Tints derive at alpha. Log the change in the design queue, because §11 is design-owned. |
| D3 | Chakra Petch **SemiBold** is not in the project, and §4 requires it for buttons and labels. | Add the OFL TTF from Google Fonts with a `THIRD_PARTY_REGISTER` row. Author a TMP asset with the 97-glyph charset, and confirm it in the editor at the kit gate. Until then, use Regular. |
| D4 | Port (Locked), Store (no slot), the Episodes panel and purchase confirmation (both Locked) cannot be reached today. STYLE_FOUNDATION and the tracker list Store and Port as "cut from overhaul". The mockup's Port content is the unimplemented Pilots & Artifacts design. | Restyle only what a player sees: the locked tab and button states. Restyle the unreachable bodies only if you want them done blind; they can only be checked by editing `CommerceAvailability`. |
| D5 | Hangar: B shows the list, the vessel and the detail together. Today it is grid ▸ detail with a Back button. | Make it one three-zone view. This changes the in-screen flow, not screen navigation. |
| D6 | Shared-prefab debt: Settings has 1,659 overrides; two configure windows are unpacked copies; party and friends exist as four scene copies each. | Settings: delete the byte-identical overrides and apply the rest before restyling. Copies: restyle them through nested kit prefabs. Converting the copies to Prefab Variants is a separate call. |

## 7. Pre-existing defects found during the inventory (not fixed here)

- **Gamepad Y freestyle toggle is dead.** `ScreenSwitcher.crystalClickHandler` is `{fileID: 0}` in
  the scene. This is filed as a separate task.
- **`MenuCrystalClickHandler.menuCanvasGroups` holds four null slots.** The menu hides in freestyle
  only because `ScreenSwitcher` hides `Screens` and `NavBar` itself. So new panels must stay under
  `Screens`, `NavBar` or `ModalWindows`.
- **The Arcade configure prefab answers ARCADE (10).** ARCADE_GAME_CONFIGURE (1) has no instance.
- **The weekly leaderboard modal is not in `ScreenSwitcher.Modals`.** `CloseAllModals` and the
  return-to-modal restore skip it.
- **The retired `ProfileModal` may block clicks.** Its GameObject is active with
  `blocksRaycasts = 1` at alpha 0. Its position on screen has not been checked.
- **The invite popup's RectTransform has two parents.** It is listed in the `m_Children` of both
  `HomeScreen` and `UI_Refactored`.

Found while mapping the Hangar for item 2 (2026-10-14):

- **The Hangar drops gamepad focus.** Grid to detail and Back re-select nothing, so the selected
  object is switched off under the pad. No Cancel / B / Escape handling exists in the menu UI.
- **A refused vessel purchase says nothing.** `ProgressionBackendGate.CloudEnabled` is false, so
  `VesselUnlockSystem.TryPurchaseVessel` refuses every paid unlock and the spend-crystals panel just
  stays open. `IsVesselHangarUnlocked() == false` only writes a debug log.
- **Three detail-view fields are unassigned in the scene:** `vesselPreviewImage` (so the authored
  `SO_Vessel.PreviewImage` never shows), `vibeButton` and `unlockButtonText` (so the price never
  shows; the caption is baked into the button sprite).
- **Authored text the 97-glyph fonts cannot draw:** a curly apostrophe in the Squirrel description
  and in Sparrow's Skyburst Rockets, an em dash in the Butterfly description.
- **Roster data gaps:** Urchin's four abilities reuse the Serpent's text; Stoat's icon is
  `Squirrel.png`; Butterfly, Thresher, Scarab and Stoat have no `PreviewImage` and no abilities;
  every ability's icon is the generic `Ability_Button.png`.

---

## 8. The component kit as built (item 0)

Generated, not hand-authored: re-run the generator rather than editing its output.

| Piece | Where | Generator |
|---|---|---|
| Role colour tokens | `UIThemeSO.controlActive` / `purchase` / `tooltip`, `UIColorToken` 10-12 | — |
| Sprites (19) | `Assets/_Graphics/UI/Kit/kit_*` | `Tools/Build/author_ui_kit_sprites.py` |
| Lock and currency glyphs | `Assets/_Graphics/UI/Chrome/chrome_lock`, `chrome_crystal` | `Tools/Build/author_ui_chrome_icons.py` |
| Button / label face | `Assets/_Graphics/Fonts/ChakraPetch/ChakraPetch-SemiBold SDF.asset` (dynamic) | `Tools/Build/author_chakra_petch_semibold.py` |
| Components | `Assets/_Scripts/UI/Kit/` | — |
| Prefabs, type roles, test scene | `Assets/_Prefabs/UI Elements/Kit/`, `Assets/_SO_Assets/UI/UITypeRoles.asset`, `Assets/_Scenes/Game_TestDesign/UIKitTestScene.unity` | `Tools/Build/author_ui_kit_prefabs.py` |

How the later items use it:

- **A button** is a `KitButton` instance. Set `Variant` and `Fill` on the instance; that is a
  genuine per-instance value, not a fork. `KitButtonSmall` is the 14-point Prefab Variant.
  `onClick` wiring is untouched: `KitButton` is a `Button`.
- **An existing scene Button can become a kit button** by swapping its script reference, keeping its
  serialized fields. `KitButton` derives from `Button`.
- **A list tile, class card or configure tile** is a `KitTile`. The owning screen sets `IsSelected`
  and `IsLocked`. Gamepad focus is the separate CTA ring, so focus can move without moving the
  selection.
- **A hex section tab** is a `KitHexNavTile`, which is a `KitTile` on hexagon sprites.
- **A top-nav tab** is a `KitTopNavTab`. Its `InactiveImage` and `ActiveImage` children are what
  `ScreenSwitcher.UpdateNavBar` already toggles, so the nav restyle needs no navigation code. Its
  root is the hit area and carries a `KitHoverTint`; its `Lock` child is the locked look, shown by a
  host's `MenuAvailabilityView` (the host insets `InactiveImage` by 23 so the word clears it).
- **A hover lift on something that is not a `KitButton`** (an EventTrigger tab, a keycap on a plain
  Button) is a `KitHoverTint` listing the `KitText` / `KitThemedGraphic` targets to lift.
- **Every label** carries `KitText` (§4 role + §11 token). Live numbers go through
  `KitText.SetText` so they get per-face `<mspace>`.
- **Static chrome** carries `KitThemedGraphic`.

Not built in item 0, because the items that need them will settle their shape:

- the party panel prefab (item 3);
- the two-label toggle and the slider prefab (item 9; the hex handle sprite exists).

## 9. Retirement list: what the kit supersedes

Each line is retired when the item that restyles its last consumer lands. Consumer counts are from
the Gate 1 measurement of `Menu_Main.unity`.

| Old sprite / style | Consumers | Replaced by | Retired at item |
|---|---|---|---|
| `_Graphics/UI/Frames/frame_cut_filled_200` / `_300` | ArcadeLobbyList, FriendListPanel, UsernameInputField (4 + 4 refs) | `kit_sliver14_fill` + `line1` | 3 |
| `_Graphics/UI/Frames/frame_cut_outline_200` | settings tab buttons (2 prefabs) | `KitHexNavTile` | 9 |
| `ArcadeScreen/Group 1585.png` (card PLATE), `Rectangle 1127 (2).png` (card RIM) | 40 + 77 refs | `kit_sliver14_*`, `KitTile` | 3, 8 |
| `HomeScreen/Play Button.png` (hub button plate) | 8 refs | `KitButton` Play/Opaque | 6 |
| `{LEGACY}/Cut Frame Corner.png`, `Cut Frame Corner Glow.png` | 10 + 10 refs | `kit_sliver*` | as found |
| NavBar art under `Design Assests/Menu_Main/R_Menu_Main/HomeScreen/`: `gradient.png`, `nav line.png` + `nav line (1)`-`(4)`, `Component 1/2.png` (L1/R1), `Polygon 165/166.png` (arrows), and the ten tab icons (`hangar`, `hangar (1)`, `Ark`, `arcade (2)`, `home`, `home (2)`, `Port`, `port (2)`, `Profile`, `store (1)`) | NavBar | `KitTopNavTab`, `KitKeycap`, band + rule | **1: done.** Menu_Main references none of the 20. Their only remaining consumers are the dead copies in `_Prefabs/MIgration_Prefabs (DELETE LATER)/` (`NavBar`, `Screens`, `ModalWindows`); delete the sprites with those prefabs. |
| Button `ColorTint` transitions with literal colours | per screen | `KitButton` states from tokens | per item |


## 10. Item 1: top nav (approved in editor 2026-10-14)

Boards on the Menu Redesign B canvas: `Main.dc.html` (interactive: click a tab, click LT / RT, click
PORT), `TopNavHover.dc.html` (HANGAR active, pointer over ARK) and `TopNavLocked.dc.html` (PORT
pressed and refused). Mockup approved 2026-10-14, with both open points settled as recommended:
`UpdateNavBar` keeps its `SetActive` toggle, and the tabs do not become `Selectable`s.

Generated by `Tools/Build/author_menu_topnav.py` (`--check`, `--self-test`); re-run it rather than
editing the scene's nav by hand. Its migration (deleting the old subtree) stands down once that
subtree is gone; the wiring checks run on every invocation, including `--check` against the scene
as committed.

```
Safe Area/NavBar  [CanvasGroup]            84 tall (was 180.35), anchors unchanged
|- Band           Image + KitThemedGraphic SurfaceVeryDark       (replaces NavBarGradient)
|- Rule           Image + KitThemedGraphic SurfaceDark, 1 px     (replaces NavBarLine)
'- NavBarButtons  HorizontalLayoutGroup, middle centre, spacing 56, 744 wide
   |- LeftArrrow  Button -> OnClickLeftArrow (unchanged), transparent hit Image, KitHoverTint
   |  '- Keycap   KitKeycap "LT"
   |- HangarLink, ArkLink, HomeLink, PortLink, ProfileLink
   |              KitTopNavTab instances; EventTrigger PointerClick -> OnClick<X>Nav added on each root
   '- RightArrow  as LeftArrrow, "RT"
```

| Element | As built |
|---|---|
| Band | `NavBar` is 84 tall at the top of the safe area. The band is opaque `SurfaceVeryDark`, where the old gradient was about 90 % opaque over its top half, so it covers nothing that was readable before. It does not raycast (nor did the gradient). |
| Tabs | `KitTopNavTab` instances, named as the old links. Width = the word, so the 56 gap is between words as on the board. `ScreenSwitcher.NavActiveImages` / `NavInactiveImages` point at each instance's `ActiveImage` / `InactiveImage`, so `UpdateNavBar` and `ResolveNavLinkObject` (which answers the image's parent, now the instance root) are unchanged. The saved state is HOME active, the screen the menu opens on. |
| Hover | `KitHoverTint` (new, `Assets/_Scripts/UI/Kit/`): pointer-only lift to `TextLight` on the inactive word, restoring each target's own token on exit or disable. The kit tab carries it with a transparent hit Image on its root, so the whole tab answers the pointer. |
| Locked PORT | An authored `MenuAvailabilityView` on the PORT instance (the one `MarkDisabledNavLinks` would otherwise add at runtime): `lockedOverlay` = the tab's `Lock` glyph (now part of `KitTopNavTab`, off by default), `label` = its inactive word, `unavailableLabelColor` = `InactiveLight`. Availability is authored **Available**: `disabledScreens` locks it at runtime, so emptying that list opens the tab with nothing else to change. In edit mode PORT therefore reads unlocked; the padlock and dim appear in Play mode. |
| Refusal | Unchanged: `MenuAvailabilityView.TryPress` plays the Denied sting and shows "Not open yet." through `ToastNotificationAPI`. The toast keeps today's look until item 11. |
| LT / RT | The same Buttons and `onClick` wiring, moved into the row as its first and last children. Their Image is now a transparent hit area and their transition is None; the `KitKeycap` child is the art, and `KitHoverTint` lifts its border and label to `TextLight` on hover. `L1 Indicator` / `R1 Indicator` are deleted. |
| Per-tab line art | `ScreenSwitcher.NavBarLine` and `NavBarLineSprites` are emptied on purpose: the code already skips both when unset, and nothing in Menu_Main now references the retired sprites (section 9). |

Deviations from the board, all small:
- **Tab tracking** is the §4 Button role (0.08 em), not the board's 0.12 em. The spec wins.
- **Locked PORT has no hover lift.** Its word's colour belongs to `MenuAvailabilityView`, so the
  instance's `KitHoverTint` is disabled rather than fighting it.
- **No focus ring on the tabs**, as agreed: they are not `Selectable`s. Gamepad moves through LT / RT,
  unchanged.
- **The lock and the dim use `SetActive` / literal colours inside `MenuAvailabilityView`**, existing
  code left alone like `UpdateNavBar`. The literal is set to the `InactiveLight` value; the PORT
  word's own colour is the kit default (`NeutralLightest`), driven externally on that one instance.

Not verified (no Unity editor in a cloud session): the in-editor look, hover, pointer hit areas,
TMP's real word widths against the generator's PIL estimate, and the FrogletTools > Game Modes >
Game Mode Prefab Kit Validate run. Steps are in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## 11. Item 2: Hangar (mockup, awaiting approval)

Boards on the Menu Redesign B canvas: `Hangar.dc.html` (interactive: pick a vessel, unlock, confirm
or cancel; keyboard focus moves the selection), and `HangarLocked` (locked Manta, gamepad focus on
its tile), `HangarSpend` (spend crystals, enough), `HangarShort` (not enough), `HangarNoArt`
(Scarab: no preview art, no abilities), which mount the same board in those starting states.
`HangarNotes` lists every change. Content is the real roster: `SO_Classlist_Classes`, the authored
names, descriptions, ability text, prices, icons and hangar previews, in the shipping state (only
the Squirrel owned, owned first).

| Zone | Mockup | Built from (at the build gate) |
|---|---|---|
| Header | "Hangar" label, "Select a vessel" (H1), crystal balance pill on the right | `PlayerDataService.CrystalBalance`, already read by the detail view |
| Vessel list (left, 340) | 11 `KitTile` rows: icon, name, "Owned" or "Locked / price". Locked rows dim the icon to 35 % with the padlock; the selected row has the 2 px light border | Replaces the 4-column `VesselGridPrefab` cards; still built from `ShipList` in `HangarScreen.PopulateGrid`, owned first |
| Selected vessel (centre, 900) | `PreviewImage` hero (16:9) with an Owned / Locked chip and the name at Display size; "About" + description; bottom-left the unlock button: Purchase "Unlock - 4000", or the inactive "Unlocked" when owned | `HangarVesselDetailView`'s name, description and unlock button, plus the preview field it already has. A vessel with no preview shows its icon |
| Abilities (right, 552) | All of the vessel's abilities with their descriptions; "No abilities are listed for the X yet." when there are none | Replaces the General / Ability tabs |
| Spend crystals | The kit popup: "Spend", "4000 to unlock Manta", the balance; Confirm (Purchase, opaque) left of Cancel, both on the bottom edge | `UnlockPanel` / `SpendCrystalsPanel`, same open / close / confirm calls |

Retired with this item: the grid-to-detail hop and its Back button, the Vibe tile (unwired), the
static Overview / Elements / Progress images, and the "Vessel Names" eye toggle (names are always on
the tiles).

Open points for the build gate:
- **Selection follows focus in the list,** so a gamepad previews each vessel as it moves and focus
  never lands on a hidden panel. This changes the in-screen flow (D5), not screen navigation.
- **Not enough crystals:** the board shows Confirm disabled plus "You need N more crystals." Today
  Confirm is hidden. The line is a new string in `HangarVesselDetailView`.
- **Stoat costs 0:** the board reads "Unlock - Free" rather than the code's "UNLOCK - 0".
- **The interactive board's Confirm unlocks the vessel.** In the build the progression gate still
  refuses paid unlocks silently (section 7); this item does not change that.

