# UI colour-literal audit — Assets/_Scripts/UI

**Against:** `Docs/STYLE_FOUNDATION.md` v0.3.1 §11 · **Taken:** 2026-08-25 · **Branch:** `claude/uithemeso-style-foundation-00fll9`

Companion to `UIThemeSO` / `UITheme`. This is a **map, not a migration** — nothing here is applied.
Every literal is left exactly where it was; the buckets say what would have to be decided before
any of them could move.

## Count, and the drift

| Count | Source |
|---|---|
| 165 | the original audit |
| 184 | a later recount |
| **187** | **this pass** (167 before this branch added the token definitions) |

My extractor is `new Color(...)` / `new Color32(...)` with **all-constant arguments**, plus the
eleven `Color.<named>` statics, plus `#RRGGBB[AA]` in string literals, over `Assets/_Scripts/UI/**.cs`.

The drift is almost certainly definitional, not a disagreement about the files:

- **8 `new Color(...)` calls take variables**, not constants — `new Color(c.r, c.g, c.b, alpha)` and
  friends. They are alpha edits on a colour that came from somewhere else, so they are not literals
  and I excluded them. `167 + 8 = 175`, which is between the two prior counts.
- `new Color[n]` **array allocations** read as `new Color` to a naive grep. There are 3 in this tree.
  `175 + 3 = 178`.
- The remaining ~6 to reach 184 are most likely `Color.Lerp`/`Color.clear` style hits, or a scan that
  reached past `_Scripts/UI` (vessel HUD colour also lives under `Controller/Vessel/`).

I did not try to reproduce either prior number. **Use 187**, and note the extractor definition
travels with it — a colour-literal count is meaningless without one.

**This branch itself proved that.** The first version of the extractor tested arguments against
`^[0-9.]+f?$`, which rejects `0xE6` — so the 20 `new Color32(0xE6, 0xE9, 0xFF, 0xFF)` literals this
branch *added*, in `UIThemeSO.cs` and `UITheme.cs`, were silently uncounted. The raw total read 167
and looked clean. They are legitimate (the token values have to be written down exactly once, and
that is where), but they are now excluded **by filename**, as a stated decision, rather than by a
regex that happened not to match them. An exclusion you cannot see is indistinguishable from a bug.
The in-scope population is unchanged at 133.

## Scope: 54 of the 187 are not in the mapping population

| Class | n | Files |
|---|---|---|
| Editor-inspector chrome (`GUI.backgroundColor`, `EditorGUI.DrawRect`) | 24 | `ActiveGameModesWindow`, `LeaderboardConfigSOEditor`, `UniversalStatsProviderEditor`, `Model/MinigameHUDInspector` |
| Debug / console markup (`<color=#FFD700>` FLOW tags, `DebugExtensions.LogColored`) | 10 | `Modals/ArcadeGameConfigureModal`, `Screens/PartyInviteNotificationPanel`, `TestMiniGameEvents` |
| §11 token definitions | 20 | `UIThemeSO.cs`, `UITheme.cs` |

The style foundation governs the game's UI, not the Editor's or the console's. These are excluded
from the mapping population and should stay excluded from any future count.

> Separately worth noting: three of those four editor files sit **outside an `Editor/` folder**
> (`Assets/_Scripts/UI/LeaderboardConfigSOEditor.cs`, `UniversalStatsProviderEditor.cs`,
> `Model/MinigameHUDInspector.cs`). That is a `CLAUDE.md` conditional-compilation concern, not a
> style one. Flagged, not touched.

## Result over the 133 in scope

| Outcome | n | % |
|---|---|---|
| **Mapped onto a §11 token** | **31** | 23% |
| (a) missing token | 16 | 12% |
| (b) belongs in a feature-level SO | 40 | 30% |
| (c) never designed | 46 | 35% |

### Mapped — which tokens actually got used

| Token | Call sites |
|---|---|
| `textLight` | 17 |
| `inactiveLight` | 6 |
| `surfaceBlack` | 4 |
| `cta` | 3 |
| `danger` | 1 |
| `textInactive` | 0 |
| `surfaceVeryDark` | 0 |
| `surfaceDark` | 0 |
| `surfaceLight` | 0 |
| `neutralLightest` | 0 |

**Five of the ten colour tokens have zero call sites in C#**: `textInactive`, `surfaceVeryDark`,
`surfaceDark`, `surfaceLight`, `neutralLightest`. They are not unused — they are authored in prefab
and scene YAML, which C# never touches. Same for **every** spacing, geometry and motion token in §11:
uGUI lays out in the inspector, so `spacing`, `sliverLarge/Small`, `hairline`, `stroke` and the four
durations have **no C# literals to map at all**.

