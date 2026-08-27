# Quest Track + Breadcrumb — Player-Activation Engine

_Cosmic Shore · Unity 6 / C#_

Two systems compose into one activation engine. The **Quest Track** is a chain of **Unlocks**;
each Unlock reveals a new app feature and is gated by a **Quest** (a completion condition). The
**Breadcrumb** (Call-to-Action) system highlights the app-shell element — and the nested path to
it — that the player should act on next. **The quest track decides WHAT'S NEXT; the breadcrumb
guides the player THERE.**

> Implements the "Quest Track + Breadcrumb" design. The progression spine already existed
> (`GameModeProgressionService` + cloud `GameModeProgressionData`); this work removed XP, retired
> the old quest stack, generalized the unlock data model, and wired the spine to drive the
> breadcrumb.

## Locked constraints (invariants)

- **C1 — XP REMOVED ENTIRELY.** Quest completion is the only progression currency. No XP storage,
  display, award, or gating. (The cloud field `PlayerProfileData.xp` is left dormant for
  back-compat — never written for progression, never read.)
- **C2 — SINGLE GUIDANCE CHANNEL.** Every in-app "go here / do this" hint routes through the
  CTA/breadcrumb system, driven solely by `GameModeProgressionService`.
- **C3 — CONTINUITY LAW.** CTA indicators bloom/grow on activate and wither/fade on dismiss — they
  never pop instantly into or out of existence.
- **C4 — UNLOCKS PERSISTENT & MONOTONIC.** Once revealed, a feature stays unlocked across sessions
  (cloud-persisted in `GameModeProgressionData` via `UGSDataService.ProgressionRepo`, key
  `GAME_MODE_PROGRESSION`). One authoritative source of truth.

## Architecture (runtime)

```
SO_UnlockList ──┐                         ┌── CallToActionTarget (app-shell element)
SO_UnlockData ──┤                         │     bloom on activate / wither on dismiss (C3)
SO_ProgressionConfig ─► GameModeProgressionService ──(KEY WIRE)──► CallToActionSystem
                          │  spine · DontDestroyOnLoad             │  AddCallToAction
                          │  cloud-persisted (C4)                  │  RemoveCallToAction
                          ├─► OnProgressionChanged ─► QuestTrackView / QuestItemCard
                          └─► GameModeProgressionData ─► UGSDataService.ProgressionRepo
                                                          ▲
UserActionSystem.OnUserActionCompleted ───────────────────┘ (dismisses CTAs whose
                                                              CompletionUserAction matches)
```

### Components

| Role | Type | File |
|---|---|---|
| Progression spine (sole breadcrumb driver) | `GameModeProgressionService` | `_Scripts/System/Progression/` |
| Persisted record (C4) | `GameModeProgressionData` | `_Scripts/System/Progression/` |
| Cloud repo | `UGSDataService.ProgressionRepo` | `_Scripts/System/CloudData/` |
| Unlock node (generalized) | `SO_UnlockData` + `FeatureKind` enum | `_Scripts/ScriptableObjects/` |
| Ordered chain | `SO_UnlockList` | `_Scripts/ScriptableObjects/` |
| Tunables | `SO_ProgressionConfig` | `_Scripts/ScriptableObjects/` |
| Breadcrumb engine | `CallToActionSystem` | `_Scripts/System/CallToAction/` |
| Breadcrumb target (on app-shell elements) | `CallToActionTarget` | `_Scripts/System/CallToAction/` |
| Breadcrumb instruction (data) | `CallToAction` | `_Scripts/System/CallToAction/` |
| User-action signal | `UserActionSystem` + `UserActionType` | `_Scripts/System/UserAction/` |
| Quest track UI | `QuestTrackView` / `QuestItemCard` | `_Scripts/UI/` |

## The generalized Unlock model

`SO_UnlockData` (formerly `SO_GameModeQuestData`; script GUID preserved so authored assets keep
their data) is one node = **{ feature revealed, gating quest, breadcrumb target(s) }**.

- **`FeatureKind`** ∈ `{ GameMode, Vessel, IntensityTier, Screen, Captain, Episode, UIElement }`.
  `GameMode` keys by `GameMode.ToString()`; other kinds key by `FeatureRef`. `UnlockKey` resolves this.
- **Gate:** `TargetType` (`QuestTargetType`) + `TargetValue`, plus the intensity-unlock fields.
- **Breadcrumb:** `CallToActionTargetID`, `CompletionUserAction`, `DependencyTargetIDs[]`.
  `HasBreadcrumb` and `BuildCallToAction()` turn the authored fields into a `CallToAction`.

**Extension points:** new feature → add to `FeatureKind`; new completion condition → add to
`QuestTargetType` + its evaluator in `GameModeProgressionService`; new breadcrumb target → add to
`CallToActionTargetType` + put a `CallToActionTarget` on the app-shell element.

## The KEY WIRE — progression drives the breadcrumb

`GameModeProgressionService` funnels **every** progression mutation through
`RaiseProgressionChanged()`, which fires `OnProgressionChanged` and then `RefreshActiveBreadcrumb()`:

1. `GetActiveFrontierUnlock()` finds the first chain node that is **reachable** (`IsUnlockReachable`)
   but not **done** (`IsUnlockObjectiveDone`) and that `HasBreadcrumb`.
2. If that target differs from the currently-lit one, the previous `CallToAction` is retracted
   (`CallToActionSystem.RemoveCallToAction`) and the new one is lit (`AddCallToAction`).

This is the single guidance channel (C2): exactly one frontier breadcrumb is live at a time.

### Frontier ownership & `CompletionUserAction = None`

Game-mode quests are multi-play ("reach intensity 4"). Their breadcrumbs are **progression-owned**:
authored with `CompletionUserAction = None`, so they are **not** dismissed after a single play —
they stay lit until the mode's objective is met, then the service retracts them on frontier advance.
(Relying on a `PlayGame` user-action to dismiss-then-re-light was rejected: the `PlayGame` action and
the `OnMiniGameEnd` progression delta fire from different classes with no guaranteed order, which
would leave the breadcrumb dark while the mode is still unfinished.)

One-shot guides (e.g. the Vessel Hangar reveal) **do** use a specific `CompletionUserAction`
(`ViewHangarMenu`) so opening the screen dismisses the indicator.

### Continuity (C3)

`CallToActionTarget` blooms its `ActiveIndicator` (scale-from-~0 + `CanvasGroup` fade-in) on
activate, runs a **looping glow pulse** (alpha yoyo, or a subtle scale yoyo when the indicator has
no `CanvasGroup`) while active, and withers it (shrink + fade-out → `SetActive(false)`) on dismiss —
all via DOTween with `SetUpdate(true)` (works while the menu pauses time). On scene re-entry an
already-active breadcrumb is shown immediately (no re-bloom) but resumes the pulse, since it
persisted — it didn't "pop into existence." The stock indicator sprite is
`Assets/_Graphics/UI/CTA_Glow_Outline_Green.png` (green, 9-slice outline glow).

## Authored content — the live chain

`Assets/_SO_Assets/GameModeQuest/` (`GameModeQuestList.asset` order):

| # | Asset | Feature | Gate | Breadcrumb target | Dep |
|---|---|---|---|---|---|
| 0 | CrystalCapture (free) | GameMode 35 | Intensity 4 | PlayGameMultiplayerCrystalCapture (433) | ArcadeMenu |
| 1 | HexRace | GameMode 33 | Intensity 4 | PlayGameHexRace (431) | ArcadeMenu |
| 2 | Joust | GameMode 34 | Intensity 4 | PlayGameMultiplayerJoust (434) | ArcadeMenu |
| 3 | PartyGame | GameMode (placeholder) | — | none (placeholder) | — |
| 4 | VesselHangar | **Screen** | all prior done | HangarMenu (300), dismiss on ViewHangarMenu | — |

Node 5 (a `Screen`, not a mode) proves the "any feature" generalization. Its reachability is the
project's own `IsVesselHangarUnlocked()` — fixed in this work to test the **persistent** done signal
(a completed quest stays at max intensity) instead of the transient `CompletedQuests` set, which a
claim empties (the prior conjunction was unsatisfiable, so the hangar — and its breadcrumb — never
unlocked).

## In-editor wiring checklist (required to see it run)

The code + data are committed, but these live-scene steps need the Unity editor:

1. **Progression GameObject** (DontDestroyOnLoad): assign `SO_UnlockList`
   (`GameModeQuestList.asset`) to `GameModeProgressionService.questList` and
   `ProgressionConfig.asset` to `progressionConfig`.
2. **CallToActionTarget on each app-shell element** in `Menu_Main` (and wherever the targets live):
   one per `CallToActionTargetType` used — `ArcadeMenu`, `HangarMenu`, and the `PlayGame*` cards
   (`433/431/434/427`). Wire each target's `ActiveIndicator`. For the fade, give the
   `ActiveIndicator` a `CanvasGroup` (scale-only bloom works without one, but the fade needs it).
3. Confirm `CallToActionSystem` and `UserActionSystem` singletons exist at runtime (Bootstrap).
4. Confirm the menu nav fires the right `UserAction`s (`ViewHangarMenu` etc.) so one-shot guides
   dismiss; game launches already fire `PlayGame`.

## v1 boundaries (intentional)

- The breadcrumb frontier is computed over game-mode nodes plus the hangar Screen node. Other
  `FeatureKind`s (Vessel/Captain/Episode) are modeled in data and resolvable via `UnlockKey`, but
  driving them as live frontiers is a follow-up.
- Dependency targets (e.g. `ArcadeMenu`) stay lit for the lifetime of their owning frontier CTA
  rather than individually fading on intermediate navigation — the existing CTA dependency model is
  a refcount tied to the parent CTA, not per-step completion.

## Retired (do not reintroduce)

- **XP (C1):** `ParticipationXpAwarder`, `XPTrackView`, `SO_XPTrackData`, `SO_XPTrackReward`, the
  `PlayerDataService` XP API, `SO_ProgressionConfig.participationXpPerGame`.
- **Old quest stack:** `Quest`, `QuestSystem`, `UserJourneySystem`, `SO_QuestChain` — the breadcrumb
  used to be driven by this stubbed/test stack; it is now driven by the progression spine.
