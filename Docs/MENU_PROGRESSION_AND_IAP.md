# Main Menu — Screens, Unlock Spec & Web-Checkout IAP

This document covers three things that were investigated and reworked together:

1. **Which menu screens are live** and what "bringing back Hangar / Home / Profile" actually requires.
2. **The exact specification for how games (and vessels) unlock** — and the new config SO that makes the rules tunable without code.
3. **The web-checkout IAP flow** for buying episodes as "support" on Steam/PC.

---

## 1. Menu screens — current state

Navigation is driven by `ScreenSwitcher` (`Assets/_Scripts/UI/ScreenSwitcher.cs`). Screen enum:

```
STORE = 0, ARK = 1, HOME = 2, PORT = 3, HANGAR = 4, PROFILE = 5
```

In `Menu_Main.unity` the `ScreenSwitcher.disabledScreens` list = **{ ARK, PORT }**. So:

| Screen | State |
|---|---|
| HOME | **Enabled** |
| HANGAR | **Enabled** |
| PROFILE | **Enabled** |
| STORE | **Has no entry in `screens` at all** — see the note below |
| ARK | **Locked** (intentional — stays as-is). **This is the screen that hosts `StoreScreen`** |
| PORT | **Locked** (intentional — stays as-is) |

> ⚠ **`MenuScreens.STORE` is a dangling enum value.** `ScreenSwitcher.screens` contains
> `{HANGAR, ARK, HOME, PORT, PROFILE}` and no `STORE` entry, while the store's content
> (`StoreScreen` + its purchase cards) lives on the **`ArkScreen`** GameObject. So `NavigateTo(STORE)`
> used to fall through `GetIndexForScreen`'s warning path to index 0 and land the player on the
> **Hangar** — which is what the two `Go To Store Button`s did. Since the commerce de-scope (§6)
> `IsScreenDisabled(STORE)` is true, so that press refuses instead of misnavigating.

**Key point:** Hangar, Home, and Profile are *not* code-disabled. There is nothing in code to "turn back on." If a screen appears empty/missing, it is a **scene/prefab wiring** issue (the rich Profile/Episode widgets historically lived in the `MIgration_Prefabs (DELETE LATER)/` prefabs and may not be instantiated in the live `Menu_Main` `ProfileScreen` root). See the wiring checklist in §4.

### Hangar — already the "development" design

The grid-based Hangar (`HangarVesselGridCard` + `HangarVesselDetailView` + `VesselUnlockSystem`) **originated on `development` and is already present on this branch** — the two branches share no history, but the Hangar C# is ~90–97% identical. The only behavioral difference has been restored:

- `HangarScreen.RefreshGridCards()` now **re-populates** the grid on unlock (cards re-sort unlocked-first), matching development, instead of only flipping the lock overlay in place.

Do **not** remove `HangarScreen : IScreen` or the `_lastLoadFrame` double-load guard — those are required by this branch's `ScreenSwitcher` and are not present on development for that reason.

---

## 2. How unlocks ACTUALLY work (the exact spec)

> Games unlock through a **quest chain**; vessels unlock by **spending crystals**;
> intensity tiers unlock by **playing**.

### 2a. Arcade game modes — quest chain

A game card is **locked** iff `!GameModeProgressionService.IsGameModeUnlocked(mode)` (checked in `ArcadeExploreView.PopulateGameSelectionList`). `IsGameModeUnlocked(mode)` returns true when **any** of:

1. `mode` is in `SO_ProgressionConfig.alwaysUnlockedModes` (default: `Maelstrom`), **or**
2. `SO_ProgressionConfig.firstQuestAlwaysUnlocked` is true **and** `mode` is the first quest in the chain ("the first game is free"), **or**
3. `mode` is in the cloud-saved `ProgressionData.UnlockedModes` set.

A mode enters `UnlockedModes` only when the player **completes the previous quest's goal** *and* **claims it** on the quest UI (`ClaimQuestAndUnlockNext` → `MarkUnlocked(nextMode)`). State is cloud-persisted under UGS Cloud Save key for progression.

**The live quest chain** (`Assets/_SO_Assets/GameModeQuest/GameModeQuestList.asset`):

| Order | Quest asset | DisplayName | GameMode | TargetType | Target |
|---|---|---|---|---|---|
| 0 (free) | GameModeQuest_Scurry | CRYSTAL CAPTURE | 35 | IntensityUnlocked | 4 |
| 1 | GameModeQuest_SkimRace | HEX RACE | 33 | IntensityUnlocked | 4 |
| 2 | GameModeQuest_Joust | JOUST | 34 | IntensityUnlocked | 4 |
| 3 | GameModeQuest_WildlifeBlitz | WILDLIFE BLITZ | 26 | IntensityUnlocked | 4 |
| 4 | GameModeQuest_PartyGame | PARTY GAME | 35 | Placeholder | 30 |
| 5 | VesselHangarUnlock | VESSEL HANGAR | (feature) | Placeholder | — |