> **The consequence is worth stating plainly:** mapping C# colour literals reaches roughly a
> quarter of the in-scope literals and none of the layout. The larger half of §11 adoption is a
> prefab/scene job, not a code job. A T-task that only edits `.cs` cannot land the style foundation.

## Flags — surfaced, not resolved

| # | Where | What |
|---|---|---|
| **F1** | `DomainVolumeHexGraphic.cs:84` | `{ Color.green, Color.red, Color.yellow }` is a hardcoded domain triad. §0/A says Jade **cyan**, Ruby **purple**, Gold **amber**. This widget is painting the wrong three colours and bypassing `SO_ColorSet` entirely. Most serious finding here. |
| **F2** | `Elements/LoadoutCard.cs:20` | `DeselectedColor = Color.white`. Deselected reading as full-brightness white inverts §10.5/§10.13, where inactive is dim. |
| **F3** | `Elements/OnlineInfoEntry.cs:39` | `onlineColor = Color.white`, but §3 assigns player online status to CTA `99FF80`. |
| **F4** | `Modals/HangarTrainingModal.cs:76` | `Color.green` as the selected-intensity tint. Maps to `cta` by intent, but §11 has no distinct *selected* token — §10.11 says selection is a white border + light fill, which is a different mechanism. |
| **F5** | `ObjectiveArrowGraphic.cs:26,29,32` | Three hardcoded greens. §3 lists the objective arrow as **team colour, full saturation, "existing — keep"** — but the shipped code is not team-coloured. Either §3's "existing" is describing something that was never true, or this widget regressed. Needs a design call. |
| **F6** | `Privacy/PrivacyConsentOverlay.cs` | 15 literals forming a **self-contained pre-palette theme** (`F0F2F7` / `C2C7D4` / `99A1B2` / `737887` / `292B36` / `171A21` / `299E85` / `474C59` / `E65C6B` / `59B8F2`). Four sit near a §11 token but none land on one (text `F0F2F7` vs `E6E9FF`; error `E65C6B` vs `FF4B3A`; decline `474C59` vs `5C5F70`). It is the single largest concentration and needs re-theming as one unit, not literal by literal. |
| **F7** | `Views/ArcadeLoadoutView.cs` ×8 | `Color.white` used as *clear the selection*. §10.11 says unselected tiles are **dim**, so identity-white is the wrong resting state — but the fix is a design decision, not a token swap. |
| **F8** | `Views/PortSquadCaptainSelectionView.cs:13` | `SelectedRowColor = Color.gray` — selected is greyer than unselected black. Reads inverted. (§10.12 Port navigation; note the Port screen is cut from the overhaul per §4, so this may be dead surface.) |

## (a) Missing token — §11 has no field for this role

| Role | n | Evidence |
|---|---|---|
| Local-player leaderboard row highlight | 6 | `#1AB2B2` teal in `LeaderboardsMenu:214-216`, `DailyChallengeLeaderboardView:93-95`. §10.10 specifies only a `*` marker — the teal is undocumented. |
| Positive / gain green | 2 | `#33FF66` in `ScoreNumberAnimator:131`, `HUDAnimationSettingsSO:34`. §2's gap table proposes `danger` and reuses CTA for *attention*, but never names a **gain** hue distinct from CTA. |
| Secondary body text | 1 | `#C2C7D4`, `PrivacyConsentOverlay:302`. §11 ships exactly one text colour. |
| Tertiary / muted text | 1 | `#99A1B2`, `PrivacyConsentOverlay:309`. |
| Input placeholder text | 1 | `#737887`, `PrivacyConsentOverlay:356`. §10.2 specs the field but not its placeholder. |
| Hyperlink | 1 | `#59B8F2`, `PrivacyConsentOverlay:407`. No link colour exists in the palette. |
| Toast surface | 1 | `#1A1A26` @90%, `ToastNotificationManager:152`. Both §11 surfaces (`00010A`, `00041F`) are blue-tinted; this is neutral. |
| Gauge normal fill | 1 | `Color.white`, `ResourceDisplay:39`. |
| Gauge full / threshold | 1 | `Color.red`, `ResourceDisplay:40`. Semantically *at capacity*, not *destructive* — folding it into `danger` would overload the token. |
| Locked-card tint | 1 | `#4C4C4C`, `GameCard:31`. §10.6 says locked cards are "grey" and gives no value. |

## (b) Belongs in a feature-level SO — 40

Two distinct owners:

