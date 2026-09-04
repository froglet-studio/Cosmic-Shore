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
| STORE | Enabled (not in disabled list) |
| ARK | **Locked** (intentional — stays as-is) |
| PORT | **Locked** (intentional — stays as-is) |

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