Per-quest goal type/value lives on `SO_GameModeQuestData` (`TargetType` + `TargetValue`) — already a ScriptableObject. Unlock order = the list order in `SO_GameModeQuestList`. **To change which game unlocks when, edit those assets** (no code).

### 2b. Intensity tiers (1–4)

A second, finer gate. Intensities 1 & 2 are available the moment a mode unlocks (`defaultMaxIntensity`). Tiers 3 and 4 unlock by either:

- **play count** — `SO_GameModeQuestData.PlaysToUnlockIntensity3 / 4` games at the previous tier, or
- **stat goal** — `Intensity3StatTarget / Intensity4StatTarget` when `IntensityUnlockStatType` is set.

The locked intensity buttons live in `ArcadeGameConfigureModal` (`IsIntensityUnlocked`). Unlocking tier 4 also completes the mode's quest.

### 2c. Vessels (Hangar)

- **Access to the Hangar feature** is gated by the quest chain: `IsVesselHangarUnlocked()` returns true once every quest *before* the quest named `SO_ProgressionConfig.vesselHangarQuestDisplayName` ("VESSEL HANGAR") is completed.
- **Individual vessels** are gated by **crystals**, not quests. Lock state + price live on the `SO_Vessel` asset (`isLocked`, `UnlockCost`, default 100). `VesselUnlockSystem.TryPurchaseVessel` spends crystals via `PlayerDataService.TrySpendCrystals` and persists the unlock to the Hangar cloud repo.

---

## 3. `SO_ProgressionConfig` — the new tunable config

`Assets/_Scripts/ScriptableObjects/SO_ProgressionConfig.cs`
Asset: `Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset`

It centralizes the values that were **previously hardcoded** in `GameModeProgressionService`:

| Field | Default | Replaces hardcoded… |
|---|---|---|
| `alwaysUnlockedModes` | `[Maelstrom]` | `if (mode == GameModes.Maelstrom) return true` |
| `firstQuestAlwaysUnlocked` | `true` | the `Quests[0]` "first is free" check |
| `defaultMaxIntensity` | `2` | the intensity floor (`= 2`) in `GameModeProgressionData` |
| `maxIntensity` | `4` | the `DebugSetMaxIntensity` clamp ceiling |
| `fullIntensityModes` | `[Maelstrom]` | `if (mode == GameModes.Maelstrom) return 4` |
| `vesselHangarQuestDisplayName` | `"VESSEL HANGAR"` | the magic string in `IsVesselHangarUnlocked` |

**Wiring:** assign `ProgressionConfig.asset` to:
- `GameModeProgressionService.progressionConfig` (the DontDestroyOnLoad progression GameObject), and

When **unwired**, both fall back to built-in defaults that reproduce the previous behavior exactly — so wiring is purely additive and safe to defer.

> Note: the 4-tier intensity ladder (1&2 free, 3, 4) assumes `defaultMaxIntensity = 2`.
> The cap (`maxIntensity`) and which modes ignore gating (`fullIntensityModes`) are freely
> tunable; changing the floor away from 2 would need the tier state machine generalized.

---

## 4. Bringing back the Profile screen content (Editor checklist)

The Profile screen root exists (`screens` index 4 → `ProfileScreen`), but the rich content needs to be present/wired in the live scene. In Unity:

1. Open `Assets/_Scenes/Menu_Main.unity`.
2. Select the **ProfileScreen** root under the Screens container.
3. Ensure it (or its children) carry:
   - `ProfileScreen` (`Assets/_Scripts/UI/Views/ProfileScreen.cs`) — wire `displayNameText`, `avatarImage`.
   - `EpisodeScreen` (`Assets/_Scripts/UI/Screens/EpisodeScreen.cs`) — wire `episodeList` → `EpisodeList.asset`, `cardContainer`, `episodeCardPrefab` (`EpisodePrefab.prefab`), `supportUsButton`, `episodePanel`.
4. Leave `ScreenSwitcher.disabledScreens` as **{ ARK, PORT }**.

(The prior content lives in `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/` — `Screens.prefab` / `UI.prefab` — if you need a reference for how it was assembled.)

---

## 5. Web-checkout IAP (buy episodes as "support")

> ⛔ **DE-SCOPED for the invite build (2026-09-11).** Everything in this section is wired and
> **switched off** — `IAPManager.OpenCheckout` declines, no screen offers a purchase, and no path
> reaches `PurchaseConfirmationModal`. Read §6 before changing anything here. The verification gap
> below is *why* it is off, not merely a scheduling accident.