**`SO_ColorSet` — 20 literals.** Domain-colour fallbacks (`?: Color.white` / `Color.gray` when
`ThemeManagerData` is unwired) plus F1's hardcoded triad and F5's objective arrow. These must not
come from `UIThemeSO`: §3 makes team colour data, and the whole point of omitting team fields from
`UIThemeSO` is that there is no team colour here to reach for. **The right fix is `SO_ColorSet`
gaining an authored fallback**, not the theme gaining a team field.

| File:line | Literal |
|---|---|
| `ConnectingPanelController.cs:160` | `#FFFFFF` — SO_ColorSet — domain accent fallback |
| `DomainVolumeHexGraphic.cs:84` | `#00FF00` — SO_ColorSet — hardcoded domain triad; SEE FLAG F1 |
| `DomainVolumeIndicator.cs:340` | `#FFFFFF` — SO_ColorSet — domain fallback |
| `DomainVolumeIndicator.cs:352` | `#FFFFFF` — SO_ColorSet — domain fallback |
| `DomainVolumeIndicator.cs:82` | `#FFFFFF` — SO_ColorSet — jade/ruby/gold fallback |
| `GameToastSystem/GameToastAPI.cs:59` | `#FFFFFF` — SO_ColorSet — domain fallback |
| `GameToastSystem/GameToastController.cs:205` | `#FFFFFF` — SO_ColorSet — domain fallback |
| `MiniGameHUD.cs:572` | `#808080` — SO_ColorSet — domain fallback |
| `ObjectiveArrowGraphic.cs:26` | `#4F802E` — SO_ColorSet — §3 objective arrow is TEAM colour; SEE FLAG F5 |
| `ObjectiveArrowGraphic.cs:29` | `#8CEB1A` — SO_ColorSet — §3 objective arrow is TEAM colour; SEE FLAG F5 |
| `ObjectiveArrowGraphic.cs:32` | `#DBFF9E` — SO_ColorSet — §3 objective arrow is TEAM colour; SEE FLAG F5 |
| `Scoreboard.cs:592` | `#808080` — SO_ColorSet — domain fallback |
| `MaelstromRoundCard.cs:67` | `#808080` — SO_ColorSet — domain fallback |
| `View/DolphinVesselHUDView.cs:92` | `#FFFFFF` — SO_ColorSet — team crystal fallback |
| `View/SquirrelVesselHUDView.cs:91` | `#FFFFFF` — SO_ColorSet — player domain fallback |

**Per-vessel HUD state colours — 17.** Ability/state tints on the vessel HUD views (Squirrel drift,
tube, overheat; Rhino crystal/line/debuff; Urchin ammo/riding; Serpent pip; Dolphin armed flash;
Manta overcharge; Sparrow blocked input). These are **gameplay state readouts, not chrome** —
`ElementalBarsConfigSO` is the precedent, and `VesselHUDView:288` already reads
`config.whiteColor` with a literal only as its fallback. They belong to a vessel HUD config SO,
routed through the `/vessel` skill.

**`HUDAnimationSettingsSO` — 3.** `scoreLossColor` and `countdownUrgentColor` are `#FF4C33`,
**Δ0.008 from `danger` `#FF4B3A`** — near-certainly the same intended colour, arrived at
independently. That SO should read `UITheme.Resolve(theme, UIColorToken.Danger)` rather than hold
its own value. The cleanest single proof that the token system is worth having.

## (c) Never designed — 46

Two sub-kinds, and the split matters because only one of them is a defect:

| Sub-kind | n | Verdict |
|---|---|---|
| **Multiply-identity `Color.white`** (untinted sprite, rest state, tween reset) | 32 | **Not a colour decision.** `img.color = Color.white` on an `Image` means *do not tint*, and `Color.Lerp(x, Color.white, t)` means *desaturate*. Tokenising these would be a category error — the white is an identity element, not a hue. Leave them. |
| **Alpha-only / `Color.clear`** (hide, raycast target, pip-empty at 25%) | 5 | Same — an alpha, not a hue. |
| **Genuine ad-hoc colour nobody specified** | 9 | Real gap: `IconRotator`'s 4-colour decorative cycle (directly against §1 "colour is information, never decoration"), `MantaVesselHUDView`'s `Color.yellow` highlight, and 4 of `PrivacyConsentOverlay`'s bespoke neutrals/teals. |

> The 32 identity-whites are the reason the raw count overstates the problem. A headline of
> "167 hardcoded colours" implies 167 undesigned decisions; the honest number of **undesigned
> player-facing colour decisions is 9**, with 16 more blocked on a missing token and 40 owned by
> a different asset.

## Full per-literal table

`—` in the Verdict column means out of scope (editor or debug).

