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
- ~~The in-game HUD, GameCanvas, end-of-game screens and vessel HUDs, including Menu_Main's
  freestyle `Game UI`.~~ **Brought in scope 2026-10-14 as item 12** (the user's direction). Heavy
  panels over the arena still "eat the game" (STYLE_FOUNDATION §1), and the vessel HUD's four-icon
  ability row stays a LOCKED structure (`Docs/claude/VESSEL_HUD_AND_ELEMENTS.md`): item 12 restyles
  the chrome around it, never its structure.
- Gameplay, networking, and how screens are navigated. This work changes how screens look and how
  they are laid out, nothing else.

---

## 1. Status

Legend: `Planned` ▸ `Mockup` ▸ `Mockup approved` ▸ `Built` ▸ `Approved in editor`.

| # | Item | Status | Mockup board | Commits |
|---|---|---|---|---|
| 0 | Component kit: T7 sprites, Chakra Petch SemiBold (T5), type roles + `TabularText` (T6), theme components, kit prefabs, kit test scene | Built | `MenuKit.dc.html` | `3b36c361`, `fe108889`, `b0918296`, `908fc2d7`, `4cba49ee`, `e932eaf2` + docs |
| 1 | Top nav (`Safe Area/NavBar`) | Approved in editor (2026-10-14); **revision approved 2026-10-14**: translucent band, see §13.1 | `Main.dc.html` (interactive), `TopNavHover.dc.html`, `TopNavLocked.dc.html` on the Menu Redesign B canvas | `8d6d2acb`, `99ef3ccd` + docs |
| 2 | Hangar screen: grid, detail and the spend-crystals panel | Approved in editor (2026-10-14) | `Hangar.dc.html` (interactive), `HangarLocked`, `HangarSpend`, `HangarShort`, `HangarNoArt`, `HangarNotes` on the Menu Redesign B canvas | `661a7f1f`, `7987b896`, `96c81faa` + docs |
| 3 | Arcade modal (`ArcadeScreenModal`): explore grid plus the party/friends panel | Built | `Arcade.dc.html` (interactive), `ArcadeSolo`, `ArcadeNoFriends`, `ArcadePicks`, `ArcadeNotes` on the Menu Redesign B canvas | `f9b59fd4`, `75cd38e7`, `5cc56adb` + docs |
| 4 | Game configure: `ArcadeGameConfigureModal`, plus the Maelstrom and Arena configure windows and the weekly leaderboard | Built (2026-10-15, section 14) | `Configure.dc.html` (interactive), `ConfigureArena`, `ConfigureMaelstrom`, `ConfigureWeekly`, `ConfigureWeeklySpent`, `ConfigureGuest`, `ConfigureLocked`, `WeeklyLeaderboard` (interactive), `LeaderboardEmpty`, `ConfigureNotes` | |
| 5 | Profile screen, Profile modal (avatar / display name), Episodes entry | Built (2026-10-15, section 15): Profile screen and profile window | `Profile.dc.html` (interactive), `ProfileFresh`, `ProfileModal` (interactive), `ProfileName`, `ProfileNameError`, `ProfileNameTaken`, `ProfileNotes` | |
| 6 | Home screen: hub row, header, settings and freestyle buttons, invite popup | Built (2026-10-15, section 21): hub row, pilot chip, question bar, settings, freestyle hint, Up next cards, party invite | `Home.dc.html` (interactive), `HomeInvite`, `HomeFocus`, `HomeOffline`, `HomeNotes` | |
| 7 | Ark screen (Elemental Converter) | Built (2026-10-15, section 16) | `Ark.dc.html` (interactive), `ArkConverting`, `ArkDone`, `ArkNotes` | |
| 8 | Arena, Toy Box and Mission modals, and Toy configure | Built (2026-10-15, section 17): Arena window, Toy Box, Toy configure and the Mission window | `ToyBox.dc.html` (interactive), `ToyBoxClaimed`, `ArenaWindow`, `MissionWindow`, `ToyConfigure` (interactive), `ToyConfigurePaint`, `ToyConfigureSpawn`, `ToyConfigureDomain`, `ModesNotes` | |
| 9 | Settings modal and Credits | Built (2026-10-15, section 18): Settings window and Credits | `Settings.dc.html` (interactive), `SettingsDisplay`, `SettingsPerformance`, `SettingsOther`, `Credits`, `SystemNotes` | |
| 10 | Closed surfaces: Port (locked), Store (no slot), Episodes panel, purchase confirmation | Built (2026-10-15, section 19): purchase confirmation drawn blind; locked states from items 1 and 5 | `ClosedSurfaces.dc.html`, `PurchaseConfirm` (blind), `SystemNotes` | |
| 11 | Menu toasts | Built (2026-10-15, section 20) | `Toasts.dc.html` (interactive), `SystemNotes` | |
| 12 | In-game UI: GameCanvas HUD chrome, scoreboard, ready / countdown / pause, in-match toasts, end-of-game screens, Menu_Main freestyle HUD | Built (2026-10-15, section 22): 12a HUD chrome, 12b Ready / waiting / countdown, 12c pause and connecting, 12d end reveal and scoreboard, 12e spectator, match feed, freestyle | `HudMatch.dc.html` (interactive), `HudCardStates`, `HudNotes`, `Hud<Vessel>` x9, `HudReady` (interactive), `HudPause`, `HudConnecting`, `HudEndReveal`, `HudScoreboard` (interactive), `HudSpectator` - page "In-game HUD" | `6a94bdd1`, `422183de`, `9abadaa9`, `f067dc57` + 12e |
| 13 | Start flow: Bootstrap splash, connection-lost retry, sign-in, pilot name, the first-run privacy overlay (age gate, consent); the boot-world loading cover | Built (2026-10-15, section 26), awaiting in-editor approval; the loading cover is Built (section 25) | `StartSplash`, `StartRetry`, `StartSignIn`, `StartSignInFailed`, `StartName`, `StartNameTaken`, `StartAgeGate`, `StartConsent`, `StartLoading`, `StartNotes` - page "Start flow" | `dc023d00d` (cover) |
| 14 | Maelstrom game UI: the hub between rounds, the stats screen, the tournament summary | Built (2026-10-15, section 27), awaiting in-editor approval | `MaelstromHub`, `MaelstromStats`, `MaelstromSummary`, `MaelstromNotes` - page "In-game HUD" | |

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

**Revised 2026-10-14 (the user's direction, after item 3 was built):**
- **Mockups first, then the builds, then one editor pass.** Every remaining item's mockup goes on the
  canvas before any of them is built; the builds follow in order; the in-editor testing of items 0-3
  and of everything after happens once, at the very end. Until then a built item stays at `Built`.
- **Home goes last.** The sequence is now 4 (game configure), 5 (Profile), 7 (Ark), 8 (Arena, Toy Box,
  Mission), 9 (Settings, Credits), 10 (closed surfaces), 11 (toasts), then 6 (Home). The item numbers
  stay as identifiers.

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

| D7 | (Raised by you, 2026-10-14.) The Arcade and Toy Box friends UI is two panels: the party panel's invite slots and a separate friends overlay with its own list. | Unify them: one party panel holding the party slots, incoming invites and friend requests, and the friends-online list with Invite / Join on each row. Built once and shared by Arcade, Arena, Toy Box and Mission (item 3). |

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
- **Arcade (found mapping item 3):**
  - **The arcade grid is unreachable by gamepad past row 3.** `ArcadeDPadNav` returns while any
    modal is open, and the Arcade is a modal; its `ScrollToButton` is commented out, so the scroll
    never follows focus either. **Fixed at item 3** (`KitScrollIntoView`), and `ArcadeDPadNav` is
    retired from all four windows (section 12).
  - **B with the friends overlay open closes the whole modal and leaves the overlay open** for the
    next visit. **Gone at item 3:** the lists live in the party panel, there is no overlay.
  - **The two close paths differ:** Back deactivates the object and jumps HOME; B animates out.
    **Fixed at item 3** (`ModalWindowCloseButton`).
  - **Dead controls:** the friends overlay's ONLINE / REQUESTS header buttons and refresh icons have
    no listeners; both `ControllerButtonPress` hooks are empty; the weekly card's LEADERBOARD button
    is authored inactive; `ArcadeScreen`'s Loadout view and toggles are unwired. **The first three
    left the Arcade with the objects that carried them at item 3**; `ArcadeScreen`'s fields remain.
  - **Cards look their titles up in `OrganicRematchGames`,** not the Arcade roster, so a mode added
    only to `ArcadeGames` keeps its placeholder title.
  - **Content:** Joust's description is "Joust joust joust"; the Grizzly's `IconActive` is a legacy
    "BUFO selected" button graphic.
- **Roster data gaps:** Urchin's four abilities reuse the Serpent's text; Stoat's icon is
  `Squirrel.png`; Butterfly, Thresher, Scarab and Stoat have no `PreviewImage` and no abilities;
  every ability's icon is the generic `Ability_Button.png`.

---

## 8. The component kit as built (item 0)

Generated, not hand-authored: re-run the generator rather than editing its output.

| Piece | Where | Generator |
|---|---|---|
| Role colour tokens | `UIThemeSO.controlActive` / `purchase` / `tooltip`, `UIColorToken` 10-12 | — |
| Sprites (22; `kit_fade_v` added at item 2, `kit_fade_h` and `kit_oct_fill` at item 3) | `Assets/_Graphics/UI/Kit/kit_*` | `Tools/Build/author_ui_kit_sprites.py` |
| Lock and currency glyphs; plus and favourite star (outline, filled) at item 3 | `Assets/_Graphics/UI/Chrome/chrome_lock`, `chrome_crystal`, `chrome_plus`, `chrome_star`, `chrome_star_fill` | `Tools/Build/author_ui_chrome_icons.py` |
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
- **A picture that must fill a frame edge to edge** (a hero preview) is an Image under a `KitCoverImage` + `Mask`: it covers at the sprite's aspect, where uGUI's `preserveAspect` only contains. It refits whenever the sprite changes, however it was set.
- **Showing and hiding a part** goes through `KitVisibility.SetShown(CanvasGroup, bool)`: alpha,
  interactable and raycasts together, and a `LayoutElement` on the same object leaves the layout while
  hidden. Never `SetActive`.
- **A control inside a scroll view** carries `KitScrollIntoView`, so pad / keyboard focus scrolls it
  into view (point `target` at the card when the control is a button inside one).
- **A list whose rows other code spawns** carries `KitListCounter` on its content: it keeps a count
  label, the section's header / divider and the empty-state line in step with the rows.
- **A panel whose rows are rebuilt while focused** carries `KitFocusKeeper` on its root.
- **A window's own Back button** carries `ModalWindowCloseButton`: it closes the enclosing
  `ModalWindowManager` exactly as gamepad B does.
- **A card the player picks** is a `KitTile` with `selectedBorderToken` (Control Active for the
  arcade's local pick) and an optional `lockLabel` beside the lock glyph.
- **The party panel** is `Main Menu Screens/Party/PartyPanel.prefab` (item 3): drop an instance into
  a mode window; it needs nothing wired from the scene. Arena, Toy Box and Mission move onto it at
  item 8.

Not built in item 0, because the items that need them will settle their shape:

- the party panel prefab (built at item 3, section 12);
- the two-label toggle and the slider prefab (item 9; the hex handle sprite exists).

## 9. Retirement list: what the kit supersedes

Each line is retired when the item that restyles its last consumer lands. Consumer counts are from
the Gate 1 measurement of `Menu_Main.unity`.

| Old sprite / style | Consumers | Replaced by | Retired at item |
|---|---|---|---|
| `_Graphics/UI/Frames/frame_cut_filled_200` / `_300` | ArcadeLobbyList, FriendListPanel, UsernameInputField (4 + 4 refs) | `kit_sliver14_fill` + `line1` | **3: the Arcade's copies are gone.** The Arena, Toy Box and Mission copies go at 8; UsernameInputField at 5. |
| `_Graphics/UI/Frames/frame_cut_outline_200` | settings tab buttons (2 prefabs) | `KitHexNavTile` | 9 |
| `ArcadeScreen/Group 1585.png` (card PLATE), `Rectangle 1127 (2).png` (card RIM) | 40 + 77 refs | `kit_sliver14_*`, `KitTile` | **3: the Arcade's cards are gone** (12 authored cards, the weekly and Maelstrom cards); the rest at 8. |
| Party rows `_Prefabs/UI Elements/Panels/Party/New Prefabs/OnlineFriendsInfo Variant.prefab`, `RequestsInfo.prefab` | the four `FriendsListPanel` copies | `PartyOnlineRow`, `PartyRequestRow` | **3: the Arcade no longer uses them**; the other three copies at 8. |
| `_Graphics/ARCADE/Favorite_Star_Active.png` / `Favorite_Star_Inactive.png` | every `GameCard` | `chrome_star_fill` / `chrome_star` | **3: the Arcade's cards no longer use them**; the Arena's cards at 8. |
| `Design Assests/Menu_Main/Gradient Background 3.png` on `Arcade_Panel` | the Arcade backdrop | `ArcadeView` NavDim + Backdrop | **3: the Image is switched off**, not deleted (its component still names the sprite); remove it with the other windows' copies at 8. |
| The legacy draw paths in `FriendInfoSlot`, `OnlineInfoEntry`, `RequestInfoEntry`, `GameCard` (the `SetActive` / literal-colour hook bodies) | the three non-Arcade copies | the `Kit*` subclasses | 8, when the last legacy copy moves onto the kit prefabs. |
| `HomeScreen/Play Button.png` (hub button plate) | 8 refs | `KitButton` Play/Opaque | 6 |
| `{LEGACY}/Cut Frame Corner.png`, `Cut Frame Corner Glow.png` | 10 + 10 refs | `kit_sliver*` | as found |
| NavBar art under `Design Assests/Menu_Main/R_Menu_Main/HomeScreen/`: `gradient.png`, `nav line.png` + `nav line (1)`-`(4)`, `Component 1/2.png` (L1/R1), `Polygon 165/166.png` (arrows), and the ten tab icons (`hangar`, `hangar (1)`, `Ark`, `arcade (2)`, `home`, `home (2)`, `Port`, `port (2)`, `Profile`, `store (1)`) | NavBar | `KitTopNavTab`, `KitKeycap`, band + rule | **1: done.** Menu_Main references none of the 20. Their only remaining consumers are the dead copies in `_Prefabs/MIgration_Prefabs (DELETE LATER)/` (`NavBar`, `Screens`, `ModalWindows`); delete the sprites with those prefabs. |
| Hangar art under `_Graphics/Hangar/`: `CurrencyBar.png`, and in `Hanger_New/`: `Eye.png`, `Frame 1085.png`, `Frame 1109 (1).png`, `Frame 1110 (4).png`, `Frame 7.png`, `Group 2048.png`, `Group 2049.png`, `Group 2097.png` (the baked "UNLOCK VESSEL" button), `Rectangle 1283 (1).png`, `Rectangle 995.png`, `popup (1).png`; plus `_Graphics/Port/Button_Flat_Gray_Flipped.png` | Hangar GridPanel / DetailPanel | `HangarView` (kit slivers, `KitButton`, `KitCurrencyPill`) | **2: done.** Menu_Main references none of the 13; only `_Prefabs/MIgration_Prefabs (DELETE LATER)/Screens.prefab` does. Delete with it. (`Back Button.png`, `Button_Flat_Orange.png`, `OmniCrystal_Active.png` and two Arcade images were also on the old panels but are still used elsewhere in Menu_Main.) |
| `HangarVesselGridCard.cs` + `VesselGridPrefab.prefab` | Hangar grid | `HangarVesselTile` (a `KitTile` variant) | **2: unused by Menu_Main.** Kept only because `MIgration_Prefabs (DELETE LATER)/Screens.prefab` still nests the prefab; delete all three together. `HangarScreen.SelectVesselForDetail` is still the card's entry point. |
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

## 11. Item 2: Hangar (approved in editor 2026-10-14)

Boards on the Menu Redesign B canvas: `Hangar.dc.html` (interactive) and `HangarLocked`,
`HangarSpend`, `HangarShort`, `HangarNoArt`, which mount the same board in those starting states.
`HangarNotes` lists every change. Mockup approved 2026-10-14 with all four open points as
recommended: selection follows focus; short of crystals, Confirm is disabled and the shortfall is
stated; a zero price reads "UNLOCK - FREE"; the purchase gate is left as it is.

Generated by `Tools/Build/author_menu_hangar.py` (`--check`, `--self-test`); re-run it rather than
editing the prefabs or the scene's Hangar by hand. Its scene migration stands down once the old
panels are gone; the wiring checks run on every invocation.

```
Screens/Hangar Screen  [FlipUI, HangarScreen]
|- Hangar_Panel              legacy, inactive, untouched
'- HangarView                (prefab instance)  HangarVesselDetailView on its root
   |- Header                 "HANGAR" / "Select a vessel" (H1); "CRYSTALS" + KitCurrencyPill
   |- Body   [HorizontalLayoutGroup, spacing 19]
   |  |- VesselList          350 fixed: "VESSELS" + owned count; ScrollRect > Viewport [RectMask2D] > Content [VLG]
   |  |                      tiles: HangarVesselTile (KitTile variant) built at runtime, owned first
   |  |- SelectedVessel      flexible 62: Hero (Clip [Mask, KitCoverImage] > Preview, FallbackIcon, Fade;
   |  |                      Border; StatusChip; Name), About, Description, Unlock (Locked: KitButton
   |  |                      Purchase / CTA; Owned: KitButton Inactive "UNLOCKED")
   |  '- Abilities           flexible 38: panel; "ABILITIES" + count; List [VLG] of HangarAbilityRow; NoAbilities
   '- SpendCrystals [CanvasGroup]   Overlay (SurfaceBlack 0.886); Dialog [VLG + CSF]: "SPEND", title,
                             Balance (KitCurrencyPill), Shortfall; Buttons on the bottom edge: Confirm, Cancel
```

| Behaviour | As built |
|---|---|
| Widths | The list is fixed at 350; the centre and the ability column share the rest 62 : 38. At 1920 that is the board (404-1307 and 1332-1880); at 1728 (16:10, Steam Deck) 784 / 476; at 1440 (4:3) 606 / 366. The preview always covers its frame. |
| Selection | `HangarVesselTile` implements `ISelectHandler`: a click or arriving focus selects the vessel. `ScreenSwitcher.Refocus` lands on the first tile (the owned Squirrel in the shipping state). Old tiles are deactivated before `Destroy` so focus never lands on one that is about to vanish. |
| After a purchase | `OnUnlockStateChanged` rebuilds the list (owned first) and keeps the selection; focus returns to the selected vessel's tile. |
| Popup | Shown and hidden by one CanvasGroup. Focus moves in (Confirm, or Cancel when short) and explicit navigation keeps it between the two buttons. Closing returns focus to the unlock button, or to the tile once the vessel is owned. B / Esc still do nothing: the menu has no Cancel handling (section 7). |
| Visibility | In the new code every show / hide is a CanvasGroup (or a LayoutElement leaving the layout), never `SetActive`; the one `SetActive(false)` is on rows and tiles being destroyed. The legacy path below it is unchanged. |
| Colours | Every graphic is a `KitThemedGraphic`, `KitText`, `KitButton` or `KitTile` token; the only literals are white art and the locked fallback icon's 0.5 alpha (a serialized field). |

Deviations from the board:
- **The crystal glyph leads the Confirm label** ("CONFIRM - 4000" after the glyph), as on the
  unlock button, because `KitButton` places its icon before its label.
- **The spend popup is composed from kit parts, not a `KitPopup` instance.** `KitPopup` has a fixed
  eyebrow / title / body with no slot for the balance row. When a second popup needs a custom body,
  give `KitPopup` a content slot and move this one onto it.
- **Ability text keeps the authored characters** the fonts lack (section 7); they will show as
  missing glyphs until the content is fixed.

Not verified (no Unity editor in a cloud session): the editor look and layout, real TMP widths, the
gamepad flow, the purchase path, and the Game Mode Prefab Kit Validate run. Prisma could not
stand in: its live compile fails on existing engine gaps (BlackHole RenderGraph types,
`MouseOrbitCamera`'s `ButtonControl`) and on the kit's `DoStateTransition(state, instant)` override,
which Prisma's `Selectable` lacks; that engine gap is filed as a separate task. Steps are in
`Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## 12. Item 3: Arcade and the shared party panel (built, awaiting in-editor approval)

Boards on the Menu Redesign B canvas: `Arcade.dc.html` (interactive), `ArcadeSolo`,
`ArcadeNoFriends`, `ArcadePicks` and `ArcadeNotes`. Mockup approved 2026-10-14 with all three
recommendations: cards are Selectables that scroll into view; Back and B close the same way, with
the out animation; the new strings ship. D7 (one party panel for seats, invites and the online
list, built once for Arcade, Arena, Toy Box and Mission) is part of the same item.

Generated by `Tools/Build/author_menu_arcade.py` (`--check`, `--self-test`); re-run it rather than
editing the prefabs or the scene's Arcade by hand. Its scene migration stands down once the old
objects are gone; the wiring checks run on every invocation, including `--check` against the
committed scene.

```
ModalWindows/ArcadeScreenModal   [FlipUI, CanvasGroup, ArcadeScreen, ModalWindowManager, Animator, OfflineUIGate]
|                                now stretches the whole safe area (it was inset 171 for the old nav)
'- Arcade_Panel                  its old gradient Image is switched off; OfflineUIGate -> PartyPanel
   '- Explore                    ArcadeExploreView KEPT (four scene objects name the view)
      '- ArcadeView              (prefab instance)
         |- NavDim               SurfaceBlack 0.75 over the 84-unit nav band
         |- Backdrop             SurfaceBlack below it
         |- Body   [HorizontalLayoutGroup, spacing 24, REVERSED: the panel draws on the left]
         |  |- ExploreScroll     ScrollRect > Viewport [RectMask2D] > Content [VLG + CSF]
         |  |  |- Features       WeeklyChallenge (KitTile + WeeklyChallengeCard) | Maelstrom (KitTile)
         |  |  |- AllGames       "ALL GAMES" + count
         |  |  '- GameGrid       7 x GameListRow [HLG] x 4 ArcadeGameCard (28 slots for 28 games)
         |  '- PartyPanel        (nested prefab, 420 fixed) - see below
         '- Header               "ARCADE" / "Explore games" (H1); Back: KitButton + B keycap + ModalWindowCloseButton

         Sibling order is focus order: ScreenSwitcher.Refocus lands on the first live Selectable, so the
         weekly card (then the Maelstrom card) comes first and Back, which would close the window on the
         first A press, comes last.

PartyPanel  [ArcadeLobbyList, FriendsListPanel (embedded), KitFocusKeeper]
|- Frame                         sliver 14, SurfaceVeryDark / SurfaceDark
'- Inner [VLG, spacing 14]
   |- Top                        "PARTY" + "n / 4"; diamond + "N Players Online"
   |- Slots                      4 x PartySlot (KitPartySlot): Occupied layer | OpenSlot tile
   |- Leave [CanvasGroup]        KitButtonSmall Cancel "LEAVE PARTY", out of the layout while solo
   |- Divider
   '- Lists                      ScrollRect: INVITES header, Requests [KitListCounter], divider,
                                 ONLINE header, OnlinePlayers [KitListCounter], the empty line
```

| Zone | As built |
|---|---|
| Window | The modal root now stretches the whole safe area and the view draws its own 0.75 dim over the nav band, as on the board. The `Standard` animator still drives the root CanvasGroup; nothing new sits at the `Content` path it animates. |
| Header | Back is a `KitButton` (Default, transparent) with a `KitKeycap` "B". `ModalWindowCloseButton` calls the window's `ModalWindowOut`, so Back and gamepad B play the same out animation and close sound and unwind the same stack entry. The old Back's `SetActive(false)` + `OnClickHomeNav` pair is gone; the arcade only opens from HOME and the screen cannot change under a modal, so nothing is lost. |
| Party seats | `KitPartySlot` (a `FriendInfoSlot`): the framed row shows the avatar inside an octagon ring painted the pilot's domain colour (`SO_ColorSet`, pushed live by `ArcadeLobbyList` as before; Surface Dark while unknown), the name, and the role line "You / Host", "You", "Host" or "Pilot". The host sees a kick icon on remote seats. An open seat is a `KitTile`, "Open slot - invite a friend below"; pressing it moves focus into the list (`FriendsListPanel.FocusList`). Seating, kick and leave are unchanged. |
| Leave | Hidden and out of the layout while you are alone (was: a disabled button). |
| Invites | `KitRequestRow` (a `RequestInfoEntry`): "PARTY INVITE" / "FRIEND REQUEST" in Control Active, warming to Danger as it expires; accept (CTA) and decline (Cancel) icon buttons. The section header and its divider show only while there is a row (`KitListCounter`). |
| Online | `KitOnlineRow` (an `OnlineInfoEntry`): status line in CTA (online, in a party), Purchase (in a match), Neutral (party full, invite pending); Invite (Default), Join (CTA) or Watch (Purchase) on the one join control; the ✕ retracts an invite. While our invite is out the Invite button stays, inactive, reading "INVITED". Members of your own party are left out of this list (`listPartyMembers` off): they are in the seats above. |
| Feature row | Weekly challenge: "WEEKLY CHALLENGE" (Purchase), this week's mode and art, "ENDS IN 6d 3h" from `WeeklyChallengeCard`, unchanged. Maelstrom: "GAUNTLET" (Control Active), its card art and its authored description; pressing it calls `ArcadeExploreView.SelectMaelstrom` -> `ArcadeGameConfigureModal.OpenMaelstrom`, wired in code so the prefab names no scene object. |
| Game cards | `KitGameCard` (a `GameCard`) on a `KitTile`: art cover-fitted under a sliver mask, a bottom scrim, the vessel chip, the genre petal(s), the favourite star (outline, or filled Control Active), the title (H3, wraps to two lines on narrow screens) and the party-pick avatars. Your own pick is the tile's selected state (2 px Control Active border); a lock is the tile's locked state (art 35 %, padlock, "LOCKED"); focus is the CTA ring. |
| Gamepad | Cards and feature cards are the scroll view's Selectables; `KitScrollIntoView` scrolls each into view when focus (not a click) lands on it, so all seven rows are reachable. Row buttons in the party lists do the same. `KitFocusKeeper` puts focus back inside the party panel when a focused row is rebuilt under it. |
| Visibility | Every show / hide in the new code is a CanvasGroup (`KitVisibility`, which also takes a hidden part out of its layout). The legacy row classes keep their `SetActive` paths for the three other copies until item 8. `ArcadeExploreView`'s card-slot pool still activates and deactivates slots and rows: that pool is shared with the Arena's legacy grid and is item 8's to convert. |

Deviations from the board, each reported:
- **"ONLINE", not "FRIENDS ONLINE".** The list is every pilot in the presence lobby
  (`HostConnectionDataSO.OnlinePlayers`), not your friends, so "Friends" would be wrong. The empty
  line reads "No one else is online right now. Pilots who come online show here, ready to invite."
- **The genre petals stay** (the board left them out): they are card identity today, drawn beside
  the vessel chip.
- **The favourite star is not a gamepad stop.** Today it sits inside the card and the pad never
  reaches it in practice; it now has no navigation at all, so the pad moves card to card. Favouriting
  by pad would need a new binding (Y is taken by the freestyle toggle).
- **"INVITED" keeps the ✕** beside it, so an invite can still be retracted as today.

Not verified (no Unity editor in a cloud session): the editor look and layout at 16:10 and 4:3, real
TMP widths, the gamepad flow, the party flows with a second client, and the Game Mode Prefab Kit
Validate run. Steps are in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

**`ArcadeDPadNav` is retired (approved 2026-10-14).** It returned while any modal was open - every
moment its window was visible - and polled the pad directly the rest of the time: with a gamepad
connected it `Select()`ed the hidden grid's first card at Start, moved a hidden selection on every
d-pad press on HOME, and `buttonSouth` invoked that hidden card. `Tools/Build/retire_arcade_dpad_nav.py`
(`--check`, `--self-test`) removed the component from the Arcade, Arena, Mission and Toy Box windows
(and the dead `MIgration_Prefabs (DELETE LATER)/ArcadeScreen.prefab` copy) and the field from the
three `ArcadeExploreView`s; `Assets/ArcadeDPadNav.cs` is deleted. In-window navigation is the
EventSystem's; the Arcade's cards also scroll into view (`KitScrollIntoView`). The Arena, Mission and
Toy Box grids are still legacy until item 8: the pad reaches their cards through automatic
navigation, as it already did while those windows were open, but nothing scrolls their grids yet.

Found while building item 3 (not fixed here):
- **`author_elemental_converter.py --check` fails at the branch base too** ("the document count moved
  by something other than +3 converter / -1 gate"): a spent one-shot gate, not this item's.

---

## 13. Items 4-11: the mockup set (2026-10-14)

Every remaining item is on the Menu Redesign B canvas, each with an interactive board, its state
boards and a "what changes" notes board (the notes boards carry the full lists; this section keeps
the decisions and the departures from the spec). Content is real: card, quest, toy, species and
settings data were read from the shipped assets, and the art is the project's own (card
backgrounds, codex portraits, hangar renders, the converter lab renders, the HOME reference render).
Nothing here is built yet; the builds follow approval, in the order of §3.

### 13.1 Two changes the user asked for on the canvas (2026-10-14)

- **Configure, revision 2** ("too empty, no animation, no prettiness" on the first tree). The tree now
  fills the panel: a numbered step gutter (01 Vessel, 02 Intensity, 03 Pilots) with value and hint,
  150 px nodes, the hull's hangar render with its class description, intensity nodes with a numeral,
  a rising bar meter and the unlock goal when locked, and per-domain seat columns with named chips. Open
  seats carry "+ AI" for the host, which replaces the armed Add AI toggle (open for approval). Motion:
  rise-in on open (panel token, 40 ms stagger capped at 8), the live route redrawing as a 2 px stroke and
  bars growing on selection (std token), and an ambient layer - a spark on the live route, a floating
  hull, a sweep on Start, a slow pan on the preview - that goes beyond §7 ("motion is short and
  functional") and is listed for decision item by item.
- **Translucent top nav** ("keep them transparent ... that thing from the old UI"). The band is Very Dark
  at 60 % with a Neutral Light hairline at 70 %, so the menu scene shows through as it did under the old
  `NavBarGradient`; every board now draws the scene behind the screen, with a Black veil from the nav
  down. This revises item 1: the build sets the `Band` image's alpha (theme colour at 0.6), nothing else.

### 13.2 Decisions (approved 2026-10-14: every recommendation, i.e. the "Drawn as" column)

| # | Item | Question | Drawn as |
|---|---|---|---|
| Q1 | 4 | Keep the ambient motion (route spark, floating hull, Start sweep, preview pan, breathing hint)? §7 allows only short, functional motion. | All on; each can be dropped alone. |
| Q2 | 4 | "+ AI" in each domain's open seat instead of the armed Add AI toggle? | "+ AI" per column. |
| Q3 | 4 | Podium medal colours have no tokens. | Gold (Light), Light, Gold (Dark). |
| Q4 | 4 | Reward bands "4-20" and "20+" overlap at 20. | "4th - 20th", "21st and below". |
| Q5 | 5 | Quest art is two legacy video stills; re-point each quest's Icon to its mode's card art? | Card art; Squirrel render for Vessel Hangar. |
| Q6 | 5 | Show the profile's unused data (crystals, games, flight time, sessions)? | Drawn (revision 2's pilot record). |
| Q7 | 6 | Avatar and name open the profile window (today they only play a sound)? | They open it. |
| Q8 | 8 | Wander's portrait is the conveyor still; Element Charger has none. | `tool_arkway`, `ethirion_omni`. |
| Q9 | 11 | Toasts move to top centre and gain an optional kind (refusal, success, error, goal) for a leading glyph. | Top centre, glyphs drawn. |
| Q10 | 6 | Home gains two "Up next" cards above the hubs: the weekly challenge (live countdown, your best) and the current quest (meter, next goal). New on Home, from data the game already has. | Drawn. |
| Q11 | 6 | A breathing "Tap the cell to fly" hint at the centre of Home, so freestyle can be found. | Drawn. |
| Q12 | 9 | Settings options each get a one-line description; the copy is proposed and needs review. | Drawn. |

### 13.3 Departures from today's behaviour, by item (all listed on the notes boards)

- **4:** Start explains a refusal ("Claim a hull to start.", "Played today - come back tomorrow.") and
  Ready shows "Waiting for others..." (both labels were unwired); LEADERBOARD only on the weekly
  challenge; the d-pad focus row and hidden steppers go; Arena draws the claimed hull's controls; the
  locked-intensity toast matches the pressed level; Maelstrom's pool summary and format are wired;
  the leaderboard's empty, loading and region states are wired and its rewards move inline.
- **5:** one profile window with Avatar / Display name tabs (fixes both panels stacking); validation
  messages shown (they only reached the log); Enter submits; five quest cards fit without scrolling.
- **6:** Arcade is Home's one opaque button; hub focus ring; Online / Offline label wired; the lamp
  only asks "Go offline?" (it also opened the profile window); the invite shows the real party count.
- **7:** title below the nav; a converter bar naming the four elements with Ready / Merging / Merged;
  CONVERT is Inactive mid-run (it restarted the sequence).
- **8:** Arena cards show every seated hull; toy cards show their group; Back closes through the window
  (Window Out plays); B steps out of a Toy configure branch before closing; Navigate drops its A binding.
- **9:** Settings in hex tabs with section 10.7 / 10.8 controls, slider values shown, the restart notice
  wired, "Custom" quality, label typos fixed, Credits authored as a row; Credits gets a real type
  hierarchy and stick scrolling.
- **10:** only the locked tab and entry states and their toasts are restyled (D4); purchase confirmation
  drawn blind.
- **11:** toasts hug their text (they render as 100 x 100 squares today) and stop blocking clicks.

Content found on the way: Joust's card description is the placeholder "Joust joust joust"; Rhino and
Stoat ability maps show "(open design slot)" rows; the Skim Race quest is still named "HEX RACE";
Astro League's description says "Rhino only" while its card seats three hulls; Buckyball and Lion's
Head have no painting art; Colorblind mode, Subtitles and Subtitle size have no consumer.

### 13.4 Revision 2 across the set (2026-10-14, "give all the others the same treatment")

The configure window's revision-2 treatment now runs through every item. It is one shared vocabulary,
so the build can implement it once in the kit:

- **Surfaces:** the blueprint grid (Neutral Dark 1 px lines every 24 px on Very Dark) with Neutral Light
  corner brackets on the main panels; the menu scene behind every screen under a Black veil.
- **Entrances:** panels rise in on the panel token (320 ms OutQuint); cards, rows, tiles and seats follow
  on the 40 ms stagger capped at 8 (section 7).
- **State motion (std / micro tokens):** bars and meters grow, toggle underlines and slider handles
  slide, a refused locked entry shakes once, toasts drop in and drain their 5 s, the quest claim and the
  omni crystal pop (ceremony, OutBack).
- **Ambient (Q1, beyond section 7):** the travelling spark on live routes and progress rails, floating
  hulls / portraits / avatars, the highlight sweep on each screen's primary action, slow pans on card
  art, breathing hints and lamps, the turning settings gear. Every piece is separable.
- **Live data:** countdowns tick (weekly challenge, today's activity), the Ark run shows its clock,
  shuffle rolls through candidate results before landing.

Per item: Profile gains the pilot record (Q6), per-intensity quest goals and the claim ceremony; the
profile window gains a "how others see you" preview, a character count and live rule ticks; Home gains
hub icons, the Up next cards (Q10), the crystal balance and a draining invite timer; the Ark gains a
recipe panel with the element crystals and the omni result; Toy Box, Arena and Toy configure take the
grid, staggered cards, floating portraits and the shuffle roll; Settings gains option descriptions (Q12)
and Credits a title band; the weekly leaderboard gains a podium, your outlined reward band and a
percentile bar.


---

## 14. Item 4: the game configure windows (built 2026-10-15, awaiting in-editor approval)

`Tools/Build/author_menu_configure.py` authors nine prefabs under
`Assets/_Prefabs/UI Elements/Main Menu Screens/Configure/` and mounts them:

| Prefab | Root component | Role |
|---|---|---|
| `ConfigureView` | `MinigameLaunchPanel` | the arcade card window body (in `ArcadeGameConfigureModal.prefab`) |
| `ConfigureArenaView` | `ArenaLaunchPanel` | the Arena window body (scene, `hostModal` = its window's modal copy) |
| `ConfigureMaelstromView` | `MaelstromLaunchPanel` | the Maelstrom window body (scene, same) |
| `ConfigureIntensityNode` | `KitIntensityNode` (+ `KitTile`) | one intensity node: numeral, caption, meter, lock + goal, route stub |
| `ConfigureDomainColumn` | `KitDomainTile` (+ `KitTile`) | one domain: tile, seat strip, open seat with "+ AI" |
| `ConfigureHullTile` | `KitHullTile` (+ `KitTile`) | one hull of the Arena strip |
| `ConfigureSeatChip` | `DomainAvatarChip` | one seat row - the modal's `chipPrefab` |
| `ConfigureAbilityRow` | `VesselControlRow` | one controls row |
| `ConfigurePoolRow` | `MaelstromPoolEntry` | one Maelstrom pool row |

The three views come from one builder because each root needs a different launch-panel class, which
a Prefab Variant cannot change; everything they share is a nested prefab.

**Code (all through the existing contract; the modal still owns every decision):**
- `IntensitySelectButton` and `DomainInfoData` draw through protected hooks; `KitIntensityNode` /
  `KitDomainTile` override them. `DomainInfoData.avatarStrip` widened to
  `HorizontalOrVerticalLayoutGroup` (seat rows stack); it gained an optional `addAiButton`.
- "+ AI" per domain (Q2): the modal wires each tile's `AddAIButton` to `AddAiToDomain` and drives the
  open seats through `ArcadeLaunchPanel.SetSeatSummary`; the armed toggle is no longer authored.
- Seat rows are named (`DomainAvatarChip.SetLabel`): "You / Host", "Pilot", and an AI by its hull.
- Start's line: "Ready to launch" / the refusal reason / "Waiting for others..." - the ready path now
  goes through `SetReadyUpState` (it was dead code); refusal copy is sentence case.
- Locked intensity: one `LockedIntensityGoal` answers the node and the toast for every level, with
  the Denied sting and a shake; a weekly challenge's pinned level is never drawn locked.
- The d-pad focus row and the hidden player/domain steppers are gone; the pad moves through the tree
  with ordinary navigation.
- Favourite star wired by the modal in code (it was dead on the Arena and Maelstrom copies).
- Arena: a hull strip (`KitHullTile`, `OnVesselPickRequested`) replaces the carousel arrows; the
  controls block follows the shown hull. Maelstrom: the format block (race target, game cap, points by
  place, seats) and the pool summary are wired.
- `ArcadeGameConfigureModal` ModalType 10 -> 1 (10 is the Arcade window's own: one B closed both).

**Departures from the boards:** the slow pan on the preview is dropped (the preview is a live render,
not a still; Q1 allowed each ambient piece to go alone). The hint lines under each step are static.

**Retired:** `Tools/Build/author_arena_launch_panel_layout.py` now stands down (its carousel is gone).

### 14.1 The weekly leaderboard (item 4b)

`LeaderboardConfigureModal`'s Content is one `LeaderboardView` instance (same generator): hex scope
tabs (KitTile, new `chrome_globe` / `chrome_pin` / `chrome_people` glyphs), a three-step podium
(`LeaderboardPodiumSlot`), the rank list (`LeaderboardRow.prefab` as the panel's template), a "your
best run" card and the inline rewards table (`LeaderboardRewardBand`; Q4 bands 1st / 2nd / 3rd /
4th - 20th / 21st and below, the local pilot's band outlined). The reward tooltip and its button are
gone (the rewards are on the page). The empty, loading and region messages now reach the screen.

Code: `WeeklyChallengeLeaderboardPanel` gained the optional podium, bands, best-run fields and a
CanvasGroup empty state; with a podium the list starts at 4th and the row tint marks you.
`WeeklyChallengeLeaderboardModal` draws KitTile tabs through the tile (no colour fade), reads the
title from the card list (`games`: "Skim Race", not "SKIMRACE") and counts down in days
(`countdownInDays`: "6d 03:12:09"). `FormatDaysCountdown` and `Ordinal` are covered by edit-mode tests.

Departures: no percentile bar on the best-run card (the board service exposes no total entrant count);
podium medals are Purchase / Light / Purchase at 55 % (Q3 - no medal tokens exist).
Retired: `FrogletTools > Interface > Wire Weekly Challenge Leaderboard` (its scene names are gone).


---

## 15. Item 5: the Profile screen and the profile window

### 15.1 The Profile screen (built 2026-10-15, awaiting in-editor approval)

`Tools/Build/author_menu_profile.py` authors `QuestCard.prefab` and `ProfileView.prefab` under
`Assets/_Prefabs/UI Elements/Main Menu Screens/Profile/` and mounts the view on `Screens/ProfileScreen`
(the old Background, NamePanel, AvatarDisplay, Scroll View and Episodes button are deleted; the root,
`ProfileScreen`, `QuestTrackView` and the inactive Episodes panel stay, because the switcher and the
panel are named by fileID).

- **Pilot card:** avatar (opens the Avatar tab), name, Change avatar / Rename, and the pilot record
  (Q6): crystals (`CrystalCurrencyDisplay`), games completed, flight time, sessions - re-read on every
  screen enter (`ProfileScreen` is now an `IScreen`; games and flight time raise no event).
- **Episodes card:** a locked `KitTile` with `CommerceAffordance` + an authored `MenuAvailabilityView`.
  `EpisodeScreen.refusalHost` points at it, so the refusal (sting, toast) plays on the card, and
  `MenuAvailabilityView.TryPress` now shakes any refused entry that carries a `KitShake`.
- **Quests:** five `QuestCard`s fit without scrolling (`scrollRect` and parallax off). Each card is a
  `KitQuestCard` drawing the legacy `QuestItemCard` state through new hooks (`DrawState`, `SetContext`,
  `SetActiveFrontier`): art + numeral, state line, two intensity goals marked done / next / to-do from
  `GetMaxUnlockedIntensity`, and Claim (with a sweep and the ceremony pop) / Play / "Unlocks after X".
  Its node and segment on the quest track belong to the card; the frontier's segment carries the spark.
  `QuestTrackView` passes each card its context and writes "N of 5 complete".
- **Quest art (Q5):** each quest's Icon is its mode's card art; the Vessel Hangar's is the Squirrel render.
- Buttons are wired by `ProfileScreen` in code (no persistent calls into the generated prefab).
- `QuestItemState` members now carry explicit values.

Departures: the hangar card shows its blurb without the per-quest "needs" list. The ambient pan on the
card art is the one Q1 allowed.

### 15.2 The profile window (built 2026-10-15, awaiting in-editor approval)

`Tools/Build/author_menu_profile_window.py` authors `ProfileAvatarButton.prefab` and
`ProfileWindowView.prefab` in the same folder and mounts the view on `PlayerDataSelectModal` (the old
AvatarContent and DisplayNameContent subtrees are deleted; the `ProfileIconSelectView` component keeps
its fileID, so ScreenSwitcher.Modals, `ProfileScreen.profileWindow`, the arcade profile widget and the
persistent calls that name it all still resolve).

- **One window, two tabs:** AVATAR / DISPLAY NAME are `KitTextTab`s (new kit control: a Selectable word
  tab with a ControlActive underline and the CTA focus ring). Pages hide by CanvasGroup;
  `ProfileIconSelectView.SelectTab(int)` switches without reopening. A bare `ModalWindowIn` (the lamp,
  the arcade widget) opens on the last tab shown instead of stacking both pages, as it used to.
- **Preview column:** "How others see you" - the avatar, the name (the typed name while the Display
  name tab is up), Online, and the party-slot chip.
- **Avatar page:** the 6 x 3 grid of `ProfileAvatarButton`s (octagon `KitTile`: selected ring, pad focus
  ring; `ProfileIconSelectButton` gained an optional `tile` and null guards). A pick saves at once and
  the note reads "Saved to your profile."; the highlight re-syncs to the profile on every open.
- **Display name page:** the field with a live "N / 25" count; the four rules tick as you type
  (`DisplayNameValidator.EvaluateRules` judges them exactly as `Validate` does, captions from
  `RuleCaptions()` so they follow the config); the danger strip carries `Validate`'s message once the
  name is long enough to judge, and the service's refusal ("already taken") after Submit; a spinning hex
  while the service checks; Submit greys until the name is valid; Enter submits; a refusal shakes the
  field. The character limit comes from the config at runtime.
- Save, Cancel, Done, the tabs and Enter are wired in code; the name buttons are now wired before the
  avatar early-returns (a missing avatar reference used to leave Save and Cancel dead).
- New glyph `chrome_diamond` (a rule not yet met; a met rule wears `chrome_confirm`).

Departures: the window keeps one height (680) on both tabs - the board shortens it to 560 on Display
name. The unpicked avatars are not dimmed to 80%.

## 16. Item 7: the Ark screen (Elemental Converter)

Built 2026-10-15, awaiting in-editor approval. `Tools/Build/author_menu_ark.py` authors `ArkView.prefab`
under `Assets/_Prefabs/UI Elements/Main Menu Screens/Ark/` and mounts it under `ArkScreen/ElementalConverter`.
The 3D picture is still built in code by `ElementalConverterView` and now sits behind the overlay
(`SetAsFirstSibling`); wiring CONVERT switches the first cut's code-built title and button off.

- **Full bleed:** the converter's RectTransform grows 170.4 units upward, so the stage runs under the
  translucent nav (ArkScreen itself still starts below it; the NavBar draws after Screens).
- **Title band:** "Ark" / "Elemental converter" / the one-line recipe, on a solid black band below the nav.
- **Recipe panel:** four `ConverterRecipeRow`s in fly-in order (Space, Charge, Mass, Time) with their
  codex renders (`Assets/_Graphics/Codex/ethirion_*.png`): Ready / Merging / Merged, a bar over the
  crystal's own flight (`OmniCombinePlayer.ElementProgress`), the merging row in CTA green with its
  crystal bobbing; "N of 4 merged"; below "Makes", the Omni block - dim until the omni forms, then the
  ceremony pop, floating.
- **Converter bar:** the run's clock ("00:11 / 00:22", `RunTime` / `RunDuration`), four segments that
  fill in order and carry the travelling spark once merged, and CONVERT (opaque Play, highlight sweep):
  Inactive and "Converting..." mid-run - a press mid-run used to restart the sequence and is now ignored -
  then "Convert again".
- `OmniCombinePlayer` gained `IsRunning`, `IsDone`, `RunTime`, `RunDuration`, `OmniFormed` and
  `ElementProgress(k)`; the view no longer calls `Select()` on entry (the switcher's Refocus lands on
  CONVERT).
- `author_elemental_converter.py` now judges its edit against the scene it edits rather than the merge
  base (its `--check` failed on every later Menu_Main edit), and its first-run self-test controls are
  planted into a synthesised pre-converter scene (they had stopped reaching their code once the merge base
  carried the converter).

Departures: the board's timings were compressed for viewing; the game keeps its own flights (about 22 s a
run). No economy is drawn (the converter has no inputs or balances yet).

## 17. Item 8: the mode windows

### 17.1 The Arena window (built 2026-10-15, awaiting in-editor approval)

`Tools/Build/author_menu_arena.py` authors `ArenaGameCard.prefab` and `ArenaView.prefab` under
`Assets/_Prefabs/UI Elements/Main Menu Screens/Arena/` and mounts the view under
`ArenaScreenModal/Arena_Panel/Explore` - the Arcade window's migration (item 3) applied to the arena:
the old Header, GameSelectScrollView, ArcadeLobbyList, CloseButton and FriendListPanel are deleted, the
kept `ArcadeExploreView` is re-pointed into the instance (roster still `ArenaGames`), the window
stretches full screen, the gradient is off and the two OfflineUIGates gate the new party panel.

- **Party:** the shared `PartyPanel.prefab` from item 3, instanced (D7) - one party layer for every
  mode window.
- **Cards:** two columns of `ArenaGameCard`s (682 x 372): art, favourite star, party picks, and what the
  card's own data says about who plays it - "Arena - 2 - 6 pilots" (Min/MaxPlayersAllowed), the
  mode's description, "2 - 3 domains" (Min/MaxDomainsAllowed) and one chip per hull it seats
  (`Vessels`). `KitArenaGameCard` draws them through a new `GameCard.DrawDetails` hook; the hull chips
  are a pool that leaves the row's layout while hidden.
- Lock, local pick, focus ring and favourite behave as the arcade card (`KitGameCard`).

### 17.2 The Mission window (built 2026-10-15, awaiting in-editor approval)

Built as the Arena window's look **without its data path**. Missions are separate games:
`SO_Mission` derives from `SO_Game`, not `SO_ArcadeGame`, and stays that way (decided
2026-10-15). The mission feature itself is untouched: `SO_Mission`, `MissionGames`,
`Arcade.LaunchMission`, the Protect mission and its scene, and the faction-mission code. The hub
entry stays Unavailable, so the window still cannot be opened from Home.

`Tools/Build/author_menu_mission.py` authors `MissionCard.prefab` and `MissionView.prefab` (in
`Main Menu Screens/Mission/`) and mounts the view under `MissionScreenModal/Mission_Panel/Explore`.
This is the Arena migration:
- The old Header, scroll view, lobby list, close button and friends panel are deleted.
- The window stretches full screen, the gradient is off, and the panel's OfflineUIGate gates the
  party panel.
- The `ArcadeExploreView` that sat on Explore is removed and `ArcadeScreen.ExploreView` is cleared
  (that field is optional). With no roster set, that view drew the arcade master list in a mission
  window.

What the window draws:
- **Grid:** a new `MissionExploreView` draws `MissionGames` (the `SO_MissionList` that
  `Arcade.LaunchMission` reads) into two columns of `KitMissionCard`s, with "ALL MISSIONS" and the
  count above them.
- **Card:** the art; "Mission - difficulty 1 - 9" (`MinDifficulty` / `MaxDifficulty`); the name;
  "N threats" (`PotentialThreats`); the description.
- **Locked:** each card carries a `MenuAvailabilityView` set to Locked and drawn undimmed, as the
  board draws it. A press refuses with the Denied sting, a shake and "Missions open with the full
  release." An Available card would raise `MissionExploreView.MissionPicked` instead.
- **Party:** the shared `PartyPanel` (D7), as in Arena.

Departure from the board: no favourite star, because favourites are keyed to arcade games.

### 17.3 The Toy Box (built 2026-10-15, awaiting in-editor approval)

`Tools/Build/author_menu_toybox.py` authors `ToyCard.prefab` and `ToyboxView.prefab` under
`Assets/_Prefabs/UI Elements/Main Menu Screens/Toybox/` and mounts the view under
`ToyboxScreenModal/Toybox_Panel/Explore`. The old Explore children (Header, GameSelectScrollView,
TopButtons), ArcadeLobbyList, CloseButton, ToyboxEmptyState, FriendListPanel and the inactive
ToyCardTemplate are deleted; `ToyboxModal` is kept (the switcher names the window) and re-pointed.

- **Party:** the shared `PartyPanel.prefab`, instanced.
- **Today's activity:** the offering toy's codex portrait, "Today's activity", the activity, the toy,
  "Ends in 7h 12m" and a CTA reward chip ("Try it +10" / "Done"); drawn in the selected (CTA) border, as
  the board marks it. `DailyActivityCard` gained optional `countdownText`, `rewardText` and `portrait`.
- **Shuffle:** the new `chrome_shuffle` glyph, "Shuffle the toy box", the detail line (what a press
  will do, or what the last one did) and a SHUFFLE button.
- **Toys:** a four-column grid of `ToyCard`s - the toy's accent as a 3px stripe, its codex portrait,
  the section, the name and the tagline; `ALL TOYS` carries the live count (`ToyboxModal.countText`).
- `ToyboxModal.cardPrefab` is now `ToyCard.prefab` (it was a scene template), and its
  `crystalClickHandler` is wired (it was found at Start).
- The Toy Box strings are sentence case (config asset and code defaults); the Label role still sets
  the eyebrows in capitals.
- `author_toybox_top_buttons.py` stands down once `ToyboxView` is in the scene (it keeps the config
  asset); its self-test proves the guard. `author_toybox_layout.py` now only finds the Toy configure
  half it still owns (item 8c) and notes the retired template as not found.

### 17.4 Toy configure (built 2026-10-15, awaiting in-editor approval)

`Tools/Build/author_menu_toyconfigure.py` authors `ToyVariantTile.prefab` and `ToyConfigureView.prefab`
(Toybox folder) and mounts the view under `ToyboxGameConfigureModal/Content` (the window's open
animation scales Content). Content's old Border and ConfigurationContent and the inactive
ToyVariantTemplate are deleted; `ToyConfigureModal` is kept and re-pointed, and spawns
`ToyVariantTile.prefab` (it was a scene template).

- **Header:** the toy's section (eyebrow), its name, Back - which steps out of a branch first and closes
  the window only from the toy's top layer (no `ModalWindowCloseButton` on it, by design).
- **Live preview** (`ToyPreviewCamera` on the view's surface, same settings) with "Live preview" and the
  selected variant's name; **About** carries the description under a heading naming what it describes.
- **Variant panel:** breadcrumb ("Spawn Matrix / Fauna / Shark") with the panel's own Back inside a
  branch (it replaces the "<  Back" row and leaves the row's layout while hidden), the count, a
  three-column grid of `ToyVariantTile`s, the status line ("Mass selected" / "Pick a variation."), and
  SWITCH (the toy's own verb: Spawn, Start...) / NAVIGATE.
- **Variant tile:** `KitToyVariantCard` through a new `ToyVariantCard.Draw` hook - glyph, name, second
  line (CTA on the row you are already on); the selected row is the tile's selected state. The glyph is
  the new `ToyShellOption.Icon` (Spawn Matrix element rows carry their petal) or, for a hull row, the
  vessel's own icon.
- The two `ControllerButtonPress` objects that fired Navigate and Close on the same press as the focused
  control (the double-fire and the B-closes-from-a-branch defects) went with the old tree.
- `author_toybox_layout.py` and the Home Hub Wiring window's toy passes stand down once the style-B
  views are in the scene.

Departure: the lab card's accent tint, glow and lift are not drawn (theme colours only).

## 18. Item 9: Settings and Credits

### 18.1 The Settings window (built 2026-10-15, awaiting in-editor approval)

`Tools/Build/author_menu_settings.py` authors `SettingsView.prefab`
(`Assets/_Prefabs/UI Elements/Main Menu Screens/Settings/`) and mounts it under
`SettingsModal.prefab`'s Content (the window animation scales Content). The prefab's old Border
subtree and its nested `OptionsMenuContent` instance are deleted. `OptionsMenuContent.prefab`
itself stays in the tree, and nothing nests it any more.

- **One window:** a dim layer behind a 1600 x 900 panel holding the eyebrow, the title (the active
  tab's name, written by `SettingsTabBar.titleText`), four hex tabs (gear / display / gauge / sliders,
  the tile's selected state is the active tab), Close (`ModalWindowCloseButton`) and the build version.
- **Four pages**, each a CanvasGroup shown through `KitVisibility` (no SetActive). Every row has a label
  and a one-line description (Q12; the copy still needs review), plus one control: a theme-tinted
  `TMP_Dropdown`, an ON / OFF pair of `KitTextTab`s, or a hex-handle slider with its value written
  next to it (`fovValueText` / `musicValueText` / `sfxValueText`: degrees, percent).
- **General** also carries the link row (bug report, privacy policy, delete my data, credits, quit).
  **Performance** carries the restart notice and the menu-only notice, plus Auto detect and Run
  benchmark (`BenchmarkSceneLauncher` on the view root).
- **Quality preset gains "Custom".** Changing anti-aliasing, texture quality or upscaling by hand
  now shows Custom instead of a preset that no longer describes the settings. Custom cannot be
  picked directly, so choosing it snaps back.
- `GameSettingsPanelController` and `SettingsTabBar` are the same classes as before, extended, so the
  in-game pause menu (`R_Pause_Menu_Panel` in `GameCanvas.prefab`, which nests `SettingsModal`) gets
  the same window.

**Override debt (decision D6), settled by deletion.** Every override and stripped stub that
addressed a retired object is now removed:
- Menu_Main: 1,636 overrides and 1 stub.
- GameCanvas.prefab: 272 overrides and 237 stubs, through `R_Pause`.

A scan of every prefab and scene in the repo finds nothing else that addresses the removed objects.
The few overrides that still have a target are kept: Menu_Main's root rect, `screenSwitcher` and the
hidden alpha, and GameCanvas's root offset and `windowAnimator`. The old Content size overrides are
dropped, because Content now stretches.

The generator keeps the retired object ids as a fixed list, so `--check` still guards against an
orphaned override after the rewrite is on disk. A target that was already stale before this change
(EndGameStatsPanel carries one) is left alone.

### 18.2 Credits (built 2026-10-15, awaiting in-editor approval)

`Tools/Build/author_menu_credits.py` authors `Panels/CreditsModal.prefab` and places one instance
in Menu_Main. The instance is the last child of `ModalWindows`, so it draws over Settings, and is
wired to the ScreenSwitcher and added to `ScreenSwitcher.Modals`. `EnsureCreditsModal` therefore
finds an authored CREDITS window and the code-built fallback (`CreditsModal.Build`) stands down.
That fallback stays in the code as the licence backstop.

- **Window:** the same window style as Settings (1200 x 900) with "Credits" as the H1 (the
  manifest's title, written into `titleText`). The right-stick hint and Close (B) sit top right.
  The scroll view has a thin ControlActive scrollbar.
- **Rows** are still generated from `Resources/CreditsManifest`, so the text has one copy. They
  now follow a real hierarchy, each level copying a hidden style in the prefab:
  - section heading: H2
  - blurb: Body
  - entry name: H3
  - role: BodySmall NeutralLightest
  - notice: BodySmall InactiveLight
  - a SurfaceDark rule above each entry

  Each section and each entry is one layout group, so a name, its role and its notice stay
  together.
- **Title band** ("Froglet Inc. presents / Cosmic Shore") opens the list. On every open, the band
  drops in, the list rises in, and the scroll resets to the top.
- **The right stick scrolls the list.** Before this the pad could not scroll it, because nothing
  in it is selectable.
- The manifest's headings are now sentence case, and its two non-ASCII characters are replaced
  ("(c)", " - "). The FMOD line and both credits gates (`CreditsReleaseGuard`,
  `check_credits_manifest.py`) are unchanged.

## 19. Item 10: the closed surfaces (built 2026-10-15, awaiting in-editor approval)

Decision D4 limits this item to what a player can see. Each surface:

| Surface | State | Where it was built |
|---|---|---|
| **Port tab** | Locked: grey with a lock glyph, still pressable. Pressing it plays the Denied sting and toasts "Not open yet."; LT / RT skip it. | Item 1 |
| **Episodes entry** | Locked on the Profile screen: grey, lock glyph, pressable. Pressing it toasts "Episodes arrive with the full release." | Item 5 |
| **Store** | No tab and no reachable button, so nothing to draw. If a Store entry ever returns, it takes the Port tab's locked look and needs a refusal message (`storeScreenMessage` is empty). | n/a |
| **Purchase confirmation** | Drawn blind (below). | This item |

The toast copy is unchanged here; how the toasts look is item 11.

**Purchase confirmation, drawn blind.** `catalogPurchase` is Locked, so every buy is refused
("Purchases open with the full release.") before this window can open. The window can only be
seen by unlocking that surface in `SO_CommerceAvailability`.

`Tools/Build/author_menu_purchase.py` authors `PurchaseConfirmView.prefab`
(`Main Menu Screens/Purchase/`) and mounts it under the Content of Menu_Main's
`PurchaseItemConfirmationModal`, which is a scene object. The old Border and Content List subtrees
are deleted. That removes the Cancel button's `ControllerButtonPress`, which closed the window on
the same B press the modal already handles.

What the view draws:
- **Balance pills** for crystals and tickets, top right.
- **"Confirm"**, then the spend line: "Spend [crystal] 400 to unlock Manta?".
- **The balance bar.** The part you keep is Light, the spend is Gold, and it is captioned "You keep
  850" / "Spend 400 of 1,250" (`DrawSpend`; numbers use the invariant culture, so they stay ASCII).
- **The item tile:** captain, game or ticket art.
- **SPEND 400** (Purchase) and **CANCEL** (Cancel) on the window's bottom edge.

Wiring:
- The `PurchaseConfirmationModal` component keeps its fileID, so `StoreScreen` and
  `HangarCaptainsView` still resolve.
- Its fields, the `IconEmitter`'s (crystals fly from the balance pill to the spend line), and the
  disabled `StoreScreen.PurchaseConfirmationButton` are re-pointed into the view.
- The buttons are wired in code (`wireButtonsInCode`), because a button inside a prefab cannot
  carry a persistent call to a scene component.

## 20. Item 11: menu toasts (built 2026-10-15, awaiting in-editor approval)

**The defect.** Menu_Main has no `ToastNotificationManager`; `ToastNotificationAPI` creates one at
runtime with no item prefab, so every menu toast was the code fallback: a 100 x 100 square in the
lower-left quadrant, under the party panels, cutting most messages off.

`Tools/Build/author_menu_toasts.py` (`--check`, `--self-test`) now authors:
- **`Resources/UI/ToastNotificationItem.prefab`.** The manager loads it whenever no prefab is
  wired (`ToastNotificationManager.ItemResourcePath`) and keeps the code fallback only for the
  case where that load fails. The toast:
  - hugs its text: `ToastNotificationItem` measures the message and asks for 360 to 720 wide,
    wrapping past 720;
  - is drawn Neutral Very Dark with a Neutral Light hairline, in Body 18;
  - has a Jade drain bar along its bottom edge that empties over the 5 s hold;
  - keeps everything visible in a Body child that drops in from above (`dropDistance` 16) while
    it fades in.
- **The container in Menu_Main**, moved to the top centre below the nav (108 down) and stacked
  10 apart. Its layout group now sizes each toast from the toast's own layout instead of a fixed
  square.
- **`ToastNotificationSettings`**, which gains `newestOnTop` (on, per the board) and
  `dropDistance`.

**Kinds.** `ToastNotificationAPI.Show(message, ToastKind)` draws a leading glyph for the kind;
`Show(message)` still works and draws none. Callers now pass a kind:

| Kind | Glyph | Callers |
|---|---|---|
| Refusal | lock, Light | every `MenuAvailabilityView` Locked press (the Port tab, Episodes, locked missions, every commerce refusal); "That party can't be joined right now." and "That match can't be spectated right now." |
| Success | check, CTA | "Friend request accepted!", quest complete, intensity unlocked |
| Error | danger diamond, Danger | the friends and party failures, the cloud-save failure |
| Goal | crystal, Neutral | the locked-intensity goal in game configure |

Unchanged: the timing (fade in 0.25 s, hold 5 s, fade out 0.2 s, unscaled), the queue of 10, the
three-visible limit and drag-to-dismiss. Toasts no longer block clicks: the old squares were
raycast plates, and now only the toast itself takes the pointer, for the drag.

## 21. Item 6: Home

### 21.1 Hub row, pilot chip, settings, hint (6a; built 2026-10-15, awaiting in-editor approval)

Home is restyled **in place**, not as a prefab. `Tools/Build/author_menu_home.py` (`--check`,
`--self-test`) patches Menu_Main directly because several tools and scripts find these objects:
- `wire_home_hub_scene.py`, `HomeHubWiringWindow` and `OfflineMenuWirer` find them by name.
- `ScreenSwitcher` finds them by component: the hub row is the parent of the first `MenuHubButton`,
  and `hubCompanions` lists the avatar and the name.

So every one of those objects keeps its GameObject, fileID and name; only its look, rect and
children change.

- **Hub row:** along the bottom, 30 apart.
  - Each hub's Button is now a `KitButton` under the same fileID, with the kit's
    FocusRing / Fill / Border, a hub glyph and its label: Toy Box (cube), Arcade (gamepad), Arena
    (crossed blades), Mission (target). These are new chrome glyphs.
  - `MenuHubButton` requires a Button, and `KitButton` is one; the press-sound call stays.
  - **Arcade** is the opaque Play button with the A keycap, and it is the pad's first stop
    (`MenuHubButton.preferredFocus`).
  - **Mission** draws Inactive and stays Unavailable.
  - Before this, all four hubs pointed their press feedback at the legacy, inactive ArcadeButton's
    Image, so none showed any press. That object is deleted, along with `QuestGraphRunner`'s stale
    `arcadeNavButton` field that pointed at it.
- **Pilot chip (top left):**
  - A framed plate behind the octagonal avatar. `HomeScreen.avatarImage` is now an image inside a
    masked octagon.
  - The name, an Online / Offline label (the lamp's `statusLabel`, which was never wired), a
    divider, and the crystal balance (`CrystalCurrencyDisplay`).
  - The lamp sits on the avatar's corner. Its stray `OnClickProfileModal` call is gone: it used to
    open the profile window and ask Go offline? at the same time.
- **Question bar:** under the chip.
  - The question (now sentence case), plus a note on what it changes ("Party, friends and the weekly
    board pause until you come back.").
  - The note uses the new `ConfirmQuestionBar.noteLabel`, fed by `OnlineStatusIndicator`'s new note
    strings.
  - Yes (CTA) and No (Cancel) straddle the bottom edge. The bar's own stray `OpenDisplayName` call
    is gone.
- **Settings:** a hex with a slowly turning gear and the X keycap under it. Its
  `ControllerButtonPress` (bound to North, so it was a second Settings opener) is removed.
- **"Tap the cell to fly"** breathes at the centre. The freestyle tap target leaves pad navigation.
- **Pad A:** A on a focused Home control now does only that control's Submit; it used to open the
  Arcade on top. With nothing focused, A still opens the Arcade.
- **`HomeScreen`:** it now re-subscribes to profile changes on every enable. Before, it went deaf
  after its first disable.
- **Tool stand-downs:**
  - `retire_profile_modal.py --check` now reads the retirement itself (survivor is PROFILE, the
    retired modal is unregistered) instead of looking for an opener's method name, which the lamp no
    longer carries.
  - `OfflineMenuWirer` leaves a style-B answer button alone.

### 21.2 Up next and the party invite (6b; built 2026-10-15, awaiting in-editor approval)

Same generator (`author_menu_home.py`).

- **Up next** (Q10): two cards above the hub row, drawn by the new `HomeUpNext`.
  - **Weekly challenge** (Gold border): the mode and its art from `ArcadeGames`, the objective, a
    live "Ends in" countdown (`WeeklyChallengeCard.FormatCountdown`) and your best (a race time, a
    score, or "None yet").
  - **Current quest:** the frontier quest's mode and art, its next goal (claimable, the quest's own
    objective, then the Intensity 3 / 4 goal descriptions), and a four-step intensity meter.
  - The weekly card opens this week's challenge in the Arcade's configure window through the new
    `ScreenSwitcher.OpenWeeklyChallenge`, the same path as the Arcade grid's weekly card. The quest
    card opens the Profile screen.
  - A card with nothing to show hides through its CanvasGroup.
  - Up next is a `hubCompanion`, so it fades with the hub row when a window opens.
  - The frontier logic moved out of `QuestTrackView` into a shared `QuestFrontier`, so Home and
    Profile cannot disagree about which quest is current.
- **Party invite:** a new `PartyInvitePopup.prefab` (`Main Menu Screens/Home/`) replaces the old
  `PartyInviteNotificationPanel Variant` instance, which carried 64 overrides. The new instance keeps
  the name `PartyInviteNotificationPanel` so `OfflineMenuWirer` still finds it, and the online-only
  gate is re-pointed to it.
  - It drops in under Settings: the host's avatar in a CTA octagon, "Party invite", the name,
    "Party 2 / 4" (the new `partyCountText`, read from the host's presence entry), and Join (CTA) /
    Decline (Cancel) on the bottom edge.
  - A Jade bar (`KitDrain`, the new `timerBar`) drains over the 3 s the invite stays up.
  - The old instance's stray entry in another object's child list is removed.
  - Departure from the board: no member avatars, because an invitee knows the host's party count
    but not who is in it.

## 22. Item 12: the in-game UI

The approved boards are on the canvas's second page, "In-game HUD" (18 boards, approved 2026-10-15
as drawn). The in-game canvas is ONE prefab, `_Prefabs/CORE/GameCanvas.prefab` (`Docs/GAMECANVAS.md`),
so item 12 restyles it IN PLACE: every object keeps its GameObject, fileID and name, and no
game-mode scene carries an override of anything written (`gamecanvas_unification_report.py --check`
stays green). One generator owns the item: `Tools/Build/author_ingame_hud.py` (`--check`,
`--self-test`).

### 22.1 The HUD's always-on chrome (12a; built 2026-10-15, awaiting in-editor approval)

- **Ability row** (the LOCKED structure, `Docs/claude/VESSEL_HUD_AND_ELEMENTS.md`): no geometry,
  icon, slot or petal-ladder change - the generator asserts the locked numbers.
  - `AbilityLockupStyle.asset` takes the style-B plate, Neutral Very Dark (`#00041F`) at 86%, and a
    Neutral Lightest (`#747BAD`) slant edge. The locked plate is the same surface at 55%. The
    upgraded state (Light edge, lifted plate, bloom) is unchanged.
  - The control chip is a style-B **keycap** carrying the control's name: `LT`, `RT`, `A`, `B`, or
    `LSHIFT` / `SPACE` on a keyboard. It is drawn by `AbilityLockupView.DrawKeycap` from new style
    fields (`keycapChips`, the kit sliver-10 fill and line, Chakra Petch SemiBold 13, theme tokens).
    While the control is held, the key reads as pressed in: a Light fill with dark text.
  - `ControlGlyphSet.asset` names each pad control (the new `padLabel`). A control with no name
    still falls back to its pad artwork. A locked slot draws no chip.
  - `ControlChipBindingTests.EveryOneThumbAbilityHasAPadKeycapName` holds the names.
- **Goal stack:** each row's trapezoid plate is re-scripted under the same fileID as a slivered
  glass Image (`kit_sliver10_fill`, Neutral Very Dark at 55%).
  - The label is Chakra Petch SemiBold 15 caps.
  - The `/target` half of the count wears Neutral Lightest, and both numbers are tabular
    (`TabularText`).
  - The bloom rests dark and only flares on a score.
  - The win condition's bar wears the local pilot's domain colour (`GoalStack.SetAccent`, fed by
    `MiniGameHUD.RefreshGoalStack`), falling back to the Control Active token.
  - `author_goal_stack.py` still owns the stack's structure and stands down once it exists.
- **Domain score columns** (`DomainScorePanel.prefab`, instantiated per domain): 168 x 96.
  - Layers: a black base at 35%, under Team Dark at 12% (never a full team fill), with a slivered
    hairline in the domain's Light tone (the new `borderImage`).
  - A 44 x 3 cap in the team colour sits on the top edge (the old bottom accent strip, moved).
  - The score is Aldrich 44, with the pilot chips under it.
  - The breathing glow is retired at rest and only flares on a score.
- **Pause hex:** a `DomainVolumeIndicator` is now authored on the pause button, carrying the new
  `centreGlyph` (`chrome_pause`, the 24th chrome icon). The gauge used to hide every child graphic,
  so nothing said what pressing it does. `MiniGameHUD.volumeIndicator` points at it.
- **Departures from the boards:**
  - The objective arrow keeps its generated colour. It is shared with three toy runners, and its
    restyle lands with the toy HUDs.
  - The pause hex shows no Start keycap under it (the button's own controller hint stays hidden, as
    before).
  - The score columns' pilot chips keep the shipped `PlayerScoreEntry` ring.

### 22.2 Ready, waiting for pilots, the countdown (12b; built 2026-10-15, awaiting in-editor approval)

- **Ready:** the button is re-scripted as a Play-variant `KitButton` under the same fileID: the
  A keycap and READY, 300 x 76.
  - Under it, "The match starts when every pilot is ready." in Body small.
  - The magenta GO sprite and the 48 px corner glyph are gone. `ControllerButtonPress` still fires
    it on the pad's A (South); its hint image is unwired because the keycap replaces it.
  - The button still shows and hides as it did (`MiniGameHUD.ToggleReadyButton`).
- **Waiting for pilots** (new `ReadyWaitingPanel` + `ReadyWaitingChip`, authored under MiniGameHUD
  and wired to `MiniGameHUDView.readyWaiting`). This answers defect 16 (no waiting state after a
  multiplayer Ready).
  - When the local pilot presses Ready and more than one human is flying, a plate appears:
    "Waiting for pilots", "You are ready. The countdown starts with the last pilot.", and the count
    ("2 / 3").
  - Under the plate, one chip per human pilot: an octagonal avatar, ringed in its domain colour
    once ready, with "Ready" or "Picking" under it.
  - Readiness comes from the match feed's SOAP channel (`GameToastSituation.PlayerReady`, raised on
    every client by NotifyPlayerReady), so the panel and the feed cannot disagree.
  - The panel hides when the countdown starts or the Ready button returns for a new round.
  - Solo, and against AI only, it never shows, because the countdown follows the press at once.
- **Countdown:** `CountdownTimer` draws text when its new `countdownText` is wired: 3, 2, 1 in
  Light, then GO in the CTA green, in Aldrich at 260 px.
  - Each beat rises from 0.7 to a 1.08 overshoot, settles, holds, and leaves at 1.3 as it fades.
    The beat length and the (empty) beep slot are unchanged.
  - The neon sprites stay as the fallback when the text is unwired.
  - The nested timer instance is wired by two modifications (`countdownText`, `waitingPanel`).

### 22.3 The pause window and the connecting panel (12c; built 2026-10-15, awaiting in-editor approval)

- **Pause** (`R_Pause_Menu_Panel.prefab`, nested in GameCanvas; `PauseMenu` keeps its fileID). One
  1000 x 440 window over a black scrim at 78%.
  - Left half: "Paused", the mode name (`PauseMenu.SpacedName` of the `GameModes` id, covered by
    `PauseMenuTests`), the setup ("Intensity 3 - 3 domains - you fly Squirrel for Jade"), and up to
    three rows. The rows are the HUD's live goals, read from the new `GoalStack.Entries`, so the
    pause window and the HUD always show the same score.
  - Right half, three buttons wired in code (the item 10 precedent): Resume (Play, Start keycap; B
    on the pad), Settings (X), and Leave match (Cancel, Y). Leave match reads "Leave party" for a
    guest.
  - Settings opens the item 9 window **over** the pause window; closing it returns to the pause
    window. The window's CanvasGroup goes non-interactable meanwhile, which also silences its pad
    shortcuts, so B closing Settings does not also resume the match.
  - Start toggles: pressing pause again resumes.
  - Defects fixed:
    - 2: a guest could not resume. The canvas pointed `replayButton` at RESUME, which is hidden for
      every client; that override is gone.
    - 3: MAIN MENU sat on A. Leave is on Y now.
    - 4: the pause button opened the window twice, through a stray `Show` call next to
      `OnClickPauseGameButton`; the `Show` call is removed.
  - The legacy Buttons subtree and the canvas's 39 overrides into it (37 on the deleted objects, plus `replayButton` and `mainMenuButton`) are deleted. Validation
    proves none survives.
  - The legacy `Options_Menu_Panel` instance (inactive) is left as it was.
- **Connecting** (`ConnectingPanel.prefab`): the off-spec `whitrabt` face is gone (Maelstrom's rank
  text is Aldrich).
  - The status line is a quiet top-left "Connecting to Shore" beside a breathing diamond.
  - A bottom-centre readout column holds the mode (Chakra Petch caps) and a phase headline in
    Display: Building the arena, Settling the arena, Waiting for pilots, or Connecting.
  - Under the headline: a 6 px bar (Control Active over Light at 16%), the phase's real counts
    ("42%  (1,234 / 2,900 prisms)", "2 / 3 ready") and the seconds waited, all tabular and in the
    invariant culture.
  - The new `phaseTitle`, `progressText` and `elapsedText` are optional, so an unwired panel keeps
    the one-string readout.
  - The arena preview shrinks to 960 x 540 above the column. The pilot roster is unchanged.
  - Departure: the board's "about 3 s" is an estimate the builder does not make. The panel shows
    the seconds waited instead.

### 22.4 The end reveal and the scoreboard (12d; built 2026-10-15, awaiting in-editor approval)

- **Reveal** (`EndGameStatsPanel.prefab`; `EndGameSequencer` keeps its timing, flourish and message
  set): a black scrim at 50%, then:
  - "Jade wins" in the winning domain's colour (Victory / Defeat when no domain won);
  - the crowd line (CLUTCHED IT) in Light, Aldrich 96, auto-sized down to 48 so the longest line in
    the set fits (defect 9);
  - a 160 x 3 rule in the winner's colour;
  - the mode's own number from its scoring rule ("Race time 02:41:37", "Crystals left 3").
  - New optional fields: `winnerText`, `winnerRule`, `detailText`.
  - The two purple ornament sprites and the teal text are gone, with the canvas's overrides into
    them.
- **Scoreboard:** the legacy `GameOverPanel` instance is gone from GameCanvas. It was 800 x 450-era
  art carrying 66 overrides and an added subtree that held the real buttons, and its Play Again
  call targeted nothing. It is replaced by one instance of the new **`ScoreboardPanel.prefab`**,
  which clones the new **`ScoreboardRow.prefab`** (`PlayerScoreCard`) and
  **`ScoreboardStat.prefab`** (`StatRowUI`). `PlayerScoreCard.prefab` is untouched; the HUD's legacy
  layout still uses it.
  - Header: "Skim Race - Intensity 3", then the banner in the winner's colour ("Jade victory" -
    never "BLUE VICTORY", defect 6; `Scoreboard.BannerLine`), then a 220 x 3 rule.
  - Rows: 1260 x 84 in a scrolling list (four visible). Each row has its rank, an octagonal avatar
    ringed in the domain colour, the name (+ " (you)"), the domain in its colour, and the score. The
    crystal reward shows in Gold. The fill is the team colour at 12% over Neutral Very Dark (never
    a full team fill), with a Light hairline for the local pilot.
  - A stats strip of tiles: label over value.
  - Buttons, wired in code (`Scoreboard.wireButtonsInCode`): Play again / Continue on **A**, Main
    menu / Leave lobby on **B**. Each pair is mutually exclusive, so one press is one action
    (defect 1: one B fired both Play Again and Main Menu). Main menu raises the menu event
    directly (`Scoreboard.OnMainMenuButtonPressed`).
  - The rematch vote faces show in a `PlayerAvatars` strip under Play again.
  - `EndOfMatchTextTests` cover the banner line and the reveal label.
- **Departures:** the board's stats strip has four tiles, but the strip draws however many the mode
  tracks (Skim Race has five). The rematch note text ("NovaKite wants a rematch") stays in the match
  feed, not under the buttons.

### 22.5 Spectating, the match feed, the freestyle HUD (12e; built 2026-10-15, awaiting in-editor approval)

- **Spectating** (`SpectatorOverlay`, built in code by `SpectatorController`): one glass bar under
  the score columns (top 136), in theme tokens and theme faces, loaded from TMP's `Resources`
  because the overlay has no serialized references.
  - The bar reads: SPECTATING, a rule, the pilot in their domain colour, "2 / 4", the < > buttons,
    and CAM: PILOT / CAM: DOLLY.
  - LEAVE is a Danger outline at the right, not a filled red slab.
  - The hint line names the real keys: LB RB / Q E to switch pilot, Y / C for the camera, Esc to
    leave.
  - The four over-1 / literal button colours are gone, taking the colour audit from 193 to 191.
  - Departure: the board's avatar and "Ruby - Sparrow" sub-line need the spectated pilot's avatar
    and hull, which `SetSpectated` is not given. The bar keeps the name, in its domain colour.
- **Match feed** (`GameFeedText.prefab`, `NotificationUI.prefab`, `GameToastSettings.asset`):
  - Each row is now a glass plate (Neutral Very Dark at 50%) that hugs its text, with a 3 px accent
    bar in the toast's domain colour (`GameToastItemView.accentImage`, unwired until now). The text
    is Aldrich 18.
  - The root's TMP was re-scripted as the plate under the same fileID, and the text moved to a
    child.
  - The rows sit 4 px apart and slide in from the LEFT, as the board draws them.
  - Defect 17: aged rows no longer vanish. They fade to 50% after 6 s; `agedAlpha` was 0.
- **Freestyle HUD** (Menu_Main): it inherits 12a through the shared assets (the ability row's plates
  and keycaps, the goal row, the hex).
  - Its exit hex now draws an X: `chrome_close`, through the new `MenuMiniGameHUD.exitGlyph` and
    `DomainVolumeIndicator.SetCentreGlyph`, since the indicator is added in code there.
  - The FTUE bar's "0 / 10" counter drops TMP's default LiberationSans for Aldrich.
  - Departure: the `Game UI` rect (defect 14, a 1 x 0 rect whose children are placed by offsets)
    is left as it is. It renders correctly today, and re-anchoring it blind would move every child.
- **Fleet doc:** the Serpent row in `Docs/claude/VESSEL_HUD_AND_ELEMENTS.md` now records Mass =
  Seed Wall / Lockdown on B. Only the Rhino still has open card slots.

Item 12's defect list (section 13.3 inventory) is closed except:
- defect 14 (above);
- defect 5: quest toasts dropped in a match. `EndGameSequencer` still posts them to a menu
  container the game scenes do not have, and the fix belongs with the quest flow;
- defect 13: the dead UI on GameCanvas (LeftDisplay, RoundTime, ThumbCursors and others) is still
  there, inactive.

### 22.6 Follow-ups (2026-10-15, awaiting in-editor approval)

- **Defect 5 fixed: quest toasts in a match.** `ToastNotificationAPI` finds its container by NAME
  (`ToastNotificationContainer`), and only Menu_Main had one. Every "Quest complete" and
  "Intensity N unlocked" toast `EndGameSequencer` raises at the end of a match was dropped with a
  warning. GameCanvas now carries its own container, bottom centre: the top is the score columns,
  and at the end the scoreboard's header. It is the canvas's last child, so it draws over the
  scoreboard, and its CanvasGroup never takes a click. Validation checks there is exactly one and
  that it draws last.
- **Defect 13, measured, mostly wrong.** Of the objects the inventory called dead UI, only
  `ThumbCursors` is dead: two cursors and two nested ThumbPerimeter instances, inactive, with no
  reference from the canvas, code or any scene. It is deleted. The rest are live:
  - `LeftDisplay`'s two counters are written by Brood Rush's mound and queen drone events
    (`MiniGameHUD.OnMoundDroneSpawned`);
  - the inline `Scoreboard` text is written, unguarded, by `MinigameHUDView.UpdateScoreUI` on every
    reset;
  - `RoundTime`, `LifeFormCounter` and `Pip` are wired into `MultiplayerHUDView` and kept on
    purpose (switched off, not deleted).
  Retiring those is a code change across 15 modes, not a prefab cleanup, and is left for a
  /refactor pass.
- **Board departures closed:**
  - **Objective arrow in the team colour.** `ObjectiveArrowGraphic.SetAccent` derives its three
    tones from the local domain's colour: the halo is the accent dimmed, the stroke is the accent,
    the core is lifted toward Light. It is fed by `MiniGameHUD.RefreshGoalStack`, with one mesh
    rebuild per colour change. The distance readout takes the colour too. Toy runners never call
    it, so they keep the lime.
  - **Pause hex keycap.** The authored `DomainVolumeIndicator` names its control under the hex:
    Start on a pad, Esc on a keyboard. It ensures its own `InputDeviceIconSetSwitcher`, so the
    label follows the device, and a keyboard player is never shown a pad name.
  - **Spectator detail line.** Under the pilot's name: "Ruby - Sparrow", the spectated pilot's
    domain and hull (`SpectatorController.DetailLine`). The avatar is still not drawn, because the
    controller has no profile-icon list.
- Still open: the Menu_Main `Game UI` rect (defect 14), the spectator avatar, and the live legacy
  HUD objects above.

### 22.7 Motion pass (2026-10-15): every surface arrives, every value travels

The user's direction: *all animations should be better than last*. Each item 12 surface was
measured against what the screen did before and what the board drew. Every one that snapped in
now has an entrance, and every value that jumped now travels.

| Surface | Before | Now |
|---|---|---|
| Goal row | bar jumped to its value; glow flared | bar **travels** (0.32 s, OutCubic; a fresh row grows in from empty); the count **punches** (x1.14) with the glow |
| Score columns | appeared as built | **pop in** one after another (staggered by sibling; scale, so the bar's layout is never fought) |
| Ready | appeared | **rises** in each time it is shown; the caption fades in a beat later |
| Waiting for pilots | (did not exist) | the plate **rises** in on Begin; a chip **ticks** (x1.2 punch) the moment its pilot readies |
| Countdown | sprite grew 1 -> 1.5 | text 0.7 -> 1.08 overshoot -> settle -> leave at 1.3 (12b) |
| Pause | Settings panel appeared | scrim **fades**, window **rises**, the three buttons **step in** one stagger apart |
| Connecting | one text string | the headline **replays a rise** on every phase change; the bar eases (as before) |
| Reveal | toast punched in | toast punches in, then the winner line **drops** in (0.1 s), the rule **grows out** (0.5 s), the detail **rises** (0.65 s), started by `EndGameSequencer.revealEntrances` |
| Scoreboard | instant panel (entrance disabled for a drift bug) | scrim **fades**, header **rises**, banner **pops** (0.08 s), rule **grows** (0.3 s), rows **stagger** in (CardEntranceAnimator, as before), stats **rise** (0.5 s), buttons **rise** (0.6 s). All scale/alpha or free-standing, so the drift bug cannot come back |
| Spectator bar | faded | fades **and drops** 14 px into place (eased with the fade) |
| Ability keycaps | lit when held | lit and **pressed in** (x0.92) when held |
| Match feed | slid + faded | unchanged (already the board's motion), rows no longer vanish (12e) |

**A kit bug the pass found:** `KitEntrance` captured an object's position at enable and wrote
offsets from it every frame. A row a view SPAWNS is enabled before its layout group places it, so
a Rise inside a layout kept writing the pre-layout position, and `Finish()` snapped the row
there. Every Rise-by-sibling row in the Settings, Profile and configure windows had this shape.
`KitEntrance.ReconcileRest` now adopts any position something else wrote mid-entrance as the new
rest pose (`KitMotionTests` covers both branches).

### 22.8 Motion pass, menu half (2026-10-15): windows arrive every time they open

Measuring the menu prefabs showed two different problems, not one.

**1. Entrances that existed never played where anyone could see them.** Menu windows
(`ModalWindowManager`) and screens (`ScreenSwitcher`) are never deactivated. They hide by
CanvasGroup alpha, so `OnEnable` fires once, at scene load, behind a closed window. Every
play-on-enable `KitEntrance` built in items 4-11 (configure, leaderboard, profile, settings, Ark,
Home) ran its one entrance invisibly at boot and then never again. **`KitEntrance.ReplayUnder(root)`**
fixes this. It plays every active entrance that has `playOnEnable` on, and leaves code-started
ones to their code. `ModalWindowManager.ModalWindowIn` calls it on open, and `ScreenSwitcher`
calls it on screen enter and on the return from freestyle. No per-prefab wiring is needed, so a
new window gets it for free.

**A replay must not undo an owner's state.** Some entrances share their CanvasGroup with a
script that dims or hides the part: `KitDomainTile.columnGroup`, `KitIntensityNode.nodeGroup`,
`LeaderboardPodiumSlot.group` (a KitVisibility owner) and `HangarVesselTile.canvasGroup`. The old
`Finish()` wrote alpha 1, so replaying on open would have un-dimmed closed tiles and shown an
empty podium step. `KitEntrance` now fades up to the alpha the group had when the entrance
started. `ReconcileAlpha` adopts anything another script writes mid-entrance, the same way
`ReconcileRest` does for position. A dimmed tile stays dimmed and a hidden step stays hidden.
`KitMotionTests` covers both cases. The Ark's Omni pop starts at its dim alpha (0.35), and its
view writes 1 straight after `Play()`, so the reconcile carries it up to 1, as before.

The leaderboard's podium steps are authored hidden (alpha 0), so a step now plays its rise when
`LeaderboardPodiumSlot.Show` fills it, and finishes the rise before `Clear` hides it. A step
filled before the window opens rises on open instead. This also fixes an older bug: the boot-time
entrance used to fade the three empty placeholder steps up to alpha 1.

**2. Surfaces that had no motion at all**:

| Surface | Before | Now |
|---|---|---|
| Arcade / Arena / Mission / Toy Box windows | header and body appeared | header **rises** (10 px), body **rises** one stagger later, on every open |
| Arcade / Arena / Toy Box cards | popped in on open (`CardGridReveal`, as before) | unchanged |
| Mission cards | appeared | **pop in** on open (`MissionExploreView` now plays `CardGridReveal` on `OnModalOpened`, as the arcade grid does) |
| Toy configure | appeared | header rises, then preview, variants and about rise one stagger apart; variant tiles **rise by grid position** |
| Hangar screen | appeared | header and body rise on every screen enter; vessel rows and ability rows **rise by list position** (a locked row keeps its dim) |
| Purchase confirm | appeared | scrim **fades**, window **pops** |
| Party seats | appeared | **rise** one after another |
| Party online / request rows | faded in (`entryFadeInSeconds`, as before) | unchanged (no second entrance on top of the code fade) |
| Party invite popup | snapped on | **drops in** (18 px) each time it appears. `PartyInviteNotificationPanel.entrance` starts it, and it is finished before the popup hides |
| Configure ability rows, leaderboard rows | appeared | **rise by sibling** |
| Scoreboard stat tiles (in game) | appeared | **rise by sibling** under the stats block's own rise |

Each touched generator now requires its entrances (`menu_b_motion.require_entrances`, run first in
validation) and plants a strip (`strip_entrances`) as a negative control.


## 23. Feature-parity audit against the old UI (2026-10-15)

The user's bar for the first full playtest: every feature the old menus and HUD had must still
work, in the new look. This audit compares everything reachable from `Menu_Main.unity` and
`GameCanvas.prefab` on `origin/bleeding-edge` (the old UI, merged into this branch first) with this
branch. "Reachable" means the scene or prefab plus every prefab it nests or references. The audit
scripts are mechanical and were run, not estimated.

**Bleeding-edge merged first** (`17cd51731`). Everything is kept from both sides. The one feature
that landed on top of the old configure modal while this branch was rebuilding it is the Skim Race
/ Slingshot / Warpline **AI difficulty** row. It is rebuilt in style B on `ConfigureView`
(`author_menu_configure.ai_row`): three plates under the domain columns. It ships hidden and the
panel shows it only for a card that offers it. It is wired to `ArcadeLaunchPanel.aiDifficultyPicker`
and covered by a negative control.

| Check | Old -> new | Result |
|---|---|---|
| Script classes no longer reachable from Menu_Main | 14 | 7 are replaced by a style-B subclass of the same class: `GameCard` -> `KitGameCard`, `FriendInfoSlot` -> `KitPartySlot`, `OnlineInfoEntry` / `RequestInfoEntry` -> `KitOnlineRow` / `KitRequestRow`, `QuestItemCard` -> `KitQuestCard`, `ToyVariantCard` -> `KitToyVariantCard`, `DomainInfoData` -> `KitDomainTile`. 3 are built-in UI scripts (Scrollbar, GridLayoutGroup). The rest are below. |
| `QuickPlayButton` (on the old Maelstrom card) | removed | That card ran BOTH `OpenMaelstrom` (persistent) AND QuickPlayButton's Skim Race launch (runtime listener). The new card opens the Maelstrom only. |
| `WeeklyChallengePlayButton` x3 | removed | The Arcade weekly card's Play button was **inactive**. The other two sat on the old configure windows next to the real Start and were never reached. The weekly challenge opens from the Home "Up next" card (`ScreenSwitcher.OpenWeeklyChallenge`) and the Arcade weekly card. |
| `IntStepper` / `PlayerCountButton` (player and domain counts) | replaced | The configure tree's domain tiles and per-domain AI seats (item 4a). The modal's `pcStepper` / `dcStepper` uses are all null-guarded. |
| `HangarVesselGridCard` | replaced | `HangarVesselTile` (item 2, approved in editor). |
| `ArcadeDPadNav` | retired | EventSystem navigation (item 3). |
| GameCanvas `TrapezoidGraphic` x3 | replaced | The goal rows' plates (item 12a). |
| Persistent button calls no longer present | 11 menu, 7 HUD | Every one is wired in code now, except: **`ScreenSwitcher.OnClickProfileModal`** from the Home avatar's lamp. That was the one real gap, fixed: the Home **avatar** opens the profile window again (`author_menu_home`, negative control). The old `GameOverPanel`'s Replay / Main menu are the scoreboard's code-wired Play again / Main menu / Leave lobby (item 12d). |
| References set on bleeding-edge and null now, on components on both sides | 17 | Every one is null-guarded in code, and the feature it served has a replacement. For example, the leaderboard's close button and reward panel became the Back button and the inline rewards (item 4b). |
| Missing scripts in changed assets | 0 new | The only unresolved script guids are package scripts (Image, TMP, layout groups) that the old scene used too. |
| Cross-prefab references to an object that does not exist | 0 new | Three into `Cell.prefab`, identical on bleeding-edge. |
| Game-mode scene overrides into removed GameCanvas objects | 0 | All 37 scenes that nest GameCanvas. |
| `Find("name")` lookups whose name disappeared | 3 | Fallbacks in `QuestItemCard` only. `KitQuestCard` draws its own states. |
| Settings options | same | Every old option is in the new window. The old music / SFX sliders (`AudioLevelSlider`) and the new ones both write `GameSetting`, which `AudioSystem` applies. The old haptics slider was unwired, and so is the new one. |

**Left as they are, deliberately:**
- Defect 14: the freestyle `Game UI` rect. It renders correctly, and re-anchoring it blind moves
  every child.
- The spectator avatar: the old bar had none either. Adding one needs a profile-icon list wired in
  every game scene.
- The live legacy HUD objects (section 22.6): a code change across 15 modes, for a `/refactor`
  pass.

The playtest script for the first full session is `Docs/MENU_REDESIGN_B_PLAYTEST.md`.

## 24. Performance: the ambient layer sleeps while nobody can see it (2026-10-15)

The owner reported a large frame-rate drop after the new UI. There were two causes.

**1. Not the UI, but merged with it.** `CrystalChargeArcs` (the Charge light on the converter
crystals) rebuilt an arc mesh for every visible crystal every frame. Unity's Profiler on
bleeding-edge measured 21.1 ms of a 36.5 ms lava-lamp frame. It reached this branch through the
bleeding-edge merge (`17cd51731`), and bleeding-edge's fix (`646fe7f6c`) is now merged too
(`57ab59d22`). The record is `Docs/PERFORMANCE_OPTIMIZATION.md`, 10-15.

**2. The UI's own share.**
- **The loops.** About 70 endless loops (`KitAmbient`, `KitSpark`) shipped in Menu_Main: Ark 10,
  the three configure views 16, about 3 per quest card, and the 12 intensity nodes.
- **Where they ran.** Almost every one sat under a CLOSED modal (modals stay active at
  CanvasGroup alpha 0) or on a screen the strip had slid outside its RectMask2D. Two more ran on
  GameCanvas for the whole match.
- **What each one cost.** Every frame, each moved a RectTransform or wrote a CanvasGroup on a canvas
  holding thousands of graphics. That makes Unity re-batch the whole canvas every frame, for objects
  nobody could see.
- **`SpectatorOverlay`** re-wrote its group and its bar's position every frame while hidden.

| Fix | Where |
|---|---|
| A loop does nothing while hidden: the product of the CanvasGroups above it is ~0, or a renderer that is not self-masked was culled by a RectMask2D. The groups are cached (re-cached on a reparent), and there is no scene search. | `KitSightline`, `KitAmbient`, `KitSpark` |
| A spark tests its PARENT's renderer for the cull, because it parks itself outside its own mask between passes. | `KitSpark` |
| A Breathe modulates the alpha its owner set. Before this, the configure footer's "Waiting for others..." (hidden through KitVisibility) was forced back to 0.5-1 every frame, so it was always on screen. | `KitAmbient.OwnerAlpha` |
| The spectator bar stops writing once settled. | `SpectatorOverlay` |

Tests: `KitMotionTests` (four sightline cases, two Breathe owner cases). **Not measured in Unity.**
There is no editor in the session, and Amoebius cannot model uGUI's native re-batch, so the
before/after capture is owed to the next editor run.

## 25. The boot world loads before the menu is revealed (2026-10-15)

The owner: *"Toy loading UI should not be there, it can be something subtle. I don't want the user
to join and see another screen growing Garland - load it first."*

**Before:** Menu_Main appeared. A second or more later (once `OnInitializeGame` and the local
player had arrived) `Cell` started building Garland, the world the menu boots into (4,259 prisms),
and `EnvironmentLoadVeil` rose over the menu: a full-screen panel with "GROWING GARLAND..." and
"BUILDING ARENA 45% (1,912 / 4,259) - 3s". Then the menu came back. That is two reveals with a
loading screen in between, on every boot and every return from a match.

**Now:** one fade into a finished world.
- **`Cell.CoverBootWorld`** (from `Start`) raises `EnvironmentLoadVeil.Cover` on the scene's first
  frame when the cell's expected config is the authored boot world (`CellConfigDataSO.BootDefault`).
  It is keyed to that data bit, not to a scene name. No game scene carries a BootDefault config, so
  their connecting screens are untouched.
- **The cover continues the scene transition.** It is opaque and black from its first frame, like
  `SceneTransitionManager`'s fade, so the player never sees the menu before the world.
- **The build is unchanged.** It still waits for the scene to settle. When it starts,
  `EnvironmentLoadVeil.Hold` claims the cover and engages the load gate at the 80 ms slice, as before.
- **A cover nothing claims** (a config with nothing to build) lets go after 20 s.
- **On release, the menu arrives.** `EnvironmentLoadVeil.Released` makes `ScreenSwitcher` replay
  the current screen's entrances, so Home rises in with the world rather than having settled out
  of sight.
- **The readout is subtle.** "LOADING" in small Chakra Petch at 60% Neutral Lightest, over a
  180 x 2 bar in Control Active, at the bottom of the screen. Before the build starts, a short
  segment drifts (`BarSpan`, honest about not knowing yet). While laying, it fills to the lay
  progress; while settling, it holds full. There is no headline and no prism count.
- **The same readout serves the other veils:** a Cell Selector swap, an Arkway run and the
  microscene conveyor (the Toy Box's toy loads).

Tests: `EnvironmentLoadVeilTests` (three bar phases). The visual change is small enough that it
was built without a board, against the owner's own description.

## 26. Item 13: the start flow (built 2026-10-15, awaiting in-editor approval)

Built from the approved Start boards (`StartSplash`, `StartRetry`, `StartSignIn`, `StartSignInFailed`,
`StartName`, `StartNameTaken`, `StartAgeGate`, `StartConsent`). The scenes are written by
`Tools/Build/author_start_flow.py` (`--check`, `--self-test`: eight negative controls); the privacy
overlay builds its own UI in code, so it is restyled in `PrivacyConsentOverlay`.

**One loading screen, not three.** The Bootstrap splash is also the scene-transition overlay
(`SceneTransitionManager._splashOverlay`). `SceneLoader.FadeFromSplashWhenSettled` now holds it while
`EnvironmentLoadVeil.IsCovering` (capped at 60 s), so on boot and on every return from a match the
player sees the branded splash with its LOADING bar, then one fade into the finished world. The
black cover of section 25 is still raised underneath, for the moments the splash is not up.

**Bootstrap splash** (`Canvas - Splash Screen`):
- The feature art stays; a dim and a bottom fade (`kit_fade_v`) go over it.
- The rotating Loader icon is deleted.
- The readout is the section 25 one: "LOADING" over a 180 x 2 `KitLoadBar`. `KitLoadBar` is new: it
  reads the prism builder through `EnvironmentLoadVeil.BarSpan`, so the splash, the sign-in scene and
  the veil draw the same truth, and it sleeps while nobody can see it (`KitSightline`).
- The status line (`BootStatusPanel.statusText`) sits under the bar in Chakra Petch.
- The flat RETRY button is replaced by a "Connection lost" window: eyebrow, title, the request's
  message (`retryMessageText`) and a kit RETRY button with an A keycap (`retryButton`, `retryPanel`).
  `BootStatusPanel` shows it by alpha, replays its rise, hides the readout while it is up and selects
  the button for the pad. Copy: "We could not reach the servers. Check your network, then retry."
  (`BootStatusBroadcaster.labelConnectionLost`, and the auth controller's last-resort retry).

**Sign-in scene** (`Authentication.unity`):
- The same dim. The unreferenced "Status Text" + Loader pair (what players saw during the auto
  sign-in: "Loading" and a spinning icon) becomes the same bottom readout. Its line is written by
  `BootStatusLine` (new), a read-only listener on the boot status channel the controller already
  raises; it hides the whole readout on Hide, so a loading bar never sits under a question.
- **Auth panel:** a slivered window. "Welcome, pilot" / "Ready to launch" / a body line / PLAY AS
  GUEST (a kit button, the controller's `guestLoginButton`) / the controller's status line, restyled.
- **Pilot-name panel:** a window. "Pilot name" / "What should we call you?" / the input field
  restyled (kit fill, a border, an "n / max" count) / four rule ticks / the controller's error line /
  CONFIRM. `DisplayNameRulesView` (new) draws the ticks from `DisplayNameValidator`'s four real rules
  (length, characters, edges, no doubles) and turns the border Danger while a typed name would be
  refused. The board's "Not already taken" tick is not built: uniqueness is only known on submit, and
  the existing error line reports it there.
- Both panels now ship **inactive**: the controller shows them by `SetActive` (kept), and a panel
  that started active flashed for a frame before the controller hid it.
- **Pad and keyboard:** the controller selects the panel's control as it appears (PLAY AS GUEST; the
  name field, so the player can type at once). The kit buttons carry no `ControllerButtonPress`: A
  arrives as an EventSystem submit on the selected button, and a pad poll as well would press twice.
  The generator's validation fails on that pair.

**Privacy overlay** (`PrivacyConsentOverlay`, built in code by AppManager on first run):
- Eyebrows "First flight" and "Privacy", Aldrich titles, Chakra Petch body, theme tokens throughout.
- Panels hug their content (a fixed width, height from the column).
- Primary (Cta fill) and secondary (Neutral border) buttons, each with a keycap, and a focus ring on
  the selected button so the pad can see where A lands.
- The birth-year field is selected on arrival and Enter continues. On the consent step "I agree" is
  selected (the board's A), and B or Esc declines, the same as "No thanks".
- The slivered sprites are five optional fields on `PrivacyConsentConfigSO`, set in
  `Resources/PrivacyConsentConfig.asset`. Empty fields draw plain rectangles.

**Not built: PLAY OFFLINE.** The board offered it beside RETRY as an open question. The auth flow
already falls back to an offline local host on its own, after its offline notice; the retry window
appears only when that offline host itself failed, so a PLAY OFFLINE button there would lead to the
path that just failed. The StartNotes board says so.

## 27. Item 14: the Maelstrom game UI (built 2026-10-15, awaiting in-editor approval)

Built from the approved boards (`MaelstromHub`, `MaelstromStats`, `MaelstromSummary`). Written by
`Tools/Build/author_maelstrom_ui.py` (`--check`, `--self-test`: eight negative controls).
`MaelstromSceneView` still binds everything by name, and every object it names keeps its name.

**The card prefabs are regenerated in place.** They keep their files and guids; `Maelstrom.unity` is
the only thing that references them.
- `MaelstormRoundScoreCardBG` (`MaelstromRoundCard`, 380 x 560): the mode's card art
  (`SO_Game.CardBackground`, cover-fitted), a "Round n" eyebrow over the mode name, "Winner" with a
  domain pill (or "Up next" / "-"), ROUND / TOTAL column heads over the rows. The Control Active
  outline (`currentRoundHighlight`) marks the current card.
- `MaelStromPlayerScoreCardContainer` (`MaelstromPlayerCard`, one 344 x 40 row): the avatar in a
  domain ring, the name over the mode's own score for the round ("01:24:30", "12 Crystals" - kept from
  the old "Round Score" line), the points the round paid the domain ("+5", new), and the domain total.
  The old "Round Score :" / "Total Score :" prefixes are gone; the column heads carry the meaning.
- `MaelstromSummaryScoreCardContainer` (`MaelstromSummaryPlayerCard`, 330 x 380): the domain's place
  ("#1"), the avatar in a ring, the name, a domain pill, "Total score" over the number.

**Hub** (between rounds):
- Top left: the mode as an eyebrow, "Round n", the win-target line.
- Top right: chips for the game pool ("10 of 16 modes, intensity 2"), the round, and the leading
  domain as a pill.
- **The round cards sit side by side.** The scroll is now horizontal and auto-scrolls to the right
  end. LB / RB step it one card at a time, except while the player is flying the preview.
- **The round about to be played is the last card**, outlined (new). It names the drawn mode and
  shows its art once the draw lands (`SetUpcomingRound`). Before that it reads "To be drawn".
- The live preview window on the right: "Next round" over the mode's name, LIVE PREVIEW with a
  breathing dot, the frame and brackets.
- **"Race to N"** (new): a bar per domain filling toward the win target, from the same standings the
  leading chip reads.
- **"Pilots ready"** (new): a face per human pilot, ringed in their domain and ticking (a pulse) when
  they ready. AI have no button, so they have no seat (`MaelstromLobby.HumanPlayers`).
- READY (with the tally, "READY 3/4") is a kit button. The countdown is a large number with its
  "Game starts in" caption, still shown only for the last few seconds. A line under READY explains the
  all-ready rule; pad hints run along the bottom.

**Stats** (the tournament is decided): the same screen with "<mode> results", "n rounds played",
"Last arena" in the preview, NEXT in place of READY, "Ruby takes the Maelstrom. Next shows the
trophy.", and no Pilots ready block.

**Summary:** the mode eyebrow, GAME WON! / GAME OVER, "RUBY WINS" in the winner's colour over a rule
that grows, the domain ranking as chips (points under each pill), the pilot cards side by side, and
PLAY AGAIN (host only) / STATS / MAIN MENU as kit buttons.

**Pad:** READY, NEXT and PLAY AGAIN are selected by the view as their screen appears (non-hosts land
on STATS), so A submits them; they carry no `ControllerButtonPress`, which would press them twice.
STATS answers Y and MAIN MENU answers B through `ControllerButtonPress`; neither is a submit.

**Open questions from the notes board, answered by the build:** the Race-to-N strip is built (it is
cheap and reads the same data); a non-host sees no PLAY AGAIN at all, as before.

**Not changed:** the hub still has no reachable MAIN MENU between rounds. The button lives under the
summary panel, which `MaelstromSceneView.WarnIfHubExitIsUnreachable` already reports; the boards do
not place one in the hub either.

## 28. Playtest fixes: skip, connecting backdrop and camera, pad navigation, touch devices (2026-10-15)

From the owner's first play of the redesign.

- **Skip on the first-login guide.** The guide ("This week's game. Open it.") now has a SKIP button
  in the bottom-right corner, above the dim, on every step. The pad's Y presses it too. It ends the
  whole tutorial: `MenuGuide.RequestSkip` → `QuestGraphRunner` cleans up, and the quest is saved as
  done so it never returns. It does not raise `ftue_completed`, which keeps meaning "played
  through". The words are authored on `QuestGuideToMicrogameNode` (`skipLabel`, `skipKeycap`); an
  empty label hides Skip.
- **Skip on every Lesson.** `DrillLibrarySO.everyLessonSkippable` (on) offers Skip on the account's
  first Lessons too, after the usual delay. Turning it off restores D7.
- **Connecting screen backdrop.** The flat purple-blue sprite is gone. The mode's own card art fills
  the screen, cover-fitted and drifting slowly (KitAmbient Pan), under a dim and a bottom fade
  (`ConnectingPanelController.backdropArt`, looked up in `ArcadeGames`).
- **The connecting preview camera picks a new move every game** (`ConnectingArenaPreview.randomShot`):
  a slow orbit, a low sweep past the build, a crane down from above, a spiral in and out, or a slow
  drift, with a random start angle and direction.
- **Party row:** INVITE and JOIN no longer overlap. The strip now sizes its slots from their
  preferred widths, and it is wide enough for INVITE + SPECTATE.
- **Pause window D-pad:** Resume is selected when the window opens, and the D-pad walks Resume /
  Settings / Leave (explicit, wrapping). Closing Settings puts the pad back on Settings.
- **The START keycap under the pause hex** sat off to one side and tilted, because the hex is
  rotated 30 degrees. The cap now undoes the rotation and hangs straight below the hex's centre.
- **Touch devices name no controls** (`KitInputDevice`). On a phone or tablet with no pad, every
  KitKeycap hides and its slot closes up (`KitKeycapVisibility`), the ability row draws no control
  chips, the pause hex has no keycap, the privacy overlay's keycaps and the Maelstrom pad hints are
  gone. Connect a pad and they come back. One detection fix: on a phone, a tap also fakes a mouse
  press, so `InputDeviceActuation` asked the mouse first and named touch players keyboard players
  (hence "RShift" on a phone). It now asks the touchscreen first, and a phone's browser counts as
  touch from the start (`Application.isMobilePlatform`).
- **Every aspect ratio:** Menu_Main, GameCanvas, the Maelstrom hub, the sign-in scene and the
  Bootstrap splash scale with `Expand` (the whole 1920 x 1080 design stays on screen on a 4:3 tablet
  and a 20:9 phone; identical to before at 16:9 and wider). The privacy overlay and the boot-world
  cover use the same scaling; the cover used raw pixels before.
- **Authored words:** the Maelstrom view, cards and seats, and the privacy overlay's eyebrows and
  keycaps now read their words from serialized fields (CLAUDE.md, player-facing text rule).

## 29. Tutorial off, Spectate, configure preview and opacity (2026-10-15)

- **The tutorial is switched off** (the owner: "I do not want the tutorial at all"). Both switches are
  data, so turning it back on is one tick each:
  - `MainQuest.asset` `questEnabled: 0`. The first-login guide ("This week's game. Open it.") and
    every later quest phase never start. `QuestGraphRunner.TryStart` already honours the flag.
  - `DrillLibrary.asset` `tutorialsEnabled: 0` (new field on `DrillLibrarySO`). The preview's
    Lesson, ghost demo and Mentor tips never start; the preview flies plainly, tap to play as before.
  - The Skip button from section 28 stays in the code for the day the tutorial returns.
- **WATCH reads SPECTATE** on the party list. `KitOnlineRow`'s Join / Spectate / Invite / Invited
  words are serialized fields on the row (authored in `PartyOnlineRow.prefab`).
- **The configure window's preview is always there.** The live arena window (tap it to fly) carried
  a rise entrance; the window opens by a CanvasGroup fade, and the preview could come to rest at
  alpha 0, so the frame, the arena and the tap target were all invisible. The preview has no
  entrance now.
- **The configure window's veil is 94% opaque** (was 72%), so the Arcade's title and game grid no
  longer read through it.
- **The Arcade grid's right column was cut off** on screens narrower than 16:9 (a MacBook's 16:10).
  The window is 1920 wide and the canvas matched height, so it was only ~1728 wide. The Expand
  scaling from section 28 fixes this; no layout change was needed.
- **The real reason the configure preview was missing (found after the first try above):** the
  "+1" toast's `PreviewMicroToast` was authored on the Preview window's own GameObject.
  `PreviewMicroToast.Hide()` calls `gameObject.SetActive(false)`, and `MinigameLaunchPanel` hides
  the toast every time a card opens, so every card open switched off the whole preview: frame, live
  arena and tap-to-play target. The component now sits on its own Toast child (ConfigureView and
  ConfigureArenaView), and `author_menu_configure.py` fails, with a negative control, if a toast
  ever shares the preview's GameObject again.

## 30. Rev 3: Home (built 2026-10-16, awaiting in-editor approval)

Mockups: the **Rev 3** page of the canvas (approved 2026-10-16). Built in this order, one commit each:

| Part | What | Generator |
|---|---|---|
| A | The old nav icons (cut glyph-only from the pre-redesign sprites) beside each tab word; the gamepad focus ring is white, not the CTA green (`KitSelectableBase.focusRingToken`). | `author_nav_glyphs.py`, `author_menu_topnav.py`, `author_ui_kit_prefabs.py` |
| B | Every toast on the right: Menu_Main's stack under the Settings hex, the party invite under it; in a match the toast container under the pause hex and the in-match feed under that, sliding in from the right. The fly hint has an × (`DismissibleHint`, remembered per device). | `author_menu_toasts.py`, `author_menu_home.py`, `author_ingame_hud.py` |
| C | The weekly challenge and current quest cards dock on the left under the pilot chip; – folds a card to a strip (remembered), × closes it for the session (`HomeDockCard`). Card focus is blue. HomeUpNext's words are authored fields. | `author_menu_home.py` |
| D | ONE friends drawer (`FriendsDrawer`) on the left edge of Menu_Main, over every window: a handle opens it, – slides it back, the screen button maximises / restores. It holds the shared PartyPanel, which the Arcade / Arena / Toy Box / Mission windows no longer nest; it is gated offline as the panel was and fades with the menu in freestyle. The four windows' backdrops are glass (55 %), not solid black. | `author_menu_friends_drawer.py`, `author_menu_arcade.py`, `author_menu_arena.py`, `author_menu_toybox.py`, `author_menu_mission.py` |

Next (rev 3): the Arcade / Arena layouts that use the room the panel gave back, then the Hangar (5 tabs, 3D
turntable), then the lava-lamp in-game HUD (approved), then Configure.

## 31. Rev 3: the Hangar (built 2026-10-16, awaiting in-editor approval)

Boards: the canvas's Rev 3 page (`R3HangarAbilities` / `Upgrades` / `UpgradesMass` / `Games` / `Pilots` / `Artifacts` /
`TabSwap`). Generator: `author_menu_hangar.py` (HangarView.prefab).

- **Centre:** the selected vessel as a live 3D model on a turntable (`HangarVesselStage`): harvested off the prefab
  asset by `VesselModelBuilder.TryBuildLive` (no gameplay component wakes), its blend-shape meshes re-skinned so the
  element morphs stay live, rendered on demand into a RawImage by a hand-stepped off-screen camera with the game's
  post-processing. Drag or the right stick turns it; it idles round on its own. Under it, the old Hangar's three
  "how it plays" sliders (`HangarVibeView`, `SO_Vessel.gameplayParameter1..3`, the original rail / thumb art).
- **Right:** five tabs with an animated swap (`HangarTabsView`: underline glide, slide + skew, scan line; LB / RB):
  - **Abilities** (`HangarAbilitiesPage`): each element's ability (ElementalAbilityMap name / first sentence, the
    vessel's own HUD icon), the control written for the player's device (ControlGlyphSet via InputHintBindingMap -
    the HUD's own path), and pressing that control or tapping the row makes the 3D model PERFORM in the element's
    colour (a lunge, a roll, a coloured burst of light).
  - **Upgrades** (`HangarUpgradesPage`): opens in the default form; a row press steps that element's preview
    0 -> 3 -> 5 -> 10 and morphs the hull; shows each element's ability and L5 upgrade.
  - **Games** (`HangarGamesPage`): the games that list this vessel (Arcade + Arena lists), art tiles, vessel-only
    first; a press opens that game's configure window (`ScreenSwitcher.OpenGame`).
  - **Pilots** (`HangarPilotsPage`): the old Captains back as pilots - portrait, name, "The Space Sparrow", quote,
    best at, how they fly as a wingmate - with four cards to pick. Read-only (levels / purchase died with PlayFab).
  - **Artifacts**: an authored empty slot - no artifact data exists in the game yet.
- Retired: the ability column's `HangarAbilityRow` prefab and script; HangarVesselDetailView's words are authored.

**Not done, and why.** "Pressing RT makes the Sparrow FIRE" is a stand-in move (lunge / roll / coloured light), not the
ability's real effect: the real thing needs the vessel alive in a world, i.e. the mode-preview session
(`ModePreviewSession` + a Barren-cell definition) - next step. Upgrades are a preview, not a purchase (no saved
per-vessel element level exists). Pilots cannot be chosen until the Pilots system (PilotSO, branch
`claude/modest-sagan-0ybcgo`) lands.

## 32. Rev 3: the four windows use the room the friends panel gave back (built 2026-10-16, awaiting in-editor approval)

The party panel left the Arcade, Arena, Toy Box and Mission windows for the one friends drawer (§30), so their explore
scroll now fills the body. Generators: `author_menu_arcade.py` (owns the shared rects), `author_menu_arena.py`,
`author_menu_toybox.py`, `author_menu_mission.py`.

- **Shared body and header** (`WINDOW_BODY`, `WINDOW_HEADER`): 100 px in from the left so nothing sits under the friends
  handle (76 px, drawn over every window), 60 from the right. Every window's validator checks this
  (`check_window_inset`, with a planted defect each).
- **Arcade** (the R3Arcade board): the two features (Weekly challenge, Maelstrom) grow to 300 tall and share the full
  width; the grid goes to **five across**, 7 x 5 = 35 slots, which holds the whole roster with no row cloned at
  runtime. `QuestGraphRunner.gameCards` names all 35.
- **Arena**: the four arena games stand in **one row of tall cards** (560 tall) instead of a 2 x 2 of wide ones; each card
  stacks its words from the bottom - hulls, tagline, title, then the eyebrow with the domain count on its right.
- **Toy Box, Mission**: only the shared inset; their layouts are unchanged.

**Not done, and why.** The board's filter chips (Racing / Combat / Collect / Favourites) need a category on
`SO_ArcadeGame`, which does not exist, and the board's "Game of the week" and quest-track column have no data source
on this branch; none was invented.

## 33. Rev 3: the Configure window - cinema preview + step cards (built 2026-10-16, awaiting in-editor approval)

The owner approved "the interesting option": option A's full live preview under option B's three step cards (canvas Rev 3
page, R3ConfigureA / R3ConfigureB). One builder, three windows (`author_menu_configure.py`, `build_view`).

- **The preview is the window.** `ModePreviewWindow` (Maelstrom: the clip) fills everything under the nav, BEHIND every
  setting, with a left and a bottom scrim. Its render height is capped at 720 (`maxRenderHeight`): a full-window surface
  measured at the display height would render a full-HD frame with MSAA; 720 keeps roughly the old 625 px frame's cost.
- **The cinema band** (y 200-500): Briefing glass on the left; the game showing through the middle with the loading
  status, the "+1" toast and the Tap to fly / **FLY** / vessel row; Controls glass on the right (Maelstrom: the game pool).
- **Three step cards** (y 540-980, glass at 82 %, a spark along each top edge): **01 Vessel** (the vessel node and class
  line; Arena: the eight hulls as a 4 x 2 grid and Claim), **02 Intensity** (the four nodes, then the objective),
  **03 Pilots** (Jade / Ruby / Gold, then the AI difficulty row). Maelstrom: 01 Intensity, 02 Pilots, Format.
- **Launch row** (y 992): status on the left, the summary chips and START on the right.
- **Flying** (`ConfigureCinema`, new): while the preview has focus, the whole Chrome group (cards, band, header) fades
  out and stops taking input, and "START or Esc to stop flying" with a **STOP** button fades in. STOP is touch's way back:
  touch has neither START nor Esc, and a full-bleed surface leaves almost nothing "outside" to tap.
- **Pad.** The full-bleed tap surface is drawn first, so it is navigation None (it would otherwise take the window's first
  focus); the pad flies through **FLY**, which calls the new `ModePreviewWindow.RequestFocus` (the same request a tap
  makes). Focus still lands on the first step card's control when the window opens; Back stays last.
- Every node the launch panels, the modal and the scene reference kept its key, so no scene reference moved. The pool row's
  name now stretches (the pool sits in a 560-wide panel).

Validated by `--check` / `--self-test`, with new planted defects: tap surface back in navigation, FLY / STOP calling
nothing, the cinema fading nothing, the preview drawn over the settings, the header ahead of the cards.

## 34. Bleeding-edge merged in; the first-login guide on the rev-3 menu (2026-10-17)

`origin/bleeding-edge` (59 commits) merged into the branch. Menu_Main kept this branch's side: everything bleeding-edge
changed there (the Arena AI difficulty row, 6d857572d / 3ae867467) hung off the old Arena window that §13 / §33 replaced,
so it was re-done in the new window instead:

- **Arena AI difficulty row:** `author_menu_configure.py` now authors the row into `ConfigureArenaView` (card 03, under
  the domains, as on the Arcade) and wires `ArenaLaunchPanel.aiDifficultyPicker` (the base class's field); a planted
  defect holds it. The panel still shows it only where `AIDifficultyRules.IsOfferedFor` says the mode's AI reads it.
- **`author_ai_difficulty_row.py` stands down** on both windows once they are style-B configure views (it was aborting
  on the missing `ConfigurationDetailView` - a spent one-shot must stand down, not abort).
- **The guide (T-20261015-f4c7):** its targets resolve by type, and all exist on the restyled menu (the ARCADE hub, the
  Arcade grid and its cards, the Settings marker). Two fixes: the guide's **Preview step** spotlighted the preview
  surface, which rev 3 made full-screen (a hole the size of the screen, its caption pushed off the bottom) - it now asks
  `ModePreviewWindow.GuideRect`, the new optional `guideTarget` (FLY here) or the surface elsewhere; and
  `author_menu_home.py` now fails if the SettingsButton loses `MenuGuideAlwaysAvailable`.

## 35. The Hangar's TRY: real ability playback (built 2026-10-17, awaiting in-editor approval; T-20261016-1ec1)

The Abilities tab's rows still make the 3D stage play a stand-in (lunge, roll, element light). **TRY IT FOR REAL** now
flies the selected vessel for real:

- `HangarAbilityPreview` (new) borrows the menu's one `ModePreviewSession` - the one the configure window flies - with a
  bare definition, `Assets/_SO_Assets/Hangar/HangarTryPreview.asset` (Barren cell, Mode 0, no objective, no clock; in its
  own folder because generators and the card-art renderer glob `Mode Previews` for one definition per card). Its window
  lies over the stage; TRY asks for the stick the moment the scale model is up, so the real cell builds behind the model
  and the player then flies the selected hull with its real abilities (RT on a Sparrow fires Pulsefire). The session's
  drill (Lesson + Mentor) coaches that hull's abilities over the picture.
- Handing it back (START, Esc, STOP, a tap outside), another vessel, another tab or leaving the screen ends the run: the
  session is struck and detached, and restores the player's hull, pose, AI target and pause itself. STOP is touch's way
  back and also cancels a slow build. The rows still light while flying (the stand-in move is skipped).
- **Pad, on a screen.** The Hangar is a screen, not a modal, so two direct pad polls would have fought the flight:
  ScreenSwitcher paged screens on LT / RT, and the tabs stepped on LB / RB. Both now stand down while
  `ModePreviewWindow.AnyHasFocus` (a modal's preview never reached them: `HasActiveModal`).
- `author_menu_hangar.py` builds it all and validates it (forwarding, the window and hint wiring, TRY / STOP calls, the
  hidden tap surface out of pad navigation, the definition's Barren cell / Mode 0 / no clock), with planted defects.

## 36. Home: compact docked cards, clear of the friends tab (2026-10-17)

The owner: "friends is overlaying vessel hangar, make vessel hangar and weekly challenge smaller tabs". The weekly and
quest cards go from 600 x 132 to 480 x 100 (art strip 132, folded 40), so both expanded end at y ~442 at 1080, above the
friends drawer's handle (y 484-596). The weekly stats row ("Ends in ... / Your best ...") now sizes each word to itself
(an HLG over TMP's preferred widths), so "YOUR BEST" no longer runs into its value. `author_menu_home.py` fails if the
expanded cards reach the handle (planted defect).

## 37. Playtest round (2026-10-17): speed, windows, Hangar text, element glyphs, double tap

From the owner's play test:

- **Speed.** Two per-frame costs from rev 3, fixed: (1) the Hangar's 3D stage rendered its vessel 30 times a second
  behind EVERY screen - the screens stay active side by side in the strip - and now renders only while it is on screen,
  visible (`KitSightline`) and not covered by TRY; (2) the in-match lava-lamp row moved 16 blobs a frame on GameCanvas,
  re-batching the whole canvas each frame, and now sits in its own nested Canvas (added by `AbilityLockupView.Awake`).
  The Hangar's Abilities and Upgrades pages also get their own Canvas (their halos animate).
- **Home under the windows.** The pilot chip (with crystals), the Settings gear and the fly hint now fade with the hub
  row whenever a window opens (`ScreenSwitcher.hubCompanions`; the hint through a wrapper, `HintDock`, so a dismissed
  hint stays dismissed). The windows' glass backdrop is **20 % more opaque** (0.55 -> 0.75), and so are the Hangar's
  glass panels.
- **Hangar text.** The Abilities names / lines and the Upgrades title / upgrade lines were stretched labels positioned
  at x -38 / -30 (left of their row), so they ran under the icon and the keycap; they start right of the glyph now and
  end with an ellipsis. `author_menu_hangar.py` fails on any stretched left-pivot label that starts outside its parent.
- **Animated element glyphs.** Each ability and upgrade row has an element-coloured hex and a halo that breathes in the
  element's colour (`KitElementHalo`, new; rows offset so they do not pulse together; firing an ability flashes it).
- **Double tap to fly.** On the configure window's full-bleed preview, one stray tap launched the player; the surface now
  needs a double tap (`ModePreviewWindow.doubleTapToFocus`), the hint reads "Double-tap to fly", and FLY / TRY still go
  in on one press.

## 38. Glowing borders everywhere; the configure preview framed again (2026-10-17)

- **Every kit border glows** (the owner: "make the borders glow everywhere in the menu"). The outline sprites
  (`kit_sliver14/10_line1/line2`, both flips, `kit_hex_line1/2`) carry a soft inner glow under the band, 6 units deep,
  in the border's own tint - baked into the texture by `author_ui_kit_sprites.py`, so it costs nothing at runtime. Slice
  safety holds (the glow follows the cut inside the corner tiles and the straight edges along stretched ones).
- **The configure preview is a framed window again** (the owner: "at least have a background and a border; it's fine if
  there is no vessel, the environment should be showing"). Briefing and Controls (Maelstrom: the pool) narrow to 460;
  between them the preview stands in an 840 x 324 frame - black background, glowing ControlActive border, corner
  brackets, a fade under the FLY row. The arena model shows there before the tap; double-tap or FLY flies in it.
  `author_menu_configure.py` fails if the frame loses its background or border.

## 39. Fly in the frame; full screen is the option; the preview keeps one cell (2026-10-17)

- **Flying stays in the little window** (the owner: "you should be just able to fly in the little window, not go full
  screen - there should be an option"). `ConfigureCinema` no longer fades the settings when the player takes the
  stick; "START or Esc to stop flying" with **STOP** and **FULL SCREEN** appears above the frame. FULL SCREEN grows the
  frame to the window and fades the settings; again, or handing the stick back, returns the frame. (Full screen scales
  the frame's texture, so it reads softer than the frame.)
- **The preview keeps one cell** ("the cell should not change for the preview"): the configure modal's new
  `previewFollowsIntensity` (off) holds the preview at the card's lowest intensity, so moving the intensity row never
  rebuilds the preview world (a multi-second cell build, a hitch per press). The match still launches at the chosen
  intensity.

## 40. Configure = Rev 3 option B (2026-10-17)

The owner picked the second version ("I like the second version of the UI more" - the R3ConfigureB board over the R4
simple one). Built in `author_menu_configure.py`:

- **A wide live-preview banner** (y 196-480): background, glowing border, brackets; the **briefing laid over its left
  side** on a fade; "Double-tap to fly" + FLY at its bottom left; loading status right of the briefing.
- **Three step cards** under it (y 498-974): **01 Vessel** - art, class, a short description beside it, and the
  **controls list** under it (option B moves Controls into the vessel card); **02 Intensity** - the four nodes and the
  objective; **03 Pilots** - Jade / Ruby / Gold and the AI row. START and the summary chips below.
- **Arena**: the banner leaves room for the Controls panel on its right (card 01 is the hull grid). **Maelstrom**: the
  clip banner leaves room for the pool panel.
- `ModePreviewWindow` now allows textures up to 8:1 (was 4:1): the banner is ~6.3:1 and a 4:1 texture stretched the arena
  sideways. The Maelstrom clip renders at 360 tall (the banner is 284).
- Deviation from the board: the three domains stay side by side as columns (the board stacks them); the column prefab's
  seat strip needs its width.

## 41. Hangar rev 4: a lit bay, the vessel fires, a look-only UPGRADE (2026-10-17)

The owner on the rev 3 Hangar: "Dont like the black screen and the sparrow doesnt shoot or anything make this part way
coooler", and "there should be a button called upgrade and there should be an animation when you upgrade". The Rev 4
boards were approved with "Approved, price is fine, make it look-only for now". Built in `author_menu_hangar.py`:

- **The black box is gone.** It was URP post-processing writing alpha 1 into the stage's render texture;
  `HangarVesselStage` now adopts the game camera with `postProcessing: false`. Behind the vessel there is a **hangar bay**
  (`Hero/Clip/Bay`): a deep glow, two rim lights, a floor pool, three breathing hex rings, a sweeping scan line and
  eight motes. It has its own nested Canvas, so its loops re-batch only the bay, and they sleep while the Hangar is
  hidden (KitSightline).
- **The vessel fires.** `HangarStageFx` (`Hero/Clip/Fx`, also on a nested Canvas) shoots six staggered bolts and a
  muzzle flash from the hull's nose in the ability's colour whenever the stage performs an ability
  (`HangarVesselStage.TryGetNoseViewport`).
- **UPGRADE** on every Upgrades row. The price is `100 x the next level` (`crystalsPerLevel`), paid from the real wallet
  (`PlayerDataService.TrySpendCrystals`, source `hangar_upgrade`), and the button greys out when the player can't afford
  it. The row itself now only focuses: a halo flash and a glow.
- **The upgrade moment.** Six crystals fly from the button to the hull. On landing, the new pip flashes white and settles
  in the element's colour, the hull morphs from the old level to the new one (`AnimateElementLevel`, 0.5 s) and swells
  (`Pulse`), a hex ring bursts, and a banner names the element, "LEVEL N" and "{ability} grows".
- **Look-only.** Levels are saved per vessel per element in PlayerPrefs (`HangarUpgradeStore`,
  `hangar.upgrade.{vessel}.{element}`, max 10). The hull shows the bought form on every visit, and **nothing in a match
  reads it**. The crystals spent are real. Making upgrades count in play is a separate decision for the owner (and
  Garrett, who owns vessel tuning).
- **The words** are serialized on `HangarUpgradesPage`: `buyTemplate`, `maxedWord`, `bannerTemplate` and
  `bannerLineTemplate`.
- **Gate:** `--self-test` now plants three more defects: the stage losing its FX reference, the bay losing its nested
  Canvas, and an UPGRADE button unwired.

## 42. Every kit label shrinks to fit its panel (2026-10-17)

The owner: "There are texts which are not set to auto size and they come out of their panels in main menu".
`KitText.Apply()` used to force `enableAutoSizing = false` at runtime, so every style-B label drew at its role size
whatever its rect held. Now `KitText.shrinkToFit` (default on) turns TMP auto-size on, capped at the role's size and
never below `minSizeScale` (0.6) of it:
- Text that fits draws exactly as before. TMP starts at the maximum and only searches smaller sizes on overflow.
- A label that must never shrink can switch `shrinkToFit` off.
- The kit emitter (`author_ui_kit_prefabs.py` `tmp`, `fit_min`) and the HUD restyle (`author_ingame_hud.py`
  `tmp_restyle`) write the same settings, so the prefabs match what runtime does.
- Every menu, in-game and Maelstrom generator was re-run: 615 of 615 kit labels now auto-size.
- The kit's validation fails on a kit label that does not auto-size; a planted label with auto-size off is caught.

## 43. Configure = Rev 4 simple; Yash's AI seat info in style B (Rev 5) (2026-10-17)

**Configure** (the owner, on the R4Configure board: "I like this UI apart from the latest one"). It replaces option B
(§40). Built in `author_menu_configure.py`:
- **The preview:** ONE framed live preview (1100 x 620, background, glowing border, brackets) on the left. FLY is in its
  bottom-left and the objective in its bottom-right. Flying stays in the frame, with STOP and FULL SCREEN above the
  picture (§39).
- **Under the preview:** the briefing in two lines, and the controls as a three-column grid (GridLayoutGroup;
  `VesselControlsPanel` fills it as before).
- **The settings column:** ONE glass column (680 wide) on the right, with Vessel, Intensity and Pilots. Pilots holds the
  three domain columns and the AI difficulty row. The column hugs its sections (`column_height`). The launch row (status,
  summary chips, START) sits just under it.
- **Arena:** the eight hulls are one row of tiles, with the class line and CLAIM under them.
- **Maelstrom:** the column holds Intensity, Pilots and Format; the briefing and the game pool sit under the clip.
- The sections keep the `Step1..3` keys, so the panel's value labels, focus order (column first, Back last) and the
  guide's FLY target are unchanged.

**AI seat info** (Yash's feature from bleeding-edge 2026-10-17; the R5 boards, approved). What it does is unchanged; it
now looks like style B:
- **Seat rows** (`ConfigureSeatChip`): the (i) is a small hex left of the X. On hover the row keeps its size
  (`hoverScale: 1`), its edge lights, and the hull's name takes the place of the name and the role. That uses a new
  `DomainAvatarChip.hoverReplacesText`, off by default so round chips keep Yash's look.
  Deviation from the board: the board kept the name and swapped only the role. A 206-px row cannot fit name, hull,
  (i) and X, so the hull name covers both.
- **The card** (`VesselInfoCard.prefab`): a glass card with a glowing border beside the settings column, holding:
  - the hull's art;
  - "Ruby AI - Space vessel";
  - the name and the Hangar's line about the vessel;
  - each element's ability as a coloured hex and its name (the hull's `ElementalAbilityMapSO`, new optional
    `VesselInfoPopup.abilityRows`).

  The modal instantiates it from the new `vesselInfoPopupPrefab` field. With nothing wired, the generated card
  still stands in.

  Deviations from the board: the card sits in a fixed spot beside the column, with no notch tracking the seat; and it
  shows no control keycaps (the popup has no input map).
- **Words:** the card's words are now serialized: `subtitleTemplate`, `elementOnlyTemplate`, `abilitiesHeading` and
  `abilityBullet`.
- **Gate:** four new planted defects: a row that grows on hover, the hover label unwired, a card row unwired, and the
  modal back on the generated card.

## 44. Playtest fixes: the Ark under the nav, the fly hint's x, flat - and x (2026-10-17)

- **The Ark** ("Ark screen is messed up"): the title and the recipe panel sat up under the top nav.
  `author_menu_ark.py` grew the converter 170.4 upward to cancel ArkScreen's authored top inset. But
  `ScreenSwitcher.LayoutScreensToViewport` gives every screen the full safe height at runtime, so the grow pushed
  everything up by 170. The converter now fills its screen exactly (`TOP_GROW = 0`, gated, with a planted 170.4 that
  the self-test catches).
- **The fly hint's x** ("Cross doesnt close this popup"): Home's double-tap surface (`HomeScreen/Button`, 840 x 360
  over the cell) drew after `Main_Menu_Panel`, right over the hint, so it took every press on the x.
  - It now draws first, behind the panel. The panel's own image is off, so a tap on empty space still reaches it.
  - The hint's x and the docked cards' - and x work again.
  - Gated in `author_menu_home.py`, with a planted reorder that the self-test catches.
- **The - and x controls** ("i dont like the designs"): the docked cards' -, the cards' and the hint's x, and the
  friends drawer's - and maximise are now flat glyphs, with no plate and no border.
  - The glyph rests dim (NeutralLightest), turns white under the pointer, and lights in the accent colour when
    pressed or focused by the pad.
  - It is a ColorTint on a plain Button, with a 28 px transparent hit area.

## 45. Rev 6: the elemental UI (approved 2026-10-17)

Boards: the canvas's Rev 6 page (`R6Language`, `R6Arcade`, `R6Ark`, `R6FriendsOrb`, `R6FriendsParty`,
`R6FriendsRail`, `R6Notes`). The owner: "I want the UI to be inspired by the elements. As the game is one of a kind so
should be the UI". Decisions:
- the friends panel becomes the **presence orb** (option A);
- the elements drive colour **across the whole menu** (PLAY and START turn Charge);
- **Charge keeps its colour** (`#FFD34D`).

Built in four steps.

### 45.1 Step 1: the elements as the colours of meaning

Each element's game meaning (`Docs/ElementalAbilitySystem/ARCHITECTURE.md`) gives it a job in the menu:

| Element | Game meaning | In the menu | Token |
|---|---|---|---|
| Charge `#FFD34D` | threat and energy | play: START, PLAY, every Play button, WATCH (a live match) | `ElementCharge` (13) |
| Mass `#7FA8FF` | substance and size | what you own: crystals, prices, every spend (Purchase), UPGRADE | `ElementMass` (14) |
| Space `#B07CFF` | reach and presence | people: online / in-your-party statuses, INVITE, JOIN, ACCEPT | `ElementSpace` (15) |
| Time `#5FF0C8` | rate and mobility | progress: the weekly challenge, its countdown, the current quest and its meter | `ElementTime` (16) |

- **Colour source:** the values are AbilityLockupStyleSO's element colours, the HUD's and the Hangar's.
- **Where they live:** `UIThemeSO.elementCharge..elementTime`, `UITheme` fallbacks, and `UIColorToken` 13-16 (checked
  free on every branch).
- **Buttons:** `KitButton`'s Play variant draws in Charge, and Purchase in Mass. New variants Mass (7), Space (8) and
  Time (9) cover actions that are about owning, people or progress.
- **Unchanged:** warnings (the configure window's reason line) and podium medals keep their colours; they are not
  about owning or progress.
- **Gate:** `author_ui_kit_prefabs.py` validation asserts the table (Play is Charge, a spend is Mass, Space, Time),
  with a planted regression that it catches.

### 45.2 Step 2: the living lattice replaces the blueprint grid

- **The shader.** `CosmicShore/UI/Lattice` (`Assets/_Graphics/Materials/Shaders/UI/Lattice.shader`) uses UI/Default's
  plumbing (stencil masking, RectMask2D clip, vertex tint). The maths is in `UILattice.cginc`:
  - a triangular lattice that drifts one lattice vector per period;
  - element energy pulses (Charge / Mass / Space / Time) running along some of its lines;
  - a few nodes twinkling.
  It draws on a sprite-less Image (the UVs span the rect) and runs on `_Time` alone, so it never dirties a canvas or
  costs CPU.
- **Crystallise-in.** With `_Reveal` on, the vertex alpha (a CanvasGroup fade) grows the lattice out from the centre
  with a brighter growth front, instead of fading it. A window's lattice crystallises in as the window opens.
- **Two materials** (`author_ui_lattice.py`):
  - `Lattice_Panel` (one 64-unit layer) replaces the grid in every panel that had it: the Ark recipe, the leaderboard
    list, the Profile card and profile-window preview, the Toy Box cards and Toy configure;
  - `Lattice_Window` (130- and 72-unit layers) sits behind the Arcade, Arena, Mission and Toy Box windows (over their
    glass) and over the configure window's veil.
  The element colours are the theme's.
- **The emitter.** `menu_b_motion.grid_node` emits the lattice; `window_lattice_node` is the window variant.
- **Verified offline.** `Tools/Build/lattice_preview.py` compiles the shipped cginc and the shader's real `frag`
  with clang (`-Wall -Werror`), renders frames, and asserts:
  - pulses appear only on carrying lines;
  - reveal 0 / 0.5 / 1 draw nothing / a disc / the plain lattice;
  - the far layer adds coverage;
  - the corners fade.
  Unity's own shader compile still needs the editor.
- **Gates.** `author_ui_lattice.py` checks the materials match the shader's properties, that no style-B screen draws
  `kit_grid`, and that every window has its lattice. Six planted defects are caught.

### 45.3 Step 3: the elemental rims

- **The shader.** `CosmicShore/UI/ElementRim` (`ElementRim.shader` + `UIElementRim.cginc`) lights a style-B border
  (the sliver line sprite) with a comet of an element's light running round it: a white-hot head and a tail in the
  element, over a dim rim of the same colour. Omni blends all four elements round the rim and turns.
- **Placing the comet.** `KitRimMesh` (a `BaseMeshEffect`) writes each vertex's place on its rect into UV1, because a
  sliced border's UVs are sprite coordinates. It runs only on mesh rebuilds; the comet animates in the shader.
- **Materials.** `ElementRim_Charge / _Mass / _Space / _Time / _Omni` are written by `author_ui_lattice.py`.
  `menu_b_motion.rim_border(node, element)` relights an existing border: rim material, TextLight tint, KitRimMesh.
- **Where the rims are:**

  | Panel | Rim | Why |
  |---|---|---|
  | Ark recipe panel | Omni | about every element |
  | Configure settings column | Charge | setting up a game |
  | Home's weekly and quest cards | Time | progress |
  | Hangar tab panel | Mass | what you own |
  | Arcade feature cards | Charge; the weekly card Time; Maelstrom Omni | — |

  The feature cards' borders keep KitTile's state tint, so the comet runs bright only on the focused card.
- **Verified offline.** `lattice_preview.py` also compiles the rim include and the shader's real `frag`, and asserts:
  - the comet travels at its set speed;
  - the rim sits at its base light away from the comet;
  - element mode keeps its colour;
  - omni shows all four elements.
- **Gates.** `author_ui_lattice.py` checks that every rim-lit Image has KitRimMesh beside it and that the Ark,
  Configure and Hangar keep their rims. Three more planted defects are caught.

### 45.4 Step 4: the presence orb and the party molecule

The owner chose option A, "Orb". The FRIENDS tab on the left edge (rev 3's handle) is gone.

- **The orb.** A 92 x 92 orb sits in the bottom-left corner over every screen (`Drawer/Orb` in Menu_Main). It is the
  friends drawer's handle: pressing it opens the party panel. What it shows:
  - you at the centre, your avatar in a ring of your domain colour;
  - up to three party members on the inner ring (top-left, top-right, bottom), each in their domain colour, with a
    flowing bond from you to them;
  - the slowly turning dashed orbit, carrying one dot per player online (up to 8);
  - "N online" under it, or "Friends" while nobody else is online. Both lines are authored fields on the orb.
- **Why 92 px and not the board's 150.** The orb draws over the windows too. At 92 it fits the 100 px left gutter the
  windows keep (`author_menu_arcade.ORB_*`); the board's size would sit over the windows' cards. The windows' gutter
  gate (`check_window_inset`) and Home's docked-card gate now measure against the orb.
- **The shader.** `CosmicShore/UI/Presence` (`Presence.shader` + `UIPresence.cginc`) draws the glow, the inner ring,
  the orbit with its dots and the three bonds on one sprite-less Image. Every radius is a fraction of the half-size.
  - The member seats' angles and radius are `author_ui_lattice.ORB_MEMBER_ANGLES` / `ORB_MEMBER_RADIUS`. The shader's
    `_BondAngles` / `_MemberRadius` and the avatars' positions are both written from them, so a bond ends under its
    avatar.
  - `KitPresenceOrb` owns one copy of `Presence_Orb.mat` (UI graphics cannot take a property block). It writes
    `_Online` and the three bond colours only when the party or the online list changes. The motion is `_Time` alone,
    so the orb never rebuilds a canvas.
- **Where the orb's facts come from.** It reads, never decides. `ArcadeLobbyList` (the party panel) now publishes:
  - `PresenceChanged`, raised only when the drawn seating, a seat's live domain colour or the online count changes;
  - `DrawnSeatCount`, `TryGetSeat` and `RemoteOnlineCount`.

  So the orb shows exactly the synced roster the panel draws. It finds the panel through its FriendsDrawer; an
  inspector reference wins.
- **The party molecule.** `PartyPanel.prefab`'s four stacked seats became a molecule:
  - the leader's seat (slot 1, the host on every device) is at the centre;
  - the other three seats are bonded round it at 155, 25 and 270 degrees, 120 units out;
  - each seat (`PartySlot.prefab`) is the avatar in its domain ring over the name and role, with the host's kick on
    its shoulder;
  - an open seat is a glass tile with a Space "+" and "Invite a friend".
- **The molecule's bonds.** `CosmicShore/UI/Bond` (`Bond.shader`, same include) draws dashes flowing outward on a thin
  rotated Image, with `KitRimMesh` for the place along it.
  - `ArcadeLobbyList.seatBonds` tints them: the pilot's domain colour, Space while it is unknown, and dim Space
    (`openBondAlpha` 0.35) for an open seat.
  - The bond's colour is its vertex colour, set only on change.
- **The window.** The drawer window carries the panel lattice behind it and a Space rim round it, and its title has the
  Space glyph. The panel header reads "Your party" with its count in Space; the online diamond is Space.
- **Authored text.** `ArcadeLobbyList`'s "1 Player Online" / "{count} Players Online" moved from code literals to
  serialized fields while this method was touched (the player-facing text rule).
- **Verified offline.**
  - `lattice_preview.py` compiles `UIPresence.cginc` with both shaders' real `frag` and asserts:
    - the orbit carries exactly one dot per player online (0, 3, 8);
    - the dots turn at the set speed;
    - a seated member's bond lights in their colour, an empty seat's bond draws nothing, and a bond lights only towards
      its own seat;
    - the glow stays in the middle;
    - the dashes flow outward;
    - the molecule bond keeps its colour and stays on its line.

    Three planted shader defects (a dot too many, an empty seat bonded, dashes flowing inward) each fail it.
  - The drawer generator checks:
    - the orb is the handle and sits on `ORB_*`;
    - the field uses the presence material;
    - the seats are on the shader's angles;
    - the window keeps its rim and lattice;
    - no Handle is left (8 planted defects caught).
  - The arcade generator checks that each bond starts at the centre avatar and ends at its seat's avatar, carries
    KitRimMesh, and that the centre seat has no bond (17 planted defects caught).
  - The lattice generator checks the orb and bond materials against their shaders (13 planted defects caught).
- **Re-emitting the drawer.** The drawer generator now removes the drawer's old documents before re-emitting it, so a
  retired node (rev 3's Handle) cannot linger orphaned in Menu_Main.