There is **no in-app store SDK** and (deliberately) no new dependency. Purchases open a hosted checkout page in the **system browser** via `Application.OpenURL`, which works on Steam/PC and mobile.

### Pieces

- `SO_IAPConfig` (`Assets/_Scripts/ScriptableObjects/SO_IAPConfig.cs`, asset `Assets/_SO_Assets/XP/IAPConfig.asset`) — checkout URLs, currency symbol, display labels. URL templates support `{productId}` and `{price}` tokens.
- `SO_EpisodeData` gained `priceUsd` (real-money price) and an optional per-episode `checkoutUrl`.
- `IAPManager` (`Assets/_Scripts/System/IAPManager.cs`) — rewritten from a stub into a web-checkout manager:
  - `InitiateEpisodePurchase(SO_EpisodeData)` — opens that episode's checkout at its price.
  - `InitiateSupportPurchase()` — opens the generic support page.
  - `OnCheckoutOpened`, `OnReturnedFromCheckout` (fires when the app regains focus mid-checkout), `OnPurchaseComplete`.
  - `ConfirmPendingPurchase(bool)` — the **single entitlement-grant seam**.
- `EpisodeScreen` — an episode with `priceUsd > 0` renders its price (`$X.XX`) and its card button opens the web checkout. Episodes with `priceUsd == 0` keep their existing play/availability behavior.

### Wiring

1. Assign `IAPConfig.asset` to the `IAPManager` GameObject's `config` field (the IAPManager is the DI-registered singleton referenced by `AppManager.iapManager`).
2. Set `checkoutBaseUrl` / `supportUrl` to the real payment page when ready (default points at `https://www.froglet.games`).

   > ⚠️ A plain site URL (like the marketing homepage) opens the **website**, not a payment
   > form. To show an actual card-entry **payment screen**, this must be a **hosted checkout**
   > URL — e.g. a Stripe Payment Link, a Ko-fi/PayPal page, or a `froglet.games/checkout` page
   > that embeds a payment processor. `Application.OpenURL` cannot render a payment UI itself.
3. On each purchasable `SO_EpisodeData`, set `priceUsd` (the "X dollars") and optionally a per-episode `checkoutUrl`.

### ⚠️ Verification gap (read before shipping money)

