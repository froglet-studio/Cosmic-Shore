# Quest Track + Breadcrumb — Player-Activation Engine

_Cosmic Shore · Unity 6 / C# · Froglet Inc._

**Design visual (Claude design tool):** <https://claude.ai/design/p/e205f3d2-aa6a-41b6-985e-e4d8f8ee17b9?file=Quest+Track+%2B+Breadcrumb.dc.html>
**Engineer handoff / work plan:** [`HANDOFF.md`](./HANDOFF.md)

Two systems compose into one activation engine. The **Quest Track** is a chain of
**Unlocks**; each Unlock reveals a new app feature and is gated by a **Quest** (a
completion condition). The **Breadcrumb** (Call-to-Action) system can highlight ANY
element of the app shell — including the whole nested path to it — to guide the player
to whatever they need to do next. **The quest track decides WHAT'S NEXT; the breadcrumb
guides the player THERE.**

This doc is the canonical home for the design. It records both the **current** code
reality and the **target** unified design. Each item below is tagged:

- **[SHIPPED]** — exists and works today.
- **[TARGET]** — part of this design, not yet built.
- **[RETIRE]** — exists today but is being deleted/replaced by this design.

> This is the design of record, authored from the visual aid above. The work to reach
> the target state is sequenced in [`HANDOFF.md`](./HANDOFF.md).

---

## Locked constraints (invariants — do not violate)

- **C1 — XP REMOVED ENTIRELY.** Quest completion is the only progression currency. No
  XP storage, display, award, or gating anywhere. (Earlier the track was the "XP track";
  XP is now gone, not made cosmetic.)
- **C2 — SINGLE GUIDANCE CHANNEL.** Every in-app "go here / do this" hint routes through
  the CTA/breadcrumb system. No bespoke per-feature arrows, highlights, or one-off
  tutorial overlays.
- **C3 — CONTINUITY LAW.** Unlock reveals and CTA highlight indicators must
  bloom / grow / fade over a visible transition; nothing pops instantly into or out of
  existence (this is the platform-wide continuity law — see root `CLAUDE.md`).
- **C4 — UNLOCKS PERSISTENT & MONOTONIC.** Once revealed, a feature stays unlocked across
  sessions (cloud-persisted). Progression is forward-only except via explicit debug/reset.
  Exactly ONE authoritative, cloud-persisted source of truth.

---

## Legend (both layers)

| Glyph | Meaning |
|---|---|
| ⬡ Unlock node | a single `{ feature, gating Quest, breadcrumb Target(s) }` |
| ⬢ Quest gate / asset | completion condition / authorable ScriptableObject |
| ◎ Breadcrumb target | app-shell element the CTA lights (blooms/fades — C3) |
| ▤ Persisted state | cloud-saved record |
| ▬ Key new wire | progression service → breadcrumb (the gap this design closes) |
| → Event edge | runtime event between nodes |
| ⇢ Dependency / data | CTA dependency path / asset feed (dashed) |
| ⊘ Deprecated · RETIRE | to be deleted |
| `[C#]` Constraint anchor | invariant governing that node/edge |

---

## Layer A — Architecture & data flow (engineer-facing)

### Runtime services

- **`GameModeProgressionService`** `[SHIPPED, generalize for TARGET]` `[C1·C4]` — the
  canonical spine. Runtime, `DontDestroyOnLoad`, cloud-persisted via
  `UGSDataService.ProgressionRepo`. Loads the quest list + config, evaluates quest
  conditions against game results (`HandleGameEnd` → `ReportQuestStat` /
  `RecordIntensityPlay`), owns the progression record, emits progression events, and
  **[TARGET]** drives the breadcrumb for the active frontier quest. Generalize so an
  Unlock reveals ANY feature, not just game modes.
  - File: `Assets/_Scripts/System/Progression/GameModeProgressionService.cs`
  - Events: `OnProgressionChanged(GameModeProgressionData)`,
    `OnQuestCompleted(SO_GameModeQuestData)`, `OnIntensityUnlocked(GameModes, int)`.
- **`CallToActionSystem`** `[SHIPPED]` `[C2·C3]` — the breadcrumb engine.
  `RegisterCallToActionTarget`, `AddCallToAction`, `IsCallToActionTargetActive`. Supports
  **dependency targets** so it can light the whole nested path to a target.
  - File: `Assets/_Scripts/System/CallToAction/CallToActionSystem.cs`
- **`UserActionSystem`** `[SHIPPED]` — resolves CTAs & quest conditions. Emits
  `OnUserActionCompleted(UserAction / UserActionType)`.
  - Files: `Assets/_Scripts/System/UserAction/UserActionSystem.cs`, `UserAction.cs`,
    `UserActionTrigger.cs`
- **`VesselUnlockSystem`** `[SHIPPED]` — a parallel unlock mechanism folded into the
  model. `TryPurchaseVessel` spends crystals; `SO_Vessel.isLocked` / `UnlockCost`.

### Authorable data assets (ScriptableObjects)