| File | Line | Hex | α | Verdict | Note |
|---|---|---|---|---|---|
| `ActiveGameModesWindow.cs` | 160 | `#80CC80` |  | — | editor-inspector chrome |
| `ActiveGameModesWindow.cs` | 168 | `#CC8080` |  | — | editor-inspector chrome |
| `ConnectingPanelController.cs` | 160 | `#FFFFFF` |  | (b) | SO_ColorSet — domain accent fallback |
| `Controller/MantaVesselHUDController.cs` | 22 | `#FF0000` |  | (b) | Manta HUD — overcharge state; danger candidate |
| `DomainVolumeHexGraphic.cs` | 84 | `#00FF00` |  | (b) | SO_ColorSet — hardcoded domain triad; SEE FLAG F1 |
| `DomainVolumeHexGraphic.cs` | 84 | `#FF0000` |  | (b) | SO_ColorSet — hardcoded domain triad; SEE FLAG F1 |
| `DomainVolumeHexGraphic.cs` | 84 | `#FFEB04` |  | (b) | SO_ColorSet — hardcoded domain triad; SEE FLAG F1 |
| `DomainVolumeIndicator.cs` | 82 | `#FFFFFF` |  | (b) | SO_ColorSet — jade/ruby/gold fallback |
| `DomainVolumeIndicator.cs` | 82 | `#FFFFFF` |  | (b) | SO_ColorSet — jade/ruby/gold fallback |
| `DomainVolumeIndicator.cs` | 82 | `#FFFFFF` |  | (b) | SO_ColorSet — jade/ruby/gold fallback |
| `DomainVolumeIndicator.cs` | 178 | `#000000` | 0.00 | (c) | alpha-0 hide, not a colour |
| `DomainVolumeIndicator.cs` | 340 | `#FFFFFF` |  | (b) | SO_ColorSet — domain fallback |
| `DomainVolumeIndicator.cs` | 352 | `#FFFFFF` |  | (b) | SO_ColorSet — domain fallback |
| `Elements/DomainInfoData.cs` | 29 | `#FFFFFF` |  | `textLight` |  |
| `Elements/DomainInfoData.cs` | 30 | `#808080` |  | `inactiveLight` |  |
| `Elements/GameCard.cs` | 31 | `#4C4C4C` |  | (a) | locked-card tint — §10.6 says 'grey', gives no value |
| `Elements/GameCard.cs` | 34 | `#FFFFFF` |  | (c) | multiply-identity white (untinted sprite) |
| `Elements/IconRotator.cs` | 25 | `#FFFFFF` |  | (c) | decorative rotator palette — violates §1 |
| `Elements/IconRotator.cs` | 26 | `#40F2FF` |  | (c) | decorative rotator palette — violates §1 |
| `Elements/IconRotator.cs` | 27 | `#FF4CD9` |  | (c) | decorative rotator palette — violates §1 |
| `Elements/IconRotator.cs` | 28 | `#A673FF` |  | (c) | decorative rotator palette — violates §1 |
| `Elements/IconRotator.cs` | 49 | `#FFFFFF` |  | (c) | multiply-identity white |
| `Elements/InputDeviceIconSetSwitcher.cs` | 73 | `#FFFFFF` |  | `textLight` | §10.5 active icon |
| `Elements/InputDeviceIconSetSwitcher.cs` | 74 | `#A6A6B2` |  | `inactiveLight` | §10.5 muted icon |
| `Elements/LoadoutCard.cs` | 20 | `#FFFFFF` |  | `inactiveLight` | SEE FLAG F2 — deselected is currently white |
| `Elements/OnlineInfoEntry.cs` | 39 | `#FFFFFF` |  | `cta` | §3 player online status; SEE FLAG F3 — currently white |
| `Elements/OnlineInfoEntry.cs` | 48 | `#FFFFFF` |  | (c) | multiply-identity white |
| `Elements/QuestItemCard.cs` | 42 | `#FFFFFF` |  | (c) | multiply-identity white |
| `Elements/RequestInfoEntry.cs` | 29 | `#FFFFFF` |  | `cta` | §2 attention reuses CTA; currently white |
| `Elements/ScoreNumberAnimator.cs` | 131 | `#33FF66` |  | (a) | positive/gain green — §2 gap table proposes no gain hue |
| `Elements/ScoreNumberAnimator.cs` | 132 | `#FF4C33` |  | (b) | HUDAnimationSettingsSO — value is danger (Δ0.008) |
| `GameToastSystem/GameToastAPI.cs` | 59 | `#FFFFFF` |  | (b) | SO_ColorSet — domain fallback |
| `GameToastSystem/GameToastController.cs` | 147 | `#FFFFFF` |  | `textLight` | non-domain toast text |
| `GameToastSystem/GameToastController.cs` | 205 | `#FFFFFF` |  | (b) | SO_ColorSet — domain fallback |
| `HUDAnimationSettingsSO.cs` | 34 | `#33FF66` |  | (a) | positive/gain green |
| `HUDAnimationSettingsSO.cs` | 36 | `#FF4C33` |  | (b) | HUDAnimationSettingsSO owns it; value is danger |
| `HUDAnimationSettingsSO.cs` | 46 | `#FF4C33` |  | (b) | HUDAnimationSettingsSO owns it; value is danger |
| `LeaderboardConfigSOEditor.cs` | 22 | `#4C80B2` | 0.30 | — | editor-inspector chrome |
| `LeaderboardConfigSOEditor.cs` | 23 | `#66B266` | 0.15 | — | editor-inspector chrome |
| `LeaderboardConfigSOEditor.cs` | 24 | `#808080` | 0.10 | — | editor-inspector chrome |
| `LeaderboardConfigSOEditor.cs` | 25 | `#CC9933` | 0.20 | — | editor-inspector chrome |
| `LeaderboardConfigSOEditor.cs` | 145 | `#80CC80` |  | — | editor-inspector chrome |
| `LeaderboardConfigSOEditor.cs` | 151 | `#CCB280` |  | — | editor-inspector chrome |
| `LeaderboardConfigSOEditor.cs` | 199 | `#4CCC4C` |  | — | editor-inspector chrome |
| `LeaderboardConfigSOEditor.cs` | 213 | `#99CCFF` |  | — | editor-inspector chrome |
| `LeaderboardConfigSOEditor.cs` | 290 | `#FF8080` |  | — | editor-inspector chrome |
| `MiniGameHUD.cs` | 119 | `#000000` | 0.50 | `surfaceBlack` | §10.3 scrim @50% |
| `MiniGameHUD.cs` | 137 | `#FFFFFF` |  | `textLight` |  |
| `MiniGameHUD.cs` | 572 | `#808080` |  | (b) | SO_ColorSet — domain fallback |
| `Modals/ArcadeGameConfigureModal.cs` | 1232 | `#FFD700` |  | — | debug/console markup |
| `Modals/ArcadeGameConfigureModal.cs` | 1288 | `#FFD700` |  | — | debug/console markup |
| `Modals/ArcadeGameConfigureModal.cs` | 1305 | `#FFD700` |  | — | debug/console markup |
| `Modals/ArcadeGameConfigureModal.cs` | 1320 | `#FF0000` |  | — | debug/console markup |
| `Modals/ArcadeGameConfigureModal.cs` | 1335 | `#FFD700` |  | — | debug/console markup |
| `Modals/GameSettingsPanelController.cs` | 53 | `#FFFFFF` |  | `textLight` | §10.8 active label |
| `Modals/GameSettingsPanelController.cs` | 54 | `#808080` |  | `inactiveLight` | §10.8 inactive label |
| `Modals/HangarTrainingModal.cs` | 76 | `#00FF00` |  | `cta` | selection; SEE FLAG F4 |
| `Modals/ModalWindowManager.cs` | 99 | `#000000` | 0.00 | (c) | Color.clear raycast target, not a colour |
| `Model/MinigameHUDInspector.cs` | 10 | `#173D7A` |  | — | editor-inspector chrome |
| `Model/MinigameHUDInspector.cs` | 11 | `#24385C` |  | — | editor-inspector chrome |
| `Model/MinigameHUDInspector.cs` | 12 | `#245236` |  | — | editor-inspector chrome |
| `Model/MinigameHUDInspector.cs` | 23 | `#339933` |  | — | editor-inspector chrome |
| `Model/MinigameHUDInspector.cs` | 24 | `#993399` |  | — | editor-inspector chrome |
| `Model/MinigameHUDInspector.cs` | 77 | `#FFFFFF` |  | — | editor-inspector chrome |
| `Model/MinigameHUDInspector.cs` | 93 | `#FFFFFF` |  | — | editor-inspector chrome |
| `ObjectiveArrowGraphic.cs` | 26 | `#4F802E` | 0.32 | (b) | SO_ColorSet — §3 objective arrow is TEAM colour; SEE FLAG F5 |
| `ObjectiveArrowGraphic.cs` | 29 | `#8CEB1A` | 0.95 | (b) | SO_ColorSet — §3 objective arrow is TEAM colour; SEE FLAG F5 |
| `ObjectiveArrowGraphic.cs` | 32 | `#DBFF9E` |  | (b) | SO_ColorSet — §3 objective arrow is TEAM colour; SEE FLAG F5 |
| `Privacy/PrivacyConsentOverlay.cs` | 109 | `#05050A` | 0.88 | `surfaceBlack` | scrim @88% (Δ0.02) |
| `Privacy/PrivacyConsentOverlay.cs` | 133 | `#E65C6B` |  | `danger` | E65C6B vs FF4B3A — SEE FLAG F6 |
| `Privacy/PrivacyConsentOverlay.cs` | 138 | `#299E85` |  | (c) | bespoke teal accept — pre-palette overlay |
| `Privacy/PrivacyConsentOverlay.cs` | 156 | `#474C59` |  | `inactiveLight` | 474C59 vs 5C5F70, Δ0.08 — SEE FLAG F6 |
| `Privacy/PrivacyConsentOverlay.cs` | 158 | `#299E85` |  | (c) | bespoke teal accept — pre-palette overlay |
| `Privacy/PrivacyConsentOverlay.cs` | 256 | `#171A21` | 0.99 | (c) | bespoke neutral dark — pre-palette overlay |
| `Privacy/PrivacyConsentOverlay.cs` | 295 | `#F0F2F7` |  | `textLight` | F0F2F7 vs E6E9FF, Δ0.04 — SEE FLAG F6 |
| `Privacy/PrivacyConsentOverlay.cs` | 302 | `#C2C7D4` |  | (a) | secondary body text — §11 has one text colour |
| `Privacy/PrivacyConsentOverlay.cs` | 309 | `#99A1B2` |  | (a) | tertiary/muted text — §11 has one text colour |
| `Privacy/PrivacyConsentOverlay.cs` | 336 | `#292B36` |  | (c) | bespoke neutral dark — pre-palette overlay |
| `Privacy/PrivacyConsentOverlay.cs` | 348 | `#F0F2F7` |  | `textLight` | F0F2F7 vs E6E9FF, Δ0.04 — SEE FLAG F6 |
| `Privacy/PrivacyConsentOverlay.cs` | 356 | `#737887` |  | (a) | input placeholder text — §10.2 does not spec it |
| `Privacy/PrivacyConsentOverlay.cs` | 383 | `#FFFFFF` |  | `textLight` |  |
| `Privacy/PrivacyConsentOverlay.cs` | 400 | `#000000` | 0.00 | (c) | alpha-0 click target, not a colour |
| `Privacy/PrivacyConsentOverlay.cs` | 407 | `#59B8F2` |  | (a) | hyperlink — no link colour in the palette |
| `ResourceDisplay.cs` | 39 | `#FFFFFF` |  | (a) | gauge normal fill — §11 has no gauge token |
| `ResourceDisplay.cs` | 40 | `#FF0000` |  | (a) | gauge full/threshold — not the same idea as danger |
| `Scoreboard.cs` | 592 | `#808080` |  | (b) | SO_ColorSet — domain fallback |
| `Screens/LeaderboardsMenu.cs` | 214 | `#1AB2B2` |  | (a) | local-player row highlight — §10.10 specs only a '*' |
| `Screens/LeaderboardsMenu.cs` | 215 | `#1AB2B2` |  | (a) | local-player row highlight |
| `Screens/LeaderboardsMenu.cs` | 216 | `#1AB2B2` |  | (a) | local-player row highlight |
| `Screens/LeaderboardsMenu.cs` | 220 | `#FFFFFF` |  | `textLight` | §10.10 row text |
| `Screens/LeaderboardsMenu.cs` | 221 | `#FFFFFF` |  | `textLight` | §10.10 row text |
| `Screens/LeaderboardsMenu.cs` | 222 | `#FFFFFF` |  | `textLight` | §10.10 row text |
| `Screens/PartyInviteNotificationPanel.cs` | 73 | `#FF00FF` |  | — | debug/console markup |
| `Screens/PartyInviteNotificationPanel.cs` | 79 | `#FF0000` |  | — | debug/console markup |
| `Screens/PartyInviteNotificationPanel.cs` | 146 | `#00FF00` |  | — | debug/console markup |
| `TestMiniGameEvents.cs` | 25 | `#00FFFF` |  | — | debug/console markup |
| `TestMiniGameEvents.cs` | 30 | `#00FFFF` |  | — | debug/console markup |
| `ThumbPerimeter.cs` | 23 | `#FFFFFF` |  | (c) | multiply-identity white |
| `ToastNotification/ToastNotificationManager.cs` | 152 | `#1A1A26` | 0.90 | (a) | toast surface — 1A1A26 is neutral; both §11 surfaces are blue-tinted |
| `ToastNotification/ToastNotificationManager.cs` | 163 | `#FFFFFF` |  | `textLight` |  |
| `MaelstromRoundCard.cs` | 67 | `#808080` |  | (b) | SO_ColorSet — domain fallback |
| `UniversalStatsProviderEditor.cs` | 17 | `#6699E6` |  | — | editor-inspector chrome |
| `UniversalStatsProviderEditor.cs` | 18 | `#4CCC66` |  | — | editor-inspector chrome |
| `UniversalStatsProviderEditor.cs` | 19 | `#E65959` |  | — | editor-inspector chrome |
| `UniversalStatsProviderEditor.cs` | 20 | `#808080` |  | — | editor-inspector chrome |
| `UniversalStatsProviderEditor.cs` | 303 | `#808080` |  | — | editor-inspector chrome |
| `UniversalStatsProviderEditor.cs` | 317 | `#99CC99` |  | — | editor-inspector chrome |
| `View/ControllerButtonIconReferences.cs` | 51 | `#FFFFFF` |  | (c) | multiply-identity white (fade reset) |
| `View/DolphinVesselHUDView.cs` | 92 | `#FFFFFF` |  | (b) | SO_ColorSet — team crystal fallback |
| `View/DolphinVesselHUDView.cs` | 94 | `#FFFFFF` |  | (b) | Dolphin HUD — armed flash state |
| `View/DolphinVesselHUDView.cs` | 122 | `#FFFFFF` |  | (c) | multiply-identity white (rest) |
| `View/DolphinVesselHUDView.cs` | 139 | `#FFFFFF` |  | (c) | multiply-identity white (rest) |
| `View/DolphinVesselHUDView.cs` | 169 | `#FFFFFF` |  | (c) | multiply-identity white (rest) |
| `View/MantaVesselHUDView.cs` | 17 | `#FFFFFF` |  | (c) | multiply-identity white (rest) |
| `View/MantaVesselHUDView.cs` | 18 | `#FFEB04` |  | (c) | Color.yellow highlight — never designed |
| `View/RhinoVesselHUDView.cs` | 27 | `#FFFFFF` |  | (c) | multiply-identity white (rest) |
| `View/RhinoVesselHUDView.cs` | 28 | `#00FF00` |  | (b) | Rhino HUD — crystal activated state |
| `View/RhinoVesselHUDView.cs` | 29 | `#FFFFFF` |  | (c) | multiply-identity white (rest) |
| `View/RhinoVesselHUDView.cs` | 30 | `#FF0000` |  | (b) | Rhino HUD — line activated state |
| `View/RhinoVesselHUDView.cs` | 31 | `#FFFFFF` |  | (c) | multiply-identity white (rest) |
| `View/RhinoVesselHUDView.cs` | 32 | `#00FFFF` |  | (b) | Rhino HUD — debuff active state |
| `View/SerpentVesselHUDView.cs` | 21 | `#FFFFFF` |  | (c) | multiply-identity white (pip full) |
| `View/SerpentVesselHUDView.cs` | 22 | `#4CFF4C` |  | (b) | Serpent HUD — pip consuming state |
| `View/SerpentVesselHUDView.cs` | 23 | `#FFFFFF` | 0.25 | (c) | white @25% = pip empty, an alpha not a hue |
| `View/SparrowHUDView.cs` | 49 | `#FF0000` |  | (b) | Sparrow HUD — blocked input; danger candidate |
| `View/SparrowHUDView.cs` | 75 | `#FFFFFF` |  | (c) | multiply-identity white |
| `View/SparrowHUDView.cs` | 202 | `#FFFFFF` |  | (c) | multiply-identity white |
| `View/SquirrelVesselHUDView.cs` | 42 | `#FFFFFF` |  | (c) | multiply-identity white (rest) |
| `View/SquirrelVesselHUDView.cs` | 44 | `#FF0000` |  | (b) | Squirrel HUD — joust flash state |
| `View/SquirrelVesselHUDView.cs` | 46 | `#66E6FF` |  | (b) | Squirrel HUD — crystal flash state |
| `View/SquirrelVesselHUDView.cs` | 52 | `#80808C` | 0.90 | (b) | Squirrel HUD — tube cooling state |
| `View/SquirrelVesselHUDView.cs` | 54 | `#FF3333` |  | (b) | Squirrel HUD — tube ready state |
| `View/SquirrelVesselHUDView.cs` | 63 | `#FFFFFF` |  | (c) | multiply-identity white (slam flash) |
| `View/SquirrelVesselHUDView.cs` | 71 | `#FF7326` |  | (b) | Squirrel HUD — overheat hot state |
| `View/SquirrelVesselHUDView.cs` | 73 | `#FFE699` |  | (b) | Squirrel HUD — overheat flash state |
| `View/SquirrelVesselHUDView.cs` | 91 | `#FFFFFF` |  | (b) | SO_ColorSet — player domain fallback |
| `View/SquirrelVesselHUDView.cs` | 92 | `#FFFFFF` |  | (c) | multiply-identity white |
| `View/SquirrelVesselHUDView.cs` | 93 | `#FFFFFF` |  | (c) | multiply-identity white |
| `View/SquirrelVesselHUDView.cs` | 115 | `#FFFFFF` |  | (c) | multiply-identity white |
| `View/SquirrelVesselHUDView.cs` | 230 | `#FFFFFF` |  | (c) | Lerp toward white = desaturation, not a colour |
| `View/SquirrelVesselHUDView.cs` | 247 | `#FFFFFF` |  | (c) | Lerp toward white = desaturation, not a colour |
| `View/SquirrelVesselHUDView.cs` | 283 | `#FF9933` |  | (b) | Squirrel HUD — double-drift state |
| `View/SquirrelVesselHUDView.cs` | 284 | `#B2E6FF` |  | (b) | Squirrel HUD — single-drift state |
| `View/UrchinVesselHUDView.cs` | 26 | `#FFFFFF` |  | (c) | multiply-identity white (rest) |
| `View/UrchinVesselHUDView.cs` | 27 | `#00FFFF` |  | (b) | Urchin HUD — ammo full state |
| `View/UrchinVesselHUDView.cs` | 33 | `#00FF00` |  | (b) | Urchin HUD — riding state |
| `View/VesselHUDView.cs` | 288 | `#FFFFFF` |  | (b) | ElementalBarsConfigSO.whiteColor already owns it |
| `Views/ArcadeLoadoutView.cs` | 88 | `#FFFFFF` |  | (c) | reset-to-identity; SEE FLAG F7 |
| `Views/ArcadeLoadoutView.cs` | 89 | `#FFFFFF` |  | (c) | reset-to-identity; SEE FLAG F7 |
| `Views/ArcadeLoadoutView.cs` | 90 | `#FFFFFF` |  | (c) | reset-to-identity; SEE FLAG F7 |
| `Views/ArcadeLoadoutView.cs` | 91 | `#FFFFFF` |  | (c) | reset-to-identity; SEE FLAG F7 |
| `Views/ArcadeLoadoutView.cs` | 200 | `#FFFFFF` |  | (c) | reset-to-identity; SEE FLAG F7 |
| `Views/ArcadeLoadoutView.cs` | 201 | `#FFFFFF` |  | (c) | reset-to-identity; SEE FLAG F7 |
| `Views/ArcadeLoadoutView.cs` | 239 | `#FFFFFF` |  | (c) | reset-to-identity; SEE FLAG F7 |
| `Views/ArcadeLoadoutView.cs` | 240 | `#FFFFFF` |  | (c) | reset-to-identity; SEE FLAG F7 |
| `Views/DailyChallengeLeaderboardView.cs` | 93 | `#1AB2B2` |  | (a) | local-player row highlight |
| `Views/DailyChallengeLeaderboardView.cs` | 94 | `#1AB2B2` |  | (a) | local-player row highlight |
| `Views/DailyChallengeLeaderboardView.cs` | 95 | `#1AB2B2` |  | (a) | local-player row highlight |
| `Views/DailyChallengeLeaderboardView.cs` | 99 | `#FFFFFF` |  | `textLight` | §10.10 row text |
| `Views/DailyChallengeLeaderboardView.cs` | 100 | `#FFFFFF` |  | `textLight` | §10.10 row text |
| `Views/DailyChallengeLeaderboardView.cs` | 101 | `#FFFFFF` |  | `textLight` | §10.10 row text |
| `Views/HangarCaptainsView.cs` | 99 | `#FFFFFF` |  | (c) | multiply-identity white |
| `Views/HangarCaptainsView.cs` | 113 | `#000000` |  | `surfaceBlack` | Color.black → 00010A |
| `Views/HangarVesselDetailView.cs` | 197 | `#FFFFFF` |  | `textLight` | button label |
| `Views/HangarVesselDetailView.cs` | 202 | `#FFFFFF` |  | `textLight` | button label |
| `Views/PortSquadCaptainSelectionView.cs` | 13 | `#808080` |  | `inactiveLight` | SEE FLAG F8 — 'Selected' is grey |
| `Views/PortSquadCaptainSelectionView.cs` | 14 | `#000000` |  | `surfaceBlack` | Color.black → 00010A |

---

**Reproduce:** the extractor and the verdict table are `Tools/Build/audit_ui_color_literals.py`.
Re-run it after any UI change that touches colour; a drifting count with no drifting definition
is how 165 became 184.