External-browser checkout returns **no receipt inside the client**. `ConfirmPendingPurchase(true)` is the only place an entitlement is granted, and today nothing calls it automatically. **Do not auto-grant on `OnReturnedFromCheckout`** for real money — wire `ConfirmPendingPurchase` to a backend order-verification step (query the payment provider for the order's paid status, then grant) so the grant is server-authoritative. Until then, the flow opens the page and the player completes payment on the web; entitlement delivery is a follow-up once a backend exists.

### Future option: in-app webview

`SO_IAPConfig.openInExternalBrowser` is a flag for a later in-app webview path. Rendering checkout in-process needs a webview plugin (e.g. Vuplex / UniWebView / gree) — none is installed, and adding one is a dependency decision. External browser is the no-dependency default.

---

## 6. The invite-build commerce de-scope (`Docs/STEAM_RELEASE_TASKS.md` R4)

Nothing in the invite build sells anything. The surfaces that would take money are **kept, wired and
switched off** — not deleted — so the paid-EA conversion is one asset edit. This is checklist item
**C4**; it is load-bearing because the invite build is the first thing anyone outside the studio
sees, and a purchase screen that half-works reads as *broken* rather than as *unfinished*.

### 6.1 One authority, and it is not a second locked look

| Piece | Where |
|---|---|
| The posture | `Resources/CommerceAvailability` (`SO_CommerceAvailability`) — **the only place to flip** |
| The look + the refusal | `MenuAvailabilityView`, unchanged (`Docs/HomeHub/ARCHITECTURE.md` §2) |
| Per-control marker | `CommerceAffordance` — carries *which surface*, never what state |
| The tests | `CommerceDeScopeTests` |

Shipped posture:

| Surface | State | Why that state |
|---|---|---|
| `StoreScreen` | `Unavailable` | Nothing in it is purchasable this window, and `MenuScreens.STORE` is not even a screen (§1) — *this is not built* is literally true |
| `Episodes` | `Locked` | The episodes exist as content and the entitlement is real; the player cannot buy one **yet** |
| `CatalogPurchase` | `Locked` | Every path to `PurchaseConfirmationModal` — the Store's cards and the Hangar's captain upgrade |
| `AllowRealMoneyCheckout` | `false` | The verification gap in §5 is unresolved, so the service declines at `OpenCheckout` |

### 6.2 What actually reached a money surface — the live path

The important finding, because it was reachable from a **cold boot** with no flags:

```
Menu_Main → nav to PROFILE → press "UnlockVesselButton"
  → EpisodeScreen.ShowPanel()        (UnityEvent invokes it on the INACTIVE panel, which it activates)
  → two "Support Us" cards + a Support Us button
  → IAPManager.InitiateSupportPurchase() → Application.OpenURL(…)
```

`UnlockVesselButton` (on `ProfileScreen`, active) despite its name does **not** unlock a vessel — its
only persistent listener is `EpisodeScreen.ShowPanel`. It now carries a `CommerceAffordance`
(`surface: Episodes`) so it dims from frame 0, and `ShowPanel` / `TogglePanel` refuse with a reason,
so the panel does not open.

### 6.3 Where each gate sits, and why there are two layers

**Presentation** — so the player can see the entry is deliberate, not dead:

- the `STORE` nav entry, via `ScreenSwitcher.MarkDisabledNavLinks` reading the config;
- `UnlockVesselButton`, via the scene-authored `CommerceAffordance`;
- `HangarCaptainsView`'s `UpgradeButton` and `GoToStoreButton`, marked at `Start`;
- episode cards and the Support Us button, marked when the panel is populated.

**Action** — so an un-gated or re-enabled screen still cannot transact:

- `PurchaseCard.OnClickBuy` and `HangarCaptainsView.OnClickBuy` — the **only** two paths to
  `PurchaseConfirmationModal`, asserted by `CommerceDeScopeTests.OnlyTheKnownPathsOpenThePurchaseModal`;
- `HangarCaptainsView.PurchaseUpgrade`, because it is public and a scene could wire a button to it;
- `IAPManager.OpenCheckout`, the choke point both checkout entry points share.

Same split `OfflineUIGate` records: gate the UI so a player is never offered something that cannot
work, and never rely on the UI alone to enforce it.

### 6.4 The soft-currency loop is NOT commerce and is untouched

Crystals are earned from match placement (`Scoreboard` → `PlayerDataService.AddCrystals`) and spent
on vessel unlocks (`VesselUnlockSystem.TryPurchaseVessel` → `TrySpendCrystals`). That is a **UGS**
loop; it is part of the invite build and it passes through none of the de-scoped surfaces.
`CommerceDeScopeTests.SoftCurrencyLoopIsUntouched` fails if any of those three files ever starts
consulting the de-scope.

**The Hangar's *captain upgrade* is a different economy and IS de-scoped.** It is priced in the
PlayFab catalog (`CatalogManager`), which `AuthenticationManager` — the legacy PlayFab auth — has to
initialize, and that is inert since the UGS migration. So `GetCaptainUpgrade` returns null, the
requirement checks never pass, and the upgrade *was already refusing* with a bare denied sting and no
explanation. That is exactly the "reads as broken" state this item exists to fix, so it now refuses
with a reason. It is not the crystal loop above, and locking it costs the invite build nothing.

### 6.5 The scene ALSO blocks most of this today — do not mistake that for the gate

Independently of the code gates, `Menu_Main` currently has: `StoreScreen` with `m_Enabled: 0`, its
content root `ArkScreen/Scroll View` inactive, `ARK` in `disabledScreens`, `HangarCaptainsView` under
two inactive ancestors, and the `EpisodeScreen` panel inactive. Several gates are therefore
**unreachable today**. They are kept deliberately: the scene state is not a decision anybody recorded
and a single re-activation undoes it, whereas the config is.

### 6.6 To convert at paid-EA

Edit `Resources/CommerceAvailability`: set each surface to `Available` and `allowRealMoneyCheckout`
to true. Nothing else has to change — but **do not set `allowRealMoneyCheckout` until
`ConfirmPendingPurchase` is backed by server-side order verification** (§5). `CommerceDeScopeTests`
asserts the de-scoped posture and will fail on the conversion commit; update it then, deliberately.

### 6.7 Known remaining items

- A second `Go To Store Button` exists under `Hangar_Panel/AbilitiesView`, both inactive, so it is
  unreachable and carries no marker. If that view is ever re-enabled it will be an undimmed button —
  its press already refuses via `IsScreenDisabled(STORE)`.
- `IAPManager` is still **started** (an active GameObject in `Bootstrap.unity`, with `config` null so
  it falls back to `SO_IAPConfig`'s defaults). It is left running on purpose: it stays DI-resolvable
  and keeps formatting prices, and `OpenCheckout` is where the posture is enforced.
- The dead copies — `MIgration_Prefabs (DELETE LATER)/{Screens,ModalWindows,IAPManager}.prefab` and
  `UI Elements/Main Menu Screens/Store Screen.prefab` — are referenced by nothing and were left alone.