- **`SO_GameModeQuestList`** `[SHIPPED → SO_UnlockList for TARGET]` — ordered `Quests[]`.
  ► **Designers add an unlock node here.** Asset:
  `_SO_Assets/GameModeQuest/GameModeQuestList.asset`.
  - File: `Assets/_Scripts/ScriptableObjects/SO_GameModeQuestList.cs`
- **`SO_GameModeQuestData`** `[SHIPPED → SO_UnlockData for TARGET]` — per-Unlock data:
  `GameMode`, `DisplayName`, `Description`, `Icon`, `TargetType` (`QuestTargetType`),
  `TargetValue`, intensity-unlock fields, `Order`, `IsPlaceholder`. **[TARGET]**
  generalize to carry `{ FeatureKind, FeatureRef, gating Quest, BreadcrumbTargetIDs }`.
  - File: `Assets/_Scripts/ScriptableObjects/SO_GameModeQuestData.cs`
- **`SO_ProgressionConfig`** `[SHIPPED]` — tunables: `alwaysUnlockedModes`,
  `firstQuestAlwaysUnlocked`, `defaultMaxIntensity`, `maxIntensity`, `fullIntensityModes`,
  `vesselHangarQuestDisplayName`. Asset: `_SO_Assets/GameModeQuest/ProgressionConfig.asset`.
  - File: `Assets/_Scripts/ScriptableObjects/SO_ProgressionConfig.cs`

### Persisted state — inside the CLOUD PERSISTENCE BOUNDARY `[C4]`

- **`GameModeProgressionData`** `[SHIPPED]` `[C1·C4]` — `UnlockedModes`,
  `CompletedQuests`, `BestStats`, `MaxUnlockedIntensity`, `IntensityPlayCounts`. The ONE
  authoritative record. **No XP fields** (C1).
  - File: `Assets/_Scripts/System/Progression/GameModeProgressionData.cs`
- **`UGSDataService.ProgressionRepo`** `[SHIPPED]` — UGS cloud save/load; monotonic,
  forward-only.

### UI

- **`QuestTrackView`** `[SHIPPED]` — renders the track; subscribes to
  `OnProgressionChanged`; reveals nodes with a bloom/fade transition (C3).
  - File: `Assets/_Scripts/UI/Views/QuestTrackView.cs`
- **`QuestItemCard`** `[SHIPPED]` — states `Locked / Unlocked / ReadyToClaim / Claimed`.
  - File: `Assets/_Scripts/UI/Elements/QuestItemCard.cs`

### Generalized Unlock abstraction `[TARGET]`

`Unlock` = one node carrying `{ feature revealed, gating Quest, breadcrumb Target(s) }`.

- `FeatureKind ∈ { GameMode, Vessel, IntensityTier, Screen, Captain, Episode, UIElement }`.
- ► **Engineers add a new feature kind** → `Unlock.FeatureKind`.
- ► **Engineers add a new completion condition** → `QuestTargetType` (+ evaluator in
  `GameModeProgressionService`).

### Labeled edges / events

```
SO_GameModeQuestList        → GameModeProgressionService : loads ordered Quests[]
SO_GameModeQuestData        → SO_GameModeQuestList       : items (asset composition)
SO_ProgressionConfig        → GameModeProgressionService : tunables
VesselUnlockSystem          → Unlock                     : TryPurchaseVessel · spend crystals
GameModeProgressionService  → Unlock                     : resolves the active Unlock
GameModeProgressionService  → GameModeProgressionData    : reads / writes progression
GameModeProgressionData     → UGSDataService.ProgressionRepo : UGS cloud save/load  [C4]
GameModeProgressionService  → QuestTrackView             : OnProgressionChanged  [progression-changed]
QuestTrackView              → QuestItemCard              : renders per-node card

★ KEY NEW WIRE ★  [TARGET]
GameModeProgressionService  ▬ CallToActionSystem :
    active frontier quest → AddCallToAction(CallToActionTargetID + DependencyTargetIDs)
    [CTA-activated]  — closes the gap; breadcrumb is no longer wired to the old stack

CallToActionSystem          → CallToActionTarget         : RegisterCallToActionTarget;
                                                           ActiveIndicator blooms  [CTA-activated · C3]
UserActionSystem            → CallToActionSystem         : OnUserActionCompleted matches
                                                           CompletionUserAction → indicator fades
                                                           [CTA-dismissed · C3] [user-action-completed]
UserActionSystem            → GameModeProgressionService : user-action-completed → evaluate
                                                           quest condition
GameModeProgressionService (loop) : OnQuestCompleted · OnIntensityUnlocked →
                                    re-light next frontier via the KEY NEW WIRE  [intensity-unlocked]
CallToAction (data)         → CallToActionSystem         : AddCallToAction{ CallToActionTargetID,
                                                           CompletionUserAction, DependencyTargetIDs }
```

### THE GAP this design closes

Today the breadcrumb is wired to an **older, mostly-stubbed** stack — `QuestSystem.AddQuest()`
is the only caller of `CallToActionSystem.AddCallToAction()`, fed by `UserJourneySystem`
walking an `SO_QuestChain` of plain `Quest` objects. The new `GameModeProgressionService`
raises **no** CTAs. The KEY NEW WIRE re-points the breadcrumb at the progression service,
and the old stack is retired.

### DEPRECATED — RETIRE `⊘`

- **XP (deleted under C1):** `SO_XPTrackData`, `SO_XPTrackReward`, `XPTrackView`,
  `ParticipationXpAwarder`, and the XP members in `PlayerDataService` (`GetXP`, `AddXP`,
  `UnlockReward`).
- **Old quest stack (replaced by spine + breadcrumb):** `Quest`, `QuestSystem`,
  `UserJourneySystem`, `SO_QuestChain`.

See [`HANDOFF.md`](./HANDOFF.md) § "Current code" for exact file paths and the
retirement checklist.

---

## Layer B — Authorable content graph (designer-facing)

Left-to-right chain of Unlock nodes. Each: unlock name • feature (+ kind) • gating Quest
& completion condition • breadcrumb Target(s) incl. dependency path. The first five nodes
are the **live seed chain** (`GameModeQuestList.asset`).

| # | Unlock | Feature (kind) | Gate (completion condition) | Breadcrumb target |
|---|---|---|---|---|
| 0 | Crystal Capture | Crystal Capture (GameMode, **FREE** / always-unlocked) | reach Intensity 4 `[IntensityUnlocked, 4]` | ArcadeMenu ⇢ Crystal Capture card (`PlayGame*`) |
| 1 | Hex Race | Hex Race (GameMode) | reach Intensity 4 `[IntensityUnlocked, 4]` | ArcadeMenu ⇢ Hex Race card |
| 2 | Joust | Joust (GameMode) | reach Intensity 4 `[IntensityUnlocked, 4]` | ArcadeMenu ⇢ Joust card |
| 3 | Wildlife Blitz | Wildlife Blitz (GameMode) | reach Intensity 4 `[IntensityUnlocked, 4]` | ArcadeMenu ⇢ Wildlife Blitz card |
| 4 | Party Game | Party Game (GameMode, **PLACEHOLDER** `IsPlaceholder=true`) | objective (placeholder) `[Placeholder]` | TBD |
| 5 | Vessel Hangar | Hangar (**Screen / Feature**, NOT a mode) | all prior quests complete `[CompletedQuests ⊇ {0..4}]` | HangarMenu nav button (config: `vesselHangarQuestDisplayName`) |

**Proof nodes (demonstrate the "any feature" generalization `[TARGET]`):**

| # | Unlock | Feature (kind) | Gate | Breadcrumb target |
|---|---|---|---|---|
| + | Manta MkII | Manta MkII (**Vessel**) | spend crystals `[VesselUnlockSystem.TryPurchaseVessel · SO_Vessel.UnlockCost]` | HangarMenu ⇢ HangarShip* card |
| + | Store | Store (**Screen**) | complete prior quest `[WinMatch / prior CompletedQuest]` | StoreMenu nav button |

**Topology:** nodes 0–4 are linear; node 5 converges from all priors (its gate is the set
of all prior `CompletedQuests`). The two proof nodes branch off the Hangar once it is
revealed.

### Completion-condition types (`QuestTargetType`)

`CrystalsCollected`, `RaceTimeUnder`, `JoustsWon`, `ScoreAbove`, `SurvivalTime`,
`WinMatch`, `IntensityUnlocked`, `Placeholder` — plus **spend-currency** (crystals, via
`VesselUnlockSystem`) `[TARGET]`.

---

## Extensibility

- **Designers add a new unlock node** → append `SO_GameModeQuestData` (→ `SO_UnlockData`)
  to `SO_GameModeQuestList` and set `Order`; pick feature, gating Quest
  (`TargetType` + `TargetValue`), and breadcrumb Target(s) + any `DependencyTargetIDs`.
- **Engineers add a new feature kind** → `Unlock.FeatureKind`.
- **Engineers add a new completion condition** → `QuestTargetType` + its evaluator in
  `GameModeProgressionService`.
- **Engineers add a new breadcrumb target** → `CallToActionTargetType` + a
  `CallToActionTarget` MonoBehaviour on the app-shell element.

---

## Open decisions (tracked in `HANDOFF.md` § 6)

1. **SOAP-ify the cross-system wire?** The service currently uses C# `event Action` +
   `*.Instance`. House style prefers SOAP `ScriptableEvent` channels for cross-system
   comms — convert the progression↔breadcrumb wire (and ideally the progression events)?
2. **Captain XP scope.** C1 clearly covers the menu/profile participation XP track.
   Captain leveling (`CaptainProgressCloudData.xp`) is a separate system — **out of scope
   by default** unless decided otherwise.
3. **Chain shape.** Strictly linear, or do we author branching unlocks now?

---

## Conventions (per root `CLAUDE.md`)

SOAP for cross-system comms; single-writer to the progression record; Reflex DI;
fail-loud (no null-guards on SOAP event fields); continuity law everywhere; no
decay/timers as a fix.
